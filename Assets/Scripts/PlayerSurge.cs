using UnityEngine;

// THE KILL STREAK, AND THE REWARD FOR ONE.
//
// Every kill drops experience points into a meter. The meter leaks one point every
// second, all the time, so points are only ever worth anything while they are FRESH.
// Fill it to fifteen before it drains and the player gets five seconds of being faster
// and hitting more often.
//
// The leak is the whole design. Without it the meter is just a counter and every player
// reaches fifteen eventually, which rewards nothing. With it, fifteen points can only be
// reached by killing several things close together - so the reward is for AGGRESSION,
// specifically for wading into a group instead of picking it apart from the edge of the
// arena. That is the exact habit the five rounds otherwise teach players out of, which is
// why it needs paying for.
public class PlayerSurge : MonoBehaviour
{
    // A single well-known instance, the same trick GameDirector uses. Movement and combat
    // ask this rather than having a reference wired up, because the surge component may be
    // added after those scripts have already woken up.
    public static PlayerSurge instance;

    [Header("What each kill is worth")]
    // A Grunt is slow and common, so it is worth least. A Darter is the fastest thing in
    // the valley and the hardest to land a swing on, so it is worth most. A Spitter sits
    // up on the high stone and has to be answered with the bow, which costs time - so it
    // sits in between.
    public int pointsForKillingAGrunt = 2;
    public int pointsForKillingADarter = 4;
    public int pointsForKillingASpitter = 3;

    [Header("The meter")]
    // Reaching this starts the surge.
    public float pointsNeededForTheSurge = 15f;
    // How fast the meter empties. One point per second, drained smoothly rather than in
    // whole steps, so the bar on screen slides down instead of ticking.
    public float pointsLostPerSecond = 1f;

    [Header("The reward")]
    public float surgeLastsSeconds = 5f;
    // Half again as fast on foot. Enough to close a gap that was not closeable a second
    // ago, which is what makes the surge worth spending on attacking rather than fleeing.
    public float surgeMovementSpeedMultiplier = 1.5f;
    // Attacks come out this many times more often. Cooldowns are DIVIDED by this, so 1.8
    // turns the sword's 0.45 second recovery into 0.25, and the bow's 1.4 second full draw
    // into 0.78.
    public float surgeAttacksThisMuchFaster = 1.8f;

    // Kept as a float rather than a whole number purely so the drain is smooth. Kills add
    // whole points; only the leak works in fractions.
    private float currentPoints = 0f;
    private float surgeSecondsRemaining = 0f;

    private CharacterStats ownStats;

    void Awake()
    {
        instance = this;
        ownStats = GetComponent<CharacterStats>();
    }

    // ------------------------------------------------------------------------
    // What the rest of the game asks
    // ------------------------------------------------------------------------

    // These two are static so PlayerMovement and PlayerCombat can ask without holding a
    // reference. Both answer 1 - meaning "change nothing" - when there is no surge
    // component at all, so the game plays exactly as it did before if this script is
    // missing from the player.
    public static float MovementSpeedMultiplierNow()
    {
        if (instance == null)
        {
            return 1f;
        }
        return instance.MovementSpeedMultiplier();
    }

    // Multiply any attack cooldown or draw time by this. It is BELOW one during a surge,
    // because a shorter wait is a higher rate of fire.
    public static float AttackTimingMultiplierNow()
    {
        if (instance == null)
        {
            return 1f;
        }
        return instance.AttackTimingMultiplier();
    }

    public float MovementSpeedMultiplier()
    {
        if (surgeSecondsRemaining > 0f)
        {
            return surgeMovementSpeedMultiplier;
        }
        return 1f;
    }

    public float AttackTimingMultiplier()
    {
        // Guarded against a zero left in the inspector, which would otherwise divide by
        // nothing and make every cooldown infinite - a power-up that silently stops the
        // player attacking at all.
        if (surgeSecondsRemaining > 0f && surgeAttacksThisMuchFaster > 0f)
        {
            return 1f / surgeAttacksThisMuchFaster;
        }
        return 1f;
    }

    public bool SurgeIsActive()
    {
        return surgeSecondsRemaining > 0f;
    }

    public float SurgeSecondsRemaining()
    {
        return surgeSecondsRemaining;
    }

    public float CurrentPoints()
    {
        return currentPoints;
    }

    // Nought to one, for the bar on screen.
    public float PointsAsFraction()
    {
        if (pointsNeededForTheSurge <= 0f)
        {
            return 0f;
        }

        float fraction = currentPoints / pointsNeededForTheSurge;
        if (fraction > 1f)
        {
            fraction = 1f;
        }
        return fraction;
    }

    // ------------------------------------------------------------------------
    // Earning points
    // ------------------------------------------------------------------------

    // Called by GameDirector, which is the one place every enemy death already reports to.
    // The enemy is identified by the same displayName the coach lines and the round plans
    // use, so a new enemy becomes worth points by being named here and nowhere else.
    public void AwardPointsForKilling(string enemyName)
    {
        // While the reward is running the meter is frozen and kills bank nothing.
        //
        // Two reasons. The bar on screen shows a COUNTDOWN during those five seconds, not
        // points, so quietly banking points behind it would make the display a lie. And
        // without this the surplus is earned and then thrown away: a full play-through of
        // the five rounds banked up to 48 points during a single surge, and every one of
        // them was lost the moment the next surge zeroed the meter.
        if (surgeSecondsRemaining > 0f)
        {
            return;
        }

        int pointsEarned = PointsWorthFor(enemyName);
        if (pointsEarned <= 0)
        {
            return;
        }

        currentPoints = currentPoints + pointsEarned;

        // No need to re-check that a surge is not already running - the guard at the top
        // of this method means a kill during a surge never reaches here at all.
        if (currentPoints >= pointsNeededForTheSurge)
        {
            StartTheSurge();
        }
    }

    private int PointsWorthFor(string enemyName)
    {
        if (enemyName == "Grunt")
        {
            return pointsForKillingAGrunt;
        }
        if (enemyName == "Darter")
        {
            return pointsForKillingADarter;
        }
        if (enemyName == "Spitter")
        {
            return pointsForKillingASpitter;
        }

        // Anything else - which means the Warden - is worth nothing. He dies once, at the
        // end, and a surge earned at that moment would have nothing left to spend itself
        // on.
        return 0;
    }

    private void StartTheSurge()
    {
        surgeSecondsRemaining = surgeLastsSeconds;

        // The meter is SPENT, not merely passed. Left at fifteen it would climb back to
        // the threshold the instant the surge ended and fire again off a single kill,
        // turning a reward for a streak into a permanent state.
        currentPoints = 0f;

        // Its own sound at last. The surge is the best thing that happens to the player
        // in a fight and it had been announcing itself with the noise a door makes.
        GameSound.Play("SurgeActivate", 0.75f);
    }

    // ------------------------------------------------------------------------
    // The leak, and the countdown
    // ------------------------------------------------------------------------

    void Update()
    {
        // Dying ends both the streak and the reward. Every path that restarts a round or
        // resets the valley goes through the player dying first, so clearing here covers
        // all of them without any of them having to know this script exists.
        if (ownStats != null && ownStats.isDead == true)
        {
            ClearEverything();
            return;
        }

        if (surgeSecondsRemaining > 0f)
        {
            surgeSecondsRemaining = surgeSecondsRemaining - Time.deltaTime;
            if (surgeSecondsRemaining < 0f)
            {
                surgeSecondsRemaining = 0f;
            }
        }

        // The meter leaks even while a conversation is open. That sounds unfair, but the
        // alternative is a player banking a streak across a chat with Orrin and cashing it
        // in afterwards, which is not a streak.
        if (currentPoints > 0f)
        {
            currentPoints = currentPoints - pointsLostPerSecond * Time.deltaTime;
            if (currentPoints < 0f)
            {
                currentPoints = 0f;
            }
        }
    }

    public void ClearEverything()
    {
        currentPoints = 0f;
        surgeSecondsRemaining = 0f;
    }
}
