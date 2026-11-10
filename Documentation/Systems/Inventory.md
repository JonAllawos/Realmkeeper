# Items and inventory

## Definitions and instances (ADR-0016)

**Definitions** (`ItemDefinition`, catalog `ItemCatalog`) are data, fixed in code and never saved:

- id, name, description, category, quality, base value, weight;
- whether the item stacks, and its maximum stack;
- its equipment slot, whether it is two-handed, and its visual;
- protection, weapon class and damage, nourishment;
- the **materials** it is made from (settlement resources);
- the shop categories that **sell** it.

The catalog has 30 items:

| Kind | Items |
|---|---|
| Weapons | Dagger, arming sword, hand axe, flanged mace; spear, quarterstaff and hunting bow (two-handed) |
| Shields | Round shield, buckler |
| Headwear | Felt hat, leather hood, iron cap |
| Clothing and armour | Linen and work tunics, leather and forester's jerkins, linen sleeves, leather bracers, wool trousers, leather leggings, simple shoes, riding boots, leather pauldrons |
| Food | Bread, cheese, travel rations |
| Trade goods | Iron bar, timber plank, dressed stone, grain sack |

**Instances** (`ItemStackData`) are what exists in the world: a stack ID, an item ID and a quantity.

## Ownership: exactly one owner

Every stack is in exactly one place:

- the player's inventory;
- one of the player's equipment slots;
- or, as a count, a shop's stock (`ShopStockData`: item and quantity).

The rules that keep this true:

- Moving an item moves the stack. It is never copied: equipping takes the stack out of the pack.
- `WorldValidator` rejects any world where a stack ID appears twice (in the pack, across slots, or
  both). Every command's result is validated before commit, so a duplicating bug cannot be saved.
- Shop stock is a count, not stacks. Buying creates new stacks (fresh IDs) or tops up existing
  ones; selling removes units from a stack and adds to the count. Coin moves the other way in the
  same transaction.

## Inventory commands

| Command | Rule |
|---|---|
| `SplitStackCommand(stack, n)` | 1 ≤ n < quantity; needs a free stack slot |
| `CombineStacksCommand(from, into)` | Same item, stackable; moves what fits; empties are removed |
| `DiscardItemCommand(stack, n)` | 1 ≤ n ≤ quantity (the item is gone; loose items in the world are not modelled yet) |
| `UseItemCommand(stack)` | Food only: −1 unit, fed + nourishment (capped at 100); refused when not hungry |

Every command also refuses items it cannot find, items that are equipped (unequip first),
non-positive quantities, and a full pack (40 stacks).

Validation of a stack:

- the item ID exists;
- the quantity is at least 1 and no more than the stack limit (1 for non-stackables);
- the purse is never negative.

## Weight

Weight is the item's weight times quantity, for worn and carried items alike. It slows the player
past 25 kg (`Player.md`). The Hub shows weight per stack and in total.
