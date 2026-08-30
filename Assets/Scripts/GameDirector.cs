using UnityEngine;

// The referee. Owns the things that are true about the run as a whole rather than about
// any one character: how much essence has been collected, what happens when the player
// dies, and whether the slice has been won.
public class GameDirector : MonoBehaviour
{
    // A single well-known instance so enemies can report a death without every one of
    // them needing a reference wired up by hand.
    public static GameDirector instance;

    [Header("Upgrade costs")]
    public int essenceCostPerUpgrade = 3;
    public float healthGainedPerUpgrade = 25f;
    public float damageGainedPerUpgrade = 6f;
    public float staminaGainedPerUpgrade = 20f;

    public int essenceCollected = 0;
    public bool theWardenIsDead = false;

    private GameObject playerObject;
    private CharacterStats playerStats;
    private PlayerSurge playerSurge;

    // Death is not instant. The HUD caption gets these couple of seconds to itself before
    // the death screen comes up over it, so that being killed reads as a moment in the
    // game rather than as a dialog box appearing.
    private float secondsUntilTheDeathScreen = 0f;
    public bool PlayerIsDead()
    {
        return playerStats != null && playerStats.isDead == true;
    }

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            playerStats = playerObject.GetComponent<CharacterStats>();

            // The kill-streak meter, added here rather than only in ValleyBuilder. The
            // valley is built from the editor menu and then SAVED into the scene, so the
            // player that actually plays is the one already serialised there - it would
            // not carry the component until somebody rebuilt the valley by hand.
            playerSurge = playerObject.GetComponent<PlayerSurge>();
            if (playerSurge == null)
            {
                playerSurge = playerObject.AddComponent<PlayerSurge>();
            }

            // Same reasoning for the thing that drives the player's limbs.
            if (playerObject.GetComponent<PlayerAnimator>() == null)
            {
                playerObject.AddComponent<PlayerAnimator>();
            }

            // The component can be added here. The MODEL cannot.
            //
            // A player serialised into the scene before the segmented mesh existed is
            // still wearing the old single lump of geometry, which was built in a T-pose
            // and has no separate limbs to move. Nothing at runtime can fix that - the
            // valley has to be rebuilt so the player is assembled again from the models
            // that exist now.
            //
            // This says so out loud, because the failure is completely silent otherwise:
            // the enemies animate perfectly (they are spawned fresh every round, so they
            // are built by the current code) while the player stands in a T-pose, and
            // that looks like the player animation being broken rather than like a stale
            // scene.
            if (playerObject.GetComponentInChildren<ProceduralAnimator>(true) == null)
            {
                Debug.LogWarning("The player in this scene has no segmented model, so it "
                    + "cannot be animated and will stand in a T-pose. This player was "
                    + "saved into the scene before the segmented mesh existed. Rebuild "
                    + "with One Valley > Rebuild Valley (Ctrl+Shift+R). Enemies are "
                    + "spawned fresh each round so they are unaffected, which is why "
                    + "they animate and the player does not.");
            }
        }

    }

    void Update()
    {
        // Noticed here as well as being reported by whatever did the killing.
        //
        // OnPlayerDied is called from exactly three places - the melee enemies, the rocks
        // the spitters throw, and the Warden - and every one of them has to remember to
        // call it. A fourth way to die added later and wired up by somebody who did not
        // know about the other three would leave a corpse lying in the valley with no
        // screen over it and no way on, which is a miserable thing to have to go hunting
        // for. Watching the flag costs one comparison a frame and makes the screen follow
        // from BEING dead rather than from having been killed by something well mannered
        // enough to say so.
        if (secondsUntilTheDeathScreen <= 0f
            && PlayerIsDead() == true
            && MainMenu.IsShowing() == false)
        {
            secondsUntilTheDeathScreen = 2.2f;
        }

        if (secondsUntilTheDeathScreen > 0f)
        {
            secondsUntilTheDeathScreen = secondsUntilTheDeathScreen - Time.deltaTime;
            if (secondsUntilTheDeathScreen <= 0f)
            {
                // Only if they are STILL dead. Anything that puts the player back on their
                // feet during those two seconds - a checkpoint loaded from the pause
                // screen, the automated play-through topping their health back up - means
                // there is no death left to report, and a screen that appeared anyway
                // would stop a perfectly healthy game for no reason anyone could see.
                if (PlayerIsDead() == true)
                {
                    MainMenu.ShowTheDeathScreen();
                }
            }
        }
    }

    public void OnEnemyDied(EnemyBrain whoDied, int essenceDropped, Vector3 whereTheyFell)
    {
        // Every death in the game already reports here, so this is the only place the
        // kill streak has to be told about. No enemy needs to know the meter exists.
        if (playerSurge != null)
        {
            playerSurge.AwardPointsForKilling(whoDied.displayName);
        }

        if (whoDied.isTheWarden == true)
        {
            theWardenIsDead = true;

            // He was carrying the thing that sealed him in. It is dropped a little above
            // the floor so it is visible over the body rather than inside it.
            WardenGem.SpawnAt(whereTheyFell + Vector3.up * 1.4f);
        }

        if (essenceDropped > 0)
        {
            EssencePickup.SpawnAt(whereTheyFell + Vector3.up * 0.6f, essenceDropped);
        }
    }

    // Dying no longer quietly restarts the round after a two-second pause. It puts up the
    // death screen instead, and the player chooses: load the last checkpoint - which, as
    // checkpoints are written at the start of every round, is the same round over again
    // with the stats they went in with - or go back to the title.
    public void OnPlayerDied()
    {
        secondsUntilTheDeathScreen = 2.2f;
    }

    // Called whenever the menu starts or loads a run. A death that was still counting
    // down when the player paused would otherwise finish counting the moment the game
    // started running again and throw the death screen up over a run that had just been
    // started or loaded - so pausing while dead and choosing New Game put the player in
    // the dungeon and then killed them there a second or two later.
    public void ForgetAnyPendingRespawn()
    {
        secondsUntilTheDeathScreen = 0f;
    }

    public void CollectEssence(int howMuch)
    {
        essenceCollected = essenceCollected + howMuch;
    }

    // Which stat to raise. Called by the shrine.
    public bool TryBuyUpgrade(int whichUpgrade)
    {
        if (essenceCollected < essenceCostPerUpgrade)
        {
            return false;
        }
        if (playerStats == null)
        {
            return false;
        }

        essenceCollected = essenceCollected - essenceCostPerUpgrade;

        if (whichUpgrade == 1)
        {
            playerStats.maximumHealth = playerStats.maximumHealth + healthGainedPerUpgrade;
            playerStats.currentHealth = playerStats.maximumHealth;
        }
        else if (whichUpgrade == 2)
        {
            playerStats.attackDamage = playerStats.attackDamage + damageGainedPerUpgrade;
        }
        else if (whichUpgrade == 3)
        {
            playerStats.maximumStamina = playerStats.maximumStamina + staminaGainedPerUpgrade;
            playerStats.currentStamina = playerStats.maximumStamina;
        }

        return true;
    }
}
