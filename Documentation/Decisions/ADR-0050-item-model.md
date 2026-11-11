# ADR-0050: Items are definitions plus instances; every instance is made in one place

**Status.** Accepted, 2026-11-11 (Phase 11).

**Context.**
- Phase 5 stacks carried only an ID, an item and a quantity. There was nowhere to keep condition, make, charges, attunement or where a thing came from.
- New categories (ammunition, potions, jewellery, materials, wondrous items) need a home without rewriting the UI each time.

**Decision.**
1. **Definitions are data, never saved.** `ItemDefinition` gains subcategory, rarity, tags, lore, source (`ContentSource`), version, and typed profiles. The profiles are `ConsumableProfile` (healing dice, nourishment, the action it takes in a fight), `MagicProfile` (ADR-0052), wear, a tool discipline, materials-for, container capacity and ammunition-for. `ItemCatalog.Validate()` checks the whole catalog.
2. **Instances carry their own state.** `ItemStackData` adds Condition (Pristine…Broken), Quality (Poor/Common/Fine), Charges, Attuned, Bound, Provenance and AcquiredAt. `SameAs` decides whether two stacks may merge: alike in every respect, and never attuned or bound.
3. **One factory.** `ItemStacks.Create` makes every new instance, so a wand starts with its charges. `ItemCommandHandlers.NewStackId` gives each instance an ID no holder already uses. `CommandContext.NextGuid` repeats within a quiet day, so a drawn ID that is already in use is skipped.
4. **Categories by table.** `ItemCategories` gives each category a name, a filter group and an order. A new category needs a row, not new UI.
5. **Catalog version.** The world records the catalog version its items were made with (`ItemCatalogVersion`, Phase 11 = 2). `ItemCatalog.Upgrade` follows any renamed IDs on load. Version 2 renames nothing.
6. **The rules data decides.** Wear and two-handedness come from the SRD profile. The spear and quarterstaff are Versatile, not two-handed (Phase 5 had them wrong), and stackable thrown weapons do not wear.

**Consequences.**
- Saves record only what differs from a fresh instance: condition Good, make Common and no charges are written as absent.
- Merging respects identity. A Fine dagger never disappears into a common stack.

**Alternatives considered.**
- *A property bag per instance:* untyped, unvalidated, easy to corrupt.
- *Unique objects for everything:* thousands of arrows would each be an object.
