using UnityEngine;

// The Warden's Eye: the stone he was carrying, left where he fell.
//
// Picking it up puts a fourth weapon in the player's hands. The way home does not open
// until they have swung it at least once - a two second delay that guarantees nobody
// leaves the Vault without having felt the thing they just won. In a demo where somebody
// else is driving, that is the difference between the new weapon being the payoff and
// the new weapon being a line of text nobody read.
public class WardenGem : MonoBehaviour
{
    public float spinDegreesPerSecond = 45f;
    public float bobHeight = 0.30f;
    public float bobSpeed = 1.6f;

    private Vector3 restingPosition;
    private float secondsAlive = 0f;

    private bool hasBeenTaken = false;

    // Counted from the moment it is taken, so the "swing it" hint does not appear at the
    // same instant as the weapon name.
    private float secondsSinceTaken = 0f;
    private bool hasAskedForASwing = false;
    private bool hasOpenedTheWayHome = false;

    private PlayerCombat playerCombat;
    private int swingsAtTheMomentItWasTaken = 0;

    // How long the name of the weapon stays written across the screen. Read by the
    // display.
    public static float SecondsOfNameLeft = 0f;

    void Start()
    {
        restingPosition = transform.position;
    }

    void Update()
    {
        secondsAlive = secondsAlive + Time.deltaTime;

        if (hasBeenTaken == false)
        {
            Float();
            return;
        }

        WatchForTheFirstSwing();
    }

    private void Float()
    {
        transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);
        float bobOffset = Mathf.Sin(secondsAlive * bobSpeed) * bobHeight;
        transform.position = restingPosition + Vector3.up * bobOffset;
    }

    void OnTriggerEnter(Collider whoTouchedIt)
    {
        if (hasBeenTaken == true)
        {
            return;
        }
        if (whoTouchedIt.CompareTag("Player") == false)
        {
            return;
        }

        TakeIt(whoTouchedIt.gameObject);
    }

    private void TakeIt(GameObject player)
    {
        hasBeenTaken = true;
        secondsSinceTaken = 0f;

        GameSound.Play("GemShatter", 0.85f);

        PlayerWeapons weapons = player.GetComponent<PlayerWeapons>();
        if (weapons != null)
        {
            weapons.UnlockTheWardensEdge();
        }

        playerCombat = player.GetComponent<PlayerCombat>();
        if (playerCombat != null)
        {
            swingsAtTheMomentItWasTaken = playerCombat.SwingsMade();
        }

        // Four seconds of the weapon's name across the middle of the screen. Long enough
        // to read twice, which is what somebody watching over a shoulder needs.
        SecondsOfNameLeft = 4f;

        // The gem itself stops being a thing in the world. The glow is left behind for a
        // moment by the renderer being disabled rather than the object being destroyed,
        // because this script still has work to do.
        Renderer gemRenderer = GetComponent<Renderer>();
        if (gemRenderer != null)
        {
            gemRenderer.enabled = false;
        }
        Collider gemCollider = GetComponent<Collider>();
        if (gemCollider != null)
        {
            gemCollider.enabled = false;
        }

        Light gemLight = GetComponentInChildren<Light>();
        if (gemLight != null)
        {
            gemLight.enabled = false;
        }
    }

    private void WatchForTheFirstSwing()
    {
        secondsSinceTaken = secondsSinceTaken + Time.deltaTime;

        if (SecondsOfNameLeft > 0f)
        {
            SecondsOfNameLeft = SecondsOfNameLeft - Time.deltaTime;
        }

        if (hasOpenedTheWayHome == true)
        {
            return;
        }

        if (hasAskedForASwing == false && secondsSinceTaken > 3.4f)
        {
            hasAskedForASwing = true;
            if (DialogueBox.instance != null)
            {
                DialogueBox.instance.Murmur(StoryDirector.Orrin,
                    "Swing it. Once. I want to know what he was keeping down there.");
            }
        }

        if (playerCombat == null)
        {
            return;
        }

        if (playerCombat.SwingsMade() > swingsAtTheMomentItWasTaken)
        {
            hasOpenedTheWayHome = true;

            if (StoryDirector.instance != null)
            {
                StoryDirector.instance.OnTheWayHomeIsEarned();
            }
        }
    }

    // Builds the eye from scratch where the Warden fell. Called by the director, so
    // nothing has to exist in the scene ahead of time.
    public static void SpawnAt(Vector3 wherePosition)
    {
        GameObject gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gem.name = "TheWardensEye";
        gem.transform.position = wherePosition;
        gem.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
        // Tipped onto a corner so it reads as a cut stone rather than as a box.
        gem.transform.rotation = Quaternion.Euler(38f, 0f, 38f);

        Collider gemCollider = gem.GetComponent<Collider>();
        gemCollider.isTrigger = true;

        Renderer gemRenderer = gem.GetComponent<Renderer>();
        Material gemMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        // The same violet the portal uses, so the eye reads as belonging to the Vault
        // rather than to the creature that was carrying it.
        gemMaterial.color = new Color(0.45f, 0.18f, 0.85f);
        gemMaterial.EnableKeyword("_EMISSION");
        gemMaterial.SetColor("_EmissionColor", new Color(0.60f, 0.25f, 1f) * 3.4f);
        gemRenderer.material = gemMaterial;

        GameObject lightObject = new GameObject("EyeLight");
        lightObject.transform.SetParent(gem.transform);
        lightObject.transform.localPosition = Vector3.zero;
        Light gemLight = lightObject.AddComponent<Light>();
        gemLight.type = LightType.Point;
        gemLight.color = new Color(0.65f, 0.35f, 1f);
        gemLight.intensity = 4.5f;
        gemLight.range = 14f;

        gem.AddComponent<WardenGem>();
    }
}
