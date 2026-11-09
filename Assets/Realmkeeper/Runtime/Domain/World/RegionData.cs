using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// A geographic region of the world: a stable identity plus the rectangle of map cells it
    /// covers. Settlements, roads and local environments belong to exactly one region.
    /// Identity is a GUID drawn from a seeded stream, never derived from the name, bounds or
    /// list position. Phase 3 worlds have one region covering the whole map; the model
    /// already allows several.
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

        public bool Contains(MapCoord cell) =>
            cell.X >= Origin.X && cell.Z >= Origin.Z && cell.X < Origin.X + Width && cell.Z < Origin.Z + Depth;

        public RegionData Clone() => new RegionData { RegionId = RegionId, Name = Name, Origin = Origin, Width = Width, Depth = Depth };
    }
}
