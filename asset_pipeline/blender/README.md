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

Prefer reusable functions for dimension enforcement, pivot placement, triangle counting, GLB export isolation, clean re-import validation, and preview rendering instead of rewriting those mechanics for every pack.

Do not put machine-specific absolute user-profile paths into reusable scripts. Repository-relative paths and explicit configuration are preferred.

Temporary agent scratch scripts should be promoted here when they are required to reproduce or maintain committed assets.
