# Phase 11 pre-flight: Inventory, Loot, Equipment, Crafting & Item Progression

Date: 2026-11-11. Inspected before any Phase 11 change.

## 1. Git and baseline

| | |
|---|---|
| Branch | `feature/phase-0-1-foundation` (continued: no reason to branch) |
| HEAD | `1707805` (Phase 10, local); `27a5261` (Phase 9, local) before it; `origin` at `2fe83eb` |
| Ahead of origin | 2 commits, not pushed |
| Working tree | Only `ProjectSettings/GraphicsSettings.asset` is modified: the owner's change, preserved and never staged |
| Save schema | v12 (`MigrationV11ToV12`, Phase 10); migrations v1→v12 chained |
| .NET | 552 passed, 0 failed, 0 skipped |
| Unity EditMode | 552 passed, 0 failed, 0 skipped |
| Unity PlayMode | 74 passed, 0 failed, 14 skipped (capture sets without `RK_CAPTURE_DIR`) |

All three suites were run at `1707805` for the Phase 10 report (Phase10/Testing.md). Phase 11 starts from them.

## 2. What exists

- **Definitions.** `ItemDefinition` (`Domain/World/Items.cs`) holds:
  - ID, name, description, category (10 values), quality (Common, Fine), value, grams, stacking, slot, two-handed,
    visual;
  - a Phase 5 damage and protection number;
  - settlement materials and the shops that sell it.
  The SRD 5.2.1 combat data is attached separately by item ID (`ItemCombat.Weapons`, `ItemCombat.Armor`).
  The catalog (`ItemCatalog.cs`) has about 50 items: 13 weapons, 2 shields, 3 headwear, 12 clothing and armour
  pieces, 3 foods, 4 trade goods, 4 foci and 8 tools.
- **Instances.** `ItemStackData` holds only a stack ID, an item ID and a quantity. There is no condition, no
  provenance, no charges and no ownership beyond the list that holds it.
- **Holders:**
  - the player's `Inventory` (a list, capped at 40 stacks: an arbitrary limit);
  - the player's `Equipment` (9 slots);
  - shop `Stock`, held as item ID and quantity, with no instance state;
  - NPC `Gear` (Phase 10), held as item IDs.
  There are no containers.
- **Commands:**
  - equip and unequip (two-handed rules);
  - split, combine, discard;
  - use (food only);
  - buy and sell. These check the shown price, keeper funds and stock, record transactions and the chronicle,
    and remember trades.
  Each command runs in the session's working copy (atomic: a failure discards it).
- **Rules integration.** `CharacterRules.LoadoutOf(player)` passes only the body armour and shield IDs. AC follows
  SRD 5.2.1 (armour, Dexterity cap, shield, training, Strength requirement). Attacks come from the main-hand item's
  weapon profile (`CombatStats.BuildSheetAttacks`). Speed and carrying capacity have effect kinds
  (`CarryCapacityPercent`), but exploration pace uses fixed 25 and 50 kg thresholds that the rules do not see.
- **Loot.**
  - Phase 10 creatures carry `LootProfile` data.
  - `WorldEncounters.Resolve` adds loot straight to the player's pack, with no container and no choice.
  - There are no loot tables, containers or caches.
- **Crafting, gathering, repair, condition, rarity, magic items, attunement:** none.
- **UI.**
  - The Hub has Inventory, Equipment, Weapons, Clothing & armour and Possessions tabs: lists with
    Equip/Eat/Split/Combine/Drop buttons, no search, sort, filter, inspection or comparison.
  - The trade window has buy and sell lists with prices.

## 3. Problems found

1. **No definition and instance separation** beyond quantity: a worn sword and a new one are indistinguishable,
   and selling loses everything about the object.
2. **An arbitrary 40-stack pack limit**, while weight (the rules' carrying capacity) does not limit anything.
3. **Two encumbrance notions:** the rules' `CarryCapacityPercent` and Hub's fixed grams thresholds disagree.
4. **The Phase 5 `Damage` and `Protection` numbers** are shown in the Hub beside the real SRD numbers.
   Misleading: the Equipment tab says "prot 3" for a shield that gives +2 AC.
5. **Loot is applied directly:** no container, no choice, not inspectable after the fight.
6. **Shop stock cannot hold a particular object** (condition, enchantment).

## 4. Plan (built on the real code; nothing parallel)

- **Model (ADR-0050).**
  - Extend `ItemDefinition` with typed profiles, keeping `Weapon` and `Armor`:
    - `Consumable` (food, potions: healing dice, nourishment, action cost);
    - `Magic` (bonuses, typed rule effects, attunement, charges and recharge, a spell, a curse);
    - `Wear` (condition applies);
    - `Tool` (discipline);
    - `Material` (discipline);
    - `Container`.
  - Add rarity, subcategory, tags, lore, provenance and a catalog version.
  - Make `ItemCategories` a metadata table (name, group, order), so the UI never hard-codes categories.
  - Extend `ItemStackData` with condition, quality, charges, attunement, binding, provenance and acquisition date.
  - Stacks merge only when their instance state matches.
- **Holders and transfers (ADR-0051).**
  - Persistent `ContainerData`: personal chest, settlement store, cache, loot, camp, with stable keys and contents.
  - A single transfer service used by every move (pack ↔ container ↔ shop), validated and atomic in the
    command's working copy.
  - The pack's limit becomes weight: the SRD carrying capacity (Strength × 15 lb). Over capacity, Speed falls to
    5 ft. Past twice capacity, nothing more can be picked up. One encumbrance rule, in `CharacterRules`.
- **Rules integration (ADR-0052).**
  - `Loadout` gains item instances' condition and magic.
  - Worn magic items contribute typed `RuleEffect`s through the same collection as features: AC, saves, an
    ability minimum, darkvision, advantage. They apply only when attuned where attunement is required.
  - Weapon bonuses and condition enter `CombatStats`. A broken weapon cannot attack.
  - Condition changes at defined rule points: a natural 1 wears the weapon; a critical hit taken wears the armour
    or shield.
  - Potions are drunk as a bonus action in combat and through the Hub outside it.
  - Wand charges cast their spell through the spell engine's free-cast path and recharge at dawn.
- **Loot (ADR-0053).**
  - Data-driven, seeded tables with guaranteed, weighted, quantity, rarity-band and conditional entries.
  - Humanoids drop their actual loadout; beasts drop materials.
  - Encounter loot lands in a persistent loot container keyed by the encounter, and is taken through a window.
  - A cache per settlement.
  - Generation and claiming are idempotent.
- **Crafting, gathering and repair (ADR-0054).**
  - Recipes: materials, tool, proficiency, station (a settlement place), time, a check, quality and failure rules.
  - Persistent work jobs: materials are held at the start; the result comes once the clock has passed.
  - Gathering sites per settlement, limited and replenished weekly by a gathering system.
  - Repair at a smith (a priced service) or by hand with tools and materials.
- **Merchants.**
  - Condition-adjusted prices.
  - A particular object sold to a shop stays that object in its stock (instance goods).
  - Keeper funds limit sales (existing).
- **UI (ADR-0055).** Inventory, inspection, comparison, equipment, loot, container, crafting and gathering, and
  repair, all through application queries and commands.
- **Schema v13** with a migration from v12. Existing stacks gain default instance state. Chests, caches and
  gathering sites are added once on load.

ADRs: 0050 (item model), 0051 (holders and transfers), 0052 (equipment and magic in the rules), 0053 (loot),
0054 (crafting, gathering, repair), 0055 (item UI and queries).

## 5. Owner decisions (recorded, not taken)

- **Bible / Product Foundation scope:** still unresolved (Phase 10 recorded it).
- **Magic items:** taken only from the SRD 5.2.1 list, with original descriptions. The owner may want others.
