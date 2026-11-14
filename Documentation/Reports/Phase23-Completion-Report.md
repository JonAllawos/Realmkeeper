# Phase 23: Completion report

**Phase:** Overland World Foundation, Expanded World Scale, Realm Architecture & Traversal.
**Date:** 2026-11-13.
**Branch:** `feature/phase-0-1-foundation`.
**Commits:** `ea1422b` (the phase), `9746185` (records the hash) and `a818a74` (the follow-up round below). All local, none pushed (the brief).

**Decisions:** ADR-0110 (scale and terrain), ADR-0111 (realms, gateways, location, save v25), ADR-0112 (transitions and streaming).
**System doc:** `Systems/Overland.md`.
**Audit:** `Phase23-Preflight-Audit.md`.
**Owner checklist:** `Phase23-Owner-Playtest-Checklist.md`.

## Summary

The player can now leave a settlement, or start from the world map, and walk the country itself. The region map becomes walkable ground at 40 m a cell, 5.12 km a side, with:
- textured terrain of 14 biomes, hills, mountains, lakes and rivers;
- roads, bridges, fords and passes;
- forests, grass and flowers from the owned nature kit; trunks and rocks are solid;
- towns seen from outside in their own kit buildings.

Walking advances the one clock at the travel scale's own rate. The player can:
- walk to a settlement and enter it;
- find a place by walking near it and go in, returning to the country when they leave;
- take the road on into a neighbouring region;
- pass through a gateway into a prototype ancient realm.

Each of these is a loading transition with a loading screen. A new game has 9 regions: 236 km² of walkable surface, and 262 km² with the ancient prototype region. Before this phase, the walkable area was under 2 km².

Not hand-played. The full Unity suites were not run (section F).

### Follow-up round (owner request, 2026-11-13)

After the first report, the owner asked for every weak spot it listed to be fixed, using owned assets first, and for the weather to be tuned as needed. All were addressed:

| Weak spot | Fix |
|---|---|
| Further regions felt empty (about 7 destinations each) | Each gains a hollow, a fallen steading, a barrow and old workings, once (`PlaceGenesis.EnsureCountryPlaces`). They use the existing cave, ruin, crypt and mine archetypes, are named after the settlement they lie near, and are found by walking. Now 11 destinations a region. |
| Untextured ground | Owned ambientCG grass and ground, and the village kit's rock, sampled in world space in `Realmkeeper/VertexColorLit`. They are blended by biome and slope and keep the biome's colour. |
| Bare grassland | Grass, ferns and flowers from the owned nature kit, made near the player and drawn within 95 m. |
| Towns as block shapes | Built from their own plan with the village kit (`ModularEnvironmentBuilder.BuildSilhouette`): the nearest town during loading, others as the player comes within 1.4 km. |
| Trees could be walked through | Pooled colliders on the trunks and rocks within 45 m. |
| No entry from the world map | "Walk the country" on the map's settlement inspector, behind the loading screen, under the same rules as entering a settlement. |
| HUD in the country | The location panel shows region · in the country · ground. The settlement-edge practice fights and outings are hidden in the country (the existing hide rule now covers it). |
| The far east was drier | Weather fronts now scale with the atlas's rows, so six-region worlds are unchanged. The Phase 21 test is back to its original floor of 30 wet days. |

**Bugs found while doing it:**
1. **The vegetation never drew upright.** The kit models were drawn without their root rotation and scale (the Blender export frame), so trees and grass lay flat, half underground. The first round's captures did not show trees; that went unnoticed. Fixed: they are now upright, as the town's scenery places them.
2. **Vegetation draws per camera.** Vegetation is now submitted per camera (`beginCameraRendering`), so captures and any second camera see it.
3. **Sites doubled.** Rebuilding the sites when a gateway opened left the old ones in place, so everything doubled. The old site root is now destroyed first.
4. **The map arrival point.** Arriving at a town's gate from the map placed the player inside its banner pole. The banner now stands 3 m aside.

---

## A. Initial audit

Full detail is in `Phase23-Preflight-Audit.md`.

**Existing systems found:**
- a seeded region generator (128 × 128-cell `RegionMap` with elevation, a lake, a river, 8 terrain types, settlements and roads);
- a 3 × 3 atlas with crossings (6 regions used in a new game);
- a route graph at 1.2 km a cell, with journeys, caravans, trade and groups;
- weather (Phase 21);
- wilderness places with layouts and discovery;
- `LocalEnvironmentStreamer` (one runtime scene for the active environment);
- simulation tiers by region (ADR-0081);
- one authoritative clock;
- JSON saves v1–v24 with ordered migrations.

**Existing dimensions and scale.**
- **Travel scale:** a region was 154 km a side.
- **Walkable area:** only settlement scenes (110–200 m half-extent) and place interiors, under 2 km² in all.
- **Destinations:** 18 settlements and 34 places.

**Scene and loading architecture.**
- One local environment at a time, built into a runtime scene and unloaded on leaving. The strategic scene is hidden, never unloaded.
- There was no loading screen; changes happened in one frame.

**Reused unchanged:**
- `RegionMap` and the region generator's geography;
- the atlas and its crossings;
- the route graph and travel scale;
- places and their entry and exit;
- the streamer's one-environment rule;
- the simulation tiers;
- the clock and `AdvanceTimeCommand`;
- the day and night atmosphere;
- the owned kits (nature, village) and the in-repo ambientCG textures;
- the settlement kit planner (`BuildingKitPlanner`) and `MeshMerger`;
- the save pipeline.

**Extended:**
- the region generator (geography-only generation);
- `WorldAtlasGenesis` (realms; country places);
- `PlaceGenesis` (country places) and `PlaceCatalog` (4 generic definitions);
- the player's location (overland, realm, return flag);
- place entry and exit (return to the country);
- the world validator;
- the route planner (surface only);
- the weather model (fronts by rows);
- the ground shader (optional textures);
- `ModularEnvironmentBuilder` (silhouettes);
- the atmosphere (fog scale, realm tint);
- the exploration controller and HUD;
- the map HUD ("Walk the country");
- the save schema (v25);
- new games (9 regions).

**Newly implemented:**
- the overland scale and terrain;
- realms and gateways;
- the overland queries and commands;
- the scale survey;
- the overland environment (ground, water, roads, vegetation, ground cover, colliders), its site views and the loading screen;
- the compass and region map.

## B. World scale

| Measure | Before | After |
|---|---|---|
| Walkable area | < 2 km² (settlements and interiors) | 236 km² surface (9 × 26.2 km²), plus 26.2 km² ancient prototype: **262 km²** |
| Regions in a new game | 6 | 9 (the whole 3 × 3 atlas) |
| Realms | none | 2 (surface; ancient prototype, 1 region) |
| Region size walked | n/a | 5.12 × 5.12 km |
| Settlements, places, gateways | 18, 34, 0 | 27, 78, 1 |
| Destinations per further region | n/a | 11 (3 settlements, 8 places); the home region has 17 |
| Roads drawn | n/a | 53 km across the surface |
| Walkable share per region | n/a | 83–87 % (mountains and deep water bar the rest) |
| River crossings per region | n/a | 31–44 (bridges, fords, passes) |
| Mean distance from a settlement to its nearest destination | n/a | 0.17 km (home) to 0.58 km |

**Distances and times.**
- Crossing a region on foot takes about 14 real minutes at a run.
- In game time, a walked road cell (40 m) is 15 game minutes, as on a journey, so crossing a region by road is about 32 game hours. Rough ground costs up to 2.6 times as long, and night doubles it.
- The clock is charged only beyond the living world's ambient 2 game minutes a second.
- Measured: a 4 s sprint covered 24 m and moved the clock from 08:02 to 08:11.

**Geographical variety.**
- 14 biomes: lake, river, shallows, shore, grassland, meadow, forest, dense forest, hills, rocky, mountain, marsh, valley and heath.
- Region kinds vary the mix: highland heath; marsh in lakeland, lowland and woodland.
- Mountain crags rise up to about 125 m above the 240 m relief.
- The ground shows rock on steep and rocky ground, dirt on heath and shore, and grass elsewhere.

**Remaining scale limits.**
- No roadside landmarks yet (waystones, cairns, camps).
- **Desert** is reserved but not generated.
- **The ancient realm** has one region.

## C. Architecture

**World and realm model (ADR-0111).**
- `RealmData`: surface and ancient prototype, with regions and seeds.
- `GatewayData`: Dormant, Open or Sealed, with two ends.
- `RealmGenesis.Ensure` is idempotent, runs from the atlas step, and takes its ids from the seed.
- The ancient region is geography only. Nothing invented: no names, cultures or inhabitants.

**Region and location persistence.**
- The player's location kind `Overland`, realm, region and pose (region metres), and `ReturnToOverland`, are saved.
- Geography, ground, vegetation, ground cover and silhouettes are regenerated from the seed.
- Discoveries are saved as before. The country places are ordinary saved places.

**Terrain (ADR-0110).**
- `OverlandTerrain` is engine-free and deterministic. It provides biome, passability, cost, road and crossing flags, concealment, sight bonus, height and walking minutes.
- Fords are placed along river paths. Bridges are roads over rivers; passes are roads over mountains.

**Routes.**
- The overland draws the region map's own roads, textured with the dirt grain.
- A region's ways on come from the route graph's crossing routes: a closed crossing shows its reason and refuses.
- Journeys, caravans and trade are unchanged.

**Loading and unloading (ADR-0112).**
- One environment at a time: a region's country, a settlement, a place, or another realm's region. The current one is torn down before the next is built.
- Within a region:
  - one coarse ground mesh;
  - detailed 16 × 16-cell chunks with colliders within 1000 m, one built per 0.2 s;
  - instanced trees and rocks within 650 m;
  - ground cover made within 200 m and drawn within 95 m;
  - pooled trunk and rock colliders within 45 m;
  - barrier colliders near the player;
  - kit silhouettes for towns within 1.4 km, one at a time.

**Scene transitions.**
- `ExplorationController.Travel` runs every transition: loading screen → validated command → teardown → build with progress → placement at the entry → hide.
- Arriving from the map takes the same screen (`ArriveFromMap`).
- A refusal shows its reason and changes nothing.

**Simulation level of detail.**
- Unchanged in kind: tiers by the player's surface region (ADR-0081).
- In the country the player's region is the local tier.
- In the ancient realm the surface keeps simulating from the region the player left (`RoutePlanner.PlayersRegion` ignores non-surface realms).

**Save compatibility.**
- Schema v25. `MigrationV24ToV25` is structural.
- A v24 save loads unchanged and gains its realms and a dormant gateway once (tested).
- Older saves gain the country places in their further regions once on load, as Phase 18 did; loading again adds nothing (tested).
- Older saves keep 6 regions. No migration moves a player into the country.

## D. Implementation

**Completed:**
- walkable scale and terrain;
- 9 regions in new games;
- realms and the prototype gateway, opened only by the campaign's command;
- the overland location and save v25;
- commands for entering the country (from a settlement or the map) and leaving it, walking, entering settlements, crossing regions, and using and setting gateways;
- discovery by sight;
- the overland environment: textured ground, water, roads, trees, ground cover, solid trunks and rocks, barriers;
- site views: kit silhouettes of settlements, place entrances, the gateway ring, signposts;
- the country places in the further regions;
- the loading screen with progress and a failure state;
- the compass, location line, location panel and region map (N);
- clock advance from walking;
- weather by atlas rows;
- the scale survey;
- tests, captures and documentation.

**Partly done:**
- **Navigation.** No waypoints and no world-map marker for the player's overland position.
- **The ancient realm.** One region, a provisional profile, no content.
- **Distant settlements.** Beyond 1.4 km they stay block shapes under the haze.

**Deferred:**
- roadside landmarks;
- mounts, fast travel, overland encounters and wildlife, hazards;
- weather on the ground (mud, snow depth, flooded fords);
- ancient realm content (undecided in the Campaign Bible);
- loading-screen art;
- entering the country mid-journey.

**Files changed:**

| | Files |
|---|---|
| **New, runtime** | `Domain/Geography/Overland.cs`<br>`Domain/World/Realms.cs`<br>`Domain/World/WorldValidator.Realms.cs`<br>`Infrastructure/Generation/RealmGenesis.cs`<br>`Infrastructure/Persistence/WorldSaveMapper.Realms.cs`<br>`Application/Overland/OverlandQueries.cs`<br>`Application/Overland/OverlandCommands.cs`<br>`Application/Overland/WorldSession.Overland.cs`<br>`Application/Overland/WorldScaleSurvey.cs`<br>`Exploration/Application/OverlandEntryHandlers.cs`<br>`Exploration/Presentation/OverlandWorld.cs`<br>`Exploration/Presentation/OverlandSiteView.cs`<br>`Exploration/Presentation/LoadingScreen.cs`<br>`Exploration/Presentation/ExplorationController.Overland.cs` |
| **New, tests** | `Tests/EditMode/World/Phase23OverlandTests.cs` (11 tests)<br>`Tests/PlayMode/Phase23PlayModeTests.cs` |
| **Modified, runtime** | `PlaceCommands.cs`, `IWorldGenerator.cs`, `Queries.cs`, `WorldSession.cs`, `PlayerCharacterData.cs`, `WorldData.cs`, `WorldValidator.cs`, `ExplorationHandlers.cs`, `ExplorationAtmosphere.DayNight.cs`, `ExplorationController.Living.cs`, `ExplorationController.Combat.cs`, `ExplorationController.cs`, `RealmkeeperRuntime.cs`, `RegionGenerator.cs`, `WorldAtlasGenesis.cs`, `PlaceGenesis.cs`, `PlaceCatalog.cs`, `WeatherModel.cs`, `ModularEnvironmentBuilder.cs`, `VertexColorLit.shader`, `RealmkeeperHud.cs` and `RealmkeeperHud.uxml`, `JsonWorldSerializer.cs`, `SaveFileDtos.cs`, `SaveMigrations.cs`, `WorldSaveMapper.cs`, `RoutePlanner.cs` |
| **Modified, tests** | `Phase16WorldTests.cs` (9 regions; country places left out of the home-only check)<br>`Phase17StoryTests.cs` and `Phase20AAppearanceTests.cs` (schema pin 24 → 25)<br>`Phase21WeatherTests.cs` (unchanged in the end: floor back at 30) |
| **Docs** | ADR-0110, ADR-0111 and ADR-0112, and the ADR index<br>`Systems/Overland.md` (new), `Persistence.md` (v25), `Travel.md`, `Exploration.md`<br>`Roadmap.md`, `Product/FutureRoadmap.md` (D1, Phase 23 note), `Product/GameDesignBible.md`, `Product/CampaignBible.md`<br>this report, the audit, the checklist, and `Screenshots/Phase23/` (13 captures and their log) |

**Architectural decisions:**
- two scales over one geography;
- modular environments with loading transitions, not a seamless continent;
- realms as separate region sets joined by gateways;
- a runtime mesh from the domain's height function, not Unity Terrain, now textured in the shader;
- vegetation drawn per camera;
- kit silhouettes built lazily, nearest first;
- D1 decided under the brief, with owner review invited.

## E. Performance

Measured by the Phase 23 PlayMode test: Unity 6000.0.47f1 editor in batch mode, 1920 × 1080 captures, this machine. Figures are from the last run, after the follow-up.

| Measure | Value |
|---|---|
| Out into the country (settlement → country) | 666 ms, including the nearest town's kit silhouette (152 ms); 12 detailed chunks ready, 10 barrier colliders |
| Walking 4 s, sprinting (3,592 frames) | median 0.9 ms, 95th percentile 1.1 ms, worst 61.5 ms (CPU frame time in the batch editor; GPU cost is not captured) |
| Trees and rocks submitted | 5,091 (instanced) |
| Ground cover drawn near the player | 654 |
| Detailed chunks, last chunk build | 14, 7.5 ms (ground weights and grass make it slower than the first round's 4 ms) |
| Kit silhouette, per town | 33–157 ms, one at a time (a brief hitch while walking toward a town) |
| Into a settlement / back out | 432 ms / 525 ms |
| Into a place (Watch Fort) | 241 ms |
| Region crossing | 490 ms (first), 338 ms (second) |
| Into the ancient realm | 429 ms |
| From the map into the country | 468 ms |
| Objects after two region round trips | 947 → 948 |
| Memory after two region round trips | 724 MB → 622 MB (no growth) |
| Scenes loaded | 2: the strategic scene and the one environment |

**Loading behaviour.** Every transition is under 0.8 s on this machine.

**Memory.** Flat across repeated transitions. The test asserts at most +40 objects and +96 MB. The first kit silhouette build left about 13,000 emptied transforms; they are now destroyed after merging.

**Rendering observations.**
- Fog from 250 to 1700 m hides the coarse horizon mesh's edge.
- Trees, rocks and ground cover are instanced per mesh and material; ground cover casts no shadow.
- The ground's textures are divided by their own average, so the biome colours stay as designed.

**Bottlenecks.**
- The kit silhouette hitch (up to about 160 ms) as a town comes within 1.4 km. If it shows in play, it can be spread over frames.
- Not measured in a player build, with GPU timing, or on lower-end hardware.

## F. Testing

**.NET harness** (`Tools/DotNet/Realmkeeper.Verification.sln`, Release): see the last line below for the follow-up's full run.
- The first round's full run: 926 passed of 926, after one failure in `Phase21WeatherTests` (26 wet days in one region). The first round lowered that test's floor.
- The follow-up instead fixed the weather model (fronts by rows) and **restored the floor to 30**. The weather tests pass (30/30).
- `Phase23OverlandTests` has 11 tests, 11 passed:
  1. scale targets;
  2. determinism;
  3. barriers and fords;
  4. walking time;
  5. settlement in and out;
  6. place discovery, entry and exit to the country;
  7. region crossing;
  8. the gateway;
  9. a v24 save loading at v25;
  10. the country places (at least 3 a further region, named "near …", unknown until found, walkable, laid out, made once);
  11. entering the country from the map by a settlement of the player's region, and refusing another region's.
- **Full suite after the follow-up: 928 passed, 0 failed of 928.** The first full run had 1 failure: `Phase16WorldTests.ANewGame_IsNineConnectedRegions…`, which checks that Phase 13 and 14 places stay in the home region and counted the new country places among them. Those belong to the further regions by design, so the test now leaves them out, as it already left out Phase 18's; its check on Phase 13 and 14 places is unchanged.

**Unity PlayMode** (batch, `-testFilter Phase23PlayModeTests`): **1 passed, 0 failed** (last run).
- It covers:
  - the loading screen;
  - out into the country, on the ground, with the nearest town in kit buildings;
  - walking with frame timing, and the clock advancing;
  - ground cover drawn;
  - **walking into a tree trunk for 3 s from 6 m: stopped after 4.8 m, 1.2 m from its centre**;
  - the region map;
  - a settlement approached, entered and left;
  - a place found by walking, entered and left back into the country;
  - saving and loading in the country;
  - two region round trips (objects and memory);
  - the gateway opened by command and passed both ways;
  - returning to the map;
  - **straight from the map into the country behind the loading screen**, and back.
- **Runs during the follow-up.** It was run several times while fixing; every failure was in the test, not the game. The captures showed the vegetation bug, the banner-pole arrival and the leftover objects, which were fixed in the game.

**Unity compilation:** clean on every batch run.

**Not run:**
- the full Unity EditMode suite;
- the full Unity PlayMode suite;
- the Phase 22 and Phase 20A targeted PlayMode tests.

The last full Unity suites were before Phase 22. **A full checkpoint run is due**, and is the next thing worth doing before Phase 24. This round changed shared pieces: `VertexColorLit` (unchanged when its texture strength is 0), `ModularEnvironmentBuilder`, the practice-button rule and the map HUD.

**Remaining failures:** none known.

## G. Visual evidence

Captures are in `Documentation/Screenshots/Phase23/`, with notes in `p23_log.txt`.

| Capture | Shows |
|---|---|
| `p23_01_loading_screen` | The loading screen: destination, status, progress |
| `p23_02_region_from_above` | The whole region (5.12 km), haze off: biomes, lake and river, mountains, roads (brief items 1 and 3) |
| `p23_03_roads_and_settlement` | A settlement and its roads leading out (item 2) |
| `p23_04_player_in_the_country` | The player walking: textured ground, grass, the compass, the location line and panel (item 4) |
| `p23_05_region_map` | The region map (N) (item 9) |
| `p23_06_settlement_approached` | Westerthorpe in its own kit buildings from the country, its road, meadow flowers, trees, distant mountains, rain (item 5) |
| `p23_07_settlement_entered` | Inside the settlement after the transition (item 6) |
| `p23_08_place_interior` | A place entered from the country: a separately loaded environment (item 7) |
| `p23_09_next_region` | Arrival in the next region: grass, flowers, a bush, blossoming trees (item 8) |
| `p23_10_gateway_open` | The prototype gateway opened by the campaign command |
| `p23_11_ancient_realm` | The ancient realm's prototype region (item 8) |
| `p23_12_grass_and_trees` | After walking into a trunk: the tree stopped the player; grass, ferns and flowers around |
| `p23_13_from_the_map` | Out into the country by Bramhaven straight from the map's "Walk the country", beside the banner |

**Not shown, or shown weakly:**
- **Region crossing:** there is no capture of the signpost itself.
- **The ancient realm:** its provisional tint is barely distinct.
- **The country places:** none is captured.
- **`p23_13`:** the banner pole fills the left of the frame (the camera stands close to it).

## H. Git

- **Branch:** `feature/phase-0-1-foundation`.
- **Before:** `de66388`, the same as origin.
- **Commits:** `ea1422b` (Phase 23), `9746185` (its hash recorded), `a818a74` (the follow-up round), and a last commit recording this hash. All local.
- **Working tree after the commit:** clean except for Unity's untracked `Assets/InitTestScene*` files, which are deliberately left out as in every phase.
- **Nothing was pushed or uploaded.**

## I. Recommendations for Phase 24

1. **A full Unity EditMode and PlayMode checkpoint run first.** Last run before Phase 22.
2. **Roadside life and landmarks.** Waystones, cairns and camps; travellers and caravans walking the roads (groups already move on the route graph).
3. **The world map and the country together.** Show the player's overland position on the atlas; allow stepping off a journey into the country.
4. **Overland encounters and hazards,** if the owner wants them before groups walk the roads.
5. **Weather on the ground.** Mud slowing walking, snow depth, rivers in spate closing fords. The weather model already has ground wetness and snow cover.
6. **Owner decisions:**
   - confirm D1 (40 m a cell);
   - what the ancient realm holds, and when gateways open.

Phase 24 has not been started.
