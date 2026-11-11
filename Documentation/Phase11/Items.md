# Phase 11: items, loot, equipment, crafting and progression

This page describes the item world as built in Phase 11. The decisions are recorded in ADR-0050 to ADR-0055. The model and inventory are in `Systems/Inventory.md`, slots, condition and magic in `Systems/Equipment.md`, and shops in `Systems/Commerce.md`.

## What exists in a world

| Thing | Where it comes from | Saved |
|---|---|---|
| Item instances (pack, slots) | Character creation, shops, containers, crafting, gathering | Yes, with their state |
| The player's chest, one per settlement | `ItemGenesis` (at the tavern; about 150 lb) | Yes |
| A cache, one per settlement | `ItemGenesis`, filled once from `old_cache` (seeded by the world and settlement) | Yes |
| Gathering sites | `ItemGenesis`, from the settlement's production | Yes (stock and capacity) |
| Loot piles | `WorldEncounters.FillLoot` when a fight is won with foes slain | Yes (the oldest 30+ cleared) |
| A shop's particular goods | Items sold that keep their identity | Yes (24 per shop at most) |
| Work in progress | `StartWorkCommand` | Yes (what it holds, the settled check, when it is ready) |

## Loot (ADR-0053)

There are 15 tables:

| Table | Picks | What it gives |
|---|---|---|
| outlaw | 1 | coin, rations, ale, dice, an amulet; potion (10 %), old key (5 %) |
| outlaw_leader | 1 | more coin; potion (40 %), jewellery, an almanac, a sealed letter (15 %) |
| cultist | 1 | coin, an almanac, an amulet, rations |
| goblin | 1 | a few coins, dice, arrows, an old key |
| soldier | 1 | coin, rations; potion (25 %) |
| game_deer, game_boar, game_small, wolf, bear | all | venison, raw hide, antler (50 %), wolf pelt |
| vermin | — | nothing |
| spider | — | linen (its silk; 40 %) |
| undead | 1 | grave goods: coin, an old key, an amulet, a gold ring |
| giant | 1 | a hoard: coin, a gold ring, a greater healing potion, hides |
| old_cache | 2 | coin, potions, an almanac, jewellery, arrows, an old key; ring of protection and +1 dagger only where rare items are allowed |

Carried gear: bandits, highwaymen, captains, goblins, cultists, veterans, guards, skeletons and ogres leave their weapons. Bandits' and goblins' blades are worn or damaged, as used. Fights and caches allow items up to Uncommon.

## Crafting (ADR-0054)

| Recipe | Discipline | Needs | Tool | Station | Time | Check | Makes | On failure |
|---|---|---|---|---|---|---|---|---|
| Smelt an iron bar | blacksmithing | 2 iron ore, 1 charcoal | smith's tools | workshop | 2 h | Str DC 10 | iron bar | lose 50 % |
| Forge a dagger | blacksmithing | iron bar, timber plank | smith's tools | workshop | 4 h | Str DC 12 | dagger (Fine on 17+) | nothing lost |
| Make arrows | blacksmithing | iron bar, timber plank | smith's tools | workshop | 3 h | Dex DC 10 | 20 arrows | lose 50 % |
| Cure a hide | leatherworking | raw hide | leatherworker's tools | — | 4 h | Wis DC 8 | 2 cured leather | lose all |
| Stitch a leather jerkin | leatherworking | 3 cured leather | leatherworker's tools | — | 6 h | Dex DC 12 | jerkin (Fine on 17+) | lose 34 % |
| Make a sling | general | cured leather | — | — | 1 h | Dex DC 5 | sling | nothing lost |
| Carve a quarterstaff | woodworking | 2 timber planks | carpenter's tools | — | 2 h | Dex DC 8 | quarterstaff (Fine on 13+) | lose 50 % |
| Sew a traveller's cloak | tailoring | 3 linen | weaver's tools | — | 4 h | Dex DC 10 | cloak | lose 34 % |
| Brew a potion of healing | alchemy | 3 silverleaf, a glass vial | alchemist's supplies | shrine | 4 h | Int DC 13 | potion of healing | lose all |
| Cook a hunter's stew | cooking | venison | cook's utensils | tavern | 1.5 h | Wis DC 8 | 2 stews | lose all |
| Smoke fish | cooking | 2 fresh fish, charcoal | cook's utensils | — | 2 h | Wis DC 8 | 2 smoked fish | lose 50 % |

The check is d20 + ability modifier + proficiency bonus when proficient with the tool. It is rolled when the work starts, from the world's dice seeded by the job's key. Reloading before collecting cannot change it.

- The work holds the materials until it is collected or abandoned.
- "Wait until done" passes world time, so the simulation lives through it, and then collects.
- Abandoning returns everything held.

## Gathering

Every settlement has hedgerows (silverleaf, a herbalism kit, 90 minutes per stint of 2). A settlement that logs has deadfall timber; one that mines has an iron seam (a miner's pick); one that fishes has fishing water (fishing tackle).

- Each site has stock and capacity (two-thirds in a hamlet).
- A stint takes stock when it starts and gives it back if abandoned.
- The weekly `GatheringSystem` restores part of each site's stock.

## Repair and attunement

- **Mending yourself:** an hour per step of condition below Good, with the craft's tools (smith's for metal, leatherworker's for light armour, carpenter's for wooden weapons and bows), DC 10 + 2 × steps. Success leaves it Good; failure leaves it as it was.
- **At an armourer:** 15 % of base value per step, at once.
- **Attunement:** an hour (a short rest) with the item on the person, three at most. A cursed item binds; a shrine lifts the curse for 60 coin.

## Screens

| Screen | What it does |
|---|---|
| Hub, Inventory | Load bar; the rules' AC and attack; search, group filters, sorting; state badges; Inspect with a rules-engine comparison; equip (rings: right hand; Light weapons: off hand), use, attune, split, store, drop |
| Hub, Equipment | Eleven slots with what is in each, unequip, inspect, attune and end attunement; the AC breakdown and attack |
| Hub, Weapons / Clothing & armour | Inventory groups |
| Hub, Crafting & work | The current work (wait until done, abandon); recipes by discipline with materials, tool, station, DC and chance; gathering sites; mending; attunement; recent outcomes |
| Hub, Storage | Containers within reach (the chest first when opened in the street), loot piles; take, take all, coin in and out |
| Street | The chest by the tavern door, the cache's flagstone by the square: walk up and press E |
| Trade window | Particular goods, Mending; offers that show condition |
| Combat panel | After a won fight, "Look over what they left", which opens Storage on the loot |
