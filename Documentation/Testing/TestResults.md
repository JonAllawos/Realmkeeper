# Test results

Only results from runs that actually took place are recorded here.

## Run 1: .NET harness, outside Unity

| | |
|---|---|
| Date | 2026-11-08 |
| Environment | Linux 6.18 container, .NET SDK 8.0.133, NUnit 3.14.0, NUnit3TestAdapter 4.5.0 |
| Command | `Tools/DotNet/verify.sh` |
| Build | Succeeded, **0 warnings, 0 errors** (warnings treated as errors) |
| Result | **75 passed, 0 failed, 0 skipped** (61 test methods; parameterized cases counted separately) |
| Duration | about 2 s |

| Fixture | Area | Methods | Cases | Result |
|---|---|---|---|---|
| `IdentifierAndRandomTests` | Domain: ID uniqueness, RNG determinism, golden values | 8 | 8 | Pass |
| `WorldModelTests` | Domain: world initialization, settlement and inventory validation, calendar | 10 | 13 | Pass |
| `SimulationSchedulerTests` | Simulation: dates, production, consumption, shortages, starvation, determinism, ordering, invalid state | 16 | 16 | Pass |
| `RegionGeneratorTests` | Generation: same-seed reproducibility, different-seed variation, placement, terrain validity, roads, editor | 8 | 8 | Pass |
| `PersistenceTests` | Persistence: round trip, ID and date preservation, missing/corrupt/future/invalid files, migrations, slot safety, atomic overwrite | 11 | 22 | Pass |
| `WorldSessionTests` | Application: commands, queries, save/load through the session, playback pacing | 8 | 8 | Pass |

### Checks beyond pass/fail

- **RNG reference check:** the xoshiro256** golden values in `IdentifierAndRandomTests`
  were computed with a separate Python implementation, not taken from the C# output.
- **Mutation check:** event IDs were temporarily switched to `Guid.NewGuid()`.
  `Simulation_IsDeterministicForIdenticalInputs` failed as it should, and the change was
  reverted.
- **Two thresholds were changed after a failing run.** Both were calibration errors in the
  tests, not product bugs, and both are recorded here for transparency:
  1. The hills floor was lowered from 2% to 1% of cells. Seed 123456789 produced 1.9%.
  2. The persistence fixture was lengthened from 150 to 300 simulated days. The fixture
     seed's first shortage falls after day 150, and the test needs events in history.

## Run 2: Unity Editor, EditMode (Gate 1)

| | |
|---|---|
| Date | 2026-11-08 (local; 2026-11-09 06:08 UTC) |
| Environment | Windows 11 Pro, Unity 6000.0.47f1, Test Framework 1.5.1 (NUnit 3.5), newtonsoft-json 3.2.1 |
| Command | Test Runner → EditMode → Run All |
| Build | Unity script compilation succeeded with **0 compiler errors, 0 compiler warnings**. Domain, Simulation, Application, Infrastructure and Tests.EditMode compiled; Presentation and Tests.PlayMode have no scripts yet, so they produce no DLL |
| Console | 0 errors, 0 warnings (only the test framework's info messages) |
| Result | **75 passed, 0 failed, 0 skipped** (61 test methods), the same totals as Run 1 |
| Duration | about 4 s |

Per fixture: `IdentifierAndRandomTests` 8/8, `WorldModelTests` 13/13,
`SimulationSchedulerTests` 16/16, `RegionGeneratorTests` 8/8, `PersistenceTests` 22/22,
`WorldSessionTests` 8/8.

None of the likely first-import issues in the handoff occurred. The asmdefs needed no changes.

## Run 3: Unity Editor, EditMode, after Stage E

| | |
|---|---|
| Date | 2026-11-08 (local; 2026-11-09 06:50 UTC) |
| Environment | As Run 2 |
| Result | **75 passed, 0 failed, 0 skipped**. Stage E added no core code, so the totals are unchanged |
| Play mode smoke check | `Realmkeeper.unity` played with no console errors or exceptions. The bootstrap logged: 16 terrain chunks, 3 settlements, 16,591 trees, 2 bridge cells (seed 123456789) |

## Run 4: Unity Editor, EditMode, after Stage F

| | |
|---|---|
| Date | 2026-11-08 (local; 2026-11-09 06:59 UTC) |
| Environment | As Run 2 |
| Result | **75 passed, 0 failed, 0 skipped** |
| Play mode manual check | No console errors or warnings. Selecting Bramhaven from the list showed the highlight ring and the inspector (population 1,644; food 9,570 at −21/day, 455 days remaining). +1 week, +1 month and Play advanced the date, and Save wrote `autosave.rkworld.json` dated 0412-04-09 (38 days after the start). Load and the Escape key were not checked separately |

## Run 5: Unity Editor, EditMode and PlayMode, after Stage H

| | |
|---|---|
| Date | 2026-11-09 (local 00:04 and 00:07; 07:04 and 07:07 UTC) |
| Environment | As Run 2 |
| EditMode | **75 passed, 0 failed, 0 skipped** |
| PlayMode | **11 passed, 0 failed, 0 skipped**, about 4.5 s. Run separately because both suites write to the same `TestResults.xml` |

| PlayMode fixture | Test | Handoff item |
|---|---|---|
| `WorldPresentationTests` | `Bootstrap_CreatesWorld_WithOneSettlementViewPerSettlementId` | 1 |
| | `TerrainMesh_VertexCountAndBounds_MatchRegionMap` | 2 |
| | `Forests_AreInstanced_WithinTargetRange` | extra |
| `CameraTests` | `Pan_MovesPivot_AndStaysClampedAtRegionEdge` | 3 |
| | `Zoom_ChangesOrthoSizeSmoothly_WithinLimits` | 4 |
| | `Rotate_SnapsToQuarterTurn_AfterEasing` | 5 |
| `SelectionAndHudTests` | `ClickingSettlement_SelectsItsId_AndShowsHighlight` | 6 |
| | `AfterSelection_InspectorShowsSettlementPopulationAndFood` | 7 |
| | `ClearingSelection_HidesInspectorAndHighlight` | 8 |
| | `PlusOneWeekButton_AdvancesSevenDays_AndRefreshesDateLabel` | 9 |
| | `PlayPause_TogglesPlayback_AndDateHoldsWhilePaused` | 10 |

The tests build the real bootstrap in the test scene with device input switched off. Camera
tests drive `IsometricCameraMath` with synthetic `CameraInput`. Selection uses
`SelectAtScreenPoint` with the settlement's projected screen position, and buttons receive
UI Toolkit `NavigationSubmitEvent`s. Real mouse and keyboard reading
(`CameraInputReader`, the Escape key) is checked by hand only.

## Run 6: Phase 2 (economy, trade, chronicle), 2026-11-09

| Suite | Where | Result |
|---|---|---|
| EditMode | .NET 8 harness (`dotnet test Tools/DotNet`) | **110 passed, 0 failed** (75 existing, some updated for Phase 2 behaviour, + 35 new) |
| EditMode | **Unity 6000.0.47f1, batch mode** (`-runTests -testPlatform EditMode`) | **110 passed, 0 failed**, 18 s |
| PlayMode | **Unity 6000.0.47f1, batch mode** (`-runTests -testPlatform PlayMode`) | **17 passed, 0 failed** (11 existing + 6 new), 15 s |

- **First Unity compile failed.** A PlayMode test was missing `using Realmkeeper.Presentation;`. It
  was fixed before the runs above. The out-of-Unity Presentation compile check did not cover
  test assemblies.
- **Mutation check:** making trade departures remove one unit less than they ship (creating
  goods) failed 30 tests, including every trade test. It was reverted, and the reversion was verified.
- **Manual play check** (editor, seed 123456789): +1 month advanced exactly 30 days. The town
  imported 8 iron and paid 48 coin (8 × 6). The chronicle recorded "18 timber from Bramhaven
  reached Westerthorpe for 54 coin" and 10-point stability rises. The console stayed free of
  errors and warnings.
- **Layout bugs found by eye, then fixed:** squashed event rows, the badge overlapping the
  header, the economy table too wide. The fixes were confirmed in a later screenshot. They are not covered by an automated test.

| New EditMode fixture | Tests | Covers |
|---|---|---|
| `Phase2.EconomyTests` | 10 | Production formula, storage limit and waste, consumption and shortfall, food balance, exhaustion estimate, labour priority and caps, specialization targets, seasonal hook, resource shortage warnings |
| `Phase2.TradeTests` | 9 | Successful shipment and payment, insufficient surplus, capacity limits, destination capacity, wait-then-return with nothing lost, buyer coin limits, 200-day conservation, failed day leaves no partial transfer, duplicate shipments rejected |
| `Phase2.SettlementWellbeingTests` | 7 | Prosperity and stability formulas, factors, ≤ 1 point per day, gradual food-shortage consequences, population growth and emigration, milestone events not daily |
| `Phase2.SimulationAndPersistenceTests` | 9 | 1/7/30-day commands, 7 × 1 = 7 days, determinism over a year, speed affects timing only, save/load mid-shipment, chronicle across save/load, Phase 1 save migration, event formatting, inspector snapshot |

| New PlayMode test (`EconomyHudTests`) | Covers |
|---|---|
| `Inspector_ShowsOneEconomyRowPerResource_WithSimulatedValues` | Table rows and values come from the ledger |
| `AdvanceMonthButton_Advances30Days_AndTradeMovesGoods` | Button path, 30 days, deliveries happen |
| `Inspector_ListsTheSelectedSettlementsTradeRoutes` | Trade section |
| `RecentEvents_ShowTheChroniclesFormattedEntries` | Event panel shows formatted facts |
| `ShortageWarnings_AppearForASettlementThatRunsShort` | Warnings and highlighted rows |
| `SaveAndLoad_RestoreTheWorldAndTheChronicleShownInTheHud` | Save/load through the session, HUD restored |

Screenshots (1920×1080, captured with the F10 tour; these show the Phase 1 HUD):
[overview](../Screenshots/realmkeeper-overview.png) ·
[settlement selected](../Screenshots/realmkeeper-settlement-selected.png).

## ADR-0009 prerequisites and exploration vertical slice (2026-11-09)

| Run | Where | Result |
|---|---|---|
| .NET harness | Windows, .NET 8 (`dotnet test Tools/DotNet/Realmkeeper.Verification.sln -c Release`) | **135 passed, 0 failed** (110 earlier + 25 new: region identity 7, session and commands 10, exploration domain and application 8) |
| Unity EditMode | Unity 6000.0.47f1 Test Runner, run by the user in the editor | **135 passed, 0 failed, 0 skipped** |
| Unity PlayMode | Unity 6000.0.47f1 Test Runner, run by the user in the editor | **30 passed, 0 failed, 0 skipped** (17 earlier + 13 new in `ExplorationPlayModeTests`) |
| Gameplay | Play Mode in the editor, played by the user; screenshots `Documentation/Screenshots/dev-20261109-1011*.png` and `-1012*.png` | Entered Bramhaven from the inspector; walked in third and first person; read the granary ledger (shown as informational) and saw the posted labour notice reflected in it |

**What the new PlayMode tests cover:**
- entering a settlement loads an environment keyed by its settlement and region IDs, and hides the map;
- returning to the map restores it and selects the settlement just left;
- repeated entry creates no duplicate residents, characters or cameras;
- entering twice is refused;
- the character walks, stays grounded and jumps under scripted input;
- switching perspective keeps the character's position;
- the third-person camera is kept in front of walls;
- talking to a resident and posting a notice are recorded through the registry;
- the ledger is informational and changes nothing;
- saving and reloading inside a settlement restores the character;
- the clock keeps running while exploring;
- a second runtime is refused.

**Found by gameplay, not by tests, and fixed after these runs:**
- roofs were built inverted (the slab pitch signs were swapped);
- the granary's loading platform covered the ledger lectern, and barrels crowded the granary door.

After the fixes the .NET harness passed 135/135 again. The Unity suites were **not** re-run
after these presentation-only fixes. The user replayed and confirmed the roofs and granary front look right.

**Not tested:**
- the mouse-look feel, and cursor capture with Esc and click;
- the Save (F5) → Load round trip in manual play (it is covered by the PlayMode test, but no screenshot of it was taken).

## Pre-Phase-2 stabilization and Phase 2 completion (2026-11-09)

| Run | Where | Result |
|---|---|---|
| .NET harness | Windows, .NET 8, Release | **174 passed, 0 failed** (135 earlier + 39 new) |
| Unity EditMode | Unity 6000.0.47f1 Test Runner, run by the user, 13:32 | **174 passed, 0 failed, 0 skipped** |
| Unity PlayMode | Unity 6000.0.47f1 Test Runner, run by the user, 13:32 | **37 passed, 0 failed, 0 skipped** (30 earlier + 7 new) |
| Gameplay check | Play Mode, screenshots `Documentation/Screenshots/dev-20261109-1332*.png` | Inspector shows the "Why" lines and the reserves trend; a strong-production marker shows on the map |

**New tests:**

| Where | Tests | Cases |
|---|---|---|
| EditMode, `Stabilization/SessionRebindingAndSaveSlotTests` | Rebinding across 3 restarts with one subscription each; rebinding to another session; slot names; default slot; independent slots; invalid slots; missing slot | 20 |
| EditMode, `Phase2/Phase2CompletionTests` | Daily order; route suspension (in transit, from the start, through the command); v3 → v4 migration; contributions match the formulas (1,080 combinations); settlement breakdown; shortage outlook; indicator rules; empty granary; active-trade marker; pausing; chronicle integrity over 180 days; 30-day timing | 19 |
| PlayMode, `SessionRestartPlayModeTests` | Map, selection and HUD rebind over 3 restarts; saving to the selected slot | 2 |
| PlayMode, `ExplorationPlayModeTests` | Restart while exploring; saving from exploration uses the selected slot | 2 |
| PlayMode, `Phase2HudPlayModeTests` | Inspector reasons and food trend; route toggle; map markers match the simulation for 15 days | 3 |

**Mutation checks:** three deliberate breaks, each caught by the new tests and then reverted:
- the binding's same-session guard removed;
- suspension ignored by trade;
- the shortage outlook inverted.

**A failure found and fixed between runs.** The first Unity PlayMode run had 36/37 passing.
`EconomyHudTests.Inspector_ListsTheSelectedSettlementsTradeRoutes` counted trade lines only
directly under the list, but each line now sits in a row with its Suspend/Resume toggle. The
test now searches at every depth and also checks one toggle per route. The second run: 37/37.

**Performance:** a 30-day run of the default world (3 settlements, 15 named residents, 6
routes) took 7.2–9.3 ms in the .NET harness. Nothing was measured in Unity or at larger scale.

## Phase 3: factions and political engine (2026-11-10)

| Run | Where | Result |
|---|---|---|
| Baseline, before changes | .NET harness | 174 passed, 0 failed |
| .NET harness | Windows, .NET 8, Release | **279 passed, 0 failed, 0 skipped** (174 earlier + 105 new cases) |
| Unity compilation | Unity 6000.0.47f1, batch mode | First run: 1 compiler error in a new test (NUnit in Unity has no `.And.Contain(object)`); fixed with `Has.Member`. Second run: **0 errors** |
| Unity EditMode | Unity 6000.0.47f1, batch mode (`-runTests -testPlatform EditMode`) | **279 passed, 0 failed, 0 skipped** |
| Unity PlayMode | Unity 6000.0.47f1, batch mode (`-runTests -testPlatform PlayMode`) | **42 passed, 0 failed, 0 skipped** (37 earlier + 5 new in `PoliticsPlayModeTests`) |

**Written for this milestone:**

| Suite | Cases | Covers |
|---|---|---|
| EditMode `Politics/PoliticalSimulationTests` | 48 | Profiles; influence terms, bounds, economic links; step limits; milestones; dues and upkeep; membership, clamping, affiliation reports; presence and rival pressure; every state threshold; relation causes; trust; goal progress and completion; goal-driven presence and claims; claim answers; decided claims left alone; status hysteresis; dormancy and dissolution; tick cadence; determinism across batching; atomic failed days; economy still daily |
| EditMode `Politics/PoliticalWorldTests` | 57 | 37 validator rejections; ID stability; generated structure; seed reproducibility and variation; no duplication on re-init or reload; v4 → v5 migration (exact match with a fresh world), and no factions invented for a world without any; exact save/load and identical continuation; dissolved factions surviving reload; every command refusing with the right status and no change; relation, claim, governance, presence and goal commands; influence explanation adds up; hardship eroding the crown's support; exploration changes nothing political; pause; a year with valid, non-spam events; performance |
| PlayMode `PoliticsPlayModeTests` | 5 | Faction list and inspector; settlement political section beside the economy; optional overlay, which follows governance and claims and keeps selection working; HUD buttons sending commands; the HUD following a simulated 70 days |
| Updated | 3 | The daily-order test now includes politics at 450; the world fingerprint covers political state; the exploration HUD test checks the governing faction |

**Mutation checks** (deliberate breaks, then reverted):

| Break | Caught by |
|---|---|
| Tick every day | 18 tests |
| Opinion moving twice as fast | 1 test |
| The governor's presence removable | 1 test |
| A dissolved faction keeping its presence | 1 test |
| The simulation overriding decided claims | 0 tests at first. I added `DecidedClaims_AreLeftAlone_WhateverRelationsDo`, which now catches it |

**Performance** (.NET harness, Windows, Release; nothing measured inside Unity):

| World | Measurement |
|---|---|
| Default (3 settlements, 5 factions, 10 relations) | 30-day run of all systems 6.3 ms, 84 KB allocated per day; one political tick 22 µs |
| Synthetic (30 settlements, 120 factions, 7,140 relations) | one political tick 54.5 ms; 30 days of politics, with the daily copy and validation, 304 ms |

**Not executed:**
- manual gameplay with the new UI;
- the F10 screenshot tour (it needs the Game view in the editor).

### Phase 3 follow-up: playtest fixes (2026-11-10)

**What the user reported:** "the graphics look horrendous", and cities could no longer be
entered or walked.

**What the new test found:** `RealScenePlayModeTests` loads the shipped `Realmkeeper.unity` and
sends a real mouse click through the Input System. With focus ignored, so that a headless run
receives device input, it showed:
- Clicking a city selects it, with or without the political overlay.
- Entering succeeds.
- **The character did not walk: 0.39 m in 1.5 s.** The run reached about 5,900 frames a second,
  so each step was shorter than `CharacterController.minMoveDistance` (0.001 m), which Unity
  silently ignores. Fixed by setting it to 0, as Unity recommends. Now 5.25 m in 1.5 s at about
  9,000 frames a second. The earlier exploration tests fixed frame time at 1/30 s, which hid this.

**Graphics changes:**
- The political overlay is now thin rings on the ground in the governor's colour, plus dashed
  rings for claims, instead of floating squares.
- Economic markers are 40% smaller.
- The faction list shows one line per faction with a colour swatch.
- The settlement Politics section sits below the economy as compact rows, with actions behind "Manage".
- Long faction names wrap in the inspector.

**Runs after the fixes:**

| Suite | Result |
|---|---|
| .NET harness | 279/279 |
| Unity EditMode (batch) | 279/279 |
| Unity PlayMode (batch) | 43/43 (+1: the real-scene test) |

**Not verified by eye yet:** the new look in the editor.

## Graphics pass (2026-11-10)

Environment: Windows 11, Unity 6000.0.47f1 (batch mode), .NET SDK 8, RTX 4070 Laptop GPU.

| Suite | Result |
|---|---|
| .NET harness (`dotnet test Tools/DotNet/Realmkeeper.Verification.sln -c Release`) | **297/297** (279 + 18 new: `BuildingKitTests` 11, `NpcAppearanceTests` 7) |
| Unity EditMode (batch) | **297/297** |
| Unity PlayMode (batch) | **48/48** (43 + `GraphicsPlayModeTests` 4 + `GraphicsCaptureTests` 1, run with `RK_CAPTURE_DIR` set) |

**Defects the new tests found** (all fixed before the final runs):
- **T-pose residents:** a body without a humanoid avatar (Female_Peasant) stood in a T-pose.
- **Render settings on exit:** the strategic fog and ambient were overwritten when leaving a town.
  The snapshot was taken after the exploration scene became active.
- **Teleport as running:** a teleport was read as running in the animation driver.

**Mutation checks** (.NET, reverted afterwards):
- Turning the front-door slot into a window fails `EveryHouse_HasAFrontDoor_AndTheTavernAnOpenDoorway`.
- Rounding footprints up fails `Plans_StayInsideTheirLayoutFootprint` and `Snap_RoundsToTheTwoMetreGrid`.

**Performance:** the editor capture and the development-build probe are in
`Documentation/Screenshots/GraphicsPass/*_stats.txt` and `devbuild-perf-*.txt`, summarised in
`Documentation/Reports/GraphicsPass-Report.md`. In a town: at most 955 draw calls, and a
1.2–1.7 ms mean frame at 1080p on the RTX 4070 Laptop GPU.

**Not verified:** a GTX 1660-class GPU (estimated only); the owner's own review of the look in the editor.

## Phase 4: NPC Intelligence Engine (2026-11-10)

Environment: Windows 11, Unity 6000.0.47f1 (batch mode), .NET SDK 8.

| Suite | Result |
|---|---|
| .NET harness | **323/323** (297 + `NpcEngineTests` 14, `NpcCommandTests` 9, `NpcPerformanceTests` 3) |
| Unity EditMode (batch) | **323/323** |
| Unity PlayMode (batch) | **50 passed, 0 failed**, 2 skipped (the evidence-capture fixtures, which run only with `RK_CAPTURE_DIR`) |

**Mutation checks** (reverted afterwards):
- Dialogue reading the world's truth instead of the person's knowledge: caught by
  `Answers_ComeFromWhatThePersonKnows_NotFromTheWorld`.
- Population reconciliation disabled: caught by
  `Tier3_IsTheRestOfThePopulation_AndNamedPeopleDieBeforeTheyCanBeCountedTwice`.

**Defects found by the new tests and fixed:**
- Re-telling news the listener already held, so new facts never spread.
- Needs crowded out by ambitions at the goal cap.
- Daily "goal achieved" spam (717 events a year).
- Execution-order clash with a test system.
- A full PlayMode run stalled. A capture fixture skipped inside `UnitySetUp`, and its teardown
  then destroyed the runner's scene objects.

**Performance** (60 simulated days, all systems):

| World | ms per day, .NET 8 | ms per day, Unity Mono |
|---|---|---|
| 15 people | 0.58 | 0.39 |
| ~100 people | 0.82 | 2.48 |
| ~300 people, 10 settlements | 10.4 | 14.6 |

Load time at 300 people was 0.3–0.46 s.

## Phase 5: Living World, Player Hub & Commerce (2026-11-10)

Environment: Windows 11, Unity 6000.0.47f1 (batch mode), .NET SDK 8.0.427. All results are at the
final Phase 5 code.

| Suite | Result |
|---|---|
| .NET harness (Release, 0 warnings) | **346/346** (323 + `CommerceTests` 20, `CommercePerformanceTests` 3) |
| Unity EditMode (batch) | **346/346** |
| Unity PlayMode (batch) | **57/57**, 0 failed, 0 skipped |

The Phase 5 capture fixture ran with `RK_CAPTURE_DIR` set, together with the Phase 4 and
graphics-pass captures.

**New PlayMode tests in the shipped scene** (`Phase5PlayModeTests`):
- Purchase at a shop counter: shelves equal stock; the keeper behind the counter; the trade window;
  coin, goods, stock and shelf; the chronicle; save and load.
- Player Hub: eight sections; equipping shows on the body and in the preview; underclothes when
  unequipped; the female frame rebuilds.
- Light and residents follow the clock: morning sun, night with lamps and moon, residents indoors
  asleep, a keeper walking from home to the counter.
- A shut shop: the keeper asleep, nobody standing in, and the window says why.

**Earlier tests updated to intended Phase 5 behaviour** (none weakened):
- Schema 7; the shop system in the daily order; old-save helpers that strip v7 fields and build
  sources pre-commerce.
- Hourly presence instead of daily; the hall plus house shops as enterable buildings.
- Three PlayMode tests now choose a resident who is outdoors, because residents indoors this hour
  cannot be approached.

**Mutation checks** (introduced, run, reverted):

| Mutant | Result |
|---|---|
| Currency duplication | Caught |
| Item duplication | Caught (the validator rejects the shared stack) |
| Invalid equipment (command check only) | Survived, because the validator independently rejects the world; caught when both layers were removed |
| Stock not decreasing | Caught |
| Partial transaction | Caught |
| Detached shopkeeper | Caught |
| Light not following the clock | Caught in PlayMode |
| Inventory not saved | Caught (the purchase test now checks the pack after reload) |

**Defects found and fixed during Phase 5:**
- Shop restocking took materials without booking them in the economy, caught by the 200-day
  conservation test.
- Generation stocked shops before the economy set the starting stores.
- The hourly plan could put a DM-moved keeper back at their counter.

**Performance, .NET** (`CommercePerformanceTests`; 30 simulated days, all systems including shops):

| World | Day | One hour of all whereabouts + shop availability | All prices |
|---|---|---|---|
| Default (15 people, 7 shops) | 0.44 ms | 0.021 ms | 0.04 ms |
| ~100 people, 7 shops | 2.54 ms | 0.138 ms | 0.68 ms |
| ~300 people, 10 settlements, 25 shops | 8.24 ms | 0.508 ms | 0.43 ms |

**Performance, development build** (Windows, RTX 4070 Laptop GPU, i9-14900HX, 1920×1080, kit art,
probe `-rkPerfProbe`):

| Pose | Mean frame | p95 | Draw calls |
|---|---|---|---|
| Map | 2.13 ms | 4.35 ms | 153 |
| Gate | 2.82 ms | 4.46 ms | 874 |
| Street | 2.77 ms | 4.78 ms | 693 |
| Building | 2.70 ms | 4.63 ms | 821 |
| Plaza | 2.65 ms | 4.90 ms | 477 |
| Tavern interior | 2.57 ms | 4.87 ms | 236 |
| Overview | 2.77 ms | 4.26 ms | 1150 |
| **Shop interior** | 2.77 ms | 4.87 ms | 871 |
| **Street, clock ×30** (08:11 → 09:23, residents re-planning and walking) | 3.90 ms | 7.46 ms | 703 |
| **Street, night** (16 lamps) | 2.74 ms | 4.78 ms | 649 |
| **Plaza, night** | 2.45 ms | 4.71 ms | 443 |

Memory: 545 MB system, 253 MB graphics.

**Not verified:** hardware other than this machine; rendered scenes with 100 or 300 residents
(only one settlement is rendered at a time; their simulation cost is above).

## Phase 6: Character Creation & Core RPG Foundation (2026-11-10)

Environment: Windows 11, Unity 6000.0.47f1 (batch mode), .NET SDK 8.0. All results are at the final
Phase 6 code.

| Suite | Result |
|---|---|
| .NET harness (Release) | **372/372**: 346 + `CharacterTests` (15 methods + 10 modifier cases) + `CharacterPerformanceTests` (1) |
| Unity EditMode (batch) | **372/372** |
| Unity EditMode, Character fixture (rerun after the final assertion was added) | **26/26** |
| Unity PlayMode (batch, `RK_CAPTURE_DIR` set, so every capture fixture ran) | **60/60**, 0 failed, 0 skipped |

**New PlayMode tests in the shipped scene:**
- `Phase6PlayModeTests.CharacterCreation_ThroughEveryStep_…`
  - Drives the real creator's UI elements through all nine steps: a dwarf Acolyte Cleric
    (Protector), by point buy.
  - Checks the body (look signature at dwarf height, the kit worn) and the Hub sheet's values.
  - Saves and reloads: nothing is duplicated, and creation is not offered again.
- `Phase6PlayModeTests.Phase5Save_LoadsAsALegacyCharacter_WithNothingLost`
  - A save made in the scene is rewritten to schema v7 without `rpg`, then loaded.
  - The player is Legacy, with the same ID, name, purse, hair, inventory, equipped dagger and pose,
    and is still in town.
- `Phase6CaptureTests.CapturePhase6Set`: 16 captures, in `Documentation/Screenshots/Phase6/`.

**Earlier tests updated to intended Phase 6 behaviour** (none weakened):
- Old-save helpers strip v8 fields (`StripToV7`).
- Migrated older saves are expected to be `Legacy`, while a fresh world is `Pending`. Everything
  else must still match.
- The schema version is read from `CurrentSchemaVersion`.

**Mutation checks** (introduced, run, reverted; the source was verified clean):

| Mutant | Result |
|---|---|
| Starting kit granted twice | Caught |
| Invalid skill accepted | Caught |
| Wrong ability modifier | Caught (3 cases) |
| Wrong hit points | Caught (2 tests) |
| Missing species validation | Caught |
| Choices lost on save | Caught (2 tests) |
| Identity replaced in migration | Caught (4 tests) |

**Defects found by reviewing the captures, and fixed:**
- The "worn" glyph was missing from the font.
- The skill bonus column was out of line.
- Chosen class and species options were missing from the Features panel.

**Performance** (`CharacterPerformanceTests`):

| Operation | .NET | Unity EditMode |
|---|---|---|
| Catalog validation, first use | 10.2 ms | 16.7 ms |
| Creation check | 21 µs | 43 µs |
| Full sheet | 20 µs | 82 µs |
| World save / load | 1.8 / 51 ms | 1.9 / 29 ms |

**Development build** (same machine, 1920×1080):
- **This run was vsync-capped**, so every pose reads 16.67 ms (60 fps). That includes the new
  "creator open (live preview)" pose (p95 17.33 ms, 792 draw calls) and "hub, character sheet"
  (p95 17.28 ms).
- These figures show 60 fps held. They are **not** comparable with Phase 5's uncapped frame times.
- Report: `Documentation/Screenshots/Phase6/devbuild-perf-phase6.txt`.

**Tested only in .NET and EditMode** (not driven through the UI):
- every species × class combination;
- every validator rejection;
- the draft logic;
- the speed, carry and price effects.

Only one creator path was driven in PlayMode.

## Phase 7: Character Progression & Core Rules Engine (2026-11-11)

Environment: Windows 11, Unity 6000.0.47f1 (batch mode), .NET SDK 8.0. All results are at the final
Phase 7 code.

| Suite | Result |
|---|---|
| .NET harness (Release) | **424/424**: 372 + `ProgressionTests` (51, counting test cases) + `ProgressionPerformanceTests` (1) |
| Unity EditMode (batch) | **424/424** (full suite), and the Character fixtures re-run after the last test edit: **78/78** |
| Unity PlayMode (batch, `RK_CAPTURE_DIR` set) | **67/67**, 0 failed, 0 skipped: 60 earlier + `Phase7PlayModeTests` (6) + `Phase7CaptureTests` (1) |

**New PlayMode tests in the shipped scene** (`Phase7PlayModeTests`). One per class: Fighter → 5,
Rogue → 6, Cleric → 3, Wizard → 4, Barbarian → 5, Warlock → 5. Each test:
1. Creates a character, saves it, **rewrites the save to the Phase 6 (v8) layout**, and loads it.
   The result is rules version 6, level 1, with one hit die from the migration.
2. Awards experience through `AwardExperienceCommand`. It checks there is no level-up before the
   award, and that experience alone does not level up.
3. Checks that the HUD's **Level up (L)** and the Hub's banner appear.
4. Clicks the Hub's level-up button and **goes through the real window for every level**:
   - Next through each page;
   - the picks made with the window's own buttons (subclass, feat and ability points, expertise,
     skills, invocations, including the warlock's open level-1 invocation);
   - Review, then Confirm.
5. Checks that the window closes onto the Hub's Progression panel, then checks the sheet: features,
   resources, scaling, subclass, roll modes, speed, and full hit points.
6. Saves and reloads: level, every choice, resources and advancement records equal. Levelling
   granted no items and no coin, no further level is offered, the same award is refused, and no
   feature is granted twice.

`Phase7CaptureTests` takes 13 captures (a Phase 6 tiefling warlock through levels 2–4); they are in
`Documentation/Screenshots/Phase7/`.

**Earlier tests updated to intended Phase 7 behaviour** (none weakened):
- the Phase 6 migration test now expects the current schema version, not 8;
- the validator's unknown-choice message gained "or premature level choice" (the existing assertion
  is unchanged).

**Mutation checks** (introduced, run, reverted; the source was verified clean):

| Mutant | Result |
|---|---|
| Wrong XP threshold (level 3 at 1,000) | Caught by `ExperienceThresholds_AreTheSrdTable` |
| Wrong proficiency bonus (`level / 4`) | Caught by 4 tests |
| Wrong hit-point increase (die / 2) | Caught by 7 tests |
| A level applied twice | **Survived at first**: the whole-sheet validator still refused it (wrong advancement-record count), but no test checked the rule itself. A test was added asserting "the next level is …"; the mutant is now caught by `ChoicesAreNeverRepeatedOrOverwritten`. |
| Replaced features still applying | Caught by `ClassMilestones_ScaleAndReplaceFeatures` |
| Ability cap not enforced | Caught by `AbilityScoreImprovement_RaisesScores_UpToTwenty` |
| Resource maximum off by one level | Caught by 4 tests |
| Feat choices lost on save | Caught by `AdvancedCharacter_SurvivesSaveAndLoadExactly` and `Progression_IsCheap` |
| The same XP award accepted twice | Caught by `Experience_IsAwardedThroughTheLedger…` |

**Performance** (`ProgressionPerformanceTests`, on a level-20 warlock):

| Operation | .NET | Unity EditMode |
|---|---|---|
| Rules evaluation | 57 µs | 52 µs |
| Full sheet (skills, roll modes, saves, HP, AC, speed, initiative, passives, features, resources) | 158 µs | 396 µs |
| Sheet validation, all 20 levels' choices | 0.70 ms | 1.16 ms |
| Suggestion and preview of a choice-heavy level (warlock 4 → 5) | 0.25 ms | 0.43 ms |
| XP award command (with whole-world validation) | 0.19 ms | 0.41 ms |
| Level-up command (with whole-world validation) | 0.86 ms | 1.10 ms |
| World save / load with a level-20 character | 3.0 / 60 ms | 3.8 / 138 ms |

**Not measured.** A development-build frame-time probe of the level-up window was not re-run. The
window is UI Toolkit only (no 3D preview), and Phase 6 measured the comparable creator window at a
held 60 fps.

**Tested only in .NET and EditMode** (not driven through the UI):
- the other six classes' level-ups, all 12 classes to level 20, and the rules milestones;
- feats' edge cases, every validator rejection, resources and rests, damage and healing;
- non-player sheets.

## Phase 8: Combat Foundation, Encounter Engine & Settlement Structures (2026-11-11)

| Suite | Environment | Result |
|---|---|---|
| .NET harness (`dotnet test Tools/DotNet/Realmkeeper.Verification.sln -c Release`) | Windows 11, .NET 8 | **470 passed, 0 failed, 0 skipped** (424 before Phase 8 + 46 new); build 0 warnings, 0 errors |
| Unity EditMode (batch, `-runTests -testPlatform EditMode`) | Unity 6000.0.47f1 | **470 passed, 0 failed, 0 skipped** (171 s) |
| Unity PlayMode (batch, `-runTests -testPlatform PlayMode`) | Unity 6000.0.47f1 | **66 passed, 0 failed, 6 skipped** (48 s). The six skipped are the capture sets, which run only with `RK_CAPTURE_DIR` |
| Phase 8 capture (`Phase8CaptureTests`, `RK_CAPTURE_DIR` set) | Unity 6000.0.47f1 | 1 passed; 10 screenshots in `Documentation/Screenshots/Phase8/` |

**New .NET / EditMode tests (46):**

| Fixture | Tests | Covers |
|---|---|---|
| `CombatTests` | 28 | SRD armour classes, the Strength requirement, untrained armour and shields, item profiles; encounter refusals (duplicates, no hostility, a second fight); initiative order and tie-breaks; reinforcements; the action economy, Dash, Disengage, Dodge, Second Wind; movement costs, difficult terrain, blocking, opportunity attacks; prone, grappled; the d20 rules over 400 attacks (natural 1 and 20, criticals), advantage, disadvantage and cancelling; range and reach; grapple and shove; resistance and temporary HP; creature death; the player dying, death saves over 16 seeds, massive damage, non-lethal knock-outs, stabilizing at the end; healing and Stabilize; the 15 conditions and their mechanics; timed conditions; the AI (valid, deterministic, never against allies, finishes a fight; retreats when hurt); the dice stream reproducible |
| `CombatIntegrationTests` | 12 | The practice encounter with the town's guard; time standing still; stale and duplicate actions refused; a fight played to its end with few chronicle entries; a resident killed through the shared death routine; a dead player; non-lethal fights; an active fight saved, loaded and played on identically; finished encounters bounded; v9, v8 and v7 saves migrating; tampered saves refused |
| `StructureTests` | 5 | Catalog classification; every place one structure and every shop housed once; distribution by size; determinism and once-only; ordering |
| `CombatPerformanceTests` | 1 | Benchmarks below |

**Earlier tests updated to intended Phase 8 behaviour** (none weakened, none removed):
- `ProgressionTests`: the armour-class assertion follows the SRD chain shirt (13 + Dex, max 2) instead of the provisional protection value. The schema assertion uses `CurrentSchemaVersion` instead of 9.
- `Phase2Worlds.StripToV7` and `ProgressionTests.StripToV8` first strip the v10 fields (`StripToV9`), so old-save fixtures stay faithful.
- `WorldFingerprint` now covers structures and encounters.

**New PlayMode tests in the shipped scene** (`Phase8PlayModeTests`, all passed):
1. `PracticeFight_PlaysThroughThePanel_ToItsEnd`:
   - the HUD button starts the fight;
   - the panel, grid and markers appear;
   - the clock and leaving are refused;
   - a far attack shows its reason on screen;
   - inspecting a bandit shows AC 12;
   - the whole fight is played by clicking attacks, grid squares, End turn and Continue;
   - the end state, the log and the chronicle are checked;
   - Close frees the character and the clock.
2. `AFightInProgress_SurvivesSaveAndLoad_AndThePanelReturns`.
3. `Withdraw_IsRefusedNextToAnEnemy_AndEndsTheFightOtherwise`.
4. `Structures_AreListedInTheInspector_SignsStandInTheStreet_AndTheHubShowsTheCredits`.

**Mutation checks** (introduced, run against the full .NET suite, reverted; the source was verified clean afterwards):

| Mutant | Result |
|---|---|
| A natural 1 no longer misses automatically | **Survived at first**: a natural 1 never reached AC 12 anyway. The attack test now also asserts the stated reason ("a natural 1 always misses"); the mutant is now caught |
| Critical hits no longer double the dice | Caught |
| Resistance no longer halves | Caught |
| Opportunity attacks never provoked | Caught (the first form was rejected by the compiler, a second form was caught by the test) |
| Difficult terrain costs normal movement | Caught |
| Initiative ordered lowest first | Caught |
| Damage at 0 HP from a critical counts one failure | Caught |
| Massive damage never kills | Caught |
| The shield ignored in AC | Caught by 2 tests |
| The validator allows two active encounters | Caught |
| The action sequence not checked | Caught |
| The AI never retreats | Caught |
| Structures ignore the largest settlement size | Caught |

**Bug found by the new tests and fixed:** in a non-lethal fight, a large hit on someone already knocked out could still kill them. Damage at 0 hit points now never kills in a non-lethal fight.

**Performance** (`CombatPerformanceTests`):

| Operation | .NET | Unity EditMode |
|---|---|---|
| Encounter snapshot for the UI | 0.08 ms | 0.19 ms |
| A turn command (validated commit), practice fight | 0.3 ms | 0.6 ms |
| Large encounter: 40 combatants, AI-only, to the end | 7 ms for 495 actions over 8 rounds (13 µs per action) | 11 ms (22 µs per action) |
| World save / load with an active encounter | 2.7 / 10.4 ms | 2.4 / 49.6 ms |
| Structure generation, world generation included | 18.8 ms | 57.7 ms |

**Not measured:** a development-build frame-time probe with the encounter panel open. The panel is UI Toolkit, and the markers add a few primitives per combatant.

**After the PlayMode run:** the marker labels were made smaller, and the player's own label was removed (they overlapped in the first capture). This is a presentation-only change. The capture set was re-run after it and passed; the PlayMode suite was not re-run.

## Phase 9: Spellcasting, Class Features & Combat Integration (2026-11-11)

| Suite | Environment | Result |
|---|---|---|
| .NET harness (`dotnet test Tools/DotNet/Realmkeeper.Verification.sln -c Release`) | Windows 11, .NET 8.0.427, i9-14900HX | **520 passed, 0 failed, 0 skipped** (470 before Phase 9 + 50 new) |
| Unity EditMode (batch, `-runTests -testPlatform EditMode`) | Unity 6000.0.47f1 | **520 passed, 0 failed, 0 skipped** (100 s); 0 compiler errors |
| Unity PlayMode (batch, `-runTests -testPlatform PlayMode`) | Unity 6000.0.47f1 | **70 passed, 0 failed, 10 skipped** (33 s). The ten skipped are the capture sets, which run only with `RK_CAPTURE_DIR` |
| Phase 9 capture (`Phase9CaptureTests`, `RK_CAPTURE_DIR` set) | Unity 6000.0.47f1 | 4 passed; 15 screenshots in `Documentation/Screenshots/Phase9/` |

**Run order.** The final .NET, EditMode and PlayMode runs above came after the last code change.
- **The capture set** ran before the last test-only change (the cylinder assertions in
  `SpellTests`), with the same runtime code.
- **One hung batch run.** A PlayMode run was found waiting on a build backend that no longer
  existed; it was stopped and run again. The figures above come from that clean run.

**New tests (50):**

| Fixture | Count | Covers |
|---|---|---|
| `SpellTests` | 16 | Catalog validity and honest status; area shapes on the grid (sphere, cone, line, cube, cylinder, cube at a point); spell attacks and saves through the shared rolls; areas with separate saves; upcasting; Magic Missile; healing; Bless and concentration replacement; concentration checks; Hold Person repeat saves; failed casts spend nothing; Pact Magic; Eldritch Blast beams; Misty Step; NPC casters |
| `FeatureTests` | 13 | Rage, Reckless Attack, Sneak Attack, Action Surge, Second Wind and Cunning Action through the shared path, Smite, Lay on Hands and Bardic Inspiration, Wild Shape, Flurry of Blows and Stunning Strike, Font of Magic and Metamagic, Agonizing Blast, Vex, deferred features not offered |
| `SpellProgressionTests` | 20 (12 tests + 8 cases) | Picks for each casting class; level-up and later picks; pick-level limits; preparation rules and the long-rest window; short rest, long rest, refusals and Arcane Recovery; healing outside a fight; a fight with concentration and effects saved and loaded; v10 → v11 migration; the caster AI; a resident as a caster |
| `MagicPerformanceTests` | 1 | Benchmarks in the Phase 9 report, section J |

**Earlier tests updated to intended Phase 9 behaviour** (none weakened or removed):
- `ProgressionTests`: all hit dice return on a long rest (SRD 5.2.1), with the reason stated.
- `CombatIntegrationTests`: the schema assertion uses `CurrentSchemaVersion`.
- `Phase2Worlds`: `StripToV10` was added and is called from `StripToV9`, so old-save fixtures stay
  faithful.

**New PlayMode tests in the shipped scene** (`Phase9PlayModeTests`, all passed):
1. `Spellbook_FiltersInspectsAndPrepares`.
2. `Combat_CastsASpellAttack_AndAnAreaSpell_ThroughTheComposer`.
3. `Combat_ActivatesAClassFeature_FromThePanel`.
4. `Rests_FromTheHub_RecoverAndShowResources_AndStateSurvivesReload`.

**A test failure during development.** Test 2 first failed: the level-1 wizard was knocked out by
the bandit archer's opening shot before its first turn, which is legitimate dice behaviour. The
driver now calls such a fight off, recovers the character in game (a hit die, a day, a long rest)
and starts again. The dice are not touched.

**Mutation checks:** 12 mutants. Each was introduced, run against the full .NET suite and reverted;
all were caught.

| Mutant | Result |
|---|---|
| Slot not spent | Caught |
| Sneak Attack not limited to once per turn | Caught |
| Rage resistance removed | Caught |
| Long-rest 16-hour gap removed | Caught |
| Short-rest regain removed | Caught |
| AI heal threshold changed | Caught |
| Half damage on a successful save | **Survived at first.** Caught after a deterministic raw-damage check that loops to a success |
| Concentration DC floor of 10 | **Survived at first.** Caught after asserting "vs DC 10" |
| Upcast dice | **Survived at first.** Caught by the new `Upcasting_AddsTheSpellsOwnDice` |
| One levelled slot per turn | **Survived at first.** Caught by the Misty Step assertion |
| Preparation limit | **Survived at first.** Caught after asserting the "At most 4" message |
| Pact slots usable at other levels | **Survived at first.** Caught after asserting `SlotsLeft(1) == 0` |

**Presentation bugs found by the captures and fixed** (re-tested and re-captured):
- the Hub's spell stats row wrapped its values;
- the aim hint was clipped;
- the Spells section was pushed out of view (panel sections now fold and stay folded);
- concentration and effects were missing from the initiative rows and the inspector.

**Not run:** a development-build frame-time probe, and manual play beyond the automated captures.

## Phase 10 runs (2026-11-11)

| Suite | Environment | Result |
|---|---|---|
| .NET | Windows 11 Pro, .NET 8 SDK, Release, warnings as errors | **552 passed**, 0 failed, 0 skipped; 0 warnings |
| Unity EditMode | Unity 6000.0.47f1, batch mode | **552 passed**, 0 failed, 0 skipped; no compiler warnings |
| Unity PlayMode | Unity 6000.0.47f1, batch mode | **74 passed**, 0 failed, 0 inconclusive, 14 skipped (capture sets without `RK_CAPTURE_DIR`) |
| Phase 10 captures | Unity 6000.0.47f1, editor (not batch), `RK_CAPTURE_DIR` set | **4 passed**: 15 screenshots and the scene performance log |

Mutants, new tests, the existing tests that changed (and why), and bugs found: `Documentation/Phase10/Testing.md`.

## Phase 16 runs (2026-11-12)

| Suite | Environment | Result |
|---|---|---|
| .NET | Windows 11 Pro, .NET 8 SDK, Release | **750 passed**, 0 failed, 0 skipped (36 of them Phase 16) |
| Unity EditMode (full) | Unity 6000.0.47f1, batch mode | **749 passed, 1 failed** (a 180 s timeout in `Phase14CrimeTests.AHousehold_WarnsOnce_ThenShowsYouOut_AndThatIsTrespass`, which took 189 s; it was already 143–167 s under Mono in Phases 14 and 15) |
| Unity EditMode (`Phase14CrimeTests` after giving that scan an explicit 10-minute timeout) | same | **8 passed**, 0 failed |
| Unity PlayMode (full) | Unity 6000.0.47f1, batch mode | **111 passed**, 0 failed, 0 inconclusive, 28 skipped (capture sets without `RK_CAPTURE_DIR`) |
| Phase 16 captures | Unity editor (not batch), `RK_CAPTURE_DIR` set | **1 passed**: 15 screenshots (`Screenshots/Phase16/`) |

The full EditMode suite was not rerun after the one-line timeout change. The regressions found and fixed, and an intermittent .NET harness abort that did not reproduce, are described in `Reports/Phase16-Completion-Report.md` §7 and §17.

## Phase 17 runs (2026-11-12)

| Suite | Environment | Result |
|---|---|---|
| .NET (baseline, `7ab5163`) | Windows 11 Pro, .NET 8 SDK, Release | **750 passed**, 0 failed, 0 skipped |
| Unity EditMode (full, baseline `7ab5163`) | Unity 6000.0.47f1, batch mode | **750 passed**, 0 failed, 0 skipped (31 min). This closes the Phase 16 gap: the Phase 14 save/reload test took 189 s, within its 10-minute limit |
| .NET (final) | same | **791 passed**, 0 failed, 0 skipped (41 new) |
| Unity EditMode (full, final) | Unity 6000.0.47f1, batch mode | **791 passed**, 0 failed, 0 skipped (34 min) |
| Unity PlayMode (full, final) | Unity 6000.0.47f1, batch mode | **115 passed**, 0 failed, 0 inconclusive, 29 skipped (capture sets without `RK_CAPTURE_DIR`) |
| Phase 17 capture | Unity editor (not batch), `RK_CAPTURE_DIR` set | **1 passed**: 19 screenshots (`Screenshots/Phase17/`) |

- **New tests.**
  - EditMode: `Campaigns/Phase17CampaignTests`, `Phase17DecisionTests`, `Phase17StoryTests` and `Phase17PerformanceTests`, plus `Atlas/Phase17AtlasTests`.
  - PlayMode: `Phase17PlayModeTests` (road fight through the panel, the atlas checklist, the campaign journal, a decision in conversation) and `Phase17CaptureTests`.
- **Existing tests changed, and why.**
  - `Phase2CompletionTests.DailyOrder…`: the campaign pass joined the documented daily order.
  - `Phase2Worlds`: gained a v18 strip step, so old-save chains rebuild the v18 layout.
  - `Phase16AtlasPlayModeTests.EveryAtlasControl…`: the road fight now opens in the combat scene, and the outlaws act. Withdrawing is refused while one stands next to the player (an existing rule), so the test uses the atlas's Withdraw, then the labelled "Let it play out".
- **Intermittent.** In one PlayMode run, a Unity engine log (a 160-byte temporary-allocator leak report while entering an ordinary town) failed the tests that followed it. It did not recur in the next runs.
- **Not run:** a development-build frame-time probe; a hand-played session; video recording.

## Phase 18 runs (2026-11-12)

| Run | Passed | Failed | Skipped | Inconclusive | Timed out | Notes |
|---|---|---|---|---|---|---|
| .NET, Release, all tests (final) | 830 | 0 | 0 | — | 0 | 3 m 33 s; includes the 39 Phase 18 tests |
| Unity EditMode, full | 827 | 0 | 0 | 0 | 0 | 39.6 min; run before the last fixes, which added 3 tests that ran in .NET only |
| Unity PlayMode, full | 113 | 1 | 30 | 2 | 0 | Run before the last fixes |
| Unity PlayMode, targeted (final) | 8 | 0 | 0 | 0 | 0 | Phase 18 capture, Phase 18 UI test, Phase 15 art tests |

Notes on the full PlayMode run:
- **The failure.** `Phase15ArtPlayModeTests.WithoutTheLocalBestiary_BodiesFallBack_WithoutErrors` expected goblins and wolves to have no model. They now have repository models (ADR-0097). The test was updated and passes in the targeted run.
- **The skipped tests** are the capture sets, which need `RK_CAPTURE_DIR`.
- **The inconclusive tests** are dice-dependent `Assume` guards in Phase 9 and Phase 11.

At the owner's direction, the full Unity suites were not rerun after the last fixes.

## Phase 19 runs (2026-11-12)

| Run | Passed | Failed | Skipped | Inconclusive | Timed out | Notes |
|---|---|---|---|---|---|---|
| .NET, Release, all tests (final) | 834 | 0 | 0 | — | 0 | Includes 3 save-list tests and 1 credits test |
| Unity PlayMode, targeted (Phase 19 UI tests, capture, session restart) | 7 | 1 | 0 | 0 | 0 | The failure: the hub test needed the real scene; moved to `Phase19ScenePlayModeTests`, which has not been run since |
| Unity EditMode, full | — | — | — | — | — | Running when the owner asked for commit and push; results to follow |
| Unity PlayMode, full | — | — | — | — | — | Queued after EditMode |
