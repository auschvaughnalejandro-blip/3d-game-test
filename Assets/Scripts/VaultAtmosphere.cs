using UnityEngine;

// Swaps the world lighting when the player passes into the Vault, and back if they ever
// return.
//
// Both arenas live in one scene, which means one sun and one set of fog and ambient
// settings between them. The valley wants daylight. The Vault wants near-darkness lit
// only by its braziers and its crystals - and until the daylight is turned off, the
// emissive materials in there are simply washed out and read as flat pale cutouts
// rather than as anything glowing.
public class VaultAtmosphere : MonoBehaviour
{
    public float secondsToCrossFade = 1.4f;

    // How dark the Vault actually is, gathered here so it can be dialled without hunting
    // through the code. Raise sunInside and the ambient colours to see more of the room;
    // push fogEndInside further out to stop the far wall being swallowed.
    //
    // These were originally set far lower, chosen to make the crystals and braziers glow.
    // They did - and made everything else invisible. A boss fight has to be readable
    // first: the player needs to see the Warden wind up, and see the ground shockwave
    // coming, or the fight is unfair rather than atmospheric.
    [Header("How dark it is inside")]
    public float sunInside = 0.95f;
    public Color sunColourInside = new Color(0.62f, 0.52f, 0.82f);

    public Color fogColourInside = new Color(0.11f, 0.085f, 0.16f);
    public float fogStartInside = 30f;
    public float fogEndInside = 140f;

    // Ambient does the heavy lifting, not the braziers.
    //
    // The floor and the wall are each ONE enormous mesh, and URP only lets a handful of
    // extra lights touch any single object - so most of the eight braziers contribute
    // nothing to the floor no matter how bright they are made. Turning them up further
    // only blows out the small patch nearest each one. Ambient light ignores that limit
    // entirely and is the only thing that actually raises the whole room.
    public Color ambientSkyInside = new Color(0.46f, 0.39f, 0.58f);
    public Color ambientEquatorInside = new Color(0.37f, 0.31f, 0.46f);
    public Color ambientGroundInside = new Color(0.22f, 0.18f, 0.28f);

    // What the valley looked like, captured on the way in so it can be put back.
    private Color valleyFogColour;
    private float valleyFogStart;
    private float valleyFogEnd;
    private Color valleyAmbientSky;
    private Color valleyAmbientEquator;
    private Color valleyAmbientGround;
    private Color valleySunColour;
    private float valleySunIntensity;
    private bool haveRememberedTheValley = false;

    private Light theSun;
    private Camera theCamera;

    private bool insideTheVault = false;
    private float crossFade = 0f;

    void Start()
    {
        theSun = Object.FindFirstObjectByType<Light>();
        theCamera = Camera.main;
        RememberTheValley();
    }

    private void RememberTheValley()
    {
        if (haveRememberedTheValley == true)
        {
            return;
        }

        valleyFogColour = RenderSettings.fogColor;
        valleyFogStart = RenderSettings.fogStartDistance;
        valleyFogEnd = RenderSettings.fogEndDistance;
        valleyAmbientSky = RenderSettings.ambientSkyColor;
        valleyAmbientEquator = RenderSettings.ambientEquatorColor;
        valleyAmbientGround = RenderSettings.ambientGroundColor;

        if (theSun != null)
        {
            valleySunColour = theSun.color;
            valleySunIntensity = theSun.intensity;
        }

        haveRememberedTheValley = true;
    }

    public void EnterTheVault()
    {
        RememberTheValley();
        insideTheVault = true;
    }

    public void ReturnToTheValley()
    {
        insideTheVault = false;
    }

    void Update()
    {
        float wanted = insideTheVault ? 1f : 0f;
        if (Mathf.Abs(crossFade - wanted) < 0.001f)
        {
            return;
        }

        float step = Time.deltaTime / secondsToCrossFade;
        if (crossFade < wanted)
        {
            crossFade = Mathf.Min(crossFade + step, wanted);
        }
        else
        {
            crossFade = Mathf.Max(crossFade - step, wanted);
        }

        ApplyBlend(crossFade);
    }

    private void ApplyBlend(float howFarIn)
    {
        RenderSettings.fogColor = Color.Lerp(valleyFogColour, fogColourInside, howFarIn);
        RenderSettings.fogStartDistance = Mathf.Lerp(valleyFogStart, fogStartInside, howFarIn);
        RenderSettings.fogEndDistance = Mathf.Lerp(valleyFogEnd, fogEndInside, howFarIn);

        RenderSettings.ambientSkyColor = Color.Lerp(
            valleyAmbientSky, ambientSkyInside, howFarIn);
        RenderSettings.ambientEquatorColor = Color.Lerp(
            valleyAmbientEquator, ambientEquatorInside, howFarIn);
        RenderSettings.ambientGroundColor = Color.Lerp(
            valleyAmbientGround, ambientGroundInside, howFarIn);

        if (theSun != null)
        {
            // Dimmed and tinted rather than switched off, so the room keeps a direction
            // to its light and shapes still read as solid.
            theSun.color = Color.Lerp(valleySunColour, sunColourInside, howFarIn);
            theSun.intensity = Mathf.Lerp(valleySunIntensity, sunInside, howFarIn);
        }

        if (theCamera != null)
        {
            // A solid dark background instead of the sky, which would otherwise still be
            // visible past the top of the dome.
            theCamera.clearFlags = howFarIn > 0.5f
                ? CameraClearFlags.SolidColor
                : CameraClearFlags.Skybox;
            theCamera.backgroundColor = fogColourInside;
        }
    }
}
