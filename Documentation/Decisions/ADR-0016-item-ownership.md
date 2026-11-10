# ADR-0016: Item definitions are data; instances are stacks with exactly one owner

**Status.** Accepted, 2026-11-10 (Phase 5).

**Context.**
- Phase 5 introduces items that can be bought, sold, carried, worn and eaten.
- Two classic bugs must be impossible:
  - duplicated items (an item both worn and packed, or bought twice from one unit of stock);
  - vanished items (a failed trade that took the coin but not the goods).

**Decision.**

1. **Definitions** (`ItemDefinition`, `ItemCatalog`) are immutable data in code and never saved:
   - category, value, weight and stacking;
   - slot, visual and stats;
   - **materials** and the shop categories that sell the item.

   Saves refer to items by stable string ID.
2. **Instances** are `ItemStackData`: a stack ID, an item ID and a quantity. They live in exactly
   one place: the player's inventory or one equipment slot.
3. **Shop stock is a count**, not stacks: item → quantity, saved as a map.
   - Buying turns count into player stacks with fresh deterministic IDs (`CommandContext.NextGuid`).
   - Selling turns stack units back into count.
4. **Moving is never copying.**
   - Equipping moves the stack; from a stack of several, one unit is split off with a new ID.
   - Unequipping moves it back.
5. **The validator guards the invariants on every commit**, so a buggy command can never be saved.
   It rejects:
   - a stack ID seen twice anywhere on the player;
   - an unknown item;
   - a quantity outside 1…stack limit;
   - an item in a slot it does not fit;
   - a two-handed weapon with an off-hand item;
   - a negative purse.
6. **Trades are single commands on one working copy.**
   - Coin, goods, stock, the keeper's till, the ledger and the chronicle change together, or nothing
     does.
   - The expected unit price is part of the command, so a price change between viewing and buying
     refuses the trade rather than charging a different amount.

**Consequences.**
- Mutation tests (deliberately broken worlds) prove the validator catches duplication and invalid
  equipment. Refusal tests prove failed trades change nothing.
- Item identity across a sale and a buy-back is not preserved: the shop holds counts, so the same
  sword comes back as a new stack. Nothing yet needs item history; durability or provenance would
  need instance-level shop stock.

**Alternatives considered.**
- *Instances everywhere, including shops.* Rejected for now: larger saves and more bookkeeping, with
  no feature needing it yet.
- *Inventory as item → count on the player.* Rejected: equipment and future per-item state need
  stack identity.
