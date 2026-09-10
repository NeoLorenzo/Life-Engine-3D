"""
Deterministic procedural generator for the low_poly_round_rocks asset pack.

Structured asset values (IDs, dimensions, seeds, budgets, material IDs) come
from the pack YAML. This module contains only rock-specific generation recipes
and visual material definitions.
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
    "mat_stone_grey_01": {"color": (0.32, 0.33, 0.35, 1.0), "roughness": 0.88},
    "mat_stone_grey_02": {"color": (0.28, 0.29, 0.30, 1.0), "roughness": 0.90},
    "mat_stone_grey_03": {"color": (0.36, 0.35, 0.34, 1.0), "roughness": 0.87},
    "mat_stone_grey_04": {"color": (0.24, 0.25, 0.26, 1.0), "roughness": 0.92},
}

REQUIRED_GENERATOR_PARAMS = (
    "num_pts",
    "roundness",
    "asymmetry",
    "flatness_bottom",
    "num_cuts",
    "cut_depth",
)


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


def _generate_round_rock_mesh(asset_spec, recipe):
    asset_id = asset_spec["id"]
    if not isinstance(recipe, dict):
        raise ValueError(f"Asset '{asset_id}' is missing required 'generator_params' mapping.")

    missing_params = [param for param in REQUIRED_GENERATOR_PARAMS if param not in recipe]
    if missing_params:
        raise ValueError(
            f"Asset '{asset_id}' generator_params is missing required parameters: {', '.join(missing_params)}"
        )

    rng = random.Random(int(asset_spec["seed"]))

    target_w, target_d, target_h = [float(v) for v in asset_spec["dimensions_m"]]
    rx = target_w * 0.5
    ry = target_d * 0.5
    rz = target_h * 0.5

    num_pts = int(recipe["num_pts"])
    roundness = float(recipe["roundness"])
    asymmetry = [float(v) for v in recipe["asymmetry"]]
    if len(asymmetry) != 3:
        raise ValueError(f"Asset '{asset_id}' asymmetry must contain exactly 3 components.")
    flatness_bottom = float(recipe["flatness_bottom"])
    num_cuts = int(recipe["num_cuts"])
    cut_depth = float(recipe["cut_depth"])

    phi = math.pi * (math.sqrt(5.0) - 1.0)
    points = []

    for i in range(num_pts):
        y_norm = 1.0 - (i / float(num_pts - 1)) * 2.0
        radius_at_y = math.sqrt(max(0.0, 1.0 - y_norm * y_norm))
        theta = phi * i

        x_norm = math.cos(theta) * radius_at_y
        z_norm = math.sin(theta) * radius_at_y

        n1 = math.sin(x_norm * 2.5 + asset_spec["seed"] * 0.1) * math.cos(y_norm * 2.1)
        n2 = math.cos(z_norm * 2.8 + asset_spec["seed"] * 0.2) * math.sin(x_norm * 1.9)
        displacement = 1.0 + (n1 * 0.10 + n2 * 0.07) * (1.0 - roundness * 0.5)

        vx = x_norm * rx * displacement
        vy = y_norm * ry * displacement
        vz = z_norm * rz * displacement

        vx += asymmetry[0] * rx * (vz / rz if rz else 0.0)
        vy += asymmetry[1] * ry * (vx / rx if rx else 0.0)
        vz += asymmetry[2] * rz * (vx * vx / (rx * rx) if rx else 0.0)

        if vz < 0:
            vz *= 1.0 - flatness_bottom * 0.40

        points.append(Vector((vx, vy, vz)))

    for _ in range(num_cuts):
        plane_normal = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(-0.35, 0.75))).normalized()
        plane_point = Vector((plane_normal.x * rx * cut_depth, plane_normal.y * ry * cut_depth, plane_normal.z * rz * cut_depth))

        for index, point in enumerate(points):
            distance = (point - plane_point).dot(plane_normal)
            if distance > 0:
                points[index] = point - distance * plane_normal

    bm = bmesh.new()
    for point in points:
        bm.verts.new(point)

    result = bmesh.ops.convex_hull(bm, input=bm.verts)
    unused = [element for element in result["geom_unused"] if isinstance(element, bmesh.types.BMVert)]
    if unused:
        bmesh.ops.delete(bm, geom=unused, context="VERTS")

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
    return obj


def _grid_position(index, count, max_width):
    columns = min(5, count)
    rows = int(math.ceil(count / float(columns)))
    column = index % columns
    row = index // columns
    x_spacing = max(0.9, max_width + 0.35)
    y_spacing = max(1.0, max_width + 0.45)
    return Vector(((column - (columns - 1) * 0.5) * x_spacing, ((rows - 1) * 0.5 - row) * y_spacing, 0.0))


def generate_pack(pack_spec):
    """Generate this pack from the YAML-backed structured specification."""
    collection_name = pack_spec["source"]["collection"]
    if bpy.data.collections.get(collection_name) is not None:
        raise RuntimeError(
            f"Collection '{collection_name}' already exists in live Blender data; refusing to overwrite unrelated work."
        )

    pack_collection = bpy.data.collections.new(collection_name)
    bpy.context.scene.collection.children.link(pack_collection)
    materials = _build_materials(pack_spec)
    generated = []
    max_width = max(float(asset["dimensions_m"][0]) for asset in pack_spec["assets"])

    for index, raw_asset in enumerate(pack_spec["assets"]):
        asset_spec = merge_asset_defaults(pack_spec, raw_asset)
        asset_id = asset_spec["id"]
        if bpy.data.objects.get(asset_id) is not None:
            raise RuntimeError(f"Object '{asset_id}' already exists in live Blender data; refusing to overwrite unrelated work.")
        recipe = asset_spec.get("generator_params")
        if not recipe or not isinstance(recipe, dict):
            raise ValueError(f"Asset '{asset_id}' is missing required 'generator_params' mapping in specification.")

        obj = _generate_round_rock_mesh(asset_spec, recipe)
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        pack_collection.objects.link(obj)

        material_id = asset_spec.get("material_id")
        if material_id:
            assign_material(obj, materials[material_id])

        obj.location = _grid_position(index, len(pack_spec["assets"]), max_width)
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj.scale = (1.0, 1.0, 1.0)

        triangles = count_triangles(obj.data)
        if triangles > int(asset_spec["hard_max_triangles"]):
            raise RuntimeError(
                f"{asset_id} generated {triangles} triangles, exceeding hard maximum {asset_spec['hard_max_triangles']}."
            )
        generated.append(obj)

    return generated
