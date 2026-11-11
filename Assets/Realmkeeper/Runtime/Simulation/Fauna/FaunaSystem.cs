using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Fauna
{
    /// <summary>
    /// Phase 10 (ADR-0044): where creatures live, and how many. Each region is cut into sectors; in each sector every
    /// species whose habitat is there gets one population, sized from its suitable cells (carrying capacity) and kept
    /// back from settlements as the species would be. Roads hold bandit bands, sized by road length and the guard at
    /// either end. Settlements keep herds sized from their farming. Deterministic and once: existing records are never
    /// replaced.
    /// </summary>
    public static class FaunaGenesis
    {
        public const int SectorCells = 32;

        /// <summary>A species' place in the region: the terrain it uses, its density per suitable cell, and how far from people it keeps.</summary>
        private sealed class Niche
        {
            public string CreatureId;
            public TerrainType[] Terrain;
            public double PerCell;
            public int MinCellsFromSettlement;
            public int Max;
            public string Habitat;
        }

        private static readonly Niche[] Niches =
        {
            new Niche { CreatureId = "deer", Terrain = new[] { TerrainType.Forest, TerrainType.Plains }, PerCell = 0.05, MinCellsFromSettlement = 3, Max = 120, Habitat = "Forest" },
            new Niche { CreatureId = "rabbit", Terrain = new[] { TerrainType.Plains }, PerCell = 0.10, MinCellsFromSettlement = 0, Max = 200, Habitat = "Grassland" },
            new Niche { CreatureId = "fox", Terrain = new[] { TerrainType.Forest, TerrainType.Plains }, PerCell = 0.012, MinCellsFromSettlement = 2, Max = 30, Habitat = "Forest" },
            new Niche { CreatureId = "boar", Terrain = new[] { TerrainType.Forest }, PerCell = 0.03, MinCellsFromSettlement = 4, Max = 60, Habitat = "Forest" },
            new Niche { CreatureId = "wolf", Terrain = new[] { TerrainType.Forest, TerrainType.Hills }, PerCell = 0.010, MinCellsFromSettlement = 8, Max = 24, Habitat = "Forest" },
            new Niche { CreatureId = "black_bear", Terrain = new[] { TerrainType.Forest, TerrainType.Hills }, PerCell = 0.004, MinCellsFromSettlement = 8, Max = 8, Habitat = "Forest" },
            new Niche { CreatureId = "brown_bear", Terrain = new[] { TerrainType.Hills, TerrainType.Mountain }, PerCell = 0.002, MinCellsFromSettlement = 12, Max = 4, Habitat = "Hills" },
            new Niche { CreatureId = "dire_wolf", Terrain = new[] { TerrainType.Hills, TerrainType.Mountain }, PerCell = 0.0015, MinCellsFromSettlement = 16, Max = 6, Habitat = "Hills" },
            new Niche { CreatureId = "giant_spider", Terrain = new[] { TerrainType.Forest }, PerCell = 0.0012, MinCellsFromSettlement = 16, Max = 4, Habitat = "Forest" },
            new Niche { CreatureId = "goblin", Terrain = new[] { TerrainType.Hills, TerrainType.Forest }, PerCell = 0.003, MinCellsFromSettlement = 14, Max = 12, Habitat = "Hills" },
            new Niche { CreatureId = "ogre", Terrain = new[] { TerrainType.Hills, TerrainType.Mountain }, PerCell = 0.0006, MinCellsFromSettlement = 20, Max = 2, Habitat = "Hills" },
            new Niche { CreatureId = "troll", Terrain = new[] { TerrainType.Mountain }, PerCell = 0.0004, MinCellsFromSettlement = 24, Max = 1, Habitat = "Mountains" },
        };

        /// <summary>Fills in populations and herds a world lacks. True when anything was added.</summary>
        public static bool Ensure(WorldData world, RegionMap map)
        {
            var added = false;
            if (world.CreaturePopulations.Count == 0)
            {
                foreach (var region in world.Regions) added |= Wildlife(world, map, region);
                added |= Roads(world, map);
                added |= Vermin(world);
            }

            foreach (var s in world.Settlements) added |= Herds(world, s);
            return added;
        }

        private static bool Wildlife(WorldData world, RegionMap map, RegionData region)
        {
            var added = false;
            var settlements = new List<MapCoord>();
            foreach (var s in world.Settlements) if (s.RegionId == region.RegionId) settlements.Add(s.Position);
            for (var sx = 0; sx < region.Width; sx += SectorCells)
                for (var sz = 0; sz < region.Depth; sz += SectorCells)
                    foreach (var n in Niches)
                    {
                        long cx = 0, cz = 0;
                        var cells = 0;
                        for (var x = sx; x < Math.Min(region.Width, sx + SectorCells); x++)
                            for (var z = sz; z < Math.Min(region.Depth, sz + SectorCells); z++)
                            {
                                var mx = region.Origin.X + x;
                                var mz = region.Origin.Z + z;
                                if (!map.InBounds(mx, mz) || map.IsRoad(mx, mz) || Array.IndexOf(n.Terrain, map.GetTerrain(mx, mz)) < 0) continue;
                                if (Nearest(settlements, mx, mz) < n.MinCellsFromSettlement) continue;
                                cells++;
                                cx += mx;
                                cz += mz;
                            }

                        var capacity = Math.Min(n.Max, (int)Math.Round(cells * n.PerCell));
                        if (capacity < 1) continue;
                        var center = new MapCoord((int)(cx / cells), (int)(cz / cells));
                        world.CreaturePopulations.Add(new CreaturePopulationData
                        {
                            PopulationId = n.CreatureId + "@" + region.RegionId + ":" + sx.ToString(CultureInfo.InvariantCulture) + "-" + sz.ToString(CultureInfo.InvariantCulture),
                            RegionId = region.RegionId, CreatureId = n.CreatureId, Habitat = n.Habitat, Center = center, RangeCells = SectorCells / 2,
                            Capacity = capacity, Count = Math.Max(1, capacity * 4 / 5), LastUpdatedOn = world.CurrentSimulationDate,
                        });
                        added = true;
                    }

            return added;
        }

        /// <summary>A bandit band works each road: a few bandits per 40 cells of road, fewer where a guard keeps either end.</summary>
        private static bool Roads(WorldData world, RegionMap map)
        {
            var added = false;
            var i = 0;
            foreach (var road in map.Roads)
            {
                var guarded = 0;
                foreach (var n in world.Npcs)
                    if (!n.IsDeceased && n.Role == NpcRole.Guard && (n.HomeSettlementId == road.From || n.HomeSettlementId == road.To)) guarded++;
                var bandits = Math.Max(2, 3 + road.Cells.Count / 40 - guarded);
                var middle = road.Cells[road.Cells.Count / 2];
                var region = world.FindRegionContaining(middle);
                if (region == null) continue;
                world.CreaturePopulations.Add(new CreaturePopulationData
                {
                    PopulationId = "bandit@road:" + i.ToString(CultureInfo.InvariantCulture), RegionId = region.RegionId, CreatureId = "bandit", Habitat = "Road",
                    Center = middle, RangeCells = Math.Max(4, road.Cells.Count / 2 + 2), Capacity = bandits + 2, Count = bandits, LastUpdatedOn = world.CurrentSimulationDate,
                });
                world.CreaturePopulations.Add(new CreaturePopulationData
                {
                    PopulationId = "highwayman@road:" + i.ToString(CultureInfo.InvariantCulture), RegionId = region.RegionId, CreatureId = "highwayman", Habitat = "Road",
                    Center = middle, RangeCells = Math.Max(4, road.Cells.Count / 2 + 2), Capacity = 1, Count = 1, LastUpdatedOn = world.CurrentSimulationDate,
                });
                i++;
                added = true;
            }

            return added;
        }

        /// <summary>Giant rats in every settlement's stores and middens: more where more people live.</summary>
        private static bool Vermin(WorldData world)
        {
            var added = false;
            foreach (var s in world.Settlements)
            {
                var count = Math.Max(2, Math.Min(40, s.Population / 60));
                world.CreaturePopulations.Add(new CreaturePopulationData
                {
                    PopulationId = "giant_rat@" + s.SettlementId, RegionId = s.RegionId, CreatureId = "giant_rat", Habitat = "Settlement", Center = s.Position,
                    RangeCells = 2, Capacity = count, Count = count, LastUpdatedOn = world.CurrentSimulationDate,
                });
                added = true;
            }

            return added;
        }

        /// <summary>The herds a settlement's farming keeps (target sizes); a herd already there is left alone.</summary>
        public static Dictionary<string, int> HerdTargets(SettlementData s)
        {
            var farming = 0;
            foreach (var p in s.Economy?.Production ?? new List<ProductionAssignment>()) if (p.SourceType == ProductionSourceType.Farming) farming += p.WorkerCapacity;
            var pastoral = s.SettlementType == SettlementType.Hamlet ? 2 : 1;
            return new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["cattle"] = Math.Min(FaunaBounds.MaxHerd, farming / 14),
                ["sheep"] = Math.Min(FaunaBounds.MaxHerd, farming * pastoral / 9),
                ["goat"] = Math.Min(FaunaBounds.MaxHerd, farming * pastoral / 30),
                ["draft_horse"] = Math.Min(FaunaBounds.MaxHerd, Math.Max(1, farming / 60)),
                ["riding_horse"] = Math.Min(FaunaBounds.MaxHerd, s.SettlementType >= SettlementType.Town ? Math.Max(2, s.Population / 160) : s.Population / 300),
                ["chicken"] = Math.Min(FaunaBounds.MaxHerd, Math.Max(6, s.Population / 6)),
                ["mastiff"] = Math.Min(FaunaBounds.MaxHerd, Math.Max(1, farming / 50)),
            };
        }

        private static bool Herds(WorldData world, SettlementData s)
        {
            var added = false;
            NpcData keeper = null;
            foreach (var n in world.Npcs)
                if (!n.IsDeceased && n.HomeSettlementId == s.SettlementId && n.Role == NpcRole.Farmer && (keeper == null || n.NpcId.CompareTo(keeper.NpcId) < 0)) keeper = n;
            foreach (var pair in HerdTargets(s))
            {
                if (pair.Value < 1) continue;
                var id = s.SettlementId + ":" + pair.Key;
                var exists = false;
                foreach (var h in world.Livestock) if (h.HerdId == id) exists = true;
                if (exists) continue;
                world.Livestock.Add(new LivestockHerdData
                {
                    HerdId = id, SettlementId = s.SettlementId, CreatureId = pair.Key, Count = pair.Value, KeeperNpcId = keeper?.NpcId ?? default,
                    LastUpdatedOn = world.CurrentSimulationDate,
                });
                added = true;
            }

            return added;
        }

        private static int Nearest(List<MapCoord> points, int x, int z)
        {
            var best = int.MaxValue;
            foreach (var p in points) best = Math.Min(best, Math.Max(Math.Abs(p.X - x), Math.Abs(p.Z - z)));
            return best;
        }
    }

    /// <summary>
    /// Phase 10: creatures live on between encounters (weekly, order 460, after the NPC engine). Populations grow toward
    /// their carrying capacity; wolves take deer in their sector; each settlement's hunters take game near it; herds
    /// grow toward what the farms can keep and are slaughtered in hunger. Integer, deterministic, no events (the
    /// chronicle records encounters, not every litter).
    /// </summary>
    public sealed class FaunaSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.fauna";

        public string SystemId => Id;
        public int ExecutionOrder => 460;

        public void Execute(SimulationDayContext context)
        {
            var world = context.World;
            if (world.CreaturePopulations.Count == 0 && world.Livestock.Count == 0) return;
            var today = context.Date;
            if (today.ToDayNumber() % WorldDate.DaysPerWeek != 3) return; // once a week, mid-week
            Week(world, today);
        }

        /// <summary>One week of change (public for tests and tools).</summary>
        public static void Week(WorldData world, WorldDate today)
        {
            foreach (var p in world.CreaturePopulations)
            {
                if (p.Capacity < 1) continue;
                var growth = Breeding(p.CreatureId) * p.Count * (p.Capacity - p.Count) / Math.Max(1, p.Capacity) / 100;
                if (growth == 0 && p.Count < p.Capacity && p.Count > 1) growth = 1;
                p.Count = Math.Max(0, Math.Min(FaunaBounds.MaxPopulation, Math.Min(p.Capacity, p.Count + growth)));
                p.LastUpdatedOn = today;
            }

            // Predation: each two wolves take a deer a week in their own sector.
            foreach (var wolves in world.CreaturePopulations)
            {
                if (wolves.CreatureId != "wolf" && wolves.CreatureId != "dire_wolf" || wolves.Count < 2) continue;
                foreach (var deer in world.CreaturePopulations)
                {
                    if (deer.CreatureId != "deer" || deer.RegionId != wolves.RegionId || !SameSector(deer, wolves)) continue;
                    var taken = Math.Min(deer.Count, wolves.Count / 2);
                    deer.Count -= taken;
                    deer.Deaths += taken;
                }
            }

            // Hunting: a settlement's hunters take game from the nearest populations of deer, boar and rabbits.
            foreach (var s in world.Settlements)
            {
                var hunters = 0;
                foreach (var a in s.Economy?.Production ?? new List<ProductionAssignment>()) if (a.SourceType == ProductionSourceType.Hunting) hunters += a.WorkerCapacity;
                if (hunters <= 0) continue;
                foreach (var game in new[] { "deer", "boar", "rabbit" })
                {
                    CreaturePopulationData near = null;
                    foreach (var p in world.CreaturePopulations)
                        if (p.CreatureId == game && p.RegionId == s.RegionId && p.Count > 0 && (near == null || Distance(p.Center, s.Position) < Distance(near.Center, s.Position))) near = p;
                    if (near == null) continue;
                    var take = Math.Min(near.Count, Math.Max(1, hunters / 3));
                    near.Count -= take;
                    near.Deaths += take;
                }
            }

            // Herds: toward what the farms keep; a hungry settlement slaughters a tenth.
            foreach (var h in world.Livestock)
            {
                var s = world.FindSettlement(h.SettlementId);
                if (s == null) continue;
                FaunaGenesis.HerdTargets(s).TryGetValue(h.CreatureId, out var target);
                if (s.IsInFoodShortage) h.Count = Math.Max(0, h.Count - Math.Max(1, h.Count / 10));
                else if (h.Count < target) h.Count = Math.Min(target, h.Count + Math.Max(1, (target - h.Count) / 8));
                h.LastUpdatedOn = today;
            }
        }

        /// <summary>Weekly breeding rate in hundredths of the logistic term (r): small game fast, big beasts slow.</summary>
        public static int Breeding(string creatureId)
        {
            switch (creatureId)
            {
                case "rabbit": return 30;
                case "giant_rat": return 25;
                case "deer": return 8;
                case "boar": return 8;
                case "fox": return 6;
                case "goblin": return 4;
                case "bandit": return 3;
                case "wolf": return 3;
                case "black_bear":
                case "brown_bear": return 1;
                default: return 1;
            }
        }

        private static bool SameSector(CreaturePopulationData a, CreaturePopulationData b) =>
            a.Center.X / FaunaGenesis.SectorCells == b.Center.X / FaunaGenesis.SectorCells && a.Center.Z / FaunaGenesis.SectorCells == b.Center.Z / FaunaGenesis.SectorCells;

        private static int Distance(MapCoord a, MapCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));
    }
}
