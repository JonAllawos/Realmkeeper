using System;
using System.Collections.Generic;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>
    /// Phase 16 (ADR-0076, ADR-0077): the connected world. The home region stays as it was. Each further region is made
    /// by the same <see cref="RegionGenerator"/> from a seed derived from the world's (so its terrain, settlements, economy,
    /// people, factions, layouts and wildlife come from the one pipeline), then merged with new identities of its own.
    /// Regions sit on an atlas grid (three columns); a region's mountain side is chosen from the neighbour it borders, so a
    /// range continues across the border rather than stopping at it. Adjacent regions are joined by a crossing between
    /// their nearest border settlements, whose kind (pass, bridge, trail, road) comes from the terrain on both sides, and
    /// by trade routes along it. Intra-region routes follow each region's own roads. Deterministic and idempotent.
    /// </summary>
    public static class WorldAtlasGenesis
    {
        public const int Columns = 3;
        public const int MaxRegions = 9;

        /// <summary>The generator's eight directions (index → x, z), as <c>RegionGenerator</c> uses them.</summary>
        private static readonly (float x, float z)[] Dir =
        {
            (1f, 0f), (0.70710678f, 0.70710678f), (0f, 1f), (-0.70710678f, 0.70710678f),
            (-1f, 0f), (-0.70710678f, -0.70710678f), (0f, -1f), (0.70710678f, -0.70710678f),
        };

        private static readonly string[] NameSuffixes = { "Ford", "Cross", "End", "Mere", "Hollow", "Reach", "Brook", "Gate" };

        public static bool Ensure(WorldData world, RegionMap homeMap, RegionGenerator generator, IRandomSourceFactory random, int regions)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Regions.Count == 0 || world.Settlements.Count == 0) return false; // a test world without geography
            regions = Math.Max(1, Math.Min(MaxRegions, regions));
            var changed = false;
            var home = world.Regions[0];
            if (home.Seed == 0) { home.Seed = world.Seed; changed = true; }
            if (home.GeneratorVersion == 0) { home.GeneratorVersion = world.GeneratorVersion; changed = true; }
            var maps = new Dictionary<RegionId, RegionMap>();
            if (homeMap != null)
            {
                homeMap.RegionId = home.RegionId;
                maps[home.RegionId] = homeMap;
                if (home.MountainDirection < 0) { home.MountainDirection = homeMap.MountainDirection; changed = true; }
                if (home.Landmarks.Count == 0) { Describe(home, homeMap); changed = true; }
            }

            if (home.Discovery == RegionDiscovery.Unknown) { home.Discovery = RegionDiscovery.Visited; changed = true; }

            for (var i = world.Regions.Count; i < regions; i++)
            {
                AddRegion(world, i, maps, generator, random);
                changed = true;
            }

            if (Link(world)) changed = true;
            // The home region's neighbours are known by name from the start (as on entering any region).
            foreach (var n in home.Neighbours)
                if (world.FindRegion(n) is RegionData nr && nr.Discovery == RegionDiscovery.Unknown) { nr.Discovery = RegionDiscovery.Discovered; changed = true; }
            if (BuildRoutes(world, maps, generator, random)) changed = true;

            // Phase 18 (ADR-0089): each further region's own places and their quests, once (a region's map is regenerated
            // only when it still lacks them, so a loaded world that has them pays nothing).
            for (var i = 1; i < world.Regions.Count; i++)
            {
                var region = world.Regions[i];
                if (PlaceGenesis.HasPhase18(world, region.RegionId) || !world.Settlements.Exists(s => s.RegionId == region.RegionId)) continue;
                if (!maps.TryGetValue(region.RegionId, out var m)) maps[region.RegionId] = m = generator.RegenerateRegion(world, region);
                m.RegionId = region.RegionId;
                if (PlaceGenesis.EnsurePhase18(world, m)) changed = true;
            }

            if (changed) Simulation.Quests.QuestGenesis.Ensure(world); // the further regions' own emergent quests, as a reload would find them
            if (RealmGenesis.Ensure(world, homeMap, generator)) changed = true; // Phase 23 (ADR-0111): the realms and the prototype gateway, once
            return changed;
        }

        // ----- Regions --------------------------------------------------------------------------------

        private static void AddRegion(WorldData world, int index, Dictionary<RegionId, RegionMap> maps, RegionGenerator generator, IRandomSourceFactory random)
        {
            var ax = index % Columns;
            var az = index / Columns;
            var side = MountainSideFor(world, ax, az, maps, generator);
            GeneratedWorld made = null;
            long seed = 0;
            for (var attempt = 0; attempt < 4 && made == null; attempt++)
            {
                seed = unchecked((long)SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "region:" + index + (attempt > 0 ? ":" + attempt : string.Empty)));
                try
                {
                    made = generator.Generate(new WorldGenerationRequest(world.WorldName, seed, world.WorldSettings.Clone(), world.WorldId)
                    {
                        IncludeCommerce = true, IncludeLivingWorld = true, RegionOnly = true, MountainDirection = side,
                    });
                }
                catch (WorldGenerationException)
                {
                    made = null; // that seed's land cannot hold three settlements: the next derived seed is tried (deterministically)
                }
            }

            if (made == null) throw new WorldGenerationException($"Region {index} of '{world.WorldName}' could not be generated.");
            var sub = made.World;
            var region = sub.Regions[0];
            region.AtlasX = ax;
            region.AtlasZ = az;
            region.Seed = seed;
            region.MountainDirection = made.Map.MountainDirection;
            region.GeneratorVersion = RegionGenerator.Version;
            Describe(region, made.Map);
            var bare = region.Name.StartsWith("Vale of ", StringComparison.Ordinal) ? region.Name.Substring(8) : region.Name;
            region.Name = KindWord(region.Kind) + " of " + bare;
            region.Discovery = RegionDiscovery.Unknown;
            made.Map.RegionId = region.RegionId;
            maps[region.RegionId] = made.Map;

            // Names stay unique across the world: a repeated settlement name takes a local suffix.
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in world.Settlements) names.Add(s.Name);
            var k = 0;
            foreach (var s in sub.Settlements)
            {
                var original = s.Name;
                while (names.Contains(s.Name)) s.Name = original + " " + NameSuffixes[k++ % NameSuffixes.Length];
                names.Add(s.Name);
            }

            // Road bands are numbered by their region's road: the number is made unique with the region (the home ones keep theirs).
            foreach (var p in sub.CreaturePopulations)
                if (p.PopulationId.Contains("@road:")) p.PopulationId = p.PopulationId.Replace("@road:", "@road:" + region.RegionId + ":");

            // Merge: the region's own world becomes part of this one (its player, chronicle and authored content are not).
            world.Regions.Add(region);
            world.Settlements.AddRange(sub.Settlements);
            world.Factions.AddRange(sub.Factions);
            world.Npcs.AddRange(sub.Npcs);
            world.NpcRelations.AddRange(sub.NpcRelations);
            world.TradeRoutes.AddRange(sub.TradeRoutes);
            world.Relations.AddRange(sub.Relations);
            world.Claims.AddRange(sub.Claims);
            world.Locations.AddRange(sub.Locations);
            world.Shops.AddRange(sub.Shops);
            world.Structures.AddRange(sub.Structures);
            world.CreaturePopulations.AddRange(sub.CreaturePopulations);
            world.Creatures.AddRange(sub.Creatures);
            world.Livestock.AddRange(sub.Livestock);
            world.SettlementLayouts.AddRange(sub.SettlementLayouts);
            world.Containers.AddRange(sub.Containers);
            world.WorkJobs.AddRange(sub.WorkJobs);
            world.GatheringSites.AddRange(sub.GatheringSites);

            // Every pair of factions has one relationship (Phase 3): the new region's with everyone else's, wary when
            // their lands border each other, otherwise strangers.
            foreach (var f in sub.Factions)
                foreach (var g in world.Factions)
                {
                    if (f.FactionId == g.FactionId || sub.Factions.Contains(g) || world.FindRelation(f.FactionId, g.FactionId) != null) continue;
                    var (a, b) = PoliticalMath.OrderPair(f.FactionId, g.FactionId);
                    var bordering = Adjacent(world, RegionOf(world, f), RegionOf(world, g));
                    world.Relations.Add(new FactionRelationData { FactionA = a, FactionB = b, Trust = bordering ? 35 : 50, Opinion = bordering ? -10 : 0, Rivalry = bordering ? 25 : 0 });
                }
        }

        /// <summary>The mountain side for a new region at (ax, az), chosen from the region it borders on the left or above.</summary>
        private static int MountainSideFor(WorldData world, int ax, int az, Dictionary<RegionId, RegionMap> maps, RegionGenerator generator)
        {
            RegionData n = null;
            int vx = 0, vz = 0;
            foreach (var r in world.Regions)
            {
                if (r.AtlasX == ax - 1 && r.AtlasZ == az) { n = r; vx = 1; vz = 0; break; }
                if (r.AtlasX == ax && r.AtlasZ == az - 1 && n == null) { n = r; vx = 0; vz = 1; }
            }

            if (n == null) return -1;
            var dn = n.MountainDirection;
            if (dn < 0) { if (!maps.TryGetValue(n.RegionId, out var m)) maps[n.RegionId] = m = generator.RegenerateRegion(world, n); dn = m.MountainDirection; }
            var toward = vx == 1 ? 0 : 2; // the direction from the neighbour into the new region
            var dot = Dir[dn].x * vx + Dir[dn].z * vz;
            if (dot > 0.5f) return (toward + 4) % 8; // the neighbour's range runs up to the border: it continues on this side
            if (dot < -0.5f) return toward;          // lowland on the neighbour's side of the border: lowland on this side too
            return dn;                               // a range along the border runs on, parallel
        }

        private static void Describe(RegionData r, RegionMap map)
        {
            int high = 0, forest = 0, water = 0, plains = 0, total = map.Width * map.Depth;
            for (var x = 0; x < map.Width; x++)
                for (var z = 0; z < map.Depth; z++)
                    switch (map.GetTerrain(x, z))
                    {
                        case TerrainType.Mountain:
                        case TerrainType.Hills: high++; break;
                        case TerrainType.Forest: forest++; break;
                        case TerrainType.DeepWater:
                        case TerrainType.ShallowWater: water++; break;
                        case TerrainType.Plains: plains++; break;
                    }

            r.Kind = high * 100 / total >= 30 ? RegionKind.Highlands : forest * 100 / total >= 35 ? RegionKind.Woodland
                : water * 100 / total >= 12 ? RegionKind.Lakeland : plains * 100 / total >= 55 ? RegionKind.Lowlands : RegionKind.Vale;
            r.Landmarks.Clear();
            if (water > 0) r.Landmarks.Add("a lake");
            if (map.Rivers.Count > 0) r.Landmarks.Add("a river from the high ground");
            if (high * 100 / total >= 15) r.Landmarks.Add("a mountain range on its " + Side(map.MountainDirection) + " side");
            if (forest * 100 / total >= 20) r.Landmarks.Add("wide forest");
        }

        private static string Side(int d)
        {
            switch (d)
            {
                case 0: return "east";
                case 1: return "north-east";
                case 2: return "north";
                case 3: return "north-west";
                case 4: return "west";
                case 5: return "south-west";
                case 6: return "south";
                case 7: return "south-east";
                default: return "far";
            }
        }

        private static string KindWord(RegionKind k)
        {
            switch (k)
            {
                case RegionKind.Highlands: return "Highlands";
                case RegionKind.Woodland: return "Woods";
                case RegionKind.Lakeland: return "Meres";
                case RegionKind.Lowlands: return "Lowlands";
                default: return "Vale";
            }
        }

        // ----- Neighbours -----------------------------------------------------------------------------

        private static bool Adjacent(WorldData world, RegionData a, RegionData b) =>
            a != null && b != null && a != b && Math.Abs(a.AtlasX - b.AtlasX) + Math.Abs(a.AtlasZ - b.AtlasZ) == 1;

        private static RegionData RegionOf(WorldData world, FactionData f)
        {
            foreach (var s in world.Settlements) if (s.FactionId == f.FactionId) return world.FindRegion(s.RegionId);
            return null;
        }

        private static bool Link(WorldData world)
        {
            var changed = false;
            foreach (var a in world.Regions)
                foreach (var b in world.Regions)
                    if (Adjacent(world, a, b) && !a.Neighbours.Contains(b.RegionId)) { a.Neighbours.Add(b.RegionId); changed = true; }
            foreach (var a in world.Regions) a.Neighbours.Sort((x, y) => x.CompareTo(y));
            return changed;
        }

        // ----- Routes ---------------------------------------------------------------------------------

        /// <summary>Terrain cost of a road through a cell, in percent of open ground.</summary>
        private static int Cost(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Forest: return 125;
                case TerrainType.Hills: return 150;
                case TerrainType.Mountain: return 190;
                default: return 100;
            }
        }

        private static bool BuildRoutes(WorldData world, Dictionary<RegionId, RegionMap> maps, RegionGenerator generator, IRandomSourceFactory random)
        {
            var changed = false;
            foreach (var region in world.Regions)
            {
                if (world.Routes.Exists(r => r.RegionA == region.RegionId && r.RegionB == region.RegionId)) continue;
                if (!maps.TryGetValue(region.RegionId, out var map)) maps[region.RegionId] = map = generator.RegenerateRegion(world, region);
                foreach (var road in map.Roads)
                {
                    var a = world.FindSettlement(road.From);
                    var b = world.FindSettlement(road.To);
                    if (a == null || b == null) continue;
                    long cost = 0;
                    var water = false;
                    foreach (var c in road.Cells)
                    {
                        var t = map.GetTerrain(c.X, c.Z);
                        cost += Cost(t);
                        if (t == TerrainType.River || t == TerrainType.ShallowWater || t == TerrainType.DeepWater) water = true;
                    }

                    world.Routes.Add(new RouteSegmentData
                    {
                        RouteId = RouteSegmentData.IdFor(a.SettlementId, b.SettlementId), A = a.SettlementId, B = b.SettlementId, RegionA = region.RegionId, RegionB = region.RegionId,
                        Kind = RouteKind.Road, Cells = road.Cells.Count, TerrainPercent = (int)(cost / Math.Max(1, road.Cells.Count)), HasBridge = water, Bridge = BridgeState.Intact,
                    });
                    changed = true;
                }
            }

            var ids = random.Create(SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "atlas-trade"));
            var index = 0;
            var start = world.CurrentSimulationDate.ToDayNumber();
            foreach (var a in world.Regions)
                foreach (var b in world.Regions)
                {
                    if (!(b.AtlasX == a.AtlasX + 1 && b.AtlasZ == a.AtlasZ) && !(b.AtlasX == a.AtlasX && b.AtlasZ == a.AtlasZ + 1)) continue;
                    if (world.Routes.Exists(r => r.IsCrossing && (r.RegionA == a.RegionId && r.RegionB == b.RegionId || r.RegionA == b.RegionId && r.RegionB == a.RegionId))) continue;
                    if (!maps.TryGetValue(a.RegionId, out var ma)) maps[a.RegionId] = ma = generator.RegenerateRegion(world, a);
                    if (!maps.TryGetValue(b.RegionId, out var mb)) maps[b.RegionId] = mb = generator.RegenerateRegion(world, b);
                    var east = b.AtlasX == a.AtlasX + 1;
                    var sa = Border(world, a, east, high: true);
                    var sb = Border(world, b, east, high: false);
                    if (sa == null || sb == null) continue;
                    var crossing = Crossing(world, a, b, sa, sb, ma, mb, east);
                    world.Routes.Add(crossing);
                    foreach (var (from, to) in new[] { (sa, sb), (sb, sa) })
                    {
                        var travel = Math.Max(1, (crossing.Cells + EconomyInitializer.CellsPerTravelDay - 1) / EconomyInitializer.CellsPerTravelDay);
                        world.TradeRoutes.Add(new Domain.Economy.TradeRouteData
                        {
                            TradeRouteId = new TradeRouteId(DeterministicGuid.Next(ids)), OriginId = from.SettlementId, DestinationId = to.SettlementId,
                            CapacityPerShipment = Math.Min(Capacity(from.SettlementType), Capacity(to.SettlementType)), TravelDays = travel,
                            FrequencyDays = EconomyInitializer.RouteFrequencyDays, EfficiencyPercent = Math.Max(85, 100 - 2 * travel),
                            Status = Domain.Economy.TradeRouteStatus.Active, NextDepartureDay = start + 1 + index++ % EconomyInitializer.RouteFrequencyDays,
                        });
                    }

                    changed = true;
                }

            world.Routes.Sort((x, y) => string.CompareOrdinal(x.RouteId, y.RouteId));
            return changed;
        }

        private static int Capacity(SettlementType type) => type == SettlementType.City ? 400 : type == SettlementType.Town ? 240 : type == SettlementType.Village ? 150 : 80;

        /// <summary>The settlement nearest the shared border (largest X or Z for the region before it, smallest for the one after).</summary>
        private static SettlementData Border(WorldData world, RegionData r, bool east, bool high)
        {
            SettlementData best = null;
            foreach (var s in world.Settlements)
            {
                if (s.RegionId != r.RegionId) continue;
                var v = east ? s.Position.X : s.Position.Z;
                var bv = best == null ? 0 : east ? best.Position.X : best.Position.Z;
                if (best == null || (high ? v > bv : v < bv) || v == bv && s.SettlementId.CompareTo(best.SettlementId) < 0) best = s;
            }

            return best;
        }

        /// <summary>The crossing between two border settlements: its length over the border and its kind from both sides' terrain.</summary>
        private static RouteSegmentData Crossing(WorldData world, RegionData a, RegionData b, SettlementData sa, SettlementData sb, RegionMap ma, RegionMap mb, bool east)
        {
            int high = 0, water = 0, forest = 0, samples = 0;
            void Sample(RegionMap m, int x, int z)
            {
                if (!m.InBounds(x, z)) return;
                samples++;
                var t = m.GetTerrain(x, z);
                if (t == TerrainType.Mountain || t == TerrainType.Hills) high++;
                else if (t == TerrainType.River || t == TerrainType.ShallowWater || t == TerrainType.DeepWater) water++;
                else if (t == TerrainType.Forest) forest++;
            }

            int cells;
            if (east)
            {
                cells = (ma.Width - 1 - sa.Position.X) + sb.Position.X + Math.Abs(sa.Position.Z - sb.Position.Z) + 1;
                var lo = Math.Min(sa.Position.Z, sb.Position.Z) - 3;
                var hi = Math.Max(sa.Position.Z, sb.Position.Z) + 3;
                for (var z = lo; z <= hi; z++) { Sample(ma, ma.Width - 1, z); Sample(mb, 0, z); }
            }
            else
            {
                cells = (ma.Depth - 1 - sa.Position.Z) + sb.Position.Z + Math.Abs(sa.Position.X - sb.Position.X) + 1;
                var lo = Math.Min(sa.Position.X, sb.Position.X) - 3;
                var hi = Math.Max(sa.Position.X, sb.Position.X) + 3;
                for (var x = lo; x <= hi; x++) { Sample(ma, x, ma.Depth - 1); Sample(mb, x, 0); }
            }

            var kind = high * 2 >= samples ? RouteKind.Pass : water > 0 ? RouteKind.Bridge : forest * 2 >= samples ? RouteKind.Trail : RouteKind.Road;
            return new RouteSegmentData
            {
                RouteId = RouteSegmentData.IdFor(sa.SettlementId, sb.SettlementId), A = sa.SettlementId, B = sb.SettlementId, RegionA = a.RegionId, RegionB = b.RegionId,
                Kind = kind, Cells = cells, TerrainPercent = kind == RouteKind.Pass ? 200 : kind == RouteKind.Trail ? 130 : kind == RouteKind.Bridge ? 110 : 100,
                HasBridge = kind == RouteKind.Bridge, Bridge = BridgeState.Intact,
            };
        }
    }
}
