# Simulation scheduling

Implementation: `Assets/Realmkeeper/Runtime/Simulation/`.

## Contract for a system

```csharp
public interface ISimulationSystem
{
    string SystemId { get; }       // unique, stable; also seeds the system's random stream
    int ExecutionOrder { get; }    // unique; ascending
    void Execute(SimulationDayContext context);
}
```

Inside `Execute`, a system may:

- read and change `context.World` (a working copy);
- use `context.Random` (its own deterministic stream);
- call `context.ReportEvent(...)`.

A system must not:

- use `System.Random`, `DateTime.Now`, `Guid.NewGuid()`, static mutable state, threads,
  `HashSet`/`Dictionary` iteration order, or `string.GetHashCode()`;
- keep references to the working world after `Execute` returns.

## Registration

```csharp
var scheduler = new SimulationScheduler(new Xoshiro256RandomFactory());
scheduler.Register(new FoodEconomySystem());
scheduler.Register(new SettlementWellbeingSystem());
```

`Register` rejects null systems, empty IDs, duplicate IDs and **duplicate execution
orders**. Two systems with the same order would run in an order nobody chose, so that is an
error, not a tie-break. Leave gaps of 100 so new systems can slot in between.

A world runs only the systems whose IDs appear in `WorldSettings.EnabledSimulationSystems`.
Unknown IDs are ignored, so a save from a build with an extra system still loads.

## Phase 1 systems

### `realmkeeper.food` (order 100), no randomness

For each settlement, in list order:

1. `consumption = ceil(population / 10)`
2. If `reserves + production ≥ consumption`, reserves become `reserves + production − consumption`.
3. Otherwise reserves become 0 and population falls by `max(1, population / 200)`. If
   reserves were above 0 before, a **`FoodReservesExhausted` (Critical)** event is reported.
4. Shortage state, with hysteresis:
   - begins when reserves < 15 days of consumption → **`FoodShortageBegan` (Warning)**
   - ends when reserves ≥ 30 days → **`FoodShortageEnded` (Info)**
   - Events are reported only on these transitions, never every day.

### `realmkeeper.wellbeing` (order 200), seeded randomness

- In shortage: stability −1 with probability 0.5; another −1 if reserves are 0; prosperity
  −1 with probability 0.2.
- Otherwise: stability +1 with probability 0.1 while below 60. When in surplus: prosperity +1
  with probability 0.05, and population grows by `max(1, population / 1000)` with
  probability 0.1.
- Stability and prosperity are kept within 0–100.

All simulation state is integral. The only floating-point values are the `Chance`
probabilities, compared against `NextDouble()`, which is built from 53 random bits.

## Guarantees (tested)

- The input world is never mutated.
- Identical world + identical systems → identical result, including event IDs.
- `AdvanceDays(n)` gives exactly the same result as `n × AdvanceDay`.
- Save → load → continue gives exactly the same result as an uninterrupted run.
- A day that throws or produces invalid state is not committed.
- Execution order follows `ExecutionOrder`, whatever the registration order.

## Performance

Each day deep-copies the whole `WorldData`. That is negligible for three settlements, but it
grows with entity count and history length (events are shared by reference, not copied). When
entity counts reach the thousands, see the options in `Documentation/Decisions/ADR-0008`.
