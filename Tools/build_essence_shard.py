"""PROP-01 — essence shard pickup.

Bible spec: "A sharp angular teal crystal about the size of a thumb, translucent, glowing
from within, with two or three tiny fragments orbiting it. Emissive and self-lit. Reads
instantly as 'pick me up'."

Replaces the small teal cube in EssencePickup.SpawnAt. The player collects dozens of
these, so it is one of the most-seen objects in the game.

A crystal is the purest case of a shape that follows from a rule: revolve a tapered
outline over a small number of segments and every facet is correct by construction.
Randomising the facet radii is what stops it looking like a machined gemstone.
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

# Six sides gives clean readable facets that still catch light differently from each
# other. More sides and it starts to look like a smooth cone at gameplay distance.
CRYSTAL_SIDES = 6


def make_crystal(name, height, waist_radius, waist_fraction, seed, sides=CRYSTAL_SIDES):
    """One faceted spike: a long point at the top, a short one at the bottom.

    `waist_fraction` is where along the height the crystal is widest. Putting it below
    the midpoint gives the long elegant upper point that reads as "crystal" rather than
    the symmetrical double cone that reads as "dice".
    """
    random.seed(seed)

    waist_z = height * waist_fraction
    shoulder_z = height * (waist_fraction + 0.30)

    profile = [
        (0.0, height),                      # top point (pole)
        (waist_radius * 0.42, shoulder_z),
        (waist_radius, waist_z),
        (waist_radius * 0.66, height * 0.13),
        (0.0, 0.0),                         # bottom point (pole)
    ]

    crystal = revolve_closed_profile(name, profile, sides)

    # Push each facet column in or out a little so no two faces are the same width.
    # This is the whole difference between "crystal" and "extruded hexagon".
    facet_jitter = [random.uniform(0.82, 1.18) for _ in range(sides)]
    for index, vertex in enumerate(crystal.data.vertices):
        radius = math.sqrt(vertex.co.x ** 2 + vertex.co.y ** 2)
        if radius < 1.0e-5:
            continue
        angle = math.atan2(vertex.co.y, vertex.co.x)
        column = int(round(angle / (2.0 * math.pi) * sides)) % sides
        scale = facet_jitter[column]
        vertex.co.x *= scale
        vertex.co.y *= scale

    recalc_normals(crystal)
    shade_flat(crystal)
    return crystal


# ----------------------------------------------------------------------------------
# The main shard
# ----------------------------------------------------------------------------------
# Size note: the bible says "the size of a thumb", which at true scale is about 60 mm and
# invisible on the floor of a dungeon from a third-person camera. Pickups are always
# drawn larger than life for this reason. 190 mm still reads as a shard in the hand and
# is findable at eight metres, which is the distance that actually matters.

shard = make_crystal("EssenceShard", height=0.190, waist_radius=0.043,
                     waist_fraction=0.30, seed=3)

parts = [shard]

# ----------------------------------------------------------------------------------
# The orbiting fragments
# ----------------------------------------------------------------------------------
# Three of them, at different radii and heights so they do not sit in a flat ring. They
# are part of the same mesh rather than separate objects: the pickup already spins as a
# whole, and one mesh means one draw call for something there are dozens of on screen.

fragment_placements = [
    (0.088, math.radians(25.0), 0.150, 0.055),
    (0.076, math.radians(155.0), 0.088, 0.042),
    (0.094, math.radians(268.0), 0.038, 0.048),
]

for index, (orbit_radius, orbit_angle, orbit_height, fragment_height) in enumerate(fragment_placements):
    fragment = make_crystal(
        "EssenceFragment" + str(index),
        height=fragment_height,
        waist_radius=fragment_height * 0.26,
        waist_fraction=0.34,
        seed=20 + index,
        sides=5,
    )

    fragment.location = (
        math.cos(orbit_angle) * orbit_radius,
        math.sin(orbit_angle) * orbit_radius,
        orbit_height,
    )
    # Tumbled, not aligned. Fragments that all point up look like a candelabra.
    random.seed(40 + index)
    fragment.rotation_euler = (
        random.uniform(-1.2, 1.2),
        random.uniform(-1.2, 1.2),
        random.uniform(0.0, 6.28),
    )
    bpy.context.view_layer.objects.active = fragment
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    parts.append(fragment)

essence = join_all(parts, "EssenceShard")
shade_flat(essence)

print("Essence shard built.")
print("  triangles: " + str(triangle_count(essence)))

corners = [essence.matrix_world @ _bbox_corner(essence, i) for i in range(8)]
print("  height:    " + str(round(max(c[2] for c in corners) - min(c[2] for c in corners), 3)) + " m")
print("  width:     " + str(round(max(c[0] for c in corners) - min(c[0] for c in corners), 3)) + " m")

print("  exported:  " + export_fbx(essence, "EssenceShard.fbx"))

for path in render_views(essence, "essence_shard"):
    print("  preview:   " + path)
