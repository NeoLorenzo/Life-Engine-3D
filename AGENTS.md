# Operating Guidelines for AI Agents & Contributors

This document defines core conventions, ownership rules, and development practices for AI coding agents and human contributors working on the **Life Engine 3D** codebase.

---

## 1. Primary Directives

1. **Inspect Before Modifying**: Always read the corresponding subsystem documentation in [`docs/systems/`](docs/systems/) and the architectural overview in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) before altering code.
2. **Review Invariants**: Read [`docs/INVARIANTS.md`](docs/INVARIANTS.md) prior to making any simulation or AI behavior modifications. Invariants must be strictly maintained unless a task explicitly mandates a redesign.
3. **Document the Actual Implementation**: Never document planned, aspirational, or hypothetical features as implemented systems. Maintain a strict separation between live behavior, technical debt/limitations, and future plans (such as [`docs/plans/planet-implementation-plan.md`](docs/plans/planet-implementation-plan.md)).
4. **Update Documentation Continuously**: Whenever a change is made that alters public APIs, state ownership, tunables, thresholds, or runtime flow, immediately update the relevant files in [`docs/`](docs/).
5. **Maintain the Changelog**: Record all simulation-significant additions, fixes, or breaking changes in [`CHANGELOG.md`](CHANGELOG.md) under `## Unreleased`.
6. **Keep README Public & High-Level**: Do not turn [`README.md`](README.md) into a dense reference manual; implementation specifics belong in [`docs/`](docs/).
7. **Explain Why in Comments**: Avoid superficial comments that restate method names or obvious syntax. Write comments that explain design rationale, units of measurement, subtle edge cases, or ownership invariants.
8. **Avoid Unsolicited Broad Refactors**: Keep modifications tightly scoped to the user request. Do not reorganize or rewrite functioning subsystems without explicit instruction.

---

## 2. Core Architectural Invariants

* **Locomotion & Stuck Detection Authority**:
  * [`HumanLocomotion`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanLocomotion.cs) is the **sole owner** of stuck-detection evaluation and recovery nudging (executed during `FixedUpdate`).
  * Behavior tree nodes may read `HumanLocomotion.IsCurrentlyStuck` or call `ClearStuckCount()`, but must **never** advance progress timers or manually calculate stuck heuristics.
  * Destination setting uses baseline comparison (`destinationChangeThreshold = 0.3m`). Subsystems must not bypass `SetDestination()` or cause continuous destination churn.
* **Thermal State Authority**:
  * [`HumanBrain.UpdateThermalState()`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs) is the **sole authoritative source** for thermal state evaluation (`ThermalStatus.Cold`, `ThermalStatus.Comfortable`, `ThermalStatus.Hot`) and perceived temperature smoothing.
  * Behavior nodes (e.g., [`NeedsWarmthNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs)) must react to `brain.currentThermalStatus` rather than implementing competing temperature thresholds.
* **Resource Conservation**:
  * Physical and inventory resource transfers must be lossless.
  * [`CraftingBlueprint.AddResource()`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Crafting/CraftingBlueprint.cs) determines the exact count of items accepted.
  * Delivering agents must only deduct the integer amount returned by `AddResource()`. Surplus inventory must remain in the agent's inventory.
  * Conversions must only consume specified input quantities and spawn exact configured outputs.
* **Behavior Tree Stateless Evaluation**:
  * The root tree is reset and evaluated anew each frame (`rootNode.ResetState()`, `rootNode.Evaluate()`).
  * Action nodes must handle interruptibility gracefully, ensuring that external state changes (e.g., target destruction, blueprint completion) fail cleanly without leaving orphaned timers or invalid references.

---

## 3. Unity & Environment Conventions

* **Unity Version**: **`6000.3.12f1`** (Unity 6).
* **Render Pipeline**: Universal Render Pipeline (URP `17.3.0`).
* **Physics & Units**:
  * Spatial units: Metric (meters, seconds, kilograms).
  * Angles: Degrees.
  * Temperature: Degrees Celsius (°C).
  * Metabolic units: Adenosine in nanomolar (`nM`), Ghrelin in picograms per milliliter (`pg/mL`).
  * Time: In-game hours (`0.0f` to `24.0f`), with progression controlled by `TimeManager.realSecondsPerGameMinute`.
* **Physics Layers**:
  * `0`: Default
  * `1`: TransparentFX
  * `2`: Ignore Raycast
  * `4`: Water
  * `5`: UI
  * `6`: Walls
  * `7`: Humans
  * `8`: Food
  * `9`: Trees
  * `10`: Resources
  * `11`: Blueprint
  * `12`: Heat Source
  * `13`: Ground
  * *Note: Be aware of bitmask shifts (`1 << 6`, `1 << 9`) across legacy scripts.*
* **NavMesh Areas**:
  * `0`: Walkable (Default)
  * `3`: Room Area (Mask `1 << 3` / area index 3 used for shelter comfort detection).

---

## 4. Verification Workflow

Before completing any task:
1. Verify that Unity compiles without errors or warnings.
2. Confirm that all markdown links (relative paths) resolve correctly.
3. Check that no unintentional code or asset modifications were introduced (`git status`, `git diff`).
4. Record any unresolved architectural trade-offs or technical debt under the corresponding "Known Limitations" section in [`docs/`](docs/).
