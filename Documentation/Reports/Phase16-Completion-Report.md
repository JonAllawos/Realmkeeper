# Phase 16 completion report: World Events, Regional Travel, Dynamic Simulation & Persistent World Expansion

Date: 2026-11-12. Branch `feature/phase-0-1-foundation`. Committed locally, **not pushed** (the brief: "Do not push").

## 1. Preflight findings

The full audit is in `Reports/Phase16-Preflight-Audit.md`. Its answers to the brief's ten questions:

| # | Question | Answer |
|---|---|---|
| 1 | Multi-region? | No. One region of 128 × 128 cells with three settlements. `RegionData` held only an ID, name, origin and size. |
| 2 | Settlements across regions? | IDs yes; positions no (cells of the one map). Every map-taking routine had to be scoped to its region. |
| 3 | Roads persistent? | No. They were regenerated from the seed. Only the trade routes (`TravelDays = ⌈cells / 25⌉`) persisted. |
| 4 | Trade a working system? | Yes (Phase 2), with conservation checks. Shipments were invisible aggregates. |
| 5 | NPCs or groups travelling? | No. Road bands were populations tied to a road. |
| 6 | Simulation tiers? | Partly: NPC updates were staggered, but nothing distinguished near from far. |
| 7 | Clock over long travel | One clock: one transactional day per midnight. Combat freezes it. There was no travel (entering was instant). |
| 8 | Events | Chronicle facts and typed states only. No world-event entity. |
| 9 | Party across regions | Nothing beyond one settlement. Waiting members did not keep their spot. |
| 10 | Rebuild a local scene from data | Yes: settlement layouts persist (ADR-0047). |

## 2. Baseline

| Item | State |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| HEAD at start | `48b0098`, equal to `origin`; the working tree was clean |
| `ProjectSettings/GraphicsSettings.asset` | Already committed by the owner in `48b0098` (a reformat with no value changes) and pushed. It was not touched in Phase 16. |
| Save schema | v17 |

## 3. World architecture changes

There is still one world model (`WorldData`). New entities:
- `RouteSegmentData`: the persistent route graph;
- `JourneyData`: the player's journey;
- `TravelingGroupData`: groups on the roads;
- `WorldEventData`: world events with the player's knowledge of them.

Regions gain atlas fields.

New systems:
- `realmkeeper.groups` (order 160);
- `realmkeeper.faction-activity` (456);
- `realmkeeper.world-events` (477).

New code (Domain and Simulation have no UnityEngine references):
- `Domain/World/Atlas.cs`;
- `Domain/World/WorldValidator.Atlas.cs`;
- `Simulation/Atlas/*`;
- `Application/Atlas/*`;
- `Infrastructure/Generation/WorldAtlasGenesis.cs`;
- `Infrastructure/Persistence/WorldSaveMapper.Atlas.cs`;
- `Presentation/UI/RealmkeeperHud.Atlas.cs`.

See ADR-0076 to ADR-0081.

## 4. Region generation

- A new game asks for six regions on a 3 × 2 atlas grid (`WorldSession.NewGameSettings()`, used by the runtime).
- `DefaultSettings()` and existing worlds keep one region.
- Every further region is made by the **same** `RegionGenerator` from a derived seed (`region:<i>`). It runs with `RegionOnly`, which leaves the authored quests and places in the home region.
- Merging:
  - road-band populations are re-keyed by region;
  - names are made unique;
  - factions of bordering regions start wary of each other.
- Each region gets a kind and landmarks from its own terrain. No new canon is written.
- The opening town for authored quests is now chosen in the home region (`QuestGenesis.FirstTown`). Before this fix, the authored quests could land in a far region.

Measured in this world: 18 settlements, 119 people, generated in about 0.6 s.

## 5. Geographic connectivity

- **Coherent borders.** A new region's mountain side comes from its neighbour. A range that reaches the shared edge continues across it; a lowland edge stays lowland.
- **Crossings.** Neighbours are joined between their nearest settlements by a pass, a bridge, a trail or a road, chosen from the terrain.
- **Discovery.** The home region is visited and its neighbours known by name. The far lands stay uncharted until the player borders them.
- **The validator** (`WorldValidator.Atlas`) checks:
  - unique atlas cells, and real, mutual neighbours;
  - routes joining real settlements in the regions they name;
  - crossings only between neighbours;
  - sane lengths and costs;
  - a consistent journey.
- **Tests** cover:
  - every settlement reachable by open routes;
  - disconnected lands refused;
  - coherent borders;
  - same seed, same world.

## 6. Travel

- **Plans.**
  - Dijkstra for the best way and Yen for up to three alternatives, over the persistent route graph.
  - Hours come from length × terrain × condition × bridge state, on a stated scale (1.2 km per cell, 4.8 km/h, half pace at night).
  - Plans can start from mid-road.
  - Only ways that can be walked are offered.
- **Journeys** run on the one clock: `TravelHourCommand` moves along the segments, then advances the world clock by an hour.
  - Halt, resume, turn and give up.
  - A road closing ahead interrupts the journey.
  - **The way back stays open** when a road closes under the traveller. This was found by the capture run, fixed, and tested.
  - On arrival the town is entered as usual.
  - A settlement in another region cannot be entered without travelling there.
- **Outlaws on the road** roll per hour (keyed). The player can:
  - fight through the one encounter engine, with combatants drawn from the band's own population;
  - slip past (Stealth);
  - or pay a toll.

  A band met once does not bar the same journey again.

## 7. Party integration

- Active members with the player travel and arrive with them. They lower the threat and join road fights.
- The atlas shows "You + n" and lists them.
- Reserves stay at home.
- **The Phase 15 waiting-member issue.** The cause was that a member's spot was never stored, so on re-entry the body was placed at a formation slot behind the player's arrival point. Fixed: `SetFollowModeCommand` takes the body's pose (`waitPose`, saved), and the settlement draws them there. Regroup clears it.
**Regressions found by the full PlayMode suite and fixed:**

| Regression | Cause | Fix |
|---|---|---|
| Phase 14 house walking | Sampling, not the world. A move between rooms is visible for 3 minutes, and the test stepped 30. With caravan leaders away, the first houses held fewer people. | The test steps by the move window. |
| Graphics: the talking animation | The town's first visible resident is now one walking a street route (others are away with caravans). A walking resident kept walking while spoken to, so the talking pose, which plays only at a standstill, never showed. | **Product fix:** a resident on a route stops and faces the player while a conversation plays, then walks on. The test waits half a second of real time for the walk to ease out. In batch mode, 5 frames was only milliseconds. |
| Politics, settlement lists, the NPC count and the economy HUD | They assumed one region. | They compare in-view or before-and-after counts. |

## 8. Caravans and travelling groups

- **Caravans.** Every shipment in transit travels with a caravan along the planned route.
  - It is led by a person of the origin: merchants before labourers; never a shopkeeper, someone posted, a party member or an escort; one per settlement at a time.
  - Guards come from the cargo.
- **Attacks** are keyed by world seed, caravan and day. A loss records the goods as `lostOnRoad` and raises a `CaravanLost` event and a chronicle line.
- **Closed roads** halt caravans and raise `TradeDisruption`. The trade system delays arrivals and holds departures on closed routes.
- **Bandit groups** mirror the road bands' populations.
- **Patrols, faction patrols and refugees** move with their causes.

## 9. Dynamic world events

There are ten kinds, each from a cause in the systems' own state:

| Kind | Cause |
|---|---|
| Food shortage | Food reserves |
| Unrest | Stability |
| Outlaws | Band strength |
| Patrols | Factions |
| Bridges | Crack, fall and mend |
| Blockades | Hostile relations |
| Conflicts | Relation states |
| Trade disruptions | Halted caravans |
| Lost caravans | Caravan losses |
| Refugees | Prolonged shortage |

- **Lifecycle:** Active → Escalating → Resolved, Failed or Expired.
- **Keys:** one open event per cause.
- **Consequences** are applied once by key, across saves.
- **Kept:** up to 60 closed events.
- **Knowledge:** events reach people in the settlements they touch, and the player through seeing, arrival and conversation (News and Rumours), with "as of" times and out-of-date marks.
- **Wording:** the capture run showed identical headlines for two bands on one road. The wording now names who is involved: bandits or highwaymen, and the faction's patrol and its target.

## 10. Faction activity

- Factions governing a road held by outlaws send patrols. Each costs 40 coin from the treasury and takes its strength from the faction's presence. Patrols wear the band down and resolve its event.
- Hostile factions that share a crossing bar it. The stronger side holds it, and it opens when relations mend.
- New cross-region relations start wary.

## 11. Regional and offscreen simulation

Tiers: Local (the player's region), Regional (its neighbours) and Distant (the rest).
- Aggregate systems run everywhere, every day.
- Distant people get their full NPC update on their weekly day.
- Measured: home 50% and distant 0% on a sample day.
- One long advance equals many short ones, and saving in between changes nothing (tested).

## 12. Quest integration

- **`RoadOutlaws`:** break the band (three of its creatures), then report.
- **`BridgeTimber`:** three timber planks for a bridge its town cannot mend.

Both are offered once through the one quest engine (source `event:{id}`). A quest holds while its event is open or the work is done, lapses when others resolve the cause, and pays once. The Phase 12 shortage quest is linked to its event, not duplicated. Dialogues: `dlg_road_outlaws`, `dlg_bridge_timber`.

## 13. Map and interface

**The world atlas** (A, or the "World atlas (A)" nav button) has:
- eight layers: Terrain, Settlements, Roads, Factions, Economy, Threats, Events and Travel;
- painted regions and roads with clickable marks;
- a legend;
- an inspector for a region, settlement, road, group, event or the party. A settlement's inspector lists the news the player knows about it and its roads, with open news first and ended news dimmed.

**Travel planning** lists the ways there with distance, duration, arrival time and notes.

**The travel strip** has: travel 1 h, travel on, halt, resume, press on through the night, give up, fight, slip past, pay, fight it out, and withdraw.

**Other changes:**
- The 3D world view and the settlement list show the player's region (`GetSettlementsInView`).
- The atlas clock uses the calendar ("15 Seedtide, Year 412, 10:03").
- The atlas window is opaque and comes to the front.

## 14. Save schema and migration

- **Schema v18.** `MigrationV17ToV18` adds the empty routes, groups and events, sets `nextWorldEventNumber`, and adds the three systems in order.
- **On load** the route graph is built for the existing region. `DiscoverLandsBeyond` adds the rest once, deterministically.
- **New fields:**
  - region atlas fields;
  - routes, journey (with `back`), groups and events;
  - trade-route `blockedBy`;
  - party `waitPose`.
- **Tests:**
  - a v17 save keeps everything and gains its route network;
  - every older schema loads through the chain (the existing per-version tests run through v18);
  - an exact round trip of a six-region world mid-journey.

## 15. Files changed

See `git show --stat` for the commit. In outline:
- **New:** Domain atlas and validator; the Phase 16 quest catalog; Simulation `Atlas/` (5 files); Application `Atlas/` (4 files); the atlas genesis; the save mapping for the atlas; the HUD atlas; .NET tests (`Tests/EditMode/Atlas/`, 6 files); PlayMode `Phase16AtlasPlayModeTests` and `Phase16AtlasCaptureTests`; six ADRs; `Systems/Travel.md`; `Systems/WorldEvents.md`; the preflight and this report; the screenshots.
- **Modified:** the session, encounters, dialogue, events wording, party commands and queries, the generator and its port, living-world and place genesis, persistence (DTOs, mapper, migrations, serializer), trade, NPC tiers, places presence, quests, the HUD, the bootstrap, the political overlay, the runtime, the PlayMode tests for the in-view settlements, and the docs listed in §16.

## 16. ADRs and documentation

- **Created:**
  - ADR-0076: the connected world;
  - ADR-0077: the route graph and scale;
  - ADR-0078: journeys on the one clock;
  - ADR-0079: groups carry the economy's goods;
  - ADR-0080: world events;
  - ADR-0081: tiers and the atlas.
- **Updated:**
  - the ADR index;
  - `Systems/Economy.md` (daily order);
  - `Systems/SimulationScheduling.md`;
  - `Systems/Persistence.md` (v18);
  - `Systems/WorldGeneration.md` (the connected world);
  - `Systems/Party.md`;
  - `Architecture/DataOwnership.md`;
  - the Game Design Bible's status;
  - the Product Foundation's Phase 16 note;
  - the roadmap (Phase 16 done; Phase 15 carry-forward status; Phase 17 not started);
  - `Testing/TestResults.md`.

## 17. Test results

| Suite | Environment | Result |
|---|---|---|
| .NET | Windows 11, .NET 8, Release | **750 passed**, 0 failed, 0 skipped. Phase 16 adds 36 of them, all passing. |
| Unity EditMode (full) | Unity 6000.0.47f1, batch mode | **749 passed, 1 failed** (timeout), 0 skipped. See the note below. |
| Unity EditMode (`Phase14CrimeTests`, rerun after the fix) | same | **8 passed**, 0 failed |
| Unity PlayMode (full) | Unity 6000.0.47f1, batch mode | **111 passed**, 0 failed, 0 inconclusive, **28 skipped** |
| Phase 16 captures | Unity editor (not batch), `RK_CAPTURE_DIR` set | **1 passed**: 15 screenshots and the capture log |

**The 28 skipped PlayMode tests** are all capture sets. Each runs only when `RK_CAPTURE_DIR` is set, which is the same as in every earlier phase:

| Fixture | Skipped |
|---|---|
| Phase 4–8 captures | 1 each |
| Phase 9 | 4 |
| Phase 10 | 4 |
| Phase 11 | 2 |
| Phase 12 | 1 |
| Phase 13 | 1 |
| Phase 14 | 2 |
| Phase 15 art | 4 |
| Phase 15 party | 2 |
| Graphics | 1 |
| Combat overhaul | 1 |
| Phase 16 | 1 (it ran separately, above) |

**The one EditMode failure** is a 180 s timeout in `Phase14CrimeTests.AHousehold_WarnsOnce_ThenShowsYouOut_AndThatIsTrespass`, which took 189 s.
- **Cause:** the test scans every house every half hour, with a save and reload each step.
- **History:** under the editor's Mono it already took 143–167 s in the Phase 14 and 15 runs. Phase 16 makes each load somewhat heavier (the atlas step and the new validation).
- **In .NET** it takes 8 s.
- **Fix:** it now carries an explicit `[Timeout(600000)]` with that explanation. Its fixture was rerun in Unity and all 8 tests pass.
- **What was not done:** the full Unity EditMode suite was not rerun after this one-line test change.

**One intermittent .NET harness abort.** Twice, a build-and-test invocation printed "Test Run Aborted" after a small project's 8 tests, while the main assembly reported nothing. Both times the build had just run, and once Unity had just closed. Three reruns, including one forced rebuild, all ran the full 750 and passed. It did not reproduce, so no cause is claimed.

**Phase 16 tests:**

| Test file | What it covers |
|---|---|
| `Phase16WorldTests` (8) | Six connected regions; coherent borders; every settlement reachable; same seed, same world, with an exact save round trip; a v17 save upgraded and gaining its lands; the validator catching broken geography; disconnected lands refused; each region's map from its own seed |
| `Phase16TravelTests` (12) | A trip on the clock; crossing a border with the party; entering another region refused; costed alternatives; outlaws fought through the one engine, paid or slipped past; turning from the road and giving up; saving mid-journey; a road closing under the traveller (the way back stays open); bridges closing and opening ways; the party travelling together; a waiting member keeping their spot |
| `Phase16SimulationTests` (15) | Every shipment a caravan; arrival equals delivery; losses recorded, never vanished; guards that repel; one long advance equals many short ones; shortage events; a fallen bridge mended once; outlaws with a patrol and a quest; barred crossings; the player breaking a band, with the quest paying once; expiry and one event per cause; news through people; offers lapsing; distant regions living more cheaply; the chronicle bounded |
| `Phase16PerformanceTests` (1) | The measurements in §19 |
| `Phase16AtlasPlayModeTests` (4, the real scene) | The atlas shows the connected world, and a journey chosen on it ends in the town; the party travels and the map says so; outlaws met with a choice; **every atlas control through the window**: the nav button, Close, all eight layer buttons, region, settlement and road selection, an alternative way, an hour, halt, resume, slip past, fight then withdraw, and give up |

**The brief's validation list:**

| Check | Covered by |
|---|---|
| Migration | v17 upgrade test, and the older per-version tests now run through v18 |
| Determinism | Same seed, same world; long advance equals short ones |
| Multi-region generation and route connectivity | World tests |
| Travel integrity | Travel tests |
| Caravan inventory accounting and economic reconciliation | `TradeSystem`'s conservation check still throws on any loss outside recorded transit loss, and the caravan-loss tests assert `lostOnRoad` |
| Event idempotency | Bridge mended once; one event per cause; quest pays once |
| Factions | Patrols and barred crossings |
| Party transitions | Border crossing with the party; no duplicates; the PlayMode arrival |
| Quests | Event quests offered, paid and lapsing |
| Performance | §19 |

The validator also refuses duplicate route, group and event IDs, two open events for one cause, and a consequence applied twice.

## 18. Screenshots and recordings

There are fifteen **automated** captures (`Phase16AtlasCaptureTests`, run in the real scene with `RK_CAPTURE_DIR`), as JPEG in `Documentation/Screenshots/Phase16/`. The capture log says what each shows. I inspected every image. Where the test changed the world to show something, the caption says so: the road bands were made bold for the encounter, and a bridge was felled for the disrupted road.

| # | File | Shows |
|---|---|---|
| 1 | `p16_01_atlas_full` | All layers; six regions, four of them charted |
| 2 | `p16_02_region_inspection` | The home region: kind, landmarks, economy, influence |
| 3 | `p16_03_connections` | Roads and a bridge crossing selected |
| 4 | `p16_04_route_selection` | A settlement's ways there, from the route data |
| 5 | `p16_05_route_comparison` | Three ways to a town beyond the border |
| 6 | `p16_06_travel_progress` | The journey under way: percent, km left, next stop |
| 7 | `p16_07_party_on_map` | "You + 2" and the party inspector |
| 8 | `p16_08_caravan_inspection` | A caravan: leader, cargo, history (it beat off outlaws) |
| 9 | `p16_09_event_inspection` | A patrol event the player saw for themselves |
| 10 | `p16_10_faction_layer` | Faction tints and regional shares |
| 11 | `p16_11_economy_layer` | Food, caravans and shortages |
| 12 | `p16_12_travel_encounter` | Outlaws bar the road: fight, slip past, pay |
| 13 | `p16_13_disrupted_route` | A road closed by a fallen bridge |
| 14 | `p16_14_arrival` | Arrived in the town with two companions |
| 15 | `p16_15_event_affecting_settlement` | A town's news about its roads |

**Recordings:** none were made. The harness captures stills only. The four requested recordings were not practical here, and none is claimed.

## 19. Performance

Six regions: 18 settlements, 119 people, 25 road segments, 24 active groups (22 caravans), 5 open events.

| Measure | Result |
|---|---|
| Route planning, three alternatives | 0.19 ms per pair |
| A simulated day | 16.9 ms |
| The atlas view | 0.13 ms |
| An hour of travel, with the clock | 6.3 ms |
| Save | 112 ms, 5.0 MB |
| Load | 371 ms |
| Generation | 0.6 s |

`Phase16PerformanceTests` holds generous bounds.

## 20. Determinism

- The same seed gives the same six-region world, and a save and load gives the identical document (tested).
- Road threats are keyed by journey and hour, and caravan attacks by caravan and day. Neither draws from a shared stream.
- One long advance equals many short ones (tested).
- Event consequences apply once across saves (tested).

## 21. Known limitations and deferred work

- **"Fight it out" on the atlas** resolves a road fight through the engine with the AI choosing the player's actions, because the map has no fight scene.
- **Groups are drawn** only in the player's region. Far caravans live in the simulation but are not drawn.
- **Travel** is on foot only. Mounts and ships are excluded by the brief, but `TravelMode` is the extension point.
- **The 3D world view** shows one region at a time; the atlas is the overview.
- **Distant people** get their full update weekly. Aggregates are daily.
- **Event scope** is local and regional. There is no war between kingdoms (excluded).
- **"Discover the lands beyond"** (for old one-region worlds) is tested in .NET. The PlayMode scene starts a six-region world, so its button is not exercised there.
- **No recordings.**
- **Phase 15 items still open:** see the roadmap's carry-forward table.

## 22. Git

- **Branch and base:** `feature/phase-0-1-foundation`, on top of `48b0098`.
- **The commit:** one local commit holds the Phase 16 implementation, tests, screenshots and documentation. A commit cannot contain its own hash, so it is given in the hand-off message and in `git log`.
- **Not pushed.** The brief says "Do not push".
- **Staging:** the files were reviewed before committing. The commit used `git add -A -- . ':!ProjectSettings/GraphicsSettings.asset' ':!Assets/InitTestScene*'`.

## 23. User-owned changes

- `ProjectSettings/GraphicsSettings.asset` was already committed and pushed by the owner (`48b0098`). It was not modified.
- The working tree was clean at the start.
- Unity's generated `Assets/InitTestScene*` files and the local-only Quaternius Bestiary were not staged.
- Nothing was discarded or pushed.
