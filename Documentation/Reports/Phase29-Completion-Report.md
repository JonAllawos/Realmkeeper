# Phase 29 completion report

**Date:** 2026-11-16. **Branch:** `feature/phase-0-1-foundation`. **Commits:** `da4fb07` (audio pass), `8c3f094` (Phase 29), `b288ce5` (townsfolk fix, after Phase 29); local only, not pushed.

**Summary.**
- Gates no longer block the way in (ADR-0121): 0 of 60 gates blocked across 10 seeds, down from 3.
- The overland ground is a Unity Terrain filled from the game's own heights, cached per region (ADR-0122).
- It matches the old mesh ground in colour, sunlight, roads and weather across 9 matched screenshot pairs.
- Ground weather has one authority, the simulated ground conditions, on both grounds.
- **Scope:** the owner narrowed Phase 29 to the gate fix and the terrain. The brief's overland exploration content (sections 3–8) is **deferred, not started**.
- **Not done:** hand-play (Claude has not played the game), a development-build measurement, a full Unity suite run.

## 1. Preflight findings

See `Systems/Phase29-Preflight-Audit.md` (written at close-out from the findings recorded during the work).
- **Gates:** gatehouses faced the settlement centre and the wall was cut by a fixed length, so slanted road crossings were blocked.
- **Terrain:** heights are game data (`OverlandTerrain.HeightAt`). MapMagic and Gaia would create a second source of truth; MicroSplat only textures Unity Terrains. None was adopted.

## 2. Systems modified

| Area | Files |
|---|---|
| Gates and walls | `Domain/Settlements/SettlementWalls.cs` (new), `SettlementLayout.cs` (generator v3), `SettlementLayoutGenerator.cs`, `Infrastructure/Persistence/WorldSaveMapper.Layouts.cs` (v2→v3 on load), `Exploration/Presentation/ModularEnvironmentBuilder.Plan.cs` |
| Overland ground | `Exploration/Presentation/OverlandWorld.cs`, `OverlandTerrainCache.cs` (new), `Presentation/Shaders/TerrainGround.shader` (new), `Art/Resources/RkTerrainGround.mat` (new) |
| Ground weather | `ExplorationAtmosphere.Weather.cs` (skips overland ground and road materials), `ExplorationController.OverlandLife.cs` (calls `OverlandWorld.ApplyGroundConditions`) |
| Docs | ADR-0121, ADR-0122, `Systems/Phase29-Performance.md`, `Systems/Overland.md`, this report, the preflight, the checklist; Product Bible amendments A7–A10 and roadmap R2–R3 (owner additions, 2026-11-16) |

## 3. Gates (ADR-0121)

| | Before | After |
|---|---|---|
| Gatehouse direction | Towards the settlement centre | Along its own road through the wall |
| Opening | Fixed per type | max(type opening, road width + 1.2 m) |
| Wall at a gate | Fixed cut along the wall | Cut back to the towers, allowing for slanted crossings |
| Road through the gate | As generated (can jog at the wall) | Straight within 9 m each side; follows a bridge that lands at the gate |
| Blocked gates (60 gates, 10 seeds) | 3 | 0 |
| Old saves | | Straightened once on load, identical to new generation; no schema change |

## 4. The overland ground (ADR-0122)

- One Unity Terrain per region: 1025² heightmap over 5.12 km (a sample every 5 m), one `TerrainCollider`, heights within 5 mm of `HeightAt`.
- Shader `Realmkeeper/TerrainGround`: biome colour and dirt and rock weights from two region textures, the ground grain, wet and snow, the game's fog.
- The 640 m chunks still stream trees, grass and barriers near the player.
- The old mesh ground stays behind `OverlandWorld.UseUnityTerrain` as a fallback. That switch defaults to `true`, so **the game draws the Unity Terrain** (see §11).
- **Region cache:** 3 regions, least recently used evicted, keyed on the immutable `OverlandTerrain` snapshot plus a fingerprint; a new or loaded world never reuses old ground.

## 5. Visual parity and ground weather

| Defect (found by the matched screenshots) | Correction | Measured after |
|---|---|---|
| Terrain darker | Biome colours in a linear half-float texture | Within 0.05 % of `ColourAt`; 459 texels above 1 kept |
| No sunlight on the terrain | Sun applied without URP's per-renderer flag | 96 % of the mesh ground's brightness, lit and ambient-only |
| Roads green or black, bridge decks lost | Roads on their own `VertexColorLit` material | Roads and decks match |
| Wet and snow differed between grounds | One authority (`GroundConditions`), shown by `OverlandWorld.ApplyGroundConditions`; the atmosphere leaves the ground alone | All 7 states identical on both grounds; correct after re-entry and after a load |

## 6. Performance (editor only; `Systems/Phase29-Performance.md`)

| Measure | Value |
|---|---|
| Ground preparation, serial build | 672–891 ms |
| Ground preparation, parallel, cache miss | 99–148 ms |
| Ground preparation, cache hit | 1–12 ms |
| Retained memory, 3 cached regions | 30.2 MB (10.1 MB per region) |

No development or player build was measured.

## 7. Automated test results

| Tests | Kind | Result |
|---|---|---|
| `Phase29GateClearanceTests` (5) | .NET Release | 5/5 pass |
| `Phase29OverlandSnapshotTests` (2) | .NET Release | 2/2 pass |
| `Phase29GatePlayModeTests` | Unity PlayMode | Pass |
| `Phase29TerrainCachePlayModeTests` (3) | Unity PlayMode | 3/3 pass |
| `Phase29TerrainParityPlayModeTests` (3) | Unity PlayMode | 3/3 pass |
| `Phase29GroundWeatherPlayModeTests` (3) | Unity PlayMode | 3/3 pass |
| `Phase23PlayModeTests`, `Phase24PlayModeTests` (overland travel, transitions, save and load, weather) | Unity PlayMode | Pass |
| `Phase29TerrainCaptureTests` (2) | Unity PlayMode, capture-only | Captured; assert nothing about likeness |

- **Full suites:** a full EditMode run (974 tests) was started early in the phase and cut off when the editor closed. **There is no full-suite result for Phase 29**; the next checkpoint should run both suites.
- `Phase28PlayModeTests` once failed an object count (308 against 268), then passed twice: possibly flaky, not investigated.

## 8. Screenshots

`Documentation/Screenshots/Phase29/`: 9 views, each drawn as the Unity Terrain (`p29_terrain_*`) and the mesh ground (`p29_mesh_*`), at 10:00 under fixed weather. Earlier sets: `before-parity/`, `before-weather/`, `before-unified/`.

## 9. Known limitations and deferred work

- Frozen ground looks wet; a flood shows only as ford barriers and wet ground (ADR-0115).
- Under deep snow, roads and bridge decks vanish. Owner decision: leave as is.
- Lake shores and road edges are smoother on the terrain (5 m samples), and rocky knolls less faceted.
- Each terrain rebuilds its PhysX heightfield on entry (about 4 MB, estimated, not measured).
- **Deferred from the brief:** overland discoveries, encounters, escorts, the ancient realm (sections 3–8).

## 10. Save compatibility

- No save schema change.
- Settlement plans below generator v3 are straightened once on load and saved as v3 next time.
- The terrain cache is never saved: it is rebuilt from the world on each session.

## 11. Open owner questions

1. **Which ground should a new player see?** `UseUnityTerrain` has been `true` since the renderer was added. Claude later asked whether to "make the Unity Terrain the default" and the owner said "not yet". That question was wrongly framed, because it already was the default, and Claude also reported it as not the default. Please confirm: keep the Unity Terrain, or switch back to the mesh ground until you have played it.
2. Whether the smoother rocky knolls are acceptable.

## 12. Git commits and push status

| Commit | Content | Pushed |
|---|---|---|
| `da4fb07` | Audio pass | No |
| `8c3f094` | Phase 29 code, tests, ADR-0122, docs, screenshots | No |
| `b288ce5` | Townsfolk standing sway fix (after Phase 29) | No |
| (this close-out) | Preflight, ADR-0121, checklist, this report, indexes | No |

Left uncommitted on purpose: the `M_LampGlow.mat` emission keyword, the Unity MCP package in `Packages/`, the `Assets/InitTestScene*` scenes.

## 13. Owner playtest checklist

`Systems/Phase29-Owner-Playtest-Checklist.md`.
