"""Import every segmented character into one Blender scene, side by side.

Each build script clears the scene before it works, so the viewport only ever holds the
last creature built. This loads all five back from their exported FBXs and lines them up
on a shared baseline, which is also the only way to check the thing that actually matters
about this cast: that they are unmistakable from one another as silhouettes.

Nothing here is exported. It is purely for looking at.

    python Tools/blender_send.py Tools/show_all_characters.py
"""

exec(open(r"C:/Users/Mark Alejandro/OneValley/OneValley-Transfer/unity-project/Tools/ov_kit.py").read())

clear_scene()

# Smallest to largest, left to right, so the size relationships read at a glance.
LINEUP = [
    ("DarterSegmented.fbx", "Darter"),
    ("PlayerSegmented.fbx", "Player"),
    ("SpitterSegmented.fbx", "Spitter"),
    ("GruntSegmented.fbx", "Grunt"),
    ("WardenSegmented.fbx", "Warden"),
]

GAP = 0.55          # clear space between neighbours
cursor_x = 0.0
everything = []

for filename, label in LINEUP:
    path = MODEL_DIR + "/" + filename
    if not os.path.exists(path):
        print(label + ": missing (" + filename + ")")
        continue

    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    arrived = [o for o in bpy.context.scene.objects
               if o not in before and o.type == "MESH"]

    if not arrived:
        print(label + ": imported nothing")
        continue

    # Measure the creature where it landed, then shift it so its left edge sits at the
    # cursor and its feet sit on z = 0.
    all_x, all_z = [], []
    for obj in arrived:
        for index in range(8):
            corner = obj.matrix_world @ _bbox_corner(obj, index)
            all_x.append(corner[0])
            all_z.append(corner[2])

    width = max(all_x) - min(all_x)
    height = max(all_z) - min(all_z)

    shift_x = cursor_x - min(all_x)
    shift_z = -min(all_z)

    # Only the roots move; children follow their parents.
    for obj in arrived:
        if obj.parent is None or obj.parent not in arrived:
            obj.location = (obj.location[0] + shift_x,
                            obj.location[1],
                            obj.location[2] + shift_z)

    everything.extend(arrived)
    print("%-9s %5.2f m tall, %5.2f m wide, %3d parts   at x = %.2f"
          % (label, height, width, len(arrived), cursor_x))

    cursor_x = cursor_x + width + GAP

print("")
print("Scene now holds " + str(len(everything)) + " objects across "
      + str(len(LINEUP)) + " characters.")
print("In Blender: hover the viewport and press Home to frame everything.")

if everything:
    for path in render_group(everything, "cast_lineup",
                             views=[("front", 0.0), ("three_quarter", 30.0)],
                             frame_padding=1.06):
        print("wrote " + path)
