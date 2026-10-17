"""Renders all built assets side by side at real scale next to a 1.8 m reference figure.
Fixed camera angle, light and color management so outliers are visible at a glance."""
import math

import bpy
from mathutils import Euler, Matrix, Vector

SPACING = 0.35
MIN_SLOT = 0.75
BACKGROUND = (0.50, 0.53, 0.56)
GROUND = (0.62, 0.60, 0.55)
LABEL = (0.08, 0.07, 0.09)


def _flat_material(name, rgb):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 1.0
    return mat


def _world_bounds(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx


def _label(text, x, y, size, mat):
    curve = bpy.data.curves.new("label", "FONT")
    curve.body = text
    curve.size = size
    curve.align_x = "CENTER"
    obj = bpy.data.objects.new("label", curve)
    obj.location = (x, y, 0.02)
    obj.rotation_euler = Euler((math.radians(90), 0, 0))
    obj.data.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _reference_figure(palette_material, palette):
    """Neutral 1.8 m capsule-ish dummy built from palette color 'linen'."""
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.2, radius2=0.2, depth=1.52,
                          matrix=Matrix.Translation((0, 0, 0.76)))
    bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=5, radius=0.13,
                              matrix=Matrix.Translation((0, 0, 1.67)))
    uv_layer = bm.loops.layers.uv.new("UVMap")
    u = palette.uv(palette.index("linen"))
    for f in bm.faces:
        f.smooth = False
        for loop in f.loops:
            loop[uv_layer].uv = u
    mesh = bpy.data.meshes.new("ref_1_8m")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(palette_material)
    obj = bpy.data.objects.new("ref_1_8m", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _set_enum(owner, prop, candidates):
    for value in candidates:
        try:
            setattr(owner, prop, value)
            return value
        except TypeError:
            continue
    return getattr(owner, prop)


def render(entries, palette_material, palette, out_path):
    scene = bpy.context.scene
    label_mat = _flat_material("sheet_label", LABEL)

    ref = _reference_figure(palette_material, palette)
    items = [(ref, "Referenz 1,8 m", None)] + [(e["object"], e["name"], e["tris"]) for e in entries]

    x = 0.0
    all_pts = []
    for obj, name, tris in items:
        mn, mx = _world_bounds(obj)
        width = max(mx.x - mn.x, MIN_SLOT)
        obj.location.x += x + width * 0.5 - (mn.x + mx.x) * 0.5
        obj.location.y -= (mn.y + mx.y) * 0.5
        obj.location.z -= mn.z
        bpy.context.view_layer.update()
        mn, mx = _world_bounds(obj)
        cx = (mn.x + mx.x) * 0.5
        text = name if tris is None else f"{name}  ({tris} tris)"
        _label(text, cx, -0.45, 0.05, label_mat)
        all_pts += [mn, mx, Vector((cx, -0.6, 0.0))]
        x = cx + width * 0.5 + SPACING

    total_w = x - SPACING
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(total_w * 0.5, 0.5, 0.0))
    ground = bpy.context.active_object
    ground.scale = (total_w + 4.0, 5.0, 1.0)
    ground.data.materials.append(_flat_material("sheet_ground", GROUND))

    world = bpy.data.worlds.new("sheet_world")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (*BACKGROUND, 1.0)
    bg.inputs["Strength"].default_value = 0.6
    scene.world = world

    sun_data = bpy.data.lights.new("sun", "SUN")
    sun_data.energy = 3.5
    sun_data.angle = math.radians(8.0)
    sun = bpy.data.objects.new("sun", sun_data)
    sun.rotation_euler = Euler((math.radians(50), 0, math.radians(-35)))
    scene.collection.objects.link(sun)

    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cam_data)
    cam.rotation_euler = Euler((math.radians(72), 0, math.radians(-12)))
    scene.collection.objects.link(cam)
    scene.camera = cam

    scene.render.resolution_x = 2000
    scene.render.resolution_y = 1000
    # Fit the ortho camera to every bounding-box corner in camera space
    rot = cam.rotation_euler.to_matrix()
    inv = rot.inverted()
    pts = []
    for obj, _, _ in items:
        pts += [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    pts += [Vector((p.x, p.y, 0.0)) for p in all_pts]
    local = [inv @ p for p in pts]
    xs, ys = [p.x for p in local], [p.y for p in local]
    aspect = scene.render.resolution_x / scene.render.resolution_y
    cam_data.ortho_scale = max(max(xs) - min(xs), (max(ys) - min(ys)) * aspect) * 1.08
    center = Vector(((max(xs) + min(xs)) * 0.5, (max(ys) + min(ys)) * 0.5, max(p.z for p in local) + 20.0))
    cam.location = rot @ center
    cam_data.clip_end = 100.0

    engine = _set_enum(scene.render, "engine", ["BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "BLENDER_WORKBENCH"])
    _set_enum(scene.view_settings, "view_transform", ["Standard"])
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True)
    return engine
