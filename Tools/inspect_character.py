"""Check whether an existing character FBX is fit to hand to Mixamo's auto-rigger.

Mixamo needs a roughly humanoid mesh with the arms held out away from the body, so it
can tell an arm from a ribcage. It is tolerant of low poly counts and of a mesh built
from separate pieces, but it is not tolerant of arms down at the sides.

Reports the things that decide pass or fail, then renders a front view.
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

TARGET = "Player"

clear_scene()

bpy.ops.import_scene.fbx(filepath=MODEL_DIR + "/" + TARGET + ".fbx")

objects = [o for o in bpy.context.scene.objects if o.type == "MESH"]
armatures = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]

print("=== " + TARGET + ".fbx ===")
print("mesh objects: " + str(len(objects)))
print("armatures:    " + str(len(armatures)))

total_triangles = 0
total_vertices = 0
for obj in objects:
    total_triangles += triangle_count(obj)
    total_vertices += len(obj.data.vertices)

print("triangles:    " + str(total_triangles))
print("vertices:     " + str(total_vertices))

# Count disconnected islands across every mesh. A humanoid modelled as one continuous
# skin has one island; a humanoid assembled from stacked primitives has one per lump.
island_total = 0
for obj in objects:
    bm = bmesh.new()
    bm.from_mesh(obj.data)

    seen = set()
    for vertex in bm.verts:
        if vertex.index in seen:
            continue
        island_total += 1
        stack = [vertex]
        seen.add(vertex.index)
        while stack:
            current = stack.pop()
            for edge in current.link_edges:
                other = edge.other_vert(current)
                if other.index not in seen:
                    seen.add(other.index)
                    stack.append(other)
    bm.free()

print("loose parts:  " + str(island_total))

# Overall proportions. Arms held out sideways make the mesh nearly as wide as it is
# tall; arms down at the sides make it roughly a third as wide. That ratio is the
# single best signal for whether Mixamo will succeed.
all_x = []
all_y = []
all_z = []
for obj in objects:
    for corner_index in range(8):
        corner = obj.matrix_world @ _bbox_corner(obj, corner_index)
        all_x.append(corner[0])
        all_y.append(corner[1])
        all_z.append(corner[2])

width = max(all_x) - min(all_x)
depth = max(all_y) - min(all_y)
height = max(all_z) - min(all_z)

print("height:       " + str(round(height, 3)))
print("width:        " + str(round(width, 3)))
print("depth:        " + str(round(depth, 3)))
print("width/height: " + str(round(width / height, 3)) + "   (T-pose is ~0.85-1.05, arms-down is ~0.30)")

if len(objects) > 1:
    merged = join_all(objects, TARGET + "_preview")
else:
    merged = objects[0]

paths = render_views(merged, TARGET.lower() + "_inspect", views=[("front", 0.0), ("side", 90.0)])
for path in paths:
    print("preview:      " + path)
