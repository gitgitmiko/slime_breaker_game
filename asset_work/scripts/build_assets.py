"""Generate original Slime Breaker assets (anime sorceress hero, slimes, boss, arena, VFX meshes)
and export Unity-ready FBX files.

Run:
  blender --background --python asset_work/scripts/build_assets.py -- <out_dir> <preview_dir> <texture_dir>

Blender axes: Z up, characters face -Y. The FBX exporter (forward -Z, up Y) turns that into
Unity's +Z forward.
"""

import json
import math
import random
import os
import subprocess
import sys
import traceback

import bmesh
import bpy
import mathutils

PALETTE = {
    # hero (chibi black-haired cat-boy witch)
    "skin": (0.86, 0.60, 0.50, 1),
    "blush": (0.92, 0.42, 0.40, 1),
    "hair": (0.045, 0.045, 0.06, 1),
    "hair_dark": (0.02, 0.02, 0.026, 1),
    "hair_light": (0.60, 0.64, 0.86, 1),
    "robe": (0.09, 0.09, 0.17, 1),
    "robe_light": (0.42, 0.26, 0.72, 1),
    "hat": (0.20, 0.11, 0.40, 1),
    "hat_band": (0.48, 0.28, 0.82, 1),
    "shirt": (0.10, 0.11, 0.20, 1),
    "shorts": (0.035, 0.035, 0.045, 1),
    "choker": (0.03, 0.03, 0.035, 1),
    "ear_inner": (0.93, 0.87, 0.78, 1),
    "frill": (0.97, 0.95, 1.0, 1),
    "stocking": (0.21, 0.13, 0.36, 1),
    "cape_inner": (0.34, 0.19, 0.60, 1),
    "boot": (0.17, 0.10, 0.27, 1),
    "gold": (0.96, 0.78, 0.30, 1),
    "iris": (0.49, 0.36, 1.0, 1),
    "pupil": (0.08, 0.06, 0.16, 1),
    "eye_white": (0.98, 0.98, 1.0, 1),
    "eye_highlight": (1.0, 1.0, 1.0, 1),
    "mouth": (0.22, 0.07, 0.07, 1),
    "crystal": (0.55, 0.90, 1.0, 1),
    "staff_wood": (0.36, 0.22, 0.14, 1),
    # enemies
    "slime": (0.22, 0.82, 0.48, 1),
    "slime_dark": (0.12, 0.58, 0.34, 1),
    "slime_fast": (0.15, 0.78, 1.0, 1),
    "slime_fin": (0.98, 0.84, 0.22, 1),
    "slime_tank": (0.46, 0.32, 0.62, 1),
    "rock": (0.45, 0.47, 0.52, 1),
    "king": (0.82, 0.18, 0.42, 1),
    "king_cape": (0.32, 0.08, 0.22, 1),
    "gem": (0.45, 0.95, 0.85, 1),
    "eye": (0.08, 0.09, 0.12, 1),
    # environment
    "bark": (0.40, 0.26, 0.15, 1),
    "leaf": (0.20, 0.58, 0.28, 1),
    "leaf_dark": (0.12, 0.40, 0.22, 1),
    "leaf_light": (0.40, 0.74, 0.34, 1),
    "mushroom": (0.88, 0.34, 0.30, 1),
    "mushroom_spot": (0.98, 0.95, 0.88, 1),
    "stem": (0.93, 0.88, 0.78, 1),
    "moss": (0.24, 0.48, 0.22, 1),
    "ruin": (0.58, 0.59, 0.62, 1),
    "grass": (0.36, 0.70, 0.28, 1),
    "petal": (1.0, 0.75, 0.85, 1),
    "petal_center": (1.0, 0.86, 0.30, 1),
    "boundary": (0.42, 0.44, 0.50, 1),
    "rune": (0.30, 0.90, 1.0, 1),
    "cactus": (0.22, 0.58, 0.24, 1),
    "cactus_dark": (0.14, 0.40, 0.16, 1),
    "sandstone": (0.78, 0.58, 0.32, 1),
    "ice_env": (0.72, 0.90, 1.0, 1),
    "pine": (0.18, 0.38, 0.32, 1),
    "swamp_bark": (0.28, 0.20, 0.12, 1),
    "reed": (0.42, 0.52, 0.22, 1),
    "shadow_stone": (0.16, 0.10, 0.24, 1),
    "lava": (1.0, 0.32, 0.06, 1),
    "basalt": (0.18, 0.14, 0.14, 1),
    "slate": (0.42, 0.46, 0.52, 1),
    "marble": (0.86, 0.82, 0.74, 1),
    "thunder_glow": (1.0, 0.92, 0.40, 1),
    # vfx
    "ring": (0.95, 0.45, 0.18, 1),
    "xp": (0.55, 0.95, 0.35, 1),
    "ice": (0.70, 0.92, 1.0, 1),
    "arcane": (0.85, 0.45, 1.0, 1),
    "fire": (1.0, 0.50, 0.16, 1),
    "powerup": (1.0, 0.92, 0.55, 1),
}

# Materials that glow in game (Unity reads the names in ProjectSetup.ConvertMaterials).
EMISSIVE = {
    "slime": 0.35,
    "slime_fast": 0.35,
    "king": 0.35,
    "gem": 0.9,
    "crystal": 1.0,
    "iris": 0.25,
    "eye_highlight": 1.0,
    "xp": 0.9,
    "ring": 0.7,
    "ice": 0.8,
    "arcane": 0.9,
    "fire": 1.0,
    "powerup": 1.0,
    "rune": 1.0,
    "ice_env": 0.55,
    "lava": 1.4,
    "thunder_glow": 1.1,
}


def parse_args():
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Pass output, preview, and texture directories after --")
    args = argv[argv.index("--") + 1 :]
    if len(args) < 3:
        raise SystemExit("Expected: <out_dir> <preview_dir> <texture_dir> [env|hero]")
    mode = args[3] if len(args) >= 4 else "all"
    return args[0], args[1], args[2], mode


OUT, PREVIEW, TEX, MODE = parse_args()
os.makedirs(OUT, exist_ok=True)
os.makedirs(PREVIEW, exist_ok=True)
os.makedirs(TEX, exist_ok=True)

CATALOG = []
BONES = {}


def sh(cmd):
    print("CMD", " ".join(cmd), flush=True)
    subprocess.check_call(cmd)


def make_textures():
    script = os.path.join(os.path.dirname(os.path.abspath(__file__)), "build_textures.py")
    if MODE != "hero":
        sh(["python3", script, TEX])
    return {
        "ground": os.path.join(TEX, "TEX_Forest_Ground.png"),
        "bark": os.path.join(TEX, "TEX_Bark.png"),
        "rock": os.path.join(TEX, "TEX_Rock.png"),
    }


# ---------------------------------------------------------------- scene helpers


def reset_file():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 800
    scene.render.resolution_y = 600
    scene.render.film_transparent = False
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    world = bpy.data.worlds.new("PreviewWorld")
    world.color = (0.70, 0.80, 0.90)
    scene.world = world
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (0.2, -4.4, 1.7)
    return scene, cam


def look_at(obj, target):
    direction = mathutils.Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def material(name, color, emission=0.0, rough=0.45, image_path=None, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Specular IOR Level"].default_value = 0.3
    if emission > 0:
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = emission
    if image_path:
        img = bpy.data.images.load(image_path)
        img.pack()
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = img
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    mat.diffuse_color = color
    return mat


def smooth(obj, angle=math.radians(60)):
    """Smooth shading with sharp edges wherever adjacent faces meet above `angle`."""
    mesh = obj.data
    edge_normals = {}
    for poly in mesh.polygons:
        poly.use_smooth = True
        normal = poly.normal.copy()
        for key in poly.edge_keys:
            edge_normals.setdefault(key, []).append(normal)
    cos_limit = math.cos(angle)
    sharp = mesh.attributes.get("sharp_edge")
    if sharp is None:
        sharp = mesh.attributes.new("sharp_edge", "BOOLEAN", "EDGE")
    for edge in mesh.edges:
        normals = edge_normals.get(edge.key)
        is_sharp = normals is not None and len(normals) == 2 and normals[0].dot(normals[1]) < cos_limit
        sharp.data[edge.index].value = is_sharp


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_xform(obj):
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def subdivide(obj, levels=1):
    activate(obj)
    mod = obj.modifiers.new("Subsurf", "SUBSURF")
    mod.levels = levels
    mod.render_levels = levels
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def bevel(obj, width=0.02, segments=2):
    activate(obj)
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def hash01(x, y, z, seed=0):
    value = math.sin(x * 127.1 + y * 311.7 + z * 74.7 + seed * 19.19) * 43758.5453
    return value - math.floor(value)


def displace(obj, amount, seed=0, along_normal=True):
    mesh = obj.data
    mesh.calc_loop_triangles()
    for vert in mesh.vertices:
        n = hash01(round(vert.co.x, 3), round(vert.co.y, 3), round(vert.co.z, 3), seed) - 0.5
        if along_normal:
            vert.co += vert.normal * n * amount
        else:
            vert.co += mathutils.Vector((n, hash01(vert.co.y, vert.co.z, vert.co.x, seed + 1) - 0.5, 0)) * amount
    return obj


def carve(obj, predicate):
    """Delete vertices (and their faces) that satisfy predicate(co)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    doomed = [v for v in bm.verts if predicate(v.co)]
    bmesh.ops.delete(bm, geom=doomed, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def finish(obj, name, scale=(1, 1, 1), rot=None, do_smooth=True):
    obj.scale = scale
    if rot is not None:
        obj.rotation_euler = rot
    apply_xform(obj)
    obj.name = name
    if do_smooth:
        smooth(obj)
    return obj


def add_sphere(name, radius, loc, scale=(1, 1, 1), seg=24, rings=12, rot=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=radius, location=loc)
    return finish(bpy.context.active_object, name, scale, rot)


def add_ico(name, radius, loc, scale=(1, 1, 1), subdiv=2, rot=None):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=loc)
    return finish(bpy.context.active_object, name, scale, rot)


def add_cube(name, size, loc, scale=(1, 1, 1), rot=None):
    bpy.ops.mesh.primitive_cube_add(size=size, location=loc)
    return finish(bpy.context.active_object, name, scale, rot, do_smooth=False)


def add_cylinder(name, radius, depth, loc, vertices=16, rot=None, scale=(1, 1, 1), radius2=None):
    if radius2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=radius2, depth=depth, location=loc)
    return finish(bpy.context.active_object, name, scale, rot)


def add_tube(name, a, b, radius, vertices=12, radius2=None):
    a = mathutils.Vector(a)
    b = mathutils.Vector(b)
    direction = b - a
    length = max(direction.length, 0.001)
    mid = (a + b) / 2
    if radius2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=length, location=mid)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=radius2, depth=length, location=mid)
    obj = bpy.context.active_object
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = mathutils.Vector((0, 0, 1)).rotation_difference(direction)
    apply_xform(obj)
    obj.rotation_mode = "XYZ"
    obj.name = name
    smooth(obj)
    return obj


def add_cone(name, radius, depth, loc, rot=(0, 0, 0), vertices=16, radius2=0.0):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=radius2, depth=depth, location=loc, rotation=rot)
    return finish(bpy.context.active_object, name)


def add_torus(name, major, minor, loc, rot=None, scale=(1, 1, 1), major_segments=32, minor_segments=12):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major,
        minor_radius=minor,
        major_segments=major_segments,
        minor_segments=minor_segments,
        location=loc,
    )
    return finish(bpy.context.active_object, name, scale, rot)


# ---------------------------------------------------------------- rigging helpers


def segment_distance(point, a, b):
    ab = b - a
    length2 = ab.length_squared
    if length2 < 1e-9:
        return (point - a).length
    t = max(0.0, min(1.0, (point - a).dot(ab) / length2))
    return (point - (a + ab * t)).length


def assign(obj, bones, mat, sharpness=2.0):
    """Attach a part to one bone (string) or blend between several bones by distance."""
    obj.data.materials.append(mat)
    if isinstance(bones, str):
        group = obj.vertex_groups.new(name=bones)
        group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
        return obj
    groups = {name: obj.vertex_groups.new(name=name) for name in bones}
    segments = {name: (mathutils.Vector(BONES[name][0]), mathutils.Vector(BONES[name][1])) for name in bones}
    for vert in obj.data.vertices:
        weights = {}
        for name in bones:
            a, b = segments[name]
            d = segment_distance(vert.co, a, b)
            weights[name] = 1.0 / (d ** sharpness + 1e-4)
        total = sum(weights.values())
        kept = {name: w / total for name, w in weights.items() if w / total > 0.04}
        total = sum(kept.values())
        for name, w in kept.items():
            groups[name].add([vert.index], w / total, "REPLACE")
    return obj


def join(parts, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    parts[0].name = name
    return parts[0]


def new_armature(name, bones):
    BONES.clear()
    arm_data = bpy.data.armatures.new(name + "_Rig")
    arm = bpy.data.objects.new(name + "_Armature", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for spec in bones:
        bone = arm_data.edit_bones.new(spec[0])
        bone.head = spec[1]
        bone.tail = spec[2]
        bone.roll = 0.0
        if spec[3]:
            bone.parent = arm_data.edit_bones[spec[3]]
            bone.use_connect = False
        BONES[spec[0]] = (spec[1], spec[2])
    bpy.ops.object.mode_set(mode="POSE")
    for pose_bone in arm.pose.bones:
        pose_bone.rotation_mode = "XYZ"
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def bind(mesh, arm):
    modifier = mesh.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm
    modifier.use_vertex_groups = True
    mesh.parent = arm


def reset_pose(arm):
    for pose_bone in arm.pose.bones:
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.scale = (1.0, 1.0, 1.0)


def add_action(arm, name, frames, apply_pose):
    if arm.animation_data is None:
        arm.animation_data_create()
    action = bpy.data.actions.new(name)
    arm.animation_data.action = action
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for frame in range(1, frames + 1):
        bpy.context.scene.frame_set(frame)
        reset_pose(arm)
        apply_pose(frame, frames)
        for pose_bone in arm.pose.bones:
            pose_bone.keyframe_insert("rotation_euler", frame=frame)
            pose_bone.keyframe_insert("location", frame=frame)
            pose_bone.keyframe_insert("scale", frame=frame)
    bpy.ops.object.mode_set(mode="OBJECT")
    return action


def cleanup_objects():
    for obj in list(bpy.data.objects):
        if obj.type in ("CAMERA", "LIGHT"):
            continue
        bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for arm in list(bpy.data.armatures):
        if arm.users == 0:
            bpy.data.armatures.remove(arm)


def render_preview(path, target, distance=4.4, height=1.7):
    cam = bpy.context.scene.camera
    cam.location = (distance * 0.35, -distance, height)
    look_at(cam, target)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def export_fbx(path, roots, animated):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")

    def select_tree(obj):
        obj.select_set(True)
        for child in obj.children:
            select_tree(child)

    for root in roots:
        select_tree(root)
    bpy.context.view_layer.objects.active = roots[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=animated,
        bake_anim_use_all_actions=animated,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=True,
    )


def record(asset_id, filename, kind, meshes, notes):
    tris = 0
    verts = 0
    for mesh in meshes:
        mesh.data.calc_loop_triangles()
        tris += len(mesh.data.loop_triangles)
        verts += len(mesh.data.vertices)
    CATALOG.append({"id": asset_id, "file": filename, "kind": kind, "triangles": tris, "vertices": verts, "notes": notes})
    print(f"ASSET {asset_id} tris={tris} verts={verts} -> {filename}", flush=True)


def bone(arm, name):
    return arm.pose.bones[name]


# ---------------------------------------------------------------- animation curve helpers


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def curve(t, points):
    """Piecewise smooth interpolation through (time, value) points, time in 0..1."""
    if t <= points[0][0]:
        return points[0][1]
    for (t0, v0), (t1, v1) in zip(points, points[1:]):
        if t <= t1:
            span = max(1e-6, t1 - t0)
            return v0 + (v1 - v0) * smoothstep((t - t0) / span)
    return points[-1][1]


def pulse(t, start, end):
    """0 -> 1 -> 0 bump between start and end."""
    if t <= start or t >= end:
        return 0.0
    return math.sin((t - start) / (end - start) * math.pi)


# ---------------------------------------------------------------- hero


def hero_bones():
    return [
        ("Root", (0, 0, 0), (0, 0, 0.2), None),
        ("Hips", (0, 0, 0.40), (0, 0, 0.50), "Root"),
        ("Spine", (0, 0, 0.50), (0, 0, 0.60), "Hips"),
        ("Chest", (0, 0, 0.60), (0, 0, 0.70), "Spine"),
        ("Neck", (0, 0, 0.70), (0, 0, 0.76), "Chest"),
        ("Head", (0, 0, 0.76), (0, 0, 1.20), "Neck"),
        ("Hat", (0, 0.0, 1.20), (0, 0.02, 1.38), "Head"),
        ("HatTip", (0, 0.10, 1.46), (0, 0.24, 1.68), "Hat"),
        ("Hair.L", (-0.17, 0.17, 0.98), (-0.21, 0.21, 0.82), "Head"),
        ("Hair.L.2", (-0.21, 0.21, 0.82), (-0.24, 0.24, 0.66), "Hair.L"),
        ("Hair.R", (0.17, 0.17, 0.98), (0.21, 0.21, 0.82), "Head"),
        ("Hair.R.2", (0.21, 0.21, 0.82), (0.24, 0.24, 0.66), "Hair.R"),
        ("Cape", (0, 0.10, 0.69), (0, 0.17, 0.44), "Chest"),
        ("Shoulder.L", (-0.05, 0, 0.67), (-0.13, 0, 0.67), "Chest"),
        ("UpperArm.L", (-0.13, 0, 0.67), (-0.19, -0.01, 0.52), "Shoulder.L"),
        ("Forearm.L", (-0.19, -0.01, 0.52), (-0.21, -0.04, 0.40), "UpperArm.L"),
        ("Hand.L", (-0.21, -0.04, 0.40), (-0.22, -0.06, 0.33), "Forearm.L"),
        ("Shoulder.R", (0.05, 0, 0.67), (0.13, 0, 0.67), "Chest"),
        ("UpperArm.R", (0.13, 0, 0.67), (0.19, -0.01, 0.52), "Shoulder.R"),
        ("Forearm.R", (0.19, -0.01, 0.52), (0.21, -0.04, 0.40), "UpperArm.R"),
        ("Hand.R", (0.21, -0.04, 0.40), (0.22, -0.06, 0.33), "Forearm.R"),
        ("Staff", (0.225, -0.07, 0.37), (0.225, -0.07, 0.85), "Hand.R"),
        ("StaffTip", (0.225, -0.07, 1.01), (0.225, -0.07, 1.10), "Staff"),
        ("UpLeg.L", (-0.075, 0, 0.40), (-0.084, 0, 0.22), "Hips"),
        ("Leg.L", (-0.084, 0, 0.22), (-0.085, 0, 0.07), "UpLeg.L"),
        ("Foot.L", (-0.085, 0, 0.07), (-0.085, -0.10, 0.02), "Leg.L"),
        ("UpLeg.R", (0.075, 0, 0.40), (0.084, 0, 0.22), "Hips"),
        ("Leg.R", (0.084, 0, 0.22), (0.085, 0, 0.07), "UpLeg.R"),
        ("Foot.R", (0.085, 0, 0.07), (0.085, -0.10, 0.02), "Leg.R"),
    ]


# ---------------------------------------------------------------- hero mesh helpers


def loft(name, centers, radii, segments=16, ovals=None, wave=None, cap_start=True, cap_end=True):
    """Tube through `centers` with per-ring radius. `ovals` scales (x, y) per ring; `wave(theta, i)` adds to the radius."""
    bm = bmesh.new()
    rings = []
    count = len(centers)
    for i, (center, radius) in enumerate(zip(centers, radii)):
        c = mathutils.Vector(center)
        prev = mathutils.Vector(centers[max(0, i - 1)])
        nxt = mathutils.Vector(centers[min(count - 1, i + 1)])
        axis = (nxt - prev)
        if axis.length < 1e-6:
            axis = mathutils.Vector((0, 0, 1))
        axis.normalize()
        ref = mathutils.Vector((0, 0, 1)) if abs(axis.z) < 0.9 else mathutils.Vector((1, 0, 0))
        u = axis.cross(ref).normalized()
        v = axis.cross(u).normalized()
        if abs(axis.z) >= 0.9:
            # Vertical rings: keep u on world X and v on world Y so ovals mean what they say.
            u = mathutils.Vector((1, 0, 0))
            v = mathutils.Vector((0, 1, 0)) if axis.z > 0 else mathutils.Vector((0, -1, 0))
        sx, sy = ovals[i] if ovals else (1.0, 1.0)
        ring = []
        for k in range(segments):
            theta = k / segments * math.tau
            r = radius + (wave(theta, i) if wave else 0.0)
            ring.append(bm.verts.new(c + u * math.cos(theta) * r * sx + v * math.sin(theta) * r * sy))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(segments):
            bm.faces.new((a[k], a[(k + 1) % segments], b[(k + 1) % segments], b[k]))
    if cap_start:
        bm.faces.new(list(reversed(rings[0])))
    if cap_end:
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    smooth(obj, math.radians(80))
    return obj


def path_tube(name, points, radius, segments=8, taper=None):
    radii = [radius * (taper(i / max(1, len(points) - 1)) if taper else 1.0) for i in range(len(points))]
    return loft(name, points, radii, segments)


def surface_y(obj, x, z, offset=0.0):
    """Front surface (-Y side) of `obj` at (x, z), pushed `offset` further out."""
    hit, location, _normal, _index = obj.ray_cast(mathutils.Vector((x, -2.0, z)), mathutils.Vector((0, 1, 0)))
    if not hit:
        return -0.2
    return location.y - offset


def lock(name, root, tip, width, depth=None, bend=(0.0, 0.0), segments=14, rings=12):
    """Teardrop hair clump hanging from `root` to a point at `tip`; `bend` curls the tip in local x/y."""
    depth = width * 0.7 if depth is None else depth
    root = mathutils.Vector(root)
    tip = mathutils.Vector(tip)
    length = (tip - root).length
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1.0, location=(0, 0, 0))
    obj = bpy.context.active_object
    for vert in obj.data.vertices:
        s = (vert.co.z + 1.0) / 2.0  # 1 at the root, 0 at the tip
        along = (1.0 - s)
        fat = min(1.0, s * 1.3) ** 1.05
        x = vert.co.x * width * fat
        y = vert.co.y * depth * fat
        x += bend[0] * along * along
        y += bend[1] * along * along
        vert.co = mathutils.Vector((x, y, -along * length))
    direction = (tip - root).normalized()
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = mathutils.Vector((0, 0, -1)).rotation_difference(direction)
    obj.location = root
    apply_xform(obj)
    obj.rotation_mode = "XYZ"
    obj.name = name
    smooth(obj, math.radians(75))
    return obj


def eye_texture(path, size=256):
    """Anime eye drawn with signed distances: sclera, violet gradient iris, pupil, two highlights, thick lash."""
    import numpy as np

    v, u = np.mgrid[size - 1:-1:-1, 0:size].astype(np.float32)
    u = u / (size - 1) * 2.0 - 1.0
    v = v / (size - 1) * 2.0 - 1.0
    aa = 2.5 / size

    def fill(d):
        return np.clip(0.5 - d / aa, 0.0, 1.0)[..., None]

    def ellipse(cx, cy, rx, ry):
        return (np.sqrt(((u - cx) / rx) ** 2 + ((v - cy) / ry) ** 2) - 1.0) * min(rx, ry)

    def lerp(a, b, t):
        return a * (1.0 - t) + b * t

    img = np.zeros((size, size, 3), np.float32)
    img[:] = (0.99, 0.98, 1.0)
    # Iris: deep amber at the top to bright golden orange at the bottom, dark brown rim (cat eye).
    t = np.clip((0.55 - v) / 1.3, 0.0, 1.0)[..., None]
    iris_col = lerp(np.array((0.62, 0.22, 0.02), np.float32), np.array((1.0, 0.70, 0.22), np.float32), t ** 1.2)
    iris = ellipse(0.0, -0.10, 0.70, 0.84)
    img = lerp(img, np.array((0.30, 0.10, 0.02), np.float32), fill(iris))
    img = lerp(img, iris_col, fill(iris + 0.07))
    # Pupil and a warm glow crescent under it.
    img = lerp(img, np.array((0.28, 0.08, 0.01), np.float32), fill(ellipse(0.0, 0.04, 0.30, 0.40)))
    img = lerp(img, np.array((1.0, 0.86, 0.45), np.float32), 0.6 * fill(ellipse(0.0, -0.56, 0.42, 0.18)))
    # Highlights (same corner on both eyes = one light source).
    img = lerp(img, np.array((1.0, 1.0, 1.0), np.float32), fill(ellipse(-0.30, 0.26, 0.24, 0.28)))
    img = lerp(img, np.array((1.0, 1.0, 1.0), np.float32), fill(ellipse(0.30, -0.40, 0.10, 0.11)))
    img = lerp(img, np.array((1.0, 1.0, 1.0), np.float32), 0.85 * fill(ellipse(0.36, 0.30, 0.06, 0.06)))
    # Thick upper lash band following the eye outline; thin lower lash.
    top = np.power(np.clip(1.0 - np.abs(u) ** 2.4, 0.0, 1.0), 1.0 / 2.2)
    lash_depth = 0.30 + 0.08 * (1.0 - np.abs(u))
    upper = fill(-(v - (top - lash_depth)) * 0.35)
    img = lerp(img, np.array((0.04, 0.03, 0.03), np.float32), upper)
    lower = np.clip(1.0 - np.abs(v + top - 0.05) / 0.06, 0.0, 1.0)[..., None] * (np.abs(u) > 0.15)[..., None]
    img = lerp(img, np.array((0.35, 0.18, 0.14), np.float32), lower * 0.6)
    rgba = np.concatenate([img, np.ones((size, size, 1), np.float32)], axis=2)
    image = bpy.data.images.new("TEX_Hero_Eye", size, size, alpha=False)
    image.pixels.foreach_set(rgba[::-1].reshape(-1))
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return path


def eye_decal(name, head, cx, cz, width, height, segments=36, rings=6):
    """Eye-shaped patch projected onto the face. UVs span the eye outline so the texture fills it."""
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    grid = []
    for ri in range(rings + 1):
        scale = ri / rings
        ring = []
        for k in range(segments if ri > 0 else 1):
            theta = k / segments * math.tau
            c, s = math.cos(theta), math.sin(theta)
            # Superellipse outline: flatter top (lash line), rounder bottom.
            ex = 2.4
            ey = 2.2 if s > 0 else 2.0
            r = 1.0 / ((abs(c) ** ex + abs(s) ** ey) ** (1.0 / max(ex, ey)) + 1e-6)
            lu = c * r * scale
            lv = s * r * scale
            lu = max(-1.0, min(1.0, lu))
            lv = max(-1.0, min(1.0, lv))
            x = cx + lu * width / 2
            z = cz + lv * height / 2
            vert = bm.verts.new((x, surface_y(head, x, z, 0.004), z))
            ring.append((vert, (lu * 0.5 + 0.5, lv * 0.5 + 0.5)))
        grid.append(ring)
    centre = grid[0][0]
    for k in range(segments):
        a = grid[1][k]
        b = grid[1][(k + 1) % segments]
        face = bm.faces.new((centre[0], a[0], b[0]))
        for loop, (_v, uv) in zip(face.loops, (centre, a, b)):
            loop[uv_layer].uv = uv
    for ri in range(1, rings):
        inner, outer = grid[ri], grid[ri + 1]
        for k in range(segments):
            quad = (inner[k], inner[(k + 1) % segments], outer[(k + 1) % segments], outer[k])
            face = bm.faces.new([q[0] for q in quad][::-1])
            for loop, (_v, uv) in zip(face.loops, quad[::-1]):
                loop[uv_layer].uv = uv
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


# A lone metaball's surface sits at ~0.57 of its radius at threshold 0.6, stiffness 2.
METABALL_REACH = 1.0 / 0.573


def metaball_mesh(name, elements, resolution=0.02, threshold=0.6):
    """Blobby surface from (kind, co, radius, size, negative) elements, converted to a mesh.

    Radii are the intended surface radii; they are scaled up by METABALL_REACH internally.
    """
    data = bpy.data.metaballs.new(name + "_mb")
    data.resolution = resolution
    data.render_resolution = resolution
    data.threshold = threshold
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    for kind, co, radius, size, negative in elements:
        element = data.elements.new(type=kind)
        element.co = co
        element.radius = radius * METABALL_REACH
        element.stiffness = 2.0
        element.use_negative = negative
        if kind == "ELLIPSOID":
            element.size_x, element.size_y, element.size_z = size
    bpy.context.view_layer.update()
    activate(obj)
    bpy.ops.object.convert(target="MESH")
    mesh_obj = bpy.context.active_object
    mesh_obj.name = name
    for poly in mesh_obj.data.polygons:
        poly.use_smooth = True
    return mesh_obj


def decimate(obj, ratio):
    activate(obj)
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.ratio = ratio
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def build_hero(mats):
    """Chibi cat-boy witch: huge shaggy black hair with cat ears, big amber eyes, :3 mouth with fangs, choker and
    bell, navy tee and black shorts, plus a witch hat the ears poke through, a short cape and the moon staff."""
    arm = new_armature("CHR_Hero_Main", hero_bones())
    p = []

    # --- head: wide soft face, full cheeks, small rounded chin
    head = add_sphere("head", 0.25, (0, 0, 0.98), scale=(1.06, 0.95, 0.97), seg=40, rings=24)
    for vert in head.data.vertices:
        below = max(0.0, 0.98 - vert.co.z) / 0.25
        vert.co.x *= 1.0 + 0.06 * math.sin(min(1.0, below * 1.6) * math.pi) - 0.12 * below ** 3
        if vert.co.y < 0:
            vert.co.y *= 1.0 - 0.05 * below
    head.data.update()
    bpy.context.view_layer.update()
    p.append(assign(head, "Head", mats["skin"]))

    # --- face: amber eye decals set low and wide, thick black brows, :3 mouth with two fangs, blush
    eye_path = eye_texture(os.path.join(TEX, "TEX_Hero_Eye.png"))
    eye_mat = material("MAT_eye_decal", (1, 1, 1, 1), rough=0.25, image_path=eye_path)
    for side in (-1, 1):
        decal = eye_decal(f"eye{side}", head, 0.098 * side, 0.935, 0.118, 0.13)
        p.append(assign(decal, "Head", eye_mat))
        brow = [(0.098 * side + dx * side, 0, 1.028 + dz) for dx, dz in ((-0.045, -0.006), (0.0, 0.008), (0.045, 0.002))]
        brow = [(x, surface_y(head, x, z, 0.003), z) for x, _y, z in brow]
        p.append(assign(path_tube(f"brow{side}", brow, 0.0085, taper=lambda t: 0.55 + 0.45 * math.sin(t * math.pi)), "Head", mats["hair_dark"]))
        bx, bz = 0.125 * side, 0.872
        p.append(assign(add_sphere(f"blush{side}", 0.036, (bx, surface_y(head, bx, bz, -0.006), bz), scale=(1.2, 0.25, 0.6), seg=16, rings=8), "Head", mats["blush"]))
    mouth = [(x, 0, z) for x, z in ((-0.03, 0.852), (-0.015, 0.842), (0.0, 0.851), (0.015, 0.842), (0.03, 0.852))]
    mouth = [(x, surface_y(head, x, z, 0.003), z) for x, _y, z in mouth]
    p.append(assign(path_tube("mouth", mouth, 0.0042, taper=lambda t: 0.55 + 0.45 * math.sin(t * math.pi)), "Head", mats["mouth"]))
    for side in (-1, 1):
        fx, fz = 0.017 * side, 0.836
        p.append(assign(add_cone(f"fang{side}", 0.0055, 0.013, (fx, surface_y(head, fx, fz, 0.003), fz), rot=(math.pi, 0, 0), vertices=8), "Head", mats["eye_white"]))

    # --- hair: one big shaggy dome (wider than the face), the face carved out, then jagged clumps
    elements = [
        ("ELLIPSOID", (0, 0.03, 1.06), 0.30, (1.06, 1.08, 0.98), False),
        ("BALL", (0, 0.11, 0.95), 0.25, None, False),
        ("BALL", (0, 0.13, 0.85), 0.17, None, False),
    ]
    for side in (-1, 1):
        elements += [
            ("BALL", (0.21 * side, -0.02, 1.0), 0.15, None, False),
            ("BALL", (0.22 * side, 0.04, 0.89), 0.125, None, False),
            ("BALL", (0.17 * side, 0.14, 0.85), 0.12, None, False),
            ("BALL", (0.13 * side, -0.10, 1.15), 0.11, None, False),
        ]
    # Lumpy wavy outline: a ring of hair tufts bulging from the dome around the sides and back (not the face).
    tuft_rng = random.Random(77)
    for ring, (z, radius, size, count) in enumerate(((1.22, 0.22, 0.085, 8), (1.1, 0.27, 0.095, 10), (0.98, 0.28, 0.095, 10), (0.88, 0.25, 0.09, 9))):
        for k in range(count):
            a = -2.45 + 4.9 * (k + 0.5 * (ring % 2)) / count + tuft_rng.uniform(-0.12, 0.12)
            if abs(a) > 2.45:
                continue
            rr = radius + tuft_rng.uniform(-0.015, 0.02)
            elements.append(("BALL", (math.sin(a) * rr, 0.04 + math.cos(a) * rr, z + tuft_rng.uniform(-0.03, 0.03)), size, None, False))
    elements.append(("ELLIPSOID", (0, -0.31, 0.875), 0.2, (1.22, 1.0, 0.95), True))
    hair = metaball_mesh("hair", elements, resolution=0.016)
    decimate(hair, 0.4)
    bpy.context.view_layer.update()
    p.append(assign(hair, "Head", mats["hair"]))
    top_z = max(v.co.z for v in hair.data.vertices)
    print(f"HAIR top {top_z:.3f}", flush=True)
    # Bangs: chunky clumps to the brow line, one centre strand dipping between the eyes.
    bangs = ((-0.23, 1.08, 0.93, 0.11, -0.03), (-0.165, 1.12, 0.985, 0.12, -0.02), (-0.10, 1.13, 1.01, 0.12, -0.01),
             (-0.04, 1.14, 1.02, 0.11, 0.0), (0.0, 1.14, 0.945, 0.06, 0.004), (0.04, 1.14, 1.02, 0.11, 0.0),
             (0.10, 1.13, 1.01, 0.12, 0.01), (0.165, 1.12, 0.985, 0.12, 0.02), (0.23, 1.08, 0.93, 0.11, 0.03))
    for index, (x, top, bottom, width, curl) in enumerate(bangs):
        tx = x + curl * 2.0
        root = (x, surface_y(head, x, top, 0.04), top)
        tip = (tx, surface_y(head, tx, bottom, 0.012), bottom)
        p.append(assign(lock(f"bang{index}", root, tip, width, 0.04, bend=(curl, -0.014)), "Head", mats["hair"]))
    for side in (-1, 1):
        tail = "Hair.L" if side < 0 else "Hair.R"
        # Side flicks sticking out sideways at cheek height (the reference's jagged silhouette).
        flicks = ((1.16, 0.33, -0.02, 0.0, 0.065), (1.06, 0.37, 0.03, 0.02, 0.07), (0.97, 0.38, 0.06, 0.04, 0.07),
                  (0.89, 0.36, 0.08, 0.08, 0.065), (0.84, 0.31, 0.07, 0.14, 0.06), (0.83, 0.24, 0.06, 0.22, 0.06))
        for k, (z, out, drop, back, width) in enumerate(flicks):
            root = (0.2 * side, 0.03 + back * 0.6, z)
            tip = (out * side, 0.05 + back, z - drop)
            p.append(assign(lock(f"flick{side}{k}", root, tip, width * 1.5, 0.08, bend=(0.0, 0.02)), ["Head", tail], mats["hair"], sharpness=3.0))
        # Clumps around the back, flicking out at the ends.
        for k, (angle, length) in enumerate(((0.35, 0.13), (0.8, 0.14), (1.2, 0.12))):
            a = angle * side
            root = (math.sin(a) * 0.22, 0.05 + math.cos(a) * 0.22, 0.93)
            tip = (math.sin(a) * 0.31, 0.08 + math.cos(a) * 0.31, 0.93 - length)
            clump = lock(f"back{side}{k}", root, tip, 0.08, 0.06, bend=(0.04 * side, 0.04))
            p.append(assign(clump, ["Head", tail, tail + ".2"], mats["hair"], sharpness=3.0))
        # Face-framing lock in front of the ear line.
        x = 0.22 * side
        root = (x, surface_y(head, x, 1.0, 0.03), 1.0)
        p.append(assign(lock(f"sidelock{side}", root, (0.215 * side, -0.14, 0.78), 0.055, 0.035, bend=(0.02 * side, -0.01)), "Head", mats["hair"]))
    p.append(assign(lock("backcenter", (0, 0.24, 0.93), (0, 0.31, 0.77), 0.09, 0.06, bend=(0.0, 0.04)), "Head", mats["hair"]))
    # Soft blue-white shine ovals on the crown, as in the reference.
    for side in (-1, 1):
        hx, hz = 0.13 * side, 1.17
        hit, loc, normal, _ = hair.ray_cast(mathutils.Vector((hx, -1.5, hz)), mathutils.Vector((0, 1, 0)))
        if hit:
            shine = add_sphere(f"hair_shine{side}", 0.036, loc - normal * 0.002, scale=(1.0, 0.1, 0.62), seg=14, rings=8, rot=(0.45, 0, -0.4 * side))
            p.append(assign(shine, "Head", mats["hair_light"]))

    # --- cat ears: black outer shell, cream fluffy inner, poking up through the hat brim
    for side in (-1, 1):
        base = mathutils.Vector((0.215 * side, 0.0, top_z - 0.1))
        ear = add_cone(f"catear{side}", 0.1, 0.2, (0, 0, 0.1), vertices=16)
        for vert in ear.data.vertices:
            vert.co.y *= 0.55
        ear.rotation_euler = (0.06, -0.3 * side, 0.0)
        ear.location = base
        apply_xform(ear)
        smooth(ear, math.radians(70))
        p.append(assign(ear, "Head", mats["hair"]))
        inner = add_cone(f"catear_in{side}", 0.07, 0.15, (0, -0.012, 0.085), vertices=12)
        for vert in inner.data.vertices:
            vert.co.y = vert.co.y * 0.3 - 0.038
        inner.rotation_euler = (0.06, -0.3 * side, 0.0)
        inner.location = base
        apply_xform(inner)
        smooth(inner, math.radians(70))
        p.append(assign(inner, "Head", mats["ear_inner"]))

    # --- witch hat: tilted back between the ears, wavy brim, bent crown, purple band, gold moon
    hat_z = top_z - 0.04
    brim_r = 0.21
    brim = add_cylinder("hat_brim", brim_r, 0.02, (0, 0.03, hat_z), vertices=48)
    for vert in brim.data.vertices:
        r = math.hypot(vert.co.x, vert.co.y - 0.03)
        a = math.atan2(vert.co.y - 0.03, vert.co.x)
        vert.co.z += (r / brim_r) ** 3 * 0.05 + math.sin(a * 5.0) * 0.01 * (r / brim_r) ** 2 + (vert.co.y - 0.03) * 0.18
    p.append(assign(brim, "Hat", mats["hat"]))
    centers, radii = [], []
    for i in range(14):
        t = i / 13
        centers.append((0.02 * t * t, 0.03 + 0.30 * t ** 2.2, hat_z + 0.02 + t * 0.44 - 0.09 * t ** 3))
        radii.append(0.12 * (1 - t) ** 0.95 + 0.01)
    crown = loft("hat_crown", centers, radii, segments=24, cap_start=False)
    p.append(assign(crown, ["Hat", "HatTip"], mats["hat"], sharpness=3.0))
    p.append(assign(add_torus("hat_band", 0.118, 0.019, (0, 0.034, hat_z + 0.05), major_segments=36, minor_segments=10, scale=(1, 1, 1.3)), "Hat", mats["hat_band"]))
    moon = add_torus("hat_moon", 0.028, 0.009, (0, -0.092, hat_z + 0.06), rot=(math.pi / 2, 0, 0), major_segments=24, minor_segments=8)
    carve(moon, lambda co: co.x > 0.01)
    p.append(assign(moon, "Hat", mats["gold"]))

    # --- body: neck, choker with gold bell, boxy navy tee, black shorts, short witch cape
    p.append(assign(add_cylinder("neck", 0.042, 0.10, (0, 0, 0.73), vertices=14), ["Chest", "Neck"], mats["skin"]))
    p.append(assign(add_torus("choker", 0.044, 0.009, (0, 0, 0.735), major_segments=24, minor_segments=8), "Neck", mats["choker"]))
    p.append(assign(add_sphere("bell", 0.017, (0, -0.052, 0.722), seg=14, rings=10), "Neck", mats["gold"]))
    tee = loft("tee", [(0, 0, 0.44), (0, 0, 0.52), (0, 0, 0.62), (0, 0.004, 0.69), (0, 0.004, 0.715)], [0.142, 0.136, 0.142, 0.125, 0.07],
               segments=28, ovals=[(1.0, 0.74)] * 5)
    p.append(assign(tee, ["Hips", "Spine", "Chest"], mats["shirt"]))
    p.append(assign(add_torus("tee_hem", 0.141, 0.007, (0, 0, 0.443), scale=(1.0, 0.74, 1.0), major_segments=36, minor_segments=6), "Hips", mats["shirt"]))
    shorts = loft("shorts", [(0, 0, 0.47), (0, 0, 0.42), (0, 0, 0.385)], [0.142, 0.158, 0.156], segments=28, ovals=[(1.0, 0.7)] * 3, cap_start=False)
    p.append(assign(shorts, ["Hips"], mats["shorts"]))
    cape = loft("cape", [(0, 0.05, 0.71), (0, 0.10, 0.62), (0, 0.15, 0.52), (0, 0.19, 0.42)], [0.11, 0.15, 0.19, 0.21],
                segments=28, ovals=[(1.0, 0.9)] * 4, cap_start=False, cap_end=False)
    carve(cape, lambda co: co.y < 0.06 + (0.71 - co.z) * 0.15)
    p.append(assign(cape, ["Chest", "Cape"], mats["cape_inner"], sharpness=3.0))
    outer = loft("cape_outer", [(0, 0.056, 0.71), (0, 0.106, 0.62), (0, 0.156, 0.52), (0, 0.196, 0.42)], [0.114, 0.154, 0.194, 0.214],
                 segments=28, ovals=[(1.0, 0.9)] * 4, cap_start=False, cap_end=False)
    carve(outer, lambda co: co.y < 0.065 + (0.71 - co.z) * 0.15)
    p.append(assign(outer, ["Chest", "Cape"], mats["robe"], sharpness=3.0))
    for side in (-1, 1):
        p.append(assign(add_sphere(f"clasp{side}", 0.014, (0.075 * side, -0.035, 0.70), seg=10, rings=6), "Chest", mats["gold"]))

    # --- arms: short tee sleeves, bare arms, mitten hands
    for side, prefix in ((-1, "L"), (1, "R")):
        s = side
        sleeve = loft(f"sleeve{prefix}", [(0.11 * s, 0, 0.685), (0.155 * s, -0.004, 0.62), (0.172 * s, -0.006, 0.582)], [0.058, 0.054, 0.052], segments=14)
        p.append(assign(sleeve, [f"Shoulder.{prefix}", f"UpperArm.{prefix}"], mats["shirt"]))
        upper = loft(f"upperarm{prefix}", [(0.15 * s, 0, 0.62), (0.19 * s, -0.01, 0.52)], [0.039, 0.036], segments=14)
        p.append(assign(upper, f"UpperArm.{prefix}", mats["skin"]))
        p.append(assign(add_sphere(f"elbow{prefix}", 0.036, (0.19 * s, -0.01, 0.52), seg=14, rings=8), [f"UpperArm.{prefix}", f"Forearm.{prefix}"], mats["skin"]))
        fore = loft(f"forearm{prefix}", [(0.19 * s, -0.01, 0.52), (0.21 * s, -0.04, 0.41)], [0.036, 0.034], segments=14)
        p.append(assign(fore, f"Forearm.{prefix}", mats["skin"]))
        p.append(assign(add_sphere(f"hand{prefix}", 0.045, (0.215 * s, -0.05, 0.372), scale=(0.9, 1.0, 1.15), seg=16, rings=10), f"Hand.{prefix}", mats["skin"]))

    # --- legs: bare, soft and straight, with rounded bare feet (as in the reference)
    for side, prefix in ((-1, "L"), (1, "R")):
        s = side
        p.append(assign(loft(f"thigh{prefix}", [(0.075 * s, 0, 0.40), (0.082 * s, 0, 0.30), (0.084 * s, 0, 0.22)], [0.072, 0.07, 0.068], segments=18),
                        [f"UpLeg.{prefix}", f"Leg.{prefix}"], mats["skin"], sharpness=3.0))
        p.append(assign(loft(f"shin{prefix}", [(0.084 * s, 0, 0.22), (0.085 * s, 0, 0.12), (0.085 * s, 0, 0.05)], [0.068, 0.067, 0.065], segments=18),
                        f"Leg.{prefix}", mats["skin"]))
        p.append(assign(loft(f"shortleg{prefix}", [(0.075 * s, 0, 0.40), (0.08 * s, 0, 0.345)], [0.083, 0.08], segments=18, cap_start=False, cap_end=False),
                        f"UpLeg.{prefix}", mats["shorts"]))
        foot = add_sphere(f"foot{prefix}", 0.067, (0.085 * s, -0.012, 0.056), scale=(1.0, 1.15, 0.85), seg=18, rings=12)
        for vert in foot.data.vertices:
            vert.co.z = max(vert.co.z, 0.0)
        p.append(assign(foot, f"Foot.{prefix}", mats["skin"]))

    # --- staff: wooden shaft, curled top holding a crescent moon and a glowing crystal
    sx, sy = 0.225, -0.07
    p.append(assign(loft("staff", [(sx, sy, 0.03), (sx, sy, 0.55), (sx, sy, 0.93)], [0.016, 0.018, 0.021], segments=10), "Staff", mats["staff_wood"]))
    curl = []
    for i in range(22):
        t = i / 21
        angle = -math.pi / 2 + t * math.tau * 0.85
        r = 0.08 * (1 - 0.45 * t)
        curl.append((sx + math.cos(angle) * r, sy, 1.0 + math.sin(angle) * r))
    curl = [(sx, sy, 0.915)] + curl
    p.append(assign(path_tube("staff_curl", curl, 0.019, segments=10, taper=lambda t: 1.0 - 0.45 * t), "Staff", mats["staff_wood"]))
    p.append(assign(add_torus("staff_grip", 0.022, 0.007, (sx, sy, 0.37), major_segments=14, minor_segments=6), "Staff", mats["gold"]))
    moon = add_torus("staff_moon", 0.07, 0.016, (sx, sy - 0.005, 1.02), rot=(math.pi / 2, 0, 0), major_segments=28, minor_segments=8)
    carve(moon, lambda co: co.x > sx + 0.028)
    p.append(assign(moon, "StaffTip", mats["gold"]))
    p.append(assign(add_ico("staff_crystal", 0.042, (sx + 0.01, sy - 0.01, 1.02), scale=(0.75, 0.75, 1.3), subdiv=2), "StaffTip", mats["crystal"]))

    mesh = join(p, "CHR_Hero_Main")
    bind(mesh, arm)
    actions = animate_hero(arm)
    return arm, mesh, actions


def animate_hero(arm):
    def aim(name, direction, weight=1.0, spin=0.0):
        """Point bone `name` along world `direction`, blended from its current pose by `weight`.

        Euler pitches on tilted arm bones swing them across the body; aiming in world space does not.
        """
        if weight <= 1e-4 and abs(spin) < 1e-6:
            return
        bpy.context.view_layer.update()
        pb = bone(arm, name)
        current = pb.matrix.copy()
        y_axis = current.to_3x3() @ mathutils.Vector((0, 1, 0))
        target = mathutils.Vector(direction).normalized()
        wanted = y_axis.normalized().slerp(target, max(0.0, min(1.0, weight)))
        turn = y_axis.rotation_difference(wanted)
        rot = turn.to_matrix() @ current.to_3x3()
        if spin:
            rot = rot @ mathutils.Matrix.Rotation(spin, 3, "Y")
        pb.matrix = mathutils.Matrix.Translation(current.to_translation()) @ rot.to_4x4()

    def hair_follow(amount, lag=0.0):
        for side in ("L", "R"):
            bone(arm, f"Hair.{side}").rotation_euler.x += amount
            bone(arm, f"Hair.{side}.2").rotation_euler.x += amount * 0.8 + lag

    def staff_world_pitch(target):
        """Rotate the staff inside the hand so its world pitch (positive = top forward) ignores the arm swing."""
        total = 0.0
        for name in ("Hips", "Spine", "Chest", "Shoulder.R", "UpperArm.R", "Forearm.R", "Hand.R"):
            total += bone(arm, name).rotation_euler.x
        bone(arm, "Staff").rotation_euler.x = target - total

    def hat(pitch=0.0, roll=0.0, tip=0.0, lift=0.0):
        bone(arm, "Hat").rotation_euler.x += pitch
        bone(arm, "Hat").rotation_euler.y += roll
        bone(arm, "Hat").location.y += lift
        bone(arm, "HatTip").rotation_euler.x += tip

    def rest_arms(breathe=0.0):
        bone(arm, "UpperArm.L").rotation_euler.z = 0.20 + breathe * 0.04
        bone(arm, "UpperArm.L").rotation_euler.x = 0.10 + breathe * 0.03
        bone(arm, "Forearm.L").rotation_euler.x = -0.30
        bone(arm, "UpperArm.R").rotation_euler.z = -0.16 - breathe * 0.03
        bone(arm, "UpperArm.R").rotation_euler.x = 0.05
        bone(arm, "Forearm.R").rotation_euler.x = -0.55

    def idle(frame, frames):
        phase = (frame - 1) / frames * math.tau
        breathe = math.sin(phase)
        bone(arm, "Hips").location.y = breathe * 0.008
        bone(arm, "Spine").rotation_euler.x = breathe * 0.025
        bone(arm, "Chest").rotation_euler.x = breathe * 0.03
        bone(arm, "Head").rotation_euler.z = math.sin(phase * 0.5) * 0.07
        bone(arm, "Head").rotation_euler.y = math.sin(phase * 0.5 + 0.4) * 0.05
        bone(arm, "Head").rotation_euler.x = -0.03 + math.sin(phase + 0.6) * 0.02
        hat(tip=math.sin(phase + 1.0) * 0.10)
        bone(arm, "HatTip").rotation_euler.z = math.cos(phase * 0.5) * 0.08
        hair_follow(math.sin(phase + 0.8) * 0.05, math.cos(phase) * 0.03)
        bone(arm, "Cape").rotation_euler.x = math.sin(phase + 0.3) * 0.05
        rest_arms(breathe)
        staff_world_pitch(-0.10 + breathe * 0.02)

    def run(frame, frames):
        """Bouncy chibi trot: big head bob, short quick steps, free arm pumping, staff carried."""
        phase = (frame - 1) / frames * math.tau
        swing = math.sin(phase)
        bounce = abs(math.cos(phase))
        bone(arm, "Hips").location.y = bounce * 0.03 - 0.012
        bone(arm, "Hips").rotation_euler.y = swing * 0.09
        bone(arm, "Hips").rotation_euler.z = swing * 0.05
        bone(arm, "Spine").rotation_euler.x = 0.12
        bone(arm, "Chest").rotation_euler.x = 0.08 + math.sin(phase * 2) * 0.03
        bone(arm, "Chest").rotation_euler.y = -swing * 0.12
        bone(arm, "Head").rotation_euler.x = -0.10 + math.sin(phase * 2 + 0.6) * 0.04
        bone(arm, "Head").rotation_euler.y = swing * 0.05
        hat(pitch=-0.04, tip=0.30 + math.sin(phase * 2 + 1) * 0.15)
        hair_follow(0.35 + math.sin(phase * 2 + 0.5) * 0.15, 0.1)
        bone(arm, "Cape").rotation_euler.x = 0.45 + math.sin(phase * 2) * 0.10
        for side, offset in (("L", 0.0), ("R", math.pi)):
            leg = math.sin(phase + offset)
            bone(arm, f"UpLeg.{side}").rotation_euler.x = -leg * 0.8
            bone(arm, f"Leg.{side}").rotation_euler.x = max(0.0, math.sin(phase + offset + 1.2)) * 1.1 + 0.08
            bone(arm, f"Foot.{side}").rotation_euler.x = -max(0.0, -leg) * 0.3
        bone(arm, "UpperArm.L").rotation_euler.x = swing * 1.0
        bone(arm, "UpperArm.L").rotation_euler.z = 0.28
        bone(arm, "Forearm.L").rotation_euler.x = -0.9
        bone(arm, "UpperArm.R").rotation_euler.x = -0.30 - swing * 0.2
        bone(arm, "UpperArm.R").rotation_euler.z = -0.22
        bone(arm, "Forearm.R").rotation_euler.x = -0.95
        staff_world_pitch(0.15 + math.sin(phase * 2) * 0.05)

    def attack(frame, frames):
        """Staff bonk: quick overhead wind-up, smash down in front (impact ~frame 6), follow-through, recover."""
        t = (frame - 1) / (frames - 1)
        wind = curve(t, [(0.0, 0.0), (0.18, 1.0), (0.33, 0.0)])
        smash = curve(t, [(0.18, 0.0), (0.33, 1.0), (0.62, 1.0), (1.0, 0.0)])
        bone(arm, "Hips").rotation_euler.y = 0.08 * wind - 0.08 * smash
        bone(arm, "Hips").location.y = -0.025 * smash
        bone(arm, "Spine").rotation_euler.x = -0.22 * wind + 0.30 * smash
        bone(arm, "Chest").rotation_euler.x = -0.12 * wind + 0.18 * smash
        bone(arm, "Chest").rotation_euler.y = 0.10 * wind - 0.10 * smash
        bone(arm, "Head").rotation_euler.x = 0.15 * wind - 0.05 * smash
        hat(pitch=-0.12 * wind + 0.10 * smash, tip=-0.4 * wind + 0.7 * smash)
        hair_follow(-0.35 * wind + 0.45 * smash)
        bone(arm, "Cape").rotation_euler.x = -0.15 * wind + 0.50 * smash
        rest_arms()
        bone(arm, "UpperArm.L").rotation_euler.z += 0.25 * smash
        bone(arm, "UpLeg.L").rotation_euler.x = -0.35 * smash
        bone(arm, "Leg.L").rotation_euler.x = 0.25 * smash
        bone(arm, "UpLeg.R").rotation_euler.x = 0.28 * smash
        bone(arm, "Leg.R").rotation_euler.x = 0.35 * smash
        staff_world_pitch(-0.10)
        # Arm and staff: up behind the head, then down hard in front.
        lift = max(wind, 0.0)
        aim("UpperArm.R", (0.12, 0.25, 1.0), lift)
        aim("Forearm.R", (0.05, 0.5, 0.9), lift)
        aim("Staff", (0.0, 0.8, 0.6), lift)
        aim("UpperArm.R", (0.12, -0.92, -0.30), smash)
        aim("Forearm.R", (0.05, -0.95, -0.25), smash)
        aim("Staff", (0.0, -0.80, -0.60), smash)
        glow = pulse(t, 0.28, 0.6)
        bone(arm, "StaffTip").scale = (1 + glow * 0.6, 1 + glow * 0.6, 1 + glow * 0.6)

    def dash(frame, frames):
        t = (frame - 1) / (frames - 1)
        lean = curve(t, [(0.0, 0.0), (0.25, 1.0), (0.7, 1.0), (1.0, 0.0)])
        bone(arm, "Hips").location.y = 0.03 * lean
        bone(arm, "Spine").rotation_euler.x = 0.55 * lean
        bone(arm, "Chest").rotation_euler.x = 0.2 * lean
        bone(arm, "Head").rotation_euler.x = -0.40 * lean
        hat(pitch=-0.25 * lean, tip=1.0 * lean, lift=0.02 * lean)
        hair_follow(1.0 * lean, 0.25 * lean)
        bone(arm, "Cape").rotation_euler.x = 1.15 * lean
        bone(arm, "UpperArm.L").rotation_euler.x = 1.1 * lean
        bone(arm, "UpperArm.L").rotation_euler.z = 0.3 * lean
        bone(arm, "Forearm.L").rotation_euler.x = -0.2 * lean
        bone(arm, "UpperArm.R").rotation_euler.x = 0.9 * lean
        bone(arm, "Forearm.R").rotation_euler.x = -0.4 * lean
        bone(arm, "UpLeg.L").rotation_euler.x = -0.8 * lean
        bone(arm, "Leg.L").rotation_euler.x = 1.2 * lean
        bone(arm, "UpLeg.R").rotation_euler.x = 0.45 * lean
        bone(arm, "Leg.R").rotation_euler.x = 0.6 * lean
        staff_world_pitch(1.2 * lean)

    def hit(frame, frames):
        t = (frame - 1) / (frames - 1)
        kick = curve(t, [(0.0, 0.0), (0.2, 1.0), (1.0, 0.0)])
        bone(arm, "Hips").location.y = 0.02 * kick
        bone(arm, "Spine").rotation_euler.x = -0.38 * kick
        bone(arm, "Chest").rotation_euler.x = -0.2 * kick
        bone(arm, "Head").rotation_euler.x = -0.35 * kick
        bone(arm, "Head").rotation_euler.z = 0.18 * kick
        hat(pitch=-0.2 * kick, tip=-0.7 * kick, lift=0.03 * kick)
        hair_follow(-0.6 * kick)
        bone(arm, "Cape").rotation_euler.x = -0.3 * kick
        for side, sign in (("L", 1), ("R", -1)):
            bone(arm, f"UpperArm.{side}").rotation_euler.x = -0.7 * kick
            bone(arm, f"UpperArm.{side}").rotation_euler.z = 0.5 * kick * sign
            bone(arm, f"Forearm.{side}").rotation_euler.x = -0.5 * kick
        bone(arm, "UpLeg.L").rotation_euler.x = -0.3 * kick
        bone(arm, "Leg.L").rotation_euler.x = 0.35 * kick
        staff_world_pitch(-0.6 * kick)

    def death(frame, frames):
        t = (frame - 1) / (frames - 1)
        kneel = curve(t, [(0.0, 0.0), (0.4, 1.0), (1.0, 1.0)])
        fall = curve(t, [(0.35, 0.0), (1.0, 1.0)])
        pop = curve(t, [(0.05, 0.0), (0.35, 1.0), (0.75, 0.6), (1.0, 0.55)])
        bone(arm, "Hips").location.y = -0.20 * kneel - 0.16 * fall
        bone(arm, "Hips").rotation_euler.x = 0.2 * kneel - 1.5 * fall
        bone(arm, "Spine").rotation_euler.x = 0.35 * kneel - 0.1 * fall
        bone(arm, "Chest").rotation_euler.x = 0.25 * kneel
        bone(arm, "Head").rotation_euler.x = 0.4 * kneel - 0.3 * fall
        bone(arm, "Head").rotation_euler.z = 0.3 * fall
        # The hat pops off and tumbles.
        hat(pitch=-0.9 * pop, roll=0.6 * pop, tip=-0.8 * fall, lift=0.18 * pop)
        hair_follow(-0.4 * fall)
        for side in ("L", "R"):
            bone(arm, f"UpLeg.{side}").rotation_euler.x = -1.3 * kneel + 0.4 * fall
            bone(arm, f"Leg.{side}").rotation_euler.x = 2.0 * kneel - 0.5 * fall
            bone(arm, f"UpperArm.{side}").rotation_euler.x = -0.3 * kneel + 0.6 * fall
            bone(arm, f"UpperArm.{side}").rotation_euler.z = (0.6 if side == "L" else -0.6) * fall
        bone(arm, "Staff").rotation_euler.x = 1.3 * fall
        bone(arm, "Staff").rotation_euler.z = -0.8 * fall

    def cast(frame, frames):
        """Skill pickup: thrust the staff straight up and twirl it, crystal flaring."""
        t = (frame - 1) / (frames - 1)
        rise = curve(t, [(0.0, 0.0), (0.3, 1.0), (0.75, 1.0), (1.0, 0.0)])
        spin = curve(t, [(0.15, 0.0), (0.7, 1.0)])
        bone(arm, "Hips").location.y = 0.03 * pulse(t, 0.2, 0.6)
        bone(arm, "Spine").rotation_euler.x = -0.10 * rise
        bone(arm, "Chest").rotation_euler.x = -0.10 * rise
        bone(arm, "Head").rotation_euler.x = 0.20 * rise
        hat(pitch=-0.12 * rise, tip=-0.3 * rise)
        hair_follow(-0.2 * rise)
        bone(arm, "Cape").rotation_euler.x = 0.3 * pulse(t, 0.1, 0.6)
        rest_arms()
        bone(arm, "UpperArm.L").rotation_euler.z += 0.6 * rise
        staff_world_pitch(-0.10)
        aim("UpperArm.R", (0.25, -0.15, 1.0), rise)
        aim("Forearm.R", (0.1, -0.1, 1.0), rise)
        aim("Staff", (0.0, 0.0, 1.0), rise, spin=spin * math.tau)
        glow = pulse(t, 0.3, 0.85)
        bone(arm, "StaffTip").scale = (1 + glow * 0.8, 1 + glow * 0.8, 1 + glow * 0.8)

    def victory(frame, frames):
        """Boss down: a hop, staff thrust up, the free hand holding the hat brim."""
        t = (frame - 1) / (frames - 1)
        hop = pulse(t, 0.05, 0.4)
        pose = curve(t, [(0.0, 0.0), (0.2, 1.0), (0.85, 1.0), (1.0, 0.0)])
        bone(arm, "Hips").location.y = 0.12 * hop - 0.02 * pose
        bone(arm, "Spine").rotation_euler.x = -0.1 * pose
        bone(arm, "Head").rotation_euler.x = 0.1 * pose
        bone(arm, "Head").rotation_euler.z = 0.15 * pose * math.sin(t * math.tau * 2)
        hat(tip=0.4 * hop - 0.2 * pose)
        hair_follow(-0.4 * hop + 0.1 * pose)
        bone(arm, "Cape").rotation_euler.x = 0.5 * hop
        for side in ("L", "R"):
            bone(arm, f"UpLeg.{side}").rotation_euler.x = -0.5 * hop
            bone(arm, f"Leg.{side}").rotation_euler.x = 0.9 * hop
        rest_arms()
        staff_world_pitch(-0.10)
        aim("UpperArm.R", (0.45, -0.2, 1.0), pose)
        aim("Forearm.R", (0.3, -0.15, 1.0), pose)
        aim("Staff", (0.15, -0.1, 1.0), pose)
        aim("UpperArm.L", (-0.55, -0.45, 0.70), pose)
        aim("Forearm.L", (0.45, -0.35, 0.82), pose)
        glow = pulse(t, 0.15, 0.9)
        bone(arm, "StaffTip").scale = (1 + glow * 0.5, 1 + glow * 0.5, 1 + glow * 0.5)

    def menu(frame, frames):
        """Main-menu loop (4 s): idle sway, a friendly wave, a staff twirl, and a little hop; seamless."""
        t = (frame - 1) / frames
        phase = t * math.tau
        breathe = math.sin(phase * 2)
        wave_in = curve(t, [(0.05, 0.0), (0.12, 1.0), (0.38, 1.0), (0.45, 0.0)])
        twirl = curve(t, [(0.52, 0.0), (0.72, 1.0)])
        twirl_arm = curve(t, [(0.50, 0.0), (0.56, 1.0), (0.72, 1.0), (0.80, 0.0)])
        hop = pulse(t, 0.82, 0.94)
        bone(arm, "Hips").location.y = breathe * 0.006 + 0.07 * hop
        bone(arm, "Hips").rotation_euler.z = math.sin(phase) * 0.04
        bone(arm, "Spine").rotation_euler.x = breathe * 0.02
        bone(arm, "Chest").rotation_euler.y = -0.12 * wave_in + math.sin(phase) * 0.03
        bone(arm, "Head").rotation_euler.z = 0.12 * wave_in + math.sin(phase) * 0.05
        bone(arm, "Head").rotation_euler.x = -0.04 + 0.05 * hop
        hat(roll=0.06 * wave_in, tip=math.sin(phase * 2 + 1.0) * 0.12 + 0.3 * hop)
        bone(arm, "HatTip").rotation_euler.z = math.cos(phase) * 0.08
        hair_follow(math.sin(phase * 2 + 0.8) * 0.05 - 0.3 * hop)
        bone(arm, "Cape").rotation_euler.x = math.sin(phase * 2 + 0.3) * 0.05 + 0.3 * hop
        rest_arms(breathe)
        for side in ("L", "R"):
            bone(arm, f"UpLeg.{side}").rotation_euler.x = -0.35 * hop
            bone(arm, f"Leg.{side}").rotation_euler.x = 0.7 * hop
        staff_world_pitch(-0.10)
        # Wave with the free hand: arm raised to the side, forearm swinging.
        waving = math.sin(t * math.tau * 9.0)
        aim("UpperArm.L", (-0.65, -0.65, 0.40), wave_in)
        aim("Forearm.L", (-0.20 + 0.45 * waving, -0.40, 0.95), wave_in)
        # Staff held out in front and spun once.
        aim("UpperArm.R", (0.30, -0.85, -0.10), twirl_arm)
        aim("Forearm.R", (0.10, -0.95, 0.20), twirl_arm)
        aim("Staff", (0.0, -0.25, 1.0), twirl_arm, spin=twirl * math.tau)
        glow = pulse(t, 0.55, 0.8)
        bone(arm, "StaffTip").scale = (1 + glow * 0.5, 1 + glow * 0.5, 1 + glow * 0.5)

    return [
        add_action(arm, "Idle", 48, idle),
        add_action(arm, "Run", 20, run),
        add_action(arm, "Attack", 16, attack),
        add_action(arm, "Dash", 10, dash),
        add_action(arm, "Hit", 10, hit),
        add_action(arm, "Death", 30, death),
        add_action(arm, "Cast", 20, cast),
        add_action(arm, "Victory", 40, victory),
        add_action(arm, "Menu", 120, menu),
    ]


# ---------------------------------------------------------------- slimes


def dumpling_boss_parts(name, radius, center_z, top, mats):
    """Boss body: a squishy glitter dumpling (pleated jelly bao, kawaii face, gold star on the forehead).

    Same armature as the slimes (Root / Body / Top); the jelly body is MAT_king (tinted per boss kind in Unity).
    """
    size = radius * 2.2
    rx = size * 0.5 * 1.12
    rz = size * 0.5 * 0.86
    cz = rz
    body = add_sphere(name + "_body", 1.0, (0, 0, cz), scale=(rx, rx, rz), seg=64, rings=36)
    pleats = 17
    for v in body.data.vertices:
        zn = v.co.z / (2 * rz)
        r = math.hypot(v.co.x, v.co.y)
        ang = math.atan2(v.co.y, v.co.x)
        w = max(0.0, min(1.0, (zn - 0.38) / 0.35))
        ridge = 1.0 + 0.085 * w * math.cos(ang * pleats + 0.3 * zn * 6.0)
        pinch = 1.0 - 0.62 * max(0.0, (zn - 0.78) / 0.22) ** 1.5
        k = ridge * pinch
        v.co.x *= k
        v.co.y *= k
        if v.co.z < 0.02 * size:
            v.co.z = 0.02 * size
    body.data.update()
    parts = [assign(body, ["Body", "Top"], mats["king"], sharpness=3.0)]
    parts.append(assign(add_sphere(name + "_nub", 0.07 * size, (0, 0, 2 * rz + 0.005 * size), scale=(1, 1, 0.9), seg=16, rings=10), "Top", mats["king"]))

    az0 = -math.pi / 2
    elev = math.radians(14)

    def on_body(az, el, push=0.0):
        return ((rx + push) * math.cos(el) * math.cos(az), (rx + push) * math.cos(el) * math.sin(az), cz + (rz + push) * math.sin(el))

    def facing(az):
        return (0, 0, az + math.pi / 2)

    eye_r = 0.058 * size
    for side in (-1, 1):
        az = az0 + side * math.radians(18)
        parts.append(assign(add_sphere(f"{name}_eye{side}", eye_r, on_body(az, elev, -0.012 * size), scale=(1.0, 0.55, 1.18), seg=18, rings=12, rot=facing(az)), "Body", mats["pupil"]))
        parts.append(assign(add_sphere(f"{name}_shine{side}", eye_r * 0.42, on_body(az - side * math.radians(2.3), elev + math.radians(4.0), 0.012 * size), scale=(1.0, 0.5, 1.0), seg=10, rings=8, rot=facing(az)), "Body", mats["eye_highlight"]))
        parts.append(assign(add_sphere(f"{name}_shine2{side}", eye_r * 0.18, on_body(az + side * math.radians(2.4), elev - math.radians(3.6), 0.012 * size), scale=(1.0, 0.5, 1.0), seg=8, rings=6, rot=facing(az)), "Body", mats["eye_highlight"]))
        cheek_az = az0 + side * math.radians(38)
        parts.append(assign(add_sphere(f"{name}_blush{side}", 0.075 * size, on_body(cheek_az, math.radians(1), -0.012 * size), scale=(1.25, 0.3, 0.72), seg=14, rings=8, rot=facing(cheek_az)), "Body", mats["blush"]))
    parts.append(assign(add_sphere(name + "_mouth", 0.04 * size, on_body(az0, math.radians(1), -0.012 * size), scale=(1.2, 0.4, 1.35), seg=14, rings=10, rot=facing(az0)), "Body", mats["mouth"]))
    parts.append(assign(add_sphere(name + "_tongue", 0.022 * size, on_body(az0, math.radians(-1), -0.004 * size), scale=(1.4, 0.4, 0.8), seg=10, rings=6, rot=facing(az0)), "Body", mats["blush"]))
    # Gold star on the forehead (like the toy's sparkle).
    star_pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = (0.085 if i % 2 == 0 else 0.038) * size
        star_pts.append((math.cos(a) * rr, math.sin(a) * rr))
    bm = bmesh.new()
    top_ring = [bm.verts.new((x, 0.0, z)) for x, z in star_pts]
    bottom_ring = [bm.verts.new((x, 0.02 * size, z)) for x, z in star_pts]
    bm.faces.new(top_ring)
    bm.faces.new(list(reversed(bottom_ring)))
    for i in range(10):
        j = (i + 1) % 10
        bm.faces.new((top_ring[i], top_ring[j], bottom_ring[j], bottom_ring[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name + "_star")
    bm.to_mesh(mesh)
    bm.free()
    star = bpy.data.objects.new(name + "_star", mesh)
    bpy.context.scene.collection.objects.link(star)
    star_el = math.radians(44)
    fx, fy, fz = on_body(az0, star_el, -0.012 * size)
    # Lay the star on the dome: tilt its -Y face to the surface normal at that elevation.
    star.rotation_euler = (-star_el, 0.0, 0.0)
    star.location = (fx, fy, fz)
    apply_xform(star)
    smooth(star, math.radians(40))
    parts.append(assign(star, "Body", mats["star"]))
    return parts


def build_slime(name, body_mat, radius, height_scale, kind, mats):
    center_z = radius * height_scale * 0.85
    top = center_z + radius * height_scale
    arm = new_armature(
        name,
        [
            ("Root", (0, 0, 0), (0, 0, 0.15), None),
            ("Body", (0, 0, 0.05), (0, 0, top), "Root"),
            ("Top", (0, 0, top * 0.8), (0, 0, top + 0.1), "Body"),
        ],
    )
    if kind == "king":
        p = dumpling_boss_parts(name, radius, center_z, top, mats)
    else:
        body = add_sphere(name + "_body", radius, (0, 0, center_z), scale=(1.05, 0.98, height_scale), seg=36, rings=20)
        for vert in body.data.vertices:
            # Flatten the base a little and bulge the sides for a gel-like silhouette.
            if vert.co.z < center_z - radius * height_scale * 0.6:
                vert.co.z = max(vert.co.z, 0.0) * 0.9 + 0.02
        p = [assign(body, ["Body", "Top"], body_mat, sharpness=3.0)]
        for index in range(3):
            angle = index * 2.1 + 0.4
            p.append(assign(add_sphere(f"{name}_blob{index}", radius * 0.22, (math.cos(angle) * radius * 0.5, math.sin(angle) * radius * 0.4 + 0.1, top - radius * 0.1), seg=14, rings=10), "Top", body_mat))
        def surface_y(x, z):
            """Front (-Y) surface of the scaled body sphere at (x, z)."""
            inner = radius * radius - (x / 1.05) ** 2 - ((z - center_z) / height_scale) ** 2
            return -0.98 * math.sqrt(max(0.0, inner))

        eye_z = center_z + radius * 0.18
        for side in (-1, 1):
            x = radius * 0.3 * side
            eye_y = surface_y(x, eye_z) + radius * 0.02
            p.append(assign(add_sphere(f"{name}_eye{side}", radius * 0.17, (x, eye_y, eye_z), scale=(1, 0.4, 1.25), seg=18, rings=10, rot=(0, 0, 0.3 * side)), "Body", mats["eye_white"]))
            p.append(assign(add_sphere(f"{name}_iris{side}", radius * 0.11, (x, eye_y - radius * 0.05, eye_z - radius * 0.01), scale=(1, 0.4, 1.2), seg=16, rings=10, rot=(0, 0, 0.3 * side)), "Body", mats["pupil"]))
            p.append(assign(add_sphere(f"{name}_shine{side}", radius * 0.045, (x - radius * 0.03 * side, eye_y - radius * 0.09, eye_z + radius * 0.05), seg=10, rings=6), "Body", mats["eye_highlight"]))
        mouth_z = eye_z - radius * 0.3
        p.append(assign(add_sphere(f"{name}_mouth", radius * 0.09, (0, surface_y(0, mouth_z) + radius * 0.01, mouth_z), scale=(1.6, 0.4, 0.6), seg=12, rings=6), "Body", mats["pupil"]))
        eye_y = surface_y(radius * 0.3, eye_z)
        if kind == "fast":
            for side in (-1, 1):
                p.append(assign(add_cone(f"{name}_fin{side}", radius * 0.2, radius * 0.7, (radius * 0.95 * side, 0.08, center_z + radius * 0.1), rot=(0.3, 1.25 * side, 0.3 * side), vertices=10), "Body", mats["slime_fin"]))
            p.append(assign(add_cone(f"{name}_crest", radius * 0.16, radius * 0.6, (0, 0.2, top - radius * 0.05), rot=(-1.1, 0, 0), vertices=10), "Top", mats["slime_fin"]))
        elif kind == "tank":
            helmet = add_ico(f"{name}_helmet", radius * 0.62, (0, 0.05, top - radius * 0.25), scale=(1.2, 1.0, 0.6), subdiv=2)
            displace(helmet, radius * 0.1, seed=3)
            p.append(assign(helmet, "Top", mats["rock"]))
            for index, (x, y, z, s) in enumerate(((-0.7, 0.2, 0.3, 0.32), (0.75, 0.1, 0.2, 0.28), (0.2, 0.75, 0.35, 0.3), (-0.3, 0.7, 0.1, 0.25))):
                plate = add_ico(f"{name}_plate{index}", radius * s, (x * radius, y * radius, center_z + z * radius), subdiv=1)
                displace(plate, radius * 0.08, seed=index)
                p.append(assign(plate, "Body", mats["rock"]))
            for side in (-1, 1):
                p.append(assign(add_cube(f"{name}_brow{side}", radius * 0.3, (radius * 0.3 * side, surface_y(radius * 0.3, eye_z + radius * 0.22) + radius * 0.02, eye_z + radius * 0.22), scale=(1.1, 0.3, 0.25), rot=(0, 0, 0.35 * side)), "Body", mats["rock"]))
    mesh = join(p, name)
    bind(mesh, arm)

    body_bone = bone(arm, "Body")
    top_bone = bone(arm, "Top")

    def squash(amount):
        body_bone.scale = (1.0 + amount, 1.0 + amount, 1.0 - amount * 1.4)

    def idle(frame, frames):
        phase = (frame - 1) / frames * math.tau
        squash(math.sin(phase) * 0.045)
        body_bone.location.y = math.sin(phase) * 0.02
        top_bone.rotation_euler.x = math.sin(phase + 0.9) * 0.08
        top_bone.rotation_euler.z = math.cos(phase * 0.5) * 0.06

    def move(frame, frames):
        phase = (frame - 1) / frames * math.tau
        hop = max(0.0, math.sin(phase))
        body_bone.location.y = hop * 0.16
        stretch = math.sin(phase) * 0.14
        body_bone.scale = (1.0 - stretch * 0.6, 1.0 - stretch * 0.6, 1.0 + stretch)
        body_bone.rotation_euler.x = -0.12 + math.sin(phase) * 0.12
        top_bone.rotation_euler.x = 0.25 - math.sin(phase) * 0.35

    def hit(frame, frames):
        t = (frame - 1) / (frames - 1)
        kick = curve(t, [(0.0, 0.0), (0.25, 1.0), (1.0, 0.0)])
        squash(0.3 * kick)
        body_bone.rotation_euler.x = -0.2 * kick
        top_bone.rotation_euler.x = -0.5 * kick

    def death(frame, frames):
        t = (frame - 1) / (frames - 1)
        melt = curve(t, [(0.0, 0.0), (1.0, 1.0)])
        body_bone.scale = (1.0 + 0.45 * melt, 1.0 + 0.45 * melt, max(0.08, 1.0 - 0.95 * melt))
        top_bone.rotation_euler.x = 0.8 * melt
        body_bone.location.y = -0.02 * melt

    def telegraph(frame, frames):
        t = (frame - 1) / (frames - 1)
        p1 = pulse(t, 0.0, 1.0)
        squash(-0.12 * p1)
        body_bone.location.y = 0.15 * p1
        top_bone.rotation_euler.x = -0.3 * p1
        top_bone.scale = (1 + 0.1 * p1, 1 + 0.1 * p1, 1 + 0.1 * p1)

    def slam(frame, frames):
        t = (frame - 1) / (frames - 1)
        lift = curve(t, [(0.0, 0.0), (0.4, 1.0), (0.55, 0.0)])
        impact = curve(t, [(0.5, 0.0), (0.62, 1.0), (1.0, 0.0)])
        body_bone.location.y = 0.35 * lift
        body_bone.scale = (1.0 - 0.15 * lift + 0.35 * impact, 1.0 - 0.15 * lift + 0.35 * impact, 1.0 + 0.25 * lift - 0.35 * impact)
        top_bone.rotation_euler.x = -0.4 * lift + 0.3 * impact

    clips = [
        add_action(arm, "Idle", 36, idle),
        add_action(arm, "Move", 18, move),
        add_action(arm, "Hit", 8, hit),
        add_action(arm, "Death", 18, death),
    ]
    if kind == "king":
        clips.append(add_action(arm, "Telegraph", 18, telegraph))
        clips.append(add_action(arm, "Slam", 18, slam))
    return arm, mesh, clips


# ---------------------------------------------------------------- environment


def static_mesh(obj, mat):
    obj.data.materials.append(mat)
    return obj


def build_tree(name, mats, canopy_scale=1.0, seed=0):
    trunk = static_mesh(add_cylinder(name + "_trunk", 0.2, 1.45, (0, 0, 0.72), vertices=12, radius2=0.12), mats["bark"])
    parts = [trunk]
    for index in range(3):
        angle = index / 3 * math.tau + seed
        parts.append(static_mesh(add_sphere(f"{name}_root{index}", 0.12, (math.cos(angle) * 0.17, math.sin(angle) * 0.17, 0.05), scale=(1.4, 1.0, 0.6), seg=12, rings=8, rot=(0, 0, angle)), mats["bark"]))
    canopy_specs = [
        ((0, 0, 1.75), 0.62 * canopy_scale, "leaf"),
        ((0.32, -0.1, 1.95), 0.42 * canopy_scale, "leaf_light"),
        ((-0.3, 0.15, 1.9), 0.4 * canopy_scale, "leaf"),
        ((0.05, 0.3, 2.1), 0.36 * canopy_scale, "leaf_light"),
        ((-0.05, -0.28, 1.6), 0.36 * canopy_scale, "leaf_dark"),
    ]
    for index, (loc, radius, key) in enumerate(canopy_specs):
        blob = add_ico(f"{name}_leaf{index}", radius, loc, scale=(1.05, 1.0, 0.85), subdiv=2)
        displace(blob, radius * 0.18, seed=seed + index)
        smooth(blob)
        parts.append(static_mesh(blob, mats[key]))
    return join(parts, name)


def build_rock(name, mats, scale, seed=0):
    rock = add_ico(name, 0.45, (0, 0, 0.3 * scale[2]), scale=scale, subdiv=2)
    displace(rock, 0.14, seed=seed)
    for vert in rock.data.vertices:
        vert.co.z = max(vert.co.z, 0.0)
    smooth(rock, math.radians(40))
    return static_mesh(rock, mats["rock_tex"])


def build_mushroom(name, mats):
    stem = static_mesh(add_cylinder(name + "_stem", 0.09, 0.3, (0, 0, 0.15), vertices=14, radius2=0.07), mats["stem"])
    cap = add_sphere(name + "_cap", 0.24, (0, 0, 0.3), scale=(1.3, 1.3, 0.6), seg=24, rings=12)
    carve(cap, lambda co: co.z < 0.27)
    parts = [stem, static_mesh(cap, mats["mushroom"])]
    for index in range(6):
        angle = index / 6 * math.tau + 0.4
        r = 0.16 + (index % 2) * 0.08
        parts.append(static_mesh(add_sphere(f"{name}_spot{index}", 0.035 + (index % 3) * 0.01, (math.cos(angle) * r, math.sin(angle) * r, 0.33 + (0.08 if r < 0.2 else 0.03)), scale=(1, 1, 0.5), seg=10, rings=6), mats["mushroom_spot"]))
    return join(parts, name)


def build_bush(name, mats):
    parts = []
    for index, (loc, radius, key) in enumerate((((0, 0, 0.26), 0.32, "leaf"), ((0.2, -0.08, 0.22), 0.22, "leaf_light"), ((-0.18, 0.1, 0.2), 0.2, "leaf_dark"), ((0.02, 0.2, 0.3), 0.18, "leaf"))):
        blob = add_ico(f"{name}_{index}", radius, loc, subdiv=2)
        displace(blob, radius * 0.2, seed=index + 10)
        smooth(blob)
        parts.append(static_mesh(blob, mats[key]))
    for index in range(4):
        angle = index / 4 * math.tau + 0.7
        parts.append(static_mesh(add_sphere(f"{name}_berry{index}", 0.035, (math.cos(angle) * 0.28, math.sin(angle) * 0.24 - 0.04, 0.3 + (index % 2) * 0.1), seg=10, rings=6), mats["mushroom"]))
    return join(parts, name)


def build_pillar(name, mats):
    base = bevel(add_cube(name + "_base", 0.8, (0, 0, 0.12), scale=(1, 1, 0.3)), 0.03)
    shaft = static_mesh(add_cylinder(name + "_shaft", 0.24, 1.4, (0, 0, 0.9), vertices=10, radius2=0.2), mats["ruin"])
    displace(shaft, 0.015, seed=5)
    cap = bevel(add_cube(name + "_cap", 0.7, (0, 0, 1.68), scale=(1, 1, 0.3)), 0.03)
    cap.data.materials.append(mats["ruin"])
    base.data.materials.append(mats["ruin"])
    moss = static_mesh(add_sphere(name + "_moss", 0.18, (0.1, -0.15, 0.3), scale=(1.4, 0.5, 1.2), seg=12, rings=8), mats["moss"])
    return join([base, shaft, cap, moss], name)


def build_grass(name, mats):
    parts = []
    for index in range(7):
        angle = index / 7 * math.tau
        r = 0.08 if index else 0.0
        h = 0.22 + hash01(index, 1, 2) * 0.16
        parts.append(static_mesh(add_cone(f"{name}_{index}", 0.03, h, (math.cos(angle) * r, math.sin(angle) * r, h / 2), rot=(math.sin(angle) * 0.35, -math.cos(angle) * 0.35, 0), vertices=6), mats["grass"]))
    return join(parts, name)


def build_flower(name, mats):
    parts = [static_mesh(add_cylinder(name + "_stem", 0.012, 0.28, (0, 0, 0.14), vertices=6), mats["grass"])]
    for index in range(5):
        angle = index / 5 * math.tau
        parts.append(static_mesh(add_sphere(f"{name}_petal{index}", 0.055, (math.cos(angle) * 0.06, math.sin(angle) * 0.06, 0.29), scale=(1.2, 0.8, 0.35), seg=12, rings=6, rot=(0, 0, angle)), mats["petal"]))
    parts.append(static_mesh(add_sphere(name + "_center", 0.035, (0, 0, 0.3), scale=(1, 1, 0.6), seg=12, rings=6), mats["petal_center"]))
    return join(parts, name)


def build_boundary_stone(name, mats):
    stone = add_ico(name, 0.35, (0, 0, 0.3), scale=(1.0, 0.7, 1.3), subdiv=2)
    displace(stone, 0.1, seed=9)
    for vert in stone.data.vertices:
        vert.co.z = max(vert.co.z, 0.0)
    smooth(stone, math.radians(40))
    static_mesh(stone, mats["rock_tex"])
    rune = static_mesh(add_ico(name + "_rune", 0.07, (0, -0.24, 0.42), scale=(1, 0.4, 1.4), subdiv=1), mats["rune"])
    return join([stone, rune], name)


def build_cactus(name, mats):
    trunk = static_mesh(add_cylinder(name + "_trunk", 0.16, 1.35, (0, 0, 0.67), vertices=12, radius2=0.13), mats["cactus"])
    arm = static_mesh(add_cylinder(name + "_arm", 0.09, 0.55, (0.32, 0, 0.85), vertices=10, rot=(0, 1.15, 0)), mats["cactus_dark"])
    tip = static_mesh(add_sphere(name + "_tip", 0.11, (0.52, 0, 1.05), seg=12, rings=8), mats["cactus"])
    arm2 = static_mesh(add_cylinder(name + "_arm2", 0.08, 0.42, (-0.26, 0.05, 0.7), vertices=10, rot=(0, -1.05, 0.2)), mats["cactus"])
    blossom = static_mesh(add_sphere(name + "_flower", 0.08, (0, 0, 1.38), scale=(1.1, 1.1, 0.55), seg=12, rings=6), mats["petal"])
    return join([trunk, arm, tip, arm2, blossom], name)


def build_ice_spike(name, mats):
    parts = []
    specs = [
        (0.22, 1.35, (0, 0, 0.67)),
        (0.14, 0.9, (0.22, -0.08, 0.45)),
        (0.12, 0.7, (-0.18, 0.14, 0.35)),
        (0.1, 0.5, (0.08, 0.2, 0.25)),
    ]
    for index, (radius, height, loc) in enumerate(specs):
        spike = add_cone(f"{name}_{index}", radius, height, loc, vertices=8)
        displace(spike, 0.03, seed=20 + index)
        parts.append(static_mesh(spike, mats["ice_env"]))
    return join(parts, name)


def build_thunder_spire(name, mats):
    base = bevel(add_cube(name + "_base", 0.7, (0, 0, 0.1), scale=(1, 1, 0.28)), 0.02)
    base.data.materials.append(mats["ruin"])
    shaft = static_mesh(add_cylinder(name + "_shaft", 0.12, 1.8, (0, 0, 1.05), vertices=8, radius2=0.06), mats["slate"])
    tip = static_mesh(add_cone(name + "_tip", 0.08, 0.35, (0, 0, 2.05), vertices=8), mats["thunder_glow"])
    ring = static_mesh(add_torus(name + "_ring", 0.22, 0.03, (0, 0, 1.55), major_segments=16, minor_segments=8), mats["thunder_glow"])
    return join([base, shaft, tip, ring], name)


def build_swamp_root(name, mats):
    trunk = static_mesh(add_cylinder(name + "_trunk", 0.18, 1.1, (0, 0, 0.55), vertices=10, radius2=0.1), mats["swamp_bark"])
    displace(trunk, 0.04, seed=33)
    parts = [trunk]
    for index in range(4):
        angle = index / 4 * math.tau + 0.3
        loc = (math.cos(angle) * 0.28, math.sin(angle) * 0.28, 0.2)
        root = add_sphere(f"{name}_root{index}", 0.12, loc, scale=(1.6, 0.7, 0.45), seg=10, rings=6, rot=(0, 0, angle))
        parts.append(static_mesh(root, mats["swamp_bark"]))
    moss = static_mesh(add_ico(name + "_moss", 0.22, (0.05, -0.08, 0.85), scale=(1.2, 0.9, 0.6), subdiv=1), mats["moss"])
    return join(parts + [moss], name)


def build_swamp_reed(name, mats):
    parts = []
    for index in range(6):
        angle = index / 6 * math.tau
        r = 0.07 if index else 0.0
        h = 0.55 + hash01(index, 3, 5) * 0.28
        parts.append(static_mesh(add_cone(f"{name}_{index}", 0.025, h, (math.cos(angle) * r, math.sin(angle) * r, h / 2), rot=(math.sin(angle) * 0.2, -math.cos(angle) * 0.2, 0), vertices=5), mats["reed"]))
    return join(parts, name)


def build_shadow_obelisk(name, mats):
    body = add_cube(name + "_body", 0.55, (0, 0, 0.85), scale=(0.7, 0.7, 3.1))
    for vert in body.data.vertices:
        if vert.co.z > 1.2:
            vert.co.x *= 0.45
            vert.co.y *= 0.45
    apply_xform(body)
    static_mesh(body, mats["shadow_stone"])
    gem = static_mesh(add_ico(name + "_gem", 0.12, (0, -0.18, 1.15), scale=(0.7, 0.4, 1.4), subdiv=1), mats["arcane"])
    return join([body, gem], name)


def build_volcano_rock(name, mats):
    rock = add_ico(name, 0.5, (0, 0, 0.32), scale=(1.2, 1.0, 0.85), subdiv=2)
    displace(rock, 0.16, seed=44)
    for vert in rock.data.vertices:
        vert.co.z = max(vert.co.z, 0.0)
    smooth(rock, math.radians(35))
    static_mesh(rock, mats["basalt"])
    glow = static_mesh(add_ico(name + "_lava", 0.18, (0.08, -0.05, 0.28), scale=(1.3, 0.8, 0.5), subdiv=1), mats["lava"])
    return join([rock, glow], name)


def build_volcano_pillar(name, mats):
    base = bevel(add_cube(name + "_base", 0.75, (0, 0, 0.12), scale=(1, 1, 0.3)), 0.03)
    base.data.materials.append(mats["basalt"])
    shaft = static_mesh(add_cylinder(name + "_shaft", 0.22, 1.35, (0, 0, 0.85), vertices=8, radius2=0.18), mats["basalt"])
    cap = static_mesh(add_sphere(name + "_cap", 0.2, (0, 0, 1.55), scale=(1, 1, 0.45), seg=12, rings=8), mats["lava"])
    return join([base, shaft, cap], name)


def build_storm_arch(name, mats):
    left = static_mesh(add_cube(name + "_l", 0.7, (-0.45, 0, 0.7), scale=(0.35, 0.35, 2.0)), mats["slate"])
    right = static_mesh(add_cube(name + "_r", 0.7, (0.45, 0, 0.7), scale=(0.35, 0.35, 2.0)), mats["slate"])
    beam = static_mesh(add_cube(name + "_beam", 0.7, (0, 0, 1.42), scale=(1.55, 0.32, 0.28)), mats["ruin"])
    return join([left, right, beam], name)


def build_crystal_spire(name, mats):
    parts = []
    specs = [
        ((0, 0, 0.7), (0.55, 0.55, 1.8), 0.28),
        ((0.22, -0.1, 0.4), (0.45, 0.45, 1.3), 0.18),
        ((-0.2, 0.12, 0.32), (0.4, 0.4, 1.1), 0.15),
        ((0.05, 0.22, 0.22), (0.35, 0.35, 0.8), 0.12),
    ]
    for index, (loc, scale, radius) in enumerate(specs):
        shard = add_ico(f"{name}_{index}", radius, loc, scale=scale, subdiv=1)
        parts.append(static_mesh(shard, mats["crystal"] if index == 0 else mats["ice_env"]))
    return join(parts, name)


def build_royal_column(name, mats):
    base = bevel(add_cube(name + "_base", 0.85, (0, 0, 0.12), scale=(1, 1, 0.28)), 0.04)
    base.data.materials.append(mats["marble"])
    shaft = static_mesh(add_cylinder(name + "_shaft", 0.2, 1.5, (0, 0, 0.95), vertices=12), mats["marble"])
    ring = static_mesh(add_torus(name + "_ring", 0.24, 0.035, (0, 0, 1.35), major_segments=20, minor_segments=8), mats["gold"])
    cap = bevel(add_cube(name + "_cap", 0.72, (0, 0, 1.75), scale=(1, 1, 0.28)), 0.03)
    cap.data.materials.append(mats["gold"])
    gem = static_mesh(add_ico(name + "_gem", 0.1, (0, 0, 1.95), subdiv=1), mats["crystal"])
    return join([base, shaft, ring, cap, gem], name)


def build_ground(mats):
    # 80 m so the camera never sees the edge; the rim rises into gentle hills past the play area.
    size = 80
    bpy.ops.mesh.primitive_grid_add(size=size, x_subdivisions=60, y_subdivisions=60, location=(0, 0, 0))
    obj = bpy.context.active_object
    for vert in obj.data.vertices:
        r = math.hypot(vert.co.x, vert.co.y)
        if r > 13.0:
            rise = (r - 13.0) / 27.0
            vert.co.z += rise * rise * 3.0 + math.sin(vert.co.x * 0.9) * 0.15 * rise + math.cos(vert.co.y * 0.7) * 0.12 * rise
    apply_xform(obj)
    obj.name = "ENV_Forest_Ground"
    uv = obj.data.uv_layers.active
    if uv is not None:
        for loop in uv.data:
            loop.uv = (loop.uv[0] * 26.0, loop.uv[1] * 26.0)
    smooth(obj)
    obj.data.materials.append(mats["ground"])
    return obj


def pose_idle(arm):
    if arm.animation_data is None:
        return
    idle = next((action for action in bpy.data.actions if action.name == "Idle"), None)
    if idle is not None:
        arm.animation_data.action = idle
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")


def render_action_frames(arm, asset_id, target_z, distance, height, clips):
    """Contact sheet frames so the animation poses can be checked without Unity."""
    for action_name, fractions in clips:
        action = next((a for a in bpy.data.actions if a.name == action_name), None)
        if action is None:
            continue
        arm.animation_data.action = action
        start, end = action.frame_range
        for fraction in fractions:
            frame = int(round(start + (end - start) * fraction))
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            render_preview(os.path.join(PREVIEW, f"{asset_id}_{action_name}_{int(fraction * 100):02d}.png"), (0, 0, target_z), distance, height)
            if os.environ.get("HERO_DEBUG_VIEWS"):
                cam = bpy.context.scene.camera
                for view, location in (("front", (0.0, -distance, target_z + 0.1)), ("side", (distance, 0.0, target_z + 0.1))):
                    cam.location = location
                    look_at(cam, (0, 0, target_z))
                    bpy.context.scene.render.filepath = os.path.join(PREVIEW, "debug", f"{action_name}_{int(fraction * 100):02d}_{view}.png")
                    bpy.ops.render.render(write_still=True)


def produce_character(asset_id, folder, builder, preview_distance, preview_height, target_z, action_sheet=None):
    arm, mesh, _actions = builder()
    if action_sheet:
        render_action_frames(arm, asset_id, target_z, preview_distance, preview_height, action_sheet)
    pose_idle(arm)
    render_preview(os.path.join(PREVIEW, asset_id + ".png"), (0, 0, target_z), preview_distance, preview_height)
    if asset_id == "CHR_Hero_Main":
        render_preview(os.path.join(PREVIEW, asset_id + "_Face.png"), (0, 0, 1.02), 1.25, 1.05)
        cam = bpy.context.scene.camera
        cam.location = (0.0, 3.4, 1.2)
        look_at(cam, (0, 0, 0.8))
        bpy.context.scene.render.filepath = os.path.join(PREVIEW, asset_id + "_Back.png")
        bpy.ops.render.render(write_still=True)
    filename = os.path.join(folder, asset_id + ".fbx")
    export_fbx(os.path.join(OUT, filename), [arm], True)
    record(asset_id, filename, "character", [mesh], "generic rig, in-place clips")
    cleanup_objects()


def produce_static(asset_id, folder, obj, distance, height, target_z):
    render_preview(os.path.join(PREVIEW, asset_id + ".png"), (0, 0, target_z), distance, height)
    filename = os.path.join(folder, asset_id + ".fbx")
    export_fbx(os.path.join(OUT, filename), [obj], False)
    record(asset_id, filename, "static", [obj], "no rig")
    cleanup_objects()


def produce_biome_props(mats):
    biomes = "Environment/Biomes"
    produce_static("ENV_Desert_Cactus", biomes, build_cactus("ENV_Desert_Cactus", mats), 3.6, 1.5, 0.7)
    produce_static("ENV_Snow_IceSpike", biomes, build_ice_spike("ENV_Snow_IceSpike", mats), 3.4, 1.5, 0.6)
    produce_static("ENV_Thunder_Spire", biomes, build_thunder_spire("ENV_Thunder_Spire", mats), 4.4, 2.1, 0.9)
    produce_static("ENV_Swamp_Root", biomes, build_swamp_root("ENV_Swamp_Root", mats), 3.6, 1.5, 0.55)
    produce_static("ENV_Swamp_Reed", biomes, build_swamp_reed("ENV_Swamp_Reed", mats), 2.4, 1.1, 0.3)
    produce_static("ENV_Shadow_Obelisk", biomes, build_shadow_obelisk("ENV_Shadow_Obelisk", mats), 4.8, 2.2, 0.95)
    produce_static("ENV_Volcano_Rock", biomes, build_volcano_rock("ENV_Volcano_Rock", mats), 2.8, 1.2, 0.3)
    produce_static("ENV_Volcano_Pillar", biomes, build_volcano_pillar("ENV_Volcano_Pillar", mats), 4.2, 1.9, 0.85)
    produce_static("ENV_Storm_Arch", biomes, build_storm_arch("ENV_Storm_Arch", mats), 4.0, 1.8, 0.8)
    produce_static("ENV_Crystal_Spire", biomes, build_crystal_spire("ENV_Crystal_Spire", mats), 4.0, 1.8, 0.7)
    produce_static("ENV_Royal_Column", biomes, build_royal_column("ENV_Royal_Column", mats), 4.6, 2.0, 0.95)


def main():
    textures = make_textures()
    reset_file()
    mats = {key: material("MAT_" + key, color, emission=EMISSIVE.get(key, 0.0)) for key, color in PALETTE.items()}
    mats["gold"] = material("MAT_gold", PALETTE["gold"], rough=0.3, metallic=0.7)
    mats["star"] = material("MAT_star", (0.98, 0.78, 0.22, 1), emission=1.0, rough=0.3)
    mats["crystal"] = material("MAT_crystal", PALETTE["crystal"], emission=1.0, rough=0.15)
    mats["gem"] = material("MAT_gem", PALETTE["gem"], emission=0.9, rough=0.2)
    mats["xp"] = material("MAT_xp", PALETTE["xp"], emission=0.9, rough=0.25)
    mats["ring"] = material("MAT_ring", PALETTE["ring"], emission=0.7, rough=0.35)
    mats["ice_env"] = material("MAT_ice_env", PALETTE["ice_env"], emission=0.55, rough=0.12)
    mats["lava"] = material("MAT_lava", PALETTE["lava"], emission=1.4, rough=0.25)
    mats["thunder_glow"] = material("MAT_thunder_glow", PALETTE["thunder_glow"], emission=1.1, rough=0.2)
    mats["ground"] = material("MAT_Forest_Ground", PALETTE["moss"], rough=0.95, image_path=textures["ground"])
    mats["bark"] = material("MAT_bark", PALETTE["bark"], rough=0.9, image_path=textures["bark"])
    mats["rock_tex"] = material("MAT_rock_tex", PALETTE["rock"], rough=0.85, image_path=textures["rock"])

    env_only = MODE == "env"
    hero_only = MODE == "hero"
    if MODE == "boss":
        studio = hero_studio()
        produce_character("BOSS_SlimeKing", "Enemies/SlimeKing", lambda: build_slime("BOSS_SlimeKing", mats["king"], 0.95, 1.05, "king", mats), 5.5, 2.1, 0.9)
        hero_studio_off(studio)
        write_catalog(merge=True)
        return
    if not env_only:
        studio = hero_studio()
        produce_character(
            "CHR_Hero_Main",
            "Characters/Hero",
            lambda: build_hero(mats),
            3.6,
            1.25,
            0.8,
            action_sheet=HERO_SHEET,
        )
        hero_studio_off(studio)
    if hero_only:
        write_catalog(merge=True)
        return
    if not env_only:
        produce_character("ENM_Slime_Normal", "Enemies/Slime", lambda: build_slime("ENM_Slime_Normal", mats["slime"], 0.48, 0.9, "normal", mats), 3.2, 1.2, 0.4)
        produce_character("ENM_Slime_Fast", "Enemies/Slime", lambda: build_slime("ENM_Slime_Fast", mats["slime_fast"], 0.36, 0.72, "fast", mats), 2.8, 1.05, 0.3)
        produce_character("ENM_Slime_Tank", "Enemies/Slime", lambda: build_slime("ENM_Slime_Tank", mats["slime_tank"], 0.62, 1.05, "tank", mats), 3.8, 1.45, 0.55)
        produce_character("BOSS_SlimeKing", "Enemies/SlimeKing", lambda: build_slime("BOSS_SlimeKing", mats["king"], 0.95, 1.05, "king", mats), 5.5, 2.1, 0.9)

        env = "Environment/ForestArena"
        produce_static("ENV_Forest_Ground", env, build_ground(mats), 26, 16, 0)
        produce_static("ENV_Forest_Tree_A", env, build_tree("ENV_Forest_Tree_A", mats, 1.0, seed=1), 5.0, 2.0, 1.2)
        produce_static("ENV_Forest_Tree_B", env, build_tree("ENV_Forest_Tree_B", mats, 1.25, seed=7), 5.6, 2.2, 1.3)
        produce_static("ENV_Forest_Rock_A", env, build_rock("ENV_Forest_Rock_A", mats, (1.3, 1.0, 0.7), seed=2), 2.6, 1.2, 0.25)
        produce_static("ENV_Forest_Rock_B", env, build_rock("ENV_Forest_Rock_B", mats, (0.8, 1.1, 0.9), seed=4), 2.4, 1.1, 0.25)
        produce_static("ENV_Forest_Mushroom", env, build_mushroom("ENV_Forest_Mushroom", mats), 2.2, 1.0, 0.25)
        produce_static("ENV_Forest_Bush", env, build_bush("ENV_Forest_Bush", mats), 2.2, 1.0, 0.25)
        produce_static("ENV_Forest_RuinPillar", env, build_pillar("ENV_Forest_RuinPillar", mats), 4.6, 1.9, 0.9)
        produce_static("ENV_Forest_Grass", env, build_grass("ENV_Forest_Grass", mats), 1.6, 0.7, 0.15)
        produce_static("ENV_Forest_Flower", env, build_flower("ENV_Forest_Flower", mats), 1.4, 0.6, 0.2)
        produce_static("ENV_Forest_BoundaryStone", env, build_boundary_stone("ENV_Forest_BoundaryStone", mats), 2.4, 1.1, 0.3)
        rune_ring = static_mesh(add_torus("ENV_Arena_RuneRing", 11.3, 0.09, (0, 0, 0.02), scale=(1, 1, 0.35), major_segments=96, minor_segments=8), mats["rune"])
        produce_static("ENV_Arena_RuneRing", env, rune_ring, 30, 18, 0)

    produce_biome_props(mats)

    if not env_only:
        ring = static_mesh(add_torus("VFX_Boss_Ring", 1.0, 0.08, (0, 0, 0.02), scale=(1, 1, 0.5), major_segments=48, minor_segments=8), mats["ring"])
        produce_static("VFX_Boss_Ring", "VFX", ring, 3.4, 2.2, 0.1)
        orb = static_mesh(add_ico("VFX_XP_Orb", 0.16, (0, 0, 0.16), subdiv=2), mats["xp"])
        produce_static("VFX_XP_Orb", "VFX", orb, 1.6, 0.7, 0.16)
        shard = static_mesh(add_ico("VFX_Ice_Shard", 0.18, (0, 0, 0.0), scale=(0.55, 0.55, 1.7), subdiv=0), mats["ice"])
        produce_static("VFX_Ice_Shard", "VFX", shard, 1.6, 0.7, 0.0)
        missile = static_mesh(add_ico("VFX_Arcane_Missile", 0.14, (0, 0, 0.0), scale=(0.7, 0.7, 1.6), subdiv=1), mats["arcane"])
        produce_static("VFX_Arcane_Missile", "VFX", missile, 1.6, 0.7, 0.0)
        bolt = static_mesh(add_ico("VFX_Magic_Bolt", 0.12, (0, 0, 0.0), subdiv=1), mats["crystal"])
        produce_static("VFX_Magic_Bolt", "VFX", bolt, 1.4, 0.6, 0.0)
        fire_ring = static_mesh(add_torus("VFX_Fire_Ring", 1.0, 0.16, (0, 0, 0.1), scale=(1, 1, 0.6), major_segments=48, minor_segments=10), mats["fire"])
        produce_static("VFX_Fire_Ring", "VFX", fire_ring, 3.4, 2.2, 0.1)
        crystal = static_mesh(add_ico("VFX_PowerUp_Crystal", 0.3, (0, 0, 0.0), scale=(0.8, 0.8, 1.6), subdiv=1), mats["powerup"])
        halo = static_mesh(add_torus("VFX_PowerUp_Crystal_ring", 0.5, 0.03, (0, 0, 0.0), major_segments=40, minor_segments=8), mats["gold"])
        produce_static("VFX_PowerUp_Crystal", "VFX", join([crystal, halo], "VFX_PowerUp_Crystal"), 2.4, 1.0, 0.0)

    write_catalog(merge=env_only)


def hero_studio():
    """EEVEE with a key/fill/rim rig so hero previews show real materials and the eye texture."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.view_settings.view_transform = "Standard"
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.62, 0.68, 0.78, 1)
        background.inputs["Strength"].default_value = 0.45
    lights = []
    for name, kind, energy, location, color in (
        ("Key", "SUN", 2.0, (-2.0, -3.0, 4.0), (1.0, 0.97, 0.92)),
        ("Fill", "SUN", 0.5, (3.0, -2.0, 1.5), (0.80, 0.85, 1.0)),
        ("Rim", "SUN", 1.6, (0.5, 3.5, 3.0), (0.90, 0.80, 1.0)),
    ):
        data = bpy.data.lights.new(name, kind)
        data.energy = energy
        data.color = color
        light = bpy.data.objects.new(name, data)
        bpy.context.scene.collection.objects.link(light)
        light.location = location
        look_at(light, (0, 0, 0.8))
        lights.append(light)
    return lights


def hero_studio_off(lights):
    for light in lights:
        bpy.data.objects.remove(light, do_unlink=True)
    bpy.context.scene.render.engine = "BLENDER_WORKBENCH"


HERO_SHEET = [
    ("Idle", (0.0, 0.5)),
    ("Run", (0.0, 0.25, 0.5, 0.75)),
    ("Attack", (0.0, 0.18, 0.33, 0.5, 0.8)),
    ("Dash", (0.5,)),
    ("Hit", (0.2,)),
    ("Death", (0.4, 1.0)),
    ("Cast", (0.5,)),
    ("Victory", (0.25, 0.6)),
    ("Menu", (0.0, 0.2, 0.3, 0.62, 0.88)),
]


def write_catalog(merge):
    catalog_path = os.path.join(OUT, "catalog.json")
    merged = CATALOG
    if merge and os.path.isfile(catalog_path):
        with open(catalog_path, "r", encoding="utf-8") as handle:
            existing = json.load(handle)
        seen = {entry.get("id") for entry in CATALOG}
        merged = [entry for entry in existing if entry.get("id") not in seen] + CATALOG
    with open(catalog_path, "w", encoding="utf-8") as handle:
        json.dump(merged, handle, indent=2)
    print("CATALOG", catalog_path, flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)
