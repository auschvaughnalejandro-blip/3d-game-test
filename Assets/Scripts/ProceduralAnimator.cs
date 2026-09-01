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

    [Header("Player swing - sword (stab)")]
    // A thrust, not a swing. The elbow does the work: it folds tight on the wind-up and
    // snaps straight through the strike, so the hand travels in a line rather than an arc.
    public float stabCockArmDegrees = 30f;
    public float stabCockElbowDegrees = 92f;
    public float stabCockYawDegrees = 18f;

    // How far out in front the arm finishes. The heavy is a committed lunge rather than a
    // jab, so it reaches further and leans further after it.
    public float stabReachDegrees = 84f;
    public float stabHeavyReachDegrees = 96f;
    public float stabExtendedElbowDegrees = 6f;
    public float stabThroughYawDegrees = 26f;
    public float stabLeanDegrees = 9f;
    public float stabHeavyLeanDegrees = 16f;

    // The step into the thrust, in metres along the way the body faces.
    //
    // This is the ONLY thing in the whole file that moves the hips anywhere but up and
    // down and sideways, so it is the one axis here that nothing else has already proved.
    // If a stab drifts sideways instead of forward, this is what to zero.
    public float stabLungeMetres = 0.12f;

    [Header("Player swing - hammer (smash)")]
    // Up over the shoulder and down with the whole body behind it. The knees are as much
    // of this as the arms are: a blow that lands with the legs straight has nothing in it.
    public float smashRaiseDegrees = 118f;
    public float smashHeavyRaiseDegrees = 142f;
    public float smashElbowFoldDegrees = 74f;
    public float smashElbowOpenDegrees = 12f;
    public float smashThroughDegrees = 46f;

    // The spine arches back under the raised head and folds forward through the blow.
    public float smashArchDegrees = 16f;
    public float smashFoldDegrees = 32f;

    // Rise onto the toes to load it, then drop into the floor to land it.
    public float smashRiseMetres = 0.05f;
    public float smashDropMetres = 0.13f;
    public float smashHeavyDropMetres = 0.20f;
    public float smashKneeDegrees = 14f;
    public float smashHeavyKneeDegrees = 24f;

    [Header("Player swing - Warden's Edge (sweep)")]
    // How far round the body turns. The heavy matches PlayerWeapons.EdgeHeavyArcDegrees
    // deliberately - the damage really does go the whole way round, and an animation that
    // said otherwise would be lying about the reach.
    public float sweepSpinDegrees = 130f;
    public float sweepHeavySpinDegrees = 360f;
    public float sweepWindUpDegrees = 34f;

    // How far the shoulders run ahead of and behind the hips. This is the whip - without
    // it a turn reads as a statue on a turntable.
    public float sweepShoulderLeadDegrees = 22f;

    // The arm is pinned out at shoulder height for the whole strike so the blade stays
    // level. All of the travel happens underneath it.
    public float sweepArmOutDegrees = 74f;
    public float sweepElbowDegrees = 10f;
    public float sweepLeanDegrees = 10f;
    public float sweepHipDropMetres = 0.05f;

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

    [Header("Bow")]
    // How far the bow arm swings forward to hold the bow out at the target. Roughly
    // horizontal from a hanging rest, because a bow held any lower cannot be sighted
    // along.
    public float bowArmForwardDegrees = 76f;

    // The bow arm is held nearly straight. Not completely - a locked elbow reads as a
    // mannequin - but the bend is small enough that the arm is obviously braced.
    public float bowArmElbowDegrees = 10f;

    // The drawing arm at REST on the string, before anything has been pulled. The hand
    // starts out beside the bow, which is why the upper arm is already well forward.
    public float drawArmForwardAtRestDegrees = 62f;
    public float drawArmElbowAtRestDegrees = 26f;

    // The drawing arm at FULL draw. The difference between this pair and the pair above
    // is the whole of the animation the player is being asked for: hold the button
    // longer, the elbow folds further and the hand travels further back.
    //
    // The elbow does nearly all of the work on purpose. Pitch is the only axis these
    // arms have, so a hand cannot actually be carried out to the side and back to the
    // cheek - but folding the elbow hard while the upper arm drops brings it to the
    // chest, and from any angle the camera can be at, that reads as an anchored draw.
    public float drawArmForwardAtFullDegrees = 12f;
    public float drawArmElbowAtFullDegrees = 138f;

    // How far the bow arm swings forward when the camera is in the archer's own head.
    //
    // Higher than the third-person angle, and it has to be. The arm is 0.58 m of reach
    // hanging off a shoulder 0.57 m above the player's transform, and the eye sits 0.75 m
    // up; at 76 degrees the bow hand lands about 0.38 m above the transform and 0.58 m in
    // front of it, which is 43 degrees BELOW the middle of the screen. A 60-degree camera
    // only sees 30 degrees down, so the bow, the string and the nocked arrow were all
    // sitting just under the bottom edge of the picture and the draw could not be seen.
    //
    // 108 degrees is 18 above horizontal, which brings the hand to about 0.70 m - within
    // 5 cm of the eye - and 7 degrees under the middle of the screen. It is also the
    // honest pose: an archer really does hold the bow up on the line between their eye
    // and what they are shooting at, and looks along the arrow past their own hand.
    public float bowArmForwardFromTheEyeDegrees = 108f;

    // The shoulders blade round as the string comes back, so the bow arm ends up
    // pointing down the shot. Small, because the body below is already turned to face
    // what is being aimed at.
    //
    // Flipped with the bow hand: turning the drawing shoulder BACK is the whole point,
    // and which way back is depends on which hand holds the bow. If it turns the wrong
    // way on a differently built character, this is the one number to negate.
    public float bowShouldersYawDegrees = 18f;

    // The head comes round to sight along the arrow, and dips slightly to bring the eye
    // down to it.
    public float bowHeadYawDegrees = 12f;
    public float bowHeadDipDegrees = 6f;

    // The chest opens and the weight settles as the draw deepens.
    public float bowLeanBackDegrees = 7f;
    public float bowHipSettleMetres = 0.03f;

    // The string is heavy, and near the end of the pull it starts to win. The tremble
    // begins only in the last stretch of the draw and is what tells the player they have
    // reached maximum without them having to look at the draw bar to find out.
    public float bowTrembleStartsAtDraw = 0.82f;
    public float bowTrembleDegrees = 2.2f;
    public float bowTrembleSpeed = 26f;

    // The loose. The drawing hand snaps back off the string and the bow arm kicks, both
    // scaled by how far the bow was actually drawn - a shot let go at the minimum barely
    // moves, a full one throws the hand right back.
    public float looseHandSnapDegrees = 34f;
    public float looseBowArmKickDegrees = 14f;

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
    private SwingShape swingShape = SwingShape.Stab;
    private float surgeAmount;
    private float drinkProgress = -1f;
    private float swapProgress = -1f;
    private float hitProgress = -1f;
    private float hitSideSign;

    // The bow. bowReadyAmount is how far the bow has been brought up into an aiming
    // stance, blended by PlayerAnimator so that raising and lowering it is not a pop.
    // bowDrawAmount is how far back the string is, nought to one, and is the number the
    // whole pose is built around.
    private float bowReadyAmount;
    private float bowDrawAmount;
    private float looseProgress = -1f;
    private float looseWasDrawnTo;

    // Whether the archer is looking down their own bow rather than being watched from
    // behind. Only ever true on the player, and only while the camera is in first person.
    private bool bowIsSightedFromTheEye;

    // Tail, carried between frames because the whole point of it is that it lags.
    private float tailYawOne;
    private float tailYawTwo;
    private float tailYawThree;
    private float facingLastFrame;

    private void Start()
    {
        FindTheParts();
        positionLastFrame = WhereTheBodyActuallyIs();
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

        // The loose lays over the draw, so the draw has to be posed first - see the note
        // on PoseTheBowLoose about the frame where the draw collapses.
        PoseTheBowDraw();
        PoseTheBowLoose();

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
        Vector3 whereWeAreNow = WhereTheBodyActuallyIs();

        Vector3 movedThisFrame = whereWeAreNow - positionLastFrame;
        positionLastFrame = whereWeAreNow;

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

    // Where the creature is, as opposed to where the animation has just put its hips.
    //
    // This component's own transform IS the hips - ValleyBuilder names the model wrapper
    // "Hips" and hangs this on it - and every pose writes hipsOffset into that transform.
    // So measuring speed from it measures the animation as well as the walk, and the
    // animation then feeds back into the walk that produced it.
    //
    // It stayed invisible while the only horizontal offset was the hit reaction's five
    // centimetres. The stab's lunge is bigger and faster, and would have started a walk
    // cycle in the legs of a player thrusting from a standstill - which would have read
    // as the stab being broken rather than as the speedometer measuring itself.
    //
    // The parent is the creature root that EnemyBrain and PlayerMovement actually move,
    // so it is the honest answer. A creature built without a wrapper has no parent to ask
    // and falls back to the old behaviour.
    private Vector3 WhereTheBodyActuallyIs()
    {
        if (transform.parent != null)
        {
            return transform.parent.position;
        }

        return transform.position;
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

    // shape decides which of the three motions to make, and comes from the weapon that
    // made the swing rather than from anything this component knows.
    public void ShowPlayerSwing(float progress, bool isHeavy, SwingShape shape)
    {
        swingProgress = progress;
        swingIsHeavy = isHeavy;
        swingShape = shape;
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

    // readyAmount is how far the bow is up, drawAmount how far the string is back. They
    // are separate because they genuinely are: the bow comes up the instant the button
    // goes down, and during the recovery of the previous shot it stays up with the
    // string forward, which is exactly what a nocked-but-not-yet-pulled bow looks like.
    public void ShowBowDraw(float readyAmount, float drawAmount)
    {
        bowReadyAmount = Mathf.Clamp01(readyAmount);
        bowDrawAmount = Mathf.Clamp01(drawAmount);
    }

    // progress runs 0 to 1 across the release. drawnTo is how far the bow had been drawn
    // when the arrow left, so the snap is as big as the shot was.
    public void ShowBowLoose(float progress, float drawnTo)
    {
        looseProgress = progress;
        looseWasDrawnTo = Mathf.Clamp01(drawnTo);
    }

    // Whether the bow is being sighted down by somebody standing inside this body, which
    // raises the bow arm to the eye line - see bowArmForwardFromTheEyeDegrees.
    //
    // Told to this script rather than worked out by it. Every creature in the game shares
    // this animator, and it has no business knowing that a camera exists, let alone which
    // of two views that camera is in. PlayerAnimator knows both and answers the question.
    public void ShowBowSightedFromTheEye(bool fromTheEye)
    {
        bowIsSightedFromTheEye = fromTheEye;
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
    // A swing, in whichever shape the weapon in hand makes.
    //
    // Three weapons, three genuinely different motions rather than one motion with the
    // numbers turned up. All they share is the three-beat structure every strike in this
    // file uses - anticipation, strike, recovery - and the fact that the whole thing is
    // scaled by the cooldown that swing actually cost, so a surge speeds the animation up
    // by exactly as much as it speeds the attack up, with nothing extra to keep in sync.
    //
    // Which shape to make is decided by the weapon and passed in. Nothing here asks what
    // the player is holding, so a fourth weapon animates by naming its shape.
    private void PoseThePlayerSwing()
    {
        if (swingProgress < 0f)
        {
            return;
        }

        float through = Mathf.Clamp01(swingProgress);

        if (swingShape == SwingShape.Smash)
        {
            PoseTheSmash(through);
            return;
        }

        if (swingShape == SwingShape.Sweep)
        {
            PoseTheSweep(through);
            return;
        }

        PoseTheStab(through);
    }

    // The sword: a thrust.
    //
    // What makes a stab read as a stab rather than as a short swing is that the hand
    // travels in a straight line down the way the player is facing, and that the ELBOW
    // does the work. So the wind-up folds the arm tight to the ribs and the strike snaps
    // it out straight with the shoulder driving forward behind it - rather than the arm
    // sweeping through an arc, which is what both of the other shapes here do.
    private void PoseTheStab(float through)
    {
        float cockUntil = swingIsHeavy ? 0.36f : 0.26f;
        float thrustUntil = swingIsHeavy ? 0.56f : 0.48f;

        float reach = swingIsHeavy ? stabHeavyReachDegrees : stabReachDegrees;
        float lean = swingIsHeavy ? stabHeavyLeanDegrees : stabLeanDegrees;

        // Every angle below is written as the pitch the part actually ends up at, with
        // positive meaning forward. Forward() is applied once at the bottom.
        float armForward;
        float elbowForward;
        float shoulderTurn;
        float leanForward;
        float lunge;

        if (through < cockUntil)
        {
            // Draw back to the hip. Accelerating into it, because a wind-up that starts
            // fast has nothing left to give the strike.
            float into = through / cockUntil;
            float eased = into * into;

            armForward = Mathf.Lerp(0f, -stabCockArmDegrees, eased);
            elbowForward = Mathf.Lerp(0f, -stabCockElbowDegrees, eased);
            shoulderTurn = stabCockYawDegrees * eased;
            leanForward = -lean * 0.35f * eased;
            lunge = 0f;
        }
        else if (through < thrustUntil)
        {
            // Out. The elbow going from folded to straight is the whole of the stab.
            float into = (through - cockUntil) / (thrustUntil - cockUntil);
            float eased = into * into;

            armForward = Mathf.Lerp(-stabCockArmDegrees, reach, eased);
            elbowForward = Mathf.Lerp(-stabCockElbowDegrees, -stabExtendedElbowDegrees, eased);
            shoulderTurn = Mathf.Lerp(stabCockYawDegrees, -stabThroughYawDegrees, eased);
            leanForward = Mathf.Lerp(-lean * 0.35f, lean, eased);
            lunge = stabLungeMetres * eased;
        }
        else
        {
            // Back to guard, eased so the point settles rather than snapping.
            float into = (through - thrustUntil) / (1f - thrustUntil);
            float eased = 1f - (1f - into) * (1f - into);

            armForward = Mathf.Lerp(reach, 0f, eased);
            elbowForward = Mathf.Lerp(-stabExtendedElbowDegrees, 0f, eased);
            shoulderTurn = Mathf.Lerp(-stabThroughYawDegrees, 0f, eased);
            leanForward = Mathf.Lerp(lean, 0f, eased);
            lunge = stabLungeMetres * (1f - eased);
        }

        shouldersYaw = shouldersYaw + shoulderTurn;
        torsoPitch = torsoPitch + Forward(leanForward);
        hipsOffset.z = hipsOffset.z + lunge;

        // The off arm counterweights - it goes back as the sword goes out. A body that
        // thrusts one arm and leaves the other hanging reads as a puppet, which is the
        // same note the enemy attack carries about its own off arm.
        float offArm = -armForward * 0.35f;

        PoseTheSwingArms(Forward(armForward), Forward(elbowForward),
            Forward(offArm), Forward(-stabCockElbowDegrees * 0.2f));
    }

    // The hammer: an overhead smash.
    //
    // The weight is the point. A hammer that is merely swung quickly is a sword; what
    // makes it a hammer is that the whole body goes up with it and then comes down with
    // it. So this is the one shape where the hips and the knees do as much work as the
    // arms, and the knees especially - a blow that lands with the legs straight has
    // nothing behind it.
    private void PoseTheSmash(float through)
    {
        float raiseUntil = swingIsHeavy ? 0.46f : 0.38f;
        float slamUntil = swingIsHeavy ? 0.62f : 0.54f;

        float raise = swingIsHeavy ? smashHeavyRaiseDegrees : smashRaiseDegrees;
        float drop = swingIsHeavy ? smashHeavyDropMetres : smashDropMetres;
        float knee = swingIsHeavy ? smashHeavyKneeDegrees : smashKneeDegrees;

        float armForward;
        float elbowForward;
        float archForward;
        float hipHeight;
        float kneeBend;

        if (through < raiseUntil)
        {
            // Up and behind the shoulder, chest opening, weight onto the back foot.
            float into = through / raiseUntil;
            float eased = into * into;

            armForward = Mathf.Lerp(0f, -raise, eased);
            elbowForward = Mathf.Lerp(0f, -smashElbowFoldDegrees, eased);
            archForward = Mathf.Lerp(0f, -smashArchDegrees, eased);
            hipHeight = smashRiseMetres * eased;
            kneeBend = 0f;
        }
        else if (through < slamUntil)
        {
            // Down. Everything arrives at once, which is what makes it land rather than
            // merely finish.
            float into = (through - raiseUntil) / (slamUntil - raiseUntil);
            float eased = into * into;

            armForward = Mathf.Lerp(-raise, smashThroughDegrees, eased);
            elbowForward = Mathf.Lerp(-smashElbowFoldDegrees, -smashElbowOpenDegrees, eased);
            archForward = Mathf.Lerp(-smashArchDegrees, smashFoldDegrees, eased);
            hipHeight = Mathf.Lerp(smashRiseMetres, -drop, eased);
            kneeBend = knee * eased;
        }
        else
        {
            // Standing back up out of it, which takes longer than the blow did.
            float into = (through - slamUntil) / (1f - slamUntil);
            float eased = 1f - (1f - into) * (1f - into);

            armForward = Mathf.Lerp(smashThroughDegrees, 0f, eased);
            elbowForward = Mathf.Lerp(-smashElbowOpenDegrees, 0f, eased);
            archForward = Mathf.Lerp(smashFoldDegrees, 0f, eased);
            hipHeight = Mathf.Lerp(-drop, 0f, eased);
            kneeBend = knee * (1f - eased);
        }

        torsoPitch = torsoPitch + Forward(archForward);
        headPitch = headPitch + Forward(archForward * 0.4f);
        hipsOffset.y = hipsOffset.y + hipHeight;

        BendTheKnees(kneeBend);

        // Both hands are on the haft, so the off arm goes WITH the weapon arm rather than
        // counterweighting against it. That is the difference between a two-handed blow
        // and a one-handed one, and from behind it is most of what tells them apart.
        PoseTheSwingArms(Forward(armForward), Forward(elbowForward),
            Forward(armForward * 0.8f), Forward(elbowForward * 0.8f));
    }

    // The Warden's Edge: a flat horizontal sweep, and on a heavy the full turn.
    //
    // This is the one shape that CANNOT be made with the arms. They have a pitch axis and
    // nothing else - there is no way to carry a hand out to the side - so a horizontal arc
    // has to come from the body turning underneath a held arm. Which is how a real sweep
    // works anyway: the arm is pinned out at shoulder height for the whole strike and the
    // hips, shoulders and torso rotate through beneath it, so the blade traces a flat
    // circle instead of a wheel.
    //
    // It is also the only pose in this file that writes hipsYaw, and that is the trick.
    // The hips carry the travel; the shoulders lead into it and lag out of it; and the
    // weapon hangs off a hand that hangs off the hips, so it comes round for free.
    private void PoseTheSweep(float through)
    {
        float windUntil = swingIsHeavy ? 0.32f : 0.26f;
        float sweepUntil = swingIsHeavy ? 0.68f : 0.58f;

        float spin = swingIsHeavy ? sweepHeavySpinDegrees : sweepSpinDegrees;

        // A full turn finishes facing the way it started rather than unwinding back, and
        // that is the whole difference between a spin and a swipe. Anything short of a
        // full turn has to come back to nought, or the swing would end with the body
        // pointing somewhere the player did not choose - and then snap straight when the
        // animation stopped.
        bool goesAllTheWayRound = spin > 270f;
        float settleAt = goesAllTheWayRound ? spin : 0f;

        float bodyYaw;
        float shoulderLead;
        float rollInto;
        float hipDrop;

        if (through < windUntil)
        {
            // Coil the other way. On a full turn this is the step that makes the spin
            // possible rather than merely decorative.
            float into = through / windUntil;
            float eased = into * into;

            bodyYaw = Mathf.Lerp(0f, -sweepWindUpDegrees, eased);
            shoulderLead = Mathf.Lerp(0f, -sweepShoulderLeadDegrees, eased);
            rollInto = 0f;
            hipDrop = 0f;
        }
        else if (through < sweepUntil)
        {
            // Round. The shoulders start behind the hips and finish ahead of them.
            float into = (through - windUntil) / (sweepUntil - windUntil);
            float eased = into * into;

            bodyYaw = Mathf.Lerp(-sweepWindUpDegrees, spin - sweepWindUpDegrees, eased);
            shoulderLead = Mathf.Lerp(
                -sweepShoulderLeadDegrees, sweepShoulderLeadDegrees, eased);
            rollInto = sweepLeanDegrees * eased;
            hipDrop = sweepHipDropMetres * eased;
        }
        else
        {
            float into = (through - sweepUntil) / (1f - sweepUntil);
            float eased = 1f - (1f - into) * (1f - into);

            bodyYaw = Mathf.Lerp(spin - sweepWindUpDegrees, settleAt, eased);
            shoulderLead = Mathf.Lerp(sweepShoulderLeadDegrees, 0f, eased);
            rollInto = sweepLeanDegrees * (1f - eased);
            hipDrop = sweepHipDropMetres * (1f - eased);
        }

        hipsYaw = hipsYaw + bodyYaw;
        shouldersYaw = shouldersYaw + shoulderLead;
        headYaw = headYaw + shoulderLead * 0.5f;
        torsoRoll = torsoRoll + rollInto;
        hipsOffset.y = hipsOffset.y - hipDrop;

        // The arm rises into the sweep and lowers out of it, and is held level in
        // between. Nothing else about it moves - all of the travel is underneath.
        float held = Mathf.Sin(through * Mathf.PI);
        float armForward = sweepArmOutDegrees * held;
        float elbowForward = -sweepElbowDegrees * held;

        // The off arm goes out too. A turn with one arm tucked in reads as a stumble.
        PoseTheSwingArms(Forward(armForward), Forward(elbowForward),
            Forward(armForward * 0.55f), Forward(elbowForward * 0.5f));
    }

    // Every swing shape drives the same two arms and has to know which is which. Written
    // once here rather than three times, because the only thing that differs between the
    // shapes is the angles - and a copy of this inside each of them would be three places
    // to get the hand wrong in.
    //
    // Lerped rather than added, because a swinging arm has to override the walk swing
    // completely. Adding to it would leave the sword drifting with the player's steps.
    private void PoseTheSwingArms(float weaponArm, float weaponForearm,
        float offArm, float offForearm)
    {
        const float HowMuchItOverridesTheWalk = 0.9f;

        // The weapon is in the same hand the creatures carry theirs in - see the note on
        // weaponIsInTheArmNamedLeft.
        if (weaponIsInTheArmNamedLeft)
        {
            upperArmLeftPitch = Mathf.Lerp(
                upperArmLeftPitch, weaponArm, HowMuchItOverridesTheWalk);
            forearmLeftPitch = Mathf.Lerp(
                forearmLeftPitch, weaponForearm, HowMuchItOverridesTheWalk);
            upperArmRightPitch = Mathf.Lerp(
                upperArmRightPitch, offArm, HowMuchItOverridesTheWalk);
            forearmRightPitch = Mathf.Lerp(
                forearmRightPitch, offForearm, HowMuchItOverridesTheWalk);
        }
        else
        {
            upperArmRightPitch = Mathf.Lerp(
                upperArmRightPitch, weaponArm, HowMuchItOverridesTheWalk);
            forearmRightPitch = Mathf.Lerp(
                forearmRightPitch, weaponForearm, HowMuchItOverridesTheWalk);
            upperArmLeftPitch = Mathf.Lerp(
                upperArmLeftPitch, offArm, HowMuchItOverridesTheWalk);
            forearmLeftPitch = Mathf.Lerp(
                forearmLeftPitch, offForearm, HowMuchItOverridesTheWalk);
        }
    }

    // Both knees, for the shapes that put weight into the floor. The shin folds back
    // further than the thigh comes forward, which is what makes it a crouch rather than
    // a sit.
    private void BendTheKnees(float degrees)
    {
        if (degrees <= 0f)
        {
            return;
        }

        thighLeftPitch = thighLeftPitch + Forward(degrees);
        thighRightPitch = thighRightPitch + Forward(degrees);
        shinLeftPitch = shinLeftPitch + Forward(-degrees * 1.6f);
        shinRightPitch = shinRightPitch + Forward(-degrees * 1.6f);
    }

    // The draw.
    //
    // The one thing this pose has to communicate is that holding the button longer pulls
    // the string further, because that is the only way the player can tell what their
    // shot is worth without reading the HUD. So every angle that moves is driven off
    // bowDrawAmount directly rather than off a timer: the pose is a POSITION on the pull,
    // not a sequence, and it tracks the draw backwards just as happily as forwards.
    //
    // That matters more than it sounds. The draw stalls whenever the previous shot is
    // still being recovered from, and it stops dead the moment the player runs out of
    // stamina. A pose built as a timed sequence would carry on regardless and show a
    // string coming back that gameplay says is not moving.
    // Where the bow arm is held, which depends on who is looking at it.
    //
    // Both the draw and the loose ask this rather than reading the field directly, so the
    // recoil kick lands on whichever pose the arm is actually in. Reading the third-person
    // angle in the loose while the draw used the first-person one would fire the bow arm
    // 32 degrees downwards on every shot taken in first person.
    private float BowArmForwardDegreesNow()
    {
        if (bowIsSightedFromTheEye == true)
        {
            return bowArmForwardFromTheEyeDegrees;
        }

        return bowArmForwardDegrees;
    }

    private void PoseTheBowDraw()
    {
        if (bowReadyAmount <= 0f)
        {
            return;
        }

        float draw = bowDrawAmount;

        // Which way round the body is working. The bow is in the same hand every other
        // weapon is in; the string is pulled by the other one.
        float handSign = weaponIsInTheArmNamedLeft ? 1f : -1f;

        // The last stretch of the pull, where the string starts to win. Nought until the
        // draw is nearly full, then climbing to one at maximum.
        float strain = Mathf.InverseLerp(bowTrembleStartsAtDraw, 1f, draw);
        float tremble = Mathf.Sin(Time.time * bowTrembleSpeed) * bowTrembleDegrees * strain;

        // The bow arm. Held out at the target and braced there for the whole draw - it
        // is the drawing arm that moves, which is what makes the pull read as a pull
        // rather than as both arms drifting apart.
        float bowArm = Forward(BowArmForwardDegreesNow());
        float bowElbow = Forward(-bowArmElbowDegrees) + Forward(tremble * 0.4f);

        // The drawing arm, all the way from resting on the string to anchored. This
        // interpolation IS the answer to "the longer it is the farther it pulls".
        float drawArm = Forward(Mathf.Lerp(
            drawArmForwardAtRestDegrees, drawArmForwardAtFullDegrees, draw));
        float drawElbow = Forward(-Mathf.Lerp(
            drawArmElbowAtRestDegrees, drawArmElbowAtFullDegrees, draw)) + Forward(tremble);

        // Faded in by how far the bow is up, so bringing it to bear and lowering it again
        // both happen over the blend rather than on one frame. The arms are LERPED to
        // rather than added to, because an aiming arm has to override the walk swing
        // completely - an archer whose arms swing along with their steps is not aiming.
        if (weaponIsInTheArmNamedLeft)
        {
            upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, bowArm, bowReadyAmount);
            forearmLeftPitch = Mathf.Lerp(forearmLeftPitch, bowElbow, bowReadyAmount);
            upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, drawArm, bowReadyAmount);
            forearmRightPitch = Mathf.Lerp(forearmRightPitch, drawElbow, bowReadyAmount);
        }
        else
        {
            upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, bowArm, bowReadyAmount);
            forearmRightPitch = Mathf.Lerp(forearmRightPitch, bowElbow, bowReadyAmount);
            upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, drawArm, bowReadyAmount);
            forearmLeftPitch = Mathf.Lerp(forearmLeftPitch, drawElbow, bowReadyAmount);
        }

        // The rest of the body leans into the pull, and all of it scales with the draw
        // rather than with the bow merely being up. A bow held at rest is a stance; a
        // bow at full draw is an effort, and the difference should be visible from the
        // shoulders and hips as well as from the arms.
        float effort = bowReadyAmount * draw;

        shouldersYaw = shouldersYaw + bowShouldersYawDegrees * handSign * effort;
        headYaw = headYaw + bowHeadYawDegrees * handSign * effort;
        headPitch = headPitch + Forward(bowHeadDipDegrees) * effort;
        torsoPitch = torsoPitch + Forward(-bowLeanBackDegrees) * effort;
        hipsOffset.y = hipsOffset.y - bowHipSettleMetres * effort;
    }

    // The loose, which is over almost before it starts.
    //
    // It runs AFTER the draw pose and lerps over the top of it, which is what covers the
    // one frame where the draw collapses from full to nothing because the button came
    // up. Without something laid over that frame the drawing arm would snap straight
    // from anchored to resting, and the shot would read as the animation glitching
    // rather than as an arrow leaving.
    private void PoseTheBowLoose()
    {
        if (looseProgress < 0f)
        {
            return;
        }

        float through = Mathf.Clamp01(looseProgress);

        // Biggest at the instant the string goes and gone by the end. Squared so it
        // falls away quickly at first and then settles, the way a hand that has just
        // lost the thing it was pulling against actually behaves.
        float kick = (1f - through) * (1f - through) * looseWasDrawnTo;

        // Where the drawing hand was when the arrow left, and where it flies to: back
        // past the anchor, with the elbow springing open as the hand goes.
        float anchoredArm = Mathf.Lerp(
            drawArmForwardAtRestDegrees, drawArmForwardAtFullDegrees, looseWasDrawnTo);
        float anchoredElbow = Mathf.Lerp(
            drawArmElbowAtRestDegrees, drawArmElbowAtFullDegrees, looseWasDrawnTo);

        float snappedArm = Forward(anchoredArm - looseHandSnapDegrees * 0.5f);
        float snappedElbow = Forward(-(anchoredElbow + looseHandSnapDegrees));

        // The bow arm kicks back against the release and comes straight again.
        float kickedBowArm = Forward(BowArmForwardDegreesNow() - looseBowArmKickDegrees);

        if (weaponIsInTheArmNamedLeft)
        {
            upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, kickedBowArm, kick);
            upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, snappedArm, kick);
            forearmRightPitch = Mathf.Lerp(forearmRightPitch, snappedElbow, kick);
        }
        else
        {
            upperArmRightPitch = Mathf.Lerp(upperArmRightPitch, kickedBowArm, kick);
            upperArmLeftPitch = Mathf.Lerp(upperArmLeftPitch, snappedArm, kick);
            forearmLeftPitch = Mathf.Lerp(forearmLeftPitch, snappedElbow, kick);
        }

        // The shoulders unwind. They were held round by the draw and the draw has gone.
        float handSign = weaponIsInTheArmNamedLeft ? 1f : -1f;
        shouldersYaw = shouldersYaw + bowShouldersYawDegrees * handSign * kick * 0.5f;
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
        positionLastFrame = WhereTheBodyActuallyIs();
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
