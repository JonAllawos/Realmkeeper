# ADR-0106: Shops have a kind from one catalog, keep a record of where their stock came from, and fill the town's empty shop buildings

**Context.** Phase 5 shops are real: one stock, one till (the keeper's own wealth), a resident keeper and daily restocking from the settlement's stores. But Phase 21's preflight audit found that:
- there are only four kinds, read from loose categories;
- three of a town's five shop buildings stood empty;
- a smithy "restocked" bows and leather jerkins straight from iron;
- items with no modelled material were "imported" with no trade road and no record.

The brief asks for distinct kinds of shop, real stock, supply and demand, and displays that match stock. It forbids a second economy or inventory, and forbids discarding existing shops, buildings or saves.

**Decision.**

1. **A kind per shop, from one data catalog** (`ShopKinds`, Domain). Each entry gives:
   - its name and trade;
   - what it deals in: an explicit item list, or for the Phase 5 kinds their categories;
   - the keeper roles it draws from, in order of preference;
   - its hours, the smallest settlement that has one, and whether it is a workshop where goods are made.

   **The kinds:**

   | Group | Kinds |
   |---|---|
   | Phase 5 kinds | General store, outfitter, smithy, tavern kitchen, granary yard, farm stall |
   | Added in Phase 21 | Bakery & provisions, leatherworks, tailor, bowyer & fletcher, apothecary, carpentry |

   `CommerceMath.Sells` asks the kind, so restocking, quests and dialogue all agree on what a shop trades.
2. **Existing shops keep everything.** On load, a shop without a kind is given the one it was made as, read from its name and categories. Its stock, goods, keeper, till and history are untouched.
   - The smithy now deals in metalwork only.
   - Stock it already holds that is no longer in its list stays on its shelf and is sold as before; it is simply not restocked.
3. **New shops go only into the town's unused shop buildings.** These are planned buildings whose function is Shop and which no location or explored interior uses. Each is kept by an existing resident who keeps no shop and is not in the player's party.
   - Kinds are added in the catalog's order: leatherworks first; then, in towns, provisions, tailor, apothecary, bowyer and carpentry.
   - Placement stops when buildings or suitable residents run out.
   - Everything is deterministic from the world seed.
   - No building is rebuilt and no resident is invented.
4. **Supply records** (`ShopData.Supply`, the 40 most recent). Each says how many of an item came in on which day, from where. The origins are:
   - **Made here:** from the settlement's stores, paid into its treasury, as in Phase 5.
   - **Imported:** only along an open trade road, which the record names.
   - **Produced:** made at the keeper's bench from the shop's own materials (Phase 21 production).
   - **From the player:** bought from the player.
5. **Imports need a road.** An item with no modelled material is bought in only when the settlement has a trade route that is not suspended. Otherwise it is not restocked, and the shop says why.
6. **Shortfalls are explained, not hidden.** The shop detail lists each item it deals in but has none of, with the reason:
   - an empty store;
   - food kept back in a shortage;
   - no open road;
   - the keeper cannot afford more;
   - unrest;
   - simply being remade at so many a day.
7. **Saved in schema v23** (a shop's `kind` and `supply`). The migration writes nothing. The kind is filled on load (point 2), and absent records mean none were kept.

**Consequences.**
- A town now has specialists whose shelves hold only their trade.
- Each restock is traceable to a store, a road or a bench.
- Every existing shop, its stock and its keeper are kept.
- Because imports now need a road, a cut-off settlement's shops run down. That is the intended effect, and it is visible in the shop's explanation.

**Alternatives considered.**
- **A separate merchant inventory per kind:** rejected. It is forbidden, and it would duplicate the Phase 5 stock.
- **Regenerating each town's shops with the new kinds:** rejected, because it would discard existing shops and keepers.
- **New vendor NPCs for the new shops:** rejected, because shops are kept by the world's own people (Phase 5, ADR-0018).
