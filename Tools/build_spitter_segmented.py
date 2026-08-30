"""CHR-06 — the Spitter, as a hierarchy of separately-rotatable parts.

The single most valuable character asset in the bible, and for a gameplay reason rather
than an aesthetic one: ValleyBuilder currently passes "Grunt" as the Spitter's model name,
so the ranged enemy is visually identical to the melee one. Round three teaches the player
a new lesson while looking exactly like round one, which reads as unfair rather than as a
new idea.

Bible spec: "roughly human height but hunched and asymmetric. One enormously overdeveloped
throwing arm far larger than the other, a heavy counterweighted tail, and a wide flat head
with a large single eye. A pouch or sling of rocks slung at the hip. Reads instantly as
'throws things from a distance' and must be impossible to confuse with a heavy melee
brute. Bold asymmetric silhouette."

Follows build_grunt_segmented.py exactly: parts hang from their own origin, the origin
sits on the joint, ProceduralAnimator finds them by name. Same conventions, same contract.

The asymmetry is the whole design. Every measurement below for the right side is roughly
three times its left-side twin, because the brief's real requirement is that a player who
sees only the silhouette knows immediately that this one throws.
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

LIMB_SIDES = 6

# ----------------------------------------------------------------------------------
# Skeleton measurements, in metres from the floor
# ----------------------------------------------------------------------------------
# Built to 1.55 m standing height, which is what MakeSpitter's shell already expects.
# Hunched, so it would be nearer 1.75 m if it straightened up.

HIP_HEIGHT = 0.80
KNEE_HEIGHT = 0.42
ANKLE_HEIGHT = 0.09

SHOULDER_HEIGHT = 1.30

# The torso leans forward by this much in Y over its length. Forward is -Y in Blender
# here, matching the Grunt's feet.
#
# The hunch is built into the GEOMETRY rather than set as a rest rotation on the torso
# object, because ProceduralAnimator.SetPitch assigns localRotation outright and would
# erase any rest lean the moment the creature moved.
TORSO_LEAN = -0.19

# Right is the throwing arm. Higher, further out, and vastly heavier than the left.
THROW_SHOULDER_X = 0.345
THROW_SHOULDER_Z = 1.32
THROW_ELBOW_Z = 0.88
THROW_WRIST_Z = 0.48

# Left is the small arm - held in close, ordinary, almost vestigial beside the other.
SMALL_SHOULDER_X = -0.21
SMALL_SHOULDER_Z = 1.26
SMALL_ELBOW_Z = 0.92
SMALL_WRIST_Z = 0.58

HIP_HALF_WIDTH = 0.155

parts_by_name = {}


def make_part(name, length, top_radius, bottom_radius, joint_world_position,
              sides=LIMB_SIDES, roughen_amount=0.008, seed=0):
    """One rigid body part, hanging down from its own origin. See the Grunt build."""
    overhang = top_radius * 0.85

    part = tapered_tube(
        name,
        start=(0.0, 0.0, overhang),
        end=(0.0, 0.0, -length),
        start_radius=top_radius,
        end_radius=bottom_radius,
        segments=sides,
    )

    if roughen_amount > 0.0:
        roughen(part, amount=roughen_amount, seed=seed)

    recalc_normals(part)
    shade_flat(part)

    part.location = joint_world_position
    parts_by_name[name] = part
    return part


def make_shaped_part(name, rings, joint_world_position, sides=LIMB_SIDES,
                     roughen_amount=0.008, seed=0, direction=(0.0, 0.0, -1.0)):
    """A limb built from a profile of (distance below the joint, radius) pairs.

    The first pass of this creature used tapered_tube for the arms, which interpolates
    between exactly two radii. That can only ever produce a cone, and a cone the
    thickness of a throwing arm reads as a pipe or a slab rather than as a limb - the
    first render showed the big arm as a featureless box standing beside the body.

    What makes a limb read as a limb is variation ALONG it: narrow where it hinges,
    swelling through the muscle. That needs more than two rings, which is all this is.
    """
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    # The overhang stub is kept small here. On the first pass it was 0.85 of the top
    # radius, which on a thick limb put a visible bulge above every joint and made the
    # arm read as three stacked blobs rather than one flowing limb.
    overhang = rings[0][1] * 0.35
    full = [(-overhang, rings[0][1])] + [(distance, radius) for distance, radius in rings]

    # Rings are laid perpendicular to `direction`, so a part can run along a leaning
    # axis - which is what lets the torso have a waist AND a forward hunch at once.
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

    part.location = joint_world_position
    parts_by_name[name] = part
    return part


def make_part_along(name, start_offset, end_offset, top_radius, bottom_radius,
                    joint_world_position, sides=LIMB_SIDES, roughen_amount=0.008, seed=0):
    """A part running in an arbitrary direction from its origin.

    The plain make_part only builds straight down, which is right for a limb but wrong
    for a leaning torso or a tail that curves away behind the creature. Same rule
    otherwise: the origin is the joint, so rotating the object swings the part.
    """
    part = tapered_tube(
        name,
        start=start_offset,
        end=end_offset,
        start_radius=top_radius,
        end_radius=bottom_radius,
        segments=sides,
    )

    if roughen_amount > 0.0:
        roughen(part, amount=roughen_amount, seed=seed)

    recalc_normals(part)
    shade_flat(part)

    part.location = joint_world_position
    parts_by_name[name] = part
    return part


def parent_to(child, parent):
    """Parent while keeping the child where it already is in space."""
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


# ----------------------------------------------------------------------------------
# Body
# ----------------------------------------------------------------------------------

hips = make_part_along(
    "Hips",
    start_offset=(0.0, 0.0, 0.0), end_offset=(0.0, 0.0, 0.18),
    top_radius=0.186, bottom_radius=0.172,
    joint_world_position=(0.0, 0.0, HIP_HEIGHT), seed=1,
)

# Torso: leans forward as it rises. The lean is what makes the creature read as hunched
# and, with the tail behind it, as balanced around its own hips.
torso_length = SHOULDER_HEIGHT - HIP_HEIGHT
# Narrower than the first pass. The throwing arm has to read as a separate mass hanging
# off the body, and against a wide torso the two merged into one block.
# Shaped rather than tapered, and this is the change that matters most on this creature.
# Earlier passes had the torso WIDER at the hips than at the chest, so it read as a
# barrel with a hat on it. A body reads as a body because it pinches at the waist and
# broadens across the chest; that is one profile, and no amount of arm tuning
# substitutes for it.
torso_axis_length = math.sqrt(TORSO_LEAN * TORSO_LEAN + torso_length * torso_length)
torso = make_shaped_part(
    "Torso",
    rings=[
        (0.00, 0.184),                      # sits on the hips
        (0.14, 0.156),                      # the waist - narrowest point
        (0.34, 0.240),                      # the chest - broadest
        (torso_axis_length, 0.198),         # up into the shoulders
    ],
    joint_world_position=(0.0, 0.0, HIP_HEIGHT),
    sides=8, seed=2,
    direction=(0.0, TORSO_LEAN, torso_length),
)
parent_to(torso, hips)

# The shoulder mass, tilted toward the throwing side. Not a symmetric slab like the
# Grunt's - the whole upper body is built lopsided around the big arm.
shoulders = make_part_along(
    "Shoulders",
    start_offset=(-0.16, 0.0, -0.03), end_offset=(0.27, 0.0, 0.09),
    top_radius=0.092, bottom_radius=0.132,
    joint_world_position=(0.0, TORSO_LEAN, SHOULDER_HEIGHT - 0.03), seed=3,
)
parent_to(shoulders, torso)

# ----------------------------------------------------------------------------------
# Head — a wide flat cap with one big eye
# ----------------------------------------------------------------------------------
# Revolved rather than tapered, because the brim is the recognisable part and a cone
# does not have one. Both ends of the outline sit at radius 0, so the revolve closes
# itself into a solid at the poles.

HEAD_JOINT_Z = SHOULDER_HEIGHT + 0.015

head = revolve_closed_profile("Head", [
    (0.000, 0.000),   # underside centre
    (0.118, 0.008),
    (0.152, 0.038),
    (0.208, 0.058),
    (0.272, 0.092),   # the brim - widest point, and the whole silhouette read
    (0.222, 0.128),
    (0.142, 0.158),
    (0.000, 0.166),   # top centre
], 14)

roughen(head, amount=0.006, seed=4)
recalc_normals(head)
shade_flat(head)
head.location = (0.0, TORSO_LEAN - 0.035, HEAD_JOINT_Z)
parts_by_name["Head"] = head
parent_to(head, torso)

# The single eye, set into the front of the cap. Not in ProceduralAnimator's part list,
# so it simply rides along with the head - which is correct, an eye does not articulate.
eye = make_part_along(
    "Eye",
    start_offset=(0.0, 0.0, 0.0), end_offset=(0.0, -0.075, 0.0),
    top_radius=0.088, bottom_radius=0.062,
    joint_world_position=(0.0, TORSO_LEAN - 0.175, HEAD_JOINT_Z + 0.072),
    sides=10, roughen_amount=0.0, seed=5,
)
parent_to(eye, head)

# ----------------------------------------------------------------------------------
# The throwing arm — right side, enormous
# ----------------------------------------------------------------------------------
# The forearm SWELLS toward the hand rather than tapering. That inversion is what makes
# the limb read as a club of muscle built for one job, instead of as a fat arm.

# Eight sides rather than six. At this thickness a hexagon reads as a machined post;
# the extra two faces are enough to round it into flesh without costing anything.
THROW_SIDES = 8

# The arm is CANTED rather than hanging straight down. An earlier pass had it vertical
# and parallel to the torso, which read as a pillar standing beside the creature instead
# of as a limb attached to it. The angle is baked into the geometry rather than set as a
# rest rotation, because ProceduralAnimator assigns localRotation outright and would
# erase it. Each joint is then computed FROM that direction, so the elbow and wrist
# actually land where the arm points rather than where a vertical arm would have put them.

def step_along(origin, direction, distance):
    """Walk `distance` from `origin` in `direction`. Returns the new point."""
    axis = _Vector(direction).normalised()
    moved = _Vector(origin) + axis.scaled(distance)
    return moved.as_tuple()


THROW_SHOULDER = (THROW_SHOULDER_X, TORSO_LEAN + 0.02, THROW_SHOULDER_Z)

UPPER_ARM_DIRECTION = (0.30, -0.10, -1.0)     # out from the body and a little forward
UPPER_ARM_LENGTH = 0.46
FOREARM_DIRECTION = (-0.06, -0.26, -1.0)      # elbow turns it back in and forward
FOREARM_LENGTH = 0.40
FIST_DIRECTION = (0.02, -0.08, -1.0)
FIST_LENGTH = 0.215

THROW_ELBOW = step_along(THROW_SHOULDER, UPPER_ARM_DIRECTION, UPPER_ARM_LENGTH)
THROW_WRIST = step_along(THROW_ELBOW, FOREARM_DIRECTION, FOREARM_LENGTH)

upper_arm_right = make_shaped_part(
    "UpperArmR",
    rings=[
        (0.00, 0.076),               # narrow where it hinges at the shoulder
        (0.10, 0.112),
        (0.21, 0.124),               # the bicep
        (0.35, 0.098),
        (UPPER_ARM_LENGTH, 0.074),   # narrow again into the elbow
    ],
    joint_world_position=THROW_SHOULDER,
    sides=THROW_SIDES, seed=11,
    direction=UPPER_ARM_DIRECTION,
)
parent_to(upper_arm_right, shoulders)

# The forearm is the one that swells TOWARD the hand rather than away from it. That
# inversion against a normal arm is what makes the limb read as built for one job.
forearm_right = make_shaped_part(
    "ForearmR",
    rings=[
        (0.00, 0.072),
        (0.11, 0.108),
        (0.26, 0.130),
        (FOREARM_LENGTH, 0.116),
    ],
    joint_world_position=THROW_ELBOW,
    sides=THROW_SIDES, seed=21,
    direction=FOREARM_DIRECTION,
)
parent_to(forearm_right, upper_arm_right)

fist_right = make_shaped_part(
    "FistR",
    rings=[
        (0.00, 0.112),
        (0.08, 0.138),
        (0.16, 0.124),
        (FIST_LENGTH, 0.080),
    ],
    joint_world_position=THROW_WRIST,
    sides=THROW_SIDES, seed=31,
    direction=FIST_DIRECTION,
)
parent_to(fist_right, forearm_right)

# ----------------------------------------------------------------------------------
# The small arm — left side, about a third the thickness
# ----------------------------------------------------------------------------------

upper_arm_left = make_shaped_part(
    "UpperArmL",
    rings=[
        (0.00, 0.036),
        (0.11, 0.048),
        (0.34, 0.034),
    ],
    joint_world_position=(SMALL_SHOULDER_X, TORSO_LEAN + 0.02, SMALL_SHOULDER_Z),
    seed=12,
)
parent_to(upper_arm_left, shoulders)

forearm_left = make_shaped_part(
    "ForearmL",
    rings=[
        (0.00, 0.032),
        (0.14, 0.040),
        (0.34, 0.028),
    ],
    joint_world_position=(SMALL_SHOULDER_X, TORSO_LEAN + 0.02, SMALL_ELBOW_Z),
    seed=22,
)
parent_to(forearm_left, upper_arm_left)

fist_left = make_shaped_part(
    "FistL",
    rings=[
        (0.00, 0.038),
        (0.05, 0.046),
        (0.105, 0.030),
    ],
    joint_world_position=(SMALL_SHOULDER_X, TORSO_LEAN + 0.02, SMALL_WRIST_Z),
    seed=32,
)
parent_to(fist_left, forearm_left)

# ----------------------------------------------------------------------------------
# Legs
# ----------------------------------------------------------------------------------

for side_name, side_sign in (("L", -1.0), ("R", 1.0)):
    hip_x = side_sign * HIP_HALF_WIDTH

    # Shaped rather than tapered, for the same reason as the arms: a straight cone reads
    # as a table leg. The thigh swells and the calf has a belly.
    thigh = make_shaped_part(
        "Thigh" + side_name,
        rings=[
            (0.00, 0.112),
            (0.11, 0.152),
            (0.28, 0.128),
            (0.38, 0.104),
        ],
        joint_world_position=(hip_x, 0.0, HIP_HEIGHT - 0.02),
        seed=40 + int(side_sign),
    )
    parent_to(thigh, hips)

    shin = make_shaped_part(
        "Shin" + side_name,
        rings=[
            (0.00, 0.098),
            (0.09, 0.126),
            (0.22, 0.096),
            (0.33, 0.072),
        ],
        joint_world_position=(hip_x, 0.0, KNEE_HEIGHT - 0.02),
        seed=50 + int(side_sign),
    )
    parent_to(shin, thigh)

    foot = make_part_along(
        "Foot" + side_name,
        start_offset=(0.0, 0.045, 0.0), end_offset=(0.0, -0.235, -0.01),
        top_radius=0.092, bottom_radius=0.070,
        joint_world_position=(hip_x, 0.0, ANKLE_HEIGHT),
        roughen_amount=0.006, seed=60 + int(side_sign),
    )
    parent_to(foot, shin)

# ----------------------------------------------------------------------------------
# The counterweight tail
# ----------------------------------------------------------------------------------
# Three segments chained back and down from the hips. Not articulated by
# ProceduralAnimator - it knows nothing of tails - so it rides rigidly with the hips.
# That is acceptable and honest; a tail sway would be a good later addition to the
# animator, and the parts are already named and parented for it.

TAIL_SEGMENTS = [
    ((0.0, 0.150, 0.760), (0.0, 0.250, -0.075), 0.132, 0.108, 70),
    ((0.0, 0.400, 0.685), (0.0, 0.265, -0.145), 0.104, 0.076, 71),
    ((0.0, 0.665, 0.540), (0.0, 0.230, -0.205), 0.072, 0.030, 72),
]

previous_tail_part = hips
for tail_index in range(len(TAIL_SEGMENTS)):
    joint, offset, top_radius, bottom_radius, seed = TAIL_SEGMENTS[tail_index]

    tail_part = make_part_along(
        "Tail" + str(tail_index + 1),
        start_offset=(0.0, 0.0, 0.0), end_offset=offset,
        top_radius=top_radius, bottom_radius=bottom_radius,
        joint_world_position=joint, seed=seed,
    )
    parent_to(tail_part, previous_tail_part)
    previous_tail_part = tail_part

# ----------------------------------------------------------------------------------
# The rock pouch
# ----------------------------------------------------------------------------------
# "A pouch or sling of rocks slung at the hip." Hung on the small-arm side, so it does
# not crowd the throwing arm's silhouette - the big arm needs clear space around it.

pouch = revolve_closed_profile("RockPouch", [
    (0.000, 0.000),
    (0.070, 0.012),
    (0.098, 0.055),
    (0.092, 0.108),
    (0.055, 0.140),
    (0.000, 0.148),
], 10)
roughen(pouch, amount=0.006, seed=80)
recalc_normals(pouch)
shade_flat(pouch)
pouch.location = (-0.235, 0.055, 0.660)
parts_by_name["RockPouch"] = pouch
parent_to(pouch, hips)

# ----------------------------------------------------------------------------------
# Export
# ----------------------------------------------------------------------------------
# bake_space_transform stays False. Baking it would flatten every part origin into the
# vertex data and destroy the joints this whole file exists to create. Blender therefore
# writes a compensating rotation onto the model root, and ValleyBuilder.AttachModel must
# preserve it rather than forcing identity - which is exactly the bug that had the
# Grunts swimming on the ground.

bpy.ops.object.select_all(action="DESELECT")
for part in parts_by_name.values():
    part.select_set(True)
bpy.context.view_layer.objects.active = hips

os.makedirs(MODEL_DIR, exist_ok=True)
export_path = MODEL_DIR + "/SpitterSegmented.fbx"

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

total_triangles = 0
for part in parts_by_name.values():
    total_triangles += triangle_count(part)

everything = list(parts_by_name.values())

all_x = []
all_y = []
all_z = []
for part in everything:
    for corner_index in range(8):
        corner = part.matrix_world @ _bbox_corner(part, corner_index)
        all_x.append(corner[0])
        all_y.append(corner[1])
        all_z.append(corner[2])

print("Segmented Spitter built.")
print("  parts:     " + str(len(parts_by_name)))
print("  triangles: " + str(total_triangles))
print("  height:    " + str(round(max(all_z) - min(all_z), 3)) + " m")
print("  width:     " + str(round(max(all_x) - min(all_x), 3)) + " m")
print("  depth:     " + str(round(max(all_y) - min(all_y), 3)) + " m")
print("  feet at:   " + str(round(min(all_z), 3)) + " m")
print("  exported:  " + export_path)

for path in render_group(everything, "spitter_segmented",
                         views=[("front", 0.0), ("three_quarter", 38.0), ("side", 90.0)]):
    print("  preview:   " + path)
