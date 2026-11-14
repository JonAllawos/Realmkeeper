# Phase 26.1 completion report

**Phase:** Post-Play Verification, Regression Closure & Compact Local HUD Map.

**Date:** 2026-11-14.

**Baseline:** `a46a90f`.

**Commits:** listed in §3. Local only; **nothing was pushed.**

**Status words used throughout:**
- **Complete:** built and verified by automated tests in the running game.
- **Partial:** part of what was asked is done.
- **Deferred:** not done this phase.
- **Unverified:** not checked by a person.

## 1. Executive summary

| Area | Status |
|---|---|
| Full Unity regression checkpoint | **Complete.** EditMode 954/954 (baseline); PlayMode 139 passed, 0 failed, 36 capture-only skips (after the map was built) |
| The four post-play fixes | **Verified in the running game** by new automated checks. Not hand-played |
| Local HUD map | **Complete** (heading-up, bottom right, marks from existing records only, labels decluttered, responsive, hidden behind windows). Marker selection deferred |
| Defects found this phase | Two in the new map, both found by its own tests and captures, and fixed (§7) |
| Performance | **Editor only** (batch mode). No development or player build was made |
| Hand-play | **Not done by Claude.** Owner checklist: 20 checks |

## 2. Preflight findings

Details are in `Systems/Phase26.1-Preflight-Audit.md`.

- **Git history.** The brief lists `3f8ded4` with the post-play fixes as local. In fact it is a documentation commit already on origin. The local-only commits were `ba3d333` (the fixes), `a899850` and `a46a90f` (report updates).
- **Last full Unity checkpoint:** Phase 25. Phase 26 and the post-play fixes had been tested only with .NET and targeted PlayMode.
- **HUD.** Both bottom corners were free during exploration. Combat uses both, but the HUD steps aside for combat.
- **Data.** Everything the map needs already exists (§12). No new tracking was added.

## 3. Git state and commit history

| | |
|---|---|
| **Branch** | `feature/phase-0-1-foundation` |
| **Remote** | `origin/feature/phase-0-1-foundation` at `3f8ded4` |
| **Commits this phase** | Code and tests, then documentation and screenshots; hashes in the closing message |
| **Ahead / behind** | 5 ahead (the three post-play commits and this phase's two), 0 behind |
| **Left out** | The two untracked `Assets/InitTestScene*` scenes (preserved). `M_LampGlow.mat`, which batch runs re-save (restored after every run) |
| **Push** | None. The owner has not authorised one |

## 4. .NET full-suite results

**Final run (after all changes):** 960 passed, 0 failed, 0 skipped (960 total), 5 min 22 s.
- That is 954 plus the 6 in `Phase26_1LocalMapTests`.

**Baseline run (alone, before the map):** 954/954, 5 min 39 s.

**An unexplained first run.** A background run at the start, chained with the EditMode run, reported **102 passed of 102** in 2 min 41 s. I could not establish why it saw only 102 tests (the output was summarised, not kept). The next run, alone and with full output, gave 954/954. **The 102 is not counted as a result.**

## 5. Unity EditMode full-suite results

**Full suite** (batch mode, Unity 6000.0.47f1, before the map code): **954 passed, 0 failed, 0 skipped (954 total)**, 2709 s.
- This closes the Phase 26 gap: it covers Phase 26 and the post-play fixes.

**After the map:**
- A focused EditMode run of `Phase26_1LocalMapTests`, `SettlementFenceAccessTests` and `SettlementLayoutTests`: 21/21.
- The map added only pure Application logic and its own tests, both also covered by the final .NET run.
- **Not re-run in full**, under the revised testing policy (isolated change, covered by focused tests).

## 6. Unity PlayMode full-suite results

**Full suite** (batch mode, after the map was built, so one run covers the baseline and the new HUD element): **139 passed, 0 failed, 36 skipped (175 total)**, 957 s.
- **The 36 skips** are the capture-only tests, which run only when `RK_CAPTURE_DIR` is set: Phase 25's 34, plus the Phase 26 and Phase 26.1 capture tests.

**`Phase26_1PlayModeTests` (4 tests)** passed inside the full run. Before it, several targeted runs were needed to make the tests right (§7).

**`Phase26_1CaptureTests`** was run on its own with capture on: passed, 22 captures.

## 7. Analysis of all failures

| Failure (targeted runs) | Classification | Resolution |
|---|---|---|
| Compile: the controller file's header duplicated mid-file | My editing error | Removed |
| Compile: `OverlandSiteKind` namespace; an unassigned `merchant`; `ScriptedExplorationInput`, `ResponseKind` namespaces | My errors | `using` lines added; variable initialised |
| Roads test found no road meshes | **Test defect.** The static geometry is merged, so a road's own mesh no longer exists | The builder's road surface was made a public function (`ModularEnvironmentBuilder.RoadStrip`), and the test checks exactly that against the terrain collider |
| Typing and number-key tests received no key presses | **Environment.** Headless runs have no focused game view | The tests set `editorInputBehaviorInPlayMode` to all-devices, as the existing real-scene tests do |
| Typing test: Q appeared not to turn the map | **Test defect.** The camera eases toward its target over real time, and batch frames are about 1 ms | The test compares the camera's target heading. A control was added (without a field, W and Q do move and turn it) |
| Map test: a 120° turn not settled in 40 frames | **Test defect** (frames, not time) | Measured in seconds: about 0.19 s |
| Map test: a mark not removed after its person left | **Test defect.** Movers refresh every 0.4 s of real time | The test waits real time |
| Roads test: 4 vertices about 25 cm off the ground | **Test defect.** They were bridge-deck vertices, which are level by design; the test's deck rule differed from the builder's | The test uses the builder's rule |
| Map test: **the map showed through the hub** | **Real defect (new code).** The map's visibility was set inline, overriding the stylesheet's hide-behind-windows rule | Fixed: shown and hidden with a class |
| Captures: **no map, black town** | **Real defect (new code)**, plus a test defect. The exploration HUD's tree (and the camera rig's camera) are rebuilt after the first entry, so the map was left on a dead tree | Fixed: the map moves itself to the live tree. The capture test shoots through the live camera. Both tests now assert the map is on the live panel |
| Feet test: 30 cm | **Test defect.** Measured 20 ms after dropping the player from 0.5 m | Waits 1.5 s: −1.1 cm (the capsule's skin counted) |

**No existing test was changed. No assertion was weakened to pass.** Each correction above made a new test measure what it claims to.

## 8. Verification of the four post-play fixes

All checks below are automated, in the running game (`Phase26_1PlayModeTests`), on a newly generated world.

| Fix | Check | Result |
|---|---|---|
| **Roads follow the walked ground** | Every third vertex of every road, as the builder makes it, against the terrain collider beneath (raycast) | 3,107 vertices on 17 roads, 2,112 of them on sloping ground (steepest 1.01 m per m): **every one exactly 3.0 cm above** the collider. Between vertices, within **2.9 cm** |
| | The player standing on the steepest road point under 0.5 m per m | Feet **−1.1 cm** from the paving (the capsule's skin counted) |
| **Fences and gates** | For every fenced plot: a player-sized capsule (radius 0.3 m, 1.1 m tall) cast through the gate, and through the back fence | 6 fenced plots of 7 fields: **passed every gate (6/6)**; **stopped by every back fence (6/6)** |
| | `SettlementFenceAccessTests` (18 settlements, six worlds) | The arrival is never inside a fenced plot; no road runs through a fence; every gate can be walked to from the arrival |
| **Typing** | A real keyboard device: a focused text field; W held for 200 frames and Q pressed | The strategic camera **did not move or turn**. With no field focused, the same keys **moved and turned it** |
| **Numbers** | A street conversation, real key events | **Key 1 opened "Talk…"** (button 1; before the fix it asked the topic below). In the conversation, **keypad 1 chose response 1** |

**Bridges** (found while verifying roads; not changed). Bridge decks are level by design. Within a bridge's reach, deck vertices stand up to **92 cm** above the ground beneath them, where the bank falls away under the deck. **Whether the road meets the deck with a visible step was not checked**, by eye or by test. It is listed as a limitation (§19).

**Not covered by automation:**
- every combination of terrain transition and road junction (the vertex check covers all roads in the test town, including junctions);
- old saves: their fences are unchanged by design (§19);
- by hand, nothing.

## 9. Local HUD map implementation

Details are in `Systems/Phase26.1-Local-HUD-Map.md` and ADR-0118.

**Code:**
- `LocalMapQueries.cs` (Application, pure): projection, edge pins, smoothing, decluttering, shortening; the naming rule `WorldSession.MapLabelFor`.
- `LocalHudMap.cs` (UI Toolkit):
  - the ground is drawn once per area into a canvas, moved and turned by its style;
  - marks are Painter2D shapes in one layer;
  - labels are a pool of five.
- `ExplorationController.LocalMap.cs`: feeds it from existing records.

**Placement:** bottom right. 184, 158 or 136 px by width class; the bottom stack keeps clear of it.

**Interaction:** clicking the map switches between two ranges. **Marker selection is deferred** (§19).

## 10. Marker categories and colours

**Every kind has its own shape; colour only helps.**

| Mark | Look |
|---|---|
| Player | Parchment arrowhead with a faint cone |
| Companion | Green ringed dot |
| Person | Teal dot |
| Merchant (a person) | Amber dot |
| Shop | Amber square, at its door |
| Service | Blue square |
| Town landmark | Parchment diamond |
| Gate | Parchment bar (edge pin) |
| Settlement | Large blue diamond (edge pin) |
| Place, way out | Parchment ring |
| Found landmark | Violet diamond |
| Waypoint | Gold downward triangle (edge pin) |
| Traveller | Teal ring |
| Outlaws | Red upward triangle |

## 11. Coordinate and orientation behaviour

**Heading-up.** Right = dx·cos h − dz·sin h; forward = dx·sin h + dz·cos h. The ground turns by −heading about the player.

**Verified** (.NET and the live map):
- the player is the centre;
- facing north, east, south and west, each direction falls where expected;
- facing a diagonal, the point ahead is straight up;
- distances are kept under any heading (200 random cases);
- turning in place turns marks the other way by the same angle;
- walking 10 m moves a mark ahead 10 m down;
- the live marks equal the geometry at headings 0, 90, 180, 270 and 45, and the ground's rotation equals −heading;
- after a 12 m move, marks follow.

**Smoothing.** A 120° turn settles within 1° in 0.19 s; exact with Reduce motion.

**Region transitions.** Entering the country rebuilds the map for the region (verified: known sites only). Crossing between two regions was **not exercised by an automated test**; the map rebuilds on any new environment root.

**Save and reload.** Not exercised by an automated test. The map rebuilds on a new environment root, and nothing is saved.

## 12. Nearby entity and location integration

| Source | Used for |
|---|---|
| The persisted settlement plan | Roads, water, fields, wall |
| The layout's footprints | Buildings |
| Shop views | Shops, at their doors |
| Building functions | Services |
| Plan landmarks, gates | Town landmarks, gates |
| Residents | People out |
| Party followers | Companions |
| Region terrain and roads | Country ground |
| Known overland sites | Settlements, places |
| `GetLandmarks()` | Found landmarks (region-scoped) |
| Waypoint | Waypoint |
| The world's travelling groups | Travellers, outlaws |

**Verified:**
- one shop mark per shop;
- people's labels equal the naming rule;
- hidden people are unmarked;
- a moved person's mark follows them;
- a person carried out of reach leaves the map;
- a landmark appears once found (captures);
- the map is hidden behind the hub and returns after.

## 13. Discovery and visibility protections

| What | Rule | Verified by |
|---|---|---|
| Strangers | Shown by trade, not name | .NET; live test against the rule |
| Hidden people | Not shown | Live |
| Unfound landmarks | Not shown | Live: marked exactly when found |
| Unknown sites | Not shown | Live |
| Outlaws | Only within 70 m | Live: none farther. **No outlaws were near in the test world, so the positive case was not exercised** |

**No save change.** No record is created.

## 14. Responsive layout results

Captures at 1920×1080, 1366×768, 1280×720, 1024×768, 1280×800, 2560×1440, 3440×1440 and 700×1000.

- **1080p** lands in the compact width class with Auto scale, so the map is 158 px there.
- **1024×768:** the action bar wraps left of the map.
- **700×1000:** the action bar sits above the map.
- **No overlap** with the vitals, action bar or standing actions was seen. The live test also checks this at the default size.

## 15. Accessibility results

| Check | Result |
|---|---|
| Text 130 % (capture) | Fewer, larger labels; none clipped after the fixes |
| High contrast (capture) | Opaque frame and chips |
| Reduce motion | Exact turns (used by the tests) |
| Colour | Every kind has its own shape |
| Not supported | Controllers and rebinding (deferred) |

## 16. Performance measurements

**Unity editor, batch mode only. No development or player build was made.**

| Measure | Result |
|---|---|
| Per frame (town, 21 marks, 10 people out) | Mean **0.030 ms**, max **0.051 ms** over 240 frames of turning |
| Refresh of moving marks (every 0.4 s) | Mean **0.008 ms** |
| 1000 direct updates | **0.0036 ms** each; managed heap +24 KB over all 1000 (about 24 bytes each, read from the Mono heap size, so approximate) |
| Elements | 12, unchanged after 240 frames of turning |
| Turn settle (120°) | 0.19 s |
| Country refresh | Includes re-reading the region's landmarks every 2 s, not measured separately |

**Not measured:**
- a dense settlement beyond the test town (10 people out);
- a development build;
- the label re-choice that now runs on large turns: its cost is in the per-frame figure above only for the turns in that test.

## 17. Screenshots

**`Documentation/Screenshots/Phase26.1/`:** 22 captures from the running game, plus `p261_capture_log.txt` (sizes, width classes, marks drawn and the map's box).

| # | Capture |
|---|---|
| 1, 13–19 | Settlement at all eight sizes |
| 2 | Shops |
| 3 | People |
| 4 | Several kinds together |
| 5, 6 | Turned 90° and 180° |
| 7 | Moving |
| 8 | Country |
| 9 | Found landmark |
| 10 | Busy street |
| 11 | High contrast |
| 12 | Text 130 % |
| 20 | Road on a slope |
| 21 | Gate |
| 22 | Numbered topics |

**Two substitutions:**
- **People (3):** the people out at 08:00 in the captured town were all shopkeepers, so they show as amber merchant dots, not teal.
- **Companions:** no companion mark appears in the captures (the capture's party step may have recruited nobody; this was not checked). The companion mark is implemented, but **not captured**.

## 18. Documentation changes

**New:**
- `Systems/Phase26.1-Preflight-Audit.md`
- `Systems/Phase26.1-Local-HUD-Map.md`
- `Systems/Phase26.1-Owner-Playtest-Checklist.md`
- ADR-0118
- this report

**Updated:**
- `Systems/UI-Architecture.md`
- `Systems/UI-UX-Design-System.md`
- `Roadmap.md`
- `Product/FutureRoadmap.md` (Phase 27 partly delivered)
- `Decisions/README.md`

**Unchanged:** campaign canon.

## 19. Known limitations

- **Old saves:** fences are unchanged. Settlement plans are saved once and never regenerated, so worlds made before `ba3d333` keep their old fences. The road drawing fix applies to every world.
- **Bridge ends:** decks are level, and up to 92 cm above the ground beneath. A possible step where road meets deck is unchecked.
- **The map:**
  - not shown inside places;
  - no marker selection;
  - the range choice is not saved.
- **Not exercised by tests:** region-to-region crossing, save and reload with the map, outlaws within sight.
- **Not captured:** a companion mark.
- **Performance:** editor only.
- **Not hand-played:** Phases 24–26.1.

## 20. Manual owner playtest checklist

`Systems/Phase26.1-Owner-Playtest-Checklist.md`: 20 checks (the four fixes, then the map). Automated verification is kept separate from your playtesting. **Claude has not played the game by hand.**

## 21. Recommended next development priorities

Suggestions only; the next brief decides.

1. Hand-play Phases 24–26.1, especially the map's size, corner and look.
2. Bridge ends: check for a step where road meets deck, and add a short ramp if there is one.
3. Map follow-ups, if wanted:
   - marker selection (a click shows a mark's name and kind);
   - a map inside places;
   - saving the range choice.
4. A development build, to measure the map and the HUD outside the editor.
5. The next full Unity checkpoint in 2–3 phases, or sooner if shared systems change.
