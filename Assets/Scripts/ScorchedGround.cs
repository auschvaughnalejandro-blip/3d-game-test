using UnityEngine;

// A patch of burning floor left where the Warden came down.
//
// This exists to take the arena away. Every other pressure in the boss fight is a thing
// that happens and then stops - a slam lands, a volley arrives, a charge goes past - and
// between them the floor is exactly as safe as it was at the start. A player who was
// willing to keep walking could always find somewhere to stand.
//
// Scorch marks do not stop. Each one is a piece of the room that is no longer available,
// and because the Warden leaves them where it LANDS, and it lands wherever the player
// was standing, the places being taken away are precisely the places the player likes.
// The pillars already work this way for cover; this does it for floor.
public class ScorchedGround : MonoBehaviour
{
    [Header("How long it burns")]
    public float lastsSeconds = 6f;
    // Long enough to read as cooling rather than as vanishing.
    public float fadesOverLastSeconds = 1.5f;

    [Header("What it does to anyone standing in it")]
    public float damagePerSecond = 14f;
    public float radius = 5f;

    private float secondsLived = 0f;
    private Transform thePlayer;
    private CharacterStats playerStats;
    private Material ownMaterial;

    // Damage is dealt in whole bites on a timer rather than smoothly every frame.
    //
    // A per-frame trickle is invisible: the health bar creeps, no hit sound ever fires
    // because a hit sound every frame would be a drone, and the player reads it as a bug
    // in the health bar rather than as something hurting them. A bite they can hear and
    // see once every third of a second is a thing happening TO them.
    private const float SecondsBetweenBites = 0.34f;
    private float secondsUntilNextBite = 0f;

    public static ScorchedGround SpawnAt(Vector3 where, float radius, float lastsSeconds)
    {
        GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        patch.name = "ScorchedGround";

        // Just above the floor, so the two surfaces do not fight over which one is drawn.
        patch.transform.position = where + Vector3.up * 0.05f;
        patch.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

        // A flattened cylinder primitive keeps its CAPSULE collider, and a capsule
        // squashed into a wide disc becomes a huge invisible dome that throws anything
        // standing on it into the air. This is a decoration and must not collide with
        // anything - the damage below is done by measuring distance, not by touching.
        Collider strayCollider = patch.GetComponent<Collider>();
        if (strayCollider != null)
        {
            Object.DestroyImmediate(strayCollider);
        }

        Material burning = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        burning.color = new Color(0.35f, 0.10f, 0.04f);
        burning.EnableKeyword("_EMISSION");
        burning.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.08f) * 2.4f);
        patch.GetComponent<Renderer>().material = burning;

        ScorchedGround scorch = patch.AddComponent<ScorchedGround>();
        scorch.radius = radius;
        scorch.lastsSeconds = lastsSeconds;
        scorch.ownMaterial = burning;

        return scorch;
    }

    void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            thePlayer = playerObject.transform;
            playerStats = playerObject.GetComponent<CharacterStats>();
        }
    }

    void Update()
    {
        secondsLived = secondsLived + Time.deltaTime;

        if (secondsLived >= lastsSeconds)
        {
            Destroy(gameObject);
            return;
        }

        FadeAsItCools();
        BurnAnyoneStandingInIt();
    }

    private void FadeAsItCools()
    {
        if (ownMaterial == null)
        {
            return;
        }

        float secondsLeft = lastsSeconds - secondsLived;
        if (secondsLeft > fadesOverLastSeconds)
        {
            return;
        }

        // Only the glow is faded, not the scorch itself. A patch that is still dangerous
        // must not look as though it has already gone out, so the fade is timed to the
        // end of its life rather than run across the whole of it.
        float howBrightStill = secondsLeft / fadesOverLastSeconds;
        ownMaterial.SetColor("_EmissionColor",
            new Color(1f, 0.35f, 0.08f) * 2.4f * howBrightStill);
    }

    private void BurnAnyoneStandingInIt()
    {
        if (secondsUntilNextBite > 0f)
        {
            secondsUntilNextBite = secondsUntilNextBite - Time.deltaTime;
            return;
        }

        if (playerStats == null || playerStats.isDead == true || thePlayer == null)
        {
            return;
        }

        // Measured flat. Jumping does not clear a fire you are standing over - the
        // shockwave is the move that rewards a jump, and if everything rewarded a jump
        // then jumping would be the whole game.
        Vector3 flatToPlayer = thePlayer.position - transform.position;
        flatToPlayer.y = 0f;

        if (flatToPlayer.magnitude > radius)
        {
            return;
        }

        secondsUntilNextBite = SecondsBetweenBites;

        playerStats.TakeDamage(damagePerSecond * SecondsBetweenBites);
        GameSound.PlayAt("RockImpact", thePlayer.position, 0.35f);

        if (playerStats.isDead == true && GameDirector.instance != null)
        {
            GameDirector.instance.OnPlayerDied();
        }
    }
}
