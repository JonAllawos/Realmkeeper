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
var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory(), new EconomyRules());
// or register individually: scheduler.Register(new LabourSystem(rules)); …
```

`Register` rejects null systems, empty IDs, duplicate IDs and **duplicate execution
orders**. Two systems with the same order would run in an order nobody chose, so that is an
error, not a tie-break. Leave gaps of 100 so new systems can slot in between.

A world runs only the systems whose IDs appear in `WorldSettings.EnabledSimulationSystems`.
Unknown IDs are ignored, so a save from a build with an extra system still loads.

## Phase 2 systems

| Order | ID | Randomness |
|---|---|---|
| 50 | `realmkeeper.labour` | None |
| 100 | `realmkeeper.production` | None |
| 150 | `realmkeeper.trade` | Only for new shipment IDs (the system's own stream) |
| 200 | `realmkeeper.consumption` | None |
| 300 | `realmkeeper.wellbeing` | None |
| 400 | `realmkeeper.population` | None |
| 500 | `realmkeeper.chronicle` | None |

Rules and formulas: [Economy](Economy.md). Phase 1's `realmkeeper.food` and its coin-flip
`realmkeeper.wellbeing` were replaced. A v1 save that lists them is migrated to the Phase 2
list on load. All simulation state is integral; there is no floating point in any rule.

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
