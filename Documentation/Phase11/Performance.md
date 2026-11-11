# Phase 11: performance

The numbers come from `Tests/EditMode/Items/ItemPerformanceTests.cs`, run with the .NET harness (Release, the Phase 2 test world of 3 settlements, a fighter in the town with 150 extra instances in the pack). The test fails above the ceilings; the measured values are printed.

| Measure | Measured | Ceiling |
|---|---|---|
| Inventory page: 150 rows, filter, search and sort | 0.22 ms each | 20 ms |
| Comparison through the rules engine (two `CombatStats`) | 0.11 ms each | 20 ms |
| Transfer to the chest (a command validating the whole world) | 1.3 ms each | 500 ms |
| 10,000 rolls of the `old_cache` table (20,000 items) | 19 ms in all | 2,000 ms |
| Item genesis for the world (6 containers, 11 sites) | 0.1 ms | 500 ms |
| Save and load of the world (554 KB) | 7 ms / 31 ms | — |
| Item sections of the save (containers, sites, pack) | 51 KB | — |

What keeps it cheap:

- Queries build rows once per call from the world and sort with simple comparisons. There is no per-frame work: the hub redraws on demand.
- The comparison runs `CombatStats` on a cloned player in a one-player world, not on the whole world.
- The one-holder check is one pass over all holders with a hash set.
- Loot piles are bounded at 30 beyond the settlements' own containers, shop goods at 24 a shop, and finished work records at 40.
- Saves write instance state only when it differs from a fresh instance, so a plain pack costs what it did in Phase 10.

In the shipped scene the item screens add no measurable frame cost when closed. The chest and cache are a handful of primitive shapes per settlement. The six Phase 11 PlayMode tests each finish in under 2.2 seconds, including scene load, town entry and a save-and-reload.
