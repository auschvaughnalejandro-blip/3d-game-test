using UnityEngine;

// Animates a segmented character entirely from code, with no bones, no skinning, no
// Animator controller and no .anim files.
//
// Why this exists
// ---------------
// The original character meshes were single rigid lumps of geometry. Nothing inside them
// could move relative to anything else, so the only motion available was tilting the
// whole creature - which is what EnemyBrain does, and what reads on screen as "they just
// lean over". A T-posed player is the same problem: the mesh was modelled in a T-pose and
// there was nothing in the file capable of changing that.
//
// The fix is not a rig. It is a tree of separate body parts, each a plain child Transform
// with its origin sitting on the joint it turns around, exported from Blender by
// Tools/build_*.py. Rotating those Transforms in LateUpdate is animation. This is how
// games did it before skinned meshes existed and it still works.
//
// How it layers onto what is already here
// ---------------------------------------
// EnemyBrain and PlayerMovement own the model root. This component owns that root's
// CHILDREN and never writes to its own transform. That is the whole reason the two
// compose without fighting.
//
// One pose, written once
// ----------------------
// The walk, the idle, a swing, a dodge and a hit reaction all want to move some of the
// same parts, and on any frame where the character is changing what it is doing, several
// of them are partly switched on at once. So none of them touch a Transform. Each writes
// into the pose accumulators below, later layers blend over earlier ones by however much
// they are faded in, and ApplyThePose does every actual rotation in one place at the end
// of the frame.
//
// The alternative - each layer writing to the Transform in turn, and reading back what
// the last one left - is what the idle breathing used to do, and it was not a pose at all
// but a running total that re-added itself every frame.
//
// Which way is which
// ------------------
// Every part is a child of Hips, and Hips carries the -90 degree rotation that stands the
// creature up out of Blender's Z-up space. So a part's OWN axes are still Blender's:
//
//     local X  ->  the creature's left-right axis   ->  PITCH (forward and back)
//     local Y  ->  the creature's forward axis      ->  ROLL
//     local Z  ->  the creature's up axis           ->  YAW
//
// which is why SetAngles passes them to Quaternion.Euler in the order (pitch, roll, yaw).
// It looks wrong and it is right. Whether "forward" is positive or negative survives the
// FBX export inconsistently, so that is resolved in exactly one place - Forward() - and
// every animation below is written in the spec's own language of forward and back.
public class ProceduralAnimator : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Tuning
    // ------------------------------------------------------------------

    [Header("Stride")]
    // How many complete strides the creature takes per metre travelled. This is the
    // single number that decides whether the feet look planted or look like they are
    // sliding on ice. Bigger means shorter, quicker steps.
    public float stridesPerMetre = 0.55f;

    // Below this speed the creature is treated as standing still and breathes instead of
    // walking. Above it the walk fades in.
    public float walkStartsAboveSpeed = 0.25f;
    public float walkFullyInAtSpeed = 1.4f;

    [Header("Walk shape")]
    public float thighSwingDegrees = 26f;
    public float kneeBendDegrees = 34f;
    public float armSwingDegrees = 20f;
    public float elbowRestDegrees = 15f;
    public float torsoLeanDegrees = 7f;
    public float hipBobMetres = 0.035f;

    [Header("Idle")]
    public float breathsPerSecond = 0.35f;
    public float breathBobMetres = 0.012f;

    [Header("Sprint")]
    // Sprinting is not just walking faster. Speed already lengthens the stride on its
    // own, so what makes a sprint read as a sprint is the shape: pitched forward, arms
    // driving harder, elbows tucked in.
    public float sprintTorsoLeanDegrees = 12f;
    public float sprintArmSwingMultiplier = 1.6f;
    public float sprintHipBobMultiplier = 2.0f;
    public float sprintElbowDegrees = 35f;

    [Header("Jump")]
    public float jumpLaunchThighDegrees = 18f;
    public float jumpLaunchArmDegrees = 40f;
    public float jumpTuckThighDegrees = 25f;
    public float jumpTuckShinDegrees = 45f;
    public float jumpAirborneArmDegrees = 20f;
    // The landing absorb is the beat that gives a jump weight. Skipping it is what makes
    // a character look like it is being teleported onto the floor.
    public float landingCrouchMetres = 0.12f;
    public float landingDropSeconds = 0.12f;
    public float landingRecoverySeconds = 0.20f;

    [Header("Dodge")]
    // dodgeSpeed is 16 against a walking speed of 5.5 - nearly three times - so this has
    // to read as a committed throw of the whole body rather than a fast walk. The roll is
    // about the FORWARD axis; a dodge that only pitches looks like a stumble.
    public float dodgeRollDegrees = 22f;
    public float dodgeHipDropMetres = 0.06f;
    public float dodgeTuckThighDegrees = 30f;
    public float dodgeTuckShinDegrees = 50f;
    public float dodgeArmsInDegrees = 40f;

    [Header("Player swing")]
    // The shoulders yawing is what carries a sword swing. Rotating Shoulders turns both
    // arms together, which is exactly what a torso-driven swing does.
    public float swingShouldersCockDegrees = 18f;
    public float swingShouldersThroughDegrees = -35f;
    public float swingArmDegrees = 90f;
    public float swingForearmCockedDegrees = 60f;
    public float swingForearmExtendedDegrees = 10f;

    public float heavyArmRaiseDegrees = 120f;
    public float heavyArmThroughDegrees = 130f;
    public float heavyShouldersYawDegrees = -30f;
    public float heavyLeanBackDegrees = 12f;
    public float heavyFoldForwardDegrees = 25f;
    public float heavyHipDropMetres = 0.10f;

    [Header("Surge")]
    // A HELD extreme reads as power. Animating through it reads as a wobble, which is
    // why this pose is struck and then kept for the whole surge.
    public float surgeArmsBackDegrees = 45f;
    public float surgeTorsoArchDegrees = 15f;
    public float surgeHipRiseMetres = 0.05f;

    [Header("Potion and swap")]
    public float drinkForearmDegrees = 105f;
    public float drinkHeadTiltDegrees = 15f;
    public float swapForearmDegrees = 45f;

    [Header("Hit reaction")]
    public float hitTorsoDegrees = 12f;
    public float hitHeadDegrees = 15f;
    public float hitArmsDegrees = 10f;
    public float hitHipShiftMetres = 0.05f;

    [Header("Enemy attack")]
    // How far the swinging arm hauls the weapon up and back over the shoulder. This is
    // the number that decides whether the wind-up reads as loading a blow or as a shrug.
    public float attackRaiseDegrees = 118f;
    public float attackElbowFoldDegrees = 52f;

    // Where the arm ends up once it has driven through the blow: past vertical, out in
    // front and low. A swing that stops where it started has no follow-through.
    public float attackFollowThroughDegrees = -52f;

    // The shoulder that is not carrying the weapon swings the opposite way. A body that
    // swings one arm and leaves the other hanging reads as a puppet.
    public float attackOffArmDegrees = 28f;

    // The torso arches back under the raised weapon and folds forward through the blow.
    // These are ON TOP of the arch EnemyBrain already puts into the model root, and are
    // deliberately smaller than it - this layer is the spine bending, not the whole
    // creature leaning.
    public float attackArchDegrees = 9f;
    public float attackFoldDegrees = 20f;

    // How long the arm takes to fall back to the walk once the blow is over.
    public float attackReleaseSeconds = 0.28f;

    // Which arm carries the weapon.
    //
    // Beware the names. Blender and Unity mirror each other, so the parts exported as "L"
    // arrive on the creature's RIGHT in Unity: ThighL and UpperArmL both sit at POSITIVE
    // local X once imported, and so does the WeaponPivot ValleyBuilder hangs the club on,
    // at x = +1.02. So the club is in the hand of the arm named "L", and this defaults to
    // true to match. If a creature is built holding its weapon on the other side, flip
    // this rather than renaming anything.
    public bool weaponIsInTheArmNamedLeft = true;

    [Header("Tail")]
    // The Spitter and the Darter both carry a three-segment tail. It is never posed
    // directly - it trails whatever the body just did, each segment lagging the one in
    // front of it, which is what makes it read as attached rather than animated.
    // Per degree per SECOND of turn, not per degree per frame. Measuring it per frame
    // made the tail swing twice as far at thirty frames a second as at sixty, which is
    // the kind of bug that only shows up on someone else's machine.
    public float tailSwingPerDegreeTurned = 0.06f;
    public float tailMaximumSwingDegrees = 34f;
    public float tailCatchUpSpeed = 7f;
    public float tailIdleSwayDegrees = 5f;
    public float tailDroopDegrees = 8f;

    [Header("Axis")]
    // Blender and Unity disagree about handedness, and the FBX axis conversion can leave
    // the pitch sign flipped. If the creature walks with its legs swinging backwards, set
    // this to 1 instead of -1. Exposed rather than hard-coded because it is far quicker to
    // flip in the inspector than to re-export the mesh.
    public float forwardSwingSign = -1f;

    // ------------------------------------------------------------------
    // The parts
    // ------------------------------------------------------------------

    private Transform hips;
    private Transform torso;
    private Transform head;
    private Transform shoulders;
    private Transform thighLeft;
    private Transform thighRight;
    private Transform shinLeft;
    private Transform shinRight;
    private Transform upperArmLeft;
    private Transform upperArmRight;
    private Transform forearmLeft;
    private Transform forearmRight;
    private Transform tailOne;
    private Transform tailTwo;
    private Transform tailThree;

    // Every pose is applied on top of the rest pose the mesh was built in, so the
    // animation never accumulates drift and a missing part simply does nothing.
    private Vector3 hipsRestPosition;
    private Quaternion hipsRestRotation;
    private bool foundTheParts;

    // ------------------------------------------------------------------
    // The pose being built this frame
    // ------------------------------------------------------------------

    private Vector3 hipsOffset;
    private float hipsYaw;
    private float hipsRoll;
    private float torsoPitch;
    private float torsoYaw;
    private float torsoRoll;
    private float headPitch;
    private float headYaw;
    private float shouldersPitch;
    private float shouldersYaw;
    private float shouldersRoll;
    private float thighLeftPitch;
    private float thighRightPitch;
    private float shinLeftPitch;
    private float shinRightPitch;
    private float upperArmLeftPitch;
    private float upperArmRightPitch;
    private float forearmLeftPitch;
    private float forearmRightPitch;

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------

    private float stridePhase;
    private Vector3 positionLastFrame;
    private float smoothedSpeed;
    private bool isDead;
    private float deadForSeconds;

    // Set by EnemyBrain every frame it is winding up or striking, and left alone the rest
    // of the time. Stored rather than acted on immediately because the brain runs in
    // Update and this component poses in LateUpdate; keeping the two apart means neither
    // has to care which order they run in. Every player driver below works the same way.
    private float windUpProgress;
    private float strikeProgress;
    private bool isAttacking;
    private float attackFade;

    // Player actions. A progress of -1 means "not happening"; 0 to 1 is how far through.
    private float sprintAmount;
    private bool isAirborne;
    private float verticalSpeed;
    private float landingSecondsElapsed = -1f;
    private float dodgeProgress = -1f;
    private float dodgeSideSign;
    private float swingProgress = -1f;
    private bool swingIsHeavy;
    private float surgeAmount;
    private float drinkProgress = -1f;
    private float swapProgress = -1f;
    private float hitProgress = -1f;
    private float hitSideSign;

    // Tail, carried between frames because the whole point of it is that it lags.
    private float tailYawOne;
    private float tailYawTwo;
    private float tailYawThree;
    private float facingLastFrame;

    private void Start()
    {
        FindTheParts();
        positionLastFrame = transform.position;
        facingLastFrame = transform.eulerAngles.y;
    }

    // Walks the model hierarchy once and remembers every part by name. Names come from
    // the Blender build scripts, so the two have to agree; if a part is missing it stays
    // null and every use of it is guarded, which means a partially-built character
    // animates as far as it can rather than throwing once per frame.
    private void FindTheParts()
    {
        Transform[] everything = GetComponentsInChildren<Transform>();

        int index = 0;
        while (index < everything.Length)
        {
            Transform part = everything[index];
            string name = part.name;

            if (name == "Hips") { hips = part; }
            else if (name == "Torso") { torso = part; }
            else if (name == "Head") { head = part; }
            else if (name == "Shoulders") { shoulders = part; }
            else if (name == "ThighL") { thighLeft = part; }
            else if (name == "ThighR") { thighRight = part; }
            else if (name == "ShinL") { shinLeft = part; }
            else if (name == "ShinR") { shinRight = part; }
            else if (name == "UpperArmL") { upperArmLeft = part; }
            else if (name == "UpperArmR") { upperArmRight = part; }
            else if (name == "ForearmL") { forearmLeft = part; }
            else if (name == "ForearmR") { forearmRight = part; }
            else if (name == "Tail1") { tailOne = part; }
            else if (name == "Tail2") { tailTwo = part; }
            else if (name == "Tail3") { tailThree = part; }

            index = index + 1;
        }

        // The hips are normally the model's own root rather than a child of it. Blender
        // exports these creatures with a single root object called Hips, and Unity's
        // importer promotes a lone root to BE the asset root and renames it after the
        // file - so nothing called "Hips" survives the import at all.
        //
        // ValleyBuilder.AttachSegmentedModel is what puts that right: it hangs the
        // imported body inside an empty wrapper, names it back to "Hips", and attaches
        // this component to the wrapper. That is what makes the search above find the
        // hips, and it is also what keeps this component off the transform EnemyBrain
        // and PlayerMovement own.
        if (hips != null)
        {
            hipsRestPosition = hips.localPosition;
            hipsRestRotation = hips.localRotation;
        }

        foundTheParts = thighLeft != null && thighRight != null;

        if (!foundTheParts)
        {
            Debug.LogWarning("ProceduralAnimator on " + gameObject.name +
                " found no leg parts. This model is probably one of the old single-mesh "
                + "FBX files rather than a segmented one, so it cannot be animated.");
        }
    }

    private void LateUpdate()
    {
        if (!foundTheParts)
        {
            return;
        }

        ClearThePose();

        if (isDead)
        {
            PoseTheDeath();
            FollowWithTheTail(0f);
            ApplyThePose();
            return;
        }

        MeasureHowFastWeAreActuallyMoving();

        // How much of the walk to show. Below the threshold this is 0 and the creature
        // breathes; in between the walk fades up, which stops a creature that is barely
        // drifting from flailing its legs.
        float walkAmount = Mathf.InverseLerp(walkStartsAboveSpeed, walkFullyInAtSpeed, smoothedSpeed);

        AdvanceTheStride();
        PoseTheWalk(walkAmount);
        PoseTheIdle(1f - walkAmount);

        // Everything below layers over the walk, in the order things override each other
        // in practice: being in the air beats sprinting, a dodge beats being in the air,
        // and a hit beats all of it.
        PoseTheJump();
        PoseTheSurge();
        PoseTheDrink();
        PoseTheWeaponSwap();
        PoseThePlayerSwing();

        FadeTheAttack();
        PoseTheAttack(attackFade);

        PoseTheCharge();
        PoseTheSummon();
        PoseTheSlamImpact();

        PoseTheDodge();
        PoseTheHitReaction();

        FollowWithTheTail(walkAmount);
        ApplyThePose();
    }

    private void ClearThePose()
    {
        hipsOffset = Vector3.zero;
        hipsYaw = 0f;
        hipsRoll = 0f;
        torsoPitch = 0f;
        torsoYaw = 0f;
        torsoRoll = 0f;
        headPitch = 0f;
        headYaw = 0f;
        shouldersPitch = 0f;
        shouldersYaw = 0f;
        shouldersRoll = 0f;
        thighLeftPitch = 0f;
        thighRightPitch = 0f;
        shinLeftPitch = 0f;
        shinRightPitch = 0f;
        upperArmLeftPitch = 0f;
        upperArmRightPitch = 0f;
        forearmLeftPitch = 0f;
        forearmRightPitch = 0f;
    }

    // Positive means the part swings FORWARD, whichever direction the FBX export decided
    // that was. Every animation is written in terms of forward and back and passes
    // through here, so a wrong export is one field to flip rather than a hunt through
    // fifty signs.
    private float Forward(float degrees)
    {
        return degrees * -forwardSwingSign;
    }

    // Speed is measured from how far the transform actually moved, not asked of the
    // controller or the agent. That way knockback, lunges and being shoved all drive the
    // legs correctly, and there is nothing to keep in sync.
    private void MeasureHowFastWeAreActuallyMoving()
    {
        Vector3 movedThisFrame = transform.position - positionLastFrame;
        positionLastFrame = transform.position;

        // Vertical movement is gravity and floor-lifting, not walking, so it is ignored.
        movedThisFrame.y = 0f;

        float speedThisFrame = 0f;
        if (Time.deltaTime > 0f)
        {
            speedThisFrame = movedThisFrame.magnitude / Time.deltaTime;
        }

        // A little smoothing, or a single frame of stutter makes the legs snap.
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speedThisFrame, Time.deltaTime * 10f);
    }

    // The phase advances with distance travelled rather than with time. This is the whole
    // trick behind feet that stay planted: walk twice as fast and you take twice as many
    // steps in the same distance, not longer ones.
    private void AdvanceTheStride()
    {
        // A charge covers ground far too fast for the ordinary stride rate, so it is
        // slowed deliberately. See chargeStrideMultiplier.
        float strideRate = stridesPerMetre
            * Mathf.Lerp(1f, chargeStrideMultiplier, chargeAmount);

        float stridesThisFrame = smoothedSpeed * Time.deltaTime * strideRate;
        stridePhase = stridePhase + stridesThisFrame * 2f * Mathf.PI;

        if (stridePhase > 2f * Mathf.PI)
        {
            stridePhase = stridePhase - 2f * Mathf.PI;
        }
    }

    private void PoseTheWalk(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        float phase = stridePhase;
        float swing = thighSwingDegrees * amount;

        // Sprinting drives the arms harder and bounces the body more. Both are blends
        // rather than switches, so crossing the sprint threshold does not pop.
        float armMultiplier = Mathf.Lerp(1f, sprintArmSwingMultiplier, sprintAmount);
        float bobMultiplier = Mathf.Lerp(1f, sprintHipBobMultiplier, sprintAmount);
        float elbow = Mathf.Lerp(elbowRestDegrees, sprintElbowDegrees, sprintAmount);

        float arms = armSwingDegrees * amount * armMultiplier;

        // Legs, half a cycle apart.
        thighLeftPitch = Mathf.Sin(phase) * swing * forwardSwingSign;
        thighRightPitch = Mathf.Sin(phase + Mathf.PI) * swing * forwardSwingSign;

        // The knee only ever folds one way, and only on the back half of the swing. A
        // knee driven by a plain sine bends backwards for half of every step, which is
        // the most common way a hand-written walk cycle ends up looking broken.
        float kneeLeft = Mathf.Max(0f, Mathf.Sin(phase + 0.9f * Mathf.PI));
        float kneeRight = Mathf.Max(0f, Mathf.Sin(phase + 1.9f * Mathf.PI));
        shinLeftPitch = kneeLeft * kneeBendDegrees * amount * -forwardSwingSign;
        shinRightPitch = kneeRight * kneeBendDegrees * amount * -forwardSwingSign;

        // Arms swing opposite the leg on the same side. This is what makes a walk look
        // like a walk rather than like a shuffle.
        upperArmLeftPitch = Mathf.Sin(phase + Mathf.PI) * arms * forwardSwingSign;
        upperArmRightPitch = Mathf.Sin(phase) * arms * forwardSwingSign;
        forearmLeftPitch = elbow * -forwardSwingSign;
        forearmRightPitch = elbow * -forwardSwingSign;

        // A forward hunch that deepens as the creature picks up speed, and deepens again
        // into the sprint.
        torsoPitch = torsoLeanDegrees * amount * -forwardSwingSign
            + Forward(sprintTorsoLeanDegrees) * sprintAmount;

        // The body rises and falls twice per stride - once for each footfall. Getting
        // this frequency wrong is what makes a walk look like a bounce.
        hipsOffset.y = hipsOffset.y
            + Mathf.Cos(phase * 2f) * hipBobMetres * amount * bobMultiplier;
    }

    private void PoseTheIdle(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        // Standing still is not the same as being frozen. A slow shallow breath is enough
        // to stop a waiting creature reading as a statue, which is most of what the old
        // models looked like.
        float breath = Mathf.Sin(Time.time * breathsPerSecond * 2f * Mathf.PI);

        hipsOffset.y = hipsOffset.y + breath * breathBobMetres * amount;
        headPitch = headPitch + breath * 1.5f * amount;
    }

    // ------------------------------------------------------------------
    // Player drivers
    //
    // PlayerAnimator reads the player's scripts and calls these once a frame. They only
    // record; the posing all happens in LateUpdate, so neither side has to care which
    // order the two components update in.
    // ------------------------------------------------------------------

    public void ShowSprint(float amount)
    {
        sprintAmount = Mathf.Clamp01(amount);
    }

    public void ShowAirborne(bool airborne, float howFastRisingOrFalling)
    {
        isAirborne = airborne;
        verticalSpeed = howFastRisingOrFalling;
    }

    // One shot, on the frame the feet touch down. The absorb runs itself from there.
    public void ShowLanding()
    {
        landingSecondsElapsed = 0f;
    }

    // progress runs 0 to 1 across dodgeLastsSeconds. sideSign is -1 for a dodge to the
    // character's left and +1 to its right, which is what the roll needs to lean into.
    public void ShowDodge(float progress, float sideSign)
    {
        dodgeProgress = progress;
        dodgeSideSign = sideSign;
    }

    public void ShowPlayerSwing(float progress, bool isHeavy)
    {
        swingProgress = progress;
        swingIsHeavy = isHeavy;
    }

    public void ShowSurge(float amount)
    {
        surgeAmount = Mathf.Clamp01(amount);
    }

    public void ShowDrinking(float progress)
    {
        drinkProgress = progress;
    }

    public void ShowWeaponSwap(float progress)
    {
        swapProgress = progress;
    }

    public void ShowHitReaction(float progress, float sideSign)
    {
        hitProgress = progress;
        hitSideSign = sideSign;
    }

    // ------------------------------------------------------------------
    // Player poses
    // ------------------------------------------------------------------

    // Four beats: push off, rise, fall, absorb. The absorb is the one that matters - a
    // character that arrives at the floor with no give in the knees reads as weightless.
    private void PoseTheJump()
    {
        if (isAirborne)
        {
            if (verticalSpeed > 0f)
            {
                // Rising. Legs snap straight and the arms throw upward, which is what
                // sells the push rather than the float.
                thighLeftPitch = Mathf.Lerp(thighLeftPitch, Forward(-jumpLaunchThighDegrees), 0.7f);
                thighRightPitch = Mathf.Lerp(thighRightPitch, Forward(-jumpLaunchThighDegrees), 0.7f);
                shinLeftPitch = Mathf.Lerp(shinLeftPitch, 0f, 0.7f);
                shinRightPitch = Mathf.Lerp(shinRightPitch, 0f, 0.7f);
                upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, Forward(-jumpLaunchArmDegrees), 0.7f);
                upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, Forward(-jumpLaunchArmDegrees), 0.7f);
            }
            else
            {
                // Falling. The legs tuck up ready for the ground.
                thighLeftPitch = Mathf.Lerp(thighLeftPitch, Forward(jumpTuckThighDegrees), 0.7f);
                thighRightPitch = Mathf.Lerp(thighRightPitch, Forward(jumpTuckThighDegrees), 0.7f);
                shinLeftPitch = Mathf.Lerp(shinLeftPitch, Forward(-jumpTuckShinDegrees), 0.7f);
                shinRightPitch = Mathf.Lerp(shinRightPitch, Forward(-jumpTuckShinDegrees), 0.7f);
                upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, Forward(-jumpAirborneArmDegrees), 0.7f);
                upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, Forward(-jumpAirborneArmDegrees), 0.7f);
            }

            return;
        }

        if (landingSecondsElapsed < 0f)
        {
            return;
        }

        landingSecondsElapsed = landingSecondsElapsed + Time.deltaTime;

        float total = landingDropSeconds + landingRecoverySeconds;
        if (landingSecondsElapsed >= total)
        {
            landingSecondsElapsed = -1f;
            return;
        }

        float crouch;
        if (landingSecondsElapsed < landingDropSeconds)
        {
            // Down fast.
            crouch = landingSecondsElapsed / landingDropSeconds;
        }
        else
        {
            // Back up slower, which is what makes it read as absorbing rather than
            // bouncing.
            float intoRecovery =
                (landingSecondsElapsed - landingDropSeconds) / landingRecoverySeconds;
            crouch = 1f - intoRecovery;
        }

        hipsOffset.y = hipsOffset.y - landingCrouchMetres * crouch;
        thighLeftPitch = thighLeftPitch + Forward(20f) * crouch;
        thighRightPitch = thighRightPitch + Forward(20f) * crouch;
        shinLeftPitch = shinLeftPitch + Forward(-34f) * crouch;
        shinRightPitch = shinRightPitch + Forward(-34f) * crouch;
        torsoPitch = torsoPitch + Forward(10f) * crouch;
    }

    // A committed throw of the body sideways. The roll about the forward axis is the
    // whole thing - without it a dodge is just a fast walk with the legs in the air.
    private void PoseTheDodge()
    {
        if (dodgeProgress < 0f)
        {
            return;
        }

        float through = Mathf.Clamp01(dodgeProgress);

        // How much of the dodge pose to show. It comes on almost instantly and eases out
        // over the last third as the leading leg reaches for the ground.
        float amount;
        if (through < 0.23f)
        {
            amount = through / 0.23f;
        }
        else if (through < 0.68f)
        {
            amount = 1f;
        }
        else
        {
            amount = 1f - (through - 0.68f) / 0.32f;
        }

        torsoRoll = torsoRoll + dodgeRollDegrees * dodgeSideSign * amount;
        hipsRoll = hipsRoll + dodgeRollDegrees * 0.4f * dodgeSideSign * amount;
        hipsOffset.y = hipsOffset.y - dodgeHipDropMetres * amount;

        // The legs tuck through the middle of the dodge and the leading one reaches out
        // to plant at the end.
        float tuck = amount;
        float plant = 0f;
        if (through > 0.68f)
        {
            plant = (through - 0.68f) / 0.32f;
        }

        thighLeftPitch = Mathf.Lerp(thighLeftPitch, Forward(dodgeTuckThighDegrees), tuck);
        thighRightPitch = Mathf.Lerp(thighRightPitch, Forward(dodgeTuckThighDegrees), tuck);
        shinLeftPitch = Mathf.Lerp(shinLeftPitch, Forward(-dodgeTuckShinDegrees), tuck);
        shinRightPitch = Mathf.Lerp(shinRightPitch, Forward(-dodgeTuckShinDegrees), tuck);

        // The leading leg extends to plant. Which leg leads is whichever side the dodge
        // is going, so the body lands on the outside foot.
        if (dodgeSideSign >= 0f)
        {
            thighLeftPitch = Mathf.Lerp(thighLeftPitch, Forward(20f), plant);
            shinLeftPitch = Mathf.Lerp(shinLeftPitch, 0f, plant);
        }
        else
        {
            thighRightPitch = Mathf.Lerp(thighRightPitch, Forward(20f), plant);
            shinRightPitch = Mathf.Lerp(shinRightPitch, 0f, plant);
        }

        // Arms pull in tight to the chest.
        upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, Forward(dodgeArmsInDegrees), tuck);
        upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, Forward(dodgeArmsInDegrees), tuck);
        forearmLeftPitch = Mathf.Lerp(forearmLeftPitch, Forward(-70f), tuck);
        forearmRightPitch = Mathf.Lerp(forearmRightPitch, Forward(-70f), tuck);
    }

    // The player's own swing. Three beats - cock, drive, recover - and the shoulders
    // yawing through the middle of it is what makes the arm look driven by the body
    // rather than waved on its own.
    private void PoseThePlayerSwing()
    {
        if (swingProgress < 0f)
        {
            return;
        }

        float through = Mathf.Clamp01(swingProgress);

        // The three beats keep the same proportions the spec gives, whether the weapon is
        // fast or slow - PlayerAnimator scales the whole thing by the weapon's own
        // cooldown, so a surge speeds the animation up exactly as much as it speeds the
        // swing up.
        float cockUntil = swingIsHeavy ? 0.47f : 0.23f;
        float driveUntil = swingIsHeavy ? 0.63f : 0.52f;

        float armDegrees;
        float shouldersDegrees;
        float forearmDegrees;
        float leanDegrees;
        float hipDrop = 0f;

        float raise = swingIsHeavy ? heavyArmRaiseDegrees : swingArmDegrees * 0.55f;
        float drive = swingIsHeavy ? -heavyArmThroughDegrees : -swingArmDegrees;
        float cockYaw = swingIsHeavy ? -heavyShouldersYawDegrees : swingShouldersCockDegrees;
        float throughYaw = swingIsHeavy ? heavyShouldersYawDegrees : swingShouldersThroughDegrees;

        if (through < cockUntil)
        {
            // Anticipation: pull back. Going the wrong way first is what gives a swing
            // somewhere to come from.
            float into = through / cockUntil;
            float eased = into * into;
            armDegrees = raise * eased;
            shouldersDegrees = cockYaw * eased;
            forearmDegrees = swingForearmCockedDegrees * eased;
            leanDegrees = swingIsHeavy ? -heavyLeanBackDegrees * eased : 0f;
        }
        else if (through < driveUntil)
        {
            // The strike. Accelerating, and it carries past neutral into follow-through.
            float into = (through - cockUntil) / (driveUntil - cockUntil);
            float eased = into * into;
            armDegrees = Mathf.Lerp(raise, drive, eased);
            shouldersDegrees = Mathf.Lerp(cockYaw, throughYaw, eased);
            forearmDegrees = Mathf.Lerp(swingForearmCockedDegrees,
                swingForearmExtendedDegrees, eased);
            leanDegrees = swingIsHeavy
                ? Mathf.Lerp(-heavyLeanBackDegrees, heavyFoldForwardDegrees, eased)
                : Mathf.Lerp(0f, 8f, eased);
            hipDrop = swingIsHeavy ? heavyHipDropMetres * eased : 0f;
        }
        else
        {
            // Recovery. Eased back to neutral so the weapon settles rather than snapping.
            float into = (through - driveUntil) / (1f - driveUntil);
            float eased = 1f - (1f - into) * (1f - into);
            armDegrees = Mathf.Lerp(drive, 0f, eased);
            shouldersDegrees = Mathf.Lerp(throughYaw, 0f, eased);
            forearmDegrees = Mathf.Lerp(swingForearmExtendedDegrees, 0f, eased);
            leanDegrees = Mathf.Lerp(
                swingIsHeavy ? heavyFoldForwardDegrees : 8f, 0f, eased);
            hipDrop = swingIsHeavy ? heavyHipDropMetres * (1f - eased) : 0f;
        }

        shouldersYaw = shouldersYaw + shouldersDegrees;
        torsoPitch = torsoPitch + Forward(leanDegrees);
        hipsOffset.y = hipsOffset.y - hipDrop;

        // The weapon is in the same hand the creatures carry theirs in - see the note on
        // weaponIsInTheArmNamedLeft.
        if (weaponIsInTheArmNamedLeft)
        {
            upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, Forward(-armDegrees), 0.85f);
            forearmLeftPitch = Mathf.Lerp(forearmLeftPitch, Forward(-forearmDegrees), 0.85f);
        }
        else
        {
            upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, Forward(-armDegrees), 0.85f);
            forearmRightPitch = Mathf.Lerp(forearmRightPitch, Forward(-forearmDegrees), 0.85f);
        }
    }

    // Struck and held for the whole surge rather than animated through. A held extreme
    // reads as power; a moving one reads as a wobble.
    private void PoseTheSurge()
    {
        if (surgeAmount <= 0f)
        {
            return;
        }

        upperArmLeftPitch = upperArmLeftPitch + Forward(-surgeArmsBackDegrees) * surgeAmount;
        upperArmRightPitch = upperArmRightPitch + Forward(-surgeArmsBackDegrees) * surgeAmount;
        torsoPitch = torsoPitch + Forward(-surgeTorsoArchDegrees) * surgeAmount;
        headPitch = headPitch + Forward(-8f) * surgeAmount;
        hipsOffset.y = hipsOffset.y + surgeHipRiseMetres * surgeAmount;
    }

    // Off hand only. The weapon stays up, because a player who lowers their guard to
    // drink and then gets hit will read it as the game having taken the weapon away.
    private void PoseTheDrink()
    {
        if (drinkProgress < 0f)
        {
            return;
        }

        float through = Mathf.Clamp01(drinkProgress);

        // Raise, hold, lower - with the hold in the middle where the drinking happens.
        float raised;
        if (through < 0.3f)
        {
            raised = through / 0.3f;
        }
        else if (through < 0.7f)
        {
            raised = 1f;
        }
        else
        {
            raised = 1f - (through - 0.7f) / 0.3f;
        }

        // The off hand is whichever one is NOT carrying the weapon.
        if (weaponIsInTheArmNamedLeft)
        {
            forearmRightPitch = Mathf.Lerp(forearmRightPitch, Forward(-drinkForearmDegrees), raised);
            upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, Forward(25f), raised);
        }
        else
        {
            forearmLeftPitch = Mathf.Lerp(forearmLeftPitch, Forward(-drinkForearmDegrees), raised);
            upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, Forward(25f), raised);
        }

        headPitch = headPitch + Forward(-drinkHeadTiltDegrees) * raised;
    }

    // Short and cheap. It exists only so that changing weapon is not instantaneous.
    private void PoseTheWeaponSwap()
    {
        if (swapProgress < 0f)
        {
            return;
        }

        float through = Mathf.Clamp01(swapProgress);

        // Out and back within the one motion.
        float cross = Mathf.Sin(through * Mathf.PI);

        forearmLeftPitch = forearmLeftPitch + Forward(-swapForearmDegrees) * cross;
        forearmRightPitch = forearmRightPitch + Forward(-swapForearmDegrees) * cross;
        upperArmLeftPitch = upperArmLeftPitch + Forward(12f) * cross;
        upperArmRightPitch = upperArmRightPitch + Forward(12f) * cross;
    }

    // Must be interruptible, which it is: PlayerAnimator simply stops sending a progress
    // and the pose is gone the same frame. A stunlock that cannot be cancelled is worse
    // than no reaction at all.
    private void PoseTheHitReaction()
    {
        if (hitProgress < 0f)
        {
            return;
        }

        float through = Mathf.Clamp01(hitProgress);

        // Snap back hard, recover slowly.
        float amount;
        if (through < 0.25f)
        {
            amount = through / 0.25f;
        }
        else
        {
            amount = 1f - (through - 0.25f) / 0.75f;
        }

        torsoPitch = torsoPitch + Forward(-hitTorsoDegrees) * amount;
        headPitch = headPitch + Forward(-hitHeadDegrees) * amount;
        upperArmLeftPitch = upperArmLeftPitch + Forward(-hitArmsDegrees) * amount;
        upperArmRightPitch = upperArmRightPitch + Forward(-hitArmsDegrees) * amount;

        // Shifted away from whatever hit them.
        hipsOffset.x = hipsOffset.x - hitHipShiftMetres * hitSideSign * amount;
        torsoRoll = torsoRoll - hitTorsoDegrees * 0.5f * hitSideSign * amount;
    }

    // ------------------------------------------------------------------
    // The tail
    // ------------------------------------------------------------------

    // Never posed directly. Each segment chases the one in front of it, and the whole
    // thing is thrown outward when the creature turns - which is what a real tail does,
    // and why it reads as attached rather than as a separate animation.
    private void FollowWithTheTail(float walkAmount)
    {
        if (tailOne == null)
        {
            return;
        }

        float facingNow = transform.eulerAngles.y;
        float turnedBy = Mathf.DeltaAngle(facingLastFrame, facingNow);
        facingLastFrame = facingNow;

        // A turn throws the tail the other way, and a walk sways it gently.
        float thrownBy = 0f;
        if (Time.deltaTime > 0f)
        {
            float turnRate = turnedBy / Time.deltaTime;
            thrownBy = -turnRate * tailSwingPerDegreeTurned;
        }

        float sway = Mathf.Sin(stridePhase) * tailIdleSwayDegrees * walkAmount;
        float wanted = Mathf.Clamp(thrownBy + sway,
            -tailMaximumSwingDegrees, tailMaximumSwingDegrees);

        // Each segment lags the one before it. Doing this with three separate lerps
        // rather than one shared value is the entire trick.
        float catchUp = Mathf.Clamp01(Time.deltaTime * tailCatchUpSpeed);
        tailYawOne = Mathf.Lerp(tailYawOne, wanted, catchUp);
        tailYawTwo = Mathf.Lerp(tailYawTwo, tailYawOne, catchUp);
        tailYawThree = Mathf.Lerp(tailYawThree, tailYawTwo, catchUp);

        // A little droop down the length of it, so the tail hangs rather than sticking
        // out level like a broom handle.
        SetAngles(tailOne, Forward(-tailDroopDegrees), tailYawOne, 0f);
        SetAngles(tailTwo, Forward(-tailDroopDegrees), tailYawTwo, 0f);
        SetAngles(tailThree, Forward(-tailDroopDegrees), tailYawThree, 0f);
    }

    // ------------------------------------------------------------------
    // Enemy attack
    // ------------------------------------------------------------------

    // Called by EnemyBrain once per frame while a blow is being loaded. Zero at the start
    // of the wind-up, one at the moment it finishes.
    public void ShowWindUp(float howFarThrough)
    {
        windUpProgress = Mathf.Clamp01(howFarThrough);
        strikeProgress = 0f;
        isAttacking = true;
    }

    // Called by EnemyBrain once per frame while the blow is landing. Zero as the strike
    // begins, one as it finishes.
    public void ShowStrike(float howFarThrough)
    {
        windUpProgress = 1f;
        strikeProgress = Mathf.Clamp01(howFarThrough);
        isAttacking = true;
    }

    // Called when the creature returns to its resting pose. The arm does not snap back -
    // it fades out over attackReleaseSeconds, so the walk takes it over smoothly.
    public void ClearAttack()
    {
        isAttacking = false;
    }

    // Both arms rather than one, for a two-handed boss move. Everything else about the
    // wind-up and strike is identical, so this is a flag rather than a second animation.
    public void UseBothArmsForTheNextAttack(bool bothArms)
    {
        attackUsesBothArms = bothArms;
    }

    private bool attackUsesBothArms;

    private void FadeTheAttack()
    {
        if (isAttacking)
        {
            attackFade = 1f;
            return;
        }

        if (attackFade <= 0f)
        {
            return;
        }

        if (attackReleaseSeconds <= 0f)
        {
            attackFade = 0f;
            return;
        }

        attackFade = attackFade - Time.deltaTime / attackReleaseSeconds;

        if (attackFade < 0f)
        {
            attackFade = 0f;
        }
    }

    // The swing, laid over whatever the walk and the idle already decided.
    //
    // This deliberately touches only the arms and the torso. The legs keep walking
    // underneath, because a creature that stops moving its feet the instant it swings
    // reads as two separate animations being cut between rather than one body doing two
    // things at once.
    //
    // Nothing here writes to this component's own transform. EnemyBrain is arching and
    // dropping the model root through the same blow, and the two are meant to add up: the
    // root is the whole creature leaning, and this is the spine and shoulders bending on
    // top of it.
    private void PoseTheAttack(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        float raise;
        float elbow;
        float spine;

        if (strikeProgress <= 0f)
        {
            // Loading. Eased so the weapon slows as it reaches the top and hangs there,
            // matching the club EnemyBrain is hauling up on the weapon pivot.
            float eased = 1f - (1f - windUpProgress) * (1f - windUpProgress);
            raise = attackRaiseDegrees * eased;
            elbow = attackElbowFoldDegrees * eased;
            spine = -attackArchDegrees * eased;
        }
        else
        {
            // Driving through. Accelerating rather than linear, and it carries past the
            // rest pose into the follow-through rather than stopping at it.
            float eased = strikeProgress * strikeProgress;
            raise = Mathf.Lerp(attackRaiseDegrees, attackFollowThroughDegrees, eased);
            elbow = Mathf.Lerp(attackElbowFoldDegrees, 0f, eased);
            spine = Mathf.Lerp(-attackArchDegrees, attackFoldDegrees, eased);
        }

        float swingingArm = raise * forwardSwingSign;
        float swingingElbow = elbow * -forwardSwingSign;
        float offArm = -attackOffArmDegrees * (raise / attackRaiseDegrees) * forwardSwingSign;

        if (attackUsesBothArms)
        {
            // A two-handed hurl or slam. Both arms do the same thing, which is what makes
            // a boss move read as heavier than a one-armed swing.
            upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, swingingArm, amount);
            upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, swingingArm, amount);
            forearmLeftPitch = Mathf.Lerp(forearmLeftPitch, swingingElbow, amount);
            forearmRightPitch = Mathf.Lerp(forearmRightPitch, swingingElbow, amount);
        }
        else if (weaponIsInTheArmNamedLeft)
        {
            upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, swingingArm, amount);
            forearmLeftPitch = Mathf.Lerp(forearmLeftPitch, swingingElbow, amount);
            upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, offArm, amount);
        }
        else
        {
            upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, swingingArm, amount);
            forearmRightPitch = Mathf.Lerp(forearmRightPitch, swingingElbow, amount);
            upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, offArm, amount);
        }

        torsoPitch = Mathf.Lerp(torsoPitch, spine * -forwardSwingSign, amount);
    }

    // ------------------------------------------------------------------
    // Boss poses
    // ------------------------------------------------------------------

    // Arms spread wide and the head tilted back so the slot in it faces the ceiling. A
    // held pose rather than a cycle - the Warden is calling something down, and standing
    // still while it happens is what makes it read as deliberate.
    public void ShowSummoning(float amount)
    {
        summonAmount = Mathf.Clamp01(amount);
    }

    // Driven on impact, not on the wind-up. Dropping the hips as the shockwave leaves is
    // what makes the ring look like it came out of the Warden rather than appearing near
    // it.
    public void ShowSlamImpact()
    {
        slamSecondsElapsed = 0f;
    }

    public float slamHipDropMetres = 0.25f;
    public float slamDropSeconds = 0.1f;
    public float slamRecoverySeconds = 0.4f;
    public float summonArmSpreadDegrees = 70f;
    public float summonHeadTiltDegrees = 25f;

    // The charge. Massive and slow: pitched right forward, shoulders rolling with each
    // footfall, and the whole stride deliberately HALVED so a creature travelling at
    // fifteen metres a second does not windmill its legs. Speed alone would drive the
    // walk cycle at a comic rate; a boss has to look heavy while it is moving fast.
    public float chargeTorsoLeanDegrees = 20f;
    public float chargeShoulderRollDegrees = 8f;
    public float chargeStrideMultiplier = 0.5f;

    private float summonAmount;
    private float slamSecondsElapsed = -1f;
    private float chargeAmount;

    // Held for as long as the Warden is charging, wind-up and run alike.
    public void ShowCharging(float amount)
    {
        chargeAmount = Mathf.Clamp01(amount);
    }

    private void PoseTheCharge()
    {
        if (chargeAmount <= 0f)
        {
            return;
        }

        torsoPitch = torsoPitch + Forward(chargeTorsoLeanDegrees) * chargeAmount;
        headPitch = headPitch + Forward(-6f) * chargeAmount;

        // The shoulders roll with the stride rather than on a clock of their own, so the
        // roll and the footfalls stay together however fast the charge is going.
        shouldersRoll = shouldersRoll
            + Mathf.Sin(stridePhase) * chargeShoulderRollDegrees * chargeAmount;
    }

    private void PoseTheSummon()
    {
        if (summonAmount <= 0f)
        {
            return;
        }

        upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch,
            Forward(-summonArmSpreadDegrees), summonAmount);
        upperArmRightPitch = Mathf.Lerp(upperArmRightPitch,
            Forward(-summonArmSpreadDegrees), summonAmount);
        forearmLeftPitch = Mathf.Lerp(forearmLeftPitch, Forward(-20f), summonAmount);
        forearmRightPitch = Mathf.Lerp(forearmRightPitch, Forward(-20f), summonAmount);
        headPitch = headPitch + Forward(-summonHeadTiltDegrees) * summonAmount;
        torsoPitch = torsoPitch + Forward(-10f) * summonAmount;
    }

    private void PoseTheSlamImpact()
    {
        if (slamSecondsElapsed < 0f)
        {
            return;
        }

        slamSecondsElapsed = slamSecondsElapsed + Time.deltaTime;

        float total = slamDropSeconds + slamRecoverySeconds;
        if (slamSecondsElapsed >= total)
        {
            slamSecondsElapsed = -1f;
            return;
        }

        float drop;
        if (slamSecondsElapsed < slamDropSeconds)
        {
            drop = slamSecondsElapsed / slamDropSeconds;
        }
        else
        {
            drop = 1f - (slamSecondsElapsed - slamDropSeconds) / slamRecoverySeconds;
        }

        hipsOffset.y = hipsOffset.y - slamHipDropMetres * drop;
        thighLeftPitch = thighLeftPitch + Forward(24f) * drop;
        thighRightPitch = thighRightPitch + Forward(24f) * drop;
        shinLeftPitch = shinLeftPitch + Forward(-40f) * drop;
        shinRightPitch = shinRightPitch + Forward(-40f) * drop;
    }

    // ------------------------------------------------------------------
    // Death
    // ------------------------------------------------------------------

    // Called by whatever kills the creature. Kept as a plain public method rather than an
    // event so it can be triggered from EnemyBrain, the Warden, or a test harness without
    // any of them needing to know about the others.
    public void PlayDeath()
    {
        isDead = true;
        deadForSeconds = 0f;
        isAttacking = false;
        attackFade = 0f;

        // Every held pose is dropped, or a Warden killed mid-charge would collapse while
        // still braced to run, and one killed mid-summon would die with its arms flung
        // wide. The death branch applies the pose too, so anything left set here would
        // sit on top of the collapse.
        chargeAmount = 0f;
        summonAmount = 0f;
        slamSecondsElapsed = -1f;
        sprintAmount = 0f;
        dodgeProgress = -1f;
        swingProgress = -1f;
        drinkProgress = -1f;
        swapProgress = -1f;
        hitProgress = -1f;
        landingSecondsElapsed = -1f;
    }

    // Enemies are recycled rather than destroyed - EnemyBrain.ResetToStartingState puts a
    // round's dead back on their feet. Without this the collapse would be permanent and
    // every reused creature would come back folded on the floor.
    public void ReturnToLife()
    {
        isDead = false;
        deadForSeconds = 0f;
        isAttacking = false;
        attackFade = 0f;
        windUpProgress = 0f;
        strikeProgress = 0f;
        smoothedSpeed = 0f;
        summonAmount = 0f;
        slamSecondsElapsed = -1f;
        chargeAmount = 0f;
        positionLastFrame = transform.position;
    }

    public bool DeathAnimationHasFinished()
    {
        return isDead && deadForSeconds >= 1f;
    }

    private void PoseTheDeath()
    {
        deadForSeconds = deadForSeconds + Time.deltaTime;

        // A one-second collapse: the legs give out, the torso folds forward, the arms
        // drop. Eased so it starts fast and settles, which reads as weight rather than as
        // a slow-motion faint.
        float through = Mathf.Clamp01(deadForSeconds / 1.0f);
        float eased = 1f - (1f - through) * (1f - through);

        torsoPitch = Mathf.Lerp(0f, 70f, eased) * -forwardSwingSign;
        thighLeftPitch = Mathf.Lerp(0f, 40f, eased) * forwardSwingSign;
        thighRightPitch = Mathf.Lerp(0f, 35f, eased) * forwardSwingSign;
        shinLeftPitch = Mathf.Lerp(0f, 60f, eased) * -forwardSwingSign;
        shinRightPitch = Mathf.Lerp(0f, 55f, eased) * -forwardSwingSign;
        upperArmLeftPitch = Mathf.Lerp(0f, 25f, eased) * forwardSwingSign;
        upperArmRightPitch = Mathf.Lerp(0f, 20f, eased) * forwardSwingSign;

        hipsOffset.y = Mathf.Lerp(0f, -0.35f, eased);
    }

    // ------------------------------------------------------------------
    // Applying it
    // ------------------------------------------------------------------

    // The one place any body part is ever moved. Everything above only decides angles.
    private void ApplyThePose()
    {
        SetAngles(torso, torsoPitch, torsoYaw, torsoRoll);
        SetAngles(head, headPitch, headYaw, 0f);
        SetAngles(shoulders, shouldersPitch, shouldersYaw, shouldersRoll);
        SetAngles(thighLeft, thighLeftPitch, 0f, 0f);
        SetAngles(thighRight, thighRightPitch, 0f, 0f);
        SetAngles(shinLeft, shinLeftPitch, 0f, 0f);
        SetAngles(shinRight, shinRightPitch, 0f, 0f);
        SetAngles(upperArmLeft, upperArmLeftPitch, 0f, 0f);
        SetAngles(upperArmRight, upperArmRightPitch, 0f, 0f);
        SetAngles(forearmLeft, forearmLeftPitch, 0f, 0f);
        SetAngles(forearmRight, forearmRightPitch, 0f, 0f);

        if (hips != null)
        {
            // Always measured from the rest pose the model was built in, never from
            // wherever the hips happen to be right now, so a pose can never turn into a
            // drift. The offset is in the wrapper's space, which is upright and
            // world-aligned, so +Y really is up and +X really is the character's right.
            hips.localPosition = hipsRestPosition + hipsOffset;

            // The rest rotation is the -90 degrees that stands the creature up. Anything
            // this component adds has to compose WITH it, which is why it multiplies
            // rather than replaces. Clearing it lays the creature on its back.
            hips.localRotation = hipsRestRotation * AnglesToRotation(0f, hipsYaw, hipsRoll);
        }
    }

    // Pitch, yaw and roll in the creature's own terms, turned into the rotation that
    // actually produces them.
    //
    // The argument order into Quaternion.Euler is (pitch, roll, yaw) and that is not a
    // mistake - see the note at the top of this file. Every part hangs under a Hips that
    // carries the -90 degree stand-up rotation, so each part's own X, Y and Z are still
    // Blender's left-right, forward and up.
    private Quaternion AnglesToRotation(float pitch, float yaw, float roll)
    {
        return Quaternion.Euler(pitch, roll, yaw);
    }

    private void SetAngles(Transform part, float pitch, float yaw, float roll)
    {
        if (part == null)
        {
            return;
        }

        part.localRotation = AnglesToRotation(pitch, yaw, roll);
    }
}
