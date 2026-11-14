# Phase 26.1: preflight audit

**Date:** 2026-11-14.

**Scope:**
- Close the Phase 26 verification gaps: the full Unity suites and the four post-play fixes.
- Add a compact local map to the exploration HUD.

## 1. Git state (inspected, not assumed)

| | |
|---|---|
| **Branch** | `feature/phase-0-1-foundation` |
| **HEAD** | `a46a90f` |
| **Tracking** | `origin/feature/phase-0-1-foundation`, at `3f8ded4` (after a fetch) |
| **Ahead / behind** | 3 ahead, 0 behind |
| **Working tree** | Clean apart from the untracked files below |
| **Untracked** | `Assets/InitTestScene37e5c1f4-….unity` and `Assets/InitTestScene8aeb0c53-….unity`, each with its `.meta`. These are test-runner scenes; they are preserved and were not committed |

**History, newest first:**

| Commit | What it is | On origin |
|---|---|---|
| `a46a90f` | Docs: the road height was traced, not measured | No |
| `a899850` | Docs: the report names the fix commit | No |
| `ba3d333` | The four post-play fixes | No |
| `3f8ded4` | Docs: the push of Phases 25 and 26 | Yes |
| `6562066` | Phase 26 | Yes |
| `5e2629a` | Phase 25 | Yes |

**A difference from the brief.** The brief lists `3f8ded4` with the post-play fixes as local and unpushed. In fact `3f8ded4` is a documentation commit and is on origin. The fixes are `ba3d333`, plus the two report commits after it; those three are local only.

Ancestry was checked: `5e2629a` and `6562066` are ancestors of HEAD.

**Batch runs re-save `M_LampGlow.mat`.** It is restored after each run and never committed.

## 2. Test history and baseline

**The last full Unity checkpoint was Phase 25** (`5e2629a`):
- Unity EditMode 947/947.
- Unity PlayMode 133 passed, 1 failed, 34 capture-only skips. The failure was a stale test, fixed and re-run green.

**Since then (not covered by any full Unity run):**
- Phase 26: trade, crafting, party, the codex, dialogue and combat interface changes.
- The post-play fixes: the settlement generator, terrain draping for roads, shared typing input, number keys.

**What earlier rounds verified:**
- **Phase 26:** .NET 951/951; targeted PlayMode only (by the owner's choice).
- **Post-play fixes:** .NET 954/954; targeted PlayMode 8/8 (`Phase10`, `Phase12`, `Phase26`).

**Decision under the revised testing policy.** A full Unity checkpoint is justified now: two phases of changes have accumulated, including shared generation and input. The plan avoids duplicate runs:
- **Full .NET suite and full Unity EditMode suite:** run at the start, on the baseline. The local map is presentation code; its pure logic is tested in .NET, so EditMode does not depend on it.
- **Full Unity PlayMode suite:** run once, after the map is built. One run then covers both the baseline and the new HUD element, instead of two identical runs.

## 3. The four post-play fixes, as implemented (`ba3d333`)

**1. Roads.**
- `ModularEnvironmentBuilder.Plan.cs`: `GroundAt` interpolates the 2 m terrain grid on the same two triangles per cell as the terrain mesh.
- `BuildRoad`: a row every metre, five points across, `RoadLift` 3 cm; bridge decks stay level.
- `FieldPoint` (fields and fences) uses `GroundAt`.

**2. Fences.**
- `SettlementLayoutGenerator.AddFenced` places yards, pastures, orchards and gardens. The fence line is sampled every metre against every road segment (half-width + 0.8 m), and no road may lie inside the plot.
- The gate goes on the side that opens onto free ground nearest a road; the alternatives are the four 90° turns of the same rectangle.
- Fence drawing: two runs either side of a 3.2 m opening, with two taller gate posts.
- `SettlementFenceAccessTests` (.NET) covers it.

**3. Typing.** `RkTyping.Active()` checks every active `UIDocument`'s focused element. The list is refreshed at most twice a second.
- The strategic camera reads no input while it is true.
- `ExplorationController.Typing()` uses it.

**4. Numbers.**
- `ExplorationController.NumberPressed`: 1–9 from the top row or the keypad, 0 for 10.
- Street topics invoke `_shownTopics[n − 1]`: the buttons as drawn.
- Dialogue responses accept 1–9.

**Not yet verified in the running game.** The road height against the walked collider, gate passage with a player-sized capsule, typing with real key events, and number keys with real key events. Phase 26.1 adds PlayMode checks for each (`Phase26_1PlayModeTests`).

## 4. HUD layout (from the Phase 25 captures and `ExplorationHud.uss`)

| Area | Content |
|---|---|
| Top left | Place, region and date; the vitals under them (one column) |
| Top right | Perspective, Save, Return to world map; the toasts under them |
| Bottom centre | Response and topics, the prompt, the action bar, the controls line |
| **Bottom left** | **Free during exploration** |
| **Bottom right** | **Free during exploration** |

**Combat:** the HUD steps aside (`xk-root--window`). Combat's own panels then use both bottom corners (the combatant on the left, the log on the right).

**Choice: bottom right.**
- The place and vitals column is on the left, so the map balances it.
- Toasts move to the bottom right only while a window is open, and the map is hidden then.
- The action bar is centred and at most about 930 px wide at 1080p; the map's 184 px leaves room on both sides.
- At compact widths, the bottom stack is padded to keep clear of the map. At narrow widths, the map shrinks and the stack rises above it.

## 5. Data the local map can reuse (no new tracking)

| Mark | Source | Rule |
|---|---|---|
| Player position and heading | `PlayerCharacter.transform`, `PlayerCharacter.Heading` | Heading is the player's own heading: movement is relative to it, and the first-person body faces it. In third person the body turns toward it as you walk |
| Settlement ground | `LocalEnvironmentLayout.Plan`: roads, waterways, fields, wall; `Buildings` footprints | The persisted plan |
| Shops | `ExplorationController.Shops` (`ShopInteriorView`: name, building) | At the building's door (`LocalPaths.ToWorld`) |
| Services | `LayoutBuilding.Function` (tavern, market hall, hall, shrine, smithy, mill, guard post, stable) | Plain words, no invented names |
| Town landmarks | `Plan.Landmarks` (keep, chapel, watchtower) | Visible buildings |
| Gates | `Plan.Gates` | — |
| Residents | `ExplorationController.Residents` (`ResidentView`) | Only those out (not `IsHidden`). Named only when `KnownPersonQueries.AcquaintanceOf` is past `Seen` (the codex's rule); otherwise by trade |
| Companions | The party's `CompanionFollower`s | — |
| Country ground | `OverlandView.Terrain` cells (40 m), `OverlandView.Roads` | — |
| Known sites | `OverlandView.Sites` where `Known` | Settlements, places, ways out |
| Landmarks | `WorldSession.GetLandmarks()` where `Found` | Region-scoped |
| Waypoint | `WaypointView` in this region | — |
| Travelling groups | `TravellerViews` (the world's own groups) | Outlaws only within 70 m (sight) |

**Not used:** hidden people, unfound landmarks, unknown sites, creatures in places. None of these is shown.

## 6. Implementation approach

- **`Application/Interface/LocalMapQueries.cs`** (pure, tested in .NET):
  - the heading-up projection, edge pins, heading smoothing, label decluttering and shortening;
  - `WorldSession.MapLabelFor` (the knowledge rule for names).
- **`Exploration/Presentation/LocalHudMap.cs`:** the UI Toolkit element.
  - **Ground:** drawn once per area into a canvas, which is moved and turned by its style.
  - **Marks:** drawn with Painter2D shapes in one layer.
  - **Labels:** a pool of five.
- **`ExplorationController.LocalMap.cs`:** feeds it.
  - Still marks are made once per environment.
  - Movers are refreshed every 0.4 s and followed by their transforms.
  - Labels and acquaintance are re-read every 2 s.
- **No save change. No new records.**
