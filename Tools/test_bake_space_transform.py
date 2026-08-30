"""Does bake_space_transform=True actually destroy the segmented part origins?

The whole segmented approach depends on each part's object origin sitting exactly on the
joint it pivots around. build_grunt_segmented.py disables bake_space_transform with the
comment "must stay False, or the part origins are baked away" - a reasonable fear, since
Blender's own tooltip calls the option experimental.

But with baking off, Blender writes Z-up vertex data into a file labelled Y-up, and Unity
reads it sideways. That is why the Grunts lie on the ground and swim.

So the fear needs testing rather than trusting. Round-trips the existing export through a
baked re-export and prints the hierarchy of both, so the parent chain and the joint
positions can be compared directly.
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

import math

SOURCE = MODEL_DIR + "/GruntSegmented.fbx"
BAKED = PREVIEW_DIR + "/_bake_test.fbx"


def dump(label):
    objects = list(bpy.context.scene.objects)
    roots = [o for o in objects if o.parent is None]

    print("")
    print("--- " + label + " ---")
    print("roots: " + str([o.name for o in roots]) + "   objects: " + str(len(objects)))

    rows = []

    def walk(obj, depth):
        parent_name = obj.parent.name if obj.parent else "-"
        location = tuple(round(v, 4) for v in obj.location)
        rotation = tuple(round(math.degrees(v), 1) for v in obj.rotation_euler)
        scale = tuple(round(v, 3) for v in obj.scale)
        rows.append((obj.name, parent_name, location, rotation, scale))
        for child in obj.children:
            walk(child, depth + 1)

    for root in roots:
        walk(root, 0)

    for name, parent, location, rotation, scale in rows:
        print("%-12s parent=%-12s loc=%-24s rot=%-20s scale=%s"
              % (name, parent, str(location), str(rotation), str(scale)))

    meshes = [o for o in objects if o.type == "MESH"]
    all_z = []
    for obj in meshes:
        for index in range(8):
            all_z.append((obj.matrix_world @ _bbox_corner(obj, index))[2])
    if all_z:
        print("vertical extent: " + str(round(max(all_z) - min(all_z), 3)))

    return rows


clear_scene()
bpy.ops.import_scene.fbx(filepath=SOURCE)
before = dump("as exported today (bake_space_transform=False)")

# Re-export the very same hierarchy with baking on.
bpy.ops.object.select_all(action="SELECT")
roots = [o for o in bpy.context.scene.objects if o.parent is None]
bpy.context.view_layer.objects.active = roots[0]

os.makedirs(PREVIEW_DIR, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=BAKED,
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    object_types={"MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    bake_space_transform=True,
    axis_forward="-Z",
    axis_up="Y",
    path_mode="STRIP",
)

clear_scene()
bpy.ops.import_scene.fbx(filepath=BAKED)
after = dump("re-exported with bake_space_transform=True")

# The verdict: did every part keep its parent and its joint position?
print("")
print("=== COMPARISON ===")

before_by_name = {row[0]: row for row in before}
after_by_name = {row[0]: row for row in after}

missing = [name for name in before_by_name if name not in after_by_name]
if missing:
    print("PARTS LOST: " + str(missing))
else:
    print("all " + str(len(before_by_name)) + " parts survived")

parent_changes = 0
origin_changes = 0
for name in before_by_name:
    if name not in after_by_name:
        continue
    if before_by_name[name][1] != after_by_name[name][1]:
        parent_changes += 1
        print("  parent changed: " + name + "  " + before_by_name[name][1]
              + " -> " + after_by_name[name][1])

    before_location = before_by_name[name][2]
    after_location = after_by_name[name][2]
    drift = max(abs(a - b) for a, b in zip(before_location, after_location))
    if drift > 0.002:
        origin_changes += 1
        print("  origin moved:   %-12s %s -> %s  (%.3f m)"
              % (name, str(before_location), str(after_location), drift))

print("parent changes: " + str(parent_changes))
print("origins moved:  " + str(origin_changes))
