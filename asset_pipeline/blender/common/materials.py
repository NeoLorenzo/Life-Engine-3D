"""
materials.py
Reusable material and UV utilities for the Life Engine 3D asset pipeline.
Creates clean glTF-compatible Principled BSDF materials and generates UV mappings.
"""

import bpy


def get_or_create_pbr_material(mat_id, base_color=(0.32, 0.32, 0.33, 1.0), roughness=0.90, metallic=0.0):
    """
    Retrieves or creates a glTF-compatible PBR Principled BSDF material.
    """
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
    """Assigns the specified material to the object."""
    if not obj.data.materials:
        obj.data.materials.append(mat)
    else:
        obj.data.materials[0] = mat


def unwrap_smart_uvs(obj, angle_limit=66.0, island_margin=0.02):
    """
    Generates a clean UV unwrap using Smart UV Project.
    """
    prev_active = bpy.context.view_layer.objects.active
    prev_mode = bpy.context.mode

    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)

    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")

    # Switch to edit mode to unwrap
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(
        angle_limit=angle_limit,
        margin_method='SCALED',
        rotate_method='AXIS_ALIGNED',
        island_margin=island_margin,
        area_weight=0.0,
        correct_aspect=True,
        scale_to_bounds=False
    )
    bpy.ops.object.mode_set(mode='OBJECT')

    if prev_active:
        bpy.context.view_layer.objects.active = prev_active
