using UnityEngine;

// An arrow, which falls.
//
// The Spitter projectile travels in a straight line because it is a lobbed rock and the
// player is meant to sidestep it. An arrow is the opposite: the player is aiming it, so
// it has to behave predictably enough to aim WITH. That means real ballistics - constant
// downward acceleration, no steering, no homing.
//
// The consequence is the thing that makes a bow interesting to use: to hit a Spitter
// standing on a ledge above you, you do not aim at it, you aim ABOVE it, and how far
// above depends on how far away it is. That is a skill the sword and hammer cannot ask
// for, because neither of them has to cross a distance.
public class Arrow : MonoBehaviour
{
    private Vector3 velocity;
    private float damage = 30f;
    private float secondsLeftToLive = 8f;

    // Deliberately gentler than the 22 the player falls under. Real arrow drop over these
    // distances would be almost nothing, and matching the player's gravity made shots
    // beyond fifteen metres need comic elevation.
    private const float Gravity = 9.8f;

    // The launch angle is solved over in PlayerCombat, and it has to solve against THIS
    // number rather than a copy of it - otherwise the arrow stops landing on the
    // crosshair the moment anybody retunes the drop.
    public static float GravityOnAnArrow()
    {
        return Gravity;
    }

    private const float HitRadius = 0.45f;

    // How long the drawn arrow is, nose to nock. Matches Arrow.fbx, and the primitive
    // fallback is built to the same length so the two are interchangeable.
    private const float ShaftLengthMetres = 0.75f;

    // The modelled arrow where there is one, and the old cylinder where there is not.
    //
    // Kept as a fallback rather than assumed, because a missing model would otherwise
    // fire an invisible arrow - and an archer whose shots cannot be seen reads as a
    // broken bow rather than as a missing file.
    private static GameObject MakeTheShaft()
    {
        GameObject modelled = Resources.Load<GameObject>("Models/Arrow");

        if (modelled != null)
        {
            return Object.Instantiate(modelled);
        }

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        // The primitive stands along its own Y and is two units tall, so half the
        // wanted length is the right scale.
        shaft.transform.localScale =
            new Vector3(0.05f, ShaftLengthMetres * 0.5f, 0.05f);
        return shaft;
    }

    public static Arrow Fire(Vector3 from, Vector3 direction, float speed, float damageDealt)
    {
        GameObject arrowObject = new GameObject("Arrow");
        arrowObject.transform.position = from;

        GameObject shaft = MakeTheShaft();
        shaft.transform.SetParent(arrowObject.transform);

        // The arrow is modelled standing along its own +Y with the nock on the origin,
        // so ninety degrees about X lays it down the arrow's forward axis. It is then
        // pushed back by half its length, because this object's position is where the
        // arrow IS for the purpose of hitting things - leaving the nock there would put
        // the whole visible shaft out in front of its own hit point.
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.transform.localPosition = new Vector3(0f, 0f, -ShaftLengthMetres * 0.5f);

        // Colliders of any kind would shove the player and every enemy around as the
        // arrow flew past. It does its own hit detection.
        Collider[] strays = shaft.GetComponentsInChildren<Collider>();
        int strayIndex = 0;
        while (strayIndex < strays.Length)
        {
            Object.Destroy(strays[strayIndex]);
            strayIndex = strayIndex + 1;
        }

        Material arrowMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        arrowMaterial.SetColor("_BaseColor", new Color(0.86f, 0.78f, 0.55f));
        arrowMaterial.EnableKeyword("_EMISSION");
        arrowMaterial.SetColor("_EmissionColor", new Color(0.5f, 0.42f, 0.2f) * 1.4f);

        Renderer[] surfaces = shaft.GetComponentsInChildren<Renderer>();
        int surfaceIndex = 0;
        while (surfaceIndex < surfaces.Length)
        {
            surfaces[surfaceIndex].material = arrowMaterial;
            surfaceIndex = surfaceIndex + 1;
        }

        Arrow arrow = arrowObject.AddComponent<Arrow>();
        arrow.velocity = direction.normalized * speed;
        arrow.damage = damageDealt;

        return arrow;
    }

    void Update()
    {
        secondsLeftToLive = secondsLeftToLive - Time.deltaTime;
        if (secondsLeftToLive <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        velocity = velocity + Vector3.down * Gravity * Time.deltaTime;

        Vector3 step = velocity * Time.deltaTime;
        float distanceThisFrame = step.magnitude;

        // Swept rather than teleported. An arrow moving at forty metres a second covers
        // most of a metre per frame, so checking only where it lands would let it pass
        // clean through anything thinner than that - which is every enemy in the game.
        if (CheckWhatItPassesThrough(distanceThisFrame) == true)
        {
            return;
        }

        transform.position = transform.position + step;

        // Point the way it is going, so the arc is visible in the arrow itself.
        if (velocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
        }
    }

    private bool CheckWhatItPassesThrough(float distanceThisFrame)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position, HitRadius, velocity.normalized, distanceThisFrame,
            ~0, QueryTriggerInteraction.Ignore);

        int index = 0;
        while (index < hits.Length)
        {
            GameObject what = hits[index].collider.gameObject;

            // Never the player who fired it.
            if (what.CompareTag("Player") == true)
            {
                index = index + 1;
                continue;
            }

            EnemyBrain enemy = what.GetComponent<EnemyBrain>();
            if (enemy != null)
            {
                CharacterStats stats = what.GetComponent<CharacterStats>();
                if (stats != null && stats.isDead == false)
                {
                    // Told that it came from range. Only the Warden does anything with
                    // that, and what it does is armour itself against arrows except
                    // while it is committed to a move of its own.
                    // The impact sound belongs to the creature that was hit, not to the
                    // arrow - it is the one that knows whether it is meat or stone, and
                    // whether that was the last hit it could take. ReceiveHitFromPlayer
                    // has already made that noise by the time this line is reached.
                    enemy.ReceiveHitFromPlayer(damage, transform.position, true);
                    Destroy(gameObject);
                    return true;
                }

                index = index + 1;
                continue;
            }

            // Anything else solid stops it dead. Scenery is what makes an arrow a
            // decision rather than a guaranteed hit - so the miss has its own small,
            // sharp sound rather than borrowing the one a thrown boulder makes.
            GameSound.PlayAt("ArrowHitStone", transform.position, 0.55f);
            Destroy(gameObject);
            return true;
        }

        return false;
    }

    // ---- Working out where a shot would land, for the crosshair ----------------------
    //
    // This lives in Arrow rather than in the HUD on purpose. It uses the SAME Gravity and
    // the SAME HitRadius as the flight above, so the marker on screen cannot quietly stop
    // agreeing with the arrow after somebody tunes one number. If the arrow changes, the
    // crosshair changes with it.

    // A fixed step rather than Time.deltaTime, so the marker sits in the same place
    // whether the game is running at thirty frames a second or two hundred. Small enough
    // that the curve is smooth, large enough that a sixty metre shot costs about forty
    // steps rather than four hundred.
    private const float PredictionStepSeconds = 0.035f;

    // An arrow still in the air after this long has gone somewhere nobody was aiming.
    private const float PredictionMaximumSeconds = 3f;

    // Where an arrow fired from here, this way, at this speed would end up.
    //
    // landsOnAnEnemy comes back true when the thing it stops against is something alive,
    // which is what lets the crosshair say "that one" rather than only "there".
    public static Vector3 PredictWhereItLands(
        Vector3 from, Vector3 direction, float speed, out bool landsOnAnEnemy)
    {
        landsOnAnEnemy = false;

        Vector3 whereItIs = from;
        Vector3 howFastItIsGoing = direction.normalized * speed;

        float secondsInTheAir = 0f;
        while (secondsInTheAir < PredictionMaximumSeconds)
        {
            // Gravity first, then the step - the same order Update uses on the real
            // arrow. Doing it the other way round drifts by half a step every step and
            // the marker ends up sitting slightly high on every long shot.
            howFastItIsGoing = howFastItIsGoing + Vector3.down * Gravity * PredictionStepSeconds;

            Vector3 step = howFastItIsGoing * PredictionStepSeconds;

            Vector3 whereItWouldStop;
            bool somethingIsInTheWay = FindWhatWouldStopIt(
                whereItIs, howFastItIsGoing, step.magnitude,
                out whereItWouldStop, out landsOnAnEnemy);

            if (somethingIsInTheWay == true)
            {
                return whereItWouldStop;
            }

            whereItIs = whereItIs + step;
            secondsInTheAir = secondsInTheAir + PredictionStepSeconds;
        }

        // Clear air the whole way. The marker goes where the arrow would be at the end of
        // its flight, which is still honest: that is where the shot is going.
        return whereItIs;
    }

    // One step of the sweep. Answers "does anything stop the arrow between here and
    // there", and if so, exactly where.
    private static bool FindWhatWouldStopIt(
        Vector3 fromHere, Vector3 travellingTowards, float howFar,
        out Vector3 wherePreciselyItStops, out bool itIsAnEnemy)
    {
        wherePreciselyItStops = fromHere;
        itIsAnEnemy = false;

        if (howFar <= 0f)
        {
            return false;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            fromHere, HitRadius, travellingTowards.normalized, howFar,
            ~0, QueryTriggerInteraction.Ignore);

        // SphereCastAll returns things in no particular order, so the nearest one has to
        // be picked out by hand. Taking whichever came back first would put the marker on
        // the rock BEHIND the enemy about half the time, which is worse than no marker.
        float distanceToTheNearest = float.MaxValue;
        bool foundSomething = false;

        int index = 0;
        while (index < hits.Length)
        {
            GameObject what = hits[index].collider.gameObject;

            bool thisOneStopsIt = false;
            bool thisOneIsAnEnemy = false;

            if (what.CompareTag("Player") == true)
            {
                // The arrow leaves from just in front of the player and ignores them on
                // the way past, so the prediction has to ignore them too. Without this
                // every shot reads as landing at the player's own feet.
                thisOneStopsIt = false;
            }
            else
            {
                EnemyBrain possibleEnemy = what.GetComponent<EnemyBrain>();
                if (possibleEnemy != null)
                {
                    // A corpse does not stop an arrow, here or in flight.
                    CharacterStats stats = what.GetComponent<CharacterStats>();
                    if (stats != null && stats.isDead == false)
                    {
                        thisOneStopsIt = true;
                        thisOneIsAnEnemy = true;
                    }
                }
                else
                {
                    // Anything else solid. Scenery is what makes an arrow a decision.
                    thisOneStopsIt = true;
                }
            }

            if (thisOneStopsIt == true && hits[index].distance < distanceToTheNearest)
            {
                distanceToTheNearest = hits[index].distance;
                itIsAnEnemy = thisOneIsAnEnemy;
                foundSomething = true;
            }

            index = index + 1;
        }

        if (foundSomething == false)
        {
            return false;
        }

        wherePreciselyItStops = fromHere + travellingTowards.normalized * distanceToTheNearest;
        return true;
    }
}
