using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>Phase 16: the geographic character of a region, read from its own terrain.</summary>
    public enum RegionKind
    {
        Vale = 0,
        Highlands = 1,
        Woodland = 2,
        Lakeland = 3,
        Lowlands = 4,
    }

    /// <summary>Phase 16: what the player knows of a region.</summary>
    public enum RegionDiscovery
    {
        Unknown = 0,
        Rumored = 1,
        Discovered = 2,
        Visited = 3,
    }

    /// <summary>
    /// A geographic region of the world: a stable identity plus the rectangle of map cells it
    /// covers. Settlements, roads and local environments belong to exactly one region.
    /// Identity is a GUID drawn from a seeded stream, never derived from the name, bounds or
    /// list position. Phase 16 (ADR-0076): each region also has its place on the world atlas, its
    /// neighbours, the seed and mountain side its own geography is regenerated from, its kind, and
    /// what the player knows of it. Settlement positions are cells of their own region's map.
    /// </summary>
    public sealed class RegionData
    {
        public const int MaxNameLength = 64;

        public RegionId RegionId { get; set; }
        public string Name { get; set; }

        /// <summary>Lowest cell (inclusive) covered by the region.</summary>
        public MapCoord Origin { get; set; }

        /// <summary>Size in cells along X.</summary>
        public int Width { get; set; }

        /// <summary>Size in cells along Z.</summary>
        public int Depth { get; set; }

        /// <summary>Phase 16: place on the world atlas grid (the home region is 0,0).</summary>
        public int AtlasX { get; set; }
        public int AtlasZ { get; set; }

        /// <summary>Phase 16: the seed its geography is generated from (the world's seed for the home region).</summary>
        public long Seed { get; set; }

        /// <summary>Phase 16: the side its mountains lie on (0–7, the generator's directions); −1: chosen by its seed.</summary>
        public int MountainDirection { get; set; } = -1;

        /// <summary>Phase 16: the generator version its geography comes from.</summary>
        public int GeneratorVersion { get; set; }

        public RegionKind Kind { get; set; }
        public RegionDiscovery Discovery { get; set; }

        /// <summary>Phase 16: regions it shares a border and a crossing with.</summary>
        public List<RegionId> Neighbours { get; set; } = new List<RegionId>();

        /// <summary>Phase 16: notable features, in words (its lake, its river, its passes).</summary>
        public List<string> Landmarks { get; set; } = new List<string>();

        public bool Contains(MapCoord cell) =>
            cell.X >= Origin.X && cell.Z >= Origin.Z && cell.X < Origin.X + Width && cell.Z < Origin.Z + Depth;

        public RegionData Clone()
        {
            var r = (RegionData)MemberwiseClone();
            r.Neighbours = new List<RegionId>(Neighbours ?? new List<RegionId>());
            r.Landmarks = new List<string>(Landmarks ?? new List<string>());
            return r;
        }
    }
}
