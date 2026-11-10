# ADR-0018: Shops are kept by residents and stocked from the settlement economy, without double counting

**Status.** Accepted, 2026-11-10 (Phase 5).

**Context.**
- The economy (Phase 2) is aggregate: food, timber, stone, iron and coin per settlement, with
  production, consumption and trade flows, and a conservation test proving nothing is created or
  lost.
- Shops sell finished goods to one player.
- They must not create goods or money out of nothing, count anything twice, or invent people.

**Decision.**

1. **Keepers are existing NPCs.**
   - Genesis assigns each shop to a resident with the right role who lives there, and points their
     workplace and routine at it.
   - With no such resident there is no shop.
   - A dead keeper shuts the shop. An absent keeper (elsewhere this hour) means it is not trading.
     Nobody stands in.
2. **The keeper's own coin is the till** (`NpcCondition.Wealth`). There is no second money pool:
   - sales credit it;
   - buying from the player and restocking debit it.
3. **Restocking converts settlement resources into merchandise** through the existing ledger
   (`ShopSystem`, order 430, after the NPC engine). Each unit:
   - removes its materials from the settlement's stores, booked as `Consumed`;
   - pays their value to the settlement treasury (keeper → treasury), booked as imported coin.

   Items without modelled materials pay an import cost. There is no food in a shortage and nothing
   in unrest.
4. **Opening stock is paid the same way** (four restock rounds). It is made **after** the economy's
   starting stores exist.
5. **Prices are explained**: each factor is a labelled percentage shown in the UI. They are bounded
   so that buying back always costs more than selling earned, which makes arbitrage impossible.
6. **Reputation grows once per day per person** (talk, trade). Repetition cannot farm it.

**Consequences.**
- The 200-day conservation test (stock + on the road = produced − consumed) still holds with shops
  restocking every day. It caught, and now guards, two bugs found while writing this ADR:
  - restocking took materials without booking them;
  - generation stocked shops before the economy set the starting stores.
- Shops respond to the world:
  - prosperity scales targets and prices;
  - shortages raise food prices and stop food restocking;
  - unrest shuts them.
- The player's coin enters the economy only through keepers. Pay for the player's own labour is out
  of scope.

**Alternatives considered.**
- *Shops with their own abstract inventory, unconnected to the economy.* Rejected: goods from
  nowhere.
- *A shop treasury separate from the keeper.* Rejected: a second pool to keep consistent, for no
  gain yet.
