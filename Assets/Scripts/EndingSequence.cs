using UnityEngine;

// The last ninety seconds: the walk north, the world changing behind you, and the title.
//
// This is the only part of the game written for somebody watching rather than somebody
// playing. The lens cycle at the end is the whole pitch in one shot - the same valley,
// the same geometry, the same fight that just happened, drawn four completely different
// ways without reloading anything. Saying that out loud takes a paragraph. Showing it
// takes six seconds.
public class EndingSequence : MonoBehaviour
{
    // Armed by the story once Orrin has said his last line. Nothing happens until the
    // player actually walks north, because being told to leave and then being taken over
    // by a cutscene is a worse ending than walking out under your own power.
    private bool isArmed = false;
    private bool isRolling = false;

    // How far north the player has to get before the ending takes over. The north gate
    // is at z = 33, so this is a little way up the road beyond it.
    public float roadBeginsAtZ = 44f;

    private Transform playerTransform;
    private StyleLens theLens;
    private OrbitCamera theCamera;

    private float secondsRolling = 0f;

    // Which lens is being shown now, as an index into the sequence below.
    private int lensStep = -1;

    // NEON, CHALK, NOIR, and home to NATURAL. Deliberately not in the order the Tab key
    // cycles them: the strongest two go first while the audience is still looking.
    private static readonly int[] LensOrder = new int[] { 2, 3, 1, 0 };

    public float secondsPerLens = 1.7f;
    public float secondsBeforeFirstLens = 1.2f;

    // Drawing.
    private Texture2D onePlainWhitePixel;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle lensNameStyle;
    private bool stylesHaveBeenBuilt = false;

    void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }

        theLens = Object.FindFirstObjectByType<StyleLens>();

        if (Camera.main != null)
        {
            theCamera = Camera.main.GetComponent<OrbitCamera>();
        }

        onePlainWhitePixel = new Texture2D(1, 1);
        onePlainWhitePixel.SetPixel(0, 0, Color.white);
        onePlainWhitePixel.Apply();
    }

    // Called by the story director when the last conversation is over.
    public void Begin()
    {
        isArmed = true;

        if (DialogueBox.instance != null)
        {
            DialogueBox.instance.Murmur(StoryDirector.Orrin, "North, then. Go on.");
        }
    }

    void Update()
    {
        if (isRolling == true)
        {
            RunTheEnding();
            return;
        }

        if (isArmed == false || playerTransform == null)
        {
            return;
        }

        if (playerTransform.position.z >= roadBeginsAtZ)
        {
            isRolling = true;
            secondsRolling = 0f;
        }
    }

    private void RunTheEnding()
    {
        secondsRolling = secondsRolling + Time.deltaTime;

        LiftTheCamera();
        ChangeTheLensOnSchedule();
    }

    // The camera pulls back and rises as the player walks away, which is the oldest
    // ending shot there is and works every time.
    private void LiftTheCamera()
    {
        if (theCamera == null)
        {
            return;
        }

        float howFarIn = secondsRolling / 9f;
        if (howFarIn > 1f)
        {
            howFarIn = 1f;
        }

        theCamera.distanceBehindTarget = Mathf.Lerp(7f, 17f, howFarIn);
        theCamera.heightAboveTarget = Mathf.Lerp(2.2f, 8.5f, howFarIn);
    }

    private void ChangeTheLensOnSchedule()
    {
        if (theLens == null)
        {
            return;
        }

        float sinceFirstLens = secondsRolling - secondsBeforeFirstLens;
        if (sinceFirstLens < 0f)
        {
            return;
        }

        int stepWeShouldBeOn = Mathf.FloorToInt(sinceFirstLens / secondsPerLens);
        if (stepWeShouldBeOn >= LensOrder.Length)
        {
            stepWeShouldBeOn = LensOrder.Length - 1;
        }

        if (stepWeShouldBeOn == lensStep)
        {
            return;
        }

        lensStep = stepWeShouldBeOn;
        theLens.ApplyStyle(LensOrder[lensStep]);
        GameSound.Play("UiClick", 0.4f);
    }

    // ------------------------------------------------------------------------
    // The title card
    // ------------------------------------------------------------------------

    private void BuildStylesIfNeeded()
    {
        if (stylesHaveBeenBuilt == true)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 62;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        subtitleStyle = new GUIStyle(GUI.skin.label);
        subtitleStyle.fontSize = 22;
        subtitleStyle.alignment = TextAnchor.MiddleCenter;

        lensNameStyle = new GUIStyle(GUI.skin.label);
        lensNameStyle.fontSize = 16;
        lensNameStyle.fontStyle = FontStyle.Bold;
        lensNameStyle.alignment = TextAnchor.MiddleCenter;

        stylesHaveBeenBuilt = true;
    }

    void OnGUI()
    {
        if (isRolling == false)
        {
            return;
        }

        BuildStylesIfNeeded();

        DrawTheLensName();
        DrawTheTitle();
    }

    // The name of the lens currently being drawn through, small and low. Without it the
    // audience sees the art change and assumes they walked into a different area.
    private void DrawTheLensName()
    {
        if (theLens == null || lensStep < 0)
        {
            return;
        }

        float secondsUntilTitle = secondsBeforeFirstLens + secondsPerLens * LensOrder.Length;
        if (secondsRolling > secondsUntilTitle + 1f)
        {
            return;
        }

        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        GUI.Label(new Rect(0f, Screen.height - 78f, Screen.width, 24f),
            "LENS  -  " + theLens.CurrentStyleName(), lensNameStyle);
        GUI.color = Color.white;
    }

    private void DrawTheTitle()
    {
        float secondsUntilTitle = secondsBeforeFirstLens + secondsPerLens * LensOrder.Length;
        float sinceTitle = secondsRolling - secondsUntilTitle;
        if (sinceTitle < 0f)
        {
            return;
        }

        // Fades up over a second and a half and then stays.
        float opacity = sinceTitle / 1.5f;
        if (opacity > 1f)
        {
            opacity = 1f;
        }

        // A band across the middle rather than a full black screen, so the world the
        // title is talking about is still visible behind it.
        GUI.color = new Color(0f, 0f, 0f, 0.55f * opacity);
        GUI.DrawTexture(new Rect(0f, Screen.height * 0.32f, Screen.width, 190f), onePlainWhitePixel);

        GUI.color = new Color(0.96f, 0.95f, 1f, opacity);
        GUI.Label(new Rect(0f, Screen.height * 0.34f, Screen.width, 80f), "ONE VALLEY", titleStyle);

        GUI.color = new Color(0.72f, 0.62f, 0.98f, opacity);
        GUI.Label(new Rect(0f, Screen.height * 0.34f + 82f, Screen.width, 34f),
            "the first of many", subtitleStyle);

        GUI.color = Color.white;
    }

    // Read by the self test so it can confirm the ending actually ran rather than
    // assuming it did.
    public bool HasReachedTheTitle()
    {
        if (isRolling == false)
        {
            return false;
        }
        float secondsUntilTitle = secondsBeforeFirstLens + secondsPerLens * LensOrder.Length;
        return secondsRolling >= secondsUntilTitle;
    }

    public bool IsRolling()
    {
        return isRolling;
    }
}
