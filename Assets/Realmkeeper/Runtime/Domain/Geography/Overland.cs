using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Geography
{
    /// <summary>
    /// Phase 23 (ADR-0110): the walkable scale of a region. The simulation keeps its travel scale (a map cell is
    /// <see cref="TravelRules.KmPerCell"/> of road for caravans, journeys and trade); the walkable overland projects the
    /// same cells at <see cref="MetresPerCell"/> metres, so the strategic map and the ground the player walks are one
    /// geography at two scales, and every metre walked advances the one clock by the travel time it represents.
    /// Positions are region-local metres from the map's (0, 0) corner: x east, z north, y up.
    /// </summary>
    public static class OverlandScale
    {
        /// <summary>Walkable metres per map cell: a 128-cell region is 5.12 km a side (26.2 km²).</summary>
        public const float MetresPerCell = 40f;

        /// <summary>Height in metres of normalised elevation 1.0.</summary>
        public const float VerticalMetres = 240f;

        /// <summary>Road kilometres one walkable metre stands for (1.2 km per 40 m: 30).</summary>
        public static double TravelKmPerMetre => TravelRules.KmPerCell / MetresPerCell;

        public static float RegionSizeMetres(RegionMap map) => map.Width * MetresPerCell;

        /// <summary>The cell a point stands in (clamped to the map).</summary>
        public static MapCoord CellAt(RegionMap map, float x, float z) =>
            new MapCoord(Clamp((int)Math.Floor(x / MetresPerCell), 0, map.Width - 1), Clamp((int)Math.Floor(z / MetresPerCell), 0, map.Depth - 1));

        /// <summary>The centre of a cell in metres.</summary>
        public static (float x, float z) CellCentre(MapCoord c) => ((c.X + 0.5f) * MetresPerCell, (c.Z + 0.5f) * MetresPerCell);

        public static bool Inside(RegionMap map, float x, float z) => x >= 0f && z >= 0f && x < map.Width * MetresPerCell && z < map.Depth * MetresPerCell;

        internal static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }

    /// <summary>Phase 23: what the ground is, for the overland's look and its rules (derived from the map; never saved).</summary>
    public enum Biome : byte
    {
        Lake = 0,
        River = 1,
        Shallows = 2,
        Shore = 3,
        Grassland = 4,
        Meadow = 5,
        Forest = 6,
        DenseForest = 7,
        Hills = 8,
        Rocky = 9,
        Mountain = 10,
        Marsh = 11,
        Valley = 12,
        Heath = 13,

        /// <summary>No current region kind makes it (there is no arid country yet); kept so a realm or region can.</summary>
        Desert = 14,
    }

    /// <summary>One cell's overland rules: what it is, whether it can be walked, how slow it is, and how it hides and shows.</summary>
    public readonly struct OverlandCell
    {
        public OverlandCell(Biome biome, bool passable, float cost, bool road, bool crossing, float concealment, float sightBonus)
        {
            Biome = biome;
            Passable = passable;
            Cost = cost;
            Road = road;
            Crossing = crossing;
            Concealment = concealment;
            SightBonus = sightBonus;
        }

        public Biome Biome { get; }
        public bool Passable { get; }

        /// <summary>Time multiplier against a good road (1 = road).</summary>
        public float Cost { get; }

        public bool Road { get; }

        /// <summary>A way over water or through mountains: a bridge, a ford or a pass.</summary>
        public bool Crossing { get; }

        /// <summary>0–1: how well the ground hides someone (dense forest most).</summary>
        public float Concealment { get; }

        /// <summary>Extra sight from high ground, 0–1.</summary>
        public float SightBonus { get; }
    }

    /// <summary>
    /// Phase 23 (ADR-0110): reads the region map as walkable country. Deterministic and engine-free: biomes from terrain,
    /// elevation, neighbours and the region's kind; passability (mountains and deep water bar the way except by a road or
    /// pass; rivers except at bridges, which are road cells, and fords); a travel-time cost per biome; concealment; and the
    /// ground height at any point, with detail noise below the cell. The same answers drive the 3D ground, its barriers and
    /// the clock.
    /// </summary>
    public sealed class OverlandTerrain
    {
        private readonly RegionMap _map;
        private readonly RegionKind _kind;
        private readonly long _seed;
        private readonly OverlandCell[] _cells;
        private readonly float[] _heights; // per cell corner, metres

        public OverlandTerrain(RegionMap map, RegionKind kind, long seed)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _kind = kind;
            _seed = seed;
            _cells = new OverlandCell[map.Width * map.Depth];
            MarkFords();
            for (var z = 0; z < map.Depth; z++)
                for (var x = 0; x < map.Width; x++)
                    _cells[z * map.Width + x] = Classify(x, z);
            _heights = new float[(map.Width + 1) * (map.Depth + 1)];
            for (var z = 0; z <= map.Depth; z++)
                for (var x = 0; x <= map.Width; x++)
                    _heights[z * (map.Width + 1) + x] = CornerHeight(x, z);
        }

        public RegionMap Map => _map;
        public RegionKind Kind => _kind;
        public float SizeX => _map.Width * OverlandScale.MetresPerCell;
        public float SizeZ => _map.Depth * OverlandScale.MetresPerCell;
        public float WaterHeight => _map.WaterLevel * OverlandScale.VerticalMetres;

        public OverlandCell Cell(int x, int z) => _cells[OverlandScale.Clamp(z, 0, _map.Depth - 1) * _map.Width + OverlandScale.Clamp(x, 0, _map.Width - 1)];

        public OverlandCell CellAt(float x, float z)
        {
            var c = OverlandScale.CellAt(_map, x, z);
            return Cell(c.X, c.Z);
        }

        /// <summary>Whether a point can be stood on (inside the region and on passable ground).</summary>
        public bool Walkable(float x, float z) => OverlandScale.Inside(_map, x, z) && CellAt(x, z).Passable;

        // ----- Heights ------------------------------------------------------------------------------------

        /// <summary>The ground height in metres at a point: corner heights interpolated, plus detail below the cell.</summary>
        public float HeightAt(float x, float z)
        {
            var fx = Math.Max(0f, Math.Min(_map.Width - 0.0001f, x / OverlandScale.MetresPerCell));
            var fz = Math.Max(0f, Math.Min(_map.Depth - 0.0001f, z / OverlandScale.MetresPerCell));
            var ix = (int)fx;
            var iz = (int)fz;
            var tx = Smooth(fx - ix);
            var tz = Smooth(fz - iz);
            var w = _map.Width + 1;
            var h00 = _heights[iz * w + ix];
            var h10 = _heights[iz * w + ix + 1];
            var h01 = _heights[(iz + 1) * w + ix];
            var h11 = _heights[(iz + 1) * w + ix + 1];
            var h = Lerp(Lerp(h00, h10, tx), Lerp(h01, h11, tx), tz);
            var cell = _cells[iz * _map.Width + ix];
            var rough = Roughness(cell.Biome);
            if (cell.Road) rough *= 0.15f;
            return h + rough * (Noise(x * 0.045f, z * 0.045f) - 0.5f) + rough * 0.4f * (Noise(x * 0.13f + 31f, z * 0.13f - 17f) - 0.5f);
        }

        private float CornerHeight(int cx, int cz)
        {
            // The mean of the cells touching the corner, mountains raised into peaks, water pressed below the surface.
            float sum = 0f;
            var n = 0;
            var mountain = 0;
            var water = 0;
            var road = 0;
            for (var dz = -1; dz <= 0; dz++)
                for (var dx = -1; dx <= 0; dx++)
                {
                    var x = cx + dx;
                    var z = cz + dz;
                    if (!_map.InBounds(x, z)) continue;
                    sum += _map.GetElevation(x, z);
                    n++;
                    var t = _map.GetTerrain(x, z);
                    if (t == TerrainType.Mountain && !_map.IsRoad(x, z)) mountain++;
                    if (t == TerrainType.DeepWater || t == TerrainType.River || t == TerrainType.ShallowWater) water++;
                    if (_map.IsRoad(x, z)) road++;
                }

            var e = n == 0 ? _map.WaterLevel : sum / n;
            var h = e * OverlandScale.VerticalMetres;
            if (mountain >= 3) h += 55f + 70f * Noise(cx * 0.37f + 5f, cz * 0.37f - 9f); // crags: too steep to climb
            else if (mountain > 0) h += 18f * mountain;
            if (water > 0 && road == 0) h = Math.Min(h, WaterHeight - 1.2f * water); // beds below the surface
            return h;
        }

        private static float Roughness(Biome b)
        {
            switch (b)
            {
                case Biome.Mountain: return 14f;
                case Biome.Rocky: return 6f;
                case Biome.Hills: return 4.5f;
                case Biome.Heath: return 2.5f;
                case Biome.Forest:
                case Biome.DenseForest: return 2.2f;
                case Biome.Marsh:
                case Biome.Shore: return 0.6f;
                case Biome.Lake:
                case Biome.River:
                case Biome.Shallows: return 0.3f;
                default: return 1.4f;
            }
        }

        // ----- Classification ----------------------------------------------------------------------------

        private OverlandCell Classify(int x, int z)
        {
            var t = _map.GetTerrain(x, z);
            var road = _map.IsRoad(x, z);
            var e = _map.GetElevation(x, z);
            switch (t)
            {
                case TerrainType.DeepWater:
                    return road ? new OverlandCell(Biome.Shallows, true, 1.4f, true, true, 0f, 0f) : new OverlandCell(Biome.Lake, false, 0f, false, false, 0f, 0f);
                case TerrainType.River:
                    if (road) return new OverlandCell(Biome.River, true, 1.05f, true, true, 0f, 0f); // a bridge: the road goes over
                    return IsFord(x, z) ? new OverlandCell(Biome.Shallows, true, 2.6f, false, true, 0f, 0f) : new OverlandCell(Biome.River, false, 0f, false, false, 0f, 0f);
                case TerrainType.ShallowWater:
                    return new OverlandCell(Biome.Shallows, true, road ? 1.3f : 2.4f, road, road, 0f, 0f);
                case TerrainType.Shore:
                    return new OverlandCell(NearWater(x, z, 1) && Wet() ? Biome.Marsh : Biome.Shore, true, road ? 1f : NearWater(x, z, 1) && Wet() ? 2.1f : 1.3f, road, false, 0.1f, 0f);
                case TerrainType.Forest:
                {
                    var dense = Count(x, z, 2, TerrainType.Forest) >= 21;
                    return new OverlandCell(dense ? Biome.DenseForest : Biome.Forest, true, road ? 1.05f : dense ? 2.0f : 1.6f, road, false, dense ? 0.8f : 0.5f, 0f);
                }
                case TerrainType.Hills:
                {
                    var rocky = e > 0.7f || Count(x, z, 1, TerrainType.Mountain) >= 2;
                    return new OverlandCell(rocky ? Biome.Rocky : Biome.Hills, true, road ? 1.1f : rocky ? 1.9f : 1.5f, road, false, 0.2f, rocky ? 0.5f : 0.35f);
                }
                case TerrainType.Mountain:
                    return road ? new OverlandCell(Biome.Rocky, true, 1.6f, true, true, 0.1f, 0.6f) : new OverlandCell(Biome.Mountain, false, 0f, false, false, 0f, 0.8f);
                default: // plains
                {
                    Biome b;
                    if (NearWater(x, z, 2) && e < _map.WaterLevel + 0.06f && Wet()) b = Biome.Marsh;
                    else if (Count(x, z, 6, TerrainType.Mountain) >= 10 && Count(x, z, 6, TerrainType.Hills) >= 10) b = Biome.Valley;
                    else if (_kind == RegionKind.Highlands && e > 0.55f) b = Biome.Heath;
                    else b = Hash(x, z, 7) % 4 == 0 ? Biome.Meadow : Biome.Grassland;
                    var cost = road ? 1f : b == Biome.Marsh ? 2.2f : b == Biome.Heath ? 1.35f : 1.25f;
                    return new OverlandCell(b, true, cost, road, false, b == Biome.Meadow ? 0.15f : 0.05f, 0f);
                }
            }
        }

        private bool Wet() => _kind == RegionKind.Lakeland || _kind == RegionKind.Lowlands || _kind == RegionKind.Woodland;

        private readonly HashSet<int> _fords = new HashSet<int>();

        /// <summary>Cells along a river's course between fords (12 cells: about 480 m walked).</summary>
        public const int FordSpacing = 12;

        /// <summary>
        /// Fords, deterministically along each river's course: one every <see cref="FordSpacing"/> cells from an offset the
        /// river's source decides, spanning every river cell within one cell of the course (rivers are wider than a cell). So a
        /// river is always crossable somewhere, and never everywhere; roads cross it by their bridges as well.
        /// </summary>
        private void MarkFords()
        {
            foreach (var river in _map.Rivers)
            {
                if (river.Count < 3) continue;
                var start = Hash(river[0].X, river[0].Z, 13) % FordSpacing;
                for (var i = start; i < river.Count; i += FordSpacing)
                {
                    var c = river[i];
                    for (var dz = -1; dz <= 1; dz++)
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var x = c.X + dx;
                            var z = c.Z + dz;
                            if (_map.InBounds(x, z) && _map.GetTerrain(x, z) == TerrainType.River) _fords.Add(z * _map.Width + x);
                        }
                }
            }
        }

        private bool IsFord(int x, int z) => _fords.Contains(z * _map.Width + x);

        private bool NearWater(int x, int z, int r)
        {
            for (var dz = -r; dz <= r; dz++)
                for (var dx = -r; dx <= r; dx++)
                    if (_map.InBounds(x + dx, z + dz) && _map.IsWater(x + dx, z + dz)) return true;
            return false;
        }

        private int Count(int x, int z, int r, TerrainType t)
        {
            var n = 0;
            for (var dz = -r; dz <= r; dz++)
                for (var dx = -r; dx <= r; dx++)
                    if (_map.InBounds(x + dx, z + dz) && _map.GetTerrain(x + dx, z + dz) == t) n++;
            return n;
        }

        // ----- Deterministic noise -----------------------------------------------------------------------

        private int Hash(int x, int z, int salt)
        {
            unchecked
            {
                var h = (uint)(_seed * 73856093) ^ (uint)(x * 19349663) ^ (uint)(z * 83492791) ^ (uint)(salt * 2654435761);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }

        private float Lattice(int x, int z) => Hash(x, z, 101) / (float)int.MaxValue;

        /// <summary>Smooth value noise, 0–1.</summary>
        public float Noise(float x, float z)
        {
            var ix = (int)Math.Floor(x);
            var iz = (int)Math.Floor(z);
            var tx = Smooth(x - ix);
            var tz = Smooth(z - iz);
            return Lerp(Lerp(Lattice(ix, iz), Lattice(ix + 1, iz), tx), Lerp(Lattice(ix, iz + 1), Lattice(ix + 1, iz + 1), tx), tz);
        }

        /// <summary>A deterministic number 0–1 for a cell and purpose (vegetation, scatter).</summary>
        public float Random01(int x, int z, int salt) => Hash(x, z, salt) / (float)int.MaxValue;

        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // ----- Travel ------------------------------------------------------------------------------------

        /// <summary>
        /// Game minutes for walking from one point to another, at the simulation's travel scale: each metre walked stands for
        /// <see cref="OverlandScale.TravelKmPerMetre"/> km of road at <see cref="TravelRules.FootKmPerHour"/>, times the ground's
        /// cost (sampled along the way). Night halves the pace, as on the roads.
        /// </summary>
        public double WalkMinutes(float x0, float z0, float x1, float z1, bool night, Realmkeeper.Domain.World.GroundConditions ground = null)
        {
            var dx = x1 - x0;
            var dz = z1 - z0;
            var d = Math.Sqrt(dx * dx + dz * dz);
            if (d <= 0.0001) return 0;
            var steps = Math.Max(1, (int)Math.Ceiling(d / (OverlandScale.MetresPerCell / 4f)));
            double cost = 0;
            for (var i = 0; i < steps; i++)
            {
                var t = (i + 0.5f) / steps;
                var c = CellAt(x0 + dx * t, z0 + dz * t);
                cost += (c.Passable ? c.Cost : 3f) * (ground?.FactorFor(c) ?? 1f); // Phase 24: mud, snow and ice (ADR-0115)
            }

            cost /= steps;
            var hours = d * OverlandScale.TravelKmPerMetre / TravelRules.FootKmPerHour * cost;
            return hours * 60.0 * (night ? 2.0 : 1.0);
        }
    }
}
