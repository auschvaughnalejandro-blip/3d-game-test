using UnityEngine;
using System.Collections.Generic;

// One round: what spawns, where, and what the valley looks like while it happens.
// Pure data, the same way an enemy is pure data.
[System.Serializable]
public class RoundPlan
{
    public string roundName = "The Approach";

    public int grunts = 0;
    public int darters = 0;
    public int spitters = 0;
    public bool theWarden = false;

    // Which stretch of the valley this round is fought in.
    public int zone = RoundDirector.ZoneApproach;

    // When true the enemies appear in a ring AROUND the player rather than from the
    // edges of the zone. There is nowhere to back away to, which is a very different
    // opening to the same number of enemies arriving from one side.
    public bool surroundsPlayer = false;
    public float surroundRadius = 13f;

    // A second half that arrives once this fraction of the first has been killed.
    // Zero means the whole round spawns at once.
    public float secondWaveAfterFraction = 0f;
    public int secondWaveGrunts = 0;
    public int secondWaveDarters = 0;
    public int secondWaveSpitters = 0;
}

// The five rounds.
//
// GameDirector still owns essence, upgrades and what happens when the player dies. This
// owns waves and the shape of the arena, and nothing else - two files rather than one
// enormous one.
public class RoundDirector : MonoBehaviour
{
    public static RoundDirector instance;

    public const int ZoneApproach = 0;
    public const int ZoneNarrows = 1;
    public const int ZoneHollow = 2;
    public const int ZoneWholeValley = 3;
    // The Vault, reached through the portal. A separate room entirely, not a stretch of
    // the valley - which is the whole point of the final round.
    public const int ZoneVault = 4;

    [Header("Timing")]
    public float secondsBeforeFirstRound = 3f;
    public float secondsBetweenRounds = 10f;
    public float bannerSeconds = 3.5f;

    // Filled in by ValleyBuilder.
    [HideInInspector] public List<Transform> approachSpawns = new List<Transform>();
    [HideInInspector] public List<Transform> narrowsSpawns = new List<Transform>();
    [HideInInspector] public List<Transform> hollowSpawns = new List<Transform>();
    [HideInInspector] public List<Transform> elevatedSpawns = new List<Transform>();

    [HideInInspector] public ZoneBarrier narrowsBarrier;
    [HideInInspector] public ZoneBarrier hollowBarrier;
    [HideInInspector] public List<Pillar> narrowsPillars = new List<Pillar>();
    [HideInInspector] public List<Pillar> hollowPillars = new List<Pillar>();
    [HideInInspector] public List<Pillar> vaultPillars = new List<Pillar>();
    [HideInInspector] public Portal thePortal;

    private RoundPlan[] rounds;

    // Which round is being fought, counting from one so it can be shown directly.
    public int currentRound = 0;
    public bool allRoundsCleared = false;

    private List<EnemyBrain> aliveThisRound = new List<EnemyBrain>();
    private int spawnedThisRound = 0;
    private bool secondWaveSent = false;

    private const int PhaseWaitingToStart = 0;
    private const int PhaseBanner = 1;
    private const int PhaseFighting = 2;
    private const int PhaseIntermission = 3;
    private const int PhaseFinished = 4;
    // Round four is over, the portal is up, and the game is waiting for the player to
    // step through it.
    private const int PhaseWaitingForPortal = 5;
    // The player is still down in the dungeon and has not agreed to fight anything.
    // Nothing spawns, nothing counts down, and the valley sits empty until the story
    // says otherwise.
    private const int PhaseHeldByTheStory = 6;

    private int phase = PhaseWaitingToStart;
    private float phaseSecondsRemaining = 0f;

    private GameObject playerObject;
    private PlayerHealing playerHealing;
    private PlayerMovement playerMovement;
    private CharacterStats playerStats;
    private ShrineOfEssence theShrine;

    void Awake()
    {
        instance = this;
        EnsureRoundsBuilt();
    }

    // The round list is built on first use rather than only in Awake, because Awake does
    // not run in the editor. Anything inspecting the rounds from a tool or a scene check
    // would otherwise be reading a null array.
    private void EnsureRoundsBuilt()
    {
        if (rounds == null)
        {
            BuildTheRounds();
        }
    }

    void Start()
    {
        playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            playerHealing = playerObject.GetComponent<PlayerHealing>();
            playerMovement = playerObject.GetComponent<PlayerMovement>();
            playerStats = playerObject.GetComponent<CharacterStats>();
        }

        // Everything starts sunk. The valley opens up one round at a time.
        HideAllPillars();
        if (narrowsBarrier != null)
        {
            narrowsBarrier.SnapTo(false);
        }
        if (hollowBarrier != null)
        {
            hollowBarrier.SnapTo(false);
        }

        theShrine = Object.FindFirstObjectByType<ShrineOfEssence>();

        // With a story in the scene the rounds wait to be sent for. Without one - an old
        // scene, or a test harness that builds the valley on its own - the original
        // behaviour is kept and the fight starts on a timer.
        if (Object.FindFirstObjectByType<StoryDirector>() != null)
        {
            phase = PhaseHeldByTheStory;
            phaseSecondsRemaining = 0f;
            return;
        }

        phase = PhaseWaitingToStart;
        phaseSecondsRemaining = secondsBeforeFirstRound;
    }

    // Called by the story once the player has walked up out of the dungeon.
    public void BeginTheFirstRound()
    {
        if (phase != PhaseHeldByTheStory)
        {
            return;
        }

        phase = PhaseWaitingToStart;
        phaseSecondsRemaining = secondsBeforeFirstRound;
    }

    // True in the quiet gap between two rounds. Read by the coaching lines, which use it
    // to find the one moment the player is safe enough to be told about the shrine.
    public bool IsBetweenRounds()
    {
        return phase == PhaseIntermission;
    }

    // Drop straight into a particular round. Used when a saved game is loaded, which is
    // the only time the fight starts anywhere other than the beginning.
    public void BeginAtRound(int roundNumber)
    {
        EnsureRoundsBuilt();

        if (roundNumber < 1)
        {
            roundNumber = 1;
        }
        if (roundNumber > rounds.Length)
        {
            roundNumber = rounds.Length;
        }

        StartRound(roundNumber);
    }

    // Everything is already over. Used when a save is loaded from after the Warden died,
    // so nothing tries to start a sixth round or wait for a fight that has been won.
    public void MarkEverythingCleared()
    {
        EnsureRoundsBuilt();

        RemoveEveryEnemy();
        currentRound = rounds.Length;
        allRoundsCleared = true;
        phase = PhaseFinished;
    }

    // Put the arena back to the state it is in when the game first boots: empty, sealed,
    // no cover raised, no round being fought, waiting to be sent for by the story.
    //
    // This is what New Game was missing. Nothing in here is written to disk and nothing
    // in here is rebuilt when a run starts, so a second run inherited the first one's
    // round number, its live enemies, its raised barriers and its smashed cover - and the
    // first thing the player saw was a HUD counting enemies they had never met.
    public void ResetForANewRun()
    {
        EnsureRoundsBuilt();

        RemoveEveryEnemy();

        currentRound = 0;
        allRoundsCleared = false;
        spawnedThisRound = 0;
        secondWaveSent = false;
        phaseSecondsRemaining = 0f;

        HideAllPillars();
        if (narrowsBarrier != null)
        {
            narrowsBarrier.SnapTo(false);
        }
        if (hollowBarrier != null)
        {
            hollowBarrier.SnapTo(false);
        }

        // Held for the story again, exactly the way Start leaves it. Getting this one
        // line wrong is silent and looks nothing like its cause: BeginTheFirstRound does
        // nothing unless the phase is PhaseHeldByTheStory, so a phase carried over from
        // the last run means walking up out of the dungeon starts no round at all, and
        // whatever round the last run died on simply carries on underneath.
        if (Object.FindFirstObjectByType<StoryDirector>() != null)
        {
            phase = PhaseHeldByTheStory;
        }
        else
        {
            phase = PhaseWaitingToStart;
            phaseSecondsRemaining = secondsBeforeFirstRound;
        }
    }

    // The name of a round, counting from one. Used on the Continue button so it says
    // where the player was rather than just offering to carry on.
    public string RoundName(int roundNumber)
    {
        EnsureRoundsBuilt();

        if (roundNumber < 1 || roundNumber > rounds.Length)
        {
            return "";
        }
        return rounds[roundNumber - 1].roundName;
    }

    private void BuildTheRounds()
    {
        rounds = new RoundPlan[5];

        // 1. Wide open ground, slow enemies, nothing to hide behind because there is
        //    nothing yet worth hiding from.
        RoundPlan one = new RoundPlan();
        one.roundName = "THE APPROACH";
        one.grunts = 6;
        one.zone = ZoneHollow;
        // Ringed, so the opening is about turning and picking a direction rather than
        // backing up and fighting one at a time.
        one.surroundsPlayer = true;
        one.surroundRadius = 13f;
        // Two more close in once a third of the ring is down, so clearing a gap does not
        // simply end the round.
        one.secondWaveAfterFraction = 0.34f;
        one.secondWaveGrunts = 2;
        rounds[0] = one;

        // 2. Darters arrive. The lesson is the LINE: a charge is locked in once it
        //    starts, so it is beaten by stepping aside, never by backing away.
        RoundPlan two = new RoundPlan();
        two.roundName = "THE PACK";
        two.grunts = 4;
        two.darters = 4;
        two.zone = ZoneHollow;
        two.secondWaveAfterFraction = 0.5f;
        two.secondWaveDarters = 3;
        rounds[1] = two;

        // 3. Cover rises out of the ground in the same round as the enemy that demands
        //    it. Spitters take the high shoulders and shoot down into the arena.
        RoundPlan three = new RoundPlan();
        three.roundName = "CROSSFIRE";
        three.grunts = 3;
        three.darters = 4;
        three.spitters = 4;
        three.zone = ZoneHollow;
        three.secondWaveAfterFraction = 0.5f;
        three.secondWaveSpitters = 2;
        rounds[2] = three;

        // 4. All three kinds at once, split into two waves so the pressure never lets up
        //    rather than arriving as one unmanageable blob.
        RoundPlan four = new RoundPlan();
        four.roundName = "THE HORDE";
        four.grunts = 5;
        four.darters = 5;
        four.spitters = 3;
        four.zone = ZoneHollow;
        four.secondWaveAfterFraction = 0.5f;
        four.secondWaveGrunts = 2;
        four.secondWaveDarters = 2;
        four.secondWaveSpitters = 2;
        rounds[3] = four;

        // 5. Not here. Clearing round four raises the portal, and the Warden is fought
        //    in the Vault - a different, sealed room reached by walking into it.
        RoundPlan five = new RoundPlan();
        five.roundName = "THE WARDEN";
        five.theWarden = true;
        five.zone = ZoneVault;
        rounds[4] = five;
    }

    void Update()
    {
        if (phase == PhaseFinished)
        {
            return;
        }

        if (phaseSecondsRemaining > 0f)
        {
            phaseSecondsRemaining = phaseSecondsRemaining - Time.deltaTime;
        }

        if (phase == PhaseWaitingToStart)
        {
            if (phaseSecondsRemaining <= 0f)
            {
                StartRound(1);
            }
        }
        else if (phase == PhaseBanner)
        {
            if (phaseSecondsRemaining <= 0f)
            {
                SpawnTheFirstWave();
            }
        }
        else if (phase == PhaseFighting)
        {
            WatchTheFight();
        }
        else if (phase == PhaseIntermission)
        {
            if (phaseSecondsRemaining <= 0f && TheShrineIsFinishedWith() == true)
            {
                StartRound(currentRound + 1);
            }
        }
        else if (phase == PhaseWaitingForPortal)
        {
            // Nothing to do. The portal calls back when the player walks through it.
        }
    }

    // Ten seconds is not enough time to hear "there is a shrine", find it, walk to it
    // and read three options - and a timer that runs out while the player is still
    // reading is exactly the kind of thing that makes a live demonstration go badly.
    //
    // So the gap between rounds does not end while the player is standing at the shrine.
    // Wandering off is what says they are finished, which needs no explaining to anybody.
    private bool TheShrineIsFinishedWith()
    {
        if (theShrine == null)
        {
            return true;
        }
        return theShrine.PlayerIsCloseEnough() == false;
    }

    // ------------------------------------------------------------------------
    // Running a round
    // ------------------------------------------------------------------------

    public void StartRound(int roundNumber)
    {
        EnsureRoundsBuilt();
        if (roundNumber > rounds.Length)
        {
            phase = PhaseFinished;
            allRoundsCleared = true;
            return;
        }

        currentRound = roundNumber;
        spawnedThisRound = 0;
        secondWaveSent = false;
        aliveThisRound.Clear();

        RoundPlan plan = rounds[roundNumber - 1];
        ShapeTheValleyFor(plan);

        // Potions come back between rounds. Rationing them WITHIN a round is the
        // decision; rationing them across the whole run would just be attrition.
        if (playerHealing != null)
        {
            playerHealing.RefillAllCharges();
        }

        // And the quiver, for exactly the same reason. Twenty arrows is a decision about
        // how to spend a minute; carrying an empty quiver into a fresh round would make
        // it a decision about how to spend the whole run, and the player would have no
        // way to see that they had started a round already out of ammunition.
        //
        // It also matters on a checkpoint reload, which comes through here: dying with
        // two arrows left and reloading into two arrows left would quietly make a hard
        // retry harder every time it was attempted.
        // Looked up here rather than cached alongside the others at startup. GameDirector
        // is what ADDS this component to a player that was serialised into the scene
        // without one, and the order two scripts run their Start in is not defined - so a
        // reference taken at startup is null on exactly the runs where GameDirector went
        // second, and the quiver would then never refill on those runs and only those.
        // Once per round is not a lookup worth caching to get wrong.
        if (playerObject != null)
        {
            PlayerQuiver quiver = playerObject.GetComponent<PlayerQuiver>();
            if (quiver != null)
            {
                quiver.RefillNow();
            }
        }

        if (playerStats != null)
        {
            playerStats.currentStamina = playerStats.maximumStamina;
        }

        GameSound.Play("RoundStart", 0.8f);

        // Saved here rather than when the round is cleared, so that dying and quitting
        // both come back to the same place: the start of the round you were fighting.
        if (GameProgress.instance != null)
        {
            GameProgress.instance.SaveCheckpoint("Round " + roundNumber + " - " + plan.roundName);
        }

        phase = PhaseBanner;
        phaseSecondsRemaining = bannerSeconds;
    }

    private void SpawnTheFirstWave()
    {
        RoundPlan plan = rounds[currentRound - 1];

        SpawnMany(plan.grunts, plan.darters, plan.spitters, plan.zone, plan);

        if (plan.theWarden == true)
        {
            SpawnTheWarden();
        }

        spawnedThisRound = aliveThisRound.Count;
        Debug.Log("Round " + currentRound + " (" + plan.roundName + ") spawned "
            + spawnedThisRound + " enemies.");

        // A round with nothing in it would clear itself immediately and skip straight to
        // the next, which reads as the game playing itself.
        if (spawnedThisRound == 0)
        {
            Debug.LogError("Round " + currentRound + " spawned NOTHING - check the spawn points.");
        }

        phase = PhaseFighting;
    }

    private void WatchTheFight()
    {
        int stillAlive = CountTheLiving();

        RoundPlan plan = rounds[currentRound - 1];

        // The second half of a split round arrives partway through the first.
        if (plan.secondWaveAfterFraction > 0f && secondWaveSent == false && spawnedThisRound > 0)
        {
            float killedFraction = 1f - (stillAlive / (float)spawnedThisRound);
            if (killedFraction >= plan.secondWaveAfterFraction)
            {
                SpawnMany(plan.secondWaveGrunts, plan.secondWaveDarters,
                    plan.secondWaveSpitters, plan.zone, plan);
                secondWaveSent = true;
                Debug.Log("Round " + currentRound + " second wave arrived.");
            }
        }

        if (stillAlive > 0)
        {
            return;
        }

        // Round cleared.
        if (currentRound >= rounds.Length)
        {
            phase = PhaseFinished;
            allRoundsCleared = true;
            return;
        }

        // Clearing the fourth round does not simply roll into the fifth. The portal
        // rises, and the player walks into it when they are ready - so the last round is
        // entered deliberately rather than arriving on a timer.
        if (currentRound == 4)
        {
            // Looked up here rather than trusted from the build, because a null reference
            // would not fail - it would quietly drop through to an ordinary intermission
            // and start the boss round in the valley with no Vault and no portal at all.
            if (thePortal == null)
            {
                thePortal = Object.FindFirstObjectByType<Portal>();
            }

            if (thePortal == null)
            {
                Debug.LogError("Round 4 is clear but there is no Portal in the scene, so "
                    + "the Vault cannot be reached. Rebuild the valley.");
            }
            else
            {
                thePortal.Open();
                phase = PhaseWaitingForPortal;
                return;
            }
        }

        GameSound.Play("RoundCleared", 0.8f);

        phase = PhaseIntermission;
        phaseSecondsRemaining = secondsBetweenRounds;
    }

    private int CountTheLiving()
    {
        int living = 0;
        int index = 0;
        while (index < aliveThisRound.Count)
        {
            EnemyBrain brain = aliveThisRound[index];
            if (brain != null && brain.gameObject.activeSelf == true)
            {
                CharacterStats stats = brain.GetComponent<CharacterStats>();
                if (stats != null && stats.isDead == false)
                {
                    living = living + 1;
                }
            }
            index = index + 1;
        }
        return living;
    }

    // Restarts the round the player died on, rather than the whole run. Reaching round
    // five and being sent back to round one would be punishing in a demo meant to be
    // watched for ten minutes.
    public void RestartCurrentRound()
    {
        RemoveEveryEnemy();

        if (playerStats != null)
        {
            playerStats.RestoreEverything();
        }

        // Health and stamina come back, so the bleeding has to go. Restarting a round
        // still carrying a wound from the attempt before it would be the round starting
        // already lost.
        PlayerAilments.ClearEverythingNow();
        if (playerMovement != null)
        {
            playerMovement.TeleportTo(StartingPointForZone(rounds[currentRound - 1].zone));
        }

        StartRound(currentRound);
    }

    private void RemoveEveryEnemy()
    {
        // Swept by type rather than walked down the tracked list, because the tracked
        // list is the one thing guaranteed NOT to know about the enemies that need
        // removing. StartRound empties it without destroying anything, so any round that
        // was interrupted - by dying, by loading a checkpoint, by starting a new run -
        // leaves its enemies alive in the valley and owned by nobody. Those orphans are
        // what a "new" game used to open with, and the enemy counter on the HUD was
        // reporting them perfectly accurately.
        EnemyBrain[] everyEnemy = Object.FindObjectsByType<EnemyBrain>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int index = 0;
        while (index < everyEnemy.Length)
        {
            Object.Destroy(everyEnemy[index].gameObject);
            index = index + 1;
        }
        aliveThisRound.Clear();

        // Anything already in flight would otherwise keep hitting a player who has just
        // respawned at the start of the round.
        Projectile[] inFlight = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        int flyingIndex = 0;
        while (flyingIndex < inFlight.Length)
        {
            Object.Destroy(inFlight[flyingIndex].gameObject);
            flyingIndex = flyingIndex + 1;
        }
    }

    // ------------------------------------------------------------------------
    // Shaping the valley
    // ------------------------------------------------------------------------

    private void ShapeTheValleyFor(RoundPlan plan)
    {
        if (plan.zone == ZoneVault)
        {
            // Nothing to seal - the Vault is already a closed room. Only the cover needs
            // raising, and it rises as the player walks in.
            RaisePillars(vaultPillars);
            return;
        }

        if (plan.zone == ZoneApproach)
        {
            // Sealed to the north. Bare ground, nothing to hide behind.
            RaiseBarrier(narrowsBarrier, true);
            RaiseBarrier(hollowBarrier, false);
        }
        else if (plan.zone == ZoneNarrows)
        {
            // The way back closes and the way forward stays shut. Cover appears among
            // the rocks.
            RaiseBarrier(narrowsBarrier, false);
            RaiseBarrier(hollowBarrier, true);
            RaisePillars(narrowsPillars);
        }
        else if (plan.zone == ZoneHollow)
        {
            // Pushed into the arena and shut in. This is also round five.
            RaiseBarrier(narrowsBarrier, false);
            RaiseBarrier(hollowBarrier, false);
            RaisePillars(narrowsPillars);
            RaisePillars(hollowPillars);
        }
        else
        {
            // The whole valley at once, every barrier down, the biggest space in the game.
            RaiseBarrier(narrowsBarrier, false);
            RaiseBarrier(hollowBarrier, false);
            RaisePillars(narrowsPillars);
            RaisePillars(hollowPillars);
        }
    }

    private void RaiseBarrier(ZoneBarrier barrier, bool shouldBeUp)
    {
        if (barrier == null)
        {
            return;
        }
        if (shouldBeUp == true)
        {
            barrier.Raise();
        }
        else
        {
            barrier.Sink();
        }
    }

    private void RaisePillars(List<Pillar> pillars)
    {
        int index = 0;
        while (index < pillars.Count)
        {
            if (pillars[index] != null)
            {
                pillars[index].BeginRising();
            }
            index = index + 1;
        }
    }

    // Every pillar in the game, the Vault's included - which the older version of this
    // missed, so cover smashed during the boss fight stayed smashed for the rest of the
    // session.
    private void HideAllPillars()
    {
        RestorePillars(narrowsPillars);
        RestorePillars(hollowPillars);
        RestorePillars(vaultPillars);
    }

    private void RestorePillars(List<Pillar> pillars)
    {
        int index = 0;
        while (index < pillars.Count)
        {
            if (pillars[index] != null)
            {
                pillars[index].RestoreForANewRun();
            }
            index = index + 1;
        }
    }

    // ------------------------------------------------------------------------
    // Spawning
    // ------------------------------------------------------------------------

    private void SpawnMany(int grunts, int darters, int spitters, int zone, RoundPlan plan)
    {
        int total = grunts + darters + spitters;
        int placed = 0;

        int index = 0;
        while (index < grunts)
        {
            SpawnOne("Grunt", ChooseAPlace(zone, false, plan, placed, total));
            placed = placed + 1;
            index = index + 1;
        }
        index = 0;
        while (index < darters)
        {
            SpawnOne("Darter", ChooseAPlace(zone, false, plan, placed, total));
            placed = placed + 1;
            index = index + 1;
        }
        index = 0;
        while (index < spitters)
        {
            // Throwers prefer the high ground, which is what forces the player to break
            // cover and climb towards them.
            SpawnOne("Spitter", ChooseAPlace(zone, true, plan, placed, total));
            placed = placed + 1;
            index = index + 1;
        }
    }

    // Either a ring around the player or one of the zone spawn points.
    private Vector3 ChooseAPlace(int zone, bool preferElevated, RoundPlan plan,
        int which, int total)
    {
        if (plan != null && plan.surroundsPlayer == true && playerObject != null && total > 0)
        {
            float angle = (which / (float)total) * Mathf.PI * 2f;
            Vector3 around = playerObject.transform.position
                + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * plan.surroundRadius;
            around.y = playerObject.transform.position.y + 0.5f;
            return around;
        }

        return PickSpawn(zone, preferElevated);
    }

    private void SpawnOne(string kind, Vector3 where)
    {
        EnemyBrain spawned = ValleyBuilder.SpawnEnemy(kind, where);
        if (spawned == null)
        {
            // Said out loud, because a spawn that fails quietly makes a round report
            // itself cleared the instant it starts - which is exactly what happened
            // before, and looked like the round system was broken rather than the spawn.
            Debug.LogError("RoundDirector could not spawn a " + kind + " at " + where);
            return;
        }
        aliveThisRound.Add(spawned);
    }

    private void SpawnTheWarden()
    {
        // On the dais at the far side of the Vault, so the player walks in and finds it
        // standing above them.
        Vector3 onTheDais = ValleyBuilder.BossArenaOrigin + new Vector3(0f, 2.5f, 15f);
        EnemyBrain warden = ValleyBuilder.SpawnEnemy("Warden", onTheDais);
        if (warden != null)
        {
            aliveThisRound.Add(warden);
        }
    }

    // Called by the portal once it has carried the player to the Vault.
    public void OnPlayerEnteredThePortal()
    {
        // The lighting changes with the room. Until the valley daylight is turned down,
        // the Vault's fires and crystals are washed out and read as flat pale shapes.
        VaultAtmosphere atmosphere = Object.FindFirstObjectByType<VaultAtmosphere>();
        if (atmosphere != null)
        {
            atmosphere.EnterTheVault();
        }

        StartRound(5);
    }

    public bool WaitingForThePortal()
    {
        return phase == PhaseWaitingForPortal;
    }

    // Used by the Warden when it summons help mid-fight.
    public void AddSummonedEnemy(EnemyBrain brain)
    {
        if (brain != null)
        {
            aliveThisRound.Add(brain);
        }
    }

    private Vector3 PickSpawn(int zone, bool preferElevated)
    {
        if (preferElevated == true && elevatedSpawns.Count > 0 && zone != ZoneApproach)
        {
            Transform high = elevatedSpawns[Random.Range(0, elevatedSpawns.Count)];
            if (high != null)
            {
                return high.position;
            }
        }

        List<Transform> pool = approachSpawns;
        if (zone == ZoneNarrows)
        {
            pool = narrowsSpawns;
        }
        else if (zone == ZoneHollow)
        {
            pool = hollowSpawns;
        }
        else if (zone == ZoneWholeValley)
        {
            // Spread across everything, so the player is pressured from both ends.
            int roll = Random.Range(0, 3);
            if (roll == 0)
            {
                pool = approachSpawns;
            }
            else if (roll == 1)
            {
                pool = narrowsSpawns;
            }
            else
            {
                pool = hollowSpawns;
            }
        }

        if (pool == null || pool.Count == 0)
        {
            return new Vector3(0f, 2f, 0f);
        }

        Transform chosen = pool[Random.Range(0, pool.Count)];
        if (chosen == null)
        {
            return new Vector3(0f, 2f, 0f);
        }
        return chosen.position;
    }

    public static Vector3 StartingPointForZone(int zone)
    {
        if (zone == ZoneVault)
        {
            return ValleyBuilder.BossArenaOrigin + new Vector3(0f, 2.5f, -18f);
        }
        if (zone == ZoneNarrows)
        {
            return new Vector3(0f, 2f, -14f);
        }
        if (zone == ZoneHollow)
        {
            return new Vector3(0f, 2f, 12f);
        }
        if (zone == ZoneWholeValley)
        {
            return new Vector3(0f, 2f, -20f);
        }
        return new Vector3(0f, 2f, -32f);
    }

    // ------------------------------------------------------------------------
    // Read by the display
    // ------------------------------------------------------------------------

    public string CurrentRoundName()
    {
        EnsureRoundsBuilt();
        if (currentRound < 1 || currentRound > rounds.Length)
        {
            return "";
        }
        return rounds[currentRound - 1].roundName;
    }

    public int TotalRounds()
    {
        EnsureRoundsBuilt();
        return rounds.Length;
    }

    public int EnemiesRemaining()
    {
        return CountTheLiving();
    }

    public bool ShowingBanner()
    {
        return phase == PhaseBanner;
    }

    public bool InIntermission()
    {
        return phase == PhaseIntermission;
    }

    public bool WaitingToStart()
    {
        return phase == PhaseWaitingToStart;
    }

    public float SecondsLeftInPhase()
    {
        return phaseSecondsRemaining;
    }

    public bool IsBossRound()
    {
        EnsureRoundsBuilt();
        if (currentRound < 1 || currentRound > rounds.Length)
        {
            return false;
        }
        return rounds[currentRound - 1].theWarden;
    }
}
