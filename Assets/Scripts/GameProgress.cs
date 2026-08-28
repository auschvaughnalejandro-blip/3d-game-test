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

        if (playerStats != null)
        {
            playerStats.maximumHealth = 100f;
            playerStats.currentHealth = 100f;
            playerStats.maximumStamina = 100f;
            playerStats.currentStamina = 100f;
            playerStats.attackDamage = 20f;
            playerStats.isDead = false;
        }

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
    // Moving the player about
    // ------------------------------------------------------------------------

    private void PutThePlayerInTheDungeon()
    {
        MoveThePlayerTo(DungeonBuilder.DungeonOrigin + DungeonBuilder.PlayerStandsAt);
    }

    private void PutThePlayerInTheVault()
    {
        MoveThePlayerTo(ValleyBuilder.BossArenaOrigin + new Vector3(0f, 2.5f, -18f));
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
