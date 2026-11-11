# Items and inventory

Phase 5 introduced items (ADR-0016). Phase 11 rebuilt them into an item model with instances, holders, loot, crafting and progression (ADR-0050 to ADR-0055). This page describes the system as it is now.

## Definitions and instances (ADR-0016, ADR-0050)

**Definitions** (`ItemDefinition`, catalog `ItemCatalog`) are data. They are fixed in code and never saved. Each one has:

- identity: ID, name, description, category, subcategory, rarity, tags, lore, source (SRD 5.2.1 or Realmkeeper original) and version;
- value, weight, stacking, slot, two-handedness and visual;
- typed profiles:
  - weapon or armour data (SRD 5.2.1);
  - `ConsumableProfile`: healing dice, nourishment, the action it takes in a fight;
  - `MagicProfile`: weapon, AC and save bonuses, rule effects, attunement, charges and recharge, spell, curse;
  - whether it wears;
  - the tool discipline;
  - the crafts it is a material for;
  - what ammunition it is;
  - container capacity;
- the settlement materials it is made from, and the shop categories that sell it.

`ItemCatalog.Validate()` checks the catalog: unique IDs, sensible stacks, slots that suit the category, combat data where combat needs it, and every reference known. Catalog version 2 (Phase 11) has **114 items**:

| Category | Count | Category | Count |
|---|---|---|---|
| Weapons | 31 | Tools | 14 |
| Armour | 14 | Materials | 9 (plus the iron bar and timber plank) |
| Shields | 3 | Food & drink | 8 |
| Clothing | 7 | Trade goods | 4 |
| Headwear | 3 | Spellcasting foci | 4 |
| Jewellery | 3 | Ammunition | 3 |
| Wondrous items | 5 | Potions | 2 |
| Books, keys, quest, containers | 1 each | | |

By rarity: 102 common, 9 uncommon, 3 rare. 11 are magic items. 66 are SRD 5.2.1 equipment and magic items; the rest are Realmkeeper originals (food, trade goods, materials, keys, letters).

**Instances** (`ItemStackData`) are what exists in the world. Each instance has:

- a stack ID, the item and a quantity;
- its own **condition** (Pristine, Good, Worn, Damaged, Broken), **make** (Poor, Common, Fine) and **charges**;
- whether it is **attuned** or **bound** by a curse;
- its **provenance** ("Made by you", "Taken from a bandit") and when it was acquired.

`ItemStacks.Create` makes every new instance. Two stacks merge only when they are alike in every respect and neither is attuned or bound.

## Holders: exactly one (ADR-0051)

Every instance is held by exactly one of these:

- the player's pack;
- an equipment slot;
- a container;
- a shop's particular goods;
- work in progress.

Shop stock of plain items is still a count. `WorldValidator` refuses any stack ID that appears twice, and every command's result is validated before commit.

**Containers** (`ContainerData`) have stable keys and persistent contents:

| Kind | Key | Where, and who may use it |
|---|---|---|
| Personal | `chest:{settlement}` | At the tavern door. The player may take from it and put into it (about 150 lb). |
| Cache | `cache:{settlement}` | Under a loose flagstone by the square. Filled once from `old_cache`. The player may take only. |
| Loot | `loot:encounter:{id}` | What a fight left behind (ADR-0053). The player may take only. |
| SettlementStore, Chest, Camp | — | Data kinds, prepared for later phases. |

Transfers are commands (`TakeFromContainer`, `PutInContainer`, `TakeAllFromContainer`, `MoveContainerCoin`, `OpenContainer`).

- A refusal changes nothing: another settlement, no deposits, too heavy, bound or attuned.
- Take-all is all or nothing.
- Taking part of a stack makes a new instance; the part left keeps its ID.

## Inventory commands

| Command | What it does |
|---|---|
| `EquipItemCommand(stack, slot?)` | Equips one item from the stack. A ring may go on either hand; a Light weapon may go in the off hand. Displaced items go to the pack. Refused while a curse binds what would come off. |
| `UnequipItemCommand(slot)` | Puts the item back in the pack. Refused for a bound item. |
| `UseItemCommand(stack)` | Drinks a potion (healing rolled with the world's dice; refused when unhurt) or eats food (refused when not hungry). |
| `SplitStackCommand` / `CombineStacksCommand` / `DiscardItemCommand` | As in Phase 5. A split keeps the instance's state. |
| `EndAttunementCommand` / `LiftCurseCommand` | End attunement, or have a shrine's priest lift a curse (60 coin). |
| `StartWorkCommand` / `CollectWorkCommand` / `CancelWorkCommand` | Craft, gather, mend or attune (ADR-0054). |

Every command refuses items it cannot find, equipped items where the pack is meant, and non-positive quantities.

## Weight

Carrying capacity is Strength × 15 lb, doubled by Powerful Build (SRD 5.2.1).

- Over capacity, the character's Speed is 5 ft. This shows on the sheet, in combat and in the exploration pace.
- The most the pack (with worn gear) may weigh is twice the capacity. Nothing heavier can be picked up, bought, taken or collected.
- The Phase 5 limit of 40 stacks and its separate 25 kg pace curve are gone. A 200-stack safeguard remains only against weightless clutter.

## Screens (ADR-0055)

The Player Hub's **Inventory** has:

- the load bar (capacity and limit);
- the rules' AC and attack;
- search, group filters with counts, and sorting (category, name, weight, value, recent; up or down, with stable ties);
- each row's state badges;
- an **Inspect** panel with a rules-engine **comparison** against what is worn;
- per-row actions: equip, right hand, off hand, use, attune, split, store, drop.

**Weapons** and **Clothing & armour** are inventory groups. **Equipment**, **Crafting & work** and **Storage** are described in `Equipment.md` and `Documentation/Phase11/Items.md`.
