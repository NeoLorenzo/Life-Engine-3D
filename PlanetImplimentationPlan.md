# Life Engine Flat Planet Simulation Implementation Plan

This planet generator is an addition to the existing Life Engine project, not a replacement for it. The purpose of this subsystem is to create a realistic flat, square planetary environment in which the current and future life simulation occurs: terrain, climate, water, biomes, resources, navigation surfaces, and ecological constraints.

The existing Life Engine systems should remain first-class:

- Human agents, behavior trees, perception, memory, hunger, sleep, thermal comfort, crafting, and resources remain part of the main simulation.
- The planet system provides environmental truth that those systems can query.
- Existing scenes, prefabs, ScriptableObjects, resources, and agent logic should be migrated onto or connected to the planet incrementally, not discarded.
- The final project goal is realistic life simulation on a realistic flat planet, with the planet acting as the world model and ecological substrate.

The planet addition targets an explorable Earth-like procedural world with:

- A flat square chunked terrain mesh on the Unity X/Z plane.
- Procedural tectonic plates, continents, oceans, mountains, climate, biomes, rivers, and vegetation adapted to a finite square world.
- Ground-level exploration and high-altitude or strategic map viewing.
- LOD, culling, streaming, and shader choices that keep performance stable.
- A data model that can later support custom flat planets, editable settings, simulation UI, and Life Engine agent/resource queries.

The world is intentionally not spherical. Do not implement cube-sphere topology, radial gravity, spherical tangent math, horizon culling from curvature, sea-level shells, or atmosphere/cloud shells. Everything should be expressed through deterministic 2D world coordinates plus height.

## Current Project Fit

This Unity project already has useful foundations that should be preserved and integrated:

- `Assets/Scripts/World` for world simulation code.
- `Assets/Scripts/Humans` for simulated human behavior, locomotion, perception, memory, and needs.
- `Assets/Scripts/AI` for the behavior tree engine.
- `Assets/Shaders` for URP Shader Graph assets.
- `Assets/Materials`, `Assets/Textures`, and existing ground texture packs.
- `Assets/Models/Trees`, `Assets/Models/Vegitation`, and low-poly foliage assets.
- `Assets/ResourceRegistry.asset` and world resource scripts for logs, sticks, stones, tools, and gatherable objects.
- URP 17.3.0 and Unity AI Navigation already installed.

Recommended new folder layout:

```text
Assets/Scripts/World/Planet/
  Core/
  Generation/
  Tectonics/
  Climate/
  Hydrology/
  Rendering/
  Vegetation/
  Debug/

Assets/Materials/Planet/
Assets/Shaders/Planet/
Assets/Textures/Planet/
Assets/Prefabs/Planet/
Assets/ScriptableObjects/Planet/
```

## Coordinate Contract

All generation and queries use a flat square coordinate system:

- The playable planet covers `[-worldHalfSize, worldHalfSize]` on X and Z.
- Y is elevation.
- Normalized planet coordinates are `uv = (x, z) / worldSize + 0.5`, with `(0, 0)` at the southwest corner and `(1, 1)` at the northeast corner.
- The center of the planet is `(0, 0, 0)` in Unity world space.
- Chunks are axis-aligned square heightfield patches.
- North/south climate bands use normalized Z unless a custom climate axis is configured.
- East/west wrap is optional and should be disabled by default. North/south wrap should stay disabled for a square finite world.

Edge behavior must be explicit because a flat square planet has borders. Choose one default and expose it in `PlanetSettings`:

- `OceanBorder`: terrain slopes down to ocean near all edges. Recommended first implementation.
- `CliffBorder`: terrain ends at steep cliffs or void.
- `InvisibleWall`: gameplay stays inside bounds while terrain can continue visually.
- `ToroidalDebug`: optional wraparound mode for testing infinite-feeling procedural systems, not the default project target.

## High-Level Build Order

Build in this order. Each phase unlocks the next and establishes the technical foundation needed by later systems.

1. Generate a square heightfield from chunked X/Z grid coordinates.
2. Build a deterministic planet data layer that can answer "what is here?" for any flat world position.
3. Generate tectonic plates using 2D spaced seeds, KD-tree or grid nearest lookups, and drift/merge passes.
4. Generate terrain height from plate-aware noise plus plate boundary stress.
5. Add chunk LOD, frustum/distance culling, streaming, low-res overview tiles, and terrain skirts before increasing resolution.
6. Simulate climate: north/south temperature bands, altitude cooling, sun strength, precipitation bands, rain shadows, and noise.
7. Assign coherent biomes using a second 2D spatial Voronoi pass, neighbor smoothing, and edge blending.
8. Split terrain and water into separate flat render surfaces.
9. Generate rivers as graph paths from mountain sources to coastal mouths or major lakes, then carve terrain.
10. Add terrain shaders, planar textures, normals, biome weights, clouds, fog, sun/moon lighting, and high-altitude map rendering.
11. Add vegetation with biome-specific density, species rules, instancing, and LOD.
12. Integrate Life Engine agent spawning, environmental queries, resource placement, debug views, settings, and generation UI.

## Core Architecture

### Main Components

- `PlanetSettings`: ScriptableObject containing seed, world size, chunk size, resolution, plate counts, water level, edge mode, climate settings, biome definitions, river settings, vegetation settings, and rendering options.
- `PlanetGenerator`: Orchestrates generation phases and owns the deterministic seed.
- `PlanetData`: Immutable generated data shared by chunks and shaders: plates, climate, biome map, river graph, edge masks, and low-res overview samples.
- `PlanetChunk`: MonoBehaviour for one rendered square terrain chunk.
- `PlanetChunkManager`: Creates, pools, updates, enables, disables, streams, and LOD-switches chunks around the viewer.
- `SquarePlanetGrid`: Converts between chunk coordinates, local vertex coordinates, normalized UVs, and Unity X/Z positions.
- `TerrainSampler`: Pure service that evaluates height, slope, plate, biome, water, river, and vegetation density at any `Vector2` world position.
- `PlanetWaterRenderer`: Separate ocean plane, lake meshes, river meshes, shore foam, and underwater effects.
- `PlanetSkyWeatherRenderer`: skybox, cloud layers, time-of-day colors, fog, high-altitude view settings, and sun/moon lighting.
- `PlanetDebugOverlay`: Toggles views for plates, stress, elevation, temperature, precipitation, biomes, river basins, chunk LOD, streaming, and edge masks.

### Data Principles

- Every generator must be deterministic from `PlanetSettings.seed`.
- Store compact global systems and sample them per chunk instead of storing per-pixel global planet data at final resolution.
- Chunks should be disposable render products. The source of truth is `PlanetData` plus `TerrainSampler`.
- Keep generation decoupled from GameObjects. Most algorithms should be plain C# classes that can be unit tested.
- Use double precision for large world coordinate math where precision matters, then convert to float for Unity mesh buffers.
- Use floating origin or origin rebasing if the square world becomes large enough for Unity float precision to cause jitter.

### Life Engine Integration Contract

The planet subsystem should expose stable query services to the existing simulation instead of forcing agent code to understand tectonics, chunks, shaders, or river generation internals.

Recommended services:

- `IPlanetSurfaceQuery`: height, normal, slope, water depth, walkability, nearest valid surface point, and local X/Z-to-world conversion.
- `IPlanetClimateQuery`: temperature, precipitation, wind/fog/weather later, daylight exposure, shade potential, and biome at position.
- `IPlanetResourceQuery`: biome-appropriate resource spawn weights, nearby natural resources, regrowth rules, and gatherable density.
- `IPlanetNavigationQuery`: local neighbor samples, obstacle/water/slope costs, and short-range path requests for flat terrain.
- `IPlanetTimeOfDayQuery`: sun direction, local light level, season/day phase later, and sky/weather values.

Existing systems should depend on those query surfaces:

- `HumanBrain` reads temperature, shade, wetness, biome, and time-of-day data for metabolic and thermal decisions.
- `HumanPerception` uses planet-aware line of sight, terrain normals, vegetation, water, and resource locations.
- `HumanLocomotion` receives surface normal and walkability data so movement conforms to terrain height and slope.
- `EnvironmentManager` becomes the bridge between global Life Engine state and generated planet climate/time systems.
- `ResourceRegistry` remains the catalog of resource types, while the planet controls where those resources naturally appear.

### Current System Compatibility Requirements

The flat planet plan is intentionally compatible with the current Life Engine systems that assume a normal Unity flat world:

- Keep gravity as `Vector3.down` and character up as `Vector3.up`.
- Keep human steering in X/Z space. Existing `HumanLocomotion` already zeros Y movement, rotates around `Vector3.up`, uses a `NavMeshAgent` as a path calculator, and moves the `Rigidbody` manually.
- Generated terrain chunks must have colliders on the configured ground layer so raycasts, foot IK, object placement, shade checks, and editor brushes can detect the surface.
- Generated walkable terrain must be included in a NavMesh build path. Add `NavMeshSurface` or an equivalent runtime NavMesh baking component for loaded chunks or active simulation regions.
- Water, cliffs, steep slopes, and world-edge blocking zones must be excluded from the walkable NavMesh or given high traversal cost.
- Preserve current NavMesh area assumptions. Existing behavior code uses `NavMesh.AllAreas` for general movement and area bit `1 << 3` for room/shelter logic, so generated terrain should use the default walkable area and shelters should continue to mark their room area.
- Preserve current layer assumptions unless the scripts are refactored: layer 6 is treated as walls/obstacles, layer 9 as trees, resources/food/threats/heat sources are found through configured layer masks, and shade checks include default, wall, and tree layers.
- Resource and tool prefabs should spawn at `TerrainSampler.Sample(worldXZ).Height` plus a small placement offset, not at fixed Y values.
- `ResourceItem` recovery should eventually query the terrain surface below the item instead of teleporting fallen objects to `y = 1.0f`.
- Climate integration should extend `EnvironmentManager.BaseTemperature` with local planet temperature, altitude, shade, water, and biome data instead of replacing the manager outright.

This means the flat planet is a better fit than a spherical planet, but compatibility still depends on producing ordinary Unity colliders, layers, NavMesh data, and Y-up world positions for generated content.

## Phase 1: Square Grid and Chunked Terrain

### Goal

Create a flat square planet made from independently generated terrain chunks on the X/Z plane.

### Implementation Steps

1. Create `SquarePlanetGrid`.
2. Represent the square world as integer chunk coordinates `(chunkX, chunkZ)`.
3. Define `worldSize`, `chunkSize`, `chunksPerSide`, and `verticesPerChunkSide`.
4. For each chunk, generate a fixed-resolution mesh, for example `33 x 33` vertices for `32 x 32` quads.
5. Convert local grid coordinates to world positions:

```csharp
Vector2 worldXZ = grid.ChunkVertexToWorldXZ(chunkCoord, localVertexCoord);
PlanetSample sample = terrainSampler.Sample(worldXZ);
Vector3 position = new Vector3(worldXZ.x, sample.Height, worldXZ.y);
```

6. Generate UVs from local chunk coordinates and optional global UVs from normalized planet coordinates.
7. Generate normals, tangents, biome vertex data, and optional debug colors.
8. Add collider only for chunks near the player or active simulation agents.

### Design Rationale

Start with chunked terrain because the planet must support high resolution, local mesh generation, independent colliders, culling, streaming, and LOD. Chunking is not an optimization afterthought; it is the base architecture.

### Deliverable

A visible low-poly square terrain world with chunk boundaries optionally visible, generated from seed and world-size settings.

## Phase 2: Planet Sampling and Mesh Pipeline

### Goal

Separate "where is the planet data?" from "how is the mesh rendered?" so chunks can be regenerated at any resolution or LOD.

### Core API

Create a sample struct:

```csharp
public struct PlanetSample
{
    public Vector2 WorldXZ;
    public Vector2 NormalizedUV;
    public float Height;
    public float WaterDepth;
    public int PlateId;
    public float PlateBoundaryDistance;
    public float ConvergentStress;
    public float DivergentStress;
    public float TransformStress;
    public float Temperature;
    public float Precipitation;
    public int PrimaryBiomeId;
    public int SecondaryBiomeId;
    public float BiomeBlend;
    public float RiverInfluence;
    public float ForestDensity;
    public float EdgeFalloff;
}
```

`TerrainSampler.Sample(Vector2 worldXZ)` should eventually fill all values, but each phase can add fields incrementally.

### Mesh Rules

- Mesh vertices sample terrain height from `TerrainSampler`.
- Vertex colors should carry biome weights, not final colors.
- Normals should sample neighboring heights in X/Z instead of relying only on per-chunk mesh normals.
- Mesh generation should run off the main thread where possible, but GameObject and Mesh assignment must happen on the main thread.

## Phase 3: Tectonic Plate Generation

### Goal

Generate Earth-like tectonic plates that are coherent, irregular, and useful for continents/mountains on a square finite world.

### Data Model

```csharp
public class Plate
{
    public int Id;
    public Vector2 CenterXZ;
    public Vector2 Motion;
    public bool IsOceanic;
    public float ElevationBias;
    public float CrustThickness;
}
```

Use a global set of plate region seeds:

```csharp
public struct PlateSeed
{
    public Vector2 PositionXZ;
    public int PlateId;
}
```

### Generation Steps

1. Generate blue-noise or Poisson-disc seed points across the square world.
2. Add controlled jitter so plates are not a visible grid.
3. Pick major plate centers from the spaced seeds.
4. Assign seeds to the nearest plate center using a KD-tree, spatial hash, or uniform grid acceleration structure.
5. Mark some plates as oceanic and some as continental.
6. Apply plate merge/drift simulation:
   - Pick a random point inside a plate.
   - Grow a radius until it touches a different plate.
   - Shift or convert points inside the radius toward the selected plate.
   - Repeat many times to get organic plate edges.
7. Spawn minor plates only near major plate boundaries, not inside major plate interiors.
8. Assign each plate a 2D motion direction.
9. Apply optional edge bias so oceanic plates are more common near `OceanBorder` edges.

### Spatial Index Requirement

Use KD-tree nearest-neighbor lookup, a spatial hash, or another spatial index before scaling resolution. Dense plate and biome generation require many nearest-point queries, and spatial indexing keeps those lookups interactive instead of checking every sample against every candidate.

### Boundary Detection

For any sample position:

1. Find the closest plate seed.
2. Find the second and third closest candidate seeds.
3. The boundary is near the perpendicular bisector between closest seeds.
4. Track distances to multiple boundaries where three plates meet.
5. Smooth boundary influence with a configurable width.

Use geometric boundary distance rather than simple neighbor-color edge detection. Geometric distances produce smooth terrain transitions and remain stable where several plates intersect.

### Plate Stress

For two neighboring plates:

- Compare each plate's 2D motion relative to the boundary normal.
- If plate motions point toward each other, mark convergent stress.
- If they pull apart, mark divergent stress.
- If they slide sideways, mark transform stress. Transform can be used later for faults/earthquakes but can be ignored for first terrain pass.

Use smooth stress values rather than binary flags so mountain height transitions remain continuous.

## Phase 4: Continents, Oceans, and Base Elevation

### Goal

Make continents follow tectonic plate structure while still looking organic on a square map.

### Terrain Function

Combine:

- Per-plate continent noise.
- Plate elevation bias.
- Oceanic/continental plate type.
- Multi-octave fractal noise.
- Boundary smoothing.
- Edge ocean falloff.
- Water cutoff.

Suggested first pass:

```text
baseNoise = fractalNoise(worldXZ * continentScale + plateNoiseOffset)
plateBias = continental ? +continentalBias : -oceanicBias
edgeFalloff = oceanBorderMask(normalizedUV)
continentMask = smoothstep(waterCutoff - blend, waterCutoff + blend, baseNoise + plateBias - edgeFalloff)
height = remap(baseNoise) + plateBias - edgeFalloff
```

### Oceanic Plates and Square Edges

Set the base land amount high enough for continents to form, then shift oceanic plates downward. If `OceanBorder` is enabled, blend terrain downward near the outer border so the finite square reads as a planet-sized land/ocean region rather than a hard terrain tile.

### Design Rationale

Sample a different noise offset or domain per plate so continents inherit plate-scale structure. One global noise field produces landmasses that are disconnected from tectonics.

## Phase 5: Mountains, Ridges, Trenches, and Terrain Detail

### Goal

Use tectonic boundaries to place major mountain ranges and trenches, then add sharper ridge detail.

### Boundary Rules

- Continental + continental convergent: large mountain ranges.
- Continental + oceanic convergent: coastal mountains on continental side plus trench on oceanic side.
- Oceanic + oceanic convergent: island arcs and trenches.
- Divergent: volcanic ridges, mid-ocean ridges, and rift valleys.
- Transform: optional fault visuals, mostly no height change in the first version.

### Height Composition

```text
height =
  basePlateElevation
  + continentNoise
  + highFrequencyTerrainNoise
  + convergentStress * mountainProfile
  - subductionStress * trenchProfile
  + divergentStress * ridgeProfile
  + riverCarving
```

### Mountain Detail

Use fractal ridge blending instead of plain Perlin for mountains.

Ridge noise idea:

```text
n = noise(worldXZ)
ridge = 1 - abs(n * 2 - 1)
ridge = pow(ridge, ridgeSharpness)
```

Blend ridge noise only where the mountain mask is strong:

```text
mountainDetail = lerp(perlinDetail, ridgeNoise, mountainMask)
```

### Design Requirements

- Use ridge noise for sharp ridgelines because plain smooth noise creates rounded mountain blobs.
- Clamp or remap the first terrain octave so it controls landmass shape without becoming extreme elevation by itself.
- Apply high-frequency detail mostly in shader normals or only where terrain resolution can support it.

## Phase 6: Chunk Performance, Culling, Streaming, and LOD

### Goal

Make the flat planet explorable without rendering or generating the whole high-resolution world.

### Chunk Visibility

Use flat-world visibility rules:

- Frustum culling through Unity renderers.
- Distance culling for detailed chunks.
- Simulation-interest culling around active humans, cameras, and important world events.
- Optional occlusion culling from mountains or structures later.
- High-altitude view switches to lower-detail overview tiles.

Do not implement horizon culling from curvature. A flat planet has no curvature-based hidden backside.

### LOD Strategy

Use a quadtree over the square world:

- Root chunk covers the full square planet.
- Chunks near the viewer split into four children.
- Each LOD keeps a fixed vertex resolution, but higher LOD chunks cover smaller X/Z areas.
- Far chunks can render low-resolution versions.
- Strategic map or high-altitude view can force large low-res chunks and simpler shaders.

### Crack Fix

When different LOD levels meet, terrain gaps appear. Fix with skirts:

- Duplicate border vertices around each chunk.
- Drop duplicates downward in world Y.
- Add side triangles between the main border and dropped border.

Use skirts as the first crack solution because they are simple, robust, and compatible with independent chunk generation.

### Generation Optimization

Use spiral fill optimization for plate/biome-heavy chunk sampling:

1. Generate from chunk center outward in rings.
2. After each ring, check whether all sampled boundary points share the same plate/biome region.
3. If yes, fill the remaining interior with the same classification and skip expensive lookups.

This is most useful for large chunks where much of the chunk is inside one plate or biome.

### Design Requirements

- Render only nearby or strategically visible chunks because a square planet can expose a large area at once.
- Use streaming budgets so chunk creation is spread over frames.
- Use spatial acceleration in generation systems before raising sample counts.
- Calculate seam normals with neighbor height data so chunk borders shade as one continuous surface.

## Phase 7: Player Movement and Surface Exploration

### Goal

Allow a player or agent to walk around a flat terrain planet.

### Gravity and Grounding

Gravity is constant:

```csharp
Vector3 gravityDirection = Vector3.down;
Vector3 up = Vector3.up;
```

Use a standard or lightly adapted character controller:

- Keep character up aligned to `Vector3.up`.
- Project movement onto the local terrain plane when climbing slopes.
- Use grounded checks downward.
- Snap or slide to local terrain height if using a kinematic controller.
- Reject movement outside planet bounds or apply the selected edge behavior.

### Navigation

Unity NavMesh can work for limited active areas on flat terrain. The first implementation should keep the current `HumanLocomotion` path intact by generating NavMesh data for the active flat terrain area.

Recommended first path:

1. Add a `PlanetNavMeshManager`.
2. Attach or create `NavMeshSurface` data for the loaded chunk group around active humans and cameras.
3. Mark terrain chunks as walkable sources only when their slope and water depth are valid.
4. Add `NavMeshModifier` or area overrides for shelters, rooms, cliffs, water, and blocked world-edge zones.
5. Rebuild NavMesh in throttled batches when chunks stream in, terrain changes, or shelters/resources add obstacles.
6. Keep `HumanLocomotion` using `NavMeshAgent.SetDestination`, `NavMesh.SamplePosition`, `NavMesh.Raycast`, and `CalculatePath` as it does now.

Do not bake one huge NavMesh for the entire planet at final resolution. Use active-region baking first, then add chunk-local walkability samples or hierarchical pathing if agents need to travel across very long distances.

For humans/creatures:

- Query slope, water depth, vegetation density, and obstacles through `IPlanetNavigationQuery`.
- Generate short-range paths on sampled grids around the agent.
- Use high-level goals from Life Engine behavior trees and planet queries for local movement costs.

## Phase 8: Climate Simulation

### Goal

Generate temperature and precipitation values that drive biomes, snow, ice, vegetation, and rivers.

### Temperature

Inputs:

- North/south climate coordinate from normalized Z.
- Configurable climate axis for rotated maps.
- Sun strength.
- Altitude.
- Noise variation.
- Optional local water moderation.

Formula sketch:

```text
latitude01 = abs(normalizedZ * 2 - 1)
equatorWarmth = 1 - latitude01
temperature =
  baseTemperature
  + equatorWarmth * equatorHeat
  + sunStrength
  - altitude * lapseRate
  + temperatureNoise
```

For a square flat planet, "latitude" is a simulation coordinate, not a spherical position. Make the warm band position, width, and axis configurable.

### Precipitation

First-pass pattern:

- Wet near the configured equatorial band.
- Dry subtropical desert bands.
- Gradually wetter toward temperate latitudes.
- Dry/cold near north/south edges if desired.
- Add smooth noise so bands are not straight.
- Add rain shadow after mountains and prevailing winds exist.

Formula sketch:

```text
equatorialWetness = exp(-pow(latitude01 / equatorBandWidth, 2))
desertDryness = exp(-pow((latitude01 - desertLatitude) / desertBandWidth, 2))
temperateRecovery = smoothstep(desertLatitude, poleLatitude, latitude01) * recoveryAmount
precipitation = equatorialWetness - desertDryness + temperateRecovery + precipitationNoise
```

### Future Climate Upgrades

After the core planet is working:

- Rain shadow effect from prevailing winds and mountain barriers.
- Ocean current approximation from nearby water bodies and map-edge oceans.
- Seasonal temperature changes from configurable sun angle.
- Evaporation near warm oceans and large lakes.

## Phase 9: Biome Assignment

### Goal

Assign large, coherent, blended biomes instead of noisy stripes and tiny isolated pixels.

### Initial Biome Map

Define biomes as points in climate space:

```csharp
public class BiomeDefinition
{
    public int Id;
    public string Name;
    public float PreferredTemperature;
    public float PreferredPrecipitation;
    public Color DebugColor;
    public Texture2D Albedo;
    public Texture2D Normal;
    public VegetationProfile Vegetation;
}
```

Examples:

- Desert: hot and dry.
- Tropical rainforest: hot and wet.
- Savannah: hot and medium dry.
- Temperate forest: moderate and wet.
- Steppe/grassland: moderate and dry.
- Tundra: cold and dry.
- Taiga: cold and moderate wet.
- Snow/ice: very cold.
- Swamp: warm/moderate and very wet near water.
- Temperate rainforest: moderate and very wet.

### Coherent Biome Voronoi

Use coherent biome control regions instead of assigning every terrain point directly from temperature and precipitation. Control regions produce large, stable biome areas while climate values still determine which biome each region prefers.

Instead:

1. Scatter biome control points across the square world using blue-noise or jittered grid placement.
2. Each control point samples temperature/precipitation and chooses a biome.
3. Every terrain sample chooses the nearest biome control point through the spatial index.
4. Run neighbor-majority smoothing over control cells several times.
5. Distort lookup positions with low-frequency noise to break straight Voronoi borders.
6. Blend between the two closest biome cells near borders.

### Shader Data

Pass biome weights to the shader instead of only final biome colors. Biome weights preserve biome identity and enable biome-specific textures, normals, and vegetation rules.

For each chunk:

- Determine up to four dominant biomes.
- Assign each biome to RGBA vertex color channels.
- Store weights in vertex colors.
- Send a per-chunk biome texture array/material property mapping channel to biome texture.

High-altitude-view exception:

- Huge overview chunks may include more than four biomes.
- From high altitude, texture detail is not visible anyway.
- Use a simpler color shader for overview or low-res chunks.

## Phase 10: Water, Oceans, Beaches, and Underwater View

### Goal

Render water as its own system instead of baking it into terrain colors.

### Water Surfaces

- Create flat ocean and lake meshes at `waterLevel`.
- Clip or tile water surfaces to visible chunk regions.
- Terrain still exists under the water, exposing trenches and shelves.
- Add shallow-water color based on terrain depth below sea level.
- Add foam or lighter shoreline where water depth is small.
- If `OceanBorder` is enabled, the outer world edge should naturally become ocean.

### Underwater

When the camera is below water level:

- Enable blue/green fog.
- Increase density with depth.
- Tint lighting.
- Reduce visibility.
- Optionally fade sun contribution.

### Atmospheric Fade

Use distance fog so far mountains fade into atmosphere. On a flat world, fog also helps hide far-terrain pop-in and the finite square boundary.

## Phase 11: Rivers, Basins, Canyons, Deltas, and Waterfalls

### Goal

Generate river networks that start in mountains, flow downhill, merge, and reach plausible coastal mouths or major lakes.

### River Mouths

Spawn river mouths first:

1. Identify coastal chunks: mixed land/water, adjacent to mostly-water area.
2. Reject tiny ponds/lakes by checking surrounding water extent.
3. Place mouths at sea-level coastal positions.
4. Store mouth position, basin id, and target water body size.

Spawn mouths at validated coastal water bodies so large rivers terminate in plausible oceans or major lakes.

### Drainage Basins

Initial assignment:

- Each terrain region chooses the nearest valid river mouth.

Improved assignment:

- Prefer mouths on the same tectonic plate.
- Treat mountain ranges as basin dividers.
- Allow basin handoff if a river crosses into another basin.

### River Sources

Spawn sources:

- High elevation.
- High precipitation.
- Mountain or hill regions.
- Not too close to another source unless tributaries are desired.

### River Graph

Represent rivers as nodes instead of dense per-point arrays.

```csharp
public struct RiverNode
{
    public Vector2 PositionXZ;
    public float Elevation;
    public float Width;
    public float Depth;
    public float Flow;
    public int BasinId;
}
```

Connect nodes with edges. Render smooth paths with splines or sampled mesh strips.

### Pathfinding

From each source:

1. Check neighboring sample positions in X/Z.
2. Early path: choose steepest downhill neighbor.
3. Flatter path: include attraction toward basin mouth.
4. Increase sampling radius in flatter areas so rivers turn more gradually.
5. If stuck in a valley above sea level, bias toward the basin mouth or carve a spillway.
6. Stop when reaching ocean, lake, world-edge ocean, or an existing larger river.

### Flow Growth

At each node:

```text
flow = previousFlow + precipitationAtNode * catchmentArea
```

When rivers come close:

- Merge smaller river into larger one.
- Add flow.
- Continue as one graph.

### Carving

Carve riverbeds using a valley profile around spline samples:

```text
distance = distanceToRiverCenterline
profile = 1 - pow(saturate(distance / riverWidth), bankSharpness)
terrainHeight -= profile * riverDepth
```

Use shallow wide profiles for mature rivers and deeper narrow profiles for canyons.

### Features

- Canyons: rare chance where river crosses young uplifted terrain.
- Waterfalls: where slope exceeds threshold and river remains continuous.
- Deltas: near river mouth, split graph into multiple distributaries with shallow sediment islands.

## Phase 12: Terrain Shading and Texturing

### Goal

Move visual detail to the GPU so terrain colors, textures, and fine surface detail are rendered per pixel instead of baked as flat CPU-generated tile colors.

### Master Terrain Shader

Use URP Shader Graph or hand-written HLSL shader with:

- Planar X/Z texture sampling for albedo and normals.
- Optional triplanar projection only on steep cliffs if texture stretching becomes visible.
- Height-based snow.
- Slope-based rock.
- Beach sand near sea level.
- Biome texture blending from vertex color weights.
- River wetness/darkening.
- Snow/ice from temperature and slope.
- Normal map detail.

### Planar Sampling

A flat square planet can use stable planar UVs:

```text
globalUV = worldXZ * textureScale
color = sampleTexture(globalUV)
```

Use world-space UVs for terrain continuity across chunks. Use local chunk UVs only for debug or special effects where seams are acceptable.

### Snow Rules

Snow/ice should depend on:

- Temperature below threshold.
- Altitude.
- Slope.
- Biome.

Keep snow primarily on cold, high, flatter terrain:

```text
snow = coldMask * highAltitudeMask * (1 - steepSlopeMask)
```

### Seam Normals

Calculate lighting with cross-chunk continuity. For normals:

- Sample terrain height slightly north/south/east/west in X/Z.
- Or generate one-vertex neighbor padding for each chunk.
- Use neighbor chunk data when available.

This fixes jagged shading at chunk borders.

## Phase 13: Sky, Clouds, Sun, Moon, and High-Altitude View

### Sky and Atmosphere

Use a skybox, fog, and lighting model rather than a physical atmosphere shell:

- Horizon-to-overhead blue gradient during day.
- Sunset color when sun is near horizon.
- Night transition when sun is below horizon.
- Star texture at night.
- Fog color/density synced with time of day.
- Optional height fog for valleys and water.

Sunset mask:

```text
sunNearHorizon * pixelNearHorizon * pixelAlignedWithSun
```

Multiply these three masks to isolate orange glow near the horizon around the sun.

### Clouds

Use one or more flat or dome-projected cloud layers:

- Large scrolling cloud planes above the world.
- Tileable opacity textures.
- Three noise/texture layers multiplied together for less repetition.
- Slow UV scrolling for motion.
- Bright edge lighting when clouds align with sun.
- Dimmer but visible clouds at night.

High-altitude view:

- Switch to lower-frequency cloud textures or a simplified cloud overlay.
- Reduce or switch post-processing settings.

### Moon

Optional:

- Add a skybox or directional moon object.
- Visible at night or near sun direction.
- Can later support tides or calendar systems as gameplay data.

## Phase 14: Vegetation and Ecosystems

### Goal

Populate biomes with trees, grass, cacti, swamp plants, rainforest foliage, and other species without killing performance.

### Forest Density

For each biome:

- Define forest density threshold.
- Use smooth noise to create patches.
- Smooth forest edges.
- Disable forests in deserts, snow, steep cliffs, underwater regions, and outside active simulation bounds.

```text
forestDensity = smoothstep(threshold, threshold + softness, noise(worldXZ))
forestDensity *= biomeForestMultiplier
forestDensity *= slopeMask
forestDensity *= temperatureMask
forestDensity *= waterMask
```

### Placement

For each visible or simulation-active chunk:

1. Split chunk into candidate grids.
2. Sample density at each candidate.
3. Randomly accept/reject based on density.
4. Jitter accepted positions to break the grid.
5. Align object up to terrain normal.
6. Randomize rotation around Y and randomize scale.

### Species Selection

Each species has:

- Preferred temperature.
- Temperature tolerance.
- Preferred precipitation.
- Precipitation tolerance.
- Biome weight.
- Min/max altitude.
- Max slope.
- Prefab/LOD group.

Use weighted random selection from climate fit.

Examples:

- Desert: cacti, scrub, sparse dry grass.
- Savannah: acacia, dry grass.
- Tundra: low grass, shrubs, dead trees.
- Taiga: pines.
- Temperate forest: oak, birch, shrubs.
- Swamp: willow, birch, reeds.
- Temperate rainforest: dense tall trees, mossy ground foliage.
- Tropical rainforest: palms, bamboo, banana trees, dense undergrowth.

### Rendering

- Use GPU instancing or batched indirect rendering for vegetation.
- Use LODGroups for trees.
- Use crossed billboards for far trees and all grass.
- Only spawn/render vegetation for chunks near the player or active agents.
- Pool instances per chunk.

### Design Requirements

- Use LOD, billboards, pooling, and instancing so large forests remain performant.
- Select vegetation species by biome, temperature, precipitation, altitude, and slope.
- Jitter placement candidates so vegetation forms organic patches instead of visible grids.
- Keep species climate-appropriate so ecosystems support the realism goal.

## Phase 15: Debugging and Tools

### Required Debug Views

Add a debug UI or keyboard toggle for:

- Chunk LOD and quadtree depth.
- Culled vs rendered chunks.
- Streaming queue and generation budget.
- World bounds and edge falloff.
- Plate IDs.
- Plate motion vectors.
- Plate boundary distance.
- Convergent/divergent/transform stress.
- Base elevation.
- Final elevation.
- Water depth.
- Temperature.
- Precipitation.
- Biomes.
- Biome blend weights.
- River mouths.
- Drainage basins.
- River graphs.
- Vegetation density.

### Biome Editor

Build a simple editor window or runtime UI:

- Drag biome points in temperature/precipitation space.
- Enable/disable biomes.
- Preview resulting climate Voronoi map.
- Regenerate biome control cells live.

Build this early because biome tuning is much faster with direct visual feedback.

### Generation Settings UI

Expose:

- Seed.
- World size.
- Chunk size.
- Water level.
- Edge mode.
- Edge ocean falloff width.
- Plate count.
- Minor plate count.
- Oceanic plate ratio.
- Mountain strength.
- Noise scales/octaves.
- Climate axis.
- Equator band position/width.
- Sun strength.
- Biome control point count.
- River count/source count.
- Cloud height/density.
- LOD distances.
- Streaming budget per frame.

## Phase 16: Testing and Validation

### Unit Tests

Add tests for pure generation code:

- Same seed produces same plates.
- World X/Z to normalized UV mapping is stable and bounded.
- Neighboring chunk borders sample identical heights.
- KD-tree or spatial index nearest result matches brute force for random sample sets.
- Biome assignment is deterministic.
- Edge falloff behaves correctly for the selected edge mode.
- River path eventually reaches water, world-edge ocean, or valid merge target.

### Visual Validation

For each generated flat planet, inspect:

- No visible chunk seams.
- No LOD cracks.
- No chunk normal seams.
- Terrain reaches or respects the configured world edge behavior.
- Plate boundaries form organic shapes.
- Minor plates are near boundaries, not trapped in plate centers.
- Continents follow plate layout.
- Oceanic plates mostly sit below sea level with islands possible.
- Mountains mostly appear at convergent boundaries.
- Trenches mostly appear near subduction boundaries.
- Climate bands follow the configured axis without becoming perfectly straight.
- Deserts appear near configured dry bands, not randomly everywhere.
- Biomes form large coherent regions with blended edges.
- Rivers start high, merge naturally, and reach coastlines, lakes, or edge oceans.
- Large rivers terminate in oceans or major lakes.
- Forests form patches, not uniform carpets.
- High-altitude view hides texture/channel limitations.

### Performance Targets

Initial targets for a mid-range desktop:

- First low-res planet preview: under 1 second.
- Full terrain data generation: under 30 seconds for medium settings.
- Ground exploration: 60 FPS target.
- High-altitude/strategic view: 60 FPS target.
- Chunk creation spread over frames to keep traversal smooth.
- No visible hitch when crossing LOD boundaries.

## Cross-System Design Constraints

- Build chunks and LOD from the start because high-resolution flat terrain needs local generation, culling, colliders, and streaming.
- Use X/Z world coordinates and normalized square UVs as the universal sampling contract.
- Use KD-tree, spatial hash, or equivalent spatial indexing for dense nearest-point queries because plates, biomes, and vegetation rely on repeated proximity lookups.
- Spawn minor plates near major plate boundaries because that produces more geologically useful plate interactions.
- Use per-plate noise offsets and plate elevation bias because continents should follow tectonic structure.
- Use geometric nearest/bisector distances for plate boundaries because mountain ranges, trenches, and continent transitions need smooth boundary weights.
- Smooth plate stress because terrain height should transition continuously across tectonic boundaries.
- Use frustum culling, distance culling, streaming budgets, and low-res overview chunks because flat worlds can expose very large visible areas.
- Treat quadtree LOD as core infrastructure because the same planet must work at ground level and high-altitude scale.
- Add terrain skirts because independent chunk LOD levels need a robust crack-covering method.
- Sample neighbor terrain for normals because adjacent chunks should shade as one continuous surface.
- Send biome weights to the shader because biome identity is needed for textures, normals, vegetation, and later ecological rules.
- Use biome control Voronoi with smoothing because climate-only lookup produces unstable, overly thin biome bands.
- Spawn validated coastal river mouths before river sources because rivers need plausible downhill targets.
- Validate water body size around river mouths because major rivers should terminate in oceans or large lakes.
- Use procedural river carving instead of full global hydraulic erosion because chunks must generate independently and efficiently.
- Blend ridge noise into mountain masks because sharp mountain ridges require different detail than smooth landmass noise.
- Use vegetation LOD, billboards, pooling, and instancing because forests and grass require massive object counts.
- Keep edge behavior explicit because a flat square planet has boundaries.

## Suggested Milestones

### Milestone 1: Plain Chunked Flat Planet

- Square chunk renderer.
- Runtime seed/world-size settings.
- Basic height noise.
- Debug chunk grid.
- World bounds and edge-mode debug view.

### Milestone 2: Plate-Based Terrain

- 2D plate generation.
- Plate debug colors.
- Oceanic/continental plate bias.
- Plate-aware continents.
- Boundary stress mountains.
- Edge ocean falloff.

### Milestone 3: Performance Foundation

- Quadtree LOD.
- Chunk pooling.
- Frustum and distance culling.
- Streaming queue.
- Skirts.
- Neighbor-aware normals.
- Low-res overview tiles.

### Milestone 4: Climate and Biomes

- Temperature map.
- Precipitation map.
- Climate axis settings.
- Biome definitions.
- Coherent biome Voronoi.
- Smooth biome blending.
- Debug biome editor.

### Milestone 5: Water and Rivers

- Ocean/lake planes.
- Shallow/coastal water.
- Underwater fog.
- River mouths.
- Drainage basins.
- River graph paths.
- Terrain carving.
- Deltas, canyons, and waterfalls.

### Milestone 6: Visual Polish

- Master planar terrain shader.
- Biome texture blending.
- Snow/rock/beach rules.
- Cloud layers.
- Fog and sky lighting.
- Day/night cycle.
- High-altitude shader path.
- Post-processing profiles for ground and overview views.

### Milestone 7: Ecosystems

- Forest density maps.
- Biome vegetation profiles.
- Species climate fitting.
- Tree/grass placement.
- Instancing and LOD.
- Runtime vegetation culling.

### Milestone 8: Integration With Life Engine

- Planet-aware spawning for existing humans, resources, trees, food, stones, shelters, fires, and crafted objects.
- Surface-aware locomotion and local navigation that lets existing agents move on flat terrain without replacing their behavior tree logic.
- Environmental query API for `HumanBrain`, `HumanPerception`, `EnvironmentManager`, and future life systems.
- Climate-aware thermal comfort values from generated temperature, shade, altitude, weather, time of day, and biome.
- Biome/resource registry integration so resource availability emerges from the planet instead of being manually placed everywhere.
- Planet-aware perception rules for line of sight, hearing radius, shelter detection, food/resource discovery, and threat memory.
- Save/load generated planet settings, seed, and any persistent life-simulation changes made on top of the generated world.
- Compatibility pass to ensure existing Life Engine scenes can still run while the planet subsystem is introduced behind feature flags or dedicated test scenes.

## Recommended First Implementation Slice

Start with a narrow vertical slice instead of trying to implement every system at once:

1. `PlanetSettings`.
2. `SquarePlanetGrid`.
3. `PlanetChunkManager`.
4. `PlanetChunk`.
5. `TerrainSampler` with only base height noise and edge falloff.
6. `PlanetDebugOverlay` with chunk/height/bounds modes.
7. Quadtree LOD plus skirts.

Once that slice is stable and seams/cracks are solved, add tectonics. If advanced climate or rivers are added before the chunk/LOD foundation is solid, they will likely need to be reworked later.

## Definition of Done for the Full Planet System

The realistic flat planet system is complete when:

- A seeded Earth-like flat square planet can be generated deterministically.
- The existing Life Engine simulation still exists and runs on top of the generated planet rather than being replaced by a standalone planet demo.
- Human agents can spawn, perceive, navigate, seek food, seek shelter, manage temperature, gather resources, craft, and interact with world objects using planet-provided environmental data.
- Terrain can be viewed smoothly from ground level and high-altitude overview.
- The configured world edge behavior is visible, stable, and gameplay-safe.
- Continents, oceans, mountain ranges, trenches, climate bands, biomes, rivers, and vegetation all visibly relate to each other.
- Chunk LOD transitions remain watertight and visually continuous.
- Rivers reach plausible water bodies and form networks with tributaries.
- Biomes are coherent and blended, not striped or speckled.
- Ground shader uses texture identities, not only baked colors.
- Vegetation is biome/climate appropriate and performant.
- Debug overlays make generation errors easy to diagnose.
- The system is modular enough to tune settings without rewriting core generation.
