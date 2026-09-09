# Export Rules

## Runtime format

Default runtime mesh format: **glTF 2.0 binary (`.glb`)** unless Unity integration or an asset-specific constraint requires another format.

## Source vs runtime

The editable `.blend` is the source of truth for authored geometry. The exported `.glb` is the runtime artifact.

Do not assume that because the Blender source is correct, the exported GLB is also correct.

## Individual export from packs

For asset packs, keep assets together in one `.blend` source file but export individual runtime assets when that is useful for Unity placement, loading, prefabs, or variation.

If the source object has an inspection-grid location, do not export that world offset accidentally.

Preferred workflow:

1. duplicate the final source object into a temporary export context;
2. preserve mesh data, UVs, materials, dimensions, and pivot;
3. set the temporary export object's intended runtime transform;
4. export only the intended asset;
5. discard the temporary export object/context;
6. leave the source inspection layout unchanged.

## Export isolation

An individual runtime export must not include unintended:

- cameras;
- lights;
- reference objects;
- inspection helpers;
- other pack assets;
- temporary geometry.

## Transform expectations

Unless explicitly overridden, exported static props should re-import with:

```text
Location = (0, 0, 0)
Rotation = (0, 0, 0)
Scale    = (1, 1, 1)
```

The local mesh origin must still satisfy the asset's pivot convention.

## Materials and UVs

Use export-compatible PBR materials. Verify that required UVs and material assignments survive export.

## Export success is not completion

A successful export operation or non-zero file size is necessary but insufficient. Every runtime file must pass the clean re-import validation defined in `VALIDATION_RULES.md`.
