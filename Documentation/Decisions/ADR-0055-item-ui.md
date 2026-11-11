# ADR-0055: The item screens are query snapshots plus command requests

**Status.** Accepted, 2026-11-11 (Phase 11).

**Context.**
- The Phase 5 hub drew its inventory from a fixed snapshot. Searching, filtering, sorting, inspecting and comparing would each need their own data.

**Decision.**
1. **Queries.** `WorldSession` gains read-only views:
   - `GetInventory(group, search, sort, descending)`: rows with their state badges, groups with counts, the load against capacity and limit, the rules' AC and attack, attunements, and current work;
   - `InspectItem`;
   - `CompareItem`, which equips a copy of the character and runs `CombatStats` on both;
   - `GetContainer` and `GetReachableContainers`;
   - `GetWorkshop`;
   - `GetShopGoods` and `GetRepairQuotes`;
   - `GetSaleOffer`.
   Sorting has stable tie-breaks (name, then instance ID).
2. **The hub asks.**
   - `PlayerHubWindow` receives sources (delegates) for these queries.
   - It raises `ItemActionRequested` with a command to run. The controller runs it on the session and redraws.
   - Waiting out work goes through `FinishWorkRequested`.
   - The window never changes the world.
3. **Screens.**
   - Inventory: the load bar, search, group chips, sorting, a details panel with comparison, and per-row actions.
   - Equipment: every slot including rings and neck, with attune and end-attunement.
   - Weapons, and Clothing & armour: inventory groups.
   - Crafting & work, and Storage.
   - The shop gains "Particular goods" and "Mending". The combat panel offers a finished fight's loot.
   - The chest and the cache stand in the street, and interacting opens Storage.

**Consequences.**
- PlayMode tests drive all of this through the shipped UI (`Phase11PlayModeTests`).
- Sale offers now include condition. A shop's sale offer and the sell command use the same function (`PlayerCommandHandlers.OfferFor`), so a worn item's shown price is the price paid.

**Alternatives considered.**
- *The window holding the session:* the presentation could change the world directly.
- *A separate inventory scene:* it duplicates the hub and its live character preview.
