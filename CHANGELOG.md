# Changelog

All notable changes to the Life Engine 3D project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## Unreleased

### Added
- **3-Mode Camera System**: Added [`SimulationCameraController`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/SimulationCameraController.cs) supporting First-Person (animated head bone follow with local eye offset and character renderer hiding), Third-Person (yaw-stabilized torso chase camera), and fixed Sky viewpoint anchored to `SkyCameraAnchor`.
- **GPU Foliage Cutout Shader**: Implemented [`TreeCutoutLit.shader`](file:///c:/UnityProjects/LifeEngine/Assets/Shaders/Foliage/TreeCutoutLit.shader) in URP, performing dynamic circular/cylindrical fragment clipping across Forward, Shadow, and Depth passes during Sky mode so agents and structures remain visible under foliage.
- **Sky Reveal Target Registry & Indicators**: Added [`SkyRevealTarget`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/SkyRevealTarget.cs) and [`SkyRevealController`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/SkyRevealController.cs) managing active world coordinates and rendering unlit horizontal ground indicator rings during Sky mode.
- **Camera Controls HUD**: Integrated [`CameraControlsUI`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/UI/CameraControlsUI.cs) with mode buttons (`1: First Person`, `2: Third Person`, `3: Sky`) and keyboard hotkeys (`1`, `2`, `3`) with automatic non-selection dimming.
- **Comprehensive Camera System Documentation**: Created [`docs/systems/camera-and-visualization.md`](docs/systems/camera-and-visualization.md) and updated [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).
- Comprehensive documentation architecture (`docs/` with architecture, simulation model, invariants, development guide, debugging guide, and subsystem specifications).
- Dedicated coding agent operational guidelines in `AGENTS.md`.
- Relocated and clarified flat planet simulation roadmap under `docs/plans/planet-implementation-plan.md`.

### Fixed
- **UI Selection Raycast Bleed**: Added `EventSystem.current.IsPointerOverGameObject()` guard in [`AgentSelector`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Core/AgentSelector.cs) to prevent clicking HUD UI buttons from triggering accidental agent selection or deselection.
- **Stuck Detection Tracking**: Repeated calls to `HumanLocomotion.SetDestination()` with the same or near-identical destination no longer reset progress timers, preventing agents from endlessly stalling near obstacles.
- **Stuck Detection Ownership**: Encapsulated stuck evaluation and recovery strictly inside `HumanLocomotion.FixedUpdate()`, removing diagnostic mutations from behavior-tree action nodes.
- **Resource Delivery Conservation**: `CraftingBlueprint.AddResource()` now returns the exact integer quantity accepted; `DeliverResourceNode` only deducts what was accepted, preserving surplus resources in agent inventory.
- **Authoritative Thermal Evaluation**: `NeedsWarmthNode` now directly reads the authoritative `HumanBrain.currentThermalStatus` enum (`ThermalStatus.Cold`) instead of evaluating independent temperature thresholds.

## 0.1.0 - Initial Prototype Baseline

### Added
- **Autonomous Human Agents**: Priority-based behavior tree architecture with internal metabolic drives (adenosine for sleep, ghrelin for hunger).
- **Physical Locomotion**: Decoupled NavMesh path calculation with Rigidbody steering, corridor flaring, local agent repulsion, bumper raycasts, velocity smoothing, and stuck recovery.
- **Sensory Perception & Memory**: Visual FOV (200°), LOS obstruction checks, omnidirectional hearing, heat source detection, and short-term threat memory merging.
- **Thermal Comfort Simulation**: Dynamic ambient temperature curve, sun direction raycasting with 5-point silhouette shade detection, and proximity-based heat sources.
- **Crafting & Resource Systems**: Physical resource items, multi-output conversions, tool requirements (Basic Axe), and visual progressive blueprint construction.
- **World & Environment**: Day/night lighting cycle with skybox/fog interpolation, `EnvironmentManager` diurnal temperature curve, fellable trees, and fruit trees.
- **Inspection & Debugging**: Interactive agent selection (`AgentSelector`), live `BehaviorTreeDebugger` editor window, in-game timescale controls (`TimeControlsUI`), and `EnvironmentPrefabBrush`.
