using UnityEngine;

// Arrows, and how few of them there are.
//
// The bow's costs up to now were all paid in the moment: the draw takes time, the string
// takes stamina, aiming takes your speed. Every one of them is forgotten the instant the
// shot leaves, so a patient archer paid nothing at all over the course of a fight - wait
// long enough between shots and the bow was free again.
//
// A quiver is the first cost the bow cannot wait out. Twenty arrows is a budget for the
// whole minute, which means the question stops being "can I afford this shot" and becomes
// "is this shot worth one of twenty" - and those are completely different questions. It
// is the same reason PlayerHealing gives out three potions rather than a potion cooldown.
//
// Kept as its own component rather than as two more fields on PlayerCombat, following the
// same split PlayerHealing and PlayerSurge already use: PlayerCombat decides what a shot
// IS, and this decides whether there is one to take.
public class PlayerQuiver : MonoBehaviour
{
    [Header("The quiver")]
    public int arrowsWhenFull = 20;

    // How long a full refill takes.
    //
    // The clock starts on the first arrow drawn from a full quiver rather than running
    // forever in the background. Anchoring it to the player's own first shot is what makes
    // it legible: the minute is visibly THEIRS, started by something they did, rather than
    // a hidden global cycle that refills at a moment they cannot predict and did not
    // cause.
    public float secondsToRefill = 60f;

    private int arrowsLeft;
    private float secondsUntilRefill = 0f;

    void Awake()
    {
        arrowsLeft = arrowsWhenFull;
    }

    void Update()
    {
        // Nothing to count down to while the quiver is already full.
        if (arrowsLeft >= arrowsWhenFull)
        {
            secondsUntilRefill = 0f;
            return;
        }

        secondsUntilRefill = secondsUntilRefill - Time.deltaTime;
        if (secondsUntilRefill <= 0f)
        {
            RefillNow();
        }
    }

    public int ArrowsLeft()
    {
        return arrowsLeft;
    }

    public int ArrowsWhenFull()
    {
        return arrowsWhenFull;
    }

    // How long until the quiver comes back, or zero when it is already full. Read by the
    // display.
    public float SecondsUntilRefill()
    {
        if (arrowsLeft >= arrowsWhenFull)
        {
            return 0f;
        }
        return secondsUntilRefill;
    }

    public bool HasAnArrow()
    {
        return arrowsLeft > 0;
    }

    // Take one, if there is one. Answers whether there was.
    //
    // The whole quiver comes back at once when the clock runs out, rather than an arrow
    // returning every three seconds. A trickle would mean an empty quiver is never really
    // empty - there is always another arrow along in a moment - and the player would
    // stand and wait for it, which is the opposite of what running out is supposed to
    // make them do. All twenty at once makes the empty quiver a real thing to plan
    // around, and the refill a moment worth noticing.
    public bool TryTakeAnArrow()
    {
        if (arrowsLeft <= 0)
        {
            return false;
        }

        // The clock starts on the arrow that breaks a full quiver, and is not restarted
        // by any of the ones after it. Restarting it per shot would let a careful player
        // push the refill away for ever by firing just often enough, so the twenty would
        // never actually run out.
        if (arrowsLeft >= arrowsWhenFull)
        {
            secondsUntilRefill = secondsToRefill;
        }

        arrowsLeft = arrowsLeft - 1;
        return true;
    }

    public void RefillNow()
    {
        bool wasShort = arrowsLeft < arrowsWhenFull;

        arrowsLeft = arrowsWhenFull;
        secondsUntilRefill = 0f;

        // Only announced when there was actually something to refill, so a round starting
        // with a full quiver is silent.
        if (wasShort == true)
        {
            GameSound.Play("EssencePickup", 0.5f);
        }
    }
}
