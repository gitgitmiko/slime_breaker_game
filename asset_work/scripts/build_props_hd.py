"""HD environment props for Slime Breaker (Blender, CPU).

Run from the repository root:
  blender --background --python asset_work/scripts/build_props_hd.py -- asset_work/output asset_work/previews asset_work/textures [name ...]

Replaces the low-poly props with textured, more realistic ones under the same asset ids, so ArenaGenerator keeps working.
Materials are named MAT_hd_<surface>; ProjectSetup.ConvertMaterials maps each to SlimeBreaker/Lit + its PBR texture set.
Vertex colour: R = baked ambient occlusion, A = wind sway weight (0 at the base). Leaf cards get spherical normals.
"""

import math
import os
import random
import sys

import bmesh
import bpy
import mathutils
from mathutils import Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_assets as ba  # noqa: E402  (parses the same -- out preview textures args)

OUT, PREVIEW, TEX = ba.OUT, ba.PREVIEW, ba.TEX
ONLY = set(a for a in sys.argv[sys.argv.index("--") + 4:]) if "--" in sys.argv else set()

# Preview colours only; Unity swaps in the PBR textures by material name.
SURFACES = {
    "bark": ((0.30, 0.23, 0.16, 1), "TEX_Bark_HD"),
    "leaf": ((0.25, 0.40, 0.12, 1), "TEX_Leaf_Atlas"),
    "grass": ((0.30, 0.45, 0.14, 1), "TEX_Grass_Atlas"),
    "rock": ((0.42, 0.41, 0.40, 1), "TEX_Rock_HD"),
    "stone": ((0.62, 0.59, 0.52, 1), "TEX_Stone_Ruin"),
    "marble": ((0.88, 0.85, 0.80, 1), "TEX_Marble_HD"),
    "gold": ((0.95, 0.75, 0.32, 1), None),
    "obsidian": ((0.05, 0.04, 0.08, 1), "TEX_Obsidian"),
    "glyph": ((0.65, 0.35, 1.0, 1), None),
    "cactus": ((0.22, 0.38, 0.15, 1), "TEX_Cactus_Skin"),
    "flower": ((0.95, 0.75, 0.85, 1), None),
    "mushcap": ((0.75, 0.12, 0.06, 1), "TEX_Mushroom_Cap"),
    "mushstem": ((0.92, 0.89, 0.80, 1), None),
    "gills": ((0.86, 0.80, 0.66, 1), None),
    "ice": ((0.72, 0.86, 0.98, 1), "TEX_Ice_HD"),
    "crystal": ((0.62, 0.40, 0.95, 1), "TEX_Ice_HD"),
    "glow_yellow": ((1.0, 0.85, 0.35, 1), None),
    "basalt": ((0.16, 0.14, 0.13, 1), "TEX_Volcano_Ground"),
    "slate": ((0.30, 0.33, 0.36, 1), "TEX_Storm_Ground"),
    "rune": ((0.30, 0.90, 1.0, 1), None),
}

MATS = {}


def surface(key):
    if key in MATS:
        return MATS[key]
    colour, texture = SURFACES[key]
    path = os.path.join(TEX, texture + ".png") if texture else None
    mat = ba.material("MAT_hd_" + key, colour, rough=0.6)
    if path and os.path.isfile(path):
        # Preview only: wire the albedo (and alpha for cut-out cards) so EEVEE renders look like the game.
        nodes = mat.node_tree.nodes
        bsdf = nodes["Principled BSDF"]
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(path, check_existing=True)
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if key in ("leaf", "grass"):
            mat.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
            mat.blend_method = "CLIP" if hasattr(mat, "blend_method") else mat.blend_method
            if hasattr(mat, "surface_render_method"):
                mat.surface_render_method = "DITHERED"
    MATS[key] = mat
    return mat


# ---------------------------------------------------------------- mesh helpers


def finish_part(obj, key, ao=None, wind=None):
    """Assign the material, paint vertex colours (R = AO, A = wind weight) and return the object."""
    obj.data.materials.clear()
    obj.data.materials.append(surface(key))
    paint(obj, ao or (lambda co: 1.0), wind or (lambda co: 0.0))
    return obj


def paint(obj, ao, wind):
    mesh = obj.data
    attr = mesh.color_attributes.get("Col") or mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            a = max(0.0, min(1.0, ao(co)))
            w = max(0.0, min(1.0, wind(co)))
            attr.data[li].color = (a, 1.0, 1.0, w)


def uv_box(obj, scale=1.0, offset=(0.0, 0.0)):
    """Planar projection per face along its dominant axis (seams hide in organic textures)."""
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv = mesh.uv_layers.active.data
    for poly in mesh.polygons:
        n = poly.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            if ax == 0:
                u, v = co.y, co.z
            elif ax == 1:
                u, v = co.x, co.z
            else:
                u, v = co.x, co.y
            uv[li].uv = (u / scale + offset[0], v / scale + offset[1])


def uv_cylinder(obj, repeats=1.0, v_scale=1.0, center=(0.0, 0.0)):
    """u = angle around Z (× repeats), v = height / v_scale. Fixes the wrap seam per face."""
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv = mesh.uv_layers.active.data
    for poly in mesh.polygons:
        us = []
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            us.append((li, (math.atan2(co.y - center[1], co.x - center[0]) / math.tau + 0.5) * repeats, co.z / v_scale))
        lo = min(u for _li, u, _v in us)
        for li, u, v in us:
            if u - lo > repeats * 0.5:
                u -= repeats
            uv[li].uv = (u, v)


def displace_fractal(obj, amount, freq, seed=0, octaves=4, z_scale=1.0, keep_bottom=None, ridged=0.0):
    mesh = obj.data
    off = Vector((seed * 17.31, seed * 5.77, seed * 11.13))
    normals = [v.normal.copy() for v in mesh.vertices]
    for v, n in zip(mesh.vertices, normals):
        p = Vector((v.co.x * freq, v.co.y * freq, v.co.z * freq * z_scale)) + off
        d = noise.fractal(p, 1.0, 2.0, octaves, noise_basis="PERLIN_ORIGINAL")
        if ridged:
            d = d * (1 - ridged) + (1.0 - abs(noise.noise(p * 2.3, noise_basis="PERLIN_ORIGINAL")) * 2.0 - 0.6) * ridged
        weight = 1.0
        if keep_bottom is not None and v.co.z < keep_bottom:
            weight = max(0.0, v.co.z / max(keep_bottom, 1e-4))
        v.co += n * d * amount * weight
    mesh.update()


def subdivide_simple(obj, levels=2):
    """Subdivide without rounding (blocks stay blocks), so noise displacement has vertices to move."""
    ba.activate(obj)
    mod = obj.modifiers.new("Subsurf", "SUBSURF")
    mod.subdivision_type = "SIMPLE"
    mod.levels = levels
    mod.render_levels = levels
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def flatten_bottom(obj, z=0.0, sink=0.0):
    for v in obj.data.vertices:
        if v.co.z < z:
            v.co.z = z
        v.co.z -= sink
    obj.data.update()


def new_object(name, bm):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def cards(name, specs, cell, atlas_cells=2, spherical=None, normal_up=0.0):
    """Quads for foliage. specs: (center, facing, up, width, height). cell: (col, row) in the atlas (row 0 = top).

    Normals blend between the card's facing, the direction from `spherical` (canopy centre) and +Z.
    """
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    normals = []
    size = 1.0 / atlas_cells
    u0 = cell[0] * size
    v0 = 1.0 - (cell[1] + 1) * size
    pad = size * 0.01
    corners_uv = [(u0 + pad, v0 + pad), (u0 + size - pad, v0 + pad), (u0 + size - pad, v0 + size - pad), (u0 + pad, v0 + size - pad)]
    for center, facing, up, w, h in specs:
        c = Vector(center)
        f = Vector(facing).normalized()
        u = Vector(up).normalized()
        r = u.cross(f).normalized()
        u = f.cross(r).normalized()
        # Card base at the bottom edge so grass/twig sprites grow from `center`.
        quad = [c - r * w * 0.5, c + r * w * 0.5, c + r * w * 0.5 + u * h, c - r * w * 0.5 + u * h]
        verts = [bm.verts.new(p) for p in quad]
        face = bm.faces.new(verts)
        for loop, uvc in zip(face.loops, corners_uv):
            loop[uv_layer].uv = uvc
        for p in quad:
            n = f.copy()
            if spherical is not None:
                n = n * 0.35 + (p - Vector(spherical)).normalized() * 0.65
            if normal_up:
                n = n * (1 - normal_up) + Vector((0, 0, 1)) * normal_up
            normals.append(n.normalized())
    obj = new_object(name, bm)
    mesh = obj.data
    loop_normals = [None] * len(mesh.loops)
    idx = 0
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            loop_normals[li] = normals[idx]
            idx += 1
        poly.use_smooth = True
    mesh.normals_split_custom_set(loop_normals)
    return obj


def export(asset_id, folder, obj, distance, height, target_z):
    ba.render_preview(os.path.join(PREVIEW, asset_id + ".png"), (0, 0, target_z), distance, height)
    filename = os.path.join(folder, asset_id + ".fbx")
    path = os.path.join(OUT, filename)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        colors_type="LINEAR",
        path_mode="STRIP",
        embed_textures=False,
        bake_anim=False,
    )
    ba.record(asset_id, filename, "static", [obj], "HD prop: PBR material names MAT_hd_*, vertex colour R=AO A=wind")
    ba.cleanup_objects()


def join(parts, name):
    obj = ba.join(parts, name)
    bpy.context.view_layer.objects.active = obj
    return obj


def ao_height(lo, hi, floor=0.35):
    return lambda co: floor + (1 - floor) * max(0.0, min(1.0, (co.z - lo) / max(hi - lo, 1e-4)))


# ---------------------------------------------------------------- trees and plants


def branch_tube(name, start, direction, length, r0, r1, rng, bend=0.25, segments=7):
    pts = []
    radii = []
    d = Vector(direction).normalized()
    p = Vector(start)
    for i in range(segments + 1):
        t = i / segments
        pts.append(tuple(p))
        radii.append(r0 + (r1 - r0) * t)
        d = (d + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.2, 0.6))) * bend * 0.3).normalized()
        p = p + d * (length / segments)
    tube = ba.loft(name, pts, radii, segments=8)
    return tube, Vector(pts[-1]), d


def broadleaf_tree(name, seed, height=4.2):
    rng = random.Random(seed)
    parts = []
    trunk_h = height * 0.55
    pts, radii = [], []
    lean = Vector((rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), 0))
    for i in range(10):
        t = i / 9
        pts.append(tuple(Vector((0, 0, t * trunk_h)) + lean * t * t))
        radii.append(0.26 * (1 - t) ** 0.6 + 0.12 + (0.12 if t < 0.08 else 0.0))
    trunk = ba.loft(name + "_trunk", pts, radii, segments=14, cap_start=False)
    displace_fractal(trunk, 0.03, 6.0, seed, z_scale=0.25)
    uv_box(trunk, 0.9)
    parts.append(finish_part(trunk, "bark", ao_height(0.0, 1.2, 0.4), lambda co: max(0, co.z - trunk_h * 0.7) * 0.1))
    for k in range(6):
        a = k / 6 * math.tau + rng.uniform(-0.3, 0.3)
        root = ba.path_tube(f"{name}_root{k}", [(0, 0, 0.45), (math.cos(a) * 0.32, math.sin(a) * 0.32, 0.12), (math.cos(a) * 0.7, math.sin(a) * 0.7, -0.05)],
                            0.11, segments=8, taper=lambda t: 1.0 - 0.75 * t)
        uv_box(root, 0.9)
        parts.append(finish_part(root, "bark", lambda co: 0.45 + co.z * 0.8))
    top = Vector(pts[-1])
    tips = []
    for k in range(6):
        a = k / 6 * math.tau + rng.uniform(-0.4, 0.4)
        start = Vector((0, 0, trunk_h * rng.uniform(0.62, 0.98))) + lean
        direction = Vector((math.cos(a), math.sin(a), rng.uniform(0.7, 1.3)))
        tube, tip, _d = branch_tube(f"{name}_branch{k}", start, direction, height * rng.uniform(0.28, 0.38), 0.085, 0.025, rng)
        uv_box(tube, 0.6)
        parts.append(finish_part(tube, "bark", ao_height(1.0, height, 0.5), lambda co: 0.25))
        tips.append(tip)
    tips.append(top + Vector((0, 0, height * 0.2)))
    center = Vector((0, 0, height * 0.72)) + lean
    specs = []
    # Ellipsoid shell of leaf sprays gives a full, round crown from every side (and from above).
    for _ in range(170):
        d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1) * 0.8 + 0.25)).normalized()
        shell = rng.uniform(0.75, 1.05)
        c = center + Vector((d.x * 1.55 * shell, d.y * 1.55 * shell, d.z * 1.05 * shell))
        up = (d * 0.6 + Vector((0, 0, 0.8))).normalized()
        facing = (d + Vector((rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), 0.3))).normalized()
        sz = rng.uniform(0.9, 1.3)
        specs.append((tuple(c - up * 0.4 * sz), tuple(facing), tuple(up), 1.0 * sz, 1.0 * sz))
    for tip in tips:
        for _ in range(12):
            off = Vector((rng.gauss(0, 0.55), rng.gauss(0, 0.55), rng.gauss(0, 0.38)))
            c = tip + off
            out = (c - center)
            out.z *= 0.6
            facing = (out.normalized() + Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), rng.uniform(0.0, 0.8)))).normalized()
            up = (out.normalized() * 0.6 + Vector((0, 0, 0.8))).normalized()
            s = rng.uniform(0.85, 1.25)
            specs.append((tuple(c - up * 0.35 * s), tuple(facing), tuple(up), 0.95 * s, 0.95 * s))
    canopy = cards(name + "_leaves", specs, (0, 0), spherical=tuple(center))
    parts.append(finish_part(canopy, "leaf", lambda co: 0.55 + 0.45 * max(0.0, min(1.0, (co.z - center.z + 1.0) / 2.2)),
                             lambda co: 0.35 + 0.65 * max(0.0, min(1.0, (Vector((co.x, co.y, 0)) - Vector((center.x, center.y, 0))).length / 1.8))))
    return join(parts, name)


def conifer_tree(name, seed, height=5.0):
    rng = random.Random(seed)
    parts = []
    pts = [(0, 0, t / 9 * height * 0.96) for t in range(10)]
    radii = [0.2 * (1 - t / 9) + 0.025 for t in range(10)]
    trunk = ba.loft(name + "_trunk", pts, radii, segments=10, cap_start=False)
    displace_fractal(trunk, 0.02, 7.0, seed, z_scale=0.3)
    uv_box(trunk, 0.8)
    parts.append(finish_part(trunk, "bark", ao_height(0.0, 1.5, 0.4)))
    specs = []
    levels = 13
    for lv in range(levels):
        t = lv / (levels - 1)
        z = height * (0.16 + 0.8 * t)
        reach = (1.0 - t) ** 0.9 * 1.6 + 0.3
        arms = max(5, int(12 - t * 6))
        # Two flat needle cards per whorl so the crown reads as solid from the high game camera.
        for k in range(2):
            a = lv * 0.9 + k * math.pi / 2
            out = Vector((math.cos(a), math.sin(a), 0))
            specs.append(((0.0, 0.0, z + 0.05), (0, 0, 1), tuple(out), reach * 1.1, reach * 1.05))
            specs.append(((0.0, 0.0, z + 0.05), (0, 0, 1), tuple(-out), reach * 1.1, reach * 1.05))
        for k in range(arms):
            a = k / arms * math.tau + lv * 0.55 + rng.uniform(-0.2, 0.2)
            out = Vector((math.cos(a), math.sin(a), 0))
            # Drooping branch card: base at the trunk, growing outward and slightly down.
            up = (out + Vector((0, 0, -0.25 + 0.2 * t))).normalized()
            facing = Vector((0, 0, 1)).cross(out).normalized()
            facing = (facing + Vector((0, 0, 0.9))).normalized()
            specs.append(((0.0, 0.0, z), tuple(facing), tuple(up), reach * 0.75, reach))
    for k in range(3):
        a = k / 3 * math.tau
        specs.append(((0, 0, height * 0.86), (math.cos(a), math.sin(a), 0), (0, 0, 1), 0.5, 0.75))
    needles = cards(name + "_needles", specs, (0, 1), spherical=(0, 0, height * 0.35), normal_up=0.25)
    parts.append(finish_part(needles, "leaf", lambda co: 0.5 + 0.5 * co.z / height,
                             lambda co: min(1.0, Vector((co.x, co.y)).length / 1.4)))
    return join(parts, name)


def bush(name, seed):
    rng = random.Random(seed)
    parts = []
    for k in range(5):
        a = k / 5 * math.tau
        stem = ba.path_tube(f"{name}_stem{k}", [(0, 0, 0), (math.cos(a) * 0.15, math.sin(a) * 0.15, 0.3), (math.cos(a) * 0.3, math.sin(a) * 0.3, 0.55)], 0.022, segments=6, taper=lambda t: 1 - 0.6 * t)
        uv_box(stem, 0.5)
        parts.append(finish_part(stem, "bark", ao_height(0, 0.5, 0.4)))
    center = Vector((0, 0, 0.42))
    specs = []
    for _ in range(46):
        d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), abs(rng.gauss(0, 0.8)))).normalized()
        c = center + Vector((d.x * 0.42, d.y * 0.42, d.z * 0.28))
        up = (d + Vector((0, 0, 0.6))).normalized()
        facing = (d + Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 0.5))).normalized()
        s = rng.uniform(0.45, 0.65)
        specs.append((tuple(c - up * 0.25 * s), tuple(facing), tuple(up), s, s))
    leaves = cards(name + "_leaves", specs, (1, 0), spherical=tuple(center))
    parts.append(finish_part(leaves, "leaf", lambda co: 0.5 + 0.5 * min(1.0, co.z / 0.8), lambda co: min(1.0, co.z / 0.7)))
    return join(parts, name)


def tuft(name, seed, cell, width, height, blades=3, key="grass"):
    rng = random.Random(seed)
    specs = []
    for k in range(blades):
        a = k / blades * math.pi + rng.uniform(-0.2, 0.2)
        facing = (math.cos(a), math.sin(a), 0.0)
        specs.append(((rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), -0.02), facing, (rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), 1.0), width, height))
    obj = cards(name, specs, cell, normal_up=0.65)
    return finish_part(obj, key, lambda co: 0.45 + 0.55 * max(0.0, min(1.0, co.z / height)),
                       lambda co: max(0.0, min(1.0, co.z / height)) ** 1.5)


# ---------------------------------------------------------------- stone, rock, ruins


def boulder(name, seed, scale=(1.0, 1.0, 0.7), radius=0.5, key="rock", subdiv=5, decimate=0.22):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=(0, 0, radius * scale[2] * 0.55))
    rock = bpy.context.active_object
    rock.scale = scale
    ba.apply_xform(rock)
    rock.name = name
    # Big soft forms + a few planar breaks; surface detail comes from the normal map, not the mesh.
    displace_fractal(rock, radius * 0.24, 1.1 / radius, seed, octaves=3, ridged=0.18)
    displace_fractal(rock, radius * 0.03, 3.5 / radius, seed + 3, octaves=2)
    flatten_bottom(rock, 0.0, sink=0.04)
    if decimate < 1.0:
        ba.decimate(rock, decimate)
    ba.smooth(rock, math.radians(55))
    uv_box(rock, 1.1)
    zmax = max(v.co.z for v in rock.data.vertices)
    return finish_part(rock, key, ao_height(0.0, zmax * 0.6, 0.35))


def fluted_column(name, height, radius, flutes=20, key="stone", broken=True, seed=0):
    rng = random.Random(seed)
    rings = 14
    pts = [(0, 0, 0.18 + i / (rings - 1) * (height - 0.18)) for i in range(rings)]
    radii = [radius * (1.0 - 0.08 * i / (rings - 1)) for i in range(rings)]
    shaft = ba.loft(name + "_shaft", pts, radii, segments=flutes * 2, wave=lambda th, i: -0.018 * (0.5 + 0.5 * math.cos(th * flutes)), cap_end=not broken)
    if broken:
        for v in shaft.data.vertices:
            cut = height - 0.25 - 0.35 * (0.5 + 0.5 * math.sin(math.atan2(v.co.y, v.co.x) * 2 + seed)) + rng.uniform(-0.05, 0.05)
            if v.co.z > cut:
                v.co.z = cut
    displace_fractal(shaft, 0.012, 9.0, seed + 1)
    uv_cylinder(shaft, repeats=2.0, v_scale=1.2)
    base = ba.bevel(ba.add_cube(name + "_base", 1.0, (0, 0, 0.09), scale=(radius * 2.7, radius * 2.7, 0.18)), 0.025)
    uv_box(base, 0.9)
    parts = [finish_part(shaft, key, ao_height(0.0, 0.8, 0.45)), finish_part(base, key, lambda co: 0.55)]
    for k in range(3):
        a = rng.uniform(0, math.tau)
        chunk = boulder(f"{name}_chunk{k}", seed + 10 + k, scale=(1.0, 0.8, 0.6), radius=0.12, key=key, subdiv=2, decimate=1.0)
        chunk.location = (math.cos(a) * radius * 2.2, math.sin(a) * radius * 2.2, 0)
        ba.apply_xform(chunk)
        parts.append(chunk)
    return parts


def standing_stone(name, seed):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.5))
    stone = bpy.context.active_object
    stone.scale = (0.45, 0.3, 1.0)
    ba.apply_xform(stone)
    ba.subdivide(stone, 3)
    for v in stone.data.vertices:
        taper = 1.0 - 0.35 * max(0.0, v.co.z - 0.2)
        v.co.x *= taper
        v.co.y *= taper
        v.co.z *= 0.95
    displace_fractal(stone, 0.05, 3.5, seed, octaves=5, ridged=0.4)
    flatten_bottom(stone, 0.0, sink=0.03)
    ba.decimate(stone, 0.5)
    ba.smooth(stone, math.radians(50))
    uv_box(stone, 1.0)
    parts = [finish_part(stone, "stone", ao_height(0.0, 0.5, 0.45))]
    glyph = ba.add_torus(name + "_glyph", 0.075, 0.012, (0, -0.135, 0.55), rot=(math.pi / 2, 0, 0), major_segments=20, minor_segments=6)
    parts.append(finish_part(glyph, "rune"))
    return join(parts, name)


def hex_prism(name, radius, height, tip=0.0, segments=6):
    pts = [(0, 0, 0), (0, 0, height)]
    radii = [radius, radius]
    if tip > 0:
        pts.append((0, 0, height + tip))
        radii.append(0.004)
    prism = ba.loft(name, pts, radii, segments=segments)
    ba.smooth(prism, math.radians(30))
    return prism


def crystal_cluster(name, seed, key, count=7, height=1.3, base_key="rock", base_radius=0.35):
    rng = random.Random(seed)
    parts = [boulder(name + "_base", seed, scale=(1.2, 1.1, 0.45), radius=base_radius, key=base_key, subdiv=3)]
    for k in range(count):
        h = height * (1.0 if k == 0 else rng.uniform(0.35, 0.75))
        r = 0.11 * (1.3 if k == 0 else rng.uniform(0.6, 1.0))
        prism = hex_prism(f"{name}_c{k}", r, h, tip=r * 1.6)
        a = rng.uniform(0, math.tau)
        tilt = 0.0 if k == 0 else rng.uniform(0.25, 0.6)
        prism.rotation_euler = (math.cos(a) * tilt, math.sin(a) * tilt, rng.uniform(0, math.tau))
        off = 0.0 if k == 0 else rng.uniform(0.08, 0.22)
        prism.location = (math.cos(a + 1.3) * off, math.sin(a + 1.3) * off, 0.08)
        ba.apply_xform(prism)
        uv_box(prism, 0.5)
        parts.append(finish_part(prism, key, lambda co: 0.7 + 0.3 * min(1.0, co.z / height)))
    return join(parts, name)


# ---------------------------------------------------------------- biome props


def saguaro(name, seed):
    rng = random.Random(seed)
    ribs = 12
    rings = 12
    pts = [(0, 0, i / (rings - 1) * 1.55) for i in range(rings)]
    radii = [0.16 * (1.0 - 0.15 * (i / (rings - 1)) ** 3) for i in range(rings)]
    trunk = ba.loft(name + "_trunk", pts, radii, segments=ribs * 3, wave=lambda th, i: 0.018 * math.cos(th * ribs))
    for v in trunk.data.vertices:
        if v.co.z > 1.45:
            k = (v.co.z - 1.45) / 0.1
            v.co.x *= max(0.2, 1 - k * 0.7)
            v.co.y *= max(0.2, 1 - k * 0.7)
    uv_cylinder(trunk, repeats=1.0, v_scale=0.5)
    parts = [finish_part(trunk, "cactus", ao_height(0.0, 0.6, 0.5))]
    for k, (side, z0, up_len) in enumerate(((1, 0.6, 0.55), (-1, 0.82, 0.4))):
        pts = [(0, 0, z0), (side * 0.22, 0, z0 + 0.02), (side * 0.33, 0, z0 + 0.12), (side * 0.36, 0, z0 + 0.12 + up_len)]
        arm = ba.loft(f"{name}_arm{k}", pts, [0.085, 0.085, 0.082, 0.07], segments=ribs * 2, wave=lambda th, i: 0.012 * math.cos(th * ribs))
        uv_cylinder(arm, repeats=1.0, v_scale=0.5, center=(side * 0.3, 0))
        parts.append(finish_part(arm, "cactus", lambda co: 0.75))
    flower = ba.add_sphere(name + "_flower", 0.05, (0, 0, 1.56), scale=(1.4, 1.4, 0.45), seg=12, rings=6)
    parts.append(finish_part(flower, "flower"))
    return join(parts, name)


def ice_cluster(name, seed):
    return crystal_cluster(name, seed, "ice", count=6, height=1.35, base_key="rock", base_radius=0.3)


def thunder_spire(name, seed):
    rng = random.Random(seed)
    pts = [(rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), i / 9 * 2.3) for i in range(10)]
    radii = [0.34 * (1 - i / 9) ** 1.2 + 0.03 for i in range(10)]
    spire = ba.loft(name + "_spire", pts, radii, segments=9)
    ba.subdivide(spire, 1)
    displace_fractal(spire, 0.07, 3.0, seed, octaves=5, ridged=0.5, z_scale=0.6)
    flatten_bottom(spire, 0.0, sink=0.05)
    ba.smooth(spire, math.radians(40))
    uv_box(spire, 1.0)
    parts = [finish_part(spire, "rock", ao_height(0.0, 1.0, 0.4))]
    for k in range(5):
        prism = hex_prism(f"{name}_shard{k}", 0.05, rng.uniform(0.18, 0.32), tip=0.08)
        a = k / 5 * math.tau + rng.uniform(-0.3, 0.3)
        z = rng.uniform(0.5, 1.6)
        r = (0.34 * (1 - z / 2.3) ** 1.2 + 0.03) * 0.85
        outward = Vector((math.cos(a), math.sin(a), 0.7)).normalized()
        prism.rotation_mode = "QUATERNION"
        prism.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(outward)
        prism.location = (math.cos(a) * r, math.sin(a) * r, z)
        ba.apply_xform(prism)
        prism.rotation_mode = "XYZ"
        uv_box(prism, 0.4)
        parts.append(finish_part(prism, "glow_yellow"))
    return join(parts, name)


def swamp_root(name, seed):
    rng = random.Random(seed)
    pts = [(0, 0, 0), (0.02, 0, 0.45), (-0.02, 0.03, 0.9), (0.04, 0, 1.15)]
    stump = ba.loft(name + "_stump", pts, [0.26, 0.2, 0.16, 0.13], segments=12, cap_start=False)
    displace_fractal(stump, 0.04, 5.0, seed, z_scale=0.3)
    uv_box(stump, 0.8)
    parts = [finish_part(stump, "bark", ao_height(0.0, 0.8, 0.35))]
    for k in range(5):
        a = k / 5 * math.tau + rng.uniform(-0.3, 0.3)
        d = rng.uniform(0.6, 0.95)
        h = rng.uniform(0.35, 0.6)
        root = ba.path_tube(f"{name}_root{k}", [(math.cos(a) * 0.1, math.sin(a) * 0.1, 0.55), (math.cos(a) * d * 0.45, math.sin(a) * d * 0.45, h),
                                                 (math.cos(a) * d * 0.85, math.sin(a) * d * 0.85, h * 0.4), (math.cos(a) * d, math.sin(a) * d, -0.08)],
                            0.08, segments=8, taper=lambda t: 1.0 - 0.6 * t)
        displace_fractal(root, 0.015, 9.0, seed + k)
        uv_box(root, 0.7)
        parts.append(finish_part(root, "bark", lambda co: 0.45 + co.z * 0.9))
    return join(parts, name)


def obelisk(name, seed):
    pts = [(0, 0, 0), (0, 0, 0.2), (0, 0, 0.22), (0, 0, 1.75), (0, 0, 2.05)]
    radii = [0.42, 0.42, 0.3, 0.2, 0.004]
    body = ba.loft(name + "_body", pts, radii, segments=4)
    body.rotation_euler = (0, 0, math.pi / 4)
    ba.apply_xform(body)
    ba.smooth(body, math.radians(30))
    uv_box(body, 0.8)
    parts = [finish_part(body, "obsidian", ao_height(0.0, 0.6, 0.5))]
    rng = random.Random(seed)
    for k in range(4):
        face = k * math.pi / 2
        n = Vector((math.cos(face), math.sin(face), 0))
        for g in range(4):
            z = 0.45 + g * 0.3
            # Faces of the 4-sided loft sit at radius × cos 45° from the axis.
            r = (0.3 - (0.1 * (z - 0.22) / 1.53)) * 0.7071 + 0.006
            glyph = ba.add_cube(f"{name}_g{k}_{g}", 1.0, tuple(n * r + Vector((0, 0, z))), scale=(0.02, 0.02, 0.02))
            glyph.scale = (abs(n.y) * rng.uniform(0.08, 0.16) + 0.012, abs(n.x) * rng.uniform(0.08, 0.16) + 0.012, rng.uniform(0.03, 0.12))
            ba.apply_xform(glyph)
            parts.append(finish_part(glyph, "glyph"))
    return join(parts, name)


def basalt_columns(name, seed):
    rng = random.Random(seed)
    parts = []
    spots = [(0, 0, 1.65), (0.26, 0.05, 1.15), (-0.2, 0.18, 1.3), (0.05, -0.27, 0.85), (-0.24, -0.15, 0.7), (0.2, 0.26, 0.55)]
    for k, (x, y, h) in enumerate(spots):
        prism = hex_prism(f"{name}_col{k}", 0.14, h * rng.uniform(0.92, 1.05))
        for v in prism.data.vertices:
            if v.co.z > h * 0.95:
                v.co.z += (v.co.x * 0.3 + v.co.y * 0.2) * rng.uniform(-1, 1)
        prism.location = (x, y, -0.02)
        prism.rotation_euler = (rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), rng.uniform(0, 1))
        ba.apply_xform(prism)
        uv_box(prism, 1.2)
        parts.append(finish_part(prism, "basalt", ao_height(0.0, 0.9, 0.4)))
    return join(parts, name)


def stone_arch(name, seed):
    parts = []
    for side in (-1, 1):
        post = ba.add_cube(f"{name}_post{side}", 1.0, (side * 0.62, 0, 0.64), scale=(0.3, 0.32, 1.28))
        subdivide_simple(post, 3)
        displace_fractal(post, 0.025, 6.0, seed + side, ridged=0.3)
        ba.smooth(post, math.radians(40))
        uv_box(post, 0.8)
        parts.append(finish_part(post, "stone", ao_height(0.0, 0.8, 0.45)))
    arc = []
    for i in range(13):
        t = i / 12
        a = math.pi * t
        arc.append((-math.cos(a) * 0.62, 0, 1.22 + math.sin(a) * 0.38))
    lintel = ba.loft(name + "_lintel", arc, [0.17] * len(arc), segments=6, ovals=[(1.0, 1.0)] * len(arc))
    displace_fractal(lintel, 0.02, 6.0, seed + 5)
    uv_box(lintel, 0.8)
    parts.append(finish_part(lintel, "stone", lambda co: 0.75))
    return join(parts, name)


def royal_column(name, seed):
    parts = []
    shaft_parts = fluted_column(name, 1.85, 0.2, flutes=16, key="marble", broken=False, seed=seed)
    parts.extend(shaft_parts[:2])
    capital = ba.loft(name + "_capital", [(0, 0, 1.85), (0, 0, 1.93), (0, 0, 2.0)], [0.2, 0.27, 0.33], segments=24)
    uv_box(capital, 0.6)
    parts.append(finish_part(capital, "marble", lambda co: 0.8))
    abacus = ba.bevel(ba.add_cube(name + "_abacus", 1.0, (0, 0, 2.04), scale=(0.72, 0.72, 0.08)), 0.015)
    uv_box(abacus, 0.6)
    parts.append(finish_part(abacus, "marble", lambda co: 0.85))
    for z in (0.32, 1.78):
        ring = ba.add_torus(f"{name}_ring{z}", 0.205, 0.018, (0, 0, z), major_segments=28, minor_segments=8)
        parts.append(finish_part(ring, "gold"))
    return join(parts, name)


def mushroom_cluster(name, seed):
    rng = random.Random(seed)
    parts = []
    for k, (x, y, s) in enumerate(((0, 0, 1.0), (0.2, 0.1, 0.68), (-0.14, 0.16, 0.5))):
        h = 0.32 * s
        r = 0.2 * s
        stem = ba.loft(f"{name}_stem{k}", [(x, y, 0), (x, y, h * 0.5), (x, y, h)], [0.05 * s, 0.04 * s, 0.035 * s], segments=12, cap_start=False)
        uv_box(stem, 0.3)
        parts.append(finish_part(stem, "mushstem", ao_height(0.0, h, 0.5)))
        ring = ba.add_torus(f"{name}_annulus{k}", 0.045 * s, 0.01 * s, (x, y, h * 0.7), major_segments=14, minor_segments=6)
        parts.append(finish_part(ring, "mushstem"))
        profile = [(x, y, h - 0.02 * s), (x, y, h + 0.02 * s), (x, y, h + 0.07 * s), (x, y, h + 0.11 * s), (x, y, h + 0.135 * s)]
        cap = ba.loft(f"{name}_cap{k}", profile, [r, r * 0.98, r * 0.82, r * 0.5, 0.01], segments=20, cap_start=False)
        uv_box(cap, 0.25)
        parts.append(finish_part(cap, "mushcap", lambda co: 0.95))
        gills = ba.add_cylinder(f"{name}_gills{k}", r * 0.97, 0.01, (x, y, h - 0.018 * s), vertices=20, radius2=0.04 * s)
        parts.append(finish_part(gills, "gills", lambda co: 0.6))
    return join(parts, name)


# ---------------------------------------------------------------- production


def lighting():
    return ba.hero_studio()


PROPS = [
    ("ENV_Forest_Tree_A", "Environment/ForestArena", lambda: broadleaf_tree("ENV_Forest_Tree_A", 11, 4.2), 9.0, 4.0, 2.2),
    ("ENV_Forest_Tree_B", "Environment/ForestArena", lambda: conifer_tree("ENV_Forest_Tree_B", 21, 5.0), 10.0, 4.5, 2.4),
    ("ENV_Forest_Bush", "Environment/ForestArena", lambda: bush("ENV_Forest_Bush", 31), 2.4, 1.2, 0.35),
    ("ENV_Forest_Grass", "Environment/ForestArena", lambda: tuft("ENV_Forest_Grass", 41, (0, 0), 0.62, 0.48), 1.6, 0.8, 0.2),
    ("ENV_Forest_Flower", "Environment/ForestArena", lambda: tuft("ENV_Forest_Flower", 51, (1, 1), 0.6, 0.5), 1.6, 0.8, 0.2),
    ("ENV_Forest_Rock_A", "Environment/ForestArena", lambda: boulder("ENV_Forest_Rock_A", 61, scale=(1.35, 1.0, 0.72), radius=0.48), 2.8, 1.3, 0.25),
    ("ENV_Forest_Rock_B", "Environment/ForestArena", lambda: boulder("ENV_Forest_Rock_B", 67, scale=(0.85, 1.1, 0.95), radius=0.45), 2.6, 1.2, 0.3),
    ("ENV_Forest_Mushroom", "Environment/ForestArena", lambda: mushroom_cluster("ENV_Forest_Mushroom", 71), 1.6, 0.8, 0.2),
    ("ENV_Forest_RuinPillar", "Environment/ForestArena", lambda: join(fluted_column("ENV_Forest_RuinPillar", 1.85, 0.24, seed=81), "ENV_Forest_RuinPillar"), 4.6, 2.0, 0.9),
    ("ENV_Forest_BoundaryStone", "Environment/ForestArena", lambda: standing_stone("ENV_Forest_BoundaryStone", 91), 2.6, 1.2, 0.45),
    ("ENV_Desert_Cactus", "Environment/Biomes", lambda: saguaro("ENV_Desert_Cactus", 101), 3.6, 1.5, 0.75),
    ("ENV_Snow_IceSpike", "Environment/Biomes", lambda: ice_cluster("ENV_Snow_IceSpike", 111), 3.4, 1.5, 0.6),
    ("ENV_Thunder_Spire", "Environment/Biomes", lambda: thunder_spire("ENV_Thunder_Spire", 121), 4.6, 2.0, 1.0),
    ("ENV_Swamp_Root", "Environment/Biomes", lambda: swamp_root("ENV_Swamp_Root", 131), 3.6, 1.5, 0.5),
    ("ENV_Swamp_Reed", "Environment/Biomes", lambda: tuft("ENV_Swamp_Reed", 141, (0, 1), 0.55, 0.95, blades=4), 2.2, 1.0, 0.4),
    ("ENV_Shadow_Obelisk", "Environment/Biomes", lambda: obelisk("ENV_Shadow_Obelisk", 151), 4.8, 2.2, 1.0),
    ("ENV_Volcano_Rock", "Environment/Biomes", lambda: boulder("ENV_Volcano_Rock", 161, scale=(1.25, 1.0, 0.8), radius=0.5, key="basalt"), 2.8, 1.2, 0.3),
    ("ENV_Volcano_Pillar", "Environment/Biomes", lambda: basalt_columns("ENV_Volcano_Pillar", 171), 4.2, 1.9, 0.8),
    ("ENV_Storm_Arch", "Environment/Biomes", lambda: stone_arch("ENV_Storm_Arch", 181), 4.0, 1.8, 0.8),
    ("ENV_Crystal_Spire", "Environment/Biomes", lambda: crystal_cluster("ENV_Crystal_Spire", 191, "crystal"), 4.0, 1.8, 0.7),
    ("ENV_Royal_Column", "Environment/Biomes", lambda: royal_column("ENV_Royal_Column", 201), 4.6, 2.0, 1.0),
]


def main():
    ba.reset_file()
    studio = lighting()
    for asset_id, folder, builder, distance, height, target_z in PROPS:
        if ONLY and asset_id not in ONLY:
            continue
        obj = builder()
        export(asset_id, folder, obj, distance, height, target_z)
    ba.hero_studio_off(studio)
    ba.write_catalog(merge=True)
    print("PROPS_HD_OK", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        import traceback

        traceback.print_exc()
        sys.exit(1)
