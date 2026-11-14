using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>Phase 23 (ADR-0111): what kind of world a realm is.</summary>
    public enum RealmKind
    {
        /// <summary>The world of the settlements, factions and roads: the atlas's regions.</summary>
        Surface = 0,

        /// <summary>The ancient world of the campaign (Campaign Bible §5): reached only through gateways.</summary>
        Ancient = 1,
    }

    /// <summary>Phase 23: a realm's environment rules, by id (the presentation reads them; the simulation may later).</summary>
    public static class RealmProfiles
    {
        public const string Surface = "surface";

        /// <summary>
        /// A provisional profile for the ancient realm's prototype region: different light, fog and sky, no weather system
        /// yet, no inhabitants. Its real environments, peoples and names are undecided (Campaign Bible §5).
        /// </summary>
        public const string AncientPrototype = "ancient-prototype";
    }

    /// <summary>
    /// Phase 23 (ADR-0111): one realm of the campaign world. The surface realm's regions are the world's
    /// <see cref="WorldData.Regions"/> (the atlas, with settlements, people and simulation); another realm keeps its own
    /// regions here, generated from their seeds and loaded on their own. One campaign: the player, party, quests, clock and
    /// simulation are shared across realms.
    /// </summary>
    public sealed class RealmData
    {
        /// <summary>Stable id ("realm:surface", "realm:ancient").</summary>
        public string RealmId { get; set; }

        /// <summary>A working name until the campaign names it.</summary>
        public string Name { get; set; }

        public RealmKind Kind { get; set; }

        /// <summary>The realm's environment rules (<see cref="RealmProfiles"/>).</summary>
        public string Profile { get; set; } = RealmProfiles.Surface;

        public long Seed { get; set; }

        /// <summary>Whether the player has been there.</summary>
        public bool Visited { get; set; }

        /// <summary>The realm's own regions (empty for the surface: its regions are the atlas's).</summary>
        public List<RealmRegionData> Regions { get; set; } = new List<RealmRegionData>();

        public RealmRegionData FindRegion(RegionId id) => Regions.Find(r => r.RegionId == id);

        public RealmData Clone()
        {
            var c = (RealmData)MemberwiseClone();
            c.Regions = new List<RealmRegionData>();
            foreach (var r in Regions) c.Regions.Add(r.Clone());
            return c;
        }
    }

    /// <summary>
    /// Phase 23: a region of a realm other than the surface: geography only, made from its seed (nothing about it is
    /// stored but its identity, seed, kind and what the player changed or found). Its inhabitants are undecided.
    /// </summary>
    public sealed class RealmRegionData
    {
        public RegionId RegionId { get; set; }
        public string Name { get; set; }
        public long Seed { get; set; }
        public RegionKind Kind { get; set; }
        public int MountainDirection { get; set; } = -1;
        public bool Visited { get; set; }

        public RealmRegionData Clone() => (RealmRegionData)MemberwiseClone();
    }

    /// <summary>Phase 23: a gateway's state. Only an open gateway can be passed.</summary>
    public enum GatewayState
    {
        Dormant = 0,
        Open = 1,
        Sealed = 2,
    }

    /// <summary>One end of a gateway: a realm, a region in it, and a cell of that region.</summary>
    public struct GatewayEnd
    {
        public string RealmId { get; set; }
        public RegionId RegionId { get; set; }
        public MapCoord Cell { get; set; }
    }

    /// <summary>
    /// Phase 23 (ADR-0111): an ancient gateway between two realms (Campaign Bible §5, §6). Its state is campaign state:
    /// opening and sealing it are the campaign's to decide (the containment structures, restoration); Phase 23 provides the
    /// record, the passage, and a command for the campaign to call.
    /// </summary>
    public sealed class GatewayData
    {
        public string GatewayId { get; set; }

        /// <summary>A working description until the campaign names it.</summary>
        public string Name { get; set; }

        public GatewayEnd A { get; set; }
        public GatewayEnd B { get; set; }
        public GatewayState State { get; set; }

        /// <summary>Whether the player has found its surface end.</summary>
        public bool Discovered { get; set; }

        /// <summary>The end in <paramref name="realmId"/>'s region, if this gateway has one there.</summary>
        public bool TryEndIn(string realmId, RegionId region, out GatewayEnd end, out GatewayEnd other)
        {
            if (A.RealmId == realmId && A.RegionId == region) { end = A; other = B; return true; }
            if (B.RealmId == realmId && B.RegionId == region) { end = B; other = A; return true; }
            end = default;
            other = default;
            return false;
        }

        public GatewayData Clone() => (GatewayData)MemberwiseClone();
    }

    /// <summary>Phase 23: the realm ids the game makes.</summary>
    public static class RealmIds
    {
        public const string Surface = "realm:surface";
        public const string Ancient = "realm:ancient";

        public static bool IsSurface(string realmId) => string.IsNullOrEmpty(realmId) || realmId == Surface;
    }
}
