"""
Deterministic procedural generator for the natural_rock_pack asset pack.

Structured asset values (IDs, dimensions, seeds, budgets, material IDs) come
from the pack YAML. This module implements family-appropriate topology strategies
for natural rock forms (pebbles, shards, slabs, wedges, angular crags, pillars,
clustered duos, crescent rocks, and landmark boulders) while ensuring every
exported asset is a single watertight manifold mesh.
"""

from __future__ import annotations

import math
import random

import bmesh
import bpy
from mathutils import Vector

from common.geometry import count_triangles, enforce_dimensions_and_origin, ensure_flat_shading
from common.materials import assign_material, get_or_create_pbr_material, unwrap_smart_uvs
from common.spec import merge_asset_defaults

PALETTE = {
    "mat_stone_grey_01": {"color": (0.33, 0.34, 0.35, 1.0), "roughness": 0.88},
    "mat_stone_grey_02": {"color": (0.27, 0.28, 0.30, 1.0), "roughness": 0.90},
    "mat_stone_grey_03": {"color": (0.36, 0.35, 0.34, 1.0), "roughness": 0.87},
    "mat_stone_grey_04": {"color": (0.22, 0.23, 0.25, 1.0), "roughness": 0.92},
    "mat_stone_grey_05": {"color": (0.42, 0.42, 0.43, 1.0), "roughness": 0.86},
}


def _build_materials(pack_spec):
    requested_ids = {
        merge_asset_defaults(pack_spec, asset).get("material_id")
        for asset in pack_spec["assets"]
    }
    materials = {}
    for mat_id in requested_ids:
        if not mat_id:
            continue
        if mat_id not in PALETTE:
            raise ValueError(f"No rock material recipe exists for material_id '{mat_id}'.")
        params = PALETTE[mat_id]
        materials[mat_id] = get_or_create_pbr_material(
            mat_id=mat_id,
            base_color=params["color"],
            roughness=params["roughness"],
        )
    return materials


def _build_minimal_bipyramid(bm, num_eq_verts, rng, asym=(0.0, 0.0, 0.0), slope=0.0):
    """
    Constructs a closed, watertight bipyramid directly.
    For num_eq_verts = 3: 5 verts, 6 triangles.
    For num_eq_verts = 4: 6 verts, 8 triangles.
    For num_eq_verts = 5: 7 verts, 10 triangles.
    """
    top_z = 0.5 + rng.uniform(-0.05, 0.05)
    bot_z = -0.5 + rng.uniform(-0.05, 0.05)
    top_x = asym[0] + (slope * 0.25)
    top_y = asym[1]
    top = bm.verts.new(Vector((top_x, top_y, top_z)))
    bot = bm.verts.new(Vector((-asym[0] * 0.5, -asym[1] * 0.5, bot_z)))

    eq_verts = []
    base_step = 2.0 * math.pi / float(num_eq_verts)
    phase_offset = rng.uniform(0.0, base_step)

    for i in range(num_eq_verts):
        angle = phase_offset + i * base_step + rng.uniform(-0.18, 0.18)
        radius = 0.5 * (1.0 + rng.uniform(-0.15, 0.15))
        vx = math.cos(angle) * radius
        vy = math.sin(angle) * radius
        vz = rng.uniform(-0.10, 0.10) + asym[2] * (vx + vy)
        eq_verts.append(bm.verts.new(Vector((vx, vy, vz))))

    for i in range(num_eq_verts):
        next_i = (i + 1) % num_eq_verts
        bm.faces.new((top, eq_verts[i], eq_verts[next_i]))
        bm.faces.new((bot, eq_verts[next_i], eq_verts[i]))


def _generate_tiny_mesh(asset_spec, recipe, rng):
    family = recipe.get("shape_family", "tiny_pebble")
    verts_target = int(recipe.get("verts", 5))
    eq_count = max(3, min(5, verts_target - 2))
    asym = [float(v) for v in recipe.get("asym", [0.0, 0.0, 0.0])]
    slope = float(recipe.get("slope", 0.0))

    bm = bmesh.new()
    _build_minimal_bipyramid(bm, eq_count, rng, asym=asym, slope=slope)
    return bm


def _boolean_union_bmeshes(bm_base, bm_add):
    """
    Combines two bmeshes into a single contiguous watertight 2-manifold bmesh
    using Blender's exact boolean solver and cleanly removes temporary datablocks.
    """
    mesh1 = bpy.data.meshes.new("_tmp_union_m1")
    mesh2 = bpy.data.meshes.new("_tmp_union_m2")
    bm_base.to_mesh(mesh1)
    bm_add.to_mesh(mesh2)
    bm_base.free()
    bm_add.free()

    obj1 = bpy.data.objects.new("_tmp_union_o1", mesh1)
    obj2 = bpy.data.objects.new("_tmp_union_o2", mesh2)
    bpy.context.scene.collection.objects.link(obj1)
    bpy.context.scene.collection.objects.link(obj2)

    mod = obj1.modifiers.new(name="Union", type="BOOLEAN")
    mod.operation = "UNION"
    mod.object = obj2
    mod.solver = "EXACT"

    bpy.context.view_layer.objects.active = obj1
    bpy.ops.object.modifier_apply(modifier="Union")

    bpy.data.objects.remove(obj2, do_unlink=True)
    bpy.data.meshes.remove(mesh2)

    bm_res = bmesh.new()
    bm_res.from_mesh(obj1.data)

    bpy.data.objects.remove(obj1, do_unlink=True)
    bpy.data.meshes.remove(mesh1)

    return bm_res


def _generate_slab_mesh(asset_spec, recipe, rng):
    """
    Constructs a solid, weathered flagstone slab with an irregular polygonal perimeter,
    varied vertical shear and beveled edge facets, and a faceted plateau top.
    """
    bm = bmesh.new()
    hard_max = int(asset_spec["hard_max_triangles"])
    n_sides = 4 if hard_max <= 20 else (5 if hard_max <= 35 else (6 if hard_max <= 60 else 7))

    rx, ry, rz = 0.50, 0.42, 0.14
    phase = rng.uniform(0.0, math.pi * 2.0)
    base_pts = []
    top_pts = []

    for i in range(n_sides):
        th = phase + i * (2.0 * math.pi / float(n_sides)) + rng.uniform(-0.14, 0.14)
        r_var = 1.0 + rng.uniform(-0.16, 0.16)
        bx = math.cos(th) * rx * r_var
        by = math.sin(th) * ry * r_var
        base_pts.append(bm.verts.new(Vector((bx, by, -rz))))
        taper = rng.uniform(0.85, 0.96)
        top_pts.append(bm.verts.new(Vector((bx * taper, by * taper, rz + rng.uniform(-0.02, 0.02)))))

    for i in range(n_sides):
        next_i = (i + 1) % n_sides
        bm.faces.new((base_pts[i], top_pts[i], top_pts[next_i], base_pts[next_i]))

    bm.faces.new(reversed(base_pts))
    bm.faces.new(top_pts)

    num_cuts = 1 if hard_max <= 20 else (2 if hard_max <= 35 else 3)
    for _ in range(num_cuts):
        n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.2, 0.6))).normalized()
        d = rng.uniform(0.32, 0.42)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                              plane_co=n * d, plane_no=n, clear_outer=True)
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    return bm


def _generate_eroded_boulder_mesh(asset_spec, recipe, rng):
    """
    Constructs a solid, heavy boulder mass with an asymmetric concave weathering
    hollow on one upper flank, maintaining solid grounded mass and natural cleavage facets.
    """
    bm = bmesh.new()
    hard_max = int(asset_spec["hard_max_triangles"])
    arch_depth = float(recipe.get("arch_depth", 0.32))
    n_sides = 6 if hard_max <= 35 else 8
    use_two_tiers = (hard_max > 35)

    bot_verts = []
    mid_verts = []
    top_verts = []
    hollow_ang = rng.uniform(0.0, math.pi * 2.0)

    for i in range(n_sides):
        ang = i * (2.0 * math.pi / float(n_sides))
        diff_ang = math.cos(ang - hollow_ang)
        cos_a = math.cos(ang)
        sin_a = math.sin(ang)

        r_bot = 0.50 * (1.0 + rng.uniform(-0.06, 0.06))
        bot_verts.append(bm.verts.new(Vector((cos_a * r_bot, sin_a * r_bot, -0.36))))

        if use_two_tiers:
            r_mid = 0.46 * (1.0 + rng.uniform(-0.05, 0.05))
            z_mid = -0.05 + rng.uniform(-0.04, 0.04)
            if diff_ang > 0.1:
                r_mid *= (1.0 - arch_depth * 0.65 * diff_ang)
                z_mid -= 0.14 * diff_ang
            mid_verts.append(bm.verts.new(Vector((cos_a * r_mid, sin_a * r_mid, z_mid))))

        r_top = 0.36 * (1.0 + rng.uniform(-0.05, 0.05))
        z_top = 0.38 + rng.uniform(-0.04, 0.04)
        if diff_ang > 0.1:
            r_top *= (1.0 - arch_depth * 0.85 * diff_ang)
            z_top -= 0.28 * diff_ang
        top_verts.append(bm.verts.new(Vector((cos_a * r_top, sin_a * r_top, z_top))))

    if use_two_tiers:
        for i in range(n_sides):
            next_i = (i + 1) % n_sides
            bm.faces.new((bot_verts[i], mid_verts[i], mid_verts[next_i], bot_verts[next_i]))
            bm.faces.new((mid_verts[i], top_verts[i], top_verts[next_i], mid_verts[next_i]))
    else:
        for i in range(n_sides):
            next_i = (i + 1) % n_sides
            bm.faces.new((bot_verts[i], top_verts[i], top_verts[next_i], bot_verts[next_i]))

    bot_c = bm.verts.new(Vector((0.0, 0.0, -0.38)))
    for i in range(n_sides):
        next_i = (i + 1) % n_sides
        bm.faces.new((bot_c, bot_verts[next_i], bot_verts[i]))

    crest_x = -math.cos(hollow_ang) * 0.18
    crest_y = -math.sin(hollow_ang) * 0.18
    top_c = bm.verts.new(Vector((crest_x, crest_y, 0.44)))
    for i in range(n_sides):
        next_i = (i + 1) % n_sides
        bm.faces.new((top_c, top_verts[i], top_verts[next_i]))

    num_cuts = 1 if hard_max <= 35 else 3
    for _ in range(num_cuts):
        normal = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.3, 0.6))).normalized()
        offset = rng.uniform(0.38, 0.46)
        bmesh.ops.bisect_plane(
            bm,
            geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
            plane_co=normal * offset,
            plane_no=normal,
            clear_outer=True,
        )
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    return bm


def _generate_clustered_duo_mesh(asset_spec, recipe, rng):
    """
    Constructs a single connected watertight mesh with two distinct rocky peaks
    and a visible central saddle crevice using two intersecting volumetric masses.
    """
    hard_max = int(asset_spec["hard_max_triangles"])
    num_cuts = 3 if hard_max <= 60 else (4 if hard_max <= 100 else 5)

    d1 = -0.26 + rng.uniform(-0.03, 0.03)
    d2 = 0.26 + rng.uniform(-0.03, 0.03)
    scale1 = (0.55, 0.55, 0.85)
    scale2 = (0.46, 0.48, 0.70)

    # Peak 1 (larger, left)
    bm1 = bmesh.new()
    bmesh.ops.create_cube(bm1, size=1.0)
    for v in bm1.verts:
        v.co.x *= scale1[0]
        v.co.y *= scale1[1]
        v.co.z *= scale1[2]
        if v.co.z > 0.0:
            v.co.x *= 0.82
            v.co.y *= 0.82
    for _ in range(num_cuts):
        n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.3, 0.7))).normalized()
        d = rng.uniform(0.35, 0.48) * min(scale1)
        bmesh.ops.bisect_plane(bm1, geom=bm1.verts[:] + bm1.edges[:] + bm1.faces[:],
                              plane_co=n * d, plane_no=n, clear_outer=True)
        bmesh.ops.holes_fill(bm1, edges=[e for e in bm1.edges if len(e.link_faces) == 1])
    for v in bm1.verts:
        v.co.x += d1

    # Peak 2 (smaller, right)
    bm2 = bmesh.new()
    bmesh.ops.create_cube(bm2, size=1.0)
    for v in bm2.verts:
        v.co.x *= scale2[0]
        v.co.y *= scale2[1]
        v.co.z *= scale2[2]
        if v.co.z > 0.0:
            v.co.x *= 0.82
            v.co.y *= 0.82
    for _ in range(num_cuts):
        n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.3, 0.7))).normalized()
        d = rng.uniform(0.35, 0.48) * min(scale2)
        bmesh.ops.bisect_plane(bm2, geom=bm2.verts[:] + bm2.edges[:] + bm2.faces[:],
                              plane_co=n * d, plane_no=n, clear_outer=True)
        bmesh.ops.holes_fill(bm2, edges=[e for e in bm2.edges if len(e.link_faces) == 1])
    for v in bm2.verts:
        v.co.x += d2
        v.co.z -= 0.06

    bm = _boolean_union_bmeshes(bm1, bm2)

    # Post-union tie cuts across the shared formation
    for _ in range(2):
        n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.2, 0.5))).normalized()
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                              plane_co=n * 0.42, plane_no=n, clear_outer=True)
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    return bm


def _generate_shelved_crag_mesh(asset_spec, recipe, rng):
    """
    Builds a faceted crag with visible stepped shelves, planar cliff faces,
    and distinct horizontal ledges using stacked volumetric tiers.
    """
    hard_max = int(asset_spec["hard_max_triangles"])
    num_cuts = 4 if hard_max <= 60 else (5 if hard_max <= 100 else 6)

    # Tier 1 (Base platform)
    bm1 = bmesh.new()
    bmesh.ops.create_cube(bm1, size=1.0)
    for v in bm1.verts:
        v.co.x *= 0.92
        v.co.y *= 0.88
        v.co.z *= 0.42
        v.co.z -= 0.20
    for _ in range(num_cuts):
        n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.3, 0.4))).normalized()
        d = rng.uniform(0.32, 0.44) * 0.88
        bmesh.ops.bisect_plane(bm1, geom=bm1.verts[:] + bm1.edges[:] + bm1.faces[:],
                              plane_co=n * d, plane_no=n, clear_outer=True)
        bmesh.ops.holes_fill(bm1, edges=[e for e in bm1.edges if len(e.link_faces) == 1])

    # Tier 2 (Upper shelf / crag tower, offset to expose horizontal ledge on Tier 1)
    bm2 = bmesh.new()
    bmesh.ops.create_cube(bm2, size=1.0)
    shelf_offset_x = rng.choice([-0.20, 0.20])
    shelf_offset_y = rng.uniform(-0.06, 0.06)
    for v in bm2.verts:
        v.co.x *= 0.52
        v.co.y *= 0.68
        v.co.z *= 0.46
        v.co.z += 0.22
        v.co.x += shelf_offset_x
        v.co.y += shelf_offset_y
        if v.co.z > 0.2:
            v.co.x *= 0.82
            v.co.y *= 0.82
    for _ in range(num_cuts):
        n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.3, 0.7))).normalized()
        d = rng.uniform(0.26, 0.40) * 0.55
        bmesh.ops.bisect_plane(bm2, geom=bm2.verts[:] + bm2.edges[:] + bm2.faces[:],
                              plane_co=Vector((shelf_offset_x, shelf_offset_y, 0.26)) + n * d, plane_no=n, clear_outer=True)
        bmesh.ops.holes_fill(bm2, edges=[e for e in bm2.edges if len(e.link_faces) == 1])

    bm = _boolean_union_bmeshes(bm1, bm2)

    # For landmark crags (very high budget), add a 3rd summit tier for double shelf
    if hard_max >= 200:
        bm3 = bmesh.new()
        bmesh.ops.create_cube(bm3, size=1.0)
        c_off_x = shelf_offset_x * 1.3
        c_off_y = rng.uniform(-0.05, 0.05)
        for v in bm3.verts:
            v.co.x *= 0.30
            v.co.y *= 0.38
            v.co.z *= 0.35
            v.co.z += 0.55
            v.co.x += c_off_x
            v.co.y += c_off_y
            if v.co.z > 0.5:
                v.co.x *= 0.75
                v.co.y *= 0.75
        for _ in range(4):
            n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.2, 0.7))).normalized()
            bmesh.ops.bisect_plane(bm3, geom=bm3.verts[:] + bm3.edges[:] + bm3.faces[:],
                                  plane_co=Vector((c_off_x, c_off_y, 0.58)) + n * 0.18, plane_no=n, clear_outer=True)
            bmesh.ops.holes_fill(bm3, edges=[e for e in bm3.edges if len(e.link_faces) == 1])
        bm = _boolean_union_bmeshes(bm, bm3)

    # Post fracture cuts
    for _ in range(2):
        n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.2, 0.5))).normalized()
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                              plane_co=n * 0.42, plane_no=n, clear_outer=True)
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    return bm


def _generate_tall_pillar_mesh(asset_spec, recipe, rng):
    """
    Constructs a grounded, stable rock pillar / monolith with a wide non-undercut base,
    tapering columnar body, multi-faceted blunt crown, and vertical jointing fractures.
    """
    bm = bmesh.new()
    hard_max = int(asset_spec["hard_max_triangles"])
    n_sides = 6
    bot_pts = []
    mid_pts = []
    top_pts = []

    r_base = 0.50
    taper_mid = rng.uniform(0.85, 0.92)
    taper_top = rng.uniform(0.65, 0.75)
    phase = rng.uniform(0.0, math.pi * 2.0)

    for i in range(n_sides):
        th = phase + i * (2.0 * math.pi / float(n_sides)) + rng.uniform(-0.15, 0.15)
        r_i = r_base * (1.0 + rng.uniform(-0.10, 0.10))
        bx = math.cos(th) * r_i
        by = math.sin(th) * r_i
        bot_pts.append(bm.verts.new(Vector((bx, by, -0.45))))
        mid_pts.append(bm.verts.new(Vector((bx * taper_mid, by * taper_mid, 0.0 + rng.uniform(-0.04, 0.04)))))
        top_pts.append(bm.verts.new(Vector((bx * taper_top, by * taper_top, 0.45 + rng.uniform(-0.03, 0.03)))))

    for i in range(n_sides):
        next_i = (i + 1) % n_sides
        bm.faces.new((bot_pts[i], mid_pts[i], mid_pts[next_i], bot_pts[next_i]))
        bm.faces.new((mid_pts[i], top_pts[i], top_pts[next_i], mid_pts[next_i]))

    bm.faces.new(reversed(bot_pts))
    top_face = bm.faces.new(top_pts)
    poke_res = bmesh.ops.poke(bm, faces=[top_face])
    for v in poke_res["verts"]:
        v.co += Vector((rng.uniform(-0.04, 0.04), rng.uniform(-0.04, 0.04), 0.12 + rng.uniform(-0.02, 0.02)))

    # Vertical columnar jointing cuts (Nz near 0 protects grounded foundation)
    num_columnar = 3 if hard_max <= 60 else (4 if hard_max <= 100 else 6)
    for _ in range(num_columnar):
        n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.15, 0.20))).normalized()
        d = rng.uniform(0.32, 0.44)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                              plane_co=n * d, plane_no=n, clear_outer=True)
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    # Crown fracture cuts
    for _ in range(2):
        n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(0.5, 0.85))).normalized()
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                              plane_co=Vector((0.0, 0.0, 0.35)) + n * 0.25, plane_no=n, clear_outer=True)
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    return bm


def _generate_wedge_mesh(asset_spec, recipe, rng):
    """
    Constructs a solid, natural fallen wedge rock (talus slab) with substantial
    vertical cliff height at the high end, solid blunt toe, multi-plane sloping
    facets, and angled flank fractures.
    """
    bm = bmesh.new()
    hard_max = int(asset_spec["hard_max_triangles"])
    bmesh.ops.create_cube(bm, size=1.0)

    # 1. Primary slope cut maintaining substantial thickness at both high and low ends
    n_main = Vector((rng.uniform(0.45, 0.65), rng.uniform(-0.15, 0.15), rng.uniform(0.70, 0.85))).normalized()
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                          plane_co=Vector((0.0, 0.0, 0.18)), plane_no=n_main, clear_outer=True)
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    # 2. High-side cliff fracture
    n_high = Vector((rng.uniform(0.70, 0.95), rng.uniform(-0.4, 0.4), rng.uniform(-0.2, 0.3))).normalized()
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                          plane_co=Vector((0.38, 0.0, 0.10)), plane_no=n_high, clear_outer=True)
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    # 3. Low-side toe fracture
    n_low = Vector((rng.uniform(-0.95, -0.70), rng.uniform(-0.3, 0.3), rng.uniform(-0.2, 0.3))).normalized()
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                          plane_co=Vector((-0.38, 0.0, -0.10)), plane_no=n_low, clear_outer=True)
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    # 4. Secondary slope and flank fractures only if budget allows
    if hard_max > 20:
        n_sub = Vector((rng.uniform(0.3, 0.6), rng.uniform(0.3, 0.6), rng.uniform(0.6, 0.85))).normalized()
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                              plane_co=Vector((0.05, 0.05, 0.22)), plane_no=n_sub, clear_outer=True)
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

        num_flanks = 1 if hard_max <= 35 else 2
        for sign in [-1.0, 1.0][:num_flanks]:
            n_flank = Vector((rng.uniform(-0.3, 0.3), sign * rng.uniform(0.75, 0.95), rng.uniform(0.1, 0.4))).normalized()
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                                  plane_co=Vector((0.0, sign * 0.40, 0.0)), plane_no=n_flank, clear_outer=True)
            bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    return bm


def _generate_angular_or_squat_mesh(asset_spec, recipe, rng):
    """
    Constructs a solid angular crag or squat boulder from an irregular non-orthogonal
    base polyhedron, ensuring every surface reads as a natural fractured stone face.
    """
    bm = bmesh.new()
    family = recipe.get("shape_family", "angular_rock")
    hard_max = int(asset_spec["hard_max_triangles"])
    is_squat = (family == "squat_rock")

    if is_squat:
        n_sides = 4 if hard_max <= 20 else (5 if hard_max <= 35 else 6)
        pts = []
        for i in range(n_sides):
            ang = i * (2.0 * math.pi / float(n_sides)) + rng.uniform(-0.15, 0.15)
            r = 0.44 * (1.0 + rng.uniform(-0.10, 0.10))
            pts.append(Vector((math.cos(ang) * r, math.sin(ang) * r, -0.32)))
        for i in range(n_sides):
            ang = (i + 0.5) * (2.0 * math.pi / float(n_sides)) + rng.uniform(-0.15, 0.15)
            r = 0.50 * (1.0 + rng.uniform(-0.08, 0.08))
            pts.append(Vector((math.cos(ang) * r, math.sin(ang) * r, 0.02 + rng.uniform(-0.04, 0.04))))
        top_n = 2 if hard_max <= 20 else (3 if hard_max <= 35 else 4)
        for i in range(top_n):
            ang = i * (2.0 * math.pi / float(top_n)) + rng.uniform(-0.20, 0.20)
            r = 0.32 * (1.0 + rng.uniform(-0.10, 0.10))
            pts.append(Vector((math.cos(ang) * r, math.sin(ang) * r, 0.30 + rng.uniform(-0.03, 0.03))))
    else:
        n_pts = 6 if hard_max <= 20 else (9 if hard_max <= 35 else (14 if hard_max <= 60 else 18))
        pts = []
        for i in range(n_pts):
            th = rng.uniform(0.0, math.pi * 2.0)
            ph = rng.uniform(-math.pi * 0.42, math.pi * 0.42)
            r = 0.50 * (1.0 + rng.uniform(-0.16, 0.16))
            pts.append(Vector((math.cos(th) * math.cos(ph) * r, math.sin(th) * math.cos(ph) * r, math.sin(ph) * r)))

    for p in pts:
        bm.verts.new(p)
    result = bmesh.ops.convex_hull(bm, input=bm.verts)
    unused = [e for e in result["geom_unused"] if isinstance(e, bmesh.types.BMVert)]
    if unused:
        bmesh.ops.delete(bm, geom=unused, context="VERTS")

    num_cuts = (0 if is_squat else 1) if hard_max <= 20 else (1 if hard_max <= 35 else (2 if hard_max <= 60 else 4))
    for _ in range(num_cuts):
        n = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.35, 0.75))).normalized()
        d = rng.uniform(0.34, 0.44)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                              plane_co=n * d, plane_no=n, clear_outer=True)
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])

    return bm


def _generate_round_rock_mesh(asset_spec, recipe, rng):
    """
    Fibonacci sphere with noise displacement and cutting planes, ideal for tumbled round boulders.
    """
    target_w, target_d, target_h = [float(v) for v in asset_spec["dimensions_m"]]
    rx, ry, rz = target_w * 0.5, target_d * 0.5, target_h * 0.5
    num_pts = int(recipe.get("num_pts", 24))
    roundness = float(recipe.get("roundness", 0.80))
    phi = math.pi * (math.sqrt(5.0) - 1.0)
    points = []

    for i in range(num_pts):
        y_norm = 1.0 - (i / float(max(1, num_pts - 1))) * 2.0
        radius_at_y = math.sqrt(max(0.0, 1.0 - y_norm * y_norm))
        theta = phi * i
        x_norm = math.cos(theta) * radius_at_y
        z_norm = math.sin(theta) * radius_at_y

        seed = int(asset_spec["seed"])
        n1 = math.sin(x_norm * 2.5 + seed * 0.1) * math.cos(y_norm * 2.1)
        n2 = math.cos(z_norm * 2.8 + seed * 0.2) * math.sin(x_norm * 1.9)
        disp = 1.0 + (n1 * 0.10 + n2 * 0.07) * (1.0 - roundness * 0.5)

        points.append(Vector((x_norm * rx * disp, y_norm * ry * disp, z_norm * rz * disp)))

    bm = bmesh.new()
    for pt in points:
        bm.verts.new(pt)
    result = bmesh.ops.convex_hull(bm, input=bm.verts)
    unused = [e for e in result["geom_unused"] if isinstance(e, bmesh.types.BMVert)]
    if unused:
        bmesh.ops.delete(bm, geom=unused, context="VERTS")
    return bm


def _build_asset_bmesh(asset_spec, recipe, rng):
    recipe = dict(recipe)
    family = recipe.get("shape_family", "angular_rock")
    hard_max = int(asset_spec["hard_max_triangles"])

    # Scale num_pts for round_rock so small tiers are faceted boulders, not 4-pt tetrahedrons
    if family == "round_rock":
        max_round_pts = 10 if hard_max <= 20 else (16 if hard_max <= 35 else (24 if hard_max <= 60 else 36))
        recipe["num_pts"] = min(int(recipe.get("num_pts", 24)), max_round_pts)

    if family in ("tiny_pebble", "tiny_chip", "tiny_slab", "tiny_wedge"):
        bm = _generate_tiny_mesh(asset_spec, recipe, rng)
    elif family == "slab":
        bm = _generate_slab_mesh(asset_spec, recipe, rng)
    elif family in ("eroded_boulder", "crescent_rock"):
        bm = _generate_eroded_boulder_mesh(asset_spec, recipe, rng)
    elif family == "clustered_duo":
        bm = _generate_clustered_duo_mesh(asset_spec, recipe, rng)
    elif family in ("shelved_crag", "landmark_crag"):
        bm = _generate_shelved_crag_mesh(asset_spec, recipe, rng)
    elif family in ("tall_pillar", "landmark_monolith"):
        bm = _generate_tall_pillar_mesh(asset_spec, recipe, rng)
    elif family == "wedge":
        bm = _generate_wedge_mesh(asset_spec, recipe, rng)
    elif family in ("angular_rock", "squat_rock"):
        bm = _generate_angular_or_squat_mesh(asset_spec, recipe, rng)
    elif family == "round_rock":
        bm = _generate_round_rock_mesh(asset_spec, recipe, rng)
    else:
        bm = _generate_angular_or_squat_mesh(asset_spec, recipe, rng)

    # Triangulate all faces
    bmesh.ops.triangulate(bm, faces=bm.faces[:])

    # Clean up duplicate vertices
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)

    # Ensure manifoldness
    non_manifold = sum(1 for e in bm.edges if not e.is_manifold)
    if non_manifold > 0:
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1])
        bmesh.ops.triangulate(bm, faces=bm.faces[:])

    # Strict triangle budget enforcement fallback using progressive planar dissolve
    if len(bm.faces) > hard_max:
        for angle_deg in (10.0, 18.0, 28.0, 40.0, 55.0):
            bmesh.ops.dissolve_limit(
                bm,
                angle_limit=math.radians(angle_deg),
                use_dissolve_boundaries=False,
                verts=bm.verts[:],
                edges=bm.edges[:],
            )
            bmesh.ops.triangulate(bm, faces=bm.faces[:])
            if len(bm.faces) <= hard_max:
                break

    return bm


def _tiered_grid_position(index):
    """
    Arranges 100 rocks in a reference-inspired tiered composition across the 50x scale range.
    Foreground (Y < 0): tiny & small rocks with close spacing.
    Midground (Y 1 to 8): medium & large rocks with moderate spacing.
    Background (Y 11 to 17): very large & giant landmark rocks with wide heroic spacing.
    """
    if index < 10:
        # Tier 0, Row 0: Tiny rocks 001-010
        row, col = 0, index
        return Vector(((col - 4.5) * 0.35, -2.4, 0.0))
    elif index < 20:
        # Tier 0, Row 1: Tiny rocks 011-020
        row, col = 1, index - 10
        return Vector(((col - 4.5) * 0.35, -1.7, 0.0))
    elif index < 30:
        # Tier 1, Row 2: Small rocks 021-030
        row, col = 2, index - 20
        return Vector(((col - 4.5) * 0.75, -0.8, 0.0))
    elif index < 40:
        # Tier 1, Row 3: Small rocks 031-040
        row, col = 3, index - 30
        return Vector(((col - 4.5) * 0.75, 0.2, 0.0))
    elif index < 50:
        # Tier 2, Row 4: Medium rocks 041-050
        row, col = 4, index - 40
        return Vector(((col - 4.5) * 1.5, 1.5, 0.0))
    elif index < 60:
        # Tier 2, Row 5: Medium rocks 051-060
        row, col = 5, index - 50
        return Vector(((col - 4.5) * 1.5, 3.2, 0.0))
    elif index < 70:
        # Tier 3, Row 6: Large rocks 061-070
        row, col = 6, index - 60
        return Vector(((col - 4.5) * 2.6, 5.5, 0.0))
    elif index < 80:
        # Tier 3, Row 7: Large rocks 071-080
        row, col = 7, index - 70
        return Vector(((col - 4.5) * 2.6, 8.2, 0.0))
    elif index < 92:
        # Tier 4, Row 8: Very large rocks 081-092 (12 rocks)
        col = index - 80
        return Vector(((col - 5.5) * 3.6, 11.8, 0.0))
    else:
        # Tier 5, Row 9: Giant landmark boulders 093-100 (8 rocks)
        col = index - 92
        return Vector(((col - 3.5) * 5.5, 16.8, 0.0))


def generate_pack(pack_spec):
    collection_name = pack_spec["source"]["collection"]
    if bpy.data.collections.get(collection_name) is not None:
        raise RuntimeError(
            f"Collection '{collection_name}' already exists in live Blender data; refusing to overwrite unrelated work."
        )

    pack_collection = bpy.data.collections.new(collection_name)
    bpy.context.scene.collection.children.link(pack_collection)
    materials = _build_materials(pack_spec)
    generated = []

    for index, raw_asset in enumerate(pack_spec["assets"]):
        asset_spec = merge_asset_defaults(pack_spec, raw_asset)
        asset_id = asset_spec["id"]
        if bpy.data.objects.get(asset_id) is not None:
            raise RuntimeError(f"Object '{asset_id}' already exists in live Blender data.")

        recipe = asset_spec.get("generator_params")
        if not recipe or not isinstance(recipe, dict):
            raise ValueError(f"Asset '{asset_id}' is missing required 'generator_params'.")

        rng = random.Random(int(asset_spec["seed"]))
        bm = _build_asset_bmesh(asset_spec, recipe, rng)

        mesh = bpy.data.meshes.new(asset_id)
        bm.to_mesh(mesh)
        bm.free()

        obj = bpy.data.objects.new(asset_id, mesh)
        bpy.context.scene.collection.objects.link(obj)

        enforce_dimensions_and_origin(
            obj,
            asset_spec["dimensions_m"],
            exact_tol=float(asset_spec.get("exact_dimension_tolerance_m", 0.001)),
            origin_tol=float(asset_spec.get("origin_tolerance_m", 0.0001)),
        )
        ensure_flat_shading(obj)
        unwrap_smart_uvs(obj)

        obj["asset_pipeline_seed"] = int(asset_spec["seed"])
        obj["asset_pipeline_generator"] = __name__

        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        pack_collection.objects.link(obj)

        material_id = asset_spec.get("material_id")
        if material_id:
            assign_material(obj, materials[material_id])

        obj.location = _tiered_grid_position(index)
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj.scale = (1.0, 1.0, 1.0)

        triangles = count_triangles(obj.data)
        hard_max = int(asset_spec["hard_max_triangles"])
        if triangles > hard_max:
            raise RuntimeError(
                f"{asset_id} generated {triangles} triangles, exceeding hard maximum {hard_max}."
            )

        generated.append(obj)

    return generated
