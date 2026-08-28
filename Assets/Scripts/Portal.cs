using UnityEngine;

// The gateway out of the valley.
//
// Appears in the arena once the fourth round is cleared, and carries the player to the
// Vault for the final fight. Deliberately something you WALK INTO rather than a fade to
// black: the player chooses the moment, which makes the last round feel entered rather
// than inflicted.
public class Portal : MonoBehaviour
{
    // There are three of these in the game now and they do not all mean the same thing,
    // so each one is told what it is for. The alternative - working it out from where it
    // happens to be standing - breaks the moment anything moves.
    public const int PurposeToTheVault = 0;
    public const int PurposeOutOfTheDungeon = 1;
    public const int PurposeHomeFromTheVault = 2;

    public int purpose = PurposeToTheVault;

    [Header("Where it leads")]
    public Vector3 destination = new Vector3(0f, 2f, 182f);

    [Header("Feel")]
    public float activationRadius = 2.6f;
    // A moment of hanging in the gate before the world changes, so the transition is
    // seen rather than skipped.
    public float secondsToPassThrough = 1.1f;
    public float riseSeconds = 2.2f;

    private Transform thePlayer;
    private PlayerMovement playerMovement;

    private Transform surface;
    private Renderer surfaceRenderer;
    private Light ownLight;

    private bool isOpen = false;
    private float openProgress = 0f;
    private float passingSecondsLeft = 0f;
    private bool hasCarriedThePlayer = false;

    private Vector3 raisedPosition;
    private Vector3 buriedPosition;

    void Awake()
    {
        raisedPosition = transform.position;
        buriedPosition = raisedPosition + Vector3.down * 8f;
        transform.position = buriedPosition;
    }

    void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            thePlayer = playerObject.transform;
            playerMovement = playerObject.GetComponent<PlayerMovement>();
        }
    }

    public void SetSurface(Transform which)
    {
        surface = which;
        if (surface != null)
        {
            surfaceRenderer = surface.GetComponent<Renderer>();
        }
    }

    public void SetLight(Light which)
    {
        ownLight = which;
    }

    // Called by the round system when the fourth round is cleared.
    public void Open()
    {
        if (isOpen == false)
        {
            GameSound.Play("PortalOpen", 0.8f);
        }
        isOpen = true;
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    public bool HasCarriedThePlayer()
    {
        return hasCarriedThePlayer;
    }

    void Update()
    {
        if (isOpen == false)
        {
            return;
        }

        RiseOutOfTheGround();
        Shimmer();

        if (hasCarriedThePlayer == true || thePlayer == null || openProgress < 1f)
        {
            return;
        }

        if (passingSecondsLeft > 0f)
        {
            passingSecondsLeft = passingSecondsLeft - Time.deltaTime;
            if (passingSecondsLeft <= 0f)
            {
                CarryThePlayerThrough();
            }
            return;
        }

        float distance = Vector3.Distance(thePlayer.position, transform.position);
        if (distance < activationRadius)
        {
            passingSecondsLeft = secondsToPassThrough;
        }
    }

    private void RiseOutOfTheGround()
    {
        if (openProgress >= 1f)
        {
            return;
        }

        openProgress = openProgress + Time.deltaTime / riseSeconds;
        if (openProgress > 1f)
        {
            openProgress = 1f;
        }

        float eased = 1f - (1f - openProgress) * (1f - openProgress);
        transform.position = Vector3.Lerp(buriedPosition, raisedPosition, eased);
    }

    // The surface breathes and the light pulses, so a stone arch reads as active rather
    // than as scenery the player has already walked past twice.
    private void Shimmer()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.1f);

        if (surfaceRenderer != null)
        {
            Color glow = Color.Lerp(
                new Color(0.42f, 0.12f, 0.72f),
                new Color(0.78f, 0.42f, 1f),
                pulse);

            if (surfaceRenderer.material.HasProperty("_BaseColor") == true)
            {
                surfaceRenderer.material.SetColor("_BaseColor", glow);
            }
            surfaceRenderer.material.SetColor("_EmissionColor", glow * (2.2f + pulse * 1.6f));
        }

        if (surface != null)
        {
            // Slowly turning, which is most of what separates a portal from a wall.
            surface.Rotate(0f, 0f, 26f * Time.deltaTime, Space.Self);
        }

        if (ownLight != null)
        {
            ownLight.intensity = (3.5f + pulse * 2.5f) * openProgress;
        }
    }

    private void CarryThePlayerThrough()
    {
        hasCarriedThePlayer = true;

        if (playerMovement != null)
        {
            playerMovement.TeleportTo(destination);
        }

        if (purpose == PurposeOutOfTheDungeon)
        {
            if (StoryDirector.instance != null)
            {
                StoryDirector.instance.OnPlayerReachedTheValley();
            }
            return;
        }

        if (purpose == PurposeHomeFromTheVault)
        {
            if (StoryDirector.instance != null)
            {
                StoryDirector.instance.OnPlayerReachedHome();
            }
            return;
        }

        if (RoundDirector.instance != null)
        {
            RoundDirector.instance.OnPlayerEnteredThePortal();
        }
    }
}
