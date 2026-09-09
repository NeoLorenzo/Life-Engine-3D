# Reference and Provenance Rules

Before modelling an asset, decide whether it should be:

1. generated from scratch;
2. modelled manually from references/measurements;
3. sourced from an existing model;
4. sourced and then modified;
5. produced through a hybrid workflow.

## Prefer existing models when appropriate

Do not waste modelling time recreating a standard object when a suitable legally usable model already exists.

This especially applies to:

- commercial furniture;
- consumer electronics;
- common branded products;
- generic standardized props;
- widely available architectural components.

A sourced model still has to pass Life Engine's dimensions, geometry, material, pivot, and runtime validation rules.

## When to model from scratch

Prefer original modelling when:

- the object is unique to the user's real environment;
- exact custom dimensions are required and no suitable base model exists;
- the desired style differs substantially from available assets;
- licensing is unclear or unsuitable;
- available geometry is inefficient or poor quality;
- the asset is trivial to create correctly.

## Provenance record

For every externally sourced model, texture, or substantial geometry component, record at minimum:

- asset ID;
- source URL or marketplace/library identifier;
- creator/author if available;
- retrieval date;
- license or usage terms;
- whether modification is permitted;
- what modifications were made;
- whether attribution is required;
- any uncertainty requiring human review.

Do not treat "publicly downloadable" as equivalent to "licensed for reuse".

## Reference images

Reference imagery may be used for modelling even when redistribution is not permitted. Do not commit copyrighted reference images to the repository unless their use and redistribution are appropriate.

Store links and notes instead when necessary.

## Branded products

For real-world branded objects, prioritize physical accuracy and efficient runtime geometry. Record the source and any uncertainty around redistribution of third-party meshes.
