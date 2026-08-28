using UnityEngine;

// The room the game now starts in.
//
// It is a sealed box a long way south of the valley, built the same way the Vault is: a
// separate place in the same scene, reached by a teleport rather than by a loading
// screen. Nothing in here can hurt the player and nothing in here moves except Orrin.
// That is the point - it is the only ninety seconds of the demo where somebody can look
// around, read something, and decide to do it.
//
// It is made of the same boxes and the same materials as the valley, borrowed from
// ValleyBuilder rather than copied, so the two cannot drift apart.
public static class DungeonBuilder
{
    // Far to the SOUTH, mirroring the Vault far to the north. Nothing between here and
    // the valley, so there is no chance of the player wandering out of one into the
    // other on foot.
    public static readonly Vector3 DungeonOrigin = new Vector3(0f, 0f, -200f);

    // All of these are measured from the origin above.
    private const float RoomHalfWidth = 17f;
    private const float RoomHalfLength = 16f;
    private const float RoomHeight = 9f;

    public static readonly Vector3 PlayerStandsAt = new Vector3(0f, 1.0f, -12f);
    private static readonly Vector3 OrrinStandsAt = new Vector3(0f, 0f, 7f);
    private static readonly Vector3 DoorStandsAt = new Vector3(0f, 0f, 13.5f);

    // Handed back to the valley builder so it can wire the story together.
    public static Portal doorOutOfTheDungeon;
    public static Wizard orrin;

    public static void BuildTheDungeon(GameObject root)
    {
        doorOutOfTheDungeon = null;
        orrin = null;

        GameObject dungeon = new GameObject("TheDungeon");
        dungeon.transform.SetParent(root.transform);
        dungeon.transform.position = DungeonOrigin;

        // Plain lit materials rather than the valley's procedural rock shader.
        //
        // The rock shader is written for a place with a sun in it. Down here there is no
        // sun - the ceiling is in the way - so a surface is lit only by the braziers and
        // by ambient light, and the rock shader answers ambient light with almost
        // nothing. The room came out very nearly black no matter how far the brazier
        // brightness was pushed, which is a lighting problem that looks exactly like a
        // brightness problem and wasted a while being treated as one.
        //
        // These are noticeably lighter than the valley's stone as well. A wall that is
        // dark grey in daylight is invisible by firelight.
        Material stone = ValleyBuilder.MakeMaterial(new Color(0.33f, 0.31f, 0.36f));
        Material floorStone = ValleyBuilder.MakeMaterial(new Color(0.26f, 0.25f, 0.29f));

        BuildTheShell(dungeon, stone, floorStone);
        BuildThePillars(dungeon, stone);
        BuildTheBraziers(dungeon);
        BuildOrrin(dungeon);
        BuildTheDoorOut(dungeon, stone);
    }

    // ------------------------------------------------------------------------
    // The room
    // ------------------------------------------------------------------------

    private static void BuildTheShell(GameObject dungeon, Material stone, Material floorStone)
    {
        Vector3 origin = DungeonOrigin;

        // Floor. Its top surface sits exactly at the origin height, so everything else in
        // here can be positioned as though the ground were at zero.
        ValleyBuilder.MakeBox(dungeon, "DungeonFloor",
            origin + new Vector3(0f, -0.5f, 0f),
            new Vector3(RoomHalfWidth * 2f, 1f, RoomHalfLength * 2f), floorStone);

        // A ceiling, which is most of what makes a room feel underground. Without one the
        // sky is visible overhead and the whole place reads as a walled courtyard.
        ValleyBuilder.MakeBox(dungeon, "DungeonCeiling",
            origin + new Vector3(0f, RoomHeight + 0.5f, 0f),
            new Vector3(RoomHalfWidth * 2f, 1f, RoomHalfLength * 2f), stone);

        ValleyBuilder.MakeBox(dungeon, "DungeonWallWest",
            origin + new Vector3(-RoomHalfWidth - 0.5f, RoomHeight * 0.5f, 0f),
            new Vector3(1f, RoomHeight, RoomHalfLength * 2f), stone);

        ValleyBuilder.MakeBox(dungeon, "DungeonWallEast",
            origin + new Vector3(RoomHalfWidth + 0.5f, RoomHeight * 0.5f, 0f),
            new Vector3(1f, RoomHeight, RoomHalfLength * 2f), stone);

        ValleyBuilder.MakeBox(dungeon, "DungeonWallSouth",
            origin + new Vector3(0f, RoomHeight * 0.5f, -RoomHalfLength - 0.5f),
            new Vector3(RoomHalfWidth * 2f + 2f, RoomHeight, 1f), stone);

        // The north wall is built in two halves with a gap between them, and the door
        // stands in the gap. A solid wall with a portal glued to it reads as a painting.
        float gapHalfWidth = 3.2f;
        float halfWallWidth = RoomHalfWidth - gapHalfWidth;

        ValleyBuilder.MakeBox(dungeon, "DungeonWallNorthWest",
            origin + new Vector3(-gapHalfWidth - halfWallWidth * 0.5f, RoomHeight * 0.5f, RoomHalfLength + 0.5f),
            new Vector3(halfWallWidth, RoomHeight, 1f), stone);

        ValleyBuilder.MakeBox(dungeon, "DungeonWallNorthEast",
            origin + new Vector3(gapHalfWidth + halfWallWidth * 0.5f, RoomHeight * 0.5f, RoomHalfLength + 0.5f),
            new Vector3(halfWallWidth, RoomHeight, 1f), stone);

        // The lintel fills everything above head height. Its first version left a band
        // of open sky between the top of the door frame and the ceiling, which is a very
        // strange thing to find underground.
        ValleyBuilder.MakeBox(dungeon, "DungeonDoorLintel",
            origin + new Vector3(0f, 7f, RoomHalfLength + 0.5f),
            new Vector3(gapHalfWidth * 2f, 4f, 1f), stone);

        // Stone behind the doorway, so what is framed by it is rock rather than daylight.
        // The door is a teleport and carries the player before they ever touch this, so
        // it being solid costs nothing and it is what turns the doorway into an alcove.
        ValleyBuilder.MakeBox(dungeon, "DungeonDoorBacking",
            origin + new Vector3(0f, 2.5f, RoomHalfLength + 1.6f),
            new Vector3(gapHalfWidth * 2f + 0.6f, 5f, 1f), stone);

        // Steps climbing towards the door, so the way out is obviously up and out rather
        // than through. Three shallow slabs is enough to read at a glance.
        int stepIndex = 0;
        while (stepIndex < 3)
        {
            float stepHeight = 0.28f * (stepIndex + 1);
            ValleyBuilder.MakeBox(dungeon, "DungeonStep" + stepIndex,
                origin + new Vector3(0f, stepHeight * 0.5f, 10.2f + stepIndex * 1.1f),
                new Vector3(7f, stepHeight, 1.1f), stone);
            stepIndex = stepIndex + 1;
        }
    }

    private static void BuildThePillars(GameObject dungeon, Material stone)
    {
        // Two rows down the length of the room. They do nothing except make the space
        // read as built rather than as an empty box, and give the braziers something to
        // throw shadows against.
        float[] pillarZ = new float[] { -9f, -2f, 5f };

        int index = 0;
        while (index < pillarZ.Length)
        {
            float z = pillarZ[index];

            ValleyBuilder.MakeBox(dungeon, "DungeonPillarWest" + index,
                DungeonOrigin + new Vector3(-11f, RoomHeight * 0.5f, z),
                new Vector3(1.6f, RoomHeight, 1.6f), stone);

            ValleyBuilder.MakeBox(dungeon, "DungeonPillarEast" + index,
                DungeonOrigin + new Vector3(11f, RoomHeight * 0.5f, z),
                new Vector3(1.6f, RoomHeight, 1.6f), stone);

            index = index + 1;
        }
    }

    // ------------------------------------------------------------------------
    // Light
    // ------------------------------------------------------------------------

    private static void BuildTheBraziers(GameObject dungeon)
    {
        // Warm, low and few. The room should be dim enough that Orrin's own light is the
        // brightest thing in it, because that is what makes the eye go to him.
        MakeOneBrazier(dungeon, new Vector3(-11f, 0f, -12f));
        MakeOneBrazier(dungeon, new Vector3(11f, 0f, -12f));
        MakeOneBrazier(dungeon, new Vector3(-11f, 0f, 1f));
        MakeOneBrazier(dungeon, new Vector3(11f, 0f, 1f));

        // One cold, weak fill from above. Braziers alone leave the floor between them
        // completely black, and a player who cannot see the floor cannot tell they are
        // allowed to walk on it. Deliberately blue against the orange firelight, so the
        // room still reads as lit by fire rather than by a lamp nobody can see.
        GameObject fillObject = new GameObject("DungeonFill");
        fillObject.transform.SetParent(dungeon.transform);
        // Kept well below the ceiling. Sitting close under it, this put a large bright
        // blob directly overhead that read as a hole in the roof.
        fillObject.transform.position = DungeonOrigin + new Vector3(0f, RoomHeight - 4.5f, -3f);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.color = new Color(0.42f, 0.48f, 0.72f);
        fill.intensity = 20f;
        fill.range = 52f;
    }

    private static void MakeOneBrazier(GameObject dungeon, Vector3 localPosition)
    {
        Material ironMaterial = ValleyBuilder.MakeMaterial(new Color(0.12f, 0.11f, 0.12f));
        // Emission over about two burns out to a white card with no colour in it, which
        // is what these looked like: sheets of paper balanced on posts.
        Material flameMaterial = ValleyBuilder.MakeGlowingMaterial(new Color(1f, 0.58f, 0.18f), 1.9f);

        Vector3 where = DungeonOrigin + localPosition;

        ValleyBuilder.MakeCylinder(dungeon, "BrazierStem",
            where + new Vector3(0f, 0.55f, 0f),
            new Vector3(0.28f, 1.1f, 0.28f), ironMaterial);

        ValleyBuilder.MakeCylinder(dungeon, "BrazierBowl",
            where + new Vector3(0f, 1.18f, 0f),
            new Vector3(0.95f, 0.26f, 0.95f), ironMaterial);

        GameObject flame = ValleyBuilder.MakeCylinder(dungeon, "BrazierFlame",
            where + new Vector3(0f, 1.38f, 0f),
            new Vector3(0.62f, 0.34f, 0.62f), flameMaterial);

        // A flame nobody can walk into. Left solid it becomes an invisible bollard in the
        // middle of the floor, and the capsule collider a flattened cylinder carries is
        // far bigger than the shape it is drawn as.
        Collider flameCollider = flame.GetComponent<Collider>();
        if (flameCollider != null)
        {
            Object.DestroyImmediate(flameCollider);
        }

        GameObject lightObject = new GameObject("BrazierLight");
        lightObject.transform.SetParent(dungeon.transform);
        lightObject.transform.position = where + new Vector3(0f, 1.9f, 0f);
        Light brazierLight = lightObject.AddComponent<Light>();
        brazierLight.type = LightType.Point;
        brazierLight.color = new Color(1f, 0.66f, 0.34f);
        // This pipeline measures point lights on a scale where the Vault's braziers sit
        // at forty over a range of forty-two. The first pass used numbers that suit the
        // old built-in renderer and the room came out nearly black; the second was still
        // reading as a cave at midnight because the range was half the Vault's.
        brazierLight.intensity = 52f;
        brazierLight.range = 30f;
    }

    // ------------------------------------------------------------------------
    // Orrin
    // ------------------------------------------------------------------------

    private static void BuildOrrin(GameObject dungeon)
    {
        GameObject wizard = MakeOrrinModel(dungeon, "Orrin", DungeonOrigin + OrrinStandsAt);

        // Turned to face back down the room, so he is looking at the player from the
        // moment the game starts.
        wizard.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        orrin = wizard.AddComponent<Wizard>();
        orrin.interactionRadius = 5.5f;
        orrin.answersWhenSpokenTo = true;
    }

    // Built once here and used twice: down in the dungeon at the start, and up in the
    // valley at the end.
    public static GameObject MakeOrrinModel(GameObject parent, string name, Vector3 where)
    {
        Material robeMaterial = ValleyBuilder.MakeMaterial(new Color(0.34f, 0.28f, 0.52f));
        Material trimMaterial = ValleyBuilder.MakeMaterial(new Color(0.56f, 0.46f, 0.78f));
        Material skinMaterial = ValleyBuilder.MakeMaterial(new Color(0.74f, 0.62f, 0.52f));
        Material staffMaterial = ValleyBuilder.MakeMaterial(new Color(0.30f, 0.22f, 0.15f));
        // Emission of four and a half burned out to a flat white ball with no colour in
        // it at all. Just under two keeps it obviously lit while staying violet.
        Material orbMaterial = ValleyBuilder.MakeGlowingMaterial(new Color(0.62f, 0.34f, 1f), 1.8f);

        GameObject wizard = new GameObject(name);
        wizard.transform.SetParent(parent.transform);
        wizard.transform.position = where;

        // The robe is a stack of thin discs that narrow gradually from hem to shoulder.
        //
        // Three fat tiers was the obvious way to do it and it looked like a wedding cake:
        // the eye reads a small number of big steps as stacked objects, and a large
        // number of small steps as one continuous surface. Twelve slices is enough to
        // cross that line, and it costs nothing that matters at this scale.
        const int RobeSlices = 12;
        const float RobeTopHeight = 1.78f;

        int slice = 0;
        while (slice < RobeSlices)
        {
            // Nought at the hem, one at the shoulder.
            float howFarUp = slice / (float)(RobeSlices - 1);

            float heightHere = howFarUp * RobeTopHeight;

            // Narrowing faster near the top than the bottom, which is how cloth hanging
            // off a pair of shoulders actually falls.
            float widthHere = Mathf.Lerp(0.98f, 0.52f, howFarUp * howFarUp);

            // Each disc is deliberately much taller than the gap to the next one, so
            // they bury each other's rims. At a half-height of 0.10 they only just
            // touched and the lit edges read as a stack of rings.
            AddPiece(wizard, "OrrinRobe" + slice, PrimitiveType.Cylinder,
                new Vector3(0f, heightHere, 0f),
                new Vector3(widthHere, 0.17f, widthHere), robeMaterial);

            slice = slice + 1;
        }

        AddPiece(wizard, "OrrinCollar", PrimitiveType.Cylinder,
            new Vector3(0f, 1.86f, 0f), new Vector3(0.50f, 0.045f, 0.50f), trimMaterial);

        AddPiece(wizard, "OrrinHead", PrimitiveType.Sphere,
            new Vector3(0f, 2.04f, 0.03f), new Vector3(0.30f, 0.34f, 0.30f), skinMaterial);

        // The hood sits OVER the back and top of the head rather than beside it. An
        // earlier version put a second sphere next to the first and he appeared to have
        // two heads. Pulled back and up so a face is still visible from the front.
        AddPiece(wizard, "OrrinHood", PrimitiveType.Sphere,
            new Vector3(0f, 2.12f, -0.12f), new Vector3(0.40f, 0.42f, 0.40f), robeMaterial);

        // A shoulder cowl joining the hood to the robe, so his head does not appear to be
        // balanced on top of a bottle.
        AddPiece(wizard, "OrrinCowl", PrimitiveType.Sphere,
            new Vector3(0f, 1.80f, -0.05f), new Vector3(0.62f, 0.34f, 0.58f), robeMaterial);

        AddPiece(wizard, "OrrinStaff", PrimitiveType.Cylinder,
            new Vector3(0.54f, 1.28f, 0.06f), new Vector3(0.07f, 1.28f, 0.07f), staffMaterial);

        GameObject orb = AddPiece(wizard, "OrrinOrb", PrimitiveType.Sphere,
            new Vector3(0.54f, 2.66f, 0.06f), new Vector3(0.27f, 0.27f, 0.27f), orbMaterial);

        // The orb is what the Wizard script breathes, and the light belongs to it.
        GameObject lightObject = new GameObject("OrrinLight");
        lightObject.transform.SetParent(orb.transform);
        lightObject.transform.localPosition = Vector3.zero;
        Light staffLight = lightObject.AddComponent<Light>();
        staffLight.type = LightType.Point;
        staffLight.color = new Color(0.60f, 0.38f, 1f);
        // Brighter than the braziers he is standing among, because the eye should go to
        // him the moment the game starts and there is nothing else in the room to say so.
        staffLight.intensity = 40f;
        staffLight.range = 26f;

        return wizard;
    }

    // Every piece of Orrin is decoration. None of them get colliders: he is talked to by
    // distance, and a robe made of solid cylinders would be a stack of bollards the
    // player can get wedged inside.
    private static GameObject AddPiece(GameObject parent, string name, PrimitiveType shape,
        Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(shape);
        piece.name = name;
        piece.transform.SetParent(parent.transform);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().material = material;

        Collider pieceCollider = piece.GetComponent<Collider>();
        if (pieceCollider != null)
        {
            Object.DestroyImmediate(pieceCollider);
        }

        return piece;
    }

    // ------------------------------------------------------------------------
    // The way out
    // ------------------------------------------------------------------------

    private static void BuildTheDoorOut(GameObject dungeon, Material stone)
    {
        Vector3 where = DungeonOrigin + DoorStandsAt;

        GameObject doorHolder = new GameObject("DungeonDoor");
        doorHolder.transform.SetParent(dungeon.transform);
        doorHolder.transform.position = where;

        // The frame is built out of the same stone as the walls, because this is a door
        // somebody cut into a room rather than a hole torn in reality. The valley portal
        // is the flashy one; this one is masonry.
        ValleyBuilder.MakeBox(doorHolder, "DoorPostWest",
            where + new Vector3(-2.3f, 2.2f, 0f), new Vector3(0.7f, 4.4f, 1.2f), stone);
        ValleyBuilder.MakeBox(doorHolder, "DoorPostEast",
            where + new Vector3(2.3f, 2.2f, 0f), new Vector3(0.7f, 4.4f, 1.2f), stone);
        ValleyBuilder.MakeBox(doorHolder, "DoorHead",
            where + new Vector3(0f, 4.6f, 0f), new Vector3(5.3f, 0.8f, 1.2f), stone);

        // Deeper and dimmer than the Vault's portal. In a dark room the brighter mix
        // blew out to flat white-pink and read as a missing texture rather than as light.
        Material surfaceMaterial = ValleyBuilder.MakeGlowingMaterial(new Color(0.34f, 0.15f, 0.68f), 1.5f);
        GameObject surface = ValleyBuilder.MakeBox(doorHolder, "DoorSurface",
            where + new Vector3(0f, 2.2f, 0f), new Vector3(3.9f, 4.4f, 0.18f), surfaceMaterial);

        // Walked through, not bumped into.
        Collider surfaceCollider = surface.GetComponent<Collider>();
        if (surfaceCollider != null)
        {
            Object.DestroyImmediate(surfaceCollider);
        }

        GameObject lampObject = new GameObject("DoorLight");
        lampObject.transform.SetParent(doorHolder.transform);
        lampObject.transform.position = where + new Vector3(0f, 2.6f, 0f);
        Light lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(0.62f, 0.36f, 1f);
        // Starts dark and is raised by the Portal script as the door opens.
        lamp.intensity = 0f;
        lamp.range = 26f;

        Portal door = doorHolder.AddComponent<Portal>();
        door.purpose = Portal.PurposeOutOfTheDungeon;
        door.destination = ValleyBuilder.PlayerStartPosition;
        door.activationRadius = 3.0f;
        door.SetSurface(surface.transform);
        door.SetLight(lamp);

        doorOutOfTheDungeon = door;
    }
}
