using UnityEngine;

// Shared by the player AND by every enemy.
// One place decides how much health a thing has, so as far as damage is concerned the
// player and a monster are the same kind of object. This is what lets one combat
// system serve both sides without duplicated code.
public class CharacterStats : MonoBehaviour
{
    [Header("Health")]
    public float maximumHealth = 100f;
    public float currentHealth = 100f;

    [Header("Stamina - only the player actually spends this")]
    public float maximumStamina = 100f;
    public float currentStamina = 100f;
    public float staminaRefilledPerSecond = 25f;

    [Header("Offence")]
    public float attackDamage = 20f;

    [Header("Reward")]
    // How much essence this character drops when killed. The player leaves this at zero.
    public int essenceDroppedOnDeath = 0;

    public bool isDead = false;

    // Counts how many times this character has been damaged. Anything that needs to know
    // "was I hit since a moment ago" takes a copy of this and compares later, which is
    // exact - unlike watching the health number, which cannot tell a hit apart from any
    // other reason health changed.
    public int timesDamaged = 0;

    // Renderers are cached once at startup so the white hit-flash does not have to
    // search the object hierarchy on every single hit.
    private Renderer[] ownRenderers;
    private Color[] originalColours;
    private float flashSecondsRemaining = 0f;

    private const float FlashTotalSeconds = 0.12f;

    void Awake()
    {
        currentHealth = maximumHealth;
        currentStamina = maximumStamina;

        ownRenderers = GetComponentsInChildren<Renderer>();
        originalColours = new Color[ownRenderers.Length];

        int rendererIndex = 0;
        while (rendererIndex < ownRenderers.Length)
        {
            // Reading .material rather than .sharedMaterial gives this object its own
            // private copy of the material. That is what stops one enemy flashing white
            // from flashing every other enemy that shares the same material.
            originalColours[rendererIndex] = ReadColourOf(ownRenderers[rendererIndex].material);
            rendererIndex = rendererIndex + 1;
        }
    }

    // Material.color reads a property named "_Color". The custom shaders in this project
    // expose "_BaseColor" instead, so asking blindly logs an error per renderer per
    // frame the flash runs. This asks what is actually there.
    private Color ReadColourOf(Material material)
    {
        if (material == null)
        {
            return Color.grey;
        }
        if (material.HasProperty("_BaseColor") == true)
        {
            return material.GetColor("_BaseColor");
        }
        if (material.HasProperty("_Tint") == true)
        {
            return material.GetColor("_Tint");
        }
        if (material.HasProperty("_Color") == true)
        {
            return material.GetColor("_Color");
        }
        return Color.grey;
    }

    private void PaintRenderer(Renderer whichRenderer, Color colour)
    {
        Material material = whichRenderer.material;
        if (material.HasProperty("_BaseColor") == true)
        {
            material.SetColor("_BaseColor", colour);
        }
        else if (material.HasProperty("_Tint") == true)
        {
            material.SetColor("_Tint", colour);
        }
        else if (material.HasProperty("_Color") == true)
        {
            material.SetColor("_Color", colour);
        }
    }

    void Update()
    {
        RefillStaminaOverTime();
        FadeTheHitFlash();
    }

    private void RefillStaminaOverTime()
    {
        if (currentStamina < maximumStamina)
        {
            currentStamina = currentStamina + staminaRefilledPerSecond * Time.deltaTime;
            if (currentStamina > maximumStamina)
            {
                currentStamina = maximumStamina;
            }
        }
    }

    private void FadeTheHitFlash()
    {
        if (flashSecondsRemaining <= 0f)
        {
            return;
        }

        flashSecondsRemaining = flashSecondsRemaining - Time.deltaTime;

        // How far back toward the normal colour we have travelled, from 0 to 1.
        float returnProgress = 1f - (flashSecondsRemaining / FlashTotalSeconds);
        if (returnProgress > 1f)
        {
            returnProgress = 1f;
        }

        int rendererIndex = 0;
        while (rendererIndex < ownRenderers.Length)
        {
            if (ownRenderers[rendererIndex] != null)
            {
                Color blended = Color.Lerp(Color.white, originalColours[rendererIndex], returnProgress);
                PaintRenderer(ownRenderers[rendererIndex], blended);
            }
            rendererIndex = rendererIndex + 1;
        }
    }

    // Returns true when this hit was the killing blow, so the attacker can react to it.
    public bool TakeDamage(float damageAmount)
    {
        if (isDead == true)
        {
            return false;
        }

        currentHealth = currentHealth - damageAmount;
        timesDamaged = timesDamaged + 1;

        // Only the player grunts when hit. Enemies have their own sounds on the blow
        // that landed, and doubling them up turns a fight into mush.
        if (CompareTag("Player") == true)
        {
            GameSound.Play("PlayerHurt", 0.7f);
        }
        flashSecondsRemaining = FlashTotalSeconds;

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;
            return true;
        }
        return false;
    }

    // Returns false when there was not enough stamina, so the caller can refuse the
    // action instead of letting the player dodge forever.
    public bool TrySpendStamina(float amountToSpend)
    {
        if (currentStamina < amountToSpend)
        {
            return false;
        }
        currentStamina = currentStamina - amountToSpend;
        return true;
    }

    public void RestoreEverything()
    {
        currentHealth = maximumHealth;
        currentStamina = maximumStamina;
        isDead = false;
        flashSecondsRemaining = 0f;

        int rendererIndex = 0;
        while (rendererIndex < ownRenderers.Length)
        {
            if (ownRenderers[rendererIndex] != null)
            {
                PaintRenderer(ownRenderers[rendererIndex], originalColours[rendererIndex]);
            }
            rendererIndex = rendererIndex + 1;
        }
    }

    // Called when the visual style changes, because the lens hands every object a brand
    // new material and the colours cached at startup are no longer the right ones.
    public void RecacheOriginalColours()
    {
        ownRenderers = GetComponentsInChildren<Renderer>();
        originalColours = new Color[ownRenderers.Length];

        int rendererIndex = 0;
        while (rendererIndex < ownRenderers.Length)
        {
            originalColours[rendererIndex] = ReadColourOf(ownRenderers[rendererIndex].material);
            rendererIndex = rendererIndex + 1;
        }
    }

    // The body's real colour, ignoring any hit-flash currently painted over it. The
    // death burst asks for this, because reading the live material colour during the
    // frame a killing blow lands returns the white of the flash instead.
    public Color BodyColour()
    {
        if (originalColours == null || originalColours.Length == 0)
        {
            return Color.grey;
        }
        return originalColours[0];
    }

    public float HealthAsFraction()
    {
        return currentHealth / maximumHealth;
    }

    public float StaminaAsFraction()
    {
        return currentStamina / maximumStamina;
    }
}
