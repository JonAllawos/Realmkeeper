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
| 9 | Character `rpg.rulesVersion`, `rpg.experienceLog` (key, source, source ID, amount, date, reason), `rpg.advancements` (level, date, hit points gained, summary); level choices under `lv{level}:…` keys in the existing `choices`; subclass in the existing `subclassId`; resources now include `hit_dice` and the warlock's `pact_slots`. Maximums and every level-derived value are never saved. | `MigrationV8ToV9` adds `rulesVersion` (6 for a created character, so its level-1 choices introduced by rules v7 stay open until its next level-up; 7 otherwise), an empty `advancements`, and an empty `experienceLog` (any experience already held becomes one `migrated:v8` entry so the ledger adds up). For a created character it adds full `hit_dice` (one per level) and renames the warlock's `spell_slots_1` to `pact_slots`, keeping its uses. Nothing else changes and nothing is invented (ADR-0028). On load the ledger must equal `experience`, the level must be allowed by it, and every level's choices must be valid. Tested in .NET (v8 documents of fighters, druids and warlocks; v7 Phase 5 documents) and in PlayMode (real saves rewritten to v8, then advanced in the shipped scene, saved and reloaded). |
| 10 | `world.encounters` (each: ID, name, kind, lethality, settlement, status, outcome, winning side, round, turn index, order, combatants with vitals, position, conditions, turn resources and initiative, difficult terrain, start and end dates, the dice seed as a decimal string, the roll counter, the action sequence, the bounded log, quest and world-event references); `world.structures` (ID, definition, name, settlement, location, shop, owner, faction, layout key, condition, establishment date); character `rpg.deathSaveSuccesses`, `rpg.deathSaveFailures`, `rpg.isStable`, `rpg.isDead`. Derived combat values (AC, attacks, speed) and structure definitions are never saved. | `MigrationV9ToV10` adds an empty `encounters` list and the default dying state (0, 0, false, false); hit points, inventory, equipment and everything else are untouched. After mapping, a save older than v10 gains its settlements' structures from its own locations and shops (`StructureGenesis`, deterministic, once): exactly those a new world of the same seed would have. Validation covers encounters (one active, consistent order, vitals and conditions in range, no resident twice) and structures. Tested in .NET (v9, v8 and v7 documents; an active fight saved, loaded byte-identical in its world section and played on identically; tampered documents refused) and in PlayMode (a fight saved mid-way in the shipped scene, reloaded, its panel reopening and play continuing). |
| 11 | Character `rpg.spellcasting` (prepared spell IDs, the preparation window `preparationChanges` −1/0/1, `lastLongRestEnded`, a bounded `restLog` of key, kind, end and summary); spell picks in the existing `choices` (`class:cantrips`, `lv{L}:spells`, `class:spellbook` …). In encounters: `effects` (instance, kind, source, target, spell or feature, values, expiry, concentration link) and `nextInstanceId`; per combatant `resources` (creature slots), `concentration` and `formCreatureId` (Wild Shape); per condition `instanceId`, `repeatSave`, `saveDc`, `saveAbility`, `escapeByCheck`. Spell and feature definitions, DCs, attack bonuses and always-prepared lists are derived, never saved. | `MigrationV10ToV11` adds an empty `spellcasting` record (nothing prepared, the window open, no rest yet) and sets `rulesVersion` 8 only for a character still being created; a finished character keeps its version, so its spell picks are offered later, never invented (ADR-0038). Encounter fields default when absent. Validation refuses spells not on the class list or above the castable level, too many prepared, a bad window, effects on no one, unknown effect kinds and a finished fight still holding effects. Tested in .NET (a Phase 8 document migrated with hit points, experience, resources, structures and chronicle unchanged; a fight with concentration and effects saved and loaded identically; tampered documents refused) and in PlayMode (rest state through save and reload in the shipped scene). |
| 12 | Phase 10. Named people's `character` (archetype, occupation, gear, purse, optional full sheet, sheet date); world `creaturePopulations` (ID, region, creature, habitat, centre, range, count, capacity, deaths, draws, last update), `creatures` (instance ID, creature, name, origin population, state, hit points, first seen, died on, last encounter), `livestock` (herd ID, settlement, creature, count, pasture key, keeper, last update) and `settlementLayouts` (identity, terrain profile, waterways, districts, roads, bridges, buildings, fields, landmarks, features, wall, gates, arrival; lines as flat coordinate lists; seeds as strings); encounters' `pendingReaction`, `nextReactionId`, `origin`; combatants' `anchor` and `creatureInstanceId`; turn `hasStart`/`startX`/`startY`. | `MigrationV11ToV12` adds the empty lists and enables `realmkeeper.fauna`. On load (after the region map is regenerated) `LivingWorldGenesis.Ensure` adds, once and deterministically, what the world lacks: the posts its places call for, NPC records and sheets, wildlife and herds, settlement plans and herds' pastures (ADR-0048). A plan is never regenerated: a saved plan loads exactly or, if invalid, refuses the load. Tested in .NET (v11 documents upgraded through a session, saved and loaded again unchanged; corrupt plans and records refused) and PlayMode (save and load inside a planned town). |
| 13 | Phase 11. Item instances' own state on every stack (`condition`, `quality`, `charges`, `attuned`, `bound`, `provenance`, `acquiredAt`; written only when different from a fresh instance); shops' `goods` (particular instances); world `containers` (key, kind, name, settlement, layout key, access, deposits, items, coin, capacity, source key, created, opened), `workJobs` (key, kind, settlement, recipe, site, stack, held items, start and ready minutes, the check's roll, total and DC, success, output and its make, outcome, done), `gatheringSites` (key, settlement, kind, name, item, tool discipline, available, capacity, per week, yield, minutes) and `itemCatalogVersion`. Item definitions, recipes and loot tables are data, never saved. | `MigrationV12ToV13` adds the empty lists, catalog version 1, empty goods for each shop, and enables `realmkeeper.gathering` after the fauna. Absent instance state reads as a fresh instance (good, common, no charges, not attuned). On load `ItemCatalog.Upgrade` follows any renamed item IDs and records the current catalog version; `ItemGenesis` (with the living world) adds each settlement's chest, cache and gathering sites once. Validation: one holder per instance across pack, slots, containers, goods and work; possible states (charges within the maximum, bound only when attuned, at most three attunements); one piece of work in progress; sites within capacity. Tested in .NET (a v12 document migrated and gaining the item world once; every state round-tripped exactly; corrupt states and unknown kinds refused) and PlayMode (the chest's contents through save and reload in the shipped scene). |
| 14 | Phase 12. World `quests` (instance ID, definition and version, state, bindings, source key, created, discovered, offered, accepted, closed and expiry minutes, objective progress, decisions, variables, outcome, rewards claimed, applied consequence keys, failure reason, history, tracked) and `conversation` (key, person, dialogue, node, quest, start, turn, variables, transcript); the player's `social` (faction standings, settlement reputations, notoriety, the keyed reputation ledger, known places, visited settlements, discoveries, rumours, decisions, one-time keys); people's regard gains `trust`, `respect`, `fear`, `gratitude`, `hostility`; settings gain `content` (tag to presentation); the knowledge topic `PlayerDeed`. Quest and dialogue definitions are data, never saved. | `MigrationV13ToV14` adds the empty quest list and social record, and enables `realmkeeper.quests` after gathering; absent regard dimensions read as 0 and absent content rules as "show". On load the living-world step adds the authored quests and today's emergent ones once (`QuestGenesis`, with genesis Chronicle notes). Validation: unique instances of known definitions with a giver, progress matching the definition, completed only with rewards given (and never the reverse), closed with a time, tracked only while open; the conversation with a living person at a real node; standings unique and in range; no ledger key twice. Tested in .NET (a v13 document gaining the quest world once; an exact round trip including a conversation in progress, resumed in a fresh session; corrupt states refused) and PlayMode (a completed quest, its rewards and regard after save and reload in the shipped scene). |
| 15 | Phase 13. World `places`: identity, definition, category, archetype, parent kind and id, region, settlement, Phase 4 location and layout key, exit pose, map cell, seed (as a string), generator version, and the saved layout (floors, rooms, connections with door state, key, lock and find DCs, guarding group, features with data, inhabitant groups and members); what the player knows, discovery and visit times, visits, holding faction, threat, explored rooms, door states, found secret ways, feature states (flags, attempts), group states (state, encounter, alerted), variables and notes. World `escorts` (person, state, place, room, destination, quest, time). The player gains `placeId`, `placeFloor`, `placeRoom`; containers gain `placeId`, `featureId`; fights gain `blocked` squares and origins gain `placeId`, `groupId`, `room`. | `MigrationV14ToV15` adds the empty place and escort lists. On load the living-world step adds the wilderness places once (`PlaceGenesis`: layouts, containers, the captive, the garrison, the rescue quest). Layouts are saved and never regenerated. Validation: unique places with valid layouts (the full `PlaceValidator`), state naming only the place's own rooms, doors, features and groups; the player on a real floor of a real place; one living identity per escort, no person both posted and held; at most three followers; place containers at real features. Tested in .NET (a v14 document gaining its places once; an exact round trip inside a place after a rescue; corrupt states refused) and PlayMode (saved and reloaded inside the mine with a follower). |
| 16 | Phase 14. World `crimes` (ID, kind, status, minute, settlement, place and room, victim, detail, items taken and coin, bounty, discovered and settled minutes, witnesses with person, recognised, description and how they saw) and `nextCrimeNumber`. Character `rpg.conditions` (type, level, source, reason, applied and expiry minutes, remedies, escape DC and tries). Places gain `bridges` (connection → Intact/Damaged/Destroyed) and each group's `woken`. Fights gain `doors` (ID, name, open, locked, squares) and `lowCover` (square, name); conditions in a fight carry their `lastingKey`. Trade resource flows gain `lostOnRoad`. `GeneratorVersion` 2 (bridges, fire rooms, keys, waking groups). | `MigrationV15ToV16` adds the empty crime list and `nextCrimeNumber = 1`. On load `PlaceGenesis.EnsurePhase14` adds this world's three to five new places once (chosen by the world's own context, the same for the same seed) with their quests; the saved Phase 13 places are untouched. Validation: crime IDs present and unique, bounties never negative and zero once settled, a seen crime never recorded as undiscovered, witnesses real and named once, `nextCrimeNumber` at least 1; bridge states only for bridges; no condition twice, no fight-only condition outside a fight, exhaustion 1–6; followers in real rooms, at most three. Tested in .NET (a v15 save with progress gaining the new places exactly once and keeping everything; an exact round trip of every new field; doors and low cover mid-fight) and PlayMode (conditions and crimes surviving a reload in the scene). |
| 17 | Phase 15. World `party` (members: person, role Active/Reserve, mode Follow/Wait, order, follow distance, joined and temporary-end minutes, where they wait: settlement or place and room; leader) and `companions` (person, standing InParty/Dismissed/Departed/TermEnded, loyalty 0–100, approvals with key, delta, reason and minute, times recruited, when and why they left, items granted, personal quest). A person's `character` gains `ownsItems`, `inventory` and `equipment` (item instances, as the player's). Fight logs may hold `Reason` entries. The party system `realmkeeper.party` runs after quests. | `MigrationV16ToV17` adds an empty party and no companion records, and enables the party system after the quest system. People keep their gear by name until they join. Validation: each member once, alive, a known person, never also escorted; at most 3 active and 3 in reserve; the leader a member; a waiting member waits somewhere; a record for every member that says so; unique records, approvals counted once; a person's items held once (checked with every other holder) and worn in slots they fit; no items without `ownsItems`. Tested in .NET (an exact round trip of the whole party; a v16 save loading with an empty party and the system on; the validator catching duplicates, double ownership and mismatched records). |

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
