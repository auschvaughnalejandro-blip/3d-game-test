using UnityEngine;

// Turning the running game into a save file, and a save file back into a running game.
//
// Saving happens at checkpoints rather than continuously: when the task is accepted, at
// the start of every round, when the Warden dies, and when the player comes home. That is
// often enough that quitting never costs more than one round, and rare enough that
// nothing has to be written while a fight is going on.
public class GameProgress : MonoBehaviour
{
    public static GameProgress instance;

    private GameObject playerObject;
    private CharacterStats playerStats;
    private PlayerMovement playerMovement;
    private PlayerWeapons playerWeapons;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        FindThePlayer();
    }

    private void FindThePlayer()
    {
        if (playerObject != null)
        {
            return;
        }

        playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
        {
            return;
        }

        playerStats = playerObject.GetComponent<CharacterStats>();
        playerMovement = playerObject.GetComponent<PlayerMovement>();
        playerWeapons = playerObject.GetComponent<PlayerWeapons>();
    }

    // ------------------------------------------------------------------------
    // Writing a checkpoint
    // ------------------------------------------------------------------------

    // Called from the handful of places in the story worth coming back to.
    public void SaveCheckpoint(string whereYouWere)
    {
        FindThePlayer();

        if (playerStats == null || StoryDirector.instance == null)
        {
            return;
        }

        SavedGame save = new SavedGame();

        save.whereYouWere = whereYouWere;

        save.act = StoryDirector.instance.currentAct;
        save.hasRefusedOrrin = StoryDirector.instance.hasRefusedAtLeastOnce;

        if (RoundDirector.instance != null)
        {
            // The round being fought now is the one to come back to. Half-finished rounds
            // are restarted rather than resumed.
            int round = RoundDirector.instance.currentRound;
            if (round < 1)
            {
                round = 1;
            }
            save.roundToResumeAt = round;
        }

        if (GameDirector.instance != null)
        {
            save.essence = GameDirector.instance.essenceCollected;
            save.theWardenIsDead = GameDirector.instance.theWardenIsDead;
        }

        // Only the maximums are kept. Current health and stamina are deliberately NOT
        // saved: coming back to a checkpoint on four health would be a punishment for
        // having stopped playing.
        save.maximumHealth = playerStats.maximumHealth;
        save.attackDamage = playerStats.attackDamage;
        save.maximumStamina = playerStats.maximumStamina;

        if (playerWeapons != null)
        {
            save.theEdgeHasBeenWon = playerWeapons.TheEdgeHasBeenWon();
        }

        save.WriteToDisk();
    }

    // ------------------------------------------------------------------------
    // Starting fresh
    // ------------------------------------------------------------------------

    public void BeginANewRun()
    {
        SavedGame.Delete();

        FindThePlayer();

        PutTheWorldBackToTheStart();

        if (playerStats != null)
        {
            playerStats.maximumHealth = 100f;
            playerStats.currentHealth = 100f;
            playerStats.maximumStamina = 100f;
            playerStats.currentStamina = 100f;
            playerStats.attackDamage = 20f;
            playerStats.isDead = false;
        }

        // isDead is set straight back to false here rather than being left for the next
        // frame, so PlayerAilments never sees the death it would otherwise clear itself
        // on. A new run has to start clean whatever killed the last one.
        PlayerAilments.ClearEverythingNow();

        if (GameDirector.instance != null)
        {
            GameDirector.instance.essenceCollected = 0;
            GameDirector.instance.theWardenIsDead = false;
            GameDirector.instance.ForgetAnyPendingRespawn();
        }

        if (StoryDirector.instance != null)
        {
            StoryDirector.instance.currentAct = StoryDirector.ActInTheDungeon;
            StoryDirector.instance.hasRefusedAtLeastOnce = false;
        }

        PutThePlayerInTheDungeon();
    }

    // ------------------------------------------------------------------------
    // Picking up where they left off
    // ------------------------------------------------------------------------

    public void ResumeFrom(SavedGame save)
    {
        FindThePlayer();

        if (save == null || save.thereIsSomethingSaved == false)
        {
            BeginANewRun();
            return;
        }

        // The same clean slate a new run gets. A checkpoint puts the player back at the
        // start of a round, and the round it puts them back into has to be as empty as
        // the round they first walked into - not still holding the enemies that killed
        // them, which is what happened before and made every retry harder than the
        // attempt that lost.
        PutTheWorldBackToTheStart();

        // What the player earned comes back first, whatever act they were in.
        if (playerStats != null)
        {
            playerStats.maximumHealth = save.maximumHealth;
            playerStats.currentHealth = save.maximumHealth;
            playerStats.maximumStamina = save.maximumStamina;
            playerStats.currentStamina = save.maximumStamina;
            playerStats.attackDamage = save.attackDamage;
            playerStats.isDead = false;
        }

        // Same reasoning as BeginANewRun. A checkpoint is a moment before the damage.
        PlayerAilments.ClearEverythingNow();

        if (GameDirector.instance != null)
        {
            GameDirector.instance.essenceCollected = save.essence;
            GameDirector.instance.theWardenIsDead = save.theWardenIsDead;
            GameDirector.instance.ForgetAnyPendingRespawn();
        }

        if (StoryDirector.instance != null)
        {
            StoryDirector.instance.hasRefusedAtLeastOnce = save.hasRefusedOrrin;
        }

        if (save.theEdgeHasBeenWon == true && playerWeapons != null)
        {
            playerWeapons.UnlockTheWardensEdge();
        }

        // Then the world is put into the shape that act expects.
        if (save.act <= StoryDirector.ActAccepted)
        {
            ResumeInTheDungeon(save.act);
        }
        else if (save.act == StoryDirector.ActFighting)
        {
            ResumeInTheValley(save.roundToResumeAt);
        }
        else if (save.act == StoryDirector.ActEyeIsWaiting
                 || save.act == StoryDirector.ActLeaving)
        {
            ResumeInTheVaultAfterTheWarden(save.theEdgeHasBeenWon);
        }
        else
        {
            ResumeOnTheWayHome();
        }
    }

    private void ResumeInTheDungeon(int act)
    {
        StoryDirector.instance.currentAct = act;
        PutThePlayerInTheDungeon();

        // Having already agreed, the door is standing open where they left it.
        if (act == StoryDirector.ActAccepted
            && StoryDirector.instance.doorOutOfTheDungeon != null)
        {
            StoryDirector.instance.doorOutOfTheDungeon.Open();
        }
    }

    private void ResumeInTheValley(int round)
    {
        StoryDirector.instance.currentAct = StoryDirector.ActFighting;

        // Round five is not fought in the valley at all, so resuming it means being put
        // back inside the Vault rather than at the south end of the arena.
        if (round >= 5)
        {
            PutThePlayerInTheVault();
        }
        else
        {
            MoveThePlayerTo(ValleyBuilder.PlayerStartPosition);
        }

        if (RoundDirector.instance != null)
        {
            RoundDirector.instance.BeginAtRound(round);
        }
    }

    private void ResumeInTheVaultAfterTheWarden(bool theEdgeIsAlreadyWon)
    {
        PutThePlayerInTheVault();

        if (RoundDirector.instance != null)
        {
            RoundDirector.instance.MarkEverythingCleared();
        }

        if (theEdgeIsAlreadyWon == true)
        {
            // The eye has been taken and the blade swung. All that is left is the walk
            // home, so the way out is already standing open.
            StoryDirector.instance.currentAct = StoryDirector.ActLeaving;
            if (StoryDirector.instance.doorHomeFromTheVault != null)
            {
                StoryDirector.instance.doorHomeFromTheVault.Open();
            }
            return;
        }

        // The Warden is dead but his eye was never picked up. A fresh one is put in the
        // middle of the room rather than where the body fell, because the body is gone.
        StoryDirector.instance.currentAct = StoryDirector.ActEyeIsWaiting;
        WardenGem.SpawnAt(ValleyBuilder.BossArenaOrigin + new Vector3(0f, 1.6f, 0f));
    }

    private void ResumeOnTheWayHome()
    {
        StoryDirector.instance.currentAct = StoryDirector.ActLeaving;

        if (RoundDirector.instance != null)
        {
            RoundDirector.instance.MarkEverythingCleared();
        }

        PutThePlayerInTheVault();

        // Walked back through rather than dropped into the valley, so the homecoming
        // conversation and the gate opening happen exactly as they would have.
        if (StoryDirector.instance.doorHomeFromTheVault != null)
        {
            StoryDirector.instance.doorHomeFromTheVault.Open();
        }
    }

    // ------------------------------------------------------------------------
    // Putting the world back
    // ------------------------------------------------------------------------

    // Everything that has to be undone before a run can begin, whether it is a brand new
    // one or a checkpoint being loaded.
    //
    // The game never reloads the scene - the title screen is drawn straight over the
    // living world, which is most of why it reads as a game rather than a dialog box -
    // and the price of that is that NOTHING resets itself between runs. Every one-way
    // latch, every open portal, every enemy still walking about and every line still
    // queued to be spoken is exactly where the last run left it. Restoring the player's
    // stats and standing them back in the dungeon, which is all this used to do,
    // therefore produced a run that looked new and was not: the round counter carried on
    // from where the last one died, the valley still held that round's enemies, and the
    // door out of the dungeon had already carried somebody once and quietly refused to do
    // it a second time.
    //
    // Anything that survives a run and is not put back here is a bug waiting to be
    // reported as "the game froze", because that is exactly what it looks like from the
    // outside: no error, no log line, just a player standing in a portal that will never
    // take them anywhere.
    private void PutTheWorldBackToTheStart()
    {
        // Whatever was being said, and whatever was queued behind it. A line left over
        // from the last run takes the controls away the moment play resumes, because
        // PlayerControl.IsBlocked is true for as long as a conversation is open.
        if (DialogueBox.instance != null)
        {
            DialogueBox.instance.ClearEverything();
        }

        // The rounds: enemies, barriers, cover, the round number and the phase.
        if (RoundDirector.instance != null)
        {
            RoundDirector.instance.ResetForANewRun();
        }

        // The story's own leavings: a beat waiting to fire, the north gate standing open,
        // Orrin still out in the valley waiting to say goodbye.
        if (StoryDirector.instance != null)
        {
            StoryDirector.instance.ResetForANewRun();
        }

        // Every portal in the scene, found by type rather than through the two references
        // the builders happen to fill in. There are three, and a portal missed here is a
        // portal the player walks into and stands in.
        Portal[] everyPortal = Object.FindObjectsByType<Portal>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int portalIndex = 0;
        while (portalIndex < everyPortal.Length)
        {
            everyPortal[portalIndex].ResetToClosed();
            portalIndex = portalIndex + 1;
        }

        // Daylight back over the valley. The Vault's near-darkness is applied to the
        // whole scene's lighting, so a run that reached the Vault hands the next one a
        // valley lit like a cellar.
        VaultAtmosphere atmosphere = Object.FindFirstObjectByType<VaultAtmosphere>();
        if (atmosphere != null)
        {
            atmosphere.ReturnToTheValley();
        }

        // The reward goes back in the Warden's chest. ResumeFrom puts it into the hand
        // again afterwards if the save says it had been won, so the order matters here.
        if (playerWeapons != null)
        {
            playerWeapons.RelockTheWardensEdge();
        }

        if (playerObject != null)
        {
            PlayerSurge surge = playerObject.GetComponent<PlayerSurge>();
            if (surge != null)
            {
                surge.ClearEverything();
            }
        }

        CoachLines coaching = Object.FindFirstObjectByType<CoachLines>();
        if (coaching != null)
        {
            coaching.ResetForANewRun();
        }

        SweepUpWhatTheLastRunDropped();
    }

    // The loose objects that are made at runtime and belong to nobody: shards lying on
    // the ground, the Warden's eye if it was never picked up, arrows stuck in the
    // scenery, and the burnt patches the Warden leaves. None of them are in the scene to
    // begin with, so leaving them about is a new run starting in a used valley.
    private void SweepUpWhatTheLastRunDropped()
    {
        EssencePickup[] shards = Object.FindObjectsByType<EssencePickup>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int shardIndex = 0;
        while (shardIndex < shards.Length)
        {
            Destroy(shards[shardIndex].gameObject);
            shardIndex = shardIndex + 1;
        }

        WardenGem[] gems = Object.FindObjectsByType<WardenGem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int gemIndex = 0;
        while (gemIndex < gems.Length)
        {
            Destroy(gems[gemIndex].gameObject);
            gemIndex = gemIndex + 1;
        }
        // The name of the weapon is written across the screen from a static counter, so
        // it outlives the gem that set it running.
        WardenGem.SecondsOfNameLeft = 0f;

        Arrow[] arrows = Object.FindObjectsByType<Arrow>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int arrowIndex = 0;
        while (arrowIndex < arrows.Length)
        {
            Destroy(arrows[arrowIndex].gameObject);
            arrowIndex = arrowIndex + 1;
        }

        ScorchedGround[] burns = Object.FindObjectsByType<ScorchedGround>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int burnIndex = 0;
        while (burnIndex < burns.Length)
        {
            Destroy(burns[burnIndex].gameObject);
            burnIndex = burnIndex + 1;
        }
    }

    // ------------------------------------------------------------------------
    // Moving the player about
    // ------------------------------------------------------------------------

    private void PutThePlayerInTheDungeon()
    {
        MoveThePlayerTo(DungeonBuilder.DungeonOrigin + DungeonBuilder.PlayerStandsAt);
    }

    private void PutThePlayerInTheVault()
    {
        MoveThePlayerTo(ValleyBuilder.BossArenaOrigin + new Vector3(0f, 2.5f, -18f));

        // Walking through the portal turns the world dark on the way in. Being PUT here
        // by a loaded save skips that, and the room is then lit by valley daylight -
        // which washes the braziers and the crystals out to flat pale shapes and reads as
        // the Vault having failed to build rather than as the lighting being wrong.
        VaultAtmosphere atmosphere = Object.FindFirstObjectByType<VaultAtmosphere>();
        if (atmosphere != null)
        {
            atmosphere.EnterTheVault();
        }
    }

    private void MoveThePlayerTo(Vector3 where)
    {
        FindThePlayer();

        if (playerMovement != null)
        {
            playerMovement.TeleportTo(where);
            return;
        }

        // Before Start has run there is no movement script to ask, so the controller is
        // moved by hand the same way the builder does it.
        if (playerObject == null)
        {
            return;
        }

        CharacterController controller = playerObject.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
        playerObject.transform.position = where;
        if (controller != null)
        {
            controller.enabled = true;
        }
    }

    // A short description of where the player is now, for the Continue button.
    public string DescribeWhereWeAre()
    {
        if (StoryDirector.instance == null)
        {
            return "The Dungeon";
        }

        int act = StoryDirector.instance.currentAct;

        if (act <= StoryDirector.ActAccepted)
        {
            return "The Dungeon";
        }
        if (act == StoryDirector.ActFighting && RoundDirector.instance != null)
        {
            int round = RoundDirector.instance.currentRound;
            if (round < 1)
            {
                round = 1;
            }
            return "Round " + round + " - " + RoundDirector.instance.RoundName(round);
        }
        if (act == StoryDirector.ActEyeIsWaiting || act == StoryDirector.ActLeaving)
        {
            return "The Vault";
        }
        return "The Long Way Home";
    }
}
