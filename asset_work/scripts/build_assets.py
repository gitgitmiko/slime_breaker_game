"""Generate original Slime Breaker assets (anime sorceress hero, slimes, boss, arena, VFX meshes)
and export Unity-ready FBX files.

Run:
  blender --background --python asset_work/scripts/build_assets.py -- <out_dir> <preview_dir> <texture_dir>

Blender axes: Z up, characters face -Y. The FBX exporter (forward -Z, up Y) turns that into
Unity's +Z forward.
"""

import json
import math
import os
import subprocess
import sys
import traceback

import bmesh
import bpy
import mathutils

PALETTE = {
    # hero (anime sorceress)
    "skin": (1.0, 0.86, 0.78, 1),
    "blush": (1.0, 0.62, 0.70, 1),
    "hair": (0.84, 0.78, 1.0, 1),
    "hair_dark": (0.62, 0.54, 0.86, 1),
    "robe": (0.23, 0.18, 0.56, 1),
    "robe_light": (0.42, 0.36, 0.84, 1),
    "hat": (0.16, 0.13, 0.40, 1),
    "cape_inner": (0.86, 0.32, 0.62, 1),
    "boot": (0.22, 0.17, 0.30, 1),
    "gold": (0.96, 0.78, 0.30, 1),
    "iris": (0.49, 0.36, 1.0, 1),
    "pupil": (0.08, 0.06, 0.16, 1),
    "eye_white": (0.98, 0.98, 1.0, 1),
    "eye_highlight": (1.0, 1.0, 1.0, 1),
    "mouth": (0.72, 0.30, 0.38, 1),
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
}


def parse_args():
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Pass output, preview, and texture directories after --")
    args = argv[argv.index("--") + 1 :]
    if len(args) < 3:
        raise SystemExit("Expected: <out_dir> <preview_dir> <texture_dir>")
    return args[0], args[1], args[2]


OUT, PREVIEW, TEX = parse_args()
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
        if obj.type == "CAMERA":
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
        ("Hips", (0, 0, 0.45), (0, 0, 0.56), "Root"),
        ("Spine", (0, 0, 0.56), (0, 0, 0.68), "Hips"),
        ("Chest", (0, 0, 0.68), (0, 0, 0.80), "Spine"),
        ("Neck", (0, 0, 0.80), (0, 0, 0.86), "Chest"),
        ("Head", (0, 0, 0.86), (0, 0, 1.25), "Neck"),
        ("HatTip", (0, 0.02, 1.48), (0, 0.06, 1.72), "Head"),
        ("Hair.L", (-0.25, 0.13, 1.06), (-0.29, 0.17, 0.80), "Head"),
        ("Hair.L.2", (-0.29, 0.17, 0.80), (-0.34, 0.21, 0.52), "Hair.L"),
        ("Hair.R", (0.25, 0.13, 1.06), (0.29, 0.17, 0.80), "Head"),
        ("Hair.R.2", (0.29, 0.17, 0.80), (0.34, 0.21, 0.52), "Hair.R"),
        ("Cape", (0, 0.12, 0.78), (0, 0.20, 0.45), "Chest"),
        ("Shoulder.L", (-0.07, 0, 0.77), (-0.20, 0, 0.75), "Chest"),
        ("UpperArm.L", (-0.20, 0, 0.75), (-0.27, -0.02, 0.57), "Shoulder.L"),
        ("Forearm.L", (-0.27, -0.02, 0.57), (-0.30, -0.08, 0.42), "UpperArm.L"),
        ("Hand.L", (-0.30, -0.08, 0.42), (-0.32, -0.12, 0.33), "Forearm.L"),
        ("Shoulder.R", (0.07, 0, 0.77), (0.20, 0, 0.75), "Chest"),
        ("UpperArm.R", (0.20, 0, 0.75), (0.27, -0.02, 0.57), "Shoulder.R"),
        ("Forearm.R", (0.27, -0.02, 0.57), (0.30, -0.08, 0.42), "UpperArm.R"),
        ("Hand.R", (0.30, -0.08, 0.42), (0.32, -0.12, 0.33), "Forearm.R"),
        ("Staff", (0.33, -0.13, 0.40), (0.33, -0.13, 1.05), "Hand.R"),
        ("StaffTip", (0.33, -0.13, 1.36), (0.33, -0.13, 1.46), "Staff"),
        ("UpLeg.L", (-0.09, 0, 0.47), (-0.095, 0, 0.26), "Hips"),
        ("Leg.L", (-0.095, 0, 0.26), (-0.10, 0, 0.10), "UpLeg.L"),
        ("Foot.L", (-0.10, 0, 0.10), (-0.10, -0.12, 0.02), "Leg.L"),
        ("UpLeg.R", (0.09, 0, 0.47), (0.095, 0, 0.26), "Hips"),
        ("Leg.R", (0.095, 0, 0.26), (0.10, 0, 0.10), "UpLeg.R"),
        ("Foot.R", (0.10, 0, 0.10), (0.10, -0.12, 0.02), "Leg.R"),
    ]


def build_hero(mats):
    arm = new_armature("CHR_Hero_Main", hero_bones())
    p = []

    # --- head and face
    p.append(assign(add_sphere("head", 0.265, (0, 0, 1.04), scale=(1.0, 0.96, 1.0), seg=36, rings=20), "Head", mats["skin"]))
    for side in (-1, 1):
        x = 0.1 * side
        p.append(assign(add_sphere(f"eye_white{side}", 0.072, (x, -0.232, 1.045), scale=(1.0, 0.32, 1.35), seg=20, rings=12, rot=(0, 0, 0.18 * side)), "Head", mats["eye_white"]))
        p.append(assign(add_sphere(f"iris{side}", 0.052, (x, -0.252, 1.035), scale=(1.0, 0.25, 1.32), seg=20, rings=12), "Head", mats["iris"]))
        p.append(assign(add_sphere(f"pupil{side}", 0.026, (x, -0.262, 1.025), scale=(1.0, 0.25, 1.4), seg=14, rings=8), "Head", mats["pupil"]))
        p.append(assign(add_sphere(f"shine{side}", 0.018, (x - 0.018 * side, -0.272, 1.072), scale=(1, 0.3, 1), seg=12, rings=8), "Head", mats["eye_highlight"]))
        p.append(assign(add_sphere(f"shine_small{side}", 0.009, (x + 0.02 * side, -0.271, 1.0), scale=(1, 0.3, 1), seg=10, rings=6), "Head", mats["eye_highlight"]))
        p.append(assign(add_sphere(f"lash{side}", 0.075, (x, -0.228, 1.08), scale=(1.05, 0.3, 0.5), seg=20, rings=10), "Head", mats["hair_dark"]))
        p.append(assign(add_cube(f"brow{side}", 0.08, (x, -0.205, 1.165), scale=(1.1, 0.22, 0.2), rot=(0, 0.12 * side, 0)), "Head", mats["hair_dark"]))
        p.append(assign(add_sphere(f"blush{side}", 0.038, (0.175 * side, -0.185, 0.975), scale=(1.0, 0.25, 0.6), seg=16, rings=8, rot=(0, 0, 0.6 * side)), "Head", mats["blush"]))
    p.append(assign(add_sphere("mouth", 0.02, (0, -0.246, 0.955), scale=(1.4, 0.3, 0.55), seg=12, rings=6), "Head", mats["mouth"]))

    # --- hair: cap leaves the face open, then bangs, side locks, twin tails
    cap = add_sphere("hair_cap", 0.285, (0, 0.012, 1.065), scale=(1.03, 1.0, 1.0), seg=36, rings=20)
    carve(cap, lambda co: co.y < -0.05 and co.z < 1.14)
    p.append(assign(cap, "Head", mats["hair"]))
    for index, (x, drop, tilt) in enumerate(((-0.17, 1.27, 0.35), (-0.085, 1.24, 0.15), (0.0, 1.25, 0.0), (0.085, 1.23, -0.15), (0.17, 1.27, -0.35))):
        p.append(assign(add_sphere(f"bang{index}", 0.06, (x, -0.21, drop), scale=(0.95, 0.5, 1.7), seg=14, rings=10, rot=(-0.25, tilt, 0)), "Head", mats["hair"]))
    for side in (-1, 1):
        p.append(assign(add_sphere(f"lock{side}", 0.065, (0.245 * side, -0.09, 0.93), scale=(0.7, 0.7, 2.3), seg=14, rings=10, rot=(0.1, -0.12 * side, 0)), "Head", mats["hair"]))
        tail_name = "Hair.L" if side < 0 else "Hair.R"
        tail = add_sphere(f"tail{side}", 0.08, (0.29 * side, 0.17, 0.80), scale=(1.0, 1.0, 3.4), seg=18, rings=16, rot=(0.12, -0.18 * side, 0))
        p.append(assign(tail, ["Head", tail_name, tail_name + ".2"], mats["hair"], sharpness=3.0))
        p.append(assign(add_sphere(f"tail_tip{side}", 0.05, (0.34 * side, 0.21, 0.52), scale=(1.0, 1.0, 1.6), seg=14, rings=10), tail_name + ".2", mats["hair_dark"]))
        p.append(assign(add_torus(f"tie{side}", 0.065, 0.02, (0.25 * side, 0.13, 1.06), rot=(0.3, 0.9 * side, 0), major_segments=20, minor_segments=8), tail_name, mats["gold"]))

    # --- witch hat
    brim = add_cylinder("hat_brim", 0.40, 0.035, (0, 0.0, 1.265), vertices=40)
    for vert in brim.data.vertices:
        r = math.hypot(vert.co.x, vert.co.y - 0.0)
        vert.co.z += (r / 0.4) ** 2 * 0.07 + math.sin(math.atan2(vert.co.y, vert.co.x) * 3.0) * 0.02 * (r / 0.4)
    p.append(assign(brim, "Head", mats["hat"]))
    p.append(assign(add_cylinder("hat_crown", 0.25, 0.30, (0, 0.0, 1.40), vertices=32, radius2=0.12), "Head", mats["hat"]))
    p.append(assign(add_torus("hat_band", 0.235, 0.028, (0, 0.0, 1.30), major_segments=36, minor_segments=10), "Head", mats["gold"]))
    p.append(assign(add_ico("hat_buckle", 0.05, (0, -0.235, 1.31), scale=(1, 0.5, 1.3), subdiv=2), "Head", mats["crystal"]))
    tip = add_cylinder("hat_tip", 0.12, 0.34, (0, 0.05, 1.66), vertices=24, radius2=0.012, rot=(0.55, 0, 0))
    p.append(assign(tip, ["Head", "HatTip"], mats["hat"], sharpness=3.0))

    # --- neck, torso, cape, skirt
    p.append(assign(add_cylinder("neck", 0.065, 0.12, (0, 0, 0.80), vertices=16), ["Chest", "Neck"], mats["skin"]))
    p.append(assign(add_sphere("bodice", 0.205, (0, 0, 0.66), scale=(0.95, 0.72, 1.05), seg=28, rings=16), ["Spine", "Chest"], mats["robe"]))
    p.append(assign(add_torus("collar", 0.11, 0.035, (0, 0, 0.79), major_segments=28, minor_segments=10), "Chest", mats["robe_light"]))
    p.append(assign(add_ico("brooch", 0.045, (0, -0.16, 0.74), scale=(1, 0.6, 1), subdiv=2), "Chest", mats["gold"]))
    cape = add_cylinder("cape", 0.36, 0.44, (0, 0.17, 0.52), vertices=28, radius2=0.19, rot=(0.28, 0, 0))
    carve(cape, lambda co: co.y < 0.08)
    p.append(assign(cape, ["Chest", "Cape"], mats["cape_inner"], sharpness=3.0))
    cape_outer = add_cylinder("cape_outer", 0.375, 0.44, (0, 0.185, 0.525), vertices=28, radius2=0.2, rot=(0.28, 0, 0))
    carve(cape_outer, lambda co: co.y < 0.095)
    p.append(assign(cape_outer, ["Chest", "Cape"], mats["hat"], sharpness=3.0))
    p.append(assign(add_cylinder("skirt", 0.36, 0.28, (0, 0, 0.42), vertices=32, radius2=0.19), ["Hips", "Spine"], mats["robe"], sharpness=3.0))
    p.append(assign(add_torus("hem", 0.345, 0.025, (0, 0, 0.29), major_segments=36, minor_segments=10), "Hips", mats["gold"]))
    p.append(assign(add_torus("belt", 0.205, 0.028, (0, 0, 0.555), major_segments=32, minor_segments=10), "Hips", mats["gold"]))

    # --- arms
    for side, prefix in ((-1, "L"), (1, "R")):
        s = side
        p.append(assign(add_sphere(f"shoulder{prefix}", 0.075, (0.20 * s, 0, 0.75), seg=20, rings=12), f"Shoulder.{prefix}", mats["robe_light"]))
        p.append(assign(add_tube(f"upperarm{prefix}", (0.20 * s, 0, 0.75), (0.27 * s, -0.02, 0.57), 0.058, vertices=16, radius2=0.062), f"UpperArm.{prefix}", mats["robe"]))
        p.append(assign(add_sphere(f"elbow{prefix}", 0.062, (0.27 * s, -0.02, 0.57), seg=18, rings=10), [f"UpperArm.{prefix}", f"Forearm.{prefix}"], mats["robe"]))
        p.append(assign(add_tube(f"sleeve{prefix}", (0.27 * s, -0.02, 0.57), (0.30 * s, -0.08, 0.44), 0.07, vertices=16, radius2=0.095), f"Forearm.{prefix}", mats["robe"]))
        p.append(assign(add_torus(f"cuff{prefix}", 0.09, 0.016, (0.30 * s, -0.08, 0.44), rot=(0.4, 0.2 * s, 0), major_segments=24, minor_segments=8), f"Forearm.{prefix}", mats["gold"]))
        p.append(assign(add_sphere(f"hand{prefix}", 0.055, (0.315 * s, -0.11, 0.37), scale=(1, 1, 1.15), seg=18, rings=10), f"Hand.{prefix}", mats["skin"]))

    # --- staff (held in the right hand)
    p.append(assign(add_tube("staff", (0.33, -0.13, 0.02), (0.33, -0.13, 1.30), 0.022, vertices=12, radius2=0.03), "Staff", mats["staff_wood"]))
    p.append(assign(add_torus("staff_grip", 0.03, 0.012, (0.33, -0.13, 0.33), major_segments=16, minor_segments=8), "Staff", mats["gold"]))
    p.append(assign(add_torus("staff_ring", 0.11, 0.018, (0.33, -0.13, 1.40), rot=(math.pi / 2, 0, 0), major_segments=28, minor_segments=10), "Staff", mats["gold"]))
    for angle in (0.0, 2.1, 4.2):
        p.append(assign(add_cone(f"staff_prong{angle}", 0.02, 0.1, (0.33, -0.13 + math.sin(angle) * 0.05, 1.31 + math.cos(angle) * 0.02), rot=(angle, 0, 0), vertices=8), "Staff", mats["gold"]))
    p.append(assign(add_ico("staff_crystal", 0.075, (0.33, -0.13, 1.40), scale=(0.75, 0.75, 1.35), subdiv=2), "StaffTip", mats["crystal"]))

    # --- legs and boots
    for side, prefix in ((-1, "L"), (1, "R")):
        s = side
        p.append(assign(add_tube(f"thigh{prefix}", (0.09 * s, 0, 0.47), (0.095 * s, 0, 0.26), 0.062, vertices=16, radius2=0.055), f"UpLeg.{prefix}", mats["skin"]))
        p.append(assign(add_sphere(f"knee{prefix}", 0.056, (0.095 * s, 0, 0.26), seg=16, rings=10), [f"UpLeg.{prefix}", f"Leg.{prefix}"], mats["skin"]))
        p.append(assign(add_tube(f"shin{prefix}", (0.095 * s, 0, 0.26), (0.10 * s, 0, 0.10), 0.054, vertices=16, radius2=0.062), f"Leg.{prefix}", mats["boot"]))
        p.append(assign(add_torus(f"boot_cuff{prefix}", 0.064, 0.016, (0.10 * s, 0, 0.19), major_segments=20, minor_segments=8), f"Leg.{prefix}", mats["gold"]))
        p.append(assign(add_sphere(f"boot{prefix}", 0.07, (0.10 * s, -0.035, 0.065), scale=(1.0, 1.6, 0.9), seg=18, rings=10), f"Foot.{prefix}", mats["boot"]))

    mesh = join(p, "CHR_Hero_Main")
    bind(mesh, arm)

    def sway(frame, frames, speed=1.0, offset=0.0):
        return math.sin(((frame - 1) / frames * math.tau + offset) * speed)

    def hair_follow(amount, lag=0.0):
        for side in ("L", "R"):
            bone(arm, f"Hair.{side}").rotation_euler.x += amount
            bone(arm, f"Hair.{side}.2").rotation_euler.x += amount * 0.8 + lag

    def staff_world_pitch(target):
        """Rotate the staff inside the hand so its world pitch (positive = crystal forward) ignores the arm swing.

        Every bone in the chain has local X aligned with world X, so pitches add up.
        """
        total = 0.0
        for name in ("Hips", "Spine", "Chest", "Shoulder.R", "UpperArm.R", "Forearm.R", "Hand.R"):
            total += bone(arm, name).rotation_euler.x
        bone(arm, "Staff").rotation_euler.x = target - total

    def idle(frame, frames):
        phase = (frame - 1) / frames * math.tau
        breathe = math.sin(phase)
        bone(arm, "Hips").location.y = breathe * 0.012
        bone(arm, "Spine").rotation_euler.x = breathe * 0.03
        bone(arm, "Chest").rotation_euler.x = breathe * 0.035
        bone(arm, "Head").rotation_euler.z = math.sin(phase * 0.5) * 0.06
        bone(arm, "Head").rotation_euler.x = -0.03 + math.sin(phase + 0.6) * 0.025
        bone(arm, "HatTip").rotation_euler.x = math.sin(phase + 1.0) * 0.12
        bone(arm, "HatTip").rotation_euler.z = math.cos(phase * 0.7) * 0.08
        hair_follow(math.sin(phase + 0.8) * 0.06, math.cos(phase) * 0.04)
        bone(arm, "Cape").rotation_euler.x = math.sin(phase + 0.3) * 0.05
        bone(arm, "UpperArm.L").rotation_euler.z = 0.22 + breathe * 0.04
        bone(arm, "UpperArm.L").rotation_euler.x = 0.15 + breathe * 0.03
        bone(arm, "Forearm.L").rotation_euler.x = -0.35
        bone(arm, "UpperArm.R").rotation_euler.z = -0.18 - breathe * 0.03
        bone(arm, "UpperArm.R").rotation_euler.x = 0.05
        bone(arm, "Forearm.R").rotation_euler.x = -0.55
        staff_world_pitch(-0.12 + breathe * 0.02)

    def run(frame, frames):
        phase = (frame - 1) / frames * math.tau
        swing = math.sin(phase)
        bounce = abs(math.cos(phase))
        bone(arm, "Hips").location.y = bounce * 0.035 - 0.01
        bone(arm, "Hips").rotation_euler.y = swing * 0.08
        bone(arm, "Spine").rotation_euler.x = 0.14
        bone(arm, "Chest").rotation_euler.x = 0.10 + math.sin(phase * 2) * 0.03
        bone(arm, "Chest").rotation_euler.y = -swing * 0.1
        bone(arm, "Head").rotation_euler.x = -0.12
        bone(arm, "HatTip").rotation_euler.x = 0.35 + math.sin(phase * 2 + 1) * 0.15
        hair_follow(0.45 + math.sin(phase * 2 + 0.5) * 0.18, 0.1)
        bone(arm, "Cape").rotation_euler.x = 0.55 + math.sin(phase * 2) * 0.12
        for side, offset in (("L", 0.0), ("R", math.pi)):
            leg = math.sin(phase + offset)
            bone(arm, f"UpLeg.{side}").rotation_euler.x = -leg * 0.85
            # The knee bends most mid-swing, before the thigh reaches its forward peak.
            bone(arm, f"Leg.{side}").rotation_euler.x = max(0.0, math.sin(phase + offset + 1.2)) * 1.0 + 0.1
        bone(arm, "UpperArm.L").rotation_euler.x = swing * 0.9
        bone(arm, "UpperArm.L").rotation_euler.z = 0.25
        bone(arm, "Forearm.L").rotation_euler.x = -0.8
        bone(arm, "UpperArm.R").rotation_euler.x = -0.35 - swing * 0.25
        bone(arm, "UpperArm.R").rotation_euler.z = -0.2
        bone(arm, "Forearm.R").rotation_euler.x = -0.9
        staff_world_pitch(0.35 + math.sin(phase * 2) * 0.05)

    def attack(frame, frames):
        t = (frame - 1) / (frames - 1)
        wind = curve(t, [(0.0, 0.0), (0.25, 1.0), (0.45, 0.0)])
        thrust = curve(t, [(0.25, 0.0), (0.42, 1.0), (0.72, 1.0), (1.0, 0.0)])
        bone(arm, "Hips").rotation_euler.y = -0.25 * wind + 0.35 * thrust
        bone(arm, "Spine").rotation_euler.x = -0.18 * wind + 0.28 * thrust
        bone(arm, "Chest").rotation_euler.x = -0.12 * wind + 0.18 * thrust
        bone(arm, "Chest").rotation_euler.y = -0.2 * wind + 0.25 * thrust
        bone(arm, "Head").rotation_euler.x = 0.1 * wind - 0.12 * thrust
        bone(arm, "HatTip").rotation_euler.x = -0.3 * wind + 0.5 * thrust
        hair_follow(-0.25 * wind + 0.4 * thrust)
        bone(arm, "Cape").rotation_euler.x = -0.1 * wind + 0.45 * thrust
        bone(arm, "UpperArm.R").rotation_euler.x = 0.7 * wind - 1.65 * thrust
        bone(arm, "UpperArm.R").rotation_euler.z = -0.25 - 0.15 * thrust
        bone(arm, "Forearm.R").rotation_euler.x = -0.6 + 0.2 * wind
        bone(arm, "UpperArm.L").rotation_euler.x = -0.4 * wind + 0.6 * thrust
        bone(arm, "UpperArm.L").rotation_euler.z = 0.45 * wind + 0.2
        bone(arm, "Forearm.L").rotation_euler.x = -0.5 - 0.3 * thrust
        staff_world_pitch(-0.5 * wind + 1.05 * thrust)
        glow = pulse(t, 0.38, 0.75)
        bone(arm, "StaffTip").scale = (1 + glow * 0.5, 1 + glow * 0.5, 1 + glow * 0.5)
        bone(arm, "UpLeg.L").rotation_euler.x = -0.25 * thrust
        bone(arm, "UpLeg.R").rotation_euler.x = 0.3 * thrust
        bone(arm, "Leg.R").rotation_euler.x = 0.3 * thrust

    def dash(frame, frames):
        t = (frame - 1) / (frames - 1)
        lean = curve(t, [(0.0, 0.0), (0.25, 1.0), (0.7, 1.0), (1.0, 0.0)])
        bone(arm, "Spine").rotation_euler.x = 0.55 * lean
        bone(arm, "Chest").rotation_euler.x = 0.2 * lean
        bone(arm, "Head").rotation_euler.x = -0.35 * lean
        bone(arm, "HatTip").rotation_euler.x = 0.9 * lean
        hair_follow(1.0 * lean, 0.2 * lean)
        bone(arm, "Cape").rotation_euler.x = 1.1 * lean
        for side in ("L", "R"):
            bone(arm, f"UpperArm.{side}").rotation_euler.x = 0.9 * lean
            bone(arm, f"Forearm.{side}").rotation_euler.x = -0.3 * lean
        bone(arm, "UpLeg.L").rotation_euler.x = -0.7 * lean
        bone(arm, "Leg.L").rotation_euler.x = 0.9 * lean
        bone(arm, "UpLeg.R").rotation_euler.x = 0.5 * lean
        bone(arm, "Leg.R").rotation_euler.x = 0.4 * lean
        staff_world_pitch(0.9 * lean)

    def hit(frame, frames):
        t = (frame - 1) / (frames - 1)
        kick = curve(t, [(0.0, 0.0), (0.2, 1.0), (1.0, 0.0)])
        bone(arm, "Hips").location.y = 0.03 * kick
        bone(arm, "Spine").rotation_euler.x = -0.4 * kick
        bone(arm, "Chest").rotation_euler.x = -0.2 * kick
        bone(arm, "Head").rotation_euler.x = -0.35 * kick
        bone(arm, "Head").rotation_euler.z = 0.15 * kick
        bone(arm, "HatTip").rotation_euler.x = -0.6 * kick
        hair_follow(-0.6 * kick)
        bone(arm, "Cape").rotation_euler.x = -0.3 * kick
        for side, sign in (("L", 1), ("R", -1)):
            bone(arm, f"UpperArm.{side}").rotation_euler.x = -0.7 * kick
            bone(arm, f"UpperArm.{side}").rotation_euler.z = 0.5 * kick * sign
            bone(arm, f"Forearm.{side}").rotation_euler.x = -0.4 * kick
        bone(arm, "UpLeg.L").rotation_euler.x = -0.3 * kick
        bone(arm, "Leg.L").rotation_euler.x = 0.3 * kick
        staff_world_pitch(-0.6 * kick)

    def death(frame, frames):
        t = (frame - 1) / (frames - 1)
        kneel = curve(t, [(0.0, 0.0), (0.4, 1.0), (1.0, 1.0)])
        fall = curve(t, [(0.35, 0.0), (1.0, 1.0)])
        bone(arm, "Hips").location.y = -0.22 * kneel - 0.18 * fall
        bone(arm, "Hips").rotation_euler.x = 0.2 * kneel - 1.5 * fall
        bone(arm, "Spine").rotation_euler.x = 0.35 * kneel - 0.1 * fall
        bone(arm, "Chest").rotation_euler.x = 0.25 * kneel
        bone(arm, "Head").rotation_euler.x = 0.4 * kneel - 0.3 * fall
        bone(arm, "Head").rotation_euler.z = 0.3 * fall
        bone(arm, "HatTip").rotation_euler.x = -0.7 * fall
        hair_follow(-0.4 * fall)
        for side in ("L", "R"):
            bone(arm, f"UpLeg.{side}").rotation_euler.x = -1.3 * kneel + 0.4 * fall
            bone(arm, f"Leg.{side}").rotation_euler.x = 2.0 * kneel - 0.5 * fall
            bone(arm, f"UpperArm.{side}").rotation_euler.x = -0.3 * kneel + 0.6 * fall
            bone(arm, f"UpperArm.{side}").rotation_euler.z = (0.6 if side == "L" else -0.6) * fall
        bone(arm, "Staff").rotation_euler.x = 1.3 * fall
        bone(arm, "Staff").rotation_euler.z = -0.8 * fall

    actions = [
        add_action(arm, "Idle", 48, idle),
        add_action(arm, "Run", 20, run),
        add_action(arm, "Attack", 14, attack),
        add_action(arm, "Dash", 10, dash),
        add_action(arm, "Hit", 10, hit),
        add_action(arm, "Death", 30, death),
    ]
    return arm, mesh, actions


# ---------------------------------------------------------------- slimes


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
    elif kind == "king":
        p.append(assign(add_torus(f"{name}_crown", radius * 0.5, radius * 0.07, (0, 0, top - radius * 0.02), major_segments=32, minor_segments=10), "Top", mats["gold"]))
        for index in range(6):
            angle = index / 6 * math.tau
            p.append(assign(add_cone(f"{name}_spike{index}", radius * 0.1, radius * 0.38, (math.cos(angle) * radius * 0.48, math.sin(angle) * radius * 0.48, top + radius * 0.16), vertices=8), "Top", mats["gold"]))
            if index % 2 == 0:
                p.append(assign(add_ico(f"{name}_crowngem{index}", radius * 0.07, (math.cos(angle) * radius * 0.5, math.sin(angle) * radius * 0.5, top + radius * 0.02), subdiv=1), "Top", mats["gem"]))
        p.append(assign(add_ico(f"{name}_gem", radius * 0.17, (0, surface_y(0, center_z + radius * 0.55) + radius * 0.02, center_z + radius * 0.55), scale=(1, 0.6, 1.3), subdiv=2), "Body", mats["gem"]))
        cape = add_cylinder(f"{name}_cape", radius * 1.15, radius * 1.5, (0, radius * 0.35, center_z - radius * 0.05), vertices=28, radius2=radius * 0.6, rot=(0.25, 0, 0))
        carve(cape, lambda co: co.y < radius * 0.15)
        p.append(assign(cape, ["Body", "Top"], mats["king_cape"], sharpness=3.0))
        for side in (-1, 1):
            p.append(assign(add_cube(f"{name}_brow{side}", radius * 0.3, (radius * 0.3 * side, surface_y(radius * 0.3, eye_z + radius * 0.24) + radius * 0.02, eye_z + radius * 0.24), scale=(1.1, 0.3, 0.25), rot=(0, 0, 0.45 * side)), "Body", mats["king_cape"]))
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


def produce_character(asset_id, folder, builder, preview_distance, preview_height, target_z, action_sheet=None):
    arm, mesh, _actions = builder()
    if action_sheet:
        render_action_frames(arm, asset_id, target_z, preview_distance, preview_height, action_sheet)
    pose_idle(arm)
    render_preview(os.path.join(PREVIEW, asset_id + ".png"), (0, 0, target_z), preview_distance, preview_height)
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


def main():
    textures = make_textures()
    reset_file()
    mats = {key: material("MAT_" + key, color, emission=EMISSIVE.get(key, 0.0)) for key, color in PALETTE.items()}
    mats["gold"] = material("MAT_gold", PALETTE["gold"], rough=0.3, metallic=0.7)
    mats["crystal"] = material("MAT_crystal", PALETTE["crystal"], emission=1.0, rough=0.15)
    mats["gem"] = material("MAT_gem", PALETTE["gem"], emission=0.9, rough=0.2)
    mats["xp"] = material("MAT_xp", PALETTE["xp"], emission=0.9, rough=0.25)
    mats["ring"] = material("MAT_ring", PALETTE["ring"], emission=0.7, rough=0.35)
    mats["ground"] = material("MAT_Forest_Ground", PALETTE["moss"], rough=0.95, image_path=textures["ground"])
    mats["bark"] = material("MAT_bark", PALETTE["bark"], rough=0.9, image_path=textures["bark"])
    mats["rock_tex"] = material("MAT_rock_tex", PALETTE["rock"], rough=0.85, image_path=textures["rock"])

    produce_character(
        "CHR_Hero_Main",
        "Characters/Hero",
        lambda: build_hero(mats),
        4.2,
        1.5,
        0.85,
        action_sheet=[("Attack", (0.25, 0.45, 0.7)), ("Run", (0.0, 0.25, 0.5)), ("Hit", (0.2,)), ("Death", (1.0,)), ("Dash", (0.5,))],
    )
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

    catalog_path = os.path.join(OUT, "catalog.json")
    with open(catalog_path, "w", encoding="utf-8") as handle:
        json.dump(CATALOG, handle, indent=2)
    print("CATALOG", catalog_path, flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)
