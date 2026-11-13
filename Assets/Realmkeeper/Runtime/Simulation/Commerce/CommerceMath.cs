using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Simulation.Commerce
{
    /// <summary>Tunable numbers of commerce (Phase 5).</summary>
    public sealed class CommerceRules
    {
        /// <summary>Shop markup over base value when selling to the player (percent).</summary>
        public int SellMarkupPercent { get; set; } = 20;

        /// <summary>Share of base value a shop pays when buying from the player (percent).</summary>
        public int BuyBackPercent { get; set; } = 50;

        /// <summary>Stability below which shops close and stop restocking (unrest).</summary>
        public int UnrestStability { get; set; } = 20;

        /// <summary>Units of each item a shop can make or buy in per day.</summary>
        public int RestockPerItemPerDay { get; set; } = 2;

        /// <summary>Coin value of one unit of each aggregate resource (what the merchant pays the treasury).</summary>
        public int ValueOf(ResourceTypeId r) => r == ResourceTypes.Iron ? 3 : 1;

        /// <summary>Share of base value paid to the treasury for items with no modelled material (cloth; imported).</summary>
        public int ImportCostPercent { get; set; } = 40;

        /// <summary>Phase 21: coin a keeper keeps back from restocking, to buy from customers (the till is never emptied on stock).</summary>
        public int KeeperFloat { get; set; } = 10;
    }

    /// <summary>One factor of a price, for the UI's explanation ("Scarce stock +15%").</summary>
    public readonly struct PriceFactor
    {
        public PriceFactor(string label, int percent)
        {
            Label = label;
            Percent = percent;
        }

        public string Label { get; }
        public int Percent { get; }
        public override string ToString() => Label + " " + (Percent > 0 ? "+" : "") + Percent.ToString(CultureInfo.InvariantCulture) + "%";
    }

    public sealed class Quote
    {
        public int UnitPrice { get; set; }
        public List<PriceFactor> Factors { get; } = new List<PriceFactor>();
        public string Refusal { get; set; }
        public bool Accepted => Refusal == null;
    }

    /// <summary>Why a shop can or cannot trade right now.</summary>
    public readonly struct ShopAvailability
    {
        public ShopAvailability(bool open, string reason)
        {
            IsOpen = open;
            Reason = reason;
        }

        public bool IsOpen { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// Shop rules shared by the daily system, the commands and the queries: whether a shop is open
    /// (hours, keeper present and alive, no unrest), what it should hold, prices with their reasons,
    /// and restocking from the settlement economy.
    /// </summary>
    public static class CommerceMath
    {
        public static ShopAvailability Availability(WorldData world, ShopData shop, CommerceRules rules = null)
        {
            rules = rules ?? new CommerceRules();
            var keeper = world.FindNpc(shop.ShopkeeperNpcId);
            if (keeper == null) return new ShopAvailability(false, "It has no keeper.");
            if (keeper.IsDeceased) return new ShopAvailability(false, keeper.Name + " has died; the shop is shut.");
            var settlement = world.FindSettlement(shop.SettlementId);
            if (settlement != null && settlement.Stability < rules.UnrestStability)
                return new ShopAvailability(false, "Shuttered: there is unrest in " + settlement.Name + ".");
            var m = world.MinuteOfDay;
            if (m < shop.OpensAt || m >= shop.ClosesAt)
                return new ShopAvailability(false, "Closed. Open " + WorldTime.Clock(shop.OpensAt) + "–" + WorldTime.Clock(shop.ClosesAt) + ".");
            var now = NpcActivityPlan.Now(world, keeper);
            if (now.Location != shop.LocationId)
            {
                var where = world.FindLocation(now.Location);
                return new ShopAvailability(false, keeper.Name + " is not here" + (where != null ? " (at the " + where.Name + ": " + now.Reason.TrimEnd('.').ToLowerInvariant() + ")" : string.Empty) + ".");
            }

            if (now.Activity == ActivityKind.Eating) return new ShopAvailability(false, keeper.Name + " is at their midday meal; back shortly.");
            return new ShopAvailability(true, "Open until " + WorldTime.Clock(shop.ClosesAt) + ".");
        }

        public static bool Sells(ShopData shop, ItemDefinition item)
        {
            // Phase 21 (ADR-0106): a kind with its own list deals in exactly that; the Phase 5 kinds keep their categories.
            var kind = ShopKinds.Find(shop.Kind);
            if (kind != null && kind.Items.Length > 0) return kind.Deals(item);
            foreach (var c in item.SoldBy) if (shop.Categories.Contains(c)) return true;
            return false;
        }

        /// <summary>
        /// Phase 21: the trade road goods come in by: a route from or to the settlement that is not suspended (the first by
        /// ID, so the same state gives the same road), or null when it is cut off.
        /// </summary>
        public static TradeRouteData ImportRoute(WorldData world, SettlementId settlement)
        {
            TradeRouteData best = null;
            foreach (var r in world.TradeRoutes)
            {
                if (r.Status == TradeRouteStatus.Suspended) continue;
                if (r.OriginId != settlement && r.DestinationId != settlement) continue;
                if (best == null || r.TradeRouteId.CompareTo(best.TradeRouteId) < 0) best = r;
            }

            return best;
        }

        /// <summary>
        /// Phase 21: why a shop has none of an item it deals in (null when it has some, or would have some tomorrow without
        /// any obstacle). The same checks as <see cref="Restock"/>, in words.
        /// </summary>
        public static string Shortfall(WorldData world, ShopData shop, ItemDefinition item, CommerceRules rules = null)
        {
            rules = rules ?? new CommerceRules();
            if (item == null || !Sells(shop, item) || shop.QuantityOf(item.Id) > 0) return null;
            var keeper = world.FindNpc(shop.ShopkeeperNpcId);
            var s = world.FindSettlement(shop.SettlementId);
            if (keeper == null || keeper.IsDeceased) return "No one keeps the shop.";
            if (s == null) return null;
            if (s.Stability < rules.UnrestStability) return "Nothing is being made or brought in while " + s.Name + " is in unrest.";
            if (item.Materials.Count == 0)
            {
                if (ImportRoute(world, s.SettlementId) == null) return "Not made here, and no trade road into " + s.Name + " is open to bring it in.";
            }
            else
                foreach (var m in item.Materials)
                {
                    if (m.ResourceTypeId == ResourceTypes.Food && s.IsInFoodShortage) return "Food is short in " + s.Name + "; none is spared to make it.";
                    if (s.ResourceInventory.Get(m.ResourceTypeId) < m.Quantity) return "The " + m.ResourceTypeId.Value + " in " + s.Name + "'s stores has run out.";
                }

            if (keeper.Condition.Wealth < MaterialCost(item, rules)) return keeper.Name + " cannot afford to make or buy in more.";
            return "Sold out; " + keeper.Name + " makes or buys in up to " + rules.RestockPerItemPerDay + " a day.";
        }

        /// <summary>How many of an item the shop aims to hold, given the settlement's prosperity.</summary>
        public static int Target(WorldData world, ShopData shop, ItemDefinition item)
        {
            if (!Sells(shop, item)) return 0;
            int baseTarget;
            switch (item.Category)
            {
                case ItemCategory.Food: baseTarget = 10; break;
                case ItemCategory.TradeGood: baseTarget = 8; break;
                case ItemCategory.Clothing: baseTarget = 3; break;
                default: baseTarget = 2; break;
            }

            var s = world.FindSettlement(shop.SettlementId);
            var prosperity = s?.Prosperity ?? 50;
            var scaled = baseTarget * (50 + prosperity) / 100; // 0.5× at 0 prosperity, 1.5× at 100
            if (s != null && s.SettlementType == SettlementType.Hamlet) scaled = scaled * 2 / 3;
            // Phase 21 (ADR-0107): a workshop keeps of each input just what its smallest batch takes (its purse is its till).
            var batch = ShopProduction.InputNeed(shop, item);
            if (batch > 0) return batch; // a workshop keeps what one batch takes of its materials, no stockpile
            return Math.Max(1, scaled);
        }

        /// <summary>What the player pays for one unit.</summary>
        public static Quote SellingPrice(WorldData world, ShopData shop, ItemDefinition item, CommerceRules rules = null)
        {
            rules = rules ?? new CommerceRules();
            var q = new Quote();
            var keeper = world.FindNpc(shop.ShopkeeperNpcId);
            var s = world.FindSettlement(shop.SettlementId);
            q.Factors.Add(new PriceFactor("Shop markup", rules.SellMarkupPercent));
            if (keeper != null)
            {
                var skill = (keeper.Skills[NpcSkill.Trading] - 50) / 10;
                if (skill != 0) q.Factors.Add(new PriceFactor("Keeper's haggling", skill));
                var regard = -keeper.PlayerRegard.Familiarity / 10 - Math.Max(0, keeper.PlayerRegard.Affinity) / 20;
                if (regard != 0) q.Factors.Add(new PriceFactor("Knows you", regard));
                var standing = world.Player?.Social == null || keeper.FactionId.IsEmpty ? 0 : -world.Player.Social.StandingWith(keeper.FactionId) / 10; // Phase 12 (ADR-0059)
                if (standing != 0) q.Factors.Add(new PriceFactor((standing < 0 ? "Standing with " : "Bad blood with ") + (world.FindFaction(keeper.FactionId)?.Name ?? "their faction"), standing));
            }

            var trade = PlayerTradePercent(world);
            if (trade != 0) q.Factors.Add(new PriceFactor("Trade connections", trade));

            var stock = shop.QuantityOf(item.Id);
            var target = Target(world, shop, item);
            if (stock <= 1) q.Factors.Add(new PriceFactor("Last ones", 15));
            else if (stock >= target && target > 0) q.Factors.Add(new PriceFactor("Well stocked", -5));
            if (s != null)
            {
                var prosperity = (s.Prosperity - 50) / 5;
                if (prosperity != 0) q.Factors.Add(new PriceFactor(s.Prosperity >= 50 ? "Prosperous town" : "Hard times", prosperity));
                if (s.Stability < 35) q.Factors.Add(new PriceFactor("Unsettled times", 10));
                if (item.Category == ItemCategory.Food && s.IsInFoodShortage) q.Factors.Add(new PriceFactor("Food shortage", 30));
            }

            var percent = 100;
            foreach (var f in q.Factors) percent += f.Percent;
            percent = Math.Max(85, Math.Min(170, percent)); // never wildly volatile
            q.UnitPrice = Math.Max(1, (item.BaseValue * percent + 50) / 100);
            return q;
        }

        /// <summary>What the shop pays the player for one unit (or why it will not buy).</summary>
        public static Quote BuyingPrice(WorldData world, ShopData shop, ItemDefinition item, CommerceRules rules = null)
        {
            rules = rules ?? new CommerceRules();
            var q = new Quote();
            if (!Sells(shop, item))
            {
                q.Refusal = "This shop does not deal in " + item.Name.ToLowerInvariant() + ".";
                return q;
            }

            var target = Target(world, shop, item);
            if (shop.QuantityOf(item.Id) >= target * 2)
            {
                q.Refusal = "They have plenty of those already.";
                return q;
            }

            var keeper = world.FindNpc(shop.ShopkeeperNpcId);
            var s = world.FindSettlement(shop.SettlementId);
            q.Factors.Add(new PriceFactor("Resale margin", rules.BuyBackPercent - 100));
            if (keeper != null)
            {
                var regard = keeper.PlayerRegard.Familiarity / 10;
                if (regard != 0) q.Factors.Add(new PriceFactor("Knows you", regard));
            }

            var trade = PlayerTradePercent(world);
            if (trade != 0) q.Factors.Add(new PriceFactor("Trade connections", -trade));
            if (shop.QuantityOf(item.Id) == 0) q.Factors.Add(new PriceFactor("Out of stock: wanted", 10));
            if (s != null && item.Category == ItemCategory.Food && s.IsInFoodShortage) q.Factors.Add(new PriceFactor("Food shortage", 20));
            var percent = 100;
            foreach (var f in q.Factors) percent += f.Percent;
            percent = Math.Max(30, Math.Min(80, percent));
            q.UnitPrice = Math.Max(1, item.BaseValue * percent / 100);
            return q;
        }

        /// <summary>The player's own price effect (Phase 6: a merchant background bargains 5% better).</summary>
        public static int PlayerTradePercent(WorldData world)
        {
            var sheet = world?.Player?.Rpg;
            if (sheet == null || sheet.State != Domain.Rpg.CreationState.Complete) return 0;
            return new Domain.Rpg.CharacterRules(sheet).ShopPricePercent;
        }

        /// <summary>The coin cost to the merchant of making or buying in one unit, paid to the settlement treasury.</summary>
        public static int MaterialCost(ItemDefinition item, CommerceRules rules)
        {
            if (item.Materials.Count == 0) return Math.Max(1, item.BaseValue * rules.ImportCostPercent / 100);
            var cost = 0;
            foreach (var m in item.Materials) cost += m.Quantity * rules.ValueOf(m.ResourceTypeId);
            return Math.Max(1, cost);
        }

        /// <summary>
        /// Restocks one shop for one day: each item below target gains up to
        /// <see cref="CommerceRules.RestockPerItemPerDay"/> units. Each unit consumes its materials from
        /// the settlement's stores (never food in a shortage) and the merchant pays the treasury, so
        /// nothing is counted twice. Returns the units added.
        /// </summary>
        public static int Restock(WorldData world, ShopData shop, CommerceRules rules)
        {
            var keeper = world.FindNpc(shop.ShopkeeperNpcId);
            var s = world.FindSettlement(shop.SettlementId);
            if (keeper == null || keeper.IsDeceased || s == null || s.Stability < rules.UnrestStability) return 0;
            var added = 0;
            var today = world.CurrentSimulationDate.IsValidDate ? world.CurrentSimulationDate.ToDayNumber() : 0;
            // Phase 21 (ADR-0106): what has no modelled material is bought in, and only along an open trade road.
            var road = ImportRoute(world, s.SettlementId);
            var from = road == null ? null : world.FindSettlement(road.OriginId == s.SettlementId ? road.DestinationId : road.OriginId);
            foreach (var item in RestockOrder(shop))
            {
                var target = Target(world, shop, item);
                if (target == 0) continue;
                if (item.Materials.Count == 0 && road == null) continue;
                // Phase 21 (ADR-0107): a workshop makes its own goods at its bench rather than buying them in.
                if (item.Materials.Count == 0 && ShopProduction.MakesHere(shop, item)) continue;
                var before = added;
                for (var n = 0; n < rules.RestockPerItemPerDay && shop.QuantityOf(item.Id) < target; n++)
                {
                    var cost = MaterialCost(item, rules);
                    if (keeper.Condition.Wealth - cost < rules.KeeperFloat) return added; // the purse is spent for today, but for its float (inputs came first)
                    var ok = true;
                    foreach (var m in item.Materials)
                    {
                        if (m.ResourceTypeId == ResourceTypes.Food && s.IsInFoodShortage) ok = false;
                        if (s.ResourceInventory.Get(m.ResourceTypeId) < m.Quantity) ok = false;
                    }

                    if (!ok) break;
                    // Materials leave the settlement's stores as merchandise and are booked as consumed, so the
                    // economy ledger balances; the keeper's coin enters the treasury from a private purse (booked
                    // as imported coin, the same way coin from outside the settlement is).
                    var day = world.CurrentSimulationDate.ToDayNumber();
                    foreach (var m in item.Materials)
                    {
                        s.ResourceInventory.Set(m.ResourceTypeId, s.ResourceInventory.Get(m.ResourceTypeId) - m.Quantity);
                        EconomyMath.Flow(s.Economy, m.ResourceTypeId, day).Consumed += m.Quantity;
                    }

                    s.ResourceInventory.Set(ResourceTypes.Coin, s.ResourceInventory.Get(ResourceTypes.Coin) + cost);
                    EconomyMath.Flow(s.Economy, ResourceTypes.Coin, day).Imported += cost;
                    keeper.Condition.Wealth -= cost;
                    AddStock(shop, item.Id, 1);
                    added++;
                }

                if (added > before)
                {
                    if (item.Materials.Count == 0)
                        shop.RecordSupply(today, item.Id, added - before, SupplyOrigin.Imported, "brought in by the road from " + (from?.Name ?? "elsewhere"));
                    else
                    {
                        var what = new List<string>();
                        foreach (var m in item.Materials) what.Add(m.ResourceTypeId.Value);
                        shop.RecordSupply(today, item.Id, added - before, SupplyOrigin.MadeLocally, "made from " + string.Join(" and ", what) + " in " + s.Name + "'s stores");
                    }
                }
            }

            return added;
        }

        /// <summary>
        /// Phase 21 (ADR-0107): the order a shop restocks in: a workshop's inputs first (its bench needs them before its
        /// shelves need bought-in wares), then the catalog's order, as before.
        /// </summary>
        public static IEnumerable<ItemDefinition> RestockOrder(ShopData shop)
        {
            var later = new List<ItemDefinition>();
            foreach (var item in ItemCatalog.Items)
            {
                if (ShopProduction.InputNeed(shop, item) > 0) yield return item;
                else later.Add(item);
            }

            foreach (var item in later) yield return item;
        }

        public static void AddStock(ShopData shop, string itemId, int quantity)
        {
            foreach (var line in shop.Stock)
            {
                if (line.ItemId != itemId) continue;
                line.Quantity += quantity;
                return;
            }

            shop.Stock.Add(new ShopStockData { ItemId = itemId, Quantity = quantity });
            shop.Stock.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));
        }

        public static bool RemoveStock(ShopData shop, string itemId, int quantity)
        {
            for (var i = 0; i < shop.Stock.Count; i++)
            {
                var line = shop.Stock[i];
                if (line.ItemId != itemId) continue;
                if (line.Quantity < quantity) return false;
                line.Quantity -= quantity;
                if (line.Quantity == 0) shop.Stock.RemoveAt(i);
                return true;
            }

            return false;
        }
    }
}
