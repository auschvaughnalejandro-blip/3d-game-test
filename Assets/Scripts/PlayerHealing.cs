using UnityEngine;

// Potions.
//
// The whole design is that healing takes TIME. An instant heal is a button you press
// whenever the number is low, and it makes damage meaningless. A heal that takes over a
// second, cannot be started mid-swing, and is cancelled the moment something hits you,
// forces the player to earn the space first - which turns "I am hurt" into a decision
// about when to disengage rather than a reflex.
public class PlayerHealing : MonoBehaviour
{
    [Header("Charges")]
    public int maximumCharges = 3;
    public int chargesLeft = 3;

    [Header("The drink itself")]
    public float healthRestored = 40f;
    public float secondsToDrink = 1.2f;

    private CharacterStats ownStats;
    private PlayerMovement ownMovement;
    private PlayerCombat ownCombat;

    private float drinkingSecondsRemaining = 0f;
    private float healthAlreadyGiven = 0f;

    // The damage count at the moment the drink began. If it has changed by the next
    // frame, something hit us and the drink is spoiled.
    //
    // Watching the health NUMBER instead was the obvious approach and it was wrong: it
    // cannot tell a hit apart from any other reason health moved, so anything that
    // adjusted health - a test, a round reset, a shrine upgrade - read as being attacked
    // and silently cancelled the drink while still charging for it.
    private int damageCountWhenDrinkBegan = 0;

    // Held briefly so the display can say why a drink failed.
    private float refusedSecondsRemaining = 0f;
    private string refusalReason = "";

    void Awake()
    {
        ownStats = GetComponent<CharacterStats>();
        ownMovement = GetComponent<PlayerMovement>();
        ownCombat = GetComponent<PlayerCombat>();
    }

    void Start()
    {
        chargesLeft = maximumCharges;
    }

    void Update()
    {
        if (refusedSecondsRemaining > 0f)
        {
            refusedSecondsRemaining = refusedSecondsRemaining - Time.deltaTime;
        }

        if (ownStats.isDead == true)
        {
            drinkingSecondsRemaining = 0f;
            return;
        }

        // No drinking mid-conversation. Q is a long way from the dialogue keys, but the
        // rule is the same one the swing and the swap follow, and consistency here is
        // what stops the conversation feeling like the game is still running underneath.
        if (PlayerControl.IsBlocked() == true)
        {
            return;
        }

        if (drinkingSecondsRemaining > 0f)
        {
            ContinueDrinking();
        }
        else if (GameInput.HealWasPressed() == true)
        {
            TryToDrink();
        }
    }

    // Public so the action is separable from the key that triggers it. Update reads the
    // input and calls this; a test can call it directly. Keeping "what the button does"
    // apart from "which button it is" is worth doing regardless of testing.
    public bool TryToDrink()
    {
        if (chargesLeft <= 0)
        {
            Refuse("NO POTIONS LEFT");
            return false;
        }
        if (ownStats.currentHealth >= ownStats.maximumHealth)
        {
            Refuse("ALREADY AT FULL HEALTH");
            return false;
        }
        if (ownMovement != null && ownMovement.IsCurrentlyDodging() == true)
        {
            Refuse("CANNOT DRINK WHILE ROLLING");
            return false;
        }
        if (ownMovement != null && ownMovement.IsAirborne() == true)
        {
            Refuse("CANNOT DRINK IN MID-AIR");
            return false;
        }
        if (ownCombat != null && ownCombat.IsSwinging() == true)
        {
            Refuse("CANNOT DRINK MID-SWING");
            return false;
        }

        GameSound.Play("PotionDrink", 0.6f);

        chargesLeft = chargesLeft - 1;
        drinkingSecondsRemaining = secondsToDrink;
        healthAlreadyGiven = 0f;
        damageCountWhenDrinkBegan = ownStats.timesDamaged;
        return true;
    }

    private void ContinueDrinking()
    {
        // Interrupted by damage. The charge is still spent, which is the cost of drinking
        // at the wrong moment.
        if (ownStats.timesDamaged != damageCountWhenDrinkBegan)
        {
            drinkingSecondsRemaining = 0f;
            Refuse("INTERRUPTED");
            return;
        }

        float secondsThisFrame = Time.deltaTime;
        if (secondsThisFrame > drinkingSecondsRemaining)
        {
            secondsThisFrame = drinkingSecondsRemaining;
        }
        drinkingSecondsRemaining = drinkingSecondsRemaining - secondsThisFrame;

        // Health arrives steadily across the drink rather than all at the end, so
        // being interrupted halfway still leaves you with half the benefit.
        float shareThisFrame = (secondsThisFrame / secondsToDrink) * healthRestored;
        float roomLeft = ownStats.maximumHealth - ownStats.currentHealth;
        if (shareThisFrame > roomLeft)
        {
            shareThisFrame = roomLeft;
        }

        ownStats.currentHealth = ownStats.currentHealth + shareThisFrame;
        healthAlreadyGiven = healthAlreadyGiven + shareThisFrame;
    }

    private void Refuse(string reason)
    {
        refusalReason = reason;
        refusedSecondsRemaining = 1.4f;
    }

    // Called by the round system between rounds.
    public void RefillAllCharges()
    {
        chargesLeft = maximumCharges;
    }

    public void GrantExtraCharge()
    {
        maximumCharges = maximumCharges + 1;
        chargesLeft = chargesLeft + 1;
    }

    public bool IsDrinking()
    {
        return drinkingSecondsRemaining > 0f;
    }

    public float DrinkProgress()
    {
        if (secondsToDrink <= 0f)
        {
            return 0f;
        }
        return 1f - (drinkingSecondsRemaining / secondsToDrink);
    }

    public string RefusalMessage()
    {
        if (refusedSecondsRemaining > 0f)
        {
            return refusalReason;
        }
        return "";
    }
}
