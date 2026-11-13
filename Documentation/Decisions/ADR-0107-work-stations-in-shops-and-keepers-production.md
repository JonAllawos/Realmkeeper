# ADR-0107: Work stations belong to shops, recipes name them, and keepers make goods at them from their own stock

**Context.** Phase 11 recipes named only a kind of place ("a workshop"), not a physical station. Smithies "restocked" finished weapons straight from iron. The Phase 21 brief asks for:
- functional forges, benches and ovens;
- crafting that uses real stations;
- an expanded recipe catalogue;
- NPCs who produce goods from real inputs.

It forbids a second economy or inventory.

**Decision.**

1. **Stations.** `StationKind` (forge, woodworker's bench, leatherworker's bench, sewing table, bread oven, kitchen hearth, apothecary's still) is listed per shop kind in `ShopKindDefinition.Stations`. A station is part of its shop: it has no separate world record. It can be worked while the shop is open and its keeper is there.
2. **Recipes name a station** (`RecipeDefinition.StationKind`).
   - The Phase 11 recipes that already needed a place keep that rule as an alternative: the shrine's still, the tavern's kitchen, a village workshop. Nothing that worked before stops working.
   - **15 new recipes:**

     | Station | Recipes |
     |---|---|
     | Forge | Shortsword, hand axe, iron cap, chain shirt |
     | Leatherworker's bench | Bracers, leggings, riding boots, backpack |
     | Sewing table | Linen tunic, padded coat |
     | Woodworker's bench | Hunting bow, fletched arrows, round shield |
     | Bread oven | Bread, travel rations |

   - Each uses catalog items only. `RecipeCatalog.Validate` passes.
3. **The player works at a station** by interacting with it in the shop (`WorkStationView`). This opens crafting there. `StartWorkCommand.Craft(recipe, shopId)` checks that the station exists and the shop is open (`CraftingStations.Problem`), and the refusal names the shop and the reason. Crafting started from the Hub uses any open station in the settlement.
4. **Keepers' production** (`ShopProduction.Produce`, in the daily shop system after restocking) runs the same recipes:
   - at the shop's stations, or with no station when the shop deals in both the inputs and the output (a leatherworker cures hides);
   - only for goods the shop deals in and is short of;
   - only from inputs in the shop's own stock;
   - within a 480-minute working day.

   Inputs leave the stock and outputs join it, recorded as **Produced** ("made at the forge by Fenna Fairweather from 1 iron bar and 1 timber plank"). No check is rolled.
5. **Restocking serves the bench.**
   - A workshop does not buy in goods it makes itself.
   - It keeps of each input just what its smallest batch takes, and restocks its inputs first.
   - Keepers keep a float of 10 coin from restocking (`CommerceRules.KeeperFloat`) to buy from customers. A new shop's opening stock may use the whole purse.
6. **Emergent supply quests are offered on load only when the daily system would offer them:** a new quest log, the week's turn, or a food shortage. Before, loading a save always offered them, while the daily system did so weekly. Production now empties input stock (the very trigger of "out of supplies"), which exposed that latent mismatch, and the save-and-continue tests caught it.

**Consequences.**
- A smith forges, a leatherworker cures and stitches, a tailor sews, an apothecary brews and a baker bakes, from things that came by road, from the town's stores or from the player.
- Each item's record says which.
- Shops whose keepers are short of coin restock less. That is the Phase 5 economy, unchanged in kind.
- Keepers' purses are low after a few days of restocking. A broader NPC customer economy is outside this phase and is noted in the report.

**Alternatives considered.**
- **Stations as separate world objects with their own state:** rejected. A shop's station has no state of its own worth saving yet (no fuel, wear or queue). The shop is its record.
- **Rolled checks for keepers:** rejected for now. It would add noise without a visible consequence. Keepers are treated as practised at their trade.
