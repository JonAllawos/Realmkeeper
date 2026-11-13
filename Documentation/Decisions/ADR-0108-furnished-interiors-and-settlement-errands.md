# ADR-0108: Interiors are furnished from a deterministic plan that is never saved; people run errands to their town's shops

**Context.** The Phase 13 interior generator plans persistent rooms, doors, stairs and features (beds, hearths and chests that the rules use). The preflight audit found the rooms nearly bare. The brief asks for interiors that make sense for their purpose, without decoration that pretends to function, and for people with reasons to be in the town's spaces.

**Decision.**

1. **A furnishing planner in the domain** (`InteriorFurnishing.Plan`). For each room it picks kit pieces by the room's kind:
   - **Bedroom:** bed, nightstand, chest, chair.
   - **Quarters and barracks:** beds by size.
   - **Hall, tap room, entrance:** table, chairs, cabinet, barrel, shelf.
   - **Kitchen:** table, barrels, pot, shelf.
   - **Workshop:** benches, crates.
   - **Cellar, storage, vault:** crates and barrels.
   - **Office:** desk, chair, bookcase.
   - **Shrine:** benches and candle stands.
   - **Armoury and gatehouse:** weapon stand, dummy, chest.
   - **Cell:** pallet, bucket.

   Wall pieces face into the room; free-standing ones need a room of at least 3 × 3 cells. The plan is deterministic from the place's own ID.
2. **The plan is validated** (`InteriorFurnishing.Validate`):
   - every piece inside its room;
   - never on a doorway, a stair or the cells around them, nor on a feature's cell;
   - no two pieces in one cell;
   - at least half of each room left clear.

   Tests check this over 540 generated interiors of nine building kinds.
3. **Presentation only.** `PlaceEnvironmentBuilder.Furnish` instantiates the plan with the CC0 kits at measured sizes (`KitProps`), with colliders for solid pieces and a warm light in each furnished room. Nothing is saved, so no save grows and nothing pretends to be usable:
   - the beds, chests and hearths the rules know remain the room's features;
   - the furniture is scenery.
4. **Shops are dressed for their trade.** Each fit-out (`ShopInteriorView.Dress`) uses kit props in the room's front corners: the smith's weapon stand and quench barrel, the leatherworker's bench, peg rack and tanning tub, the tailor's cutting table and dress form, the apothecary's still and bottle shelf, the baker's built oven with its fire. Wares remain exactly the shop's stock.
5. **Errands** (`NpcActivityPlan.ErrandAt`). On a free day at home, one in two people spends an hour of the afternoon (13:00–16:30) at one of their town's shops, never their own, within its hours. In rain, snow or a storm they stay in.
   - It is a pure function of the seed, the person and the day, like the rest of the plan.
   - Exploration shows them there, trading.

**Consequences.**
- Entered homes, workshops and stores are furnished for their purpose, the same each visit.
- Towns' shops see customers on rest days.
- No new save data.

**Alternatives considered.**
- **Saving furniture as features:** rejected. The furniture is not interactive state, and saving it would grow every save for scenery.
- **Furnishing in presentation only, with no domain plan:** rejected. Validation and determinism are tested in the domain, where they can be tested without Unity.
