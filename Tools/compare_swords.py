"""Render Sword.fbx and WardensEdge.fbx together at true scale.

WPN-04's brief makes a comparative claim - "clearly a far greater weapon than a plain
sword" - so it can only be checked with a comparative image. Each rendered alone and
fitted to the frame, the two look almost identical; that tells you nothing.

Re-imports the exported FBXs rather than rebuilding, so this also confirms the exports
are valid and land at the right size after the axis conversion.
"""

exec(open(r"C:/Users/Mark Alejandro/OneValley/OneValley-Transfer/unity-project/Tools/ov_kit.py").read())

clear_scene()


def import_and_gather(filename, shift_x):
    """Import one FBX, merge whatever came in, and move it sideways."""
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=MODEL_DIR + "/" + filename)
    arrived = [o for o in bpy.context.scene.objects if o not in before and o.type == "MESH"]

    if len(arrived) > 1:
        merged = join_all(arrived, filename.replace(".fbx", ""))
    else:
        merged = arrived[0]

    # Drop it to sit on z = 0 so both weapons share a baseline and the height
    # difference is honest rather than an artefact of where the pivot happens to be.
    lowest = min((merged.matrix_world @ _bbox_corner(merged, i))[2] for i in range(8))
    merged.location = (shift_x, 0.0, merged.location[2] - lowest)
    return merged


sword = import_and_gather("Sword.fbx", -0.28)
edge = import_and_gather("WardensEdge.fbx", 0.28)


def measure(obj, label):
    corners = [obj.matrix_world @ _bbox_corner(obj, i) for i in range(8)]
    height = max(c[2] for c in corners) - min(c[2] for c in corners)
    span = max(c[0] for c in corners) - min(c[0] for c in corners)
    print(label + ": " + str(round(height, 3)) + " m tall, "
          + str(round(span, 3)) + " m guard span, "
          + str(triangle_count(obj)) + " tris")
    return height


sword_height = measure(sword, "Sword      ")
edge_height = measure(edge, "WardensEdge")
print("Edge is " + str(round(edge_height / sword_height, 2)) + "x the sword's length")

setup_preview_render()
scene = bpy.context.scene
scene.render.resolution_x = 1100
scene.render.resolution_y = 900

camera_data = bpy.data.cameras.new("CompareCamera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = 2.1
camera = bpy.data.objects.new("CompareCamera", camera_data)
bpy.context.collection.objects.link(camera)
scene.camera = camera

camera.location = (0.0, -4.0, 0.95)
camera.rotation_euler = (math.radians(90.0), 0.0, 0.0)

os.makedirs(PREVIEW_DIR, exist_ok=True)
scene.render.filepath = PREVIEW_DIR + "/sword_vs_edge.png"
bpy.ops.render.render(write_still=True)
print("wrote " + scene.render.filepath)
