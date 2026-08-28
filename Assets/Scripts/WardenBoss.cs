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

    [Header("Charge")]
    public float chargeSpeed = 15f;
    public float chargeSeconds = 1.1f;
    public float chargeWindUpSeconds = 0.9f;
    public float secondsBetweenCharges = 9f;
    public float chargeDamage = 30f;

    [Header("Volley - phase two")]
    public float secondsBetweenVolleys = 7f;
    public int rocksPerVolley = 3;
    public float volleySpreadDegrees = 22f;
    public float volleyRockSpeed = 16f;
    public float volleyRockDamage = 18f;

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

    // Shockwave state.
    private bool shockwaveRunning = false;
    private float shockwaveRadius = 0f;
    private bool shockwaveHasHitPlayer = false;
    private Transform shockwaveRing;

    // A beat of stillness at every phase change, so the player can see the rules changed.
    private float phaseFlourishLeft = 0f;

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
        BuildShockwaveRing();
    }

    void Update()
    {
        if (ownStats == null || ownStats.isDead == true || thePlayer == null)
        {
            HideShockwaveRing();
            return;
        }

        WatchForPhaseChange();

        if (phaseFlourishLeft > 0f)
        {
            phaseFlourishLeft = phaseFlourishLeft - Time.deltaTime;
            return;
        }

        TickCooldowns();

        // A charge overrides everything else while it is running.
        if (isCharging == true || chargeWindUpLeft > 0f)
        {
            ContinueCharging();
            return;
        }

        if (shockwaveRunning == true)
        {
            ContinueShockwave();
        }

        DecideWhatToUse();
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

        if (shouldBe == phase)
        {
            return;
        }

        phase = shouldBe;
        EnterPhase(phase);
    }

    private void EnterPhase(int newPhase)
    {
        GameSound.PlayAt("WardenSlam", transform.position, 1f);

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
        }
        else if (newPhase == 3)
        {
            brain.moveSpeed = brain.moveSpeed * 1.15f;
            brain.secondsBetweenAttacks = brain.secondsBetweenAttacks * 0.85f;
            summonCooldown = 3f;
            shockwaveCooldown = 5f;
        }
    }

    private void TickCooldowns()
    {
        chargeCooldown = chargeCooldown - Time.deltaTime;
        volleyCooldown = volleyCooldown - Time.deltaTime;
        summonCooldown = summonCooldown - Time.deltaTime;
        shockwaveCooldown = shockwaveCooldown - Time.deltaTime;
    }

    private void DecideWhatToUse()
    {
        float distance = Vector3.Distance(transform.position, thePlayer.position);

        // Charging is for when the player has backed off. Up close the ordinary slam is
        // the right answer, and the brain handles that on its own.
        if (chargeCooldown <= 0f && distance > 9f && distance < 34f)
        {
            BeginCharge();
            return;
        }

        if (phase >= 2 && volleyCooldown <= 0f && distance > 6f)
        {
            ThrowAVolley();
            volleyCooldown = secondsBetweenVolleys;
            return;
        }

        if (phase >= 3 && summonCooldown <= 0f)
        {
            SummonHelp();
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

            if (chargeWindUpLeft <= 0f)
            {
                isCharging = true;
                chargeSecondsLeft = chargeSeconds;
            }
            return;
        }

        // Played on the first frame of the run, not the wind-up, so the sound lands
        // with the movement rather than ahead of it.
        if (chargeSecondsLeft + Time.deltaTime >= chargeSeconds)
        {
            GameSound.PlayAt("WardenCharge", transform.position, 0.9f);
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
    // The volley
    // ------------------------------------------------------------------------

    private void ThrowAVolley()
    {
        GameSound.PlayAt("RockThrow", transform.position, 0.8f);

        Vector3 from = transform.position + Vector3.up * 1.4f;
        Vector3 aimAt = thePlayer.position + Vector3.up * 0.6f;
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
        GameSound.PlayAt("WardenSlam", transform.position, 1f);
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
