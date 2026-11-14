# Phase 23: Preflight audit

**Date:** 2026-11-13. **Brief:** "Phase 23 — Overland World Foundation, Expanded World Scale, Realm Architecture & Traversal" (given in chat).

**Repository:** the brief names `Documents/Projects/Realmkeeper`; the project is at `C:\Dev\Realmkeeper`.
- **Branch:** `feature/phase-0-1-foundation`.
- **HEAD:** `de66388`, the same as origin (pushed earlier on 2026-11-13 at the owner's instruction).
- **Working tree:** clean apart from Unity's `Assets/InitTestScene*`.

## 1. What already works

| Area | What exists | Where |
|---|---|---|
| World generation | A seeded region generator. Each region is a 128 × 128 cell `RegionMap`: elevation, a lake, a river, 8 terrain types (deep and shallow water, river, shore, plains, forest, hills, mountain), up to ~3 settlements on flat land near water, and roads between them. It is deterministic per stage stream and regenerated from the seed on load (geography is never saved). | `Infrastructure/Generation/RegionGenerator.cs`, `Domain/Geography/RegionMap.cs` |
| The atlas | Up to 9 regions on a 3-column grid (`WorldAtlasGenesis`). A neighbour continues the mountain range across the border, and crossings join border settlements by pass, bridge, trail or road. A new game had **6 regions**. | ADR-0076 |
| Travel | A route graph (`RouteSegmentData`: cells, terrain %, condition, bridges and their state, closures) at **1.2 km a cell**, walked hour by hour by journeys. Caravans, trade and groups use the same graph. Weather slows it (Phase 21). | `Domain/World/Atlas.cs`, ADR-0077, ADR-0078 |
| Region kinds | Vale, Woodland, Highlands, Lakeland, Lowlands, with climate (Phase 21). | `RegionData.Kind` |
| Places | Wilderness places (caves, ruins, forts, crypts and more), each with a region, a map cell, a seeded multi-floor layout, persistent doors, loot, traps and people, and discovery states. They are entered from the map once discovered. | ADR-0062, ADR-0065 |
| Local scenes | `LocalEnvironmentStreamer` makes one runtime scene for the active environment (a settlement layout of 110–200 m half-extent, or a place) and unloads it. A second load of the same environment is refused, so nothing is duplicated. The strategic scene is hidden, never unloaded. | `Exploration/WorldStreaming` |
| Simulation level of detail | Tiers by region (local, near, far) from the player's region (`SimulationTiers`); a daily tick for the rest of the world. | ADR-0081 |
| Time | One authoritative clock. A settlement runs at 2 game minutes per real second ("living world"); the map runs whole days per tick. `AdvanceTimeCommand` advances it from anywhere. | `SimulationPlayback`, `PlayerCommands.cs` |
| Persistence | JSON saves with ordered migrations (v1–v24). Stable GUID identities for regions, settlements and people; string ids for places and routes. Changes are kept apart from generated data: route condition, bridges, place state and discoveries are saved; geography is regenerated. | `Infrastructure/Persistence` |

## 2. What is incomplete

- **No walkable country.** Between settlements there is only the strategic map and the abstract journey. The playable area is the settlement scenes (about 0.05–0.16 km² each) and place interiors.
- **No world-space position for the player** outside a settlement or place.
- **No realms.** The campaign's ancient world (Campaign Bible §5) has no representation.
- **Terrain has 8 types.** There is no marsh, dense forest, valley, heath, rocky ground, ford or pass as walkable ground.
- **No loading screen.** Environment changes happen in one frame, with no feedback.

## 3. Extend, do not replace

- Keep the travel scale (1.2 km a cell): caravans, trade, journeys, weather and quests all read it.
- Keep `RegionMap` and the region generator, the atlas and its crossings, the route graph, places and their entry, the streamer, the simulation tiers and the save pipeline.
- **Add:**
  - a walkable projection of each region;
  - the overland as a third kind of local environment, beside settlements and places;
  - realms and gateways;
  - a loading-screen transition around the existing environment loads.

## 4. Current scale, measured

`WorldScaleSurvey` measured a new game (seed 123456789) through the session's own geography.

**Before Phase 23 (6 regions):**
- **Travel scale:** 6 × (128 cells × 1.2 km)² ≈ 6 × 154 km a side.
- **Walkable:** only the settlement scenes and place interiors, under 2 km² in all.
- **Destinations:** 18 settlements, 34 places (14 in the home region, 4 in each further region).

## 5. Target scale and reasoning

Decided in ADR-0110 (owner decision D1, taken under the Phase 23 brief).

**Walking scale: 40 m per cell.** A region is 5.12 km a side, 26.2 km² of walkable country, and the map and the ground are one geography. 40 m was chosen because:
- at the existing ~6 m/s run, crossing a region takes about 14 real minutes;
- the nearest destination to each settlement is 0.4–1.2 km away, 1–3 minutes on foot;
- at 100 m a cell, the same content would sit 1–3 km apart in 164 km² regions: the empty map the brief rules out;
- at 20 m, regions shrink to 6.6 km² and mountains and rivers lose their scale.

**Time:** one walked metre stands for 30 m of road (1.2 km ÷ 40 m), so caravans, journeys and the walker agree.
- A cell of good road is 15 game minutes.
- Running on a road roughly matches the settlement clock (2 game minutes a second). Slower ground adds its extra time through `AdvanceTimeCommand`.

**Regions:** a new game grows from 6 to **9** (the whole 3 × 3 atlas).

**Measured after the change:**

| Measure | Value |
|---|---|
| Walkable surface | 9 × 26.2 km² = **236 km²** |
| The ancient realm's prototype region | 26.2 km² more |
| Total walkable | **262 km²** |
| Roads | 53 km |
| Destinations | 27 settlements, 46 places, 1 gateway |
| Walkable share per region | 83–87 %; mountains and deep water bar the rest |
| River crossings per region | 31–44 (bridges, fords and passes) |

**Density is the gap.** Outside the home region, a region has about 7 destinations (3 settlements, 4 places) in 26 km². That is the main thing Phase 24 or 25 must add: landmarks and minor sites, more places for the further regions, roadside life. Phase 23 does not pad the count with empty markers.

## 6. One world, or modular regions and realms

**Modular.**
- Each region is its own walkable environment, about 5 km across, loaded on its own; crossing a region border is a loading transition.
- Settlements, places and large interiors stay their own environments, entered with a loading transition.
- Realms are separate sets of regions, reached through gateways, with a loading transition.

**Why:**
- one seamless continent of 236 km² would need floating origin, terrain streaming across region seams, and simulation tiers by distance rather than by region;
- the brief prefers reliability to seamlessness;
- the existing streamer, layouts and tiers already work per environment.

Within a region, ground detail streams by distance (§8).

## 7. Scene loading and persistence requirements

- **One active environment at a time** (the streamer's rule). A transition unloads the current one, then loads the next.
- **The world data is authoritative.** The player's location (kind, realm, region, pose), discoveries, gateway state and the return flag are saved. Ground, vegetation and barriers are rebuilt from the seed.
- **Unloaded regions keep simulating** by their tiers. In another realm, the surface is simulated from where the player left it.
- **No duplication.** Transitions are commands with validation, which the streamer and the world validator guard.

## 8. Performance risks

- **Ground:** a 128 × 128 region at 10 m detail is about 263,000 ground vertices. It is built in chunks of 16 × 16 cells (640 m): detailed chunks near the player, one coarse mesh of the whole region for the horizon.
- **Vegetation:** about 40 % of cells are forest. The kit's trees are drawn only within about 400 m, merged by chunk; beyond that the coarse ground carries the forest's colour.
- **Colliders:** mesh colliders only for chunks near the player; barrier walls only along impassable cells near the player.
- **Generation time:** 9 regions instead of 6, measured.
- **Save size:** realms and gateways are a few hundred bytes. Geography is never saved.

## 9. Save compatibility risks

- **v24 → v25 is structural.** The new player fields are optional, with defaults that mean what older saves meant. Realms and gateways are made once on load by the atlas step, deterministically, and nothing else changes (tested).
- **Older saves keep 6 regions.** Only new games get 9.
- **Not migrated:** the player is never moved into the country by a migration.

## 10. Implementation sequence

1. Domain: the walkable scale and terrain rules (`OverlandScale`, `OverlandTerrain`, biomes, passability, crossings, heights, walking time); realms and gateways (`RealmData`, `GatewayData`); the player's overland location.
2. Infrastructure: `RealmGenesis` (the surface, the ancient prototype region as geography only, a dormant prototype gateway); geography-only generation; schema v25.
3. Application: overland queries (sites, exits, gateways, discovery by sight); commands (enter, walk, enter a settlement, cross a region, use a gateway, set a gateway's state); the scale survey; 9 regions for new games.
4. Presentation: the overland environment (chunked ground, water, roads, vegetation, barriers, entrances); the loading transition; the compass and region map.
5. Tests (.NET, targeted Unity), measurements, captures, documentation, and a local commit.
