"""
Reusable preview rendering utilities for the Life Engine 3D asset pipeline.
"""

from __future__ import annotations

import math
import os

import bpy
from mathutils import Vector


INSPECTION_NAMES = {
    "collection": "Inspection_Environment",
    "world": "Inspection_World",
    "key_light": "Inspection_KeyLight",
    "fill_light": "Inspection_FillLight",
    "floor": "Inspection_Floor",
    "floor_material": "Inspection_FloorMat",
    "camera": "Inspection_Camera",
}


def setup_inspection_environment():
    scene = bpy.context.scene
    world = scene.world
    if world is None:
        world = bpy.data.worlds.new(INSPECTION_NAMES["world"])
        scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background:
        background.inputs["Color"].default_value = (0.75, 0.76, 0.78, 1.0)
        background.inputs["Strength"].default_value = 0.50

    env_collection = bpy.data.collections.get(INSPECTION_NAMES["collection"])
    if env_collection is None:
        env_collection = bpy.data.collections.new(INSPECTION_NAMES["collection"])
    if env_collection.name not in scene.collection.children:
        scene.collection.children.link(env_collection)

    key = bpy.data.objects.get(INSPECTION_NAMES["key_light"])
    if key is None:
        data = bpy.data.lights.new(name=INSPECTION_NAMES["key_light"], type="SUN")
        key = bpy.data.objects.new(name=INSPECTION_NAMES["key_light"], object_data=data)
        env_collection.objects.link(key)
    key.data.energy = 5.0
    key.data.color = (1.0, 0.98, 0.95)
    key.location = (-4.0, -5.0, 6.0)
    key.rotation_euler = (Vector((0.0, 0.0, 0.2)) - key.location).to_track_quat("-Z", "Y").to_euler()

    fill = bpy.data.objects.get(INSPECTION_NAMES["fill_light"])
    if fill is None:
        data = bpy.data.lights.new(name=INSPECTION_NAMES["fill_light"], type="SUN")
        fill = bpy.data.objects.new(name=INSPECTION_NAMES["fill_light"], object_data=data)
        env_collection.objects.link(fill)
    fill.data.energy = 1.8
    fill.data.color = (0.90, 0.94, 1.0)
    fill.location = (5.0, -4.0, 4.5)
    fill.rotation_euler = (Vector((0.0, 0.0, 0.2)) - fill.location).to_track_quat("-Z", "Y").to_euler()

    floor = bpy.data.objects.get(INSPECTION_NAMES["floor"])
    if floor is None:
        bpy.ops.mesh.primitive_plane_add(size=30.0, location=(0.0, 0.0, -0.0005))
        floor = bpy.context.active_object
        floor.name = INSPECTION_NAMES["floor"]
        for collection in list(floor.users_collection):
            collection.objects.unlink(floor)
        env_collection.objects.link(floor)
        floor_material = bpy.data.materials.new(name=INSPECTION_NAMES["floor_material"])
        floor_material.use_nodes = True
        bsdf = floor_material.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (0.78, 0.78, 0.79, 1.0)
            bsdf.inputs["Roughness"].default_value = 0.92
        floor.data.materials.append(floor_material)

    camera = bpy.data.objects.get(INSPECTION_NAMES["camera"])
    if camera is None:
        camera_data = bpy.data.cameras.new(name=INSPECTION_NAMES["camera"])
        camera = bpy.data.objects.new(name=INSPECTION_NAMES["camera"], object_data=camera_data)
        env_collection.objects.link(camera)
    scene.camera = camera
    return env_collection, camera


def render_asset_preview(obj, output_path, resolution=(960, 540)):
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    output_path = os.path.abspath(output_path)
    scene = bpy.context.scene
    _, camera = setup_inspection_environment()

    min_x = min(v.co.x for v in obj.data.vertices)
    max_x = max(v.co.x for v in obj.data.vertices)
    min_y = min(v.co.y for v in obj.data.vertices)
    max_y = max(v.co.y for v in obj.data.vertices)
    min_z = min(v.co.z for v in obj.data.vertices)
    max_z = max(v.co.z for v in obj.data.vertices)
    width, depth, height = max_x-min_x, max_y-min_y, max_z-min_z
    max_dimension = max(width, depth, height, 0.25)

    camera.data.lens = 45.0
    distance = max_dimension * 2.5
    camera.location = Vector((distance*0.707, -distance*0.707, distance*0.55 + height*0.5))
    camera.rotation_euler = (Vector((0.0, 0.0, height*0.45)) - camera.location).to_track_quat("-Z", "Y").to_euler()

    temp_mesh = obj.data.copy()
    temp_mesh.name = f"__preview_{obj.name}_mesh"
    temp_obj = bpy.data.objects.new(f"__preview_{obj.name}", temp_mesh)
    scene.collection.objects.link(temp_obj)
    temp_obj.location = (0.0, 0.0, 0.0)
    temp_obj.rotation_euler = (0.0, 0.0, 0.0)
    temp_obj.scale = (1.0, 1.0, 1.0)

    pack_collections = [c for c in scene.collection.children if c.name != INSPECTION_NAMES["collection"]]
    previous_hides = {c: c.hide_render for c in pack_collections}
    previous_path = scene.render.filepath
    previous_resolution = (scene.render.resolution_x, scene.render.resolution_y)

    try:
        for collection in pack_collections:
            collection.hide_render = True
        temp_obj.hide_render = False
        scene.render.filepath = output_path
        scene.render.resolution_x = int(resolution[0])
        scene.render.resolution_y = int(resolution[1])
        bpy.ops.render.render(write_still=True)
    finally:
        for collection, hidden in previous_hides.items():
            collection.hide_render = hidden
        scene.render.filepath = previous_path
        scene.render.resolution_x, scene.render.resolution_y = previous_resolution
        if temp_obj.name in bpy.data.objects:
            bpy.data.objects.remove(temp_obj, do_unlink=True)
        if temp_mesh.name in bpy.data.meshes and temp_mesh.users == 0:
            bpy.data.meshes.remove(temp_mesh, do_unlink=True)
    return output_path


def render_overview(pack_collection, output_path, resolution=(1920, 1080)):
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    output_path = os.path.abspath(output_path)
    scene = bpy.context.scene
    _, camera = setup_inspection_environment()
    previous_collection_hide = pack_collection.hide_render
    previous_object_hides = {obj: obj.hide_render for obj in pack_collection.objects}
    previous_path = scene.render.filepath
    previous_resolution = (scene.render.resolution_x, scene.render.resolution_y)

    try:
        pack_collection.hide_render = False
        mesh_objs = [obj for obj in pack_collection.objects if obj.type == "MESH"]
        for obj in pack_collection.objects:
            obj.hide_render = False

        if mesh_objs:
            min_x = min(obj.location.x + min(v.co.x for v in obj.data.vertices) for obj in mesh_objs)
            max_x = max(obj.location.x + max(v.co.x for v in obj.data.vertices) for obj in mesh_objs)
            min_y = min(obj.location.y + min(v.co.y for v in obj.data.vertices) for obj in mesh_objs)
            max_y = max(obj.location.y + max(v.co.y for v in obj.data.vertices) for obj in mesh_objs)
            min_z = min(obj.location.z + min(v.co.z for v in obj.data.vertices) for obj in mesh_objs)
            max_z = max(obj.location.z + max(v.co.z for v in obj.data.vertices) for obj in mesh_objs)

            span_x = max_x - min_x
            span_y = max_y - min_y
            span_z = max_z - min_z
            center_x = (min_x + max_x) * 0.5
            center_y = (min_y + max_y) * 0.5
            center_z = (min_z + max_z) * 0.5

            # Dynamically adapt floor plane to cover the pack extent
            floor = bpy.data.objects.get(INSPECTION_NAMES["floor"])
            if floor is not None:
                floor_extent = max(40.0, max(span_x, span_y) * 3.0)
                floor.scale = (floor_extent / 30.0, floor_extent / 30.0, 1.0)
                floor.location = (center_x, center_y, -0.0005)

            # Frame camera so all assets fit comfortably in 16:9 view
            camera.data.lens = 32.0
            dist_x = (span_x * 0.5) / math.tan(math.radians(28.0))
            dist_y = (span_y * 0.5) / math.tan(math.radians(16.0))
            dist_z = (span_z * 0.5) / math.tan(math.radians(16.0))
            distance = max(dist_x * 1.15, dist_y * 1.35, dist_z * 1.5, 5.2)

            target_point = Vector((center_x, center_y + span_y * 0.08, center_z * 0.45))
            camera.location = Vector((
                center_x,
                center_y - distance * 0.90,
                center_z + distance * 0.48,
            ))
            camera.rotation_euler = (target_point - camera.location).to_track_quat("-Z", "Y").to_euler()
        else:
            camera.data.lens = 28.0
            camera.location = Vector((0.0, -5.2, 3.2))
            camera.rotation_euler = (Vector((0.0, 0.0, 0.25)) - camera.location).to_track_quat("-Z", "Y").to_euler()

        scene.render.filepath = output_path
        scene.render.resolution_x = int(resolution[0])
        scene.render.resolution_y = int(resolution[1])
        bpy.ops.render.render(write_still=True)
    finally:
        pack_collection.hide_render = previous_collection_hide
        for obj, hidden in previous_object_hides.items():
            obj.hide_render = hidden
        scene.render.filepath = previous_path
        scene.render.resolution_x, scene.render.resolution_y = previous_resolution
    return output_path
