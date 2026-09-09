# Validation Rules

An asset is not complete because Blender generated it, saved it, or exported it successfully.

Validation is a required stage of the pipeline.

## 1. Source validation

Validate the final Blender source asset for:

- object identity and expected mesh count;
- target dimensions;
- polygon budget;
- vertex count;
- topology sanity;
- normals;
- UV presence and validity;
- material assignment;
- rotation and scale;
- local geometry relative to origin;
- deterministic metadata where applicable.

## 2. Exact dimensional validation

For an exact target dimension `D` and tolerance `T`:

```text
abs(actual - D) <= T
```

Default exact-dimension tolerance:

```text
T = 0.001 m
```

Validate X, Y, and Z independently.

If the specification defines an allowed envelope instead, validate all axes against both minimum and maximum bounds.

## 3. Origin validation

Default origin tolerance:

```text
1e-4 m
```

For a bottom-center origin, local mesh coordinates must satisfy approximately:

```text
(min_x + max_x) / 2 = 0
(min_y + max_y) / 2 = 0
min_z = 0
```

Check all three conditions independently.

## 4. Polygon validation

Count **triangles**, not only Blender polygon faces, because runtime cost is triangle-based.

- Preferred budgets are optimization targets.
- Hard maxima are hard failures.
- There is no implicit minimum triangle count.

## 5. Clean-scene runtime re-import

Every exported runtime model must be imported into a clean temporary Blender scene or equivalent isolated validation context.

For each imported asset verify:

- expected number of mesh objects;
- expected object identity;
- no unexpected cameras/lights/helpers/meshes;
- dimensions still match the specification;
- triangle budget still passes;
- location, rotation, and scale are correct;
- pivot/origin alignment remains correct;
- UVs survived export;
- materials survived export;
- import completed without errors.

The validator must inspect all imported objects, not merely count mesh objects.

Imported validation datablocks must be cleaned up after each run so repeated validation does not accumulate `.001`/`.002` resources or alter later results.

A file existing on disk is not sufficient validation.

## 6. Unity runtime validation

FBX assets placed under `Assets/` must also be validated after Unity imports them.

At minimum verify:

- Unity successfully imports the FBX as an asset;
- the expected mesh count is present;
- no unintended cameras, lights, or skinned meshes are present for static props;
- physical dimensions remain correct after Blender-to-Unity axis conversion;
- triangle budgets remain valid;
- the runtime pivot semantics remain correct;
- required materials survive import;
- the imported root transform is suitable for runtime placement.

The Blender re-import result and Unity import result are separate validation stages. Passing one does not imply the other passed.

## 7. Visual inspection

Inspect individual previews and the pack overview for:

- silhouette quality;
- visible artifacts;
- accidental intersections;
- over-dense geometry;
- insufficient variation;
- inconsistent material treatment;
- obvious scale problems.

Visual review complements numerical validation; it does not replace it.

## 8. Failure policy

Any hard validation failure means the asset is incomplete.

Correct the asset and rerun validation.

Do not silently convert hard failures into warnings. Do not claim completion while unresolved hard failures remain.

## 9. Validation outputs

Prefer producing:

- structured Blender/source/runtime results (`.json`);
- a concise human-readable Markdown report;
- a Unity validation manifest for FBX assets under `Assets/`;
- a Unity-side validation report after Unity imports those assets.

Validation reports must derive provenance/license information from the asset specification or provenance record. Validators must not invent licensing terms.
