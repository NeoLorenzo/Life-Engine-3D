# Asset Pack Rules

An asset pack is a set of related assets intended to be generated, inspected, maintained, and versioned together.

## One authoritative `.blend` per pack

Default rule: use **one source Blender file per asset pack**, not one `.blend` file per asset.

Example:

```text
ground_stones.blend
```

containing:

```text
Ground_Stones
├── stone_01
├── stone_02
├── stone_03
└── ...
```

Create independent `.blend` files only when the assets are genuinely separate source projects or the specification explicitly requires it.

## Collections

Every pack should have a clearly named top-level collection containing the final pack assets.

Inspection helpers, cameras, lights, reference meshes, and temporary generation objects should live outside the runtime asset collection or be clearly separated in subcollections.

## Inspection layout

The source `.blend` should make human review easy.

Arrange pack assets so they:

- do not overlap;
- are easy to compare;
- use consistent presentation;
- have enough spacing for silhouette inspection;
- preserve meaningful names.

A 2D grid is preferred for small and medium packs.

Inspection placement must not change the asset-local geometry or runtime pivot.

## Export isolation

Individual runtime assets may be exported separately from a shared source pack.

When source objects are positioned in an inspection grid, export through a temporary object or temporary export scene so the runtime object can be exported at the intended origin without destroying the source layout.

The source inspection file remains authoritative and should not be rearranged simply to accommodate exports.

## Preview requirements

Unless explicitly waived, every asset pack should include:

- one preview per asset;
- one overview render showing the whole pack.

Previews are inspection artifacts, not marketing renders. Favor consistent framing and readable form over cinematic presentation.

## Variation

When a pack contains multiple variants, variation must be meaningful.

Do not create apparent variety by merely changing scale, rotation, or tiny noise parameters on the same base mesh. Vary silhouette, proportions, topology, facet structure, materials, or construction method where appropriate while preserving pack cohesion.
