"""Colour every character with the bible palette and light them properly.

Flat grey clay is genuinely hard to judge - it hides good proportion and flatters bad
proportion equally. This assigns a material per body part from the hex values in
ASSET_BIBLE.md section 0.4, lights the scene, and switches the viewport to Material
Preview so the models can be looked at in colour directly in Blender.

These materials are for LOOKING ONLY. BLENDER_ASSET_PLAN.md is explicit that Blender
materials do not survive into URP cleanly, so nothing here is exported; Unity assigns its
own material and keeps the models compatible with the style lens.

    python Tools/blender_send.py Tools/preview_coloured.py
"""

exec(open(r"C:/Users/Mark Alejandro/OneValley/OneValley-Transfer/unity-project/Tools/ov_kit.py").read())


def srgb_to_linear(channel):
    """Blender wants linear colour; hex codes are sRGB. Without this every colour
    renders noticeably washed out and lighter than the palette intends."""
    if channel <= 0.04045:
        return channel / 12.92
    return ((channel + 0.055) / 1.055) ** 2.4


def hex_colour(code):
    code = code.lstrip("#")
    red = int(code[0:2], 16) / 255.0
    green = int(code[2:4], 16) / 255.0
    blue = int(code[4:6], 16) / 255.0
    return (srgb_to_linear(red), srgb_to_linear(green), srgb_to_linear(blue), 1.0)


def make_material(name, colour, roughness=0.75, emission_strength=0.0):
    material = bpy.data.materials.get(name)
    if material is not None:
        return material

    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = colour
    shader.inputs["Roughness"].default_value = roughness
    if "Metallic" in shader.inputs:
        shader.inputs["Metallic"].default_value = 0.0

    if emission_strength > 0.0:
        if "Emission Color" in shader.inputs:
            shader.inputs["Emission Color"].default_value = colour
        elif "Emission" in shader.inputs:
            shader.inputs["Emission"].default_value = colour
        shader.inputs["Emission Strength"].default_value = emission_strength

    return material


# ----------------------------------------------------------------------------------
# Palette, straight from ASSET_BIBLE.md 0.4
# ----------------------------------------------------------------------------------

LEATHER = make_material("Leather", hex_colour("#5C4632"), roughness=0.85)
DARK_METAL = make_material("DarkMetal", hex_colour("#3A3A42"), roughness=0.45)
PALE_METAL = make_material("PaleMetal", hex_colour("#8A8577"), roughness=0.40)
SKIN = make_material("Skin", hex_colour("#C89B7B"), roughness=0.80)
HAIR = make_material("Hair", hex_colour("#3A2E28"), roughness=0.90)
ENEMY_EYE = make_material("EnemyEye", hex_colour("#FFD94D"), emission_strength=4.0)
VAULT_VIOLET = make_material("VaultViolet", hex_colour("#8C38F2"), emission_strength=5.0)

CHARACTERS = [
    ("DarterSegmented.fbx", "Darter", "#8C5238", False),
    ("PlayerSegmented.fbx", "Player", "#4D6B9E", True),
    ("SpitterSegmented.fbx", "Spitter", "#5C8042", False),
    ("GruntSegmented.fbx", "Grunt", "#6B5B47", False),
    ("WardenSegmented.fbx", "Warden", "#2B2B33", False),
]


def material_for(part_name, body_material, is_player):
    """Pick a material from the part's name.

    Names come from the build scripts and are already descriptive, so the rules read as
    what they are rather than as a lookup table.
    """
    lowered = part_name.lower()

    if "eye" in lowered:
        return ENEMY_EYE
    if "headslot" in lowered:
        return VAULT_VIOLET
    if "hair" in lowered:
        return HAIR
    if "buckle" in lowered:
        return PALE_METAL
    if "guard" in lowered or "slot" in lowered:
        return DARK_METAL
    for word in ("belt", "strap", "bracer", "cuff", "pouch", "wrap", "foot", "boot"):
        if word in lowered:
            return LEATHER
    if is_player:
        # Only the traveller has bare skin worth showing; the creatures are hide all over.
        for word in ("head", "hand", "brow", "nose"):
            if word in lowered:
                return SKIN

    return body_material


clear_scene()

cursor_x = 0.0
everything = []

for filename, label, body_hex, is_player in CHARACTERS:
    path = MODEL_DIR + "/" + filename
    if not os.path.exists(path):
        print(label + ": missing")
        continue

    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    arrived = [o for o in bpy.context.scene.objects
               if o not in before and o.type == "MESH"]

    body_material = make_material(label + "Body", hex_colour(body_hex), roughness=0.82)

    all_x, all_z = [], []
    for obj in arrived:
        obj.data.materials.clear()
        obj.data.materials.append(material_for(obj.name, body_material, is_player))
        for index in range(8):
            corner = obj.matrix_world @ _bbox_corner(obj, index)
            all_x.append(corner[0])
            all_z.append(corner[2])

    width = max(all_x) - min(all_x)
    shift_x = cursor_x - min(all_x)
    shift_z = -min(all_z)

    for obj in arrived:
        if obj.parent is None or obj.parent not in arrived:
            obj.location = (obj.location[0] + shift_x,
                            obj.location[1],
                            obj.location[2] + shift_z)

    everything.extend(arrived)
    print("%-9s coloured %s, %2d parts" % (label, body_hex, len(arrived)))
    cursor_x = cursor_x + width + 0.55

# ----------------------------------------------------------------------------------
# A floor and three lights, so the models sit somewhere rather than float in the void
# ----------------------------------------------------------------------------------

bpy.ops.mesh.primitive_plane_add(size=40.0, location=(cursor_x * 0.5, 0.0, 0.0))
floor = bpy.context.active_object
floor.name = "PreviewFloor"
floor.data.materials.append(make_material("FloorStone", hex_colour("#4C4750"), roughness=0.95))

key = bpy.data.lights.new("Key", type="AREA")
key.energy = 900.0
key.size = 6.0
key_object = bpy.data.objects.new("Key", key)
key_object.location = (cursor_x * 0.5 - 3.0, -6.0, 6.0)
key_object.rotation_euler = (math.radians(55.0), 0.0, math.radians(-28.0))
bpy.context.collection.objects.link(key_object)

fill = bpy.data.lights.new("Fill", type="AREA")
fill.energy = 260.0
fill.size = 8.0
fill.color = (0.72, 0.78, 1.0)
fill_object = bpy.data.objects.new("Fill", fill)
fill_object.location = (cursor_x * 0.5 + 5.0, -4.0, 3.0)
fill_object.rotation_euler = (math.radians(72.0), 0.0, math.radians(52.0))
bpy.context.collection.objects.link(fill_object)

rim = bpy.data.lights.new("Rim", type="AREA")
rim.energy = 500.0
rim.size = 5.0
rim.color = (1.0, 0.72, 0.42)
rim_object = bpy.data.objects.new("Rim", rim)
rim_object.location = (cursor_x * 0.5, 6.5, 4.0)
rim_object.rotation_euler = (math.radians(-115.0), 0.0, 0.0)
bpy.context.collection.objects.link(rim_object)

world = bpy.context.scene.world
if world is None:
    world = bpy.data.worlds.new("PreviewWorld")
    bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.055, 0.058, 0.070, 1.0)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.9

# ----------------------------------------------------------------------------------
# Render, and put the live viewport into Material Preview so it can be orbited in colour
# ----------------------------------------------------------------------------------

scene = bpy.context.scene
for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        continue

scene.render.resolution_x = 1500
scene.render.resolution_y = 700
scene.render.film_transparent = False
scene.view_settings.view_transform = "Standard"

centre, extent = bounds_of(everything)

camera_data = bpy.data.cameras.new("ColourCamera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = extent * 1.04
camera = bpy.data.objects.new("ColourCamera", camera_data)
bpy.context.collection.objects.link(camera)
scene.camera = camera
camera.location = (centre[0] + extent * 0.55, centre[1] - extent * 2.2, centre[2] + extent * 0.30)
camera.rotation_euler = (math.radians(82.0), 0.0, math.radians(14.0))

os.makedirs(PREVIEW_DIR, exist_ok=True)
scene.render.filepath = PREVIEW_DIR + "/cast_coloured.png"
bpy.ops.render.render(write_still=True)
print("")
print("wrote " + scene.render.filepath)

switched = 0
for area in bpy.context.screen.areas:
    if area.type == "VIEW_3D":
        for space in area.spaces:
            if space.type == "VIEW_3D":
                space.shading.type = "MATERIAL"
                switched += 1

print("viewport set to Material Preview in " + str(switched) + " editor(s).")
print("Press Home in the viewport to frame everything; orbit freely.")
print("Materials are PREVIEW ONLY - nothing here is exported.")
