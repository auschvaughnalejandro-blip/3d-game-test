"""WPN-01, 02, 03, 04, 06 — the weapons, built from the bible's written specs.

Section 0.7 of ASSET_BIBLE.md ranks the weapons fourth by priority and gives the reason:
they are held in front of the camera for the entire game. All five are currently grey
primitive cubes assembled in BuildThePlayer.

Every one of these is lathe, loft and array work. A blade is a cross section swept along
a line with a taper; a bow limb is the same sweep along a curve; a pommel is a revolved
outline. None of it needs an artist's eye, which is why these are on my half of the split
rather than being generated or bought.

Orientation: each weapon is built pointing along +Z with the grip near the origin, so
after the FBX axis conversion it arrives in Unity pointing along +Y with its handle at
the pivot. If a weapon comes in sideways, rotate the prefab rather than re-exporting -
the convention here is deliberate and consistent across all five.

Run:  python Tools/blender_send.py Tools/build_weapons.py
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


# ----------------------------------------------------------------------------------
# Local helpers
#
# These live here rather than in ov_kit.py because another session is editing that file
# and there is no reason for two people to be writing to it at once.
# ----------------------------------------------------------------------------------

def loft_along_path(name, cross_section, path_points, widths, thicknesses):
    """Sweep a 2D cross section along a 3D path, scaling it at each step.

    `cross_section` is a list of (across, through) pairs describing the shape side-on -
    for a blade that is a flattened diamond, for a bow limb a rectangle. `widths` and
    `thicknesses` scale that shape independently at each path point, which is what lets a
    blade taper to a point in one axis while staying the same thickness in the other.

    This one function builds every blade, limb and shaft below.
    """
    if not (len(path_points) == len(widths) == len(thicknesses)):
        raise ValueError("path, widths and thicknesses must be the same length")

    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    rings = []
    for index in range(len(path_points)):
        centre = _Vector(path_points[index])
        width = widths[index]
        thickness = thicknesses[index]

        # Build a frame square to the path at this point, so the cross section is always
        # perpendicular to the sweep. Without this the section is stuck in the XY plane
        # and anything swept along X comes out as a flat ribbon instead of a bar - which
        # is exactly how the first crossguard went wrong.
        if index == 0:
            tangent = _Vector(path_points[1]) - _Vector(path_points[0])
        elif index == len(path_points) - 1:
            tangent = _Vector(path_points[-1]) - _Vector(path_points[-2])
        else:
            tangent = _Vector(path_points[index + 1]) - _Vector(path_points[index - 1])
        tangent = tangent.normalised()

        # Seeded so that a path running up +Z gives across=+X and through=+Y, which is
        # the orientation every blade below is written against.
        seed = _Vector((0.0, -1.0, 0.0))
        if abs(tangent.dot(seed)) > 0.95:
            seed = _Vector((0.0, 0.0, 1.0))
        side = tangent.cross(seed).normalised()
        up = tangent.cross(side).normalised()

        ring = []
        for across, through in cross_section:
            point = (centre
                     + side.scaled(across * width)
                     + up.scaled(through * thickness))
            ring.append(bm.verts.new(point.as_tuple()))
        rings.append(ring)

    side_count = len(cross_section)

    for index in range(len(rings) - 1):
        lower = rings[index]
        upper = rings[index + 1]
        for step in range(side_count):
            a = lower[step]
            b = lower[(step + 1) % side_count]
            c = upper[(step + 1) % side_count]
            d = upper[step]
            bm.faces.new((a, b, c, d))

    # Cap both ends. A zero-width ring collapses to a point and is skipped, which is how
    # a blade tip closes itself without a degenerate face.
    if widths[0] > 1.0e-5:
        bm.faces.new(list(reversed(rings[0])))
    if widths[-1] > 1.0e-5:
        bm.faces.new(rings[-1])

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def box(name, centre, half_x, half_y, half_z):
    """An axis-aligned box. Verbose on purpose - the eight corners are written out."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    cx, cy, cz = centre
    corners = [
        bm.verts.new((cx - half_x, cy - half_y, cz - half_z)),
        bm.verts.new((cx + half_x, cy - half_y, cz - half_z)),
        bm.verts.new((cx + half_x, cy + half_y, cz - half_z)),
        bm.verts.new((cx - half_x, cy + half_y, cz - half_z)),
        bm.verts.new((cx - half_x, cy - half_y, cz + half_z)),
        bm.verts.new((cx + half_x, cy - half_y, cz + half_z)),
        bm.verts.new((cx + half_x, cy + half_y, cz + half_z)),
        bm.verts.new((cx - half_x, cy + half_y, cz + half_z)),
    ]

    bm.faces.new((corners[0], corners[3], corners[2], corners[1]))  # bottom
    bm.faces.new((corners[4], corners[5], corners[6], corners[7]))  # top
    bm.faces.new((corners[0], corners[1], corners[5], corners[4]))
    bm.faces.new((corners[1], corners[2], corners[6], corners[5]))
    bm.faces.new((corners[2], corners[3], corners[7], corners[6]))
    bm.faces.new((corners[3], corners[0], corners[4], corners[7]))

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def export_group(objects, filename):
    """Export several objects to one FBX, keeping them as separate meshes.

    Used only by the Warden's Edge, where the glowing core has to stay a distinct object
    so Unity can put an emissive material on it while the iron around it stays dark.
    """
    os.makedirs(MODEL_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    path = MODEL_DIR + "/" + filename
    bpy.ops.export_scene.fbx(
        filepath=path,
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
    return path


# A flattened diamond. This is the cross section of every blade here: a central ridge
# front and back, tapering to an edge on each side. It is what makes a blade read as a
# blade from any angle rather than as a flat plank.
BLADE_SECTION = [
    (-1.00, 0.00),   # left edge
    (-0.45, 0.55),
    (0.00, 1.00),    # front ridge
    (0.45, 0.55),
    (1.00, 0.00),    # right edge
    (0.45, -0.55),
    (0.00, -1.00),   # back ridge
    (-0.45, -0.55),
]

# A plain rounded rectangle, for bow limbs and shafts.
LIMB_SECTION = [
    (-1.00, -0.55),
    (0.00, -1.00),
    (1.00, -0.55),
    (1.00, 0.55),
    (0.00, 1.00),
    (-1.00, 0.55),
]


# ----------------------------------------------------------------------------------
# WPN-01 — Sword
# ----------------------------------------------------------------------------------
# "A plain arming sword. Straight double-edged steel blade about 80cm, simple straight
# crossguard, leather-wrapped grip, plain disc pommel. Well used, slightly nicked at the
# edge, no engraving, no jewels, no glow. An honest functional weapon."

def build_sword():
    clear_scene()
    parts = []

    GRIP_TOP = 0.155

    # The blade. Full width for most of its length, then a fast taper over the last
    # fifth to a point - which is what an arming sword actually does, and what stops it
    # reading as a kitchen knife.
    blade_path = []
    blade_widths = []
    blade_thicknesses = []

    BLADE_LENGTH = 0.80
    BLADE_STEPS = 14

    for step in range(BLADE_STEPS + 1):
        travel = step / float(BLADE_STEPS)
        z = GRIP_TOP + 0.030 + travel * BLADE_LENGTH

        if travel < 0.80:
            width = 0.0255 - travel * 0.0035
        else:
            # Last fifth: run the width down to almost nothing for the point.
            into_tip = (travel - 0.80) / 0.20
            width = (0.0255 - 0.80 * 0.0035) * (1.0 - into_tip * 0.97)

        thickness = 0.0042 - travel * 0.0016

        blade_path.append((0.0, 0.0, z))
        blade_widths.append(width)
        blade_thicknesses.append(thickness)

    blade = loft_along_path("SwordBlade", BLADE_SECTION,
                            blade_path, blade_widths, blade_thicknesses)
    parts.append(blade)

    # Crossguard - a plain straight bar, tapering slightly toward each tip so it is not
    # a raw cube. Swept along X directly at its final position: no object-level scale or
    # rotation, because those apply about the object origin and will fling the geometry
    # somewhere else entirely.
    guard_path = []
    guard_widths = []
    guard_thicknesses = []

    GUARD_STEPS = 8
    GUARD_HALF_SPAN = 0.098

    for step in range(GUARD_STEPS + 1):
        travel = step / float(GUARD_STEPS)
        across = (travel * 2.0 - 1.0) * GUARD_HALF_SPAN

        guard_path.append((across, 0.0, GRIP_TOP + 0.015))
        guard_widths.append(0.0112 - abs(across) * 0.030)
        guard_thicknesses.append(0.0090 - abs(across) * 0.022)

    guard = loft_along_path("SwordGuard", LIMB_SECTION,
                            guard_path, guard_widths, guard_thicknesses)
    parts.append(guard)

    # Grip. The leather wrap is eleven shallow rings rather than a smooth cylinder -
    # cheap in triangles and it catches the light as a wrap should.
    WRAP_COUNT = 11
    for wrap_index in range(WRAP_COUNT):
        low = 0.020 + wrap_index * 0.0122
        high = low + 0.0092
        radius = 0.0150 if wrap_index % 2 == 0 else 0.0161
        parts.append(tapered_tube(
            "SwordGripWrap" + str(wrap_index),
            start=(0.0, 0.0, low), end=(0.0, 0.0, high),
            start_radius=radius, end_radius=radius, segments=10,
        ))

    # Disc pommel, revolved.
    pommel_profile = [
        (0.000, 0.0195),
        (0.016, 0.0180),
        (0.027, 0.0110),
        (0.029, 0.0000),
        (0.026, -0.0105),
        (0.015, -0.0170),
        (0.000, -0.0185),
    ]
    pommel = revolve_closed_profile("SwordPommel", pommel_profile, 14)
    parts.append(pommel)

    sword = join_all(parts, "Sword")

    # "Well used, slightly nicked at the edge." Half a millimetre of jitter - enough to
    # kill the machined look, far too little to read as damage.
    roughen(sword, amount=0.0005, seed=21)
    shade_flat(sword)
    return sword


# ----------------------------------------------------------------------------------
# WPN-02 — Hammer
# ----------------------------------------------------------------------------------
# "A heavy two-handed war maul. Short thick wooden haft bound with iron, an enormous
# blunt rectangular iron head with a reinforcing collar. Battered, chipped, obviously
# enormously heavy. Silhouette must read instantly as 'slow and crushing' beside a sword."

def build_hammer():
    clear_scene()
    parts = []

    HAFT_LENGTH = 0.86

    haft = tapered_tube(
        "HammerHaft",
        start=(0.0, 0.0, 0.0), end=(0.0, 0.0, HAFT_LENGTH),
        start_radius=0.0225, end_radius=0.0268, segments=12,
    )
    parts.append(haft)

    # Iron bindings down the haft.
    for binding_index in range(4):
        low = 0.055 + binding_index * 0.165
        parts.append(tapered_tube(
            "HammerBinding" + str(binding_index),
            start=(0.0, 0.0, low), end=(0.0, 0.0, low + 0.028),
            start_radius=0.0262, end_radius=0.0262, segments=12,
        ))

    # The collar where the head meets the haft.
    collar = tapered_tube(
        "HammerCollar",
        start=(0.0, 0.0, HAFT_LENGTH - 0.075), end=(0.0, 0.0, HAFT_LENGTH + 0.020),
        start_radius=0.0345, end_radius=0.0400, segments=12,
    )
    parts.append(collar)

    # The head. Deliberately a plain heavy rectangle - the whole job of this silhouette
    # is to say "crushing" next to the sword's "cutting", and a blunt slab says it
    # faster than any amount of detail.
    head = box("HammerHead",
               centre=(0.0, 0.0, HAFT_LENGTH + 0.072),
               half_x=0.0720, half_y=0.0665, half_z=0.1080)
    parts.append(head)

    # A raised striking band around each face, so the head is not a bare cube.
    band = box("HammerBand",
               centre=(0.0, 0.0, HAFT_LENGTH + 0.072),
               half_x=0.0762, half_y=0.0706, half_z=0.0330)
    parts.append(band)

    hammer = join_all(parts, "Hammer")
    bevel(hammer, width=0.006, segments=1, angle_degrees=35.0)

    # "Battered, chipped." Heavier than the sword's - this thing has been used on stone.
    roughen(hammer, amount=0.0018, seed=33)
    shade_flat(hammer)
    return hammer


# ----------------------------------------------------------------------------------
# WPN-03 — Bow
# ----------------------------------------------------------------------------------
# "A plain recurve hunting bow. Dark laminated wood limbs, leather-wrapped grip, pale
# twisted string. Practical hunting gear, no decoration, no carving."
#
# The limb is the one shape here that is genuinely a curve rather than an assembly, and
# it is why a bow is a good thing to build this way: the recurve is a formula, and
# sweeping a cross section along it gives a correct limb every time.

def build_bow():
    clear_scene()
    parts = []

    LIMB_STEPS = 16
    LIMB_REACH = 0.62

    def build_limb(direction, label):
        path = []
        widths = []
        thicknesses = []

        for step in range(LIMB_STEPS + 1):
            travel = step / float(LIMB_STEPS)

            z = direction * (0.055 + travel * LIMB_REACH)

            # The recurve: the limb bows away from the archer, then turns back toward
            # them over the last third. One sine term for the belly, one for the tip.
            belly = math.sin(travel * math.pi * 0.92) * 0.088
            tip_return = max(0.0, travel - 0.66) / 0.34
            y = belly - tip_return * tip_return * 0.115

            path.append((0.0, y, z))
            widths.append(0.0125 - travel * 0.0058)
            thicknesses.append(0.0092 - travel * 0.0043)

        return loft_along_path("BowLimb" + label, LIMB_SECTION, path, widths, thicknesses)

    parts.append(build_limb(1.0, "Upper"))
    parts.append(build_limb(-1.0, "Lower"))

    # The grip.
    grip = tapered_tube(
        "BowGrip",
        start=(0.0, 0.0, -0.085), end=(0.0, 0.0, 0.085),
        start_radius=0.0165, end_radius=0.0165, segments=10,
    )
    parts.append(grip)

    for wrap_index in range(6):
        low = -0.058 + wrap_index * 0.0205
        parts.append(tapered_tube(
            "BowWrap" + str(wrap_index),
            start=(0.0, 0.0, low), end=(0.0, 0.0, low + 0.013),
            start_radius=0.0178, end_radius=0.0178, segments=10,
        ))

    # The string, strung between the two tips. Its ends have to match the limb tips
    # exactly or the bow reads as broken, so they are computed from the same numbers
    # rather than typed in again.
    tip_y = 0.088 * math.sin(math.pi * 0.92) - 0.115
    tip_z = 0.055 + LIMB_REACH

    string = tapered_tube(
        "BowString",
        start=(0.0, tip_y, tip_z), end=(0.0, tip_y, -tip_z),
        start_radius=0.0022, end_radius=0.0022, segments=5,
    )
    parts.append(string)

    bow = join_all(parts, "Bow")
    roughen(bow, amount=0.0004, seed=44)
    shade_flat(bow)
    return bow


# ----------------------------------------------------------------------------------
# WPN-06 — Arrow
# ----------------------------------------------------------------------------------
# "A single hunting arrow. Straight wooden shaft, simple leaf-shaped iron broadhead,
# three grey goose-feather fletchings. Plain, functional, no decoration."
#
# Arrow.cs currently spawns a cylinder.

def build_arrow():
    clear_scene()
    parts = []

    SHAFT_LENGTH = 0.68

    shaft = tapered_tube(
        "ArrowShaft",
        start=(0.0, 0.0, 0.0), end=(0.0, 0.0, SHAFT_LENGTH),
        start_radius=0.0048, end_radius=0.0044, segments=8,
    )
    parts.append(shaft)

    # Leaf-shaped broadhead: a flat blade that swells then tapers to a point.
    head_path = []
    head_widths = []
    head_thicknesses = []

    HEAD_STEPS = 8
    for step in range(HEAD_STEPS + 1):
        travel = step / float(HEAD_STEPS)
        z = SHAFT_LENGTH - 0.010 + travel * 0.082

        # Widest a third of the way along, then to a point.
        if travel < 0.33:
            width = 0.0050 + (travel / 0.33) * 0.0135
        else:
            width = 0.0185 * (1.0 - ((travel - 0.33) / 0.67) ** 1.35)

        head_path.append((0.0, 0.0, z))
        head_widths.append(max(width, 0.0002))
        head_thicknesses.append(0.0028 - travel * 0.0016)

    parts.append(loft_along_path("ArrowHead", BLADE_SECTION,
                                 head_path, head_widths, head_thicknesses))

    # Three fletchings at 120 degrees. Each is a thin swept vane, not a flat plane, so
    # it does not vanish edge-on in flight.
    for feather_index in range(3):
        angle = 2.0 * math.pi * feather_index / 3.0

        # Turn the cross section rather than the object. Rotating the object as well as
        # placing the path at an angle applies the turn twice, and object rotation has
        # to be applied through an operator that acts on the selection rather than on
        # the object you hand it - two ways to get this subtly wrong for no benefit.
        vane_section = []
        for across, through in LIMB_SECTION:
            vane_section.append((
                across * math.cos(angle) - through * math.sin(angle),
                across * math.sin(angle) + through * math.cos(angle),
            ))

        vane_path = []
        vane_widths = []
        vane_thicknesses = []

        VANE_STEPS = 6
        for step in range(VANE_STEPS + 1):
            travel = step / float(VANE_STEPS)
            z = 0.045 + travel * 0.090

            # Tallest in the middle, tapering at both ends.
            height = math.sin(travel * math.pi) * 0.0125 + 0.0015

            vane_path.append((
                math.cos(angle) * (0.0046 + height * 0.5),
                math.sin(angle) * (0.0046 + height * 0.5),
                z,
            ))
            vane_widths.append(height)
            vane_thicknesses.append(0.0007)

        vane = loft_along_path("ArrowFletch" + str(feather_index), vane_section,
                               vane_path, vane_widths, vane_thicknesses)
        parts.append(vane)

    # The nock.
    parts.append(tapered_tube(
        "ArrowNock",
        start=(0.0, 0.0, 0.0), end=(0.0, 0.0, 0.020),
        start_radius=0.0062, end_radius=0.0058, segments=8,
    ))

    arrow = join_all(parts, "Arrow")
    shade_flat(arrow)
    return arrow


# ----------------------------------------------------------------------------------
# WPN-04 — The Warden's Edge
# ----------------------------------------------------------------------------------
# "A long two-handed greatsword made of the same material as a sealed vault. Blade about
# 1.4m, dark iron with a channel of blazing violet crystal running down its centre from
# guard to tip. Heavy angular crossguard with a violet stone set in it. The blade looks
# grown rather than forged, faceted like crystal along the edges. Clearly a far greater
# weapon than a plain sword, without being ornate."
#
# The reward weapon: 5m reach, 200 degree arc. Its entire job is to look obviously
# superior to WPN-01 at a glance, so every dimension here is deliberately set against
# the sword's - longer blade, wider guard, heavier everything.
#
# Exported as TWO meshes. The core keeps its own object so Unity can give it an emissive
# violet material while the iron around it stays dark; joining them would make that
# impossible.

def build_wardens_edge():
    clear_scene()
    iron_parts = []

    GRIP_TOP = 0.290
    BLADE_LENGTH = 1.40
    BLADE_STEPS = 20

    blade_path = []
    blade_widths = []
    blade_thicknesses = []

    for step in range(BLADE_STEPS + 1):
        travel = step / float(BLADE_STEPS)
        z = GRIP_TOP + 0.050 + travel * BLADE_LENGTH

        if travel < 0.86:
            width = 0.0430 - travel * 0.0060
        else:
            into_tip = (travel - 0.86) / 0.14
            width = (0.0430 - 0.86 * 0.0060) * (1.0 - into_tip * 0.96)

        blade_path.append((0.0, 0.0, z))
        blade_widths.append(width)
        blade_thicknesses.append(0.0072 - travel * 0.0026)

    blade = loft_along_path("EdgeBlade", BLADE_SECTION,
                            blade_path, blade_widths, blade_thicknesses)
    iron_parts.append(blade)

    # Angular crossguard: a heavy bar swept back toward the wielder at both ends, which
    # reads as deliberate and forged rather than as the sword's plain straight bar.
    guard_path = []
    guard_widths = []
    guard_thicknesses = []

    GUARD_STEPS = 10
    GUARD_HALF_SPAN = 0.150

    for step in range(GUARD_STEPS + 1):
        travel = step / float(GUARD_STEPS)
        across = (travel * 2.0 - 1.0) * GUARD_HALF_SPAN

        guard_path.append((
            across,
            0.0,
            GRIP_TOP + 0.026 - abs(across) * 0.115,
        ))
        guard_widths.append(0.0195 - abs(across) * 0.055)
        guard_thicknesses.append(0.0125 - abs(across) * 0.030)

    guard = loft_along_path("EdgeGuard", LIMB_SECTION,
                            guard_path, guard_widths, guard_thicknesses)
    iron_parts.append(guard)

    # Grip - long enough for two hands, which is half of why it reads as a greatsword.
    for wrap_index in range(16):
        low = 0.045 + wrap_index * 0.0148
        radius = 0.0182 if wrap_index % 2 == 0 else 0.0194
        iron_parts.append(tapered_tube(
            "EdgeGripWrap" + str(wrap_index),
            start=(0.0, 0.0, low), end=(0.0, 0.0, low + 0.0112),
            start_radius=radius, end_radius=radius, segments=10,
        ))

    iron_parts.append(revolve_closed_profile("EdgePommel", [
        (0.000, 0.0430),
        (0.020, 0.0400),
        (0.032, 0.0250),
        (0.034, 0.0000),
        (0.030, -0.0230),
        (0.018, -0.0370),
        (0.000, -0.0400),
    ], 14))

    iron = join_all(iron_parts, "WardensEdge")

    # "Faceted like crystal along the edges, grown rather than forged." Coarser jitter
    # than the sword got, and flat shading, so the blade breaks into visible planes
    # instead of reading as smooth steel.
    roughen(iron, amount=0.0016, seed=57)
    shade_flat(iron)

    # ---- the glowing core, kept separate ----
    core_parts = []

    core_path = []
    core_widths = []
    core_thicknesses = []

    for step in range(BLADE_STEPS + 1):
        travel = step / float(BLADE_STEPS)
        z = GRIP_TOP + 0.055 + travel * (BLADE_LENGTH - 0.020)

        if travel < 0.86:
            width = 0.0092 - travel * 0.0022
        else:
            into_tip = (travel - 0.86) / 0.14
            width = (0.0092 - 0.86 * 0.0022) * (1.0 - into_tip * 0.94)

        core_path.append((0.0, 0.0, z))
        core_widths.append(max(width, 0.0002))
        core_thicknesses.append(0.0078 - travel * 0.0028)

    core_parts.append(loft_along_path("EdgeCoreChannel", BLADE_SECTION,
                                      core_path, core_widths, core_thicknesses))

    # The stone set in the crossguard. Its height is folded into the profile rather than
    # set as an object location, for the same reason as the fletchings above.
    STONE_CENTRE = GRIP_TOP + 0.026
    core_parts.append(revolve_closed_profile("EdgeCoreStone", [
        (0.0000, STONE_CENTRE + 0.0225),
        (0.0130, STONE_CENTRE + 0.0125),
        (0.0165, STONE_CENTRE + 0.0000),
        (0.0130, STONE_CENTRE - 0.0125),
        (0.0000, STONE_CENTRE - 0.0225),
    ], 8))

    core = join_all(core_parts, "WardensEdgeCore")
    shade_flat(core)

    return iron, core


# ----------------------------------------------------------------------------------
# Run them all
# ----------------------------------------------------------------------------------

def report(obj):
    corners = [obj.matrix_world @ _bbox_corner(obj, i) for i in range(8)]
    length = max(c[2] for c in corners) - min(c[2] for c in corners)
    span = max(c[0] for c in corners) - min(c[0] for c in corners)
    print("    " + str(triangle_count(obj)) + " tris, "
          + str(round(length, 3)) + " m long, "
          + str(round(span, 3)) + " m across")


print("WPN-01 Sword")
sword = build_sword()
report(sword)
export_fbx(sword, "Sword.fbx")
render_views(sword, "sword", views=[("front", 0.0), ("three_quarter", 40.0)])

print("WPN-02 Hammer")
hammer = build_hammer()
report(hammer)
export_fbx(hammer, "Hammer.fbx")
render_views(hammer, "hammer", views=[("front", 0.0), ("three_quarter", 40.0)])

print("WPN-03 Bow")
bow = build_bow()
report(bow)
export_fbx(bow, "Bow.fbx")
render_views(bow, "bow", views=[("front", 0.0), ("side", 90.0)])

print("WPN-06 Arrow")
arrow = build_arrow()
report(arrow)
export_fbx(arrow, "Arrow.fbx")
render_views(arrow, "arrow", views=[("front", 0.0), ("three_quarter", 40.0)])

print("WPN-04 Warden's Edge")
edge_iron, edge_core = build_wardens_edge()
report(edge_iron)
print("    core: " + str(triangle_count(edge_core)) + " tris (separate mesh, emissive)")
export_group([edge_iron, edge_core], "WardensEdge.fbx")
render_views(edge_iron, "wardens_edge", views=[("front", 0.0), ("three_quarter", 40.0)])

print("")
print("All five weapons exported to Assets/Resources/Models/")
print("Previews in Docs/previews/")
