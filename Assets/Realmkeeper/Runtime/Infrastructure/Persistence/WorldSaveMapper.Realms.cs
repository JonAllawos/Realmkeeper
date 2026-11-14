using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v25 (Phase 23, ADR-0111): realms, their own regions and the gateways between them.</summary>
    public static partial class WorldSaveMapper
    {
        private static void WriteRealms(WorldData w, WorldDto dto)
        {
            dto.Realms = new List<RealmDto>();
            foreach (var r in w.Realms)
            {
                var d = new RealmDto { RealmId = r.RealmId, Name = r.Name, Kind = r.Kind.ToString(), Profile = r.Profile, Seed = r.Seed, Visited = r.Visited, Regions = new List<RealmRegionDto>() };
                foreach (var g in r.Regions)
                    d.Regions.Add(new RealmRegionDto { RegionId = g.RegionId.ToString(), Name = g.Name, Seed = g.Seed, Kind = g.Kind.ToString(), MountainDirection = g.MountainDirection, Visited = g.Visited });
                dto.Realms.Add(d);
            }

            dto.Gateways = new List<GatewayDto>();
            foreach (var g in w.Gateways)
                dto.Gateways.Add(new GatewayDto { GatewayId = g.GatewayId, Name = g.Name, State = g.State.ToString(), Discovered = g.Discovered, A = End(g.A), B = End(g.B) });
        }

        private static GatewayEndDto End(GatewayEnd e) => new GatewayEndDto { RealmId = e.RealmId, RegionId = e.RegionId.ToString(), X = e.Cell.X, Z = e.Cell.Z };

        private static void ReadRealms(WorldDto dto, WorldData world)
        {
            world.Realms = new List<RealmData>();
            foreach (var d in dto.Realms ?? new List<RealmDto>())
            {
                if (d == null) throw new SaveDataException("A realm is empty.");
                var r = new RealmData
                {
                    RealmId = d.RealmId, Name = d.Name, Kind = ParseEnum<RealmKind>(d.Kind, "realm kind"), Profile = d.Profile ?? RealmProfiles.Surface, Seed = d.Seed, Visited = d.Visited,
                };
                foreach (var g in d.Regions ?? new List<RealmRegionDto>())
                {
                    if (g == null) throw new SaveDataException("A realm region is empty.");
                    r.Regions.Add(new RealmRegionData
                    {
                        RegionId = new RegionId(RequiredGuid(g.RegionId, "realm regionId")), Name = g.Name, Seed = g.Seed, Kind = ParseEnum<RegionKind>(g.Kind, "realm region kind"),
                        MountainDirection = g.MountainDirection, Visited = g.Visited,
                    });
                }

                world.Realms.Add(r);
            }

            world.Gateways = new List<GatewayData>();
            foreach (var g in dto.Gateways ?? new List<GatewayDto>())
            {
                if (g == null || g.A == null || g.B == null) throw new SaveDataException("A gateway is empty.");
                world.Gateways.Add(new GatewayData
                {
                    GatewayId = g.GatewayId, Name = g.Name, State = ParseEnum<GatewayState>(g.State, "gateway state"), Discovered = g.Discovered,
                    A = new GatewayEnd { RealmId = g.A.RealmId, RegionId = new RegionId(RequiredGuid(g.A.RegionId, "gateway regionId")), Cell = new MapCoord(g.A.X, g.A.Z) },
                    B = new GatewayEnd { RealmId = g.B.RealmId, RegionId = new RegionId(RequiredGuid(g.B.RegionId, "gateway regionId")), Cell = new MapCoord(g.B.X, g.B.Z) },
                });
            }
        }
    }

    public sealed class RealmDto
    {
        public string RealmId { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Profile { get; set; }
        public long Seed { get; set; }
        public bool Visited { get; set; }
        public List<RealmRegionDto> Regions { get; set; }
    }

    public sealed class RealmRegionDto
    {
        public string RegionId { get; set; }
        public string Name { get; set; }
        public long Seed { get; set; }
        public string Kind { get; set; }
        public int MountainDirection { get; set; }
        public bool Visited { get; set; }
    }

    public sealed class GatewayDto
    {
        public string GatewayId { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public bool Discovered { get; set; }
        public GatewayEndDto A { get; set; }
        public GatewayEndDto B { get; set; }
    }

    public sealed class GatewayEndDto
    {
        public string RealmId { get; set; }
        public string RegionId { get; set; }
        public int X { get; set; }
        public int Z { get; set; }
    }
}
