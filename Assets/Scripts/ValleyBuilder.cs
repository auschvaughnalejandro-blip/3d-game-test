using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using Unity.AI.Navigation;

// Builds the whole of One Valley out of Unity primitives.
//
// The creatures are ASSEMBLED rather than being single capsules: a torso, a head, eyes,
// limbs and a weapon, each a separate piece. That is what lets a player tell at a glance
// which way something is facing and what it is about to do. It is still primitives, and
// it is not a substitute for real modelled characters - it is what stands in until those
// arrive.
//
// Running this twice is safe: it deletes what it built last time before building again.
public static class ValleyBuilder
{
    private const string RootObjectName = "ValleyRoot";

    private const float ValleyHalfWidth = 30f;
    // The south edge sits well behind the player start on purpose. The chase camera wants
    // seven metres of clear space behind the player, and if the wall is closer than that
    // the view starts the game buried inside rock.
    private const float ValleySouthEdge = -46f;
    private const float ValleyNorthEdge = 35f;
    private const float CliffHeight = 14f;

    public static Vector3 PlayerStartPosition = new Vector3(0f, 1.3f, -32f);

    // Collected while building, then handed to RoundDirector.
    private static List<Transform> approachSpawnPoints = new List<Transform>();
    private static List<Transform> narrowsSpawnPoints = new List<Transform>();
    private static List<Transform> hollowSpawnPoints = new List<Transform>();
    private static List<Transform> elevatedSpawnPoints = new List<Transform>();
    private static List<Pillar> narrowsPillarList = new List<Pillar>();
    private static List<Pillar> hollowPillarList = new List<Pillar>();
    private static GameObject narrowsBarrierObject;
    private static GameObject hollowBarrierObject;

    // The Vault sits far to the north of the valley, well past anything the player can
    // walk to. Keeping the two arenas in one scene rather than loading a second one
    // means the portal is a teleport rather than a loading screen.
    public static readonly Vector3 BossArenaOrigin = new Vector3(0f, 0f, 200f);
    private static List<Pillar> vaultPillarList = new List<Pillar>();
    private static GameObject portalObject;

    // Kept so the story can slide the north gate open at the end. It is an ordinary box
    // rather than a ZoneBarrier, so nothing else knows how to move it.
    private static GameObject theGateObject;

    // The way back out of the Vault, and the Orrin who waits in the valley for it.
    private static GameObject homewardPortalObject;
    private static GameObject valleyOrrinObject;

    // How wide the opening in the north cliff is. Matched to the gate that plugs it, so
    // that nothing can walk around the gate while it is shut.
    private const float NorthGapHalfWidth = 5f;

    // Shared materials, built once per rebuild so that hundreds of pieces do not each
    // create their own copy.
    private static Material eyeGlowMaterial;
    private static Material darkMetalMaterial;

    // The collider belonging to the valley floor. Height queries are aimed at this one
    // collider rather than at the whole world, because a ray fired at everything hits
    // whichever character happens to be standing there and stacks the next one on top.
    private static Collider theGroundCollider;

    // Every floor in the game that is an imported mesh rather than a solid box.
    //
    // These are the floors a body can be pushed THROUGH, so they are the floors the
    // characters hold themselves above every frame. The dungeon's floor is not in here
    // and does not need to be: it is a MakeBox cube, a genuine solid with a volume, and
    // nothing has ever gone through one of those.
    private static List<Collider> theSheetFloors = new List<Collider>();
    private static float whenTheFloorsWereLastSearchedFor = -99f;

    // Where runtime-spawned enemies are parented. Enemies are no longer placed when the
    // valley is built - they arrive round by round - so the builder keeps this so it can
    // serve those requests later.
    private static Transform enemyFolder;

    public static void BuildTheValley()
    {
        RemoveAnyPreviousValley();

        // Cleared each rebuild so a stale reference to the previous valley's floor,
        // which has just been destroyed, is never queried.
        theGroundCollider = null;
        theSheetFloors.Clear();

        eyeGlowMaterial = MakeGlowingMaterial(new Color(1f, 0.85f, 0.3f), 3f);
        darkMetalMaterial = MakeMaterial(new Color(0.13f, 0.13f, 0.15f));

        GameObject root = new GameObject(RootObjectName);

        BuildGroundAndCliffs(root);

        // The floor is sculpted now rather than flat, so everything placed after this
        // has to ask the ground how high it is. Physics only knows about colliders once
        // their transforms have been pushed across, hence the sync.
        Physics.SyncTransforms();

        BuildTheNarrows(root);
        BuildTheHollow(root);

        GameObject player = BuildThePlayer(root);
        PointTheCameraAtThePlayer(player);

        BuildTheArenaFurniture(root);
        BuildTheVault(root);
        BuildTheShrine(root);

        // Everything the story needs must exist before the story is wired up, and the
        // dungeon door has to be told where the player start is - which is only settled
        // once BuildThePlayer has stood the player on the terrain.
        DungeonBuilder.BuildTheDungeon(root);
        BuildTheHomewardPortal(root);
        BuildTheOrrinWhoWaits(root);

        BuildTheDirectorAndDisplay(root);

        // Last of all, because until now the player has been standing in the valley so
        // that the terrain could tell it how high the ground is.
        MoveThePlayerIntoTheDungeon(player);

        SetTheLighting();
    }

    // The player is built in the valley and then moved, rather than being built in the
    // dungeon in the first place. Standing a character correctly on the sculpted terrain
    // is fiddly and already works; the dungeon floor is a box at a known height and
    // needs none of it.
    private static void MoveThePlayerIntoTheDungeon(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        // The controller has to be switched off to be moved. It caches its own position
        // and will otherwise drag the player straight back.
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        player.transform.position = DungeonBuilder.DungeonOrigin + DungeonBuilder.PlayerStandsAt;
        // Facing up the room towards Orrin and the door, so the first thing on screen is
        // the thing the player is meant to walk to.
        player.transform.rotation = Quaternion.identity;

        if (controller != null)
        {
            controller.enabled = true;
        }
    }

    // The way back from the Vault, standing behind where the portal drops the player so
    // that turning round is what finds it.
    private static void BuildTheHomewardPortal(GameObject root)
    {
        GameObject holder = new GameObject("HomewardPortal");
        holder.transform.SetParent(root.transform);
        holder.transform.position = BossArenaOrigin + new Vector3(0f, 0f, -22f);

        Material frameMaterial = MakeRockMaterial(
            new Color(0.30f, 0.28f, 0.34f),
            new Color(0.18f, 0.17f, 0.21f),
            new Color(0.07f, 0.07f, 0.09f),
            1.2f, 1.0f);

        MakeBox(holder, "HomewardPostWest",
            holder.transform.position + new Vector3(-2.6f, 2.4f, 0f),
            new Vector3(0.8f, 4.8f, 1.2f), frameMaterial);
        MakeBox(holder, "HomewardPostEast",
            holder.transform.position + new Vector3(2.6f, 2.4f, 0f),
            new Vector3(0.8f, 4.8f, 1.2f), frameMaterial);
        MakeBox(holder, "HomewardHead",
            holder.transform.position + new Vector3(0f, 5.0f, 0f),
            new Vector3(6.0f, 0.8f, 1.2f), frameMaterial);

        Material surfaceMaterial = MakeGlowingMaterial(new Color(0.45f, 0.85f, 0.75f), 2.6f);
        GameObject surface = MakeBox(holder, "HomewardSurface",
            holder.transform.position + new Vector3(0f, 2.4f, 0f),
            new Vector3(4.4f, 4.8f, 0.18f), surfaceMaterial);

        Collider surfaceCollider = surface.GetComponent<Collider>();
        if (surfaceCollider != null)
        {
            Object.DestroyImmediate(surfaceCollider);
        }

        GameObject lampObject = new GameObject("HomewardLight");
        lampObject.transform.SetParent(holder.transform);
        lampObject.transform.position = holder.transform.position + new Vector3(0f, 2.8f, 0f);
        Light lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        // Green-white rather than violet. Every other gateway in this game is the
        // Vault's colour; this is the one that leads away from it.
        lamp.color = new Color(0.5f, 1f, 0.85f);
        lamp.intensity = 0f;
        lamp.range = 26f;

        Portal home = holder.AddComponent<Portal>();
        home.purpose = Portal.PurposeHomeFromTheVault;
        home.destination = new Vector3(0f, GroundHeightAt(0f, 18f) + 1.2f, 18f);
        home.activationRadius = 3.0f;
        home.SetSurface(surface.transform);
        home.SetLight(lamp);

        homewardPortalObject = holder;
    }

    // Orrin, waiting in the valley for the player to come back up. Switched off until
    // the story turns him on, so he is not standing in the middle of the arena during
    // four rounds of fighting.
    private static void BuildTheOrrinWhoWaits(GameObject root)
    {
        float groundHeight = GroundHeightAt(5f, 26f);

        valleyOrrinObject = DungeonBuilder.MakeOrrinModel(root, "OrrinInTheValley",
            new Vector3(5f, groundHeight, 26f));

        // Facing south, back down the arena, watching for the player to appear.
        valleyOrrinObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        Wizard waiting = valleyOrrinObject.AddComponent<Wizard>();
        // He speaks on his own when the player arrives. There is nothing to ask him.
        waiting.answersWhenSpokenTo = false;

        valleyOrrinObject.SetActive(false);
    }

    private static void RemoveAnyPreviousValley()
    {
        GameObject previousRoot = GameObject.Find(RootObjectName);
        while (previousRoot != null)
        {
            Object.DestroyImmediate(previousRoot);
            previousRoot = GameObject.Find(RootObjectName);
        }

        if (Camera.main != null)
        {
            OrbitCamera leftoverCameraScript = Camera.main.GetComponent<OrbitCamera>();
            if (leftoverCameraScript != null)
            {
                Object.DestroyImmediate(leftoverCameraScript);
            }
        }
    }

    // ----------------------------------------------------------------------------
    // Terrain
    // ----------------------------------------------------------------------------

    private static void BuildGroundAndCliffs(GameObject root)
    {
        // Real photographed grass-and-rock for the floor, real cliff face for the walls.
        // Both are CC0 sets from Poly Haven, projected triplanar so neither needs UVs.
        Material groundMaterial = MakeTexturedRockMaterial(
            "Textures/Terrain", "Terrain",
            0.11f, 1.1f, new Color(0.95f, 0.98f, 0.90f),
            new Color(0.38f, 0.39f, 0.28f),
            new Color(0.24f, 0.26f, 0.18f),
            new Color(0.11f, 0.12f, 0.08f));

        Material cliffMaterial = MakeTexturedRockMaterial(
            "Textures/Cliff", "Cliff",
            0.075f, 1.4f, new Color(0.92f, 0.90f, 0.88f),
            new Color(0.40f, 0.38f, 0.37f),
            new Color(0.24f, 0.23f, 0.24f),
            new Color(0.09f, 0.09f, 0.10f));

        float valleyLength = ValleyNorthEdge - ValleySouthEdge;
        float valleyMiddleZ = (ValleyNorthEdge + ValleySouthEdge) * 0.5f;

        PlaceTheTerrain(root, groundMaterial, valleyMiddleZ, valleyLength);

        MakeBox(root, "CliffWest",
            new Vector3(-ValleyHalfWidth, CliffHeight * 0.5f, valleyMiddleZ),
            new Vector3(3f, CliffHeight, valleyLength), cliffMaterial);

        MakeBox(root, "CliffEast",
            new Vector3(ValleyHalfWidth, CliffHeight * 0.5f, valleyMiddleZ),
            new Vector3(3f, CliffHeight, valleyLength), cliffMaterial);

        MakeBox(root, "CliffSouth",
            new Vector3(0f, CliffHeight * 0.5f, ValleySouthEdge),
            new Vector3(ValleyHalfWidth * 2f, CliffHeight, 3f), cliffMaterial);

        // The north cliff is built in two halves with a gap between them, because the
        // demo now ends by walking OUT of the valley rather than by a caption appearing.
        // The gap is exactly as wide as the gate that stands in front of it, so for the
        // whole of the game it is sealed and nothing can path through it.
        float halfCliffWidth = ValleyHalfWidth - NorthGapHalfWidth;

        MakeBox(root, "CliffNorthWest",
            new Vector3(-NorthGapHalfWidth - halfCliffWidth * 0.5f, CliffHeight * 0.5f, ValleyNorthEdge),
            new Vector3(halfCliffWidth, CliffHeight, 3f), cliffMaterial);

        MakeBox(root, "CliffNorthEast",
            new Vector3(NorthGapHalfWidth + halfCliffWidth * 0.5f, CliffHeight * 0.5f, ValleyNorthEdge),
            new Vector3(halfCliffWidth, CliffHeight, 3f), cliffMaterial);

        BuildTheRoadNorth(root, groundMaterial, cliffMaterial);
    }

    // The road out. Nobody fights here and nothing spawns here - it exists so that the
    // last thing the demo does is a walk rather than a fade.
    private static void BuildTheRoadNorth(GameObject root, Material groundMaterial, Material cliffMaterial)
    {
        const float RoadStartsAtZ = 34f;
        const float RoadEndsAtZ = 78f;
        const float RoadHalfWidth = 7f;

        float roadMiddleZ = (RoadStartsAtZ + RoadEndsAtZ) * 0.5f;
        float roadLength = RoadEndsAtZ - RoadStartsAtZ;

        // Laid at a fixed height rather than following the sculpted terrain, which stops
        // at the valley edge. Slightly below zero so its top face sits level with the
        // arena floor the player steps off.
        MakeBox(root, "RoadNorth",
            new Vector3(0f, -0.5f, roadMiddleZ),
            new Vector3(RoadHalfWidth * 2f, 1f, roadLength), groundMaterial);

        // Walls either side, so the road reads as a pass cut through the rock and the
        // camera has something to frame the walk against.
        MakeBox(root, "RoadWallWest",
            new Vector3(-RoadHalfWidth - 1.5f, CliffHeight * 0.5f, roadMiddleZ),
            new Vector3(3f, CliffHeight, roadLength), cliffMaterial);

        MakeBox(root, "RoadWallEast",
            new Vector3(RoadHalfWidth + 1.5f, CliffHeight * 0.5f, roadMiddleZ),
            new Vector3(3f, CliffHeight, roadLength), cliffMaterial);
    }

    // The sculpted valley floor, modelled in Blender and exported as an FBX.
    // If the model is missing for any reason this falls back to the old flat box, so a
    // broken import leaves the valley plain rather than dropping the player into space.
    private static void PlaceTheTerrain(GameObject root, Material groundMaterial,
        float valleyMiddleZ, float valleyLength)
    {
        GameObject terrainModel = Resources.Load<GameObject>("Models/ValleyTerrain");

        if (terrainModel == null)
        {
            Debug.LogWarning("ValleyTerrain model not found - using a flat ground box instead.");
            MakeBox(root, "Ground",
                new Vector3(0f, -0.5f, valleyMiddleZ),
                new Vector3(ValleyHalfWidth * 2f, 1f, valleyLength),
                groundMaterial);
            return;
        }

        GameObject terrain = Object.Instantiate(terrainModel);
        terrain.name = "Ground";
        terrain.transform.SetParent(root.transform);
        terrain.transform.position = Vector3.zero;
        terrain.transform.rotation = Quaternion.identity;

        // Blender writes FBX in centimetres, which leaves a factor of a hundred sitting
        // on the imported root transform. The mesh itself is already in metres, so the
        // scale is forced back to one rather than letting the valley come out 6.4
        // kilometres wide with the cliffs huddled at its centre.
        terrain.transform.localScale = Vector3.one;

        int childIndex = 0;
        while (childIndex < terrain.transform.childCount)
        {
            terrain.transform.GetChild(childIndex).localScale = Vector3.one;
            childIndex = childIndex + 1;
        }

        Renderer[] surfaces = terrain.GetComponentsInChildren<Renderer>();
        int surfaceIndex = 0;
        while (surfaceIndex < surfaces.Length)
        {
            surfaces[surfaceIndex].material = groundMaterial;
            surfaceIndex = surfaceIndex + 1;
        }

        // A mesh collider so the player walks on the actual sculpted surface rather
        // than on an invisible flat plane where the box used to be.
        MeshFilter[] meshParts = terrain.GetComponentsInChildren<MeshFilter>();
        int meshIndex = 0;
        while (meshIndex < meshParts.Length)
        {
            MeshCollider walkingSurface = meshParts[meshIndex].gameObject.AddComponent<MeshCollider>();
            walkingSurface.sharedMesh = meshParts[meshIndex].sharedMesh;

            // Remember the largest piece as the surface everything is measured against.
            if (theGroundCollider == null)
            {
                theGroundCollider = walkingSurface;
            }
            meshIndex = meshIndex + 1;
        }

        AddBedrockUnder(root, "ValleyBedrock", terrain);
    }

    // How high the valley floor is under a given point, found by dropping a ray onto it.
    // Every hard-coded Y value in this file became wrong the moment the ground stopped
    // being flat, so placement asks this instead of assuming zero.
    private static float GroundHeightAt(float x, float z)
    {
        // Found by looking rather than by remembering. This used to read a collider cached
        // when the scene was built and answer zero if it was missing - and the cache is a
        // static, which the domain reload on entering play mode wipes. Every runtime spawn
        // was therefore told the ground was at y=0.
        if (theGroundCollider == null)
        {
            GameObject ground = GameObject.Find("Ground");
            if (ground != null)
            {
                theGroundCollider = ground.GetComponentInChildren<MeshCollider>();
            }
        }

        if (theGroundCollider == null)
        {
            return 0f;
        }

        // Aimed at the floor's own collider rather than fired into the world at large.
        // A general raycast hits whatever is standing at that spot, so each enemy ended
        // up perched on the head of the one placed before it.
        Ray downward = new Ray(new Vector3(x, 80f, z), Vector3.down);
        RaycastHit whatWasHit;
        if (theGroundCollider.Raycast(downward, out whatWasHit, 200f) == true)
        {
            return whatWasHit.point.y;
        }
        return 0f;
    }

    // Zone two. Rock that pinches the valley and forces the player to commit to a line.
    private static void BuildTheNarrows(GameObject root)
    {
        Material rockMaterial = MakeTexturedRockMaterial(
            "Textures/Cliff", "Cliff",
            0.10f, 1.5f, new Color(1f, 0.97f, 0.93f),
            new Color(0.44f, 0.41f, 0.38f),
            new Color(0.26f, 0.24f, 0.23f),
            new Color(0.10f, 0.09f, 0.09f));

        MakeBox(root, "NarrowsWestShoulder",
            new Vector3(-19f, 3.5f, -2f), new Vector3(20f, 7f, 12f), rockMaterial);

        MakeBox(root, "NarrowsEastShoulder",
            new Vector3(19f, 3.5f, 4f), new Vector3(20f, 7f, 12f), rockMaterial);

        MakeBox(root, "Pillar1", new Vector3(-4f, 2f, -6f), new Vector3(3f, 4f, 3f), rockMaterial);
        MakeBox(root, "Pillar2", new Vector3(5f, 1.6f, 0f), new Vector3(3.5f, 3.2f, 3.5f), rockMaterial);
        MakeBox(root, "Pillar3", new Vector3(-6f, 1.3f, 6f), new Vector3(4f, 2.6f, 3f), rockMaterial);
        MakeBox(root, "Pillar4", new Vector3(2f, 2.4f, 9f), new Vector3(2.6f, 4.8f, 2.6f), rockMaterial);
    }

    // Zone three. Deliberately empty, so the Warden's warning ring is never hidden.
    private static void BuildTheHollow(GameObject root)
    {
        Material floorMaterial = MakeRockMaterial(
            new Color(0.30f, 0.26f, 0.32f),
            new Color(0.17f, 0.15f, 0.20f),
            new Color(0.07f, 0.06f, 0.09f),
            1.25f, 0.9f);

        Material gateMaterial = MakeRockMaterial(
            new Color(0.22f, 0.20f, 0.24f),
            new Color(0.12f, 0.11f, 0.14f),
            new Color(0.05f, 0.05f, 0.06f),
            1.40f, 1.2f);

        // The terrain is deliberately flattened across this arena, so the stone platform
        // is kept a little smaller than the flattened zone. Any wider and its rim would
        // start clipping through rising ground.
        float arenaGroundHeight = GroundHeightAt(0f, 22f);
        MakeCylinder(root, "HollowFloor",
            new Vector3(0f, arenaGroundHeight + 0.10f, 22f),
            new Vector3(29f, 0.16f, 29f), floorMaterial);

        float gateGroundHeight = GroundHeightAt(0f, 33f);
        theGateObject = MakeBox(root, "TheGate",
            new Vector3(0f, gateGroundHeight + 5f, 33f),
            new Vector3(10f, 10f, 1.5f), gateMaterial);
    }

    // ----------------------------------------------------------------------------
    // The player
    // ----------------------------------------------------------------------------

    private static GameObject BuildThePlayer(GameObject root)
    {
        Material clothMaterial = MakeMaterial(new Color(0.30f, 0.42f, 0.62f));
        Material skinMaterial = MakeMaterial(new Color(0.78f, 0.64f, 0.52f));
        Material bladeMaterial = MakeMaterial(new Color(0.72f, 0.76f, 0.80f));

        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.transform.SetParent(root.transform);

        // Stand the player on the terrain rather than at a guessed height. The controller
        // is centred on the middle of the body, so that is half a body height up.
        float startGroundHeight = GroundHeightAt(PlayerStartPosition.x, PlayerStartPosition.z);
        PlayerStartPosition = new Vector3(
            PlayerStartPosition.x,
            startGroundHeight + 1.77f * 0.5f,
            PlayerStartPosition.z);
        player.transform.position = PlayerStartPosition;

        // 1.77 m tall, matching the model as it came out of Blender.
        const float PlayerHeight = 1.77f;

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = PlayerHeight;
        controller.radius = 0.38f;
        controller.center = Vector3.zero;
        controller.slopeLimit = 50f;
        controller.stepOffset = 0.4f;
        TuneControllerAgainstBeingSquashedThroughTheFloor(controller);

        AttachModel(player, "Player", clothMaterial, PlayerHeight * 0.5f, 1.0f);

        // THREE weapons, all parented to the player. PlayerWeapons shows one and hides
        // the rest; the names matter, because it finds them by the prefixes "Sword",
        // "Hammer" and "Bow".
        AddPart(player, "SwordBlade", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, 0.34f), new Vector3(0.07f, 0.07f, 1.00f), bladeMaterial);
        AddPart(player, "SwordGuard", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, -0.18f), new Vector3(0.24f, 0.08f, 0.08f), darkMetalMaterial);

        // The hammer reads as heavier at a glance: a short thick haft and a big blunt
        // head, so which weapon is in hand is obvious from the silhouette alone.
        Material haftMaterial = MakeMaterial(new Color(0.34f, 0.24f, 0.16f));
        AddPart(player, "HammerHaft", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, 0.20f), new Vector3(0.09f, 0.09f, 0.86f), haftMaterial);
        AddPart(player, "HammerHead", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, 0.68f), new Vector3(0.30f, 0.30f, 0.34f), darkMetalMaterial);
        AddPart(player, "HammerCollar", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, 0.46f), new Vector3(0.15f, 0.15f, 0.10f), bladeMaterial);

        // The bow is held ACROSS the body rather than along it, so at a glance it is
        // obviously not a blade. Three staves make the curve and a pale string closes it.
        Material bowMaterial = MakeMaterial(new Color(0.40f, 0.27f, 0.14f));
        Material stringMaterial = MakeMaterial(new Color(0.86f, 0.83f, 0.72f));

        AddPart(player, "BowGrip", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, 0.30f), new Vector3(0.07f, 0.34f, 0.07f), bowMaterial);
        AddPart(player, "BowUpperLimb", PrimitiveType.Cube,
            new Vector3(0.58f, 0.24f, 0.26f), new Vector3(0.05f, 0.30f, 0.10f), bowMaterial);
        AddPart(player, "BowLowerLimb", PrimitiveType.Cube,
            new Vector3(0.58f, -0.20f, 0.26f), new Vector3(0.05f, 0.30f, 0.10f), bowMaterial);
        AddPart(player, "BowString", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, 0.20f), new Vector3(0.02f, 0.74f, 0.02f), stringMaterial);

        // The Warden's Edge. Hidden until the gem is taken - PlayerWeapons finds it by
        // the prefix "Edge" and keeps it switched off until then.
        //
        // Longer than the sword by half again, and it glows, because the one thing the
        // player must understand within a second of picking it up is that this is not
        // the weapon they walked in with.
        Material edgeMaterial = MakeGlowingMaterial(new Color(0.58f, 0.28f, 1f), 2.6f);
        Material edgeCoreMaterial = MakeGlowingMaterial(new Color(0.85f, 0.72f, 1f), 4.0f);

        AddPart(player, "EdgeBlade", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, 0.52f), new Vector3(0.10f, 0.05f, 1.55f), edgeMaterial);
        AddPart(player, "EdgeCore", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, 0.52f), new Vector3(0.035f, 0.06f, 1.50f), edgeCoreMaterial);
        AddPart(player, "EdgeGuard", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, -0.22f), new Vector3(0.40f, 0.09f, 0.10f), edgeMaterial);
        AddPart(player, "EdgeGrip", PrimitiveType.Cube,
            new Vector3(0.58f, 0.02f, -0.40f), new Vector3(0.08f, 0.08f, 0.34f), darkMetalMaterial);

        CharacterStats stats = player.AddComponent<CharacterStats>();
        stats.maximumHealth = 100f;
        stats.currentHealth = 100f;
        stats.maximumStamina = 100f;
        stats.currentStamina = 100f;
        stats.staminaRefilledPerSecond = 24f;
        stats.attackDamage = 20f;
        stats.essenceDroppedOnDeath = 0;

        player.AddComponent<PlayerMovement>();
        player.AddComponent<PlayerWeapons>();
        player.AddComponent<PlayerCombat>();
        player.AddComponent<PlayerHealing>();
        // The kill-streak meter. GameDirector adds this as well if it finds it missing,
        // which is what keeps an already-saved scene working without a rebuild - but a
        // freshly built player should carry it from the start like every other script.
        player.AddComponent<PlayerSurge>();

        return player;
    }

    private static void PointTheCameraAtThePlayer(GameObject player)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
        }

        OrbitCamera followScript = mainCamera.gameObject.AddComponent<OrbitCamera>();
        followScript.targetToFollow = player.transform;
        mainCamera.farClipPlane = 300f;
    }

    // ----------------------------------------------------------------------------
    // The enemies
    // ----------------------------------------------------------------------------

    // Spawn points, zone barriers and cover. No enemies: those arrive round by round,
    // spawned by RoundDirector out of these positions.
    private static void BuildTheArenaFurniture(GameObject root)
    {
        GameObject folder = new GameObject("Enemies");
        folder.transform.SetParent(root.transform);
        enemyFolder = folder.transform;

        GameObject spawnFolder = new GameObject("SpawnPoints");
        spawnFolder.transform.SetParent(root.transform);

        approachSpawnPoints.Clear();
        narrowsSpawnPoints.Clear();
        hollowSpawnPoints.Clear();
        elevatedSpawnPoints.Clear();
        narrowsPillarList.Clear();
        hollowPillarList.Clear();

        // Ground spawn points, set out to the sides so nothing ever materialises on top
        // of the player.
        Vector3 approachMiddle = new Vector3(0f, 0f, -16f);
        Vector3 narrowsMiddle = new Vector3(0f, 0f, 2f);
        Vector3 arenaMiddle = new Vector3(0f, 0f, 22f);

        AddSpawnPoint(spawnFolder, approachSpawnPoints, -11f, -20f, approachMiddle);
        AddSpawnPoint(spawnFolder, approachSpawnPoints, 11f, -20f, approachMiddle);
        AddSpawnPoint(spawnFolder, approachSpawnPoints, -8f, -13f, approachMiddle);
        AddSpawnPoint(spawnFolder, approachSpawnPoints, 8f, -13f, approachMiddle);

        AddSpawnPoint(spawnFolder, narrowsSpawnPoints, -9f, -4f, narrowsMiddle);
        AddSpawnPoint(spawnFolder, narrowsSpawnPoints, 9f, 1f, narrowsMiddle);
        AddSpawnPoint(spawnFolder, narrowsSpawnPoints, -6f, 7f, narrowsMiddle);
        AddSpawnPoint(spawnFolder, narrowsSpawnPoints, 5f, 9f, narrowsMiddle);

        // Eight around the arena rather than four: every ordinary round is fought here
        // now, so enemies have to be able to arrive from any side.
        int arenaSpawn = 0;
        while (arenaSpawn < 8)
        {
            float angle = (arenaSpawn / 8f) * Mathf.PI * 2f + 0.3f;
            AddSpawnPoint(spawnFolder, hollowSpawnPoints,
                Mathf.Cos(angle) * 14f, 22f + Mathf.Sin(angle) * 14f, arenaMiddle);
            arenaSpawn = arenaSpawn + 1;
        }

        // High ground on top of the narrows shoulders, for throwers. Standing above the
        // fight is what makes a Spitter worth climbing to.
        AddElevatedSpawnPoint(spawnFolder, -17f, -2f, 7.6f);
        AddElevatedSpawnPoint(spawnFolder, 17f, 4f, 7.6f);
        AddElevatedSpawnPoint(spawnFolder, -16f, 3f, 7.6f);

        Material barrierMaterial = MakeTexturedRockMaterial(
            "Textures/Cliff", "Cliff",
            0.09f, 1.5f, new Color(0.78f, 0.76f, 0.74f),
            new Color(0.36f, 0.34f, 0.33f),
            new Color(0.22f, 0.21f, 0.21f),
            new Color(0.08f, 0.08f, 0.09f));

        narrowsBarrierObject = MakeBarrier(root, "NarrowsBarrier", -11f, barrierMaterial);
        hollowBarrierObject = MakeBarrier(root, "HollowBarrier", 12f, barrierMaterial);

        Material pillarMaterial = MakeTexturedRockMaterial(
            "Textures/Cliff", "Cliff",
            0.12f, 1.4f, new Color(0.95f, 0.93f, 0.90f),
            new Color(0.42f, 0.40f, 0.38f),
            new Color(0.26f, 0.25f, 0.24f),
            new Color(0.10f, 0.10f, 0.11f));

        MakePillar(root, narrowsPillarList, -4f, -3f, pillarMaterial);
        MakePillar(root, narrowsPillarList, 4f, 0f, pillarMaterial);
        MakePillar(root, narrowsPillarList, -3f, 5f, pillarMaterial);
        MakePillar(root, narrowsPillarList, 5f, 7f, pillarMaterial);

        // Arena cover sits between nine and thirteen metres out - deliberately OUTSIDE
        // the 5.5 m slam radius, so cover can never hide the warning ring.
        int pillarIndex = 0;
        while (pillarIndex < 6)
        {
            float angle = (pillarIndex / 6f) * Mathf.PI * 2f + 0.4f;
            float radius = 9.5f + (pillarIndex % 2) * 3f;
            MakePillar(root, hollowPillarList,
                Mathf.Cos(angle) * radius,
                22f + Mathf.Sin(angle) * radius,
                pillarMaterial);
            pillarIndex = pillarIndex + 1;
        }
    }

    // The Vault: the arena for the final fight, modelled in Blender and assembled here.
    //
    // Everything visible is an imported mesh. The only things made from code are the
    // lights, because a light is not geometry, and the pillars are placed rather than
    // baked into the room so they can rise and shatter.
    private static void BuildTheVault(GameObject root)
    {
        vaultPillarList.Clear();

        GameObject vault = new GameObject("TheVault");
        vault.transform.SetParent(root.transform);
        vault.transform.position = BossArenaOrigin;

        Material stoneMaterial = MakeTexturedRockMaterial(
            "Textures/Cliff", "Cliff",
            0.10f, 1.3f, new Color(0.52f, 0.50f, 0.58f),
            new Color(0.30f, 0.29f, 0.34f),
            new Color(0.18f, 0.17f, 0.21f),
            new Color(0.07f, 0.07f, 0.09f));

        GameObject structure = PlaceModel(vault, "BossArena", "VaultStructure",
            Vector3.zero, stoneMaterial, true);

        // The Vault's floor is an imported mesh exactly like the valley's, so it is the
        // same one-sided sheet and needs the same slab underneath it. The Warden throws
        // bodies around harder than anything in the valley does.
        AddBedrockUnder(vault, "VaultBedrock", structure);

        // The crystals get their own violet glow, and are the same colour as the
        // Warden's core so the room reads as belonging to it.
        // Pushed hard: in a dark room these ARE the lighting, and the post-processing
        // bloom only blooms what is meaningfully brighter than white.
        Material crystalMaterial = MakeGlowingMaterial(new Color(0.55f, 0.22f, 0.95f), 2.1f);
        PlaceModel(vault, "BossArenaCrystals", "VaultCrystals",
            Vector3.zero, crystalMaterial, false);

        // Braziers: eight around the wall, matching where the bowls were modelled.
        Material flameMaterial = MakeGlowingMaterial(new Color(1f, 0.52f, 0.14f), 3.2f);
        int brazierIndex = 0;
        while (brazierIndex < 8)
        {
            float angle = (brazierIndex / 8f) * Mathf.PI * 2f + 0.4f;
            float radius = 24f - 3.2f;
            Vector3 where = new Vector3(Mathf.Cos(angle) * radius, 3.5f, Mathf.Sin(angle) * radius);

            PlaceModel(vault, "BrazierFlame", "Flame", where, flameMaterial, false);

            GameObject lampObject = new GameObject("BrazierLight");
            lampObject.transform.SetParent(vault.transform);
            lampObject.transform.localPosition = where + Vector3.up * 0.8f;

            Light lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.62f, 0.28f);
            // Eight of these ring the wall, and they are the room's real light source.
            lamp.intensity = 40f;
            lamp.range = 42f;

            brazierIndex = brazierIndex + 1;
        }

        // Cover, at thirteen metres - outside the five and a half metre slam radius, so
        // hiding never costs the player sight of the warning ring.
        int pillarIndex = 0;
        while (pillarIndex < 8)
        {
            float angle = (pillarIndex / 8f) * Mathf.PI * 2f + 0.25f;
            Vector3 where = new Vector3(Mathf.Cos(angle) * 13f, 0f, Mathf.Sin(angle) * 13f);

            GameObject pillar = PlaceModel(vault, "VaultPillar", "VaultPillar",
                where, stoneMaterial, true);
            if (pillar != null)
            {
                // The Warden shatters these mid-fight, so they carve rather than bake.
                AddCarvingObstacle(pillar, new Vector3(1.9f, 3.6f, 1.9f));
                vaultPillarList.Add(pillar.AddComponent<Pillar>());
            }
            pillarIndex = pillarIndex + 1;
        }

        // A cold violet key light, so the room is lit by its crystals and its fires and
        // nothing else. The valley is daylight; this is deliberately not.
        GameObject keyObject = new GameObject("VaultKeyLight");
        keyObject.transform.SetParent(vault.transform);
        keyObject.transform.localPosition = new Vector3(0f, 16f, 14f);
        Light key = keyObject.AddComponent<Light>();
        key.type = LightType.Point;
        key.color = new Color(0.55f, 0.35f, 0.95f);
        // One broad fill over the middle of the floor, so the fight is readable in the
        // space between the braziers rather than only near the walls.
        key.intensity = 26f;
        key.range = 70f;

        // The portal stands at the north edge of the ORIGINAL arena, not in the Vault.
        BuildThePortal(root);
    }

    private static void BuildThePortal(GameObject root)
    {
        float groundHeight = GroundHeightAt(0f, 30f);

        GameObject portalHolder = new GameObject("Portal");
        portalHolder.transform.SetParent(root.transform);
        portalHolder.transform.position = new Vector3(0f, groundHeight, 30f);

        Material frameMaterial = MakeTexturedRockMaterial(
            "Textures/Cliff", "Cliff",
            0.14f, 1.3f, new Color(0.60f, 0.55f, 0.70f),
            new Color(0.32f, 0.30f, 0.36f),
            new Color(0.20f, 0.19f, 0.23f),
            new Color(0.08f, 0.08f, 0.10f));

        PlaceModel(portalHolder, "Portal", "PortalFrame", Vector3.zero, frameMaterial, true);

        Material surfaceMaterial = MakeGlowingMaterial(new Color(0.6f, 0.25f, 1f), 3f);
        GameObject surface = PlaceModel(portalHolder, "PortalSurface", "PortalSurface",
            Vector3.zero, surfaceMaterial, false);

        GameObject lampObject = new GameObject("PortalLight");
        lampObject.transform.SetParent(portalHolder.transform);
        lampObject.transform.localPosition = new Vector3(0f, 3.1f, 0f);
        Light lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(0.65f, 0.35f, 1f);
        lamp.intensity = 0f;
        lamp.range = 26f;

        Portal portal = portalHolder.AddComponent<Portal>();
        portal.destination = BossArenaOrigin + new Vector3(0f, 2.5f, -18f);
        if (surface != null)
        {
            portal.SetSurface(surface.transform);
        }
        portal.SetLight(lamp);

        portalObject = portalHolder;
    }

    // Instantiates an imported model, corrects the centimetre scale, paints it and
    // optionally gives it a collider so the player can stand on or behind it.
    private static GameObject PlaceModel(GameObject parent, string modelName, string newName,
        Vector3 localPosition, Material material, bool solid)
    {
        GameObject prefab = Resources.Load<GameObject>("Models/" + modelName);
        if (prefab == null)
        {
            Debug.LogWarning("Model missing: " + modelName);
            return null;
        }

        GameObject placed = Object.Instantiate(prefab);
        placed.name = newName;
        placed.transform.SetParent(parent.transform);
        placed.transform.localPosition = localPosition;
        placed.transform.localRotation = Quaternion.identity;
        placed.transform.localScale = Vector3.one;

        int childIndex = 0;
        while (childIndex < placed.transform.childCount)
        {
            placed.transform.GetChild(childIndex).localScale = Vector3.one;
            childIndex = childIndex + 1;
        }

        Renderer[] surfaces = placed.GetComponentsInChildren<Renderer>();
        int surfaceIndex = 0;
        while (surfaceIndex < surfaces.Length)
        {
            surfaces[surfaceIndex].material = material;
            surfaceIndex = surfaceIndex + 1;
        }

        // Anything imported brings no collider of its own, so walls and floors need one
        // added or the player walks straight through the room.
        if (solid == true)
        {
            MeshFilter[] meshes = placed.GetComponentsInChildren<MeshFilter>();
            int meshIndex = 0;
            while (meshIndex < meshes.Length)
            {
                MeshCollider surfaceCollider = meshes[meshIndex].gameObject.AddComponent<MeshCollider>();
                surfaceCollider.sharedMesh = meshes[meshIndex].sharedMesh;
                meshIndex = meshIndex + 1;
            }
        }

        return placed;
    }

    // Whether a creature could stand at this point without being inside the scenery.
    //
    // Pillars and zone barriers do not count, because they spend the whole game either
    // sunk below the floor or rising on cue - they stand at their full height only while
    // the valley is being BUILT, which is exactly when this runs. Treating them as solid
    // shoves spawn points away from places that will be wide open by the time anything
    // spawns there.
    private static bool IsClearOfScenery(Vector3 where)
    {
        Collider[] touching = Physics.OverlapSphere(where, 0.9f, ~0,
            QueryTriggerInteraction.Ignore);

        int index = 0;
        while (index < touching.Length)
        {
            GameObject what = touching[index].gameObject;

            bool sinksOutOfTheWay =
                what.GetComponent<Pillar>() != null || what.GetComponent<ZoneBarrier>() != null;

            if (sinksOutOfTheWay == false)
            {
                return false;
            }
            index = index + 1;
        }

        return true;
    }

    // Places a spawn point on ground that is actually OPEN, not merely ground.
    //
    // GroundHeightAt only ever looks at the terrain collider. That is right for finding
    // the height of the floor and completely blind to anything standing on it. Two points
    // of the arena ring landed inside the north cliff: the terrain answered "floor at
    // y=0.2", the cliff was never consulted, and the enemy was born inside solid rock.
    // Unity shoved it out through the one-sided ground, it fell forever, and the round sat
    // waiting for something no player could ever reach. That is the bug that made round
    // two impossible to finish.
    private static void AddSpawnPoint(GameObject parent, List<Transform> into,
        float x, float z, Vector3 pullToward)
    {
        // Colliders built moments ago are not in the physics world until this is called,
        // and a query against them would sail straight through.
        Physics.SyncTransforms();

        Vector3 settled = new Vector3(x, GroundHeightAt(x, z) + 1.2f, z);
        bool foundOpenGround = false;

        int attempt = 0;
        while (attempt < 12)
        {
            if (IsClearOfScenery(settled) == true)
            {
                foundOpenGround = true;
                break;
            }

            // Walk in toward the middle of the arena rather than giving up. A metre and a
            // half at a time steps clear of a cliff face in a few tries without pulling
            // the ring noticeably out of shape.
            Vector3 inward = pullToward - settled;
            inward.y = 0f;
            settled = settled + inward.normalized * 1.5f;
            settled.y = GroundHeightAt(settled.x, settled.z) + 1.2f;

            attempt = attempt + 1;
        }

        if (foundOpenGround == false)
        {
            settled = new Vector3(
                pullToward.x, GroundHeightAt(pullToward.x, pullToward.z) + 1.2f, pullToward.z);
            Debug.LogWarning("The spawn point wanted at (" + x + ", " + z + ") is buried in "
                + "scenery and could not be walked clear of. It has been moved to the "
                + "middle of its arena instead.");
        }

        GameObject point = new GameObject("Spawn");
        point.transform.SetParent(parent.transform);
        point.transform.position = settled;
        into.Add(point.transform);
    }

    private static void AddElevatedSpawnPoint(GameObject parent, float x, float z, float y)
    {
        GameObject point = new GameObject("HighSpawn");
        point.transform.SetParent(parent.transform);
        point.transform.position = new Vector3(x, y, z);
        elevatedSpawnPoints.Add(point.transform);
    }

    private static GameObject MakeBarrier(GameObject root, string name, float z, Material material)
    {
        float groundHeight = GroundHeightAt(0f, z);
        GameObject slab = MakeBox(root, name,
            new Vector3(0f, groundHeight + 4f, z),
            new Vector3(ValleyHalfWidth * 2f, 8f, 2.4f),
            material);
        AddCarvingObstacle(slab, new Vector3(ValleyHalfWidth * 2f, 8f, 2.4f));
        slab.AddComponent<ZoneBarrier>();
        return slab;
    }

    private static void MakePillar(GameObject root, List<Pillar> into,
        float x, float z, Material material)
    {
        float groundHeight = GroundHeightAt(x, z);
        GameObject pillar = MakeBox(root, "Pillar",
            new Vector3(x, groundHeight + 1.6f, z),
            new Vector3(1.5f, 3.2f, 1.5f),
            material);
        AddCarvingObstacle(pillar, new Vector3(1.5f, 3.2f, 1.5f));
        into.Add(pillar.AddComponent<Pillar>());
    }

    // Makes a moving object cut its own hole in the walkable map.
    //
    // Anything that is in a different place at the end of a round than at the start of it
    // cannot simply be baked in, because the bake happens once. A carving obstacle is
    // re-cut from the mesh wherever it currently stands, and the hole disappears by itself
    // when the object is switched off - which is exactly what a shattered pillar does.
    private static void AddCarvingObstacle(GameObject onWhat, Vector3 size)
    {
        NavMeshObstacle obstacle = onWhat.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = size;
        obstacle.center = Vector3.zero;
        obstacle.carving = true;
        // Recut only once it has come to rest. Recarving every frame of a rise is
        // expensive and the mesh would be stale by the time anything used it anyway.
        obstacle.carveOnlyStationary = true;
    }

    // Drops a point onto whatever solid surface is under it, and nudges it clear of
    // anything already standing there so nothing is ever born inside something else.
    private static Vector3 PlaceOnGround(Vector3 wanted)
    {
        // A creature built earlier THIS FRAME is not in the physics world yet, so the
        // overlap test below cannot see it. A whole wave spawns in one frame, so without
        // this several of them are placed on the identical spot, Unity shoves the stack
        // apart hard enough to push one through the one-sided ground, and it falls out of
        // the world. That is the last of the round-four stragglers.
        Physics.SyncTransforms();

        Vector3 settled = SurfaceUnder(wanted);

        // If something is already standing here, step around it. Twelve tries is plenty
        // for a crowded round, and giving up quietly is better than looping forever.
        int attempt = 0;
        while (attempt < 12)
        {
            Collider[] alreadyHere = Physics.OverlapSphere(settled, 1.1f);
            bool occupied = false;

            int index = 0;
            while (index < alreadyHere.Length)
            {
                if (alreadyHere[index].GetComponent<CharacterController>() != null)
                {
                    occupied = true;
                    break;
                }
                index = index + 1;
            }

            // Stepping clear of a crowd must not step INTO the scenery. Round four spawns
            // nineteen enemies around one ring, so the step-around fires constantly - and
            // pushing outward by two metres from the northern arc walked them straight
            // into the cliff face, where they were shoved through the ground and fell out
            // of the world. Being crowded is a much smaller problem than being buried.
            if (occupied == false && IsClearOfScenery(settled) == false)
            {
                occupied = true;
            }

            if (occupied == false)
            {
                return settled;
            }

            float angle = attempt * 0.9f;
            settled = settled + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2.2f;
            settled = SurfaceUnder(settled);

            attempt = attempt + 1;
        }

        return settled;
    }

    // Where the solid world is directly under a point, as it is right now.
    //
    // Returns false when there is nothing underneath at all, so a caller is never handed a
    // plausible-looking zero and left to build on it.
    public static bool TryFindSurfaceUnder(Vector3 near, out float surfaceHeight)
    {
        surfaceHeight = near.y;

        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(near.x, near.y + 3f, near.z), Vector3.down, 260f,
            ~0, QueryTriggerInteraction.Ignore);

        bool foundOne = false;

        int index = 0;
        while (index < hits.Length)
        {
            // Creatures are not scenery. Standing on one is how a spawn ends up on
            // somebody else's head and both get shoved somewhere neither should be.
            if (hits[index].collider.GetComponent<CharacterController>() == null)
            {
                if (foundOne == false || hits[index].point.y > surfaceHeight)
                {
                    surfaceHeight = hits[index].point.y;
                    foundOne = true;
                }
            }
            index = index + 1;
        }

        return foundOne;
    }

    // Somewhere a creature of this size can stand at these coordinates, searched from high
    // above so it still works for something that has already fallen under the world and
    // cannot see the ground from where it is.
    // Is this point underneath the valley floor altogether?
    //
    // The floor is a mesh collider, and a mesh collider is one-sided: it stops things
    // landing on it from above and does nothing at all to something that has got beneath
    // it. Creatures get shoved under it when they are crowded together - against the
    // gate, mostly - and once under, there is nothing to stand on and no way back up.
    // They then sink very slowly for the rest of the round, unreachable and unkillable.
    //
    // Being below the LOWEST point of the entire floor mesh is unambiguous. Nothing in
    // the valley can legitimately be down there, and unlike a downward ray it cannot be
    // fooled by the buried portal frame sitting underneath the terrain.
    //
    // Only asked about points inside the valley. The Vault and the dungeon are separate
    // rooms far away with their own floors, and their creatures must not be judged
    // against the valley's.
    public static bool IsUnderneathTheValley(Vector3 where)
    {
        // Looked up rather than remembered, for the same reason GroundHeightAt does it:
        // this cache is a static, and entering play mode reloads the domain and wipes it.
        if (theGroundCollider == null)
        {
            GameObject ground = GameObject.Find("Ground");
            if (ground != null)
            {
                theGroundCollider = ground.GetComponentInChildren<MeshCollider>();
            }
        }

        if (theGroundCollider == null)
        {
            return false;
        }

        Bounds groundBounds = theGroundCollider.bounds;

        bool insideTheValleyFootprint =
            where.x > groundBounds.min.x && where.x < groundBounds.max.x &&
            where.z > groundBounds.min.z && where.z < groundBounds.max.z;

        if (insideTheValleyFootprint == false)
        {
            return false;
        }

        // Half a metre of slack, so a creature standing in a dip right at the lowest
        // point of the valley is never mistaken for one that has fallen through it.
        return where.y < groundBounds.min.y - 0.5f;
    }

    // The top of the floor directly beneath a point, for a character that wants to check
    // it is still standing on top of it rather than underneath it.
    //
    // Answers false when the point is not over one of the mesh floors at all - out on the
    // road north, or down in Orrin's cellar. Both of those are built from solid boxes,
    // nothing can be squeezed through them, and measuring a character in one of them
    // against the valley's floor two hundred metres away would be nonsense.
    //
    // The ray is aimed at the floor collider ALONE, never fired into the world at large.
    // A general raycast finds whichever creature happens to be standing on that spot and
    // reports the top of its head as the ground, which is how bodies used to end up
    // stacked on one another.
    public static bool TryFindFloorUnder(Vector3 where, out float floorHeight)
    {
        floorHeight = 0f;

        FindTheSheetFloorsIfNeeded();

        bool foundOne = false;

        int index = 0;
        while (index < theSheetFloors.Count)
        {
            Collider floor = theSheetFloors[index];
            index = index + 1;

            // Destroyed with the last rebuild. The list is rebuilt below on the next
            // call once this one has emptied it.
            if (floor == null)
            {
                theSheetFloors.Clear();
                return false;
            }

            Bounds covers = floor.bounds;

            bool overThisFloor =
                where.x > covers.min.x && where.x < covers.max.x &&
                where.z > covers.min.z && where.z < covers.max.z;

            if (overThisFloor == false)
            {
                continue;
            }

            // Started above the highest point of this floor rather than above the
            // character, so it still answers for a body that has already got underneath
            // and cannot see the floor from where it is.
            Ray downward = new Ray(new Vector3(where.x, covers.max.y + 5f, where.z), Vector3.down);
            RaycastHit whatWasHit;
            if (floor.Raycast(downward, out whatWasHit, covers.size.y + 10f) == false)
            {
                continue;
            }

            // The valley is one mesh split into several pieces, so a point can be over
            // more than one of them. The highest surface is the one being walked on.
            if (foundOne == false || whatWasHit.point.y > floorHeight)
            {
                floorHeight = whatWasHit.point.y;
                foundOne = true;
            }
        }

        return foundOne;
    }

    // Found by looking rather than by remembering, for the same reason GroundHeightAt
    // does it: this list is a static, and entering play mode reloads the script domain
    // and empties every static in this file. A list filled while the scene was being
    // built is gone by the time a character asks a question of it.
    private static void FindTheSheetFloorsIfNeeded()
    {
        if (theSheetFloors.Count > 0)
        {
            return;
        }

        // GameObject.Find walks the whole scene, and this is asked once per character per
        // frame. While the floors are missing - between a rebuild tearing the old valley
        // down and the new one going up - an unthrottled search would do that walk
        // thirteen times a frame and find nothing every time. Half a second is far shorter
        // than anyone can notice and cheap enough to leave running.
        if (Time.unscaledTime - whenTheFloorsWereLastSearchedFor < 0.5f)
        {
            return;
        }
        whenTheFloorsWereLastSearchedFor = Time.unscaledTime;

        CollectSheetFloorsFrom("Ground");
        CollectSheetFloorsFrom("VaultStructure");
    }

    private static void CollectSheetFloorsFrom(string objectName)
    {
        GameObject floor = GameObject.Find(objectName);
        if (floor == null)
        {
            return;
        }

        MeshCollider[] pieces = floor.GetComponentsInChildren<MeshCollider>();
        int index = 0;
        while (index < pieces.Length)
        {
            theSheetFloors.Add(pieces[index]);
            index = index + 1;
        }
    }

    // Somewhere in the middle of the arena that is definitely walkable. Anything rescued
    // is put here rather than back where it fell, because where it fell is by definition
    // a place that swallows creatures.
    public static Vector3 MiddleOfTheArena()
    {
        return new Vector3(0f, 0f, 22f);
    }

    public static Vector3 SafeStandingSpot(float x, float z, float bodyHalfHeight)
    {
        float surfaceHeight;
        if (TryFindSurfaceUnder(new Vector3(x, 150f, z), out surfaceHeight) == true)
        {
            return new Vector3(x, surfaceHeight + bodyHalfHeight + 0.25f, z);
        }

        // Nothing at those coordinates at all. The middle of the arena always has floor.
        if (TryFindSurfaceUnder(new Vector3(0f, 150f, 20f), out surfaceHeight) == true)
        {
            return new Vector3(0f, surfaceHeight + bodyHalfHeight + 0.25f, 20f);
        }

        return new Vector3(x, 3f, z);
    }

    // Finds the surface a creature should stand on at a given point.
    //
    // The ray starts just ABOVE the requested position rather than high in the sky,
    // because a spawn point on top of a rock shoulder MEANS that shoulder. Aiming only at
    // the terrain collider from overhead threw the height away and dropped the creature to
    // the valley floor - which at the shoulders is underneath solid rock. The character
    // controller then shoved it out through the one-sided ground, and it fell out of the
    // world and had to be rescued. That is what the Spitters were doing every round.
    //
    // Other creatures are ignored, so nobody is ever placed standing on someone else.
    private static Vector3 SurfaceUnder(Vector3 wanted)
    {
        float surfaceHeight;
        if (TryFindSurfaceUnder(wanted, out surfaceHeight) == false)
        {
            // Nothing underneath at all. Better to leave the point where it was asked for
            // than to drop it to zero, which in this valley is often below the ground.
            return wanted;
        }

        return new Vector3(wanted.x, surfaceHeight + 1.3f, wanted.z);
    }

    // Builds one enemy of the named kind at a position, for RoundDirector to call at
    // runtime. The creature recipes are unchanged - this only chooses between them.
    public static EnemyBrain SpawnEnemy(string kind, Vector3 where)
    {
        // A static field does NOT survive entering play mode. Unity reloads the script
        // domain when play starts, and every static resets to null - so the folder found
        // while building the scene in the editor is gone by the time a round asks for an
        // enemy. Every spawn then failed silently and each round reported itself cleared
        // the instant it began.
        //
        // The folder is therefore found, or made, on demand.
        if (enemyFolder == null)
        {
            GameObject found = GameObject.Find("Enemies");
            if (found == null)
            {
                found = new GameObject("Enemies");
                GameObject root = GameObject.Find("ValleyRoot");
                if (root != null)
                {
                    found.transform.SetParent(root.transform);
                }
            }
            enemyFolder = found.transform;
        }

        GameObject holder = enemyFolder.gameObject;

        // Put it on the ground that is actually there RIGHT NOW, rather than trusting
        // the height baked into a spawn point when the scene was built.
        //
        // Spawning even slightly inside another body makes Unity shove the two apart to
        // separate them, and a character controller shoved hard enough passes straight
        // through the terrain - which is one-sided, so there is no floor underneath to
        // catch it. Enemies were ending up forty thousand metres down and falling
        // forever, and a round that is waiting on a falling enemy never ends.
        where = PlaceOnGround(where);

        // Standing on the ground is not the same as standing somewhere a creature can
        // BE. Two of the arena spawn points sit just past the northern lip of the
        // terrain, where the ground the ray finds is a sliver with nothing behind it -
        // the enemy appeared, slid off, and fell out of the world, and the round then
        // waited forever for something nobody could reach. Snapping to the walkable map
        // is the only check that actually answers the right question.
        // From here on, where.y IS THE GROUND the creature stands on - settled once,
        // here, and not second-guessed further down.
        Vector3 onWalkableGround;
        if (NavigationField.TryFindNearbyPoint(where, 5f, out onWalkableGround) == true)
        {
            where = onWalkableGround;
        }
        else
        {
            float surfaceHeight;
            if (TryFindSurfaceUnder(where, out surfaceHeight) == true)
            {
                where.y = surfaceHeight;
            }
        }

        // Last word on the matter: nothing is ever created off the walkable map.
        //
        // The crowd step-around above can shuffle a spawn two metres at a time, and on the
        // northern arc of the arena a couple of those steps walk it into the cliff - from
        // where the nearest walkable ground may be further than the snap will reach. One
        // enemy in a busy round is enough to leave that round unfinishable, so rather than
        // chase every path that could put it there, this refuses the position outright and
        // uses the middle of the arena, which is always walkable.
        Vector3 confirmed;
        if (NavigationField.IsReady == true
            && NavigationField.TryFindNearbyPoint(where, 1.5f, out confirmed) == false)
        {
            Vector3 middleOfTheArena = new Vector3(0f, 2f, 22f);
            if (NavigationField.TryFindNearbyPoint(middleOfTheArena, 20f, out confirmed) == true)
            {
                Debug.LogWarning("A " + kind + " was about to be built at " + where
                    + ", which is not walkable ground. Moved to " + confirmed + " instead.");
                where = confirmed;
            }
        }

        if (kind == "Grunt")
        {
            return MakeGrunt(holder, where);
        }
        if (kind == "Darter")
        {
            return MakeDarter(holder, where);
        }
        if (kind == "Spitter")
        {
            return MakeSpitter(holder, where);
        }
        if (kind == "Warden")
        {
            return MakeWarden(holder, where);
        }

        Debug.LogWarning("Unknown enemy kind: " + kind);
        return null;
    }

    // A squat brute with a club. Chops a wedge in front of itself - walk around behind it.
    private static EnemyBrain MakeGrunt(GameObject parent, Vector3 where)
    {
        Material hideMaterial = MakeMaterial(new Color(0.52f, 0.26f, 0.22f));
        Material woodMaterial = MakeMaterial(new Color(0.35f, 0.24f, 0.15f));

        // 1.91 m tall, which is the height of the model as it came out of Blender.
        GameObject grunt = MakeEnemyShell(parent, "Grunt", where,
            "Grunt", hideMaterial, 1.91f, 0.45f, 1.0f);

        // The club hangs off a pivot out at the right fist. Rotating the pivot is what
        // raises the weapon during the wind-up and swings it down on the strike.
        GameObject shoulderPivot = new GameObject("WeaponPivot");
        shoulderPivot.transform.SetParent(grunt.transform);
        shoulderPivot.transform.localPosition = new Vector3(1.02f, 0.44f, 0.05f);
        shoulderPivot.transform.localRotation = Quaternion.identity;

        GameObject club = AttachModel(shoulderPivot, "GruntClub", woodMaterial, 0f, 1.0f);
        if (club != null)
        {
            // The club stands up along its own Y axis. Tipping it forward lays it along
            // Z, which is the axis the swing rotates around.
            club.transform.localRotation = Quaternion.Euler(78f, 0f, 0f);
            club.transform.localPosition = new Vector3(0f, -0.06f, 0.12f);
        }

        CharacterStats stats = grunt.GetComponent<CharacterStats>();
        stats.maximumHealth = 30f;
        stats.currentHealth = 30f;
        stats.attackDamage = 10f;
        stats.essenceDroppedOnDeath = 1;

        EnemyBrain brain = grunt.GetComponent<EnemyBrain>();
        brain.displayName = "Grunt";
        brain.attackShape = EnemyBrain.AttackShapeSweep;
        brain.detectionRadius = 13f;
        brain.loseInterestRadius = 20f;
        brain.moveSpeed = 2.3f;
        brain.attackRange = 2.8f;
        brain.secondsBetweenAttacks = 1.9f;
        brain.windUpSeconds = 0.7f;
        brain.strikeSeconds = 0.28f;
        brain.damageLandsAfterSeconds = 0.12f;
        brain.sweepHalfAngleDegrees = 60f;
        brain.retreatsAfterAttacking = false;
        brain.isTheWarden = false;
        brain.weaponPivot = shoulderPivot.transform;

        // Arches well back over the raised club, then folds hard through the chop.
        brain.windUpLeanDegrees = 12f;
        brain.strikeLeanDegrees = -19f;

        return brain;
    }

    // A lean sprinter. Charges down a straight line - step sideways out of it.
    private static EnemyBrain MakeDarter(GameObject parent, Vector3 where)
    {
        Material shellMaterial = MakeMaterial(new Color(0.88f, 0.44f, 0.10f));
        Material spikeMaterial = MakeMaterial(new Color(0.20f, 0.12f, 0.08f));
        Material redEyeMaterial = MakeGlowingMaterial(new Color(1f, 0.25f, 0.15f), 4f);

        // Low and long: 0.85 m at the shoulder but 2.2 m nose to tail.
        GameObject darter = MakeEnemyShell(parent, "Darter", where,
            "Darter", shellMaterial, 0.85f, 0.42f, 1.0f);

        CharacterStats stats = darter.GetComponent<CharacterStats>();
        stats.maximumHealth = 15f;
        stats.currentHealth = 15f;
        stats.attackDamage = 20f;
        stats.essenceDroppedOnDeath = 2;

        EnemyBrain brain = darter.GetComponent<EnemyBrain>();
        brain.displayName = "Darter";
        brain.attackShape = EnemyBrain.AttackShapeLunge;
        brain.detectionRadius = 17f;
        brain.loseInterestRadius = 26f;
        brain.moveSpeed = 5.2f;
        brain.attackRange = 2.2f;
        brain.secondsBetweenAttacks = 1.9f;
        // Barely any telegraph - just a crouch. This is what makes the Darter
        // frightening despite having almost no health.
        brain.windUpSeconds = 0.36f;
        brain.strikeSeconds = 0.34f;
        brain.lungeSpeed = 23f;
        brain.lungeMinimumRange = 4.5f;
        brain.lungeMaximumRange = 12f;
        brain.retreatsAfterAttacking = true;
        brain.retreatSeconds = 0.9f;
        brain.isTheWarden = false;

        // A crouch rather than an arch: the Darter drops its nose and coils before it
        // springs, then flattens out along the charge.
        brain.windUpLeanDegrees = -11f;
        brain.strikeLeanDegrees = 9f;

        return brain;
    }

    // A slow siege engine. Slams a circle around itself - the only answer is distance.
    private static EnemyBrain MakeWarden(GameObject parent, Vector3 where)
    {
        Material stoneMaterial = MakeMaterial(new Color(0.30f, 0.19f, 0.40f));
        Material trimMaterial = MakeMaterial(new Color(0.16f, 0.10f, 0.22f));
        Material coreMaterial = MakeGlowingMaterial(new Color(0.75f, 0.35f, 1f), 3.5f);

        // The model is 2.7 m tall as built; scaling it up to 3.65 m gives the boss the
        // presence it needs next to a 1.9 m Grunt.
        GameObject warden = MakeEnemyShell(parent, "Warden", where,
            "Warden", stoneMaterial, 3.65f, 0.85f, 1.35f);

        // The warning ring that grows on the ground during the wind-up. It starts hidden
        // and EnemyBrain switches it on. Sitting just above the floor avoids the two
        // surfaces fighting over which one is drawn.
        GameObject ring = MakeRawCylinder("DangerRing", new Vector3(1f, 0.02f, 1f),
            MakeGlowingMaterial(new Color(1f, 0.6f, 0.2f), 2f));
        ring.transform.SetParent(warden.transform);
        ring.transform.localPosition = new Vector3(0f, -3.65f * 0.5f + 0.06f, 0f);
        ring.transform.localRotation = Quaternion.identity;

        CharacterStats stats = warden.GetComponent<CharacterStats>();
        // Six hundred, not two hundred. Three phases need enough runway for each one to
        // be felt, and at two hundred the fight ended before phase two arrived.
        stats.maximumHealth = 600f;
        stats.currentHealth = 600f;
        stats.attackDamage = 40f;
        stats.essenceDroppedOnDeath = 0;

        EnemyBrain brain = warden.GetComponent<EnemyBrain>();
        brain.displayName = "The Warden";
        brain.attackShape = EnemyBrain.AttackShapeSlam;
        brain.detectionRadius = 20f;
        brain.loseInterestRadius = 45f;
        brain.moveSpeed = 1.9f;
        brain.attackRange = 5f;
        brain.secondsBetweenAttacks = 2.8f;
        // A long, obvious wind-up. Forty damage is nearly half the player's health, so
        // the telegraph has to be generous enough to be fair.
        brain.windUpSeconds = 1.25f;
        brain.strikeSeconds = 0.3f;
        brain.damageLandsAfterSeconds = 0.05f;
        brain.slamRadius = 5.5f;
        brain.retreatsAfterAttacking = false;
        brain.isTheWarden = true;
        brain.dangerRing = ring.transform;

        // The Warden has no weapon, so the whole slam is carried by the body. It rears
        // further back than the Grunt and drives further forward, because a four metre
        // golem moving the same amount would barely register.
        brain.windUpLeanDegrees = 15f;
        brain.strikeLeanDegrees = -24f;

        // The boss brain rides alongside the ordinary one, switching capabilities on as
        // its health falls.
        warden.AddComponent<WardenBoss>();

        return brain;
    }

    // Hangs a model built in Blender under an object, at the right size and with its
    // feet on the object's base.
    //
    // Two things always need correcting on import. Blender writes FBX in centimetres,
    // which leaves a factor of a hundred on the imported root, so every scale is forced
    // back explicitly. And the model's own origin is at its feet, so it has to be pushed
    // DOWN by half the body height to line up with a character controller centred on the
    // middle of the body.
    private static GameObject AttachModel(GameObject parent, string modelName,
        Material material, float feetBelowCentre, float modelScale)
    {
        GameObject prefab = Resources.Load<GameObject>("Models/" + modelName);
        if (prefab == null)
        {
            Debug.LogWarning("Model '" + modelName + "' not found - leaving " + parent.name + " invisible.");
            return null;
        }

        GameObject model = Object.Instantiate(prefab);
        model.name = modelName + "Model";
        model.transform.SetParent(parent.transform);
        model.transform.localPosition = new Vector3(0f, -feetBelowCentre, 0f);
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = new Vector3(modelScale, modelScale, modelScale);

        int childIndex = 0;
        while (childIndex < model.transform.childCount)
        {
            model.transform.GetChild(childIndex).localScale = Vector3.one;
            childIndex = childIndex + 1;
        }

        Renderer[] surfaces = model.GetComponentsInChildren<Renderer>();
        int surfaceIndex = 0;
        while (surfaceIndex < surfaces.Length)
        {
            surfaces[surfaceIndex].material = material;
            surfaceIndex = surfaceIndex + 1;
        }

        // The controller on the parent is the only collider a character may have.
        // Anything the model brought with it would make every sword swing count twice.
        Collider[] strays = model.GetComponentsInChildren<Collider>();
        int strayIndex = 0;
        while (strayIndex < strays.Length)
        {
            Object.DestroyImmediate(strays[strayIndex]);
            strayIndex = strayIndex + 1;
        }

        return model;
    }


    // A hunched thrower. Keeps its distance and lobs rocks, which is the only attack in
    // the game that does not require reaching the player - so it is the enemy that turns
    // standing in the open from free into expensive.
    //
    // Reuses the Grunt model at three-quarter size and a sickly green, because there is
    // no Spitter mesh. Honest stand-in rather than a fifth half-finished model.
    private static EnemyBrain MakeSpitter(GameObject parent, Vector3 where)
    {
        Material hideMaterial = MakeMaterial(new Color(0.36f, 0.50f, 0.26f));

        GameObject spitter = MakeEnemyShell(parent, "Spitter", where,
            "Grunt", hideMaterial, 1.55f, 0.40f, 0.78f);

        CharacterStats stats = spitter.GetComponent<CharacterStats>();
        stats.maximumHealth = 12f;
        stats.currentHealth = 12f;
        stats.attackDamage = 14f;
        stats.essenceDroppedOnDeath = 2;

        EnemyBrain brain = spitter.GetComponent<EnemyBrain>();
        brain.displayName = "Spitter";
        brain.attackShape = EnemyBrain.AttackShapeRanged;
        brain.detectionRadius = 20f;
        brain.loseInterestRadius = 30f;
        brain.moveSpeed = 3.0f;
        brain.attackRange = 16f;
        brain.secondsBetweenAttacks = 2.4f;
        // A long, obvious wind-up. The rock is slow enough to dodge, but only if the
        // throw is seen coming.
        brain.windUpSeconds = 0.8f;
        brain.strikeSeconds = 0.25f;
        brain.damageLandsAfterSeconds = 0.05f;
        brain.preferredRangeMinimum = 6f;
        brain.preferredRangeMaximum = 14f;
        brain.projectileSpeed = 14f;
        brain.throwHeight = 1.3f;
        brain.retreatsAfterAttacking = false;
        brain.isTheWarden = false;

        // Rocks back to throw, then snaps forward with the release.
        brain.windUpLeanDegrees = 16f;
        brain.strikeLeanDegrees = -20f;

        return brain;
    }

    // The bare body every enemy is built on: a controller, stats, a brain and a model.
    private static GameObject MakeEnemyShell(GameObject parent, string name, Vector3 where,
        string modelName, Material bodyMaterial, float bodyHeight, float bodyRadius, float modelScale)
    {
        GameObject shell = new GameObject(name);
        shell.transform.SetParent(parent.transform);

        // The controller is centred on the middle of the body, so the transform sits
        // half a body height above the ground it is standing on.
        //
        // The caller has already settled which ground this creature stands on, against
        // the walkable map where there is one. Working it out AGAIN here with a downward
        // ray was how enemies ended up perched on slivers of cliff face beside the arena:
        // the ray finds the highest surface below, and near a cliff base that is a ledge
        // no creature can stand on. One answer, decided once.
        shell.transform.position = new Vector3(where.x, where.y + bodyHeight * 0.5f, where.z);
        shell.transform.localScale = Vector3.one;

        CharacterController controller = shell.AddComponent<CharacterController>();
        controller.height = bodyHeight;
        controller.radius = bodyRadius;
        controller.center = Vector3.zero;
        controller.slopeLimit = 55f;
        controller.stepOffset = 0.4f;
        TuneControllerAgainstBeingSquashedThroughTheFloor(controller);

        GameObject body = AttachModel(shell, modelName, bodyMaterial, bodyHeight * 0.5f, modelScale);

        // The agent is a route planner, not a driver. It is deliberately NOT allowed to
        // move or turn the creature - the character controller keeps doing that, so
        // gravity, knockback, lunges and the fall-out-of-the-world rescue all keep
        // working exactly as they did. All the brain takes from it is a direction.
        //
        // It starts disabled because enabling an agent that is not standing on the
        // navigation mesh logs an error, and at build time there is no mesh yet. The
        // brain turns it on once it has confirmed both.
        NavMeshAgent agent = shell.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        agent.radius = bodyRadius;
        agent.height = bodyHeight;
        agent.baseOffset = 0f;
        agent.speed = 4f;
        agent.angularSpeed = 360f;
        agent.acceleration = 40f;
        agent.stoppingDistance = 0f;
        agent.autoBraking = false;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        // Agents shoving through one another looks worse than walking into a rock, and
        // round four puts thirteen of them on the field at once.
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.GoodQualityObstacleAvoidance;

        shell.AddComponent<CharacterStats>();
        EnemyBrain brain = shell.AddComponent<EnemyBrain>();
        brain.bodySize = bodyHeight * 0.5f;

        // Handing the brain the body separately from the root is what lets it arch and
        // fold the creature without touching where it stands or which way it faces.
        if (body != null)
        {
            brain.bodyTransform = body.transform;
            // The model hangs half a body height below the controller's centre, and
            // every lean and drop is measured from there.
            brain.restingBodyHeight = -bodyHeight * 0.5f;
        }

        return shell;
    }

    // ----------------------------------------------------------------------------
    // Fixtures
    // ----------------------------------------------------------------------------

    private static void BuildTheShrine(GameObject root)
    {
        Material shrineMaterial = MakeGlowingMaterial(new Color(0.25f, 0.9f, 0.8f), 1.6f);

        float shrineGroundHeight = GroundHeightAt(7f, -31f);

        GameObject shrine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shrine.name = "ShrineOfEssence";
        shrine.transform.SetParent(root.transform);
        shrine.transform.position = new Vector3(7f, shrineGroundHeight + 1.1f, -31f);
        shrine.transform.localScale = new Vector3(1.1f, 2.2f, 1.1f);
        shrine.transform.Rotate(0f, 45f, 0f);
        shrine.GetComponent<Renderer>().material = shrineMaterial;

        // A trigger so the player can stand inside the shrine's space without being
        // blocked by it.
        shrine.GetComponent<BoxCollider>().isTrigger = true;
        shrine.AddComponent<ShrineOfEssence>();

        Material baseMaterial = MakeMaterial(new Color(0.23f, 0.22f, 0.24f));
        MakeCylinder(root, "ShrineBase",
            new Vector3(7f, shrineGroundHeight + 0.08f, -31f),
            new Vector3(4.5f, 0.2f, 4.5f), baseMaterial);
    }

    private static void BuildTheDirectorAndDisplay(GameObject root)
    {
        GameObject director = new GameObject("GameDirector");
        director.transform.SetParent(root.transform);

        GameDirector directorScript = director.AddComponent<GameDirector>();
        directorScript.essenceCostPerUpgrade = 3;

        // The round system. Given the spawn points and arena furniture directly rather
        // than searching for them, so nothing depends on object names.
        RoundDirector rounds = director.AddComponent<RoundDirector>();
        rounds.approachSpawns = approachSpawnPoints;
        rounds.narrowsSpawns = narrowsSpawnPoints;
        rounds.hollowSpawns = hollowSpawnPoints;
        rounds.elevatedSpawns = elevatedSpawnPoints;
        rounds.narrowsPillars = narrowsPillarList;
        rounds.hollowPillars = hollowPillarList;
        rounds.vaultPillars = vaultPillarList;

        if (portalObject != null)
        {
            rounds.thePortal = portalObject.GetComponent<Portal>();
        }

        if (narrowsBarrierObject != null)
        {
            rounds.narrowsBarrier = narrowsBarrierObject.GetComponent<ZoneBarrier>();
        }
        if (hollowBarrierObject != null)
        {
            rounds.hollowBarrier = hollowBarrierObject.GetComponent<ZoneBarrier>();
        }

        director.AddComponent<HudDisplay>();
        director.AddComponent<CursorControl>();

        // The post-processing stack. Added before the lens, because the lens reads the
        // finished scene and this only affects the image drawn from it.
        director.AddComponent<ValleyAtmosphere>();
        director.AddComponent<VaultAtmosphere>();

        // Works out the walkable map once the world exists, so enemies can path around
        // the scenery instead of walking into it. See PATHFINDING_MATHS.md in the docs folder.
        director.AddComponent<NavigationField>();

        // The lens goes on last, because it repaints every renderer that already exists
        // in the scene and so needs the valley to be finished before it runs.
        director.AddComponent<StyleLens>();

        WireUpTheStory(director);
    }

    // Everything that turns four rounds and a boss into a journey with a beginning and
    // an end. Kept in one place so it is obvious what can be deleted to get the old
    // straight-to-the-fight behaviour back.
    private static void WireUpTheStory(GameObject director)
    {
        director.AddComponent<DialogueBox>();

        // Saving and loading, and the screens that drive them. GameProgress goes on
        // first because the menu asks it to start or resume a run.
        director.AddComponent<GameProgress>();

        StoryDirector story = director.AddComponent<StoryDirector>();
        story.doorOutOfTheDungeon = DungeonBuilder.doorOutOfTheDungeon;

        if (homewardPortalObject != null)
        {
            story.doorHomeFromTheVault = homewardPortalObject.GetComponent<Portal>();
        }
        if (theGateObject != null)
        {
            story.theGate = theGateObject.transform;
        }
        if (valleyOrrinObject != null)
        {
            story.orrinInTheValley = valleyOrrinObject.GetComponent<Wizard>();
        }

        director.AddComponent<CoachLines>();
        director.AddComponent<EndingSequence>();

        // Last, so that by the time its Awake runs everything it might have to start or
        // resume already exists.
        director.AddComponent<MainMenu>();
    }

    private static void SetTheLighting()
    {
        Light sun = Object.FindFirstObjectByType<Light>();
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(42f, 30f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
        }

        // A little haze makes the far end of the valley read as distant, which is most of
        // what sells a small space as a real place.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.62f, 0.63f, 0.60f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 230f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.48f, 0.53f, 0.60f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.36f, 0.37f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.19f, 0.17f);
    }

    // ----------------------------------------------------------------------------
    // Small helpers
    // ----------------------------------------------------------------------------

    // One visible piece of a creature. Colliders are stripped from every part, because
    // the CharacterController on the root is the only collider a character should have -
    // a second one would make each sword swing register twice.
    private static GameObject AddPart(GameObject parent, string name, PrimitiveType shape,
        Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.transform.SetParent(parent.transform);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().material = material;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Object.DestroyImmediate(partCollider);
        }

        return part;
    }

    public static Material MakeMaterial(Color colour)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            urpLit = Shader.Find("Standard");
        }

        Material material = new Material(urpLit);
        material.color = colour;
        if (material.HasProperty("_BaseColor") == true)
        {
            material.SetColor("_BaseColor", colour);
        }

        // Slightly rough and non-metallic, which is what makes untextured shapes look
        // like stone rather than plastic.
        if (material.HasProperty("_Smoothness") == true)
        {
            material.SetFloat("_Smoothness", 0.14f);
        }
        if (material.HasProperty("_Metallic") == true)
        {
            material.SetFloat("_Metallic", 0f);
        }

        return material;
    }

    // A real photographed surface, projected on from all three world axes so it needs no
    // texture coordinates. Used for the terrain and the cliffs, neither of which has
    // usable UVs.
    //
    // Falls back to the procedural noise material if the textures or the shader are
    // missing, so the valley degrades to "plainer" rather than to bright magenta.
    private static Material MakeTexturedRockMaterial(string textureFolder, string namePrefix,
        float metresPerTile, float normalStrength, Color tint,
        Color fallbackBase, Color fallbackSecond, Color fallbackCrevice)
    {
        Shader triplanar = Shader.Find("OneValley/TriplanarPBR");
        Texture2D albedo = Resources.Load<Texture2D>(textureFolder + "/" + namePrefix + "_albedo");
        Texture2D normal = Resources.Load<Texture2D>(textureFolder + "/" + namePrefix + "_normal");
        Texture2D rough = Resources.Load<Texture2D>(textureFolder + "/" + namePrefix + "_rough");
        Texture2D occlusion = Resources.Load<Texture2D>(textureFolder + "/" + namePrefix + "_ao");

        if (triplanar == null || albedo == null)
        {
            Debug.LogWarning("Triplanar shader or " + namePrefix
                + " textures missing - falling back to procedural rock.");
            return MakeRockMaterial(fallbackBase, fallbackSecond, fallbackCrevice,
                1.2f, normalStrength);
        }

        Material material = new Material(triplanar);
        material.SetTexture("_AlbedoMap", albedo);
        if (normal != null)
        {
            material.SetTexture("_NormalMap", normal);
        }
        if (rough != null)
        {
            material.SetTexture("_RoughMap", rough);
        }
        if (occlusion != null)
        {
            material.SetTexture("_OcclusionMap", occlusion);
        }

        material.SetColor("_Tint", tint);
        // Tiling is expressed as tiles per metre, so a smaller number means the texture
        // is stretched wider across the world.
        material.SetFloat("_Tiling", metresPerTile);
        material.SetFloat("_BlendSharpness", 5f);
        material.SetFloat("_NormalStrength", normalStrength);
        material.SetFloat("_RoughnessScale", 1f);
        material.SetFloat("_OcclusionStrength", 0.85f);

        return material;
    }

    // A surface that generates its own detail from noise instead of a painted texture.
    // Falls back to a plain lit material if the shader failed to compile, so a broken
    // shader makes the valley plain rather than bright magenta.
    public static Material MakeRockMaterial(Color baseColour, Color secondColour,
        Color creviceColour, float patternScale, float bumpiness)
    {
        Shader rockShader = Shader.Find("OneValley/ProceduralRock");
        if (rockShader == null)
        {
            Debug.LogWarning("ProceduralRock shader not found - falling back to flat colour.");
            return MakeMaterial(baseColour);
        }

        Material material = new Material(rockShader);
        material.SetColor("_BaseColor", baseColour);
        material.SetColor("_SecondColor", secondColour);
        material.SetColor("_CrackColor", creviceColour);
        material.SetFloat("_NoiseScale", patternScale);
        material.SetFloat("_NoiseContrast", 1.25f);
        material.SetFloat("_CrackDepth", 0.5f);
        material.SetFloat("_BumpStrength", bumpiness);
        material.SetFloat("_Smoothness", 0.06f);

        return material;
    }

    public static Material MakeGlowingMaterial(Color colour, float glowStrength)
    {
        Material material = MakeMaterial(colour);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", colour * glowStrength);
        return material;
    }

    // A slab of solid rock under a mesh floor, spanning the whole of it.
    //
    // A non-convex MeshCollider is not a solid in the way a cube is. PhysX treats it as an
    // infinitely thin, one-sided SHEET: it stops something landing on it from above, and
    // to something that has got beneath it, it is not there at all. Under the valley floor
    // there was then nothing whatsoever - no geometry of any kind, all the way down - so a
    // body that ended up on the wrong side of the sheet fell for ever.
    //
    // This is the backstop rather than the fix. HoldAboveTheFloor on each character is
    // what stops bodies getting under the sheet in the first place. What the slab buys is
    // that IF one ever does, it lands on rock a few metres down where the rescue can see
    // it and undo it, instead of falling out of the world entirely.
    //
    // Deliberately NOT walkable and NOT visible: it is left out of the navigation bake,
    // because the baker collects physics colliders and would otherwise lay a second floor
    // down here and let enemies path about underneath the world quite happily.
    private static void AddBedrockUnder(GameObject parent, string name, GameObject floor)
    {
        // PlaceModel hands back null when the model it wanted is missing from Resources,
        // and it has already said so in the console. There is no floor to put a slab
        // under in that case.
        if (floor == null)
        {
            return;
        }

        Renderer[] pieces = floor.GetComponentsInChildren<Renderer>();
        if (pieces.Length == 0)
        {
            Debug.LogWarning("No renderers under " + floor.name + ", so no bedrock was "
                + "placed beneath it. Anything pushed through that floor will fall.");
            return;
        }

        Bounds covered = pieces[0].bounds;
        int index = 1;
        while (index < pieces.Length)
        {
            covered.Encapsulate(pieces[index].bounds);
            index = index + 1;
        }

        // Far enough below that nothing standing in the deepest dip of the floor is ever
        // resting on the slab instead, and thick enough that nothing falling at gravity's
        // 22 m/s can cross it between two frames.
        const float ClearanceBelowTheFloor = 4f;
        const float SlabThickness = 6f;
        const float OverhangOnEverySide = 20f;

        GameObject bedrock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bedrock.name = name;
        bedrock.transform.SetParent(parent.transform);
        bedrock.transform.position = new Vector3(
            covered.center.x,
            covered.min.y - ClearanceBelowTheFloor - SlabThickness * 0.5f,
            covered.center.z);

        // Wider than the floor above it on every side, so a body shoved out sideways at
        // the very lip of the valley still has something underneath it.
        bedrock.transform.localScale = new Vector3(
            covered.size.x + OverhangOnEverySide,
            SlabThickness,
            covered.size.z + OverhangOnEverySide);

        // Never seen by anybody - it lives under the floor. Only the collider matters,
        // and a renderer down here would be a few thousand pixels of grey drawn every
        // frame for nothing.
        bedrock.GetComponent<Renderer>().enabled = false;

        NavMeshModifier leaveItOutOfThePathfinding = bedrock.AddComponent<NavMeshModifier>();
        leaveItOutOfThePathfinding.ignoreFromBuild = true;
    }

    // The two controller settings that decide how hard a crowded body gets shoved.
    //
    // Both are left at Unity's defaults unless something says otherwise, and both defaults
    // are wrong for creatures this small.
    private static void TuneControllerAgainstBeingSquashedThroughTheFloor(
        CharacterController controller)
    {
        // A tenth of the radius, which is the ratio Unity's own documentation asks for.
        // The default is a flat 0.08 m whatever the body: on the Darter, radius 0.42 and
        // near enough a sphere already, that is a fifth of the body. Two of those wedged
        // together are depenetrated by a long way in a single step, and a step longer
        // than the capsule's radius is exactly what puts its centre on the far side of a
        // floor that is only a sheet.
        controller.skinWidth = controller.radius * 0.1f;

        // Tiny corrections are applied rather than thrown away. The default of 0.001 m
        // discards precisely the small nudges that would ease a body out of geometry it
        // is barely wedged in - so instead it stays wedged, the overlap grows, and when
        // it finally does resolve it resolves violently.
        controller.minMoveDistance = 0f;
    }

    public static GameObject MakeBox(GameObject parent, string name, Vector3 where, Vector3 scale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent.transform);
        box.transform.position = where;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().material = material;
        return box;
    }

    public static GameObject MakeCylinder(GameObject parent, string name, Vector3 where, Vector3 scale, Material material)
    {
        GameObject cylinder = MakeRawCylinder(name, scale, material);
        cylinder.transform.SetParent(parent.transform);
        cylinder.transform.position = where;
        return cylinder;
    }

    private static GameObject MakeRawCylinder(string name, Vector3 scale, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        // A Unity cylinder is two units tall by default, so the vertical scale is halved
        // to make the numbers passed in mean actual metres.
        cylinder.transform.localScale = new Vector3(scale.x, scale.y * 0.5f, scale.z);
        cylinder.GetComponent<Renderer>().material = material;

        // Unity gives a cylinder primitive a CAPSULE collider, not a cylindrical one.
        // Flattened out to a wide disc that capsule becomes an enormous invisible dome,
        // and anything standing on it gets lifted metres into the air. Every cylinder
        // here is decoration, so the collider simply goes.
        Collider misleadingCollider = cylinder.GetComponent<Collider>();
        if (misleadingCollider != null)
        {
            Object.DestroyImmediate(misleadingCollider);
        }

        return cylinder;
    }
}
