"""PROP-03 — floor-standing iron brazier.

Bible spec: "A shallow wide bowl on a slender three-legged stand, hand-forged black iron,
hammer marks visible, rim warped by heat, heavy soot staining. Empty — no fire in this
image. Simple and old."

Four in the dungeon, eight in the Vault, via MakeOneBrazier.

Everything here follows from a rule: the bowl is one revolved outline, the legs are one
leg repeated at 120 degrees, and the hand-forged look is a small random nudge applied
after the fact. Nothing about it needs an artist's eye, which is exactly why it is on the
list of things worth building this way.
"""

exec(open(r"c:/Users/Mark Alejandro/OneValley/OneValley-Transfer/unity-project/Tools/ov_kit.py").read())

clear_scene()

BOWL_SEGMENTS = 24
LEG_SEGMENTS = 8

# ----------------------------------------------------------------------------------
# The bowl — one closed cross section, revolved.
# ----------------------------------------------------------------------------------
# Traced as: centre of the inside floor, out and up the inner wall, over the rim, back
# down the outer wall, along the underside, home. Because the outline closes on itself
# the revolve is watertight and the wall thickness is literally the gap between the two
# halves of the list — about 22 mm, which is heavy hand-forged iron at this scale.

# Proportion note, learned from the first render: an early version stood 0.79 m tall and
# 0.78 m wide, and a vessel as wide as it is tall reads as a bird bath, not as a thing
# you light a fire in. The bowl came in and the stand went up. Taller than wide is what
# makes the silhouette say "brazier" — which matters doubly here, because the NEON and
# CHALK lenses draw silhouette and nothing else.

bowl_profile = [
    (0.000, 0.800),   # inside floor, centre  (pole)
    (0.100, 0.797),
    (0.185, 0.812),
    (0.255, 0.855),
    (0.300, 0.915),
    (0.316, 0.972),   # inner lip
    (0.336, 0.978),   # over the rim
    (0.322, 0.910),
    (0.276, 0.848),
    (0.200, 0.800),
    (0.105, 0.777),
    (0.000, 0.772),   # underside, centre  (pole)
]

bowl = revolve_closed_profile("BrazierBowl", bowl_profile, BOWL_SEGMENTS)

# "Rim warped by heat" — a slow three-lobed wave around the circumference. Three lobes
# rather than something faster, so it reads as a warped shape at gameplay distance
# instead of as a jagged edge.
warp_ring(bowl, z_min=0.90, z_max=1.00, amount=0.016, lobes=3, seed=11)

# ----------------------------------------------------------------------------------
# The stand — a short central column, three splayed legs, and a binding ring.
# ----------------------------------------------------------------------------------

parts = [bowl]

column = tapered_tube(
    "BrazierColumn",
    start=(0.0, 0.0, 0.420),
    end=(0.0, 0.0, 0.790),
    start_radius=0.046,
    end_radius=0.060,
    segments=12,
)
parts.append(column)

LEG_COUNT = 3
LEG_FOOT_RADIUS = 0.285

for leg_index in range(LEG_COUNT):
    angle = 2.0 * math.pi * leg_index / LEG_COUNT

    hip = (
        math.cos(angle) * 0.040,
        math.sin(angle) * 0.040,
        0.450,
    )
    foot = (
        math.cos(angle) * LEG_FOOT_RADIUS,
        math.sin(angle) * LEG_FOOT_RADIUS,
        0.005,
    )

    leg = tapered_tube(
        "BrazierLeg" + str(leg_index),
        start=hip,
        end=foot,
        start_radius=0.036,
        end_radius=0.021,
        segments=LEG_SEGMENTS,
    )
    parts.append(leg)

    # A small pad where the leg meets the floor, so it does not look like it is
    # balancing on a point.
    pad = tapered_tube(
        "BrazierFoot" + str(leg_index),
        start=(foot[0], foot[1], 0.030),
        end=(foot[0], foot[1], 0.000),
        start_radius=0.026,
        end_radius=0.040,
        segments=LEG_SEGMENTS,
    )
    parts.append(pad)

# The binding ring that ties the three legs together. Real braziers have one and it
# gives the silhouette something to read in the empty space under the bowl — which
# matters more than usual here, because two of the game's four lenses draw silhouette
# only.
RING_RADIUS = 0.183
RING_HEIGHT = 0.245
RING_THICKNESS = 0.016
ring_segments = 18

for segment_index in range(ring_segments):
    angle_a = 2.0 * math.pi * segment_index / ring_segments
    angle_b = 2.0 * math.pi * (segment_index + 1) / ring_segments

    ring_piece = tapered_tube(
        "BrazierRing" + str(segment_index),
        start=(math.cos(angle_a) * RING_RADIUS, math.sin(angle_a) * RING_RADIUS, RING_HEIGHT),
        end=(math.cos(angle_b) * RING_RADIUS, math.sin(angle_b) * RING_RADIUS, RING_HEIGHT),
        start_radius=RING_THICKNESS,
        end_radius=RING_THICKNESS,
        segments=6,
        cap=False,
    )
    parts.append(ring_piece)

# ----------------------------------------------------------------------------------
# Assemble and finish
# ----------------------------------------------------------------------------------

brazier = join_all(parts, "Brazier")

# "Hammer marks visible" — 3 mm of random push along the surface normal. Small on
# purpose. Anything larger stops reading as forged metal and starts reading as damage.
roughen(brazier, amount=0.003, seed=7)

shade_flat(brazier)

print("Brazier built.")
print("  triangles: " + str(triangle_count(brazier)))

corners = [brazier.matrix_world @ _bbox_corner(brazier, i) for i in range(8)]
height = max(c[2] for c in corners) - min(c[2] for c in corners)
width = max(c[0] for c in corners) - min(c[0] for c in corners)
print("  height:    " + str(round(height, 3)) + " m")
print("  width:     " + str(round(width, 3)) + " m")

exported = export_fbx(brazier, "Brazier.fbx")
print("  exported:  " + exported)

paths = render_views(brazier, "brazier")
for path in paths:
    print("  preview:   " + path)
