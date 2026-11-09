# Data ownership

| Data | Owner | Mutated by | Persisted | Source of truth |
|---|---|---|---|---|
| `WorldData` (identity, seed, versions, date, settings) | `WorldSession` | Commands only | Yes (JSON) | Save file |
| `SettlementData`, `ResourceInventory` | `WorldData` | Simulation systems (on a working copy), generator | Yes | Save file |
| `FactionData`, `NpcData` | `WorldData` | Generator (Phase 1) | Yes | Save file |
| `HistoricalEvent` | `WorldData` | Appended by the scheduler; immutable | Yes | Save file |
| `RegionMap` (elevation, terrain, rivers, roads) | `WorldSession.Map` | Generator; future `IRegionMapEditor` | **No**, rebuilt from seed + generator version | Seed |
| Simulation rule constants (`FoodEconomyRules`) | Composition root | Configuration | No | Code / future ScriptableObject |
| GameObjects, meshes, materials | Presentation | Presentation | Never | Derived from the two rows above |
| UI selection state (selected `SettlementId`) | Presentation | Input | No | Transient |
| `SettlementEconomy`, trade routes, shipments (Phase 2) | `WorldData` | Economy and trade systems (on a working copy), generator | Yes | Save file |
| *Future:* character state (position, perspective, inventory) | `WorldData` | Exploration commands only | Yes | Save file |
| *Future:* local environment state (changes inside a settlement) | `WorldData`, keyed by settlement and region ID | Exploration commands only | Yes (as deltas over generated or authored content) | Save file + seed |
| *Future:* exploration GameObjects (buildings, props, NPC avatars) | Exploration presentation | Streaming | Never | Derived from world data |

## Rules

- **Entity identity is a GUID**, never a name. Selection, lookups, events and save files all
  use `SettlementId` / `FactionId` / `EventId`. Names are display data and may repeat or change.
- **IDs are deterministic.** The generator and the scheduler draw GUIDs from seeded streams,
  so the same seed gives the same IDs. Only `WorldId` is random by default, because two
  worlds made from the same seed are still different worlds.
- **Presentation never holds mutable domain objects.** It receives snapshots and sends commands.
- **ScriptableObjects may hold configuration**, such as visual themes or tuning values, but
  never world state.
- **Unity scenes contain no world state.** A scene holds only the bootstrap and empty
  containers. Everything in the world is spawned from data at runtime.
- **One world for both perspectives (binding, ADR-0009).** Exploration mode reads the same
  `WorldData` through the same session and changes it only with commands. It never keeps a
  parallel copy of settlements, NPCs or resources, not even a cache that outlives a snapshot.
