using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

// A throwaway harness for one bug and one bug only: what a run inherits from the run
// before it.
//
// The game never reloads the scene - the title screen is drawn straight over the living
// world - so nothing between runs resets itself unless somebody resets it by hand. This
// plays a run, dies, goes back to the title, starts a new game, and then checks the
// things that used to be carried over: the round number, the enemies still standing in
// the valley, and the one-way latch inside a Portal that made it carry the player exactly
// once per launch of the game.
//
// Written the way CLAUDE.md says to write these: driven across frames from Update rather
// than run in one editor command, gated on conditions rather than on fixed times, and
// finished by writing a log and a done-marker that a waiting reader can poll for.
public class RestartRegressionTest : MonoBehaviour
{
    public const string ArmedFlag = "OneValleyRestartRegressionArmed";

    private static string ReportFolder = Application.dataPath + "/../Logs/";

    // Every wait below is measured in UNSCALED seconds. Half of this test is spent with a
    // menu up, and a menu stops the clock - so a harness timing itself on Time.time would
    // simply stop counting on the death screen and hang there for ever.
    private const float GiveUpAfterSeconds = 180f;
    private const float GiveUpOnOneStepAfterSeconds = 30f;

    private StringBuilder report = new StringBuilder();
    private bool finished = false;
    private int failures = 0;

    private float startedAt = 0f;
    private float thisStepStartedAt = 0f;
    private float nextActionAt = 0f;

    private CharacterStats playerStats;
    private PlayerMovement playerMovement;
    private StoryDirector theStory;
    private Wizard orrinInTheDungeon;

    private int roundReachedOnTheFirstRun = 0;
    private int enemiesAliveWhenTheyDied = 0;

    private const int ScreenTitle = 0;
    private const int ScreenPlaying = 1;
    private const int ScreenDead = 3;

    private int stage = 0;

    private const int StageStartTheFirstRun = 0;
    private const int StageTalkToOrrin = 1;
    private const int StageAcceptHim = 2;
    private const int StageWalkThroughTheDoor = 3;
    private const int StageFightUntilRoundOneIsUnderway = 4;
    private const int StageDieOnPurpose = 5;
    private const int StageBackToTheMainMenu = 6;
    private const int StageStartTheSecondRun = 7;
    private const int StageCheckTheWorldWasPutBack = 8;
    private const int StageTalkToOrrinAgain = 9;
    private const int StageAcceptHimAgain = 10;
    private const int StageWalkThroughTheDoorAgain = 11;
    private const int StageCheckRoundOneStartedOver = 12;
    private const int StageDone = 13;

    void Start()
    {
        // Left behind in the saved scene this object would take over every ordinary press
        // of Play, so it runs only when it was armed, and disarms itself immediately.
        if (PlayerPrefs.GetInt(ArmedFlag, 0) != 1)
        {
            Destroy(gameObject);
            return;
        }

        PlayerPrefs.SetInt(ArmedFlag, 0);
        PlayerPrefs.Save();

        startedAt = Time.unscaledTime;
        thisStepStartedAt = Time.unscaledTime;

        Note("=== RESTART REGRESSION TEST ===");
        Note("started at " + System.DateTime.Now.ToString("HH:mm:ss"));
        Note("");

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
        {
            Fail("there is no object tagged Player in the scene");
            Finish();
            return;
        }

        playerStats = playerObject.GetComponent<CharacterStats>();
        playerMovement = playerObject.GetComponent<PlayerMovement>();

        theStory = Object.FindFirstObjectByType<StoryDirector>();
        if (theStory == null)
        {
            Fail("there is no StoryDirector, so there is no run to restart");
            Finish();
            return;
        }

        Wizard[] wizards = Object.FindObjectsByType<Wizard>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int index = 0;
        while (index < wizards.Length)
        {
            if (wizards[index].answersWhenSpokenTo == true)
            {
                orrinInTheDungeon = wizards[index];
            }
            index = index + 1;
        }

        if (orrinInTheDungeon == null)
        {
            Fail("there is nobody to talk to, so no run can be started at all");
            Finish();
            return;
        }
    }

    void Update()
    {
        if (finished == true)
        {
            return;
        }

        if (Time.unscaledTime - startedAt > GiveUpAfterSeconds)
        {
            Fail("the whole test ran out of time at stage " + stage);
            Finish();
            return;
        }

        if (Time.unscaledTime - thisStepStartedAt > GiveUpOnOneStepAfterSeconds)
        {
            Fail("stage " + stage + " never finished - it waited "
                + Mathf.RoundToInt(GiveUpOnOneStepAfterSeconds) + "s for a condition that "
                + "never became true");
            Finish();
            return;
        }

        // Lines are read one at a time by a person. The test simply pushes them along.
        if (DialogueBox.instance != null && DialogueBox.instance.AQuestionIsWaiting() == false)
        {
            DialogueBox.instance.SkipTheCurrentLine();
        }

        if (Time.unscaledTime < nextActionAt)
        {
            return;
        }

        RunTheCurrentStage();
    }

    private void RunTheCurrentStage()
    {
        if (stage == StageStartTheFirstRun)
        {
            if (ReadMenuScreen() != ScreenTitle)
            {
                return;
            }

            Note("--- FIRST RUN ---");
            PressMenuButton("StartANewGame");
            MoveOn(StageTalkToOrrin, 0.4f);
            return;
        }

        if (stage == StageTalkToOrrin)
        {
            if (ReadMenuScreen() != ScreenPlaying)
            {
                return;
            }

            playerMovement.TeleportTo(StandingSpotInFrontOfOrrin());
            if (orrinInTheDungeon.PlayerIsCloseEnough() == false)
            {
                return;
            }

            orrinInTheDungeon.SpeakToMe();
            MoveOn(StageAcceptHim, 0.6f);
            return;
        }

        if (stage == StageAcceptHim)
        {
            if (DialogueBox.instance.AQuestionIsWaiting() == false)
            {
                return;
            }

            DialogueBox.instance.AnswerTheQuestion(true);
            Note("agreed to go");
            MoveOn(StageWalkThroughTheDoor, 1.2f);
            return;
        }

        if (stage == StageWalkThroughTheDoor)
        {
            Portal door = theStory.doorOutOfTheDungeon;
            if (door == null)
            {
                Fail("there is no door out of the dungeon");
                Finish();
                return;
            }

            if (door.IsOpen() == false)
            {
                return;
            }

            // Stood in the gateway and held there. The portal wants the player inside its
            // radius for a moment before it takes them, so one teleport is not enough.
            if (door.HasCarriedThePlayer() == false)
            {
                playerMovement.TeleportTo(door.transform.position + new Vector3(0f, 1f, 0f));
                return;
            }

            Note("the door carried the player up into the valley");
            MoveOn(StageFightUntilRoundOneIsUnderway, 0.5f);
            return;
        }

        if (stage == StageFightUntilRoundOneIsUnderway)
        {
            if (RoundDirector.instance == null)
            {
                Fail("there is no RoundDirector");
                Finish();
                return;
            }

            // Waited for enemies to actually be standing there, not merely for the round
            // number to change. The enemies are the thing the second run used to inherit.
            if (RoundDirector.instance.EnemiesRemaining() <= 0)
            {
                return;
            }

            roundReachedOnTheFirstRun = RoundDirector.instance.currentRound;
            enemiesAliveWhenTheyDied = RoundDirector.instance.EnemiesRemaining();

            Note("round " + roundReachedOnTheFirstRun + " is under way with "
                + enemiesAliveWhenTheyDied + " enemies alive");
            Check(roundReachedOnTheFirstRun == 1,
                "the first run begins at round 1 (it began at round "
                + roundReachedOnTheFirstRun + ")");

            MoveOn(StageDieOnPurpose, 0.2f);
            return;
        }

        if (stage == StageDieOnPurpose)
        {
            Note("");
            Note("--- DYING ---");
            playerStats.currentHealth = 0f;
            playerStats.isDead = true;
            MoveOn(StageBackToTheMainMenu, 0.2f);
            return;
        }

        if (stage == StageBackToTheMainMenu)
        {
            // GameDirector holds the death screen back for a couple of seconds so the HUD
            // caption gets its moment, so this waits rather than assuming.
            if (ReadMenuScreen() != ScreenDead)
            {
                return;
            }

            Note("the death screen came up");
            PressMenuButton("GiveUpAndReturnToTheTitle");
            MoveOn(StageStartTheSecondRun, 0.4f);
            return;
        }

        if (stage == StageStartTheSecondRun)
        {
            if (ReadMenuScreen() != ScreenTitle)
            {
                return;
            }

            Note("back at the title screen");
            Note("");
            Note("--- SECOND RUN: NEW GAME ---");
            PressMenuButton("StartANewGame");
            MoveOn(StageCheckTheWorldWasPutBack, 0.4f);
            return;
        }

        if (stage == StageCheckTheWorldWasPutBack)
        {
            if (ReadMenuScreen() != ScreenPlaying)
            {
                return;
            }

            // This is the bug, in five checks.
            Check(RoundDirector.instance.currentRound == 0,
                "the round counter went back to nothing (it is "
                + RoundDirector.instance.currentRound + ")");

            Check(RoundDirector.instance.EnemiesRemaining() == 0,
                "the valley is empty (there are "
                + RoundDirector.instance.EnemiesRemaining() + " enemies alive)");

            EnemyBrain[] strays = Object.FindObjectsByType<EnemyBrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(strays.Length == 0,
                "no enemy objects were left behind anywhere in the scene (found "
                + strays.Length + ")");

            Check(theStory.currentAct == StoryDirector.ActInTheDungeon,
                "the story went back to the dungeon (act is " + theStory.currentAct + ")");

            Portal door = theStory.doorOutOfTheDungeon;
            Check(door != null && door.HasCarriedThePlayer() == false,
                "the door out of the dungeon is willing to carry the player again");
            Check(door != null && door.IsOpen() == false,
                "the door out of the dungeon is shut again");

            Check(playerStats.isDead == false, "the player is alive again");

            MoveOn(StageTalkToOrrinAgain, 0.3f);
            return;
        }

        if (stage == StageTalkToOrrinAgain)
        {
            playerMovement.TeleportTo(StandingSpotInFrontOfOrrin());
            if (orrinInTheDungeon.PlayerIsCloseEnough() == false)
            {
                return;
            }

            orrinInTheDungeon.SpeakToMe();
            MoveOn(StageAcceptHimAgain, 0.6f);
            return;
        }

        if (stage == StageAcceptHimAgain)
        {
            if (DialogueBox.instance.AQuestionIsWaiting() == false)
            {
                return;
            }

            Note("Orrin can be spoken to a second time, and asked the question again");
            DialogueBox.instance.AnswerTheQuestion(true);
            MoveOn(StageWalkThroughTheDoorAgain, 1.2f);
            return;
        }

        if (stage == StageWalkThroughTheDoorAgain)
        {
            Portal door = theStory.doorOutOfTheDungeon;

            if (door.IsOpen() == false)
            {
                return;
            }

            // The heart of the reported bug: this used to be where a second run stopped
            // dead, standing in a lit archway that had already done its one job.
            if (door.HasCarriedThePlayer() == false)
            {
                playerMovement.TeleportTo(door.transform.position + new Vector3(0f, 1f, 0f));
                return;
            }

            Note("the door carried the player a SECOND time");
            MoveOn(StageCheckRoundOneStartedOver, 0.5f);
            return;
        }

        if (stage == StageCheckRoundOneStartedOver)
        {
            if (RoundDirector.instance.EnemiesRemaining() <= 0)
            {
                return;
            }

            int round = RoundDirector.instance.currentRound;
            Note("the second run is fighting round " + round + " with "
                + RoundDirector.instance.EnemiesRemaining() + " enemies");

            Check(theStory.currentAct == StoryDirector.ActFighting,
                "walking through the door started the fight again");
            Check(round == 1,
                "the second run starts at round 1 rather than carrying on from round "
                + roundReachedOnTheFirstRun + " (it is at round " + round + ")");

            MoveOn(StageDone, 0f);
            return;
        }

        if (stage == StageDone)
        {
            Finish();
        }
    }

    private Vector3 StandingSpotInFrontOfOrrin()
    {
        return orrinInTheDungeon.transform.position + new Vector3(0f, 1.0f, -3f);
    }

    private void MoveOn(int nextStage, float pauseSeconds)
    {
        stage = nextStage;
        thisStepStartedAt = Time.unscaledTime;
        nextActionAt = Time.unscaledTime + pauseSeconds;
    }

    // ------------------------------------------------------------------------
    // Reaching into the menu
    // ------------------------------------------------------------------------
    //
    // The menu keeps which screen it is on, and the buttons themselves, private - which
    // is right, because nothing in the game should be pressing them. A test is the one
    // thing that has to, so it reads the state and presses the buttons by reflection
    // rather than the private-ness being loosened for its benefit.

    private static int ReadMenuScreen()
    {
        MainMenu menu = Object.FindFirstObjectByType<MainMenu>();
        if (menu == null)
        {
            return -1;
        }

        FieldInfo field = typeof(MainMenu).GetField("screen",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            return -1;
        }
        return (int)field.GetValue(menu);
    }

    private void PressMenuButton(string methodName)
    {
        MainMenu menu = Object.FindFirstObjectByType<MainMenu>();
        if (menu == null)
        {
            Fail("there is no MainMenu in the scene");
            return;
        }

        MethodInfo method = typeof(MainMenu).GetMethod(methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
        {
            Fail("MainMenu has no method called " + methodName);
            return;
        }
        method.Invoke(menu, null);
    }

    // ------------------------------------------------------------------------
    // The report
    // ------------------------------------------------------------------------

    private void Check(bool itIsTrue, string whatWasExpected)
    {
        if (itIsTrue == true)
        {
            Note("  PASS  " + whatWasExpected);
            return;
        }
        Fail(whatWasExpected);
    }

    private void Fail(string what)
    {
        failures = failures + 1;
        Note("  FAIL  " + what);
    }

    private void Note(string line)
    {
        report.AppendLine(line);
        Debug.Log("[restart-test] " + line);
    }

    private void Finish()
    {
        if (finished == true)
        {
            return;
        }
        finished = true;

        Note("");
        if (failures == 0)
        {
            Note("RESULT: PASS - a new run starts from a clean world");
        }
        else
        {
            Note("RESULT: FAIL - " + failures + " check(s) failed");
        }
        Note("took " + Mathf.RoundToInt(Time.unscaledTime - startedAt) + "s");

        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText(ReportFolder + "restart_regression.log", report.ToString());
        File.WriteAllText(ReportFolder + "restart_regression_done.txt",
            failures == 0 ? "PASS" : "FAIL");
    }
}
