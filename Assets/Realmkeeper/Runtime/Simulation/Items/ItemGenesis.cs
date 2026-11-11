using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Simulation.Items
{
    /// <summary>
    /// Phase 11 (ADR-0051, ADR-0054): what a world (or a pre-Phase-11 save) lacks of the item world — in each settlement
    /// the player's own chest (at the tavern), one forgotten cache filled once from a loot table, and the places its
    /// land yields materials, grounded in what the settlement actually produces (its fishers fish, its miners mine).
    /// Deterministic (the world seed) and once only: existing containers and sites are never refilled or replaced.
    /// </summary>
    public static class ItemGenesis
    {
        public static string ChestKey(SettlementId s) => "chest:" + s;
        public static string CacheKey(SettlementId s) => "cache:" + s;

        /// <summary>The player's chest holds this much (about 150 lb).</summary>
        public const int ChestCapacityGrams = 68000;

        public static bool Ensure(WorldData world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var changed = false;
            foreach (var s in world.Settlements)
            {
                if (EnsureChest(world, s)) changed = true;
                if (EnsureCache(world, s)) changed = true;
                if (EnsureSites(world, s)) changed = true;
            }

            return changed;
        }

        private static LocationData First(WorldData world, SettlementId s, params LocationKind[] kinds)
        {
            foreach (var k in kinds)
                foreach (var l in world.Locations)
                    if (l.SettlementId == s && l.Kind == k) return l;
            return null;
        }

        private static bool EnsureChest(WorldData world, SettlementData s)
        {
            if (world.FindContainer(ChestKey(s.SettlementId)) != null) return false;
            var at = First(world, s.SettlementId, LocationKind.Tavern, LocationKind.Administration, LocationKind.Square);
            world.Containers.Add(new ContainerData
            {
                Key = ChestKey(s.SettlementId), Kind = ContainerKind.Personal, Name = $"Your chest at {at?.Name ?? s.Name}",
                SettlementId = s.SettlementId, LayoutKey = at?.LayoutKey, PlayerAccess = true, AcceptsDeposits = true, CapacityGrams = ChestCapacityGrams,
                SourceKey = "genesis", Opened = true,
            });
            return true;
        }

        private static bool EnsureCache(WorldData world, SettlementData s)
        {
            if (world.FindContainer(CacheKey(s.SettlementId)) != null) return false;
            var at = First(world, s.SettlementId, LocationKind.Square, LocationKind.Market, LocationKind.GuardPost);
            var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "cache:" + s.SettlementId);
            var loot = LootTables.Roll("old_cache", new LootContext { MaxRarity = ItemRarity.Uncommon }, seed);
            var box = new ContainerData
            {
                Key = CacheKey(s.SettlementId), Kind = ContainerKind.Cache, Name = $"A loose flagstone by {at?.Name ?? "the square"}",
                SettlementId = s.SettlementId, LayoutKey = at?.LayoutKey, PlayerAccess = true, AcceptsDeposits = false, Coin = loot.Coin, SourceKey = "table:old_cache",
            };
            var n = 0;
            foreach (var d in loot.Items)
            {
                var def = ItemCatalog.Find(d.ItemId);
                if (def == null) continue;
                var s0 = ItemStacks.Create(new ItemStackId(StackGuid(seed, n++)), def, d.Quantity, "Found in a cache at " + s.Name);
                s0.Condition = d.Condition;
                box.Items.Add(s0);
            }

            world.Containers.Add(box);
            return true;
        }

        /// <summary>A stable instance ID from a seed (genesis has no command context).</summary>
        public static Guid StackGuid(ulong seed, int n)
        {
            var a = SeedMixer.Mix(SeedMixer.Combine(seed, "stack:" + n));
            var b = SeedMixer.Mix(SeedMixer.Combine(a, "b"));
            var bytes = new byte[16];
            BitConverter.GetBytes(a).CopyTo(bytes, 0);
            BitConverter.GetBytes(b).CopyTo(bytes, 8);
            return new Guid(bytes);
        }

        /// <summary>The gathering sites a settlement's land offers, from what it produces.</summary>
        public static List<GatheringSiteData> SitesFor(SettlementData s)
        {
            var list = new List<GatheringSiteData>();
            var sources = new HashSet<ProductionSourceType>();
            foreach (var p in s.Economy.Production) sources.Add(p.SourceType);
            var small = s.SettlementType == SettlementType.Hamlet;
            GatheringSiteData Site(string kind, string name, string item, string tool, int capacity, int perWeek, int yield, int minutes) => new GatheringSiteData
            {
                Key = $"site:{s.SettlementId}:{kind}", SettlementId = s.SettlementId, Kind = kind, Name = name, ItemId = item, ToolDiscipline = tool,
                Capacity = small ? capacity * 2 / 3 : capacity, Available = small ? capacity * 2 / 3 : capacity, PerWeek = perWeek, Yield = yield, Minutes = minutes,
            };

            // Every settlement has hedgerows and woodland edges to forage.
            list.Add(Site("foraging", $"Hedgerows about {s.Name}", "silverleaf", "foraging", 12, 4, 2, 90));
            if (sources.Contains(ProductionSourceType.Logging)) list.Add(Site("forestry", $"Deadfall in {s.Name}'s woods", "timber_plank", null, 10, 3, 1, 120));
            if (sources.Contains(ProductionSourceType.Mining)) list.Add(Site("mining", $"The old seam above {s.Name}", "iron_ore", "mining", 12, 4, 2, 180));
            if (sources.Contains(ProductionSourceType.Fishing)) list.Add(Site("fishing", $"The water below {s.Name}", "fresh_fish", "fishing", 14, 6, 2, 120));
            return list;
        }

        private static bool EnsureSites(WorldData world, SettlementData s)
        {
            var changed = false;
            foreach (var site in SitesFor(s))
            {
                var has = false;
                foreach (var x in world.GatheringSites) if (x.Key == site.Key) has = true;
                if (has) continue;
                world.GatheringSites.Add(site);
                changed = true;
            }

            return changed;
        }
    }

    /// <summary>
    /// Weekly regrowth of gathering sites (order 470, after the fauna): each site recovers its weekly amount up to its
    /// capacity on the first day of each week. Nothing is endless; a worked-out site comes back slowly.
    /// </summary>
    public sealed class GatheringSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.gathering";

        public string SystemId => Id;
        public int ExecutionOrder => 470;

        public void Execute(SimulationDayContext context)
        {
            if (context.Date.ToDayNumber() % Domain.Calendar.WorldDate.DaysPerWeek != 0) return;
            foreach (var site in context.World.GatheringSites)
                site.Available = Math.Min(site.Capacity, site.Available + site.PerWeek);
        }
    }
}
