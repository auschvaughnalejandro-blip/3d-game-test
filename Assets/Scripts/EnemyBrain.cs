using UnityEngine;
using UnityEngine.AI;

// THE ONE ENEMY SCRIPT.
//
// The Grunt, the Darter and the Warden are all this exact script. Nothing about any of
// them is written in code - they differ only by the numbers and the attack shape chosen
// below. That is the whole argument: new content is new data, not new code.
//
// The three attack shapes matter as much as the numbers do. An enemy that walks up and
// touches you is the same enemy no matter what its health is. An enemy that chops a cone
// in front of itself, or charges down a line, or slams a circle around itself, is a
// different fight - because each one is escaped in a different direction.
public class EnemyBrain : MonoBehaviour
{
    // ------------------------------------------------------------------------
    // The three shapes of attack
    // ------------------------------------------------------------------------

    // Chops a wedge directly in front. Escaped by getting BEHIND it.
    public const int AttackShapeSweep = 0;
    // Charges in a straight line through where the player was standing. Escaped by
    // stepping SIDEWAYS out of the line.
    public const int AttackShapeLunge = 1;
    // Slams a full circle centred on itself. Escaped by getting FAR AWAY.
    public const int AttackShapeSlam = 2;
    // Throws a rock from a distance. Escaped by putting something SOLID in between -
    // this is the shape that makes cover matter, because it is the only one that does
    // not need to reach the player at all.
    public const int AttackShapeRanged = 3;

    // ------------------------------------------------------------------------
    // What this particular enemy is
    // ------------------------------------------------------------------------

    [Header("Identity")]
    public string displayName = "Grunt";

    // Which set of creature sounds this body speaks with. "Grunt", "Darter", "Spitter"
    // or "Warden" - GameSound.PlayCreature glues this to what just happened and asks
    // for, say, "SpitterHurt".
    //
    // Separate from displayName on purpose. displayName is shown to the player and is
    // free to be prose - the boss's is "The Warden" - and hanging file lookups off a
    // string that exists to be read is how a rename silently turns a creature mute.
    public string soundVoice = "Grunt";
    public bool isTheWarden = false;
    public int attackShape = AttackShapeSweep;

    // Roughly how big this creature is, in metres. Used to scale the death burst.
    // It used to be read from the transform's scale, but the bodies are real models
    // now and all sit at scale one, so the size has to be stated rather than inferred.
    public float bodySize = 1f;

    [Header("Senses")]
    // Outside this distance the enemy idles. Inside it, it commits.
    public float detectionRadius = 14f;
    // Once provoked it chases past its detection radius up to here before giving up.
    // Without this, enemies flicker on and off at the exact boundary.
    public float loseInterestRadius = 22f;

    [Header("Movement")]
    public float moveSpeed = 2.4f;
    public float turningSpeed = 8f;

    // How much of a hit's shove this creature actually takes, nought to one.
    //
    // Everything used to take the full seven metres a second, the Warden included, and
    // that was quietly one of the worst problems in the boss fight. Each arrow shoved it
    // roughly nine-tenths of a metre, and a bow fired fast enough pushed a creature that
    // walks at 1.9 m/s backwards faster than it could walk forwards - so a player who
    // kept shooting could never be reached at all. A four metre stone golem should not
    // be moved by an arrow.
    //
    // Left at one for every ordinary creature, so nothing else in the game changes.
    public float knockbackTaken = 1f;

    // Killing this one builds no kill streak.
    //
    // Set on the creatures the Warden summons mid-fight, and set for a reason that was a
    // real hole in the boss: killing summons fed the Surge, the Surge cuts attack timings
    // to 1/1.8, and the player came out of it hitting far harder than before. Phase three
    // was ARMING the player - the boss's own hardest move was the best thing that could
    // happen to them. Summons are pressure now and nothing else.
    public bool killingThisBuildsNoStreak = false;

    // The boss script, when this creature happens to be the Warden. Null for everything
    // else, and asked only when an arrow lands.
    private WardenBoss theBossRidingAlong;

    [Header("Attack timing")]
    // How close it has to be before it will start an attack.
    public float attackRange = 2.4f;
    public float secondsBetweenAttacks = 2.0f;
    // The telegraph. The enemy commits to a visible pose for this long before the blow
    // lands, which is what gives the player a fair window to get out of the way.
    public float windUpSeconds = 0.6f;
    // How long the blow itself takes.
    public float strikeSeconds = 0.25f;
    // How far into the strike the damage actually happens.
    public float damageLandsAfterSeconds = 0.08f;

    [Header("Attack shape sizes")]
    // Sweep: how wide the wedge in front is, in degrees either side of straight ahead.
    public float sweepHalfAngleDegrees = 65f;
    // Lunge: how fast and how far the charge travels.
    public float lungeSpeed = 22f;
    // Lunge: it needs runway, so it will not start a charge closer than this.
    public float lungeMinimumRange = 4f;
    public float lungeMaximumRange = 11f;
    // Slam: the radius of the circle, which is also the size of the warning ring.
    public float slamRadius = 5.5f;

    [Header("Ranged")]
    // The band it tries to hold. Closer than the minimum and it backs off; further than
    // the maximum and it walks in. Standing still and shooting would make it a turret.
    public float preferredRangeMinimum = 6f;
    public float preferredRangeMaximum = 14f;
    public float projectileSpeed = 14f;
    // Where the rock leaves from, measured up from the feet.
    public float throwHeight = 1.2f;

    [Header("Behaviour flavour")]
    public bool retreatsAfterAttacking = false;
    public float retreatSeconds = 1.1f;
    public float retreatSpeedMultiplier = 1.4f;

    [Header("Telegraph parts - filled in by ValleyBuilder")]
    // The whole body, as a child of the object that does the walking and turning.
    // Leaning THIS rather than the root is what lets the creature arch backwards and
    // fold forwards without disturbing which way it is facing or where it stands.
    public Transform bodyTransform;
    // How far the body arches back at the peak of a wind-up, in degrees.
    public float windUpLeanDegrees = 26f;
    // How far it folds forward at the moment the blow lands.
    public float strikeLeanDegrees = -38f;

    // The pivot the club hangs from. Rotating it swings the weapon.
    public Transform weaponPivot;
    // The flat disc on the ground that shows where a slam is about to land.
    public Transform dangerRing;

    // ------------------------------------------------------------------------
    // Internal state
    // ------------------------------------------------------------------------

    private const int StateIdle = 0;
    private const int StateChasing = 1;
    private const int StateWindingUp = 2;
    private const int StateStriking = 3;
    private const int StateRecovering = 4;

    private int currentState = StateIdle;

    private CharacterStats ownStats;
    private CharacterController bodyController;

    // Works out WHICH WAY to walk when the straight line is blocked. It never moves the
    // creature - the character controller does all of that, exactly as before.
    private NavMeshAgent pathAgent;
    private float nextRepathAt = 0f;

    // Four times a second. A route that is a quarter of a second stale is wrong by
    // centimetres, and recomputing every frame for thirteen enemies is waste.
    private const float SecondsBetweenRepaths = 0.25f;
    private Transform thePlayer;
    private CharacterStats playerStats;

    private Vector3 startingPosition;

    // How many times this creature has had to be pulled back out of the void. More than a
    // couple means the place it keeps being put is itself the problem.
    private int timesRescued = 0;
    private Quaternion startingRotation;
    private Vector3 originalScale;

    private float stateSecondsRemaining = 0f;
    private float attackCooldownRemaining = 0f;
    private float retreatSecondsRemaining = 0f;
    private bool hasBeenProvoked = false;

    // Set once per strike so a single swing cannot damage the player twice.
    private bool damageDealtThisStrike = false;
    // The direction a lunge committed to when it started. The charge does NOT home in
    // on the player once it is moving, which is precisely what makes sidestepping work.
    private Vector3 lungeDirection = Vector3.zero;

    private Vector3 knockbackVelocity = Vector3.zero;
    private float verticalSpeed = 0f;

    private Renderer dangerRingRenderer;

    // Where the body sits when nothing is happening. Leans and drops are measured from
    // here, so a creature always returns to its own resting height rather than to zero.
    //
    // Set by ValleyBuilder rather than discovered in Awake. Awake does not run in the
    // editor, so a value read there is zero whenever the scene is only being looked at,
    // and every pose preview yanked the body a metre into the air.
    public float restingBodyHeight = 0f;

    // How long this creature has been off the walkable map. Counted rather than acted on
    // immediately, so a creature thrown clear by a hammer is not mistaken for the bug.
    private float secondsSpentUnderTheGround = 0f;

    // Stops one death being announced twice.
    private bool hasAlreadyDied = false;

    // The limb animator on the model, when this creature has a segmented one. Null for
    // every creature still using a single-mesh model, so every call to it is guarded.
    //
    // Found the first time it is wanted rather than in Awake, and this timing is not
    // negotiable. ValleyBuilder adds this brain to the creature and only THEN hands it
    // bodyTransform - and for an enemy spawned in the middle of a round rather than
    // baked into the scene, AddComponent has already run Awake by that point, with
    // bodyTransform still null. An Awake lookup finds nothing for every enemy in every
    // round. Looking it up on demand is the only timing that works for both.
    private ProceduralAnimator limbs;
    private bool haveLookedForTheLimbs = false;

    // How long this creature has been lying dead, and how long it is allowed to lie
    // there before it is switched off.
    //
    // A creature with no limb animator has nothing to collapse, so it keeps the old
    // behaviour exactly: the burst goes off and the body is gone the same frame.
    // Leaving a rigid single-mesh statue standing for a second after it burst would
    // look worse than popping it.
    private float secondsSpentDying = 0f;
    private float secondsAllowedToLieDying = 0f;

    void Awake()
    {
        ownStats = GetComponent<CharacterStats>();
        bodyController = GetComponent<CharacterController>();
        pathAgent = GetComponent<NavMeshAgent>();

        startingPosition = transform.position;
        startingRotation = transform.rotation;
        originalScale = transform.localScale;

        if (dangerRing != null)
        {
            dangerRingRenderer = dangerRing.GetComponent<Renderer>();
            dangerRing.gameObject.SetActive(false);
        }

        // Only fall back to reading it if nobody set it, so a hand-placed enemy still
        // behaves rather than snapping to zero.
        if (bodyTransform != null && restingBodyHeight == 0f)
        {
            restingBodyHeight = bodyTransform.localPosition.y;
        }
    }

    // The animator sits on the same object this brain leans - the model wrapper - which
    // is exactly what lets the two share a creature without fighting over a transform.
    // See ValleyBuilder.AttachSegmentedModel.
    private ProceduralAnimator TheLimbs()
    {
        if (haveLookedForTheLimbs == true)
        {
            return limbs;
        }

        // Deliberately does NOT mark the search as done. A creature whose body has not
        // been hung on it yet gets asked again next time rather than being written off.
        if (bodyTransform == null)
        {
            return null;
        }

        limbs = bodyTransform.GetComponent<ProceduralAnimator>();
        haveLookedForTheLimbs = true;
        return limbs;
    }

    void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            thePlayer = playerObject.transform;
            playerStats = playerObject.GetComponent<CharacterStats>();
        }

        // Null on every ordinary creature, and that is the whole test for "is this the
        // boss" as far as taking damage is concerned. Found in Start rather than Awake
        // because ValleyBuilder adds WardenBoss after the brain, so an Awake lookup would
        // reliably find nothing.
        theBossRidingAlong = GetComponent<WardenBoss>();

        StartPathfindingIfPossible();
    }

    // Turns the route planner on, but only once there is a map to plan on and this
    // creature is actually standing on it.
    //
    // Enabling an agent that is off the mesh logs an error and then silently refuses to
    // produce a path, which would look exactly like the walking-into-rocks bug it is
    // supposed to fix. Better to check, and fall back honestly if not.
    private void StartPathfindingIfPossible()
    {
        if (pathAgent == null || NavigationField.IsReady == false)
        {
            return;
        }

        Vector3 onTheMesh;
        if (NavigationField.TryFindNearbyPoint(transform.position, 4f, out onTheMesh) == false)
        {
            Debug.LogWarning(displayName + " spawned at " + transform.position
                + ", which is not near any walkable ground. It will walk straight at the "
                + "player instead of pathing.");
            return;
        }

        // The body is moved onto the mesh BEFORE the agent is switched on. An agent
        // enabled while standing off the mesh complains and then quietly refuses to plan
        // anything, which would look exactly like the walking-into-rocks bug this is
        // meant to fix. The correction is usually a few centimetres - the walkable
        // surface is the ground inset by the body radius, not the ground itself.
        onTheMesh.y = transform.position.y;

        bodyController.enabled = false;
        transform.position = onTheMesh;
        bodyController.enabled = true;

        pathAgent.enabled = true;
        pathAgent.nextPosition = transform.position;
    }

    void Update()
    {
        if (ownStats.isDead == true)
        {
            // Plenty of things can empty a creature's health: a swing, a fall, the boss
            // standing on it, the rescue above giving up on it, or a test driving its
            // stats directly. Only ONE of those routes ran Die(), so a creature killed
            // any other way used to die silently - no burst, no sound, no essence, and
            // in the Warden's case no eye left behind for the player to pick up.
            Die();
            KeepLyingThere();
            return;
        }

        if (thePlayer == null)
        {
            return;
        }

        if (attackCooldownRemaining > 0f)
        {
            attackCooldownRemaining = attackCooldownRemaining - Time.deltaTime;
        }

        // Another script is carrying this body this frame. Nothing below should move it,
        // and nothing below should decide it has fallen out of the world either - it is
        // deliberately in mid-air.
        if (ordinaryMovementIsSuspended == true)
        {
            return;
        }

        RescueIfFallenOutOfTheWorld();
        ApplyKnockbackAndGravity();

        // A dead player should not keep being chased and hit.
        if (playerStats != null && playerStats.isDead == true)
        {
            ReturnToRestingPose();
            currentState = StateIdle;
            hasBeenProvoked = false;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, thePlayer.position);

        // The route is kept current every frame, whatever this creature happens to be
        // doing, because the decision below is now made partly FROM the route and a stale
        // route would freeze that decision in place. Asking for a route only while
        // already walking was the whole bug: a creature that had decided it was close
        // enough to attack stopped refreshing its route, so the route never got the
        // chance to tell it that it was not close enough at all.
        KeepTheRouteFresh();

        if (currentState == StateWindingUp)
        {
            ContinueWindingUp(distanceToPlayer);
        }
        else if (currentState == StateStriking)
        {
            ContinueStriking(distanceToPlayer);
        }
        else if (currentState == StateRecovering)
        {
            ContinueRecovering();
        }
        else
        {
            DecideWhatToDo(distanceToPlayer);
        }
    }

    // ------------------------------------------------------------------------
    // Deciding
    // ------------------------------------------------------------------------

    private void DecideWhatToDo(float distanceToPlayer)
    {
        if (hasBeenProvoked == false && distanceToPlayer <= detectionRadius)
        {
            hasBeenProvoked = true;
        }
        else if (hasBeenProvoked == true && distanceToPlayer > loseInterestRadius)
        {
            hasBeenProvoked = false;
        }

        if (hasBeenProvoked == false)
        {
            ReturnToRestingPose();
            return;
        }

        TurnToFaceThePlayer();

        if (retreatSecondsRemaining > 0f)
        {
            ContinueRetreating();
            return;
        }

        // Everything from here down asks "how far away is the player" in the only sense
        // that matters to a creature deciding whether to swing: how far away it is TO
        // REACH. Noticing the player, above, deliberately still uses the straight line -
        // a creature should look up when someone walks past behind a rock. What it must
        // not do is attack the rock.
        //
        // Two separate things can put a wall between them, and both have to be asked:
        //   - a wall it cannot see through, which is the line of sight, and
        //   - a wall it can see over but not walk through, which is the long way round.
        // A chest-high slab is the second without being the first, which is exactly the
        // case that had them swinging at scenery.
        bool haveAClearShot = CanSeeThePlayer();
        float distanceToWalk = WalkingDistanceToThePlayer(distanceToPlayer);

        bool closeEnoughToAttack =
            haveAClearShot == true && PlayerIsInRangeToAttack(distanceToWalk) == true;

        if (attackCooldownRemaining <= 0f && closeEnoughToAttack == true)
        {
            BeginWindUp();
            return;
        }

        // The Darter backs off when it is too close to get a run-up, rather than standing
        // in the player's face doing nothing. Backing off from someone who is on the far
        // side of a wall is nonsense, though, so this only applies once there is a shot.
        if (attackShape == AttackShapeLunge
            && haveAClearShot == true
            && distanceToWalk < lungeMinimumRange)
        {
            BackAwayToGetRunway();
            return;
        }

        // A thrower that stands its ground while being charged is free damage. Backing
        // off is what forces the player to actually chase it down.
        if (attackShape == AttackShapeRanged)
        {
            // Cover is THE answer to a thrower - it is the whole reason this attack shape
            // exists. That means nothing unless a thrower with no shot goes and gets one
            // instead of standing behind a rock lobbing stones into it.
            if (haveAClearShot == false)
            {
                WalkTowardThePlayer();
            }
            else if (distanceToWalk < preferredRangeMinimum)
            {
                BackAwayToGetRunway();
            }
            else if (distanceToWalk > preferredRangeMaximum)
            {
                WalkTowardThePlayer();
            }
            return;
        }

        if (distanceToWalk > attackRange || haveAClearShot == false)
        {
            WalkTowardThePlayer();
        }
    }

    private bool PlayerIsInRangeToAttack(float distanceToPlayer)
    {
        if (attackShape == AttackShapeLunge)
        {
            // A charge needs runway. Too close and there is nothing to charge across.
            return distanceToPlayer >= lungeMinimumRange && distanceToPlayer <= lungeMaximumRange;
        }
        if (attackShape == AttackShapeSlam)
        {
            return distanceToPlayer <= slamRadius * 0.8f;
        }
        if (attackShape == AttackShapeRanged)
        {
            return distanceToPlayer >= preferredRangeMinimum
                && distanceToPlayer <= preferredRangeMaximum;
        }
        return distanceToPlayer <= attackRange;
    }

    // ------------------------------------------------------------------------
    // Winding up - the telegraph
    // ------------------------------------------------------------------------

    private void BeginWindUp()
    {
        currentState = StateWindingUp;
        stateSecondsRemaining = windUpSeconds;
        damageDealtThisStrike = false;

        // The creature announces itself BEFORE the blow, which is the whole point of a
        // telegraph. Until now the wind-up was visual only, so an attack starting behind
        // the player or off the edge of the screen had no tell at all and simply landed.
        // Everything else about these attacks is built to be fair; this is what makes
        // that fairness reach a player who is not looking at the attacker.
        GameSound.PlayCreature(soundVoice, "WindUp", transform.position, 0.75f);

        if (attackShape == AttackShapeSlam && dangerRing != null)
        {
            dangerRing.gameObject.SetActive(true);
        }
    }

    private void ContinueWindingUp(float distanceToPlayer)
    {
        stateSecondsRemaining = stateSecondsRemaining - Time.deltaTime;

        // Zero at the start of the wind-up, one at the moment it finishes.
        float howFarThrough = 1f - (stateSecondsRemaining / windUpSeconds);
        if (howFarThrough < 0f)
        {
            howFarThrough = 0f;
        }

        ShowWindUpPose(howFarThrough);

        // A charging enemy aims during the wind-up but not after, so the player can read
        // where it is about to go and step out of that line.
        if (attackShape != AttackShapeSlam)
        {
            TurnToFaceThePlayer();
        }

        if (stateSecondsRemaining > 0f)
        {
            return;
        }

        BeginStrike();
    }

    public void ShowWindUpPose(float howFarThrough)
    {
        LeanTheBodyForWindUp(howFarThrough);

        if (attackShape == AttackShapeSweep)
        {
            // The club rises overhead, then HANGS there for the last stretch of the
            // wind-up. A weapon that travels at constant speed and stops dead reads as
            // a windscreen wiper; one that slows as it reaches the top and hangs there
            // reads as something heavy being hauled up against its own weight.
            if (weaponPivot != null)
            {
                weaponPivot.localRotation = Quaternion.Euler(
                    RaiseAngleForWindUp(howFarThrough), 0f, 0f);
            }
        }
        else if (attackShape == AttackShapeSlam)
        {
            // The ring on the ground grows to exactly the size of the area that is
            // about to be hit. What you see is what will hurt you.
            {
                // The ring is a child of a scaled body, so its own scale has to be
                // divided by the body scale to end up the right size in the world.
                float wantedWorldDiameter = slamRadius * 2f * howFarThrough;
                float localSize = wantedWorldDiameter / originalScale.x;
                dangerRing.localScale = new Vector3(localSize, 0.02f, localSize);

                PaintTheDangerRing(howFarThrough);
            }
        }
    }

    // The body arching backwards as the blow is loaded, and sinking slightly on its
    // legs. This is the part that was missing entirely: only the club used to move, and
    // a weapon swinging on a body that stands perfectly still reads as a machine.
    private void LeanTheBodyForWindUp(float howFarThrough)
    {
        if (bodyTransform == null)
        {
            return;
        }

        // Dip forward first, then arch back. Going the wrong way before the right way
        // is the single biggest thing separating a heavy motion from a mechanical one.
        float lean;
        float sink;
        if (howFarThrough < 0.18f)
        {
            float intoTheDip = howFarThrough / 0.18f;
            lean = Mathf.Lerp(0f, -7f, intoTheDip);
            sink = Mathf.Lerp(0f, -0.06f, intoTheDip);
        }
        else
        {
            float afterTheDip = (howFarThrough - 0.18f) / 0.82f;
            float eased = 1f - (1f - afterTheDip) * (1f - afterTheDip);
            lean = Mathf.Lerp(-7f, windUpLeanDegrees, eased);
            sink = Mathf.Lerp(-0.06f, 0.04f, eased);
        }

        bodyTransform.localRotation = Quaternion.Euler(lean, 0f, 0f);
        bodyTransform.localPosition = new Vector3(0f, restingBodyHeight + sink, 0f);

        // The root leaning is the whole creature tipping. The shoulders hauling the
        // weapon up are a separate layer inside it, and this is where the animator is
        // told how far through that haul we are. Hooked here rather than in each attack
        // shape because every shape - sweep, slam and lunge - comes through this one
        // method with the phase already worked out.
        ProceduralAnimator animator = TheLimbs();
        if (animator != null)
        {
            animator.ShowWindUp(howFarThrough);
        }
    }

    // The body folding forward with the blow, driving through it and then rebounding.
    private void LeanTheBodyForStrike(float howFarThrough)
    {
        if (bodyTransform == null)
        {
            return;
        }

        float lean;
        float drop;
        if (howFarThrough < 0.55f)
        {
            float intoTheSwing = howFarThrough / 0.55f;
            float eased = intoTheSwing * intoTheSwing;
            lean = Mathf.Lerp(windUpLeanDegrees, strikeLeanDegrees, eased);
            drop = Mathf.Lerp(0.04f, -0.16f, eased);
        }
        else
        {
            // The rebound. A body that stops dead on impact looks weightless.
            float intoTheSettle = (howFarThrough - 0.55f) / 0.45f;
            lean = Mathf.Lerp(strikeLeanDegrees, strikeLeanDegrees * 0.6f, intoTheSettle);
            drop = Mathf.Lerp(-0.16f, -0.05f, intoTheSettle);
        }

        bodyTransform.localRotation = Quaternion.Euler(lean, 0f, 0f);
        bodyTransform.localPosition = new Vector3(0f, restingBodyHeight + drop, 0f);

        ProceduralAnimator animator = TheLimbs();
        if (animator != null)
        {
            animator.ShowStrike(howFarThrough);
        }
    }

    // How far the weapon has been raised, as an angle, given how far through the
    // wind-up we are.
    //
    // Deliberately NOT a straight line. The first third is slow - the weight has to be
    // got moving - the middle is quick, and the last quarter barely moves at all so the
    // club hangs at the top before it comes down. That hang is what makes the strike
    // feel released rather than merely next.
    private float RaiseAngleForWindUp(float howFarThrough)
    {
        // A small dip BELOW rest before the lift. Anticipation: every heavy motion
        // starts by going the other way first.
        if (howFarThrough < 0.18f)
        {
            float intoTheDip = howFarThrough / 0.18f;
            return Mathf.Lerp(0f, 14f, intoTheDip);
        }

        float afterTheDip = (howFarThrough - 0.18f) / 0.82f;

        // Ease out: fast at first, settling as it reaches the top.
        float eased = 1f - (1f - afterTheDip) * (1f - afterTheDip);
        return Mathf.Lerp(14f, -128f, eased);
    }

    // The swing itself. Fast through the middle, and it overshoots the resting angle
    // before settling back, so the blow lands with weight instead of parking.
    private float SwingAngleForStrike(float howFarThrough)
    {
        if (howFarThrough < 0.55f)
        {
            // Ease IN hard: the club accelerates all the way down.
            float intoTheSwing = howFarThrough / 0.55f;
            float eased = intoTheSwing * intoTheSwing;
            return Mathf.Lerp(-128f, 52f, eased);
        }

        // Past the bottom it rebounds a little, the way a heavy weapon jars on impact.
        float intoTheSettle = (howFarThrough - 0.55f) / 0.45f;
        return Mathf.Lerp(52f, 30f, intoTheSettle);
    }

    // The warning ring is painted directly rather than left to the visual style, because
    // a telegraph that a player cannot see is a telegraph that does not exist. Gameplay
    // feedback stays readable no matter which lens is active.
    private void PaintTheDangerRing(float howFarThrough)
    {
        if (dangerRingRenderer == null)
        {
            return;
        }

        Color warmingUp = Color.Lerp(
            new Color(1f, 0.75f, 0.2f),
            new Color(1f, 0.15f, 0.1f),
            howFarThrough);

        dangerRingRenderer.material.color = warmingUp;
        if (dangerRingRenderer.material.HasProperty("_BaseColor") == true)
        {
            dangerRingRenderer.material.SetColor("_BaseColor", warmingUp);
        }
    }

    // ------------------------------------------------------------------------
    // Striking - the blow itself
    // ------------------------------------------------------------------------

    private void BeginStrike()
    {
        currentState = StateStriking;
        stateSecondsRemaining = strikeSeconds;
        damageDealtThisStrike = false;

        // The effort of the blow leaving, which is a different sound from it landing.
        // A ranged attacker is silent here - its noise belongs to the moment the rock
        // is actually released, which is in ThrowARock, not to the start of the throw.
        if (attackShape == AttackShapeSweep)
        {
            GameSound.PlayCreature(soundVoice, "Swing", transform.position, 0.55f);
        }
        else if (attackShape == AttackShapeLunge)
        {
            GameSound.PlayCreature(soundVoice, "Lunge", transform.position, 0.8f);
        }

        if (attackShape == AttackShapeLunge)
        {
            // The direction is locked in HERE, at the moment the charge begins. From now
            // on it does not steer, which is what makes sidestepping a real answer.
            Vector3 towardPlayer = thePlayer.position - transform.position;
            towardPlayer.y = 0f;
            lungeDirection = towardPlayer.normalized;
        }
    }

    private void ContinueStriking(float distanceToPlayer)
    {
        stateSecondsRemaining = stateSecondsRemaining - Time.deltaTime;
        float secondsIntoStrike = strikeSeconds - stateSecondsRemaining;

        if (attackShape == AttackShapeSweep)
        {
            ShowTheChop(secondsIntoStrike);
        }
        else if (attackShape == AttackShapeSlam)
        {
            float howFarThroughSlam = secondsIntoStrike / strikeSeconds;
            if (howFarThroughSlam > 1f)
            {
                howFarThroughSlam = 1f;
            }
            ShowTheSlam(howFarThroughSlam);
        }
        else if (attackShape == AttackShapeLunge)
        {
            // The charge is the attack. Moving and hitting are the same act.
            bodyController.Move(lungeDirection * lungeSpeed * Time.deltaTime);
        }

        // A lunge is deliberately excluded here. Its damage happens on contact during
        // the charge, not at a fixed moment - and LandTheBlow burns the
        // damageDealtThisStrike flag the instant it runs, which used to make the
        // charge silently harmless: the flag was already spent by the time the Darter
        // actually reached the player.
        if (attackShape != AttackShapeLunge
            && damageDealtThisStrike == false
            && secondsIntoStrike >= damageLandsAfterSeconds)
        {
            LandTheBlow(distanceToPlayer);
        }

        // Contact is also checked by distance rather than relying only on the physics
        // callback. Two character controllers grazing each other at charge speed do not
        // reliably report a collision, and a charge that passes through the player
        // without touching them is worse than one that hits slightly early.
        if (attackShape == AttackShapeLunge
            && damageDealtThisStrike == false
            && distanceToPlayer <= attackRange)
        {
            damageDealtThisStrike = true;
            HurtThePlayer();
        }

        if (stateSecondsRemaining > 0f)
        {
            return;
        }

        BeginRecovery();
    }

    public void ShowTheChop(float secondsIntoStrike)
    {
        float howFarThroughChop = secondsIntoStrike / strikeSeconds;
        if (howFarThroughChop > 1f)
        {
            howFarThroughChop = 1f;
        }

        // Club and body drive through together. Both accelerate into the blow and both
        // rebound off it, which is what makes the two read as one motion rather than a
        // weapon being waved by a statue.
        LeanTheBodyForStrike(howFarThroughChop);

        if (weaponPivot != null)
        {
            weaponPivot.localRotation = Quaternion.Euler(
                SwingAngleForStrike(howFarThroughChop), 0f, 0f);
        }
    }

    // The Warden has no weapon, so its slam is carried entirely by the body: rearing
    // back and up through the wind-up, then driving down.
    private void ShowTheSlam(float howFarThrough)
    {
        LeanTheBodyForStrike(howFarThrough);
    }

    private void LandTheBlow(float distanceToPlayer)
    {
        damageDealtThisStrike = true;

        bool theBlowConnected = false;

        if (attackShape == AttackShapeSweep)
        {
            theBlowConnected = PlayerIsInsideTheWedge();
        }
        else if (attackShape == AttackShapeSlam)
        {
            theBlowConnected = distanceToPlayer <= slamRadius;
            FlashTheDangerRing();

            // The slam lands whether or not it caught anybody - the ground was still
            // hit. Tying this to theBlowConnected would make a dodged slam silent, and a
            // move the player successfully escaped would give no confirmation that they
            // had escaped anything.
            GameSound.PlayCreature(soundVoice, "Impact", transform.position, 1f);
        }
        else if (attackShape == AttackShapeRanged)
        {
            ThrowARock();
            // Nothing connects here. The rock decides that for itself on the way over,
            // which is exactly why it can be dodged and blocked.
            theBlowConnected = false;
        }

        if (theBlowConnected == true)
        {
            HurtThePlayer();
        }
    }

    // Aimed at where the player IS, deliberately not where they are going. Leading the
    // target would make the rock unavoidable; aiming at the current position means
    // simply moving beats it, and standing still does not.
    private void ThrowARock()
    {
        if (thePlayer == null)
        {
            return;
        }

        Vector3 from = transform.position + Vector3.up * (throwHeight - bodySize);
        Vector3 aimAt = thePlayer.position + Vector3.up * 0.6f;
        Vector3 towards = aimAt - from;

        GameSound.PlayCreature(soundVoice, "Throw", transform.position, 0.7f);
        Projectile.Fire(from, towards, projectileSpeed, ownStats.attackDamage, gameObject);
    }

    private bool PlayerIsInsideTheWedge()
    {
        Vector3 towardPlayer = thePlayer.position - transform.position;
        towardPlayer.y = 0f;

        if (towardPlayer.magnitude > attackRange * 1.25f)
        {
            return false;
        }

        // Only counts if the player is actually in front. Getting behind a Grunt is a
        // real and readable answer to it.
        float angleToPlayer = Vector3.Angle(transform.forward, towardPlayer);
        return angleToPlayer <= sweepHalfAngleDegrees;
    }

    private void FlashTheDangerRing()
    {
        if (dangerRingRenderer == null)
        {
            return;
        }
        Color impact = new Color(1f, 1f, 1f);
        dangerRingRenderer.material.color = impact;
        if (dangerRingRenderer.material.HasProperty("_BaseColor") == true)
        {
            dangerRingRenderer.material.SetColor("_BaseColor", impact);
        }
    }

    // The lunge damages on contact during the charge rather than at a fixed moment, so
    // it hurts whoever it actually runs into.
    void OnControllerColliderHit(ControllerColliderHit whatWasHit)
    {
        if (currentState != StateStriking || attackShape != AttackShapeLunge)
        {
            return;
        }
        if (damageDealtThisStrike == true)
        {
            return;
        }
        if (whatWasHit.gameObject.CompareTag("Player") == false)
        {
            return;
        }

        damageDealtThisStrike = true;
        HurtThePlayer();
    }

    private void HurtThePlayer()
    {
        if (playerStats == null || playerStats.isDead == true)
        {
            return;
        }

        playerStats.TakeDamage(ownStats.attackDamage);

        // The mark this particular creature leaves behind - a Darter's bite bleeds, a
        // Grunt's club staggers. Which one is decided inside PlayerAilments off this
        // creature's displayName, so nothing here has to know what the effects are.
        //
        // Applied before the death check on purpose. If this blow killed the player the
        // ailment is cleared a frame later anyway, and putting it after would mean a
        // creature that lands a killing blow behaves differently from one that does not,
        // for no reason a player could ever see.
        PlayerAilments.ApplyForAttackerNamed(displayName);

        if (playerStats.isDead == true && GameDirector.instance != null)
        {
            GameDirector.instance.OnPlayerDied();
        }
    }

    // ------------------------------------------------------------------------
    // Recovering
    // ------------------------------------------------------------------------

    private void BeginRecovery()
    {
        currentState = StateRecovering;
        // The gap after a swing during which the enemy is helpless. This is the window
        // the player is meant to attack into.
        stateSecondsRemaining = 0.45f;
        attackCooldownRemaining = secondsBetweenAttacks;

        if (dangerRing != null)
        {
            dangerRing.gameObject.SetActive(false);
        }

        if (retreatsAfterAttacking == true)
        {
            retreatSecondsRemaining = retreatSeconds;
        }
    }

    private void ContinueRecovering()
    {
        stateSecondsRemaining = stateSecondsRemaining - Time.deltaTime;

        // Ease back out of the attack pose over the recovery.
        float howFarBack = 1f - (stateSecondsRemaining / 0.45f);
        if (howFarBack < 0f)
        {
            howFarBack = 0f;
        }
        if (howFarBack > 1f)
        {
            howFarBack = 1f;
        }

        // Straightening up is deliberately SLOW - much slower than the blow that got it
        // here. The gap between a fast strike and a slow recovery is what makes the
        // creature look like it has just spent effort, and it is also the window the
        // player is meant to attack into.
        if (bodyTransform != null)
        {
            bodyTransform.localRotation = Quaternion.Slerp(
                bodyTransform.localRotation, Quaternion.identity, howFarBack * 0.35f);
            bodyTransform.localPosition = Vector3.Lerp(
                bodyTransform.localPosition,
                new Vector3(0f, restingBodyHeight, 0f),
                howFarBack * 0.35f);
        }

        if (weaponPivot != null)
        {
            weaponPivot.localRotation = Quaternion.Slerp(
                weaponPivot.localRotation,
                Quaternion.identity,
                howFarBack * 0.4f);
        }

        if (stateSecondsRemaining <= 0f)
        {
            ReturnToRestingPose();
            currentState = StateChasing;
        }
    }

    // ------------------------------------------------------------------------
    // Moving
    // ------------------------------------------------------------------------

    private void WalkTowardThePlayer()
    {
        currentState = StateChasing;

        Vector3 whereToStep = DirectionOfTravelTowardThePlayer();

        // Face the way it is going, not the way the player is. Walking sideways around a
        // rock while staring straight through it looks like a bug even when the route is
        // perfect. Once it is close enough to attack it stops chasing and squares up to
        // the player again, which is where facing the target actually matters.
        TurnToward(whereToStep);

        // Only horizontal movement here. Falling is handled once per frame in
        // ApplyKnockbackAndGravity, so an enemy standing still still falls.
        Vector3 beforeTheStep = transform.position;
        bodyController.Move(whereToStep * moveSpeed * Time.deltaTime);
        CountOffTheFootfalls(transform.position - beforeTheStep);
    }

    // Footfalls, for the creatures heavy enough to earn them.
    //
    // Deliberately NOT every enemy. Thirteen creatures each placing footsteps turns a
    // fight into a rainstorm and buries the sounds that carry information - the wind-ups.
    // Only a body big enough that the ground would notice gets these, which at present
    // means the Warden and nothing else.
    private float metresSinceLastFootfall = 0f;

    private void CountOffTheFootfalls(Vector3 movedThisFrame)
    {
        if (IsMadeOfStone() == false)
        {
            return;
        }

        Vector3 alongTheGround = movedThisFrame;
        alongTheGround.y = 0f;

        metresSinceLastFootfall = metresSinceLastFootfall + alongTheGround.magnitude;

        // A far longer stride than the player's 0.91 m, because it is a far bigger
        // creature. Hearing the Warden walk is also a warning in its own right - it is
        // the only enemy the player is expected to keep track of without looking.
        if (metresSinceLastFootfall < 1.9f)
        {
            return;
        }

        metresSinceLastFootfall = 0f;
        GameSound.PlayCreature(soundVoice, "Step", transform.position, 0.7f);
    }

    // Which way to walk to get closer to the player, going around anything in the way.
    //
    // Straight at the player is only correct when nothing is between the two. Otherwise
    // the direction that actually shortens the journey is the direction of the first leg
    // of the route around the obstacle, which is what the agent computes. The maths is in
    // PATHFINDING_MATHS.md in the RPG Game docs folder - briefly, the route is the taut
    // the player, and this is the heading of its first straight run.
    //
    // Every failure falls back to walking straight at the player. That is the old,
    // stupid behaviour, but it is better than standing still, and it is loud about why
    // in the one case that indicates a broken setup.
    private Vector3 DirectionOfTravelTowardThePlayer()
    {
        Vector3 straightAtThePlayer = thePlayer.position - transform.position;
        straightAtThePlayer.y = 0f;
        straightAtThePlayer = straightAtThePlayer.normalized;

        if (pathAgent == null || pathAgent.enabled == false || pathAgent.isOnNavMesh == false)
        {
            return straightAtThePlayer;
        }

        // Where the body is, and where it is headed, are both kept current by
        // KeepTheRouteFresh, which runs every frame in Update no matter what state this
        // creature is in. It used to be done here, which meant it only happened while
        // the creature was already walking.
        if (pathAgent.pathPending == true)
        {
            return straightAtThePlayer;
        }

        // A partial path means the player is somewhere unreachable. Heading along it
        // anyway still closes the distance, which is the right thing to do.
        if (pathAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            return straightAtThePlayer;
        }

        Vector3 wanted = pathAgent.desiredVelocity;
        wanted.y = 0f;

        // Zero means it has arrived, or has nothing planned yet.
        if (wanted.sqrMagnitude < 0.0001f)
        {
            return straightAtThePlayer;
        }

        return wanted.normalized;
    }

    // Keeps the agent's idea of where this creature is standing, and where it is trying
    // to get to, up to date.
    //
    // Deliberately runs every frame in every state. The route is what the attack decision
    // is made from now, so a creature that has stopped walking still needs one - that is
    // precisely the moment it has to be told that the player it can see is fifteen metres
    // away round the side of a rock.
    //
    // The planning itself is still only four times a second. This is cheap on the frames
    // in between.
    private void KeepTheRouteFresh()
    {
        if (pathAgent == null || pathAgent.enabled == false || pathAgent.isOnNavMesh == false)
        {
            return;
        }

        // The agent is told where the body really is, because the body is being moved by
        // the character controller behind its back. Without this the agent believes it is
        // still standing where it started and plans from there.
        pathAgent.nextPosition = transform.position;

        if (Time.time >= nextRepathAt)
        {
            nextRepathAt = Time.time + SecondsBetweenRepaths;
            pathAgent.SetDestination(thePlayer.position);
        }
    }

    // How far this creature would actually have to WALK to reach the player.
    //
    // Straight-line distance stops being the distance the moment a wall is involved. A
    // creature on the far side of a slab is two metres from the player as the crow flies
    // and fifteen as it walks, and it is the fifteen that decides whether it is still
    // travelling or close enough to swing.
    //
    // Working it out is only adding up the legs of the route the navigation mesh has
    // already planned - the taut string of PATHFINDING_MATHS.md section 7, measured
    // rather than walked.
    //
    // Anything short of a complete route falls back to the straight line, on purpose.
    // A partial route means the player is standing somewhere the creature cannot reach
    // at all, and a creature that refuses to attack in that situation is a creature the
    // player can farm from on top of a rock. Falling back means the worst case is the
    // old behaviour rather than a new and much sillier one.
    private float WalkingDistanceToThePlayer(float straightLineDistance)
    {
        if (pathAgent == null || pathAgent.enabled == false || pathAgent.isOnNavMesh == false)
        {
            return straightLineDistance;
        }

        // A route still being worked out has no corners yet, which would measure as zero -
        // "already there" - the exact mistake this method exists to prevent.
        if (pathAgent.pathPending == true)
        {
            return straightLineDistance;
        }

        NavMeshPath route = pathAgent.path;
        if (route == null || route.status != NavMeshPathStatus.PathComplete)
        {
            return straightLineDistance;
        }

        Vector3[] corners = route.corners;
        if (corners.Length < 2)
        {
            return straightLineDistance;
        }

        float total = 0f;

        int index = 1;
        while (index < corners.Length)
        {
            total = total + Vector3.Distance(corners[index - 1], corners[index]);
            index = index + 1;
        }

        return total;
    }

    // Whether there is a clear line to swing or throw along.
    //
    // Nothing here can hit what it cannot see. The ranged shape is explicitly designed
    // around that - the comment at the top of this file says cover is how a thrower is
    // beaten - and that promise was never actually kept, because nothing ever checked.
    //
    // Measured centre to centre. Both bodies are character controllers centred on the
    // middle of the body, so this is chest to chest: a ray along it is stopped by a
    // chest-high slab, which is correct, and is not stopped by the ground underfoot,
    // which a foot-to-foot ray would be on every slope in the valley.
    //
    // Other creatures are not cover. A crowd between a spitter and the player would
    // otherwise switch the spitter off completely, and creatures shuffle about
    // constantly, so its shot would flicker on and off with them.
    private bool CanSeeThePlayer()
    {
        Vector3 eye = transform.position;
        Vector3 target = thePlayer.position;

        Vector3 towardTarget = target - eye;
        float howFar = towardTarget.magnitude;

        // Standing inside one another. There is nothing that could be in between.
        if (howFar < 0.05f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            eye, towardTarget / howFar, howFar, ~0, QueryTriggerInteraction.Ignore);

        int index = 0;
        while (index < hits.Length)
        {
            // Creatures are not scenery - not this one, not the player it is looking at,
            // and not whoever else is milling about between them.
            if (hits[index].collider.GetComponent<CharacterController>() == null)
            {
                return false;
            }

            index = index + 1;
        }

        return true;
    }

    private void BackAwayToGetRunway()
    {
        Vector3 directionAway = transform.position - thePlayer.position;
        directionAway.y = 0f;
        directionAway = directionAway.normalized;

        bodyController.Move(directionAway * moveSpeed * 0.8f * Time.deltaTime);
    }

    private void ContinueRetreating()
    {
        retreatSecondsRemaining = retreatSecondsRemaining - Time.deltaTime;

        Vector3 directionAway = transform.position - thePlayer.position;
        directionAway.y = 0f;
        directionAway = directionAway.normalized;

        bodyController.Move(directionAway * moveSpeed * retreatSpeedMultiplier * Time.deltaTime);
    }

    // A teleport has to be told to the route planner as well as done to the body.
    // Otherwise it carries on planning from wherever it last believed the creature was,
    // and hands back directions for a journey starting somewhere else entirely.
    private void WarpTheAgentTo(Vector3 where)
    {
        if (pathAgent == null || pathAgent.enabled == false)
        {
            return;
        }

        Vector3 onTheMesh;
        if (NavigationField.TryFindNearbyPoint(where, 6f, out onTheMesh) == true)
        {
            pathAgent.Warp(onTheMesh);
        }

        // Force a fresh route rather than finishing the old one from the new place.
        nextRepathAt = 0f;
    }

    private void TurnToFaceThePlayer()
    {
        TurnToward(thePlayer.position - transform.position);
    }

    private void TurnToward(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion wantedRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            wantedRotation,
            turningSpeed * Time.deltaTime);
    }

    private void ReturnToRestingPose()
    {
        transform.localScale = originalScale;

        // The arm does not snap back - the animator fades the swing out over a couple of
        // tenths, so the walk picks the shoulders back up smoothly.
        ProceduralAnimator animator = TheLimbs();
        if (animator != null)
        {
            animator.ClearAttack();
        }

        if (bodyTransform != null)
        {
            bodyTransform.localRotation = Quaternion.identity;
            bodyTransform.localPosition = new Vector3(0f, restingBodyHeight, 0f);
        }

        if (weaponPivot != null)
        {
            weaponPivot.localRotation = Quaternion.identity;
        }
        if (dangerRing != null && dangerRing.gameObject.activeSelf == true)
        {
            dangerRing.gameObject.SetActive(false);
        }
    }

    // Anything that ends up below the valley is put back where it started.
    //
    // A character controller shoved out of another body can pass through the terrain,
    // which is a one-sided mesh with nothing underneath it. The creature then falls for
    // ever, stays perfectly alive, and the round waits on it indefinitely. Catching it
    // costs one comparison a frame and turns an unwinnable round into a brief oddity.
    private void RescueIfFallenOutOfTheWorld()
    {
        // The quiet fix is tried first, and usually settles it.
        //
        // A body a metre under the terrain does not need carrying to the middle of the
        // arena. It needs lifting by a metre, exactly where it already is, and done that
        // way nobody watching ever knows it happened. Everything below this is for the
        // cases a lift cannot answer - genuinely falling, or under a part of the world
        // that has no floor over it to be put back on top of.
        if (HoldAboveTheFloor() == true)
        {
            secondsSpentUnderTheGround = 0f;
            return;
        }

        bool needsRescuing = false;

        // Genuinely falling. Nothing in the valley is anywhere near this low, so no
        // further thought is needed.
        if (transform.position.y < -30f)
        {
            needsRescuing = true;
            secondsSpentUnderTheGround = 0f;
        }
        else if (ValleyBuilder.IsUnderneathTheValley(transform.position) == true)
        {
            // Underneath the floor of the valley. There is no argument to be had about
            // this one and no reason to wait: nothing can stand there, and the mesh
            // floor above is one-sided so it can never climb back out on its own.
            needsRescuing = true;
            secondsSpentUnderTheGround = 0f;
        }
        else if (NavigationField.IsReady == true)
        {
            // The failure that actually happens is much quieter than falling: the
            // creature ends up inside the scenery near the portal and settles there at
            // about y = -3 for the whole round. It is unreachable and unkillable, so the
            // round waits on it forever, but it never gets far enough down to look like
            // a fall.
            //
            // The question asked is NOT "how far below the ground is it". Firing a ray
            // downwards at that spot misses the terrain and finds the buried portal
            // frame instead, so the floor appears to be metres below the creature and it
            // never looks buried at all. That is the same trap SelfTest documents.
            //
            // The honest question is the one the player's ability to reach it depends
            // on: is there anywhere within a couple of metres that a walking creature
            // could actually stand?
            Vector3 somewhereToStand;
            bool canBeReached = NavigationField.TryFindNearbyPoint(
                transform.position, 2.5f, out somewhereToStand);

            if (canBeReached == false)
            {
                // Given a moment before acting. A hammer blow can throw a creature clear
                // of the walkable ground for a fraction of a second quite legitimately,
                // and teleporting it out of mid-flight would look far worse than the bug.
                secondsSpentUnderTheGround = secondsSpentUnderTheGround + Time.deltaTime;
                if (secondsSpentUnderTheGround > 0.75f)
                {
                    needsRescuing = true;
                }
            }
            else
            {
                secondsSpentUnderTheGround = 0f;
            }
        }

        if (needsRescuing == false)
        {
            return;
        }

        secondsSpentUnderTheGround = 0f;
        timesRescued = timesRescued + 1;

        // Put back onto ground that is really there, rather than back where it started.
        // Most of these falls BEGIN with a starting position that was inside the terrain,
        // so returning to it drops the creature straight through again - and again, and
        // again. The round then waits forever on an enemy nobody can reach or kill, which
        // is exactly how round two became impossible to finish.
        //
        // NOT put back where it fell. Somewhere that swallows a creature once will
        // swallow it again, and the first version of this did exactly that: the warning
        // in the console read "put back at (-2.81, 0.87, 31.08)", which is the same spot
        // it had just been pulled out of, and it fell straight back through.
        //
        // The middle of the arena is used instead. It is where the fight is happening, it
        // is flat, and the player is standing on it - so it is known to be reachable by
        // the only definition that matters.
        Vector3 middle = ValleyBuilder.MiddleOfTheArena();

        // Spread around a small ring so several rescued at once do not end up inside one
        // another and shove each other straight back through the floor.
        float spreadAngle = Random.Range(0f, Mathf.PI * 2f);
        float rescueX = middle.x + Mathf.Cos(spreadAngle) * 4f;
        float rescueZ = middle.z + Mathf.Sin(spreadAngle) * 4f;

        Vector3 safeSpot = ValleyBuilder.SafeStandingSpot(rescueX, rescueZ, bodySize);

        // Pulled onto the walkable map. Standing on solid geometry is not the same thing
        // as standing somewhere the player can get to, and it is the second one that
        // decides whether the round can end.
        Vector3 onTheWalkableMap;
        if (NavigationField.TryFindNearbyPoint(safeSpot, 14f, out onTheWalkableMap) == true)
        {
            safeSpot = onTheWalkableMap + Vector3.up * (bodySize * 0.5f + 0.2f);
        }

        bodyController.enabled = false;
        transform.position = safeSpot;
        bodyController.enabled = true;
        WarpTheAgentTo(safeSpot);

        verticalSpeed = 0f;
        knockbackVelocity = Vector3.zero;

        // The creature is NOT killed off any more, however many rescues it takes.
        //
        // This used to delete it on the third one, on the argument that a round which can
        // never finish is a worse outcome than one enemy fewer. That argument was sound
        // while creatures were being put back somewhere that swallowed them again - but
        // deleting it is what the player actually SAW: an enemy blinking out of existence
        // mid-fight for no reason the game ever offered. It was the bug, as far as anyone
        // watching was concerned.
        //
        // It is also no longer the trade it was. LateUpdate now holds bodies on top of the
        // floor in the first place, and there is bedrock under the valley if one ever gets
        // past that, so a third rescue no longer means "this spot keeps swallowing it" -
        // it means something new is wrong, and destroying the evidence is the last thing
        // worth doing about that.
        if (timesRescued >= 3)
        {
            Debug.LogError(displayName + " has been pulled back out of the floor "
                + timesRescued + " times, which should not be possible now that bodies are"
                + " held above it every frame. It has been put back at " + safeSpot
                + " rather than removed. Its starting position was " + startingPosition
                + ".");
            return;
        }

        Debug.LogWarning(displayName + " went through the floor and was put back at "
            + safeSpot + " (rescue " + timesRescued + ").");
    }

    // Holds the creature on top of the floor instead of letting it be pushed through it.
    //
    // THIS is the fix for falling out of the world. Everything below it - the rescue, the
    // bedrock under the valley - is a net for the case where this somehow fails.
    //
    // The valley floor and the Vault floor are imported meshes, and a non-convex mesh
    // collider is a one-sided sheet with empty space behind it. Crowded creatures - jammed
    // against the gate at the north end, mostly - are depenetrated out of one another by
    // the physics engine, and that shove is not always sideways: a body pinned between two
    // others and a wall comes out along whichever axis it is least deeply overlapped on,
    // and often enough that is straight down. One shove longer than the capsule's radius
    // puts its centre under the sheet, and from underneath, the floor may as well not be
    // there. Gravity does the rest.
    //
    // Run in LateUpdate so it is the LAST thing to touch the position each frame - after
    // every Move in the state machine, after the lunge, after the gravity step. Catching
    // the shove in the same frame it happens turns the whole bug into a correction of a
    // few centimetres that nobody can see, rather than a fall that has to be noticed and
    // undone afterwards.
    void LateUpdate()
    {
        HoldAboveTheFloor();
    }

    // Puts the body back on top of the floor if it has got under it. Answers whether it
    // had to do anything, so the rescue below can tell "already dealt with, quietly" from
    // "this one needs carrying somewhere else".
    private bool HoldAboveTheFloor()
    {
        if (bodyController == null || bodyController.enabled == false)
        {
            return false;
        }

        float floorHeight;
        if (ValleyBuilder.TryFindFloorUnder(transform.position, out floorHeight) == false)
        {
            // Not over one of the mesh floors - in the cellar, or out on the road north.
            // Both are built from solid boxes that nothing can be pushed through.
            return false;
        }

        // Measured at the feet rather than at the middle, because the controller is
        // centred on the body.
        float feetHeight = transform.position.y - bodyController.height * 0.5f;

        // Six-tenths of a metre of slack. On a sculpted slope the floor directly beneath
        // the centre of a capsule genuinely does sit a little above the point the capsule
        // is resting on, and nudging every creature on every hillside once a frame would
        // be a worse bug than the one being fixed. A body that has actually been pushed
        // through the sheet is a whole body-length under it, never six centimetres.
        if (feetHeight >= floorHeight - 0.6f)
        {
            return false;
        }

        Vector3 backOnTop = transform.position;
        backOnTop.y = floorHeight + bodyController.height * 0.5f;

        // The controller has to be switched off across the move, or it fights the change
        // and drags the body back to where it believes it should be.
        bodyController.enabled = false;
        transform.position = backOnTop;
        bodyController.enabled = true;

        // Whatever downward speed had built up under there is dropped with it. Left
        // alone, gravity carries on from where it left off and drives the body straight
        // back through on the next frame.
        verticalSpeed = 0f;
        return true;
    }

    // Set while another script is carrying this body itself - so far only the Warden's
    // leap, which flies it along an arc of its own.
    //
    // While it is true this brain applies neither gravity nor its own chase movement.
    // Without it the two fight: gravity accumulates at 22 m/s squared the moment the body
    // leaves the ground, and over a two-thirds of a second flight that drags it several
    // metres back down through its own arc. A seven metre leap comes out as a stumble,
    // and it looks like the jump animation being wrong rather than like two scripts
    // moving the same CharacterController in the same frame.
    private bool ordinaryMovementIsSuspended = false;

    public void SuspendOrdinaryMovement(bool suspended)
    {
        ordinaryMovementIsSuspended = suspended;

        if (suspended == true)
        {
            // Cleared rather than left to accumulate. Whatever downward speed had built
            // up before the leap would otherwise be waiting to be applied all at once the
            // moment the body was handed back.
            verticalSpeed = 0f;
            knockbackVelocity = Vector3.zero;
        }
    }

    private void ApplyKnockbackAndGravity()
    {
        if (bodyController.isGrounded == true)
        {
            // A small downward push rather than zero keeps the controller pressed into
            // the ground, which stops it reporting "not grounded" every other frame.
            verticalSpeed = -2f;
        }
        else
        {
            verticalSpeed = verticalSpeed - 22f * Time.deltaTime;
        }

        // Falling and being shoved are applied every frame whether or not the enemy is
        // chasing, so an idle enemy still rests on the ground.
        Vector3 verticalAndShove = new Vector3(0f, verticalSpeed, 0f) + knockbackVelocity;
        bodyController.Move(verticalAndShove * Time.deltaTime);

        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 8f * Time.deltaTime);
        }
    }

    // ------------------------------------------------------------------------
    // Being hit, and dying
    // ------------------------------------------------------------------------

    // The ordinary way in - a swing. Everything that hit an enemy before this file grew
    // a boss still calls exactly this and behaves exactly as it did.
    public void ReceiveHitFromPlayer(float damageAmount, Vector3 cameFromPosition)
    {
        ReceiveHitFromPlayer(damageAmount, cameFromPosition, false);
    }

    // The same, but saying whether it was shot from a distance rather than swung.
    //
    // Only the Warden cares, and only about arrows: it is armoured against them except
    // while it is committed to one of its own moves. Melee deliberately does not carry
    // that penalty - closing to five metres with a boss that leaps, charges and slams is
    // the risk, and the damage is what it is paid for.
    public void ReceiveHitFromPlayer(float damageAmount, Vector3 cameFromPosition, bool shotFromRange)
    {
        if (ownStats.isDead == true)
        {
            return;
        }

        // Being hit provokes an enemy even from outside its senses, so nobody can be
        // picked off from safety without consequence.
        hasBeenProvoked = true;

        if (shotFromRange == true && theBossRidingAlong != null)
        {
            damageAmount = damageAmount * theBossRidingAlong.HowMuchOfAnArrowLands();
        }

        bool thisWasTheKillingBlow = ownStats.TakeDamage(damageAmount);

        // The sound of the blow landing is decided HERE rather than by whoever swung,
        // and that is deliberate. What a hit sounds like depends on what was hit - a
        // sword into a Darter and the same sword into four tonnes of Warden are not the
        // same event - and this is the only place that knows both the material and
        // whether the creature survived. PlayerCombat and Arrow used to each play their
        // own "HitEnemy" and neither could have known either fact.
        string whatItIsMadeOf = "Flesh";
        if (IsMadeOfStone() == true)
        {
            whatItIsMadeOf = "Stone";
        }

        string whatStruckIt = "Hit";
        if (shotFromRange == true)
        {
            whatStruckIt = "ArrowHit";
        }

        if (thisWasTheKillingBlow == true)
        {
            // The kill is the ordinary impact with something laid over it, not a
            // different sound. Swapping the impact out entirely on the last hit makes
            // the killing blow feel like it came from another weapon.
            GameSound.PlayWithAccent(whatStruckIt + whatItIsMadeOf, "KillingBlow",
                transform.position, 0.85f, 0.6f);
        }
        else
        {
            GameSound.PlayAt(whatStruckIt + whatItIsMadeOf, transform.position, 0.8f);
        }

        // It cries out only if it survived. The death sound already opens with the
        // creature's voice, and playing both on the killing blow doubles the voice over
        // itself and turns the kill into mush - which is what CharacterStats has always
        // said about the player, and it is just as true here.
        if (thisWasTheKillingBlow == false)
        {
            GameSound.PlayCreature(soundVoice, "Hurt", transform.position, 0.7f);
        }

        Vector3 shoveDirection = transform.position - cameFromPosition;
        shoveDirection.y = 0f;
        knockbackVelocity = shoveDirection.normalized * 7f * knockbackTaken;

        if (thisWasTheKillingBlow == true)
        {
            Die();
        }
    }

    // What this creature is made of, for the sound of hitting it. The Warden is the only
    // thing in the game built out of rock; everything else bleeds.
    public bool IsMadeOfStone()
    {
        return soundVoice == "Warden";
    }

    private void Die()
    {
        // Reachable from the hit that killed it AND from Update noticing it is dead, so
        // it has to be safe to call twice.
        if (hasAlreadyDied == true)
        {
            return;
        }
        hasAlreadyDied = true;

        ReturnToRestingPose();

        // Every creature used to die on the same two clips, so a 3.65 m stone Warden
        // and a Darter made an identical noise going down.
        GameSound.PlayCreature(soundVoice, "Death", transform.position, 0.85f);

        DeathBurst.SpawnAt(
            transform.position,
            ownStats.BodyColour(),
            bodySize);

        if (GameDirector.instance != null)
        {
            GameDirector.instance.OnEnemyDied(this, ownStats.essenceDroppedOnDeath, transform.position);
        }

        // Everything above happens on the frame of the kill, whether or not the body
        // then lingers - the essence, the sound, the burst and the round's tally are all
        // settled immediately, so a collapse can never hold a round open. RoundDirector
        // counts a creature as living only if it is BOTH switched on and not dead, so a
        // body still folding up is already not counted.
        StopBeingAnObstacle();

        secondsSpentDying = 0f;
        secondsAllowedToLieDying = 0f;

        ProceduralAnimator animator = TheLimbs();
        if (animator != null)
        {
            animator.PlayDeath();
            secondsAllowedToLieDying = 1f;
        }

        KeepLyingThere();
    }

    // A corpse must stop pushing the player around and stop soaking up swings the
    // instant it dies, however long it takes to finish falling over. Both the controller
    // and the agent go, which between them are the only reasons anything else in the
    // game can touch this creature: the player's swing finds enemies with an
    // OverlapSphere, and the controller is the only collider a creature has.
    private void StopBeingAnObstacle()
    {
        if (bodyController != null)
        {
            bodyController.enabled = false;
        }

        if (pathAgent != null && pathAgent.enabled == true)
        {
            pathAgent.enabled = false;
        }
    }

    private void KeepLyingThere()
    {
        if (hasAlreadyDied == false)
        {
            return;
        }

        secondsSpentDying = secondsSpentDying + Time.deltaTime;

        if (secondsSpentDying < secondsAllowedToLieDying)
        {
            return;
        }

        gameObject.SetActive(false);
    }

    public void ResetToStartingState()
    {
        hasAlreadyDied = false;
        secondsSpentDying = 0f;
        secondsAllowedToLieDying = 0f;
        ownStats.RestoreEverything();

        // Enemies are recycled between rounds rather than rebuilt, so a creature that
        // collapsed has to be told to stand up again. Without this it would come back
        // folded on the floor for the rest of the run.
        ProceduralAnimator animator = TheLimbs();
        if (animator != null)
        {
            animator.ReturnToLife();
        }

        bodyController.enabled = false;
        transform.position = startingPosition;
        transform.rotation = startingRotation;
        bodyController.enabled = true;

        // Dying switched the route planner off so that a body on the floor would stop
        // shouldering living enemies out of its way. Start() is what normally turns it
        // back on, and Start does not run again on a creature that was only switched
        // off and on - so without this line every recycled enemy would spend the rest of
        // the run with no pathfinding, walking straight into rocks.
        StartPathfindingIfPossible();

        WarpTheAgentTo(startingPosition);

        ReturnToRestingPose();
        currentState = StateIdle;
        stateSecondsRemaining = 0f;
        attackCooldownRemaining = 0f;
        retreatSecondsRemaining = 0f;
        hasBeenProvoked = false;
        damageDealtThisStrike = false;
        knockbackVelocity = Vector3.zero;

        gameObject.SetActive(true);
    }
}
