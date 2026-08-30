"""Import every weapon and prop into one Blender scene, side by side.

The companion to show_all_characters.py. Same idea: each build script clears the scene,
so this loads the exported FBXs back to see the whole set together and at true relative
scale - which is the only way to check the claim WPN-04 has to satisfy, that the Warden's
Edge looks obviously superior to the plain sword.

Nothing here is exported. It is purely for looking at.

    python Tools/blender_send.py Tools/show_all_props.py
"""

import os

# blender_send.py defines ONEVALLEY_ROOT before it ships this file over the socket. When
# Blender runs the file directly (--background --python) nothing is injected, so fall back
# to this file's own location. Either way no machine-specific path is baked in.
if "ONEVALLEY_ROOT" in globals():
    PROJECT_ROOT = ONEVALLEY_ROOT
else:
    PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

exec(open(os.path.join(PROJECT_ROOT, "Tools", "ov_kit.py")).read())

clear_scene()

# Weapons first in the order the player meets them, then the props.
LINEUP = [
    ("Arrow.fbx", "Arrow"),
    ("Sword.fbx", "Sword"),
    ("Hammer.fbx", "Hammer"),
    ("Bow.fbx", "Bow"),
    ("WardensEdge.fbx", "WardensEdge"),
    ("Brazier.fbx", "Brazier"),
    ("EssenceShard.fbx", "EssenceShard"),
]

GAP = 0.26
cursor_x = 0.0
everything = []

for filename, label in LINEUP:
    path = MODEL_DIR + "/" + filename
    if not os.path.exists(path):
        print("%-13s missing (%s)" % (label, filename))
        continue

    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    arrived = [o for o in bpy.context.scene.objects
               if o not in before and o.type == "MESH"]

    if not arrived:
        print("%-13s imported nothing" % label)
        continue

    all_x, all_z = [], []
    for obj in arrived:
        for index in range(8):
            corner = obj.matrix_world @ _bbox_corner(obj, index)
            all_x.append(corner[0])
            all_z.append(corner[2])

    width = max(all_x) - min(all_x)
    height = max(all_z) - min(all_z)

    # Stand each one on z = 0 with its left edge at the cursor. The Warden's Edge is two
    # meshes - dark iron and the emissive core - so only unparented roots are moved and
    # anything parented follows.
    shift_x = cursor_x - min(all_x)
    shift_z = -min(all_z)

    for obj in arrived:
        if obj.parent is None or obj.parent not in arrived:
            obj.location = (obj.location[0] + shift_x,
                            obj.location[1],
                            obj.location[2] + shift_z)

    everything.extend(arrived)
    print("%-13s %5.2f m tall, %5.2f m wide, %2d mesh(es)   at x = %.2f"
          % (label, height, width, len(arrived), cursor_x))

    cursor_x = cursor_x + width + GAP

print("")
print("Scene now holds " + str(len(everything)) + " objects.")
print("In Blender: hover the viewport and press Home to frame everything.")

if everything:
    for path in render_group(everything, "props_lineup",
                             views=[("front", 0.0)],
                             frame_padding=1.05):
        print("wrote " + path)
