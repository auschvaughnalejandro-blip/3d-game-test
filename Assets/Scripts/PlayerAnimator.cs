using UnityEngine;

// Drives the player's limbs from the player's own state.
//
// Why this exists
// ---------------
// ProceduralAnimator can animate any segmented character, but it does not know what a
// player is. EnemyBrain is what tells it a Grunt is winding up a club; nothing was ever
// telling it what the player was doing, so the player walked and did nothing else. Every
// dodge, swing, jump, drink and hit landed with the body completely still.
//
// This is the missing half. It reads the player scripts once a frame and calls the same
// kind of Show* methods EnemyBrain calls. It owns no timing of its own.
//
// Where the timings come from
// ---------------------------
// Every duration here is read from the field that already governs the gameplay:
// dodgeLastsSeconds for the roll, the weapon's own cooldown for a swing, secondsToDrink
// for a potion, surgeLastsSeconds for the surge. None of them are re-stated.
//
// That matters more than it looks. An animation that invents its own duration drifts out
// of step with the hitbox it is supposed to be selling - a roll animation that runs
// longer than the roll tells the player they are still invulnerable when they are not,
// and they read that as the game cheating rather than as an animation bug.
//
// The one exception is the weapon swap, which is called out where it happens.
public class PlayerAnimator : MonoBehaviour
{
    // How quickly the sprint pose blends in and out. Sprinting is a held state rather
    // than an event, so it fades rather than switching, or crossing the threshold pops.
    public float sprintBlendSeconds = 0.2f;

    // The swap has no gameplay duration to borrow - changing weapon is instantaneous, and
    // the only existing timer is the 1.2 s the HUD spends announcing the new weapon,
    // which is far longer than a pair of hands takes. So this one number is the
    // animation's own, and it is short on purpose.
    public float swapTakesSeconds = 0.30f;

    // Long enough to read as a flinch, short enough that it can never feel like a
    // stunlock. It is interruptible regardless - see below.
    public float hitReactionSeconds = 0.25f;

    private ProceduralAnimator limbs;
    private PlayerMovement movement;
    private PlayerCombat combat;
    private PlayerSurge surge;
    private PlayerHealing healing;
    private PlayerWeapons weapons;
    private CharacterStats ownStats;
    private CharacterController bodyController;

    private float sprintAmount = 0f;

    // Swings, swaps and hits are events rather than states, so each is spotted by
    // watching a counter the other script already keeps and noticing when it moves.
    private int swingsSeen = -1;
    private float swingSecondsElapsed = -1f;
    private float swingTakesSeconds = 0.45f;
    private bool swingWasHeavy = false;

    private int swapsSeen = -1;
    private float swapSecondsElapsed = -1f;

    private int timesDamagedSeen = -1;
    private float hitSecondsElapsed = -1f;
    private float hitSideSign = 1f;

    private bool wasAirborneLastFrame = false;

    void Start()
    {
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        surge = GetComponent<PlayerSurge>();
        healing = GetComponent<PlayerHealing>();
        weapons = GetComponent<PlayerWeapons>();
        ownStats = GetComponent<CharacterStats>();
        bodyController = GetComponent<CharacterController>();

        FindTheLimbs();

        // Start the counters where they already are, or the player would swing, swap and
        // flinch once each on the first frame of the game.
        if (combat != null)
        {
            swingsSeen = combat.SwingsMade();
        }
        if (weapons != null)
        {
            swapsSeen = weapons.SwapsMade();
        }
        if (ownStats != null)
        {
            timesDamagedSeen = ownStats.timesDamaged;
        }
    }

    // The animator lives on the model wrapper hanging under the player, put there by
    // ValleyBuilder.AttachSegmentedModel. It is looked for in the children rather than on
    // this object, and a player still wearing the old single-mesh model simply has none -
    // in which case everything below quietly does nothing rather than throwing.
    private void FindTheLimbs()
    {
        limbs = GetComponentInChildren<ProceduralAnimator>();

        if (limbs == null)
        {
            Debug.Log("PlayerAnimator found no ProceduralAnimator under the player. "
                + "This is expected while the player is still using the single-mesh "
                + "model; the player will walk but will not act.");
        }
    }

    void Update()
    {
        if (limbs == null)
        {
            // The model can be rebuilt underneath us between rounds, so keep looking
            // rather than giving up permanently after one miss at startup.
            FindTheLimbs();
            if (limbs == null)
            {
                return;
            }
        }

        DriveTheSprint();
        DriveTheJump();
        DriveTheDodge();
        DriveTheSwing();
        DriveTheSurge();
        DriveTheDrink();
        DriveTheSwap();
        DriveTheHitReaction();
    }

    // ------------------------------------------------------------------

    // Speed alone already lengthens the stride, so what this switches on is the SHAPE of
    // a sprint - pitched forward, arms driving, elbows in. The threshold sits between the
    // walking and sprinting speeds rather than at either of them, so it reads as soon as
    // the player commits rather than only at full pace.
    private void DriveTheSprint()
    {
        if (movement == null)
        {
            return;
        }

        float threshold = (movement.walkingSpeed + movement.sprintingSpeed) * 0.5f;
        float flatSpeed = FlatSpeedNow();

        float wanted = 0f;
        if (flatSpeed > threshold && movement.IsAirborne() == false)
        {
            wanted = 1f;
        }

        if (sprintBlendSeconds > 0f)
        {
            float step = Time.deltaTime / sprintBlendSeconds;
            sprintAmount = Mathf.MoveTowards(sprintAmount, wanted, step);
        }
        else
        {
            sprintAmount = wanted;
        }

        limbs.ShowSprint(sprintAmount);
    }

    // Measured from the controller rather than from the input, so being shoved, knocked
    // back or carried counts exactly as much as running does.
    private float FlatSpeedNow()
    {
        if (bodyController == null)
        {
            return 0f;
        }

        Vector3 velocity = bodyController.velocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }

    private void DriveTheJump()
    {
        if (movement == null)
        {
            return;
        }

        bool airborne = movement.IsAirborne();
        limbs.ShowAirborne(airborne, movement.VerticalSpeed());

        // The landing absorb fires on the one frame the feet come back down.
        if (wasAirborneLastFrame == true && airborne == false)
        {
            limbs.ShowLanding();
        }

        wasAirborneLastFrame = airborne;
    }

    private void DriveTheDodge()
    {
        if (movement == null)
        {
            return;
        }

        float progress = movement.DodgeProgress();

        if (progress < 0f)
        {
            limbs.ShowDodge(-1f, 0f);
            return;
        }

        // Which side the body is being thrown over. Positive is the player's right, and
        // a dodge straight forward or back comes out near zero, which correctly rolls the
        // body hardly at all.
        Vector3 direction = movement.DodgeDirection();
        float sideways = Vector3.Dot(direction.normalized, transform.right);

        limbs.ShowDodge(progress, sideways);
    }

    // A swing is an event - the damage lands the instant the button goes down - so it is
    // spotted by watching the counter PlayerCombat already keeps, and then played out
    // across the cooldown that swing cost.
    private void DriveTheSwing()
    {
        if (combat == null)
        {
            return;
        }

        int swingsNow = combat.SwingsMade();
        if (swingsNow != swingsSeen)
        {
            swingsSeen = swingsNow;
            swingSecondsElapsed = 0f;
            swingWasHeavy = combat.LastSwingWasHeavy();
            swingTakesSeconds = combat.LastSwingTookSeconds();

            if (swingTakesSeconds <= 0f)
            {
                swingTakesSeconds = 0.45f;
            }
        }

        if (swingSecondsElapsed < 0f)
        {
            limbs.ShowPlayerSwing(-1f, false);
            return;
        }

        swingSecondsElapsed = swingSecondsElapsed + Time.deltaTime;

        if (swingSecondsElapsed >= swingTakesSeconds)
        {
            swingSecondsElapsed = -1f;
            limbs.ShowPlayerSwing(-1f, false);
            return;
        }

        limbs.ShowPlayerSwing(swingSecondsElapsed / swingTakesSeconds, swingWasHeavy);
    }

    private void DriveTheSurge()
    {
        if (surge == null)
        {
            limbs.ShowSurge(0f);
            return;
        }

        // Held flat out for the whole surge rather than animated through. The pose is the
        // statement; moving it about would dilute it.
        if (surge.SurgeIsActive() == true)
        {
            limbs.ShowSurge(1f);
        }
        else
        {
            limbs.ShowSurge(0f);
        }
    }

    private void DriveTheDrink()
    {
        if (healing == null)
        {
            return;
        }

        // DrinkProgress() alone is not enough: it answers 1.0 when nothing is being
        // drunk, which is indistinguishable from a drink that has just finished. So the
        // gate is IsDrinking(), and the progress is only trusted inside it.
        if (healing.IsDrinking() == true)
        {
            limbs.ShowDrinking(healing.DrinkProgress());
        }
        else
        {
            limbs.ShowDrinking(-1f);
        }
    }

    private void DriveTheSwap()
    {
        if (weapons == null)
        {
            return;
        }

        int swapsNow = weapons.SwapsMade();
        if (swapsNow != swapsSeen)
        {
            swapsSeen = swapsNow;
            swapSecondsElapsed = 0f;
        }

        if (swapSecondsElapsed < 0f)
        {
            limbs.ShowWeaponSwap(-1f);
            return;
        }

        swapSecondsElapsed = swapSecondsElapsed + Time.deltaTime;

        if (swapSecondsElapsed >= swapTakesSeconds || swapTakesSeconds <= 0f)
        {
            swapSecondsElapsed = -1f;
            limbs.ShowWeaponSwap(-1f);
            return;
        }

        limbs.ShowWeaponSwap(swapSecondsElapsed / swapTakesSeconds);
    }

    // Interruptible by construction: a second hit restarts the timer from zero rather
    // than queueing behind the first, and nothing here blocks input at all. The player
    // can dodge out of a flinch on the frame it starts.
    private void DriveTheHitReaction()
    {
        if (ownStats == null)
        {
            return;
        }

        int damagedNow = ownStats.timesDamaged;
        if (damagedNow != timesDamagedSeen)
        {
            timesDamagedSeen = damagedNow;
            hitSecondsElapsed = 0f;
            hitSideSign = WhichSideTheHitCameFrom();
        }

        if (hitSecondsElapsed < 0f)
        {
            limbs.ShowHitReaction(-1f, 0f);
            return;
        }

        hitSecondsElapsed = hitSecondsElapsed + Time.deltaTime;

        if (hitSecondsElapsed >= hitReactionSeconds || hitReactionSeconds <= 0f)
        {
            hitSecondsElapsed = -1f;
            limbs.ShowHitReaction(-1f, 0f);
            return;
        }

        limbs.ShowHitReaction(hitSecondsElapsed / hitReactionSeconds, hitSideSign);
    }

    // Nothing records where damage came from, and threading that through every source -
    // a club, a thrown rock, an arrow, the Warden's shockwave - would mean touching all
    // of them. So the nearest living enemy is used as the direction instead.
    //
    // It is right almost every time, because the thing that just hit the player is
    // overwhelmingly the thing standing closest to them, and when it is wrong the cost is
    // that a flinch leans the other way for a fifth of a second.
    private float WhichSideTheHitCameFrom()
    {
        // The overload that takes no sort mode is the non-deprecated one, and asking it
        // to exclude inactive objects means a corpse mid-collapse is not mistaken for
        // whatever just hit us.
        EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);

        float nearestDistance = float.MaxValue;
        Vector3 nearestPosition = transform.position + transform.forward;
        bool foundOne = false;

        int index = 0;
        while (index < enemies.Length)
        {
            EnemyBrain enemy = enemies[index];
            if (enemy != null)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPosition = enemy.transform.position;
                    foundOne = true;
                }
            }
            index = index + 1;
        }

        if (foundOne == false)
        {
            return 0f;
        }

        Vector3 towardThem = nearestPosition - transform.position;
        towardThem.y = 0f;

        return Mathf.Sign(Vector3.Dot(towardThem.normalized, transform.right));
    }
}
