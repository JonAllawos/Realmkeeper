# Phase 28 completion report

**Phase:** City Arrival, Gates, Security & World-to-Settlement Integration.

**Date:** 2026-11-15.

**Baseline:** `94d0305` on `feature/phase-0-1-foundation`.

**Commits:** listed in §13. Local only; **nothing was pushed.**

**Status words used throughout:**
- **Complete:** built and shown working by tests in the running game.
- **Partial:** part of what was asked is done.
- **Not seen:** implemented but not observed in the test world.
- **Unverified:** not checked by a person.

**Not hand-played.**

## 1. Preflight findings

Details are in `Systems/Phase28-Preflight-Audit.md`.
- **The arrival was a generic point:** 30 % in from the outer end of the first main road, at the plan's edge. For a town that is about 85 m outside its walls, in the fields, and every settlement looked the same.
- **The plan already knew its front gate** (`Gates[0]`, the "town gate" landmark), but nothing used it.
- **The named gate guards** stood at a point derived from that edge arrival, also outside the walls.
- **The unnamed watch** stood one to a gate, on the road's centre line.
- **The wall** left out whole 20 m segments near each gate, leaving gaps beside the gatehouse.
- **The gatehouse** was two towers and a slab: no gate, no portcullis.
- **Villages and hamlets** had no marked way in.
- **The way in from the country** was the first road cell found in scan order.
- **Plans are persisted:** everything was fixable by deriving from the plan at load, with no save change.

## 2. Systems modified

| Area | Change |
|---|---|
| `Domain/Settlements/SettlementEntrances` (new) | The entrances, from the plan: gate, inward direction, validated arrival, watch posts, rank; deterministic fallback; unwalled ways in moved off bends (ADR-0120) |
| `LocalEnvironmentGenerator.FromPlan`, `LocalEnvironmentLayout` | Arrival from the main entrance; `Entrances`, `ArrivalIsFallback`, `GatePost(n)`, `NamedAtGate`; `Gate` = the main gate's first post |
| `EnterSettlementFromOverlandHandler` | In by the entrance facing the way the player came |
| `OverlandQueries.Gate`, `GateOf`, all callers (sites, navigation, travellers, leaving, map point) | The way in from the country on the road to the main gate |
| `TownsfolkPlanner` | Posts beside the road facing out (main gate's pair first); named gate guards hold the first posts and count toward the watch; the watch by status (walled at least 2; city ×1.5; fortress ×2; night: the main gate) |
| `ExplorationController.Townsfolk` | A posted guard keeps its facing |
| `ModularEnvironmentBuilder.Plan` | The wall cut exactly at gatehouses and lanes, postern lintels; gatehouse proportions by status, battlements both faces, corbels, portcullis, open leaves (with thin colliders), lanterns, foundations to the lowest ground; ways in for villages and hamlets |
| `LocalHudMap` | `PlayerAt` (where it last drew the player), for the stale-position test |

**No save schema change.**

## 3. Arrival, before and after

| | Before | After |
|---|---|---|
| From the strategic map | The edge of the plan, about 190 m from a town's centre (85 m outside its walls) | **Just inside the front gate**, 11 m in, on the road, facing the streets: 94 m from the centre in the test town (walls at 105 m) |
| From the country | The same edge point | The entrance facing the way come; via the country's way in, which now lies on the main road: the main gate |
| A village or hamlet | The edge | Just past its way in (4 m), on the road |
| Again, after a load, after leaving | (Varied with the above) | The same gate every time (tested) |
| Fallback | None needed: the edge point always existed | Next entrance in rank; only if none, the stored edge point, reported |

**Old saves** arrive at their gates too: the arrival is derived from the stored plan.

## 4. Gates and walls

Details are in `Systems/Phase28-Settlement-Arrival-and-Gates.md`.

- **Gatehouses:**
  - open plank leaves laid back against the passage;
  - a raised iron portcullis (a metre showing under the arch, more than 3 m up);
  - corbels, battlements on both faces, lanterns either side;
  - foundations down to the lowest ground under each tower;
  - a city's (8 m opening, 5 m towers, 2.5 m taller) and a fortress's (5.5 m towers, 4 m taller) are larger.
- **Walls:**
  - run up to the towers with no gap (tested every 0.8 m within 14 m of the main gate);
  - lanes pass through narrow posterns under a stone lintel.
- **Villages:** a timber gateway with a lantern and a lamp post. **Hamlets:** a stone waymarker and a lamp post.
- **Passage:** a player-sized capsule passes from 16 m outside to the arrival; the player walks in with their own controls (tested).

## 5. Guards and security

- **The existing watch, extended** (no new guard system):
  - posts in pairs beside the road just inside each gate, facing out;
  - the main gate's pair first; by night only the main gate's pair;
  - walled towns keep at least that pair; cities half as many again; fortresses twice as many; hamlets under 120 people none.
- **Named gate guards** (the simulation's residents) now stand at their own posts at the gate, not 85 m outside. The unnamed watch takes the posts they leave and counts them, so no post is held twice.
  - Test town: 3 named at posts, 1 unnamed at a post, 2 on patrol.
- **Faction colours:** uniforms in the owner's colour and banners at the gate (Phase 27 shared colours).

## 6. Roads and approaches

Measured over 90 entrances in 45 plans:
- **Main roads** run through their gates. The sharpest turn within 15 m of a wall gate is 57°. Wall gates stay where the wall is; reshaping those roads would mean changing the generator (new worlds only), so it was not done.
- **Unwalled ways in** now always stand on a straight stretch (under 25° within 4 m). Four had stood on bends of 29–83°.
- **Hill creases** on approaches, away from bridge banks: at most **8.9°** of grade change per 2 m. The sharp readings (up to 53°) are river banks under bridge decks. Bridges are unchanged (owner-approved).
- **The terrain's 2 m triangles were not smoothed:** that would change the walked ground. The road stays draped 3 cm above it (the Phase 26.1 test, rerun and passing).

## 7. Settlement types tested

| Type | Tested | How |
|---|---|---|
| Hamlet | Yes | Arrival, way in, map (running game); plans in seven worlds (.NET) |
| Village | Yes | The same |
| Town (walled) | Yes | Everything above (running game and .NET) |
| City, fortress | **Not seen** | Logic tests (watch size; their gatehouse proportions are code paths); none in view in the test world |

## 8. Automated test results

| Group | Result |
|---|---|
| `Phase28SettlementArrivalTests` (.NET, new) | 6/6: every plan of seven worlds (45, 15 walled) arrives inside its front gate, on the road, clear, facing in; fallback order; determinism; from the map, again and after a save; the country's way in leads to the main gate; the watch; approaches and gate ground |
| Related .NET groups (Phase 27, 23, 24, layouts, NPCs, exploration, fences) | 100/100 |
| .NET full | **974/974** (Release, 5 min 26 s) |
| `Phase28PlayModeTests` (Unity, new) | 3/3: from the map into a walled town and through its gate, the wall and gatehouse checks, the watch, walking in, out to the country and back, save and load, again from the map; a village and a hamlet with the map never stale; repeated entry timed with no growth |
| Unity focused regression | 61 run, 60 passed. Classes: Phase28, Phase27, Phase26_1, Phase23, Phase24, Phase16Atlas, Phase10, Phase13, Phase14, Phase5, Phase11, Npc, Exploration, Phase8, RealScene |

**The one regression failure:** Phase 24's living-country test, "a group near the player is drawn".
- **Cause:** it follows the friendly group nearest the country's way in. That way in now lies on the main road, and the group it picked reached its town during the test's 455 m walk, so it left the road (the traveller logic was right).
- **Fix:** the test now follows the next nearest group when its pick has arrived (up to three). It still requires a group near the player to be drawn. Then 2/2 runs passed.

**Tests changed (none weakened):**
- `SettlementLayoutTests`: "on the ground" now measured where the arrival is (the gate), not at the old edge point.
- `Phase27SettlementLifeTests`: the unnamed watch is the watch less the named gate guards at posts, asserted exactly, plus that the two together are the watch.
- `Phase23OverlandTests` and `Phase24OverlandLifeTests`: use `GateOf`, like the runtime.
- `Phase24PlayModeTests`: as above.

**Full suites:** not run this phase. The last full checkpoint was the 2026-11-15 cleanup pass (EditMode 968/968; PlayMode 141 passed, 0 failed, 37 skipped). **Next checkpoint:** recommended after Phase 29 (two phases on).

## 9. Performance

Details are in `Systems/Phase28-Performance.md`.
- Entrances: 15 µs per settlement.
- Entry from the map: 619–657 ms to the first frame.
- 13,708 objects after each of 5 entries (no growth); memory steady.
- No new per-frame work. Editor only; no player build.

## 10. Screenshots

`Documentation/Screenshots/Phase28/`, 1920×1080 from the running game, with `p28_capture_log.txt`:

| # | Capture |
|---|---|
| 01 | Hamlet entrance (waymarker, lamp) |
| 02 | Village entrance (timber gateway) |
| 03 | Town entrance |
| 04 | Gate from outside |
| 05 | Gate from inside |
| 06 | Exterior approach |
| 07 | Guards at the entrance |
| 08 | Guards on patrol |
| 09 | Faction identity |
| 10 | Road meeting the gate |
| 11 | Approach uphill |
| 12 | Strategic map → arrival (with the interface) |
| 13 | Local map after arrival (cropped) |
| 14 | Approach downhill |

**Missing:** 15, a major city's entrance. The test world has no city or fortress in view.

## 11. Known limitations and deferred work

- **Cities and fortresses not seen:** larger gatehouses and watch are implemented, but not captured or played.
- **Wall gates on bends:** up to 57° within 15 m; the generator was not changed.
- **Hill creases:** at most 9° per 2 m on approaches, from the terrain's own triangles; the ground was not reshaped (owner's rule).
- **Gates never close** and there are no new access rules (as briefed).
- **The country's way in moved** to the main road for every settlement. A player who saved out in the country stays where they saved; only the next exit uses the new point.
- **Not hand-played.** Phases 24–28 are not hand-played.

## 12. Save compatibility

- **No schema change.** Entrances are derived from the stored plan.
- The stored edge arrival stays, used only as the reported fallback.
- Existing saves arrive at their gates.
- A save made inside a settlement keeps its position (tested: save, load, same place).

## 13. Git commits and push status

Branch `feature/phase-0-1-foundation`, baseline `94d0305`.
- **Commits:** code and tests, then documentation and screenshots (hashes in the closing message).
- **Not committed:** the two untracked `Assets/InitTestScene*` scenes (preserved). `M_LampGlow.mat` was restored after every batch run.
- **Push:** pushed 2026-11-15 at the owner's instruction: `94d0305..0c8b77a` to `origin/feature/phase-0-1-foundation`. This note is recorded in a follow-up commit.

## 14. Owner playtest checklist

`Systems/Phase28-Owner-Playtest-Checklist.md`: 17 checks. Automated results are kept separate from your playtesting. **Claude has not played the game by hand.**
