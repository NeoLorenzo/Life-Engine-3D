"""
geometry.py
Reusable geometric utilities for the Life Engine 3D asset pipeline.
Provides exact dimension enforcement, bottom-center origin alignment,
mesh statistics calculation, and topology verification.
"""

import math
import bpy
import bmesh
from mathutils import Vector


def get_mesh_bounds(obj):
    """
    Computes local bounding box min and max vectors from evaluated vertex coordinates.
    Assumes object scale is (1, 1, 1).
    """
    mesh = obj.data
    if not mesh.vertices:
        return Vector((0, 0, 0)), Vector((0, 0, 0))

    xs = [v.co.x for v in mesh.vertices]
    ys = [v.co.y for v in mesh.vertices]
    zs = [v.co.z for v in mesh.vertices]

    min_corner = Vector((min(xs), min(ys), min(zs)))
    max_corner = Vector((max(xs), max(ys), max(zs)))
    return min_corner, max_corner


def get_mesh_dimensions(obj):
    """Returns the (width, depth, height) of the mesh data in meters."""
    min_c, max_c = get_mesh_bounds(obj)
    return Vector((max_c.x - min_c.x, max_c.y - min_c.y, max_c.z - min_c.z))


def enforce_dimensions_and_origin(obj, target_dims, exact_tol=0.001, origin_tol=0.0001):
    """
    Translates vertices so the local pivot is bottom-center:
      (min_x + max_x) / 2 = 0
      (min_y + max_y) / 2 = 0
      min_z = 0
    Scales vertices independently per axis so actual dimensions match target_dims.
    Validates that final dimensions are within exact_tol of target_dims.
    """
    mesh = obj.data
    min_c, max_c = get_mesh_bounds(obj)

    cx = (min_c.x + max_c.x) * 0.5
    cy = (min_c.y + max_c.y) * 0.5
    cz = min_c.z

    cur_w = max_c.x - min_c.x
    cur_d = max_c.y - min_c.y
    cur_h = max_c.z - min_c.z

    target_w, target_d, target_h = target_dims

    sx = target_w / cur_w if cur_w > 0 else 1.0
    sy = target_d / cur_d if cur_d > 0 else 1.0
    sz = target_h / cur_h if cur_h > 0 else 1.0

    for v in mesh.vertices:
        v.co.x = (v.co.x - cx) * sx
        v.co.y = (v.co.y - cy) * sy
        v.co.z = (v.co.z - cz) * sz

    mesh.update()

    # Re-verify post-correction
    final_min, final_max = get_mesh_bounds(obj)
    final_w = final_max.x - final_min.x
    final_d = final_max.y - final_min.y
    final_h = final_max.z - final_min.z

    err_x = abs(final_w - target_w)
    err_y = abs(final_d - target_d)
    err_z = abs(final_h - target_h)

    if max(err_x, err_y, err_z) > exact_tol:
        raise ValueError(
            f"Dimension tolerance exceeded on {obj.name}: "
            f"targets={target_dims}, actuals=({final_w:.4f}, {final_d:.4f}, {final_h:.4f}), "
            f"errors=({err_x:.5f}, {err_y:.5f}, {err_z:.5f})"
        )

    final_cx = (final_min.x + final_max.x) * 0.5
    final_cy = (final_min.y + final_max.y) * 0.5
    final_cz = final_min.z

    if abs(final_cx) > origin_tol or abs(final_cy) > origin_tol or abs(final_cz) > origin_tol:
        raise ValueError(
            f"Origin alignment tolerance exceeded on {obj.name}: "
            f"center=({final_cx:.6f}, {final_cy:.6f}), min_z={final_cz:.6f}"
        )


def count_triangles(mesh):
    """Calculates true triangle count of the mesh."""
    return sum(len(p.vertices) - 2 for p in mesh.polygons)


def ensure_flat_shading(obj):
    """Ensures all polygons are flat shaded."""
    for p in obj.data.polygons:
        p.use_smooth = False


def check_manifold_and_clean(mesh):
    """
    Checks if the mesh topology is 2-manifold (watertight, no open edges, no non-manifold edges).
    Returns (is_manifold, non_manifold_count).
    """
    bm = bmesh.new()
    bm.from_mesh(mesh)
    non_manifold_count = 0
    for e in bm.edges:
        if not e.is_manifold:
            non_manifold_count += 1
    bm.free()
    return (non_manifold_count == 0, non_manifold_count)
