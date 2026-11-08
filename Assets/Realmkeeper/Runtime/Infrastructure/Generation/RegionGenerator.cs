using System;
using System.Collections.Generic;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Infrastructure.Generation
{
    public sealed class RegionGeneratorSettings
    {
        public int Width { get; set; } = 128;
        public int Depth { get; set; } = 128;
        public float CellSize { get; set; } = 2f;
        public float MaxElevation { get; set; } = 36f;
        public float WaterLevel { get; set; } = 0.32f;
        public int EdgeMargin { get; set; } = 12;
    }

    /// <summary>
    /// First-pass seed-based region generator (version 1). Pipeline, each stage with its own
    /// random stream derived from the seed:
    /// layout -> elevation -> lake -> river -> classify -> settlements -> flatten ->
    /// reclassify -> roads -> world data. Produces geography (RegionMap) and authoritative
    /// world data (WorldData) as separate objects; nothing here touches Unity.
    /// </summary>
    public sealed class RegionGenerator : IWorldGenerator
    {
        public const int Version = 1;

        private static readonly float[,] Directions =
        {
            { 1f, 0f }, { 0.70710678f, 0.70710678f }, { 0f, 1f }, { -0.70710678f, 0.70710678f },
            { -1f, 0f }, { -0.70710678f, -0.70710678f }, { 0f, -1f }, { 0.70710678f, -0.70710678f },
        };

        private static readonly SettlementType[] SiteTypes = { SettlementType.Town, SettlementType.Village, SettlementType.Hamlet };

        private readonly IRandomSourceFactory _randomFactory;
        private readonly RegionGeneratorSettings _settings;
        private readonly FoodEconomyRules _foodRules = new FoodEconomyRules();

        public RegionGenerator(IRandomSourceFactory randomFactory, RegionGeneratorSettings settings = null)
        {
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
            _settings = settings ?? new RegionGeneratorSettings();
        }

        public int GeneratorVersion => Version;

        public bool SupportsVersion(int generatorVersion) => generatorVersion == Version;

        public RegionMap RegenerateRegion(WorldData world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!SupportsVersion(world.GeneratorVersion))
                throw new NotSupportedException($"Generator version {world.GeneratorVersion} is not supported (this build: {Version}).");
            return Generate(new WorldGenerationRequest(world.WorldName, world.Seed, world.WorldSettings, world.WorldId)).Map;
        }

        public GeneratedWorld Generate(WorldGenerationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.WorldName)) throw new ArgumentException("World name is required.", nameof(request));
            if (request.Settings == null) throw new ArgumentException("World settings are required.", nameof(request));

            var root = SeedMixer.FromWorldSeed(request.Seed);
            IRandomSource Stream(string stage) => _randomFactory.Create(SeedMixer.Combine(root, stage));

            var map = new RegionMap(_settings.Width, _settings.Depth, _settings.CellSize, _settings.MaxElevation, _settings.WaterLevel);
            var layout = ChooseLayout(Stream("layout"));
            BuildElevation(map, root, layout);
            CarveLake(map, root, layout);
            var river = CarveRiver(map, Stream("river"), layout);
            var riverSet = new HashSet<MapCoord>(river);
            var forest = new LatticeNoise(SeedMixer.Combine(root, "forest"));
            Classify(map, riverSet, forest, layout);

            var sites = PlaceSettlements(map, Stream("settlements"));
            Flatten(map, sites);
            Classify(map, riverSet, forest, layout);
            ClearAroundSites(map, sites);
            if (river.Count >= 2) map.AddRiver(river);

            var world = BuildWorldData(request, map, sites, Stream("ids"), Stream("names"), Stream("stats"));
            BuildRoads(map, world.Settlements);

            var worldValidation = WorldValidator.Validate(world);
            var mapValidation = RegionMapValidator.Validate(map, world.Settlements);
            if (!worldValidation.IsValid || !mapValidation.IsValid)
                throw new WorldGenerationException($"Seed {request.Seed} produced invalid output: {worldValidation} {mapValidation}");

            return new GeneratedWorld(world, map);
        }

        // ----- Layout --------------------------------------------------------------------

        private struct Layout
        {
            public float DirX, DirZ;      // unit vector pointing toward the mountain side
            public float LakeX, LakeZ;    // cell coordinates
            public float LakeRadius;      // cells
            public int SourceX, SourceZ;  // river source cell
        }

        private Layout ChooseLayout(IRandomSource rng)
        {
            var d = rng.NextInt(0, 8);
            var dx = Directions[d, 0];
            var dz = Directions[d, 1];
            var px = -dz; // perpendicular
            var pz = dx;
            var w = _settings.Width - 1;
            var h = _settings.Depth - 1;

            var lakeOffset = rng.Range(-0.2f, 0.2f);
            var sourceOffset = rng.Range(-0.28f, 0.28f);
            return new Layout
            {
                DirX = dx,
                DirZ = dz,
                LakeX = (0.5f - dx * 0.27f + px * lakeOffset) * w,
                LakeZ = (0.5f - dz * 0.27f + pz * lakeOffset) * h,
                LakeRadius = rng.Range(9f, 13.5f),
                SourceX = Clamp((int)((0.5f + dx * 0.38f + px * sourceOffset) * w), 2, w - 2),
                SourceZ = Clamp((int)((0.5f + dz * 0.38f + pz * sourceOffset) * h), 2, h - 2),
            };
        }

        private float MountainMask(Layout layout, int x, int z)
        {
            var u = x / (float)(_settings.Width - 1) - 0.5f;
            var v = z / (float)(_settings.Depth - 1) - 0.5f;
            return SmoothStep(0.17f, 0.5f, u * layout.DirX + v * layout.DirZ);
        }

        // ----- Elevation -----------------------------------------------------------------

        private void BuildElevation(RegionMap map, ulong root, Layout layout)
        {
            var baseNoise = new LatticeNoise(SeedMixer.Combine(root, "elevation.base"));
            var hillNoise = new LatticeNoise(SeedMixer.Combine(root, "elevation.hills"));
            var ridgeNoise = new LatticeNoise(SeedMixer.Combine(root, "elevation.ridges"));

            for (var z = 0; z < map.Depth; z++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var b = baseNoise.Fbm(x / 40f, z / 40f, 5);
                    var hills = hillNoise.Fbm(x / 16f, z / 16f, 3);
                    var ridge = ridgeNoise.Ridged(x / 22f, z / 22f, 4);
                    var mask = MountainMask(layout, x, z);
                    var e = 0.37f + 0.2f * (b - 0.5f) + 0.07f * (hills - 0.5f) + mask * (0.16f + 0.44f * ridge);
                    map.SetElevation(x, z, e);
                }
            }
        }

        private static void CarveLake(RegionMap map, ulong root, Layout layout)
        {
            var wobble = new LatticeNoise(SeedMixer.Combine(root, "lake.shape"));
            var reach = layout.LakeRadius * 1.6f;
            var bed = map.WaterLevel - 0.1f;

            for (var z = 0; z < map.Depth; z++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var dx = x - layout.LakeX;
                    var dz = z - layout.LakeZ;
                    var r = reach * (0.82f + 0.36f * wobble.Sample(x / 7f, z / 7f));
                    var d2 = (dx * dx + dz * dz) / (r * r);
                    if (d2 >= 1f) continue;
                    var t = SmoothStep(0f, 1f, 1f - d2);
                    var e = map.GetElevation(x, z);
                    map.SetElevation(x, z, e + (bed - e) * t);
                }
            }
        }

        // ----- River ---------------------------------------------------------------------

        private List<MapCoord> CarveRiver(RegionMap map, IRandomSource rng, Layout layout)
        {
            // Meandering centre line by recursive midpoint displacement.
            var points = new List<(float x, float z)> { (layout.SourceX, layout.SourceZ), (layout.LakeX, layout.LakeZ) };
            for (var level = 0; level < 5; level++)
            {
                var next = new List<(float x, float z)>(points.Count * 2);
                for (var i = 0; i < points.Count - 1; i++)
                {
                    var a = points[i];
                    var b = points[i + 1];
                    var mx = (a.x + b.x) * 0.5f;
                    var mz = (a.z + b.z) * 0.5f;
                    var sx = b.x - a.x;
                    var sz = b.z - a.z;
                    var offset = rng.Range(-0.22f, 0.22f);
                    next.Add(a);
                    next.Add((mx - sz * offset, mz + sx * offset));
                }

                next.Add(points[points.Count - 1]);
                points = next;
            }

            // Rasterize to a 4-connected cell path, stopping where the river enters open water.
            var cells = new List<MapCoord>();
            var index = new Dictionary<MapCoord, int>();
            void Visit(int x, int z)
            {
                var c = new MapCoord(Clamp(x, 0, map.Width - 1), Clamp(z, 0, map.Depth - 1));
                if (cells.Count > 0 && cells[cells.Count - 1] == c) return;
                if (index.TryGetValue(c, out var existing))
                {
                    // Remove loops so the path never crosses itself.
                    for (var k = cells.Count - 1; k > existing; k--) { index.Remove(cells[k]); cells.RemoveAt(k); }
                    return;
                }

                index[c] = cells.Count;
                cells.Add(c);
            }

            for (var i = 0; i < points.Count - 1; i++)
            {
                var x0 = (int)Math.Round(points[i].x);
                var z0 = (int)Math.Round(points[i].z);
                var x1 = (int)Math.Round(points[i + 1].x);
                var z1 = (int)Math.Round(points[i + 1].z);
                Visit(x0, z0);
                while (x0 != x1 || z0 != z1)
                {
                    if (Math.Abs(x1 - x0) >= Math.Abs(z1 - z0)) x0 += Math.Sign(x1 - x0);
                    else z0 += Math.Sign(z1 - z0);
                    Visit(x0, z0);
                }
            }

            var end = cells.Count;
            for (var i = 0; i < cells.Count; i++)
            {
                if (map.GetElevation(cells[i].X, cells[i].Z) < map.WaterLevel - 0.02f) { end = i; break; }
            }

            cells.RemoveRange(end, cells.Count - end);
            if (cells.Count < 2) return cells;

            // Monotonically descending bed, then carve a valley around it.
            var floor = map.WaterLevel - 0.02f;
            var bed = new float[cells.Count];
            var previous = map.GetElevation(cells[0].X, cells[0].Z) - 0.02f;
            for (var i = 0; i < cells.Count; i++)
            {
                var e = map.GetElevation(cells[i].X, cells[i].Z) - 0.015f;
                previous = Math.Max(floor, Math.Min(previous - 0.0006f, e));
                bed[i] = previous;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                for (var dz = -3; dz <= 3; dz++)
                {
                    for (var dx = -3; dx <= 3; dx++)
                    {
                        var x = c.X + dx;
                        var z = c.Z + dz;
                        if (!map.InBounds(x, z)) continue;
                        var d2 = dx * dx + dz * dz;
                        if (d2 > 9) continue;
                        var allowed = bed[i] + 0.011f * d2;
                        if (map.GetElevation(x, z) > allowed) map.SetElevation(x, z, allowed);
                    }
                }
            }

            return cells;
        }

        // ----- Classification ------------------------------------------------------------

        private void Classify(RegionMap map, HashSet<MapCoord> river, LatticeNoise forest, Layout layout)
        {
            var wl = map.WaterLevel;
            for (var z = 0; z < map.Depth; z++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var e = map.GetElevation(x, z);
                    TerrainType t;
                    if (river.Contains(new MapCoord(x, z))) t = TerrainType.River;
                    else if (e < wl - 0.06f) t = TerrainType.DeepWater;
                    else if (e < wl) t = TerrainType.ShallowWater;
                    else
                    {
                        var slope = Slope(map, x, z);
                        if (e > 0.68f || (e > 0.58f && slope > 0.03f)) t = TerrainType.Mountain;
                        else if (e > 0.5f || slope > 0.022f) t = TerrainType.Hills;
                        else t = TerrainType.Plains;
                    }

                    map.SetTerrain(x, z, t);
                }
            }

            // Shores and forests are second-pass refinements over the base classes.
            for (var z = 0; z < map.Depth; z++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var t = map.GetTerrain(x, z);
                    if (t != TerrainType.Plains && t != TerrainType.Hills) continue;
                    var e = map.GetElevation(x, z);
                    if (e < wl + 0.025f && NextToOpenWater(map, x, z))
                    {
                        map.SetTerrain(x, z, TerrainType.Shore);
                        continue;
                    }

                    var density = forest.Fbm(x / 14f, z / 14f, 4) + 0.12f * MountainMask(layout, x, z);
                    if (density > 0.575f) map.SetTerrain(x, z, TerrainType.Forest);
                }
            }
        }

        private static float Slope(RegionMap map, int x, int z)
        {
            var e = map.GetElevation(x, z);
            var s = 0f;
            if (x > 0) s = Math.Max(s, Math.Abs(e - map.GetElevation(x - 1, z)));
            if (x < map.Width - 1) s = Math.Max(s, Math.Abs(e - map.GetElevation(x + 1, z)));
            if (z > 0) s = Math.Max(s, Math.Abs(e - map.GetElevation(x, z - 1)));
            if (z < map.Depth - 1) s = Math.Max(s, Math.Abs(e - map.GetElevation(x, z + 1)));
            return s;
        }

        private static bool NextToOpenWater(RegionMap map, int x, int z)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    var nz = z + dz;
                    if (!map.InBounds(nx, nz)) continue;
                    var t = map.GetTerrain(nx, nz);
                    if (t == TerrainType.DeepWater || t == TerrainType.ShallowWater) return true;
                }
            }

            return false;
        }

        // ----- Settlements ---------------------------------------------------------------

        private struct Site
        {
            public MapCoord Cell;
            public SettlementType Type;
            public int Radius;
        }

        private List<Site> PlaceSettlements(RegionMap map, IRandomSource rng)
        {
            var waterDistance = WaterDistanceField(map, 40);
            var margin = _settings.EdgeMargin;
            var candidates = new List<(float score, int order, MapCoord cell)>();

            for (var z = margin; z < map.Depth - margin; z++)
            {
                for (var x = margin; x < map.Width - margin; x++)
                {
                    var jitter = (float)rng.NextDouble(); // drawn for every cell to keep the stream aligned
                    if (map.GetTerrain(x, z) != TerrainType.Plains) continue;
                    var d = waterDistance[z * map.Width + x];
                    if (d < 3) continue;
                    if (!IsFlat(map, x, z, 2, 0.03f)) continue;

                    var fertility = CountTerrain(map, new MapCoord(x, z), 7, TerrainType.Plains, TerrainType.Shore) / 150f;
                    var water = d <= 6 ? 1f : Math.Max(0f, 1f - (d - 6) / 10f);
                    candidates.Add((fertility + 0.5f * water + 0.1f * jitter, candidates.Count, new MapCoord(x, z)));
                }
            }

            candidates.Sort((a, b) => a.score != b.score ? b.score.CompareTo(a.score) : a.order.CompareTo(b.order));

            foreach (var minDistance in new[] { 34, 26, 18, 12 })
            {
                var chosen = new List<Site>();
                foreach (var c in candidates)
                {
                    var ok = true;
                    foreach (var s in chosen)
                    {
                        if (s.Cell.DistanceSquared(c.cell) < minDistance * minDistance) { ok = false; break; }
                    }

                    if (!ok) continue;
                    var type = SiteTypes[chosen.Count];
                    chosen.Add(new Site { Cell = c.cell, Type = type, Radius = type == SettlementType.Town ? 5 : type == SettlementType.Village ? 4 : 3 });
                    if (chosen.Count == SiteTypes.Length) return chosen;
                }
            }

            throw new WorldGenerationException($"Could not find {SiteTypes.Length} valid settlement sites.");
        }

        private static int[] WaterDistanceField(RegionMap map, int cap)
        {
            var w = map.Width;
            var field = new int[w * map.Depth];
            var queue = new Queue<int>();
            for (var i = 0; i < field.Length; i++)
            {
                if (map.IsWater(i % w, i / w)) { field[i] = 0; queue.Enqueue(i); }
                else field[i] = cap;
            }

            while (queue.Count > 0)
            {
                var i = queue.Dequeue();
                var x = i % w;
                var z = i / w;
                var next = field[i] + 1;
                if (next >= cap) continue;
                void Relax(int nx, int nz)
                {
                    if (!map.InBounds(nx, nz)) return;
                    var ni = nz * w + nx;
                    if (field[ni] <= next) return;
                    field[ni] = next;
                    queue.Enqueue(ni);
                }

                Relax(x + 1, z);
                Relax(x - 1, z);
                Relax(x, z + 1);
                Relax(x, z - 1);
            }

            return field;
        }

        private static bool IsFlat(RegionMap map, int x, int z, int radius, float tolerance)
        {
            var e = map.GetElevation(x, z);
            for (var dz = -radius; dz <= radius; dz++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (!map.InBounds(x + dx, z + dz)) return false;
                    if (Math.Abs(map.GetElevation(x + dx, z + dz) - e) > tolerance) return false;
                }
            }

            return true;
        }

        private static int CountTerrain(RegionMap map, MapCoord center, int radius, TerrainType a, TerrainType b = TerrainType.DeepWater, TerrainType c = TerrainType.DeepWater)
        {
            var count = 0;
            for (var dz = -radius; dz <= radius; dz++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dz * dz > radius * radius) continue;
                    var x = center.X + dx;
                    var z = center.Z + dz;
                    if (!map.InBounds(x, z)) continue;
                    var t = map.GetTerrain(x, z);
                    if (t == a || (b != TerrainType.DeepWater && t == b) || (c != TerrainType.DeepWater && t == c)) count++;
                }
            }

            return count;
        }

        private static void Flatten(RegionMap map, List<Site> sites)
        {
            foreach (var site in sites)
            {
                var target = Math.Max(map.GetElevation(site.Cell.X, site.Cell.Z), map.WaterLevel + 0.03f);
                var reach = site.Radius + 4;
                for (var dz = -reach; dz <= reach; dz++)
                {
                    for (var dx = -reach; dx <= reach; dx++)
                    {
                        var x = site.Cell.X + dx;
                        var z = site.Cell.Z + dz;
                        if (!map.InBounds(x, z) || map.IsWater(x, z)) continue;
                        var d = (float)Math.Sqrt(dx * dx + dz * dz);
                        if (d > reach) continue;
                        var w = d <= site.Radius ? 1f : 1f - (d - site.Radius) / 4f;
                        var e = map.GetElevation(x, z);
                        map.SetElevation(x, z, e + (target - e) * w);
                    }
                }
            }
        }

        private static void ClearAroundSites(RegionMap map, List<Site> sites)
        {
            foreach (var site in sites)
            {
                var r = site.Radius + 1;
                for (var dz = -r; dz <= r; dz++)
                {
                    for (var dx = -r; dx <= r; dx++)
                    {
                        var x = site.Cell.X + dx;
                        var z = site.Cell.Z + dz;
                        if (!map.InBounds(x, z) || dx * dx + dz * dz > r * r || map.IsWater(x, z)) continue;
                        map.SetTerrain(x, z, TerrainType.Plains);
                    }
                }
            }
        }

        // ----- World data ----------------------------------------------------------------

        private WorldData BuildWorldData(WorldGenerationRequest request, RegionMap map, List<Site> sites,
            IRandomSource ids, IRandomSource names, IRandomSource stats)
        {
            var worldId = request.WorldId.IsEmpty ? WorldId.NewRandom() : request.WorldId;
            var world = WorldData.CreateNew(worldId, request.WorldName, request.Seed, Version, request.Settings);
            var nameGen = new NameGenerator(names);

            var settlements = new List<SettlementData>();
            foreach (var site in sites)
            {
                var s = new SettlementData
                {
                    SettlementId = new SettlementId(DeterministicGuid.Next(ids)),
                    Name = nameGen.NextPlaceName(),
                    SettlementType = site.Type,
                    Position = site.Cell,
                };
                InitializeStats(map, s, stats);
                settlements.Add(s);
            }

            var crown = new FactionData { FactionId = new FactionId(DeterministicGuid.Next(ids)), Name = "The Crown of " + settlements[0].Name };
            var freeFolk = new FactionData { FactionId = new FactionId(DeterministicGuid.Next(ids)), Name = "Free Folk of " + settlements[2].Name };
            world.Factions.Add(crown);
            world.Factions.Add(freeFolk);
            settlements[0].FactionId = crown.FactionId;
            settlements[1].FactionId = crown.FactionId;
            settlements[2].FactionId = freeFolk.FactionId;
            world.Settlements.AddRange(settlements);

            world.HistoricalEvents.Add(new HistoricalEvent(
                new EventId(DeterministicGuid.Next(ids)), world.CurrentSimulationDate, HistoricalEventType.WorldFounded,
                EventSeverity.Info, SettlementId.Empty, $"The chronicle of {world.WorldName} begins."));
            return world;
        }

        private void InitializeStats(RegionMap map, SettlementData s, IRandomSource rng)
        {
            switch (s.SettlementType)
            {
                case SettlementType.Town: s.Population = rng.NextInt(1600, 2200); break;
                case SettlementType.Village: s.Population = rng.NextInt(450, 750); break;
                default: s.Population = rng.NextInt(120, 220); break;
            }

            s.Prosperity = rng.NextInt(48, 70);
            s.Stability = rng.NextInt(55, 73);

            // Land-limited vs labour-limited food: towns outgrow their fields and rely on reserves.
            var fertile = CountTerrain(map, s.Position, 7, TerrainType.Plains, TerrainType.Shore);
            s.FoodProductionPerDay = Math.Min(fertile, s.Population * 12 / 100);

            var consumption = _foodRules.DailyConsumption(s.Population);
            s.ResourceInventory.Set(ResourceTypes.Food, consumption * rng.NextInt(45, 76));
            s.ResourceInventory.Set(ResourceTypes.Timber, CountTerrain(map, s.Position, 10, TerrainType.Forest) * 4);
            s.ResourceInventory.Set(ResourceTypes.Stone, CountTerrain(map, s.Position, 10, TerrainType.Hills, TerrainType.Mountain) * 3);
            s.ResourceInventory.Set(ResourceTypes.Ore, CountTerrain(map, s.Position, 12, TerrainType.Mountain) * 2);
            s.ResourceInventory.Set(ResourceTypes.Coin, s.Population / 2 + rng.NextInt(0, 100));
        }

        private static void BuildRoads(RegionMap map, IReadOnlyList<SettlementData> settlements)
        {
            var planner = new RoadPlanner(map);
            for (var i = 0; i < settlements.Count; i++)
            {
                for (var j = i + 1; j < settlements.Count; j++)
                {
                    var path = planner.FindPath(settlements[i].Position, settlements[j].Position);
                    if (path == null)
                        throw new WorldGenerationException($"No road route between {settlements[i].Name} and {settlements[j].Name}.");
                    map.AddRoad(new RoadPath(settlements[i].SettlementId, settlements[j].SettlementId, path));
                }
            }
        }

        // ----- Math helpers --------------------------------------------------------------

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            var t = (x - edge0) / (edge1 - edge0);
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }
    }
}
