"""Generate original low-poly Slime Breaker assets and export Unity FBX files.

Run:
  blender --background --python asset_work/scripts/build_assets.py -- <out_dir> <preview_dir> <texture_dir>
"""

import json
import math
import os
import subprocess
import sys
import traceback

import bpy
import mathutils

PALETTE = {
    "skin": (0.96, 0.78, 0.66, 1),
    "hair": (0.90, 0.35, 0.22, 1),
    "cloth": (0.12, 0.55, 0.62, 1),
    "cloth_dark": (0.08, 0.28, 0.38, 1),
    "gold": (0.95, 0.76, 0.28, 1),
    "eye": (0.08, 0.09, 0.12, 1),
    "eye_white": (0.95, 0.96, 0.98, 1),
    "slime": (0.22, 0.82, 0.48, 1),
    "slime_fast": (0.15, 0.78, 1.0, 1),
    "slime_fin": (0.98, 0.84, 0.22, 1),
    "slime_tank": (0.42, 0.30, 0.55, 1),
    "rock": (0.45, 0.47, 0.52, 1),
    "king": (0.82, 0.18, 0.42, 1),
    "gem": (0.45, 0.95, 0.85, 1),
    "bark": (0.38, 0.24, 0.14, 1),
    "leaf": (0.18, 0.55, 0.28, 1),
    "leaf_dark": (0.12, 0.38, 0.22, 1),
    "mushroom": (0.86, 0.35, 0.31, 1),
    "stem": (0.93, 0.88, 0.78, 1),
    "moss": (0.24, 0.48, 0.22, 1),
    "ruin": (0.55, 0.56, 0.58, 1),
    "ring": (0.95, 0.45, 0.18, 1),
    "xp": (0.55, 0.95, 0.35, 1),
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


def sh(cmd):
    print("CMD", " ".join(cmd), flush=True)
    subprocess.check_call(cmd)


def make_textures():
    ground = os.path.join(TEX, "TEX_Forest_Ground.png")
    sh(
        [
            "magick",
            "-size",
            "512x512",
            "plasma:#2f6b32-#c4d98a",
            "-blur",
            "0x1.2",
            "-modulate",
            "95,55,98",
            ground,
        ]
    )
    return ground


def reset_file():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 480
    scene.render.film_transparent = False
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    world = bpy.data.worlds.new("PreviewWorld")
    world.color = (0.62, 0.78, 0.88)
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


def material(name, color, emission=0.0, rough=0.45, image_path=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Specular IOR Level"].default_value = 0.25
    if emission > 0:
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = emission
    if image_path:
        img = bpy.data.images.load(image_path)
        img.pack()
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = img
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def smooth(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True


def apply_xform(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def add_sphere(name, radius, loc, scale=(1, 1, 1), seg=16, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=seg, ring_count=rings, radius=radius, location=loc
    )
    obj = bpy.context.active_object
    obj.scale = scale
    apply_xform(obj)
    obj.name = name
    smooth(obj)
    return obj


def add_ico(name, radius, loc, scale=(1, 1, 1), subdiv=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=loc)
    obj = bpy.context.active_object
    obj.scale = scale
    apply_xform(obj)
    obj.name = name
    smooth(obj)
    return obj


def add_cube(name, size, loc, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_cube_add(size=size, location=loc)
    obj = bpy.context.active_object
    obj.scale = scale
    apply_xform(obj)
    obj.name = name
    return obj


def add_cylinder(name, radius, depth, loc, vertices=8):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth, location=loc
    )
    obj = bpy.context.active_object
    apply_xform(obj)
    obj.name = name
    smooth(obj)
    return obj


def add_cylinder_between(name, a, b, radius, vertices=8):
    a = mathutils.Vector(a)
    b = mathutils.Vector(b)
    direction = b - a
    length = max(direction.length, 0.001)
    mid = (a + b) / 2
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=length, location=mid
    )
    obj = bpy.context.active_object
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = mathutils.Vector((0, 0, 1)).rotation_difference(direction)
    apply_xform(obj)
    obj.name = name
    smooth(obj)
    return obj


def add_cone(name, radius, depth, loc, rot=(0, 0, 0), vertices=8):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices, radius1=radius, depth=depth, location=loc, rotation=rot
    )
    obj = bpy.context.active_object
    apply_xform(obj)
    obj.name = name
    smooth(obj)
    return obj


def add_torus(name, major, minor, loc):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major,
        minor_radius=minor,
        major_segments=28,
        minor_segments=8,
        location=loc,
    )
    obj = bpy.context.active_object
    apply_xform(obj)
    obj.name = name
    smooth(obj)
    return obj


def assign(obj, bone, mat):
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    obj.data.materials.append(mat)
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
    cam.location = (0.15, -distance, height)
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
        tris += len(mesh.data.polygons)
        verts += len(mesh.data.vertices)
    CATALOG.append(
        {
            "id": asset_id,
            "file": filename,
            "kind": kind,
            "polygons": tris,
            "vertices": verts,
            "notes": notes,
        }
    )
    print(f"ASSET {asset_id} polys={tris} verts={verts} -> {filename}", flush=True)


def bone(pose, name):
    return pose.pose.bones[name]


def build_hero(mats):
    parts = []
    parts.append(assign(add_sphere("head", 0.27, (0, 0, 1.12), seg=18, rings=12), "Head", mats["skin"]))
    parts.append(assign(add_sphere("hair_top", 0.18, (0, 0.02, 1.36), scale=(1.1, 1.0, 0.7)), "Head", mats["hair"]))
    parts.append(assign(add_cone("hair_l", 0.09, 0.28, (-0.16, 0.02, 1.22), rot=(0.4, 0, 0.8)), "Head", mats["hair"]))
    parts.append(assign(add_cone("hair_r", 0.09, 0.28, (0.16, 0.02, 1.22), rot=(0.4, 0, -0.8)), "Head", mats["hair"]))
    parts.append(assign(add_sphere("bang", 0.08, (0, -0.18, 1.28), scale=(1.4, 0.6, 0.7)), "Head", mats["hair"]))
    parts.append(assign(add_sphere("eye_l", 0.055, (-0.09, -0.2, 1.16), scale=(1, 0.45, 1.15)), "Head", mats["eye_white"]))
    parts.append(assign(add_sphere("eye_r", 0.055, (0.09, -0.2, 1.16), scale=(1, 0.45, 1.15)), "Head", mats["eye_white"]))
    parts.append(assign(add_sphere("pupil_l", 0.028, (-0.09, -0.23, 1.15)), "Head", mats["eye"]))
    parts.append(assign(add_sphere("pupil_r", 0.028, (0.09, -0.23, 1.15)), "Head", mats["eye"]))
    parts.append(assign(add_sphere("body", 0.24, (0, 0, 0.7), scale=(0.9, 0.72, 1.05)), "Chest", mats["cloth"]))
    parts.append(assign(add_cone("cloak", 0.26, 0.55, (0, 0.12, 0.58), rot=(0.35, 0, 0)), "Spine", mats["cloth_dark"]))
    parts.append(assign(add_sphere("hip", 0.16, (0, 0, 0.42), scale=(1.1, 0.8, 0.7)), "Hips", mats["cloth_dark"]))
    parts.append(assign(add_cylinder_between("arm_l", (-0.18, 0, 0.86), (-0.34, -0.02, 0.62), 0.055), "Arm.L", mats["skin"]))
    parts.append(assign(add_cylinder_between("arm_r", (0.18, 0, 0.86), (0.34, -0.02, 0.62), 0.055), "Arm.R", mats["skin"]))
    parts.append(assign(add_sphere("hand_l", 0.055, (-0.36, -0.03, 0.56)), "Hand.L", mats["skin"]))
    parts.append(assign(add_sphere("hand_r", 0.055, (0.36, -0.03, 0.56)), "Hand.R", mats["skin"]))
    parts.append(assign(add_cylinder_between("staff", (0.36, -0.05, 0.42), (0.36, -0.28, 1.05), 0.03, vertices=6), "Hand.R", mats["bark"]))
    parts.append(assign(add_sphere("staff_gem", 0.07, (0.36, -0.3, 1.12)), "Hand.R", mats["gem"]))
    parts.append(assign(add_cylinder_between("leg_l", (-0.1, 0, 0.4), (-0.11, 0, 0.16), 0.06), "UpLeg.L", mats["cloth"]))
    parts.append(assign(add_cylinder_between("leg_r", (0.1, 0, 0.4), (0.11, 0, 0.16), 0.06), "UpLeg.R", mats["cloth"]))
    parts.append(assign(add_cylinder_between("shin_l", (-0.11, 0, 0.16), (-0.12, 0.02, 0.02), 0.05), "Leg.L", mats["skin"]))
    parts.append(assign(add_cylinder_between("shin_r", (0.11, 0, 0.16), (0.12, 0.02, 0.02), 0.05), "Leg.R", mats["skin"]))
    parts.append(assign(add_sphere("foot_l", 0.055, (-0.12, -0.04, 0.03), scale=(1, 1.5, 0.6)), "Foot.L", mats["gold"]))
    parts.append(assign(add_sphere("foot_r", 0.055, (0.12, -0.04, 0.03), scale=(1, 1.5, 0.6)), "Foot.R", mats["gold"]))
    mesh = join(parts, "CHR_Hero_Main")
    bones = [
        ("Root", (0, 0, 0), (0, 0, 0.2), None),
        ("Hips", (0, 0, 0.4), (0, 0, 0.55), "Root"),
        ("Spine", (0, 0, 0.55), (0, 0, 0.75), "Hips"),
        ("Chest", (0, 0, 0.75), (0, 0, 0.95), "Spine"),
        ("Head", (0, 0, 0.95), (0, 0, 1.3), "Chest"),
        ("Arm.L", (-0.18, 0, 0.86), (-0.36, -0.02, 0.6), "Chest"),
        ("Hand.L", (-0.36, -0.02, 0.6), (-0.4, -0.05, 0.48), "Arm.L"),
        ("Arm.R", (0.18, 0, 0.86), (0.36, -0.02, 0.6), "Chest"),
        ("Hand.R", (0.36, -0.02, 0.6), (0.4, -0.2, 0.85), "Arm.R"),
        ("UpLeg.L", (-0.1, 0, 0.4), (-0.11, 0, 0.16), "Hips"),
        ("Leg.L", (-0.11, 0, 0.16), (-0.12, 0.02, 0.02), "UpLeg.L"),
        ("Foot.L", (-0.12, 0.02, 0.02), (-0.12, -0.08, 0.0), "Leg.L"),
        ("UpLeg.R", (0.1, 0, 0.4), (0.11, 0, 0.16), "Hips"),
        ("Leg.R", (0.11, 0, 0.16), (0.12, 0.02, 0.02), "UpLeg.R"),
        ("Foot.R", (0.12, 0.02, 0.02), (0.12, -0.08, 0.0), "Leg.R"),
    ]
    arm = new_armature("CHR_Hero_Main", bones)
    bind(mesh, arm)

    def idle(frame, frames):
        phase = (frame - 1) / frames * math.tau
        bone(arm, "Hips").location.y = math.sin(phase) * 0.015
        bone(arm, "Chest").rotation_euler.x = math.sin(phase) * 0.04
        bone(arm, "Head").rotation_euler.z = math.sin(phase * 0.5) * 0.05
        bone(arm, "Arm.L").rotation_euler.z = 0.15 + math.sin(phase) * 0.05
        bone(arm, "Arm.R").rotation_euler.z = -0.15 - math.sin(phase) * 0.05

    def run(frame, frames):
        phase = (frame - 1) / frames * math.tau
        swing = math.sin(phase)
        bone(arm, "Hips").location.y = abs(math.sin(phase * 2)) * 0.03
        bone(arm, "UpLeg.L").rotation_euler.x = swing * 0.8
        bone(arm, "UpLeg.R").rotation_euler.x = -swing * 0.8
        bone(arm, "Leg.L").rotation_euler.x = max(0.0, -swing) * 0.7
        bone(arm, "Leg.R").rotation_euler.x = max(0.0, swing) * 0.7
        bone(arm, "Arm.L").rotation_euler.x = -swing * 0.6
        bone(arm, "Arm.R").rotation_euler.x = swing * 0.6
        bone(arm, "Chest").rotation_euler.x = 0.12

    def attack(frame, frames):
        t = (frame - 1) / (frames - 1)
        swing = math.sin(t * math.pi)
        wind = min(1.0, t / 0.35)
        bone(arm, "Chest").rotation_euler.y = -0.4 * wind + swing * 0.8
        bone(arm, "Arm.R").rotation_euler.x = -1.1 * swing
        bone(arm, "Arm.R").rotation_euler.z = -0.4 + swing * 0.3
        bone(arm, "Hand.R").rotation_euler.x = -0.6 * swing
        bone(arm, "Hips").rotation_euler.y = swing * 0.25

    def dash(frame, frames):
        t = (frame - 1) / (frames - 1)
        lean = math.sin(t * math.pi)
        bone(arm, "Spine").rotation_euler.x = 0.45 * lean
        bone(arm, "Head").rotation_euler.x = -0.2 * lean
        bone(arm, "Arm.L").rotation_euler.x = -0.8 * lean
        bone(arm, "Arm.R").rotation_euler.x = -0.8 * lean

    def hit(frame, frames):
        t = (frame - 1) / (frames - 1)
        kick = math.sin(t * math.pi)
        bone(arm, "Spine").rotation_euler.x = -0.35 * kick
        bone(arm, "Head").rotation_euler.x = 0.25 * kick
        bone(arm, "Hips").location.z = 0.04 * kick

    def death(frame, frames):
        t = (frame - 1) / (frames - 1)
        bone(arm, "Hips").rotation_euler.x = -1.35 * t
        bone(arm, "Chest").rotation_euler.x = -0.3 * t
        bone(arm, "Head").rotation_euler.x = 0.4 * t
        bone(arm, "UpLeg.L").rotation_euler.x = 0.4 * t
        bone(arm, "UpLeg.R").rotation_euler.x = -0.2 * t

    actions = [
        add_action(arm, "Idle", 32, idle),
        add_action(arm, "Run", 24, run),
        add_action(arm, "Attack", 16, attack),
        add_action(arm, "Dash", 10, dash),
        add_action(arm, "Hit", 8, hit),
        add_action(arm, "Death", 22, death),
    ]
    return arm, mesh, actions


def build_slime(name, body_mat, radius, height_scale, kind, mats):
    center_z = radius * height_scale * 0.85
    parts = [
        assign(
            add_ico(name + "_body", radius, (0, 0, center_z), scale=(1.05, 0.95, height_scale), subdiv=2),
            "Body",
            body_mat,
        )
    ]
    eye_z = center_z + radius * 0.15
    eye_y = -radius * 0.72
    parts.append(assign(add_sphere(name + "_eye_l", radius * 0.16, (-radius * 0.28, eye_y, eye_z), scale=(1, 0.45, 1.2), seg=10, rings=8), "Body", mats["eye_white"]))
    parts.append(assign(add_sphere(name + "_eye_r", radius * 0.16, (radius * 0.28, eye_y, eye_z), scale=(1, 0.45, 1.2), seg=10, rings=8), "Body", mats["eye_white"]))
    parts.append(assign(add_sphere(name + "_pupil_l", radius * 0.07, (-radius * 0.28, eye_y - radius * 0.08, eye_z), seg=8, rings=6), "Body", mats["eye"]))
    parts.append(assign(add_sphere(name + "_pupil_r", radius * 0.07, (radius * 0.28, eye_y - radius * 0.08, eye_z), seg=8, rings=6), "Body", mats["eye"]))
    if kind == "fast":
        parts.append(assign(add_cone(name + "_fin_l", radius * 0.18, radius * 0.55, (-radius * 0.95, 0.05, center_z), rot=(0, 1.2, 0.4)), "Body", mats["slime_fin"]))
        parts.append(assign(add_cone(name + "_fin_r", radius * 0.18, radius * 0.55, (radius * 0.95, 0.05, center_z), rot=(0, -1.2, -0.4)), "Body", mats["slime_fin"]))
    elif kind == "tank":
        parts.append(assign(add_cube(name + "_rock_a", radius * 0.45, (0, 0.05, center_z + radius * 0.85), scale=(1.2, 0.8, 0.55)), "Body", mats["rock"]))
        parts.append(assign(add_cube(name + "_rock_b", radius * 0.32, (-radius * 0.55, 0.1, center_z + radius * 0.35), scale=(0.8, 0.7, 0.6)), "Body", mats["rock"]))
        parts.append(assign(add_cube(name + "_rock_c", radius * 0.28, (radius * 0.6, 0.02, center_z + radius * 0.2)), "Body", mats["rock"]))
    elif kind == "king":
        parts.append(assign(add_torus(name + "_crown", radius * 0.55, radius * 0.08, (0, 0, center_z + radius * 0.95)), "Body", mats["gold"]))
        for index, x in enumerate((-0.22, 0.0, 0.22)):
            parts.append(
                assign(
                    add_cone(
                        f"{name}_spike_{index}",
                        radius * 0.1,
                        radius * 0.38,
                        (x * radius, -0.02, center_z + radius * 1.15),
                    ),
                    "Body",
                    mats["gold"],
                )
            )
        parts.append(assign(add_sphere(name + "_gem", radius * 0.16, (0, -radius * 0.55, center_z - radius * 0.05)), "Body", mats["gem"]))
    mesh = join(parts, name)
    top = center_z + radius * height_scale
    bones = [
        ("Root", (0, 0, 0), (0, 0, 0.15), None),
        ("Body", (0, 0, 0.15), (0, 0, top), "Root"),
    ]
    arm = new_armature(name, bones)
    bind(mesh, arm)

    def idle(frame, frames):
        phase = (frame - 1) / frames * math.tau
        body = bone(arm, "Body")
        body.location.y = math.sin(phase) * 0.04
        body.scale = (
            1.0 + math.sin(phase) * 0.06,
            1.0 - math.sin(phase) * 0.04,
            1.0 + math.cos(phase) * 0.05,
        )

    def move(frame, frames):
        phase = (frame - 1) / frames * math.tau
        body = bone(arm, "Body")
        body.location.y = abs(math.sin(phase * 2)) * 0.06
        squash = math.sin(phase * 2)
        body.scale = (1.0 + squash * 0.1, 1.0 + 0.02, 1.0 - squash * 0.08)
        body.rotation_euler.x = math.sin(phase) * 0.08

    def hit(frame, frames):
        t = (frame - 1) / (frames - 1)
        kick = math.sin(t * math.pi)
        body = bone(arm, "Body")
        body.scale = (1.0 + 0.28 * kick, 1.0 + 0.28 * kick, 1.0 - 0.22 * kick)

    def death(frame, frames):
        t = (frame - 1) / (frames - 1)
        body = bone(arm, "Body")
        body.scale = (1.0 + 0.35 * t, 1.0 + 0.35 * t, 1.0 - 0.75 * t)
        body.location.y = -0.05 * t

    def telegraph(frame, frames):
        t = (frame - 1) / (frames - 1)
        pulse = math.sin(t * math.pi)
        body = bone(arm, "Body")
        body.scale = (1.0 + 0.12 * pulse, 1.0 + 0.12 * pulse, 1.0 + 0.18 * pulse)
        body.location.y = 0.12 * pulse

    def slam(frame, frames):
        t = (frame - 1) / (frames - 1)
        body = bone(arm, "Body")
        if t < 0.45:
            lift = t / 0.45
            body.location.y = 0.25 * lift
            body.scale = (0.9, 0.9, 1.15)
        else:
            drop = (t - 0.45) / 0.55
            body.location.y = 0.25 * (1 - drop)
            body.scale = (1.0 + 0.25 * drop, 1.0 + 0.25 * drop, 1.0 - 0.2 * drop)

    clips = [
        add_action(arm, "Idle", 28, idle),
        add_action(arm, "Move", 18, move),
        add_action(arm, "Hit", 8, hit),
        add_action(arm, "Death", 18, death),
    ]
    if kind == "king":
        clips.append(add_action(arm, "Telegraph", 16, telegraph))
        clips.append(add_action(arm, "Slam", 18, slam))
    return arm, mesh, clips


def static_mesh(obj, mat):
    obj.data.materials.append(mat)
    return obj


def build_tree(name, mats, canopy_scale=1.0):
    trunk = static_mesh(add_cylinder(name + "_trunk", 0.16, 1.3, (0, 0, 0.65), vertices=7), mats["bark"])
    leaf_a = static_mesh(add_ico(name + "_leaf_a", 0.55, (0, 0, 1.55), scale=(1.1 * canopy_scale, 1.0, 0.85)), mats["leaf"])
    leaf_b = static_mesh(add_ico(name + "_leaf_b", 0.38, (0.28, -0.05, 1.85), scale=(1, 1, 0.8)), mats["leaf_dark"])
    return join([trunk, leaf_a, leaf_b], name)


def build_rock(name, mats, scale):
    return static_mesh(add_ico(name, 0.45, (0, 0, 0.28 * scale[2]), scale=scale, subdiv=1), mats["rock"])


def build_mushroom(name, mats):
    stem = static_mesh(add_cylinder(name + "_stem", 0.08, 0.28, (0, 0, 0.14), vertices=8), mats["stem"])
    cap = static_mesh(add_sphere(name + "_cap", 0.2, (0, 0, 0.3), scale=(1.3, 1.3, 0.55), seg=12, rings=8), mats["mushroom"])
    return join([stem, cap], name)


def build_bush(name, mats):
    a = static_mesh(add_ico(name + "_a", 0.28, (0, 0, 0.22), subdiv=1), mats["leaf"])
    b = static_mesh(add_ico(name + "_b", 0.18, (0.16, -0.05, 0.2), subdiv=1), mats["leaf_dark"])
    return join([a, b], name)


def build_pillar(name, mats):
    shaft = static_mesh(add_cylinder(name + "_shaft", 0.22, 1.4, (0, 0, 0.7), vertices=8), mats["ruin"])
    cap = static_mesh(add_cube(name + "_cap", 0.7, (0, 0, 1.45), scale=(1, 1, 0.25)), mats["ruin"])
    return join([shaft, cap], name)


def build_ground(mats):
    bpy.ops.mesh.primitive_grid_add(size=30, x_subdivisions=20, y_subdivisions=20, location=(0, 0, 0))
    obj = bpy.context.active_object
    for vert in obj.data.vertices:
        edge = max(abs(vert.co.x), abs(vert.co.y))
        if edge > 10:
            vert.co.z += math.sin(vert.co.x * 0.7) * 0.08 + math.cos(vert.co.y * 0.5) * 0.06
    apply_xform(obj)
    obj.name = "ENV_Forest_Ground"
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


def produce_character(asset_id, folder, builder, preview_distance, preview_height, target_z):
    arm, mesh, _actions = builder()
    pose_idle(arm)
    render_preview(
        os.path.join(PREVIEW, asset_id + ".png"),
        (0, 0, target_z),
        preview_distance,
        preview_height,
    )
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
    ground_path = make_textures()
    reset_file()
    mats = {
        key: material("MAT_" + key, color, emission=0.35 if key in {"slime", "slime_fast", "king", "gem", "xp", "ring"} else 0.0)
        for key, color in PALETTE.items()
    }
    mats["ground"] = material("MAT_Forest_Ground", PALETTE["moss"], rough=0.9, image_path=ground_path)
    mats["gem"] = material("MAT_Gem", PALETTE["gem"], emission=0.8, rough=0.2)
    mats["xp"] = material("MAT_XP", PALETTE["xp"], emission=0.9, rough=0.25)
    mats["ring"] = material("MAT_BossRing", PALETTE["ring"], emission=0.7, rough=0.35)

    produce_character(
        "CHR_Hero_Main",
        "Characters/Hero",
        lambda: build_hero(mats),
        4.6,
        1.6,
        0.75,
    )
    produce_character(
        "ENM_Slime_Normal",
        "Enemies/Slime",
        lambda: build_slime("ENM_Slime_Normal", mats["slime"], 0.48, 0.9, "normal", mats),
        3.2,
        1.2,
        0.4,
    )
    produce_character(
        "ENM_Slime_Fast",
        "Enemies/Slime",
        lambda: build_slime("ENM_Slime_Fast", mats["slime_fast"], 0.36, 0.72, "fast", mats),
        2.8,
        1.05,
        0.3,
    )
    produce_character(
        "ENM_Slime_Tank",
        "Enemies/Slime",
        lambda: build_slime("ENM_Slime_Tank", mats["slime_tank"], 0.62, 1.05, "tank", mats),
        3.8,
        1.45,
        0.55,
    )
    produce_character(
        "BOSS_SlimeKing",
        "Enemies/SlimeKing",
        lambda: build_slime("BOSS_SlimeKing", mats["king"], 0.95, 1.05, "king", mats),
        5.5,
        2.1,
        0.9,
    )

    produce_static("ENV_Forest_Ground", "Environment/ForestArena", build_ground(mats), 18, 12, 0)
    produce_static("ENV_Forest_Tree_A", "Environment/ForestArena", build_tree("ENV_Forest_Tree_A", mats, 1.0), 4.5, 1.8, 0.9)
    produce_static("ENV_Forest_Tree_B", "Environment/ForestArena", build_tree("ENV_Forest_Tree_B", mats, 1.25), 5.0, 2.0, 1.0)
    produce_static("ENV_Forest_Rock_A", "Environment/ForestArena", build_rock("ENV_Forest_Rock_A", mats, (1.3, 1.0, 0.7)), 2.6, 1.2, 0.25)
    produce_static("ENV_Forest_Rock_B", "Environment/ForestArena", build_rock("ENV_Forest_Rock_B", mats, (0.8, 1.1, 0.9)), 2.4, 1.1, 0.25)
    produce_static("ENV_Forest_Mushroom", "Environment/ForestArena", build_mushroom("ENV_Forest_Mushroom", mats), 2.2, 1.0, 0.25)
    produce_static("ENV_Forest_Bush", "Environment/ForestArena", build_bush("ENV_Forest_Bush", mats), 2.2, 1.0, 0.25)
    produce_static("ENV_Forest_RuinPillar", "Environment/ForestArena", build_pillar("ENV_Forest_RuinPillar", mats), 4.2, 1.7, 0.8)
    ring = static_mesh(add_torus("VFX_Boss_Ring", 1.0, 0.08, (0, 0, 0.02)), mats["ring"])
    produce_static("VFX_Boss_Ring", "VFX", ring, 3.4, 2.2, 0.1)
    orb = static_mesh(add_ico("VFX_XP_Orb", 0.18, (0, 0, 0.18), subdiv=1), mats["xp"])
    produce_static("VFX_XP_Orb", "VFX", orb, 1.6, 0.7, 0.18)

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
