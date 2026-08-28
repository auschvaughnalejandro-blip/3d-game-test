using UnityEngine;

// The scatter of shards an enemy breaks into when it dies.
//
// This is deliberately not a Unity particle system. Particles would need an asset set up
// in the editor and, more importantly, would not change appearance when the visual style
// changes. Real objects made of the same primitives as everything else stay consistent
// with whatever lens is currently active.
public class DeathBurst : MonoBehaviour
{
    private Vector3 flightVelocity;
    private Vector3 spinAxis;
    private float spinSpeed;
    private float secondsLeftToLive;
    private float totalLifetimeSeconds;
    private Vector3 startingScale;

    private const float GravityOnShards = 14f;

    // How many pieces an enemy breaks into. Enough to read as a burst, few enough that
    // killing a room full of enemies does not stutter.
    private const int ShardsPerDeath = 14;

    public static void SpawnAt(Vector3 where, Color colour, float sizeOfTheThingThatDied)
    {
        int shardIndex = 0;
        while (shardIndex < ShardsPerDeath)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "DeathShard";

            // Colliders are removed so a spray of debris cannot shove the player around
            // or trip the sword's hit detection.
            Object.DestroyImmediate(shard.GetComponent<BoxCollider>());

            float shardSize = Random.Range(0.12f, 0.30f) * sizeOfTheThingThatDied;
            shard.transform.position = where + Random.insideUnitSphere * 0.5f * sizeOfTheThingThatDied;
            shard.transform.localScale = new Vector3(shardSize, shardSize, shardSize);
            shard.transform.rotation = Random.rotation;

            Material shardMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            shardMaterial.color = colour;
            shard.GetComponent<Renderer>().material = shardMaterial;

            DeathBurst burstBehaviour = shard.AddComponent<DeathBurst>();
            burstBehaviour.Launch(sizeOfTheThingThatDied);

            shardIndex = shardIndex + 1;
        }
    }

    private void Launch(float sizeOfTheThingThatDied)
    {
        // Mostly outward and upward, so the burst reads as an explosion rather than a
        // pile of dropped blocks.
        Vector3 outward = Random.insideUnitSphere.normalized;
        outward.y = Mathf.Abs(outward.y) * 0.8f + 0.5f;

        flightVelocity = outward * Random.Range(3.5f, 7.5f) * sizeOfTheThingThatDied;
        spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(180f, 620f);

        totalLifetimeSeconds = Random.Range(0.7f, 1.1f);
        secondsLeftToLive = totalLifetimeSeconds;
        startingScale = transform.localScale;
    }

    void Update()
    {
        secondsLeftToLive = secondsLeftToLive - Time.deltaTime;
        if (secondsLeftToLive <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        flightVelocity.y = flightVelocity.y - GravityOnShards * Time.deltaTime;
        transform.position = transform.position + flightVelocity * Time.deltaTime;
        transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);

        // Shrinking away is what lets a shard disappear without a hard pop.
        float fractionOfLifeLeft = secondsLeftToLive / totalLifetimeSeconds;
        transform.localScale = startingScale * fractionOfLifeLeft;
    }
}
