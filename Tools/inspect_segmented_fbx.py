"""Print the exact hierarchy and transforms inside GruntSegmented.fbx.

The Grunts lie flat on the ground and swim. The theory was that the Z-up to Y-up
compensation rides on the model root, and that ValleyBuilder was clearing it. Preserving
the root rotation changed nothing, so the theory is wrong somewhere and it is time to
look at the file rather than reason about it.

Re-imports the exported FBX and dumps every object's parent, location and rotation, which
is the same structure Unity is handed.
"""

exec(open(r"C:/Users/Mark Alejandro/OneValley/OneValley-Transfer/unity-project/Tools/ov_kit.py").read())

import math

for filename in ("GruntSegmented.fbx", "SpitterSegmented.fbx"):
    clear_scene()

    path = MODEL_DIR + "/" + filename
    if not os.path.exists(path):
        print(filename + ": NOT FOUND")
        continue

    bpy.ops.import_scene.fbx(filepath=path)

    print("")
    print("=== " + filename + " ===")

    objects = list(bpy.context.scene.objects)

    roots = [o for o in objects if o.parent is None]
    print("root objects: " + str([o.name for o in roots]))
    print("total objects: " + str(len(objects)))
    print("")
    print("%-16s %-16s %-26s %-26s" % ("NAME", "PARENT", "LOCATION", "ROTATION (deg XYZ)"))

    def describe(obj, depth):
        parent_name = obj.parent.name if obj.parent else "-"
        location = tuple(round(value, 3) for value in obj.location)
        rotation = tuple(round(math.degrees(value), 1) for value in obj.rotation_euler)
        print("%-16s %-16s %-26s %-26s" % (
            ("  " * depth) + obj.name, parent_name, str(location), str(rotation)))

        for child in obj.children:
            describe(child, depth + 1)

    for root in roots:
        describe(root, 0)

    # The question that actually matters: is the creature standing up, and where are its
    # feet? A model lying on its back has almost no extent in the up axis and a lot in
    # the depth axis.
    all_x, all_y, all_z = [], [], []
    for obj in objects:
        if obj.type != "MESH":
            continue
        for index in range(8):
            corner = obj.matrix_world @ _bbox_corner(obj, index)
            all_x.append(corner[0])
            all_y.append(corner[1])
            all_z.append(corner[2])

    if all_z:
        print("")
        print("extent  X (width): " + str(round(max(all_x) - min(all_x), 3)))
        print("extent  Y (depth): " + str(round(max(all_y) - min(all_y), 3)))
        print("extent  Z (up):    " + str(round(max(all_z) - min(all_z), 3)))
        print("lowest  Z:         " + str(round(min(all_z), 3)))
