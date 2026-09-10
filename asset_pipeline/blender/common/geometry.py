"""
Reusable geometric utilities for the Life Engine 3D asset pipeline.
"""

from __future__ import annotations

import bmesh
from mathutils import Vector


def get_mesh_bounds(obj):
    """Return local-space mesh bounds as (min_corner, max_corner)."""
    mesh = obj.data
    if not mesh.vertices:
        return Vector((0.0, 0.0, 0.0)), Vector((0.0, 0.0, 0.0))

    xs = [vertex.co.x for vertex in mesh.vertices]
    ys = [vertex.co.y for vertex in mesh.vertices]
    zs = [vertex.co.z for vertex in mesh.vertices]
    return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))


def get_mesh_dimensions(obj):
    min_corner, max_corner = get_mesh_bounds(obj)
    return max_corner - min_corner


def enforce_dimensions_and_origin(obj, target_dims, exact_tol=0.001, origin_tol=0.0001):
    """
    Normalize mesh-local geometry to exact target dimensions and a bottom-center
    origin. Generators should establish intended proportions before this final
    normalization rather than using this function as their primary shape tool.
    """
    mesh = obj.data
    min_corner, max_corner = get_mesh_bounds(obj)

    center_x = (min_corner.x + max_corner.x) * 0.5
    center_y = (min_corner.y + max_corner.y) * 0.5
    bottom_z = min_corner.z

    current = (
        max_corner.x - min_corner.x,
        max_corner.y - min_corner.y,
        max_corner.z - min_corner.z,
    )
    if any(value <= 0 for value in current):
        raise ValueError(f"Cannot normalize zero-sized mesh '{obj.name}': {current}")

    target = tuple(float(value) for value in target_dims)
    scale = tuple(target[index] / current[index] for index in range(3))

    for vertex in mesh.vertices:
        vertex.co.x = (vertex.co.x - center_x) * scale[0]
        vertex.co.y = (vertex.co.y - center_y) * scale[1]
        vertex.co.z = (vertex.co.z - bottom_z) * scale[2]
    mesh.update()

    final_min, final_max = get_mesh_bounds(obj)
    actual = (
        final_max.x - final_min.x,
        final_max.y - final_min.y,
        final_max.z - final_min.z,
    )
    errors = tuple(abs(actual[index] - target[index]) for index in range(3))
    if max(errors) > float(exact_tol):
        raise ValueError(
            f"Dimension tolerance exceeded on {obj.name}: target={target}, "
            f"actual={actual}, errors={errors}"
        )

    final_center_x = (final_min.x + final_max.x) * 0.5
    final_center_y = (final_min.y + final_max.y) * 0.5
    if (
        abs(final_center_x) > float(origin_tol)
        or abs(final_center_y) > float(origin_tol)
        or abs(final_min.z) > float(origin_tol)
    ):
        raise ValueError(
            f"Origin alignment tolerance exceeded on {obj.name}: "
            f"center=({final_center_x}, {final_center_y}), min_z={final_min.z}"
        )


def count_triangles(mesh):
    """Return Blender's evaluated triangulation count for the mesh data."""
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def ensure_flat_shading(obj):
    for polygon in obj.data.polygons:
        polygon.use_smooth = False


def check_manifold(mesh, weld_distance=None):
    """Return (is_manifold, non_manifold_edge_count)."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    if weld_distance is not None:
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=float(weld_distance))
    non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()
    return non_manifold == 0, non_manifold
