# Blender Pipeline Automation

Reusable Blender automation for the Life Engine 3D asset pipeline belongs here.

Suggested organization:

```text
blender/
├── common/
│   ├── geometry.py
│   ├── transforms.py
│   ├── materials.py
│   ├── export.py
│   ├── validation.py
│   └── preview.py
└── generators/
    └── <pack_or_asset_generators>.py
```

Before writing task-specific automation, inspect this directory and reuse existing helpers where appropriate.

If a current asset task requires generally reusable pipeline functionality that does not yet exist, implement it here as part of that task instead of duplicating the same mechanics in a pack-specific scratch script. Do not build speculative infrastructure merely because it might be useful later; let the common pipeline grow from concrete asset-generation requirements.

Prefer reusable functions for dimension enforcement, pivot placement, transform normalization, triangle and vertex counting, topology/UV/material validation, GLB export isolation, clean re-import validation, structured validation reporting, and preview rendering instead of rewriting those mechanics for every pack.

Asset-specific procedural modelling logic belongs under `generators/` when it must be preserved for reproducibility or maintenance.

Do not put machine-specific absolute user-profile paths into reusable scripts. Repository-relative paths and explicit configuration are preferred.

Temporary agent scratch scripts are acceptable during exploration, but any logic required to reproduce or maintain committed assets should be promoted here before the asset task is considered complete.
