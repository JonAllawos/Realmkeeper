using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Commerce
{
    /// <summary>
    /// Phase 21 (ADR-0107): the work stations a settlement has. A station is part of a shop of a kind that keeps one (the
    /// smith's forge, the leatherworker's bench, the baker's oven …); it can be worked while the shop is open and its
    /// keeper there. Nothing here is a separate object in the world: the shop is the station's owner and record.
    /// </summary>
    public static class CraftingStations
    {
        public static bool Provides(ShopData shop, StationKind kind)
        {
            var def = ShopKinds.Find(shop.Kind);
            return def != null && kind != StationKind.None && Array.IndexOf(def.Stations, kind) >= 0;
        }

        /// <summary>The shops in a settlement that keep a station of this kind.</summary>
        public static List<ShopData> In(WorldData world, SettlementId settlement, StationKind kind)
        {
            var list = new List<ShopData>();
            foreach (var s in world.Shops) if (s.SettlementId == settlement && Provides(s, kind)) list.Add(s);
            return list;
        }

        /// <summary>
        /// Why a recipe cannot be worked at a station here now (null: it can, at <paramref name="usedAt"/>). With
        /// <paramref name="at"/> set, only that shop's station is considered (the one the player stands at).
        /// </summary>
        public static string Problem(WorldData world, SettlementId settlement, RecipeDefinition r, ShopId at, out ShopData usedAt)
        {
            usedAt = null;
            if (r == null || r.StationKind == StationKind.None) return null;
            var name = StationWords.Name(r.StationKind);
            var shops = In(world, settlement, r.StationKind);
            if (!at.IsEmpty) shops.RemoveAll(s => s.ShopId != at);
            if (shops.Count == 0)
                return at.IsEmpty
                    ? $"{r.Name} needs a {name}, and no shop here keeps one."
                    : $"{r.Name} needs a {name}, and there is none here.";
            string closed = null;
            foreach (var s in shops)
            {
                var open = CommerceMath.Availability(world, s);
                if (open.IsOpen) { usedAt = s; return null; }
                closed = closed ?? $"The {name} is at {s.Name}: {open.Reason}";
            }

            return closed;
        }
    }

    /// <summary>
    /// Phase 21 (ADR-0107): what a keeper makes at their own station each day, through the same recipes the player uses,
    /// from their shop's own stock: inputs leave the stock, the goods join it, and the record says so. Bounded by a working
    /// day's minutes, only for goods the shop deals in and is short of, and only from inputs it holds. No check is rolled
    /// (keepers are practised at their trade); no material appears from nowhere.
    /// </summary>
    public static class ShopProduction
    {
        /// <summary>Minutes of making in a keeper's day (the rest is selling).</summary>
        public const int MinutesPerDay = 480;

        public static int Produce(WorldData world, ShopData shop, CommerceRules rules = null)
        {
            rules = rules ?? new CommerceRules();
            var def = ShopKinds.Find(shop.Kind);
            if (def == null || def.Stations.Length == 0) return 0;
            var keeper = world.FindNpc(shop.ShopkeeperNpcId);
            var s = world.FindSettlement(shop.SettlementId);
            if (keeper == null || keeper.IsDeceased || s == null || s.Stability < rules.UnrestStability) return 0;
            var today = world.CurrentSimulationDate.IsValidDate ? world.CurrentSimulationDate.ToDayNumber() : 0;
            var minutes = MinutesPerDay;
            var made = 0;
            var progress = true;
            while (progress && minutes > 0)
            {
                progress = false;
                foreach (var r in RecipeCatalog.Recipes)
                {
                    if (!Works(shop, def, r)) continue;
                    var output = ItemCatalog.Find(r.OutputItemId);
                    if (output == null || shop.QuantityOf(output.Id) >= CommerceMath.Target(world, shop, output)) continue;
                    if (r.Minutes > minutes) continue;
                    var enough = true;
                    foreach (var (item, qty) in r.Inputs) if (shop.QuantityOf(item) < qty) enough = false;
                    if (!enough) continue;
                    var used = new List<string>();
                    foreach (var (item, qty) in r.Inputs)
                    {
                        CommerceMath.RemoveStock(shop, item, qty);
                        used.Add(qty + " " + (ItemCatalog.Find(item)?.Name.ToLowerInvariant() ?? item));
                    }

                    CommerceMath.AddStock(shop, output.Id, r.OutputQuantity);
                    var where = r.StationKind == StationKind.None ? "made by " + keeper.Name : "made at the " + StationWords.Name(r.StationKind) + " by " + keeper.Name;
                    shop.RecordSupply(today, output.Id, r.OutputQuantity, SupplyOrigin.Produced, where + " from " + string.Join(" and ", used));
                    minutes -= r.Minutes;
                    made += r.OutputQuantity;
                    progress = true;
                    if (minutes <= 0) break;
                }
            }

            return made;
        }

        /// <summary>Whether the shop's keeper makes this item at their bench (so the shop does not buy it in).</summary>
        public static bool MakesHere(ShopData shop, ItemDefinition item) => item != null && Profile(shop).Makes.Contains(item.Id);

        /// <summary>The least of an item one batch of something the shop makes takes (0 when it is no input here): what it keeps of it.</summary>
        public static int InputNeed(ShopData shop, ItemDefinition item) => item != null && Profile(shop).Needs.TryGetValue(item.Id, out var n) ? n : 0;

        // What a kind of shop makes and keeps depends only on its kind and categories and the static catalogs: worked out once
        // per kind, not once per item per day (a day's restock asks for every item of every shop).
        private sealed class WorkProfile
        {
            public readonly HashSet<string> Makes = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> Needs = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private static readonly Dictionary<string, WorkProfile> Profiles = new Dictionary<string, WorkProfile>(StringComparer.Ordinal);

        private static WorkProfile Profile(ShopData shop)
        {
            var key = shop.Kind + ":" + string.Join(",", shop.Categories);
            lock (Profiles)
            {
                if (Profiles.TryGetValue(key, out var p)) return p;
                p = new WorkProfile();
                var def = ShopKinds.Find(shop.Kind);
                if (def != null && def.Stations.Length > 0)
                    foreach (var r in RecipeCatalog.Recipes)
                    {
                        if (!Works(shop, def, r)) continue;
                        if (InputsObtainable(shop, r)) p.Makes.Add(r.OutputItemId);
                        foreach (var (id, qty) in r.Inputs) p.Needs[id] = p.Needs.TryGetValue(id, out var have) ? Math.Min(have, qty) : qty;
                    }

                Profiles[key] = p;
                return p;
            }
        }

        // Every input is something the shop deals in (so it can hold it), and not itself only made here from this output.
        private static bool InputsObtainable(ShopData shop, RecipeDefinition r)
        {
            foreach (var (id, _) in r.Inputs)
                if (!CommerceMath.Sells(shop, ItemCatalog.Find(id))) return false;
            return r.Inputs.Count > 0;
        }

        /// <summary>
        /// Whether a shop's keeper works a recipe: at a station the shop keeps, or with no station when the shop deals in
        /// both what it takes and what it makes (a leatherworker cures hides).
        /// </summary>
        public static bool Works(ShopData shop, ShopKindDefinition def, RecipeDefinition r)
        {
            var output = ItemCatalog.Find(r.OutputItemId);
            if (output == null || !CommerceMath.Sells(shop, output)) return false;
            if (r.StationKind != StationKind.None) return Array.IndexOf(def.Stations, r.StationKind) >= 0;
            foreach (var (item, _) in r.Inputs)
                if (!CommerceMath.Sells(shop, ItemCatalog.Find(item))) return false;
            return r.Inputs.Count > 0;
        }
    }
}
