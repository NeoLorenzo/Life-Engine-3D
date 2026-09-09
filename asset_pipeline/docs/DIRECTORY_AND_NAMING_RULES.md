# Directory and Naming Rules

## Pipeline location

The canonical pipeline lives at:

```text
asset_pipeline/
```

Reusable Blender automation belongs under:

```text
asset_pipeline/blender/
```

Asset-specific procedural generators may live under:

```text
asset_pipeline/blender/generators/
```

when they are useful beyond a single transient run.

## Asset storage

Prefer separating editable art source from Unity runtime assets.

Recommended structure for new work:

```text
ArtSource/
└── <category>/
    └── <pack_id>/
        ├── source/
        │   └── <pack_id>.blend
        ├── renders/
        ├── references/
        └── provenance/

Assets/
└── Art/
    └── <category>/
        └── <pack_id>/
            ├── Models/
            ├── Materials/
            └── Prefabs/
```

This keeps `.blend` source files, reference images, and preview renders out of Unity's import pipeline while keeping runtime assets under `Assets/`.

Existing asset packs may retain their current structure until deliberately migrated. Do not silently move existing files during unrelated asset work.

## Naming

Use stable lowercase `snake_case` IDs for runtime asset identifiers and files unless a subsystem requires another convention.

Examples:

```text
stone_01
oak_log_02
herman_miller_aeron
```

Use readable PascalCase or Title_Case collection names in Blender where useful:

```text
Ground_Stones
Office_Furniture
```

## Pack naming

Each pack should have one stable `pack_id`, used consistently for:

- source filename;
- manifest/spec filename;
- overview render;
- output directory;
- validation report.

## Temporary files

Do not commit temporary export scenes, Blender recovery copies, scratch outputs, `.blend1` backups, or agent scratch files unless explicitly required.
