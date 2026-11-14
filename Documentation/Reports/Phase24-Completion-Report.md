# Phase 24 completion report

**Phase:** Overland World Living Systems, Navigation Integration, Encounters, Hazards & Ground Weather.

**Date:** 2026-11-13.

**Baseline:** `f46634d` (Phase 23, pushed). Phase 24 was committed locally as `e0df4dd` per the brief, then pushed to origin on 2026-11-14 at the owner's instruction.

## 1. Executive summary

The walkable country from Phase 23 now has life, hazards and one consistent sense of where the player is.

**What was added:**
- **Landmarks.** Each region has landmarks generated from its seed and geography: waystones, hilltop cairns, travellers' camps, abandoned camps, ruined steadings, watch posts, and wrecks where a real lost caravan lies. They are found by sight and can be read, climbed, searched or rested at.
- **Travellers.** The world simulation's own travelling groups are drawn where their journeys have them, on their roads. You can talk to them, and you can help refugees.
- **Navigation.** The atlas marks the player's real position. Waypoints appear on the compass.
- **Journeys.** A planned journey and walking on foot share one travel state, so you can step off a journey and rejoin it.
- **Encounters.** They come only from real creature populations and bandit bands, and they are fought with the one combat engine on the ground where you stand.
- **Ground weather.** It comes from the existing saved weather: wet ground, mud, snow, frozen ground, flooded fords and shortened sight. It slows both the clock and the player's pace, and it changes how the ground looks.
- **Saves.** The schema moves to v26, with a structural migration from v25.
- **Footsteps (owner request).** They are now quieter and land on the walk cycle.

**Tests:**
- .NET 938/938.
- Unity EditMode 938/938.
- Unity PlayMode: 132 passed, 1 failed, 33 skipped (capture-only).
  - The one failure is the new Phase 24 test. The game behaved correctly; the test had not allowed for a Phase 16 road threat (§15).
  - The test was fixed and re-run on its own: it passes.

**Not hand-played.** The owner checklist is open.

## 2. Preflight findings

The full audit is in `Systems/Phase24-Preflight-Audit.md`. The main points:

- **Git:** clean at `f46634d`, the same as origin. The untracked `InitTestScene*` files were preserved, and so was `GraphicsSettings.asset`.
- **Already in place to reuse:**
  - Phase 16 travelling groups, journeys, road threats and bandit bands;
  - Phase 21 weather per region (`GroundWetness`, `SnowCover`, three-hour periods);
  - creature populations with their ranges;
  - `EncounterGenerator` and `WorldEncounters`;
  - the loot tables;
  - discovery, reputation and rumours;
  - the Phase 23 overland terrain, compass and region map.
- **Gaps:**
  - The country had no landmarks.
  - Groups existed only on the atlas.
  - The atlas marked the player's last town, not where they actually were.
  - A journey's road position and the country pose were two unconnected places.
  - There were no encounters on foot.
  - Weather did not touch the ground.
- **Decisions taken in the audit:**
  - one travel state, with a mapping between road positions and country points, rather than a second state;
  - every encounter and traveller comes from existing simulation data;
  - ground conditions are derived from the weather, not saved.

## 3. Features

| Feature | Where | Status |
|---|---|---|
| Landmarks: 7 kinds, found, read, climbed, searched, rested at | `Landmarks.cs`, `OverlandLifeViews.cs` | Implemented, tested |
| Travellers on the roads, talking to them, helping refugees | `OverlandLife.cs` (`Travellers`) | Implemented; the travellers are tested; talking to refugees has no test of its own |
| The atlas marks the player's real position | `AtlasQueries.PlayerAt`, `OverlandCommands.MapPoint` | Implemented, tested |
| Waypoints: settlements, places, gateways, landmarks, the journey | `OverlandNavigation.cs` (`Waypoints`) | Implemented, tested |
| One travel state: step off a journey, rejoin it, finish on foot | `OverlandCommands` (Enter, Leave, Rejoin), `OverlandEntryHandlers` | Implemented, tested |
| Overland encounters: creatures and bandits; fight, slip away, back off, pay | `OverlandEncounters` | Implemented, tested |
| Night rest broken in the open; safe at a travellers' camp | `RestRules`, `SpellCommands.Rest` | Implemented; no test of its own |
| Ground weather: walking, fords, sight and the look of the ground | `GroundConditions.cs`, `OverlandWorld`, `VertexColorLit.shader` | Implemented, tested |
| Save v26 | `WorldSaveMapper.Overland.cs`, `MigrationV25ToV26` | Implemented, tested |
| Footsteps on the walk cycle, quieter | `HumanoidCharacter.cs` | Implemented; not checked by ear |

## 4. Reused systems

- **Travelling groups and journeys (Phase 16).**
  - `TravelingGroupData` and its road positions supply the visible travellers.
  - `JourneyData` (segment, from, metres) is the road side of the one travel state.
  - The road threat and its toll were reused unchanged.
- **Bandit bands and creature populations** are the only encounter sources.
- **Combat.** `EncounterGenerator.Plan` and `WorldEncounters.StartPlan` run the fight. There is no second combat engine.
- **Weather (Phase 21).** `RegionWeatherData` is the only input to the ground conditions.
- **Loot.** The `old_cache` roll fills landmark caches, held in the existing container and storage UI.
- **Knowledge.** Discovery, `PlayerLearns`, rumours and reputation are reused for wrecks, news and refugees.
- **Phase 23 overland.**
  - The terrain, barriers and crossings.
  - The compass and region map (for the marks).
  - Loading transitions.
  - `NpcAppearanceResolver` and `HumanoidCharacterBuilder` for traveller bodies.
  - The owned kits for landmark models.

## 5. Architecture

**Layers.**
- **Domain:**
  - `OverlandState` (landmark states, encounter records and the encounter clock);
  - `GroundConditions` (a pure rule);
  - `WorldValidator.Overland`.
- **Application:**
  - `Landmarks` and `LandmarkGenesis`;
  - `Travellers`;
  - `OverlandEncounters`;
  - `OverlandRoads` and `Waypoints`;
  - the commands and queries on `WorldSession`.
- **Infrastructure:** the v26 DTOs and the migration.
- **Presentation:**
  - `LandmarkView` and `TravellerGroupView`;
  - `ExplorationController.OverlandLife` (the HUD lines and the encounter panel);
  - the shader values.

**Invariants.**
- **One world state.** Everything new hangs off `WorldData.Overland` and `PlayerCharacterData.Waypoint`.
- **One travel state.** A journey's position maps to a country point through `OverlandRoads.PointOf`. Leaving the country maps back to a road position, a settlement or a place.
- **Never saved, always the same.** Generated landmarks and ground conditions are derived, deterministic and never saved.
- **No new world content.** Encounters and travellers never create creatures, groups or factions.

**Decisions.**
- ADR-0113: one travel state and waypoints.
- ADR-0114: landmarks, visible travellers and encounters.
- ADR-0115: ground weather.

## 6. Landmarks and travellers

**Landmarks.**
- **Caps per region:**
  - waystones at most 6, 40 cells apart along roads, and 10 cells apart at junctions;
  - cairns at most 4;
  - travellers' camps at most 3;
  - abandoned camps 2;
  - ruined steadings 2;
  - a watch post at each faction's road exit;
  - a wreck for each real `CaravanLost` event.
- **In the test region:** 12 landmarks.
- **Ids:** stable (`landmark:{region}:{kind}:{n}`).
- **Placement:** always on walkable ground, falling back to the nearest walkable cell.
- **Wording:** waystones name each road's settlement, its compass direction and the hours on foot. No names or lore are invented.
- **Interactions:**
  - read (waystone);
  - climb (cairn: finds places and landmarks within 650 m, less in poor weather and at night);
  - search once (camps, steadings, wrecks);
  - rest (a travellers' camp: never interrupted);
  - standing (watch post).
- **Models:** built from owned kit models only.

**Travellers.**
- **In the test region:** 8 groups at the time, 6 bandit bands and 2 caravans.
- **Where they are drawn:** where the simulation has them.
- **When they are drawn:**
  - bodies are made within 600 m and dropped beyond 800 m;
  - at most four figures a group, the leader with their real looks;
  - a caravan has its wagon.
- **Moving with the simulation:** a caravan moved 12 m along its road over a short wait, toward the same destination.
- **Talking:**
  - shares news: the unresolved events of their origin, destination or roads, learnt as rumours;
  - a patrol answers by your standing.
- **Refugees:** a ration raises their home town's reputation, once per group.

## 7. Navigation and journeys

**The atlas marks the player's real position.**
- Turning to the map from the country first walks to:
  - the waiting journey's point, when within 80 m of it;
  - a settlement, when it is nearer than any road or within 400 m;
  - otherwise the nearest road.
- The walk is charged to the clock.
- In the test, the map marked cell (12, 81) after the player walked out from (25.7, 72.7). That is the nearest road, within the 40-cell test bound.

**Waypoints.**
- What can be marked: known settlements, places, gateways, landmarks, or the journey.
- Unknown targets are refused.
- In the player's own region, the waypoint points to the target itself, for example "Mossholm: 2.9 km".
- In another region, it points to the exit toward it (a breadth-first search over passable crossings).

**Journeys.**
- **Stepping off:** done from the atlas ("Walk from here (on foot)") or by entering the country on the road. The journey is halted and the player put on the road at its point, in 512 ms in the test.
- **Rejoining:** J rejoins within 80 m, and the journey continues as Travelling.
- **Entering a settlement on foot:**
  - the destination: the journey is Arrived;
  - another settlement: it is Abandoned.
- **Crossing a region border on foot:** the journey is Abandoned.

## 8. Encounters

**When they are checked.**
- While walking, once per half-hour bucket of the clock (`encounter:{region}:{bucket}`).
- After any encounter, three quiet hours.

**Sources.** Only:
- creature populations whose range covers the spot, with a challenge rating above 0, so rabbits never count;
- bandit bands stationed within 160 m of a road.

**Odds.**
- Raised by: night, deep woods, marsh and rocks.
- Lowered by: roads, towns, watch posts within 700 m, patrols and poor sight.
- The roll is seeded (`NpcHash`), so loading never rerolls an encounter.

**Choices.**
- **Fight:** the existing combat on the overland ground. In the test, a boar encounter was fought there (`p24_07b`).
- **Slip away:** a Stealth check.
- **Back off.**
- **Pay** (bandits only).
- Walking more than 200 m away counts as backing off.

**Record.** The 40 most recent outcomes are kept. At most one encounter is pending at a time, and the validator checks it.

**Night rest.** A long rest at night in the open, inside a population's range, can be broken. A travellers' camp is safe.

## 9. Weather and terrain

`GroundConditions.Of(region weather, minute)` is a pure rule; nothing new is saved.

| State | Condition | Road | Off the road | Ford |
|---|---|---|---|---|
| Wet | wetness 40+ | ×1.0 | ×1.15 | ×1.1 |
| Mud | wetness 70+ | ×1.1 | ×1.4 | ×1.3 |
| Snow | snow cover 20+ | ×1.1 | ×1.5 | ×1.2 |
| Deep snow | snow cover 60+ | ×1.3 | ×2.0 | ×1.3 |
| Frozen | ≤ −2 °C on wet ground | ×1.05 | ×1.1 | ×1.5 |

- **Fords.**
  - **When they flood:** at wetness 85 or more, or at 70 or more while it rains hard (and not when frozen).
  - **What flooding does:** a flooded ford becomes a barrier in the view, and stepping onto it is refused with the way round ("cross by a bridge").
  - **Bridges and journeys are unaffected:** bridges are roads, and journeys' routes are roads.
- **Sight.**
  - Thick fog ×0.4, fog ×0.6, severe weather ×0.6, rain or snow ×0.8; night halves it.
  - It shortens discovery and a cairn's view, and lowers encounter odds slightly.
- **One factor, two effects.** The same factor charges the clock (`WalkMinutes`) and divides the player's pace.
- **The look of the ground.** Two shader values, set only when the condition changes: wet ground is darker, and snow whitens level ground.
- **In the test:** Muddy ground (wetness 100), fords flooded, off-road walking ×1.40, sight ×0.80. Later the ford was fordable again (`p24_08`, `p24_09`).

## 10. Saves

- **Schema v26.**
  - `world.overland`: landmark states (found, found at, searched, rested day), encounter records, `LastCheckBucket` and `LastEncounterAt`.
  - `player.waypoint`.
- **Migration.** `MigrationV25ToV26` is structural: both fields are optional. A v25 save loads with nothing found, nothing met and no mark.
- **Tested in the played scene.** Saving and loading in the country kept the 2 found landmarks, the 10 travelling groups and the waypoint.
- **Tested in .NET.** Round trips, and the migration.
- **Schema pins.** Two older tests pin the schema version. They were updated from 25 to 26, because the version changed intentionally.

## 11. Performance

Measured in the editor in batch mode on the development machine, not a player build.

| Measure | Phase 24 | Note |
|---|---|---|
| Out into the country (town to country) | 831 ms (1050 ms on one earlier run) | Before region filtering it was 1.9 s: travellers and waypoints had built other regions' terrain. Now only the player's region is built |
| Stepping off a journey (map to country) | 512 ms (749 ms on one run) | |
| Living-country refresh (once a second) | 0.2–0.3 ms | Travellers, ground, marks |
| Landmark generation | once per region per session | Cached |
| Region round trips | 1003 → 1003 objects, 718 → 719 MB | No growth across repeated transitions |

Nothing new scans the world every frame. The ground material is updated only when the condition changes.

## 12. .NET results

`Tools/DotNet`, Release: **938 passed, 0 failed, 0 skipped (938 total)**, in 5 min 46 s. That is the 928 tests of Phase 23 plus the 10 Phase 24 tests (`Phase24OverlandLifeTests`).

## 13. EditMode results

Full Unity EditMode suite (batch mode, Unity 6000.0.47f1): **938 passed, 0 failed, 0 skipped (938 total)**, in 2527 s.

## 14. PlayMode results

**The full suite** (166 test cases):
- **132 passed, 1 failed, 33 skipped**, in 801 s;
- the 33 skipped are the capture-only tests, ignored outside capture runs, as in earlier phases.

**The fix.** The one failure (§15) was fixed in the test. `Phase24PlayModeTests` was then re-run on its own: **1/1 passed**, with the 11 captures written.

## 15. Regression and failure analysis

| Test | Classification | Cause | Action |
|---|---|---|---|
| `Phase24PlayModeTests.TheLivingCountry_…` (full run, line 255: "Outlaws bar the road: deal with them first.") | **Test defect in the new test**: not a regression, not environment, not pre-existing | In the full run, the test's hour of journey travel met a Phase 16 road threat. Stepping off the journey is correctly refused until the threat is settled, which is existing Phase 16 behaviour. When run alone, the hour met no outlaws, so the test had not allowed for it | The test now settles a threat first (pays, or slips past), and is inconclusive rather than failing if one cannot be settled. Re-run alone: passed. On that run no threat was met, so the new branch was not exercised |

**Changes to older tests**, all because behaviour changed intentionally:
- **`Phase17StoryTests`, `Phase20AAppearanceTests`:** the schema pin is now 26.
- **`Phase23PlayModeTests`:** its from-the-map step must step off the journey when one is planned, because entering the country on the road now halts the journey (ADR-0113).
  - It now uses `EnterOverlandCommand` when there is a journey.
  - It still uses `WalkCountryBySelected` when there is none.

No expectation was changed merely to make a test pass.

**Timing sensitivity.** Whether the journey's hour meets outlaws can differ between a run alone and the full suite, so the test is written to pass either way.

## 16. Screenshots

All are in `Documentation/Screenshots/Phase24/`, written by `Phase24PlayModeTests`, with its log in `p24_log.txt`.

| # | File | Shows |
|---|---|---|
| 1 | `p24_01_landmarks.jpg` | A roadside landmark (ruined steading) from the owned kits |
| 2 | `p24_02_traveller.jpg` | A caravan of the simulation on its road |
| 3 | `p24_03_roadside_interaction.jpg` | Reading a waystone |
| 4 | `p24_04_map_position.jpg` | The atlas with the player's real position |
| 5 | `p24_05_waypoint.jpg` | A waypoint on the compass, with its distance |
| 6 | `p24_06_stepped_off.jpg` | Off the road mid-journey; "Back on the road (J)" |
| 7 | `p24_07_encounter.jpg` | An encounter panel (boars in deep woods) |
| 7b | `p24_07b_encounter_fight.jpg` | The encounter fought on the overland ground |
| 8 | `p24_08_weather_ground.jpg` | Muddy ground, fords in flood |
| 9 | `p24_09_river_crossing.jpg` | A ford under the current conditions |
| 10 | `p24_10_after_load.jpg` | After saving and loading in the country |

## 17. Docs

**New:**
- `Systems/Phase24-Preflight-Audit.md`
- `Systems/Overland-Living-Systems.md`
- `Systems/Phase24-Owner-Playtest-Checklist.md`
- this report
- ADR-0113, ADR-0114, ADR-0115

**Updated:**
- `Decisions/README.md`
- `Systems/Persistence.md` (v26)
- `Systems/Overland.md` (cross-link)
- `Roadmap.md`
- `Product/GameDesignBible.md`
- `Product/FutureRoadmap.md`
- `Product/CampaignBible.md` (no content added; the note says so)

## 18. Git

- **Branch:** `feature/phase-0-1-foundation`, based on `f46634d`.
- **Commits:** the Phase 24 work is committed locally in one commit, with its hash in the closing message.
- **Left out on purpose:**
  - `ProjectSettings/GraphicsSettings.asset` and the untracked `Assets/InitTestScene*` files, which stay untracked and were not deleted;
  - `M_LampGlow.mat`, which batch runs re-save; it was restored and not committed.
- **Push:** nothing was pushed under the brief. On 2026-11-14 the owner asked for the push, and `e0df4dd` was pushed to origin.

## 19. Deferred work

**Not here yet:**
- encounters that are escort requests, stranded travellers or roadside trade;
- quests begun by encounters;
- patrols turning hostile.

**Known limits:**
- **Map positions** are only roads, settlements or places, never arbitrary off-road points (there is no off-road route planner).
- **Flooded fords** are barred and announced, but the water is not drawn higher.
- **The ancient realm** has no landmarks or encounters, because its content is undecided.
- **Measurements** are editor-only, not from a player build.

**Not verified:**
- **Footsteps.** I cannot listen in batch mode, so the owner should judge them by ear. They can be tuned with `FootfallPhase` and `FootstepWalkGain` / `FootstepRunGain`.
- **No dedicated tests** for talking to refugees and for a night rest being broken.
- **Layouts below 1280×720** are unchanged and unsupported.
- **D1** (the 40 m walking scale) still needs the owner's confirmation.

## 20. Checklist

`Systems/Phase24-Owner-Playtest-Checklist.md` has 21 checks:
- landmarks;
- travellers;
- navigation;
- journeys;
- encounters;
- ground weather;
- stability;
- the owner's calls on pace, map positions and D1.

Nobody has hand-played Phase 24.

## 21. Phase 25 priorities

Suggested; the next brief decides.

1. **Hand-play Phase 24.** Tune encounter frequency, landmark density and the mud and snow factors from the owner's playtest.
2. **Confirm D1** (40 m a cell) before more content is tied to the scale.
3. **Arbitrary map positions** with an off-road route planner, if the owner wants them (an owner call in the checklist).
4. **Fill in the encounter kinds:** escorts, stranded travellers, roadside trade and quest hooks, from existing systems only.
5. **Draw water levels** on flooded fords and rivers in spate.
6. **Measure in a player build** (Phase 23 and 24 figures are editor-only).
7. **Add dedicated tests** for helping refugees and for broken night rest.
