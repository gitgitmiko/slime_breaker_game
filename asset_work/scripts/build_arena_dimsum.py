"""Dim sum steamer arena for Slime Breaker (Blender, CPU).

Run from the repository root:
  blender --background --python asset_work/scripts/build_arena_dimsum.py -- asset_work/output asset_work/previews asset_work/textures [asset_id ...]

The arena is the inside of a bamboo steamer standing on a glittery table:
  ENV_Dimsum_Floor / _Wall / _Table      the steamer (woven mat floor, pale wood wall, glitter tabletop outside)
  ENV_Dimsum_Lid / _Tier / _ChopLying    giant table decor outside the wall
  ENV_Dimsum_*                           squishy-toy props: jelly mini dumplings with kawaii faces, bamboo, bok choy,
                                         scallions, chopsticks, sauce dish, tea cup, chilies, shiitake, bamboo leaves
Materials are named MAT_hd_<surface>; ProjectSetup maps them to SlimeBreaker/Lit (or Jelly for MAT_hd_jelly).
Vertex colour: R = baked ambient occlusion, A = wind weight (unused here).
"""

import math
import os
import random
import sys

import bmesh
import bpy
import mathutils
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_assets as ba  # noqa: E402
import build_props_hd as bp  # noqa: E402

OUT, PREVIEW, TEX = ba.OUT, ba.PREVIEW, ba.TEX
ONLY = set(a for a in sys.argv[sys.argv.index("--") + 4:]) if "--" in sys.argv else set()
ENV = "Environment/Dimsum"

# Preview colours only; Unity swaps in the real looks by material name.
bp.SURFACES.update({
    "steamwood": ((0.90, 0.76, 0.52, 1), "TEX_Steamer_Wood"),
    "bambooband": ((0.62, 0.44, 0.22, 1), None),
    "weave": ((0.86, 0.68, 0.38, 1), "TEX_Bamboo_Weave"),
    "floor": ((0.86, 0.68, 0.38, 1), None),
    "glittertable": ((0.62, 0.20, 0.26, 1), None),
    "bamboogreen": ((0.42, 0.62, 0.22, 1), None),
    "bamboo_leaf": ((0.30, 0.55, 0.18, 1), "TEX_Bamboo_Leaf_Atlas"),
    "jade": ((0.30, 0.60, 0.24, 1), None),
    "bokstem": ((0.86, 0.93, 0.74, 1), None),
    "scallion_green": ((0.28, 0.62, 0.22, 1), None),
    "scallion_white": ((0.93, 0.95, 0.84, 1), None),
    "porcelain": ((0.97, 0.97, 0.95, 1), None),
    "porcelain_blue": ((0.25, 0.45, 0.85, 1), None),
    "tea": ((0.62, 0.32, 0.08, 1), None),
    "soy": ((0.08, 0.03, 0.02, 1), None),
    "chili_red": ((0.80, 0.07, 0.06, 1), None),
    "chili_stem": ((0.22, 0.45, 0.15, 1), None),
    "shiitake": ((0.34, 0.20, 0.11, 1), None),
    "shiitake_gill": ((0.88, 0.82, 0.68, 1), None),
    "lacquer_red": ((0.62, 0.06, 0.07, 1), None),
    "jelly": ((0.96, 0.74, 0.84, 1), None),
    "face_eye": ((0.02, 0.02, 0.03, 1), None),
    "face_white": ((1.0, 1.0, 1.0, 1), None),
    "face_blush": ((1.0, 0.52, 0.58, 1), None),
    "face_mouth": ((0.72, 0.14, 0.2, 1), None),
})


# ---------------------------------------------------------------- generic geometry


def revolve(name, profile, segments=48, uv_u=1.0, uv_v=1.0, probe=None):
    """Surface of revolution about Z from a (radius, z) profile. Points with radius 0 collapse to the axis.

    UV: u = angle × uv_u, v = path length / uv_v. `probe(normal)` is checked on the highest face to pick the
    winding that points outward; default is "normal.z > 0".
    """
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    rings = []
    for r, z in profile:
        if r < 1e-5:
            axis = bm.verts.new((0.0, 0.0, z))
            rings.append([axis] * segments)
        else:
            rings.append([bm.verts.new((r * math.cos(k / segments * math.tau), r * math.sin(k / segments * math.tau), z)) for k in range(segments)])
    lengths = [0.0]
    for (r0, z0), (r1, z1) in zip(profile, profile[1:]):
        lengths.append(lengths[-1] + math.hypot(r1 - r0, z1 - z0))
    for pi in range(len(rings) - 1):
        a, b = rings[pi], rings[pi + 1]
        for k in range(segments):
            k2 = (k + 1) % segments
            quad = [(a[k], k, pi), (a[k2], k + 1, pi), (b[k2], k + 1, pi + 1), (b[k], k, pi + 1)]
            seen, verts = set(), []
            for v, ku, pj in quad:
                if id(v) in seen:
                    continue
                seen.add(id(v))
                verts.append((v, ku, pj))
            if len(verts) < 3:
                continue
            try:
                face = bm.faces.new([v for v, _ku, _pj in verts])
            except ValueError:
                continue
            for loop, (_v, ku, pj) in zip(face.loops, verts):
                loop[uv_layer].uv = (ku / segments * uv_u, lengths[pj] / uv_v)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    top = max(bm.faces, key=lambda f: f.calc_center_median().z)
    ok = (probe or (lambda n: n.z > 0))(top.normal)
    if not ok:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    ba.smooth(obj, math.radians(70))
    return obj


def part(obj, key, ao=None):
    return bp.finish_part(obj, key, ao)


def sphere(name, radius, loc, scale=(1, 1, 1), seg=20, rings=12, rot=None):
    return ba.add_sphere(name, radius, loc, scale=scale, seg=seg, rings=rings, rot=rot)


def curve_points(fn, count):
    return [fn(i / (count - 1)) for i in range(count)]


def tube(name, points, radii, segments=10, ovals=None, cap_start=True, cap_end=True):
    return ba.loft(name, points, radii, segments=segments, ovals=ovals, cap_start=cap_start, cap_end=cap_end)


# ---------------------------------------------------------------- the steamer


def floor_disc():
    bpy.ops.mesh.primitive_circle_add(vertices=128, radius=25.4, fill_type="TRIFAN", location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = "ENV_Dimsum_Floor"
    mesh = obj.data
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        co = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (co.x / 4.0, co.y / 4.0)
    return part(obj, "floor")


def steamer_wall():
    inner_r, outer_r = 24.5, 26.4
    profile = [(inner_r, 0.0), (inner_r, 1.9), (inner_r + 0.06, 2.22), (inner_r + 0.28, 2.42), (inner_r + 0.7, 2.52), (25.45, 2.55),
               (outer_r - 0.7, 2.52), (outer_r - 0.28, 2.42), (outer_r - 0.04, 2.2), (outer_r, 1.9), (outer_r, 0.0)]
    # Revolved from inside-bottom up and over to outside-bottom: outward normals point away from the wood.
    wall = revolve("wall", profile, segments=192, uv_u=26.0 * math.tau / 3.0, uv_v=3.0, probe=lambda n: n.z > 0)
    parts = [part(wall, "steamwood", lambda co: 0.55 + 0.45 * min(1.0, co.z / 2.0))]
    for z, minor in ((0.45, 0.11), (1.55, 0.1)):
        band = ba.add_torus(f"hoop{z}", outer_r + 0.01, minor, (0, 0, z), major_segments=192, minor_segments=8)
        bp.uv_box(band, 2.0)
        parts.append(part(band, "bambooband"))
    # Rope-wrapped bamboo ties where the steamer's rim ring is joined (a few knots round the outside).
    for k in range(16):
        a = k / 16 * math.tau
        knot = sphere(f"knot{k}", 0.16, (math.cos(a) * (outer_r + 0.06), math.sin(a) * (outer_r + 0.06), 1.0), scale=(1, 1, 1.5), seg=10, rings=8)
        parts.append(part(knot, "bambooband"))
    return bp.join(parts, "ENV_Dimsum_Wall")


def table_ring():
    profile = [(25.9, 0.0), (140.0, 0.0)]
    table = revolve("table", [(25.9, -0.012), (60.0, -0.012), (140.0, -0.012)], segments=160, uv_u=1.0, uv_v=1.0, probe=lambda n: n.z > 0)
    uv = table.data.uv_layers.active
    for loop in table.data.loops:
        co = table.data.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (co.x / 6.0, co.y / 6.0)
    return part(table, "glittertable")


def lid():
    """Giant woven bamboo lid leaning on the table: dome top, wood rim band, bamboo hoop, small knob."""
    top = revolve("lid_top", [(0.0, 1.55), (1.8, 1.5), (3.6, 1.22), (5.2, 0.8), (6.2, 0.45), (6.5, 0.25), (6.5, 0.0)], segments=64, uv_u=2.0, uv_v=6.0)
    parts = [part(top, "weave", lambda co: 0.6 + 0.4 * min(1.0, co.z / 1.6))]
    band = revolve("lid_band", [(6.55, 0.0), (6.55, 0.62), (6.38, 0.7), (6.0, 0.7), (6.0, 0.0)], segments=64, uv_u=6.0, uv_v=1.0, probe=lambda n: n.x * 0 + 1 > 0)
    parts.append(part(band, "steamwood"))
    knob = sphere("lid_knob", 0.42, (0, 0, 1.62), scale=(1, 1, 0.85))
    parts.append(part(knob, "steamwood"))
    lid_obj = bp.join(parts, "lid")
    lid_obj.rotation_euler = (math.radians(68), 0, 0)
    ba.apply_xform(lid_obj)
    lid_obj.location.z = 5.6
    ba.apply_xform(lid_obj)
    lid_obj.name = "ENV_Dimsum_Lid"
    return lid_obj


def tier():
    """A second steamer stacked behind the arena (solid drum with wood body, bamboo hoops, a lid on top)."""
    body = revolve("tier_body", [(0.0, 0.0), (9.0, 0.0), (9.0, 2.2), (8.7, 2.45), (0.0, 2.45)], segments=96, uv_u=9.0 * math.tau / 3.0, uv_v=3.0)
    parts = [part(body, "steamwood")]
    for z in (0.5, 1.7):
        parts.append(part(ba.add_torus(f"tier_hoop{z}", 9.02, 0.1, (0, 0, z), major_segments=96, minor_segments=8), "bambooband"))
    drum2 = revolve("tier_body2", [(0.0, 2.45), (8.4, 2.45), (8.4, 4.4), (8.1, 4.65), (0.0, 4.65)], segments=96, uv_u=8.4 * math.tau / 3.0, uv_v=3.0)
    parts.append(part(drum2, "steamwood"))
    for z in (2.9, 4.1):
        parts.append(part(ba.add_torus(f"tier_hoop2_{z}", 8.42, 0.1, (0, 0, z), major_segments=96, minor_segments=8), "bambooband"))
    cap = revolve("tier_lid", [(0.0, 4.95), (3.0, 4.9), (6.0, 4.75), (8.4, 4.65), (8.4, 4.65)], segments=96, uv_u=2.0, uv_v=8.0)
    parts.append(part(cap, "weave"))
    return bp.join(parts, "ENV_Dimsum_Tier")


def chop_lying():
    """Two giant chopsticks lying on the table: square-ish tapered rods with a lacquered handle end."""
    parts = []
    for k, (y, tilt) in enumerate(((0.0, 0.0), (1.4, 0.04))):
        pts = [(x, y + x * tilt, 0.45) for x in (-9.0, -3.0, 3.0, 9.0)]
        wood = tube(f"chop{k}", pts, [0.14, 0.2, 0.28, 0.4], segments=8)
        bp.uv_box(wood, 2.0)
        parts.append(part(wood, "steamwood", lambda co: 0.6 + 0.4 * min(1.0, co.z / 0.9)))
        grip = tube(f"chopgrip{k}", [(x, y + x * tilt, 0.46) for x in (3.2, 6.0, 9.0)], [0.3, 0.35, 0.405], segments=8)
        parts.append(part(grip, "lacquer_red"))
        ring = ba.add_torus(f"chopring{k}", 0.38, 0.05, (3.1, y + 3.1 * tilt, 0.46), rot=(0, math.pi / 2, 0), major_segments=14, minor_segments=6)
        parts.append(part(ring, "gold"))
    return bp.join(parts, "ENV_Dimsum_ChopLying")


# ---------------------------------------------------------------- squishy dumplings


def dumpling(name, seed, size, with_face=True):
    """Soft jelly bao: pleated top twisting into a nub, flat base, kawaii face on the -Y side."""
    rng = random.Random(seed)
    rx = 0.5 * size * 1.12
    rz = 0.5 * size * 0.84
    cz = rz  # sits on the floor
    body = sphere(name + "_body", 1.0, (0, 0, cz), scale=(rx, rx, rz), seg=56, rings=32)
    pleats = rng.choice((15, 16, 17))
    twist = rng.uniform(-0.25, 0.25)
    for v in body.data.vertices:
        zn = (v.co.z - 0.0) / (2 * rz)  # 0 at the base, 1 at the top
        r = math.hypot(v.co.x, v.co.y)
        ang = math.atan2(v.co.y, v.co.x)
        w = max(0.0, min(1.0, (zn - 0.38) / 0.35))
        ridge = 1.0 + 0.085 * w * math.cos(ang * pleats + twist * zn * 6.0)
        pinch = 1.0 - 0.62 * max(0.0, (zn - 0.78) / 0.22) ** 1.5
        k = ridge * pinch
        v.co.x *= k
        v.co.y *= k
        if v.co.z < 0.02 * size:
            v.co.z = 0.02 * size  # flat, slightly squashed base
    body.data.update()
    bp.uv_box(body, 1.0)
    parts = [part(body, "jelly", lambda co: 0.7 + 0.3 * min(1.0, co.z / (2 * rz)))]
    nub = sphere(name + "_nub", 0.065 * size, (0, 0, 2 * rz + 0.005 * size), scale=(1, 1, 0.9), seg=12, rings=8)
    parts.append(part(nub, "jelly"))
    if with_face:
        az0 = -math.pi / 2  # facing -Y
        elev = math.radians(14)

        def on_body(az, el, push=0.0):
            x = (rx + push) * math.cos(el) * math.cos(az)
            y = (rx + push) * math.cos(el) * math.sin(az)
            z = cz + (rz + push) * math.sin(el)
            return (x, y, z)

        def facing(az):
            return (0, 0, az + math.pi / 2)

        eye_r = 0.054 * size
        for side in (-1, 1):
            az = az0 + side * math.radians(17)
            eye = sphere(f"{name}_eye{side}", eye_r, on_body(az, elev, -0.012 * size), scale=(1.0, 0.55, 1.18), seg=14, rings=10, rot=facing(az))
            parts.append(part(eye, "face_eye"))
            spark = sphere(f"{name}_spark{side}", eye_r * 0.4, on_body(az - side * math.radians(2.2), elev + math.radians(3.8), 0.012 * size), scale=(1.0, 0.5, 1.0), seg=8, rings=6, rot=facing(az))
            parts.append(part(spark, "face_white"))
            cheek = sphere(f"{name}_blush{side}", 0.07 * size, on_body(az0 + side * math.radians(36), math.radians(2), -0.012 * size), scale=(1.2, 0.3, 0.7), seg=12, rings=8, rot=facing(az0 + side * math.radians(36)))
            parts.append(part(cheek, "face_blush"))
        mouth = sphere(name + "_mouth", 0.034 * size, on_body(az0, math.radians(2), -0.008 * size), scale=(1.2, 0.4, 1.0), seg=10, rings=8, rot=facing(az0))
        parts.append(part(mouth, "face_mouth"))
    return bp.join(parts, name)


# ---------------------------------------------------------------- plants and tableware


def bamboo(name, seed):
    rng = random.Random(seed)
    parts = []
    specs = ((0.0, 0.0, 4.6), (0.34, 0.12, 3.8), (-0.26, 0.2, 3.1))
    leaf_specs = []
    for k, (x, y, h) in enumerate(specs):
        lean = Vector((rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), 0))
        pts, radii = [], []
        steps = 24
        for i in range(steps + 1):
            t = i / steps
            node = 1.0 if (i % 4 == 0 and 0 < i < steps) else 0.0
            pts.append((x + lean.x * t * t * h, y + lean.y * t * t * h, t * h))
            radii.append(0.085 * (1 - 0.35 * t) + 0.012 * node)
        stalk = tube(f"{name}_stalk{k}", pts, radii, segments=10, cap_start=False)
        bp.uv_cylinder(stalk, repeats=1.0, v_scale=1.0, center=(x, y))
        parts.append(part(stalk, "bamboogreen", lambda co: 0.55 + 0.45 * min(1.0, co.z / 1.2)))
        for i in range(4, steps, 4):
            ring = ba.add_torus(f"{name}_node{k}_{i}", radii[i] * 1.12, 0.014, (pts[i][0], pts[i][1], pts[i][2]), major_segments=14, minor_segments=5)
            parts.append(part(ring, "bamboogreen"))
        top = Vector(pts[-1])
        for j in range(7):
            a = rng.uniform(0, math.tau)
            lift = rng.uniform(-0.1, 0.5)
            facing = Vector((math.cos(a), math.sin(a), lift))
            c = top - Vector((0, 0, rng.uniform(0.0, 1.1))) + Vector((math.cos(a), math.sin(a), 0)) * 0.12
            leaf_specs.append((tuple(c), tuple(facing), (0, 0, 1), rng.uniform(1.0, 1.35), rng.uniform(1.0, 1.5)))
    leaves = bp.cards(name + "_leaves", leaf_specs, (0, 0), atlas_cells=1, spherical=(0, 0, 3.4), normal_up=0.4)
    bp.paint(leaves, lambda co: 0.55 + 0.45 * min(1.0, max(0.0, (co.z - 1.5) / 3.0)), lambda co: min(1.0, max(0.0, (co.z - 2.0) * 0.4)))
    leaves.data.materials.clear()
    leaves.data.materials.append(bp.surface("bamboo_leaf"))
    parts.append(leaves)
    return bp.join(parts, name)


def bok_choy(name, seed):
    rng = random.Random(seed)
    parts = []
    count = 11
    for k in range(count):
        a = k / count * math.tau + rng.uniform(-0.15, 0.15)
        ring = 0 if k < 6 else 1
        out = 0.34 + ring * 0.0
        tilt = (0.35 if ring == 0 else 0.7) + rng.uniform(-0.08, 0.08)
        height = (0.95 if ring == 0 else 0.7) * rng.uniform(0.9, 1.1)
        base = Vector((math.cos(a) * 0.05, math.sin(a) * 0.05, 0.0))
        direction = Vector((math.cos(a) * math.sin(tilt), math.sin(a) * math.sin(tilt), math.cos(tilt)))
        pts = [tuple(base + direction * (height * 0.55) * t + Vector((0, 0, 0))) for t in (0, 0.4, 0.8, 1.0)]
        stalk = tube(f"{name}_stalk{k}", pts, [0.07, 0.065, 0.052, 0.04], segments=8, ovals=[(1.5, 0.7)] * 4, cap_start=False)
        bp.uv_box(stalk, 0.6)
        parts.append(part(stalk, "bokstem", lambda co: 0.5 + 0.5 * min(1.0, co.z / 0.5)))
        # Blade: a wavy, cupped leaf growing from the end of the stalk.
        tip = Vector(pts[-1])
        bl = height * 0.62
        bw = 0.34 + rng.uniform(-0.04, 0.06)
        bm = bmesh.new()
        uv_layer = bm.loops.layers.uv.new("UVMap")
        cols, rows = 12, 8
        side_dir = Vector((-math.sin(a), math.cos(a), 0))
        up_dir = Vector((direction.x * 0.45, direction.y * 0.45, 1.0)).normalized()
        grid = []
        for ri in range(rows + 1):
            t = ri / rows
            row = []
            width = math.sin(math.pi * min(1.0, t * 0.85 + 0.15)) ** 0.7 * bw
            spine = tip + up_dir * (bl * t) + Vector((math.cos(a), math.sin(a), 0)) * (0.18 * t * t * bl) - Vector((0, 0, 0.05 * t * t * bl))
            for ci in range(cols + 1):
                s = ci / cols * 2 - 1
                cup = 0.09 * width * (1 - s * s) * (1.0 - t * 0.4)
                ruffle = 0.035 * math.sin(t * 9.0 + s * 3.0 + k) * abs(s)
                p = spine + side_dir * (s * width) + Vector((0, 0, cup + ruffle))
                row.append(bm.verts.new(p))
            grid.append(row)
        for ri in range(rows):
            for ci in range(cols):
                f = bm.faces.new((grid[ri][ci], grid[ri][ci + 1], grid[ri + 1][ci + 1], grid[ri + 1][ci]))
                for loop in f.loops:
                    loop[uv_layer].uv = (0.5, 0.5)
        # Thin solid: second skin below so the underside is lit as well (back faces are culled in the game).
        bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=0.018)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(f"{name}_blade{k}")
        bm.to_mesh(mesh)
        bm.free()
        blade = bpy.data.objects.new(f"{name}_blade{k}", mesh)
        bpy.context.scene.collection.objects.link(blade)
        ba.smooth(blade, math.radians(80))
        parts.append(part(blade, "jade", lambda co: 0.45 + 0.55 * min(1.0, max(0.0, (co.z - 0.2) / 0.9))))
    return bp.join(parts, name)


def scallions(name, seed):
    rng = random.Random(seed)
    parts = []
    for k in range(9):
        a = k / 9 * math.tau + rng.uniform(-0.3, 0.3)
        r0 = rng.uniform(0.0, 0.1)
        lean = rng.uniform(0.1, 0.32)
        h = rng.uniform(0.85, 1.25)
        base = Vector((math.cos(a) * r0, math.sin(a) * r0, 0))
        d = Vector((math.cos(a) * lean, math.sin(a) * lean, 1.0)).normalized()
        white = [tuple(base + d * h * t) for t in (0.0, 0.18, 0.34)]
        parts.append(part(tube(f"{name}_w{k}", white, [0.034, 0.036, 0.03], segments=8, cap_start=False), "scallion_white", lambda co: 0.55 + 0.45 * min(1.0, co.z / 0.4)))
        green = [tuple(base + d * h * t) for t in (0.34, 0.6, 0.85, 1.0)]
        curve = Vector((math.cos(a), math.sin(a), 0)) * 0.1
        green = [(p[0] + curve.x * i * 0.5, p[1] + curve.y * i * 0.5, p[2] - 0.03 * i * i) for i, p in enumerate(green)]
        parts.append(part(tube(f"{name}_g{k}", green, [0.03, 0.028, 0.02, 0.006], segments=8, cap_start=False), "scallion_green"))
    return bp.join(parts, name)


def chopsticks(name, seed):
    parts = []
    for k, (x0, x1, lean) in enumerate(((0.13, -0.2, 1), (-0.13, 0.2, -1))):
        pts = [(x0 + (x1 - x0) * (t ** 1.6), lean * 0.02 * t, t * 3.4) for t in (0.0, 0.25, 0.5, 0.7, 0.85, 1.0)]
        radii = [0.04, 0.05, 0.063, 0.072, 0.077, 0.08]
        wood = tube(f"{name}_wood{k}", pts[:4], radii[:4], segments=8, cap_start=True, cap_end=False)
        bp.uv_box(wood, 1.5)
        parts.append(part(wood, "steamwood", lambda co: 0.55 + 0.45 * min(1.0, co.z / 1.5)))
        lacquer = tube(f"{name}_lacquer{k}", pts[3:], radii[3:], segments=8, cap_start=False, cap_end=True)
        parts.append(part(lacquer, "lacquer_red"))
        ring = ba.add_torus(f"{name}_ring{k}", 0.074, 0.011, (pts[3][0], pts[3][1], pts[3][2]), major_segments=14, minor_segments=6)
        parts.append(part(ring, "gold"))
        ring2 = ba.add_torus(f"{name}_ring2{k}", 0.081, 0.011, (pts[5][0], pts[5][1], pts[5][2] - 0.07), major_segments=14, minor_segments=6)
        parts.append(part(ring2, "gold"))
    # Little ceramic rest holding both at the base.
    rest = revolve(name + "_rest", [(0.0, 0.0), (0.42, 0.0), (0.46, 0.08), (0.4, 0.16), (0.0, 0.17)], segments=32)
    parts.append(part(rest, "porcelain"))
    return bp.join(parts, name)


def sauce_dish(name, seed):
    rng = random.Random(seed)
    saucer = revolve(name + "_dish", [(0.0, 0.0), (0.46, 0.0), (0.5, 0.04), (0.92, 0.2), (1.0, 0.3), (0.96, 0.31), (0.86, 0.24), (0.55, 0.12), (0.0, 0.1)], segments=48)
    parts = [part(saucer, "porcelain", lambda co: 0.6 + 0.4 * min(1.0, co.z / 0.3))]
    rim = ba.add_torus(name + "_rim", 0.97, 0.022, (0, 0, 0.305), major_segments=48, minor_segments=6)
    parts.append(part(rim, "porcelain_blue"))
    sauce = revolve(name + "_soy", [(0.0, 0.2), (0.66, 0.2), (0.7, 0.19), (0.0, 0.19)], segments=36, probe=lambda n: n.z > 0)
    parts.append(part(sauce, "soy"))
    for i in range(7):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0.0, 0.48)
        flake = sphere(f"{name}_chili{i}", rng.uniform(0.03, 0.055), (math.cos(a) * r, math.sin(a) * r, 0.205), scale=(1.3, 1.0, 0.45), seg=8, rings=6)
        parts.append(part(flake, "chili_red"))
    for i in range(4):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0.1, 0.5)
        bit = ba.add_torus(f"{name}_onion{i}", 0.04, 0.012, (math.cos(a) * r, math.sin(a) * r, 0.21), major_segments=10, minor_segments=5)
        parts.append(part(bit, "scallion_green"))
    return bp.join(parts, name)


def tea_cup(name, seed):
    cup = revolve(name + "_cup", [(0.0, 0.1), (0.2, 0.1), (0.26, 0.14), (0.4, 0.3), (0.55, 0.55), (0.62, 0.84), (0.6, 0.86), (0.55, 0.84), (0.44, 0.52), (0.28, 0.36), (0.0, 0.34)], segments=48)
    saucer = revolve(name + "_saucer", [(0.0, 0.0), (0.7, 0.0), (0.92, 0.07), (0.98, 0.14), (0.93, 0.15), (0.7, 0.08), (0.0, 0.1)], segments=48)
    parts = [part(cup, "porcelain", lambda co: 0.6 + 0.4 * min(1.0, co.z / 0.8)), part(saucer, "porcelain")]
    tea = revolve(name + "_tea", [(0.0, 0.72), (0.5, 0.72), (0.52, 0.71), (0.0, 0.7)], segments=40)
    parts.append(part(tea, "tea"))
    band = ba.add_torus(name + "_band", 0.612, 0.032, (0, 0, 0.7), major_segments=48, minor_segments=8)
    parts.append(part(band, "porcelain_blue"))
    foot = ba.add_torus(name + "_foot", 0.25, 0.02, (0, 0, 0.12), major_segments=36, minor_segments=6)
    parts.append(part(foot, "porcelain_blue"))
    for i in range(6):
        a = i / 6 * math.tau
        petal = sphere(f"{name}_petal{i}", 0.055, (math.cos(a) * 0.5, math.sin(a) * 0.5, 0.43), scale=(1.0, 0.55, 1.5), seg=10, rings=8, rot=(0, 0, a + math.pi / 2))
        parts.append(part(petal, "porcelain_blue"))
    return bp.join(parts, name)


def chilies(name, seed):
    rng = random.Random(seed)
    parts = []
    for k, (angle, length, curl) in enumerate(((0.3, 1.5, 0.55), (2.9, 1.25, -0.5))):
        pts, radii = [], []
        steps = 12
        for i in range(steps + 1):
            t = i / steps
            x = math.cos(angle) * t * length - math.sin(angle) * curl * t * t * length * 0.5
            y = math.sin(angle) * t * length + math.cos(angle) * curl * t * t * length * 0.5
            z = 0.13 + 0.05 * math.sin(t * math.pi) + 0.03
            pts.append((x - math.cos(angle) * length * 0.4, y - math.sin(angle) * length * 0.4, z))
            radii.append(0.13 * math.sin(math.pi * min(1.0, 0.18 + t * 0.78)) ** 0.6 + 0.012)
        body = tube(f"{name}_body{k}", pts, radii, segments=12, cap_start=False)
        bp.uv_box(body, 1.0)
        parts.append(part(body, "chili_red", lambda co: 0.6 + 0.4 * min(1.0, co.z / 0.25)))
        cap = sphere(f"{name}_cap{k}", 0.1, (pts[0][0], pts[0][1], pts[0][2] + 0.01), scale=(1.1, 1.1, 0.65), seg=10, rings=8)
        parts.append(part(cap, "chili_stem"))
        d = Vector(pts[1]) - Vector(pts[0])
        stem_end = Vector(pts[0]) - d.normalized() * 0.28 + Vector((0, 0, 0.1))
        stem = tube(f"{name}_stem{k}", [tuple(Vector(pts[0]) - d.normalized() * 0.02), tuple(Vector(pts[0]) - d.normalized() * 0.16 + Vector((0, 0, 0.05))), tuple(stem_end)], [0.034, 0.028, 0.02], segments=8)
        parts.append(part(stem, "chili_stem"))
    return bp.join(parts, name)


def shiitake(name, seed):
    rng = random.Random(seed)
    parts = []
    for k, (x, y, s) in enumerate(((0, 0, 1.0), (0.34, 0.12, 0.72), (-0.26, 0.2, 0.58))):
        h = 0.2 * s
        r = 0.27 * s
        stem = tube(f"{name}_stem{k}", [(x, y, 0), (x, y, h * 0.55), (x, y, h)], [0.05 * s, 0.043 * s, 0.04 * s], segments=10, cap_start=False)
        parts.append(part(stem, "shiitake_gill", lambda co: 0.55 + 0.45 * min(1.0, co.z / h)))
        dome = revolve(f"{name}_cap{k}", [(r * 1.0, h), (r * 0.98, h + 0.03 * s), (r * 0.82, h + 0.1 * s), (r * 0.55, h + 0.15 * s), (r * 0.25, h + 0.18 * s), (0.0, h + 0.185 * s)], segments=28)
        for v in dome.data.vertices:
            ang = math.atan2(v.co.y - y if False else v.co.y, v.co.x)
        dome.location = (x, y, 0)
        ba.apply_xform(dome)
        bp.uv_box(dome, 0.5)
        parts.append(part(dome, "shiitake", lambda co: 0.85))
        gills = revolve(f"{name}_gills{k}", [(0.0, h), (r * 0.98, h)], segments=28)
        gills.location = (x, y, 0)
        ba.apply_xform(gills)
        parts.append(part(gills, "shiitake_gill", lambda co: 0.6))
    return bp.join(parts, name)


def bamboo_leaves(name, seed):
    rng = random.Random(seed)
    specs = []
    for k in range(5):
        a = rng.uniform(0, math.tau)
        c = (rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 0.012 + 0.004 * k)
        specs.append((c, (0, 0, 1), (math.cos(a), math.sin(a), 0), rng.uniform(0.5, 0.75), rng.uniform(0.95, 1.4)))
    obj = bp.cards(name, specs, (0, 0), atlas_cells=1, normal_up=0.9)
    bp.paint(obj, lambda co: 0.85, lambda co: 0.0)
    obj.data.materials.clear()
    obj.data.materials.append(bp.surface("bamboo_leaf"))
    return obj


# ---------------------------------------------------------------- production

ASSETS = [
    # id, builder, preview distance, height, target z
    ("ENV_Dimsum_Floor", lambda: floor_disc(), 80.0, 60.0, 0.0),
    ("ENV_Dimsum_Wall", lambda: steamer_wall(), 80.0, 62.0, 1.0),
    ("ENV_Dimsum_Table", lambda: table_ring(), 150.0, 110.0, 0.0),
    ("ENV_Dimsum_Lid", lambda: lid(), 22.0, 12.0, 3.2),
    ("ENV_Dimsum_Tier", lambda: tier(), 36.0, 18.0, 2.5),
    ("ENV_Dimsum_ChopLying", lambda: chop_lying(), 26.0, 14.0, 0.5),
    ("ENV_Dimsum_Dumpling_A", lambda: dumpling("ENV_Dimsum_Dumpling_A", 5, 1.45), 4.2, 1.8, 0.6),
    ("ENV_Dimsum_Dumpling_B", lambda: dumpling("ENV_Dimsum_Dumpling_B", 9, 1.0), 3.2, 1.4, 0.4),
    ("ENV_Dimsum_Bamboo", lambda: bamboo("ENV_Dimsum_Bamboo", 3), 11.0, 4.6, 2.2),
    ("ENV_Dimsum_BokChoy", lambda: bok_choy("ENV_Dimsum_BokChoy", 7), 3.6, 1.6, 0.5),
    ("ENV_Dimsum_Scallion", lambda: scallions("ENV_Dimsum_Scallion", 11), 3.0, 1.4, 0.5),
    ("ENV_Dimsum_Chopsticks", lambda: chopsticks("ENV_Dimsum_Chopsticks", 13), 9.0, 3.6, 1.6),
    ("ENV_Dimsum_Sauce", lambda: sauce_dish("ENV_Dimsum_Sauce", 17), 4.4, 1.8, 0.2),
    ("ENV_Dimsum_Teacup", lambda: tea_cup("ENV_Dimsum_Teacup", 19), 4.4, 1.8, 0.4),
    ("ENV_Dimsum_Chili", lambda: chilies("ENV_Dimsum_Chili", 23), 3.6, 1.8, 0.1),
    ("ENV_Dimsum_Shiitake", lambda: shiitake("ENV_Dimsum_Shiitake", 29), 2.4, 1.0, 0.15),
    ("ENV_Dimsum_BambooLeaves", lambda: bamboo_leaves("ENV_Dimsum_BambooLeaves", 31), 2.6, 1.6, 0.0),
]


def main():
    ba.reset_file()
    studio = ba.hero_studio()
    for asset_id, builder, distance, height, target_z in ASSETS:
        if ONLY and asset_id not in ONLY:
            continue
        obj = builder()
        bp.export(asset_id, ENV, obj, distance, height, target_z)
    ba.hero_studio_off(studio)
    ba.write_catalog(merge=True)
    print("ARENA_DIMSUM_OK", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        import traceback

        traceback.print_exc()
        sys.exit(1)
