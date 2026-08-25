# Debugging & Diagnostic Tools

This guide outlines runtime inspection tools, visual gizmos, diagnostic rays, and common failure modes across the **Life Engine 3D** simulation.

---

## 1. Agent State & Inspector Diagnostics

### Runtime Selection System
* **Selection Mechanism**: [`AgentSelector`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Core/AgentSelector.cs) listens for left mouse clicks in the game view, casts a ray against scene colliders, and finds the parent [`HumanBrain`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs).
* **Selection Visual**: Automatically enables the agent's `selectionVisual` child transform (outline/ring indicator) and selects the `GameObject` in the Unity Editor Hierarchy.
* **Inspector Fields to Monitor**:
  * `adenosineConcentration`: Sleep pressure ($10.0 - 100.0\text{ nM}$).
  * `ghrelinConcentration`: Hunger drive ($500.0 - 1200.0+\text{ pg/mL}$).
  * `perceivedTemperature`: Smoothed body temperature ($^\circ\text{C}$).
  * `currentThermalStatus`: Current enum state (`Cold`, `Comfortable`, `Hot`).
  * `isInShade`: True if all 5 silhouette raycasts are obstructed.
  * `currentStateDisplay`: Multiline formatted string showing active node states.
  * `inventory` & `toolInventory`: Carried resources and tools.

---

## 2. Locomotion Visual Diagnostics

[`HumanLocomotion`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanLocomotion.cs) renders real-time debug rays in the Scene View to expose physical steering and solver interactions:

| Ray Color | Source / Meaning | Code Reference |
| :--- | :--- | :--- |
| **White** | **Desired Velocity**: Target velocity vector computed by blended steering before physics damping and low-pass filtering. | `Debug.DrawRay(rb.position + Vector3.up * 0.5f, lastDesiredVelocity, Color.white)` |
| **Green** | **Actual Linear Velocity**: True physical velocity of the agent's `Rigidbody`. | `Debug.DrawRay(rb.position + Vector3.up * 0.55f, rb.linearVelocity, Color.green)` |
| **Red** | **Collision Normal / Bumper Ray**: Shows contact normals from physical collisions in `OnCollisionStay()`, or active left/right bumper raycasts ($35^\circ$) detecting approaching wall geometry. | `Debug.DrawRay(contact.point, contact.normal * 0.5f, Color.red)` / Bumper hits |
| **Cyan** | **NavMesh Wall Projection**: Drawn when `NavMesh.Raycast` detects an upcoming boundary and projects the movement velocity onto the wall plane. | `Debug.DrawRay(wallHit.position, wallHit.normal, Color.cyan)` |
| **Magenta** | **Rescue Nudge Direction**: Appears when stuck rescue is active (`rescueActiveTimer > 0`), displaying the perpendicular impulse vector. | `Debug.DrawRay(rb.position + Vector3.up * 1.5f, rescueNudgeDir * 2f, Color.magenta)` |
| **Yellow** | **Resource Target Line**: Rendered between the agent and its target resource item/tree in [`CollectResourceNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs). | `Debug.DrawLine(context.Brain.transform.position + Vector3.up, context.CurrentResourceTarget.position, Color.yellow)` |

---

## 3. Perception & Environment Gizmos

When an agent or environment object is selected in the Editor Hierarchy, gizmos render sensory volumes:

* **Visual FOV & Threat Cone** ([`HumanPerception.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanPerception.cs)):
  * **Green / Red FOV Rays**: Extends from head height along the left and right field-of-view boundaries ($200^\circ$, radius $15.0\text{m}$). The rays turn **Red** if a `primaryThreat` is currently visible, otherwise **Green**.
  * **Cyan Wire Sphere**: $2.0\text{m}$ omnidirectional hearing radius.
* **Heat Source Radii** ([`HeatSource.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/HeatSource.cs)):
  * **Orange Wire / Solid Sphere**: Visualizes the maximum thermal radius ($6.0\text{m}$ default) within which the heat source radiates warmth.

---

## 4. Behavior Tree Debugger Window

Open the debugger via **Window $\rightarrow$ Life Engine $\rightarrow$ Behavior Tree Debugger**.

```text
[Window] Behavior Tree Debugger
├── Tree Graph View (Hierarchical Bezier Curves)
│   ├── [Green Box] Running Node (Active execution)
│   ├── [Grey Box]  Idle / Succeeded / Failed Node
│   └── Node Name + Dynamic Debug Text (e.g., "Ghrelin: 1250 pg/mL", "Harvesting Oak (45%)")
```

### Live Debug Strings
Every node implements `GetDebugText()` to display state-specific telemetry:
* `NeedsSleepNode`: `"Adenosine: 45.2nm"`
* `SleepNode`: `"Adenosine: 24.1nm (Clearing)"`
* `CheckDangerNode`: `"Threat: Wolf"` or `"Panic Timer: 2.1s"`
* `WanderNode`: `"Wander CD: 1.4s"`
* `NeedsFoodNode`: `"Ghrelin: 1300 pg/mL"`
* `EatFoodNode`: `"Chowing Down: 0.8s"` or `"Chasing"`
* `FellTreeNode`: `"Harvesting Tree_Oak (65%)"`
* `CollectResourceNode`: `"Picking up... 40%"` or `"Breaking Bush_a (80%)"`
* `DeliverResourceNode`: `"Adding to project... 70%"`
* `ConvertResourceNode`: `"Working... 50%"`
* `NeedsWarmthNode`: `"Perceived: 14.5°C [Cold]"`

---

## 5. Common Failure Modes & Troubleshooting

### Agent Does Not Move
1. **NavMesh Verification**: Check if the agent's `NavMeshAgent` component is placed on a baked NavMesh (`agent.isOnNavMesh`). If the agent is in the air or off the mesh, `HumanLocomotion.IsAgentReady()` returns `false`.
2. **Sleeping State**: Verify if `isSleeping == true`. When sleeping, locomotion and physics are explicitly disabled.

### Agent Stuck Against Obstacles
* **Expected Behavior**: If progress is $< 0.05\text{m}$ over $0.4\text{s}$ while attempting to move, `HumanLocomotion` triggers `PerformRescue()`, nudging the agent perpendicularly ($0.05\text{m}$) and recalculating the path.
* **Troubleshooting**: Check if an unbaked physical collider is obstructing a NavMesh path corridor. NavMesh obstacles must be carved into the mesh or avoided using `obstacleLayer` (`Layer 6`).

### Perception Fails to Detect Target
* **FOV / Hearing Check**: Ensure the target is within $15\text{m}$ and within the $200^\circ$ FOV (or within $2\text{m}$ hearing).
* **Layer Mask Validation**: Ensure targets are assigned to their designated layers:
  * Food $\rightarrow$ `Food` (`Layer 8`)
  * Trees / Bushes $\rightarrow$ `Trees` (`Layer 9`)
  * Ground Items $\rightarrow$ `Resources` (`Layer 10`)
  * Campfires $\rightarrow$ `Heat Source` (`Layer 12`)
* **LOS Raycast Obstruction**: Confirm no `Walls` (`Layer 6`) collider blocks the line of sight between the agent's eye position ($+1.5\text{m}$) and the target's height offset ($+0.15\text{m}$ for items, $+0.6\text{m}$ for trees, $+0.8\text{m}$ for humans).

### Crafting or Delivery Stalls
* **Resource Registry Mappings**: Ensure every `ResourceType` used in recipes or trees has a valid `worldPrefab` and `visualPrefab` assigned in `ResourceRegistry.asset`. Missing prefabs cause `ConvertResourceNode` or `FellableTree.Fell()` to log error warnings and halt conversion.
* **Blueprint Delivery**: Ensure the blueprint prefab has a [`CraftingBlueprint`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Crafting/CraftingBlueprint.cs) component and non-empty `requirements`.
