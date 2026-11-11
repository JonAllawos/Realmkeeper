# Phase 11 report: Inventory, Loot, Equipment, Crafting & Item Progression

Date: 2026-11-11. Branch `feature/phase-0-1-foundation`, on top of Phase 10 (`1707805`). Committed locally only; not pushed.

## A. Phase summary

Phase 11 turns items from Phase 5's stacks (an ID, an item and a count) into an interconnected system:

- **Item model.** Definitions are data. Each instance carries its own condition, make, charges, attunement, curse binding and provenance (ADR-0050). The catalog grows from 50 to **114 items**, 66 of them SRD 5.2.1 equipment and magic items.
- **Holders and transfers.** Every instance has exactly one holder: pack, slot, container, a shop's particular goods, or work in progress. Transfers are atomic commands, and the pack is limited by SRD carrying capacity, not 40 stacks (ADR-0051).
- **Equipment and magic in the rules.** Condition, magic bonuses, attunement, curses, wands with charges, ammunition, rings on either hand and off-hand Light weapons all act through the one rules engine (ADR-0052).
- **Loot.** It is deterministic, filled once into a container per fight from what the foes carried and 15 loot tables, and bounded by rarity. Each settlement has a cache (ADR-0053).
- **Crafting, gathering, repair and attunement.** Each is work that holds its inputs, rolls a tool check when it starts, and takes world time. Gathering sites come from the settlement economy and recover weekly. An armourer mends for a price (ADR-0054).
- **Merchants.** Offers count condition and make. Distinct items keep their identity in a shop's particular goods and can be bought back.
- **UI.** The hub's Inventory has search, filters, sorting, inspection and a rules-engine comparison. Equipment shows eleven slots; there are new **Crafting & work** and **Storage** sections. The chest and cache stand in the street. The trade window gains particular goods and mending, and the combat panel offers the loot (ADR-0055).
- **Schema v13** with migration. v12 saves gain the item world once.

Verification:

- .NET: **574 passed**, of which 22 are new.
- Unity EditMode: **574 / 574 passed**.
- Unity PlayMode: **80 passed, 16 skipped (capture sets), 0 failed**, including 6 new tests covering 17 UI flows.
- Mutation checks: **12 of 12 mutants caught** by tests.
- Screenshots: **13**, from the shipped scene.

## B. Starting Git state and Phase 10 integration

- Start: `1707805` (Phase 10), clean except the owner's `ProjectSettings/GraphicsSettings.asset`, which is preserved and not staged.
- The Phase 10 baseline was recorded in `Documentation/Phase11/PreFlight.md`: .NET 552, EditMode 552, PlayMode 74 passed / 14 skipped (capture sets), schema v12.
- The pre-flight audit found these problems, all fixed in this phase:
  - an arbitrary 40-stack pack limit;
  - two encumbrance notions (Phase 5's 25 kg pace curve beside the SRD capacity);
  - Phase 5 "protection" and "damage" numbers that did not match combat;
  - loot dropped straight into the pack;
  - shop stock with no room for distinct instances.
- Phase 10 is integrated rather than duplicated:
  - Encounter resolution now fills a loot container.
  - `LivingWorldGenesis` runs the item genesis.
  - NPC loot uses the bestiary's creature IDs.
  - Settlement production decides gathering sites.
  - Shops, keepers and location kinds (workshop, tavern, shrine) decide stations and services.

## C. Architecture and item data model

| Layer | Added or changed |
|---|---|
| Domain | `ItemModel.cs`: rarity, condition and its effects, the category table, consumable and magic profiles, carrying, weights, the `ItemStacks` factory and repair rules. `Items.cs` adds categories, slots (neck, two rings), the Poor make, and the instance's state. `ItemCatalog.Phase11.cs` has 64 new items, defaults, validation and the catalog upgrade. `Containers.cs`, `Loot.cs`, `Recipes.cs`, `WorldValidator.Items.cs`. Rules: `Loadout` carries conditions, weight and item effects; new `AbilityMinimum` and `SaveBonus` effects; carrying capacity and encumbrance in `CharacterRules`. Combat: `CombatEngine.Items.cs` (ammunition, wear, potions, wand charges), off-hand attacks, magic and condition in attack breakdowns. |
| Simulation | `ItemGenesis` (chests, caches, sites) and the weekly `GatheringSystem` (order 470). |
| Application | `ItemCommands.cs`: 12 commands, the weight limit, ID safety, `WorldSession.FinishWork`. `ItemQueries.cs`: the inventory, inspection, comparison, containers, the workshop, shop goods and repair quotes. Equip with slot choice and curses, potions outside fights, condition-aware selling, particular goods, wand recharge on a long rest, loot containers. |
| Infrastructure | Schema v13 DTOs, `WorldSaveMapper.Items.cs`, `MigrationV12ToV13`, the item genesis in the living-world step. |
| Presentation | `PlayerHubWindow.Items.cs`, `ContainerInteractable.cs`, `ExplorationController.Items.cs`; the trade window's goods and mending; the combat panel's loot button; styles. |

The definition/instance split, the single factory and the stack-ID safety are described in ADR-0050. One finding from this phase: `CommandContext.NextGuid` is deterministic per day and event count, so two commands on a quiet day could draw the same ID. The new tests found this. Every new item ID and work key now skips any value already held.

## D. Item catalog additions and counts by category

Catalog version 2: **114 items** (50 → 114).

| Category | Count | Added in Phase 11 |
|---|---|---|
| Weapons | 31 | club, light hammer, sickle, sling, battleaxe, flail, morningstar, warhammer, greatsword, maul, glaive, halberd, pike, longbow, heavy crossbow; +1 longsword, +1 dagger, Berserker Axe |
| Armour | 14 | padded, hide, scale mail, breastplate, ring mail, splint; +1 chain shirt |
| Shields | 3 | +1 shield |
| Ammunition | 3 | arrows, crossbow bolts, sling bullets |
| Tools | 14 | leatherworker's, cook's, alchemist's, weaver's, miner's pick, fishing tackle (smith's, carpenter's and herbalism kit gain disciplines) |
| Materials | 9 (+ iron bar, timber plank) | iron ore, charcoal, raw hide, wolf pelt, cured leather, antler, silverleaf, linen, glass vial |
| Food & drink | 8 | venison, fresh fish, hunter's stew, smoked fish, ale |
| Potions | 2 | healing (2d4+2), greater healing (4d4+4) |
| Jewellery | 3 | gold ring, silver amulet, ring of protection |
| Wondrous | 5 | cloak of protection, gauntlets of ogre power, goggles of night, boots of elvenkind, wand of magic missiles |
| Clothing, headwear, foci, trade goods | 7, 3, 4, 4 | the traveller's cloak |
| Book, key, quest, container | 1 each | almanac, old key, sealed letter, backpack |

By rarity: 102 common, 9 uncommon, 3 rare. All 11 magic items are SRD 5.2.1. Every description and lore line is Realmkeeper's own.

## E. Inventory and storage functionality

- **The pack** is limited by weight: SRD carrying capacity is Strength × 15 lb (Powerful Build doubles it). Over capacity, Speed is 5 ft everywhere; past twice capacity nothing more can be taken.
- **Inventory screen:**
  - the load bar;
  - the rules' AC and attack;
  - search (name, category, badges, tags, description);
  - group filters with counts;
  - sorting by category, name, weight, value or recent, up or down, with stable ties;
  - state badges;
  - Inspect;
  - a rules-engine comparison;
  - per-row actions: equip, right hand, off hand, use, attune, split, store, drop.
- **Containers:**
  - the player's chest in every settlement (about 150 lb, take and deposit);
  - a cache per settlement (take only, filled once);
  - loot piles (take only).
  Take, put, take all (all or nothing) and coin both ways. Partial transfers make a new instance; the rest keeps its ID.
- **Street:** the chest stands by the tavern door and the cache under a flagstone by the square. Walking up and pressing E opens Storage.

## F. Equipment and rules integration

- **Slots.** Eleven, including neck and two rings. A Light weapon in the off hand gives the bonus-action off-hand attack (no positive modifier to damage). The spear and quarterstaff are now Versatile, as in SRD 5.2.1.
- **Condition.**
  - Damaged: −1 to attack, or −1 AC.
  - Broken: no attacks, or no AC.
  - A natural 1 wears the weapon; a critical hit taken wears the armour, or else the shield.
  - Each condition changes value.
- **Magic.**
  - The +1 weapon bonus is a named part of attack and damage.
  - AC and save bonuses apply while worn.
  - Gauntlets set Strength to 19; goggles give darkvision; boots give advantage on Stealth.
  - Attunement: three items, an hour each.
  - A curse binds when attuned (no unequip, no end of attunement, no sale or storage) until a shrine lifts it for 60 coin.
  - The wand casts Magic Missile from its charges through the spell engine and recharges after a long rest.
- **Ammunition** is taken from the pack per shot, and half is returned after the fight. Without ammunition, a ranged weapon's attack is unavailable.
- **Potions** are a bonus action in a fight and can be drunk outside one.
- **The numbers on screen are the rules'.** The hub, the comparison and combat read the same `CombatStats`.

## G. Loot generation and persistence

- 15 loot tables, plus carried-gear lines for 9 humanoids. Tables support guaranteed, chance and weighted entries, ranges, conditions, tags and nesting. `Validate` rejects unknown references and lines that could never be given (this caught 9 such lines during development).
- Rolls are seeded by the encounter and the combatant. `FillLoot` creates one `loot:encounter:{id}` container. A second call returns it unchanged, and replays and reloads cannot duplicate loot.
- Rarity is capped at Uncommon for fights and caches; rare entries need a tag.
- After a won fight the combat panel offers "Look over what they left (n items and c coin)". It opens in Storage, and nothing goes straight into the pack or purse.

## H. Crafting, gathering and repair implementation

- **11 recipes** in 7 disciplines (table in `Documentation/Phase11/Items.md`), with materials, tool and proficiency, station (workshop, tavern, shrine), time, check, output, Fine on beating the DC by 5, and loss on failure.
- **Work.**
  - It holds its inputs and rolls when it starts (seeded by the job key; save-scumming cannot change it).
  - Only one piece of work runs at a time.
  - Cancelling returns everything.
  - "Wait until done" advances world time through the simulation and then collects.
- **Gathering.** Hedgerows (silverleaf) everywhere; deadfall timber, an ore seam and fishing water where the settlement logs, mines or fishes. Stock is finite and recovers weekly.
- **Repair.**
  - At an armourer, 15 % of base value per step.
  - By the player, the craft's tools, an hour per step, DC 10 + 2 × steps.

## I. Merchant and economy integration

- Offers scale by condition and make, named as a price factor. The trade window and the sell command use the same function. While reading the code I found and fixed a bug here: the window used to show the unscaled price, which would have made every worn sale fail with "the offer has changed".
- Distinct items keep their identity in a shop's particular goods (24 at most) and are bought back as that instance.
- Mending is paid to the keeper. A curse is lifted at a shrine. Shops still restock from settlement materials (Phase 5), and loot and gathering add goods without touching settlement stocks.

## J. UI/UX changes and screenshots

The hub now has 11 sections: Character, Spells & rest, Appearance, Inventory, Equipment, Weapons, Clothing & armour, **Crafting & work**, **Storage**, Currency & possessions, and Info. The trade window gains **Particular goods** and **Mending**. The combat panel gains the loot button. The chest and cache are interactables in the street.

Screenshots (`Documentation/Screenshots/Phase11/`, captured by `Phase11CaptureTests` from the shipped scene):

| File | What it shows |
|---|---|
| `p11_01_inventory.jpg` | The inventory: 23 instances with state badges, the load bar (63.3 of 108.9 kg capacity, most 217.7 kg), the rules' AC 17 breakdown and attack, search, group chips with counts, sorting |
| `p11_02_inventory_filtered_sorted.jpg` | Weapons & armour, by value (highest first); the damaged shortsword's value halved |
| `p11_03_inspect_wand.jpg` | The Wand of Magic Missiles inspected: spell, 4 of 7 charges, recharge, rarity, SRD source |
| `p11_04_compare_armour.jpg` | +1 chain shirt against the worn chain shirt: AC 17 → 18 ("better"), speed the same, from the rules engine |
| `p11_05_equipment.jpg` | +1 longsword, dagger in the off hand, rings on both hands, cloak and ring of protection attuned; AC 18 with each part named |
| `p11_06_cursed_axe.jpg` | The Berserker Axe attuned and bound: unequipping refused with the reason; the curse in the inspection |
| `p11_07_chest_in_the_street.jpg` | The player's chest by the tavern door in Bramhaven |
| `p11_08_storage.jpg` | Storage: the chest (ore and 50 coin left in it, its capacity) and the cache under the flagstone |
| `p11_09_crafting.jpg` | Crafting & work: recipes with materials, tool, station, DC and chance; the jerkin in progress |
| `p11_10_work_outcomes.jpg` | Outcomes: the jerkin failed (d20 6 + 2 vs DC 12; 34 % spoiled), deadfall gathered; mending and attunement lists |
| `p11_11_armourer_goods_and_mending.jpg` | The armourer: the damaged shortsword just sold among particular goods (12 c), the worn mace under Mending (4 c), offers marked "(worn)" |
| `p11_12_fight_leaves_loot.jpg` | A won ambush: the panel offers "19 items and 7 coin"; nothing went into the pack |
| `p11_13_loot_in_storage.jpg` | The loot in Storage: the bandits' worn crossbow and scimitars, bolts, an amulet; the cache and chest below |

The capture log with every action taken is `p11_capture_log.txt`. Every screen is the shipped UI over a live world, and none is a mockup. The capture run surfaced one test-harness bug, fixed: the camera was held across a save reload.

## K. Save schema and migrations

Schema **v13**:

- Stacks gain `condition`, `quality`, `charges`, `attuned`, `bound`, `provenance` and `acquiredAt`. Each is written only when it differs from a fresh instance.
- Shops gain `goods`.
- The world gains `containers`, `workJobs`, `gatheringSites` and `itemCatalogVersion`.

`MigrationV12ToV13` adds the empty lists, catalog version 1 and empty goods, and enables `realmkeeper.gathering` after the fauna. On load, `ItemCatalog.Upgrade` follows any renamed IDs (none in version 2) and records version 2. The item genesis adds chests, caches and sites once.

The validator checks:

- one holder per instance across all holders;
- possible states: charges within the maximum, bound only when attuned, at most three attunements;
- at most one piece of work in progress;
- sites within capacity;
- slots that fit (rings, off-hand Light weapons).

Tests cover a v12 document that loads with its items intact and gains the item world once, an exact round trip of every state, and corrupt states and unknown kinds refused.

## L. Test results, including exact counts

| Suite | Result |
|---|---|
| .NET harness (`dotnet test Tools/DotNet/Realmkeeper.Verification.sln -c Release`) | **574 passed, 0 failed** (552 before + 22 new: 21 item tests, 1 performance test) |
| Unity EditMode (6000.0.47f1, batch) | **574 / 574 passed** |
| Unity PlayMode (batch) | **96 tests: 80 passed, 16 skipped, 0 failed** (Phase 10: 74 passed, 14 skipped). The skipped tests are the capture sets, which need `RK_CAPTURE_DIR`. The 6 Phase 11 tests (17 flows) passed again after the last presentation change, in the capture run. |
| Captures (`-testFilter Phase11`, `RK_CAPTURE_DIR`) | 2 capture tests passed (after the camera fix) and 13 screenshots taken |
| Mutation checks (12) | **12 caught.** Copying instead of moving on a put; take-all without the weight check; a 4th attunement allowed; a curse that does not bind; cancel that keeps materials; damaged shields with no penalty; no encumbrance speed; loot filled twice; offers ignoring condition; sites not regrowing; the validator's one-holder check removed; the magic weapon bonus removed. Two mutants first failed only on a compiler warning (unreachable code); they were rewritten so they compile, and tests caught both. |

The new .NET tests (`Tests/EditMode/Items/ItemSystemTests.cs`) cover:

- catalog, tables and recipes consistent;
- genesis deterministic and once;
- chest transfers keep identity and one holder;
- refused transfers change nothing;
- the weight limit and 5 ft speed;
- rings and the off hand;
- magic and condition in the rules;
- attunement, the limit of three and curses;
- potions and wand recharge;
- smith and self repair;
- crafting holds, determinism and cancel;
- crafting refusals;
- gathering is finite and recovers weekly;
- loot in a container, filled once;
- loot tables deterministic and rarity-bounded;
- particular goods;
- inventory filter, search and stable sort;
- every state round-tripped;
- v12 migration;
- validator fault injection (8 impossible states);
- corrupt saves refused.

Tests changed:

- The Phase 5 two-handed example moves from the spear (Versatile in SRD 5.2.1) to the greatsword.
- The Goliath's capacity is now asserted against the SRD rule.
- The daily order adds the gathering system.
- The v12 strip helper is added.

## M. Performance results

From `ItemPerformanceTests` (.NET, Release):

| Measure | Result | Ceiling |
|---|---|---|
| Inventory page (150 rows; filter, search, sort) | 0.22 ms | 20 ms |
| Comparison through the rules engine | 0.11 ms | 20 ms |
| Transfer to the chest (validated command) | 1.3 ms | 500 ms |
| 10,000 rolls of `old_cache` | 19 ms | 2,000 ms |
| Item genesis (test world) | 0.1 ms | 500 ms |
| Save / load of the test world (554 KB) | 7 ms / 31 ms | — |
| Item sections of the save | 51 KB | — |

## N. Known limitations and deferred work

See `Documentation/Phase11/Limitations.md`. In short:

- Armorsmithing and enchanting have no recipes.
- Camps, settlement stores and others' chests are kinds without instances.
- The backpack adds no capacity.
- Keys and letters unlock nothing yet.
- Recharge happens after a long rest, not at dawn.
- Only the curse's binding is modelled.
- Only the player's gear wears.
- Out-of-town loot is reached from Storage, not a physical drop.
- There is no drag and drop, and no quantity entry in the UI.
- The trade window's new lists are short at 1080p.

## O. Licensing and attribution

- SRD 5.2.1 (CC BY 4.0) supplies the equipment tables, ammunition, tools, potions of healing, the 11 magic items' rules, carrying capacity and the attunement limit. These are listed in `CREDITS.md` and in the in-game credits (`Credits.RulesUse`).
- Every item description, lore line, recipe, loot table and setting name is Realmkeeper's own. No proprietary item text, art or setting content is reproduced.
- The chest and cache are built from primitive shapes, and no new art assets were added.

## P. Commit hash and final Git status

- Phase 11 is one local commit on `feature/phase-0-1-foundation`, on top of `1707805`: "Phase 11: inventory, loot, equipment, crafting and item progression". A file cannot contain its own commit's hash, so the hash is given with the delivery and in `git log`. **Not pushed.** The branch is ahead of `origin` by 3.
- Staged with `git add -A -- . ':!ProjectSettings/GraphicsSettings.asset' ':!Assets/InitTestScene*'`.
- Left unstaged:
  - the owner's pre-existing `ProjectSettings/GraphicsSettings.asset`;
  - a temporary `Assets/InitTestScene….unity` that the Unity test runner left behind after the capture run. It is not part of the project.
- No history was rewritten, and no reset, clean or checkout was run.

## Q. Recommended Phase 12 priorities

1. **Quests and world events that use items:** the old key and sealed letter, deliveries, bounties paid in goods, and loot from raids.
2. **Companions** with their own sheets and inventories, sharing the holder model and the comparison.
3. **Enchanting and armorsmithing recipes**, and crafted magic items within rarity rules, with identification.
4. **The field as a place:** persistent out-of-town sites where loot piles, camps and caches physically stand.
5. **Trade by the player between settlements**, moving goods along the existing routes and prices.
6. **UI depth:** drag and drop, quantity entry, and keyboard navigation in the inventory and storage.
