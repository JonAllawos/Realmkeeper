# Phase 28: performance

**Conditions:**
- Unity 6000.0.47f1 editor, **batch mode**; the .NET harness in Release.
- **No development or player build was made.** None of these figures are player-build numbers.
- Test town: Bramhaven (1,644 people, two gates).

## Measured

| Measure | Result | Where |
|---|---|---|
| Deriving a settlement's entrances and arrival (`SettlementEntrances.Arrival`) | **15 µs per settlement** (45 plans × 20 rounds) | .NET, Release |
| Entering the town from the map (Enter pressed → first frame of the settlement), 5 times | **619–657 ms** | Editor |
| Scene objects after each of the 5 entries (town, 2 s after arrival) | **13,708 every time** (no growth) | Editor |
| Managed memory after each entry (after a full collection) | 572–593 MB, steady (editor process; not comparable to a build) | Editor |
| Gate objects kept as objects (gatehouse roots, portcullises, gate leaves) | 12 in the town; the rest is merged static geometry | Editor |
| Townsfolk with the same key after entry | None (asserted on every entry) | Editor |

## How the new parts cost nothing per frame

- **Entrances:** computed once per layout at load (15 µs), never per frame.
- **Gatehouses, walls and ways in:** static geometry, built once with the settlement and merged with the rest. The portcullis, leaves, lanterns and corbels add a few dozen small meshes per gate before merging.
- **Gate guards:** the existing watch planner's specs (one hourly plan) and the existing named residents. No new update loop, scan or search. A posted guard's facing is a field read in the existing townsfolk update.
- **The overland way in (`GateOf`):** reads the plan's main bearing without the standing checks; called where `Gate` was called before.

## Comparison with Phase 27

Phase 27 measured entering the town at about **1.65–1.70 s**. That figure included the layout, the build with dressing and herds, the residents and several settling frames. This phase measures from Enter to the first frame (619–657 ms) instead, so **the two are not directly comparable**. No regression was found against Phase 27's other measures: the townsfolk update and the figure builds are unchanged.

## Not measured

- A development or player build.
- Draw calls and GPU time.
- City and fortress gatehouses (none in the test world in view).
- Pathfinding: the arrival uses no pathfinding; townsfolk routes are Phase 27's, unchanged.
