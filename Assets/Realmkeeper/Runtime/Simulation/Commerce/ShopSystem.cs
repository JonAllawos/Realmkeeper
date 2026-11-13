using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Simulation.Commerce
{
    /// <summary>
    /// Daily restocking (order 430: after the NPC engine, so the dead keep no shop, and before
    /// politics). Each shop's keeper turns settlement materials into merchandise and pays the
    /// treasury for them (<see cref="CommerceMath.Restock"/>). Runs inside the day's transaction.
    /// </summary>
    public sealed class ShopSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.shops";

        private readonly CommerceRules _rules;

        public ShopSystem(CommerceRules rules = null)
        {
            _rules = rules ?? new CommerceRules();
        }

        public string SystemId => Id;
        public int ExecutionOrder => 430;

        public void Execute(SimulationDayContext context)
        {
            foreach (var shop in context.World.Shops)
            {
                CommerceMath.Restock(context.World, shop, _rules);
                ShopProduction.Produce(context.World, shop, _rules); // Phase 21 (ADR-0107): the keeper works their own station
                shop.LastRestockedOn = context.Date;
            }
        }
    }

    /// <summary>
    /// Creates what a world (or a pre-Phase-5 save) lacks: the shops of each settlement, kept by
    /// existing residents, stocked from the settlement's own stores, and the player's starting
    /// clothes, purse and provisions. Deterministic and once only.
    /// </summary>
    public static class CommerceGenesis
    {
        public const int StartingPurse = 150;

        public static int EnsureShops(WorldData world, CommerceRules rules = null)
        {
            rules = rules ?? new CommerceRules();
            if (world.Locations.Count == 0) return 0;
            var added = 0;
            foreach (var s in world.Settlements)
            {
                var has = false;
                foreach (var shop in world.Shops) if (shop.SettlementId == s.SettlementId) has = true;
                if (has) continue;
                foreach (var (kind, role, categories, opens, closes, name) in Plan(s.SettlementType))
                {
                    var place = NpcGenesis.FirstOf(world, s.SettlementId, kind);
                    if (kind == LocationKind.Workshop && name == "Smithy")
                    {
                        place = null;
                        foreach (var l in NpcGenesis.LocationsOf(world, s.SettlementId, LocationKind.Workshop)) if (l.LayoutKey != SettlementSites.Granary) place = l;
                    }
                    else if (kind == LocationKind.Workshop)
                    {
                        place = null;
                        foreach (var l in NpcGenesis.LocationsOf(world, s.SettlementId, LocationKind.Workshop)) if (l.LayoutKey == SettlementSites.Granary) place = l;
                    }

                    if (place == null) continue;
                    var keeper = KeeperFor(world, s.SettlementId, place.LocationId, role);
                    if (keeper == null) continue; // no resident to keep it: no shop (never an invented vendor)
                    var shop = new ShopData
                    {
                        ShopId = new ShopId(NpcHash.Guid(NpcHash.Of(world.Seed, "shop", s.SettlementId + ":" + place.LocationId))),
                        SettlementId = s.SettlementId,
                        LocationId = place.LocationId,
                        Name = Surname(keeper.Name) + "'s " + name,
                        Kind = ShopKinds.Infer(new ShopData { Name = name, Categories = new List<ShopCategory>(categories) }), // Phase 21
                        Categories = new List<ShopCategory>(categories),
                        ShopkeeperNpcId = keeper.NpcId,
                        OpensAt = opens,
                        ClosesAt = closes,
                        LastRestockedOn = world.CurrentSimulationDate,
                    };

                    // The keeper works there, in the shop's hours.
                    if (keeper.WorkplaceId != place.LocationId)
                    {
                        var old = keeper.WorkplaceId;
                        keeper.WorkplaceId = place.LocationId;
                        for (var d = 0; d < NpcScheduleData.Days; d++) if (keeper.Schedule[d] == old) keeper.Schedule[d] = place.LocationId;
                    }

                    world.Shops.Add(shop);
                    // Opening stock: what the keeper can afford to make or buy in from the settlement's stores.
                    for (var round = 0; round < 4; round++) CommerceMath.Restock(world, shop, rules);
                    added++;
                }
            }

            return added;
        }

        /// <summary>
        /// Phase 21 (ADR-0106): every shop gets its kind (older shops: the kind they were made as, nothing else changed), and a
        /// settlement's unused shop buildings gain the specialists its size calls for, each kept by a resident who keeps no
        /// shop and is not travelling with the player. Deterministic; buildings already used, explored or lived in are never
        /// taken. Returns the shops added.
        /// </summary>
        public static int EnsureKinds(WorldData world, CommerceRules rules = null)
        {
            rules = rules ?? new CommerceRules();
            foreach (var shop in world.Shops)
            {
                if (shop.Kind == ShopKind.Unspecified) shop.Kind = ShopKinds.Infer(shop);
                if (shop.Supply == null) shop.Supply = new List<SupplyRecordData>();
            }

            if (world.Locations.Count == 0) return 0;
            var added = 0;
            foreach (var s in world.Settlements)
            {
                if (world.FindLayout(s.SettlementId) == null) continue;
                var present = new HashSet<ShopKind>();
                foreach (var shop in world.Shops) if (shop.SettlementId == s.SettlementId) present.Add(shop.Kind);
                var free = UnusedShopBuildings(world, s.SettlementId);
                foreach (var kind in ShopKinds.AddedIn(s.SettlementType))
                {
                    if (free.Count == 0) break;
                    if (present.Contains(kind)) continue;
                    var def = ShopKinds.Find(kind);
                    var keeper = SpecialistKeeper(world, s.SettlementId, def);
                    if (keeper == null) break; // no one left to keep a shop: no shop (never an invented vendor)
                    var building = free[0];
                    free.RemoveAt(0);
                    var place = new LocationData
                    {
                        LocationId = new LocationId(NpcHash.Guid(NpcHash.Of(world.Seed, "location", s.SettlementId + ":" + LocationKind.Shop + ":" + building.Key))),
                        SettlementId = s.SettlementId,
                        Kind = LocationKind.Shop,
                        Name = def.Name,
                        LayoutKey = building.Key,
                    };
                    world.Locations.Add(place);
                    var shop = new ShopData
                    {
                        ShopId = new ShopId(NpcHash.Guid(NpcHash.Of(world.Seed, "shop", s.SettlementId + ":" + place.LocationId))),
                        SettlementId = s.SettlementId,
                        LocationId = place.LocationId,
                        Name = Surname(keeper.Name) + "'s " + def.Name,
                        Kind = kind,
                        Categories = new List<ShopCategory>(def.Categories),
                        ShopkeeperNpcId = keeper.NpcId,
                        OpensAt = def.Opens,
                        ClosesAt = def.Closes,
                        LastRestockedOn = world.CurrentSimulationDate,
                    };
                    var old = keeper.WorkplaceId;
                    keeper.WorkplaceId = place.LocationId;
                    for (var d = 0; d < NpcScheduleData.Days; d++)
                        if (keeper.Schedule[d] == old || keeper.Schedule[d].IsEmpty) keeper.Schedule[d] = place.LocationId;
                    if (keeper.CurrentLocationId == old) keeper.CurrentLocationId = place.LocationId;
                    world.Shops.Add(shop);
                    building.IsSite = true; // the world now uses this building (its place in the plan is unchanged)
                    StructureGenesis.EnsureFor(world, place); // a functional structure, like every other place
                    present.Add(kind);
                    // Opening stock: bought with the keeper's whole purse (the float is for trading days), then a day at the bench.
                    var keepFloat = rules.KeeperFloat;
                    rules.KeeperFloat = 0;
                    for (var round = 0; round < 4; round++) CommerceMath.Restock(world, shop, rules);
                    rules.KeeperFloat = keepFloat;
                    ShopProduction.Produce(world, shop, rules);
                    added++;
                }
            }


            return added;
        }

        /// <summary>A settlement's planned shop buildings with no location or explored interior on them, by key.</summary>
        public static List<Domain.Settlements.BuildingData> UnusedShopBuildings(WorldData world, SettlementId settlement)
        {
            var free = new List<Domain.Settlements.BuildingData>();
            var plan = world.FindLayout(settlement);
            if (plan == null) return free;
            foreach (var b in plan.Buildings)
            {
                if (b.Function != Domain.Settlements.BuildingFunction.Shop) continue;
                var used = false;
                foreach (var l in world.Locations) if (l.SettlementId == settlement && l.LayoutKey == b.Key) { used = true; break; }
                if (!used) foreach (var p in world.Places) if (p.SettlementId == settlement && p.LayoutKey == b.Key) { used = true; break; }
                if (!used) free.Add(b);
            }

            free.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return free;
        }

        /// <summary>
        /// How many more specialists a settlement can take: the kinds its size calls for that it lacks, as far as it has
        /// unused shop buildings for them.
        /// </summary>
        public static int SpecialistsWanted(WorldData world, SettlementData s)
        {
            var present = new HashSet<ShopKind>();
            foreach (var shop in world.Shops) if (shop.SettlementId == s.SettlementId) present.Add(shop.Kind == ShopKind.Unspecified ? ShopKinds.Infer(shop) : shop.Kind);
            var missing = 0;
            foreach (var kind in ShopKinds.AddedIn(s.SettlementType)) if (!present.Contains(kind)) missing++;
            return Math.Min(missing, UnusedShopBuildings(world, s.SettlementId).Count);
        }

        /// <summary>A resident to keep a new specialist shop: the kind's preferred roles first, then the order of a seeded hash.</summary>
        private static NpcData SpecialistKeeper(WorldData world, SettlementId settlement, ShopKindDefinition def)
        {
            NpcData best = null;
            var bestRank = int.MaxValue;
            ulong bestHash = 0;
            foreach (var n in world.Npcs)
            {
                if (n.IsDeceased || n.HomeSettlementId != settlement || n.Tier == NpcTier.Unassigned || n.ResidenceId.IsEmpty) continue;
                if (world.Party?.Find(n.NpcId) != null) continue;
                if (Places.PlacePresence.IsAway(world, n)) continue;
                var keepsOne = false;
                foreach (var s in world.Shops) if (s.ShopkeeperNpcId == n.NpcId) { keepsOne = true; break; }
                if (keepsOne) continue;
                var rank = Array.IndexOf(def.Keepers, n.Role);
                if (rank < 0) continue;
                var hash = NpcHash.Of(world.Seed, "keeper:" + def.Kind, n.NpcId.ToString());
                if (rank < bestRank || (rank == bestRank && hash < bestHash)) { best = n; bestRank = rank; bestHash = hash; }
            }

            return best;
        }

        private static IEnumerable<(LocationKind, NpcRole, ShopCategory[], int, int, string)> Plan(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.Hamlet:
                    yield return (LocationKind.Workshop, NpcRole.Farmer, new[] { ShopCategory.Provisions }, 7 * 60, 16 * 60, "Farm Stall");
                    break;
                case SettlementType.Village:
                    yield return (LocationKind.Shop, NpcRole.Merchant, new[] { ShopCategory.General, ShopCategory.Clothing }, 8 * 60, 18 * 60, "General Store");
                    yield return (LocationKind.Tavern, NpcRole.Innkeeper, new[] { ShopCategory.Provisions }, 10 * 60, 22 * 60, "Tavern Kitchen");
                    break;
                default:
                    yield return (LocationKind.Shop, NpcRole.Merchant, new[] { ShopCategory.General, ShopCategory.Clothing }, 8 * 60, 18 * 60, "Outfitters");
                    yield return (LocationKind.Workshop, NpcRole.Smith, new[] { ShopCategory.WeaponsAndArmour }, 8 * 60, 17 * 60, "Smithy");
                    yield return (LocationKind.Tavern, NpcRole.Innkeeper, new[] { ShopCategory.Provisions }, 10 * 60, 22 * 60, "Tavern Kitchen");
                    yield return (LocationKind.Workshop, NpcRole.Labourer, new[] { ShopCategory.Materials }, 8 * 60, 17 * 60, "Granary Yard");
                    break;
            }
        }

        private static NpcData KeeperFor(WorldData world, SettlementId settlement, LocationId place, NpcRole role)
        {
            NpcData fallback = null;
            foreach (var n in world.Npcs)
            {
                if (n.IsDeceased || n.HomeSettlementId != settlement || n.Tier == NpcTier.Unassigned) continue;
                var keepsOne = false;
                foreach (var s in world.Shops) if (s.ShopkeeperNpcId == n.NpcId) keepsOne = true;
                if (keepsOne) continue;
                if (n.Role == role && n.WorkplaceId == place) return n;
                if (n.Role == role && fallback == null) fallback = n;
            }

            return fallback;
        }

        private static string Surname(string name)
        {
            var parts = name?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts == null || parts.Length == 0 ? "The" : parts[parts.Length - 1];
        }

        /// <summary>The player's starting clothes, a knife, bread and a purse (new worlds; once on migration).</summary>
        public static bool EnsurePlayer(WorldData world)
        {
            var p = world.Player;
            if (p == null || p.IsOutfitted) return false;
            var n = 0;
            ItemStackData Stack(string item, int quantity) => new ItemStackData
            {
                StackId = new ItemStackId(NpcHash.Guid(NpcHash.Of(world.Seed, "player-item", p.CharacterId.ToString(), n++))),
                ItemId = item,
                Quantity = quantity,
            };

            p.Equipment[EquipmentSlot.Chest] = Stack("tunic_linen", 1);
            p.Equipment[EquipmentSlot.Hands] = Stack("sleeves_linen", 1);
            p.Equipment[EquipmentSlot.Legs] = Stack("trousers_wool", 1);
            p.Equipment[EquipmentSlot.Feet] = Stack("shoes_simple", 1);
            p.Inventory.Add(Stack("dagger_iron", 1));
            p.Inventory.Add(Stack("bread_loaf", 3));
            p.Purse = StartingPurse;
            p.Fed = 80;
            p.IsOutfitted = true;
            return true;
        }
    }
}
