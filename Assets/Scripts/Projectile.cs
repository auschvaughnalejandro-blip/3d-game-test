using UnityEngine;

// A thrown rock.
//
// The important rule is the LAST one: a projectile is destroyed by anything solid it
// touches, not just by the player. That single line is what turns the stone pillars in
// the arena from scenery into cover, and it is why the Spitter changes how the valley is
// fought in rather than just adding another health bar.
//
// Built entirely in code, the same way EssencePickup builds its shard, so there is no
// prefab to wire up and nothing that can come unlinked.
public class Projectile : MonoBehaviour
{
    public float speed = 14f;
    public float damage = 14f;
    public float secondsBeforeGivingUp = 6f;

    // How close to the player counts as a hit. The projectile is small and fast, so
    // relying on collision alone would let it pass straight through on a slow frame.
    public float hitRadius = 0.7f;

    private Vector3 flightDirection = Vector3.zero;
    private float secondsAlive = 0f;
    private bool hasLanded = false;

    private Transform thePlayer;

    // Whether this rock has already announced passing the player. See the near miss
    // below - it is a one-shot, not a state.
    private bool hasWhistledPast = false;
    private CharacterStats playerStats;

    // Who fired it, so it cannot immediately shoot itself in the back.
    private GameObject whoFiredIt;

    // The thrower's displayName, remembered as TEXT at the moment of firing rather than
    // read off the thrower when the rock lands. A rock is in the air for the best part of
    // a second and the creature that threw it is very often shot dead during that second,
    // which leaves whoFiredIt pointing at a destroyed object. Copying the name at launch
    // means a Spitter's last rock still lands as a Spitter's rock.
    private string firedByName = "";

    void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            thePlayer = playerObject.transform;
            playerStats = playerObject.GetComponent<CharacterStats>();
        }
    }

    void Update()
    {
        if (hasLanded == true)
        {
            return;
        }

        secondsAlive = secondsAlive + Time.deltaTime;
        if (secondsAlive > secondsBeforeGivingUp)
        {
            Destroy(gameObject);
            return;
        }

        float travelThisFrame = speed * Time.deltaTime;
        Vector3 startedAt = transform.position;

        // Look ahead along the path rather than just moving and checking afterwards. At
        // fourteen metres a second a rock crosses most of a body between two frames, so
        // testing only the end points would let it pass through walls and players alike.
        RaycastHit whatIsInTheWay;
        bool somethingSolid = Physics.Raycast(
            startedAt,
            flightDirection,
            out whatIsInTheWay,
            travelThisFrame + hitRadius,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (somethingSolid == true && whatIsInTheWay.collider.gameObject != whoFiredIt)
        {
            transform.position = whatIsInTheWay.point;
            StopOn(whatIsInTheWay.collider.gameObject);
            return;
        }

        transform.position = startedAt + flightDirection * travelThisFrame;

        // A separate proximity check, because the player's capsule can slip past a thin
        // ray on a diagonal approach.
        if (thePlayer != null)
        {
            Vector3 toPlayer = thePlayer.position - transform.position;
            if (toPlayer.magnitude < hitRadius + 0.4f)
            {
                StopOn(thePlayer.gameObject);
                return;
            }

            // Close, but not close enough to hit. A near miss said nothing at all
            // before, so a dodge that worked and a rock that was never going to land
            // felt identical - the player got no confirmation that their sidestep was
            // what saved them.
            //
            // Announced once per rock. Without the flag this fires every frame the rock
            // spends inside the radius, which at 14 m/s is a burst of about a dozen.
            if (hasWhistledPast == false && toPlayer.magnitude < 2.6f)
            {
                hasWhistledPast = true;
                GameSound.Play("ArrowFlyBy", 0.5f);
            }
        }

        // Spin, purely so it reads as a tumbling rock rather than a sliding ball.
        transform.Rotate(37f, 61f, 23f, Space.Self);
    }

    private void StopOn(GameObject whatWasHit)
    {
        hasLanded = true;

        if (whatWasHit.CompareTag("Player") == true && playerStats != null)
        {
            playerStats.TakeDamage(damage);

            // A Spitter's rock leaves the player weakened. The Warden throws rocks out of
            // this same script during its volley, and it leaves nothing - which is handled
            // by PlayerAilments simply not naming it, rather than by a check here.
            PlayerAilments.ApplyForAttackerNamed(firedByName);

            if (playerStats.isDead == true && GameDirector.instance != null)
            {
                GameDirector.instance.OnPlayerDied();
            }
        }

        GameSound.PlayAt("RockImpact", transform.position, 0.6f);

        // Break apart wherever it stopped, so the player can see that cover actually
        // absorbed the shot rather than the rock quietly vanishing.
        DeathBurst.SpawnAt(transform.position, new Color(0.55f, 0.42f, 0.30f), 0.5f);
        Destroy(gameObject);
    }

    // Builds a rock from scratch and sends it on its way.
    public static void Fire(Vector3 from, Vector3 towards, float speed, float damage,
        GameObject firedBy)
    {
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "ThrownRock";
        rock.transform.position = from;
        rock.transform.localScale = new Vector3(0.34f, 0.30f, 0.36f);

        // No collider of its own. It finds what it hits by looking ahead, and a physical
        // collider would only let it shove characters around on its way past.
        Collider ownCollider = rock.GetComponent<Collider>();
        if (ownCollider != null)
        {
            Object.DestroyImmediate(ownCollider);
        }

        Material rockMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        rockMaterial.color = new Color(0.42f, 0.30f, 0.20f);
        rockMaterial.EnableKeyword("_EMISSION");
        rockMaterial.SetColor("_EmissionColor", new Color(0.6f, 0.25f, 0.1f) * 1.4f);
        rock.GetComponent<Renderer>().material = rockMaterial;

        Projectile flying = rock.AddComponent<Projectile>();
        flying.flightDirection = towards.normalized;
        flying.speed = speed;
        flying.damage = damage;
        flying.whoFiredIt = firedBy;

        // Read here, once, while the thrower is certainly still alive.
        if (firedBy != null)
        {
            EnemyBrain throwerBrain = firedBy.GetComponent<EnemyBrain>();
            if (throwerBrain != null)
            {
                flying.firedByName = throwerBrain.displayName;
            }
        }
    }
}
