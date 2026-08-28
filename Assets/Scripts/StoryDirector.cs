using UnityEngine;

// Which part of the story we are in, and what that changes.
//
// The rounds, the Warden and the shrine all already knew how to run themselves. What was
// missing was a reason for any of it to begin: the fight simply started three seconds
// after the game loaded. This holds the rounds back until somebody has agreed to fight,
// and picks the story up again once the Warden is dead.
//
// Everything here is one plain integer and a handful of flags on purpose. A story this
// short does not need a state machine framework, and a framework would make it far
// harder to read what actually happens next.
public class StoryDirector : MonoBehaviour
{
    public static StoryDirector instance;

    // The player is in the dungeon and has not agreed to anything yet.
    public const int ActInTheDungeon = 0;
    // Agreed, and walking towards the door out.
    public const int ActAccepted = 1;
    // Out in the valley with the rounds running.
    public const int ActFighting = 2;
    // The Warden is dead and his eye is lying on the floor of the Vault.
    public const int ActEyeIsWaiting = 3;
    // The eye has been taken and the way home is open.
    public const int ActLeaving = 4;
    // Back in the valley, walking north with Orrin waiting.
    public const int ActGoingHome = 5;
    // The road, the last re-skin, the title.
    public const int ActTheRoad = 6;

    public int currentAct = ActInTheDungeon;

    // Remembered so that agreeing after refusing gets a different line. It is a tiny
    // detail and it is the single cheapest thing in this whole project that makes the
    // conversation feel written rather than generated.
    public bool hasRefusedAtLeastOnce = false;

    // The name given to the wizard's question, matched against what the dialogue box
    // reports back. Spelled once here so a typo cannot silently break the story.
    public const string QuestionFightTheWarden = "fightTheWarden";
    public const string QuestionWhereNext = "whereNext";

    public const string Orrin = "ORRIN, THE LENSMAKER";

    // Filled in by the builders.
    [HideInInspector] public Portal doorOutOfTheDungeon;
    [HideInInspector] public Portal doorHomeFromTheVault;
    [HideInInspector] public Transform theGate;
    [HideInInspector] public Wizard orrinInTheValley;

    private GameObject playerObject;

    // Where the north gate sits when shut and when open. The gate is a plain box rather
    // than a ZoneBarrier, so it is slid by hand here.
    private Vector3 gateShutPosition = Vector3.zero;
    private bool gateHasBeenMeasured = false;
    private bool gateIsOpening = false;

    private float secondsUntilNextStoryBeat = 0f;
    private int storyBeatWaiting = 0;

    private const int BeatNothing = 0;
    private const int BeatOpenTheDungeonDoor = 1;
    private const int BeatOrrinGreetsYouHome = 2;
    private const int BeatRollTheEnding = 3;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        playerObject = GameObject.FindWithTag("Player");
    }

    void Update()
    {
        WatchForAnswers();
        RunAnyWaitingBeat();
        SlideTheGateIfItIsOpening();
        WatchForTheWardenDying();
    }

    // ------------------------------------------------------------------------
    // Answers to questions
    // ------------------------------------------------------------------------

    private void WatchForAnswers()
    {
        if (DialogueBox.instance == null)
        {
            return;
        }

        string answered = DialogueBox.instance.AnsweredQuestionName();
        if (answered == "")
        {
            return;
        }

        bool saidYes = DialogueBox.instance.AnswerWasYes();

        if (answered == QuestionFightTheWarden)
        {
            if (saidYes == true)
            {
                AcceptTheTask();
            }
            else
            {
                RefuseTheTask();
            }
            return;
        }

        if (answered == QuestionWhereNext)
        {
            // Both answers lead to the same road. The question is there to give the
            // player a voice at the end, not to branch a demo that has one ending.
            if (saidYes == true)
            {
                DialogueBox.instance.Say(Orrin, "North, then. It is the harder way, and the one worth walking.");
            }
            else
            {
                DialogueBox.instance.Say(Orrin, "Quiet. Yes. You have earned some of that.");
            }
            DialogueBox.instance.Say(Orrin, "Go on. The gate is open, and I have kept you long enough.");

            currentAct = ActTheRoad;
            WaitThenRun(1.0f, BeatRollTheEnding);
        }
    }

    private void AcceptTheTask()
    {
        currentAct = ActAccepted;

        if (hasRefusedAtLeastOnce == true)
        {
            DialogueBox.instance.Say(Orrin, "You went away and thought about it. Good. The ones who say yes straight away are the ones I bury.");
        }
        else
        {
            DialogueBox.instance.Say(Orrin, "Then I will not waste your time with thanks.");
        }

        // The first checkpoint of the run. Somebody who agrees, wanders off and comes
        // back tomorrow should not have to sit through the conversation again.
        if (GameProgress.instance != null)
        {
            GameProgress.instance.SaveCheckpoint("The Dungeon");
        }

        DialogueBox.instance.Say(Orrin, "The stair behind me runs up into the valley. Walk it, and keep walking - they will find you long before you find them.");
        DialogueBox.instance.Say(Orrin, "I cannot come. But I made every stone in that place, and I can still see through it. You will hear me.");

        // The door is opened a beat AFTER the last line, so the player watches it open
        // rather than discovering it was already open behind them.
        WaitThenRun(0.4f, BeatOpenTheDungeonDoor);
    }

    private void RefuseTheTask()
    {
        hasRefusedAtLeastOnce = true;

        DialogueBox.instance.Say(Orrin, "Then the valley keeps its dead a while longer.");
        DialogueBox.instance.Say(Orrin, "I have waited eleven years. I can wait until you have looked around.");

        // Deliberately left in ActInTheDungeon. The wizard re-arms, the door stays shut,
        // and nothing is lost - which matters, because the first thing anybody shown this
        // demo does is press No to find out whether it breaks.
        currentAct = ActInTheDungeon;
    }

    // ------------------------------------------------------------------------
    // Beats that happen a moment after something else
    // ------------------------------------------------------------------------

    private void WaitThenRun(float seconds, int whichBeat)
    {
        secondsUntilNextStoryBeat = seconds;
        storyBeatWaiting = whichBeat;
    }

    private void RunAnyWaitingBeat()
    {
        if (storyBeatWaiting == BeatNothing)
        {
            return;
        }

        // A beat queued behind a conversation waits for the conversation to finish, so
        // the world never changes underneath a line the player is still reading.
        if (DialogueBox.ConversationIsOpen() == true)
        {
            return;
        }

        secondsUntilNextStoryBeat = secondsUntilNextStoryBeat - Time.deltaTime;
        if (secondsUntilNextStoryBeat > 0f)
        {
            return;
        }

        int beatToRun = storyBeatWaiting;
        storyBeatWaiting = BeatNothing;

        if (beatToRun == BeatOpenTheDungeonDoor)
        {
            if (doorOutOfTheDungeon != null)
            {
                doorOutOfTheDungeon.Open();
                GameSound.Play("PortalOpen", 0.8f);
            }
        }
        else if (beatToRun == BeatOrrinGreetsYouHome)
        {
            SpeakTheHomecoming();
        }
        else if (beatToRun == BeatRollTheEnding)
        {
            EndingSequence ending = Object.FindFirstObjectByType<EndingSequence>();
            if (ending != null)
            {
                ending.Begin();
            }
        }
    }

    // ------------------------------------------------------------------------
    // Crossing from the dungeon into the valley
    // ------------------------------------------------------------------------

    // Called by the dungeon door once it has carried the player up into the valley.
    public void OnPlayerReachedTheValley()
    {
        if (currentAct != ActAccepted)
        {
            return;
        }

        currentAct = ActFighting;

        if (RoundDirector.instance != null)
        {
            RoundDirector.instance.BeginTheFirstRound();
        }

        DialogueBox.instance.Murmur(Orrin, "The Approach. They come at you from every side here - do not back away, turn.");
    }

    // ------------------------------------------------------------------------
    // After the Warden
    // ------------------------------------------------------------------------

    private void WatchForTheWardenDying()
    {
        if (currentAct != ActFighting)
        {
            return;
        }
        if (GameDirector.instance == null || GameDirector.instance.theWardenIsDead == false)
        {
            return;
        }

        currentAct = ActEyeIsWaiting;

        if (GameProgress.instance != null)
        {
            GameProgress.instance.SaveCheckpoint("The Vault");
        }

        // The eye is dropped by the Warden itself so that it lands where he fell rather
        // than at a position guessed here.
    }

    // Called by WardenGem once the player has picked it up AND swung the new blade.
    public void OnTheWayHomeIsEarned()
    {
        if (currentAct != ActEyeIsWaiting)
        {
            return;
        }

        currentAct = ActLeaving;

        if (doorHomeFromTheVault != null)
        {
            doorHomeFromTheVault.Open();
            GameSound.Play("PortalOpen", 0.8f);
        }

        DialogueBox.instance.Murmur(Orrin, "There. Behind you - the way out. Come up, and let me look at you.");
    }

    // Called by the homeward door once it has put the player back in the valley.
    public void OnPlayerReachedHome()
    {
        if (currentAct != ActLeaving)
        {
            return;
        }

        currentAct = ActGoingHome;

        if (GameProgress.instance != null)
        {
            GameProgress.instance.SaveCheckpoint("The Long Way Home");
        }

        OpenTheNorthGate();

        if (orrinInTheValley != null)
        {
            orrinInTheValley.gameObject.SetActive(true);
        }

        WaitThenRun(1.4f, BeatOrrinGreetsYouHome);
    }

    private void SpeakTheHomecoming()
    {
        DialogueBox.instance.Say(Orrin, "You are carrying his eye. I can see it from here - it is putting your shadow on the wrong side of you.");
        DialogueBox.instance.Say(Orrin, "Eleven years I have been the man who sealed a door. Now I am something else, and I have not decided what.");
        DialogueBox.instance.Say(Orrin, "That was one valley. There are others, and they are not all built out of the same stone.");
        DialogueBox.instance.Ask(Orrin, "Where will you go?", QuestionWhereNext, "North.", "Somewhere quiet.");
    }

    // ------------------------------------------------------------------------
    // The north gate
    // ------------------------------------------------------------------------

    private void OpenTheNorthGate()
    {
        if (theGate == null)
        {
            return;
        }

        if (gateHasBeenMeasured == false)
        {
            gateShutPosition = theGate.position;
            gateHasBeenMeasured = true;
        }
        gateIsOpening = true;
        GameSound.Play("BarrierMove", 0.8f);
    }

    private void SlideTheGateIfItIsOpening()
    {
        if (gateIsOpening == false || theGate == null)
        {
            return;
        }

        // Straight up and out of sight. The gate is ten metres tall, so lifting it eleven
        // clears the opening completely and leaves no lip to catch the player.
        Vector3 openPosition = gateShutPosition + Vector3.up * 11f;
        theGate.position = Vector3.MoveTowards(theGate.position, openPosition, 3.2f * Time.deltaTime);

        if (theGate.position == openPosition)
        {
            gateIsOpening = false;
        }
    }

    // Read by the display so it can keep the round counter off the screen while the
    // player is still in the dungeon, where there are no rounds.
    public bool TheFightHasStarted()
    {
        return currentAct >= ActFighting;
    }
}
