using UnityEngine;

// WHAT EACH CREATURE LEAVES BEHIND AFTER IT HITS YOU.
//
// Until now every enemy hit did exactly one thing - subtract a number - and the only
// difference between the three creatures was how big that number was and how hard the
// swing was to avoid. That makes the roster read as one enemy at three difficulties.
//
// So each creature now leaves its own mark on the player:
//
//   Darter (the wolf)  BLEEDING   - 2 health a second, ticking, for six seconds.
//   Grunt              STUNNED    - 20% slower on foot for two seconds.
//   Spitter            WEAKENED   - the player's own attacks do half damage for five.
//
// Each one punishes the thing that creature is meant to punish. The Darter is the one
// you are supposed to step aside from, so being caught by it keeps costing after the
// charge is over. The Grunt is slow and readable, so being hit by one is the game saying
// you were too slow to matter - and it makes you slower. The Spitter sits on high stone
// out of reach, so its rock takes away the very thing you need to go and answer it.
//
// EVERY AILMENT REFRESHES RATHER THAN STACKING. This is the single most important rule
// in the file. Round two puts four Darters in the arena at once; if a second bite added
// a second bleed the player would be taking -8 a second through no mistake beyond being
// surrounded, and 100 health would be gone in twelve seconds with nothing to do about
// it. Refreshing means a second hit resets the clock and nothing more, so the cost of
// being hit is always the cost of ONE hit, however many creatures land it.
//
// The static accessors at the top are the same trick PlayerSurge uses: PlayerMovement
// and PlayerCombat ask this without holding a reference, and every one of them answers
// the harmless value when the component is missing entirely. A player built before this
// script existed plays exactly as it did before.
public class PlayerAilments : MonoBehaviour
{
    public static PlayerAilments instance;

    [Header("Bleeding - left by the Darter")]
    // Two health a second, taken in whole one-second bites rather than smeared across
    // every frame. A number that visibly drops by 2, pauses, and drops by 2 again reads
    // as a wound. The same total drained smoothly reads as the health bar being broken.
    public float bleedDamagePerTick = 2f;
    public float bleedLastsSeconds = 6f;

    [Header("Stunned - left by the Grunt")]
    // 0.8 means twenty per cent slower, which is what "a bit" is worth here: enough to
    // lose a step backing out of a second swing, not enough to feel like the controls
    // have stopped answering.
    public float stunMovementMultiplier = 0.8f;
    // Deliberately shorter than the Grunt's own 1.9 second attack cadence. At 2.5 the
    // slow would still be running when the next club came down and a single Grunt could
    // hold the player at reduced speed forever, which is not a status effect - it is a
    // permanent change to how fast the player walks.
    public float stunLastsSeconds = 2f;

    [Header("Weakened - left by the Spitter")]
    // "Two times less damage" - so every attack the player makes is halved.
    public float weakenDamageMultiplier = 0.5f;
    public float weakenLastsSeconds = 5f;

    // How long each one has left. Zero means the player does not have it.
    private float bleedSecondsRemaining = 0f;
    private float stunSecondsRemaining = 0f;
    private float weakenSecondsRemaining = 0f;

    // Counts down to the next bleed bite. Kept separate from the duration so the ticks
    // stay on their own rhythm when a fresh bite refreshes the timer mid-second.
    private float secondsUntilNextBleedTick = 0f;

    private const float SecondsBetweenBleedTicks = 1f;

    private CharacterStats ownStats;

    void Awake()
    {
        instance = this;
        ownStats = GetComponent<CharacterStats>();
    }

    // ------------------------------------------------------------------------
    // What the rest of the game asks
    // ------------------------------------------------------------------------

    // Multiply walking and sprinting speed by this. One when nothing is wrong.
    public static float MovementSpeedMultiplierNow()
    {
        if (instance == null)
        {
            return 1f;
        }
        if (instance.stunSecondsRemaining > 0f)
        {
            return instance.stunMovementMultiplier;
        }
        return 1f;
    }

    // Multiply any damage the PLAYER deals by this. One when nothing is wrong.
    //
    // This is the player's outgoing damage only. Nothing an enemy does is routed through
    // here, so a weakened player still takes full damage - being weakened is not the same
    // as being fragile, and confusing the two would make the Spitter the most dangerous
    // thing in the valley rather than the most annoying.
    public static float OutgoingDamageMultiplierNow()
    {
        if (instance == null)
        {
            return 1f;
        }
        if (instance.weakenSecondsRemaining > 0f)
        {
            return instance.weakenDamageMultiplier;
        }
        return 1f;
    }

    // Called from every path that puts the player back on their feet - dying, loading a
    // checkpoint, starting a new run, restarting a round. Safe to call when the component
    // is missing, which is why it is static.
    public static void ClearEverythingNow()
    {
        if (instance == null)
        {
            return;
        }
        instance.ClearEverything();
    }

    public void ClearEverything()
    {
        bleedSecondsRemaining = 0f;
        stunSecondsRemaining = 0f;
        weakenSecondsRemaining = 0f;
        secondsUntilNextBleedTick = 0f;
    }

    // ------------------------------------------------------------------------
    // Catching an ailment
    // ------------------------------------------------------------------------

    // THE ONE PLACE that decides which creature leaves which mark.
    //
    // Called with the same displayName the round plans, the coach lines and the surge
    // meter already use, so a fourth creature gets an effect by being named here and
    // nowhere else. Anything not named - which today means the Warden - leaves no
    // ailment at all: the boss already has three phases, a slam, a volley and burning
    // ground, and it does not need a fourth thing happening to the player on top.
    public static void ApplyForAttackerNamed(string enemyName)
    {
        if (instance == null)
        {
            return;
        }

        if (enemyName == "Darter")
        {
            instance.BeginBleeding();
        }
        else if (enemyName == "Grunt")
        {
            instance.BeginStun();
        }
        else if (enemyName == "Spitter")
        {
            instance.BeginWeakness();
        }
    }

    public void BeginBleeding()
    {
        if (ownStats != null && ownStats.isDead == true)
        {
            return;
        }

        // Asked BEFORE the duration is refreshed, because refreshing it is what destroys
        // the answer.
        bool wasAlreadyBleeding = bleedSecondsRemaining > 0f;

        // Only the moment a wound OPENS. A second bite on an already-bleeding player
        // extends the clock without re-announcing itself - the state has not changed, so
        // saying so again is just noise.
        if (wasAlreadyBleeding == false)
        {
            GameSound.Play("Weakened", 0.7f);
        }

        // Refreshed, not added to. See the note at the top of the file.
        bleedSecondsRemaining = bleedLastsSeconds;

        // A fresh wound's first tick lands a full second after the bite, never instantly.
        // The Darter's own 20 damage has already been taken by the time this is called,
        // and a bleed tick in the same frame would read as the charge having hit for 22
        // rather than as a wound that then opened.
        //
        // A wound that was ALREADY bleeding keeps the rhythm it had, so a second bite
        // extends the bleeding without also resetting the clock on the next tick - which
        // would otherwise let a player being bitten steadily take almost no bleed damage
        // at all, each bite pushing the next tick back out of reach.
        if (wasAlreadyBleeding == false)
        {
            secondsUntilNextBleedTick = SecondsBetweenBleedTicks;
        }
    }

    public void BeginStun()
    {
        if (ownStats != null && ownStats.isDead == true)
        {
            return;
        }
        stunSecondsRemaining = stunLastsSeconds;

        // Control has just been taken away, and that is the one thing a player must
        // never have to work out for themselves. Until now a stun was silent and looked
        // exactly like the controls having stopped working.
        GameSound.Play("Stunned", 0.85f);
    }

    public void BeginWeakness()
    {
        if (ownStats != null && ownStats.isDead == true)
        {
            return;
        }
        weakenSecondsRemaining = weakenLastsSeconds;
        GameSound.Play("Weakened", 0.6f);
    }

    // ------------------------------------------------------------------------
    // Running the clocks
    // ------------------------------------------------------------------------

    void Update()
    {
        // Dying clears everything, exactly as it does for the kill streak. Every path
        // that restarts a round or reloads a checkpoint goes through the player being
        // dead first, so this alone covers most of them - the explicit calls elsewhere
        // are belt and braces for the ones that set isDead back to false in the same
        // frame they cleared it.
        if (ownStats != null && ownStats.isDead == true)
        {
            ClearEverything();
            return;
        }

        // Frozen while the game is not being played. Bleeding out behind a pause menu or
        // during a conversation with Orrin would be damage the player was given no chance
        // to answer, and the first time it killed somebody it would read as a bug.
        if (MainMenu.IsShowing() == true || PlayerControl.IsBlocked() == true)
        {
            return;
        }

        CountDownStun();
        CountDownWeakness();
        RunTheBleed();
    }

    private void CountDownStun()
    {
        if (stunSecondsRemaining <= 0f)
        {
            return;
        }

        stunSecondsRemaining = stunSecondsRemaining - Time.deltaTime;
        if (stunSecondsRemaining < 0f)
        {
            stunSecondsRemaining = 0f;
        }
    }

    private void CountDownWeakness()
    {
        if (weakenSecondsRemaining <= 0f)
        {
            return;
        }

        weakenSecondsRemaining = weakenSecondsRemaining - Time.deltaTime;
        if (weakenSecondsRemaining < 0f)
        {
            weakenSecondsRemaining = 0f;
        }
    }

    private void RunTheBleed()
    {
        if (bleedSecondsRemaining <= 0f)
        {
            return;
        }

        bleedSecondsRemaining = bleedSecondsRemaining - Time.deltaTime;
        if (bleedSecondsRemaining < 0f)
        {
            bleedSecondsRemaining = 0f;
        }

        secondsUntilNextBleedTick = secondsUntilNextBleedTick - Time.deltaTime;
        if (secondsUntilNextBleedTick > 0f)
        {
            return;
        }

        secondsUntilNextBleedTick = SecondsBetweenBleedTicks;
        TakeOneBleedTick();
    }

    private void TakeOneBleedTick()
    {
        if (ownStats == null || ownStats.isDead == true)
        {
            return;
        }

        // Quiet on purpose. This fires once a second for the whole wound, so anything
        // with a real transient in it becomes unbearable by the fourth tick.
        GameSound.Play("BleedTick", 0.5f);

        ownStats.TakeDamage(bleedDamagePerTick);

        // A bleed CAN be the thing that kills the player, so it has to report the death
        // the same way every other source of damage does. Leaving this out would drop the
        // player to zero health and simply carry on playing.
        if (ownStats.isDead == true && GameDirector.instance != null)
        {
            GameDirector.instance.OnPlayerDied();
            ClearEverything();
        }
    }

    // ------------------------------------------------------------------------
    // What the HUD asks
    // ------------------------------------------------------------------------

    public bool IsBleeding()
    {
        return bleedSecondsRemaining > 0f;
    }

    public bool IsStunned()
    {
        return stunSecondsRemaining > 0f;
    }

    public bool IsWeakened()
    {
        return weakenSecondsRemaining > 0f;
    }

    public float BleedSecondsRemaining()
    {
        return bleedSecondsRemaining;
    }

    public float StunSecondsRemaining()
    {
        return stunSecondsRemaining;
    }

    public float WeakenSecondsRemaining()
    {
        return weakenSecondsRemaining;
    }
}
