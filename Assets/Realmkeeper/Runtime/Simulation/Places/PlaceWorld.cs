using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Items;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Simulation.Places
{
    /// <summary>
    /// Phase 13 (ADR-0066): whether a person is somewhere other than their schedule says — held captive, following the
    /// player, waiting where they were left, or posted to a place's garrison. Such a person is never shown in their home
    /// settlement, so one identity is never in two places.
    /// </summary>
    public static class PlacePresence
    {
        public static bool IsAway(WorldData world, NpcData npc)
        {
            if (world == null || npc == null) return false;
            if (world.Escorts != null)
                foreach (var e in world.Escorts)
                    if (e != null && e.NpcId == npc.NpcId && e.OverridesSchedule) return true;
            // Phase 15: an active party member travels with the player; reserve members live their ordinary lives.
            if (world.Party?.Find(npc.NpcId) is PartyMemberData member && member.Role == PartyRole.Active) return true;
            // Phase 16: a caravan's leader or a patrol's captain is on the road.
            if (world.Groups != null) foreach (var g in world.Groups) if (g.IsActive && g.LeaderNpcId == npc.NpcId && g.State != TravelGroupState.Arrived) return true;
            return PostedAt(world, npc.NpcId) != null;
        }

        /// <summary>The place a person is posted to (a garrison member not yet defeated), or null.</summary>
        public static PlaceData PostedAt(WorldData world, NpcId npc)
        {
            if (world?.Places == null || npc.IsEmpty) return null;
            foreach (var p in world.Places)
            {
                if (p?.Layout == null || p.Layout.Groups.Count == 0) continue;
                foreach (var g in p.Layout.Groups)
                {
                    if (p.GroupStateOf(g.Id) == GroupState.Defeated) continue;
                    foreach (var m in g.Members)
                        if (m.NpcId == npc) return p;
                }
            }

            return null;
        }

        /// <summary>People following the player (they go wherever the player goes).</summary>
        public static List<NpcId> Followers(WorldData world)
        {
            var list = new List<NpcId>();
            if (world?.Escorts == null) return list;
            foreach (var e in world.Escorts)
                if (e != null && e.State == EscortState.Following) list.Add(e.NpcId);
            return list;
        }
    }

    /// <summary>
    /// Phase 13 (ADR-0064): the Phase 11 containers that stand in a place, made once per container feature and filled
    /// once from the feature's loot table, seeded by the place (the same place always holds the same things).
    /// </summary>
    public static class PlaceContents
    {
        public static string Key(string placeId, string featureId) => "box:" + placeId + ":" + featureId;

        public static bool Ensure(WorldData world, PlaceData place)
        {
            if (world == null || place?.Layout == null) return false;
            var changed = false;
            foreach (var f in place.Layout.Features)
            {
                if (f.Kind != FeatureKind.Container) continue;
                var key = Key(place.PlaceId, f.Id);
                if (world.FindContainer(key) != null) continue;
                var def = FeatureCatalog.Find(f.DefinitionId);
                var owned = f.Get("owned") == "yes";
                var ownerGroup = f.Get("owner_group");
                var box = new ContainerData
                {
                    Key = key, Kind = f.DefinitionId == "bones_pile" || f.DefinitionId == "niche" ? ContainerKind.Cache : ContainerKind.Chest,
                    Name = (def?.Name ?? "Container") + " — " + (place.Layout.Room(f.Room)?.Name ?? place.Name),
                    SettlementId = place.Category == PlaceCategory.Interior ? place.SettlementId : default, PlaceId = place.PlaceId, FeatureId = f.Id,
                    PlayerAccess = !owned && ownerGroup == null, AcceptsDeposits = false, SourceKey = "place:" + place.PlaceId,
                };
                var table = f.Get("loot");
                if (table != null) // Phase 14: owned containers hold their owners' things too
                {
                    var seed = SeedMixer.Combine(place.Seed, "contents:" + f.Id);
                    var loot = LootTables.Roll(table, new LootContext { MaxRarity = ItemRarity.Uncommon }, seed);
                    box.Coin = loot.Coin;
                    var n = 0;
                    foreach (var d in loot.Items)
                    {
                        var item = ItemCatalog.Find(d.ItemId);
                        if (item == null) continue;
                        var s = ItemStacks.Create(new ItemStackId(ItemGenesis.StackGuid(seed, n++)), item, d.Quantity, "Found in " + place.Name);
                        s.Condition = d.Condition;
                        box.Items.Add(s);
                    }
                }

                var key2 = f.Get("key");
                if (key2 != null && ItemCatalog.Find(key2) != null)
                    box.Items.Add(ItemStacks.Create(new ItemStackId(ItemGenesis.StackGuid(SeedMixer.Combine(place.Seed, "key:" + f.Id), 0)), ItemCatalog.Find(key2), 1, "Kept in " + place.Name));
                world.Containers.Add(box);
                changed = true;
            }

            return changed;
        }

        /// <summary>A container guarded by a group opens to the player once that group is defeated.</summary>
        public static void FreeGuarded(WorldData world, PlaceData place, string groupId)
        {
            foreach (var f in place.Layout.Features)
            {
                if (f.Kind != FeatureKind.Container || f.Get("owner_group") != groupId) continue;
                var box = world.FindContainer(Key(place.PlaceId, f.Id));
                if (box == null || box.PlayerAccess) continue;
                box.PlayerAccess = true;
                if (box.Items.Count == 0 && box.Coin == 0 && f.Get("loot") != null)
                {
                    var seed = SeedMixer.Combine(place.Seed, "contents:" + f.Id);
                    var loot = LootTables.Roll(f.Get("loot"), new LootContext { MaxRarity = ItemRarity.Uncommon }, seed);
                    box.Coin = loot.Coin;
                    var n = 0;
                    foreach (var d in loot.Items)
                    {
                        var item = ItemCatalog.Find(d.ItemId);
                        if (item == null) continue;
                        var s = ItemStacks.Create(new ItemStackId(ItemGenesis.StackGuid(seed, n++)), item, d.Quantity, "Found in " + place.Name);
                        s.Condition = d.Condition;
                        box.Items.Add(s);
                    }
                }
            }
        }
    }

    /// <summary>Phase 13 (ADR-0065): what the player knows of places, and the Chronicle lines it makes.</summary>
    public static class PlaceKnowledge
    {
        /// <summary>Raises what the player knows of a place (never lowers it). True when it changed.</summary>
        public static bool Raise(WorldData world, PlaceData place, PlaceDiscovery to, QuestReporter report, string how)
        {
            if (place == null || place.Discovery >= to) return false;
            var was = place.Discovery;
            place.Discovery = to;
            if (to >= PlaceDiscovery.Discovered && place.DiscoveredAt < 0) place.DiscoveredAt = QuestClock.Now(world);
            if (was < PlaceDiscovery.Discovered && to >= PlaceDiscovery.Discovered && place.Category != PlaceCategory.Interior)
                report?.Invoke(HistoricalEventType.PlaceDiscovered, EventSeverity.Info, EventImportance.Minor, Settlements(place),
                    new Dictionary<string, string> { ["place"] = place.Name, ["how"] = how ?? "found", ["placeId"] = place.PlaceId }, Array.Empty<NpcId>());
            return true;
        }

        public static IReadOnlyList<SettlementId> Settlements(PlaceData p) => p.SettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { p.SettlementId };

        /// <summary>Rough words for where a place lies from a settlement ("north-east of Bramhaven, an hour's walk").</summary>
        public static string Direction(WorldData world, PlaceData place)
        {
            var s = world.FindSettlement(place.SettlementId);
            if (s == null) return "somewhere in the region";
            var dx = place.Cell.X - s.Position.X;
            var dz = place.Cell.Z - s.Position.Z;
            var angle = Math.Atan2(dz, dx) * 180.0 / Math.PI;
            string[] names = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
            var i = (int)Math.Round(((angle % 360) + 360) % 360 / 45.0) % 8;
            var d = Math.Sqrt(dx * dx + dz * dz);
            var far = d < 10 ? "not far" : d < 20 ? "a fair walk" : "a long way";
            return $"{names[i]} of {s.Name}, {far}";
        }
    }

    /// <summary>Phase 13: how dangerous a place is said to be, from the inhabitants still there.</summary>
    public static class PlaceThreat
    {
        public static int Of(PlaceData place)
        {
            if (place?.Layout == null) return 0;
            var xp = 0;
            foreach (var g in place.Layout.Groups)
            {
                var s = place.GroupStateOf(g.Id);
                if (s == GroupState.Defeated || s == GroupState.Peaceful) continue;
                foreach (var m in g.Members) xp += m.CreatureId != null ? CreatureCatalog.Find(m.CreatureId)?.Xp ?? 0 : 50;
            }

            return Math.Min(100, xp / 10);
        }

        public static bool Cleared(PlaceData place)
        {
            if (place?.Layout == null) return false;
            var hostile = false;
            foreach (var g in place.Layout.Groups)
            {
                var s = place.GroupStateOf(g.Id);
                if (g.Behaviour == GroupBehaviour.Guard && s != GroupState.Defeated) return false; // a garrison holds it
                if (s != GroupState.Defeated && s != GroupState.Peaceful) hostile = true;
            }

            return !hostile && place.Layout.Groups.Count > 0;
        }

        public static string Now(WorldData world) => QuestClock.Now(world).ToString(CultureInfo.InvariantCulture);
    }
}

namespace Realmkeeper.Simulation.Places
{
    using Realmkeeper.Domain.Economy;

    /// <summary>
    /// Phase 14 (ADR-0071): a bandit hideout above a road takes a share of the trade along it while its outlaws are
    /// there. Driven off or talked off the road (the place records which), the road is free again.
    /// </summary>
    public static class RoadThreat
    {
        public const int Percent = 25;

        public static int PercentTaken(WorldData world, TradeRouteData route)
        {
            if (world?.Places == null || route == null) return 0;
            foreach (var p in world.Places)
            {
                if (p == null || p.DefinitionId != Phase14Places.Hideout || p.Variables.ContainsKey("road")) continue;
                if (!p.Variables.TryGetValue("route", out var id)) continue;
                var watched = world.FindTradeRoute(new Realmkeeper.Domain.Identifiers.TradeRouteId(System.Guid.Parse(id)));
                if (watched == null) continue;
                var same = (watched.OriginId == route.OriginId && watched.DestinationId == route.DestinationId) || (watched.OriginId == route.DestinationId && watched.DestinationId == route.OriginId);
                if (!same) continue;
                foreach (var g in p.Layout.Groups)
                {
                    var s = p.GroupStateOf(g.Id);
                    if (g.WakesOn == null && s != GroupState.Defeated && s != GroupState.Departed) return Percent;
                }
            }

            return 0;
        }
    }
}
