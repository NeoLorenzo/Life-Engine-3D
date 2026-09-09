# Life Engine 3D Asset Generation Prompt Template

Use this template when assigning a 3D asset or asset-pack task to Antigravity, Codex, or another agent with Blender access.

Replace all `<PLACEHOLDERS>` before execution.

---

# Task: <ASSET OR PACK NAME>

Create or modify the 3D asset work defined by this task for **Life Engine 3D**.

## Mandatory pipeline

Before doing any modelling, read and follow:

- `asset_pipeline/README.md`
- `asset_pipeline/docs/MODELLING_RULES.md`
- `asset_pipeline/docs/ASSET_PACK_RULES.md`
- `asset_pipeline/docs/DIRECTORY_AND_NAMING_RULES.md`
- `asset_pipeline/docs/EXPORT_RULES.md`
- `asset_pipeline/docs/VALIDATION_RULES.md`
- `asset_pipeline/docs/MATERIAL_RULES.md`
- `asset_pipeline/docs/REFERENCE_AND_PROVENANCE_RULES.md`
- `asset_pipeline/docs/BLENDER_MCP_OPERATIONS.md`

Treat these files as requirements for this task. Do **not** modify the pipeline standards themselves unless explicitly instructed.

Use the structured specification at:

`<PATH TO ASSET PACK SPEC YAML>`

as the authoritative asset-specific requirements.

If the specification conflicts with a pipeline default, the explicit asset specification wins. If it appears to conflict with a hard validation or safety rule, stop and report the conflict instead of silently ignoring either requirement.

## Objective

<SHORT DESCRIPTION OF WHAT TO CREATE AND WHERE IT WILL BE USED>

## Source strategy

Before modelling, determine whether each requested asset should be:

- created from scratch;
- modelled from supplied measurements/references;
- sourced from an existing legally usable 3D model;
- sourced and modified;
- produced with a hybrid workflow.

Do not recreate standardized real-world products unnecessarily when a suitable legally usable model exists. Record provenance for any externally sourced geometry or textures.

## Modelling requirements

- Use meters.
- Enforce exact target dimensions after all geometry-changing operations.
- Use the fewest polygons necessary to preserve the intended visual result.
- There is no minimum triangle count unless the asset specification explicitly creates one.
- Respect preferred and hard polygon maxima.
- Keep topology clean and game-ready.
- Use the specified pivot/origin convention.
- Apply final rotation and scale unless explicitly overridden.
- Use valid UVs where required.
- Use simple runtime-compatible materials.
- Use deterministic seeds for procedural generation and record them.

## Asset-pack source structure

If this task is an asset pack, use **one authoritative `.blend` source file for the pack** unless the specification explicitly says otherwise.

Place final pack assets in a clearly named collection and arrange them in a clean inspection layout.

Do not create one `.blend` per variant merely because runtime assets export individually.

## Export

Export runtime assets according to the specification.

For individual GLBs exported from an inspection layout, use a temporary export context so inspection-grid transforms do not leak into runtime files.

Export only intended runtime content. Do not include cameras, lights, helpers, reference meshes, or unrelated pack assets.

## Validation

Do not consider an export complete because the file exists.

For every runtime export:

1. import it into a clean temporary Blender scene;
2. validate dimensions;
3. validate triangle count;
4. validate object count and identity;
5. validate location / rotation / scale;
6. validate the local pivot/origin convention;
7. validate UVs and materials;
8. check for unintended imported objects;
9. fix failures and rerun validation.

Generate machine-readable validation output and a concise Markdown report.

## Preview renders

Create consistent individual preview renders and, for packs, an overview render unless the specification explicitly disables them.

Previews should prioritize inspection clarity over cinematic presentation.

## Reusable tooling

Where practical, preserve reusable Blender generation, export, preview, and validation logic under:

`asset_pipeline/blender/`

Do not leave important reproducibility logic only in temporary agent scratch directories.

## Blender MCP behavior

Follow `asset_pipeline/docs/BLENDER_MCP_OPERATIONS.md` exactly.

If MCP becomes unavailable, use the bounded reconnect/restart procedure. Do not repair, reinstall, copy, or patch Blender or system runtime files.

## Completion report

At completion, report:

- files created/modified;
- source `.blend` path;
- runtime export paths;
- preview paths;
- validation report path;
- dimensions and triangle counts per asset;
- deterministic seeds where applicable;
- provenance for external assets;
- any warnings or unresolved limitations.

Do not claim completion while any hard validation failure remains.
