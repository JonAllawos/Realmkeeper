# Commerce

Phase 5 adds shops that sell finished goods to the player. They are built into the settlement
economy rather than beside it (ADR-0018).

## Shops

A shop (`ShopData`, saved in v7) has:

- a stable ID, its settlement and its location (a building);
- a name and the categories it deals in;
- its **shopkeeper** (an existing NPC);
- its stock (item → count) and opening hours;
- the day it last restocked, and its sales and purchase totals in coin.

`CommerceGenesis.EnsureShops` creates shops once, for settlements that have none. Each is kept by a
resident with the right role who already lives there.

| Settlement | Shops |
|---|---|
| Town | Outfitters (general goods and clothing, 08–18; the "Shop" house); Smithy (arms and armour, 08–17; the smithy house); Tavern Kitchen (provisions, 10–22); Granary Yard (materials, 08–17) |
| Village | General Store; Tavern Kitchen |
| Hamlet | Farm Stall (provisions, 07–16) |

- No vendor is ever invented: without a suitable resident, there is no shop.
- The keeper's workplace becomes the shop and their routine follows its hours.
- A keeper keeps one shop.

## When a shop trades

`CommerceMath.Availability` says whether a shop trades and why not. It is shut when:

- it has no keeper, or the keeper has died ("shut since … died");
- its settlement is in unrest (stability below 20);
- it is outside its hours ("Closed. Open 08:00–18:00");
- the keeper is not there this hour ("… is not here (at the tavern: an evening at the tavern)");
- the keeper is at their midday meal.

If the keeper is away, the shop waits. Nobody replaces them.

## Buying (atomic)

`BuyItemCommand(shop, item, quantity, expectedUnitPrice)` follows nine steps:

1. Check the shop is available.
2. Check stock.
3. Check the price is the one the player was shown; if it has changed, refuse and state the new
   price.
4. Check the player's funds.
5. Deduct the coin.
6. Transfer the goods (top up stacks, then new stacks; refuse if the pack is full).
7. Reduce the shop's stock.
8. Credit the keeper.
9. Record the trade in the ledger and in the chronicle.

All nine happen on one working copy, which is validated whole before it commits. A failure at any
step changes nothing. Tests check every refusal leaves the saved world byte-identical.

`SellItemCommand(shop, stack, quantity, expectedUnitPrice)` is the mirror:

- the shop must deal in the item and not already hold twice its target;
- the keeper must be able to pay from their own coin.

## The merchant's money

The keeper's till is their own `NpcCondition.Wealth`, which the NPC engine already tracks. There
is no second money pool:

- Sales raise it.
- Buying from the player, and restocking, spend it.
- A keeper with no coin cannot buy from the player, or restock.

## Prices, explained

`CommerceMath.SellingPrice` gives the player's price. It is the base value plus these factors, each
shown in the trade window:

| Factor | Effect |
|---|---|
| Shop markup | +20% |
| Keeper's haggling | (trading skill − 50) / 10 % |
| Knows you | − familiarity / 10 − affinity / 20 % |
| Last ones | +15% at 1 left |
| Well stocked | −5% at or above target |
| Town fortunes | (prosperity − 50) / 5 % |
| Unsettled times | +10% when stability is below 35 |
| Food shortage | +30% on food |

The result is held to 85–170% of base, so prices never swing wildly.

`CommerceMath.BuyingPrice` is what the shop pays the player:

| Factor | Effect |
|---|---|
| Resale margin | −50% |
| Knows you | + familiarity / 10 % |
| Out of stock: wanted | +10% |
| Food shortage | +20% |

That result is held to 30–80% of base. The shop refuses outright if it does not deal in the item
or already has plenty.

Because the selling price never falls below 85% of base and the buying price never rises above
80%, **buying back always costs more than selling earned**. There is no money to be made by
cycling goods (tested).

## Restocking from the economy

`ShopSystem` (`realmkeeper.shops`, order 430, after the NPC engine and before politics) restocks
each shop daily. Each item below its target gains up to 2 units.

**Targets** come from the category (food 10, trade goods 8, clothing 3, others 2), scaled by
prosperity (half at 0, one and a half at 100). Hamlets hold two-thirds of that.

**Each unit:**

1. **consumes its materials from the settlement's stores** (a sword: 3 iron and 1 timber). No food
   is taken during a shortage.
2. books them as `Consumed` in the economy flow.
3. pays their value (iron 3 a unit, others 1) **into the settlement treasury**, booked as imported
   coin.
4. is paid for **by the keeper**.

Items with no modelled material (cloth) pay an import cost of 40% of base value to the treasury.

The long-run conservation test still balances, because materials leave the stores only as booked
consumption and nothing is counted twice. Writing this test exposed a bug: restocking was taking
materials without booking them, and that is fixed. The opening stock is four restock rounds, paid
for the same way. It is made after the economy's starting stores are set.

There is no restocking in unrest, without a keeper, or when the keeper or the settlement cannot
pay.

## Reputation that cannot be farmed

Each NPC holds `PlayerRegard`:

- familiarity 0–100;
- affinity −100…100;
- counts of trades and conversations;
- the last day of each.

| Event | Effect |
|---|---|
| Trading | +3 familiarity and +1 affinity, **once per day per keeper** |
| Talking | +2 familiarity, once per day per person |

Repeating either on the same day only increases the counts. Failed trades never count. The Hub
lists who knows the player and why.

## The record

Every trade is kept in two places:

- **The ledger:** a `TransactionData` with date, minute, kind, shop, keeper, item, quantity, unit
  price and total. The last 500 are kept; validation checks total = quantity × price.
- **The chronicle:** a `PlayerPurchase` or `PlayerSale` event (Minor) with structured facts.
  Example from the captured run: "Maud the Wayfarer bought Linen sleeves from Elric Brooke at Brooke's Outfitters in Bramhaven for 4 coin."

## In the world

The Outfitters and the Smithy are walk-in interiors. Each has:

- an open doorway, plank floor and ceiling;
- a counter;
- shelves on the back and side walls;
- a rack for long goods;
- in the smithy, a forge and an anvil.

**Every unit of stock stands on the shelves as its own model**, up to 15 of one item, and the
interior is rebuilt whenever stock changes. A PlayMode test checks that the shelves equal the stock
before and after a purchase, and after save and load.

The keeper stands behind the counter when the world says they are working there. They are absent
when it says they are not.

Trade happens at the **counter** (E), or through **"Trade"** when talking to the keeper. The trade
window shows:

- the shop's status;
- the stock, with every price and its factors;
- the shop's offer for each thing the player carries, or its reason for refusing;
- the player's purse;
- how well the keeper knows them;
- the shop's recent trades.
