using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Geography
{
    public enum TerrainType : byte
    {
        DeepWater = 0,
        ShallowWater = 1,
        River = 2,
        Shore = 3,
        Plains = 4,
        Forest = 5,
        Hills = 6,
        Mountain = 7,
    }

    /// <summary>A road between two settlements, as an ordered list of 4-connected grid cells.</summary>
    public sealed class RoadPath
    {
        public RoadPath(SettlementId from, SettlementId to, IReadOnlyList<MapCoord> cells)
        {
            From = from;
            To = to;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        }

        public SettlementId From { get; }
        public SettlementId To { get; }
        public IReadOnlyList<MapCoord> Cells { get; }
    }

    /// <summary>
    /// Geographical data for one region: a regular grid of cells with normalized elevation
    /// (0..1) and a terrain classification. Pure data: it knows nothing about meshes,
    /// materials or GameObjects. The presentation layer builds visuals from it.
    /// </summary>
    public sealed class RegionMap
    {
        private readonly float[] _elevation;
        private readonly TerrainType[] _terrain;
        private readonly bool[] _road;
        private readonly List<RoadPath> _roads = new List<RoadPath>();
        private readonly List<IReadOnlyList<MapCoord>> _rivers = new List<IReadOnlyList<MapCoord>>();

        public RegionMap(int width, int depth, float cellSize, float maxElevation, float waterLevel)
        {
            if (width < 8 || depth < 8) throw new ArgumentOutOfRangeException(nameof(width), "Region must be at least 8x8 cells.");
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (maxElevation <= 0f) throw new ArgumentOutOfRangeException(nameof(maxElevation));
            if (waterLevel <= 0f || waterLevel >= 1f) throw new ArgumentOutOfRangeException(nameof(waterLevel));

            Width = width;
            Depth = depth;
            CellSize = cellSize;
            MaxElevation = maxElevation;
            WaterLevel = waterLevel;
            _elevation = new float[width * depth];
            _terrain = new TerrainType[width * depth];
            _road = new bool[width * depth];
        }

        public int Width { get; }
        public int Depth { get; }

        /// <summary>World-space size of one cell (presentation scale hint).</summary>
        public float CellSize { get; }

        /// <summary>World-space height corresponding to normalized elevation 1.0.</summary>
        public float MaxElevation { get; }

        /// <summary>Normalized elevation of the water surface.</summary>
        public float WaterLevel { get; }

        /// <summary>Phase 16: the region this map is the geography of (empty for a map not yet tied to a region).</summary>
        public RegionId RegionId { get; set; }

        /// <summary>Phase 16: the side its mountains lie on (the generator's direction index 0–7).</summary>
        public int MountainDirection { get; set; } = -1;

        public IReadOnlyList<RoadPath> Roads => _roads;

        /// <summary>River centre lines, ordered from source to mouth.</summary>
        public IReadOnlyList<IReadOnlyList<MapCoord>> Rivers => _rivers;

        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Depth;
        public bool InBounds(MapCoord c) => InBounds(c.X, c.Z);

        public float GetElevation(int x, int z) => _elevation[Index(x, z)];
        public void SetElevation(int x, int z, float value) => _elevation[Index(x, z)] = Clamp01(value);

        public TerrainType GetTerrain(int x, int z) => _terrain[Index(x, z)];
        public void SetTerrain(int x, int z, TerrainType value) => _terrain[Index(x, z)] = value;

        public bool IsRoad(int x, int z) => _road[Index(x, z)];

        public bool IsWater(int x, int z)
        {
            var t = GetTerrain(x, z);
            return t == TerrainType.DeepWater || t == TerrainType.ShallowWater || t == TerrainType.River;
        }

        public void AddRoad(RoadPath road)
        {
            if (road == null) throw new ArgumentNullException(nameof(road));
            foreach (var cell in road.Cells)
            {
                if (!InBounds(cell)) throw new ArgumentOutOfRangeException(nameof(road), $"Road cell {cell} is outside the region.");
            }

            _roads.Add(road);
            foreach (var cell in road.Cells) _road[Index(cell.X, cell.Z)] = true;
        }

        public void AddRiver(IReadOnlyList<MapCoord> cells)
        {
            if (cells == null || cells.Count < 2) throw new ArgumentException("A river needs at least two cells.", nameof(cells));
            foreach (var cell in cells)
            {
                if (!InBounds(cell)) throw new ArgumentOutOfRangeException(nameof(cells), $"River cell {cell} is outside the region.");
            }

            _rivers.Add(cells);
        }

        /// <summary>Elevation in world units, for presentation.</summary>
        public float GetWorldHeight(int x, int z) => GetElevation(x, z) * MaxElevation;

        public float WaterWorldHeight => WaterLevel * MaxElevation;

        private int Index(int x, int z)
        {
            if (!InBounds(x, z)) throw new ArgumentOutOfRangeException(nameof(x), $"Cell ({x}, {z}) is outside the {Width}x{Depth} region.");
            return z * Width + x;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
