# Phase 28: preflight audit

**Date:** 2026-11-15.

**Baseline:** `94d0305`, branch `feature/phase-0-1-foundation`; the working tree is clean apart from the two preserved, untracked `Assets/InitTestScene*` scenes.

**Method:** reading the code paths below. Nothing was changed before this audit.

## 1. How a settlement is selected and entered

| Way in | Command and handler | Where the player is put |
|---|---|---|
| Strategic (world) map | `EnterSettlementCommand` → `EnterSettlementHandler` (`Exploration/Application/ExplorationHandlers.cs`) | `p.Pose = layout.ArrivalPose` |
| On foot from the country (Phase 23) | `EnterSettlementFromOverlandCommand` → `EnterSettlementFromOverlandHandler` | The same `layout.ArrivalPose` |
| Loading a save | `ExplorationController.LoadEnvironment` | The saved `player.Pose` (a roadside fight uses its own stage) |

`ExplorationModule.LayoutFor` (command side, from `WorldData`) and the presentation's `LayoutFor` (from the session) both build the local layout with `LocalEnvironmentGenerator.FromPlan` from the **persisted settlement plan** (`SettlementLayoutData`, ADR-0047).

## 2. How the arrival point is decided

- `SettlementLayoutGenerator.Arrival` stores `plan.Arrival` at generation:
  - the point 30 % of the way in from the **outer end of the first main road**, at the edge of the plan;
  - facing along the road.
- `FromPlan` turns it into `ArrivalPose`.

**Consequence: arrival is at the edge of the plan, not at an entrance.**

| Size | Plan half-extent | Walls / core | Arrival from the centre |
|---|---|---|---|
| Towns and larger | 200 m | Walls at 105 m | about 190 m: **some 85 m outside the walls**, in open fields |
| Villages | 150 m | Core 70 m | about 140 m |
| Hamlets | 110 m | Core 40 m | about 100 m |

Every entry, whatever the settlement, looks the same: a road in open grass at the edge of the walkable land. This matches the owner's report.

## 3. Arrival based on entrances?

**No.** The arrival is a generic point on a road at the plan's edge. Gates are not consulted.

## 4. How the main gate is identified

- `SettlementLayoutGenerator.Walls` adds a gate where each **main road** (one per road out to a neighbouring settlement, `RoadData.Toward`) crosses the wall circle, in road order.
- `Gates[0]` (the first main road, the same road the arrival uses) is the plan's own "town gate": the `lm-gate` landmark "where the main road passes the walls".
- Only walled plans (`HasWalls`: towns, cities and fortresses) have gates. Villages and hamlets have none.

## 5. The entrance as built (`ModularEnvironmentBuilder.Plan.cs`)

**A gatehouse is built over every gate** (`BuildGatehouse`):
- two 4 m stone towers, a 7.5 m opening and a stone lintel;
- colliders on the towers and the lintel only, so the way is open and walkable.

**Defects:**
- **Gaps in the wall.** The wall leaves out every 20 m wall segment (32-gon at r = 105 m) whose midpoint is within 12 m of a gate. Depending on where the gate falls, it leaves gaps of 2–20 m beside the 15.5 m gatehouse. Visible in the Phase 27 capture `p27_06`.
- **No gate.** No leaves, no portcullis: the lintel is a plain slab.
- **Posterns.** Wall segments crossed by any lane are removed whole, leaving a 20 m gap for a 3–5 m lane.
- **Villages and hamlets** have no entrance structure at all.

## 6. Guards, patrols, faction identity, security

- **Phase 27 watch (`TownsfolkPlanner`):**
  - `Watch` = population ÷ 250 (1–10; none under 120 people), halved at night;
  - by day, one guard **post per gate**, standing 6 m inside the gate **on the road's centre line**;
  - the rest patrol from the gates to civic buildings.
  - Guards wear the owner's colour (`RkFactionColours`).
  - **Defect:** guards stand in the roadway; only one per gate, even at the main gate.
- **Named gate guards** (residents whose site is `SettlementSites.Gate`): anchored at `layout.Gate`, which `FromPlan` derives from the **edge arrival point**. So they stand in the fields 85 m outside the walls, not at the gate.
- **Banners** (Phase 27): back to back on poles either side of each gate.
- **Security does not scale** beyond the population count: a city and a town of equal size look the same; fortification and status are unused.

## 7. Roads to the entrance

- Main roads run from the centre outward to the plan's edge and pass through their gate, by construction, so the approach does lead to the gate.
- Lanes that cross the wall make a gap (a postern) but have no structure.
- **Phase 27 carry-over:** corners are rounded and ends capped. Hill creases are the terrain's own 2 m triangles; the road is draped on the collider, 3 cm above it at every vertex (verified in Phase 26.1 and 27).
- **Leaving for the country:** `OverlandQueries.Gate` puts the player on the first road cell found, in scan order, in a ring six cells (240 m) from the settlement. That way in is chosen without regard to which road leads to the main gate.

## 8. Old and new settlements

- Plans are persisted (save DTO: `Gates`, `Wall`, `ArrivalX/Z/Yaw`).
- Everything above is derived from the plan at load, so a change to the derivation applies to old and new settlements alike, without a save change.
- Changing the generator's stored `Arrival` would only affect new worlds, and would make old and new behave differently. **So the generator is left alone.**

## 9. Plan for Phase 28 (from these findings)

1. **Entrances from the plan, one pure function** (`SettlementEntrances`): the gate (or, unwalled, where the main road enters the core), the inward direction, a validated arrival point just inside, and the guard posts beside the road.
   - **Main entrance:** the plan's own front gate, the first main road (`Gates[0]`, the `lm-gate` landmark).
   - **Deterministic fallback:** the next entrance; then the legacy edge point, reported.
2. **Arrival:**
   - from the map, at the main entrance;
   - from the country, at the entrance facing the way the player came.
   - `layout.Gate` (the named gate guards) moves to the main entrance.
3. **The overland way in** sits on the road that leads to the main gate, so leaving and re-entering line up.
4. **Gates and entrances:**
   - **Walls:** cut exactly around each gatehouse and each lane; no whole segments dropped.
   - **Gatehouse:** open gate leaves and a raised portcullis; the way kept clear.
   - **Small entrance structures:** a timber gateway for villages, a waymarker for hamlets.
   - **Size by status:** a city's and a fortress's gatehouses are larger.
5. **The watch:**
   - posts in pairs at the main gate, beside the road, facing out;
   - the count scaled by status (city, fortress) through the existing planner;
   - no new guard system.
6. **Tests:** arrival over every settlement of the test worlds; walk-through and collider checks in the running game; the HUD map, save and load, and re-entry.
