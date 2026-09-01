using UnityEngine;

// The Warden, as a boss rather than a large Grunt.
//
// This rides alongside EnemyBrain rather than replacing it. The ordinary brain keeps
// doing what it does for every creature - chase, telegraph, slam - and this switches
// extra capabilities on as the Warden's health falls. Three phases, each one adding a
// pressure the previous phase did not have:
//
//   1. It CHASES and slams. You cannot stand still.
//   2. It THROWS. You cannot stand in the open, so cover starts to matter.
//   3. It SUMMONS and SHOCKWAVES. Cover is mostly rubble by now, and the answer stops
//      being "hide" and becomes "jump".
//
// The cruelty of the design is in the middle: phase two demands cover, and the charge
// from phase one destroys cover, so the arena grows more dangerous the longer you take.
public class WardenBoss : MonoBehaviour
{
    [Header("Phases, as fractions of full health")]
    public float phaseTwoAt = 0.66f;
    public float phaseThreeAt = 0.33f;
    // The last stand. Everything sharpens and the fight gets a deadline.
    public float enrageAt = 0.15f;

    [Header("Recovering when nobody is fighting it")]
    // How long the Warden has to go untouched before it starts healing.
    //
    // This closes the last way of winning without engaging. Hiding behind a pillar, or
    // circling at a distance without shooting, used to cost the player nothing at all -
    // the fight simply paused and waited for them. Now standing off loses ground, and
    // the Warden is the only thing in the room that benefits from a long silence.
    //
    // Eight seconds is deliberately longer than any of its own attack cycles, so a player
    // who is genuinely fighting and merely dodging through a volley never triggers it.
    //
    // This was four seconds at twelve health a second, and that combination made the
    // fight unwinnable rather than merely hard. An arrow through the armour does about
    // ten; the quiver runs dry after twenty of them and takes a minute to come back. So
    // the Warden regained health faster than the bow could take it off, and every dry
    // spell handed back more than the full quiver had removed. The player was doing the
    // fight correctly and watching the health bar refill anyway.
    public float secondsOfPeaceBeforeItHeals = 8f;
    public float healthRegainedPerSecond = 5f;

    // The most it can EVER claw back above the worst it has been brought to.
    //
    // This is the rule that makes the fight finishable no matter how badly it goes. Every
    // point of damage past its previous low is permanent, so progress only ever moves one
    // way and a bad minute costs at most this much of it. Recovery is meant to punish
    // standing around, not to reset the fight - and without a floor those are the same
    // thing whenever the player's damage happens to be lower than the regeneration.
    public float mostItCanEverHealBack = 50f;

    [Header("Armour")]
    // What an arrow does when the Warden is NOT committed to a move of its own. Low
    // enough that shooting a closed-up Warden is plainly the wrong thing to be doing,
    // high enough that a shot taken at the wrong moment is a waste rather than an
    // insult.
    //
    // Raised from 0.3 after play. At a third, an arrow outside the window did about ten
    // damage against a boss that was regaining twelve a second, so shooting at the wrong
    // moment was not merely wasteful - it lost ground. Just under a half keeps the timing
    // lesson (a window shot is still worth more than two outside one) without making the
    // wrong choice actively negative.
    public float arrowDamageThroughTheArmour = 0.45f;

    [Header("Charge")]
    public float chargeSpeed = 15f;
    public float chargeSeconds = 1.1f;
    // A charge no longer runs for a fixed time. 1.1 seconds carries 16.5 metres, and the
    // Vault is 48 metres across - so a player standing at the far wall could watch the
    // charge stop a long way short and simply walk away from whatever came next. The run
    // is now as long as the distance needs, up to this.
    public float longestChargeSeconds = 2.4f;
    public float chargeWindUpSeconds = 0.9f;
    public float secondsBetweenCharges = 9f;
    public float chargeDamage = 30f;

    [Header("Leap - the answer to a player who simply backs away")]
    // The move the fight was missing. Every other thing the Warden does needs it to be
    // near the player already: the slam needs five metres, the charge covers a fixed
    // line, and the volley waited until phase two. A player who walked backwards faster
    // than 1.9 m/s - which is every player - was in no danger at all from anything.
    //
    // The leap arrives wherever they are. Backing away now buys a wind-up rather than
    // safety, and the answer becomes moving LATE rather than moving FAR.
    public float secondsBetweenLeaps = 13f;
    public float leapWindUpSeconds = 1.5f;
    public float leapTravelSeconds = 0.65f;
    public float leapArcHeight = 7f;

    // How far through the wind-up the landing spot stops following the player.
    //
    // Without this the leap was arithmetically impossible to escape, and that is not an
    // exaggeration - it was worth working out. The marker tracked the player every frame
    // right up to the final one, so the ring committed to exactly where they stood at the
    // instant the Warden left the ground. That left only the 0.65 second flight to clear
    // a six metre circle: 9.2 metres a second. Walking is 5.5, sprinting 8.5, and a whole
    // dodge roll covers 5.6 metres - so nothing the player could do got them out, and the
    // only honest description of the move was an unavoidable 34 damage every thirteen
    // seconds.
    //
    // Locking the aim halfway gives them the REST of the wind-up plus the flight, and the
    // ring stops moving while they can still see it. That is what makes "move late" a
    // real instruction rather than a thing the comments claimed.
    public float leapAimLocksAtFraction = 0.5f;

    // Three and a half metres against 1.4 seconds of warning is 2.5 metres a second to
    // step clear - comfortable at a walk, trivial with a roll, and still a hit for anyone
    // who stands and watches it happen.
    public float leapLandingRadius = 3.5f;
    public float leapDamage = 34f;
    public float leapMaximumDistance = 30f;
    // Closer than this the charge and the slam are the right answers, and a leap would
    // only be a worse version of both.
    public float leapUsedBeyondDistance = 12f;
    // How long the floor burns where it came down. Phase two onwards only.
    public float scorchLastsSeconds = 6f;

    [Header("Volley - phase two")]
    public float secondsBetweenVolleys = 7f;
    public int rocksPerVolley = 3;
    public float volleySpreadDegrees = 22f;
    public float volleyRockSpeed = 16f;
    public float volleyRockDamage = 18f;
    // How much of the player's travel to aim ahead of. Deliberately less than all of it:
    // a full lead is unanswerable, and a player who changes direction during the throw
    // should still beat it.
    public float volleyLeadFraction = 0.8f;

    [Header("Summons - phase three")]
    public float secondsBetweenSummons = 20f;
    public int gruntsPerSummon = 2;
    public int dartersPerSummon = 2;

    [Header("Shockwave - phase three")]
    public float secondsBetweenShockwaves = 11f;
    public float shockwaveSpeed = 13f;
    public float shockwaveDamage = 26f;
    public float shockwaveMaximumRadius = 26f;
    // How high off the ground counts as having cleared it. A jump reaches about 1.1 m,
    // so this is comfortably inside that.
    public float shockwaveClearanceHeight = 0.75f;

    private EnemyBrain brain;
    private CharacterStats ownStats;
    private CharacterController bodyController;

    private Transform thePlayer;
    private CharacterStats playerStats;
    private PlayerMovement playerMovement;

    private int phase = 1;
    private bool hasEnraged = false;

    private float secondsSinceItWasLastHurt = 0f;
    private float healthItHadLastFrame = -1f;
    private float lowestHealthItHasReached = -1f;

    private float chargeCooldown = 0f;
    private float volleyCooldown = 0f;
    private float summonCooldown = 0f;
    private float shockwaveCooldown = 0f;

    // Charge state.
    private bool isCharging = false;
    private float chargeSecondsLeft = 0f;
    private float chargeWindUpLeft = 0f;
    private Vector3 chargeDirection = Vector3.zero;
    private bool chargeHasHitPlayer = false;
    private bool chargeRunHasBegun = false;

    // Leap state.
    private float leapCooldown = 0f;
    private bool isLeaping = false;
    private float leapWindUpLeft = 0f;
    private float leapTravelLeft = 0f;
    private Vector3 leapStartedAt = Vector3.zero;
    private Vector3 leapLandsAt = Vector3.zero;
    private Transform leapMarker;

    // The glowing core on the model, and how bright it is right now.
    private Material coreMaterial;
    private float coreGlowNow = 0.5f;

    // Where the player was last frame, so the volley can be aimed where they are GOING.
    //
    // Measured here rather than read off PlayerMovement because the player is moved by
    // several different things - walking, the dodge roll, being shoved - and the only
    // thing that matters for leading a throw is where they actually went.
    private Vector3 playerWasAt = Vector3.zero;
    private Vector3 playerVelocity = Vector3.zero;
    private bool havePlayerHistory = false;

    // Shockwave state.
    private bool shockwaveRunning = false;
    private float shockwaveRadius = 0f;
    private bool shockwaveHasHitPlayer = false;
    private Transform shockwaveRing;

    // A beat of stillness at every phase change, so the player can see the rules changed.
    private float phaseFlourishLeft = 0f;

    [Header("Boss tells")]
    // How long the Warden spends hauling both arms overhead before the rocks leave.
    //
    // This is a NEW gameplay timing, not just an animation length, and that is
    // deliberate. A volley used to fire on the same frame it was decided on, with no
    // tell at all - the player simply started taking damage from across the arena. The
    // wind-up is what turns it into something that can be answered by getting behind
    // cover, which is the whole point of phase two.
    //
    // Because the volley now fires when this expires, the animation and the attack run
    // off one clock and cannot drift apart.
    public float volleyWindUpSeconds = 0.7f;

    // How long the arms stay flung wide after a summon. Nothing in the game waits on
    // this - the creatures have already arrived - so it is a pose length rather than a
    // gameplay timing.
    public float summonPoseSeconds = 1.0f;

    private float volleyWindUpLeft = 0f;
    private float volleyReleaseLeft = 0f;
    private float summonPoseLeft = 0f;

    // The limb animator on the Warden's model, when it has a segmented one. Found on
    // demand for the same reason EnemyBrain does it: this component is added by
    // ValleyBuilder before the body is hung on the brain, so anything looked up in Awake
    // would be null.
    private ProceduralAnimator limbs;
    private bool haveLookedForTheLimbs = false;

    private ProceduralAnimator TheLimbs()
    {
        if (haveLookedForTheLimbs == true)
        {
            return limbs;
        }

        if (brain == null || brain.bodyTransform == null)
        {
            return null;
        }

        limbs = brain.bodyTransform.GetComponent<ProceduralAnimator>();
        haveLookedForTheLimbs = true;
        return limbs;
    }

    void Start()
    {
        brain = GetComponent<EnemyBrain>();
        ownStats = GetComponent<CharacterStats>();
        bodyController = GetComponent<CharacterController>();

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            thePlayer = playerObject.transform;
            playerStats = playerObject.GetComponent<CharacterStats>();
            playerMovement = playerObject.GetComponent<PlayerMovement>();
        }

        // A boss that gives up and wanders home is not a boss.
        if (brain != null)
        {
            brain.loseInterestRadius = 500f;
            brain.detectionRadius = 500f;
        }

        chargeCooldown = secondsBetweenCharges * 0.6f;
        leapCooldown = secondsBetweenLeaps * 0.5f;
        BuildShockwaveRing();
        BuildTheLeapMarker();
    }

    // The landing marker is not parented to the Warden - it marks a place on the ground
    // while the Warden is in the air above it - so it has to be cleaned up by hand.
    void OnDestroy()
    {
        if (leapMarker != null)
        {
            Destroy(leapMarker.gameObject);
        }
    }

    void Update()
    {
        if (ownStats == null || ownStats.isDead == true || thePlayer == null)
        {
            HideShockwaveRing();
            HideTheLeapMarker();

            // Killed in mid-air, or the player disappeared under it. Either way the body
            // has to be handed back to the brain, or it stays hanging over the arena
            // with its gravity switched off for the rest of the round.
            if (isLeaping == true)
            {
                isLeaping = false;
                if (brain != null)
                {
                    brain.SuspendOrdinaryMovement(false);
                }
            }
            return;
        }

        WatchThePlayersSpeed();
        WatchForPhaseChange();
        HealIfNobodyIsFightingIt();

        // Kept up here, above every early return below, so the glow never freezes
        // part-way through a fade because the Warden happened to be mid-leap or mid
        // phase-change on the frame it mattered.
        KeepTheCoreLookingRight();

        // A leap already in the air finishes even through a phase change. The flourish
        // returns early, and a return while airborne would leave a four metre golem
        // hanging motionless over the arena for a second and a bit - which reads as the
        // game having hung rather than as a boss changing gear.
        if (isLeaping == true)
        {
            TickCooldowns();
            ContinueLeaping();
            return;
        }

        if (phaseFlourishLeft > 0f)
        {
            phaseFlourishLeft = phaseFlourishLeft - Time.deltaTime;
            return;
        }

        TickCooldowns();
        KeepTheBossPosesRunning();

        // A charge overrides everything else while it is running.
        if (isCharging == true || chargeWindUpLeft > 0f)
        {
            ContinueCharging();
            return;
        }

        // So does a leap being wound up. The airborne half is handled further up, before
        // the phase flourish can interrupt it.
        if (leapWindUpLeft > 0f)
        {
            ContinueLeaping();
            return;
        }

        // A volley being loaded holds the Warden still, exactly as the charge does.
        if (volleyWindUpLeft > 0f || volleyReleaseLeft > 0f)
        {
            ContinueTheVolley();
            return;
        }

        if (shockwaveRunning == true)
        {
            ContinueShockwave();
        }

        DecideWhatToUse();
    }

    // Health regained while nothing is hurting it.
    //
    // Noticing that it was hit is done by watching the health NUMBER fall, which is the
    // approach PlayerHealing explicitly warns against for the player - and it is right to
    // there and wrong to here. The player's health moves for all sorts of reasons that
    // are not damage: potions, shrine upgrades, a round reset, a test. The Warden's only
    // ever moves for two, and this method is one of them, so it can simply account for
    // its own healing and treat every other fall as a hit.
    private void HealIfNobodyIsFightingIt()
    {
        if (ownStats.currentHealth < healthItHadLastFrame - 0.001f)
        {
            secondsSinceItWasLastHurt = 0f;
        }

        secondsSinceItWasLastHurt = secondsSinceItWasLastHurt + Time.deltaTime;

        // The worst it has ever been brought to. Remembered rather than recomputed,
        // because it is the whole record of the player's progress through the fight.
        if (lowestHealthItHasReached < 0f
            || ownStats.currentHealth < lowestHealthItHasReached)
        {
            lowestHealthItHasReached = ownStats.currentHealth;
        }

        // It can come back up to here and no further, so damage past the low water mark
        // can never be undone.
        float asHighAsItMayHeal = lowestHealthItHasReached + mostItCanEverHealBack;
        if (asHighAsItMayHeal > ownStats.maximumHealth)
        {
            asHighAsItMayHeal = ownStats.maximumHealth;
        }

        if (secondsSinceItWasLastHurt >= secondsOfPeaceBeforeItHeals
            && ownStats.currentHealth < asHighAsItMayHeal)
        {
            ownStats.currentHealth =
                ownStats.currentHealth + healthRegainedPerSecond * Time.deltaTime;

            if (ownStats.currentHealth > asHighAsItMayHeal)
            {
                ownStats.currentHealth = asHighAsItMayHeal;
            }
        }

        healthItHadLastFrame = ownStats.currentHealth;
    }

    // How fast the player is actually travelling across the ground, smoothed.
    //
    // Smoothed because a single stuttering frame - a dropped frame, a dodge starting,
    // the controller being shoved - produces an enormous instantaneous speed, and an
    // unsmoothed reading would throw the whole volley at a wall.
    private void WatchThePlayersSpeed()
    {
        if (Time.deltaTime <= 0f)
        {
            return;
        }

        if (havePlayerHistory == true)
        {
            Vector3 moved = thePlayer.position - playerWasAt;
            moved.y = 0f;

            Vector3 measuredNow = moved / Time.deltaTime;
            playerVelocity = Vector3.Lerp(playerVelocity, measuredNow, 6f * Time.deltaTime);
        }

        playerWasAt = thePlayer.position;
        havePlayerHistory = true;
    }

    private void WatchForPhaseChange()
    {
        float healthFraction = ownStats.currentHealth / ownStats.maximumHealth;

        int shouldBe = 1;
        if (healthFraction <= phaseThreeAt)
        {
            shouldBe = 3;
        }
        else if (healthFraction <= phaseTwoAt)
        {
            shouldBe = 2;
        }

        // The last stand sits underneath the phases rather than being a fourth one. It
        // changes no rules and adds no moves - it only sharpens what is already there,
        // so there is nothing new for the player to read at the worst possible moment.
        if (hasEnraged == false && healthFraction <= enrageAt)
        {
            Enrage();
        }

        // Phases only ever go FORWARDS.
        //
        // This became load-bearing the moment the Warden could heal. Every phase applies
        // its changes by multiplying - moveSpeed by 1.25, the attack gap by 0.85 - so a
        // boss that healed from just under a threshold to just over it and back would
        // enter the same phase twice and multiply twice. A player who kept letting it
        // recover would be teaching it to move at a speed nothing in the game was built
        // to survive, and it would read as the boss being wildly broken rather than as
        // an arithmetic mistake here.
        if (shouldBe <= phase)
        {
            return;
        }

        phase = shouldBe;
        EnterPhase(phase);
    }

    private void Enrage()
    {
        hasEnraged = true;

        // Its own sound, deeper and longer than a phase change. These two moments used
        // to share one clip, which meant the fight's single most important announcement
        // - nothing shifts it any more, and it is now faster than you - was indistinct
        // from the ordinary rules change that came before it.
        GameSound.PlayAt("WardenEnrage", transform.position, 1f);
        DeathBurst.SpawnAt(transform.position + Vector3.up * 2f,
            new Color(1f, 0.3f, 0.25f), 2.4f);
        phaseFlourishLeft = 1.2f;

        // Every clock halves.
        secondsBetweenCharges = secondsBetweenCharges * 0.5f;
        secondsBetweenVolleys = secondsBetweenVolleys * 0.5f;
        secondsBetweenLeaps = secondsBetweenLeaps * 0.5f;
        secondsBetweenShockwaves = secondsBetweenShockwaves * 0.5f;

        if (brain != null)
        {
            // Fast enough to run a walking player down. Up to here the Warden has been
            // slower than the player at every phase, so disengaging was always available
            // as a way of resetting the fight for free. It is not any more, and that is
            // the whole of what the last stand is for: the fight now has to END.
            brain.moveSpeed = 5.6f;
            brain.secondsBetweenAttacks = brain.secondsBetweenAttacks * 0.7f;

            // And nothing shifts it at all. A dying boss that can still be nudged around
            // by chip damage invites exactly the shove-lock the bow used to manage.
            brain.knockbackTaken = 0f;
        }

        // It stops recovering, too. Waiting it out was already a poor plan; at this point
        // there is nothing left to wait for.
        healthRegainedPerSecond = 0f;
    }

    private void EnterPhase(int newPhase)
    {
        GameSound.PlayAt("WardenPhase", transform.position, 1f);

        // A visible beat where it stops and flares, so the change of rules is announced
        // rather than simply happening.
        phaseFlourishLeft = 1.2f;

        DeathBurst.SpawnAt(transform.position + Vector3.up * 2f,
            new Color(0.75f, 0.35f, 1f), 1.6f);

        if (newPhase == 2)
        {
            // Angrier and faster once the rocks start flying.
            brain.moveSpeed = brain.moveSpeed * 1.25f;
            brain.secondsBetweenAttacks = brain.secondsBetweenAttacks * 0.85f;
            volleyCooldown = 2f;

            // And it comes across the arena more often. The leap is what makes distance
            // cost something, so distance should cost more as the fight goes on.
            secondsBetweenLeaps = secondsBetweenLeaps * 0.8f;
        }
        else if (newPhase == 3)
        {
            brain.moveSpeed = brain.moveSpeed * 1.15f;
            brain.secondsBetweenAttacks = brain.secondsBetweenAttacks * 0.85f;
            summonCooldown = 3f;
            shockwaveCooldown = 5f;

            secondsBetweenLeaps = secondsBetweenLeaps * 0.75f;

            // The tell shortens too, so the answer stops being comfortable - but only a
            // little. The wind-up is now half of the player's escape window rather than
            // decoration, so cutting it hard cuts their ability to leave the ring, and
            // this is the phase where they can least afford that.
            leapWindUpSeconds = leapWindUpSeconds * 0.9f;
        }
    }

    private void TickCooldowns()
    {
        chargeCooldown = chargeCooldown - Time.deltaTime;
        volleyCooldown = volleyCooldown - Time.deltaTime;
        summonCooldown = summonCooldown - Time.deltaTime;
        shockwaveCooldown = shockwaveCooldown - Time.deltaTime;
        leapCooldown = leapCooldown - Time.deltaTime;
    }

    // The two poses that are held on a timer of their own rather than driven frame by
    // frame from a move that is still running.
    private void KeepTheBossPosesRunning()
    {
        ProceduralAnimator animator = TheLimbs();
        if (animator == null)
        {
            return;
        }

        if (summonPoseLeft > 0f)
        {
            summonPoseLeft = summonPoseLeft - Time.deltaTime;
            animator.ShowSummoning(1f);
        }
        else
        {
            animator.ShowSummoning(0f);
        }

        // The charge pose is switched off here rather than in ContinueCharging, because
        // ContinueCharging stops being called the moment the charge ends.
        //
        // The leap borrows the same pose for its crouch, so it has to be named here too.
        // Left out, this would reset the crouch to nothing every frame of the wind-up and
        // the leap would be rebuilding a pose that had just been wiped - which happens to
        // look right only because of the order the two run in, and would break silently
        // the first time anybody moved either call.
        if (isCharging == false && chargeWindUpLeft <= 0f
            && isLeaping == false && leapWindUpLeft <= 0f)
        {
            animator.ShowCharging(0f);
        }
    }

    private void DecideWhatToUse()
    {
        float distance = Vector3.Distance(transform.position, thePlayer.position);

        // Charging is for when the player has backed off. Up close the ordinary slam is
        // the right answer, and the brain handles that on its own.
        //
        // The upper limit used to be 34 metres, which in a 48 metre arena meant a player
        // hugging the far wall was outside every move the Warden had. There is no upper
        // limit now - the run simply lasts as long as the distance needs.
        if (chargeCooldown <= 0f && distance > 9f)
        {
            BeginCharge();
            return;
        }

        // The leap fills the gaps the charge leaves. Between the two there is now
        // something coming at almost any range, which is the whole point: standing far
        // away was free, and it should not have been.
        if (leapCooldown <= 0f && distance > leapUsedBeyondDistance)
        {
            BeginTheLeap();
            return;
        }

        if (phase >= 2 && volleyCooldown <= 0f && distance > 6f)
        {
            BeginTheVolley();
            volleyCooldown = secondsBetweenVolleys;
            return;
        }

        if (phase >= 3 && summonCooldown <= 0f)
        {
            SummonHelp();
            summonPoseLeft = summonPoseSeconds;
            summonCooldown = secondsBetweenSummons;
            return;
        }

        if (phase >= 3 && shockwaveCooldown <= 0f && shockwaveRunning == false)
        {
            BeginShockwave();
            shockwaveCooldown = secondsBetweenShockwaves;
        }
    }

    // ------------------------------------------------------------------------
    // The charge
    // ------------------------------------------------------------------------

    private void BeginCharge()
    {
        chargeWindUpLeft = chargeWindUpSeconds;
        chargeHasHitPlayer = false;
        chargeRunHasBegun = false;

        Vector3 toPlayer = thePlayer.position - transform.position;
        toPlayer.y = 0f;
        chargeDirection = toPlayer.normalized;
    }

    private void ContinueCharging()
    {
        if (chargeWindUpLeft > 0f)
        {
            chargeWindUpLeft = chargeWindUpLeft - Time.deltaTime;

            // It keeps aiming during the wind-up and stops aiming the instant it moves,
            // exactly like the Darter. Sidestepping late is the answer.
            Vector3 toPlayer = thePlayer.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                chargeDirection = toPlayer.normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(chargeDirection), 6f * Time.deltaTime);
            }

            if (brain != null && brain.bodyTransform != null)
            {
                float through = 1f - (chargeWindUpLeft / chargeWindUpSeconds);
                brain.bodyTransform.localRotation = Quaternion.Euler(18f * through, 0f, 0f);
            }

            // The root rotation above is the whole Warden tipping. This is the body
            // inside it setting itself: pitched forward, shoulders rolling, and the
            // stride slowed right down so a creature about to move at fifteen metres a
            // second does not windmill its legs.
            ProceduralAnimator windingUp = TheLimbs();
            if (windingUp != null)
            {
                windingUp.ShowCharging(1f - (chargeWindUpLeft / chargeWindUpSeconds));
            }

            if (chargeWindUpLeft <= 0f)
            {
                isCharging = true;

                // Long enough to actually arrive, decided at the moment it commits and
                // from the distance as it stands then. A fixed run made the charge a
                // move that only worked at one range.
                float distanceToCover = Vector3.Distance(transform.position, thePlayer.position);
                float secondsToCoverIt = distanceToCover / chargeSpeed;
                chargeSecondsLeft = Mathf.Clamp(secondsToCoverIt, chargeSeconds, longestChargeSeconds);
            }
            return;
        }

        // Played on the first frame of the run, not the wind-up, so the sound lands with
        // the movement rather than ahead of it.
        //
        // Tracked with a flag rather than by comparing the time left against the run
        // length, which is how it used to work. That comparison assumed every charge ran
        // for exactly chargeSeconds, and now that a long charge starts with more than
        // that on the clock it would have been true for well over a second - playing the
        // roar every frame for the whole first half of the run.
        if (chargeRunHasBegun == false)
        {
            chargeRunHasBegun = true;
            GameSound.PlayAt("WardenWindUp", transform.position, 0.9f);
        }

        chargeSecondsLeft = chargeSecondsLeft - Time.deltaTime;

        if (bodyController != null)
        {
            bodyController.Move(chargeDirection * chargeSpeed * Time.deltaTime);
        }

        if (brain != null && brain.bodyTransform != null)
        {
            brain.bodyTransform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
        }

        ProceduralAnimator running = TheLimbs();
        if (running != null)
        {
            running.ShowCharging(1f);
        }

        // Anything solid in the way gets broken. This is what strips the arena of cover
        // over the course of the fight.
        SmashThroughPillars();

        if (chargeHasHitPlayer == false)
        {
            float distance = Vector3.Distance(transform.position, thePlayer.position);
            if (distance < 3.2f)
            {
                chargeHasHitPlayer = true;
                HurtPlayer(chargeDamage);
            }
        }

        if (chargeSecondsLeft <= 0f)
        {
            isCharging = false;
            chargeCooldown = secondsBetweenCharges;
            if (brain != null && brain.bodyTransform != null)
            {
                brain.bodyTransform.localRotation = Quaternion.identity;
            }
        }
    }

    private void SmashThroughPillars()
    {
        Collider[] touching = Physics.OverlapSphere(transform.position, 2.6f);
        int index = 0;
        while (index < touching.Length)
        {
            Pillar pillar = touching[index].GetComponent<Pillar>();
            if (pillar != null && pillar.IsBroken() == false)
            {
                pillar.TakeAHit();
                // The charge stops dead on a pillar, so ramming cover costs it the rest
                // of the run rather than being free.
                chargeSecondsLeft = 0f;
            }
            index = index + 1;
        }
    }

    // ------------------------------------------------------------------------
    // The leap
    // ------------------------------------------------------------------------

    // A ring on the floor showing where the Warden is about to come down.
    //
    // Not parented to the Warden, unlike the shockwave ring. The whole point of this one
    // is that it stays on the ground marking a place while the Warden travels through
    // the air away from it, so it lives in the world and is moved by hand.
    private void BuildTheLeapMarker()
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "WardenLeapLanding";
        marker.transform.localScale =
            new Vector3(leapLandingRadius * 2f, 0.02f, leapLandingRadius * 2f);

        // A flattened cylinder primitive keeps its CAPSULE collider, which becomes a
        // huge invisible dome that throws anything standing on it into the air. This is
        // a decoration and must not collide with anything.
        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Object.DestroyImmediate(markerCollider);
        }

        Material markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        markerMaterial.color = new Color(1f, 0.35f, 0.12f);
        markerMaterial.EnableKeyword("_EMISSION");
        markerMaterial.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.08f) * 3f);
        marker.GetComponent<Renderer>().material = markerMaterial;

        leapMarker = marker.transform;
        marker.SetActive(false);
    }

    private void ShowTheLeapMarker()
    {
        if (leapMarker == null)
        {
            return;
        }

        if (leapMarker.gameObject.activeSelf == false)
        {
            leapMarker.gameObject.SetActive(true);
        }

        // Just above the floor, so the two surfaces do not fight over which is drawn.
        leapMarker.position = leapLandsAt + Vector3.up * 0.06f;
    }

    private void HideTheLeapMarker()
    {
        if (leapMarker != null && leapMarker.gameObject.activeSelf == true)
        {
            leapMarker.gameObject.SetActive(false);
        }
    }

    private void BeginTheLeap()
    {
        leapWindUpLeft = leapWindUpSeconds;
        AimTheLeapAtThePlayer();

        // The crouch, not the jump. The player's window to leave the marked circle opens
        // here, so this has to be audibly a WARNING and not an impact - a slam sound at
        // this moment tells them something has already happened and it is too late.
        GameSound.PlayAt("WardenWindUp", transform.position, 0.8f);
    }

    // Where the leap would land if it committed right now.
    //
    // Kept updating through the whole wind-up and committed on the last frame of it, so
    // the answer is to move LATE. Moving early only drags the marker along behind you,
    // which is the same rule the charge already teaches - and a boss whose moves all
    // answer to the same instinct is one a player can learn.
    private void AimTheLeapAtThePlayer()
    {
        Vector3 toPlayer = thePlayer.position - transform.position;
        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;
        if (distance > leapMaximumDistance)
        {
            toPlayer = toPlayer.normalized * leapMaximumDistance;
            distance = leapMaximumDistance;
        }

        // The Warden's own height is kept rather than the player's, so it lands on the
        // floor it took off from. Reading the player's height instead would send it into
        // the ground whenever they were stood in a dip.
        leapLandsAt = transform.position + toPlayer;

        if (distance > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(toPlayer.normalized), 6f * Time.deltaTime);
        }
    }

    // Face the committed landing spot without moving it. Used for the second half of the
    // wind-up, once the aim has locked.
    private void TurnTowardsTheLandingSpot()
    {
        Vector3 towardsIt = leapLandsAt - transform.position;
        towardsIt.y = 0f;

        if (towardsIt.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(towardsIt.normalized), 6f * Time.deltaTime);
    }

    private void ContinueLeaping()
    {
        if (leapWindUpLeft > 0f)
        {
            leapWindUpLeft = leapWindUpLeft - Time.deltaTime;

            // Tracking stops partway through, and from then on the ring is fixed on the
            // floor. Everything after that moment is time the player has to leave it.
            float howFarThroughTheWindUp = 1f - (leapWindUpLeft / leapWindUpSeconds);
            if (howFarThroughTheWindUp < leapAimLocksAtFraction)
            {
                AimTheLeapAtThePlayer();
            }
            else
            {
                // Still turning to face where it is going, but no longer choosing where
                // that is. The Warden should look committed, because it is.
                TurnTowardsTheLandingSpot();
            }

            ShowTheLeapMarker();

            // The crouch. The charge pose is the closest thing the animator has to a
            // creature gathering itself, and reusing it means the leap needs no new
            // animation at all.
            ProceduralAnimator crouching = TheLimbs();
            if (crouching != null)
            {
                crouching.ShowCharging(1f - (leapWindUpLeft / leapWindUpSeconds));
            }

            if (leapWindUpLeft <= 0f)
            {
                isLeaping = true;
                leapTravelLeft = leapTravelSeconds;
                leapStartedAt = transform.position;
                GameSound.PlayAt("WardenLeapLaunch", transform.position, 0.9f);

                // The brain stops applying gravity and stops chasing for the flight.
                // Both would otherwise be moving the same CharacterController as the arc
                // below, in the same frame, in the opposite direction.
                if (brain != null)
                {
                    brain.SuspendOrdinaryMovement(true);
                }
            }
            return;
        }

        leapTravelLeft = leapTravelLeft - Time.deltaTime;

        float howFarThrough = 1f - Mathf.Clamp01(leapTravelLeft / leapTravelSeconds);

        // A straight line across the ground with a sine arc added on top. Sine is zero at
        // both ends and one in the middle, which is exactly the shape of a jump.
        Vector3 alongTheGround = Vector3.Lerp(leapStartedAt, leapLandsAt, howFarThrough);
        float heightNow = Mathf.Sin(howFarThrough * Mathf.PI) * leapArcHeight;
        Vector3 wantsToBeAt = alongTheGround + Vector3.up * heightNow;

        // Moved by the difference rather than teleported, so walls and pillars still stop
        // it and it cannot be leapt through the outside of the Vault.
        if (bodyController != null)
        {
            bodyController.Move(wantsToBeAt - transform.position);
        }

        ProceduralAnimator flying = TheLimbs();
        if (flying != null)
        {
            flying.ShowCharging(1f);
        }

        if (leapTravelLeft <= 0f)
        {
            LandTheLeap();
        }
    }

    private void LandTheLeap()
    {
        isLeaping = false;
        leapCooldown = secondsBetweenLeaps;
        HideTheLeapMarker();

        // The body goes back to the brain before anything else, so that even if a line
        // below throws, the Warden is not left frozen in the air with gravity switched
        // off - which would be unrecoverable rather than merely wrong.
        if (brain != null)
        {
            brain.SuspendOrdinaryMovement(false);
        }

        // The heaviest sound in the game, and one of the handful allowed to briefly
        // push everything else down to make room for itself.
        GameSound.PlayAt("WardenLeapLand", transform.position, 1f);
        DeathBurst.SpawnAt(transform.position, new Color(1f, 0.5f, 0.15f), 2.2f);

        ProceduralAnimator animator = TheLimbs();
        if (animator != null)
        {
            animator.ShowCharging(0f);
            animator.ShowSlamImpact();
        }

        // Anything solid underneath is broken by the landing, exactly as the charge
        // breaks what it runs into. The arena keeps getting barer.
        SmashThroughPillars();

        // And from phase two the landing leaves the floor burning. Held back from phase
        // one deliberately: the first phase is where the player learns what the leap is,
        // and a move that both hits hard AND permanently takes ground away is too much to
        // read the first time it happens.
        if (phase >= 2)
        {
            ScorchedGround.SpawnAt(transform.position, leapLandingRadius, scorchLastsSeconds);
        }

        // No jumping this one. The shockwave is the move that rewards a jump, and giving
        // two moves the same answer would quietly make the pair of them one move.
        Vector3 flatToPlayer = thePlayer.position - transform.position;
        flatToPlayer.y = 0f;
        if (flatToPlayer.magnitude <= leapLandingRadius)
        {
            HurtPlayer(leapDamage);
        }
    }

    // ------------------------------------------------------------------------
    // The volley
    // ------------------------------------------------------------------------

    // Both arms overhead over a long wind-up, then a two-handed hurl. Longer than the
    // Grunt's tell on purpose - a boss telegraphs harder, because being hit by something
    // the player never saw coming reads as unfair rather than as difficult.
    private void BeginTheVolley()
    {
        volleyWindUpLeft = volleyWindUpSeconds;
        volleyReleaseLeft = 0f;

        // Aim on the spot rather than tracking through the wind-up, so stepping behind a
        // pillar during the tell actually works.
        Vector3 toPlayer = thePlayer.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
        }

        ProceduralAnimator animator = TheLimbs();
        if (animator != null)
        {
            animator.UseBothArmsForTheNextAttack(true);
        }
    }

    private void ContinueTheVolley()
    {
        ProceduralAnimator animator = TheLimbs();

        if (volleyWindUpLeft > 0f)
        {
            volleyWindUpLeft = volleyWindUpLeft - Time.deltaTime;

            if (animator != null && volleyWindUpSeconds > 0f)
            {
                animator.ShowWindUp(1f - (volleyWindUpLeft / volleyWindUpSeconds));
            }

            if (volleyWindUpLeft <= 0f)
            {
                // The rocks leave at the top of the hurl, not at the start of it.
                ThrowAVolley();

                // The release is short and fixed: it is the arms coming down, and the
                // rocks are already gone, so nothing waits on it.
                volleyReleaseLeft = 0.25f;
            }
            return;
        }

        volleyReleaseLeft = volleyReleaseLeft - Time.deltaTime;

        if (animator != null)
        {
            animator.ShowStrike(1f - Mathf.Clamp01(volleyReleaseLeft / 0.25f));
        }

        if (volleyReleaseLeft <= 0f)
        {
            volleyReleaseLeft = 0f;
            if (animator != null)
            {
                animator.ClearAttack();
                animator.UseBothArmsForTheNextAttack(false);
            }
        }
    }

    private void ThrowAVolley()
    {
        // The same throw the Spitter uses, dropped a fourth. A four metre golem heaving
        // a boulder and a hunched thrower lobbing a stone should not be the same pitch,
        // and one number is a cheaper answer than another recording.
        GameSound.PlayAt("RockThrow", transform.position, 0.8f, 0.75f);

        Vector3 from = transform.position + Vector3.up * 1.4f;

        // Aimed where the player is GOING, not where they are.
        //
        // The rocks travel at 16 metres a second, so from across the arena they take
        // well over a second to arrive - and a player walking at 5.5 has moved seven
        // metres by then. The fan was thrown at a place they had already left, every
        // time, which is why the volley read as decoration rather than as an attack.
        float roughFlightSeconds =
            Vector3.Distance(from, thePlayer.position) / volleyRockSpeed;
        Vector3 leadBy = playerVelocity * roughFlightSeconds * volleyLeadFraction;

        Vector3 aimAt = thePlayer.position + Vector3.up * 0.6f + leadBy;
        Vector3 straightAt = (aimAt - from).normalized;

        // A fan rather than a single rock, so sidestepping alone is not enough and the
        // player has to actually get behind something.
        int rockIndex = 0;
        while (rockIndex < rocksPerVolley)
        {
            float spread = 0f;
            if (rocksPerVolley > 1)
            {
                spread = -volleySpreadDegrees
                    + (rockIndex / (float)(rocksPerVolley - 1)) * volleySpreadDegrees * 2f;
            }

            Vector3 direction = Quaternion.Euler(0f, spread, 0f) * straightAt;
            Projectile.Fire(from, direction, volleyRockSpeed, volleyRockDamage, gameObject);
            rockIndex = rockIndex + 1;
        }
    }

    // ------------------------------------------------------------------------
    // Summoning
    // ------------------------------------------------------------------------

    private void SummonHelp()
    {
        // The summon was completely silent. Creatures simply appeared, which in phase
        // three - where the player is already handling a charge, a volley and a leap -
        // reads as the arena spawning things at random rather than as the boss doing
        // something. This is the only Warden sound that is not physical, because what it
        // does is not a hit.
        GameSound.PlayAt("WardenSummon", transform.position, 0.9f);

        DeathBurst.SpawnAt(transform.position + Vector3.up * 2f,
            new Color(0.75f, 0.35f, 1f), 1.4f);

        int index = 0;
        while (index < gruntsPerSummon)
        {
            SummonOne("Grunt", index, gruntsPerSummon + dartersPerSummon);
            index = index + 1;
        }

        int darterIndex = 0;
        while (darterIndex < dartersPerSummon)
        {
            SummonOne("Darter", gruntsPerSummon + darterIndex, gruntsPerSummon + dartersPerSummon);
            darterIndex = darterIndex + 1;
        }
    }

    private void SummonOne(string kind, int which, int total)
    {
        // Spread around the Warden rather than on top of the player, so being summoned
        // on is never an instant unavoidable hit.
        float angle = (which / (float)total) * Mathf.PI * 2f;
        Vector3 where = transform.position
            + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 7f;
        where.y = transform.position.y;

        EnemyBrain summoned = ValleyBuilder.SpawnEnemy(kind, where);
        if (summoned != null)
        {
            // These are pressure, not food. Killing them used to build the kill streak,
            // and the streak makes the player attack nearly twice as fast - so the
            // Warden's own summons were the fastest route to the weapon that killed it.
            summoned.killingThisBuildsNoStreak = true;
        }

        if (summoned != null && RoundDirector.instance != null)
        {
            // Registered with the round, or the round would count itself finished while
            // the summons were still walking around.
            RoundDirector.instance.AddSummonedEnemy(summoned);
        }
    }

    // ------------------------------------------------------------------------
    // The shockwave
    // ------------------------------------------------------------------------

    private void BuildShockwaveRing()
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Shockwave";
        ring.transform.SetParent(transform);
        ring.transform.localPosition = new Vector3(0f, -1.7f, 0f);
        ring.transform.localScale = new Vector3(0.1f, 0.02f, 0.1f);

        Collider ringCollider = ring.GetComponent<Collider>();
        if (ringCollider != null)
        {
            Object.DestroyImmediate(ringCollider);
        }

        Material ringMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        ringMaterial.color = new Color(1f, 0.55f, 0.15f);
        ringMaterial.EnableKeyword("_EMISSION");
        ringMaterial.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f) * 3f);
        ring.GetComponent<Renderer>().material = ringMaterial;

        shockwaveRing = ring.transform;
        ring.SetActive(false);
    }

    private void BeginShockwave()
    {
        GameSound.PlayAt("WardenShockwave", transform.position, 1f);

        // Driven on impact rather than on the wind-up. The hips dropping as the ring
        // leaves is what makes the shockwave look like it came OUT of the Warden instead
        // of merely appearing near it.
        ProceduralAnimator animator = TheLimbs();
        if (animator != null)
        {
            animator.ShowSlamImpact();
        }

        shockwaveRunning = true;
        shockwaveRadius = 0f;
        shockwaveHasHitPlayer = false;

        if (shockwaveRing != null)
        {
            shockwaveRing.gameObject.SetActive(true);
        }
    }

    private void ContinueShockwave()
    {
        shockwaveRadius = shockwaveRadius + shockwaveSpeed * Time.deltaTime;

        if (shockwaveRing != null)
        {
            shockwaveRing.localScale = new Vector3(shockwaveRadius * 2f, 0.02f, shockwaveRadius * 2f);
        }

        if (shockwaveHasHitPlayer == false)
        {
            Vector3 flatToPlayer = thePlayer.position - transform.position;
            flatToPlayer.y = 0f;
            float distance = flatToPlayer.magnitude;

            // The ring is a band, not a disc: it passes THROUGH the player rather than
            // filling the arena, which is what makes the timing of a jump matter.
            if (distance > shockwaveRadius - 1.4f && distance < shockwaveRadius + 1.4f)
            {
                shockwaveHasHitPlayer = true;

                float heightAboveGround = thePlayer.position.y - transform.position.y + 1.7f;
                bool jumpedIt = playerMovement != null
                    && playerMovement.IsAirborne() == true
                    && heightAboveGround > shockwaveClearanceHeight;

                if (jumpedIt == false)
                {
                    HurtPlayer(shockwaveDamage);
                }
            }
        }

        if (shockwaveRadius >= shockwaveMaximumRadius)
        {
            shockwaveRunning = false;
            HideShockwaveRing();
        }
    }

    private void HideShockwaveRing()
    {
        if (shockwaveRing != null && shockwaveRing.gameObject.activeSelf == true)
        {
            shockwaveRing.gameObject.SetActive(false);
        }
    }

    private void HurtPlayer(float amount)
    {
        if (playerStats == null || playerStats.isDead == true)
        {
            return;
        }

        playerStats.TakeDamage(amount);
        if (playerStats.isDead == true && GameDirector.instance != null)
        {
            GameDirector.instance.OnPlayerDied();
        }
    }

    // ------------------------------------------------------------------------
    // The core, and when arrows are worth firing
    // ------------------------------------------------------------------------

    // Is the Warden committed to something right now?
    //
    // This is the answer to the last thing that made the bow the whole fight. Even with
    // the draw fixed, an archer could still stand at range and treat the Warden as a
    // target rather than as an opponent - every arrow was worth the same whenever it was
    // fired, so there was never a reason to watch what the boss was doing.
    //
    // Now there is. While the Warden is winding up, charging, in the air, or recovering,
    // its core is exposed and an arrow does its full damage. The rest of the time it is
    // a wall of stone and an arrow does very little. The skill the bow asks for stops
    // being "hold still and click" and becomes "know what it is about to do" - which is
    // the same skill the melee weapons have always asked for, and the reason the fight
    // has a rhythm at all.
    public bool CoreIsOpen()
    {
        if (chargeWindUpLeft > 0f || isCharging == true)
        {
            return true;
        }
        if (leapWindUpLeft > 0f || isLeaping == true)
        {
            return true;
        }
        if (volleyWindUpLeft > 0f || volleyReleaseLeft > 0f)
        {
            return true;
        }
        if (summonPoseLeft > 0f || shockwaveRunning == true)
        {
            return true;
        }

        // The beat at a phase change, where it stops and flares. It is already a moment
        // the game is asking the player to look at, and it should be worth looking at.
        return phaseFlourishLeft > 0f;
    }

    // What fraction of an arrow's damage actually lands, right now.
    public float HowMuchOfAnArrowLands()
    {
        if (CoreIsOpen() == true)
        {
            return 1f;
        }
        return arrowDamageThroughTheArmour;
    }

    // The core is a real object on the model, and it BRIGHTENS when it is open.
    //
    // Without this the rule is invisible and the bow reads as randomly weak - the player
    // would be told nothing, and "sometimes my arrows do nothing" is a bug report rather
    // than a mechanic. The glow is the whole of the teaching.
    private void KeepTheCoreLookingRight()
    {
        if (haveBuiltTheCore == false)
        {
            BuildTheCore();
        }

        if (coreMaterial == null)
        {
            return;
        }

        float wantedGlow = 0.5f;
        if (CoreIsOpen() == true)
        {
            wantedGlow = 7f;
        }

        // Eased rather than snapped, so it reads as something opening rather than as a
        // light being switched on.
        coreGlowNow = Mathf.Lerp(coreGlowNow, wantedGlow, 9f * Time.deltaTime);
        coreMaterial.SetColor("_EmissionColor", CoreColour * coreGlowNow);
    }

    private static readonly Color CoreColour = new Color(0.85f, 0.30f, 1f);

    // Built on demand rather than in Start, for the same reason TheLimbs is found on
    // demand: ValleyBuilder hangs the body on the brain after both scripts exist, so
    // anything that reaches for bodyTransform too early reliably finds nothing.
    private bool haveBuiltTheCore = false;

    private void BuildTheCore()
    {
        if (brain == null || brain.bodyTransform == null)
        {
            return;
        }

        haveBuiltTheCore = true;

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "WardenCore";
        core.transform.SetParent(brain.bodyTransform);

        // Chest height on a 3.65 m body whose origin is at its middle, and out in front
        // far enough to be visible from the side the player is usually on.
        core.transform.localPosition = new Vector3(0f, 0.45f, 0.30f);
        core.transform.localScale = Vector3.one * 0.5f;

        // It must not be solid. A collider here would stop arrows short of the body and
        // shove the player around whenever they walked into the Warden's chest.
        Collider strayCollider = core.GetComponent<Collider>();
        if (strayCollider != null)
        {
            Object.DestroyImmediate(strayCollider);
        }

        coreMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        coreMaterial.color = new Color(0.35f, 0.12f, 0.5f);
        coreMaterial.EnableKeyword("_EMISSION");
        coreMaterial.SetColor("_EmissionColor", CoreColour * 0.5f);
        core.GetComponent<Renderer>().material = coreMaterial;

        coreGlowNow = 0.5f;
    }

    // Read by the display for the boss bar.
    public int CurrentPhase()
    {
        return phase;
    }

    public float HealthFraction()
    {
        if (ownStats == null || ownStats.maximumHealth <= 0f)
        {
            return 0f;
        }
        return ownStats.currentHealth / ownStats.maximumHealth;
    }
}
