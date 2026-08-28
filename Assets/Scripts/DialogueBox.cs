using System.Collections.Generic;
using UnityEngine;

// One line of speech, waiting its turn.
public class SpokenLine
{
    public string speaker = "";
    public string words = "";

    // A question stops the queue dead until the player presses Y or N. Anything that is
    // not a question is advanced with space, E or a click.
    public bool isAQuestion = false;

    // Questions are given a name so that whoever asked can recognise the answer when it
    // comes back. This is deliberately a plain string rather than a callback: the story
    // is easier to follow when asking and answering are written in the same place and
    // read top to bottom.
    public string questionName = "";

    public string yesLabel = "Yes";
    public string noLabel = "No";
}

// Everything anybody says out loud, on two separate channels.
//
// The two channels exist because a conversation and a tutorial hint are opposite things.
// A conversation should stop the world: the player is standing still talking to somebody
// and nothing is trying to kill them. A hint arrives in the middle of a fight, and
// freezing the game to deliver it would be infuriating - so hints scroll past at the
// bottom of the screen and are never waited on.
//
// Mixing those two up is the usual way tutorials become annoying, so they are kept as
// separate queues that cannot interfere with each other.
public class DialogueBox : MonoBehaviour
{
    public static DialogueBox instance;

    // ---- the blocking channel: conversations --------------------------------

    private List<SpokenLine> conversationQueue = new List<SpokenLine>();
    private SpokenLine lineBeingSpoken = null;

    // Letters appear one at a time. Reading a line arrive is more engaging than having it
    // land fully formed, and it gives the player a reason to look at the box at all.
    private float lettersRevealed = 0f;
    public float lettersPerSecond = 55f;

    // Set for exactly one frame after a question is answered, so whoever asked can
    // notice. Cleared at the top of the next Update.
    private string questionJustAnswered = "";
    private bool lastAnswerWasYes = false;

    // ---- the non-blocking channel: murmurs ----------------------------------

    private List<SpokenLine> murmurQueue = new List<SpokenLine>();
    private SpokenLine murmurShowing = null;
    private float murmurSecondsLeft = 0f;

    // Long enough to read a two-line hint without hurrying, short enough that it is gone
    // before the next one needs the space.
    public float murmurSecondsOnScreen = 5.5f;

    // ---- drawing ------------------------------------------------------------

    private Texture2D onePlainWhitePixel;
    private GUIStyle speakerStyle;
    private GUIStyle wordsStyle;
    private GUIStyle promptStyle;
    private GUIStyle murmurStyle;
    private bool stylesHaveBeenBuilt = false;

    private static readonly Color SpeakerColour = new Color(0.72f, 0.55f, 1f);
    private static readonly Color MurmurColour = new Color(0.80f, 0.72f, 0.98f);

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        onePlainWhitePixel = new Texture2D(1, 1);
        onePlainWhitePixel.SetPixel(0, 0, Color.white);
        onePlainWhitePixel.Apply();
    }

    // ------------------------------------------------------------------------
    // What the rest of the game calls
    // ------------------------------------------------------------------------

    // Queue one line of conversation. The world stops until it has been read.
    public void Say(string whoIsSpeaking, string whatTheySay)
    {
        SpokenLine line = new SpokenLine();
        line.speaker = whoIsSpeaking;
        line.words = whatTheySay;
        line.isAQuestion = false;
        conversationQueue.Add(line);
    }

    // Queue a question. Nothing after it is spoken until it has been answered.
    public void Ask(string whoIsSpeaking, string whatTheyAsk, string nameForTheQuestion,
        string labelForYes, string labelForNo)
    {
        SpokenLine line = new SpokenLine();
        line.speaker = whoIsSpeaking;
        line.words = whatTheyAsk;
        line.isAQuestion = true;
        line.questionName = nameForTheQuestion;
        line.yesLabel = labelForYes;
        line.noLabel = labelForNo;
        conversationQueue.Add(line);
    }

    // Queue a hint. It appears at the bottom of the screen and fades on its own, and the
    // player never has to press anything.
    public void Murmur(string whoIsSpeaking, string whatTheySay)
    {
        SpokenLine line = new SpokenLine();
        line.speaker = whoIsSpeaking;
        line.words = whatTheySay;
        murmurQueue.Add(line);
    }

    // True while a conversation is on screen. Movement, the camera, swinging and drinking
    // all check this, which is how the world holds still while somebody is talking.
    public static bool ConversationIsOpen()
    {
        if (instance == null)
        {
            return false;
        }
        if (instance.lineBeingSpoken != null)
        {
            return true;
        }
        return instance.conversationQueue.Count > 0;
    }

    // The name of the question answered this frame, or an empty string on every other
    // frame. The story director watches this.
    public string AnsweredQuestionName()
    {
        return questionJustAnswered;
    }

    public bool AnswerWasYes()
    {
        return lastAnswerWasYes;
    }

    // Is there a question on screen right now waiting to be answered?
    public bool AQuestionIsWaiting()
    {
        if (lineBeingSpoken == null)
        {
            return false;
        }
        return lineBeingSpoken.isAQuestion;
    }

    // Answer the question on screen without touching the keyboard. The automated
    // play-through uses this, and it is the seam a gamepad or a mouse-clickable button
    // would come in through later.
    public void AnswerTheQuestion(bool theAnswerIsYes)
    {
        if (AQuestionIsWaiting() == false)
        {
            return;
        }
        FinishTheQuestion(theAnswerIsYes);
    }

    // Move a plain line along without touching the keyboard. Does nothing to a question,
    // which must be answered rather than skipped.
    public void SkipTheCurrentLine()
    {
        if (lineBeingSpoken == null || lineBeingSpoken.isAQuestion == true)
        {
            return;
        }

        lineBeingSpoken = null;
    }

    // Throws away anything still queued. Used when the story moves on regardless.
    public void ClearEverything()
    {
        conversationQueue.Clear();
        lineBeingSpoken = null;
        murmurQueue.Clear();
        murmurShowing = null;
        murmurSecondsLeft = 0f;
    }

    // ------------------------------------------------------------------------
    // Running the queues
    // ------------------------------------------------------------------------

    void Update()
    {
        // The answer only survives for the frame after it was given, so clearing it at
        // the top of the next Update is exactly right.
        questionJustAnswered = "";

        // Nothing said out loud moves on while a menu is up. A click is one of the three
        // ways to advance a line, so without this the very click that presses Resume is
        // also read as "next line" and swallows the line waiting behind the menu - and it
        // was a conversation ending that way, with the game not actually running, that
        // used to take the mouse away and kill the rest of the buttons with it.
        if (MainMenu.IsShowing() == true)
        {
            return;
        }

        AdvanceTheConversation();
        AdvanceTheMurmurs();
    }

    private void AdvanceTheConversation()
    {
        if (lineBeingSpoken == null)
        {
            if (conversationQueue.Count == 0)
            {
                return;
            }

            lineBeingSpoken = conversationQueue[0];
            conversationQueue.RemoveAt(0);
            lettersRevealed = 0f;

            // The mouse is released for as long as the conversation lasts, so the player
            // can see what they are answering. CursorControl does that by watching
            // ConversationIsOpen - which has just become true - rather than it being set
            // from here, so that a conversation which happens to end underneath a menu
            // can no longer take the pointer off that menu.
            return;
        }

        bool everyLetterIsShowing = lettersRevealed >= lineBeingSpoken.words.Length;
        if (everyLetterIsShowing == false)
        {
            lettersRevealed = lettersRevealed + lettersPerSecond * Time.unscaledDeltaTime;
        }

        if (lineBeingSpoken.isAQuestion == true)
        {
            // A question cannot be answered before it has finished being asked, which
            // stops somebody mashing space from accidentally agreeing to fight a boss.
            if (everyLetterIsShowing == false)
            {
                return;
            }

            if (GameInput.YesWasPressed() == true)
            {
                FinishTheQuestion(true);
            }
            else if (GameInput.NoWasPressed() == true)
            {
                FinishTheQuestion(false);
            }
            return;
        }

        if (GameInput.ContinueWasPressed() == true)
        {
            if (everyLetterIsShowing == false)
            {
                // First press fills the line in rather than skipping it. This is the
                // convention every game with a text box uses, and players expect it.
                lettersRevealed = lineBeingSpoken.words.Length;
                return;
            }

            lineBeingSpoken = null;
            GameSound.Play("UiClick", 0.35f);
        }
    }

    private void FinishTheQuestion(bool theAnswerWasYes)
    {
        questionJustAnswered = lineBeingSpoken.questionName;
        lastAnswerWasYes = theAnswerWasYes;

        lineBeingSpoken = null;
        GameSound.Play("UiClick", 0.5f);
    }

    private void AdvanceTheMurmurs()
    {
        if (murmurShowing != null)
        {
            murmurSecondsLeft = murmurSecondsLeft - Time.deltaTime;
            if (murmurSecondsLeft <= 0f)
            {
                murmurShowing = null;
            }
            return;
        }

        if (murmurQueue.Count == 0)
        {
            return;
        }

        murmurShowing = murmurQueue[0];
        murmurQueue.RemoveAt(0);
        murmurSecondsLeft = murmurSecondsOnScreen;
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

        speakerStyle = new GUIStyle(GUI.skin.label);
        speakerStyle.fontSize = 18;
        speakerStyle.fontStyle = FontStyle.Bold;

        wordsStyle = new GUIStyle(GUI.skin.label);
        wordsStyle.fontSize = 21;
        wordsStyle.wordWrap = true;

        promptStyle = new GUIStyle(GUI.skin.label);
        promptStyle.fontSize = 16;
        promptStyle.alignment = TextAnchor.MiddleRight;

        murmurStyle = new GUIStyle(GUI.skin.label);
        murmurStyle.fontSize = 19;
        murmurStyle.fontStyle = FontStyle.Bold;
        murmurStyle.alignment = TextAnchor.MiddleCenter;
        murmurStyle.wordWrap = true;

        stylesHaveBeenBuilt = true;
    }

    void OnGUI()
    {
        if (MainMenu.IsShowing() == true)
        {
            return;
        }

        BuildStylesIfNeeded();

        DrawTheMurmur();
        DrawTheConversation();
    }

    private void DrawTheConversation()
    {
        if (lineBeingSpoken == null)
        {
            return;
        }

        float boxWidth = Screen.width * 0.66f;
        if (boxWidth > 900f)
        {
            boxWidth = 900f;
        }
        float boxHeight = 168f;
        float boxLeft = (Screen.width - boxWidth) * 0.5f;
        float boxTop = Screen.height - boxHeight - 70f;

        GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.88f);
        GUI.DrawTexture(new Rect(boxLeft, boxTop, boxWidth, boxHeight), onePlainWhitePixel);

        // A thin lit edge along the top. Without it the box is a black rectangle and
        // reads as a bug rather than as part of the game.
        GUI.color = new Color(0.55f, 0.38f, 0.95f, 0.9f);
        GUI.DrawTexture(new Rect(boxLeft, boxTop, boxWidth, 2f), onePlainWhitePixel);

        GUI.color = SpeakerColour;
        GUI.Label(new Rect(boxLeft + 24f, boxTop + 12f, boxWidth - 48f, 26f),
            lineBeingSpoken.speaker, speakerStyle);

        int lettersToShow = Mathf.FloorToInt(lettersRevealed);
        if (lettersToShow > lineBeingSpoken.words.Length)
        {
            lettersToShow = lineBeingSpoken.words.Length;
        }
        string visibleWords = lineBeingSpoken.words.Substring(0, lettersToShow);

        GUI.color = new Color(0.94f, 0.94f, 0.97f);
        GUI.Label(new Rect(boxLeft + 24f, boxTop + 44f, boxWidth - 48f, boxHeight - 78f),
            visibleWords, wordsStyle);

        bool everyLetterIsShowing = lettersToShow >= lineBeingSpoken.words.Length;

        if (lineBeingSpoken.isAQuestion == true)
        {
            if (everyLetterIsShowing == true)
            {
                GUI.color = new Color(0.55f, 0.95f, 0.72f);
                GUI.Label(new Rect(boxLeft + 24f, boxTop + boxHeight - 34f, boxWidth * 0.5f, 24f),
                    "[Y]  " + lineBeingSpoken.yesLabel, speakerStyle);

                GUI.color = new Color(0.95f, 0.62f, 0.55f);
                GUI.Label(new Rect(boxLeft + boxWidth * 0.5f, boxTop + boxHeight - 34f, boxWidth * 0.5f - 24f, 24f),
                    "[N]  " + lineBeingSpoken.noLabel, promptStyle);
            }
        }
        else
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(boxLeft, boxTop + boxHeight - 30f, boxWidth - 24f, 24f),
                "space", promptStyle);
        }

        GUI.color = Color.white;
    }

    private void DrawTheMurmur()
    {
        if (murmurShowing == null)
        {
            return;
        }

        // Fades out over its last second rather than vanishing, so it does not look like
        // the text was cut off mid-read.
        float opacity = 1f;
        if (murmurSecondsLeft < 1f)
        {
            opacity = murmurSecondsLeft;
        }

        float boxWidth = Screen.width * 0.62f;
        if (boxWidth > 820f)
        {
            boxWidth = 820f;
        }
        float boxLeft = (Screen.width - boxWidth) * 0.5f;

        // Sits above the controls reminder rather than on top of it.
        float boxTop = Screen.height - 132f;

        GUI.color = new Color(0f, 0f, 0f, 0.55f * opacity);
        GUI.DrawTexture(new Rect(boxLeft, boxTop, boxWidth, 52f), onePlainWhitePixel);

        GUI.color = new Color(MurmurColour.r, MurmurColour.g, MurmurColour.b, opacity);
        GUI.Label(new Rect(boxLeft + 14f, boxTop + 4f, boxWidth - 28f, 44f),
            murmurShowing.words, murmurStyle);

        GUI.color = Color.white;
    }
}
