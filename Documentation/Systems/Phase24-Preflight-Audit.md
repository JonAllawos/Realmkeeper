# Phase 24: Preflight audit

**Date:** 2026-11-13. **Brief:** "Phase 24 — Overland World Living Systems, Navigation Integration, Encounters, Hazards & Ground Weather" (given in chat).

**Repository:** the brief names `Documents/Projects/Realmkeeper`; the project is at `C:\Dev\Realmkeeper`.
- **Branch:** `feature/phase-0-1-foundation`.
- **HEAD:** `f46634d`, the same as origin (Phase 23 was pushed on 2026-11-13 at the owner's instruction). Nothing is ahead or behind.
- **Working tree:** clean apart from Unity's untracked `Assets/InitTestScene*`, which is left alone as in every phase. No stashes and no user changes to preserve.
- **Push:** this phase commits locally and does not push (the brief).

## A. Phase 23 overland architecture, as built

| Area | What exists | Where |
|---|---|---|
| Realms | `RealmData` (surface; ancient prototype with one geography-only region) and `GatewayData` (Dormant, Open or Sealed; two ends). `RealmGenesis.Ensure` is idempotent and seed-derived. | `Domain/World/Realms.cs`, `Infrastructure/Generation/RealmGenesis.cs`, ADR-0111 |
| Region maps | A 128 × 128-cell `RegionMap` per region, regenerated from the seed (never saved): elevation, terrain, rivers, `RoadPath`s between its settlements. | `Domain/Geography/RegionMap.cs`, `RegionGenerator` |
| Overland terrain | `OverlandTerrain`: 14 biomes, passability, walking cost, crossings (bridges, fords every 12 river cells, passes), height, `WalkMinutes`. Cached per region. | `Domain/Geography/Overland.cs`, ADR-0110 |
| Scale | 40 m a cell (a region is 5.12 km); the travel scale stays 1.2 km a cell (one walked metre = 30 m of road). | ADR-0110 |
| Atlas | Regions at `AtlasX/AtlasZ`. Atlas coordinates are region offset × 128 + the cell, the same grid the country uses. The atlas already draws a player marker (`PlayerAt`), groups (`GroupAt`) and world events. | `Application/Atlas/AtlasQueries.cs`, `RealmkeeperHud.Atlas.cs` |
| Player location | `CharacterLocationKind.Overland` with realm, region and pose (region metres); `ReturnToOverland`. | `PlayerCharacterData` |
| Transitions | `ExplorationController.Travel` and `ArriveFromMap`, behind `LoadingScreen`; one environment at a time. | ADR-0112 |
| Streaming | `OverlandWorld`: coarse and detailed chunks, instanced trees, ground cover and pooled trunk colliders near the player, barriers, kit silhouettes built lazily. | `Exploration/Presentation/OverlandWorld.cs` |
| Clock | Walking charges `WalkMinutes` beyond the living world's ambient time, through `AdvanceTimeCommand`. | `ExplorationController.Overland.StepClock` |
| Simulation tiers | By the player's surface region (ADR-0081); unchanged by Phase 23. | `SimulationTiers`, `RoutePlanner.PlayersRegion` |
| Saves | Schema v25 (realms, gateways, the overland player); `MigrationV24ToV25` is structural. | `Infrastructure/Persistence` |
| Discovery | Places and gateways within 140 m are discovered by walking; regions become visited. | `OverlandCommands.Discover` |
| Weather | `RegionWeatherData` per region: eight three-hour periods (kind, intensity, temperature, wind, cloud), plus `Moisture`, `GroundWetness` and `SnowCover`, **already saved** (v23). Fronts scale with atlas rows (Phase 23). | `Domain/World/Environment.cs`, `WeatherModel` |

## B. Existing gameplay systems, and what each offers Phase 24

| System | What exists | Reuse in Phase 24 |
|---|---|---|
| Travelling groups | `TravelingGroupData` (`realmkeeper.groups`): caravans (a real shipment), merchant parties, patrols and faction patrols, pilgrims, refugees, bandits (a real road band). Each has a leader who is one of the world's people, a member count, nodes and segments, departure and due times, a state, and history. The atlas places them with `GroupAt`. | **The source of every visible traveller.** No new people are created: a group's position on its route at the current time is mapped onto the region's own road path. |
| Journeys | `JourneyData`: Travelling, Halted, Interrupted, Arrived or Abandoned, with an exact position (segment, from-settlement, metres). `StartJourneyCommand` already plans from partway along a road (`PlansFrom` and `Position`). Road threats (`threat:` + group), road fights staged at the road's nearer settlement (Phase 17). | **Stepping off a journey**, back onto it, and the map's player position, all through this state; no second travel state. |
| Encounters | `EncounterGenerator.Plan` (purposes Outskirts, Road, Farms, near a settlement, or a named population) and `WorldEncounters.StartPlan`. Individuals are drawn from real `CreaturePopulationData`, which has a region, a `Center` and `RangeCells`. Fight loot goes to a container. | **Overland encounters** draw only from a population whose range covers the player's cell, or a bandit band holding a road the player walks. Combat finds the ground by raycast around the player, so it can run on the overland terrain (to be verified in PlayMode). |
| Road threats | `TravelCommandHandlers.Toll(band)`; fight, slip past or pay (`ResolveRoadThreatCommand`, journeys only). | The same three choices for a band met on foot, using the same toll. |
| Containers | `ContainerData`, including the kind "a hidden or abandoned cache, filled once from a loot table". The kind "Camp" is data only. Taking items and opening containers are existing commands. | **Searching a landmark** creates a cache filled once from an existing loot table (`LootTables.Roll`). |
| Discoveries | `QuestRuntime.Discover(world, DiscoveryData)`: categories, certainty, major, place. | Landmark finds and evidence are recorded as discoveries. |
| World-event knowledge | `WorldEvents.PlayerLearns(e, Rumored or Confirmed, now, from)`; the atlas shows what the player knows. | **News from travellers** is a real rumour of a real event. |
| Reputation and crime | `Reputation.Change`; crime records by faction (`CrimeRules`). | A patrol's attitude, and thanks for helping refugees. |
| Rest | `RestRules`: in open country a rest is a camp; there is no interruption outside places. | A camp landmark is safe; a long rest in the open can be broken by an overland encounter. |
| NPC bodies | `NpcAppearanceResolver` and `HumanoidCharacterBuilder` (residents). | Group figures: the leader's real appearance, plus escorts. |
| Dialogue | `DialogueEngine` belongs to settlement conversations. | Not extended this phase: travellers offer news, help and trade-free interactions through the site interaction, not a new dialogue tree. |
| Combat | One combat system, on a grid laid around the player. | Reused unchanged. |

## C. Verification status carried over from Phase 23

- **The fixes are present.** The four stale-test fixes are in HEAD (`f39f97e`): the Phase 18 performance test times one snapshot, with its bound unchanged; the Phase 16 and 17 atlas PlayMode pins are 9; the Phase 20 tiefling test makes the legacy-ability choice.
- **What was verified then:** the full EditMode suite (927/928) and the full PlayMode suite (129/132 with 33 capture-only skips) ran before the fixes. After the fixes, the four affected tests (3 EditMode and 9 PlayMode cases) and the full .NET suite (928/928) passed.
- **The gap:** the full Unity suites have not run since those fixes.
- **Plan:** focused .NET and targeted Unity tests while implementing. Then the full Unity EditMode and PlayMode suites **once**, after Phase 24 is complete, plus the full .NET suite. Each failure is reported and classified (regression, stale test, environment, pre-existing) with its evidence.

## D. Owner decisions outstanding

- **D1, the walking scale (40 m a cell): not confirmed by the owner.** Phase 24 does not change it.
  - Changing it later would change region size (5.12 km), the time charged per walked metre (30 m of road), landmark spacing (chosen below in cells, so it scales), encounter distances (in metres, which would need retuning) and every Phase 23 performance figure.
  - Nothing in this phase hard-codes 40 m beyond `OverlandScale`.
- **The ancient realm:** content undecided (Campaign Bible). Phase 24 adds nothing to it. Its systems take the realm into account (see section G).

## E. Gaps against the brief

1. **The atlas player marker:** `PlayerAt` knows settlements and the road, not the country or a place. It must use the persistent overland pose.
2. **Leaving the country to the map** loses the position (the map shows the last settlement). There are no waypoints.
3. **Journeys:** while one is under way, the country cannot be entered, and arriving on foot does not end it.
4. **No landmarks.**
5. **Groups are invisible in the country.**
6. **No overland encounters:** outings start only from a settlement.
7. **Weather does not touch the ground:** walking cost, fords, sight and the ground's look ignore it.

## F. Design decisions (inferred from the brief and the existing code; recorded in ADR-0113 and ADR-0114)

1. **One travel state.** The country and the road share the journey's road position.
   - **Stepping off:** a journey that is not barred by outlaws is halted, and the player stands on the region's road path at the journey's exact place.
   - **Back to the map:** from the country, the player first walks to the nearest road (its walking time is charged). The map then shows them there, on the road, and a journey can be planned from that point with the existing planner.
   - **Resuming:** possible from within 80 m of where the player left the road.
   - **Ending the journey:** entering its destination on foot ends it as arrived; entering another settlement, or crossing to another region, gives it up.
   - **Not supported:** arbitrary off-road positions on the map. The map position is always a road position, a settlement or a place, so the existing planner never sees a position it cannot route from.
2. **Landmarks** are generated from the region's seed, roads, rivers and terrain (Domain, engine-free), with stable ids `landmark:{region}:{kind}:{n}`.
   - Only their **changes** are saved (found, searched, rested at).
   - **Initial set:** waystones at road junctions and along long roads; cairns on hilltops (a view that reveals what is near); a traveller's camp by a road (a safe rest); an abandoned camp and a ruined homestead off the road (searched once); a watch post where a faction holds a border.
   - **Wrecks** are not generated: one appears only where a real `CaravanLost` event lies on one of the region's roads, and it carries that event as evidence.
3. **Visible travellers.** Every group whose position is in the player's region is drawn, but only the groups within about 600 m get bodies. The leader is the real person, escorts get stable appearances, and caravans have a cart. Bodies are pooled.
   - **Asking for news** teaches the player, as rumours, the events the group's origin and destination know of.
   - **Helping refugees** (giving a ration) improves standing with their home settlement.
   - **A patrol** answers according to the player's standing and record with its faction.
4. **Overland encounters** are checked at most once per 30 walked game-minutes, in deterministic buckets (`encounter:{region}:{bucket}`). Each record is saved, so loading never rerolls one.
   - **Sources:** a creature population whose range covers the player's cell, or a bandit band stationed on a road the player is on.
   - **Weights:** distance from settlements and roads, night, weather, and nearby patrols.
   - **Cooldown:** 3 game hours after any encounter.
   - **Choices:** fight (the existing combat), slip away (Stealth against the band's or creature's notice) or back off. For bandits, also pay the existing toll.
   - **Records:** bounded, the 40 most recent.
5. **Ground weather** comes from the existing `RegionWeatherData` (wetness, snow cover, the current period) through a Domain rule, `GroundConditions`. No new weather state is saved.
   - **Mud** slows off-road walking.
   - **Snow** slows all ground, and roads less.
   - **Heavy, sustained rain floods fords:** they become impassable, and bridges then matter.
   - **Fog, heavy rain and snow** shorten the distance at which things are noticed.
   - The ground's material darkens when wet and whitens under snow.
   - Each effect is shown on the HUD with its reason.
6. **Waypoints** are saved on the player: `{kind}:{id}` of a known site, never an unknown one.
   - **The compass** points to it.
   - **In another region,** the HUD names the region and points to the way on toward it (a breadth-first search over passable crossings).

## G. Persistence and the ancient realm

- **Save schema v26:**
  - landmark states (found, searched, rested-on day);
  - overland encounter records and the encounter clock;
  - the player's waypoint.

  Its migration is structural: absent means none found, none met, no waypoint. Weather needs nothing new.
- **The ancient realm:**
  - landmarks and encounters key on realm and region, and the ancient prototype gets no generated landmarks (no lore);
  - ground weather uses the region's weather when the region has it, and otherwise the calm defaults;
  - waypoints work in any realm.

## H. Performance risks

| Risk | Mitigation |
|---|---|
| Landmark generation per region | Once per region load, cached with the terrain. |
| Group bodies | Pooled, within 600 m only, at most about 6 groups. |
| Encounter checks | On the 0.5 s walking step, not per frame. |
| Ground material | Updated only when the condition changes. |
| Flooded-ford barriers | Rebuilt with the chunks only when a ford's state changes. |
| The atlas | The marker uses existing views; no new per-frame scans. |

The Phase 23 measurements are the baseline. They come from the editor in batch mode, not from a player build.

## I. Implementation sequence

1. Navigation: the atlas position, waypoints, stepping off and back onto journeys, arriving on foot.
2. Landmarks: generation, discovery, interactions, persistence, views.
3. Travellers: positions on roads, views, news, refugees, patrols.
4. Encounters: the clock and selection, choices, outcomes, persistence.
5. Ground weather: conditions, walking cost, fords, sight, visuals.
6. Save v26 and migration; region transitions; HUD feedback.
7. Tests (.NET and targeted Unity), captures, the full checkpoint, documents, the report, and a local commit.
