# Life Engine 3D Asset Pipeline

This directory defines the canonical workflow for creating, sourcing, modifying, validating, and exporting 3D assets for Life Engine.

All humans and agents working on 3D assets should follow these rules unless an asset specification explicitly overrides a default. Asset-specific requirements override defaults, but they do not silently override hard validation rules unless the specification says so explicitly.

## Canonical workflow

```text
Asset request
→ asset specification
→ reference / provenance decision
→ Blender source creation or modification
→ source validation
→ inspection layout + preview renders
→ runtime export
→ clean re-import validation
→ Unity/runtime validation when applicable
→ completion report
```

## Required reading

Before creating or modifying assets, read:

- `docs/MODELLING_RULES.md`
- `docs/ASSET_PACK_RULES.md`
- `docs/DIRECTORY_AND_NAMING_RULES.md`
- `docs/EXPORT_RULES.md`
- `docs/VALIDATION_RULES.md`
- `docs/MATERIAL_RULES.md`
- `docs/REFERENCE_AND_PROVENANCE_RULES.md`
- `docs/BLENDER_MCP_OPERATIONS.md`

Use `templates/ASSET_GENERATION_PROMPT.md` as the standard agent prompt and `templates/asset_pack_spec.yaml` as the preferred structured specification format.

## Core principles

1. **Physical dimensions are authoritative.** Exact target dimensions must be enforced after geometry-changing operations and validated numerically.
2. **Use the minimum geometry necessary.** There is no minimum polygon count. Geometry must justify itself through silhouette, deformation, shading, animation, or gameplay needs.
3. **One source file per asset pack by default.** Related assets should be easy to inspect together in one `.blend` file while still exporting as individual runtime assets where useful.
4. **Source and runtime assets are different concerns.** Preserve editable source assets, but validate the actual exported runtime files independently.
5. **Do not declare success because a file exists.** Runtime exports must be cleanly re-imported and validated.
6. **Prefer existing legitimate assets when appropriate.** Standard commercial objects, common products, and generic props should be sourced or adapted when a suitable legally usable model already exists.
7. **Record provenance.** Externally sourced geometry, textures, and references must have their source and licensing status recorded.
8. **Make procedural work reproducible.** Record seeds and generator parameters where randomness is used.
9. **Make packs easy to inspect.** Source files should contain a clean inspection layout and overview render.
10. **Tool failure is not authorization to repair applications or the OS.** Follow the bounded Blender MCP recovery procedure and stop if it fails.
11. **The structured pack specification is the machine-readable source of truth.** Do not duplicate IDs, dimensions, seeds, budgets, material IDs, or output paths in generator code.
12. **Do not destroy unrelated Blender state.** Pipeline runs must use isolated contexts and restore the user's original working scene.

## Standard completion criteria

An asset or pack is complete only when:

- source geometry satisfies the specification;
- dimensions and transforms pass validation;
- topology, UV, and material requirements pass;
- polygon budgets pass;
- runtime exports succeed;
- each runtime export passes clean-scene re-import validation;
- Unity/runtime validation passes when the exported asset is intended for Unity;
- requested previews exist;
- provenance is recorded where relevant;
- reusable generation/validation logic is preserved in the repository when practical;
- the final report contains no unresolved hard failures.

For Unity-facing FBX assets, a Blender-only PASS is not full completion. The pack remains incomplete until the Unity validation report passes.
