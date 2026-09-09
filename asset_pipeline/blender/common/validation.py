"""
Reusable source/runtime validation and reporting for the Life Engine 3D asset pipeline.
"""

from __future__ import annotations

import datetime
import json
import os

import bmesh
import bpy

from common.geometry import get_mesh_bounds, count_triangles
from common.spec import merge_asset_defaults


def _identity_transform_result(obj, tolerance=1e-4):
    loc, rot, scale = obj.location, obj.rotation_euler, obj.scale
    loc_ok = max(abs(loc.x), abs(loc.y), abs(loc.z)) <= tolerance
    rot_ok = max(abs(rot.x), abs(rot.y), abs(rot.z)) <= tolerance
    scale_ok = max(abs(scale.x-1.0), abs(scale.y-1.0), abs(scale.z-1.0)) <= tolerance
    return {"location":[round(loc.x,6),round(loc.y,6),round(loc.z,6)],"rotation":[round(rot.x,6),round(rot.y,6),round(rot.z,6)],"scale":[round(scale.x,6),round(scale.y,6),round(scale.z,6)],"passed":loc_ok and rot_ok and scale_ok}


def _dimension_result(obj, target_dims, tolerance):
    min_c, max_c = get_mesh_bounds(obj)
    actual = (max_c.x-min_c.x, max_c.y-min_c.y, max_c.z-min_c.z)
    errors = tuple(abs(actual[i]-float(target_dims[i])) for i in range(3))
    return {"target":[float(v) for v in target_dims],"actual":[round(v,6) for v in actual],"errors":[round(v,6) for v in errors],"passed":max(errors)<=tolerance}


def _origin_result(obj, origin_mode, tolerance):
    min_c, max_c = get_mesh_bounds(obj)
    if origin_mode == "bottom_center":
        cx=(min_c.x+max_c.x)*0.5; cy=(min_c.y+max_c.y)*0.5; min_z=min_c.z
        return {"mode":origin_mode,"center_xy":[round(cx,6),round(cy,6)],"min_z":round(min_z,6),"passed":abs(cx)<=tolerance and abs(cy)<=tolerance and abs(min_z)<=tolerance}
    return {"mode":origin_mode,"passed":False,"reason":f"Automated origin validation is not implemented for origin mode '{origin_mode}'."}


def _topology_result(mesh, preferred_max, hard_max, weld_for_manifold=False):
    triangles = count_triangles(mesh)
    bm = bmesh.new(); bm.from_mesh(mesh)
    if weld_for_manifold:
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    non_manifold_edges = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()
    return {"vertices":len(mesh.vertices),"triangles":triangles,"preferred_max":int(preferred_max),"hard_max":int(hard_max),"within_preferred":triangles<=int(preferred_max),"within_hard_max":triangles<=int(hard_max),"non_manifold_edges":non_manifold_edges,"manifold":non_manifold_edges==0,"passed":triangles<=int(hard_max) and non_manifold_edges==0}


def _uv_result(mesh, required):
    present=len(mesh.uv_layers)>0
    return {"required":bool(required),"present":present,"layer_count":len(mesh.uv_layers),"passed":present if required else True}


def _material_result(obj, required):
    assigned=any(material is not None for material in obj.data.materials)
    names=[material.name for material in obj.data.materials if material is not None]
    return {"required":bool(required),"assigned":assigned,"names":names,"passed":assigned if required else True}


def validate_source_asset(obj, raw_asset_spec, pack_spec):
    spec=merge_asset_defaults(pack_spec, raw_asset_spec); failures=[]; warnings=[]
    report={"asset_id":spec["id"],"stage":"source","passed":False,"failure_reasons":failures,"warnings":warnings}
    if obj is None or obj.type != "MESH":
        failures.append("Expected one mesh object."); return report
    if obj.name != spec["id"]:
        failures.append(f"Object identity mismatch: expected '{spec['id']}', got '{obj.name}'.")

    dimensions=_dimension_result(obj,spec["dimensions_m"],float(spec.get("exact_dimension_tolerance_m",0.001))); report["dimensions"]=dimensions
    if not dimensions["passed"]: failures.append("Source dimensions exceed configured tolerance.")
    origin=_origin_result(obj,spec.get("origin","bottom_center"),float(spec.get("origin_tolerance_m",0.0001))); report["origin"]=origin
    if not origin["passed"]: failures.append("Source pivot/origin does not satisfy the configured convention.")
    topology=_topology_result(obj.data,spec["preferred_max_triangles"],spec["hard_max_triangles"],False); report["topology"]=topology
    if not topology["within_hard_max"]: failures.append(f"Triangle count {topology['triangles']} exceeds hard maximum {topology['hard_max']}.")
    elif not topology["within_preferred"]: warnings.append(f"Triangle count {topology['triangles']} exceeds preferred maximum {topology['preferred_max']}.")
    if not topology["manifold"]: failures.append(f"Source mesh contains {topology['non_manifold_edges']} non-manifold edges.")
    uv=_uv_result(obj.data,spec.get("uv_required",True)); report["uv"]=uv
    if not uv["passed"]: failures.append("Required UV layer is missing.")
    material=_material_result(obj,spec.get("material_required",True)); report["material"]=material
    if not material["passed"]: failures.append("Required material assignment is missing.")

    rot=obj.rotation_euler; scale=obj.scale
    transform_ok=max(abs(rot.x),abs(rot.y),abs(rot.z))<=1e-4 and max(abs(scale.x-1),abs(scale.y-1),abs(scale.z-1))<=1e-4
    report["source_transform"]={"inspection_location":[round(v,6) for v in obj.location],"rotation":[round(v,6) for v in rot],"scale":[round(v,6) for v in scale],"passed":transform_ok}
    if not transform_ok: failures.append("Source object rotation/scale must be normalized.")
    report["generation"]={"mode":spec.get("generation_mode"),"seed":spec.get("seed")}
    report["provenance"]=spec.get("provenance")
    report["passed"]=not failures
    return report


def validate_fbx_asset(fbx_path, raw_asset_spec, pack_spec):
    spec=merge_asset_defaults(pack_spec, raw_asset_spec); asset_id=spec["id"]; fbx_path=os.path.abspath(fbx_path); failures=[]; warnings=[]
    report={"asset_id":asset_id,"stage":"runtime_reimport","format":"fbx","runtime_path":fbx_path,"file_exists":os.path.exists(fbx_path),"file_size_bytes":os.path.getsize(fbx_path) if os.path.exists(fbx_path) else 0,"passed":False,"failure_reasons":failures,"warnings":warnings,"generation":{"mode":spec.get("generation_mode"),"seed":spec.get("seed")},"provenance":spec.get("provenance")}
    if not report["file_exists"] or report["file_size_bytes"]<=0:
        failures.append(f"Runtime FBX is missing or empty: {fbx_path}"); return report

    original_scene=bpy.context.scene
    before_objects=set(bpy.data.objects); before_meshes=set(bpy.data.meshes); before_materials=set(bpy.data.materials); before_images=set(bpy.data.images)
    temp_scene=bpy.data.scenes.new(name=f"__validate_{asset_id}"); bpy.context.window.scene=temp_scene
    try:
        bpy.ops.import_scene.fbx(filepath=fbx_path,use_manual_orientation=False,global_scale=1.0,bake_space_transform=False,use_custom_normals=True,use_image_search=False,use_anim=False,use_custom_props=False,axis_forward="-Z",axis_up="Y")
        imported_objects=list(temp_scene.objects); mesh_objects=[obj for obj in imported_objects if obj.type=="MESH"]; non_mesh_objects=[obj for obj in imported_objects if obj.type!="MESH"]
        report["object_inventory"]={"total":len(imported_objects),"mesh_count":len(mesh_objects),"non_mesh":[{"name":obj.name,"type":obj.type} for obj in non_mesh_objects],"passed":len(mesh_objects)==1 and len(non_mesh_objects)==0}
        if len(mesh_objects)!=1: failures.append(f"Expected exactly one mesh object, found {len(mesh_objects)}.")
        if non_mesh_objects: failures.append("Unexpected non-mesh objects were imported: "+", ".join(f"{obj.name} ({obj.type})" for obj in non_mesh_objects))
        if len(mesh_objects)!=1: return report
        obj=mesh_objects[0]

        source_name_collision=any(existing.name==asset_id for existing in before_objects)
        suffix_is_collision=source_name_collision and obj.name.startswith(asset_id+".") and obj.name[len(asset_id)+1:].isdigit()
        identity_ok=obj.name==asset_id or suffix_is_collision
        report["identity"]={"expected":asset_id,"actual":obj.name,"blender_name_collision_normalized":suffix_is_collision,"passed":identity_ok}
        if not identity_ok: failures.append(f"Imported object identity mismatch: expected '{asset_id}', got '{obj.name}'.")

        transforms=_identity_transform_result(obj); report["transforms"]=transforms
        if not transforms["passed"]: failures.append("Imported FBX object transforms are not identity.")
        dimensions=_dimension_result(obj,spec["dimensions_m"],float(spec.get("exact_dimension_tolerance_m",0.001))); report["dimensions"]=dimensions
        if not dimensions["passed"]: failures.append("Imported FBX dimensions exceed configured tolerance.")
        origin=_origin_result(obj,spec.get("origin","bottom_center"),float(spec.get("origin_tolerance_m",0.0001))); report["origin"]=origin
        if not origin["passed"]: failures.append("Imported FBX pivot/origin does not satisfy the configured convention.")
        topology=_topology_result(obj.data,spec["preferred_max_triangles"],spec["hard_max_triangles"],True); report["topology"]=topology
        if not topology["within_hard_max"]: failures.append(f"Triangle count {topology['triangles']} exceeds hard maximum {topology['hard_max']}.")
        elif not topology["within_preferred"]: warnings.append(f"Triangle count {topology['triangles']} exceeds preferred maximum {topology['preferred_max']}.")
        if not topology["manifold"]: failures.append(f"Imported FBX contains {topology['non_manifold_edges']} non-manifold edges after seam-vertex welding.")
        uv=_uv_result(obj.data,spec.get("uv_required",True)); report["uv"]=uv
        if not uv["passed"]: failures.append("Required UV layer did not survive FBX export/re-import.")
        material=_material_result(obj,spec.get("material_required",True)); report["material"]=material
        if not material["passed"]: failures.append("Required material assignment did not survive FBX export/re-import.")
        report["passed"]=not failures
        return report
    finally:
        bpy.context.window.scene=original_scene
        if temp_scene.name in bpy.data.scenes: bpy.data.scenes.remove(temp_scene,do_unlink=True)
        for obj in [value for value in bpy.data.objects if value not in before_objects]: bpy.data.objects.remove(obj,do_unlink=True)
        for mesh in [value for value in bpy.data.meshes if value not in before_meshes and value.users==0]: bpy.data.meshes.remove(mesh,do_unlink=True)
        for material in [value for value in bpy.data.materials if value not in before_materials and value.users==0]: bpy.data.materials.remove(material,do_unlink=True)
        for image in [value for value in bpy.data.images if value not in before_images and value.users==0]: bpy.data.images.remove(image,do_unlink=True)


def validate_runtime_asset(runtime_path, raw_asset_spec, pack_spec):
    format_name=str((pack_spec.get("export") or {}).get("format","fbx")).lower()
    if format_name!="fbx": raise ValueError(f"Unsupported runtime validation format: {format_name}")
    return validate_fbx_asset(runtime_path,raw_asset_spec,pack_spec)


def _relpath_for_report(path,repo_root):
    if not path: return None
    return os.path.relpath(os.path.abspath(path),os.path.abspath(repo_root)).replace("\\","/")


def write_validation_reports(source_results,runtime_results,json_path,md_path,pack_info,repo_root):
    os.makedirs(os.path.dirname(os.path.abspath(json_path)),exist_ok=True); os.makedirs(os.path.dirname(os.path.abspath(md_path)),exist_ok=True)
    all_source_passed=all(result.get("passed",False) for result in source_results); all_runtime_passed=all(result.get("passed",False) for result in runtime_results); blender_validation_passed=all_source_passed and all_runtime_passed
    serializable_runtime=[]
    for result in runtime_results:
        item=dict(result); item["runtime_path"]=_relpath_for_report(item.get("runtime_path"),repo_root); serializable_runtime.append(item)
    payload={"pack_info":pack_info,"source_results":source_results,"runtime_results":serializable_runtime,"all_source_passed":all_source_passed,"all_runtime_passed":all_runtime_passed,"blender_validation_passed":blender_validation_passed,"unity_validation_status":"PENDING","pipeline_complete":False,"generated_at":datetime.datetime.now(datetime.timezone.utc).isoformat()}
    with open(json_path,"w",encoding="utf-8") as handle: json.dump(payload,handle,indent=2)

    source_by_id={result["asset_id"]:result for result in source_results}; runtime_by_id={result["asset_id"]:result for result in serializable_runtime}; asset_ids=list(dict.fromkeys([*source_by_id.keys(),*runtime_by_id.keys()]))
    lines=[f"# {pack_info.get('display_name',pack_info.get('id','Asset Pack'))} Validation Report","","## Summary","",f"- Pack ID: `{pack_info.get('id','unknown')}`",f"- Runtime format: `{pack_info.get('runtime_format','unknown')}`",f"- Source file: `{pack_info.get('source_blend','unknown')}`",f"- Blender validation: **`{'PASS' if blender_validation_passed else 'FAIL'}`**",f"- Source validation: **`{'PASS' if all_source_passed else 'FAIL'}`**",f"- Runtime re-import validation: **`{'PASS' if all_runtime_passed else 'FAIL'}`**","- Unity validation: **`PENDING`**","- Full pipeline status: **`INCOMPLETE`** until the Unity validation report passes.","","## Asset results","","| Asset | Source | Runtime | Dimensions | Triangles | Origin | UV | Material |","|---|:---:|:---:|:---:|---:|:---:|:---:|:---:|"]
    for asset_id in asset_ids:
        source=source_by_id.get(asset_id,{}); runtime=runtime_by_id.get(asset_id,{}); dims=runtime.get("dimensions") or source.get("dimensions") or {}; topology=runtime.get("topology") or source.get("topology") or {}; origin=runtime.get("origin") or source.get("origin") or {}; uv=runtime.get("uv") or source.get("uv") or {}; material=runtime.get("material") or source.get("material") or {}
        lines.append(f"| `{asset_id}` | `{'PASS' if source.get('passed') else 'FAIL'}` | `{'PASS' if runtime.get('passed') else 'FAIL'}` | `{'PASS' if dims.get('passed') else 'FAIL'}` | `{topology.get('triangles','?')}` | `{'PASS' if origin.get('passed') else 'FAIL'}` | `{'PASS' if uv.get('passed') else 'FAIL'}` | `{'PASS' if material.get('passed') else 'FAIL'}` |")
    failures=[]; warnings=[]
    for result in [*source_results,*serializable_runtime]:
        failures.extend(f"**{result['asset_id']} ({result['stage']})**: {reason}" for reason in result.get("failure_reasons",[])); warnings.extend(f"**{result['asset_id']} ({result['stage']})**: {warning}" for warning in result.get("warnings",[]))
    lines.extend(["","## Hard validation failures",""]); lines.extend([f"- {failure}" for failure in failures] or ["- None."]); lines.extend(["","## Warnings",""]); lines.extend([f"- {warning}" for warning in warnings] or ["- None."])
    lines.extend(["","## Provenance",""])
    provenance_rows=[]
    for result in source_results:
        provenance=result.get("provenance"); generation=result.get("generation") or {}
        if provenance: provenance_rows.append(f"- `{result['asset_id']}`: `{json.dumps(provenance,sort_keys=True)}`")
        else: provenance_rows.append(f"- `{result['asset_id']}`: generation mode `{generation.get('mode')}`, seed `{generation.get('seed')}`; no external provenance record.")
    lines.extend(provenance_rows or ["- None."])
    with open(md_path,"w",encoding="utf-8") as handle: handle.write("\n".join(lines)+"\n")
    return payload


def write_unity_validation_manifest(pack_spec,output_path):
    output_dir=(pack_spec.get("export") or {})["output_directory"].rstrip("/\\"); assets=[]
    for raw_asset in pack_spec["assets"]:
        spec=merge_asset_defaults(pack_spec,raw_asset)
        assets.append({"id":spec["id"],"asset_path":f"{output_dir}/{spec['id']}.fbx".replace("\\","/"),"dimensions_blender_xyz_m":[float(v) for v in spec["dimensions_m"]],"origin":spec.get("origin","bottom_center"),"origin_tolerance_m":float(spec.get("origin_tolerance_m",0.0001)),"dimension_tolerance_m":float(spec.get("exact_dimension_tolerance_m",0.001)),"preferred_max_triangles":int(spec["preferred_max_triangles"]),"hard_max_triangles":int(spec["hard_max_triangles"]),"material_required":bool(spec.get("material_required",True))})
    payload={"pack_id":pack_spec["pack"]["id"],"runtime_format":"fbx","unity_report_path":pack_spec["validation"]["unity_report"].replace("\\","/"),"assets":assets}
    os.makedirs(os.path.dirname(os.path.abspath(output_path)),exist_ok=True)
    with open(output_path,"w",encoding="utf-8") as handle: json.dump(payload,handle,indent=2)
    return payload
