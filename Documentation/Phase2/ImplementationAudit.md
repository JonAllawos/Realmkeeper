# Phase 2 implementation audit

Written before any Phase 2 code changed (branch `feature/phase-0-1-foundation` at `46c0c6e`).

## Structure found

| Layer | Contents |
|---|---|
| Domain | `WorldData`, `SettlementData`, `ResourceInventory` (sorted, non-negative, overflow-checked), IDs (GUID structs), `WorldDate` (12 × 30 days), `RegionMap`, `WorldValidator`, seeded RNG interfaces |
| Simulation | `SimulationScheduler` (copy → run systems in explicit order → validate → commit), `FoodEconomySystem` (order 100), `SettlementWellbeingSystem` (order 200) |
| Application | `WorldSession` (commands, queries, `WorldChanged`), `SimulationPlayback` (real-time pacing), immutable query snapshots |
| Infrastructure | `RegionGenerator` v1 (terrain, 3 settlements, roads, initial stats), JSON persistence (DTOs, mapper, migrations by schema version), xoshiro256** RNG, composition root |
| Presentation | Bootstrap, world visuals, camera, selection, UI Toolkit HUD |
| Tests | 61 EditMode methods / 75 cases, 11 PlayMode |

## What the displayed statistics really are

| Statistic | Source | Verdict |
|---|---|---|
| Population | Generator, then food system (starvation) and wellbeing system (random growth) | Dynamic, but growth is a coin flip |
| Prosperity, Stability | Generator, then wellbeing system: ±1 on `Chance(0.05…0.5)` rolls | Dynamic but **unexplained random drift** |
| Food reserves | Food system: `+FoodProductionPerDay − ceil(pop / 10)` each day | Dynamic, correct |
| Food production | Set once by the generator (`min(fertile cells, 12% of pop)`) | **Static placeholder**: no farms, workforce or sources |
| Timber, Stone, Ore, Coin | Set once by the generator from nearby terrain counts | **Static placeholders**: nothing produces or consumes them |
| Days of food remaining, net food | Computed in the `SettlementSummary` query from production − consumption | Derived correctly, but only for food |
| Faction | Generator | Static (fine for this phase) |

## One day, traced

`+1 day` → `WorldSession.Execute(AdvanceDayCommand)` → `SimulationScheduler.AdvanceDay(world)`:
validate the input world → deep-clone → date + 1 → a per-day context whose RNG is seeded by
(world seed, day number, system ID) → `FoodEconomySystem` → `SettlementWellbeingSystem` →
append the events → validate → commit. `WorldSession` replaces its world and raises
`WorldChanged`, and the HUD refreshes.

- **Seven days in one command vs seven single days:** identical. `AdvanceDays` loops
  `AdvanceDay`, and each day's random stream depends only on the day and the system, not on
  how the days were batched. This is covered by `Simulation_StepwiseAndBatchedAdvancementAreIdentical` (45 days).
- **Save and load:** a field-by-field round trip, covered by
  `SaveLoad_RoundTripPreservesEntireWorldState` and by a test that continuing after a load matches an uninterrupted run.
- **Atomic days:** a failed system or invalid output leaves the input world untouched (tested).

## Problems found

1. **Random prosperity, stability and growth.** `SettlementWellbeingSystem` changes ratings and
   population on coin flips. The specification asks for explainable, configurable formulas.
2. **No real economy.** Food production is a single static number, and the four other resources never change.
3. **Starvation is immediate.** On the first day reserves hit zero, population drops by
   pop / 200. There is no grace period.
4. **"Ore" vs "Iron".** The resource key is `ore`, but the specification names it Iron. Old saves
   need a migration.
5. **Events are pre-written English.** `HistoricalEvent` carries a single settlement ID and a
   finished sentence from the simulation. There is no structured data, no list of involved
   settlements, and no importance level.
6. **Duplicated configuration.** `RegionGenerator` builds its own `new FoodEconomyRules()`
   instead of using the configured rules.
7. **Validation gaps.** No storage capacities, no unknown-resource checks, no references
   between settlements (nothing needed them yet).
8. **Growing per-day cost.** The validator re-checks every historical event every day,
   which is O(history). This is acceptable now, but worth noting for very long games.

## What is kept as is

The scheduler (copy → validate → commit), deterministic seeding, the ID types, the calendar,
terrain generation (unchanged, so existing seeds keep their geography), JSON persistence
with migrations, `WorldSession` commands and queries, playback pacing, and the whole
presentation layer. Phase 2 extends these. It does not replace them.

## Phase 2 design decisions

- **Economy data lives on `SettlementData`.** That covers production sources with workforce,
  capacity and efficiency, storage capacities, and the last day's ledger (production and
  consumption by resource and by source). The simulation writes it, and the UI only reads it.
- **Systems run in a fixed order:** labour (50) → production (100) → trade (150) → consumption (200) →
  wellbeing (300) → population (400) → chronicle milestones (500). Each one is a small, separately
  testable `ISimulationSystem`. None of them uses randomness.
- **Trade routes and shipments are stored in `WorldData`.** Goods leave the origin on departure,
  travel inside a shipment record, and are added at the destination on arrival. If the
  destination is full, the shipment waits and then returns, so nothing is created or lost
  except an explicit, recorded transit loss.
- **Events carry structured facts.** Each event has an ID, date, type, involved settlement IDs,
  key/value data and an importance level. The Application layer's `EventFormatter` turns
  those facts into sentences, so the simulation never writes UI text.
- **Save schema v2.** A JSON migration renames `ore` to `iron`. Worlds without economy data
  (v1 saves) get it from the regenerated terrain when they load. Terrain generation is
  unchanged, so the generator version stays at 1.
