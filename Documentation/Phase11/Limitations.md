# Phase 11: known limitations and deferred work

Phase 11 has no feature that is only data. Where something is prepared but not built, it is listed here.

## Not built (prepared in data only)

- **Disciplines without recipes.** Armorsmithing and enchanting are named disciplines with no recipes. No magic item can be crafted.
- **Container kinds without places.** `Chest` (someone else's), `SettlementStore` and `Camp` are kinds with no instances. There are no camps, and the granary's goods are not shown as a container.
- **Container items as items.** The backpack is an item with a capacity. It does not add carrying room or hold items of its own; the pack is the character's.
- **Keys and letters.** The old key and the sealed letter are items with no lock or quest that uses them.

## Simplifications (documented, deliberate)

- **Recharge after a long rest**, not at dawn (ADR-0052), so it cannot be farmed by waiting.
- **Curses.** Only binding is modelled. The Berserker Axe's madness when hurt is not simulated, and its `StatusNote` says so. Lifting a curse is a 60-coin shrine service, not the Remove Curse spell.
- **Attunement** is its own hour of work for each item, done anywhere outside a fight. It is not combined with the player's short or long rests, and there is nothing to interrupt it.
- **Wear** happens only to the player's items. NPCs' and creatures' gear does not wear. Tools do not wear from use.
- **Ammunition.** The pack is treated as the quiver; there is no quiver slot. Recovery is half, rounded down, per type, at the end of the fight, whatever was hit.
- **Self-repair** returns an item to Good, never to Pristine. Fine items cannot be made Pristine by mending.
- **The comparison** uses the Dexterity save to show save changes. A save bonus from a magic item applies to every save, so the line stands for all of them.
- **Prices.** Particular goods are priced from the shop's selling price × condition × make. Haggling and regard apply through the shop's own factors.

## Interface

- Out-of-town loot piles are reached from Storage wherever the player is. There is no physical drop on the field, because there is no persistent field location to put them in yet (ADR-0053).
- The chest and the cache are simple shapes, with no open animation. Like the rest of the town's procedural props, they are labelled placeholders.
- Drag and drop between the pack and a container is not implemented. Transfers are buttons on each row (Store, Take, Take all).
- In the trade window at 1920 × 1080, the new **Particular goods** and **Mending** lists get only about one row of height below the main lists and must be scrolled (see `p11_11`). A layout pass is deferred.
- Splitting from the UI always halves a stack; entering a quantity is not offered. Commands accept any quantity.
- Search matches the name, category, badges, tags and description as plain substrings. There is no fuzzy matching.

## Deferred to later phases

- Enchanting and crafted magic items, item identification, item sets, durability for tools, and companion inventories.
- Settlement storage the player can rent, banks, and goods carried between settlements by the player as trade.
- Loot from other world events (raids, quests). Only encounters and caches fill containers now.
