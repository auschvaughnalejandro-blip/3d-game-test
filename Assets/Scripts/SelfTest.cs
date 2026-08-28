using UnityEngine;
using System.Text;
using System.IO;

// An automated play-through of the whole game, start to finish.
//
// This is not a unit test. It plays the real scene: it lets the round system spawn
// whatever it wants, kills what appears, walks the player into the portal, and fights the
// Warden through all three phases - writing down what it actually saw at every step.
//
// The point is the silent failures. The worst bugs in this project have all been things
// that reported success while doing nothing: a round clearing itself because no enemy ever
// spawned, a spawn falling through the terrain, a boss phase that never fired. A person
// watching for those has to sit through five rounds. This does it in about two minutes and
// leaves a written record.
//
// Armed by One Valley -> Run Self Test, which sets the flag and enters play mode. It
// disarms the flag the moment it starts, so an ordinary play session never triggers it.
public class SelfTest : MonoBehaviour
{
    public const string ArmedFlag = "OneValleySelfTestArmed";

    // Enemies are killed one at a time rather than all at once, so that a round which
    // spawns a second wave partway through actually gets the chance to.
    private const float SecondsBetweenKills = 0.28f;

    // Chunks small enough that both phase thresholds are crossed on separate hits.
    private const float DamagePerHitOnTheWarden = 35f;
    private const float SecondsBetweenHitsOnTheWarden = 0.35f;

    private const float GiveUpAfterSeconds = 300f;

    private static string ReportFolder =
        "C:/Users/HP/AppData/Local/Temp/claude/c--Users-HP-Desktop-RPG-Game/" +
        "85dc9e8a-2ebd-409f-bc75-faa3c9f9f98e/scratchpad/";

    private StringBuilder report = new StringBuilder();
    private float startedAt = 0f;
    private float nextActionAt = 0f;
    private bool finished = false;

    private int roundBeingWatched = -1;
    private int mostEnemiesSeenThisRound = 0;
    private int killsThisRound = 0;
    private int lastBossPhaseSeen = 0;
    private bool haveEnteredThePortal = false;

    // A round that never ends is the failure this whole harness exists to catch, so it is
    // watched for directly rather than being left to the overall timeout.
    private float roundStartedAt = 0f;
    private int fellOutOfTheWorldThisRound = 0;
    private int buriedThisRound = 0;

    // Where the unreachable ones actually were. Without this the count says something is
    // wrong but not where to look, which is the difference between a report and a clue.
    private string whereTheStuckOnesWere = "";
    private int placesAlreadyWrittenDown = 0;
    private bool alreadyReportedThisRoundStuck = false;
    private const float ARoundShouldNeverTakeLongerThan = 45f;

    private CharacterStats playerStats;
    private PlayerMovement playerMovement;

    // ---- the story around the fight -----------------------------------------

    private StoryDirector theStory;

    // How far through the dungeon and the ending the test has got. The stages are walked
    // one at a time with a pause between them, because several of them only become true
    // a frame or two after the one before.
    private int storyStage = 0;
    private float nextStoryActionAt = 0f;

    private const int StageWalkUpToOrrin = 0;
    private const int StageRefuseHim = 1;
    private const int StageAskAgain = 2;
    private const int StageAcceptHim = 3;
    private const int StageWalkThroughTheDoor = 4;
    private const int StageFighting = 5;
    private const int StageTakeTheEye = 6;
    private const int StageSwingTheEdge = 7;
    private const int StageGoHome = 8;
    private const int StageTalkToOrrinAgain = 9;
    private const int StageWalkNorth = 10;
    private const int StageWatchTheTitle = 11;
    private const int StageCheckSavingAndLoading = 12;
    private const int StageDone = 13;

    private Wizard orrinInTheDungeon;

    void Start()
    {
        // Left behind in the saved scene, this object would otherwise take over every
        // ordinary press of Play. It runs only when the menu item armed it, and disarms
        // itself immediately so the next run has to be asked for too.
        if (PlayerPrefs.GetInt(ArmedFlag, 0) != 1)
        {
            Destroy(gameObject);
            return;
        }

        PlayerPrefs.SetInt(ArmedFlag, 0);
        PlayerPrefs.Save();

        startedAt = Time.time;

        Note("=== ONE VALLEY SELF TEST ===");
        Note("started at " + System.DateTime.Now.ToString("HH:mm:ss"));

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
        {
            Note("FAIL: there is no object tagged Player in the scene");
            Finish();
            return;
        }
        playerStats = playerObject.GetComponent<CharacterStats>();
        playerMovement = playerObject.GetComponent<PlayerMovement>();
        Note("player found at " + playerObject.transform.position);

        CheckTheSoundLoaded();
        CheckTheSceneWasBuilt();
        CheckTheStoryWasBuilt();
        ShortenTheWaiting();
    }

    // Sound is loaded from Resources by name, which fails silently if the files are not
    // where GameSound expects them - every clip simply never plays and nothing complains.
    private void CheckTheSoundLoaded()
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio");
        Note("audio clips found in Resources/Audio: " + clips.Length);

        if (clips.Length == 0)
        {
            Note("FAIL: no audio clips loaded, the whole game will be silent");
            return;
        }

        string names = "";
        int index = 0;
        while (index < clips.Length && index < 40)
        {
            names = names + clips[index].name + " ";
            index = index + 1;
        }
        Note("  clips: " + names);
    }

    private void CheckTheSceneWasBuilt()
    {
        if (RoundDirector.instance == null)
        {
            Note("FAIL: no RoundDirector in the scene");
            Finish();
            return;
        }

        Note("rounds configured: " + RoundDirector.instance.TotalRounds());

        GameObject vault = GameObject.Find("TheVault");
        Note("the Vault exists: " + (vault != null));

        if (RoundDirector.instance.thePortal == null)
        {
            Note("FAIL: no portal was built, round 5 can never be reached");
        }
        else
        {
            Note("portal built at " + RoundDirector.instance.thePortal.transform.position);
        }
    }

    private void CheckTheStoryWasBuilt()
    {
        theStory = Object.FindFirstObjectByType<StoryDirector>();

        if (theStory == null)
        {
            Note("no StoryDirector in the scene - running the old straight-to-the-fight test");
            storyStage = StageFighting;
            return;
        }

        Note("");
        Note("--- THE DUNGEON ---");

        float howFarFromTheDungeon = Vector3.Distance(
            playerMovement.transform.position, DungeonBuilder.DungeonOrigin);
        bool startsInTheDungeon = howFarFromTheDungeon < 40f;
        Note("player starts in the dungeon: " + startsInTheDungeon);
        if (startsInTheDungeon == false)
        {
            Note("FAIL: the game does not start in the dungeon, so the story never begins");
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

        Note("Orrin is in the dungeon: " + (orrinInTheDungeon != null));
        if (orrinInTheDungeon == null)
        {
            Note("FAIL: there is nobody to talk to, so the game can never be started");
            Finish();
            return;
        }

        if (theStory.doorOutOfTheDungeon == null)
        {
            Note("FAIL: there is no door out of the dungeon");
        }
        else
        {
            Note("the door out is shut at the start: "
                 + (theStory.doorOutOfTheDungeon.IsOpen() == false));
        }
    }

    // ------------------------------------------------------------------------
    // Act one: the dungeon
    // ------------------------------------------------------------------------

    private void PlayTheDungeon()
    {
        // Lines are read one at a time by a person. The test simply pushes them along.
        if (DialogueBox.instance != null && DialogueBox.instance.AQuestionIsWaiting() == false)
        {
            DialogueBox.instance.SkipTheCurrentLine();
        }

        if (Time.time < nextStoryActionAt)
        {
            return;
        }

        if (storyStage == StageWalkUpToOrrin)
        {
            // Stood in front of him rather than teleported onto him, so the distance
            // check that gates the conversation is really exercised.
            //
            // A metre up, because the character controller is measured from the middle of
            // the body: dropped in at floor level it starts half sunk into the floor and
            // falls straight through it.
            playerMovement.TeleportTo(StandingSpotInFrontOfOrrin());

            if (orrinInTheDungeon.PlayerIsCloseEnough() == false)
            {
                Note("FAIL: standing three metres away is not close enough to talk to him");
                Finish();
                return;
            }

            Note("walked up to Orrin and he can be spoken to");
            orrinInTheDungeon.SpeakToMe();
            storyStage = StageRefuseHim;
            nextStoryActionAt = Time.time + 0.6f;
            return;
        }

        if (storyStage == StageRefuseHim)
        {
            if (DialogueBox.instance.AQuestionIsWaiting() == false)
            {
                return;
            }

            // Saying NO first, deliberately. This is the thing anybody shown the demo
            // presses to see whether it breaks, so it is the thing most worth testing.
            DialogueBox.instance.AnswerTheQuestion(false);
            storyStage = StageAskAgain;
            nextStoryActionAt = Time.time + 1.2f;
            return;
        }

        if (storyStage == StageAskAgain)
        {
            if (DialogueBox.ConversationIsOpen() == true)
            {
                return;
            }

            // Put back in front of him before asking whether he can still be spoken to.
            // The point of this check is that refusing is not a dead end, not that the
            // player happened to stay put while the refusal was being read out.
            playerMovement.TeleportTo(StandingSpotInFrontOfOrrin());

            bool doorStillShut = theStory.doorOutOfTheDungeon.IsOpen() == false;
            bool stillInTheDungeon = theStory.currentAct == StoryDirector.ActInTheDungeon;

            Note("refused him: the door stayed shut: " + doorStillShut);
            Note("refused him: the game did not move on: " + stillInTheDungeon);
            Note("refused him: he remembers being refused: " + theStory.hasRefusedAtLeastOnce);

            if (doorStillShut == false || stillInTheDungeon == false)
            {
                Note("FAIL: saying no started the game anyway");
            }
            if (orrinInTheDungeon.PlayerIsCloseEnough() == false)
            {
                Note("FAIL: he cannot be asked a second time, so refusing is a dead end");
            }

            orrinInTheDungeon.SpeakToMe();
            storyStage = StageAcceptHim;
            nextStoryActionAt = Time.time + 0.6f;
            return;
        }

        if (storyStage == StageAcceptHim)
        {
            if (DialogueBox.instance.AQuestionIsWaiting() == false)
            {
                return;
            }

            DialogueBox.instance.AnswerTheQuestion(true);
            Note("accepted the task");
            storyStage = StageWalkThroughTheDoor;
            nextStoryActionAt = Time.time + 1.0f;
            return;
        }

        if (storyStage == StageWalkThroughTheDoor)
        {
            if (DialogueBox.ConversationIsOpen() == true)
            {
                return;
            }

            Portal door = theStory.doorOutOfTheDungeon;
            if (door.IsOpen() == false)
            {
                // Given a few seconds: the door opens a beat after the last line.
                if (Time.time - startedAt > 60f)
                {
                    Note("FAIL: the door out of the dungeon never opened");
                    Finish();
                }
                return;
            }

            if (door.HasCarriedThePlayer() == true)
            {
                Note("the door carried the player to " + playerMovement.transform.position);
                Note("the rounds have begun: " + (theStory.currentAct == StoryDirector.ActFighting));
                Note("");
                storyStage = StageFighting;
                return;
            }

            // Stand in the doorway and let it do the rest.
            playerMovement.TeleportTo(door.transform.position + new Vector3(0f, 1f, 0f));
            return;
        }
    }

    private Vector3 StandingSpotInFrontOfOrrin()
    {
        return orrinInTheDungeon.transform.position + new Vector3(0f, 1.0f, -3f);
    }

    // ------------------------------------------------------------------------
    // Act four: the eye, the blade and the road
    // ------------------------------------------------------------------------

    private void PlayTheEpilogue()
    {
        if (storyStage == StageFighting)
        {
            Note("");
            Note("ALL ROUNDS CLEARED after " + Mathf.RoundToInt(Time.time - startedAt) + "s");
            Note("");
            Note("--- AFTER THE WARDEN ---");
            storyStage = StageTakeTheEye;
            nextStoryActionAt = Time.time + 1.0f;
            return;
        }

        if (DialogueBox.instance != null && DialogueBox.instance.AQuestionIsWaiting() == false)
        {
            DialogueBox.instance.SkipTheCurrentLine();
        }

        if (Time.time < nextStoryActionAt)
        {
            return;
        }

        PlayerWeapons weapons = playerMovement.GetComponent<PlayerWeapons>();
        PlayerCombat combat = playerMovement.GetComponent<PlayerCombat>();

        if (storyStage == StageTakeTheEye)
        {
            WardenGem eye = Object.FindFirstObjectByType<WardenGem>();
            if (eye == null)
            {
                Note("FAIL: the Warden died but left no eye behind, so there is no reward");
                Finish();
                return;
            }

            if (weapons.TheEdgeHasBeenWon() == true)
            {
                Note("took the eye, and the Warden's Edge is in hand");
                Note("  the weapon in hand is now " + weapons.WeaponInHand().weaponName);
                Note("  its swing arc is " + weapons.WeaponInHand().swingArcDegrees + " degrees");
                storyStage = StageSwingTheEdge;
                nextStoryActionAt = Time.time + 0.6f;
                return;
            }

            playerMovement.TeleportTo(eye.transform.position);
            return;
        }

        if (storyStage == StageSwingTheEdge)
        {
            if (theStory.currentAct == StoryDirector.ActLeaving)
            {
                Note("swung the Edge, and the way home opened");
                storyStage = StageGoHome;
                nextStoryActionAt = Time.time + 0.8f;
                return;
            }

            combat.PerformSwingNow(false);
            return;
        }

        if (storyStage == StageGoHome)
        {
            Portal home = theStory.doorHomeFromTheVault;
            if (home == null)
            {
                Note("FAIL: there is no way out of the Vault");
                Finish();
                return;
            }

            if (home.HasCarriedThePlayer() == true)
            {
                Note("came home to " + playerMovement.transform.position);
                Note("  the north gate is opening: " + (theStory.theGate != null));
                Note("  Orrin is waiting: "
                     + (theStory.orrinInTheValley != null
                        && theStory.orrinInTheValley.gameObject.activeSelf == true));
                storyStage = StageTalkToOrrinAgain;
                nextStoryActionAt = Time.time + 2.4f;
                return;
            }

            if (home.IsOpen() == true)
            {
                playerMovement.TeleportTo(home.transform.position + new Vector3(0f, 1f, 0f));
            }
            return;
        }

        if (storyStage == StageTalkToOrrinAgain)
        {
            if (DialogueBox.instance.AQuestionIsWaiting() == true)
            {
                DialogueBox.instance.AnswerTheQuestion(true);
                Note("answered Orrin at the end");
                storyStage = StageWalkNorth;
                nextStoryActionAt = Time.time + 1.6f;
            }
            return;
        }

        if (storyStage == StageWalkNorth)
        {
            if (DialogueBox.ConversationIsOpen() == true)
            {
                return;
            }

            EndingSequence ending = Object.FindFirstObjectByType<EndingSequence>();
            if (ending == null)
            {
                Note("FAIL: there is no ending in the scene");
                Finish();
                return;
            }

            // Walk north up the road, past the gate that has just opened.
            playerMovement.TeleportTo(new Vector3(0f, 1.2f, ending.roadBeginsAtZ + 3f));
            Note("walked north onto the road");
            storyStage = StageWatchTheTitle;
            nextStoryActionAt = Time.time + 0.5f;
            return;
        }

        if (storyStage == StageWatchTheTitle)
        {
            EndingSequence ending = Object.FindFirstObjectByType<EndingSequence>();

            if (ending.IsRolling() == false)
            {
                Note("FAIL: reaching the road did not start the ending");
                Finish();
                return;
            }

            if (ending.HasReachedTheTitle() == true)
            {
                StyleLens lens = Object.FindFirstObjectByType<StyleLens>();
                Note("the ending ran and reached the title card");
                if (lens != null)
                {
                    Note("  the lens finished on " + lens.CurrentStyleName());
                }
                Note("");
                Note("THE WHOLE STORY PLAYED THROUGH in "
                     + Mathf.RoundToInt(Time.time - startedAt) + "s");

                storyStage = StageCheckSavingAndLoading;
                nextStoryActionAt = Time.time + 0.3f;
            }
            return;
        }

        if (storyStage == StageCheckSavingAndLoading)
        {
            CheckSavingAndLoading();
            storyStage = StageDone;
            Finish();
        }
    }

    // ------------------------------------------------------------------------
    // Saving and loading
    // ------------------------------------------------------------------------

    // Run last, because it deliberately throws the player back into the middle of the
    // valley and there is nothing after it that would mind.
    //
    // Checked by actually writing a file, reading it back and then LOADING it into the
    // running game, rather than by trusting that a save which was written must also work.
    // A save system that writes perfectly and restores wrongly is the usual way this goes
    // wrong, and only the second half of that is worth testing.
    private void CheckSavingAndLoading()
    {
        Note("");
        Note("--- SAVING AND LOADING ---");

        if (GameProgress.instance == null)
        {
            Note("FAIL: there is no GameProgress in the scene, so nothing can ever be saved");
            return;
        }

        Note("save file lives at " + SavedGame.WhereItLives());

        // A checkpoint was written at the start of every round during the run above, so
        // there should already be one on disk.
        SavedGame afterTheRun = SavedGame.Load();
        Note("a checkpoint was written during the run: " + afterTheRun.thereIsSomethingSaved);
        if (afterTheRun.thereIsSomethingSaved == false)
        {
            Note("FAIL: nothing was saved at any point in a complete play-through");
            return;
        }
        Note("  it says: " + afterTheRun.whereYouWere + " (" + afterTheRun.whenYouSavedIt + ")");

        // Now a made-up checkpoint from the middle of the run, loaded into the live game.
        SavedGame pretend = new SavedGame();
        pretend.thereIsSomethingSaved = true;
        pretend.whereYouWere = "Round 3 - CROSSFIRE";
        pretend.act = StoryDirector.ActFighting;
        pretend.roundToResumeAt = 3;
        pretend.essence = 7;
        pretend.maximumHealth = 175f;
        pretend.attackDamage = 38f;
        pretend.maximumStamina = 140f;
        pretend.hasRefusedOrrin = true;
        pretend.theEdgeHasBeenWon = false;

        GameProgress.instance.ResumeFrom(pretend);

        CharacterStats stats = playerMovement.GetComponent<CharacterStats>();

        Note("loaded a checkpoint from round 3:");
        Note("  the round being fought is now " + RoundDirector.instance.currentRound
             + " (wanted 3)");
        Note("  the player is at " + playerMovement.transform.position);
        Note("  essence " + GameDirector.instance.essenceCollected + " (wanted 7)");
        Note("  max health " + stats.maximumHealth + " (wanted 175)");
        Note("  attack " + stats.attackDamage + " (wanted 38)");
        Note("  max stamina " + stats.maximumStamina + " (wanted 140)");
        Note("  Orrin remembers the refusal: "
             + StoryDirector.instance.hasRefusedAtLeastOnce);

        if (RoundDirector.instance.currentRound != 3)
        {
            Note("FAIL: loading a round 3 save did not put the game into round 3");
        }
        if (GameDirector.instance.essenceCollected != 7)
        {
            Note("FAIL: essence was not restored");
        }
        if (Mathf.Abs(stats.maximumHealth - 175f) > 0.5f
            || Mathf.Abs(stats.attackDamage - 38f) > 0.5f
            || Mathf.Abs(stats.maximumStamina - 140f) > 0.5f)
        {
            Note("FAIL: the upgrades bought at the shrine were not restored");
        }

        // Back in the valley, not still standing in the Vault or down in the dungeon.
        float howFarNorthOfTheDungeon = playerMovement.transform.position.z
                                        - DungeonBuilder.DungeonOrigin.z;
        bool isInTheValley = howFarNorthOfTheDungeon > 100f
                             && playerMovement.transform.position.z < 100f;
        Note("  the player is back in the valley: " + isInTheValley);
        if (isInTheValley == false)
        {
            Note("FAIL: loading a valley checkpoint left the player somewhere else entirely");
        }

        // And a new game must throw all of that away rather than quietly keeping it.
        GameProgress.instance.BeginANewRun();

        bool saveIsGone = SavedGame.ASaveExists() == false;
        bool essenceReset = GameDirector.instance.essenceCollected == 0;
        bool healthReset = Mathf.Abs(stats.maximumHealth - 100f) < 0.5f;
        bool backInTheDungeon = Vector3.Distance(
            playerMovement.transform.position, DungeonBuilder.DungeonOrigin) < 40f;

        Note("new game: the old save is deleted: " + saveIsGone);
        Note("new game: essence back to zero: " + essenceReset);
        Note("new game: upgrades cleared: " + healthReset);
        Note("new game: back in the dungeon: " + backInTheDungeon);

        if (saveIsGone == false || essenceReset == false
            || healthReset == false || backInTheDungeon == false)
        {
            Note("FAIL: New Game did not start from the very beginning");
        }
    }

    // Banners and intermissions are there for the player, not for a machine. Cutting them
    // down keeps the whole run inside a couple of minutes.
    private void ShortenTheWaiting()
    {
        RoundDirector director = RoundDirector.instance;
        director.secondsBeforeFirstRound = 1f;
        director.secondsBetweenRounds = 2f;
        director.bannerSeconds = 1f;
        Note("(banners and intermissions shortened for the test run)");
    }

    private bool haveCheckedTheTitleScreen = false;

    void Update()
    {
        if (finished == true)
        {
            return;
        }

        // Done on the first frame rather than in Start, because every script's Start has
        // to have run before the answer means anything - including the menu's own.
        if (haveCheckedTheTitleScreen == false)
        {
            haveCheckedTheTitleScreen = true;

            Note("the game booted to the title screen: " + MainMenu.IsShowing());
            Note("  and stopped the clock while it was up: " + (Time.timeScale == 0f));

            if (MainMenu.IsShowing() == false)
            {
                Note("FAIL: the game started playing without anybody pressing anything");
            }

            // Straight past the title screen from here. Nothing in this harness can click
            // a button, and a paused game would sit there until the run timed out.
            MainMenu.skipStraightIntoTheGame = true;
            return;
        }

        if (Time.time - startedAt > GiveUpAfterSeconds)
        {
            Note("FAIL: gave up after " + GiveUpAfterSeconds + " seconds");
            DescribeWhereItGotStuck();
            Finish();
            return;
        }

        // Editing a script while the test runs makes Unity recompile mid-play, which
        // reloads the domain and takes the whole scene with it. Without this the run then
        // throws once per frame for the rest of its timeout instead of saying what
        // happened.
        if (RoundDirector.instance == null)
        {
            Note("FAIL: the RoundDirector vanished mid-run at t+"
                 + Mathf.RoundToInt(Time.time - startedAt) + "s.");
            Note("  Usually this means a script was edited during the run and Unity");
            Note("  reloaded the domain. Re-run without touching the scripts.");
            Finish();
            return;
        }

        KeepThePlayerAlive();

        // The rounds no longer begin on their own, so the dungeon has to be played
        // first. Until that is done there is no fight to watch.
        if (theStory != null && storyStage < StageFighting)
        {
            PlayTheDungeon();
            return;
        }

        WatchForANewRound();

        if (RoundDirector.instance.allRoundsCleared == true)
        {
            if (theStory != null)
            {
                PlayTheEpilogue();
                return;
            }

            Note("");
            Note("ALL ROUNDS CLEARED after " + Mathf.RoundToInt(Time.time - startedAt) + "s");
            Finish();
            return;
        }

        WalkIntoThePortalWhenItOpens();
        WatchTheBossPhases();
        WatchForAStuckRound();

        if (Time.time < nextActionAt)
        {
            return;
        }
        KillSomething();
    }

    // The test is checking that the game runs, not that this particular player survives
    // it, so damage is simply undone. Without this the run ends in round two.
    private void KeepThePlayerAlive()
    {
        if (playerStats == null)
        {
            return;
        }
        if (playerStats.currentHealth < playerStats.maximumHealth)
        {
            playerStats.currentHealth = playerStats.maximumHealth;
            playerStats.isDead = false;
        }
    }

    private void WatchForANewRound()
    {
        int round = RoundDirector.instance.currentRound;
        if (round == roundBeingWatched)
        {
            // Remember the high-water mark, because a round with a second wave has more
            // enemies in total than are ever alive at one time.
            int alive = RoundDirector.instance.EnemiesRemaining();
            if (alive > mostEnemiesSeenThisRound)
            {
                mostEnemiesSeenThisRound = alive;
            }
            return;
        }

        if (roundBeingWatched > 0)
        {
            Note("  round " + roundBeingWatched + " ended: " + killsThisRound +
                 " enemies killed, most alive at once was " + mostEnemiesSeenThisRound +
                 ", took " + Mathf.RoundToInt(Time.time - roundStartedAt) + "s");

            if (buriedThisRound > 0)
            {
                Note("  FAIL: " + buriedThisRound + " enemies were somewhere no walking "
                     + "creature can stand during round " + roundBeingWatched
                     + ". A player cannot reach or kill those, so the round would never "
                     + "have ended for a person. Seen at: " + whereTheStuckOnesWere);
            }

            if (fellOutOfTheWorldThisRound > 0)
            {
                Note("  FAIL: " + fellOutOfTheWorldThisRound +
                     " enemies dropped below the world during round " + roundBeingWatched);
            }

            if (killsThisRound == 0)
            {
                Note("  FAIL: round " + roundBeingWatched +
                     " cleared without a single enemy being killed");
            }
        }

        roundBeingWatched = round;
        mostEnemiesSeenThisRound = 0;
        killsThisRound = 0;
        roundStartedAt = Time.time;
        fellOutOfTheWorldThisRound = 0;
        buriedThisRound = 0;
        whereTheStuckOnesWere = "";
        placesAlreadyWrittenDown = 0;
        alreadyReportedThisRoundStuck = false;

        if (round > 0)
        {
            Note("");
            Note("ROUND " + round + " - " + RoundDirector.instance.CurrentRoundName() +
                 "   (t+" + Mathf.RoundToInt(Time.time - startedAt) + "s)");
        }
    }

    private void WalkIntoThePortalWhenItOpens()
    {
        if (haveEnteredThePortal == true)
        {
            return;
        }

        Portal portal = RoundDirector.instance.thePortal;
        if (portal == null)
        {
            return;
        }

        // Asked BEFORE the waiting test, not after. Carrying the player is the same event
        // that starts round five, so by the next frame the round system no longer reports
        // itself as waiting - and a check made below that test never fires at all.
        if (portal.HasCarriedThePlayer() == true)
        {
            haveEnteredThePortal = true;

            bool insideTheVault =
                Vector3.Distance(playerMovement.transform.position,
                                 ValleyBuilder.BossArenaOrigin) < 30f;

            Note("portal carried the player to " + playerMovement.transform.position);
            Note("player is inside the Vault: " + insideTheVault);
            return;
        }

        if (RoundDirector.instance.WaitingForThePortal() == false)
        {
            return;
        }
        if (portal.IsOpen() == false)
        {
            return;
        }

        // Stand in the gate rather than calling the handler directly, so the rise, the
        // dwell and the teleport are all exercised the way a player would exercise them.
        if (playerMovement != null)
        {
            playerMovement.TeleportTo(portal.transform.position + new Vector3(0f, 1f, 0f));
        }
    }

    private void WatchTheBossPhases()
    {
        WardenBoss boss = Object.FindFirstObjectByType<WardenBoss>();
        if (boss == null)
        {
            return;
        }

        if (lastBossPhaseSeen == 0)
        {
            lastBossPhaseSeen = boss.CurrentPhase();
            Note("  Warden spawned at " + boss.transform.position +
                 ", phase " + lastBossPhaseSeen);

            bool inTheVault =
                Vector3.Distance(boss.transform.position, ValleyBuilder.BossArenaOrigin) < 30f;
            Note("  Warden is in the Vault: " + inTheVault);
            return;
        }

        if (boss.CurrentPhase() != lastBossPhaseSeen)
        {
            lastBossPhaseSeen = boss.CurrentPhase();
            Note("  Warden entered phase " + lastBossPhaseSeen +
                 " at " + Mathf.RoundToInt(boss.HealthFraction() * 100f) + "% health");
        }
    }

    // Catches the round that will not end.
    //
    // The player hits this as "two enemies left, nothing on screen, stuck forever". From
    // in here it looks like a round whose living count refuses to reach zero, so the thing
    // to write down is WHERE those survivors actually are - underground, out of the world,
    // or standing somewhere unreachable.
    private void WatchForAStuckRound()
    {
        if (roundBeingWatched < 1)
        {
            return;
        }

        EnemyBrain[] everyone = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);

        // Counted afresh every frame and kept as a high-water mark. Adding them up instead
        // would count the same stuck enemy once per frame and report hundreds of them.
        int fallenRightNow = 0;
        int unreachableRightNow = 0;

        int index = 0;
        while (index < everyone.Length)
        {
            CharacterStats stats = everyone[index].GetComponent<CharacterStats>();
            if (stats != null && stats.isDead == false)
            {
                Vector3 at = everyone[index].transform.position;

                if (at.y < -5f)
                {
                    fallenRightNow = fallenRightNow + 1;
                }
                else
                {
                    // Asked as "could a walking creature be standing here at all?" rather
                    // than by comparing heights against the ground.
                    //
                    // Comparing heights sounds simpler but quietly lies: an enemy standing
                    // perfectly well underneath an overhang, or under the Vault dome, has
                    // solid rock above it and looks buried by that test. Walkable ground is
                    // the honest question, because it is the same question the player's
                    // ability to reach the thing depends on.
                    Vector3 reachable;
                    if (NavigationField.TryFindNearbyPoint(at, 2.5f, out reachable) == false)
                    {
                        unreachableRightNow = unreachableRightNow + 1;
                        WriteDownAnUnreachableEnemy(everyone[index].displayName, at);
                    }
                }
            }
            index = index + 1;
        }

        if (fallenRightNow > fellOutOfTheWorldThisRound)
        {
            fellOutOfTheWorldThisRound = fallenRightNow;
        }
        if (unreachableRightNow > buriedThisRound)
        {
            buriedThisRound = unreachableRightNow;
        }

        if (alreadyReportedThisRoundStuck == true)
        {
            return;
        }
        if (Time.time - roundStartedAt < ARoundShouldNeverTakeLongerThan)
        {
            return;
        }

        alreadyReportedThisRoundStuck = true;

        Note("  FAIL: round " + roundBeingWatched + " has run for "
             + Mathf.RoundToInt(Time.time - roundStartedAt)
             + "s without clearing. The survivors are:");

        int reported = 0;
        index = 0;
        while (index < everyone.Length)
        {
            CharacterStats stats = everyone[index].GetComponent<CharacterStats>();
            if (stats != null && stats.isDead == false)
            {
                Vector3 at = everyone[index].transform.position;
                Note("    " + everyone[index].displayName + " at " + at
                     + (at.y < -5f ? "   <<< BELOW THE WORLD" : ""));
                reported = reported + 1;
            }
            index = index + 1;
        }

        if (reported == 0)
        {
            Note("    nothing alive at all - the round is counting enemies that do not exist");
        }
    }

    // Records a handful of examples rather than every sighting, since the same stuck
    // enemy is seen again on every frame it stays stuck.
    private void WriteDownAnUnreachableEnemy(string who, Vector3 at)
    {
        if (placesAlreadyWrittenDown >= 4)
        {
            return;
        }
        placesAlreadyWrittenDown = placesAlreadyWrittenDown + 1;
        whereTheStuckOnesWere = whereTheStuckOnesWere + who + at.ToString() + "  ";
    }

    private void KillSomething()
    {
        EnemyBrain[] everyone = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);

        EnemyBrain theWarden = null;
        EnemyBrain anyoneElse = null;

        int index = 0;
        while (index < everyone.Length)
        {
            EnemyBrain one = everyone[index];
            CharacterStats stats = one.GetComponent<CharacterStats>();

            if (stats != null && stats.isDead == false)
            {
                if (one.GetComponent<WardenBoss>() != null)
                {
                    theWarden = one;
                }
                else if (anyoneElse == null)
                {
                    anyoneElse = one;
                }
            }
            index = index + 1;
        }

        // Minions first. The Warden summons help in phase three and the whole point of
        // that phase is the choice between the two, so both have to be killable.
        EnemyBrain target = anyoneElse != null ? anyoneElse : theWarden;
        if (target == null)
        {
            return;
        }

        CharacterStats targetStats = target.GetComponent<CharacterStats>();

        if (target == theWarden)
        {
            targetStats.TakeDamage(DamagePerHitOnTheWarden);
            nextActionAt = Time.time + SecondsBetweenHitsOnTheWarden;
        }
        else
        {
            targetStats.TakeDamage(99999f);
            nextActionAt = Time.time + SecondsBetweenKills;
        }

        if (targetStats.isDead == true)
        {
            killsThisRound = killsThisRound + 1;
        }
    }

    private void DescribeWhereItGotStuck()
    {
        RoundDirector director = RoundDirector.instance;
        if (director == null)
        {
            Note("  the RoundDirector was already gone");
            return;
        }
        Note("  round at the time: " + director.currentRound);
        Note("  enemies remaining: " + director.EnemiesRemaining());
        Note("  waiting for the portal: " + director.WaitingForThePortal());
        Note("  showing a banner: " + director.ShowingBanner());
        Note("  in an intermission: " + director.InIntermission());
        Note("  entered the portal: " + haveEnteredThePortal);
    }

    private void Note(string line)
    {
        report.AppendLine(line);
        Debug.Log("[SelfTest] " + line);
    }

    private void Finish()
    {
        if (finished == true)
        {
            return;
        }
        finished = true;

        Note("");
        Note("=== END OF SELF TEST ===");

        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText(ReportFolder + "selftest.log", report.ToString());
        // Written last and separately, so a reader waiting on this file knows the report
        // beside it is complete rather than half-written.
        File.WriteAllText(ReportFolder + "selftest_done.txt", "done");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
