using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Geography
{
    /// <summary>Inclusive rectangle of changed cells, used by renderers to rebuild only what changed.</summary>
    public readonly struct RegionBounds
    {
        public RegionBounds(int minX, int minZ, int maxX, int maxZ)
        {
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }
    }

    public enum TerrainEditKind
    {
        SetElevation = 0,
        SetTerrain = 1,
    }

    /// <summary>A recorded manual edit. Intended future use: persisted as deltas on top of the generated map.</summary>
    public readonly struct TerrainEdit
    {
        public TerrainEdit(TerrainEditKind kind, MapCoord cell, float elevation, TerrainType terrain)
        {
            Kind = kind;
            Cell = cell;
            Elevation = elevation;
            Terrain = terrain;
        }

        public TerrainEditKind Kind { get; }
        public MapCoord Cell { get; }
        public float Elevation { get; }
        public TerrainType Terrain { get; }
    }

    /// <summary>
    /// Seam for the future World Editor. Edits operate on generated <see cref="RegionMap"/>
    /// data, so the generator never needs to know about manual changes.
    /// </summary>
    public interface IRegionMapEditor
    {
        RegionMap Map { get; }
        IReadOnlyList<TerrainEdit> Edits { get; }

        event Action<RegionBounds> RegionChanged;

        void SetElevation(MapCoord cell, float normalizedElevation);
        void SetTerrain(MapCoord cell, TerrainType terrain);

        /// <summary>Raises (positive delta) or lowers terrain with a linear falloff over the radius.</summary>
        void Sculpt(MapCoord center, int radius, float delta);
    }

    public sealed class RegionMapEditor : IRegionMapEditor
    {
        private readonly List<TerrainEdit> _edits = new List<TerrainEdit>();

        public RegionMapEditor(RegionMap map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public RegionMap Map { get; }
        public IReadOnlyList<TerrainEdit> Edits => _edits;

        public event Action<RegionBounds> RegionChanged;

        public void SetElevation(MapCoord cell, float normalizedElevation)
        {
            RequireInBounds(cell);
            Map.SetElevation(cell.X, cell.Z, normalizedElevation);
            _edits.Add(new TerrainEdit(TerrainEditKind.SetElevation, cell, Map.GetElevation(cell.X, cell.Z), Map.GetTerrain(cell.X, cell.Z)));
            RegionChanged?.Invoke(new RegionBounds(cell.X, cell.Z, cell.X, cell.Z));
        }

        public void SetTerrain(MapCoord cell, TerrainType terrain)
        {
            RequireInBounds(cell);
            Map.SetTerrain(cell.X, cell.Z, terrain);
            _edits.Add(new TerrainEdit(TerrainEditKind.SetTerrain, cell, Map.GetElevation(cell.X, cell.Z), terrain));
            RegionChanged?.Invoke(new RegionBounds(cell.X, cell.Z, cell.X, cell.Z));
        }

        public void Sculpt(MapCoord center, int radius, float delta)
        {
            RequireInBounds(center);
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

            var minX = Math.Max(0, center.X - radius);
            var maxX = Math.Min(Map.Width - 1, center.X + radius);
            var minZ = Math.Max(0, center.Z - radius);
            var maxZ = Math.Min(Map.Depth - 1, center.Z + radius);
            var r = radius + 1f;

            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var cell = new MapCoord(x, z);
                    var distance = (float)Math.Sqrt(cell.DistanceSquared(center));
                    if (distance > radius) continue;
                    var falloff = 1f - distance / r;
                    Map.SetElevation(x, z, Map.GetElevation(x, z) + delta * falloff);
                    _edits.Add(new TerrainEdit(TerrainEditKind.SetElevation, cell, Map.GetElevation(x, z), Map.GetTerrain(x, z)));
                }
            }

            RegionChanged?.Invoke(new RegionBounds(minX, minZ, maxX, maxZ));
        }

        private void RequireInBounds(MapCoord cell)
        {
            if (!Map.InBounds(cell)) throw new ArgumentOutOfRangeException(nameof(cell), $"Cell {cell} is outside the region.");
        }
    }
}
