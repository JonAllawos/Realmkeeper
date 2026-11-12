# Phase 16 final verification (done in Phase 17)

Date: 2026-11-12. Baseline: `7ab5163` on `feature/phase-0-1-foundation`, level with origin.

## 1. The outstanding item: the full EditMode suite

The Phase 16 report left one gap. A Phase 14 save/reload test (`Phase14CrimeTests.AHousehold_WarnsOnce`) took 189 s against its old 180 s limit. Its limit was raised to 10 minutes and its fixture rerun (8/8), but the full suite was not rerun.

**Closed.** The full Unity EditMode suite was run on the unchanged `7ab5163`, before any Phase 17 change:

| Suite | Passed | Failed | Skipped | Time |
|---|---|---|---|---|
| Unity EditMode (full, batch mode, Unity 6000.0.47f1) | **750** | 0 | 0 | 31 min |

- **The Phase 14 test.** It passed in 189 s again, so its run time is unchanged; only its limit changed. Its assertions were not touched. It remains the slowest test (the next slowest is 47 s).
- **No other test was over 50 s,** and nothing else timed out.

The other baselines on `7ab5163`:

| Suite | Result | Phase 16 report |
|---|---|---|
| .NET | **750 passed**, 0 failed, 0 skipped | 750 passed |
| Unity EditMode | **750 passed** | 749 passed, 1 timeout |
| Unity PlayMode | Run with Phase 17's changes (see the Phase 17 report): the Phase 16 PlayMode tests pass, one of them updated for the new fight scene | 111 passed, 28 skipped |

## 2. The atlas and travel checklist

The brief asks for an actual gameplay session. These checks were made **through the shipped scene and its real UI, driven by automated PlayMode tests** (button clicks, the dialogue and combat panels, saving and loading through the runtime). A person did not play them by hand. That distinction is kept throughout.

| Check | How verified | Result |
|---|---|---|
| Open and close the atlas | PlayMode `TheAtlasChecklist…` (toggle) and Phase 16 `EveryAtlasControl…` (nav button, Close) | Pass |
| All six regions inspected | PlayMode `TheAtlasChecklist…` clicks every region mark; the inspector names each (uncharted lands stay "Uncharted lands") | Pass |
| Known and unknown information | EditMode `Phase17AtlasTests.GroupsBeyondSight…` (unknown groups not shown; reported ones as reported); Phase 16 atlas tests (uncharted regions) | Pass |
| All eight layers | PlayMode `TheAtlasChecklist…` turns each off and on | Pass |
| Select a destination; best route and alternatives; compare | PlayMode `TheAtlasChecklist…`: ways sorted, all walkable, one button each | Pass |
| Begin, halt, resume | PlayMode `TheAtlasChecklist…`, `EveryAtlasControl…` | Pass |
| Turn back | PlayMode `TheAtlasChecklist…`: a new journey from the road back to the origin | Pass |
| Abandon | PlayMode `EveryAtlasControl…` | Pass |
| Save and reload during travel | PlayMode `TheAtlasChecklist…` (runtime save, reload, the same journey, percent and party); EditMode `Phase17AtlasTests` (saved mid-fight on the road) | Pass |
| Arrive; the party arrives together | PlayMode `TheAtlasChecklist…` (entered on arrival, both companions' bodies there); Phase 16 tests | Pass |
| Companions' conditions and resources intact | Phase 16 EditMode `ThePartyTravelsTogether_ReservesStayHome_NobodyIsDuplicated…` | Pass |
| Reserve companions stay where they are; wait spots persist | Phase 16 EditMode (`ThePartyTravelsTogether_ReservesStayHome…`, `AMemberToldToWait_KeepsTheSpotTheyStoodOn…`) | Pass |
| Regional discovery correct | Phase 16 EditMode (`ANewGame_IsSixConnectedRegions…`, `AcrossTheBorder_ThePartyEntersTheNextRegion…`) | Pass |
| Groups and caravans represented correctly | EditMode `Phase17AtlasTests.GroupsBeyondSight…` (seen where they are in the player's region; reported elsewhere, at the report's place and time) | Pass |
| Events and Chronicle not repeated | Phase 16 EditMode (`EventsExpire_AndTheSameCause_NeverOpensTwo`, `TheChronicle_StaysInOrder_AndBounded`); Phase 17 (a delayed consequence raises its event once across reloads) | Pass |
| Closed roads and bridges give no unusable route | Phase 16 EditMode (`ABrokenBridge_ClosesTheWay_PlansGoAround…`); PlayMode `TheAtlasChecklist…` (every offered way walkable) | Pass |
| Recovery when a road closes underneath | Phase 16 EditMode `ARoadThatClosesUnderTheTraveller_LeavesTheWayBack_AndNoWayThrough` | Pass |

**Movement rules preserved.**
- Outside combat, local exploration is unchanged: free WASD, no grid, no movement budget, no slowdown.
- In combat, the tactical grid, movement budget, difficult terrain, action economy and opportunity attacks are the existing ones. The road fight uses the same panel.
- Strategic travel stays a separate world action.

## 3. Improvements made in Phase 17

- **The atlas fight transition (ADR-0086).** A fight met on the road now opens in the combat scene, staged on the ground at the road's nearer end, and is played with the existing tactical panel. The party stays on the road and leaves the ground when the player says; travel resumes only when asked.
  - Tested: EditMode `Phase17AtlasTests` (the fight, save and reload mid-fight, leaving, resuming, withdrawing) and PlayMode `AFightOnTheRoad_IsFoughtInTheCombatScene_AndThePartyReturnsToTheRoad` (through the panel).
  - Remaining limit: the ground shown is the near settlement's land, not a generated stretch of road.
- **Groups beyond sight.** They show where a report puts them, as reported.
- **Atlas PlayMode test updated.** `EveryAtlasControl…` withdrew from a road fight with the atlas button. That relied on the old behaviour: no fight scene, and the outlaws not acting. It now withdraws through the combat panel on the player's turn, then closes it and returns to the road.

## 4. Not done

- **No gameplay recordings.** The batch-mode test runner cannot record video, and no recording tool is set up in this environment. Evidence is captured screenshots with captions (Phase 17 report).
- **No hand-played session** of the checklist; automated UI-driven tests stand in for it, as stated above.
