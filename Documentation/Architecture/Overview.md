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

## Long-term direction: one world, two perspectives (binding)

Realmkeeper will gain an exploration mode (first- and third-person) beside the strategic map.
Both must be clients of the **same** `WorldSession` and `WorldData`. See
[Product foundation, Addendum A](../Product/ProductFoundation.md#addendum-a-dual-perspective-persistent-world-binding)
and [ADR-0009](../Decisions/ADR-0009-dual-perspective-world.md). Nothing below is built yet.

```
   ┌──────────────────────────┐   ┌─────────────────────────────────────────────┐
   │ Strategic Presentation   │   │ Exploration.Presentation / .Interaction /   │
   │ (today: Presentation)    │   │ .WorldStreaming            (Unity, future)  │
   └────────────┬─────────────┘   └───────────────────┬─────────────────────────┘
                │ commands / queries / changes        │ commands / queries / changes
   ┌────────────▼─────────────────────────────────────▼─────────────────────────┐
   │ Application: WorldSession (one per running world) + Exploration.Application │
   └────────────┬───────────────────────────────────────────────────────────────┘
                │
   ┌────────────▼──────────────┐   ┌───────────────────────────────┐
   │ Simulation (one scheduler)│   │ Exploration.Domain (future)   │
   └────────────┬──────────────┘   └──────────────┬────────────────┘
                └──────────────► Domain ◄─────────┘
```

Rules that follow from it, on top of the principles above:

6. **No second world.** Exploration never keeps its own copy of settlement, NPC or resource
   data. It renders snapshots and sends commands, like the strategic HUD.
7. **The clock ignores the camera.** Days advance through `SimulationPlayback` in either mode.
8. **Exploration keys on stable IDs.** Local environments, NPC instances and the character
   refer to settlement and region IDs. They are never looked up by name or scene path.

## Extension points

| Need | Extend by |
|---|---|
| New daily rule (trade, disease, factions) | Implement `ISimulationSystem`, register it with a unique `ExecutionOrder`, add its ID to `WorldSettings.EnabledSimulationSystems` |
| Manual terrain editing (World Editor) | `IRegionMapEditor` / `RegionMapEditor` already mutate a `RegionMap` and report changed bounds |
| Save format change | Bump `WorldData.CurrentSchemaVersion` and add an `ISaveMigration` |
| Generator change | Bump `RegionGenerator.Version`; old saves are refused until a version-pinned generator is kept |
| New persistence backend | Implement `IWorldStore` |
| Exploration (future) | New `Exploration.*` modules per ADR-0009; added one at a time for the vertical slice |

## Known architectural constraints

- The assembly `Realmkeeper.Application` creates the namespace `Realmkeeper.Application`.
  Inside `Realmkeeper.*` namespaces, the bare name `Application` resolves to that namespace,
  not to `UnityEngine.Application`. Presentation code must write
  `UnityEngine.Application.persistentDataPath` in full. The assembly name was required by
  the specification, so it was kept and this constraint is documented instead.
- `WorldSession` is single-threaded.
