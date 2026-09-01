using UnityEngine;

// Light attack on click, heavy attack on hold.
// Hits are found with a sphere placed in front of the player rather than with an
// animated weapon collider. It is far simpler, and at this scale it reads the same on
// screen.
public class PlayerCombat : MonoBehaviour
{
    // Holding the button longer than this turns the swing into a heavy one.
    public float holdSecondsForHeavy = 0.32f;

    // Damage, reach, cooldown and knockback all come from whichever weapon is in hand
    // now, rather than being fixed here. Swapping weapons changes how the player fights
    // without changing a line of this script.
    private PlayerWeapons ownWeapons;

    private CharacterStats ownStats;
    private PlayerMovement ownMovement;

    // How many arrows are left. Every use below is guarded - a missing quiver means
    // unlimited arrows, which is the old behaviour rather than a bow that cannot fire.
    private PlayerQuiver ownQuiver;

    // Found on demand, and NOT in Awake, which is where it obviously belongs and where it
    // would have been silently wrong.
    //
    // GameDirector is what adds this component to a player that was serialised into the
    // scene without one, and it does that in Start. Unity runs every Awake before any
    // Start, so an Awake lookup here runs before the component it is looking for has been
    // added and reliably finds nothing - and because a null quiver deliberately means
    // "unlimited arrows", the failure is completely silent. The bow would simply go back
    // to being free and the limit would read as never having been written.
    private PlayerQuiver TheQuiver()
    {
        if (ownQuiver == null)
        {
            ownQuiver = GetComponent<PlayerQuiver>();
        }
        return ownQuiver;
    }

    private float cooldownSecondsRemaining = 0f;
    private float buttonHeldForSeconds = 0f;
    private bool buttonIsDown = false;

    // Whether the string actually came back this frame, as opposed to the button merely
    // being held through the recovery of the previous shot. Cleared at the top of every
    // Update and set again by the input, so it describes this frame and no other.
    private bool drawIsAdvancingThisFrame = false;

    // Purely so the player can see that a swing happened. Set every time we attack and
    // counted down in Update.
    private float swingFlashSecondsRemaining = 0f;
    public GameObject swingIndicator;

    // What the crosshair should say about the shot as it stands. Worked out once a frame
    // while the bow is drawn.
    private bool crosshairIsOnAnEnemy = false;
    private bool shotWouldNotGetThere = false;

    // Counted only so that the Warden's Eye can wait for the player to actually try the
    // weapon it just gave them before opening the way home.
    private int swingsMade = 0;

    // Arrows that have actually left the bow, and how far it was drawn when the last one
    // did. Counted separately from swingsMade because they are not swings: PlayerAnimator
    // spots a shot by watching this move, exactly the way it spots a swing, and plays the
    // release scaled by the draw that produced it.
    //
    // A fumbled draw - under the minimum, or arms giving out - deliberately does not
    // count. Nothing left the bow, so there is nothing to animate the release of.
    private int arrowsLoosed = 0;
    private float lastShotDrawFraction = 0f;
    private float lastShotRecoverySeconds = 0.35f;

    // Where the crosshair is pointing, worked out once a frame while the bow is drawn.
    //
    // Cached rather than asked for repeatedly because working it out costs a RaycastAll
    // down the whole aim distance, and three callers now want it in the same frame: the
    // shot solution, the body turning to face what it is aiming at, and the nocked arrow
    // lying on the bow. One cast a frame, shared.
    private Vector3 aimPointThisFrame = Vector3.zero;
    private bool haveAnAimPoint = false;

    public int ArrowsLoosed()
    {
        return arrowsLoosed;
    }

    public float LastShotDrawFraction()
    {
        return lastShotDrawFraction;
    }

    // How long the bow is out of action for after the last shot, surge already applied.
    //
    // The release animation runs across exactly this, for the same reason the swing
    // animation runs across the swing's cooldown: the recovery IS the shot as far as the
    // player can tell, so a hand that finishes snapping back early would say the bow was
    // ready when it was not.
    public float LastShotRecoverySeconds()
    {
        return lastShotRecoverySeconds;
    }

    // The direction the shot would actually travel in, from the chest. Read by
    // PlayerMovement, which turns the body to face it while the string is back, and by
    // NockedArrow, which lays the shaft along it.
    //
    // Falls back to the way the player is already facing whenever there is no aim to
    // speak of, so a caller never has to handle a zero vector.
    public Vector3 AimDirection()
    {
        if (haveAnAimPoint == false)
        {
            return transform.forward;
        }

        Vector3 fromTheChest = aimPointThisFrame - (transform.position + Vector3.up * 0.6f);
        if (fromTheChest.sqrMagnitude < 0.0001f)
        {
            return transform.forward;
        }

        return fromTheChest.normalized;
    }

    public int SwingsMade()
    {
        return swingsMade;
    }

    // What the last swing was, and how long the player is locked out for because of it.
    //
    // Both are read by PlayerAnimator, which runs the swing animation across exactly this
    // cooldown. That is deliberate: the swing has no wind-up of its own in gameplay - the
    // damage lands the instant the button goes down - so the cooldown IS the swing as far
    // as the player can tell, and an animation any other length would either finish while
    // they were still locked out or still be going when they could swing again.
    //
    // It also means a surge speeds the animation up by exactly as much as it speeds the
    // attack up, with nothing extra to keep in sync, because the multiplier is already
    // baked into the cooldown before it is stored here.
    private bool lastSwingWasHeavy = false;
    private float lastSwingTookSeconds = 0.45f;
    private SwingShape lastSwingShape = SwingShape.Stab;

    public bool LastSwingWasHeavy()
    {
        return lastSwingWasHeavy;
    }

    public float LastSwingTookSeconds()
    {
        return lastSwingTookSeconds;
    }

    // Which shape the last swing was. Comes straight off the weapon that made it, so the
    // animator never has to ask what is in the player's hand.
    public SwingShape LastSwingShape()
    {
        return lastSwingShape;
    }

    void Awake()
    {
        ownStats = GetComponent<CharacterStats>();
        ownMovement = GetComponent<PlayerMovement>();
        ownWeapons = GetComponent<PlayerWeapons>();
    }

    void Update()
    {
        if (cooldownSecondsRemaining > 0f)
        {
            cooldownSecondsRemaining = cooldownSecondsRemaining - Time.deltaTime;
        }

        FadeTheSwingIndicator();

        // Forgotten every frame and worked out again at the end of this one, and only
        // while the bow is actually drawn. That way a warning colour can never be left
        // stuck on the crosshair after the player dies, starts a conversation, or looses
        // the shot.
        crosshairIsOnAnEnemy = false;
        shotWouldNotGetThere = false;
        drawIsAdvancingThisFrame = false;

        // Forgotten with the rest of it, so a stale aim can never outlive the draw that
        // produced it and leave the body turned at nothing.
        haveAnAimPoint = false;

        if (ownStats.isDead == true)
        {
            return;
        }

        // Nobody swings a sword mid-sentence. Without this the click that advances a line
        // of dialogue also starts an attack, which is how a player ends up mysteriously
        // out of stamina the moment a conversation ends.
        if (PlayerControl.IsBlocked() == true)
        {
            buttonIsDown = false;
            return;
        }

        WatchForAttackInput();

        // Before the two below, both of which want the answer. See aimPointThisFrame.
        RememberWhereWeAreAiming();

        // After the input rather than before it, so the crosshair shows the draw as it
        // stands this frame instead of trailing it by one.
        WorkOutWhereTheShotWouldLand();
    }

    private void WatchForAttackInput()
    {
        if (GameInput.AttackWasPressed() == true)
        {
            buttonIsDown = true;
            buttonHeldForSeconds = 0f;

            // An empty quiver refuses the draw outright rather than letting the player
            // pull a full string and find out at the release that there was never an
            // arrow on it. A wasted 1.4 second draw while something is walking at them is
            // a far worse punishment than being told no immediately, and it would read as
            // the bow having broken rather than as the quiver being empty.
            if (TheBowIsEmpty() == true)
            {
                buttonIsDown = false;
                GameSound.Play("WeaponSwap", 0.3f);
            }
            else if (ownWeapons != null && ownWeapons.WeaponInHand().isRanged == true)
            {
                // The arrow going on the string, and then the stave taking the load. The
                // bow made no sound at all until the moment it fired, so a draw - which
                // is over a second of the player standing still and committing to a shot
                // - gave nothing back at all while it was happening.
                GameSound.Play("BowNock", 0.7f);
                GameSound.Play("BowDraw", 0.7f);
            }
        }

        if (buttonIsDown == true)
        {
            // The draw does not begin until the previous shot has been recovered from.
            //
            // This is what stops tapping from beating aiming. The recovery used to tick
            // down THROUGH the draw, so a full draw cost its own 1.4 seconds while a tap
            // cost only the 0.35 second recovery - and since a tapped arrow still did a
            // third of full damage, tapping won on damage per second by about forty per
            // cent. The bow was at its most dangerous when used in the one way it was
            // documented as being useless in.
            //
            // Holding the string through the recovery instead means every shot costs its
            // recovery AND its draw, and the curve finally slopes the right way.
            bool stillRecovering = cooldownSecondsRemaining > 0f
                && ownWeapons != null
                && ownWeapons.WeaponInHand().isRanged == true;

            if (stillRecovering == false)
            {
                buttonHeldForSeconds = buttonHeldForSeconds + Time.deltaTime;
                drawIsAdvancingThisFrame = true;
            }
        }

        // Before the release is read, so a draw that runs the player dry this frame is
        // abandoned rather than loosing on the way out.
        DrainStaminaWhileDrawing();

        if (GameInput.AttackWasReleased() == true && buttonIsDown == true)
        {
            buttonIsDown = false;

            // A bow is loosed, not swung. How long the button was held decides how hard
            // the arrow flies rather than whether the swing was light or heavy.
            if (ownWeapons != null && ownWeapons.WeaponInHand().isRanged == true)
            {
                LooseAnArrow(buttonHeldForSeconds);
                return;
            }

            bool wasAHeavySwing = buttonHeldForSeconds >= holdSecondsForHeavy;
            PerformSwing(wasAHeavySwing);
        }
    }

    // One raycast a frame, and only while there is a bow drawn to aim with. A sword has
    // nothing to aim, and a bow at rest is not being pointed at anything.
    private void RememberWhereWeAreAiming()
    {
        if (IsDrawingABow() == false)
        {
            return;
        }

        aimPointThisFrame = WhereTheCameraIsPointing();
        haveAnAimPoint = true;
    }

    // How long a full draw takes RIGHT NOW, which is shorter while a kill streak is
    // running. Both the crosshair and the loosed arrow ask this rather than reading
    // secondsToFullDraw straight off the weapon, for exactly the reason set out below the
    // arrow code: two copies of the same arithmetic drift apart the first time anybody
    // adjusts the bow, and then the crosshair quietly lies instead of visibly breaking.
    private float SecondsToFullDrawNow(WeaponKind bow)
    {
        return bow.secondsToFullDraw * PlayerSurge.AttackTimingMultiplierNow();
    }

    // A ranged weapon in hand with nothing left to put on it.
    //
    // A missing quiver component answers false, not true - a player serialised into the
    // scene before the quiver existed keeps the old unlimited arrows rather than standing
    // there unable to fire at all.
    public bool TheBowIsEmpty()
    {
        if (ownWeapons == null || ownWeapons.WeaponInHand().isRanged == false)
        {
            return false;
        }
        PlayerQuiver quiver = TheQuiver();
        if (quiver == null)
        {
            return false;
        }
        return quiver.HasAnArrow() == false;
    }

    // Read by the display, so the arrow count can be shown next to the bow.
    public PlayerQuiver Quiver()
    {
        return TheQuiver();
    }

    // Is the string actually back right now? Read by PlayerMovement, which slows the
    // player down and refuses to sprint while it is true.
    public bool IsDrawingABow()
    {
        if (ownWeapons == null || ownWeapons.WeaponInHand().isRanged == false)
        {
            return false;
        }
        return buttonIsDown;
    }

    // Is the bow still recovering from the last shot, with the button already held?
    //
    // The HUD needs this because the draw does not begin until the recovery is over, so
    // for that third of a second the player is holding the button with an empty draw bar
    // in front of them. Left unshown it reads as the bow having stopped responding.
    public bool IsRecoveringWithTheBowHeld()
    {
        if (IsDrawingABow() == false)
        {
            return false;
        }
        return cooldownSecondsRemaining > 0f;
    }

    // How far back the string has to come before there is a shot. The HUD marks this on
    // the draw bar, and it has to: a draw that quietly produces no arrow is indis-
    // tinguishable from a bow that has stopped working, and the player would rightly
    // report it as a bug.
    public float MinimumDrawToLoose()
    {
        if (ownWeapons == null || ownWeapons.WeaponInHand().isRanged == false)
        {
            return 0f;
        }
        return ownWeapons.WeaponInHand().minimumDrawToLoose;
    }

    // What is left of the player's speed while aiming, straight off whichever ranged
    // weapon is in hand. Asked rather than stored so a second bow added later carries
    // its own handling without PlayerMovement knowing it exists.
    public float MovementWhileDrawing()
    {
        if (ownWeapons == null)
        {
            return 1f;
        }
        return ownWeapons.WeaponInHand().movementWhileDrawing;
    }

    // The string is heavy. Holding it back spends out of the same pool the dodge spends
    // from, so aiming and still being able to roll are now one budget rather than two.
    //
    // The regen has to be held off as well as the stamina spent. Stamina refills at 25 a
    // second and the draw costs 12, so on its own the drain would be swallowed whole and
    // the player would gain stamina by aiming.
    private void DrainStaminaWhileDrawing()
    {
        if (IsDrawingABow() == false)
        {
            return;
        }

        // Only while the string is actually coming back. Charging stamina through the
        // recovery as well would take it while the draw bar visibly is not moving, and a
        // cost with no matching progress on screen reads as a bug rather than as a price.
        if (drawIsAdvancingThisFrame == false)
        {
            return;
        }

        WeaponKind bow = ownWeapons.WeaponInHand();

        ownStats.HoldOffStaminaRegen(0.15f);

        float costThisFrame = bow.staminaPerSecondDrawing * Time.deltaTime;
        bool couldAffordIt = ownStats.TrySpendStamina(costThisFrame);

        if (couldAffordIt == false)
        {
            // Out of stamina with the string still back. The arms give out and the draw
            // is abandoned rather than loosing whatever it had - otherwise running dry
            // would be the cheapest way in the game to fire an arrow.
            //
            // buttonIsDown going false here also means the draw does not silently
            // restart while the button is still held: the player has to let go and pull
            // again, which is what "your arms gave out" should feel like.
            buttonIsDown = false;
            buttonHeldForSeconds = 0f;
            GameSound.Play("WeaponSwap", 0.3f);
        }
    }

    // How far the bow is drawn right now, nought to one. The display reads this.
    public float DrawFraction()
    {
        if (ownWeapons == null || ownWeapons.WeaponInHand().isRanged == false)
        {
            return 0f;
        }
        if (buttonIsDown == false)
        {
            return 0f;
        }

        WeaponKind bow = ownWeapons.WeaponInHand();
        float howFar = buttonHeldForSeconds / SecondsToFullDrawNow(bow);
        if (howFar > 1f)
        {
            howFar = 1f;
        }
        return howFar;
    }

    private void LooseAnArrow(float heldForSeconds)
    {
        if (cooldownSecondsRemaining > 0f)
        {
            return;
        }

        WeaponKind bow = ownWeapons.WeaponInHand();

        float drawn = heldForSeconds / SecondsToFullDrawNow(bow);
        if (drawn > 1f)
        {
            drawn = 1f;
        }

        // Not far enough back to be a shot at all. The string slips and nothing leaves,
        // and deliberately no recovery is charged - a recovery here would stack a
        // punishment on top of a fumble, and the fumble is punishment enough.
        //
        // It costs no arrow either. The quiver is spent below, once the shot is certain,
        // so a fumbled draw wastes the time it took and nothing else. Charging one of
        // twenty for a misclick would be a cost the player could not see coming.
        if (drawn < bow.minimumDrawToLoose)
        {
            GameSound.Play("WeaponSwap", 0.3f);
            return;
        }

        // The arrow comes out of the quiver here, after every reason not to fire has been
        // ruled out and before anything that assumes a shot happened.
        PlayerQuiver quiver = TheQuiver();
        if (quiver != null && quiver.TryTakeAnArrow() == false)
        {
            GameSound.Play("WeaponSwap", 0.3f);
            return;
        }

        // Damage climbs with the SQUARE of the draw, so the back half of the pull is
        // worth far more than the front half.
        //
        // The old curve ran from a third of full damage at no draw up to full, linearly.
        // That made a half-drawn shot a reasonable trade for the time it saved, and a
        // tapped one better still. Squaring it makes a half draw worth a quarter, so
        // there is no longer a fast cheap way to use the bow - only a slow expensive one.
        float speed = ArrowSpeedAtDraw(bow, drawn);
        float damage = bow.damage * drawn * drawn;

        // Weakness is read at the moment the string is RELEASED, not when the arrow
        // arrives. An arrow already in flight is a thing that has left the player, and
        // having it grow weaker on the way to the target because a rock landed meanwhile
        // would be invisible and unaccountable. The bow is also the answer to the Spitter
        // that inflicted this, which is the whole point: it makes the shot back cost two
        // arrows instead of one rather than taking the shot away.
        damage = damage * PlayerAilments.OutgoingDamageMultiplierNow();

        // Aimed AT the point under the crosshair, with the launch angle solved so gravity
        // brings it down exactly there.
        Vector3 target = WhereTheCameraIsPointing();
        Vector3 from = WhereTheArrowWouldStart(target);

        bool canReachIt;
        Vector3 direction = LaunchDirectionToHit(from, target, speed, out canReachIt);

        Arrow.Fire(from, direction, speed, damage);

        // The bow's own sound, and pitched by how far the string actually came back. A
        // half draw is a lighter, higher snap and a full one is a deep thump, so the
        // shot the player just took is audible as the shot it was - which matters here
        // more than anywhere, because damage climbs with the SQUARE of the draw and the
        // difference between a tap and a full pull is most of the weapon.
        float snapPitch = Mathf.Lerp(1.12f, 0.90f, drawn);
        GameSound.Play("BowRelease", 0.55f + drawn * 0.35f, snapPitch);

        cooldownSecondsRemaining = bow.cooldownSeconds * PlayerSurge.AttackTimingMultiplierNow();
        swingFlashSecondsRemaining = 0.12f;

        // Recorded last, once the shot has definitely happened, so that every early
        // return above leaves the animator with nothing to play.
        lastShotDrawFraction = drawn;
        lastShotRecoverySeconds = cooldownSecondsRemaining;
        arrowsLoosed = arrowsLoosed + 1;
    }

    // ---- Where the shot goes ---------------------------------------------------------
    //
    // The facts that decide where an arrow ends up. LooseAnArrow and the crosshair both
    // ask these rather than working it out for themselves, and that is the only reason
    // what is drawn on screen can be trusted. Two copies of this arithmetic would drift
    // apart the first time anybody adjusted the bow, and the crosshair would start
    // quietly lying rather than visibly breaking - much the worse failure of the two.

    // How far out to look for whatever the player is pointing at. Past this the crosshair
    // is over open sky and there is nothing to converge on.
    private const float FurthestAimDistance = 80f;

    // The point in the world that sits under the middle of the screen - what the
    // crosshair is actually over.
    private Vector3 WhereTheCameraIsPointing()
    {
        Camera eye = Camera.main;
        if (eye == null)
        {
            return transform.position + transform.forward * FurthestAimDistance;
        }

        Vector3 fromTheEye = eye.transform.position;
        Vector3 alongTheView = eye.transform.forward;

        RaycastHit[] hits = Physics.RaycastAll(
            fromTheEye, alongTheView, FurthestAimDistance, ~0, QueryTriggerInteraction.Ignore);

        // The nearest thing that is not the player. In a third-person view the player's
        // own body sits between the camera and everything else, so without stepping over
        // it every shot would aim at the back of their own head.
        float distanceToTheNearest = float.MaxValue;
        bool foundSomething = false;

        int index = 0;
        while (index < hits.Length)
        {
            if (hits[index].collider.gameObject.CompareTag("Player") == false)
            {
                if (hits[index].distance < distanceToTheNearest)
                {
                    distanceToTheNearest = hits[index].distance;
                    foundSomething = true;
                }
            }
            index = index + 1;
        }

        if (foundSomething == true)
        {
            return fromTheEye + alongTheView * distanceToTheNearest;
        }

        // Nothing under the crosshair but sky. Aim a long way off along the same line, so
        // the shot still goes where the player is looking.
        return fromTheEye + alongTheView * FurthestAimDistance;
    }

    private Vector3 WhereTheArrowWouldStart(Vector3 target)
    {
        // Chest height, and a little way out in front so the shaft does not begin inside
        // the player's own shoulder. "In front" means toward whatever is being aimed at.
        Vector3 chest = transform.position + Vector3.up * 0.6f;

        Vector3 towardTheTarget = target - chest;
        towardTheTarget.y = 0f;

        if (towardTheTarget.sqrMagnitude < 0.0001f)
        {
            towardTheTarget = transform.forward;
        }

        return chest + towardTheTarget.normalized * 0.9f;
    }

    // The angle to launch at so the arrow FALLS ONTO the aim point instead of passing
    // under it.
    //
    // This is the whole of the bug the crosshair exposed. The arrow used to be fired
    // along the camera's forward direction from the player's chest - but the camera looks
    // at a point 2.2m above the player and the chest is at 0.6m, so the arrow set off
    // along a line PARALLEL to the camera's and 1.6m below it. Parallel lines never meet,
    // so the shot was low at every range, at every draw, before gravity was even
    // considered. Solving the angle to an actual point is what makes the crosshair mean
    // something.
    //
    // canReachIt comes back false when the target is simply too far for this draw. The
    // arrow physically cannot get there at that speed, and the crosshair has to say so
    // rather than promise a hit it cannot deliver.
    private Vector3 LaunchDirectionToHit(Vector3 from, Vector3 target, float speed, out bool canReachIt)
    {
        canReachIt = true;

        Vector3 toTarget = target - from;

        Vector3 acrossTheGround = new Vector3(toTarget.x, 0f, toTarget.z);
        float horizontalDistance = acrossTheGround.magnitude;
        float heightDifference = toTarget.y;

        // Straight up or straight down. There is no arc to solve, so just point at it.
        if (horizontalDistance < 0.01f)
        {
            return toTarget.normalized;
        }

        float gravity = Arrow.GravityOnAnArrow();
        float speedSquared = speed * speed;

        // The standard ballistic solution. Of the two arcs that reach a target, the minus
        // root is the flatter one - which is the one that looks like a bow shot rather
        // than a mortar lobbing over a wall.
        float underTheRoot = speedSquared * speedSquared
            - gravity * (gravity * horizontalDistance * horizontalDistance
                + 2f * heightDifference * speedSquared);

        if (underTheRoot < 0f)
        {
            // Out of reach at this draw. Forty-five degrees carries furthest, so the shot
            // still goes as far as it possibly can and visibly drops short, which is the
            // honest thing to show.
            canReachIt = false;
            return (acrossTheGround.normalized + Vector3.up).normalized;
        }

        float tangentOfTheAngle =
            (speedSquared - Mathf.Sqrt(underTheRoot)) / (gravity * horizontalDistance);

        return (acrossTheGround.normalized + Vector3.up * tangentOfTheAngle).normalized;
    }

    private float ArrowSpeedAtDraw(WeaponKind bow, float drawn)
    {
        return Mathf.Lerp(bow.arrowSpeedAtNoDraw, bow.arrowSpeedAtFullDraw, drawn);
    }

    // Fly the shot in advance, purely so the crosshair can tell the truth about it.
    //
    // The arc is solved to land on the aim point, so if the predicted flight stops
    // somewhere else then something is in the way or the draw is too weak to carry that
    // far. Either way the crosshair would otherwise be promising a hit it cannot deliver,
    // which is the one thing a crosshair must never do.
    //
    // Done here, once a frame, rather than in the display - because OnGUI runs more than
    // once per frame, and forty sphere casts twice a frame for no benefit at all is
    // exactly the sort of cost that stays invisible until it suddenly is not.
    private void WorkOutWhereTheShotWouldLand()
    {
        if (ownWeapons == null)
        {
            return;
        }

        WeaponKind weapon = ownWeapons.WeaponInHand();
        if (weapon.isRanged == false)
        {
            return;
        }

        // Only while the string is actually back. A bow at rest has no shot to check.
        if (buttonIsDown == false)
        {
            return;
        }

        // Under the minimum there is no shot to describe. Saying "that one" about an
        // enemy the string cannot yet reach would be the crosshair promising a hit it
        // cannot deliver, which is the one thing this whole block exists to prevent. The
        // draw bar is already showing red; the crosshair simply stays neutral.
        float drawnSoFar = DrawFraction();
        if (drawnSoFar < weapon.minimumDrawToLoose)
        {
            return;
        }

        // The point RememberWhereWeAreAiming already worked out this frame. Casting for
        // it a second time would be the same answer at twice the price.
        Vector3 target = aimPointThisFrame;
        Vector3 from = WhereTheArrowWouldStart(target);
        float speed = ArrowSpeedAtDraw(weapon, drawnSoFar);

        bool canReachIt;
        Vector3 direction = LaunchDirectionToHit(from, target, speed, out canReachIt);

        bool wouldHitSomethingAlive;
        Vector3 wouldLandAt = Arrow.PredictWhereItLands(
            from, direction, speed, out wouldHitSomethingAlive);

        // Something alive stopped it. That is a hit whether or not it was the exact point
        // aimed at - an enemy standing in front of the rock behind them still dies.
        if (wouldHitSomethingAlive == true)
        {
            crosshairIsOnAnEnemy = true;
            shotWouldNotGetThere = false;
            return;
        }

        if (canReachIt == false)
        {
            shotWouldNotGetThere = true;
            return;
        }

        // A metre and a half of slack, because the prediction steps in fixed hops and is
        // never going to land on the exact centimetre.
        float howFarOffItLands = Vector3.Distance(wouldLandAt, target);
        if (howFarOffItLands > 1.5f)
        {
            shotWouldNotGetThere = true;
        }
    }

    // Read by the display, which colours the crosshair from them.
    public bool CrosshairIsOnAnEnemy()
    {
        return crosshairIsOnAnEnemy;
    }

    public bool ShotWouldNotGetThere()
    {
        return shotWouldNotGetThere;
    }

    // Whether to draw a crosshair at all. A sword does not get one.
    public bool RangedWeaponIsInHand()
    {
        if (ownWeapons == null)
        {
            return false;
        }
        return ownWeapons.WeaponInHand().isRanged;
    }

    // Swing without a button being pressed. Used by the automated play-through, and the
    // seam any scripted moment would come in through.
    public void PerformSwingNow(bool isHeavy)
    {
        PerformSwing(isHeavy);
    }

    private void PerformSwing(bool isHeavy)
    {
        if (cooldownSecondsRemaining > 0f)
        {
            return;
        }
        // Swinging mid-roll would let the player dodge and attack at the same time,
        // which removes the whole point of the stamina economy.
        if (ownMovement.IsCurrentlyDodging() == true)
        {
            return;
        }

        WeaponKind weapon = ownWeapons.WeaponInHand();

        // The weapon supplies the base damage, and the player's own attack stat is added
        // on top - so shrine upgrades still matter regardless of what is being held.
        float reach = weapon.reach;
        float damage = weapon.damage + (ownStats.attackDamage - 20f);

        // A Spitter's rock halves this. Applied to the base damage before the heavy
        // multiplier below, so a heavy swing is weakened by the same proportion as a
        // light one rather than escaping the penalty by being big.
        damage = damage * PlayerAilments.OutgoingDamageMultiplierNow();

        if (isHeavy == true)
        {
            bool couldAffordIt = ownStats.TrySpendStamina(weapon.heavyStaminaCost);
            if (couldAffordIt == false)
            {
                // Not enough stamina, so the heavy quietly downgrades to a light swing
                // rather than doing nothing and feeling broken.
                isHeavy = false;
            }
        }

        if (isHeavy == true)
        {
            reach = weapon.heavyReach;
            damage = damage * weapon.heavyDamageMultiplier;
            cooldownSecondsRemaining = weapon.heavyCooldownSeconds;
        }
        else
        {
            cooldownSecondsRemaining = weapon.cooldownSeconds;
        }

        // A kill streak shortens the recovery on whichever swing was just made. It is
        // applied here, once, after the weapon has decided its own timing - so a new
        // weapon added later is sped up by the surge without knowing the surge exists.
        cooldownSecondsRemaining = cooldownSecondsRemaining * PlayerSurge.AttackTimingMultiplierNow();

        // Remembered for the animator, after the surge multiplier has been applied so
        // that the animation matches the swing the player actually just made.
        lastSwingWasHeavy = isHeavy;
        lastSwingTookSeconds = cooldownSecondsRemaining;
        lastSwingShape = weapon.swingShape;

        // The weapon names its own sound, so adding a third weapon later needs no
        // change here at all.
        if (weapon.weaponName == "HAMMER")
        {
            GameSound.Play("HammerWhiff", 0.55f);
        }
        else if (weapon.weaponName == "WARDEN'S EDGE")
        {
            // The heaviest swing sound in the library, pitched DOWN rather than given a
            // new recording. It is the only weapon that gets the hammer's weight and the
            // sword's speed at the same time, and it should sound like it.
            GameSound.Play("HammerWhiff", 0.7f, 0.86f);
        }
        else
        {
            GameSound.Play("SwordWhiff", 0.5f);
        }

        swingFlashSecondsRemaining = 0.12f;
        swingsMade = swingsMade + 1;
        if (swingIndicator != null)
        {
            swingIndicator.SetActive(true);
            swingIndicator.transform.localScale = Vector3.one * reach;
        }

        // How far around the player this weapon reaches. A heavy swing of the Warden's
        // Edge is the one attack in the game that goes the whole way round.
        float arcDegrees = weapon.swingArcDegrees;
        if (isHeavy == true && weapon.weaponName == "WARDEN'S EDGE")
        {
            arcDegrees = PlayerWeapons.EdgeHeavyArcDegrees;
        }

        DamageEverythingInFront(reach, damage, arcDegrees);
    }

    private void DamageEverythingInFront(float reach, float damage, float arcDegrees)
    {
        // The sphere sits a little ahead of the player so that the swing covers the
        // space in front rather than a ring centred on the player's own feet.
        //
        // A wide weapon is centred closer to the player instead, because a two hundred
        // degree swing that starts a full stride ahead would miss the things standing at
        // the player's own shoulders - which are exactly the things it exists to hit.
        float howFarAheadToCentre = reach * 0.5f;
        if (arcDegrees > 180f)
        {
            howFarAheadToCentre = reach * 0.1f;
        }

        Vector3 centreOfSwing = transform.position + transform.forward * howFarAheadToCentre + Vector3.up;
        Collider[] thingsHit = Physics.OverlapSphere(centreOfSwing, reach * 0.6f);

        // Half the arc, because the test below measures from the centre line outwards in
        // either direction.
        float halfTheArc = arcDegrees * 0.5f;

        int hitIndex = 0;
        while (hitIndex < thingsHit.Length)
        {
            Collider oneThing = thingsHit[hitIndex];

            // Never damage ourselves with our own swing.
            if (oneThing.gameObject != gameObject)
            {
                EnemyBrain possibleEnemy = oneThing.GetComponent<EnemyBrain>();
                if (possibleEnemy != null && SwingCovers(oneThing.transform.position, halfTheArc) == true)
                {
                    // No sound here. What a landed blow sounds like depends on what
                    // was struck and whether it survived, and only the creature knows
                    // both - ReceiveHitFromPlayer makes the noise.
                    possibleEnemy.ReceiveHitFromPlayer(damage, transform.position);
                }
            }
            hitIndex = hitIndex + 1;
        }
    }

    // Is that thing inside the wedge this swing sweeps through?
    //
    // Height is thrown away before the angle is measured. Without that, a Spitter
    // standing on a ledge slightly above the player counts as being at a steep angle and
    // survives a swing that visibly connected.
    private bool SwingCovers(Vector3 whereTheyAre, float halfTheArc)
    {
        if (halfTheArc >= 180f)
        {
            return true;
        }

        Vector3 towardsThem = whereTheyAre - transform.position;
        towardsThem.y = 0f;

        // Standing exactly on top of us. There is no direction to measure, so count it.
        if (towardsThem.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        Vector3 facing = transform.forward;
        facing.y = 0f;

        float angleBetween = Vector3.Angle(facing, towardsThem);
        return angleBetween <= halfTheArc;
    }

    // Read by PlayerHealing, which refuses to start a drink during a swing.
    public bool IsSwinging()
    {
        return cooldownSecondsRemaining > 0f || buttonIsDown == true;
    }

    private void FadeTheSwingIndicator()
    {
        if (swingFlashSecondsRemaining <= 0f)
        {
            return;
        }

        swingFlashSecondsRemaining = swingFlashSecondsRemaining - Time.deltaTime;
        if (swingFlashSecondsRemaining <= 0f && swingIndicator != null)
        {
            swingIndicator.SetActive(false);
        }
    }
}
