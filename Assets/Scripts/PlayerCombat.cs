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

    private float cooldownSecondsRemaining = 0f;
    private float buttonHeldForSeconds = 0f;
    private bool buttonIsDown = false;

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

    public bool LastSwingWasHeavy()
    {
        return lastSwingWasHeavy;
    }

    public float LastSwingTookSeconds()
    {
        return lastSwingTookSeconds;
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
        }

        if (buttonIsDown == true)
        {
            buttonHeldForSeconds = buttonHeldForSeconds + Time.deltaTime;
        }

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

    // How long a full draw takes RIGHT NOW, which is shorter while a kill streak is
    // running. Both the crosshair and the loosed arrow ask this rather than reading
    // secondsToFullDraw straight off the weapon, for exactly the reason set out below the
    // arrow code: two copies of the same arithmetic drift apart the first time anybody
    // adjusts the bow, and then the crosshair quietly lies instead of visibly breaking.
    private float SecondsToFullDrawNow(WeaponKind bow)
    {
        return bow.secondsToFullDraw * PlayerSurge.AttackTimingMultiplierNow();
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

        // A tapped bow is nearly useless on purpose. The whole point of the weapon is
        // that it costs time, and time is what the player does not have when something
        // is already close.
        float speed = ArrowSpeedAtDraw(bow, drawn);
        float damage = bow.damage * Mathf.Lerp(0.35f, 1f, drawn);

        // Aimed AT the point under the crosshair, with the launch angle solved so gravity
        // brings it down exactly there.
        Vector3 target = WhereTheCameraIsPointing();
        Vector3 from = WhereTheArrowWouldStart(target);

        bool canReachIt;
        Vector3 direction = LaunchDirectionToHit(from, target, speed, out canReachIt);

        Arrow.Fire(from, direction, speed, damage);

        GameSound.Play("SwordSwing", 0.45f);

        cooldownSecondsRemaining = bow.cooldownSeconds * PlayerSurge.AttackTimingMultiplierNow();
        swingFlashSecondsRemaining = 0.12f;
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

        Vector3 target = WhereTheCameraIsPointing();
        Vector3 from = WhereTheArrowWouldStart(target);
        float speed = ArrowSpeedAtDraw(weapon, DrawFraction());

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

        // The weapon names its own sound, so adding a third weapon later needs no
        // change here at all.
        if (weapon.weaponName == "HAMMER")
        {
            GameSound.Play("HammerSwing", 0.55f);
        }
        else if (weapon.weaponName == "WARDEN'S EDGE")
        {
            // The heaviest swing sound in the library, pitched by the mixer rather than
            // by a new recording. It is the only weapon that gets the hammer's weight
            // and the sword's speed at the same time, and it should sound like it.
            GameSound.Play("HammerSwing", 0.7f);
        }
        else
        {
            GameSound.Play("SwordSwing", 0.5f);
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
                    possibleEnemy.ReceiveHitFromPlayer(damage, transform.position);
                    GameSound.PlayAt("HitEnemy", oneThing.transform.position, 0.7f);
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
