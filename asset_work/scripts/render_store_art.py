"""Renders transparent character cut-outs for the app icon and Play Store art from the final FBX files.

Read-only for the FBX files (they are imported, never re-exported).

    blender --background --python asset_work/scripts/render_store_art.py -- <repo root> <out dir>

Writes hero_face.png, hero_full.png, boss.png, slime.png (RGBA, 1024 px) into <out dir>.
"""
import math
import os
import sys

import bpy
from mathutils import Vector


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = argv[0] if argv else os.getcwd()
    out = argv[1] if len(argv) > 1 else os.path.join(root, "asset_work", "store", "renders")
    return root, out


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.film_transparent = True
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.72, 0.74, 0.86, 1)
    background.inputs["Strength"].default_value = 0.55
    for name, energy, location, color in (
        ("Key", 2.4, (-2.0, -3.0, 4.0), (1.0, 0.96, 0.9)),
        ("Fill", 0.7, (3.0, -2.0, 1.5), (0.8, 0.86, 1.0)),
        ("Rim", 2.2, (0.5, 3.5, 3.0), (0.95, 0.8, 1.0)),
    ):
        data = bpy.data.lights.new(name, "SUN")
        data.energy = energy
        data.color = color
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = location
        look_at(light, (0, 0, 0.8))
    cam_data = bpy.data.cameras.new("Camera")
    cam = bpy.data.objects.new("Camera", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def import_fbx(path, action_hint=None, frame_fraction=0.0):
    bpy.ops.import_scene.fbx(filepath=path)
    armature = next((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), None)
    if armature is not None and action_hint:
        action = next((a for a in bpy.data.actions if a.name.endswith(action_hint)), None)
        if action is not None:
            if armature.animation_data is None:
                armature.animation_data_create()
            armature.animation_data.action = action
            start, end = action.frame_range
            bpy.context.scene.frame_set(int(start + (end - start) * frame_fraction))
    return armature


def bounds():
    low = Vector((1e9, 1e9, 1e9))
    high = Vector((-1e9, -1e9, -1e9))
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(depsgraph)
        for corner in evaluated.bound_box:
            world = evaluated.matrix_world @ Vector(corner)
            low = Vector(map(min, low, world))
            high = Vector(map(max, high, world))
    return low, high


def shoot(cam, path, target, distance, height, side=0.35, lens=50):
    cam.data.lens = lens
    cam.location = (target[0] + distance * side, target[1] - distance, height)
    look_at(cam, target)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("STORE_RENDER", path, flush=True)


def main():
    root, out = args()
    os.makedirs(out, exist_ok=True)
    art = os.path.join(root, "Assets", "Art")

    cam = reset()
    import_fbx(os.path.join(art, "Characters", "Hero", "CHR_Hero_Main.fbx"), "Idle", 0.0)
    low, high = bounds()
    top = high.z
    shoot(cam, os.path.join(out, "hero_face.png"), (0, 0, top * 0.62), 1.7, top * 0.68, side=0.3)
    shoot(cam, os.path.join(out, "hero_full.png"), (0, 0, top * 0.5), 2.1, top * 0.58, side=0.35)

    cam = reset()
    import_fbx(os.path.join(art, "Enemies", "SlimeKing", "BOSS_SlimeKing.fbx"), "Idle", 0.0)
    low, high = bounds()
    size = max(high.x - low.x, high.z - low.z)
    shoot(cam, os.path.join(out, "boss.png"), (0, 0, high.z * 0.48), size * 2.1, high.z * 0.62, side=-0.3)

    cam = reset()
    import_fbx(os.path.join(art, "Enemies", "Slime", "ENM_Slime_Normal.fbx"), "Idle", 0.0)
    low, high = bounds()
    size = max(high.x - low.x, high.z - low.z)
    shoot(cam, os.path.join(out, "slime.png"), (0, 0, high.z * 0.45), size * 2.2, high.z * 0.8, side=0.2)
    print("STORE_RENDER_OK", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        import traceback
        traceback.print_exc()
        sys.exit(1)
