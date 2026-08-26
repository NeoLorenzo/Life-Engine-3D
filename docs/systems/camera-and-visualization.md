# Camera & Visualization System

## Purpose
The Camera & Visualization system provides multi-mode viewing capabilities (First-Person, Third-Person, and Sky) for observing the artificial life simulation and humanoid agents. In Sky mode, it provides GPU-driven cylindrical tree canopies cutouts and circular ground indicators to ensure human agents and player/agent-constructed structures remain visible through dense foliage.

---

## Responsibilities
* Manage the authoritative `Camera.main` transform, clipping planes, and field-of-view across three distinct camera modes via [`SimulationCameraController`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/SimulationCameraController.cs).
* Follow the selected agent's animated humanoid head in First-Person mode, applying local eye offsets and temporarily hiding the character's mesh renderers.
* Follow the selected agent in Third-Person chase mode, maintaining a yaw-stable horizontal basis isolated from walk-cycle roll/pitch and sleep rotations.
* Maintain a fixed simulation viewpoint in Sky mode via [`SkyCameraAnchor`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/SimulationCameraController.cs).
* Coordinate GPU-driven circular tree canopy clipping via [`SkyRevealController`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/SkyRevealController.cs) and [`TreeCutoutLit.shader`](file:///c:/UnityProjects/LifeEngine/Assets/Shaders/Foliage/TreeCutoutLit.shader).
* Track active reveal targets (agents, campfires, shelters, blueprints) through a zero-allocation static registry in [`SkyRevealTarget`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/SkyRevealTarget.cs) and render unlit circular ground indicator rings exclusively during Sky mode.
* Provide HUD controls and keyboard shortcuts (`1`, `2`, `3`) via [`CameraControlsUI`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/UI/CameraControlsUI.cs).
* Guard agent world selection raycasts against UI pointer interactions in [`AgentSelector`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Core/AgentSelector.cs).

---

## Non-Responsibilities
* Does **not** drive agent navigation, steering, or locomotion (owned by [`HumanLocomotion`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanLocomotion.cs)).
* Does **not** modify agent decision-making, needs, or behavior trees (owned by [`HumanBrain`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs)).
* Does **not** alter world time progression or environmental temperature (owned by [`TimeManager`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/TimeManager.cs) and [`EnvironmentManager`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/EnvironmentManager.cs)).

---

## Main Files
* [`Assets/Scripts/Camera/CameraMode.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/CameraMode.cs): Enumeration defining `FirstPerson`, `ThirdPerson`, and `Sky`.
* [`Assets/Scripts/Camera/SimulationCameraController.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/SimulationCameraController.cs): Authoritative camera controller managing transform follow logic, clipping, renderer visibility, and mode transitions.
* [`Assets/Scripts/Camera/SkyRevealTarget.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/SkyRevealTarget.cs): Marker component and static registry for objects requiring canopy reveal and indicator rings.
* [`Assets/Scripts/Camera/SkyRevealController.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Camera/SkyRevealController.cs): Central manager uploading active reveal targets (`Vector4[64]`) to shader globals.
* [`Assets/Shaders/Foliage/TreeCutoutLit.shader`](file:///c:/UnityProjects/LifeEngine/Assets/Shaders/Foliage/TreeCutoutLit.shader): Custom URP Lit shader performing GPU cylindrical fragment discards in Forward and Shadow passes when Sky mode is enabled.
* [`Assets/Scripts/UI/CameraControlsUI.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/UI/CameraControlsUI.cs): HUD component managing mode buttons and hotkey inputs (`1`, `2`, `3`).
* [`Assets/Scripts/Core/AgentSelector.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Core/AgentSelector.cs): Selection manager with UI raycast protection (`IsPointerOverGameObject()`).

---

## Camera Modes & Transform Mechanics

### 1. First-Person Mode (`CameraMode.FirstPerson`)
* **Target Bone**: `HumanBodyBones.Head` sampled via the agent's `Animator`. Falls back to root transform $+ 1.65\text{m}$ vertical offset if unrigged.
* **Camera Position**: $\vec{p}_{\text{head}} + \mathbf{R}_{\text{head}} \cdot \vec{o}_{\text{eye}}$ (default local eye offset: `(0, 0.06, 0.12)`).
* **Camera Rotation**: $\mathbf{R}_{\text{head}}$ matching the physical head orientation.
* **Camera Settings**: $\text{FOV} = 75^\circ$, $\text{NearClip} = 0.05\text{m}$.
* **Character Mesh Hiding**: When active, `Renderer` components belonging to the agent's body mesh are cached and set to `enabled = false`. Held tools (parented under `toolSlot`), carried resources (parented under `resourceSlot`), selection indicators, and indicator rings are explicitly preserved and stay visible. When leaving First-Person mode, deselecting the agent, or destroying the target, the original enabled states are restored.

### 2. Third-Person Chase Mode (`CameraMode.ThirdPerson`)
* **Target Bone**: `HumanBodyBones.Chest` (or `Spine` fallback).
* **Yaw-Stable Follow Basis**: The forward direction is calculated as $\vec{f}_{\text{horizontal}} = \text{ProjectOnPlane}(\vec{f}_{\text{root}}, \text{Up})$. If the horizontal magnitude is negligible (e.g. agent lying down while sleeping), the last stable horizontal forward is retained to prevent the camera from pitching into the ground.
* **Camera Position**: $\vec{p}_{\text{torso}} - \vec{f}_{\text{horizontal}} \cdot d_{\text{behind}} + \text{Up} \cdot h_{\text{torso}}$ (default: $d_{\text{behind}} = 3.5\text{m}$, $h_{\text{torso}} = 1.5\text{m}$).
* **Look Target**: $\vec{p}_{\text{torso}} + \text{Up} \cdot y_{\text{lookOffset}}$ (default: $y_{\text{lookOffset}} = 0.4\text{m}$).
* **Camera Settings**: $\text{FOV} = 65^\circ$, $\text{NearClip} = 0.2\text{m}$.

### 3. Fixed Sky Mode (`CameraMode.Sky`)
* **Authoritative Anchor**: Position and rotation defined by the scene's `SkyCameraAnchor` transform (defaulting to the startup Main Camera transform if unassigned).
* **Camera Settings**: $\text{FOV} = 60^\circ$, $\text{NearClip} = 0.3\text{m}$, $\text{FarClip} = 1000\text{m}$.
* **Canopy Cutout & Reveal Rings**: Toggles `_SkyRevealEnabled = 1.0` in shader globals and enables circular unlit ground rings across all active `SkyRevealTarget` instances.

---

## Sky Reveal & Tree Cutout System

### GPU Shader Integration ([`TreeCutoutLit.shader`](file:///c:/UnityProjects/LifeEngine/Assets/Shaders/Foliage/TreeCutoutLit.shader))
* **Shader Globals**:
  * `float _SkyRevealEnabled`: `1.0` in Sky mode, `0.0` otherwise.
  * `int _SkyRevealCount`: Number of active reveal targets (bounded by 64).
  * `float4 _SkyRevealTargets[64]`: Array of `(x, y, z, radius)` world-space coordinates.
* **Target Capacity & Deterministic Overflow Policy**:
  * The GPU uniform buffer accommodates up to 64 active targets (`MaxRevealTargets = 64`).
  * If active reveal targets exceed 64, `SkyRevealController` follows a **Deterministic FIFO Registration Policy**, binding the first 64 registered active targets in `SkyRevealTarget.ActiveTargets` list. This ensures stable, deterministic rendering without hash-order fluctuation.
* **Fragment Discard Logic**:
  ```hlsl
  if (_SkyRevealEnabled > 0.5)
  {
      int count = min(_SkyRevealCount, 64);
      for (int i = 0; i < count; i++)
      {
          float2 delta = positionWS.xz - _SkyRevealTargets[i].xz;
          float radius = _SkyRevealTargets[i].w;
          if (dot(delta, delta) < radius * radius)
          {
              clip(-1.0);
              break;
          }
      }
  }
  ```
* **Pass Coverage**: Implemented across `UniversalForward`, `ShadowCaster`, `DepthOnly`, and `DepthNormals` passes to ensure direct sunlight and shadows accurately reflect canopy cutouts.

### Registered Targets & Radii
| Target Entity | Prefabs | Reveal Radius | Ring Color |
| :--- | :--- | :--- | :--- |
| **Human Agents** | `Human 1.prefab`, `male01_1`..`male03_3` | $1.75\text{m}$ | Cyan (`#33E6FF`) |
| **Campfires** | `Small Campfire`, `Tiny Campfire`, `Blueprint_Tiny_Campfire` | $2.50\text{m}$ | Amber / Orange (`#FF991A`) |
| **Shelters** | `Basic Shelter` | $4.00\text{m}$ | Green (`#4DFF66`) |
| **Tool Blueprints** | `Blueprint_Basic_Axe` | $1.50\text{m}$ | Amber / Orange (`#FF991A`) |

---

## User Interface & Interaction

### HUD Controls ([`CameraControlsUI.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/UI/CameraControlsUI.cs))
* **Buttons**: `1: First Person`, `2: Third Person`, `3: Sky`.
* **State Dimming**: First Person and Third Person buttons are automatically set to non-interactable when no human is selected.
* **Keyboard Hotkeys**: `1` (First Person), `2` (Third Person), `3` (Sky) using the new Unity Input System (`Keyboard.current`).
* **UI Raycast Protection**: [`AgentSelector.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Core/AgentSelector.cs) checks `EventSystem.current.IsPointerOverGameObject()` prior to performing selection raycasts, preventing unintended agent selection or deselection when clicking HUD buttons.
