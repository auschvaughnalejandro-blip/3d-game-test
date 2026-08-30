"""List every part in every segmented character, with its parent.

This is the authoritative source for the handoff document - taken from the exported FBXs
rather than from the build scripts, so it is what Unity actually receives.
"""

exec(open(r"C:/Users/Mark Alejandro/OneValley/OneValley-Transfer/unity-project/Tools/ov_kit.py").read())

# The eleven names ProceduralAnimator.FindTheParts looks for. Anything else in a model is
# carried along by its parent but is never posed.
ANIMATED = {
    "Hips", "Torso", "Head",
    "ThighL", "ThighR", "ShinL", "ShinR",
    "UpperArmL", "UpperArmR", "ForearmL", "ForearmR",
}

for filename in ("GruntSegmented.fbx", "SpitterSegmented.fbx", "DarterSegmented.fbx",
                 "WardenSegmented.fbx", "PlayerSegmented.fbx"):
    path = MODEL_DIR + "/" + filename
    if not os.path.exists(path):
        print(filename + ": MISSING")
        continue

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=path)

    objects = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    roots = [o for o in objects if o.parent is None]

    print("")
    print("=== " + filename.replace("Segmented.fbx", "") + " (" + str(len(objects)) + " parts) ===")

    animated_found = []
    carried = []

    def walk(obj, depth):
        marker = "*" if obj.name in ANIMATED else " "
        print("  %s %s%s" % (marker, "  " * depth, obj.name))
        if obj.name in ANIMATED:
            animated_found.append(obj.name)
        else:
            carried.append(obj.name)
        for child in sorted(obj.children, key=lambda c: c.name):
            walk(child, depth + 1)

    for root in roots:
        walk(root, 0)

    missing = sorted(ANIMATED - set(animated_found))
    print("  animated by ProceduralAnimator: " + str(len(animated_found)) + "/11")
    if missing:
        print("  NOT PRESENT: " + ", ".join(missing))
    print("  carried (never posed): " + ", ".join(sorted(carried)))
