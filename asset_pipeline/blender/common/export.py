"""
Reusable FBX export utilities for the Life Engine 3D asset pipeline.
"""

from __future__ import annotations

import os
import uuid

import bpy


def export_isolated_fbx(obj, output_path):
    """Export one mesh object as an isolated Unity-facing FBX without moving source."""
    if obj is None or obj.type != "MESH":
        raise ValueError("export_isolated_fbx requires a mesh object.")

    output_path = os.path.abspath(output_path)
    if os.path.splitext(output_path)[1].lower() != ".fbx":
        raise ValueError(f"FBX output path must end in .fbx: {output_path}")
    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    scene = bpy.context.scene
    view_layer = bpy.context.view_layer
    previous_active = view_layer.objects.active
    previous_selected = list(bpy.context.selected_objects)

    token = uuid.uuid4().hex[:8]
    temp_collection = bpy.data.collections.new(f"__asset_export_{token}")
    scene.collection.children.link(temp_collection)

    temp_mesh = obj.data.copy()
    temp_mesh.name = f"__asset_export_mesh_{token}"
    # mesh.copy() already carries material slots; do not append them again.
    temp_obj = bpy.data.objects.new(obj.name, temp_mesh)
    temp_collection.objects.link(temp_obj)
    temp_obj.location = (0.0, 0.0, 0.0)
    temp_obj.rotation_euler = (0.0, 0.0, 0.0)
    temp_obj.scale = (1.0, 1.0, 1.0)

    try:
        bpy.ops.object.select_all(action="DESELECT")
        temp_obj.select_set(True)
        view_layer.objects.active = temp_obj

        bpy.ops.export_scene.fbx(
            filepath=output_path,
            use_selection=True,
            object_types={"MESH"},
            global_scale=1.0,
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS",
            use_space_transform=True,
            bake_space_transform=False,
            axis_forward="-Z",
            axis_up="Y",
            add_leaf_bones=False,
            bake_anim=False,
            use_mesh_modifiers=True,
            mesh_smooth_type="OFF",
            use_triangles=False,
            use_custom_props=False,
            path_mode="AUTO",
        )
    finally:
        if temp_obj.name in bpy.data.objects:
            bpy.data.objects.remove(temp_obj, do_unlink=True)
        if temp_mesh.name in bpy.data.meshes and temp_mesh.users == 0:
            bpy.data.meshes.remove(temp_mesh, do_unlink=True)
        if temp_collection.name in bpy.data.collections:
            bpy.data.collections.remove(temp_collection, do_unlink=True)

        bpy.ops.object.select_all(action="DESELECT")
        for selected in previous_selected:
            if selected.name in bpy.data.objects:
                selected.select_set(True)
        if previous_active and previous_active.name in bpy.data.objects:
            view_layer.objects.active = previous_active

    if not os.path.exists(output_path) or os.path.getsize(output_path) == 0:
        raise RuntimeError(f"FBX export failed or created an empty file: {output_path}")
    return output_path


def export_runtime_asset(obj, output_path, format_name="fbx"):
    format_name = str(format_name).lower()
    if format_name != "fbx":
        raise ValueError(f"Unsupported runtime export format: {format_name}")
    return export_isolated_fbx(obj, output_path)
