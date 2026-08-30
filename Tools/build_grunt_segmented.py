"""CHR-04 — the Grunt, rebuilt as a hierarchy of separately-rotatable parts.

The existing Grunt.fbx is a single rigid mesh, which is why the only "animation" possible
was tilting the whole creature. This build produces the same creature as a parent/child
tree of body parts, each with its object origin sitting exactly on the joint it pivots
around. FBX preserves that hierarchy, Unity instantiates it as nested Transforms, and
ProceduralAnimator.cs then swings them from code.

No bones and no skinning anywhere. Rigid parts on a transform tree is how games did this
before skinned meshes existed; it still works, it costs nothing, and it suits a chunky
flat-shaded brute far better than a smooth deforming skin would.

The one rule that matters: a part's mesh is modelled hanging DOWN from the world origin,
and the object is then moved to its joint. Get that wrong and the limb rotates around its
middle or its far end and the creature turns itself inside out.

Bible spec: "slightly taller than a man and much broader. Thick sloped shoulders, short
neck, long heavy arms, squat powerful legs. Blunt, patient, immovable. Bold simple
silhouette, low detail." Height 1.91 m, matching what ValleyBuilder already expects.
"""

exec(open(r"c:/Users/Mark Alejandro/OneValley/OneValley-Transfer/unity-project/Tools/ov_kit.py").read())

clear_scene()

# Six sides keeps every limb chunky and faceted. This creature is read as a silhouette in
# two of the game's four lenses, so smooth roundness would buy nothing and cost triangles.
LIMB_SIDES = 6

# ----------------------------------------------------------------------------------
# Skeleton measurements, in metres from the floor
# ----------------------------------------------------------------------------------
# Squat and broad on purpose: the Grunt has to be unmistakable beside the Darter at a
# glance, and the difference is almost entirely in the shoulder width and the leg length.

TOTAL_HEIGHT = 1.91

HIP_HEIGHT = 0.86          # low hips, short legs — squat and powerful
KNEE_HEIGHT = 0.44
ANKLE_HEIGHT = 0.09

SHOULDER_HEIGHT = 1.52     # torso runs from hip to here
# Arms sit inboard of the shoulder slab's outer edge, not level with it. The first pass
# had them flush at 0.41 and they read as two posts standing beside the creature rather
# than as its arms. The slab now clearly overhangs them, which is also what sells
# "thick sloped shoulders" in silhouette.
SHOULDER_HALF_WIDTH = 0.34
ELBOW_HEIGHT = 1.00
WRIST_HEIGHT = 0.50        # long heavy arms, hands hanging near the knees

HIP_HALF_WIDTH = 0.17

parts_by_name = {}


def make_part(name, length, top_radius, bottom_radius, joint_world_position,
              sides=LIMB_SIDES, roughen_amount=0.010, seed=0):
    """One rigid body part, built hanging down from its own origin.

    `joint_world_position` is where the part's pivot sits in the finished creature. The
    mesh itself is generated from (0,0,0) down to (0,0,-length), so once the object is
    moved to the joint, rotating it swings the part like a limb rather than sliding it.

    The mesh actually starts a little way ABOVE the origin. That overhang is what stops a
    segmented character falling apart visibly at every joint the moment it moves: the
    stub rotates with the limb and stays buried inside the parent part in any pose the
    animation reaches. Without it the first render showed daylight through both knees.
    Sized from the limb's own radius, so it scales with the part rather than being a
    magic number.
    """
    overhang = top_radius * 0.85

    part = tapered_tube(
        name,
        start=(0.0, 0.0, overhang),
        end=(0.0, 0.0, -length),
        start_radius=top_radius,
        end_radius=bottom_radius,
        segments=sides,
    )

    # "Coarse hide, cracked and calloused" — a small push along the normals. Applied
    # before the object is moved, so the noise is in the part's own space.
    if roughen_amount > 0.0:
        roughen(part, amount=roughen_amount, seed=seed)

    recalc_normals(part)
    shade_flat(part)

    part.location = joint_world_position
    parts_by_name[name] = part
    return part


def make_part_upward(name, length, bottom_radius, top_radius, joint_world_position,
                     sides=LIMB_SIDES, roughen_amount=0.010, seed=0):
    """As make_part, but the mesh grows UP from the origin.

    Used for the torso and head, which pivot at their base and lean forward, rather than
    hanging from a joint above them like an arm does.
    """
    part = tapered_tube(
        name,
        start=(0.0, 0.0, 0.0),
        end=(0.0, 0.0, length),
        start_radius=bottom_radius,
        end_radius=top_radius,
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
    """Parent while keeping the child where it already is in space.

    Blender needs the parent-inverse matrix set by hand when parenting from script;
    without it the child jumps by the parent's transform the moment it is attached.
    """
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


# ----------------------------------------------------------------------------------
# The body
# ----------------------------------------------------------------------------------

# Hips: the root of everything, and what the walk cycle bobs up and down.
hips = make_part_upward("Hips", length=0.20, bottom_radius=0.26, top_radius=0.24,
                        joint_world_position=(0.0, 0.0, HIP_HEIGHT), seed=1)

# Torso: pivots at the hips and leans. Widens towards the shoulders, which is where the
# creature's whole "much broader than a man" reading comes from.
torso_length = SHOULDER_HEIGHT - HIP_HEIGHT
torso = make_part_upward("Torso", length=torso_length, bottom_radius=0.25, top_radius=0.36,
                         joint_world_position=(0.0, 0.0, HIP_HEIGHT), seed=2)
parent_to(torso, hips)

# The shoulder slab. "Thick sloped shoulders" — a wide flat block across the top of the
# torso, which is the single most recognisable part of the silhouette.
shoulders = make_part_upward("Shoulders", length=0.22, bottom_radius=0.44, top_radius=0.34,
                             joint_world_position=(0.0, 0.0, SHOULDER_HEIGHT - 0.10), seed=3)
shoulders.scale = (1.0, 0.62, 1.0)   # flattened front-to-back into a slab
parent_to(shoulders, torso)

# Head: short thick neck, blunt skull. Sits low between the shoulders.
head = make_part_upward("Head", length=0.30, bottom_radius=0.15, top_radius=0.19,
                        joint_world_position=(0.0, 0.0, SHOULDER_HEIGHT + 0.09), seed=4)
parent_to(head, torso)

# ----------------------------------------------------------------------------------
# Arms — long and heavy, hands hanging near the knees
# ----------------------------------------------------------------------------------

for side_name, side_sign in (("L", -1.0), ("R", 1.0)):
    shoulder_x = side_sign * SHOULDER_HALF_WIDTH

    upper_arm = make_part(
        "UpperArm" + side_name,
        length=SHOULDER_HEIGHT - ELBOW_HEIGHT,
        top_radius=0.145,
        bottom_radius=0.120,
        joint_world_position=(shoulder_x, 0.0, SHOULDER_HEIGHT - 0.04),
        seed=10 + int(side_sign),
    )
    parent_to(upper_arm, shoulders)

    forearm = make_part(
        "Forearm" + side_name,
        length=ELBOW_HEIGHT - WRIST_HEIGHT,
        top_radius=0.125,
        bottom_radius=0.105,
        joint_world_position=(shoulder_x, 0.0, ELBOW_HEIGHT - 0.04),
        seed=20 + int(side_sign),
    )
    parent_to(forearm, upper_arm)

    # A blunt fist, so the arm ends in something rather than tapering into nothing.
    fist = make_part(
        "Fist" + side_name,
        length=0.20,
        top_radius=0.135,
        bottom_radius=0.115,
        joint_world_position=(shoulder_x, 0.0, WRIST_HEIGHT - 0.02),
        seed=30 + int(side_sign),
    )
    parent_to(fist, forearm)

# ----------------------------------------------------------------------------------
# Legs — short, thick, and set wide
# ----------------------------------------------------------------------------------

for side_name, side_sign in (("L", -1.0), ("R", 1.0)):
    hip_x = side_sign * HIP_HALF_WIDTH

    thigh = make_part(
        "Thigh" + side_name,
        length=HIP_HEIGHT - KNEE_HEIGHT,
        top_radius=0.165,
        bottom_radius=0.135,
        joint_world_position=(hip_x, 0.0, HIP_HEIGHT - 0.02),
        seed=40 + int(side_sign),
    )
    parent_to(thigh, hips)

    shin = make_part(
        "Shin" + side_name,
        length=KNEE_HEIGHT - ANKLE_HEIGHT,
        top_radius=0.140,
        bottom_radius=0.110,
        joint_world_position=(hip_x, 0.0, KNEE_HEIGHT - 0.02),
        seed=50 + int(side_sign),
    )
    parent_to(shin, thigh)

    # Feet point forward along -Y, which is Blender's forward and becomes Unity's +Z
    # after the axis conversion on export.
    foot = tapered_tube(
        "Foot" + side_name,
        start=(0.0, 0.0, 0.0),
        end=(0.0, -0.26, 0.0),
        start_radius=0.115,
        end_radius=0.095,
        segments=LIMB_SIDES,
    )
    roughen(foot, amount=0.008, seed=60 + int(side_sign))
    recalc_normals(foot)
    shade_flat(foot)
    foot.location = (hip_x, 0.0, ANKLE_HEIGHT)
    parts_by_name["Foot" + side_name] = foot
    parent_to(foot, shin)

# ----------------------------------------------------------------------------------
# Export
# ----------------------------------------------------------------------------------
# The whole tree exports as one FBX. Selecting every object and exporting with
# use_selection keeps the parent/child relationships, which is the entire point.

bpy.ops.object.select_all(action="DESELECT")
for part in parts_by_name.values():
    part.select_set(True)
bpy.context.view_layer.objects.active = hips

os.makedirs(MODEL_DIR, exist_ok=True)
export_path = MODEL_DIR + "/GruntSegmented.fbx"
bpy.ops.export_scene.fbx(
    filepath=export_path,
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    object_types={"MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    bake_space_transform=False,   # must stay False, or the part origins are baked away
    axis_forward="-Z",
    axis_up="Y",
    path_mode="STRIP",
)

total_triangles = 0
for part in parts_by_name.values():
    total_triangles += triangle_count(part)

print("Segmented Grunt built.")
print("  parts:     " + str(len(parts_by_name)))
print("  triangles: " + str(total_triangles))
print("  names:     " + ", ".join(sorted(parts_by_name.keys())))
print("  exported:  " + export_path)

all_parts = list(parts_by_name.values())

for path in render_group(all_parts, "grunt_segmented"):
    print("  preview:   " + path)


# ----------------------------------------------------------------------------------
# Walk cycle proof
# ----------------------------------------------------------------------------------
# The point of this block is to check, before a single line of C# is written, that the
# joint origins are actually in the right places — that rotating UpperArmR swings the arm
# from the shoulder rather than sliding it sideways, and that the knee folds backwards.
#
# It also gives an honest look at the walk cycle itself. Motion cannot be judged from one
# still, but it CAN be judged from its extremes, and a walk that reads correctly at the
# contact and passing poses almost always reads correctly in motion.
#
# The maths here is deliberately the same maths ProceduralAnimator.cs will run in Unity,
# so anything wrong shows up here where it is cheap to fix.
#
# Blender axis note, since it decides every sign below: rotating about +X takes +Z to -Y
# and -Z to +Y. Forward is -Y. So for a limb hanging down (-Z) a NEGATIVE X rotation
# swings it forward, and for the torso (+Z) a POSITIVE X rotation leans it forward.

THIGH_SWING_DEGREES = 26.0
KNEE_BEND_DEGREES = 34.0
ARM_SWING_DEGREES = 20.0
ELBOW_REST_DEGREES = 15.0
TORSO_LEAN_DEGREES = 7.0
HIP_BOB_METRES = 0.035


def pose_walk(phase):
    """Pose the whole creature at one point in the stride, phase in radians."""

    def set_pitch(part_name, degrees):
        parts_by_name[part_name].rotation_euler = (math.radians(degrees), 0.0, 0.0)

    # Legs, half a cycle apart.
    set_pitch("ThighL", math.sin(phase) * THIGH_SWING_DEGREES)
    set_pitch("ThighR", math.sin(phase + math.pi) * THIGH_SWING_DEGREES)

    # The knee only ever folds backwards, and only on the back half of the swing. A knee
    # driven by a plain sine bends the wrong way for half of every step, which is the
    # single most common way a procedural walk looks broken.
    set_pitch("ShinL", max(0.0, math.sin(phase + 0.9 * math.pi)) * KNEE_BEND_DEGREES)
    set_pitch("ShinR", max(0.0, math.sin(phase + 1.9 * math.pi)) * KNEE_BEND_DEGREES)

    # Arms swing opposite the leg on the same side.
    set_pitch("UpperArmL", math.sin(phase + math.pi) * ARM_SWING_DEGREES)
    set_pitch("UpperArmR", math.sin(phase) * ARM_SWING_DEGREES)
    set_pitch("ForearmL", ELBOW_REST_DEGREES)
    set_pitch("ForearmR", ELBOW_REST_DEGREES)

    # A constant forward hunch, plus the twice-per-stride bob that carries the weight.
    set_pitch("Torso", TORSO_LEAN_DEGREES)
    hips.location = (0.0, 0.0, HIP_HEIGHT + math.cos(phase * 2.0) * HIP_BOB_METRES)


PHASE_LABELS = ["contact", "passing", "contact_opposite", "passing_opposite"]

for step_index, label in enumerate(PHASE_LABELS):
    phase = 2.0 * math.pi * step_index / len(PHASE_LABELS)
    pose_walk(phase)
    bpy.context.view_layer.update()

    written = render_group(all_parts, "grunt_walk_" + str(step_index) + "_" + label,
                           views=[("side", 90.0)], frame_padding=1.30)
    for path in written:
        print("  walk pose: " + path)
