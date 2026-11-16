# ADR-0122: The overland ground is a Unity Terrain fed by the game's own heights, cached per region

**Status:** Accepted for Phase 29. Colour, sunlight, roads and ground weather now match the mesh ground in all seven
matched screenshot pairs (see "Visual parity"). Owner hand-play and a development-build measurement outstanding.
(2026-11-16)

## Context

- The owner asked whether MapMagic 2, Gaia or MicroSplat could replace the terrain.
- The overland's heights are game data: `OverlandTerrain.HeightAt` (Domain) is read by travel, walkability, roads, settlement layouts and gates, places, and the .NET tests.
- MapMagic and Gaia generate their own heights; MicroSplat textures Unity terrains only. Adopting any of them as the source of the ground would create a second source of terrain truth.
- Phase 23's renderer drew the ground as 640 m chunk meshes (`Realmkeeper/VertexColorLit`): a coarse mesh everywhere, a 10 m detail mesh with a `MeshCollider` near the player.

## Decision

1. **The overland ground is drawn as one Unity Terrain per region, filled from `OverlandTerrain.HeightAt`.**
   - Heightmap 1025² over a 128-cell region (5.12 km): a sample every 5 m. One `TerrainCollider`.
   - Shader `Realmkeeper/TerrainGround` (a port of `VertexColorLit`): biome colour and dirt/rock weights from two region textures (10 m texels), the ambientCG grain, Phase 24 wet ground and snow, the game's fog.
   - The material `Art/Resources/RkTerrainGround.mat` carries the shader into builds.
   - The 640 m chunks still stream trees, grass and barriers near the player.
   - Heights and colours are sampled in parallel (read-only data, hashed noise); every Unity object is made on the main thread.
2. **The old mesh ground stays** behind `OverlandWorld.UseUnityTerrain` (default `true`), unchanged, for comparison and as a fallback. Both paths read the same `HeightAt`.
3. **The built terrain assets are cached per region** (`OverlandTerrainCache`), as below.

## The cache

### What is cached, and what is not

| Kept | Rebuilt on every entry |
|---|---|
| The `TerrainData` (heightmap), the biome-colour texture, the ground-weights texture, the terrain's base height | The terrain `GameObject` and its `TerrainCollider` (1–2 ms), the ground material, water, roads, edges, trees |

### Key and the guarantee behind it

- **Key: the session's `OverlandTerrain` object itself** (held by a weak reference), plus a fingerprint.
- **Why identity is enough:** an `OverlandTerrain` is an immutable snapshot.
  - Every field is `readonly`. Its cells and corner heights are computed once, in the constructor.
  - Its noise is a pure hash of its `readonly` seed.
  - The only thing it reads from its map afterwards is the map's `Width` and `Depth`, which are get-only.
  - So nothing can change its heights, biomes, fords, kind, seed or size. Even an edit to its `RegionMap` after construction changes nothing it answers.
  - Verified by `Phase29OverlandSnapshotTests` (.NET): every corner, every cell and 4,000 further points are unchanged after the map is rewritten; reflection finds no settable property or non-readonly field.
- **The lifecycle that gives new ground a new object:**
  - `WorldSession.GetOverlandTerrain` makes one terrain per region and keeps it for the session.
  - `WorldSession.Replace` (a new, loaded or reconnected world) clears them all, so the next entry brings a new object that no entry matches.
  - `RegionMap` mutators run only during world generation (`RegionGenerator`). `RegionMapEditor` is not used at runtime (only a generation test).
- **The fingerprint** (size, kind, heightmap resolution, texels per cell, `FormatVersion`, and a 33 × 33 grid of heights to the millimetre and biomes) is an **integrity check, not the invalidation mechanism**. It cannot detect arbitrary changes and does not need to, given the guarantee above. A mismatch drops the entry; it is never reused.

### Invalidation rules

| Event | Result |
|---|---|
| A new, loaded or replaced world | The session's terrain objects are new: every later entry misses and rebuilds |
| The old world's terrain is collected | Its entry is dropped when found dead |
| Unity unloads a cached asset | The entry is dropped (assets are marked `DontUnloadUnusedAsset` to prevent this) |
| Fingerprint mismatch on the same object | The entry is dropped; the region rebuilds |
| The terrain build changes what it produces | Bump `OverlandTerrainCache.FormatVersion` |
| Play mode starts (domain reload off) | The cache starts empty |

### Memory and eviction

- **Capacity: 3 regions** (the region walked, the one before, and one more), least recently used evicted first. A lowered capacity applies at the next lookup.
- **Ownership:** the cache owns what it holds and destroys it on eviction (at the end of the frame). An overland world never destroys cache-owned assets.
- The region on screen is always the most recently used, so with capacity ≥ 1 it cannot be evicted while shown.
- **Threads:** main thread only (Unity objects); a lock keeps the bookkeeping whole.

## Consequences

- Repeat entry into a region costs 1–12 ms of ground preparation instead of about 100–140 ms (measured, editor).
- Up to 30.2 MB is retained with all three regions cached (measured; see `Systems/Phase29-Performance.md`).
- Each terrain object rebuilds its PhysX heightfield; that memory is not in the cache's figure.
- MapMagic, Gaia and MicroSplat are not adopted.

## Visual parity (found by the Phase 29 screenshots; corrected 2026-11-16)

The first matched screenshots (`Documentation/Screenshots/Phase29/before-parity/`) showed the Unity Terrain much darker than the mesh ground, with roads in green or black. Three causes were found and corrected:

| Defect | Root cause (verified) | Correction |
|---|---|---|
| Darker colours | `ColourAt` returns colours already in linear space (vertex colours are not converted by Unity). They were stored in an **sRGB** texture, so the GPU converted them to linear a second time | The biome colour texture is **linear RGBAHalf**: it holds exactly the vertex colours' values, including those above 1 (459 sampled texels) that an 8-bit texture would clip. Read back from the GPU: within 0.05 % of `ColourAt` |
| No sunlight on the terrain at all | URP's `GetMainLight()` multiplies the sun by `unity_LightData.z`, a per-renderer culling flag, which a Unity Terrain drawn through a custom shader receives as 0. Measured: the terrain's lit value equalled its ambient-only value exactly | `TerrainGround` applies the sun's shadow attenuation but not that flag (the sun is directional and never culled from the ground). Measured after: 96 % of the mesh ground, lit and ambient-only |
| Roads green or black; bridge decks lost | Roads (and the bridge decks, which are road vertices over water) were drawn with `_ground`, the terrain material, which ignores vertex colours | With the Unity Terrain, roads get their own `Realmkeeper/VertexColorLit` material with the same grain; the mesh fallback is unchanged (roads share the ground's material) |

`OverlandTerrainCache.FormatVersion` is now 2. Cache entries are 10.1 MB per region (measured), up from 7.4 MB.

### Ground weather: one path for both grounds (decided and implemented 2026-11-16)

**Decision (owner, on Claude's recommendation):** ADR-0115's `_Wet` and `_Snow` own the overland ground's weather, on the
mesh ground and the Unity Terrain alike, roads included. The atmosphere's per-visit copies keep settlements, roofs and foliage.

- `ExplorationAtmosphere.Adopt` skips every `OverlandWorld`'s `GroundMaterial` and `RoadMaterial`.
- The conditions-to-material mapping moved, unchanged, from the controller into `OverlandWorld.ApplyGroundConditions`.
- `Phase29GroundWeatherPlayModeTests` (2): out in the country, real wet, deep-snow and dry conditions (`GroundConditions.Of`)
  are applied and the atmosphere's real weather pass runs. Every ground and road surface (77 on the mesh ground, 2 on the
  terrain) is still drawn with its own material and carries the conditions' values.
- The matched wet and snow screenshots now agree; the mesh ground shows lying snow for the first time.

**Verified across every state (Phase 29 follow-up, 2026-11-16).** Architecture: the simulated `GroundConditions`
(`WorldSession.GetGroundConditions`, ADR-0115) is the one authority; `OverlandWorld.ApplyGroundConditions` is its one
presentation for the ground, the roads and the bridge decks they carry (absolute `_Wet` and `_Snow`, never multiplied; ford
barriers when flooded). The controller applies it on entering the country and on every change. The atmosphere presents the
sky, light, fog, precipitation, settlements and foliage, and does not touch the overland ground or road materials.

| State (deterministic fixture) | `_Wet` | `_Snow` | Fords flooded | Ford barriers (terrain / mesh) |
|---|---|---|---|---|
| Dry | 0.00 | 0.00 | no | 10 / 10 |
| Wet | 0.30 | 0.00 | no | 10 / 10 |
| Muddy | 0.76 | 0.00 | no | 10 / 10 |
| Flooded | 0.96 | 0.00 | yes | 12 / 12 |
| Frozen | 0.40 | 0.00 | no | 10 / 10 |
| Snow | 0.00 | 0.50 | no | 10 / 10 |
| Deep snow | 0.00 | 1.00 | no | 10 / 10 |

Shown twice over, each followed by three atmosphere passes: nothing erased, no material replaced, no `_BaseColor` changed.
Re-entry (from the cache) and a save and load show the simulated ground again, not a stale look. Test:
`Phase29GroundWeatherPlayModeTests.EveryGroundState_ShowsTheSameOnBothGrounds_SurvivesTheAtmosphere_AndTheAuthorityReturnsOnReEntryAndLoad`.

**Presentation limits kept as they are (ADR-0115, not expanded here):** frozen ground looks like wet ground (no frost look); a
flood shows as ford barriers and very wet ground (no rising water); under deep snow the roads and bridge decks whiten
completely and cannot be seen, on both grounds alike. **Owner decision (2026-11-16): leave as is**; roads may vanish under deep snow.

The record of the problem as found:

- `ExplorationAtmosphere.Adopt` gives every **renderer** whose name or material matches a ground word its own per-visit material copy, and shows weather by tinting the copy's `_BaseColor`. It runs again on every weather change (`AdoptScene`).
- The mesh ground's chunks and the roads are renderers, so they draw those copies. `OverlandWorld.SetGroundWeather` (ADR-0115: `_Wet`, `_Snow`) writes to the original material, so on the mesh ground it is visible only until the next adoption. In the matched screenshots, a manual wet or snow setting does not show on the mesh ground at all.
- A Unity **Terrain is not a renderer**, so it is never adopted. It shows `_Wet` and `_Snow` as ADR-0115 intends, but not the atmosphere's tint.
- So under real weather the two grounds looked different in wet and snow. This predated Phase 29 for the mesh ground: its lying snow was never visible, because the atmosphere's tint multiplies the ground's colour by near-white and the ground shader has no base texture to swap for snow.

## Alternatives rejected

- **MapMagic 2 / Gaia as the source of the ground:** a second source of terrain truth; Asset Store per-seat licences; Gaia is paid.
- **MicroSplat:** textures only, for Unity Terrain layers; the Realmkeeper shader already gives the look and the ground weather.
- **Cache keyed by region ID and seed:** would survive a world reload and could serve the old world's ground if a region ID were reused; object identity cannot.
- **Caching only the sampled arrays:** keeps the 65–88 ms heightmap setup on every entry.
