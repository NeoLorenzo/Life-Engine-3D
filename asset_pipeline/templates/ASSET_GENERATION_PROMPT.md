# Life Engine 3D Asset Generation Prompt Template

Use this template when assigning a 3D asset or asset-pack task to Antigravity, Codex, or another agent with Blender access.

Replace all `<PLACEHOLDERS>` before execution.

---

# Task: <ASSET OR PACK NAME>

Before starting, read `asset_pipeline/README.md` and the pipeline documentation it references. Follow those rules for modelling, sourcing, file structure, Blender automation, export, validation, previews, provenance, and completion reporting.

Use `asset_pipeline/templates/asset_pack_spec.yaml` to create the task-specific machine-readable specification where applicable.

The prompt contains the descriptive brief. The YAML contains structured execution and validation values.

## Objective

<WHAT TO CREATE AND HOW IT WILL BE USED>

## Visual brief

<DESCRIBE THE REQUIRED APPEARANCE, SHAPE, STYLE, IMPORTANT FEATURES, AND WHAT TO AVOID>

## Reference images

<ATTACH REFERENCE IMAGES OR PROVIDE THEIR PATHS / URLS HERE>

<OPTIONAL NOTES ABOUT WHAT PARTICULAR REFERENCES SHOULD BE USED FOR>

## Measurements and known facts

<ANY IMPORTANT MEASUREMENTS OR RELATIONSHIPS NOT SUITABLY EXPRESSED AS SIMPLE STRUCTURED VALUES IN THE YAML>

## Asset-specific constraints or exceptions

<ONLY TASK-SPECIFIC REQUIREMENTS THAT ARE NOT ALREADY DEFINED BY THE PIPELINE DOCUMENTATION>
