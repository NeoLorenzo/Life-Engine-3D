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

for machine-readable asset requirements such as IDs, dimensions, budgets, source/output paths, seeds, pivots, export settings, and validation settings.

The descriptive brief in this prompt is authoritative for what the asset should look like and how it should be interpreted. Do not duplicate the full visual description into the YAML specification.

If the specification conflicts with a pipeline default, the explicit asset specification wins. If it appears to conflict with a hard validation or safety rule, stop and report the conflict instead of silently ignoring either requirement.

## Objective

<DESCRIBE WHAT TO CREATE, WHY IT EXISTS, AND WHERE/HOW IT WILL BE USED IN LIFE ENGINE>

## Visual and construction brief

<DESCRIBE THE REQUIRED SHAPE, PROPORTIONS, STYLE, CONSTRUCTION, MATERIAL APPEARANCE, IMPORTANT FEATURES, AND ANY DETAILS THAT MUST OR MUST NOT BE PRESENT>

Use this section for the substantive asset description rather than encoding prose descriptions into the structured YAML template.

## Reference images

Use the following reference images as visual evidence for the requested assets:

<REFERENCE IMAGE PATHS / URLS / SUPPLIED ATTACHMENTS>

For each reference, determine which role it serves:

- **authoritative geometry / measurement reference** — use it to determine proportions, placement, shape, or construction;
- **appearance reference** — use it primarily for materials, colors, surface treatment, or style;
- **context reference** — use it to understand how the asset appears, is positioned, or relates to surrounding objects/environment.

If useful, record the role of each image in task notes or the completion report.

When supplied explicit measurements conflict with apparent proportions in an image, the explicit measurements are authoritative unless this task explicitly says otherwise.

Do not infer hidden geometry with false precision. If something is not visible, measured, or otherwise specified, make the simplest plausible construction consistent with the available evidence and record any meaningful uncertainty in the completion report.

Do not commit copyrighted reference imagery to the repository unless redistribution is appropriate. Use external links, local task inputs, or provenance/reference notes where required by the pipeline rules.

## Measurements and known facts

<LIST ANY IMPORTANT MEASUREMENTS, REAL-WORLD FACTS, OR RELATIONSHIPS THAT REQUIRE EXPLANATION BEYOND THE MACHINE-READABLE DIMENSIONS IN THE YAML>

## Asset-specific constraints

<LIST ANY TASK-SPECIFIC REQUIREMENTS, EXCLUSIONS, OR EXCEPTIONS>

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

## Pipeline tooling

Inspect `asset_pipeline/blender/` before implementing task-specific automation.

Reuse existing pipeline helpers wherever possible.

If required reusable functionality does not yet exist, implement it under `asset_pipeline/blender/` as part of this task rather than duplicating the same mechanics in an asset-specific scratch script.

Functionality that should normally become reusable includes:

- dimension enforcement and measurement;
- pivot/origin placement;
- transform normalization;
- triangle and vertex counting;
- topology checks;
- UV and material validation;
- isolated GLB export;
- clean re-import validation;
- preview rendering;
- structured validation reporting.

Asset-specific modelling or procedural generation logic may live under:

`asset_pipeline/blender/generators/`

Temporary scripts are acceptable during exploration, but any logic required to reproduce or maintain committed assets must be promoted into the repository before completion.

Do not build speculative abstractions merely because they might be useful later. Add reusable infrastructure when the current task actually needs it, and keep it general enough for subsequent asset runs to reuse.

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
- how supplied reference images were used where relevant;
- any meaningful visual/geometry uncertainty;
- any warnings or unresolved limitations.

Do not claim completion while any hard validation failure remains.
