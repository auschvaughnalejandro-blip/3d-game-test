"""Every model in the project, in one Blender scene.

Replaces flipping between show_all_characters.py and show_all_props.py. Characters stand
in a back row, weapons and props in a front row, everything at true relative scale with
its feet or base on z = 0.

Seeing the whole set at once is not just convenience: the cast has to be distinguishable
as silhouettes, and the weapons have to read against each other at a glance. Neither
claim can be checked one model at a time.

Nothing here is exported. It is purely for looking at.

    python Tools/blender_send.py Tools/show_everything.py
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

CHARACTERS = [
    ("DarterSegmented.fbx", "Darter"),
    ("PlayerSegmented.fbx", "Player"),
    ("SpitterSegmented.fbx", "Spitter"),
    ("GruntSegmented.fbx", "Grunt"),
    ("WardenSegmented.fbx", "Warden"),
]

PROPS = [
    ("EssenceShard.fbx", "EssenceShard"),
    ("Arrow.fbx", "Arrow"),
    ("Sword.fbx", "Sword"),
    ("Hammer.fbx", "Hammer"),
    ("Bow.fbx", "Bow"),
    ("WardensEdge.fbx", "WardensEdge"),
    ("Brazier.fbx", "Brazier"),
]

# The prop row sits this far forward of the characters, so the two rows read as rows
# rather than as one crowd.
PROP_ROW_Y = -2.6


def load_row(lineup, row_y, gap):
    """Import each model in turn, standing it on the floor left-to-right along X."""
    cursor_x = 0.0
    loaded = []

    for filename, label in lineup:
        path = MODEL_DIR + "/" + filename
        if not os.path.exists(path):
            print("%-13s MISSING (%s)" % (label, filename))
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

        shift_x = cursor_x - min(all_x)
        shift_z = -min(all_z)

        # Only unparented roots move; children ride along with their parents, which is
        # what keeps a segmented character's joint hierarchy intact.
        for obj in arrived:
            if obj.parent is None or obj.parent not in arrived:
                obj.location = (obj.location[0] + shift_x,
                                obj.location[1] + row_y,
                                obj.location[2] + shift_z)

        loaded.extend(arrived)
        print("%-13s %5.2f m tall, %5.2f m wide, %2d mesh(es)  at x = %.2f"
              % (label, height, width, len(arrived), cursor_x))

        cursor_x = cursor_x + width + gap

    return loaded, cursor_x


print("--- characters (back row) ---")
character_objects, character_span = load_row(CHARACTERS, 0.0, 0.55)

print("")
print("--- weapons and props (front row) ---")
prop_objects, prop_span = load_row(PROPS, PROP_ROW_Y, 0.26)

everything = character_objects + prop_objects

print("")
print("Scene holds " + str(len(everything)) + " objects: "
      + str(len(CHARACTERS)) + " characters, " + str(len(PROPS)) + " props.")
print("Character row spans " + str(round(character_span, 2)) + " m; prop row "
      + str(round(prop_span, 2)) + " m.")
print("")
print("In Blender: hover the viewport and press Home to frame everything.")
print("Numpad 1 = front, Numpad 3 = side, Numpad 7 = top.")
print("Nothing here exports - edits made in this scene do not reach the game.")

if everything:
    for path in render_group(everything, "everything",
                             views=[("front", 0.0), ("three_quarter", 34.0)],
                             frame_padding=1.04):
        print("wrote " + os.path.basename(path))
