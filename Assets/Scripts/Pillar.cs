using UnityEngine;

// A stone pillar. Cover, and eventually rubble.
//
// Two jobs. It rises out of the ground when a round begins, which is a cheap but strong
// beat for a round transition. And it can be broken, which is the heart of the Warden
// fight: the boss's ranged volley demands cover, and its charge destroys cover, so the
// arena grows steadily more dangerous the longer the fight lasts.
public class Pillar : MonoBehaviour
{
    [Header("Toughness")]
    // Two hits from a charging Warden. Enough that cover is not disposable, few enough
    // that a long fight visibly strips the arena bare.
    public int hitsToBreak = 2;
    private int hitsTaken = 0;

    [Header("Rising")]
    public float riseSeconds = 1.5f;

    private Vector3 raisedPosition;
    private Vector3 buriedPosition;
    private float riseProgress = 0f;
    private bool isRising = false;
    private bool isBroken = false;

    void Awake()
    {
        raisedPosition = transform.position;

        // Buried far enough down that no part of it pokes through the floor.
        float height = 3f;
        Renderer ownRenderer = GetComponent<Renderer>();
        if (ownRenderer != null)
        {
            height = ownRenderer.bounds.size.y;
        }
        buriedPosition = raisedPosition + Vector3.down * (height + 0.6f);
    }

    // Sinks it out of sight without any animation. Used when the valley is built, so the
    // pillars are not standing there before the round that raises them.
    public void HideImmediately()
    {
        transform.position = buriedPosition;
        riseProgress = 0f;
        isRising = false;
    }

    public void BeginRising()
    {
        if (isBroken == true)
        {
            return;
        }
        if (isRising == false && riseProgress < 1f)
        {
            GameSound.PlayAt("PillarRise", transform.position, 0.45f);
        }
        isRising = true;
    }

    void Update()
    {
        if (isRising == false || riseProgress >= 1f)
        {
            return;
        }

        riseProgress = riseProgress + Time.deltaTime / riseSeconds;
        if (riseProgress > 1f)
        {
            riseProgress = 1f;
            isRising = false;
        }

        // Eased so it slows as it arrives, which reads as something enormously heavy
        // grinding to a halt rather than a box sliding on rails.
        float eased = 1f - (1f - riseProgress) * (1f - riseProgress);
        transform.position = Vector3.Lerp(buriedPosition, raisedPosition, eased);
    }

    // Called when something heavy runs into it.
    public void TakeAHit()
    {
        if (isBroken == true)
        {
            return;
        }

        hitsTaken = hitsTaken + 1;

        if (hitsTaken < hitsToBreak)
        {
            // Chips fly off, so the player can see it is damaged and will not last.
            DeathBurst.SpawnAt(transform.position + Vector3.up * 1.2f,
                new Color(0.42f, 0.40f, 0.38f), 0.7f);
            return;
        }

        Shatter();
    }

    private void Shatter()
    {
        isBroken = true;
        GameSound.PlayAt("PillarBreak", transform.position, 0.85f);

        Renderer ownRenderer = GetComponent<Renderer>();
        Color rubbleColour = new Color(0.42f, 0.40f, 0.38f);
        if (ownRenderer != null && ownRenderer.material.HasProperty("_BaseColor") == true)
        {
            rubbleColour = ownRenderer.material.GetColor("_BaseColor");
        }

        // Three bursts up the height of the pillar, so it comes apart along its length
        // instead of puffing out of a single point.
        DeathBurst.SpawnAt(transform.position + Vector3.up * 0.6f, rubbleColour, 1.1f);
        DeathBurst.SpawnAt(transform.position + Vector3.up * 1.6f, rubbleColour, 1.0f);
        DeathBurst.SpawnAt(transform.position + Vector3.up * 2.6f, rubbleColour, 0.9f);

        gameObject.SetActive(false);
    }

    public bool IsBroken()
    {
        return isBroken;
    }
}
