# Persistence

Implementation: `Assets/Realmkeeper/Runtime/Infrastructure/Persistence/`.
Port: `Realmkeeper.Application.Ports.IWorldStore`.

## Files

- One file per slot: `<root>/<slot>.rkworld.json`. In Unity, `root` is meant to be
  `UnityEngine.Application.persistentDataPath + "/Saves"` (wired up in the Presentation stage).
- Slot names may use only `[A-Za-z0-9_-]` and be at most 48 characters long. Anything
  else, such as `../x`, `a/b` or `C:`, is refused. One rule, `Application.SaveSlot`, is used by
  the store, the save and load commands, the session host and the Unity runtime.
- **Save slots.** `SaveSlot.Default` is `autosave`, the slot every earlier build used, so
  existing saves keep loading. `RealmkeeperRuntime.ActiveSlot` is the one slot both the map and
  exploration views save to and load from:
  - `SelectSlot(name)` changes it;
  - `Load(name)` selects the slot when the load succeeds;
  - an invalid name is refused (`ValidationFailed`) and leaves the active slot unchanged.

  Loading a slot that has no save returns `NotFound`, and the current world stays. There is
  no save browser yet.
- Writes are atomic: the file is written to `<file>.tmp`, then swapped in with
  `File.Replace`, or `File.Move` for a new slot. A failed write leaves the previous save
  intact.
- Files over 32 MB are refused on load.

## Schema history

| Version | Adds | Migration |
|---|---|---|
| 1 | Phase 1 world | — |
| 2 | Phase 2 economy, trade routes, shipments, structured events | `MigrationV1ToV2`, then the economy is derived from terrain on load |
| 3 | Regions, settlement `regionId`, player character, named residents, labour focus (ADR-0009) | `MigrationV2ToV3` (structural), then `WorldPopulationInitializer` with the generation streams |
| 4 | `tradeRoutes[].isSuspended` | `MigrationV3ToV4`: sets `false` (every route in an older save was trading); nothing else changes. A test checks that a v3 copy of a save loads identical to the v4 original. |
| 5 | Faction type, status, dates, leader, influence, treasury, presence and goals; `relations`; `claims`; settlement `administratorFactionId`; event `factionIds` | `MigrationV4ToV5` adds the empty arrays and lists `realmkeeper.politics` before the chronicle system for worlds running the standard systems. After mapping, `PoliticalInitializer` builds the political structure once: existing factions keep their IDs and names, and the new ones get the IDs a fresh world of that seed gets. No settlement, resource, route or event changes. A world with no factions gets none. A test checks that a v4 copy of a save loads identical to a fresh v5 world of that seed. |
| 6 | Named people: sex, birth date, tier, personality, skills, condition, residence, workplace, current location, weekly schedule, goals, memories, knowledge, deceased; world `locations` and `npcRelations`; event `npcIds` | `MigrationV5ToV6` adds the empty arrays and lists `realmkeeper.npcs` before the political system. After mapping, `NpcGenesis.EnsurePeople` runs once for people still unassigned. It derives locations and each person's state from the seed and IDs, adds present-day knowledge of their own settlement, and adds only the ties present facts imply (shared home, workplace, faction leader). It adds no memories and no events. A test checks that a v5 copy of a save loads identical to a fresh v6 world of that seed, and that loading a v6 save never re-initialises anyone. |
| 7 | World `minuteOfDay`; player species, age, background, description, appearance, purse, fed, inventory, equipment, `isOutfitted`; world `shops` (keeper, location, categories, hours, stock, totals, last restock) and `transactions` (ledger, last 500); NPC `playerRegard` | `MigrationV6ToV7` adds `minuteOfDay` 480 (08:00), empty `shops` and `transactions`, and lists `realmkeeper.shops` after the NPC system. After mapping, `CommerceGenesis` runs once: shops for settlements that have none, kept by existing residents and stocked from their own settlement's stores (the keeper pays the treasury, materials are booked as consumed), and the player's starting kit if `isOutfitted` is false. It invents no trades, no events and no history. A test checks that a v6 copy of a save loads identical to a fresh v7 world of that seed (pre-v7 saves are built with generation's pre-commerce mode), and that loading a v7 save never re-initialises. |
| 8 | Player `rpg`: creation state (`Legacy`, `Pending`, `Complete`), title, history, species, background, class and subclass IDs, level, experience, ability method, six base scores, creation choices, current and temporary hit points, resource uses, `startingEquipmentGranted`, creation date. Derived values (modifiers, maximum HP, AC, bonuses) are never saved. | `MigrationV7ToV8` adds `player.rpg = { state: "Legacy", level: 1, experience: 0 }` and nothing else: no species, class, scores, history or items are invented, and the player's ID, name, look, inventory, equipment, purse, location and pose are untouched. New worlds start `Pending`. Creation later grants the class and background kit exactly once (ADR-0023). A v8 save without an `rpg` record is refused as corrupt. Tested in .NET (v7 documents of every older seed path) and in PlayMode (a real Phase 5 save rewritten to v7 and loaded in the shipped scene). |

## Format (schema v2, Phase 2)

v2 adds, per settlement, an `economy` object (specialization, production assignments, storage
capacity, last-day ledger, per-source output, shortage statuses, lifetime production,
markers). At world level it adds `tradeRoutes` and `shipments`. Events gain `settlementIds`,
`data` and `importance`. The `ore` resource is `iron`, and `foodProductionPerDay` is gone.

**v1 → v2 migration** (`MigrationV1ToV2`, always registered): renames `ore` to `iron`, drops
`foodProductionPerDay`, and replaces the Phase 1 system list. Economy and trade are absent in
v1. On load, `WorldSession` asks the generator to initialize them from the regenerated
terrain, giving the same result as a fresh world of that seed. Text-only v1 events keep their
wording. This is tested by building a real v1 document and loading it through the session.

The v1 layout, for reference:

```json
{
  "format": "realmkeeper.world",
  "schemaVersion": 1,
  "savedAtUtc": "2026-11-08T17:24:04.0000000Z",
  "world": {
    "worldId": "…guid…",
    "worldName": "Emberfell",
    "seed": 9001,
    "generatorVersion": 1,
    "currentSimulationDate": "0412-04-16",
    "settings": { "simulationSpeed": "Normal", "startingDate": "0412-03-01",
                  "enabledSimulationSystems": ["realmkeeper.food", "realmkeeper.wellbeing"] },
    "settlements": [ { "settlementId": "…", "name": "Oakgate", "settlementType": "Town",
                       "positionX": 114, "positionZ": 68, "population": 2188,
                       "prosperity": 61, "stability": 64, "factionId": "…",
                       "foodProductionPerDay": 143, "isInFoodShortage": false,
                       "resources": [ { "resourceTypeId": "food", "quantity": 10512 } ] } ],
    "factions": [ … ], "npcs": [ ], "historicalEvents": [ … ]
  }
}
```

- IDs are stored in GUID `D` format, enums as names (never numbers), and dates as `YYYY-MM-DD`.
- What is saved: identity, seed, generator version, schema version, date, settings,
  settlements with inventories and shortage state, factions, NPCs and the full event history.
- What is not saved: terrain. It is rebuilt from `seed` + `generatorVersion` on load
  (ADR-0005), then the settlements are checked to still sit on valid land.

## Load outcomes

| Status | When |
|---|---|
| `Success` | Parsed, migrated, mapped and validated |
| `NotFound` | No file, or an illegal slot name |
| `Corrupt` | Empty, malformed JSON, wrong or missing format marker, missing or non-integer `schemaVersion`, wrong value types |
| `UnsupportedVersion` | `schemaVersion` newer than this build, or no migration path from an older one |
| `Invalid` | Bad GUID, unknown enum name, impossible date, duplicate resource stack, or any `WorldValidator` error (negative population, duplicate IDs, dangling references, …) |

`WorldSession.Execute(LoadWorldCommand)` additionally rejects saves whose generator version
this build can't rebuild. **Any failed load leaves the active world untouched.**

## Versioning

- The domain model and the file layout are separate (`SaveFileDtos.cs`), so renaming a
  domain property does not silently break old saves.
- To change the layout: bump `WorldData.CurrentSchemaVersion`, then register an
  `ISaveMigration { SourceVersion = old; Migrate(JObject) }` with `JsonWorldSerializer`.
  Migrations are chained (v0 → v1 → v2 …) and work on the raw JSON tree, so old DTO classes
  never need to be kept. The chain is covered by a test.

## Security

`TypeNameHandling.None` (a save file can never choose which .NET type gets created),
`MaxDepth` 32, a size cap and slot-name filtering.
