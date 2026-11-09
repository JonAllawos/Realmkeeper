using System.Collections.Generic;

namespace Realmkeeper.Infrastructure.Persistence
{
    // Save-file data transfer objects (schema version 3).
    // v3 (ADR-0009 prerequisites): regions, settlement.regionId, the player character, named
    // residents (role, acquaintance) and economy.labourFocus.
    // Deliberately separate from domain types: the on-disk layout can evolve through
    // migrations without changing domain classes, and domain classes carry no
    // serialization attributes. Enums, IDs and dates are stored as strings so files stay
    // readable and robust to enum reordering.
    //
    // v2 (Phase 2): settlement economy, trade routes, shipments, structured events; the
    // "ore" resource is "iron"; settlement.foodProductionPerDay is gone (derived from labour).

    public sealed class WorldSaveFileDto
    {
        public string Format { get; set; }
        public int SchemaVersion { get; set; }
        public string SavedAtUtc { get; set; }
        public WorldDto World { get; set; }
    }

    public sealed class WorldDto
    {
        public string WorldId { get; set; }
        public string WorldName { get; set; }
        public long Seed { get; set; }
        public int GeneratorVersion { get; set; }
        public string CurrentSimulationDate { get; set; }
        public WorldSettingsDto Settings { get; set; }
        public List<RegionDto> Regions { get; set; }
        public List<SettlementDto> Settlements { get; set; }
        public PlayerDto Player { get; set; }
        public List<FactionDto> Factions { get; set; }
        public List<NpcDto> Npcs { get; set; }
        public List<HistoricalEventDto> HistoricalEvents { get; set; }
        public List<TradeRouteDto> TradeRoutes { get; set; }
        public List<ShipmentDto> Shipments { get; set; }
    }

    public sealed class WorldSettingsDto
    {
        public string SimulationSpeed { get; set; }
        public string StartingDate { get; set; }
        public List<string> EnabledSimulationSystems { get; set; }
    }

    public sealed class SettlementDto
    {
        public string SettlementId { get; set; }
        public string Name { get; set; }
        public string SettlementType { get; set; }

        /// <summary>v3+. Absent in older saves; assigned on load from the region containing the settlement.</summary>
        public string RegionId { get; set; }

        public int PositionX { get; set; }
        public int PositionZ { get; set; }
        public int Population { get; set; }
        public int Prosperity { get; set; }
        public int Stability { get; set; }
        public string FactionId { get; set; }
        public bool IsInFoodShortage { get; set; }
        public List<ResourceStackDto> Resources { get; set; }

        /// <summary>Null in saves that predate the economy; the session initializes it on load.</summary>
        public EconomyDto Economy { get; set; }
    }

    public sealed class ResourceStackDto
    {
        public string ResourceTypeId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class EconomyDto
    {
        public string Specialization { get; set; }
        public string SecondarySpecialization { get; set; }

        /// <summary>v3+. Null when no labour notice is in force.</summary>
        public string LabourFocus { get; set; }
        public List<ProductionDto> Production { get; set; }
        public List<ResourceStackDto> StorageCapacity { get; set; }
        public long LedgerDay { get; set; }
        public List<ResourceFlowDto> LastDay { get; set; }
        public List<SourceOutputDto> LastDaySources { get; set; }
        public List<ResourceStatusDto> ResourceStatuses { get; set; }
        public List<ResourceStackDto> LifetimeProduced { get; set; }
        public int DaysWithoutFood { get; set; }
        public int PopulationProgressMilli { get; set; }
        public int ProsperityTarget { get; set; }
        public int StabilityTarget { get; set; }
        public int TradeActivity { get; set; }
        public MarkersDto Markers { get; set; }
    }

    public sealed class ProductionDto
    {
        public string SourceType { get; set; }
        public int AllocationWeight { get; set; }
        public int WorkerCapacity { get; set; }
        public int EfficiencyPercent { get; set; }
        public int Workers { get; set; }
    }

    public sealed class ResourceFlowDto
    {
        public string ResourceTypeId { get; set; }
        public int Produced { get; set; }
        public int Wasted { get; set; }
        public int Demand { get; set; }
        public int Consumed { get; set; }
        public int Shortfall { get; set; }
        public int Imported { get; set; }
        public int Exported { get; set; }
    }

    public sealed class SourceOutputDto
    {
        public string SourceType { get; set; }
        public int Workers { get; set; }
        public int Amount { get; set; }
    }

    public sealed class ResourceStatusDto
    {
        public string ResourceTypeId { get; set; }
        public bool InShortage { get; set; }
        public int DaysBelowThreshold { get; set; }
    }

    public sealed class MarkersDto
    {
        public int ReportedProsperity { get; set; }
        public int ReportedStability { get; set; }
        public int ReportedPopulation { get; set; }
        public int CoinMilestone { get; set; }
        public long LastFoodExhaustedReportDay { get; set; } = -1;
        public long LastStarvationReportDay { get; set; } = -1;
        public List<ResourceStackDto> ProductionMilestoneIndex { get; set; }
    }

    public sealed class FactionDto
    {
        public string FactionId { get; set; }
        public string Name { get; set; }
    }

    public sealed class NpcDto
    {
        public string NpcId { get; set; }
        public string Name { get; set; }
        public string HomeSettlementId { get; set; }
        public string FactionId { get; set; }
        public string Role { get; set; }

        /// <summary>Date the player first met this person (YYYY-MM-DD), or null.</summary>
        public string MetPlayerOn { get; set; }

        public int ConversationCount { get; set; }
    }

    public sealed class RegionDto
    {
        public string RegionId { get; set; }
        public string Name { get; set; }
        public int OriginX { get; set; }
        public int OriginZ { get; set; }
        public int Width { get; set; }
        public int Depth { get; set; }
    }

    public sealed class PlayerDto
    {
        public string CharacterId { get; set; }
        public string Name { get; set; }
        public string LocationKind { get; set; }
        public string SettlementId { get; set; }
        public string RegionId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Yaw { get; set; }
        public string Perspective { get; set; }
        public string LastSettlementId { get; set; }
    }

    public sealed class HistoricalEventDto
    {
        public string EventId { get; set; }
        public string Date { get; set; }
        public string EventType { get; set; }
        public string Severity { get; set; }
        public string Importance { get; set; }

        /// <summary>Primary settlement (kept for readability and v1 compatibility).</summary>
        public string SettlementId { get; set; }

        public List<string> SettlementIds { get; set; }
        public Dictionary<string, string> Data { get; set; }
        public string Description { get; set; }
    }

    public sealed class TradeRouteDto
    {
        public string TradeRouteId { get; set; }
        public string OriginId { get; set; }
        public string DestinationId { get; set; }
        public int CapacityPerShipment { get; set; }
        public int TravelDays { get; set; }
        public int FrequencyDays { get; set; }
        public int EfficiencyPercent { get; set; }
        public string Status { get; set; }

        /// <summary>Schema v4. Absent in older saves, which means "not suspended".</summary>
        public bool IsSuspended { get; set; }

        public long NextDepartureDay { get; set; }
        public int ShipmentsDelivered { get; set; }
        public List<ResourceStackDto> Delivered { get; set; }
        public long LastReportedDay { get; set; }
        public long LastDeliveryDay { get; set; }
        public string LastDeliveryResource { get; set; }
        public int LastDeliveryQuantity { get; set; }
        public int LastDeliveryPayment { get; set; }
    }

    public sealed class ShipmentDto
    {
        public string ShipmentId { get; set; }
        public string TradeRouteId { get; set; }
        public string OriginId { get; set; }
        public string DestinationId { get; set; }
        public string ResourceTypeId { get; set; }
        public int Quantity { get; set; }
        public int DeliveredQuantity { get; set; }
        public int LostQuantity { get; set; }
        public int Payment { get; set; }
        public long DepartureDay { get; set; }
        public long ArrivalDay { get; set; }
        public int DaysWaiting { get; set; }
        public bool LossApplied { get; set; }
        public bool IsReturning { get; set; }
    }
}
