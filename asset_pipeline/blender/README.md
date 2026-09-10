# Blender Pipeline Automation

Reusable Blender automation for the Life Engine 3D asset pipeline belongs here.

Suggested organization:

```text
blender/
├── common/
│   ├── geometry.py
│   ├── materials.py
│   ├── export.py
│   ├── validation.py
│   ├── preview.py
│   └── spec.py
├── generators/
│   └── <pack_or_asset_generators>.py
└── run_pack_pipeline.py
```

This layout is illustrative, not a checklist. Add reusable modules only when a concrete asset task requires them.

## Structured source of truth

The task-specific asset-pack YAML is the structured source of truth for IDs, dimensions, seeds, budgets, material IDs, paths, export configuration, and validation configuration.

Do not duplicate those values into a generator module. Generator modules should contain only asset-specific modelling recipes or logic that cannot be expressed as the common structured specification.

`run_pack_pipeline.py` must load the YAML and pass the same normalized asset specification to generation, export, and validation.

## Generic runner contract

`run_pack_pipeline.py` is pack-agnostic. It reads `generator.module` and `generator.entrypoint` from the pack specification and invokes that generator dynamically.

Example:

```powershell
python asset_pipeline/blender/run_pack_pipeline.py ArtSource/environment/low_poly_round_rocks/low_poly_round_rocks_spec.yaml
```

The runner may dispatch execution through Blender MCP when run outside Blender.

## Scene safety

Never clear the user's active Blender file globally to prepare an asset run.

The pipeline must operate in an isolated scene/context, refuse destructive name collisions, and restore the user's original active scene after execution. The authoritative pack `.blend` should be written from the isolated pipeline scene rather than replacing the user's currently open `.blend` via `save_as_mainfile()`.

## Reuse rules

Before writing task-specific automation, inspect this directory and reuse existing helpers where appropriate.

If a current asset task requires generally reusable pipeline functionality that does not yet exist, implement it here as part of that task instead of duplicating the same mechanics in a pack-specific scratch script. Do not build speculative infrastructure merely because it might be useful later; let the common pipeline grow from concrete asset-generation requirements.

Prefer reusable functions for dimension enforcement, pivot placement, transform normalization, triangle and vertex counting, topology/UV/material validation, isolated FBX export, clean re-import validation, structured validation reporting, and preview rendering instead of rewriting those mechanics for every pack.

Asset-specific procedural modelling logic belongs under `generators/` when it must be preserved for reproducibility or maintenance.

Do not put machine-specific absolute user-profile paths into reusable scripts. Repository-relative paths and explicit configuration are preferred.

Temporary agent scratch scripts are acceptable during exploration, but any logic required to reproduce or maintain committed assets should be promoted here before the asset task is considered complete.
