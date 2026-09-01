using UnityEngine;

// The arrow that is on the bow before it is an arrow in the air.
//
// Why this exists
// ---------------
// The body pose in ProceduralAnimator says the string is being pulled - the drawing
// elbow folds further the longer the button is held, the shoulders blade round, the arms
// start to shake near maximum. But the player is watching their character from behind at
// a distance, and at that distance an elbow angle is a small thing to read a shot off.
//
// A shaft that visibly slides backwards is not. It is the same information as the draw
// bar in the HUD, said in the world instead of on the glass, and it is the thing that
// makes a half-drawn shot look half-drawn rather than merely early.
//
// Why it is not part of the bow model
// -----------------------------------
// Bow.fbx is a single joined mesh - limbs, grip, wrap and string all welded together by
// Tools/build_weapons.py before it is exported. Nothing inside it can move relative to
// anything else, so the string genuinely cannot bend and an arrow modelled into it could
// not slide. The arrow has to be a separate object, and once it is separate it may as
// well be the one that already exists: Resources/Models/Arrow, the same shaft Arrow.cs
// fires, so the thing that leaves the bow is the thing that was on it.
//
// Where it sits
// -------------
// On the grip of the bow, which moves with the hand, which moves with the draw pose - so
// this script never has to know anything about arms. It asks where the bow is, lays the
// shaft along where the shot is going, and slides it back by however far the string is
// drawn.
public class NockedArrow : MonoBehaviour
{
    // How far back the nock travels between a slack string and a full draw. Roughly the
    // distance a real draw covers, and the number to change if the shaft looks like it
    // is being pulled through the bow or barely moving at all.
    public float pullMetres = 0.55f;

    // Where the nock rests before anything has been pulled. The string sits a little
    // behind the grip even at rest, so the shaft starts fractionally back.
    public float restOffsetMetres = 0.06f;

    // Fine placement across the bow, measured from the grip. The arrow rests on the
    // knuckle rather than through the middle of the hand, which is what these two are
    // for. Small numbers - they are centimetres, not a pose.
    public float sideOffsetMetres = 0.04f;
    public float heightOffsetMetres = 0.03f;

    private PlayerCombat ownCombat;

    // Asked how far the bow has been raised, so the stave can be stood upright across
    // exactly the same blend the arms come up on.
    private PlayerAnimator ownAnimator;

    // How the bow sits in the hand when nothing has interfered with it, captured the
    // moment the bow is found and before anything below has written to its rotation.
    //
    // Kept because standing the bow up is a blend FROM this, and once this script has
    // written a rotation of its own it can no longer read the hand's answer back off the
    // transform - it would be reading its own last frame.
    private Quaternion bowRestLocalRotation = Quaternion.identity;

    // The bow in the hand, which is where the grip is. Found by the name ValleyBuilder
    // gives it, and looked for again if it is not there yet - the model can be rebuilt
    // underneath us between rounds.
    private Transform bowInTheHand;

    private GameObject shaft;

    // How long the drawn arrow is, nose to nock. The same number Arrow.cs uses, for the
    // same model, so a nocked shaft and a flying one are the same length.
    private const float ShaftLengthMetres = 0.75f;

    void Start()
    {
        ownCombat = GetComponent<PlayerCombat>();
        ownAnimator = GetComponent<PlayerAnimator>();
    }

    // LateUpdate, and after the limbs have been posed, because the grip is wherever the
    // draw pose has just put the hand. Running in Update would lay the arrow on where the
    // bow was last frame.
    //
    // Script execution order is not pinned, so this may still read a one-frame-old grip.
    // That is deliberately tolerated rather than solved with an execution-order attribute:
    // one frame of lag on a decorative shaft is invisible, and pinning an order for it
    // would be a constraint on every future script for no gain.
    void LateUpdate()
    {
        // Both questions asked first, and both cheap. Neither touches the hierarchy, which
        // matters because the search below walks every transform on the player and would
        // otherwise do it once a frame for ever on a model that has no bow to find.
        bool shaftBelongsOnTheString = ShouldBeShowing();
        bool bowIsUp = HowFarTheBowIsRaised() > 0f;

        if (shaftBelongsOnTheString == false && bowIsUp == false)
        {
            HideTheShaft();
            return;
        }

        if (FindTheBow() == false)
        {
            HideTheShaft();
            return;
        }

        // Straightened before the test below, and deliberately. The bow stays up through
        // the recovery after a shot, for longer than the string is held - so a stave that
        // was only straightened while an arrow sat on it would flip back to lying along
        // the forearm on the very frame the arrow left.
        StandTheBowUpAsItIsRaised();

        if (shaftBelongsOnTheString == false)
        {
            HideTheShaft();
            return;
        }

        if (shaft == null)
        {
            MakeTheShaft();
        }

        if (shaft == null)
        {
            return;
        }

        shaft.SetActive(true);
        LayItOnTheBow();
    }

    private bool ShouldBeShowing()
    {
        if (ownCombat == null)
        {
            return false;
        }

        // Only while the string is actually held. The moment it is loosed the arrow is
        // an Arrow, fired by PlayerCombat, and two of them on screen at once would read
        // as the shot having misfired.
        //
        // This covers the fumbles as well without knowing they exist: a draw abandoned
        // for want of stamina clears the same flag a loosed one does, and the shaft goes
        // with it. It covers the other weapons too - IsDrawingABow is false with a sword
        // in hand - so there is nothing here that has to know what a weapon is.
        return ownCombat.IsDrawingABow();
    }

    private bool FindTheBow()
    {
        if (bowInTheHand != null)
        {
            return true;
        }

        // Every part of the model, including the inactive ones - the bow is switched off
        // whenever another weapon is in hand, and it has to be findable then too or it
        // would only ever be found on the frames it is already visible.
        Transform[] everything = GetComponentsInChildren<Transform>(true);

        int index = 0;
        while (index < everything.Length)
        {
            if (everything[index].name == "BowModel")
            {
                bowInTheHand = everything[index];
                bowRestLocalRotation = bowInTheHand.localRotation;
                return true;
            }
            index = index + 1;
        }

        // A player still wearing the old single-mesh model has no bow object to hang
        // this off. Nothing is drawn, and nothing throws.
        return false;
    }

    // The same fallback Arrow.cs keeps, and for the same reason: a missing model would
    // otherwise leave the player drawing on an empty bow, which reads as the arrow having
    // failed to nock rather than as a file that did not import.
    private void MakeTheShaft()
    {
        GameObject modelled = Resources.Load<GameObject>("Models/Arrow");

        if (modelled != null)
        {
            shaft = Object.Instantiate(modelled);
        }
        else
        {
            shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            // The primitive stands along its own Y and is two units tall, so half the
            // wanted length is the right scale.
            shaft.transform.localScale =
                new Vector3(0.05f, ShaftLengthMetres * 0.5f, 0.05f);
        }

        shaft.name = "NockedArrow";

        // Parented to the player so that it is destroyed with them and does not litter
        // the hierarchy. Its position is written in world space every frame regardless,
        // so the parent is for tidiness rather than for the transform.
        shaft.transform.SetParent(transform, true);

        // A collider here would shove the player and every enemy that walked past the
        // drawn bow. The arrow that matters for hitting things is the one PlayerCombat
        // fires; this one is scenery.
        Collider[] strays = shaft.GetComponentsInChildren<Collider>();
        int strayIndex = 0;
        while (strayIndex < strays.Length)
        {
            Object.Destroy(strays[strayIndex]);
            strayIndex = strayIndex + 1;
        }

        PaintIt();
    }

    // The same pale wood the fired arrow is, so the shot does not appear to change colour
    // as it leaves.
    private void PaintIt()
    {
        Material arrowMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        arrowMaterial.SetColor("_BaseColor", new Color(0.86f, 0.78f, 0.55f));

        Renderer[] renderers = shaft.GetComponentsInChildren<Renderer>();
        int index = 0;
        while (index < renderers.Length)
        {
            renderers[index].sharedMaterial = arrowMaterial;
            index = index + 1;
        }
    }

    // Stands the bow upright while it is being aimed, instead of letting it lie along the
    // arm that is holding it.
    //
    // The bow is parented into the hand with a fixed rotation, which is right for a stave
    // carried at rest - it hangs vertically beside the leg - and wrong the moment the arm
    // comes up to aim. The hand is a child of the forearm, the draw pose swings that
    // forearm about 66 degrees forward, and the bow goes with it: by the time it is being
    // sighted along, the limbs are lying nearly flat rather than standing across the shot.
    // Sighting from inside the player's head makes it worse again, because the bow arm is
    // raised further still to bring the bow into view at all.
    //
    // A real archer's wrist keeps the bow upright however the arm is held. There is no
    // wrist joint in these models, so the correction is made here instead.
    //
    // The frame is the same one the arrow is laid in, and the rotation is built the same
    // way every weapon in this project is: LookRotation times a 90 degree tip about X puts
    // the model's own +Y onto the first argument. Here that +Y is the line of the limbs,
    // so it goes onto "up from the shot" and the bow stands across the arrow. The check
    // that this is right is the rest pose: with the arm hanging and the shot horizontal,
    // it works out to the same straight-up the hand already holds the bow in.
    // Nought while the bow is stowed, one while it is fully up, and part way through
    // either. Zero without an animator, which is the honest answer: with nothing raising
    // the bow there is no aiming stance to straighten it for.
    private float HowFarTheBowIsRaised()
    {
        if (ownAnimator == null)
        {
            return 0f;
        }

        return ownAnimator.BowReadyAmount();
    }

    private void StandTheBowUpAsItIsRaised()
    {
        if (ownAnimator == null || ownCombat == null)
        {
            return;
        }
        if (bowInTheHand == null || bowInTheHand.parent == null)
        {
            return;
        }

        // Left entirely alone while the bow is stowed, so a bow in the hand of somebody
        // walking, rolling or drinking behaves exactly as it always did.
        float raised = HowFarTheBowIsRaised();
        if (raised <= 0f)
        {
            return;
        }

        Vector3 alongTheShot = ownCombat.AimDirection();
        if (alongTheShot.sqrMagnitude < 0.0001f)
        {
            return;
        }
        alongTheShot = alongTheShot.normalized;

        Vector3 across = Vector3.Cross(Vector3.up, alongTheShot);
        if (across.sqrMagnitude < 0.0001f)
        {
            // Aiming straight up or straight down. There is no "across" to stand the bow
            // on, and one frame of the old orientation is better than a flip.
            return;
        }
        across = across.normalized;

        Vector3 upFromTheShot = Vector3.Cross(alongTheShot, across);

        Quaternion asTheHandHoldsIt = bowInTheHand.parent.rotation * bowRestLocalRotation;
        Quaternion standingAcrossTheShot =
            Quaternion.LookRotation(upFromTheShot, alongTheShot) * Quaternion.Euler(90f, 0f, 0f);

        bowInTheHand.rotation =
            Quaternion.Slerp(asTheHandHoldsIt, standingAcrossTheShot, Mathf.Clamp01(raised));
    }

    private void LayItOnTheBow()
    {
        // Where the shot is going. The body is already turned to face it while the string
        // is back, so this is very nearly the player's own forward - but only very nearly,
        // because the elevation is not in the body at all, and an arrow aimed up at a
        // Spitter on a ledge should visibly point up at it.
        Vector3 alongTheShot = ownCombat.AimDirection();

        if (alongTheShot.sqrMagnitude < 0.0001f)
        {
            alongTheShot = transform.forward;
        }
        alongTheShot = alongTheShot.normalized;

        // A frame to place the shaft in: along the shot, across it, and up from it. Built
        // from the shot rather than from the player so that the arrow stays put on the
        // bow when the aim swings up or down.
        Vector3 across = Vector3.Cross(Vector3.up, alongTheShot);

        // Aiming straight up or straight down leaves no sideways direction to speak of.
        // The offsets are centimetres and the shot is nearly vertical, so dropping them
        // is both harmless and the only thing that can be done.
        if (across.sqrMagnitude < 0.0001f)
        {
            across = Vector3.zero;
        }
        else
        {
            across = across.normalized;
        }

        Vector3 upFromTheShot = Vector3.Cross(alongTheShot, across);

        Vector3 grip = bowInTheHand.position;

        // How far back the string is. This is the whole point of the script.
        float drawnBack = restOffsetMetres + ownCombat.DrawFraction() * pullMetres;

        Vector3 nock = grip
            + across * sideOffsetMetres
            + upFromTheShot * heightOffsetMetres
            - alongTheShot * drawnBack;

        shaft.transform.position = nock;

        // The model stands along its own +Y with the nock on the origin - the same
        // convention every weapon in this project is built to - so laying that +Y onto
        // the direction of the shot puts the nock on the string and the point out front.
        shaft.transform.rotation =
            Quaternion.LookRotation(alongTheShot, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
    }

    private void HideTheShaft()
    {
        if (shaft != null && shaft.activeSelf == true)
        {
            shaft.SetActive(false);
        }
    }
}
