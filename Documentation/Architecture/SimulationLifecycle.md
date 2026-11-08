# Simulation lifecycle

## World lifecycle

```
CreateWorldCommand ──► RegionGenerator.Generate(seed) ──► WorldData + RegionMap ──► WorldSession (active)
LoadWorldCommand   ──► JsonWorldStore.Load ──► validate ──► RegenerateRegion(seed, version) ──► WorldSession
AdvanceDay(s)Command ──► SimulationScheduler ──► new WorldData (committed) ──► WorldChanged(Advanced, events)
SaveWorldCommand   ──► JsonWorldSerializer ──► atomic file write
```

## One simulated day

`SimulationScheduler.AdvanceDay(world)`:

1. **Validate the input** with `WorldValidator`. An invalid input is rejected and nothing runs.
2. **Copy** with `world.DeepClone()`, then set `CurrentSimulationDate` to the next day.
3. **Create a `SimulationDayContext`.** The day seed is
   `Combine(Mix(worldSeed), dayNumber)`, and event IDs come from their own stream derived
   from that seed.
4. **Run each registered system in ascending `ExecutionOrder`**, skipping systems not in
   `WorldSettings.EnabledSimulationSystems`. Before each one runs, the context gets a fresh
   random stream derived from `(daySeed, systemId)`.
   - Phase 1 order: `realmkeeper.food` (100), then `realmkeeper.wellbeing` (200).
5. **If a system throws**, the whole day is rejected and the input world is returned unchanged.
6. **Append reported events** to `HistoricalEvents`.
7. **Validate the output.** If any invariant is broken (negative population, ratings outside
   0–100, duplicate IDs, dangling references, events dated in the future), the day is
   rejected.
8. **Commit**: return the working copy as the new world.

`AdvanceDays(n)` repeats this. Each day commits on its own, and the run stops at the first
failure, returning the state after the last good day and the number of days completed.

## Real-time pacing

`SimulationPlayback.Tick(deltaSeconds)` (Application) collects frame time and issues
`AdvanceDaysCommand` at whole-day boundaries:

| Speed | Seconds per day |
|---|---|
| Slow | 2.0 |
| Normal | 1.0 |
| Fast | 0.4 |
| VeryFast | 0.1 |

At most 4 days are advanced per tick. A larger backlog (for example after a debugger pause)
is dropped, not replayed. Simulation results depend only on the number of days advanced,
never on frame rate. Presentation calls `Tick(Time.deltaTime)` from one `MonoBehaviour`.
That call is the only connection between `Update()` and the simulation.

## Why every system gets its own stream

If systems shared one random stream, adding or reordering a system would change the random
outcomes of every system after it. Separate streams keyed by system ID keep systems
independent: adding a trade system next phase will not change how stability drifts under
the existing seed.
