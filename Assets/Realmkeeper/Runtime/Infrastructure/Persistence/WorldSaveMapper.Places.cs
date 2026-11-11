using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v15 (Phase 13): an explorable place — identity, saved layout and every change made to it.</summary>
    public sealed class PlaceDto
    {
        public string PlaceId { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string ArchetypeId { get; set; }
        public string ParentKind { get; set; }
        public string ParentId { get; set; }
        public string RegionId { get; set; }
        public string SettlementId { get; set; }
        public string LocationId { get; set; }
        public string LayoutKey { get; set; }
        public float ExitX { get; set; }
        public float ExitY { get; set; }
        public float ExitZ { get; set; }
        public float ExitYaw { get; set; }
        public int CellX { get; set; }
        public int CellZ { get; set; }
        public string Seed { get; set; }
        public int GeneratorVersion { get; set; }
        public PlaceLayoutDto Layout { get; set; }
        public string Discovery { get; set; }
        public long DiscoveredAt { get; set; } = -1;
        public long LastVisitedAt { get; set; } = -1;
        public int Visits { get; set; }
        public string FactionId { get; set; }
        public int Threat { get; set; }
        public List<string> ExploredRooms { get; set; }
        public Dictionary<string, string> Doors { get; set; }
        public List<string> FoundConnections { get; set; }
        public Dictionary<string, FeatureStateDto> Features { get; set; }
        public Dictionary<string, GroupStateDto> Groups { get; set; }
        public Dictionary<string, string> Variables { get; set; }
        public List<string> Notes { get; set; }
    }

    public sealed class PlaceLayoutDto
    {
        public List<PlaceFloorDto> Floors { get; set; }
        public List<PlaceRoomDto> Rooms { get; set; }
        public List<PlaceConnectionDto> Connections { get; set; }
        public List<PlaceFeatureDto> Features { get; set; }
        public List<PlaceGroupDto> Groups { get; set; }
    }

    public sealed class PlaceFloorDto
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public float Elevation { get; set; }
    }

    public sealed class PlaceRoomDto
    {
        public string Id { get; set; }
        public int Floor { get; set; }
        public int X { get; set; }
        public int Z { get; set; }
        public int W { get; set; }
        public int D { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public float Ceiling { get; set; }
        public bool Secret { get; set; }
        public bool Restricted { get; set; }
        public bool Lit { get; set; }
        public string Hazard { get; set; }
        public List<string> Tags { get; set; }
    }

    public sealed class PlaceConnectionDto
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string A { get; set; }
        public string B { get; set; }
        public int X { get; set; }
        public int Z { get; set; }
        public int W { get; set; }
        public int D { get; set; }
        public int ToX { get; set; }
        public int ToZ { get; set; }
        public string Door { get; set; }
        public string KeyItem { get; set; }
        public int LockDc { get; set; }
        public int FindDc { get; set; }
        public string GuardedBy { get; set; }
    }

    public sealed class PlaceFeatureDto
    {
        public string Id { get; set; }
        public string Room { get; set; }
        public string Kind { get; set; }
        public string DefinitionId { get; set; }
        public int X { get; set; }
        public int Z { get; set; }
        public float Yaw { get; set; }
        public bool Hidden { get; set; }
        public int FindDc { get; set; }
        public bool Required { get; set; }
        public Dictionary<string, string> Data { get; set; }
    }

    public sealed class PlaceGroupDto
    {
        public string Id { get; set; }
        public string Room { get; set; }
        public string Name { get; set; }
        public string Behaviour { get; set; }
        public string FactionId { get; set; }
        public string WakesOn { get; set; }
        public List<GroupMemberDto> Members { get; set; }
    }

    public sealed class GroupMemberDto
    {
        public string CreatureId { get; set; }
        public string NpcId { get; set; }
        public int X { get; set; }
        public int Z { get; set; }
    }

    public sealed class FeatureStateDto
    {
        public string Flags { get; set; }
        public int Attempts { get; set; }
        public long ChangedAt { get; set; } = -1;
    }

    public sealed class GroupStateDto
    {
        public string State { get; set; }
        public string EncounterId { get; set; }
        public bool Alerted { get; set; }
    }

    public sealed class EscortDto
    {
        public string NpcId { get; set; }
        public string State { get; set; }
        public string PlaceId { get; set; }
        public string Room { get; set; }
        public string Destination { get; set; }
        public string QuestInstanceId { get; set; }
        public long ChangedAt { get; set; } = -1;
    }

    public static partial class WorldSaveMapper
    {
        private static void WritePlaces(WorldData w, WorldDto dto)
        {
            dto.Places = new List<PlaceDto>();
            foreach (var p in w.Places)
            {
                var d = new PlaceDto
                {
                    PlaceId = p.PlaceId, DefinitionId = p.DefinitionId, Name = p.Name, Description = p.Description, Category = p.Category.ToString(), ArchetypeId = p.ArchetypeId,
                    ParentKind = p.ParentKind.ToString(), ParentId = p.ParentId, RegionId = p.RegionId.IsEmpty ? null : p.RegionId.ToString(),
                    SettlementId = p.SettlementId.IsEmpty ? null : p.SettlementId.ToString(), LocationId = p.LocationId.IsEmpty ? null : p.LocationId.ToString(), LayoutKey = p.LayoutKey,
                    ExitX = p.ExitPose.X, ExitY = p.ExitPose.Y, ExitZ = p.ExitPose.Z, ExitYaw = p.ExitPose.Yaw, CellX = p.Cell.X, CellZ = p.Cell.Z,
                    Seed = p.Seed.ToString(CultureInfo.InvariantCulture), GeneratorVersion = p.GeneratorVersion, Layout = LayoutDto(p.Layout), Discovery = p.Discovery.ToString(),
                    DiscoveredAt = p.DiscoveredAt, LastVisitedAt = p.LastVisitedAt, Visits = p.Visits, FactionId = p.FactionId.IsEmpty ? null : p.FactionId.ToString(), Threat = p.Threat,
                    ExploredRooms = new List<string>(p.ExploredRooms), FoundConnections = new List<string>(p.FoundConnections), Variables = new Dictionary<string, string>(p.Variables),
                    Notes = new List<string>(p.Notes), Doors = new Dictionary<string, string>(), Features = new Dictionary<string, FeatureStateDto>(), Groups = new Dictionary<string, GroupStateDto>(),
                };
                foreach (var pair in p.Doors) d.Doors[pair.Key] = pair.Value.ToString();
                foreach (var pair in p.Features) d.Features[pair.Key] = new FeatureStateDto { Flags = pair.Value.Flags.ToString(), Attempts = pair.Value.Attempts, ChangedAt = pair.Value.ChangedAt };
                foreach (var pair in p.Groups)
                    d.Groups[pair.Key] = new GroupStateDto { State = pair.Value.State.ToString(), EncounterId = pair.Value.EncounterId.IsEmpty ? null : pair.Value.EncounterId.ToString(), Alerted = pair.Value.Alerted };
                dto.Places.Add(d);
            }

            dto.Escorts = new List<EscortDto>();
            foreach (var e in w.Escorts)
                dto.Escorts.Add(new EscortDto
                {
                    NpcId = e.NpcId.ToString(), State = e.State.ToString(), PlaceId = e.PlaceId, Room = e.Room, Destination = e.Destination.IsEmpty ? null : e.Destination.ToString(),
                    QuestInstanceId = e.QuestInstanceId, ChangedAt = e.ChangedAt,
                });
        }

        private static PlaceLayoutDto LayoutDto(PlaceLayout l)
        {
            var d = new PlaceLayoutDto
            {
                Floors = l.Floors.ConvertAll(f => new PlaceFloorDto { Index = f.Index, Name = f.Name, Elevation = f.Elevation }),
                Rooms = l.Rooms.ConvertAll(r => new PlaceRoomDto
                {
                    Id = r.Id, Floor = r.Floor, X = r.Rect.X, Z = r.Rect.Z, W = r.Rect.W, D = r.Rect.D, Kind = r.Kind.ToString(), Name = r.Name, Ceiling = r.Ceiling, Secret = r.Secret,
                    Restricted = r.Restricted, Lit = r.Lit, Hazard = r.Hazard, Tags = new List<string>(r.Tags),
                }),
                Connections = l.Connections.ConvertAll(c => new PlaceConnectionDto
                {
                    Id = c.Id, Kind = c.Kind.ToString(), A = c.A, B = c.B, X = c.Rect.X, Z = c.Rect.Z, W = c.Rect.W, D = c.Rect.D, ToX = c.ToX, ToZ = c.ToZ, Door = c.Door.ToString(),
                    KeyItem = c.KeyItem, LockDc = c.LockDc, FindDc = c.FindDc, GuardedBy = c.GuardedBy,
                }),
                Features = l.Features.ConvertAll(f => new PlaceFeatureDto
                {
                    Id = f.Id, Room = f.Room, Kind = f.Kind.ToString(), DefinitionId = f.DefinitionId, X = f.X, Z = f.Z, Yaw = f.Yaw, Hidden = f.Hidden, FindDc = f.FindDc, Required = f.Required,
                    Data = new Dictionary<string, string>(f.Data),
                }),
                Groups = l.Groups.ConvertAll(g => new PlaceGroupDto
                {
                    Id = g.Id, Room = g.Room, Name = g.Name, Behaviour = g.Behaviour.ToString(), FactionId = g.FactionId.IsEmpty ? null : g.FactionId.ToString(), WakesOn = g.WakesOn,
                    Members = g.Members.ConvertAll(m => new GroupMemberDto { CreatureId = m.CreatureId, NpcId = m.NpcId.IsEmpty ? null : m.NpcId.ToString(), X = m.X, Z = m.Z }),
                }),
            };
            return d;
        }

        private static void ReadPlaces(WorldDto dto, WorldData world)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in dto.Places ?? new List<PlaceDto>())
            {
                if (d == null) throw new SaveDataException("Null place entry.");
                if (string.IsNullOrEmpty(d.PlaceId) || !seen.Add(d.PlaceId)) throw new SaveDataException($"A place has no id, or a repeated one ('{d.PlaceId}').");
                if (d.Layout == null) throw new SaveDataException($"Place '{d.PlaceId}' has no layout.");
                if (!ulong.TryParse(d.Seed ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed)) throw new SaveDataException($"Place '{d.PlaceId}' has a bad seed.");
                var p = new PlaceData
                {
                    PlaceId = d.PlaceId, DefinitionId = d.DefinitionId, Name = d.Name, Description = d.Description, Category = ParseEnum<PlaceCategory>(d.Category, "place category"),
                    ArchetypeId = d.ArchetypeId, ParentKind = ParseEnum<PlaceParentKind>(d.ParentKind, "place parentKind"), ParentId = d.ParentId,
                    RegionId = new RegionId(OptionalGuid(d.RegionId, "place regionId")), SettlementId = new SettlementId(OptionalGuid(d.SettlementId, "place settlementId")),
                    LocationId = new LocationId(OptionalGuid(d.LocationId, "place locationId")), LayoutKey = d.LayoutKey, ExitPose = new LocalPose(d.ExitX, d.ExitY, d.ExitZ, d.ExitYaw),
                    Cell = new MapCoord(d.CellX, d.CellZ), Seed = seed, GeneratorVersion = d.GeneratorVersion, Layout = Layout(d.Layout, d.PlaceId),
                    Discovery = ParseEnum<PlaceDiscovery>(d.Discovery, "place discovery"), DiscoveredAt = d.DiscoveredAt, LastVisitedAt = d.LastVisitedAt, Visits = d.Visits,
                    FactionId = new FactionId(OptionalGuid(d.FactionId, "place factionId")), Threat = d.Threat,
                    ExploredRooms = new List<string>(d.ExploredRooms ?? new List<string>()), FoundConnections = new List<string>(d.FoundConnections ?? new List<string>()),
                    Variables = new Dictionary<string, string>(d.Variables ?? new Dictionary<string, string>(), StringComparer.Ordinal), Notes = new List<string>(d.Notes ?? new List<string>()),
                };
                foreach (var pair in d.Doors ?? new Dictionary<string, string>()) p.Doors[pair.Key] = ParseEnum<DoorState>(pair.Value, "place door");
                foreach (var pair in d.Features ?? new Dictionary<string, FeatureStateDto>())
                {
                    if (pair.Value == null) throw new SaveDataException($"Place '{d.PlaceId}' has a null feature state.");
                    p.Features[pair.Key] = new FeatureStateData { Flags = ParseFlags(pair.Value.Flags), Attempts = pair.Value.Attempts, ChangedAt = pair.Value.ChangedAt };
                }

                foreach (var pair in d.Groups ?? new Dictionary<string, GroupStateDto>())
                {
                    if (pair.Value == null) throw new SaveDataException($"Place '{d.PlaceId}' has a null group state.");
                    p.Groups[pair.Key] = new GroupStateData
                    {
                        State = ParseEnum<GroupState>(pair.Value.State, "group state"), EncounterId = new EncounterId(OptionalGuid(pair.Value.EncounterId, "group encounterId")), Alerted = pair.Value.Alerted,
                    };
                }

                world.Places.Add(p);
            }

            foreach (var e in dto.Escorts ?? new List<EscortDto>())
            {
                if (e == null) throw new SaveDataException("Null escort entry.");
                world.Escorts.Add(new EscortData
                {
                    NpcId = new NpcId(RequiredGuid(e.NpcId, "escort npcId")), State = ParseEnum<EscortState>(e.State, "escort state"), PlaceId = e.PlaceId, Room = e.Room,
                    Destination = new SettlementId(OptionalGuid(e.Destination, "escort destination")), QuestInstanceId = e.QuestInstanceId, ChangedAt = e.ChangedAt,
                });
            }
        }

        private static FeatureFlags ParseFlags(string text)
        {
            if (string.IsNullOrEmpty(text)) return FeatureFlags.None;
            var all = FeatureFlags.None;
            foreach (FeatureFlags f in Enum.GetValues(typeof(FeatureFlags))) all |= f;
            if (char.IsDigit(text[0]) || !Enum.TryParse<FeatureFlags>(text, false, out var value) || (value & ~all) != 0) throw new SaveDataException($"Invalid feature flags '{text}'.");
            return value;
        }

        private static PlaceLayout Layout(PlaceLayoutDto d, string placeId)
        {
            var l = new PlaceLayout();
            foreach (var f in d.Floors ?? new List<PlaceFloorDto>()) l.Floors.Add(new PlaceFloor { Index = f.Index, Name = f.Name, Elevation = f.Elevation });
            foreach (var r in d.Rooms ?? new List<PlaceRoomDto>())
            {
                if (r == null) throw new SaveDataException($"Place '{placeId}' has a null room.");
                l.Rooms.Add(new PlaceRoom
                {
                    Id = r.Id, Floor = r.Floor, Rect = new CellRect(r.X, r.Z, r.W, r.D), Kind = ParseEnum<RoomKind>(r.Kind, "room kind"), Name = r.Name, Ceiling = r.Ceiling,
                    Secret = r.Secret, Restricted = r.Restricted, Lit = r.Lit, Hazard = r.Hazard, Tags = new List<string>(r.Tags ?? new List<string>()),
                });
            }

            foreach (var c in d.Connections ?? new List<PlaceConnectionDto>())
            {
                if (c == null) throw new SaveDataException($"Place '{placeId}' has a null connection.");
                l.Connections.Add(new PlaceConnection
                {
                    Id = c.Id, Kind = ParseEnum<ConnectionKind>(c.Kind, "connection kind"), A = c.A, B = c.B, Rect = new CellRect(c.X, c.Z, c.W, c.D), ToX = c.ToX, ToZ = c.ToZ,
                    Door = ParseEnum<DoorState>(c.Door, "door state"), KeyItem = c.KeyItem, LockDc = c.LockDc, FindDc = c.FindDc, GuardedBy = c.GuardedBy,
                });
            }

            foreach (var f in d.Features ?? new List<PlaceFeatureDto>())
            {
                if (f == null) throw new SaveDataException($"Place '{placeId}' has a null feature.");
                l.Features.Add(new PlaceFeature
                {
                    Id = f.Id, Room = f.Room, Kind = ParseEnum<FeatureKind>(f.Kind, "feature kind"), DefinitionId = f.DefinitionId, X = f.X, Z = f.Z, Yaw = f.Yaw, Hidden = f.Hidden,
                    FindDc = f.FindDc, Required = f.Required, Data = new Dictionary<string, string>(f.Data ?? new Dictionary<string, string>(), StringComparer.Ordinal),
                });
            }

            foreach (var g in d.Groups ?? new List<PlaceGroupDto>())
            {
                if (g == null) throw new SaveDataException($"Place '{placeId}' has a null group.");
                var group = new PlaceGroup
                {
                    Id = g.Id, Room = g.Room, Name = g.Name, Behaviour = ParseEnum<GroupBehaviour>(g.Behaviour, "group behaviour"),
                    FactionId = new FactionId(OptionalGuid(g.FactionId, "group factionId")), WakesOn = g.WakesOn,
                };
                foreach (var m in g.Members ?? new List<GroupMemberDto>())
                    group.Members.Add(new GroupMember { CreatureId = m.CreatureId, NpcId = new NpcId(OptionalGuid(m.NpcId, "member npcId")), X = m.X, Z = m.Z });
                l.Groups.Add(group);
            }

            return l;
        }
    }
}
