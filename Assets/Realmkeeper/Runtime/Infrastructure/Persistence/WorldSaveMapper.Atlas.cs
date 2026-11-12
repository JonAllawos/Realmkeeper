using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v18 (Phase 16, ADR-0077): one segment of the route network.</summary>
    public sealed class RouteSegmentDto
    {
        public string RouteId { get; set; }
        public string A { get; set; }
        public string B { get; set; }
        public string RegionA { get; set; }
        public string RegionB { get; set; }
        public string Kind { get; set; }
        public int Cells { get; set; }
        public int TerrainPercent { get; set; } = 100;
        public string Condition { get; set; }
        public bool HasBridge { get; set; }
        public string Bridge { get; set; }
        public string BlockedBy { get; set; }
        public int RepairDays { get; set; }
    }

    /// <summary>Schema v18 (ADR-0078): the player's journey.</summary>
    public sealed class JourneyDto
    {
        public string JourneyId { get; set; }
        public string OriginId { get; set; }
        public string DestinationId { get; set; }
        public List<string> Nodes { get; set; }
        public List<string> Segments { get; set; }
        public int SegmentIndex { get; set; }
        public int MetresIntoSegment { get; set; }
        public string Mode { get; set; }
        public string Status { get; set; }
        public long StartedAt { get; set; }
        public int HoursTravelled { get; set; }
        public string Reason { get; set; }
        public string EncounterId { get; set; }
        public List<string> Cleared { get; set; }
        public bool Back { get; set; }
    }

    /// <summary>Schema v18 (ADR-0079): a group on the roads.</summary>
    public sealed class TravelingGroupDto
    {
        public string GroupId { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string FactionId { get; set; }
        public string LeaderNpcId { get; set; }
        public int Members { get; set; }
        public int Strength { get; set; }
        public string OriginId { get; set; }
        public string DestinationId { get; set; }
        public List<string> Nodes { get; set; }
        public List<string> Segments { get; set; }
        public long DepartedAt { get; set; }
        public long DueAt { get; set; }
        public string State { get; set; }
        public string ShipmentId { get; set; }
        public string PopulationId { get; set; }
        public string StationRouteId { get; set; }
        public string EventId { get; set; }
        public List<string> History { get; set; }
    }

    /// <summary>Schema v18 (ADR-0080): a world event and the player's knowledge of it.</summary>
    public sealed class WorldEventDto
    {
        public string EventId { get; set; }
        public string Type { get; set; }
        public string State { get; set; }
        public int Severity { get; set; }
        public string Cause { get; set; }
        public string SettlementId { get; set; }
        public string RouteId { get; set; }
        public string RegionId { get; set; }
        public string FactionA { get; set; }
        public string FactionB { get; set; }
        public string GroupId { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
        public long ExpiresAt { get; set; } = -1;
        public long ClosedAt { get; set; } = -1;
        public string Key { get; set; }
        public List<string> Applied { get; set; }
        public List<string> History { get; set; }
        public string Known { get; set; }
        public long KnownAt { get; set; } = -1;
        public string KnownState { get; set; }
        public int KnownSeverity { get; set; }
        public string KnownFrom { get; set; }
    }

    public static partial class WorldSaveMapper
    {
        private static string Id<T>(T id) where T : struct => id.Equals(default(T)) ? null : id.ToString();

        private static void WriteAtlas(WorldData w, WorldDto dto)
        {
            dto.Routes = new List<RouteSegmentDto>();
            foreach (var r in w.Routes)
                dto.Routes.Add(new RouteSegmentDto
                {
                    RouteId = r.RouteId, A = r.A.ToString(), B = r.B.ToString(), RegionA = r.RegionA.ToString(), RegionB = r.RegionB.ToString(), Kind = r.Kind.ToString(),
                    Cells = r.Cells, TerrainPercent = r.TerrainPercent, Condition = r.Condition.ToString(), HasBridge = r.HasBridge, Bridge = r.Bridge.ToString(),
                    BlockedBy = r.BlockedBy, RepairDays = r.RepairDays,
                });

            var j = w.Journey;
            dto.Journey = j == null ? null : new JourneyDto
            {
                JourneyId = j.JourneyId, OriginId = j.OriginId.ToString(), DestinationId = j.DestinationId.ToString(), Nodes = j.Nodes.ConvertAll(n => n.ToString()),
                Segments = new List<string>(j.Segments), SegmentIndex = j.SegmentIndex, MetresIntoSegment = j.MetresIntoSegment, Mode = j.Mode.ToString(), Status = j.Status.ToString(),
                StartedAt = j.StartedAt, HoursTravelled = j.HoursTravelled, Reason = j.Reason, EncounterId = j.EncounterId, Cleared = new List<string>(j.Cleared), Back = j.Back,
            };

            dto.Groups = new List<TravelingGroupDto>();
            foreach (var g in w.Groups)
                dto.Groups.Add(new TravelingGroupDto
                {
                    GroupId = g.GroupId, Kind = g.Kind.ToString(), Name = g.Name, FactionId = Id(g.FactionId), LeaderNpcId = Id(g.LeaderNpcId), Members = g.Members, Strength = g.Strength,
                    OriginId = Id(g.OriginId), DestinationId = Id(g.DestinationId), Nodes = g.Nodes.ConvertAll(n => n.ToString()), Segments = new List<string>(g.Segments),
                    DepartedAt = g.DepartedAt, DueAt = g.DueAt, State = g.State.ToString(), ShipmentId = Id(g.ShipmentId), PopulationId = g.PopulationId,
                    StationRouteId = g.StationRouteId, EventId = g.EventId, History = new List<string>(g.History),
                });

            dto.WorldEvents = new List<WorldEventDto>();
            dto.NextWorldEventNumber = w.NextWorldEventNumber;
            foreach (var e in w.WorldEvents)
                dto.WorldEvents.Add(new WorldEventDto
                {
                    EventId = e.EventId, Type = e.Type.ToString(), State = e.State.ToString(), Severity = e.Severity, Cause = e.Cause, SettlementId = Id(e.SettlementId), RouteId = e.RouteId,
                    RegionId = Id(e.RegionId), FactionA = Id(e.FactionA), FactionB = Id(e.FactionB), GroupId = e.GroupId, CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt,
                    ExpiresAt = e.ExpiresAt, ClosedAt = e.ClosedAt, Key = e.Key, Applied = new List<string>(e.Applied), History = new List<string>(e.History),
                    Known = e.Known.ToString(), KnownAt = e.KnownAt, KnownState = e.KnownState.ToString(), KnownSeverity = e.KnownSeverity, KnownFrom = e.KnownFrom,
                });
        }

        private static void ReadAtlas(WorldDto dto, WorldData world)
        {
            foreach (var d in dto.Routes ?? new List<RouteSegmentDto>())
            {
                if (d == null || string.IsNullOrEmpty(d.RouteId)) throw new SaveDataException("A route segment is empty or has no ID.");
                world.Routes.Add(new RouteSegmentData
                {
                    RouteId = d.RouteId, A = new SettlementId(RequiredGuid(d.A, "route a")), B = new SettlementId(RequiredGuid(d.B, "route b")),
                    RegionA = new RegionId(RequiredGuid(d.RegionA, "route regionA")), RegionB = new RegionId(RequiredGuid(d.RegionB, "route regionB")),
                    Kind = ParseEnum<RouteKind>(d.Kind, "route kind"), Cells = d.Cells, TerrainPercent = d.TerrainPercent, Condition = ParseEnum<RouteCondition>(d.Condition, "route condition"),
                    HasBridge = d.HasBridge, Bridge = d.Bridge == null ? BridgeState.Intact : ParseEnum<BridgeState>(d.Bridge, "route bridge"), BlockedBy = d.BlockedBy, RepairDays = d.RepairDays,
                });
            }

            var j = dto.Journey;
            if (j != null)
                world.Journey = new JourneyData
                {
                    JourneyId = j.JourneyId, OriginId = new SettlementId(RequiredGuid(j.OriginId, "journey origin")), DestinationId = new SettlementId(RequiredGuid(j.DestinationId, "journey destination")),
                    Nodes = (j.Nodes ?? new List<string>()).ConvertAll(n => new SettlementId(RequiredGuid(n, "journey node"))), Segments = new List<string>(j.Segments ?? new List<string>()),
                    SegmentIndex = j.SegmentIndex, MetresIntoSegment = j.MetresIntoSegment, Mode = ParseEnum<TravelMode>(j.Mode, "travel mode"), Status = ParseEnum<JourneyStatus>(j.Status, "journey status"),
                    StartedAt = j.StartedAt, HoursTravelled = j.HoursTravelled, Reason = j.Reason, EncounterId = j.EncounterId, Cleared = new List<string>(j.Cleared ?? new List<string>()), Back = j.Back,
                };

            foreach (var g in dto.Groups ?? new List<TravelingGroupDto>())
            {
                if (g == null || string.IsNullOrEmpty(g.GroupId)) throw new SaveDataException("A group on the roads is empty or has no ID.");
                world.Groups.Add(new TravelingGroupData
                {
                    GroupId = g.GroupId, Kind = ParseEnum<TravelGroupKind>(g.Kind, "group kind"), Name = g.Name, FactionId = new FactionId(OptionalGuid(g.FactionId, "group faction")),
                    LeaderNpcId = new NpcId(OptionalGuid(g.LeaderNpcId, "group leader")), Members = g.Members, Strength = g.Strength,
                    OriginId = new SettlementId(OptionalGuid(g.OriginId, "group origin")), DestinationId = new SettlementId(OptionalGuid(g.DestinationId, "group destination")),
                    Nodes = (g.Nodes ?? new List<string>()).ConvertAll(n => new SettlementId(RequiredGuid(n, "group node"))), Segments = new List<string>(g.Segments ?? new List<string>()),
                    DepartedAt = g.DepartedAt, DueAt = g.DueAt, State = ParseEnum<TravelGroupState>(g.State, "group state"), ShipmentId = new ShipmentId(OptionalGuid(g.ShipmentId, "group shipment")),
                    PopulationId = g.PopulationId, StationRouteId = g.StationRouteId, EventId = g.EventId, History = new List<string>(g.History ?? new List<string>()),
                });
            }

            world.NextWorldEventNumber = Math.Max(1, dto.NextWorldEventNumber);
            foreach (var e in dto.WorldEvents ?? new List<WorldEventDto>())
            {
                if (e == null || string.IsNullOrEmpty(e.EventId)) throw new SaveDataException("A world event is empty or has no ID.");
                world.WorldEvents.Add(new WorldEventData
                {
                    EventId = e.EventId, Type = ParseEnum<WorldEventType>(e.Type, "world event type"), State = ParseEnum<WorldEventState>(e.State, "world event state"), Severity = e.Severity,
                    Cause = e.Cause, SettlementId = new SettlementId(OptionalGuid(e.SettlementId, "event settlement")), RouteId = e.RouteId, RegionId = new RegionId(OptionalGuid(e.RegionId, "event region")),
                    FactionA = new FactionId(OptionalGuid(e.FactionA, "event factionA")), FactionB = new FactionId(OptionalGuid(e.FactionB, "event factionB")), GroupId = e.GroupId,
                    CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt, ExpiresAt = e.ExpiresAt, ClosedAt = e.ClosedAt, Key = e.Key, Applied = new List<string>(e.Applied ?? new List<string>()),
                    History = new List<string>(e.History ?? new List<string>()), Known = e.Known == null ? EventKnowledge.Unknown : ParseEnum<EventKnowledge>(e.Known, "event knowledge"),
                    KnownAt = e.KnownAt, KnownState = e.KnownState == null ? WorldEventState.Active : ParseEnum<WorldEventState>(e.KnownState, "event known state"), KnownSeverity = e.KnownSeverity,
                    KnownFrom = e.KnownFrom,
                });
            }
        }
    }
}
