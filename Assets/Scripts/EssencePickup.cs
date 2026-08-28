using UnityEngine;

// The glowing shard an enemy leaves behind. Walk over it to collect it.
// It builds itself in code rather than coming from a prefab, so there is nothing to
// wire up in the editor and nothing to accidentally unlink.
public class EssencePickup : MonoBehaviour
{
    public int essenceWorth = 1;
    public float spinDegreesPerSecond = 120f;
    public float bobHeight = 0.18f;
    public float bobSpeed = 2.5f;

    private Vector3 restingPosition;
    private float secondsAlive = 0f;

    void Start()
    {
        restingPosition = transform.position;
    }

    void Update()
    {
        secondsAlive = secondsAlive + Time.deltaTime;

        // Spinning and bobbing is what makes a plain cube read as "pick me up" without
        // any art at all.
        transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);
        float bobOffset = Mathf.Sin(secondsAlive * bobSpeed) * bobHeight;
        transform.position = restingPosition + Vector3.up * bobOffset;
    }

    void OnTriggerEnter(Collider whoTouchedIt)
    {
        if (whoTouchedIt.CompareTag("Player") == false)
        {
            return;
        }

        GameSound.Play("EssencePickup", 0.5f);

        if (GameDirector.instance != null)
        {
            GameDirector.instance.CollectEssence(essenceWorth);
        }
        Destroy(gameObject);
    }

    // Builds a shard from scratch at a position. Called by the director when an enemy
    // dies, so nothing has to exist in the scene ahead of time.
    public static void SpawnAt(Vector3 wherePosition, int howMuchItIsWorth)
    {
        GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = "EssenceShard";
        shard.transform.position = wherePosition;
        shard.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        shard.transform.Rotate(35f, 0f, 35f);

        // The collider becomes a trigger so the player walks through it rather than
        // bumping into it like a wall.
        Collider shardCollider = shard.GetComponent<Collider>();
        shardCollider.isTrigger = true;

        Renderer shardRenderer = shard.GetComponent<Renderer>();
        Material glowingMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        glowingMaterial.color = new Color(0.55f, 0.95f, 0.85f);
        glowingMaterial.EnableKeyword("_EMISSION");
        glowingMaterial.SetColor("_EmissionColor", new Color(0.3f, 1f, 0.85f) * 2.2f);
        shardRenderer.material = glowingMaterial;

        EssencePickup pickupBehaviour = shard.AddComponent<EssencePickup>();
        pickupBehaviour.essenceWorth = howMuchItIsWorth;
    }
}
