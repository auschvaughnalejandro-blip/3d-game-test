using UnityEngine;

// The title screen and the pause screen.
//
// The title screen is drawn straight over the living scene rather than over a picture or
// a black background: the camera is already sitting behind the player in the dungeon,
// looking down the hall at the lit doorway, and that is a better title card than anything
// that could be painted. It also means there is no second scene to load and nothing to
// keep in step with the first.
//
// Time is stopped while either screen is up. That is belt and braces alongside
// PlayerControl.IsBlocked: the gate stops input being read, and the stopped clock stops
// anything that is already in motion from carrying on behind the menu.
//
// What this does NOT do is touch the mouse. CursorControl asks IsShowing once a frame and
// frees the pointer for as long as the answer is yes. Every script that used to set the
// cursor for itself has been stripped of it, because two scripts each certain they knew
// where the mouse belonged is exactly how the buttons on these screens ended up
// unclickable.
public class MainMenu : MonoBehaviour
{
    private static MainMenu instance;

    private const int ScreenTitle = 0;
    private const int ScreenPlaying = 1;
    private const int ScreenPaused = 2;
    private const int ScreenDead = 3;

    private int screen = ScreenTitle;

    // Set by the automated play-through, which has no hands and cannot press New Game.
    public static bool skipStraightIntoTheGame = false;

    private SavedGame theSaveOnDisk;

    private Texture2D onePlainWhitePixel;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallStyle;
    private bool stylesHaveBeenBuilt = false;

    private static readonly Color Violet = new Color(0.72f, 0.55f, 1f);
    private static readonly Color Blood = new Color(0.86f, 0.24f, 0.24f);

    void Awake()
    {
        instance = this;

        // Read once, here, rather than every frame the title screen is drawn - touching
        // the disk sixty times a second to ask the same question would be silly.
        theSaveOnDisk = SavedGame.Load();
    }

    void Start()
    {
        onePlainWhitePixel = new Texture2D(1, 1);
        onePlainWhitePixel.SetPixel(0, 0, Color.white);
        onePlainWhitePixel.Apply();

        ShowTheTitleScreen();
    }

    // Anything that reads the keyboard asks this before it acts.
    public static bool IsShowing()
    {
        if (instance == null)
        {
            return false;
        }
        return instance.screen != ScreenPlaying;
    }

    // Put up by GameDirector a couple of seconds after the player is killed, once the
    // HUD caption has had its moment.
    //
    // Static, because the thing that knows the player has died is the referee, and handing
    // it a reference to the menu wired up by hand is exactly the sort of thing that gets
    // half done. Everything else about the screen comes free from being a screen: IsShowing
    // already answers true for anything that is not ScreenPlaying, so input is gated, the
    // HUD hides itself, the clock stops and CursorControl hands the pointer back. None of
    // that has to be repeated here.
    public static void ShowTheDeathScreen()
    {
        if (instance == null)
        {
            return;
        }

        // Asked twice. Reading the disk again would be for nothing.
        if (instance.screen == ScreenDead)
        {
            return;
        }

        instance.ShowDeath();
    }

    private void ShowDeath()
    {
        // Read fresh rather than trusting the copy taken in Awake. Checkpoints are written
        // as the run goes along - at the start of every round, and at each act of the
        // story - so what is on disk when the player dies in round four is a very
        // different thing from what was there when the game booted.
        theSaveOnDisk = SavedGame.Load();

        screen = ScreenDead;
        StopTheWorld();
    }

    void Update()
    {
        // The self test sets this before anything else runs. Checked every frame rather
        // than only at startup, because which script reaches Start first is not something
        // to rely on.
        if (skipStraightIntoTheGame == true && screen != ScreenPlaying)
        {
            skipStraightIntoTheGame = false;
            StartANewGame();
            return;
        }

        if (screen == ScreenPlaying)
        {
            // Escape pauses rather than releasing the mouse. Letting go of the mouse used
            // to be all Escape did, which is fine in an editor and baffling in a game.
            if (GameInput.EscapeWasPressed() == true && DialogueBox.ConversationIsOpen() == false)
            {
                PauseTheGame();
            }
            return;
        }

        if (screen == ScreenDead)
        {
            // Escape deliberately does nothing. There is no un-pausing being dead - the
            // only ways on are the two buttons.
            return;
        }

        if (screen == ScreenPaused)
        {
            if (GameInput.EscapeWasPressed() == true)
            {
                ResumePlaying();
            }
        }
    }

    // ------------------------------------------------------------------------
    // Moving between screens
    // ------------------------------------------------------------------------

    private void ShowTheTitleScreen()
    {
        screen = ScreenTitle;
        StopTheWorld();
    }

    private void PauseTheGame()
    {
        screen = ScreenPaused;
        StopTheWorld();
    }

    private void StopTheWorld()
    {
        // The clock, and only the clock. The mouse belongs to CursorControl, which frees
        // it for as long as IsShowing keeps saying a menu is up. Setting it here as well
        // was the older arrangement and it was worse than useless: it put the pointer
        // right for one frame and then let anything else that fancied it take the pointer
        // straight back, leaving buttons that could be seen but never clicked.
        Time.timeScale = 0f;
    }

    private void ResumePlaying()
    {
        screen = ScreenPlaying;
        Time.timeScale = 1f;

        // The mouse is recaptured by CursorControl later in this same frame, because
        // IsShowing has just started answering false.
    }

    private void StartANewGame()
    {
        if (GameProgress.instance != null)
        {
            GameProgress.instance.BeginANewRun();
        }
        theSaveOnDisk = new SavedGame();
        ResumePlaying();
    }

    private void ContinueTheSavedGame()
    {
        if (GameProgress.instance != null)
        {
            GameProgress.instance.ResumeFrom(theSaveOnDisk);
        }
        ResumePlaying();
    }

    private void SaveAndReturnToTheTitle()
    {
        if (GameProgress.instance != null)
        {
            GameProgress.instance.SaveCheckpoint(GameProgress.instance.DescribeWhereWeAre());
        }
        theSaveOnDisk = SavedGame.Load();
        ShowTheTitleScreen();
    }

    // Chosen from the death screen. Note what this does NOT do: unlike the pause screen,
    // it does not save on the way out. Writing a checkpoint here would overwrite the one
    // the player is about to be offered on the title screen with the exact moment they
    // died, which is the one moment nobody wants back.
    private void GiveUpAndReturnToTheTitle()
    {
        theSaveOnDisk = SavedGame.Load();
        ShowTheTitleScreen();
    }

    private void QuitTheGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        // Application.Quit does nothing at all inside the editor, so the play button has
        // to be un-pressed by hand instead.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------------

    private void BuildStylesIfNeeded()
    {
        if (stylesHaveBeenBuilt == true)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 64;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        subtitleStyle = new GUIStyle(GUI.skin.label);
        subtitleStyle.fontSize = 20;
        subtitleStyle.alignment = TextAnchor.MiddleCenter;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 22;
        buttonStyle.fontStyle = FontStyle.Bold;

        smallStyle = new GUIStyle(GUI.skin.label);
        smallStyle.fontSize = 14;
        smallStyle.alignment = TextAnchor.MiddleCenter;

        stylesHaveBeenBuilt = true;
    }

    void OnGUI()
    {
        if (screen == ScreenPlaying)
        {
            return;
        }

        BuildStylesIfNeeded();

        if (screen == ScreenTitle)
        {
            DrawTheTitleScreen();
        }
        else if (screen == ScreenDead)
        {
            DrawTheDeathScreen();
        }
        else
        {
            DrawThePauseScreen();
        }
    }

    private void DrawTheTitleScreen()
    {
        // Darkened rather than blacked out, so the dungeon behind is still visible. The
        // lit doorway showing through the title is most of why this reads as a game
        // rather than as a dialog box.
        GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.66f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), onePlainWhitePixel);

        GUI.color = new Color(0.96f, 0.95f, 1f);
        GUI.Label(new Rect(0f, Screen.height * 0.16f, Screen.width, 84f), "ONE VALLEY", titleStyle);

        GUI.color = Violet;
        GUI.Label(new Rect(0f, Screen.height * 0.16f + 82f, Screen.width, 30f),
            "the first of many", subtitleStyle);

        float buttonWidth = 320f;
        float buttonLeft = (Screen.width - buttonWidth) * 0.5f;
        float buttonTop = Screen.height * 0.44f;

        GUI.color = Color.white;

        if (theSaveOnDisk.thereIsSomethingSaved == true)
        {
            if (GUI.Button(new Rect(buttonLeft, buttonTop, buttonWidth, 52f), "Continue", buttonStyle) == true)
            {
                ContinueTheSavedGame();
            }

            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            GUI.Label(new Rect(buttonLeft, buttonTop + 54f, buttonWidth, 22f),
                theSaveOnDisk.whereYouWere + "   -   " + theSaveOnDisk.whenYouSavedIt, smallStyle);
            GUI.color = Color.white;
        }
        else
        {
            // Shown but dead, rather than hidden. A button that appears once there is
            // something to load is more confusing than one that is obviously not ready.
            GUI.enabled = false;
            GUI.Button(new Rect(buttonLeft, buttonTop, buttonWidth, 52f), "Continue", buttonStyle);
            GUI.enabled = true;

            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(new Rect(buttonLeft, buttonTop + 54f, buttonWidth, 22f),
                "no saved game yet", smallStyle);
            GUI.color = Color.white;
        }

        if (GUI.Button(new Rect(buttonLeft, buttonTop + 90f, buttonWidth, 52f), "New Game", buttonStyle) == true)
        {
            StartANewGame();
        }

        if (GUI.Button(new Rect(buttonLeft, buttonTop + 154f, buttonWidth, 44f), "Quit", buttonStyle) == true)
        {
            QuitTheGame();
        }

        GUI.color = new Color(1f, 1f, 1f, 0.45f);
        GUI.Label(new Rect(0f, Screen.height - 42f, Screen.width, 22f),
            "WASD move   -   mouse look   -   E speak   -   V first person   -   "
            + "TAB change lens   -   ESC pause", smallStyle);
        GUI.color = Color.white;
    }

    private void DrawTheDeathScreen()
    {
        // Darker than the pause screen, and red rather than blue-black. The scene behind
        // is still faintly visible, because whatever killed you is worth a last look.
        GUI.color = new Color(0.10f, 0.01f, 0.02f, 0.78f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), onePlainWhitePixel);

        GUI.color = Blood;
        GUI.Label(new Rect(0f, Screen.height * 0.20f, Screen.width, 84f),
            "YOU ARE DEAD", titleStyle);

        string whereYouFell = "";
        if (GameProgress.instance != null)
        {
            whereYouFell = GameProgress.instance.DescribeWhereWeAre();
        }

        GUI.color = new Color(1f, 1f, 1f, 0.75f);
        GUI.Label(new Rect(0f, Screen.height * 0.20f + 82f, Screen.width, 28f),
            whereYouFell, subtitleStyle);

        float buttonWidth = 340f;
        float buttonLeft = (Screen.width - buttonWidth) * 0.5f;
        float buttonTop = Screen.height * 0.46f;

        GUI.color = Color.white;

        // The checkpoint goes first, because it is what somebody who has just died is
        // reaching for - the same reason Continue sits above New Game on the title screen.
        if (theSaveOnDisk.thereIsSomethingSaved == true)
        {
            if (GUI.Button(new Rect(buttonLeft, buttonTop, buttonWidth, 52f),
                "Load Last Checkpoint", buttonStyle) == true)
            {
                // The same load the title screen does. Checkpoints are written at the
                // start of each round, so this puts the player back at the beginning of
                // the round that killed them, with the stats they had going into it.
                ContinueTheSavedGame();
            }

            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            GUI.Label(new Rect(buttonLeft, buttonTop + 54f, buttonWidth, 22f),
                theSaveOnDisk.whereYouWere + "   -   " + theSaveOnDisk.whenYouSavedIt, smallStyle);
            GUI.color = Color.white;
        }
        else
        {
            // Shown but dead, for the same reason the title screen does it: a button that
            // only appears sometimes is more confusing than one that is plainly not ready.
            GUI.enabled = false;
            GUI.Button(new Rect(buttonLeft, buttonTop, buttonWidth, 52f),
                "Load Last Checkpoint", buttonStyle);
            GUI.enabled = true;

            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(new Rect(buttonLeft, buttonTop + 54f, buttonWidth, 22f),
                "no checkpoint reached yet", smallStyle);
            GUI.color = Color.white;
        }

        if (GUI.Button(new Rect(buttonLeft, buttonTop + 90f, buttonWidth, 52f),
            "Back to Main Menu", buttonStyle) == true)
        {
            GiveUpAndReturnToTheTitle();
        }
    }

    private void DrawThePauseScreen()
    {
        GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.72f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), onePlainWhitePixel);

        GUI.color = new Color(0.96f, 0.95f, 1f);
        GUI.Label(new Rect(0f, Screen.height * 0.22f, Screen.width, 60f), "PAUSED", titleStyle);

        string whereWeAre = "";
        if (GameProgress.instance != null)
        {
            whereWeAre = GameProgress.instance.DescribeWhereWeAre();
        }

        GUI.color = Violet;
        GUI.Label(new Rect(0f, Screen.height * 0.22f + 76f, Screen.width, 28f), whereWeAre, subtitleStyle);

        float buttonWidth = 340f;
        float buttonLeft = (Screen.width - buttonWidth) * 0.5f;
        float buttonTop = Screen.height * 0.46f;

        GUI.color = Color.white;

        if (GUI.Button(new Rect(buttonLeft, buttonTop, buttonWidth, 52f), "Resume", buttonStyle) == true)
        {
            ResumePlaying();
        }

        if (GUI.Button(new Rect(buttonLeft, buttonTop + 64f, buttonWidth, 52f),
            "Save and Quit to Title", buttonStyle) == true)
        {
            SaveAndReturnToTheTitle();
        }

        if (GUI.Button(new Rect(buttonLeft, buttonTop + 128f, buttonWidth, 44f), "Quit to Desktop", buttonStyle) == true)
        {
            // Saved on the way out, so closing the window is never the thing that loses a
            // run. The checkpoints already cover a crash; this covers an ordinary quit.
            if (GameProgress.instance != null)
            {
                GameProgress.instance.SaveCheckpoint(GameProgress.instance.DescribeWhereWeAre());
            }
            QuitTheGame();
        }

        GUI.color = new Color(1f, 1f, 1f, 0.45f);
        GUI.Label(new Rect(0f, Screen.height - 42f, Screen.width, 22f),
            "ESC to go back", smallStyle);
        GUI.color = Color.white;
    }
}
