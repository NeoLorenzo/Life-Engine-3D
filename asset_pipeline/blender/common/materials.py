"""
Reusable material and UV utilities for the Life Engine 3D asset pipeline.
"""

from __future__ import annotations

import bpy


def get_or_create_pbr_material(mat_id, base_color=(0.32, 0.32, 0.33, 1.0), roughness=0.90, metallic=0.0):
    """Retrieve or create a simple Principled BSDF material."""
    mat = bpy.data.materials.get(mat_id)
    if mat is None:
        mat = bpy.data.materials.new(name=mat_id)
        mat.use_nodes = True

    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf is None:
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")

    if "Base Color" in bsdf.inputs:
        bsdf.inputs["Base Color"].default_value = base_color
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = roughness
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = metallic
    return mat


def assign_material(obj, mat):
    """Assign one primary material without accumulating duplicate slots."""
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def unwrap_smart_uvs(obj, angle_limit=66.0, island_margin=0.02):
    """Generate Smart UVs while restoring caller selection/active-object/mode."""
    view_layer = bpy.context.view_layer
    previous_active = view_layer.objects.active
    previous_selected = list(bpy.context.selected_objects)
    previous_mode = bpy.context.mode

    try:
        if previous_mode != "OBJECT" and previous_active is not None:
            bpy.ops.object.mode_set(mode="OBJECT")

        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        view_layer.objects.active = obj

        if not obj.data.uv_layers:
            obj.data.uv_layers.new(name="UVMap")

        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(
            angle_limit=angle_limit,
            margin_method="SCALED",
            rotate_method="AXIS_ALIGNED",
            island_margin=island_margin,
            area_weight=0.0,
            correct_aspect=True,
            scale_to_bounds=False,
        )
        bpy.ops.object.mode_set(mode="OBJECT")
    finally:
        if bpy.context.mode != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")

        bpy.ops.object.select_all(action="DESELECT")
        for selected in previous_selected:
            if selected.name in bpy.data.objects:
                selected.select_set(True)
        if previous_active and previous_active.name in bpy.data.objects:
            view_layer.objects.active = previous_active

        if previous_mode.startswith("EDIT") and previous_active and previous_active.name in bpy.data.objects:
            try:
                bpy.ops.object.mode_set(mode="EDIT")
            except RuntimeError:
                pass
