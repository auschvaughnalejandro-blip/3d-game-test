using UnityEngine;

// The camera the player sees the game through, in either of its two views: orbiting
// behind them over the shoulder, or sitting in their head. V swaps between the two.
//
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

    [Header("First person")]
    // Both views share the same yaw and pitch, so the toggle never spins the world round:
    // whatever was in the middle of the screen is still in the middle of it afterwards.
    public bool startInFirstPerson = false;

    // How far above the player's own transform the eyes sit.
    //
    // Measured off the model rather than guessed. Tools/build_remaining_characters.py
    // puts the player's shoulders 1.46 m off the floor and stands a head 0.26 m tall on
    // top of them, so the crown is at about 1.75 m and the eyes are roughly 0.11 m under
    // that, at 1.64 m. The controller is centred on a body 1.77 m tall, which puts the
    // feet 0.885 m below the transform - so 1.64 - 0.885 is where the eyes come out.
    public float firstPersonEyeHeight = 0.75f;

    // Pushed forward out of the middle of the skull, which is a shade wider than 0.1 m at
    // the cheek. Enough that looking down finds the chest rather than the inside of the
    // neck, and short enough that the view never leaves the body.
    public float firstPersonForwardNudge = 0.18f;

    // Nearly straight up and nearly straight down. The third-person limits exist to stop
    // the camera swinging over the player's head and under their feet, and there is no
    // camera out there to swing in this view - so the only reason left to clamp at all is
    // to keep the horizon the right way up.
    public float firstPersonLowestPitchDegrees = -85f;
    public float firstPersonHighestPitchDegrees = 85f;

    // How close to the lens something can be and still be drawn, while the camera is in
    // the player's head.
    //
    // The default 0.3 m is a third-person number and it is too far out here. The bow hand
    // at full stretch sits about 0.45 m from the eye, so at 0.3 the hand clears the near
    // plane by 15 cm and a tremble or a lean is enough to slice it in half - and the
    // nocked arrow, which travels 0.61 m backwards as the string comes back, spends the
    // last of its draw disappearing into the plane rather than into the archer's cheek.
    public float firstPersonNearClipPlane = 0.08f;

    // Whatever the near plane was before first person borrowed it, put back on the way
    // out. Read from the camera rather than assumed, so a scene that ships a different
    // value keeps it.
    private float nearClipInThirdPerson = 0.3f;
    private Camera ownCamera = null;

    private float currentYawDegrees = 0f;
    private float currentPitchDegrees = 20f;

    private bool lookingOutOfTheirEyes = false;

    // The player's own head, hidden while the camera is inside it. Found the first time it
    // is wanted rather than in Start, because the camera can exist before the model has
    // been hung underneath the player.
    private Renderer[] headRenderers = null;
    private bool haveLookedForTheHead = false;

    // Still no cursor work in here. This used to lock and hide the mouse the moment the
    // camera woke up, which quietly fought with the title screen: whichever of the two ran
    // second won, and when it was the camera the game opened with no pointer on the menu
    // at all. CursorControl owns the mouse now and works it out from the state of the game
    // every frame instead.
    void Start()
    {
        ownCamera = GetComponent<Camera>();
        if (ownCamera != null)
        {
            nearClipInThirdPerson = ownCamera.nearClipPlane;
        }

        if (startInFirstPerson == true)
        {
            SwitchToFirstPerson();
        }
    }

    // Read by PlayerMovement, which turns the body to face wherever the view points while
    // this is true. The arms and the weapon hang off a body standing exactly where the
    // camera is, so a body still facing the way it last walked puts the sword out of the
    // side of the screen.
    public bool IsFirstPerson()
    {
        return lookingOutOfTheirEyes;
    }

    // Forced back over the shoulder. The ending sequence asks for this before it pulls the
    // camera back and up: that shot is of the player walking away up the road, and there
    // is nothing to see out of their own eyes.
    public void ReturnToThirdPerson()
    {
        if (lookingOutOfTheirEyes == true)
        {
            SwitchToThirdPerson();
        }
    }

    // LateUpdate rather than Update, so the camera moves AFTER the player has finished
    // moving this frame. Doing it in Update causes a visible jitter.
    void LateUpdate()
    {
        if (targetToFollow == null)
        {
            return;
        }

        // The mouse is released during a conversation so the player can see it, which
        // means every twitch of it would otherwise spin the camera while they read. The
        // view toggle sits behind the same question for the same reason: V is a letter,
        // and a letter typed at a menu should not move the camera behind it.
        if (PlayerControl.IsBlocked() == false)
        {
            currentYawDegrees = currentYawDegrees + GameInput.MouseMovedSideways() * mouseSensitivity;
            currentPitchDegrees = currentPitchDegrees - GameInput.MouseMovedVertically() * mouseSensitivity;

            if (GameInput.ViewToggleWasPressed() == true)
            {
                ToggleTheView();
            }
        }

        ClampThePitch();

        if (lookingOutOfTheirEyes == true)
        {
            PlaceTheCameraInTheirHead();
        }
        else
        {
            PlaceTheCameraBehindThem();
        }
    }

    // Which limits apply depends on which view is up, because the two want very different
    // things from the pitch. Done every frame rather than only on the toggle, so that a
    // pitch of 80 degrees picked up in first person cannot be left illegally steep the
    // moment the camera goes back over the shoulder.
    private void ClampThePitch()
    {
        float lowest = lowestPitchDegrees;
        float highest = highestPitchDegrees;

        if (lookingOutOfTheirEyes == true)
        {
            lowest = firstPersonLowestPitchDegrees;
            highest = firstPersonHighestPitchDegrees;
        }

        if (currentPitchDegrees < lowest)
        {
            currentPitchDegrees = lowest;
        }
        if (currentPitchDegrees > highest)
        {
            currentPitchDegrees = highest;
        }
    }

    private void PlaceTheCameraBehindThem()
    {
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

    // No orbit, no distance, and deliberately no wall check. There is no gap between the
    // camera and the player for a cliff to get into, so the raycast above would only ever
    // find the ground under their feet and shove the view down into it.
    //
    // The eye height is measured off the player's transform rather than off the head bone
    // of the model. The head bone breathes, bobs with every stride and dips when the bow
    // comes up; all three are pleasant to watch from behind and unbearable when the camera
    // is riding on them.
    private void PlaceTheCameraInTheirHead()
    {
        Quaternion viewRotation = Quaternion.Euler(currentPitchDegrees, currentYawDegrees, 0f);
        Vector3 eyePosition = targetToFollow.position + Vector3.up * firstPersonEyeHeight;

        // Nudged along the flat facing rather than along the view, so that looking down
        // does not walk the camera forwards into the player's own chest.
        Vector3 flatFacing = Quaternion.Euler(0f, currentYawDegrees, 0f) * Vector3.forward;
        eyePosition = eyePosition + flatFacing * firstPersonForwardNudge;

        transform.position = eyePosition;
        transform.rotation = viewRotation;
    }

    private void ToggleTheView()
    {
        if (lookingOutOfTheirEyes == true)
        {
            SwitchToThirdPerson();
        }
        else
        {
            SwitchToFirstPerson();
        }
    }

    private void SwitchToFirstPerson()
    {
        lookingOutOfTheirEyes = true;
        ShowTheHead(false);

        if (ownCamera != null)
        {
            ownCamera.nearClipPlane = firstPersonNearClipPlane;
        }
    }

    private void SwitchToThirdPerson()
    {
        lookingOutOfTheirEyes = false;
        ShowTheHead(true);

        if (ownCamera != null)
        {
            ownCamera.nearClipPlane = nearClipInThirdPerson;
        }
    }

    // The head is switched off while the camera is inside it and back on when it leaves.
    //
    // Only the head. The torso, the arms and whichever weapon is in hand all stay, so the
    // player can still watch their own sword swing and their own bow come up. That is
    // worth keeping: this demo is nothing but the fighting, and a first-person view with
    // no hands in it would show off less of the animation work than the third-person one
    // rather than more.
    //
    // It is the Renderer that is switched off and never the GameObject, because
    // ProceduralAnimator poses the head's Transform every frame and found it by name once
    // at Start. A disabled Renderer is still posed and still parents whatever hangs off
    // it; a disabled GameObject would take its children with it and stop the animator
    // finding anything below the neck.
    private void ShowTheHead(bool visible)
    {
        FindTheHeadIfNotAlreadyFound();

        if (headRenderers == null)
        {
            return;
        }

        int index = 0;
        while (index < headRenderers.Length)
        {
            if (headRenderers[index] != null)
            {
                headRenderers[index].enabled = visible;
            }
            index = index + 1;
        }
    }

    private void FindTheHeadIfNotAlreadyFound()
    {
        if (haveLookedForTheHead == true)
        {
            return;
        }
        if (targetToFollow == null)
        {
            return;
        }

        Transform[] everything = targetToFollow.GetComponentsInChildren<Transform>(true);

        int index = 0;
        while (index < everything.Length)
        {
            if (everything[index].name == "Head")
            {
                // Everything drawn as part of the head, which on the player means the
                // hair as well - it is built parented to the head, and a ponytail left
                // behind would hang in the middle of the lens. Nothing below the neck is
                // caught: the shoulder guard and every other part are siblings of the
                // head rather than children of it.
                headRenderers = everything[index].GetComponentsInChildren<Renderer>(true);
                haveLookedForTheHead = true;
                return;
            }
            index = index + 1;
        }

        // Nothing called "Head" anywhere under the player. That is the old single-lump
        // model rather than a segmented one, and it has no head that can be hidden apart
        // from the rest of the body - so first person still works, it just has the inside
        // of a face in it. Marked as looked-for either way, so this walk of the hierarchy
        // does not happen again on every toggle.
        haveLookedForTheHead = true;
    }

    // The player movement script asks for this so that pressing W means away from the
    // camera rather than a fixed compass direction.
    public float CurrentYawDegrees()
    {
        return currentYawDegrees;
    }
}
