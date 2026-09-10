"""
Generic Life Engine 3D asset-pack pipeline runner.

The pack YAML is the structured source of truth. The runner dynamically loads
the configured generator, operates in an isolated Blender scene, exports FBX,
performs clean re-import validation, writes reports/manifests, and restores the
user's original Blender scene without deleting unrelated work.
"""

from __future__ import annotations

import argparse
import importlib
import json
import os
import socket
import sys

PIPELINE_BLENDER_DIR=os.path.dirname(os.path.abspath(__file__))
REPO_ROOT=os.path.abspath(os.path.join(PIPELINE_BLENDER_DIR,"..",".."))
if PIPELINE_BLENDER_DIR not in sys.path: sys.path.insert(0,PIPELINE_BLENDER_DIR)
from common.spec import load_pack_spec, merge_asset_defaults, resolve_repo_path, validate_pack_spec
try:
    import bpy
except ImportError:
    bpy=None


def _capture_blender_datablocks():
    return {"objects":set(bpy.data.objects),"meshes":set(bpy.data.meshes),"materials":set(bpy.data.materials),"collections":set(bpy.data.collections),"worlds":set(bpy.data.worlds),"lights":set(bpy.data.lights),"cameras":set(bpy.data.cameras)}


def _remove_new_datablocks(before):
    for value in [item for item in bpy.data.objects if item not in before["objects"]]: bpy.data.objects.remove(value,do_unlink=True)
    for value in [item for item in bpy.data.collections if item not in before["collections"]]: bpy.data.collections.remove(value,do_unlink=True)
    for collection_name in ("meshes","materials","worlds","lights","cameras"):
        collection=getattr(bpy.data,collection_name)
        for value in [item for item in collection if item not in before[collection_name] and item.users==0]: collection.remove(value)


def _assert_no_live_name_collisions(pack_spec):
    from common.preview import INSPECTION_NAMES
    object_names={asset["id"] for asset in pack_spec["assets"]}; object_names.update({INSPECTION_NAMES["key_light"],INSPECTION_NAMES["fill_light"],INSPECTION_NAMES["floor"],INSPECTION_NAMES["camera"]})
    collection_names={pack_spec["source"]["collection"],INSPECTION_NAMES["collection"]}
    material_names={merge_asset_defaults(pack_spec,asset).get("material_id") for asset in pack_spec["assets"] if merge_asset_defaults(pack_spec,asset).get("material_id")}; material_names.add(INSPECTION_NAMES["floor_material"])
    conflicts=[]
    conflicts.extend(f"object:{name}" for name in object_names if bpy.data.objects.get(name)); conflicts.extend(f"collection:{name}" for name in collection_names if bpy.data.collections.get(name)); conflicts.extend(f"material:{name}" for name in material_names if bpy.data.materials.get(name))
    if bpy.data.worlds.get(INSPECTION_NAMES["world"]): conflicts.append(f"world:{INSPECTION_NAMES['world']}")
    if conflicts: raise RuntimeError("Asset pipeline refused to overwrite existing live Blender datablocks: "+", ".join(sorted(conflicts)))


def _write_source_blend_non_destructively(work_scene,source_blend_path):
    os.makedirs(os.path.dirname(source_blend_path),exist_ok=True); temp_path=source_blend_path+".asset_pipeline_tmp.blend"
    if os.path.exists(temp_path): os.remove(temp_path)
    bpy.data.libraries.write(temp_path,{work_scene},fake_user=True,compress=True); os.replace(temp_path,source_blend_path)
    if not os.path.exists(source_blend_path) or os.path.getsize(source_blend_path)<=0: raise RuntimeError(f"Failed to write authoritative source blend: {source_blend_path}")


def _pack_paths(pack_spec,repo_root):
    validation=pack_spec["validation"]
    return {"source_blend":resolve_repo_path(repo_root,pack_spec["source"]["blend_path"]),"export_dir":resolve_repo_path(repo_root,pack_spec["export"]["output_directory"]),"json_report":resolve_repo_path(repo_root,validation["json_report"]),"markdown_report":resolve_repo_path(repo_root,validation["markdown_report"]),"unity_manifest":resolve_repo_path(repo_root,validation["unity_manifest"]),"unity_report":resolve_repo_path(repo_root,validation["unity_report"])}


def execute_pipeline_in_blender(pack_spec,repo_root=REPO_ROOT):
    if bpy is None: raise RuntimeError("execute_pipeline_in_blender must run inside Blender.")
    validate_pack_spec(pack_spec); paths=_pack_paths(pack_spec,repo_root); os.makedirs(paths["export_dir"],exist_ok=True)
    pack=pack_spec["pack"]; export_config=pack_spec["export"]; inspection=pack_spec.get("inspection") or {}; runtime_format=export_config["format"].lower()
    if runtime_format!="fbx": raise RuntimeError(f"Unsupported runtime format: {runtime_format}")
    generator_config=pack_spec.get("generator") or {}; generator_module_name=generator_config.get("module"); generator_entrypoint=generator_config.get("entrypoint")
    if not generator_module_name or not generator_entrypoint: raise RuntimeError("Pack spec must configure generator.module and generator.entrypoint.")

    from common import export as export_mod
    from common import preview as preview_mod
    from common import validation as validation_mod
    importlib.reload(export_mod); importlib.reload(preview_mod); importlib.reload(validation_mod)
    generator_module=importlib.import_module(generator_module_name); importlib.reload(generator_module); generator=getattr(generator_module,generator_entrypoint)

    original_scene=bpy.context.scene
    if bpy.context.window is None: raise RuntimeError("Asset pipeline currently requires an active Blender window/context.")
    _assert_no_live_name_collisions(pack_spec); before=_capture_blender_datablocks(); work_scene=bpy.data.scenes.new(name=f"AssetPipeline_{pack['id']}"); bpy.context.window.scene=work_scene
    source_results=[]; runtime_results=[]; exported_paths=[]; preview_paths=[]; overview_path=None
    try:
        generated_objects=list(generator(pack_spec)); expected_ids=[asset["id"] for asset in pack_spec["assets"]]; generated_by_id={obj.name:obj for obj in generated_objects}
        if len(generated_objects)!=len(expected_ids) or set(generated_by_id)!=set(expected_ids): raise RuntimeError(f"Generator output does not match pack specification. Expected {expected_ids}, got {sorted(generated_by_id)}.")
        pack_collection=bpy.data.collections.get(pack_spec["source"]["collection"])
        if pack_collection is None: raise RuntimeError(f"Generator did not create expected collection '{pack_spec['source']['collection']}'.")

        for raw_asset in pack_spec["assets"]: source_results.append(validation_mod.validate_source_asset(generated_by_id[raw_asset["id"]],raw_asset,pack_spec))
        source_passed=all(result["passed"] for result in source_results)
        renders_dir=os.path.join(os.path.dirname(os.path.dirname(paths["source_blend"])),"renders"); os.makedirs(renders_dir,exist_ok=True)
        if inspection.get("individual_previews",True):
            for asset_id in expected_ids:
                preview_path=os.path.join(renders_dir,f"{asset_id}.png"); preview_mod.render_asset_preview(generated_by_id[asset_id],preview_path); preview_paths.append(preview_path)
        if inspection.get("overview_preview",True):
            overview_path=os.path.join(renders_dir,f"{pack['id']}_overview.png"); preview_mod.render_overview(pack_collection,overview_path)

        _write_source_blend_non_destructively(work_scene,paths["source_blend"])
        if source_passed:
            if os.path.exists(paths["unity_report"]): os.remove(paths["unity_report"])
            for raw_asset in pack_spec["assets"]:
                for stale_extension in (".glb",".gltf"):
                    stale_path=os.path.join(paths["export_dir"],f"{raw_asset['id']}{stale_extension}")
                    if os.path.exists(stale_path): os.remove(stale_path)
            for raw_asset in pack_spec["assets"]:
                asset_id=raw_asset["id"]; runtime_path=os.path.join(paths["export_dir"],f"{asset_id}.fbx"); exported_paths.append(export_mod.export_runtime_asset(generated_by_id[asset_id],runtime_path,runtime_format))
            if pack_spec["validation"].get("clean_reimport",True):
                for raw_asset in pack_spec["assets"]:
                    runtime_path=os.path.join(paths["export_dir"],f"{raw_asset['id']}.fbx"); runtime_results.append(validation_mod.validate_runtime_asset(runtime_path,raw_asset,pack_spec))
        else:
            runtime_results=[{"asset_id":raw_asset["id"],"stage":"runtime_reimport","format":runtime_format,"passed":False,"failure_reasons":["Runtime export skipped because source validation failed."],"warnings":[]} for raw_asset in pack_spec["assets"]]

        validation_mod.write_unity_validation_manifest(pack_spec,paths["unity_manifest"])
        pack_info={"id":pack["id"],"display_name":pack.get("display_name",pack["id"]),"category":pack.get("category"),"runtime_format":runtime_format,"source_blend":os.path.relpath(paths["source_blend"],repo_root).replace("\\","/"),"export_dir":os.path.relpath(paths["export_dir"],repo_root).replace("\\","/"),"overview_render":os.path.relpath(overview_path,repo_root).replace("\\","/") if overview_path else None,"unity_manifest":os.path.relpath(paths["unity_manifest"],repo_root).replace("\\","/"),"unity_report":os.path.relpath(paths["unity_report"],repo_root).replace("\\","/")}
        report_payload=validation_mod.write_validation_reports(source_results,runtime_results,paths["json_report"],paths["markdown_report"],pack_info,repo_root)
        blender_passed=bool(report_payload["blender_validation_passed"]); status="unity_validation_pending" if blender_passed else "failed_validation"
        return {"status":status,"blender_validation_passed":blender_passed,"pipeline_complete":False,"pack_id":pack["id"],"runtime_format":runtime_format,"source_blend":pack_info["source_blend"],"exported_assets":[os.path.relpath(path,repo_root).replace("\\","/") for path in exported_paths],"previews":[os.path.relpath(path,repo_root).replace("\\","/") for path in preview_paths],"overview_render":pack_info["overview_render"],"validation_json":os.path.relpath(paths["json_report"],repo_root).replace("\\","/"),"validation_markdown":os.path.relpath(paths["markdown_report"],repo_root).replace("\\","/"),"unity_manifest":pack_info["unity_manifest"],"unity_report_expected":pack_info["unity_report"]}
    finally:
        bpy.context.window.scene=original_scene
        if work_scene.name in bpy.data.scenes: bpy.data.scenes.remove(work_scene,do_unlink=True)
        _remove_new_datablocks(before)


def run_via_mcp(spec_path,host="127.0.0.1",port=9876):
    spec_path=os.path.abspath(spec_path); pack_spec=load_pack_spec(spec_path); repo_root=REPO_ROOT; serialized_spec=json.dumps(pack_spec); serialized_root=json.dumps(repo_root)
    code=f'''\nimport importlib, json, os, sys\npipeline_dir = {json.dumps(PIPELINE_BLENDER_DIR)}\nif pipeline_dir in sys.path: sys.path.remove(pipeline_dir)\nsys.path.insert(0, pipeline_dir)\nif "run_pack_pipeline" in sys.modules and getattr(sys.modules["run_pack_pipeline"], "__file__", "") != os.path.join(pipeline_dir, "run_pack_pipeline.py"): del sys.modules["run_pack_pipeline"]\nimport run_pack_pipeline\nimportlib.reload(run_pack_pipeline)\npack_spec = json.loads({json.dumps(serialized_spec)})\nrepo_root = json.loads({json.dumps(serialized_root)})\nresult = run_pack_pipeline.execute_pipeline_in_blender(pack_spec, repo_root)\nprint("PIPELINE_RESULT_JSON:" + json.dumps(result))\n'''
    command={"type":"execute_code","params":{"code":code}}
    with socket.socket(socket.AF_INET,socket.SOCK_STREAM) as connection:
        connection.settimeout(900.0); connection.connect((host,int(port))); connection.sendall(json.dumps(command).encode("utf-8")); chunks=[]
        while True:
            chunk=connection.recv(16384)
            if not chunk: break
            chunks.append(chunk)
            try: return json.loads(b"".join(chunks).decode("utf-8"))
            except json.JSONDecodeError: continue
    raise RuntimeError("Did not receive a complete JSON response from Blender MCP.")


def _parse_args():
    parser=argparse.ArgumentParser(description="Run a Life Engine asset-pack pipeline."); parser.add_argument("spec",help="Repository-relative or absolute path to pack YAML."); parser.add_argument("--host",default="127.0.0.1"); parser.add_argument("--port",type=int,default=9876); return parser.parse_args()


if __name__=="__main__":
    args=_parse_args(); spec_path=args.spec
    if not os.path.isabs(spec_path): spec_path=os.path.join(REPO_ROOT,spec_path)
    if bpy is not None: print(json.dumps(execute_pipeline_in_blender(load_pack_spec(spec_path),REPO_ROOT),indent=2))
    else: print(json.dumps(run_via_mcp(spec_path,args.host,args.port),indent=2))
