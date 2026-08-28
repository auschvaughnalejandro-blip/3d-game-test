using UnityEngine;

// The shrine near the valley entrance where essence is spent on permanent upgrades.
// It only tracks whether the player is standing close enough; the heads-up display
// asks it that question and draws the prompt, and the director does the actual sums.
public class ShrineOfEssence : MonoBehaviour
{
    public float interactionRadius = 4f;

    private Transform thePlayer;
    private float pulseSeconds = 0f;
    private Renderer ownRenderer;

    // Held briefly after a purchase so the display can confirm it happened.
    private float confirmationSecondsRemaining = 0f;
    private string lastPurchaseName = "";

    void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            thePlayer = playerObject.transform;
        }
        ownRenderer = GetComponentInChildren<Renderer>();
    }

    void Update()
    {
        pulseSeconds = pulseSeconds + Time.deltaTime;

        if (confirmationSecondsRemaining > 0f)
        {
            confirmationSecondsRemaining = confirmationSecondsRemaining - Time.deltaTime;
        }

        // A slow rotation so the shrine reads as important rather than as scenery.
        transform.Rotate(Vector3.up, 25f * Time.deltaTime, Space.World);

        if (PlayerIsCloseEnough() == false)
        {
            return;
        }

        WatchForUpgradeKeys();
    }

    private void WatchForUpgradeKeys()
    {
        int chosenUpgrade = GameInput.WhichUpgradeKeyWasPressed();

        if (chosenUpgrade == 0)
        {
            return;
        }
        if (GameDirector.instance == null)
        {
            return;
        }

        bool purchaseWorked = GameDirector.instance.TryBuyUpgrade(chosenUpgrade);
        if (purchaseWorked == true)
        {
            GameSound.Play("ShrineBuy", 0.7f);
            confirmationSecondsRemaining = 1.6f;

            if (chosenUpgrade == 1)
            {
                lastPurchaseName = "VITALITY RAISED";
            }
            else if (chosenUpgrade == 2)
            {
                lastPurchaseName = "STRENGTH RAISED";
            }
            else
            {
                lastPurchaseName = "ENDURANCE RAISED";
            }
        }
    }

    public bool PlayerIsCloseEnough()
    {
        if (thePlayer == null)
        {
            return false;
        }
        return Vector3.Distance(transform.position, thePlayer.position) <= interactionRadius;
    }

    public string ConfirmationMessage()
    {
        if (confirmationSecondsRemaining > 0f)
        {
            return lastPurchaseName;
        }
        return "";
    }
}
