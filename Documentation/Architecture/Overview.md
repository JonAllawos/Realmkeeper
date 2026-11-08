# Architecture overview

Realmkeeper is split into an **engine-independent core** and a **Unity presentation shell**.

```
            ┌───────────────────────────────────────────┐
            │ Presentation (Unity: scene, camera, input, │
            │ rendering, UI Toolkit)                     │
            └───────────────┬───────────────────────────┘
                            │ commands / queries / WorldChanged
            ┌───────────────▼───────────────────────────┐
            │ Application  WorldSession, commands,      │
            │              queries, SimulationPlayback, │
            │              ports: IWorldGenerator,      │
            │                     IWorldStore           │
            └──────┬───────────────────────▲────────────┘
                   │                       │ implements ports
            ┌──────▼──────────┐   ┌────────┴─────────────┐
            │ Simulation      │   │ Infrastructure       │
            │ scheduler,      │   │ RNG, RegionGenerator,│
            │ daily systems   │   │ JSON store,          │
            └──────┬──────────┘   │ composition root     │
                   │              └────────┬─────────────┘
            ┌──────▼───────────────────────▼─────────────┐
            │ Domain  IDs, WorldData, SettlementData,    │
            │         calendar, inventory, RegionMap,    │
            │         validators, IRandomSource          │
            └────────────────────────────────────────────┘
```

## Principles

1. **The core never references UnityEngine.** Domain, Simulation, Application and
   Infrastructure set `noEngineReferences: true` in their `.asmdef`, so any Unity call
   inside them is a compile error. They are also compiled by `Tools/DotNet`, which has no
   Unity assemblies at all.
2. **The simulation is a pure function of state.** `SimulationScheduler.AdvanceDay(world)`
   returns a new world and never mutates its input. It uses no wall-clock time and no
   `Update()`. Randomness comes only from seeded streams.
3. **Geography and world data are separate objects.** `RegionMap` (terrain, rivers, roads)
   is derived from the seed. `WorldData` (settlements, factions, events, date) is the
   authoritative, saved state. GameObjects are built from both and own neither.
4. **Ports and adapters.** Application defines what it needs (`IWorldGenerator`,
   `IWorldStore`). Infrastructure implements those interfaces. `RealmkeeperComposition` is
   the only class that wires concrete types together.
5. **One way to change state.** Every mutation goes through a command handled by
   `WorldSession.Execute(...)`. Presentation receives immutable snapshots
   (`SettlementSummary`, `EventSummary`, `WorldOverview`).

## Extension points

| Need | Extend by |
|---|---|
| New daily rule (trade, disease, factions) | Implement `ISimulationSystem`, register it with a unique `ExecutionOrder`, add its ID to `WorldSettings.EnabledSimulationSystems` |
| Manual terrain editing (World Editor) | `IRegionMapEditor` / `RegionMapEditor` already mutate a `RegionMap` and report changed bounds |
| Save format change | Bump `WorldData.CurrentSchemaVersion` and add an `ISaveMigration` |
| Generator change | Bump `RegionGenerator.Version`; old saves are refused until a version-pinned generator is kept |
| New persistence backend | Implement `IWorldStore` |

## Known architectural constraints

- The assembly `Realmkeeper.Application` creates the namespace `Realmkeeper.Application`.
  Inside `Realmkeeper.*` namespaces, the bare name `Application` resolves to that namespace,
  not to `UnityEngine.Application`. Presentation code must write
  `UnityEngine.Application.persistentDataPath` in full. The assembly name was required by
  the specification, so it was kept and this constraint is documented instead.
- `WorldSession` is single-threaded.
