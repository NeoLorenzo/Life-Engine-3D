# Life Engine 3D

**Life Engine 3D** is an advanced 3D simulation of virtual humans powered by a custom behavior tree AI system in Unity. Agents interact with a dynamic environment to satisfy metabolic needs, seek thermal comfort, gather resources, craft tools, and build structures.

---

## 🚀 Key Features

### 🧠 Behavior Tree AI
*   **Hierarchical Decision-Making**: Utilizes modular selectors, sequences, and action nodes to evaluate states and prioritize behaviors.
*   **State Visualization**: The active node states of the behavior tree are visually represented dynamically in the UI when an agent is selected.

### 🩸 Metabolic Drives & Hysteresis
*   **Adenosine (Sleep)**: Concentrations build up naturally while awake and clear during sleep. When adenosine levels peak, agents seek a safe place to sleep.
*   **Ghrelin (Hunger)**: Tracks hunger progression. Agents automatically look for and consume food when hunger thresholds are breached.
*   **Thermal Comfort & Hysteresis**:
    *   Agents dynamically monitor ambient temperature.
    *   **Shade Detection**: Features a robust 5-point raycast coverage system (silhouette check) that detects if an agent is fully covered by shade from trees or walls to decrease perceived temperature in daylight.
    *   **Warmth Seek**: When cold, agents seek out existing campfires or construct new ones.
    *   **Hysteresis**: Buffer boundaries prevent agents from rapid state ping-ponging (e.g., constantly switching between cold and comfortable).

### 🏃 Custom steering & Locomotion
*   **NavMesh Integration**: Employs Unity's Navigation Mesh system coupled with custom steering logic.
*   **Physics-driven Crowd Avoidance**: Avoids NavMesh collision freezing under high time-scale multipliers (tested up to 8x), allowing agents to steer smoothly around geometry, trees, and other humans.

### 🌲 Resource Gathering & Crafting
*   **Felling Trees**: Agents can acquire tools (like axes) from the ground or craft them, approach trees, and chop them down to harvest wood.
*   **Resource Recipe System**: Features recipe conversion trees. If a specific resource type is required (e.g., specific stick lengths or log weights), agents can gather larger resources and chop/split them down (conversions & multi-output fallbacks).
*   **Blueprint Placement**: Agents select site locations, place blueprints (e.g., campfires, tools), collect the necessary ingredients, and build them.

---

## 📁 Codebase Structure

The core codebase is organized within `Assets/Scripts/`:

*   **`AI/`**: Base classes for the behavior tree framework (`BehaviorTree.cs` containing `Node`, `Composite`, `Selector`, `Sequence`, `ActionNode`).
*   **`Humans/`**: Agent logic and brains.
    *   [`HumanBrain.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs): The central orchestrator that manages variables (adenosine, ghrelin, temperature), evaluates the behavior tree, and updates visuals.
    *   [`HumanLocomotion.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanLocomotion.cs): Steering, pathfinding, and physics collision handling.
    *   [`HumanPerception.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanPerception.cs): Scans the environment for food, heat sources, trees, and items.
    *   [`HumanMemory.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanMemory.cs): Retains short-term memory of scanned interactables.
    *   [`Behaviors/HumanBehaviors.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs): Houses behavior tree leaf nodes (e.g., `NeedsSleepNode`, `FellTreeNode`, `CollectResourceNode`).
*   **`World/`**: Environmental systems.
    *   [`DayNightCycle.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/DayNightCycle.cs): Handles solar pathing and light direction.
    *   [`EnvironmentManager.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/EnvironmentManager.cs): Manages world-wide temperature cycles.
    *   [`ResourceRegistry.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/ResourceRegistry.cs): Configures mappings between item types, visual models, and recipes.
    *   [`TimeManager.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/TimeManager.cs): Powers time-scale acceleration.
*   **`UI/`**: Interactive canvas elements like time-scaling buttons (`TimeControlsUI.cs`).
*   **`Crafting/`**: Managing blueprints, ingredient delivery, and construction.
*   **`Core/`**: Player controls like selecting agents (`AgentSelector.cs`).

---

## 🛠️ Getting Started

### 📋 Prerequisites
*   Unity Editor (compatible with 3D URP pipelines).

### 🕹️ How to Run
1.  Open the project in Unity.
2.  Open the main scene: `Assets/Scenes/SampleScene.unity`.
3.  Press the **Play** button in the Editor.
4.  Use the mouse to select agents in the world to view their active behavior tree state and metabolic levels. Use the UI panel to adjust simulation time-scale.
