# Export Rules

## Runtime format

Default runtime mesh format: **FBX (`.fbx`)**.

FBX is the standard runtime mesh artifact for Unity-facing static and skinned assets unless an asset-specific requirement explicitly selects another supported format.

## Source vs runtime

The editable `.blend` is the source of truth for authored geometry. The exported `.fbx` is the Unity/runtime artifact.

Do not assume that because the Blender source is correct, the exported FBX is also correct.

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

## Unity-oriented FBX conventions

Unless explicitly overridden:

- export only the intended mesh object(s);
- use metric units;
- export with `-Z` forward and `Y` up for Unity-facing FBX files;
- do not export animation for static props;
- do not export cameras or lights;
- preserve UVs, normals, materials, dimensions, and the local pivot;
- do not bake inspection-layout transforms into runtime geometry.

## Transform expectations

Unless explicitly overridden, exported static props should re-import into Blender's FBX importer with:

```text
Location = (0, 0, 0)
Rotation = (0, 0, 0)
Scale    = (1, 1, 1)
```

The local mesh origin must still satisfy the asset's pivot convention.

Unity validation is authoritative for the final Unity import result. Coordinate-system conversion may change axis ordering between Blender and Unity while preserving physical dimensions and pivot semantics.

## Materials and UVs

Use FBX-compatible material inputs. Verify that required UVs and material assignments survive export and Unity import.

## Export success is not completion

A successful export operation or non-zero file size is necessary but insufficient.

Every runtime file must pass:

1. the clean Blender re-import validation defined in `VALIDATION_RULES.md`; and
2. Unity-side validation when the asset is intended to live under `Assets/`.
