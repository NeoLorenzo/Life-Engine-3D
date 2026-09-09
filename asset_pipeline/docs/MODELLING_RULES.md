# 3D Modelling Rules

## Units

Use **meters** as the canonical modelling unit.

Convert source measurements to meters before modelling. Do not mix centimeters, millimeters, Unity units, and Blender units implicitly.

## Dimensions

When an asset specification gives exact dimensions, they are hard requirements.

Default exact-dimension tolerance:

```text
±0.001 m
```

After all geometry-changing operations:

1. apply or evaluate final geometry-changing modifiers;
2. calculate the final mesh bounding box;
3. scale the finished mesh independently per axis if required to hit the target dimensions;
4. apply scale;
5. recalculate the bounding box;
6. fail validation if any axis exceeds the allowed tolerance.

Do not rely on approximate procedural scaling.

If a specification provides only an allowed size envelope rather than exact dimensions, validate every axis against that envelope.

## Polygon efficiency

Use the **fewest polygons necessary for the intended visual result**.

There is no minimum triangle count.

Geometry is justified when it materially contributes to one or more of:

- silhouette;
- readable form;
- required deformation or animation;
- required collision fidelity;
- intended faceting or hard-surface structure;
- gameplay-relevant detail.

Do not:

- subdivide merely to hit a target count;
- generate a high-resolution mesh only to decimate it when a simpler base mesh can produce the same result;
- retain hidden or internal geometry with no purpose;
- spend polygons on details that are below the expected viewing scale.

Specifications should normally define a **preferred maximum** and a **hard maximum**. The preferred maximum may be exceeded only when visible quality materially benefits. The hard maximum may not be exceeded without explicit approval.

## Topology

Final runtime meshes should contain no unintended:

- duplicate faces or vertices;
- loose vertices;
- overlapping duplicate geometry;
- internal geometry;
- non-manifold geometry;
- zero-area faces;
- invalid normals.

Modifiers used only for generation should normally be applied before validation and export.

## Transforms

Unless explicitly overridden:

```text
Rotation = (0, 0, 0)
Scale    = (1, 1, 1)
```

Inspection-scene placement must not be baked into the runtime asset.

## Origins / pivots

The asset specification controls the pivot convention.

Default for static ground-placeable props: **bottom-center**.

In local mesh coordinates this means approximately:

```text
(min_x + max_x) / 2 = 0
(min_y + max_y) / 2 = 0
min_z = 0
```

Do not validate only `min_z`; validate horizontal centering independently.

## Shading

Use the shading approach required by the style specification.

For stylized low-poly assets, flat shading is the default. Do not add geometry solely to produce a shading effect that can be achieved more cheaply with normals or materials, unless faceted geometry is part of the intended silhouette.

## UVs

Runtime meshes should have a valid UV map unless the asset specification explicitly states that UVs are unnecessary.

UVs must not be corrupt and must survive runtime export/re-import.

## Deterministic procedural generation

If procedural randomness is used:

- use explicit deterministic seeds;
- record the seed per asset;
- record materially important generator parameters;
- preserve reusable generator code in the repository when practical.

The same generator, parameters, and seed should reproduce substantially the same asset.
