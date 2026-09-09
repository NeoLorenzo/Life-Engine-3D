"""
export.py
Reusable export utilities for the Life Engine 3D asset pipeline.
Exports individual runtime assets (GLB) from inspection layouts non-destructively.
"""

import os
import bpy


def export_isolated_glb(obj, output_path):
    """
    Exports a single object as an isolated glTF 2.0 binary (.glb) file.
    Non-destructively creates a temporary export object at world origin (0, 0, 0)
    so inspection grid positions are never baked into exported runtime assets.
    """
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    norm_path = os.path.normpath(output_path)

    # 1. Create temporary duplicate mesh and object
    temp_mesh = obj.data.copy()
    temp_mesh.name = f"temp_{obj.name}_mesh"
    temp_obj = bpy.data.objects.new(name=obj.name, object_data=temp_mesh)

    # Copy materials
    for mat in obj.data.materials:
        temp_mesh.materials.append(mat)

    bpy.context.scene.collection.objects.link(temp_obj)

    # Ensure export object is at world origin with standard transforms
    temp_obj.location = (0.0, 0.0, 0.0)
    temp_obj.rotation_euler = (0.0, 0.0, 0.0)
    temp_obj.scale = (1.0, 1.0, 1.0)

    # 2. Deselect all, select only temp_obj
    bpy.ops.object.select_all(action='DESELECT')
    temp_obj.select_set(True)
    bpy.context.view_layer.objects.active = temp_obj

    # 3. Perform glTF export
    bpy.ops.export_scene.gltf(
        filepath=norm_path,
        export_format='GLB',
        use_selection=True,
        export_apply=True,
        export_cameras=False,
        export_lights=False,
        export_extras=False,
        export_materials='EXPORT',
        export_yup=True
    )

    # 4. Clean up temporary export object
    bpy.data.objects.remove(temp_obj, do_unlink=True)
    bpy.data.meshes.remove(temp_mesh, do_unlink=True)

    if not os.path.exists(norm_path) or os.path.getsize(norm_path) == 0:
        raise RuntimeError(f"GLB export failed or created empty file: {norm_path}")

    return norm_path
