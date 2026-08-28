using UnityEngine;

// Third-person camera that orbits the player on the mouse.
// Kept deliberately simple: no smoothing springs, no cinematics. It only has to be
// comfortable enough that someone can play for ten minutes without fighting it.
public class OrbitCamera : MonoBehaviour
{
    public Transform targetToFollow;

    public float distanceBehindTarget = 7f;
    public float heightAboveTarget = 2.2f;
    public float mouseSensitivity = 3f;

    // Pitch is clamped so the player can never flip the camera over the top.
    public float lowestPitchDegrees = -20f;
    public float highestPitchDegrees = 65f;

    private float currentYawDegrees = 0f;
    private float currentPitchDegrees = 20f;

    // No Start any more. This used to lock and hide the mouse the moment the camera woke
    // up, which quietly fought with the title screen: whichever of the two ran second
    // won, and when it was the camera the game opened with no pointer on the menu at all.
    // CursorControl owns the mouse now and works it out from the state of the game every
    // frame instead.

    // LateUpdate rather than Update, so the camera moves AFTER the player has finished
    // moving this frame. Doing it in Update causes a visible jitter.
    void LateUpdate()
    {
        if (targetToFollow == null)
        {
            return;
        }

        // The mouse is released during a conversation so the player can see it, which
        // means every twitch of it would otherwise spin the camera while they read.
        if (PlayerControl.IsBlocked() == false)
        {
            currentYawDegrees = currentYawDegrees + GameInput.MouseMovedSideways() * mouseSensitivity;
            currentPitchDegrees = currentPitchDegrees - GameInput.MouseMovedVertically() * mouseSensitivity;
        }

        if (currentPitchDegrees < lowestPitchDegrees)
        {
            currentPitchDegrees = lowestPitchDegrees;
        }
        if (currentPitchDegrees > highestPitchDegrees)
        {
            currentPitchDegrees = highestPitchDegrees;
        }

        Quaternion orbitRotation = Quaternion.Euler(currentPitchDegrees, currentYawDegrees, 0f);
        Vector3 pointToLookAt = targetToFollow.position + Vector3.up * heightAboveTarget;
        Vector3 desiredCameraPosition = pointToLookAt + orbitRotation * (Vector3.back * distanceBehindTarget);

        // If a cliff sits between the player and where the camera wants to be, pull the
        // camera in to just in front of it. Without this the view ends up inside rock.
        Vector3 directionOutToCamera = desiredCameraPosition - pointToLookAt;
        RaycastHit whatWasHit;
        bool somethingIsInTheWay = Physics.Raycast(
            pointToLookAt,
            directionOutToCamera.normalized,
            out whatWasHit,
            directionOutToCamera.magnitude,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (somethingIsInTheWay == true)
        {
            desiredCameraPosition = whatWasHit.point - directionOutToCamera.normalized * 0.3f;
        }

        transform.position = desiredCameraPosition;
        transform.rotation = Quaternion.LookRotation(pointToLookAt - transform.position);
    }

    // The player movement script asks for this so that pressing W means away from the
    // camera rather than a fixed compass direction.
    public float CurrentYawDegrees()
    {
        return currentYawDegrees;
    }
}
