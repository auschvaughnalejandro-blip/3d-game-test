"""Shared geometry helpers for the One Valley asset builds.

Each build script pastes this in with `exec(open(...).read())` so the whole thing arrives
at Blender as one self-contained block. Kept deliberately plain and explicit — every
function does one nameable thing and the maths is written out rather than compressed.

Conventions: Blender metres, Z-up. Export flips to Unity's Y-up on the way out.
"""

import bpy
import bmesh
import math
import random
import os


# ----------------------------------------------------------------------------------
# Scene
# ----------------------------------------------------------------------------------

def clear_scene():
    """Delete every object, leaving an empty scene."""
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        if block.users == 0:
            bpy.data.meshes.remove(block)


# ----------------------------------------------------------------------------------
# Primitives built from explicit vertices
# ----------------------------------------------------------------------------------

def revolve_closed_profile(name, profile, segments):
    """Spin a closed (radius, height) outline around the Z axis into a solid mesh.

    `profile` is a list of (radius, z) pairs tracing all the way around the cross
    section and back to the start — inner wall up, over the rim, outer wall down, along
    the underside. Because the outline is already closed, the result is watertight with
    no Solidify modifier and the wall thickness is exactly what was asked for.

    A profile point at radius 0 becomes a single pole vertex with a triangle fan, which
    is what lets a bowl have a proper centre instead of a pinhole.
    """
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    # One ring of vertices per profile point. Radius-0 points collapse to one shared
    # vertex rather than `segments` coincident ones.
    rings = []
    for radius, z in profile:
        if abs(radius) < 1.0e-6:
            rings.append([bm.verts.new((0.0, 0.0, z))])
        else:
            ring = []
            for step in range(segments):
                angle = 2.0 * math.pi * step / segments
                x = radius * math.cos(angle)
                y = radius * math.sin(angle)
                ring.append(bm.verts.new((x, y, z)))
            rings.append(ring)

    bm.verts.ensure_lookup_table()

    # Bridge each neighbouring pair of rings, wrapping the last back to the first.
    for index in range(len(rings)):
        lower = rings[index]
        upper = rings[(index + 1) % len(rings)]

        if len(lower) == 1 and len(upper) == 1:
            continue

        if len(lower) == 1:
            pole = lower[0]
            for step in range(segments):
                a = upper[step]
                b = upper[(step + 1) % segments]
                bm.faces.new((pole, b, a))
            continue

        if len(upper) == 1:
            pole = upper[0]
            for step in range(segments):
                a = lower[step]
                b = lower[(step + 1) % segments]
                bm.faces.new((pole, a, b))
            continue

        for step in range(segments):
            a = lower[step]
            b = lower[(step + 1) % segments]
            c = upper[(step + 1) % segments]
            d = upper[step]
            bm.faces.new((a, b, c, d))

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def tapered_tube(name, start, end, start_radius, end_radius, segments, cap=True):
    """A cone-ish tube running between two arbitrary points in space.

    Used for legs, hafts and shafts, where the axis is rarely one of the world axes.
    Builds a local frame from the start-to-end direction so the rings stay square to
    the tube rather than to the world.
    """
    start_vector = _Vector(start)
    end_vector = _Vector(end)
    axis = end_vector - start_vector
    length = axis.length()
    if length < 1.0e-9:
        raise ValueError("tapered_tube needs two distinct points")
    axis = axis.scaled(1.0 / length)

    # Any vector not parallel to the axis works as a seed for the perpendicular frame.
    seed = _Vector((0.0, 0.0, 1.0))
    if abs(axis.dot(seed)) > 0.95:
        seed = _Vector((1.0, 0.0, 0.0))
    side = axis.cross(seed).normalised()
    up = axis.cross(side).normalised()

    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    lower = []
    upper = []
    for step in range(segments):
        angle = 2.0 * math.pi * step / segments
        offset_x = math.cos(angle)
        offset_y = math.sin(angle)

        low_point = start_vector + side.scaled(offset_x * start_radius) + up.scaled(offset_y * start_radius)
        high_point = end_vector + side.scaled(offset_x * end_radius) + up.scaled(offset_y * end_radius)

        lower.append(bm.verts.new(low_point.as_tuple()))
        upper.append(bm.verts.new(high_point.as_tuple()))

    for step in range(segments):
        a = lower[step]
        b = lower[(step + 1) % segments]
        c = upper[(step + 1) % segments]
        d = upper[step]
        bm.faces.new((a, b, c, d))

    if cap:
        bm.faces.new(list(reversed(lower)))
        bm.faces.new(upper)

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


class _Vector:
    """A tiny 3D vector so the helpers do not depend on mathutils being imported."""

    def __init__(self, values):
        self.x, self.y, self.z = float(values[0]), float(values[1]), float(values[2])

    def __add__(self, other):
        return _Vector((self.x + other.x, self.y + other.y, self.z + other.z))

    def __sub__(self, other):
        return _Vector((self.x - other.x, self.y - other.y, self.z - other.z))

    def scaled(self, factor):
        return _Vector((self.x * factor, self.y * factor, self.z * factor))

    def dot(self, other):
        return self.x * other.x + self.y * other.y + self.z * other.z

    def cross(self, other):
        return _Vector((
            self.y * other.z - self.z * other.y,
            self.z * other.x - self.x * other.z,
            self.x * other.y - self.y * other.x,
        ))

    def length(self):
        return math.sqrt(self.dot(self))

    def normalised(self):
        length = self.length()
        return self.scaled(1.0 / length) if length > 1.0e-9 else _Vector((0.0, 0.0, 0.0))

    def as_tuple(self):
        return (self.x, self.y, self.z)


# ----------------------------------------------------------------------------------
# Surface treatment
# ----------------------------------------------------------------------------------

def roughen(obj, amount, seed, along_normal=True):
    """Nudge every vertex a little, so a lathed shape stops looking machined.

    This is what stands in for hammer marks and hand-cutting. Keep `amount` small —
    a few millimetres on a metre-scale object. Large values just look like noise.
    """
    random.seed(seed)
    mesh = obj.data
    mesh.calc_normals_split() if hasattr(mesh, "calc_normals_split") else None

    for vertex in mesh.vertices:
        jitter = (random.uniform(-1.0, 1.0) * amount,
                  random.uniform(-1.0, 1.0) * amount,
                  random.uniform(-1.0, 1.0) * amount)
        if along_normal:
            scale = random.uniform(-1.0, 1.0) * amount
            vertex.co.x += vertex.normal.x * scale
            vertex.co.y += vertex.normal.y * scale
            vertex.co.z += vertex.normal.z * scale
        else:
            vertex.co.x += jitter[0]
            vertex.co.y += jitter[1]
            vertex.co.z += jitter[2]


def warp_ring(obj, z_min, z_max, amount, lobes, seed=0):
    """Bend a horizontal band of the mesh up and down around its circumference.

    The brazier's rim is "warped by heat"; this is that, done as a slow sine around the
    object so the distortion reads as a shape rather than as noise.
    """
    random.seed(seed)
    phase = random.uniform(0.0, math.pi * 2.0)
    for vertex in obj.data.vertices:
        if z_min <= vertex.co.z <= z_max:
            angle = math.atan2(vertex.co.y, vertex.co.x)
            vertex.co.z += math.sin(angle * lobes + phase) * amount


def recalc_normals(obj):
    """Point every face outward.

    The revolve builds its pole fans by a winding rule that is correct at one end and
    inverted at the other, which Unity renders as a hole rather than as a surface. This
    is cheaper to call everywhere than to reason about per shape.
    """
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def shade_flat(obj):
    for polygon in obj.data.polygons:
        polygon.use_smooth = False


def shade_smooth(obj):
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def join_all(objects, name):
    """Merge a list of objects into one, returning the survivor."""
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    joined.data.name = name
    return joined


def bevel(obj, width, segments=1, angle_degrees=30.0):
    """Take the razor edge off hard-surface geometry. Applied immediately."""
    modifier = obj.modifiers.new(name="Bevel", type="BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(angle_degrees)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def triangle_count(obj):
    total = 0
    for polygon in obj.data.polygons:
        total += len(polygon.vertices) - 2
    return total


# ----------------------------------------------------------------------------------
# Output
# ----------------------------------------------------------------------------------

PROJECT_ROOT = r"c:/Users/Mark Alejandro/OneValley/OneValley-Transfer/unity-project"
MODEL_DIR = PROJECT_ROOT + "/Assets/Resources/Models"
PREVIEW_DIR = PROJECT_ROOT + "/Docs/previews"


def export_fbx(obj, filename):
    """Write one object to Assets/Resources/Models with Unity's axes.

    Blender is Z-up and Unity is Y-up. Without this conversion everything arrives in
    Unity lying on its side, which is the classic silent import failure.
    """
    os.makedirs(MODEL_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

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


def setup_preview_render():
    """Workbench with cavity shading — fast, and it shows form and silhouette clearly.

    Deliberately not a lit render. What needs judging at this stage is the shape, and a
    pretty render hides a bad one.
    """
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False

    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "SINGLE"
    shading.single_color = (0.62, 0.60, 0.63)
    shading.show_cavity = True
    shading.cavity_type = "BOTH"
    shading.curvature_ridge_factor = 1.0
    shading.curvature_valley_factor = 1.0
    shading.show_object_outline = True
    shading.show_shadows = False

    scene.view_settings.view_transform = "Standard"


def render_views(obj, basename, views=None):
    """Render an object from several angles and return the file paths written.

    Orthographic on purpose: it is the view that makes proportion errors obvious, and
    proportion is the thing most likely to be wrong.
    """
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    setup_preview_render()

    if views is None:
        views = [("front", 0.0), ("three_quarter", 38.0), ("side", 90.0)]

    # Frame the object from its bounding box so the camera fits any size of asset.
    corners = [obj.matrix_world @ _bbox_corner(obj, index) for index in range(8)]
    xs = [corner[0] for corner in corners]
    ys = [corner[1] for corner in corners]
    zs = [corner[2] for corner in corners]
    centre = ((min(xs) + max(xs)) / 2.0, (min(ys) + max(ys)) / 2.0, (min(zs) + max(zs)) / 2.0)
    extent = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = extent * 1.25
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    bpy.context.scene.camera = camera

    distance = extent * 3.0
    written = []

    for label, yaw_degrees in views:
        yaw = math.radians(yaw_degrees)
        pitch = math.radians(9.0)

        camera.location = (
            centre[0] + distance * math.sin(yaw) * math.cos(pitch),
            centre[1] - distance * math.cos(yaw) * math.cos(pitch),
            centre[2] + distance * math.sin(pitch),
        )
        camera.rotation_euler = (math.radians(90.0) - pitch, 0.0, yaw)

        path = PREVIEW_DIR + "/" + basename + "_" + label + ".png"
        bpy.context.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        written.append(path)

    return written


def _bbox_corner(obj, index):
    from mathutils import Vector
    return Vector(obj.bound_box[index])


def bounds_of(objects):
    """Centre and largest dimension across a whole group of objects.

    A segmented character is many objects, and framing the camera on any one of them
    (the hips, say) gives a close-up of a thigh rather than a creature.
    """
    xs, ys, zs = [], [], []
    for obj in objects:
        for index in range(8):
            corner = obj.matrix_world @ _bbox_corner(obj, index)
            xs.append(corner[0])
            ys.append(corner[1])
            zs.append(corner[2])

    centre = ((min(xs) + max(xs)) / 2.0, (min(ys) + max(ys)) / 2.0, (min(zs) + max(zs)) / 2.0)
    extent = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    return centre, extent


def render_group(objects, basename, views=None, frame_padding=1.18):
    """Render a group of objects together, framed on all of them at once."""
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    setup_preview_render()

    if views is None:
        views = [("front", 0.0), ("three_quarter", 38.0), ("side", 90.0)]

    centre, extent = bounds_of(objects)

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = extent * frame_padding
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    bpy.context.scene.camera = camera

    distance = extent * 3.0
    written = []

    for label, yaw_degrees in views:
        yaw = math.radians(yaw_degrees)
        pitch = math.radians(6.0)

        camera.location = (
            centre[0] + distance * math.sin(yaw) * math.cos(pitch),
            centre[1] - distance * math.cos(yaw) * math.cos(pitch),
            centre[2] + distance * math.sin(pitch),
        )
        camera.rotation_euler = (math.radians(90.0) - pitch, 0.0, yaw)

        path = PREVIEW_DIR + "/" + basename + "_" + label + ".png"
        bpy.context.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        written.append(path)

    bpy.data.objects.remove(camera, do_unlink=True)
    return written
