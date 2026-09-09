"""
run_pack_pipeline.py
Master orchestrator for the 'low_poly_round_rocks' asset pack pipeline.
Can be run directly inside Blender or dispatched externally via the Blender MCP socket (127.0.0.1:9876).
"""

import os
import sys
import json

INSIDE_BLENDER = False
try:
    import bpy
    INSIDE_BLENDER = True
except ImportError:
    INSIDE_BLENDER = False


def execute_pipeline_in_blender():
    """Executes the complete asset pack pipeline inside the active Blender session."""
    pipeline_blender_dir = os.path.dirname(os.path.abspath(__file__))
    if pipeline_blender_dir not in sys.path:
        sys.path.insert(0, pipeline_blender_dir)

    repo_root = os.path.abspath(os.path.join(pipeline_blender_dir, "..", ".."))

    # Canonical paths
    source_blend_dir = os.path.join(repo_root, "ArtSource", "environment", "low_poly_round_rocks", "source")
    source_blend_path = os.path.join(source_blend_dir, "low_poly_round_rocks.blend")
    export_dir = os.path.join(repo_root, "Assets", "Art", "environment", "low_poly_round_rocks", "Models")
    renders_dir = os.path.join(repo_root, "ArtSource", "environment", "low_poly_round_rocks", "renders")
    report_json_path = os.path.join(repo_root, "ArtSource", "environment", "low_poly_round_rocks", "validation_report.json")
    report_md_path = os.path.join(repo_root, "ArtSource", "environment", "low_poly_round_rocks", "validation_report.md")

    os.makedirs(source_blend_dir, exist_ok=True)
    os.makedirs(export_dir, exist_ok=True)
    os.makedirs(renders_dir, exist_ok=True)

    print("==================================================")
    print("STARTING LOW-POLY ROUND ROCKS ASSET PIPELINE")
    print("==================================================")

    # 1. Clean existing scene objects & collections non-destructively
    scene = bpy.context.scene
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for col in list(bpy.data.collections):
        bpy.data.collections.remove(col, do_unlink=True)

    # Import and reload submodules
    import importlib
    import common.geometry as geom_mod
    import common.materials as mat_mod
    import common.export as export_mod
    import common.validation as val_mod
    import common.preview as prev_mod
    import generators.low_poly_round_rocks as gen_mod

    importlib.reload(geom_mod)
    importlib.reload(mat_mod)
    importlib.reload(export_mod)
    importlib.reload(val_mod)
    importlib.reload(prev_mod)
    importlib.reload(gen_mod)

    # 2. Procedural Generation & Inspection Grid
    print("\n[Step 1/5] Generating 10 Low-Poly Round Rocks...")
    rocks = gen_mod.generate_all_round_rocks(collection_name="Low_Poly_Round_Rocks")
    pack_col = bpy.data.collections.get("Low_Poly_Round_Rocks")
    print(f"Successfully generated {len(rocks)} rocks in collection 'Low_Poly_Round_Rocks'")

    # 3. Save initial authoritative source blend
    print(f"\n[Step 2/5] Saving source .blend to: {source_blend_path}")
    bpy.ops.wm.save_as_mainfile(filepath=source_blend_path)
    print(f"Saved: {os.path.exists(source_blend_path)} ({os.path.getsize(source_blend_path)} bytes)")

    # 4. Export individual GLBs (isolated at origin)
    print(f"\n[Step 3/5] Exporting individual GLBs to: {export_dir}")
    exported_glbs = []
    for rock_obj in rocks:
        glb_path = os.path.join(export_dir, f"{rock_obj.name}.glb")
        out = export_mod.export_isolated_glb(rock_obj, glb_path)
        exported_glbs.append(out)
        print(f"  Exported {rock_obj.name}.glb ({os.path.getsize(out)} bytes)")

    # 5. Clean-scene re-import validation
    print("\n[Step 4/5] Running clean-scene re-import validation...")
    val_results = []
    for cfg in gen_mod.ROCK_CONFIGS:
        glb_path = os.path.join(export_dir, f"{cfg['id']}.glb")
        res = val_mod.validate_glb_asset(glb_path, cfg)
        val_results.append(res)
        status_str = "PASS" if res["passed"] else "FAIL"
        tris = res.get("topology", {}).get("triangles", 0)
        print(f"  [{status_str}] {cfg['id']}: tris={tris}, file_size={res['file_size_bytes']} bytes")
        if not res["passed"]:
            print(f"    Failure reasons: {res['failure_reasons']}")

    all_passed = all(r["passed"] for r in val_results)

    # 6. Render individual previews & overview image
    print(f"\n[Step 5/5] Rendering previews to: {renders_dir}")
    rendered_previews = []
    for rock_obj in rocks:
        preview_path = os.path.join(renders_dir, f"{rock_obj.name}.png")
        p_out = prev_mod.render_asset_preview(rock_obj, preview_path, resolution=(960, 540))
        rendered_previews.append(p_out)
        print(f"  Rendered preview: {rock_obj.name}.png")

    overview_path = os.path.join(renders_dir, "low_poly_round_rocks_overview.png")
    prev_mod.render_overview(pack_col, overview_path, resolution=(1920, 1080))
    print(f"  Rendered overview: low_poly_round_rocks_overview.png")

    # Save final source blend file to keep preview camera & inspection environment
    bpy.ops.wm.save_as_mainfile(filepath=source_blend_path)

    # 7. Write validation reports
    pack_info = {
        "id": "low_poly_round_rocks",
        "display_name": "Low-Poly Round Rocks",
        "category": "environment",
        "source_blend": source_blend_path,
        "export_dir": export_dir,
        "renders_dir": renders_dir,
        "overview_render": overview_path
    }
    val_mod.write_validation_reports(val_results, report_json_path, report_md_path, pack_info)
    print(f"\nValidation reports generated:\n  JSON: {report_json_path}\n  Markdown: {report_md_path}")

    print("\n==================================================")
    print("PIPELINE EXECUTION COMPLETED")
    print(f"ALL VALIDATIONS PASSED: {all_passed}")
    print("==================================================")

    return {
        "status": "success" if all_passed else "failed_validation",
        "all_passed": all_passed,
        "source_blend": source_blend_path,
        "exported_glbs": exported_glbs,
        "rendered_previews": rendered_previews,
        "overview_render": overview_path,
        "validation_json": report_json_path,
        "validation_md": report_md_path,
        "validation_results": val_results
    }


def run_via_mcp(host="127.0.0.1", port=9876):
    """Dispatches the pipeline execution through the Blender MCP socket connection."""
    import socket
    pipeline_file = os.path.abspath(__file__).replace("\\", "/")

    code = f"""
import sys, os, json
pipeline_file = "{pipeline_file}"
pipeline_dir = os.path.dirname(pipeline_file)
if pipeline_dir not in sys.path:
    sys.path.insert(0, pipeline_dir)

import run_pack_pipeline
import importlib
importlib.reload(run_pack_pipeline)
result = run_pack_pipeline.execute_pipeline_in_blender()
print("PIPELINE_RESULT_JSON:" + json.dumps(result))
"""

    cmd = {"type": "execute_code", "params": {"code": code}}
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(300.0)
    s.connect((host, port))
    s.sendall(json.dumps(cmd).encode("utf-8"))

    chunks = []
    while True:
        chunk = s.recv(16384)
        if not chunk:
            break
        chunks.append(chunk)
        try:
            full_data = b"".join(chunks).decode("utf-8")
            res = json.loads(full_data)
            s.close()
            return res
        except json.JSONDecodeError:
            continue

    s.close()
    raise RuntimeError("Did not receive complete JSON response from Blender MCP")


if __name__ == "__main__":
    if INSIDE_BLENDER:
        execute_pipeline_in_blender()
    else:
        resp = run_via_mcp()
        print("MCP Result Summary:")
        if resp.get("status") == "success":
            print("Successfully executed pipeline in Blender!")
            out = resp.get("result", {}).get("result", "")
            for line in out.splitlines()[-25:]:
                print(line)
        else:
            print("Pipeline execution encountered an error:", resp)
