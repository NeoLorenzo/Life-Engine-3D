# World, Environment & Time System

## Purpose
The World, Environment & Time system simulates global time progression, astronomical lighting and day/night transitions, diurnal temperature curves, interactive heat sources, and harvestable vegetation.

---

## Responsibilities
* Advance in-game hours, days, and timescales via [`TimeManager`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/TimeManager.cs).
* Evaluate global diurnal ambient temperatures via [`EnvironmentManager`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/EnvironmentManager.cs).
* Drive solar rotation, light color/intensity, ambient illumination, and fog density via [`DayNightCycle`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/DayNightCycle.cs).
* Track active heat-emitting objects via [`HeatSource`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/HeatSource.cs).
* Manage interactive harvesting and transformation lifecycles for [`FellableTree`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/FellableTree.cs) and [`FruitTree`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/FruitTree.cs).

---

## Non-Responsibilities
* Does **not** control agent physiological variables (owned by [`HumanBrain`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Humans/HumanBrain.cs)).
* Does **not** own agent behavior decisions or movement.

---

## Main Files
* [`Assets/Scripts/World/TimeManager.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/TimeManager.cs): Central clock singleton.
* [`Assets/Scripts/World/EnvironmentManager.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/EnvironmentManager.cs): World temperature curve singleton.
* [`Assets/Scripts/World/DayNightCycle.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/DayNightCycle.cs): Celestial rotation and environmental lighting singleton.
* [`Assets/Scripts/World/HeatSource.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/HeatSource.cs): Heat emitter component and static registry.
* [`Assets/Scripts/World/FellableTree.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/FellableTree.cs): Harvestable tree/bush transforming into stump + resource drops.
* [`Assets/Scripts/World/FruitTree.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/FruitTree.cs): Periodic physical food drop spawner.
* [`Assets/Scripts/Fire/VFX_FireController.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/Fire/VFX_FireController.cs): Particle and light visual controller for campfires.

---

## State / Data

### Time State ([`TimeManager.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/TimeManager.cs))
* `realSecondsPerGameMinute = 1.0f`: Ratio of real seconds to in-game minutes.
* `currentTimeHours = 8.0f`: In-game hour ($0.0 - 24.0$).
* `currentDay = 1`: Day counter.
* `timeScaleMultiplier = 1.0f`: Active speed multiplier.

### Environment & Temperature ([`EnvironmentManager.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/EnvironmentManager.cs))
* `AnimationCurve temperatureCurve`: Diurnal temperature mapping (X: 0..24h, Y: $^\circ\text{C}$).
  * `00:00`: $5.0^\circ\text{C}$
  * `06:00`: $8.0^\circ\text{C}$
  * `12:00`: $22.0^\circ\text{C}$
  * `14:00`: $25.0^\circ\text{C}$
  * `18:00`: $18.0^\circ\text{C}$
* `BaseTemperature`: Read-only property returning current evaluated ambient temperature.

### Day/Night Lighting ([`DayNightCycle.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/DayNightCycle.cs))
* `SunDirection`: Normalized vector pointing along the directional light forward axis.
* `IsDaylight`: True when sun light intensity $> 0.1$.
* Visual Gradients & Curves: `lightColor`, `lightIntensity`, `ambientColor`, `ambientIntensity`, `reflectionIntensity`, `fogColor`, `fogDensity`.

### Static Heat Registry ([`HeatSource.cs`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/HeatSource.cs))
* `public static readonly List<HeatSource> ActiveSources`: Global list of enabled heat sources.

---

## Execution Flow

```mermaid
graph TD
    TM["TimeManager.Update()"] -->|Advances currentTimeHours| TM
    TM -->|Evaluates hour on curve| EM["EnvironmentManager.Update() -> BaseTemperature"]
    TM -->|Rotates DirectionalLight| DNC["DayNightCycle.Update()"]
    DNC -->|Interpolates| Sky["Skybox / Ambient Light / Fog Gradients"]
    
    FT["FruitTree.Update()"] -->|Timer Check (spawnInterval = 30s)| Apple["Instantiate Apple Prefab (Falls via Rigidbody)"]
    
    AI["AI FellTreeNode"] -->|Invokes Fell()| Tree["FellableTree.Fell()"]
    Tree --> Stump["Instantiate stumpPrefab"]
    Tree --> Drops["Instantiate Resource Drops from Registry"]
    Tree --> Cleanup["Destroy(treeGameObject)"]
```

### 1. Sun Rotation Formula
$$\text{sunRotationX} = \left(\frac{\text{currentTimeHours} - 6.0}{24.0}\right) \cdot 360^\circ$$
* Sunrise at `06:00` ($X = 0^\circ$).
* Solar Noon at `12:00` ($X = 90^\circ$).
* Sunset at `18:00` ($X = 180^\circ$).
* Midnight at `00:00` ($X = 270^\circ$).

### 2. Fellable Tree Transformation (`FellableTree.Fell()`)
1. Instantiates `stumpPrefab` at `stumpSpawnPoint` position/rotation.
2. Iterates through configured `ResourceSpawnGroup[]` drops, queries `ResourceRegistry.GetWorldPrefab()`, and instantiates resource drops at configured child slot transforms.
3. Destroys living tree GameObject.

### 3. Fruit Tree Dropping (`FruitTree.Update()`)
Every `spawnInterval` ($30\text{s}$), checks `spawnChance` ($50\%$). If successful, selects a random child transform containing `"AppleSlot"` and instantiates `applePrefab` to drop under gravity.

---

## Public API / Important Methods

* `TimeManager.Pause()`: Sets `isPaused = true` and `Time.timeScale = 0f`.
* `TimeManager.Play(float speedMultiplier)`: Resumes clock and sets `Time.timeScale = speedMultiplier`.
* `TimeManager.GetTimeString()`: Formats current time as `"HH:MM"` military string.
* `EnvironmentManager.BaseTemperature`: Returns current ambient temperature ($^\circ\text{C}$).
* `DayNightCycle.SunDirection`: Returns directional sunlight vector.
* `DayNightCycle.IsDaylight`: Returns true if sun light intensity $> 0.1$.
* `HeatSource.GetHeatBonusAt(Vector3 observerPosition)`: Calculates linear distance falloff warmth bonus.
* `FellableTree.Fell()`: Triggers physical tree destruction and resource spawning.
* `FellableTree.DropsResource(ResourceType type)`: Returns true if this tree yields the given resource.

---

## Important Invariants
* **Singleton Lifecycle**: `TimeManager`, `EnvironmentManager`, and `DayNightCycle` enforce single-instance lifecycle (`Destroy(gameObject)` on duplicate awakening).
* **Registry Coupling**: `FellableTree` must have a valid [`ResourceRegistry`](file:///c:/UnityProjects/LifeEngine/Assets/Scripts/World/ResourceRegistry.cs) assigned to resolve resource drop prefabs.

---

## Configuration / Tunables
* `realSecondsPerGameMinute`: $1.0\text{ s}$.
* `FruitTree.spawnInterval`: $30.0\text{ s}$, `spawnChance`: $0.5$.
* `FellableTree.harvestDurationMinutes`: In-game harvesting duration ($1.0\text{ min}$ default).

---

## Known Limitations
* **Global Uniformity**: Base temperature is uniform across the entire scene and does not vary by altitude or local geography.
