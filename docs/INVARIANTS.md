# Architectural Invariants & System Constraints

This document records the foundational invariants and architectural rules of the **Life Engine 3D** codebase. Future changes, optimizations, and agent refactors must maintain these guarantees.

---

## 1. Resource Conservation Invariant

> **Rule**: Resources must never be created or destroyed implicitly during transfer operations. All inventory and world transactions must be strictly conservative.

* **Blueprint Delivery**:
  * [`CraftingBlueprint.AddResource(type, amount)`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Crafting/CraftingBlueprint.cs) calculates $\text{accepted} = \min(\text{needed}, \text{delivered})$ and increments `amountCurrent` by $\text{accepted}$.
  * [`DeliverResourceNode.Evaluate()`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs) must only deduct the returned $\text{accepted}$ amount from `HumanBrain.inventory`. Any surplus remains in the agent's inventory.
* **Resource Conversions**:
  * [`ConvertResourceNode.Evaluate()`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs) must verify that `GetResourceCount(inputType) >= requiredAmount` before starting work.
  * Upon completion, it consumes exactly `requiredAmount` and instantiates the exact configured output items (`ResourceOutput[]`).
* **World Recovery**:
  * Items falling out of bounds ($y < -10.0\text{m}$) are rescued by [`ResourceItem`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/ResourceItem.cs) and repositioned to surface level rather than silently destroyed.

---

## 2. Thermal State Authority Invariant

> **Rule**: [`HumanBrain.UpdateThermalState()`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs) is the sole authoritative owner of thermal state evaluation and perceived temperature smoothing.

* **No Competing Thresholds**: Behavior tree nodes (e.g., [`NeedsWarmthNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs), `FindShadeSpotNode`, `IsInComfortableShadeNode`) must react directly to `HumanBrain.currentThermalStatus` (`ThermalStatus.Cold`, `ThermalStatus.Comfortable`, `ThermalStatus.Hot`) or `HumanBrain.isInShade`.
* **Centralized Hysteresis**: Hysteresis buffer calculations ($2.0^\circ\text{C}$ deadband) and smoothing rate ($2.0^\circ\text{C}/\text{s}$) exist exclusively within `HumanBrain.UpdateThermalState()` to prevent oscillation between behavior branches.

---

## 3. Locomotion & Stuck Detection Ownership Invariant

> **Rule**: [`HumanLocomotion`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanLocomotion.cs) is the sole owner of movement physics, NavMesh synchronization, progress tracking, and stuck recovery.

* **Encapsulated Evaluation**: Stuck progress checking and rescue nudges execute exclusively inside `HumanLocomotion.FixedUpdate()`.
* **Read-Only Behavior Tree Access**: Behavior tree nodes (e.g., [`FleeNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs)) may read `HumanLocomotion.IsCurrentlyStuck` to trigger local replanning or invoke `ClearStuckCount()`, but must **never** advance progress timers, modify internal velocity profiles, or calculate independent stuck heuristics.

---

## 4. Destination Baseline Invariant

> **Rule**: Repeatedly passing the same or proximate target position to `SetDestination()` must not reset stuck detection progress.

* **Baseline Distance Check**: [`HumanLocomotion.SetDestination(destination)`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanLocomotion.cs) compares incoming targets against `baselineDestination`.
* **Threshold Guarantee**: If $\|\vec{d}_{\text{baseline}} - \vec{d}_{\text{new}}\| \le \text{destinationChangeThreshold}$ ($0.3\text{m}$), the destination is updated on the underlying `NavMeshAgent` without resetting `stuckTimer` or `lastDistanceToTarget`.
* **Full Reset Trigger**: Only an explicit target displacement $> 0.3\text{m}$, or an explicit call to `HumanLocomotion.Stop()`, establishes a new progress baseline.

---

## 5. Behavior Tree Evaluation Semantics

> **Rule**: The behavior tree is stateless across frame transitions; nodes manage task progress through timestamps or shared `HumanContext` state.

* **Top-Down Priority Evaluation**: Every frame in `HumanBrain.Update()`, `rootNode.ResetState()` is executed followed by `rootNode.Evaluate()`.
* **Priority Preemption**: If a higher-priority branch (e.g., Sleep at Priority 0 or Danger at Priority 1) succeeds or enters `Running`, lower-priority branches are preempted immediately.
* **Interruptibility & Cleanup**: Nodes must not assume uninterrupted execution. In-flight timers and target references must fail cleanly if a higher-priority behavior preempts execution.

---

## 6. Agent Knowledge Boundaries & Known Exceptions

> **Rule**: Agents should observe the world strictly through sensory perception (`HumanPerception`), except where explicitly recorded as technical debt.

### Standard Sensory Perception
* Visual detection requires target proximity $\le 15\text{m}$, horizontal angle within $200^\circ$ FOV, and unobstructed line-of-sight raycasts to target height offsets.
* Hearing operates omnidirectionally within $2.0\text{m}$.
* Heat sources are queried via proximity scan against active registered sources.

### Known Exceptions / Technical Debt (Non-Invariants)
* [`FindHarvestableSourceNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs) queries all scene trees via `Object.FindObjectsByType<FellableTree>()`.
* [`FindToolOnGroundNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs) queries all scene tools via `Object.FindObjectsByType<ToolItem>()`.
* [`FindShadeSpotNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs) queries all trees globally via `Object.FindObjectsByType<FellableTree>()`.
* *Future refactors should route these queries through `HumanPerception` or spatial partitions.*

---

## 7. State Transfer & Target Safety

> **Rule**: Behavior nodes must validate target existence every frame to handle external destruction gracefully.

* When food is consumed, trees are felled, or blueprints are completed, the underlying `GameObject` is destroyed.
* Action nodes (`EatFoodNode`, `FellTreeNode`, `DeliverResourceNode`) must check for `null` targets, handle destruction mid-action, and reset context references (`context.CurrentFoodTarget = null`, `context.CurrentTreeTarget = null`, `context.CurrentBlueprintInstance = null`) without throwing null reference exceptions.
