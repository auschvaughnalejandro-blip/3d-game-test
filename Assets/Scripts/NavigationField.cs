using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

// Builds the walkable map the enemies path across.
//
// The valley is assembled entirely from code, so there is no navigation mesh sitting in
// the scene file to load - it has to be worked out from the world once that world exists.
// This does that once, early, and then leaves it alone.
//
// What it produces is the convex decomposition of the free space described in
// PATHFINDING_MATHS.md section 5 (in the RPG Game docs folder): the ground carved into
// triangles that contain no
// obstacle, so an enemy crossing any single triangle can move in a straight line safely
// and the only real question left is which triangles to cross, in what order.
//
// Moving obstacles - the pillars that rise between rounds and the barriers that seal the
// zones - are NOT baked in here. They cut their own holes as they move, which is what
// NavMeshObstacle carving does.
public class NavigationField : MonoBehaviour
{
    // Asked by anything that wants to know whether pathfinding can be trusted yet.
    public static bool IsReady = false;

    private NavMeshSurface surface;

    void Awake()
    {
        // Statics do not survive the domain reload when play mode starts, but they DO
        // survive a scene rebuild in the editor, so this is reset explicitly rather than
        // trusted to be false.
        IsReady = false;
    }

    private bool haveBuilt = false;

    // Built on the SECOND frame, not in Start.
    //
    // The round system sinks every pillar and barrier out of sight in its own Start, and
    // the order two Start methods run in is not defined. Baking first would capture the
    // pillars while they were still standing and wall the arena off with obstacles that
    // are no longer there - permanently, because a bake happens once. Waiting one frame
    // guarantees every Start has already run.
    void Update()
    {
        if (haveBuilt == true)
        {
            return;
        }

        haveBuilt = true;
        Build();
    }

    public void Build()
    {
        surface = GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = gameObject.AddComponent<NavMeshSurface>();
        }

        surface.collectObjects = CollectObjects.All;

        // Colliders rather than renderers. Half the scenery here is decoration with no
        // collider at all - flames, crystal seams, the portal surface - and baking those
        // would wall the arena off with things the player can walk straight through.
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;

        // A character controller is a collider, so the player standing in the valley while
        // this runs would be baked in as a permanent pillar of rock. They are switched off
        // for the single frame the bake takes and switched back on immediately.
        CharacterController[] creatures =
            Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None);

        int index = 0;
        while (index < creatures.Length)
        {
            creatures[index].enabled = false;
            index = index + 1;
        }

        surface.BuildNavMesh();

        index = 0;
        while (index < creatures.Length)
        {
            creatures[index].enabled = true;
            index = index + 1;
        }

        NavMeshTriangulation map = NavMesh.CalculateTriangulation();
        int triangleCount = map.indices.Length / 3;

        if (triangleCount == 0)
        {
            Debug.LogError("The navigation mesh came out empty, so every enemy will fall "
                + "back to walking straight at the player and through the scenery. Check "
                + "that the ground has a collider.");
            IsReady = false;
            return;
        }

        IsReady = true;
        Debug.Log("Navigation mesh built: " + triangleCount + " triangles covering "
            + map.vertices.Length + " vertices.");
    }

    // The nearest point an agent can actually stand on.
    //
    // Spawning is done against the terrain collider, which is not quite the same surface -
    // the navigation mesh is inset by the agent radius and clipped around obstacles, so a
    // position that is legitimately on the ground can still be off the mesh. An agent
    // dropped off the mesh silently refuses to path at all.
    public static bool TryFindNearbyPoint(Vector3 near, float searchRadius, out Vector3 onTheMesh)
    {
        onTheMesh = near;

        if (IsReady == false)
        {
            return false;
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(near, out hit, searchRadius, NavMesh.AllAreas) == false)
        {
            return false;
        }

        onTheMesh = hit.position;
        return true;
    }
}
