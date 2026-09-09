"""
preview.py
Reusable preview rendering utilities for the Life Engine 3D asset pipeline.
Sets up neutral inspection environments, renders individual asset previews,
and renders asset pack overview images.
"""

import os
import math
import bpy
from mathutils import Vector


def setup_inspection_environment():
    """
    Creates or configures a dedicated 'Inspection_Environment' collection containing a neutral
    studio lighting setup, soft shadow contact floor, and inspection camera.
    """
    scene = bpy.context.scene

    # Ambient World Lighting
    world = scene.world
    if world is None:
        world = bpy.data.worlds.new("Inspection_World")
        scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.75, 0.76, 0.78, 1.0)
        bg.inputs["Strength"].default_value = 0.50

    env_col = bpy.data.collections.get("Inspection_Environment")
    if env_col is None:
        env_col = bpy.data.collections.new("Inspection_Environment")
    if env_col.name not in scene.collection.children:
        scene.collection.children.link(env_col)

    # 1. Key Sun Light (from high front-left to emphasize facets and depth)
    key_light_obj = bpy.data.objects.get("Inspection_KeyLight")
    if key_light_obj is None:
        key_data = bpy.data.lights.new(name="Inspection_KeyLight", type='SUN')
        key_light_obj = bpy.data.objects.new(name="Inspection_KeyLight", object_data=key_data)
        env_col.objects.link(key_light_obj)

    key_light_obj.data.energy = 5.0
    key_light_obj.data.color = (1.0, 0.98, 0.95)
    key_light_obj.location = (-4.0, -5.0, 6.0)
    dir_key = Vector((0.0, 0.0, 0.2)) - key_light_obj.location
    key_light_obj.rotation_euler = dir_key.to_track_quat('-Z', 'Y').to_euler()

    # 2. Fill Sun Light (from front-right to soften shadows)
    fill_light_obj = bpy.data.objects.get("Inspection_FillLight")
    if fill_light_obj is None:
        fill_data = bpy.data.lights.new(name="Inspection_FillLight", type='SUN')
        fill_light_obj = bpy.data.objects.new(name="Inspection_FillLight", object_data=fill_data)
        env_col.objects.link(fill_light_obj)

    fill_light_obj.data.energy = 1.8
    fill_light_obj.data.color = (0.90, 0.94, 1.0)
    fill_light_obj.location = (5.0, -4.0, 4.5)
    dir_fill = Vector((0.0, 0.0, 0.2)) - fill_light_obj.location
    fill_light_obj.rotation_euler = dir_fill.to_track_quat('-Z', 'Y').to_euler()

    # 3. Soft Shadow Contact Floor
    floor_obj = bpy.data.objects.get("Inspection_Floor")
    if floor_obj is None:
        bpy.ops.mesh.primitive_plane_add(size=30.0, location=(0, 0, -0.0005))
        floor_obj = bpy.context.active_object
        floor_obj.name = "Inspection_Floor"
        for col in list(floor_obj.users_collection):
            col.objects.unlink(floor_obj)
        env_col.objects.link(floor_obj)

        floor_mat = bpy.data.materials.new(name="Inspection_FloorMat")
        floor_mat.use_nodes = True
        bsdf = floor_mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (0.78, 0.78, 0.79, 1.0)
            bsdf.inputs["Roughness"].default_value = 0.92
        floor_obj.data.materials.append(floor_mat)

    # 4. Inspection Camera
    cam_obj = bpy.data.objects.get("Inspection_Camera")
    if cam_obj is None:
        cam_data = bpy.data.cameras.new(name="Inspection_Camera")
        cam_obj = bpy.data.objects.new(name="Inspection_Camera", object_data=cam_data)
        env_col.objects.link(cam_obj)

    scene.camera = cam_obj
    return env_col, cam_obj


def render_asset_preview(obj, output_path, resolution=(960, 540)):
    """
    Renders an individual asset preview framed cleanly from a 3/4 elevated perspective.
    Places a temporary preview instance at world origin with camera focused squarely on it.
    """
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    norm_path = os.path.normpath(output_path)

    scene = bpy.context.scene
    env_col, cam_obj = setup_inspection_environment()

    # Calculate object bounding size
    mesh = obj.data
    xs = [v.co.x for v in mesh.vertices]
    ys = [v.co.y for v in mesh.vertices]
    zs = [v.co.z for v in mesh.vertices]
    w = max(xs) - min(xs)
    d = max(ys) - min(ys)
    h = max(zs) - min(zs)
    max_dim = max(w, d, h, 0.25)

    # Camera framing
    cam_obj.data.lens = 45.0
    dist = max_dim * 2.5
    cam_obj.location = Vector((dist * 0.707, -dist * 0.707, dist * 0.55 + h * 0.5))
    target = Vector((0.0, 0.0, h * 0.45))
    direction = target - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

    # Duplicate object temporarily to origin (0, 0, 0)
    temp_mesh = mesh.copy()
    temp_obj = bpy.data.objects.new(f"temp_prev_{obj.name}", temp_mesh)
    for mat in mesh.materials:
        temp_mesh.materials.append(mat)
    scene.collection.objects.link(temp_obj)
    temp_obj.location = (0, 0, 0)
    temp_obj.rotation_euler = (0, 0, 0)
    temp_obj.scale = (1, 1, 1)

    # Hide all pack collections during single asset render
    pack_collections = [c for c in scene.collection.children if c.name != "Inspection_Environment"]
    prev_hides = {c: c.hide_render for c in pack_collections}
    for c in pack_collections:
        c.hide_render = True

    scene.render.filepath = norm_path
    scene.render.resolution_x = resolution[0]
    scene.render.resolution_y = resolution[1]

    bpy.ops.render.render(write_still=True)

    # Restore visibility
    for c, hide in prev_hides.items():
        c.hide_render = hide

    # Clean up temporary preview object
    bpy.data.objects.remove(temp_obj, do_unlink=True)
    bpy.data.meshes.remove(temp_mesh, do_unlink=True)

    return norm_path


def render_overview(pack_collection, output_path, resolution=(1920, 1080)):
    """
    Renders an overview image of all assets arranged in their inspection grid.
    """
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    norm_path = os.path.normpath(output_path)

    scene = bpy.context.scene
    env_col, cam_obj = setup_inspection_environment()

    # Ensure pack collection is visible
    pack_collection.hide_render = False
    for o in pack_collection.objects:
        o.hide_render = False

    # Position camera to frame the 2x5 grid cleanly
    cam_obj.data.lens = 28.0
    cam_obj.location = Vector((0.0, -5.2, 3.2))
    target = Vector((0.0, 0.0, 0.25))
    direction = target - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

    scene.render.filepath = norm_path
    scene.render.resolution_x = resolution[0]
    scene.render.resolution_y = resolution[1]

    bpy.ops.render.render(write_still=True)
    return norm_path
