# Simulation Model & Dynamics

This document details the mathematical models, biological drives, physical dynamics, and environmental rules governing the Life Engine 3D simulation.

---

## 1. Time Progression

World time is centrally orchestrated by [`TimeManager`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/TimeManager.cs).

### Parameters & Units
* **In-Game Time**: Represented in hours as a floating-point value (`currentTimeHours`) from `0.0f` to `24.0f`.
* **Day Counter**: Monotonically increasing integer (`currentDay`), starting at Day `1`.
* **Starting Time**: `08:00` (8.00 hours).
* **Time Conversion**: `realSecondsPerGameMinute = 1.0f`. One real-world second corresponds to one in-game minute ($60 \text{ seconds} = 1 \text{ game hour}$).
* **Timescale Multiplier**: `timeScaleMultiplier = 1.0f` (tunable via runtime UI up to `64.0f`, or `0.0f` when paused).

### Formula
At each frame update:
$$\Delta \text{GameHours} = \left( \frac{\Delta t_{\text{unscaled}}}{\text{realSecondsPerGameMinute}} \right) \cdot \frac{1}{60} \cdot \text{timeScaleMultiplier}$$

$$\text{currentTimeHours} \leftarrow \text{currentTimeHours} + \Delta \text{GameHours}$$

When $\text{currentTimeHours} \ge 24.0$:
$$\text{currentTimeHours} \leftarrow \text{currentTimeHours} - 24.0, \quad \text{currentDay} \leftarrow \text{currentDay} + 1$$

### Interaction with Unity Physics
When `TimeManager.Play(speedMultiplier)` is called, `Time.timeScale` is set directly to `speedMultiplier`, scaling Unity physics steps (`FixedUpdate`), particle systems, and standard animations alongside the simulation clock.

---

## 2. Physiology: Sleep & Adenosine

Sleep pressure is modeled as an internal concentration of **Adenosine** managed by [`HumanBrain`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs).

### Parameters & Units
* **Concentration Variable**: `adenosineConcentration` (Units: nanomolar, $\text{nM}$).
* **Starting / Baseline Concentration**: `10.0 nM`.
* **Buildup Rate (Awake)**: `adenosineBuildupPerHour = 5.625 nM / hour`.
* **Clearance Rate (Asleep)**: `adenosineClearancePerHour = 11.25 nM / hour` ($2\times$ the buildup rate).
* **Sleep Trigger Threshold**: `100.0 nM` (evaluated in [`NeedsSleepNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs)).
* **Wake Threshold**: `10.0 nM` (evaluated in [`SleepNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs)).

### Lifecycle & Mechanics
1. **Accumulation**: While `isSleeping == false`, `HumanBrain.Update()` adds:
   $$\Delta \text{Adenosine} = \text{adenosineBuildupPerHour} \cdot \Delta \text{GameHours}$$
   Reaching `100.0 nM` requires approximately **16.0 in-game hours** of continuous wakefulness.
2. **Sleep State Transition (`FallAsleep()`)**:
   * `isSleeping` set to `true`.
   * `HumanLocomotion` stopped and disabled; `NavMeshAgent` disabled.
   * `Rigidbody.isKinematic` set to `true`.
   * Capsule position offset downward by $-0.5\text{m}$ on the Y-axis.
   * Model rotated $-90^\circ$ on the X-axis while preserving existing yaw.
3. **Clearance**: [`SleepNode.Evaluate()`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs) decreases adenosine:
   $$\Delta \text{Adenosine} = -\text{adenosineClearancePerHour} \cdot \Delta \text{GameHours}$$
   Clearing from `100.0 nM` down to `10.0 nM` takes exactly **8.0 in-game hours** of sleep.
4. **Waking (`WakeUp()`)**:
   * `isSleeping` set to `false`.
   * Restores `Rigidbody.isKinematic = false`, enables `NavMeshAgent` and `HumanLocomotion`.
   * Repositions transform $+0.5\text{m}$ on Y and restores vertical rotation ($0^\circ$ pitch/roll).

---

## 3. Physiology: Hunger & Ghrelin

Hunger drive is modeled as a concentration of **Ghrelin** managed by [`HumanBrain`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs).

### Parameters & Units
* **Concentration Variable**: `ghrelinConcentration` (Units: picograms per milliliter, $\text{pg/mL}$).
* **Starting / Satiated Baseline**: `500.0 pg/mL`.
* **Accumulation Rate (Awake)**: `ghrelinBuildupPerHour = 140.0 pg/mL / hour`.
* **Hunger Threshold**: `ghrelinHungerThreshold = 1200.0 pg/mL`.

### Lifecycle & Mechanics
1. **Accumulation**: While awake, ghrelin accumulates:
   $$\Delta \text{Ghrelin} = \text{ghrelinBuildupPerHour} \cdot \Delta \text{GameHours}$$
   Reaching the hunger threshold of `1200.0 pg/mL` takes **5.0 in-game hours** from baseline.
2. **Food Seeking**: Once $\text{ghrelinConcentration} \ge 1200.0$, [`NeedsFoodNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs) succeeds. [`SeesFoodNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs) queries `HumanPerception.PerformFoodScan()` to locate nearby food objects on the `Food` layer (`Layer 8`).
3. **Consumption ([`EatFoodNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs))**:
   * Agent runs toward the target (`walkSpeed` $\rightarrow$ `runSpeed`).
   * When within $1.0\text{m}$, the food GameObject is destroyed and `EatingTimer` begins.
   * After $1.5\text{ seconds}$ of eating duration, `ghrelinConcentration` resets to `500.0 pg/mL` and the node returns `NodeState.Success`.

---

## 4. Thermal Comfort Model

The thermal model balances ambient environmental conditions, direct solar radiation (shade), and active heat sources. The state is authoritatively computed in [`HumanBrain.UpdateThermalState()`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs).

### Parameters & Units
* **Temperature Unit**: Degrees Celsius ($^\circ\text{C}$).
* **Comfort Range**: $\text{comfortRangeMin} = 18.0^\circ\text{C}$, $\text{comfortRangeMax} = 26.0^\circ\text{C}$.
* **Default Perceived Temperature**: $22.0^\circ\text{C}$.
* **Thermal Status**: `ThermalStatus` enum (`Cold`, `Comfortable`, `Hot`).
* **Hysteresis Buffer**: $\text{buffer} = 2.0^\circ\text{C}$.
* **Perceived Temperature Smoothing Rate**: $2.0^\circ\text{C} / \text{second}$ (`Mathf.MoveTowards`).

### Target Temperature Calculation
$$T_{\text{target}} = T_{\text{base}} + \Delta T_{\text{shade}} + \sum \Delta T_{\text{heatsource}}$$

1. **Base World Temperature ($T_{\text{base}}$)**: Evaluated by [`EnvironmentManager`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/EnvironmentManager.cs) from an `AnimationCurve` over $0\dots24$ hours:
   * Midnight (`00:00`): $5.0^\circ\text{C}$
   * Morning (`06:00`): $8.0^\circ\text{C}$
   * Noon (`12:00`): $22.0^\circ\text{C}$
   * Peak Afternoon (`14:00`): $25.0^\circ\text{C}$
   * Evening (`18:00`): $18.0^\circ\text{C}$
2. **Shade Bonus ($\Delta T_{\text{shade}}$)**:
   * If `DayNightCycle.IsDaylight` is `true`, a 5-point silhouette raycast checks coverage along $-\vec{d}_{\text{sun}}$ up to $30\text{m}$ against mask `Default (0) | Walls (6) | Trees (9)`:
     * Head ($+1.8\text{m}$ Y)
     * Center ($+1.0\text{m}$ Y)
     * Right Shoulder ($+1.5\text{m}$ Y, $+0.4\text{m}$ local X)
     * Left Shoulder ($+1.5\text{m}$ Y, $-0.4\text{m}$ local X)
     * Feet ($+0.2\text{m}$ Y)
   * A raycast point is shaded if it hits an obstacle on Layer 6 (Walls) or an object with collider height $\ge 2.0\text{m}$.
   * If all 5 points are blocked, the agent is in full shade: $\text{isInShade} = \text{true}$ and $\Delta T_{\text{shade}} = -10.0^\circ\text{C}$. Otherwise, $\Delta T_{\text{shade}} = 0.0^\circ\text{C}$.
3. **Heat Source Contribution ($\Delta T_{\text{heatsource}}$)**:
   * Scans active sources registered in [`HeatSource.ActiveSources`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/HeatSource.cs).
   * Linear distance falloff per source:
     $$\Delta T = \text{strength} \cdot \left(1 - \frac{\text{distance}}{\text{radius}}\right) \quad (\text{for distance} < \text{radius})$$
     * Campfire default: $\text{strength} = 25.0^\circ\text{C}$, $\text{radius} = 6.0\text{m}$.
4. **Perceived Temperature Smoothing**:
   $$T_{\text{perceived}} \leftarrow \text{MoveTowards}(T_{\text{perceived}}, T_{\text{target}}, 2.0 \cdot \Delta t)$$
5. **Hysteresis State Machine**:
   * If currently `Hot`: stays `Hot` until $T_{\text{perceived}} \le 24.0^\circ\text{C}$ ($\text{comfortRangeMax} - \text{buffer}$).
   * If currently `Cold`: stays `Cold` until $T_{\text{perceived}} \ge 20.0^\circ\text{C}$ ($\text{comfortRangeMin} + \text{buffer}$).
   * If currently `Comfortable`: transitions to `Hot` if $T_{\text{perceived}} > 26.0^\circ\text{C}$, or `Cold` if $T_{\text{perceived}} < 18.0^\circ\text{C}$.

---

## 5. Locomotion & Physics Model

Physical steering is managed by [`HumanLocomotion`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanLocomotion.cs).

### Physical Parameters
* **Speeds**: $\text{walkSpeed} = 1.5\text{ m/s}$, $\text{runSpeed} = 3.5\text{ m/s}$.
* **NavMeshAgent**: Radius $0.3\text{m}$, `updatePosition = false`, `updateRotation = false`, `ObstacleAvoidanceType.HighQualityObstacleAvoidance`.
* **Rigidbody & Collider**: Capsule collider radius $0.25\text{m}$ with frictionless physics material (`staticFriction = 0`, `dynamicFriction = 0`). `Rigidbody.interpolation = Interpolate`, `constraints = FreezeRotation`.

### Steering & Avoidance Pipeline
In each `FixedUpdate()` step:
1. **Next Corner Target**: Retrieves `agent.steeringTarget`.
2. **Corridor Flaring**: Queries closest NavMesh edge within $0.45\text{m}$. If close, offsets target outward:
   $$\vec{t}_{\text{flare}} = \vec{t} + \vec{n}_{\text{edge}} \cdot 0.25\text{m}$$
3. **Local Repulsion**: Samples nearby agents in $1.5\text{m}$ radius. Each neighbor adds horizontal repulsive force:
   $$\vec{F}_{\text{repulse}} = \sum \frac{\vec{p}_{\text{self}} - \vec{p}_{\text{other}}}{\text{dist}} \cdot \left(1 - \frac{\text{dist}}{1.5\text{m}}\right) \cdot 2.0$$
4. **Bumper Rays**: Fires two angled raycasts ($\pm 35^\circ$, length $0.65\text{m}$) at head height ($+1.0\text{m}$) against obstacle layers (0, 6, 9) to steer away from approaching corners.
5. **Rotation**: Smoothly interpolates transform yaw toward move direction:
   $$\text{rot} \leftarrow \text{Slerp}(\text{rot}, \text{LookRotation}(\vec{d}_{\text{move}}), 8.0 \cdot \Delta t_{\text{fixed}})$$
6. **NavMesh Projection & Damping**: Projects velocity along NavMesh raycast walls via `Vector3.ProjectOnPlane`. Dampens reverse jitter if $\vec{v}_{\text{linear}} \cdot \vec{v}_{\text{move}} < -0.1$.
7. **Low-Pass Velocity Filter**:
   $$\vec{v}_{\text{rb}} \leftarrow \text{Lerp}(\vec{v}_{\text{rb}}, \vec{v}_{\text{desired}}, 0.3)$$
8. **Hard NavMesh Clamping**: If physics solver drifts $> 0.1\text{m}$ from the NavMesh, snaps `rb.position` back via `NavMesh.SamplePosition(0.5m)`.

### Stuck Detection & Rescue Mechanics
* **Evaluation Interval**: Every $0.4\text{ seconds}$ during active navigation.
* **Progress Check**: Computes progress toward `baselineDestination`:
   $$\text{progress} = \text{dist}_{\text{last}} - \text{dist}_{\text{current}}$$
* **Stuck Condition**: $\text{progress} < 0.05\text{m}$ **AND** $\|\vec{v}_{\text{linear}}\| < 0.2\text{ m/s}$.
* **Rescue Execution (`PerformRescue()`)**:
  * Increments `consecutiveStuckCount` and sets `isCurrentlyStuck = true`.
  * Computes perpendicular normal: $\vec{n}_{\text{perp}} = \pm (\vec{f} \times \vec{u})$.
  * Performs micro-teleport offset: $\vec{p} \leftarrow \vec{p} + \vec{n}_{\text{perp}} \cdot 0.05\text{m}$.
  * Sets `rescueActiveTimer = 0.5\text{s}` with forced speed multiplier $0.8$.
  * Refreshes NavMesh path via `agent.SetDestination(agent.destination)`.

---

## 6. Resources & Crafting Model

The economy is composed of discrete items, physical drops, recipes, and blueprints.

### Resource Types & Data Model
Defined in [`ResourceType.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/ResourceType.cs):
* Wood: `Log_1`, `Log_2`, `Log_3`, `Log_4`, `Stick_1`, `Stick_2`, `Stick_3`, `Stick_4`
* Stone: `Stone`, `Sharpened_Stone`

### Crafting & Blueprints ([`CraftingBlueprint.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Crafting/CraftingBlueprint.cs))
* Blueprints contain a list of [`ResourceRequirement`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Crafting/CraftingBlueprint.cs) structs: `type`, `amountRequired`, `amountCurrent`, `blueprintVisuals[]`, and `normalVisuals[]`.
* **Visual Progression**: As resources are delivered, blueprint ghost meshes are deactivated and physical meshes are enabled sequentially.
* **Delivery & Conservation**:
  $$\text{accepted} = \min(\text{amountRequired} - \text{amountCurrent}, \text{inventoryCount})$$
  * The blueprint absorbs $\text{accepted}$ items and returns the value to [`DeliverResourceNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs).
  * The agent inventory is decremented by exactly $\text{accepted}$. Excess inventory is conserved.
* **Completion**: When all requirements are satisfied, `CraftingBlueprint.Complete()` instantiates `finalPrefab` at transform position and destroys the blueprint instance.

### Multi-Output Conversions ([`ConvertResourceNode`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/Behaviors/HumanBehaviors.cs))
Conversions transform larger resources into smaller components with non-destructive secondary outputs:
* 1x `Stick_3` $\rightarrow$ 1x `Stick_2` + 1x `Stick_1` (Duration: 1.0s)
* 1x `Stick_4` $\rightarrow$ 1x `Stick_3` + 1x `Stick_1` (Duration: 1.0s)
* 1x `Log_3` $\rightarrow$ 1x `Log_2` + 1x `Log_1` (Duration: 1.0s)
* 1x `Log_4` $\rightarrow$ 1x `Log_3` + 1x `Log_1` (Duration: 1.0s)
* 2x `Stone` $\rightarrow$ 1x `Sharpened_Stone` + 1x `Stone` (Duration: 5.0s)
Outputs instantiate as physical world items at the agent's feet while consuming the exact input stack.
