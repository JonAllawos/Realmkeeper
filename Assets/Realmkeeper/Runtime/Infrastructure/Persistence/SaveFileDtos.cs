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

        /// <summary>Schema v5.</summary>
        public List<FactionRelationDto> Relations { get; set; }

        /// <summary>Schema v5.</summary>
        public List<TerritorialClaimDto> Claims { get; set; }

        /// <summary>Schema v6. Persistent places in settlements.</summary>
        public List<LocationDto> Locations { get; set; }

        /// <summary>Schema v6. Relationships between named people.</summary>
        public List<NpcRelationDto> NpcRelations { get; set; }

        /// <summary>Schema v7. Minute of the day (absent: 08:00).</summary>
        public int? MinuteOfDay { get; set; }

        public List<ShopDto> Shops { get; set; }
        public List<TransactionDto> Transactions { get; set; }
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

        /// <summary>Schema v5. Noble house administering for the governor; absent when none.</summary>
        public string AdministratorFactionId { get; set; }

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

    /// <summary>Schema v5 (Phase 3). Fields beyond ID and name are absent in older saves; the political initializer fills them on load.</summary>
    public sealed class FactionDto
    {
        public string FactionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string FoundedOn { get; set; }
        public string Status { get; set; }
        public string DissolvedOn { get; set; }
        public string LeaderNpcId { get; set; }
        public int Influence { get; set; }
        public int InfluenceTarget { get; set; }
        public int Treasury { get; set; }
        public int DormantTicks { get; set; }
        public int LastReportedInfluence { get; set; }
        public List<FactionPresenceDto> Presence { get; set; }
        public List<FactionGoalDto> Goals { get; set; }
    }

    public sealed class FactionPresenceDto
    {
        public string SettlementId { get; set; }
        public int Strength { get; set; }
        public int Members { get; set; }
        public string Status { get; set; }
        public string EstablishedOn { get; set; }
        public int LastReportedSharePerMille { get; set; }
    }

    public sealed class FactionGoalDto
    {
        public string GoalId { get; set; }
        public string Type { get; set; }
        public string TargetSettlementId { get; set; }
        public string TargetFactionId { get; set; }
        public int TargetValue { get; set; }
        public int Progress { get; set; }
        public int Priority { get; set; }
        public string Status { get; set; }
        public string CreatedOn { get; set; }
        public string ClosedOn { get; set; }
        public string CloseReason { get; set; }
        public bool IsManual { get; set; }
    }

    public sealed class FactionRelationDto
    {
        public string FactionA { get; set; }
        public string FactionB { get; set; }
        public int Opinion { get; set; }
        public int Trust { get; set; }
        public int Rivalry { get; set; }
        public string Agreement { get; set; }
        public string AgreementSince { get; set; }
        public string LastCause { get; set; }
        public string LastReportedState { get; set; }
    }

    public sealed class TerritorialClaimDto
    {
        public string ClaimId { get; set; }
        public string ClaimantId { get; set; }
        public string SettlementId { get; set; }
        public string Status { get; set; }
        public string FiledOn { get; set; }
        public string UpdatedOn { get; set; }
        public bool IsManual { get; set; }
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

        // Schema v6 (Phase 4). Absent in older saves; the migration and initializer fill them.
        public string Sex { get; set; }
        public string BirthDate { get; set; }
        public string Tier { get; set; }
        public Dictionary<string, int> Personality { get; set; }
        public Dictionary<string, int> Skills { get; set; }
        public NpcConditionDto Condition { get; set; }
        public string ResidenceId { get; set; }
        public string WorkplaceId { get; set; }
        public string CurrentLocationId { get; set; }

        /// <summary>Seven location IDs, one per weekday (day number mod 7); null entries are empty.</summary>
        public List<string> Schedule { get; set; }
        public List<NpcGoalDto> Goals { get; set; }
        public List<NpcMemoryDto> Memories { get; set; }
        public List<NpcKnowledgeDto> Knowledge { get; set; }
        public bool IsDeceased { get; set; }
        public string DiedOn { get; set; }
        public string LastUpdatedOn { get; set; }

        /// <summary>Schema v7. How they regard the player.</summary>
        public PlayerRegardDto PlayerRegard { get; set; }
    }

    public sealed class NpcConditionDto
    {
        public int Health { get; set; }
        public int Satiation { get; set; }
        public int Energy { get; set; }
        public int Morale { get; set; }
        public int Safety { get; set; }
        public int Wealth { get; set; }
    }

    public sealed class NpcGoalDto
    {
        public string GoalId { get; set; }
        public string Type { get; set; }
        public int Priority { get; set; }
        public int Progress { get; set; }
        public string Status { get; set; }
        public string CreatedOn { get; set; }
        public string ClosedOn { get; set; }
        public string Source { get; set; }
        public string TargetSkill { get; set; }
        public string TargetNpcId { get; set; }
    }

    public sealed class NpcMemoryDto
    {
        public string MemoryId { get; set; }
        public string EventId { get; set; }
        public string Date { get; set; }
        public string Category { get; set; }
        public string Subject { get; set; }
        public string SettlementId { get; set; }
        public string FactionId { get; set; }
        public List<string> NpcIds { get; set; }
        public int Salience { get; set; }
        public int Confidence { get; set; }
        public string Source { get; set; }
    }

    public sealed class NpcKnowledgeDto
    {
        public string KnowledgeId { get; set; }
        public string Topic { get; set; }
        public string SubjectId { get; set; }
        public string Value { get; set; }
        public string Status { get; set; }
        public int Confidence { get; set; }
        public string LearnedOn { get; set; }
        public string SourceNpcId { get; set; }
        public int Hops { get; set; }
        public string EventId { get; set; }
    }

    public sealed class LocationDto
    {
        public string LocationId { get; set; }
        public string SettlementId { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string LayoutKey { get; set; }
    }

    public sealed class NpcRelationDto
    {
        public string NpcA { get; set; }
        public string NpcB { get; set; }
        public string Kind { get; set; }
        public int Affinity { get; set; }
        public int Trust { get; set; }
        public int Respect { get; set; }
        public int Fear { get; set; }
        public int Rivalry { get; set; }
        public string Origin { get; set; }
        public string LastCause { get; set; }
        public string Since { get; set; }
        public string LastChanged { get; set; }
        public int Interactions { get; set; }
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

        // Schema v7 (Phase 5). Absent in older saves.
        public string Species { get; set; }
        public int Age { get; set; }
        public string Background { get; set; }
        public string Description { get; set; }
        public PlayerAppearanceDto Appearance { get; set; }
        public int Purse { get; set; }
        public int Fed { get; set; }
        public bool IsOutfitted { get; set; }

        /// <summary>Schema v8 (Phase 6): the RPG character.</summary>
        public CharacterSheetDto Rpg { get; set; }
        public List<ItemStackDto> Inventory { get; set; }

        /// <summary>Slot name → stack.</summary>
        public Dictionary<string, ItemStackDto> Equipment { get; set; }
    }

    public sealed class PlayerAppearanceDto
    {
        public string Frame { get; set; }
        public string HairId { get; set; }
        public string BeardId { get; set; }
        public int SkinTone { get; set; }
        public int HairColour { get; set; }
        public int ClothTint { get; set; }
    }

    public sealed class ItemStackDto
    {
        public string StackId { get; set; }
        public string ItemId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class ShopDto
    {
        public string ShopId { get; set; }
        public string SettlementId { get; set; }
        public string LocationId { get; set; }
        public string Name { get; set; }
        public List<string> Categories { get; set; }
        public string ShopkeeperNpcId { get; set; }
        public Dictionary<string, int> Stock { get; set; }
        public int OpensAt { get; set; }
        public int ClosesAt { get; set; }
        public string LastRestockedOn { get; set; }
        public int TotalSales { get; set; }
        public int TotalPurchases { get; set; }
    }

    public sealed class TransactionDto
    {
        public string TransactionId { get; set; }
        public string Date { get; set; }
        public int MinuteOfDay { get; set; }
        public string Kind { get; set; }
        public string ShopId { get; set; }
        public string ShopkeeperNpcId { get; set; }
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public int Total { get; set; }
        public string Source { get; set; }
    }

    public sealed class PlayerRegardDto
    {
        public int Familiarity { get; set; }
        public int Affinity { get; set; }
        public int Trades { get; set; }
        public int Conversations { get; set; }
        public long LastTradeDay { get; set; }
        public long LastTalkDay { get; set; }
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

        /// <summary>Schema v5. Factions involved; absent in older events.</summary>
        public List<string> FactionIds { get; set; }

        /// <summary>Schema v6. Named people involved; absent in older events.</summary>
        public List<string> NpcIds { get; set; }
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

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v8 (Phase 6): the player's RPG character. Derived values are not saved.</summary>
    public sealed class CharacterSheetDto
    {
        public string State { get; set; }
        public string Title { get; set; }
        public string History { get; set; }
        public string SpeciesId { get; set; }
        public string BackgroundId { get; set; }
        public string ClassId { get; set; }
        public string SubclassId { get; set; }
        public int Level { get; set; } = 1;
        public int Experience { get; set; }
        public string Method { get; set; }
        public System.Collections.Generic.List<int> BaseScores { get; set; }
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> Choices { get; set; }
        public int CurrentHp { get; set; }
        public int TemporaryHp { get; set; }
        public System.Collections.Generic.Dictionary<string, int> Resources { get; set; }
        public bool StartingEquipmentGranted { get; set; }
        public string CreatedOn { get; set; }
    }
}
