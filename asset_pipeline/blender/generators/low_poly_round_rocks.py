"""
low_poly_round_rocks.py
Deterministic procedural generator for the 'low_poly_round_rocks' asset pack.
Generates 10 distinct, naturally irregular, rounded, visibly faceted low-poly rocks.
Adheres strictly to triangle budgets (preferred <= 250, hard max 400),
exact target dimensions (+-0.001 m), and bottom-center pivots.
"""

import math
import random
import bpy
import bmesh
from mathutils import Vector

# Import common utilities
from common.geometry import enforce_dimensions_and_origin, count_triangles, ensure_flat_shading
from common.materials import get_or_create_pbr_material, assign_material, unwrap_smart_uvs

# Authoritative specification of the 10 rocks
ROCK_CONFIGS = [
    {
        "id": "round_rock_01",
        "seed": 1001,
        "name": "Small Compact River Pebble",
        "dimensions_m": (0.32, 0.28, 0.22),
        "num_pts": 55,
        "roundness": 0.88,
        "asymmetry": (0.03, -0.03, 0.04),
        "flatness_bottom": 0.25,
        "num_cuts": 2,
        "cut_depth": 0.86,
        "mat_id": "mat_stone_grey_01",
        "grid_pos": (-2.2, 0.65, 0.0)
    },
    {
        "id": "round_rock_02",
        "seed": 1002,
        "name": "Small Flattened Skipping Stone",
        "dimensions_m": (0.38, 0.32, 0.19),
        "num_pts": 58,
        "roundness": 0.84,
        "asymmetry": (-0.02, 0.02, 0.02),
        "flatness_bottom": 0.55,
        "num_cuts": 3,
        "cut_depth": 0.84,
        "mat_id": "mat_stone_grey_02",
        "grid_pos": (-1.1, 0.65, 0.0)
    },
    {
        "id": "round_rock_03",
        "seed": 1003,
        "name": "Small Asymmetrical Field Stone",
        "dimensions_m": (0.44, 0.36, 0.26),
        "num_pts": 65,
        "roundness": 0.78,
        "asymmetry": (-0.06, 0.05, -0.04),
        "flatness_bottom": 0.32,
        "num_cuts": 3,
        "cut_depth": 0.82,
        "mat_id": "mat_stone_grey_03",
        "grid_pos": (0.0, 0.65, 0.0)
    },
    {
        "id": "round_rock_04",
        "seed": 1004,
        "name": "Medium Rounded Egg Rock",
        "dimensions_m": (0.50, 0.42, 0.34),
        "num_pts": 70,
        "roundness": 0.86,
        "asymmetry": (0.04, -0.04, 0.03),
        "flatness_bottom": 0.28,
        "num_cuts": 3,
        "cut_depth": 0.85,
        "mat_id": "mat_stone_grey_01",
        "grid_pos": (1.1, 0.65, 0.0)
    },
    {
        "id": "round_rock_05",
        "seed": 1005,
        "name": "Medium Chunky Ground Stone",
        "dimensions_m": (0.55, 0.46, 0.36),
        "num_pts": 75,
        "roundness": 0.76,
        "asymmetry": (0.05, 0.04, -0.05),
        "flatness_bottom": 0.36,
        "num_cuts": 4,
        "cut_depth": 0.80,
        "mat_id": "mat_stone_grey_04",
        "grid_pos": (2.2, 0.65, 0.0)
    },
    {
        "id": "round_rock_06",
        "seed": 1006,
        "name": "Medium Low-Profile Rounded Mound",
        "dimensions_m": (0.62, 0.50, 0.28),
        "num_pts": 75,
        "roundness": 0.82,
        "asymmetry": (-0.03, -0.04, 0.03),
        "flatness_bottom": 0.48,
        "num_cuts": 3,
        "cut_depth": 0.83,
        "mat_id": "mat_stone_grey_02",
        "grid_pos": (-2.6, -0.65, 0.0)
    },
    {
        "id": "round_rock_07",
        "seed": 1007,
        "name": "Broad Rounded Terrain Boulder",
        "dimensions_m": (0.70, 0.58, 0.40),
        "num_pts": 82,
        "roundness": 0.80,
        "asymmetry": (0.04, 0.05, 0.05),
        "flatness_bottom": 0.32,
        "num_cuts": 4,
        "cut_depth": 0.81,
        "mat_id": "mat_stone_grey_03",
        "grid_pos": (-1.3, -0.65, 0.0)
    },
    {
        "id": "round_rock_08",
        "seed": 1008,
        "name": "Chunky Asymmetrical Rounded Boulder",
        "dimensions_m": (0.76, 0.62, 0.46),
        "num_pts": 88,
        "roundness": 0.74,
        "asymmetry": (-0.05, 0.06, -0.05),
        "flatness_bottom": 0.38,
        "num_cuts": 4,
        "cut_depth": 0.79,
        "mat_id": "mat_stone_grey_04",
        "grid_pos": (0.0, -0.65, 0.0)
    },
    {
        "id": "round_rock_09",
        "seed": 1009,
        "name": "Large Rounded Field Boulder",
        "dimensions_m": (0.86, 0.72, 0.52),
        "num_pts": 95,
        "roundness": 0.78,
        "asymmetry": (0.05, -0.04, 0.05),
        "flatness_bottom": 0.34,
        "num_cuts": 5,
        "cut_depth": 0.80,
        "mat_id": "mat_stone_grey_01",
        "grid_pos": (1.4, -0.65, 0.0)
    },
    {
        "id": "round_rock_10",
        "seed": 1010,
        "name": "Substantial Rounded Landmark Boulder",
        "dimensions_m": (0.96, 0.80, 0.60),
        "num_pts": 105,
        "roundness": 0.82,
        "asymmetry": (-0.04, 0.04, 0.04),
        "flatness_bottom": 0.36,
        "num_cuts": 5,
        "cut_depth": 0.82,
        "mat_id": "mat_stone_grey_03",
        "grid_pos": (2.8, -0.65, 0.0)
    }
]

# Stone Material Palette
PALETTE = {
    "mat_stone_grey_01": {"color": (0.32, 0.33, 0.35, 1.0), "roughness": 0.88},
    "mat_stone_grey_02": {"color": (0.28, 0.29, 0.30, 1.0), "roughness": 0.90},
    "mat_stone_grey_03": {"color": (0.36, 0.35, 0.34, 1.0), "roughness": 0.87},
    "mat_stone_grey_04": {"color": (0.24, 0.25, 0.26, 1.0), "roughness": 0.92},
}


def build_stone_materials():
    """Builds all shared materials from the palette."""
    materials = {}
    for mat_id, params in PALETTE.items():
        materials[mat_id] = get_or_create_pbr_material(
            mat_id=mat_id,
            base_color=params["color"],
            roughness=params["roughness"]
        )
    return materials


def generate_round_rock_mesh(config):
    """
    Deterministically generates a single low-poly rounded rock mesh according to config.
    Uses Fibonacci point distribution on an ellipsoid deformed by low-frequency harmonics,
    projected planar facet cuts, and convex hull generation.
    Guarantees 100% 2-manifold watertight geometry.
    """
    seed = config["seed"]
    random.seed(seed)

    target_w, target_d, target_h = config["dimensions_m"]
    rx = target_w * 0.5
    ry = target_d * 0.5
    rz = target_h * 0.5

    num_pts = config.get("num_pts", 65)
    roundness = config.get("roundness", 0.8)
    asymmetry = config.get("asymmetry", (0, 0, 0))
    flatness_bottom = config.get("flatness_bottom", 0.3)
    num_cuts = config.get("num_cuts", 3)
    cut_depth = config.get("cut_depth", 0.82)

    # 1. Distribute points using golden-angle Fibonacci spiral on sphere
    phi = math.pi * (math.sqrt(5.0) - 1.0)
    pts = []

    for i in range(num_pts):
        y_norm = 1.0 - (i / float(num_pts - 1)) * 2.0
        radius_at_y = math.sqrt(max(0.0, 1.0 - y_norm * y_norm))
        theta = phi * i

        x_norm = math.cos(theta) * radius_at_y
        z_norm = math.sin(theta) * radius_at_y

        # Multi-frequency organic perturbation
        n1 = math.sin(x_norm * 2.5 + seed * 0.1) * math.cos(y_norm * 2.1)
        n2 = math.cos(z_norm * 2.8 + seed * 0.2) * math.sin(x_norm * 1.9)
        disp = 1.0 + (n1 * 0.10 + n2 * 0.07) * (1.0 - roundness * 0.5)

        # Base proportions directly controlled by generator
        vx = x_norm * rx * disp
        vy = y_norm * ry * disp
        vz = z_norm * rz * disp

        # Controlled organic asymmetry
        vx += asymmetry[0] * rx * (vz / rz if rz > 0 else 0)
        vy += asymmetry[1] * ry * (vx / rx if rx > 0 else 0)
        vz += asymmetry[2] * rz * (vx * vx / (rx * rx) if rx > 0 else 0)

        # Grounding bottom flattening
        if vz < 0:
            vz *= (1.0 - flatness_bottom * 0.40)

        pts.append(Vector((vx, vy, vz)))

    # 2. Projected planar cuts: project points outside each cut plane onto that plane.
    for _ in range(num_cuts):
        plane_normal = Vector((
            random.uniform(-1.0, 1.0),
            random.uniform(-1.0, 1.0),
            random.uniform(-0.35, 0.75)
        )).normalized()

        plane_pt = Vector((
            plane_normal.x * rx * cut_depth,
            plane_normal.y * ry * cut_depth,
            plane_normal.z * rz * cut_depth
        ))

        for i, pt in enumerate(pts):
            dist_to_plane = (pt - plane_pt).dot(plane_normal)
            if dist_to_plane > 0:
                pts[i] = pt - dist_to_plane * plane_normal

    # 3. Build convex hull in bmesh
    bm = bmesh.new()
    for pt in pts:
        bm.verts.new(pt)

    res = bmesh.ops.convex_hull(bm, input=bm.verts)
    unused_verts = [v for v in res['geom_unused'] if isinstance(v, bmesh.types.BMVert)]
    bmesh.ops.delete(bm, geom=unused_verts, context='VERTS')

    # Convert to mesh
    mesh = bpy.data.meshes.new(config["id"])
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(config["id"], mesh)
    bpy.context.scene.collection.objects.link(obj)

    # 4. Enforce bottom-center origin and exact dimensions (+-0.001 m)
    enforce_dimensions_and_origin(obj, config["dimensions_m"])

    # 5. Flat Shading
    ensure_flat_shading(obj)

    # 6. UV Unwrapping
    unwrap_smart_uvs(obj)

    return obj


def generate_all_round_rocks(collection_name="Low_Poly_Round_Rocks"):
    """
    Generates all 10 round rocks, places them in collection_name in an inspection grid,
    and assigns cohesive stone materials.
    """
    scene = bpy.context.scene

    # Setup target collection
    pack_col = bpy.data.collections.get(collection_name)
    if pack_col is None:
        pack_col = bpy.data.collections.new(collection_name)
    if pack_col.name not in scene.collection.children:
        scene.collection.children.link(pack_col)

    materials = build_stone_materials()
    generated_objects = []

    for cfg in ROCK_CONFIGS:
        # Remove any existing object with this ID
        existing = bpy.data.objects.get(cfg["id"])
        if existing:
            bpy.data.objects.remove(existing, do_unlink=True)

        obj = generate_round_rock_mesh(cfg)

        # Move to pack collection
        for col in list(obj.users_collection):
            col.objects.unlink(obj)
        pack_col.objects.link(obj)

        # Assign material
        mat = materials.get(cfg["mat_id"])
        if mat:
            assign_material(obj, mat)

        # Position in inspection grid
        grid_pos = cfg.get("grid_pos", (0.0, 0.0, 0.0))
        obj.location = Vector(grid_pos)
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj.scale = (1.0, 1.0, 1.0)

        tris = count_triangles(obj.data)
        verts = len(obj.data.vertices)
        print(f"Generated {cfg['id']}: tris={tris}, verts={verts}, grid_pos={grid_pos}")
        generated_objects.append(obj)

    return generated_objects
