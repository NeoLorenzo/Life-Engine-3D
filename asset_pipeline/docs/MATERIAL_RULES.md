# Material Rules

## Runtime compatibility

Default to simple glTF-compatible PBR materials using Principled BSDF unless the target runtime requires something else.

## Simplicity

Use the simplest material setup that achieves the intended appearance.

Avoid unnecessary:

- complex procedural node graphs;
- high-resolution textures;
- layered effects that will not export cleanly;
- unique materials when assets can legitimately share one.

## Low-poly assets

For stylized low-poly assets, prefer readable shape and facet structure over texture complexity.

Simple base-color variation plus appropriate roughness is usually sufficient unless the asset specification requests more.

## Texture scale

Texture resolution must be justified by expected on-screen size and viewing distance. Do not use large textures for tiny environmental props.

## Material reuse

Within a cohesive asset pack, reuse shared materials where possible. Create variants only when they materially improve the pack.

## Export verification

Material assignment must be checked after runtime export and clean re-import. A correct Blender viewport appearance does not prove the runtime material survived export.
