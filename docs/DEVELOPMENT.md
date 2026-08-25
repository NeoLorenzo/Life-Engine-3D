# Contributor & Development Guide

This guide covers setup instructions, project structure, component workflows, Unity configuration requirements, and testing practices for **Life Engine 3D**.

---

## 1. Prerequisites & Environment

### Editor Version
* **Unity Version**: `6000.3.12f1` (Unity 6). Check [`ProjectSettings/ProjectVersion.txt`](file:///c:/UnityProjects/LifeEngine/ProjectSettings/ProjectVersion.txt).

### Rendering & Core Packages
Configured in [`Packages/manifest.json`](file:///c:/UnityProjects/LifeEngine/Packages/manifest.json):
* **Render Pipeline**: Universal Render Pipeline (URP `17.3.0`)
* **AI Navigation**: `com.unity.ai.navigation` (`2.0.11`)
* **Input System**: `com.unity.inputsystem` (`1.19.0`)
* **Test Framework**: `com.unity.test-framework` (`1.6.0`)
* **UGUI**: `com.unity.ugui` (`2.0.0`)

---

## 2. Quick Start

1. Open the repository root in **Unity 6000.3.12f1**.
2. Open the main development scene: [`Assets/Scenes/SampleScene.unity`](file:///c:/UnityProjects/LifeEngine/Assets/Scenes/SampleScene.unity).
3. Press **Play** in the Unity Editor.
4. Use runtime simulation controls:
   * **Left Click**: Select any human agent to highlight them and inspect their behavior in inspector/debugger.
   * **Spacebar**: Toggle pause / resume simulation.
   * **Enter / Numpad Enter**: Reset simulation speed to $1\times$.
   * **`+` / `=`**: Double simulation speed (up to $64\times$).
   * **`-`**: Halve simulation speed (down to $1\times$).
5. Open the real-time AI visualizer via the top menu: **Window $\rightarrow$ Life Engine $\rightarrow$ Behavior Tree Debugger**.

---

## 3. Project Structure

```text
Assets/
├── Editor/                         # Custom editor tools and debug windows
│   ├── BehaviorTreeDebugger.cs     # Real-time node graph visualizer
│   └── EnvironmentPrefabBrush.cs   # Scene painting tool for trees & foliage
├── Prefabs/                        # Configured entity and world prefabs
│   ├── Agents/                     # Humanoid agent prefabs (Human 1, male variants)
│   ├── Campfires/                  # Campfires and campfire blueprints
│   ├── Food/                       # Apples and edible items
│   ├── Shelter/                    # Room structures with NavMesh room area
│   ├── Tools/                      # Finished tool items (Basic_Axe)
│   └── Vegitation/                 # Fellable trees, bushes, and plants
├── Scenes/
│   └── SampleScene.unity           # Primary sandbox environment
├── Scripts/
│   ├── AI/                         # Generic Behavior Tree implementation (Nodes, Composites)
│   ├── Core/                       # Runtime selection and interaction (AgentSelector)
│   ├── Crafting/                   # Blueprint construction and requirement logic
│   ├── Fire/                       # Procedural particle/light VFX controller
│   ├── Humans/                     # Brain, Locomotion, Perception, Memory, Animation
│   │   └── Behaviors/              # Simulation action nodes and HumanContext
│   ├── UI/                         # Runtime HUD and timescale hotkeys (TimeControlsUI)
│   └── World/                      # Time, environment curve, day/night, resources, harvesting
└── Settings/                       # URP asset settings and graphics profiles
```

---

## 4. Development Workflows

### Adding a New Resource Type
1. Open [`Assets/Scripts/World/ResourceType.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/ResourceType.cs) and append the new enum value to `ResourceType`.
2. Open the `ResourceRegistry.asset` ScriptableObject in the Inspector.
3. In the **Resources** list, add a mapping pairing the enum value with:
   * `visualPrefab`: Carried model attached to the agent's hand (`resourceSlot`).
   * `worldPrefab`: Physical drop item in the world with a [`ResourceItem`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/ResourceItem.cs) component.

### Adding a New Tool
1. Create a 3D model prefab with a [`ToolItem`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/ToolItem.cs) component and assign a unique string name (e.g., `"Pickaxe"`).
2. Add a mapping in `ResourceRegistry.tools`.
3. If agents should hold it, update [`HumanBrain.UpdateToolVisual()`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs) or create a generalized tool mapping.

### Adding a New Recipe / Conversion
1. In `ResourceRegistry.asset`, add a new entry to **Recipes**:
   * `input`: Input `ResourceType`.
   * `inputQuantity`: Number of units consumed.
   * `outputs`: List of `ResourceOutput` (type + count).
   * `duration`: Time required in seconds.
2. In [`HumanBrain.BuildBehaviorTree()`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs), add a fallback sequence using `CreateConversionFallback` or `CreateMultiOutputFallback`.

### Creating a New Behavior Tree Node
1. In [`Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs), create a class inheriting from [`Node`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/AI/BehaviorTree.cs).
2. Accept [`HumanContext`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs) in the constructor and assign `this.Name`.
3. Implement `public override NodeState Evaluate()`:
   * Return `NodeState.Success` when the objective is achieved.
   * Return `NodeState.Running` while active tasks/movement are in progress.
   * Return `NodeState.Failure` if preconditions fail or targets are invalid.
4. Override `public override string GetDebugText()` to display contextual diagnostic text in the [`BehaviorTreeDebugger`](file:///c:/UnityProjects/LifeEngine/Assets/Editor/BehaviorTreeDebugger.cs).

---

## 5. Unity Configuration Assumptions

### Physics Layers
The project relies on specific layer indices configured in [`ProjectSettings/TagManager.asset`](file:///c:/UnityProjects/LifeEngine/ProjectSettings/TagManager.asset):
* `0`: Default (Obstacle raycasts)
* `1`: TransparentFX
* `2`: Ignore Raycast
* `4`: Water
* `5`: UI
* `6`: Walls (Room detection & shade silhouette raycasts)
* `7`: Humans (Agent colliders)
* `8`: Food (Food scanning overlap mask)
* `9`: Trees (Tree & bush harvesting / shade raycasts)
* `10`: Resources (Ground item scan mask)
* `11`: Blueprint (Construction placement overlap mask)
* `12`: Heat Source (Campfire heat detection)
* `13`: Ground (Locomotion surface and brush raycasts)

> [!WARNING]
> Several legacy scripts contain bitwise literals (`1 << 6` for walls, `1 << 9` for trees, `1 << 0` for default). Do not change layer assignments in `TagManager.asset` without auditing codebase layer masks.

### NavMesh Area Masks
* `Area 0 (Default / Walkable)`: General terrain traversal.
* `Area 3 (Room / Shelter)`: Assigned to interior shelter structures. Evaluated in [`NeedsShelterNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs) via area mask `1 << 3`.

### Singleton Dependencies
The scene requires single instances of:
* [`TimeManager`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/TimeManager.cs)
* [`EnvironmentManager`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/EnvironmentManager.cs)
* [`DayNightCycle`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/DayNightCycle.cs)

---

## 6. Testing & Validation

### Automated Testing
* **Status**: Automated unit and integration tests are not yet implemented. The `com.unity.test-framework` package is installed and ready for test fixture creation under `Assets/Tests/`.

### Manual Validation Workflow
When introducing changes:
1. **Console Check**: Confirm zero compilation errors and no recurring runtime warnings.
2. **Behavior Tree Inspection**: Open **Window $\rightarrow$ Life Engine $\rightarrow$ Behavior Tree Debugger**, select an active human, and verify proper state transitions (green = `Running`, grey = `Idle`/`Success`/`Failure`).
3. **High Timescale Stress Test**: Accelerate simulation speed to $8\times$, $16\times$, and $32\times$ using `+`. Verify that physical locomotion remains stable without jitter, wall penetration, or physics solver lock.
4. **Locomotion Diagnostics**: Enable Gizmos in the Scene View to inspect real-time steering vectors (white desired velocity, green linear velocity, red collision normals).
