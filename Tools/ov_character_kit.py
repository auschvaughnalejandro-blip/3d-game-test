"""Shared machinery for building segmented characters.

Everything here was worked out while building the Spitter and is kept separate from
ov_kit.py so that build_grunt_segmented.py, which the other session wrote against the
original kit, is not disturbed.

The one rule the whole approach rests on: a part's mesh is built hanging from its own
origin, and the object is then moved to the joint it pivots around. Get that wrong and a
limb rotates around its middle or its far end and the creature turns itself inside out.

Two lessons are baked into the defaults:

- Limbs need MORE THAN TWO RADII. A straight taper between two numbers can only make a
  cone, and a cone the thickness of an arm reads as a pipe. Variation along the length -
  narrow at the hinge, swelling through the muscle - is what makes a limb read as a limb.
- Limbs need to be CANTED. A part hanging exactly parallel to the torso reads as a pillar
  standing beside the creature rather than as something attached to it. The angle has to
  be built into the geometry, because ProceduralAnimator assigns localRotation outright
  and would erase a rest rotation.
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

DEFAULT_SIDES = 6


def new_character():
    """Start a fresh scene and return the empty part registry for one creature."""
    clear_scene()
    return {}


def shaped_part(registry, name, rings, joint, sides=DEFAULT_SIDES, roughen_amount=0.008,
                seed=0, direction=(0.0, 0.0, -1.0)):
    """A part built from a profile of (distance from the joint, radius) pairs.

    `direction` is the axis the part runs along, from its joint outward. Rings are laid
    perpendicular to it, so a limb can cant outward, a torso can lean forward, and a tail
    can sweep back, all with the joint still exactly on the pivot.
    """
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    # A short stub above the joint, so the parent's socket stays filled when the part
    # rotates and no daylight opens at the joint. Kept small: at 0.85 of the radius it
    # put a visible bulge above every joint on a thick limb.
    overhang = rings[0][1] * 0.35
    full = [(-overhang, rings[0][1])] + [(distance, radius) for distance, radius in rings]

    axis = _Vector(direction).normalised()
    seed_vector = _Vector((0.0, -1.0, 0.0))
    if abs(axis.dot(seed_vector)) > 0.95:
        seed_vector = _Vector((1.0, 0.0, 0.0))
    side = axis.cross(seed_vector).normalised()
    up = axis.cross(side).normalised()

    made_rings = []
    for distance, radius in full:
        centre = axis.scaled(distance)
        ring = []
        for step in range(sides):
            angle = 2.0 * math.pi * step / sides
            point = (centre
                     + side.scaled(math.cos(angle) * radius)
                     + up.scaled(math.sin(angle) * radius))
            ring.append(bm.verts.new(point.as_tuple()))
        made_rings.append(ring)

    for index in range(len(made_rings) - 1):
        lower = made_rings[index]
        upper = made_rings[index + 1]
        for step in range(sides):
            a = lower[step]
            b = lower[(step + 1) % sides]
            c = upper[(step + 1) % sides]
            d = upper[step]
            bm.faces.new((a, b, c, d))

    bm.faces.new(list(reversed(made_rings[0])))
    bm.faces.new(made_rings[-1])

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    part = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(part)

    if roughen_amount > 0.0:
        roughen(part, amount=roughen_amount, seed=seed)

    recalc_normals(part)
    shade_flat(part)

    part.location = joint
    registry[name] = part
    return part


def revolved_part(registry, name, profile, joint, sides=12, roughen_amount=0.006, seed=0):
    """A part made by spinning an outline - heads, caps, pouches, helms."""
    part = revolve_closed_profile(name, profile, sides)
    if roughen_amount > 0.0:
        roughen(part, amount=roughen_amount, seed=seed)
    recalc_normals(part)
    shade_flat(part)
    part.location = joint
    registry[name] = part
    return part


def step_along(origin, direction, distance):
    """Walk `distance` from `origin` along `direction`, returning the new point.

    Joints are computed from the direction a limb actually points rather than typed in
    separately, so a canted arm's elbow lands where the arm goes instead of where a
    vertical arm would have put it.
    """
    axis = _Vector(direction).normalised()
    return (_Vector(origin) + axis.scaled(distance)).as_tuple()


def attach(child, parent):
    """Parent while keeping the child where it already is in space."""
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


def finish_character(registry, filename, root_part, basename, views=None):
    """Export the whole tree as one FBX and render previews. Reports the measurements.

    bake_space_transform stays False. It was tested directly on the Grunt and moves 12 of
    16 joint origins - a fist by 1.15 m - and leaks non-uniform parent scale into every
    child. The axis conversion is done by Unity instead, via bakeAxisConversion: 1 in the
    model's .meta.
    """
    bpy.ops.object.select_all(action="DESELECT")
    for part in registry.values():
        part.select_set(True)
    bpy.context.view_layer.objects.active = root_part

    os.makedirs(MODEL_DIR, exist_ok=True)
    export_path = MODEL_DIR + "/" + filename

    bpy.ops.export_scene.fbx(
        filepath=export_path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="STRIP",
    )

    everything = list(registry.values())

    total_triangles = 0
    all_x, all_y, all_z = [], [], []
    for part in everything:
        total_triangles += triangle_count(part)
        for index in range(8):
            corner = part.matrix_world @ _bbox_corner(part, index)
            all_x.append(corner[0])
            all_y.append(corner[1])
            all_z.append(corner[2])

    print("  parts:     " + str(len(registry)))
    print("  triangles: " + str(total_triangles))
    print("  height:    " + str(round(max(all_z) - min(all_z), 3)) + " m")
    print("  width:     " + str(round(max(all_x) - min(all_x), 3)) + " m")
    print("  depth:     " + str(round(max(all_y) - min(all_y), 3)) + " m")
    print("  feet at:   " + str(round(min(all_z), 3)) + " m")

    if views is None:
        views = [("front", 0.0), ("three_quarter", 38.0), ("side", 90.0)]
    for path in render_group(everything, basename, views=views):
        print("  preview:   " + os.path.basename(path))

    return export_path


def write_meta_axis_fix(filename):
    """Set bakeAxisConversion: 1 on a model's .meta if Unity has already made one.

    With bake_space_transform off in Blender and bakeAxisConversion off in Unity, NOBODY
    performs the Z-up to Y-up conversion: Unity reads Blender's Z-up vertices as though
    they were Y-up and the creature arrives lying on its back. That is what had the
    Grunts swimming across the floor.
    """
    meta_path = MODEL_DIR + "/" + filename + ".meta"
    if not os.path.exists(meta_path):
        print("  meta:      not created yet - Unity will generate it, then it needs")
        print("             bakeAxisConversion: 1 setting by hand")
        return False

    with open(meta_path, "r", encoding="utf-8") as handle:
        text = handle.read()

    if "bakeAxisConversion: 1" in text:
        print("  meta:      bakeAxisConversion already 1")
        return True

    fixed = text.replace("bakeAxisConversion: 0", "bakeAxisConversion: 1")
    if fixed == text:
        print("  meta:      no bakeAxisConversion key found")
        return False

    with open(meta_path, "w", encoding="utf-8") as handle:
        handle.write(fixed)
    print("  meta:      bakeAxisConversion set to 1")
    return True
