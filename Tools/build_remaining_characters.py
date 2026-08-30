"""CHR-05 Darter, CHR-01 Warden and CHR-10 Player, segmented.

Three creatures whose whole design problem is being unmistakable from one another in
silhouette - the game draws flat unlit colour in two of its four lenses, so outline is
all that survives. The Grunt is a squat brute, the Spitter is lopsided around one huge
arm; these three have to be as decisively different again.

Same contract as build_grunt_segmented.py: part names Hips, Torso, Head, ThighL/R,
ShinL/R, UpperArmL/R, ForearmL/R, joints on the pivots, no bones anywhere.

Run:  python Tools/blender_send.py Tools/build_remaining_characters.py
"""

import os

# blender_send.py defines ONEVALLEY_ROOT before it ships this file over the socket. When
# Blender runs the file directly (--background --python) nothing is injected, so fall back
# to this file's own location. Either way no machine-specific path is baked in.
if "ONEVALLEY_ROOT" in globals():
    PROJECT_ROOT = ONEVALLEY_ROOT
else:
    PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

exec(open(os.path.join(PROJECT_ROOT, "Tools", "ov_character_kit.py")).read())


# ==================================================================================
# CHR-05 — the Darter
# ==================================================================================
# "A fast lean predatory creature, slightly shorter than a man, built entirely for one
# straight-line lunge. Long powerful digitigrade hind legs, forward-leaning body, narrow
# head, small forelimbs held tight to the chest. Nervous, twitchy, all forward momentum.
# Unmistakably different from a heavy brute."
#
# MakeDarter's shell is 0.85 m tall, so this is built low and long rather than upright.
# Everything about the shape is one idea: the whole animal is pointed forwards, balanced
# over its hips, with the tail paying for the head. That is what a lunger looks like, and
# it is also the read that separates it from every other creature in the game at a glance.

def build_darter():
    parts = new_character()

    HIP_HEIGHT = 0.68

    hips = shaped_part(parts, "Hips",
                       rings=[(0.00, 0.150), (0.16, 0.132)],
                       joint=(0.0, 0.0, HIP_HEIGHT),
                       direction=(0.0, 0.30, 1.0), seed=1)

    # The body runs FORWARD from the hips, near horizontal, rather than standing up.
    TORSO_DIRECTION = (0.0, -0.94, 0.20)
    TORSO_LENGTH = 0.58
    TORSO_JOINT = (0.0, 0.0, HIP_HEIGHT)

    torso = shaped_part(parts, "Torso",
                        rings=[
                            (0.00, 0.148),   # over the hips
                            (0.15, 0.126),   # waist, drawn in
                            (0.38, 0.192),   # deep chest - where the lunge comes from
                            (TORSO_LENGTH, 0.108),
                        ],
                        joint=TORSO_JOINT, sides=8, seed=2,
                        direction=TORSO_DIRECTION)
    attach(torso, hips)

    NECK = step_along(TORSO_JOINT, TORSO_DIRECTION, TORSO_LENGTH)

    # Narrow head on a short neck, carried low and thrust forward.
    head = shaped_part(parts, "Head",
                       rings=[
                           (0.00, 0.088),
                           (0.09, 0.108),
                           (0.22, 0.072),
                           (0.34, 0.030),   # snout
                       ],
                       joint=NECK, sides=8, seed=3,
                       direction=(0.0, -1.0, -0.16))
    attach(head, torso)

    # Two small eyes set high and forward. Not articulated - they ride with the head.
    for side_name, side_sign in (("L", -1.0), ("R", 1.0)):
        eye = shaped_part(parts, "Eye" + side_name,
                          rings=[(0.00, 0.030), (0.032, 0.022)],
                          joint=(side_sign * 0.062, NECK[1] - 0.085, NECK[2] + 0.048),
                          sides=6, roughen_amount=0.0, seed=4,
                          direction=(side_sign * 0.55, -0.6, 0.25))
        attach(eye, head)

    # ---- digitigrade hind legs ----
    # Thigh forward and down, shin back and down, then a long metatarsal forward to the
    # toes. That Z-folded leg is the single most recognisable thing about the animal, and
    # it maps exactly onto the three parts the animator already knows: Thigh, Shin, Foot.

    for side_name, side_sign in (("L", -1.0), ("R", 1.0)):
        hip_x = side_sign * 0.115

        THIGH_DIRECTION = (0.0, -0.45, -1.0)
        THIGH_LENGTH = 0.28
        HIP_JOINT = (hip_x, 0.0, HIP_HEIGHT - 0.03)

        thigh = shaped_part(parts, "Thigh" + side_name,
                            rings=[
                                (0.00, 0.082),
                                (0.10, 0.122),   # heavy driving muscle
                                (0.22, 0.092),
                                (THIGH_LENGTH, 0.066),
                            ],
                            joint=HIP_JOINT, seed=40 + int(side_sign),
                            direction=THIGH_DIRECTION)
        attach(thigh, hips)

        KNEE = step_along(HIP_JOINT, THIGH_DIRECTION, THIGH_LENGTH)

        SHIN_DIRECTION = (0.0, 0.55, -1.0)   # folds back under the animal
        SHIN_LENGTH = 0.26

        shin = shaped_part(parts, "Shin" + side_name,
                           rings=[
                               (0.00, 0.062),
                               (0.09, 0.086),
                               (SHIN_LENGTH, 0.040),   # tendon-thin at the hock
                           ],
                           joint=KNEE, seed=50 + int(side_sign),
                           direction=SHIN_DIRECTION)
        attach(shin, thigh)

        HOCK = step_along(KNEE, SHIN_DIRECTION, SHIN_LENGTH)

        # The long metatarsal, and the reason the animal stands on its toes.
        foot = shaped_part(parts, "Foot" + side_name,
                           rings=[
                               (0.00, 0.042),
                               (0.14, 0.036),
                               (0.15, 0.030),
                               (0.21, 0.046),   # splayed toes on the ground
                           ],
                           joint=HOCK, seed=60 + int(side_sign),
                           direction=(0.0, -0.62, -1.0))
        attach(foot, shin)

    # ---- small forelimbs, held tight to the chest ----
    CHEST = step_along(TORSO_JOINT, TORSO_DIRECTION, 0.40)

    for side_name, side_sign in (("L", -1.0), ("R", 1.0)):
        SHOULDER = (side_sign * 0.135, CHEST[1] + 0.02, CHEST[2] + 0.02)
        UPPER_DIRECTION = (side_sign * 0.30, -0.55, -1.0)
        UPPER_LENGTH = 0.17

        upper = shaped_part(parts, "UpperArm" + side_name,
                            rings=[(0.00, 0.048), (0.07, 0.058), (UPPER_LENGTH, 0.038)],
                            joint=SHOULDER, seed=10 + int(side_sign),
                            direction=UPPER_DIRECTION)
        attach(upper, torso)

        ELBOW = step_along(SHOULDER, UPPER_DIRECTION, UPPER_LENGTH)

        forearm = shaped_part(parts, "Forearm" + side_name,
                              rings=[(0.00, 0.034), (0.08, 0.040), (0.16, 0.018)],
                              joint=ELBOW, seed=20 + int(side_sign),
                              direction=(side_sign * 0.10, -0.86, -0.50))
        attach(forearm, upper)

    # ---- tail, paying for the head ----
    TAIL = [
        ((0.0, 0.130, HIP_HEIGHT - 0.02), (0.0, 1.0, -0.10), 0.30, 0.108, 0.082, 70),
        (None, (0.0, 1.0, -0.26), 0.28, 0.078, 0.052, 71),
        (None, (0.0, 1.0, -0.42), 0.26, 0.048, 0.016, 72),
    ]

    previous = hips
    joint = TAIL[0][0]
    for index in range(len(TAIL)):
        _, direction, length, top_radius, bottom_radius, seed = TAIL[index]

        segment = shaped_part(parts, "Tail" + str(index + 1),
                              rings=[(0.00, top_radius), (length, bottom_radius)],
                              joint=joint, seed=seed, direction=direction)
        attach(segment, previous)
        previous = segment
        joint = step_along(joint, direction, length)

    return parts, hips


# ==================================================================================
# CHR-01 — the Warden
# ==================================================================================
# "A colossal armoured stone-and-iron guardian, three times human height, built to be
# imprisoned rather than to serve. Broad hunched shoulders, long heavy arms ending in
# blunt crushing fists, a short thick neck and a featureless helm-like head with a single
# horizontal slot. Massive, slow, immensely strong. Bold simple readable silhouette."
#
# Built at its true 3.65 m, which is the height MakeWarden's shell already uses. Note
# that ValleyBuilder currently passes modelScale 1.35 for the Warden, sized for the old
# single-mesh model - that has to become 1.0 or this arrives five metres tall.
#
# The design problem is that "big" is not a silhouette. What makes something read as
# colossal is proportion, not size: a small head, shoulders far wider than the hips, and
# arms long enough to hang past the knee.

def build_warden():
    parts = new_character()

    HIP_HEIGHT = 1.84
    SHOULDER_HEIGHT = 3.25
    KNEE_HEIGHT = 1.00
    ANKLE_HEIGHT = 0.28

    hips = shaped_part(parts, "Hips",
                       rings=[(0.00, 0.42), (0.30, 0.40)],
                       joint=(0.0, 0.0, HIP_HEIGHT),
                       sides=8, roughen_amount=0.020, seed=1,
                       direction=(0.0, 0.0, 1.0))

    # Hunched: the torso leans forward, and it is far broader at the chest than the hips.
    TORSO_DIRECTION = (0.0, -0.30, 1.0)
    TORSO_LENGTH = SHOULDER_HEIGHT - HIP_HEIGHT
    TORSO_JOINT = (0.0, 0.0, HIP_HEIGHT)

    torso = shaped_part(parts, "Torso",
                        rings=[
                            (0.00, 0.40),
                            (0.34, 0.36),
                            (0.86, 0.62),    # the great armoured chest
                            (TORSO_LENGTH, 0.50),
                        ],
                        joint=TORSO_JOINT, sides=8, roughen_amount=0.022, seed=2,
                        direction=TORSO_DIRECTION)
    attach(torso, hips)

    SHOULDER_TOP = step_along(TORSO_JOINT, TORSO_DIRECTION, TORSO_LENGTH)

    # A shoulder slab far wider than the hips. This is most of the silhouette.
    shoulders = shaped_part(parts, "Shoulders",
                            rings=[(0.00, 0.30), (0.42, 0.46), (0.92, 0.30)],
                            joint=(-0.46, SHOULDER_TOP[1], SHOULDER_TOP[2] - 0.10),
                            sides=8, roughen_amount=0.020, seed=3,
                            direction=(1.0, 0.0, 0.10))
    shoulders.scale = (1.0, 0.66, 1.0)
    attach(shoulders, torso)

    # A small featureless helm, set low between the shoulders. Small on purpose: nothing
    # says "enormous" like a head that looks too little for the body carrying it.
    head = shaped_part(parts, "Head",
                       rings=[(0.00, 0.20), (0.14, 0.25), (0.34, 0.22), (0.42, 0.15)],
                       joint=(0.0, SHOULDER_TOP[1] - 0.05, SHOULDER_TOP[2] - 0.06),
                       sides=8, roughen_amount=0.014, seed=4,
                       direction=(0.0, -0.14, 1.0))
    attach(head, torso)

    # The single horizontal slot - the only feature on the whole head.
    slot = shaped_part(parts, "HeadSlot",
                       rings=[(0.00, 0.055), (0.30, 0.055)],
                       joint=(-0.15, SHOULDER_TOP[1] - 0.30, SHOULDER_TOP[2] + 0.20),
                       sides=4, roughen_amount=0.0, seed=5,
                       direction=(1.0, 0.0, 0.0))
    slot.scale = (1.0, 0.34, 1.0)
    attach(slot, head)

    # ---- long heavy arms, fists hanging past the knee ----
    for side_name, side_sign in (("L", -1.0), ("R", 1.0)):
        SHOULDER = (side_sign * 0.62, SHOULDER_TOP[1] + 0.04, SHOULDER_TOP[2] - 0.06)

        UPPER_DIRECTION = (side_sign * 0.24, -0.06, -1.0)
        UPPER_LENGTH = 0.86

        upper = shaped_part(parts, "UpperArm" + side_name,
                            rings=[
                                (0.00, 0.20),
                                (0.20, 0.29),
                                (0.52, 0.25),
                                (UPPER_LENGTH, 0.19),
                            ],
                            joint=SHOULDER, sides=8, roughen_amount=0.020,
                            seed=10 + int(side_sign), direction=UPPER_DIRECTION)
        attach(upper, shoulders)

        ELBOW = step_along(SHOULDER, UPPER_DIRECTION, UPPER_LENGTH)

        FOREARM_DIRECTION = (side_sign * -0.06, -0.20, -1.0)
        FOREARM_LENGTH = 0.78

        forearm = shaped_part(parts, "Forearm" + side_name,
                              rings=[
                                  (0.00, 0.185),
                                  (0.22, 0.255),
                                  (FOREARM_LENGTH, 0.235),
                              ],
                              joint=ELBOW, sides=8, roughen_amount=0.020,
                              seed=20 + int(side_sign), direction=FOREARM_DIRECTION)
        attach(forearm, upper)

        WRIST = step_along(ELBOW, FOREARM_DIRECTION, FOREARM_LENGTH)

        fist = shaped_part(parts, "Fist" + side_name,
                           rings=[(0.00, 0.26), (0.16, 0.31), (0.40, 0.22)],
                           joint=WRIST, sides=8, roughen_amount=0.024,
                           seed=30 + int(side_sign), direction=(0.0, -0.10, -1.0))
        attach(fist, forearm)

    # ---- short thick legs ----
    for side_name, side_sign in (("L", -1.0), ("R", 1.0)):
        hip_x = side_sign * 0.30

        thigh = shaped_part(parts, "Thigh" + side_name,
                            rings=[
                                (0.00, 0.28),
                                (0.22, 0.36),
                                (HIP_HEIGHT - KNEE_HEIGHT, 0.27),
                            ],
                            joint=(hip_x, 0.0, HIP_HEIGHT - 0.06),
                            sides=8, roughen_amount=0.020,
                            seed=40 + int(side_sign), direction=(side_sign * 0.05, 0.0, -1.0))
        attach(thigh, hips)

        shin = shaped_part(parts, "Shin" + side_name,
                           rings=[
                               (0.00, 0.25),
                               (0.20, 0.31),
                               (KNEE_HEIGHT - ANKLE_HEIGHT, 0.21),
                           ],
                           joint=(hip_x + side_sign * 0.04, 0.0, KNEE_HEIGHT),
                           sides=8, roughen_amount=0.020,
                           seed=50 + int(side_sign), direction=(0.0, 0.0, -1.0))
        attach(shin, thigh)

        foot = shaped_part(parts, "Foot" + side_name,
                           rings=[(0.00, 0.24), (0.20, 0.26), (0.46, 0.20)],
                           joint=(hip_x + side_sign * 0.04, 0.06, ANKLE_HEIGHT),
                           sides=6, roughen_amount=0.016,
                           seed=60 + int(side_sign), direction=(0.0, -1.0, -0.16))
        attach(foot, shin)

    return parts, hips


# ==================================================================================
# CHR-10 — the Player
# ==================================================================================
# "A lightly armoured traveller, ordinary human build, mid-twenties. Blue-grey padded
# tunic over dark trousers, worn leather belt and boots, a single shoulder guard on the
# right, no helmet, hair tied back. Practical, travel-worn, not heroic and not ornate.
# Must read clearly from BEHIND: distinctive shoulder line and back detail, since that is
# the view for the entire game."
#
# 1.77 m, which is what BuildThePlayer already uses.
#
# Said plainly: this is the weakest of the four. A human at close third-person range is
# the hardest thing on the whole asset list to build this way, and a free rigged Mixamo
# character would beat it outright and arrive animated. This exists so the game is
# complete and consistent, not because it is the right long-term answer.

def build_player():
    parts = new_character()

    HIP_HEIGHT = 0.96
    SHOULDER_HEIGHT = 1.46
    KNEE_HEIGHT = 0.50
    ANKLE_HEIGHT = 0.09

    hips = shaped_part(parts, "Hips",
                       rings=[(0.00, 0.148), (0.16, 0.140)],
                       joint=(0.0, 0.0, HIP_HEIGHT),
                       sides=8, roughen_amount=0.004, seed=1,
                       direction=(0.0, 0.0, 1.0))

    TORSO_DIRECTION = (0.0, -0.06, 1.0)
    TORSO_LENGTH = SHOULDER_HEIGHT - HIP_HEIGHT
    TORSO_JOINT = (0.0, 0.0, HIP_HEIGHT)

    torso = shaped_part(parts, "Torso",
                        rings=[
                            (0.00, 0.145),
                            (0.16, 0.132),   # waist
                            (0.38, 0.190),   # chest
                            (TORSO_LENGTH, 0.168),
                        ],
                        joint=TORSO_JOINT, sides=8, roughen_amount=0.004, seed=2,
                        direction=TORSO_DIRECTION)
    attach(torso, hips)

    SHOULDER_TOP = step_along(TORSO_JOINT, TORSO_DIRECTION, TORSO_LENGTH)

    shoulders = shaped_part(parts, "Shoulders",
                            rings=[(0.00, 0.098), (0.20, 0.118), (0.40, 0.098)],
                            joint=(-0.20, SHOULDER_TOP[1], SHOULDER_TOP[2] - 0.02),
                            sides=8, roughen_amount=0.004, seed=3,
                            direction=(1.0, 0.0, 0.0))
    attach(shoulders, torso)

    # Head, and hair tied back - the back of the head is the view the player actually has
    # for the whole game, so the tie is the one piece of detail worth spending on.
    head = shaped_part(parts, "Head",
                       rings=[(0.00, 0.072), (0.06, 0.100), (0.19, 0.104), (0.26, 0.062)],
                       joint=(0.0, SHOULDER_TOP[1] - 0.01, SHOULDER_TOP[2] + 0.03),
                       sides=8, roughen_amount=0.003, seed=4,
                       direction=(0.0, -0.04, 1.0))
    attach(head, torso)

    hair = shaped_part(parts, "Hair",
                       rings=[(0.00, 0.052), (0.10, 0.040), (0.20, 0.020)],
                       joint=(0.0, SHOULDER_TOP[1] + 0.085, SHOULDER_TOP[2] + 0.20),
                       sides=6, roughen_amount=0.003, seed=5,
                       direction=(0.0, 0.55, -1.0))
    attach(hair, head)

    # The single shoulder guard, on the right. The one asymmetry in the whole figure, and
    # the thing that tells the player which way round they are at eight metres.
    guard = shaped_part(parts, "ShoulderGuard",
                        rings=[(0.00, 0.128), (0.09, 0.140), (0.20, 0.104)],
                        joint=(0.20, SHOULDER_TOP[1], SHOULDER_TOP[2] + 0.03),
                        sides=8, roughen_amount=0.005, seed=6,
                        direction=(0.34, 0.0, -1.0))
    attach(guard, shoulders)

    # ---- clothing and kit ----
    # Everything below is chosen by one test: does it change the OUTLINE? NEON and CHALK
    # draw flat unlit colour with no shading at all, so a crease or a fold or a painted
    # seam is simply not there in half the game's visual styles. A belt that stands proud
    # of the waist survives all four; a wrinkle drawn on the tunic survives two.

    # The belt. A closed loop revolved into a ring, so it stands off the body rather than
    # being a stripe painted on it.
    belt = revolved_part(parts, "Belt", [
        (0.150, -0.030),
        (0.178, -0.019),
        (0.178, 0.019),
        (0.150, 0.030),
        (0.143, 0.019),
        (0.143, -0.019),
    ], joint=(0.0, 0.0, HIP_HEIGHT + 0.035), sides=12, roughen_amount=0.003, seed=7)
    attach(belt, hips)

    buckle = shaped_part(parts, "BeltBuckle",
                         rings=[(0.00, 0.042), (0.030, 0.038)],
                         joint=(0.0, -0.168, HIP_HEIGHT + 0.035),
                         sides=4, roughen_amount=0.0, seed=8,
                         direction=(0.0, -1.0, 0.0))
    buckle.scale = (1.0, 1.35, 1.0)
    attach(buckle, belt)

    # The tunic skirt, flaring from the belt to mid-thigh. This is the single biggest
    # change to the silhouette - it turns a pair of bare legs into a clothed figure.
    tunic = revolved_part(parts, "TunicSkirt", [
        (0.152, 0.000),
        (0.196, -0.130),
        (0.216, -0.250),
        (0.203, -0.262),
        (0.180, -0.135),
        (0.142, -0.008),
    ], joint=(0.0, 0.0, HIP_HEIGHT + 0.020), sides=12, roughen_amount=0.004, seed=9)
    attach(tunic, hips)

    # Collar at the neck, and a strap across the chest for the pack he is carrying.
    collar = revolved_part(parts, "Collar", [
        (0.098, -0.024),
        (0.124, -0.014),
        (0.124, 0.016),
        (0.098, 0.026),
        (0.090, 0.016),
        (0.090, -0.014),
    ], joint=(0.0, SHOULDER_TOP[1] - 0.01, SHOULDER_TOP[2] + 0.030),
        sides=10, roughen_amount=0.003, seed=15)
    attach(collar, torso)

    strap = shaped_part(parts, "ChestStrap",
                        rings=[(0.00, 0.030), (0.42, 0.030)],
                        joint=(-0.140, -0.128, SHOULDER_TOP[2] - 0.020),
                        sides=4, roughen_amount=0.003, seed=16,
                        direction=(0.62, 0.16, -1.0))
    strap.scale = (1.0, 0.42, 1.0)
    attach(strap, torso)

    # ---- a face, such as it is ----
    # Deliberately just a brow and a nose. The game is third-person from behind for its
    # whole runtime - CHR-11 in the bible exists precisely because the BACK view is the
    # one that matters - so anything more here is detail nobody will ever be positioned
    # to see, and eyes at this scale read as two dark specks at best.

    brow = shaped_part(parts, "Brow",
                       rings=[(0.00, 0.030), (0.115, 0.030)],
                       joint=(-0.058, SHOULDER_TOP[1] - 0.082, SHOULDER_TOP[2] + 0.175),
                       sides=4, roughen_amount=0.0, seed=17,
                       direction=(1.0, 0.0, 0.0))
    brow.scale = (1.0, 0.55, 1.0)
    attach(brow, head)

    nose = shaped_part(parts, "Nose",
                       rings=[(0.00, 0.026), (0.042, 0.016)],
                       joint=(0.0, SHOULDER_TOP[1] - 0.082, SHOULDER_TOP[2] + 0.150),
                       sides=4, roughen_amount=0.0, seed=18,
                       direction=(0.0, -1.0, -0.30))
    attach(nose, head)

    for side_name, side_sign in (("L", -1.0), ("R", 1.0)):
        SHOULDER = (side_sign * 0.185, SHOULDER_TOP[1], SHOULDER_TOP[2] - 0.01)

        UPPER_DIRECTION = (side_sign * 0.14, -0.04, -1.0)
        UPPER_LENGTH = 0.30

        upper = shaped_part(parts, "UpperArm" + side_name,
                            rings=[(0.00, 0.060), (0.10, 0.076), (UPPER_LENGTH, 0.056)],
                            joint=SHOULDER, roughen_amount=0.004,
                            seed=10 + int(side_sign), direction=UPPER_DIRECTION)
        attach(upper, shoulders)

        ELBOW = step_along(SHOULDER, UPPER_DIRECTION, UPPER_LENGTH)

        FOREARM_DIRECTION = (side_sign * 0.04, -0.14, -1.0)
        FOREARM_LENGTH = 0.28

        forearm = shaped_part(parts, "Forearm" + side_name,
                              rings=[(0.00, 0.052), (0.10, 0.062), (FOREARM_LENGTH, 0.044)],
                              joint=ELBOW, roughen_amount=0.004,
                              seed=20 + int(side_sign), direction=FOREARM_DIRECTION)
        attach(forearm, upper)

        WRIST = step_along(ELBOW, FOREARM_DIRECTION, FOREARM_LENGTH)

        # A leather bracer over the forearm. Stands proud of the arm by 8 mm, which is
        # enough to break the limb's outline into two shapes instead of one tube.
        bracer = shaped_part(parts, "Bracer" + side_name,
                             rings=[(0.00, 0.062), (0.03, 0.066), (0.13, 0.062), (0.16, 0.056)],
                             joint=step_along(ELBOW, FOREARM_DIRECTION, 0.10),
                             roughen_amount=0.003,
                             seed=70 + int(side_sign), direction=FOREARM_DIRECTION)
        attach(bracer, forearm)

        hand = shaped_part(parts, "Hand" + side_name,
                           rings=[(0.00, 0.046), (0.05, 0.054), (0.13, 0.036)],
                           joint=WRIST, roughen_amount=0.003,
                           seed=30 + int(side_sign), direction=(0.0, -0.10, -1.0))
        attach(hand, forearm)

    for side_name, side_sign in (("L", -1.0), ("R", 1.0)):
        hip_x = side_sign * 0.098

        thigh = shaped_part(parts, "Thigh" + side_name,
                            rings=[(0.00, 0.098), (0.14, 0.114), (HIP_HEIGHT - KNEE_HEIGHT, 0.082)],
                            joint=(hip_x, 0.0, HIP_HEIGHT - 0.04),
                            sides=8, roughen_amount=0.004,
                            seed=40 + int(side_sign), direction=(side_sign * 0.04, 0.0, -1.0))
        attach(thigh, hips)

        shin = shaped_part(parts, "Shin" + side_name,
                           rings=[(0.00, 0.076), (0.10, 0.090), (KNEE_HEIGHT - ANKLE_HEIGHT, 0.056)],
                           joint=(hip_x + side_sign * 0.02, 0.0, KNEE_HEIGHT),
                           sides=8, roughen_amount=0.004,
                           seed=50 + int(side_sign), direction=(0.0, 0.0, -1.0))
        attach(shin, thigh)

        # The turned-down cuff at the top of the boot - a travelling boot, not a greave.
        cuff = shaped_part(parts, "BootCuff" + side_name,
                           rings=[(0.00, 0.092), (0.05, 0.086), (0.11, 0.074)],
                           joint=(hip_x + side_sign * 0.02, 0.0, ANKLE_HEIGHT + 0.155),
                           sides=8, roughen_amount=0.004,
                           seed=80 + int(side_sign), direction=(0.0, 0.0, -1.0))
        attach(cuff, shin)

        boot = shaped_part(parts, "Foot" + side_name,
                           rings=[(0.00, 0.070), (0.12, 0.072), (0.25, 0.056)],
                           joint=(hip_x + side_sign * 0.02, 0.03, ANKLE_HEIGHT),
                           sides=6, roughen_amount=0.004,
                           seed=60 + int(side_sign), direction=(0.0, -1.0, -0.18))
        attach(boot, shin)

    return parts, hips


# ==================================================================================
# Build all three
# ==================================================================================

print("CHR-05 Darter")
darter_parts, darter_root = build_darter()
finish_character(darter_parts, "DarterSegmented.fbx", darter_root, "darter_segmented")
write_meta_axis_fix("DarterSegmented.fbx")

print("")
print("CHR-01 Warden")
warden_parts, warden_root = build_warden()
finish_character(warden_parts, "WardenSegmented.fbx", warden_root, "warden_segmented")
write_meta_axis_fix("WardenSegmented.fbx")

print("")
print("CHR-10 Player")
player_parts, player_root = build_player()
finish_character(player_parts, "PlayerSegmented.fbx", player_root, "player_segmented")
write_meta_axis_fix("PlayerSegmented.fbx")

print("")
print("All three exported to Assets/Resources/Models/")
