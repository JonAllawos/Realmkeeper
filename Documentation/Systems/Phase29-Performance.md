# Phase 29: performance (overland ground and its cache)

**Conditions:**
- Unity 6000.0.47f1 **editor**, Play mode tests driven from the open editor. Seed 123456789.
- **No development or player build was made.** None of these figures are player-build numbers; development-build validation is outstanding (see the end).
- "Measured" = read from the game's own timing logs or the profiler during the runs named. "Estimate" = derived, not measured.

## Ground preparation (measured)

How long `OverlandWorld.Build` spends on the ground (the "prepared in" and "Unity Terrain … in" log lines).

| Ground | Ground preparation | Source |
|---|---|---|
| Old mesh ground (Phase 23 chunks) | 44–51 ms (30 ms with no trees, cache test) | Phase 23 run before the change; `Phase29TerrainCachePlayModeTests` |
| Unity Terrain, serial build | 672–745 ms (first 891 ms) | Phase 23 and 24 runs |
| Unity Terrain, parallel build, no cache | 99–148 ms | Phase 23 run |
| Unity Terrain, cache **miss** (build and store) | 99–142 ms | Phase 23 and 24 runs; cache tests |
| Unity Terrain, cache **hit** | 1 ms for the terrain; 3–12 ms for the whole overland setup | Phase 23 and 24 runs; cache tests |

A miss, broken down (measured): heights sampled 11–26 ms, heightmap set 66–86 ms, colour and weights 18–27 ms, textures 1 ms, terrain object and collider 1–2 ms.

## Whole entry into the country (measured, and variable)

From "Out to the country" to the first frame there ("in the country of … built in"). Same Phase 23 sequence and arrival points, before and after the cache.

| Entry | Parallel, no cache | With the cache | Cache |
|---|---|---|---|
| First entry (740, 3100) | 646 ms | 653 ms | miss |
| Same region again (2540, 2140) | 561 ms | 346 ms | hit |
| Same region again (2500, 2420) | 278 ms | 167 ms | hit |
| After save and load (2500, 2420) | 290 ms | 347 ms | miss (correct: a loaded world) |
| A different region, first time (Barrowfield) | 400 ms | 211 ms | miss |
| Back after visiting another region (500, 4940) | 210 ms | 419 ms | hit |
| Barrowfield again | 480 ms | 112 ms | hit |
| Back again (500, 4940) | 219 ms | 116 ms | hit |
| The ancient realm, first time | 203 ms | 208 ms | miss |
| Back to the vale (1626, 4700) | 210 ms | 344 ms | hit |
| (660, 3260) | 476 ms | 376 ms | hit |

**Variability.** The whole entry also includes the sites, the settlement silhouettes, trees and grass, near-chunk streaming and garbage collection, and swings by ±200 ms between runs at the same point. Some hits are slower than the matching uncached runs for that reason. The clean measure of the cache is the ground preparation: about **100–130 ms saved per repeat entry**.

## Memory

| Item | Value | Kind |
|---|---|---|
| One cached region (`TerrainData` + both textures, `Profiler.GetRuntimeMemorySizeLong`) | 10.1 MB (7.4 MB before the colour texture became half-float) | Measured |
| Two cached regions | 14.8 MB (before the half-float change) | Measured |
| Three cached regions (the capacity) | 30.2 MB | Measured (Phase 23 run, after the half-float change) |
| The PhysX heightfield of a terrain collider | Not measured; on the order of 4 MB for 1025² samples | Estimate. Exists per terrain object, not per cache entry: freed with the overland world, rebuilt on each entry |
| Managed sample arrays (about 6 MB per build) | Released after the build; not retained | By construction |

## Tests run (focused; full suites not run)

| Test | Kind | Result |
|---|---|---|
| `Phase29TerrainCachePlayModeTests` (3): miss then hit with the same heights and textures; drawn ground and collider within 5 mm of `HeightAt`, built or cached; ground weather on a cached material; region switching and LRU eviction; a new terrain object or a loaded save never served old ground; the mesh fallback on the same heights, cache untouched | Unity PlayMode | 3/3 pass |
| `Phase23PlayModeTests`, `Phase24PlayModeTests`: country travel, every transition, save and load, region switches, ancient realm, landmarks, encounters, ground weather | Unity PlayMode | Pass |
| `Phase29GatePlayModeTests`: settlement gates and transitions | Unity PlayMode | Pass |
| `Phase29OverlandSnapshotTests` (2): the immutability guarantee | .NET Release | 2/2 pass |
| `Phase29GateClearanceTests` (5) | .NET Release | 5/5 pass |
| `Phase29TerrainCaptureTests` (2): capture-only, both ground paths | Unity PlayMode | Captured; asserts nothing about likeness |

## Visual comparison (screenshots, `Documentation/Screenshots/Phase29/`)

Same views of the same places, at 10:00, for both paths (`p29_terrain_*` and `p29_mesh_*`): settlement approach, countryside, hills and steep ground, a road and bridge, wet ground, snow, the region from above.

**First capture (`before-parity/`):** not equivalent. The Unity Terrain was markedly darker in every view, and its roads lost their colour.

**After the parity correction (2026-11-16):** the countryside, hills, road and bridge, settlement approach and overview pairs match by eye in hue, brightness, shading, grain, roads and the bridge deck. A controlled render of a level plain measures the terrain at 96 % of the mesh ground's brightness, lit and ambient-only. Small differences remain: smoother lake shores and road edges (5 m sampling against 10 m), and smoother faceting on rocky knolls (heightmap normals).

**After the ground-weather decision (2026-11-16):** the wet and snow pairs, now driven through the game's own conditions and the atmosphere's real pass, match as well: both grounds darken when wet and whiten under deep snow. The earlier wet and snow captures are kept in `before-weather/`.

**Unified ground weather (2026-11-16):** every view is now taken under a stated deterministic fixture (`GroundWeatherFixtures`: the region's weather record and a matching overcast sky with nothing falling; playback paused). Two views were added: `08_road_and_bridge_wet` and `09_road_and_bridge_snow`. All nine pairs match; the set before this pass is in `before-unified/`. Under deep snow the roads and bridge decks are fully white on both grounds (ADR-0122).

## Not measured

- **A development build.** `PerfBuild.Build` needs the editor closed, and its probe (`-rkPerfProbe`) measures fixed town poses only; measuring first entry and cached re-entry needs an overland scenario added to the probe.
- Frame time while walking on the Unity Terrain against the mesh ground (only the Phase 23 test's walking sample ran, which asserts but does not compare).
- GPU cost of the terrain's LOD (`heightmapPixelError` 4) against the chunk meshes.
