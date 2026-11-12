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

        /// <summary>Schema v10 (Phase 8).</summary>
        public List<EncounterDto> Encounters { get; set; }

        /// <summary>Schema v10 (Phase 8).</summary>
        public List<StructureDto> Structures { get; set; }

        // Schema v12 (Phase 10): the living world.
        public List<CreaturePopulationDto> CreaturePopulations { get; set; }
        public List<CreatureInstanceDto> Creatures { get; set; }
        public List<LivestockHerdDto> Livestock { get; set; }
        public List<SettlementLayoutDto> SettlementLayouts { get; set; }

        // Schema v13 (Phase 11): items in the world.
        public List<ContainerDto> Containers { get; set; }
        public List<WorkJobDto> WorkJobs { get; set; }
        public List<GatheringSiteDto> GatheringSites { get; set; }
        public int? ItemCatalogVersion { get; set; }

        // Schema v14 (Phase 12): quests and the conversation in progress.
        public List<QuestInstanceDto> Quests { get; set; }
        public ConversationDto Conversation { get; set; }

        // Schema v15 (Phase 13): explorable places and the people held in or brought out of them.
        public List<PlaceDto> Places { get; set; }
        public List<EscortDto> Escorts { get; set; }

        // Schema v16 (Phase 14).
        public List<CrimeDto> Crimes { get; set; }
        public int NextCrimeNumber { get; set; } = 1;

        // Schema v17 (Phase 15): the party and companions' histories.
        public PartyDto Party { get; set; }
        public List<CompanionRecordDto> Companions { get; set; }

        // Schema v18 (Phase 16): the route network, the journey, groups on the roads and world events.
        public List<RouteSegmentDto> Routes { get; set; }
        public JourneyDto Journey { get; set; }
        public List<TravelingGroupDto> Groups { get; set; }
        public List<WorldEventDto> WorldEvents { get; set; }
        public int NextWorldEventNumber { get; set; } = 1;

        // Schema v19 (Phase 17): campaigns begun in this world (companions' remarks are on their records).
        public List<CampaignInstanceDto> Campaigns { get; set; }
    }

    public sealed class WorldSettingsDto
    {
        public string SimulationSpeed { get; set; }
        public string StartingDate { get; set; }
        public List<string> EnabledSimulationSystems { get; set; }

        /// <summary>Schema v14 (Phase 12): content presentation, tag name → Show/Soften/Hide (absent: everything shown).</summary>
        public Dictionary<string, string> Content { get; set; }
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
        public int LostOnRoad { get; set; } // v16
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

        /// <summary>Phase 10 (v12): archetype, gear, purse and (for sheet-holders) the character sheet; null before v12.</summary>
        public NpcCharacterDto Character { get; set; }
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

        // Schema v18 (Phase 16): the atlas (absent: the home region at 0,0 with its geography from the world's seed).
        public int AtlasX { get; set; }
        public int AtlasZ { get; set; }
        public long Seed { get; set; }
        public int MountainDirection { get; set; } = -1;
        public int GeneratorVersion { get; set; }
        public string Kind { get; set; }
        public string Discovery { get; set; }
        public List<string> Neighbours { get; set; }
        public List<string> Landmarks { get; set; }
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

        // Schema v15 (Phase 13): the place the character is in (with locationKind Place) and its floor.
        public string PlaceId { get; set; }
        public int PlaceFloor { get; set; }
        public string PlaceRoom { get; set; }

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

        /// <summary>Schema v14 (Phase 12): standing, reputation, notoriety, the ledger, discoveries, rumours, decisions.</summary>
        public PlayerSocialDto Social { get; set; }
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

        // Schema v13 (Phase 11): the instance's own state (absent: good, common, no charges, not attuned).
        public string Condition { get; set; }
        public string Quality { get; set; }
        public int? Charges { get; set; }
        public bool Attuned { get; set; }
        public bool Bound { get; set; }
        public string Provenance { get; set; }
        public long? AcquiredAt { get; set; }
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

        /// <summary>Schema v13 (Phase 11): particular items the shop holds.</summary>
        public List<ItemStackDto> Goods { get; set; }
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

    /// <summary>Phase 10 (v12): a named person's mechanical record.</summary>
    public sealed class NpcCharacterDto
    {
        public string ArchetypeId { get; set; }
        public string Occupation { get; set; }
        public CharacterSheetDto Sheet { get; set; }
        public string Body { get; set; }
        public string Shield { get; set; }
        public string MainHand { get; set; }
        public string OffHand { get; set; }
        public List<string> Carried { get; set; }
        public int Purse { get; set; }
        public string SheetSince { get; set; }

        // Schema v17 (Phase 15): a companion's own items (absent: none; they still have their gear by name).
        public bool OwnsItems { get; set; }
        public List<ItemStackDto> Inventory { get; set; }
        public Dictionary<string, ItemStackDto> Equipment { get; set; }
    }

    public sealed class PlayerRegardDto
    {
        public int Familiarity { get; set; }
        public int Affinity { get; set; }
        public int Trades { get; set; }
        public int Conversations { get; set; }
        public long LastTradeDay { get; set; }
        public long LastTalkDay { get; set; }

        // Schema v14 (Phase 12): the further dimensions of regard (absent: 0).
        public int Trust { get; set; }
        public int Respect { get; set; }
        public int Fear { get; set; }
        public int Gratitude { get; set; }
        public int Hostility { get; set; }
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
        public string BlockedBy { get; set; } // v18

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

        // Schema v9 (Phase 7).
        public int RulesVersion { get; set; }
        public System.Collections.Generic.List<ExperienceAwardDto> ExperienceLog { get; set; }
        public System.Collections.Generic.List<AdvancementRecordDto> Advancements { get; set; }

        // Schema v16 (Phase 14): lasting conditions.
        public System.Collections.Generic.List<LastingConditionDto> Conditions { get; set; }

        // Schema v10 (Phase 8): the dying state.
        public int DeathSaveSuccesses { get; set; }
        public int DeathSaveFailures { get; set; }
        public bool IsStable { get; set; }
        public bool IsDead { get; set; }

        // Schema v11 (Phase 9): prepared spells, the preparation window and rests.
        public SpellcastingDto Spellcasting { get; set; }
    }

    public sealed class ExperienceAwardDto
    {
        public string Key { get; set; }
        public string Source { get; set; }
        public string SourceId { get; set; }
        public int Amount { get; set; }
        public string Date { get; set; }
        public string Reason { get; set; }
    }

    public sealed class AdvancementRecordDto
    {
        public int Level { get; set; }
        public string Date { get; set; }
        public int HitPointsGained { get; set; }
        public string Summary { get; set; }
    }
}

namespace Realmkeeper.Infrastructure.Persistence
{
    using System.Collections.Generic;

    /// <summary>Schema v10 (Phase 8): an encounter, active or recently finished. Derived values (AC, attacks, speed) are not saved.</summary>
    public sealed class EncounterDto
    {
        public string EncounterId { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Lethality { get; set; }
        public string SettlementId { get; set; }
        public string Status { get; set; }
        public string Outcome { get; set; }
        public string WinningSide { get; set; }
        public int Round { get; set; }
        public int TurnIndex { get; set; }
        public List<string> Order { get; set; }
        public List<CombatantDto> Combatants { get; set; }
        public List<GridPosDto> DifficultTerrain { get; set; }

        // Schema v15 (Phase 13): walls around a fight in a place.
        public List<GridPosDto> Blocked { get; set; }

        // Schema v16 (Phase 14): doors and low cover in the fight's area.
        public List<EncounterDoorDto> Doors { get; set; }
        public List<LowCoverDto> LowCover { get; set; }
        public string StartedOn { get; set; }
        public int StartedAtMinute { get; set; }
        public string EndedOn { get; set; }

        /// <summary>The dice seed, as a decimal string (it is a 64-bit unsigned value).</summary>
        public string Seed { get; set; }

        public int RollCount { get; set; }
        public int ActionSequence { get; set; }
        public List<CombatLogEntryDto> Log { get; set; }
        public string QuestRef { get; set; }
        public string WorldEventRef { get; set; }

        // Schema v11 (Phase 9).
        public List<ActiveEffectDto> Effects { get; set; }
        public int NextInstanceId { get; set; } = 1;

        // Schema v12 (Phase 10): the action waiting on the player's answer, and the next question's number.
        public PendingReactionDto PendingReaction { get; set; }
        public int NextReactionId { get; set; } = 1;
        public EncounterOriginDto Origin { get; set; }
    }

    public sealed class GridPosDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }

    public sealed class EncounterOriginDto
    {
        // Schema v15 (Phase 13): a fight in a place.
        public string PlaceId { get; set; }
        public string GroupId { get; set; }
        public string Room { get; set; }

        public string Type { get; set; }
        public string Objective { get; set; }
        public string ProtectedNpcId { get; set; }
        public string NearSettlementId { get; set; }
        public string Context { get; set; }
        public string Terrain { get; set; }
        public List<string> SourceIds { get; set; }
        public int XpBudget { get; set; }
        public int XpTotal { get; set; }
        public string Difficulty { get; set; }
        public string PlanSeed { get; set; }
        public bool Resolved { get; set; }
        public string Result { get; set; }
        public bool ObjectiveMet { get; set; }
    }

    public sealed class PendingReactionDto
    {
        public int Id { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string ReactorId { get; set; }
        public string TriggerId { get; set; }
        public string TargetId { get; set; }
        public string Trigger { get; set; }
        public string Cost { get; set; }
        public string Consequence { get; set; }
        public string ActorId { get; set; }
        public int Sequence { get; set; }
        public List<bool> Answers { get; set; }
        public ActionRecordDto Action { get; set; }
    }

    public sealed class ActionRecordDto
    {
        public string Kind { get; set; }
        public string Id { get; set; }
        public string Option { get; set; }
        public string Target { get; set; }
        public List<string> Targets { get; set; }
        public List<GridPosDto> Path { get; set; }
        public GridPosDto Point { get; set; }
        public int Level { get; set; }
        public int Amount { get; set; }
        public List<string> Metamagic { get; set; }
        public string FreeWith { get; set; }
    }

    public sealed class CreaturePopulationDto
    {
        public string PopulationId { get; set; }
        public string RegionId { get; set; }
        public string CreatureId { get; set; }
        public string Habitat { get; set; }
        public int CenterX { get; set; }
        public int CenterZ { get; set; }
        public int RangeCells { get; set; }
        public int Count { get; set; }
        public int Capacity { get; set; }
        public int Deaths { get; set; }
        public int Drawn { get; set; }
        public string LastUpdatedOn { get; set; }
    }

    public sealed class CreatureInstanceDto
    {
        public string InstanceId { get; set; }
        public string CreatureId { get; set; }
        public string Name { get; set; }
        public string OriginId { get; set; }
        public string State { get; set; }
        public int CurrentHp { get; set; }
        public string FirstSeenOn { get; set; }
        public string DiedOn { get; set; }
        public string LastEncounterId { get; set; }
    }

    public sealed class LivestockHerdDto
    {
        public string HerdId { get; set; }
        public string SettlementId { get; set; }
        public string CreatureId { get; set; }
        public int Count { get; set; }
        public string PastureKey { get; set; }
        public string KeeperNpcId { get; set; }
        public string LastUpdatedOn { get; set; }
    }

    public sealed class CombatantDto
    {
        public string CombatantId { get; set; }
        public string Kind { get; set; }
        public string SourceId { get; set; }
        public string Name { get; set; }
        public string Side { get; set; }
        public string FactionId { get; set; }
        public string Control { get; set; }
        public string CreatureId { get; set; }
        public int MaxHp { get; set; }
        public int CurrentHp { get; set; }
        public int TemporaryHp { get; set; }
        public int DeathSaveSuccesses { get; set; }
        public int DeathSaveFailures { get; set; }
        public string State { get; set; }
        public bool UsesDeathSaves { get; set; }
        public GridPosDto Position { get; set; }
        public List<ConditionDto> Conditions { get; set; }
        public TurnResourcesDto Turn { get; set; }
        public int InitiativeRoll { get; set; }
        public int Initiative { get; set; }
        public int TieBreak { get; set; }
        public int JoinedRound { get; set; }

        // Schema v11 (Phase 9).
        public Dictionary<string, int> Resources { get; set; }
        public ConcentrationDto Concentration { get; set; }
        public string FormCreatureId { get; set; }

        // Schema v12 (Phase 10).
        public GridPosDto Anchor { get; set; }
        public string CreatureInstanceId { get; set; }
    }

    public sealed class ConditionDto
    {
        public string Type { get; set; }
        public string SourceId { get; set; }
        public string Reason { get; set; }
        public int AppliedRound { get; set; }
        public string Duration { get; set; }
        public string ExpiresOn { get; set; }
        public int ExpiresRound { get; set; }
        public int Level { get; set; }
        public int EscapeDc { get; set; }

        // Schema v11 (Phase 9).
        public int InstanceId { get; set; }
        public bool RepeatSave { get; set; }
        public int SaveDc { get; set; }
        public string SaveAbility { get; set; }
        public bool EscapeByCheck { get; set; }

        // Schema v16 (Phase 14): a lasting condition brought into the fight.
        public string LastingKey { get; set; }
    }

    /// <summary>Schema v16 (Phase 14).</summary>
    public sealed class LastingConditionDto
    {
        public string Type { get; set; }
        public int Level { get; set; }
        public string Source { get; set; }
        public string Reason { get; set; }
        public long AppliedAt { get; set; }
        public long ExpiresAt { get; set; } = -1;
        public string Remedies { get; set; }
        public int EscapeDc { get; set; }
        public int EscapeTries { get; set; }
    }

    public sealed class EncounterDoorDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<GridPosDto> Squares { get; set; }
        public bool Open { get; set; }
        public bool Locked { get; set; }
    }

    public sealed class LowCoverDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Name { get; set; }
    }

    public sealed class TurnResourcesDto
    {
        public bool ActionUsed { get; set; }
        public bool BonusActionUsed { get; set; }
        public bool ReactionUsed { get; set; }
        public bool InteractionUsed { get; set; }
        public int MovementUsed { get; set; }
        public int MovementGranted { get; set; }
        public string LightAttackWeapon { get; set; }
        public int AttacksLeft { get; set; }
        public bool Disengaged { get; set; }
        public bool Dodging { get; set; }

        // Schema v11 (Phase 9).
        public bool SlotSpellCast { get; set; }
        public bool SneakAttackUsed { get; set; }
        public bool ActionSurgeUsed { get; set; }
        public bool SurgeActionPending { get; set; }
        public bool Reckless { get; set; }
        public bool AttackedOrForcedSave { get; set; }
        public string LastHitTargetId { get; set; }
        public bool LastHitMelee { get; set; }
        public bool LastHitCritical { get; set; }
        public bool LastHitMonkWeapon { get; set; }
        public bool AttackActionTaken { get; set; }
        public bool StunningStrikeUsed { get; set; }
        public bool FrenzyUsed { get; set; }
        public bool SacredWeaponUsed { get; set; }

        // Schema v12 (Phase 10).
        public bool HasStart { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }
    }

    public sealed class CombatLogEntryDto
    {
        public int Sequence { get; set; }
        public int Round { get; set; }
        public string Kind { get; set; }
        public string ActorId { get; set; }
        public string TargetId { get; set; }
        public string Text { get; set; }
        public List<int> Rolls { get; set; }
        public int Value { get; set; }
    }

    /// <summary>Schema v10 (Phase 8): a settlement structure. Its definition is data and is not saved.</summary>
    public sealed class StructureDto
    {
        public string StructureId { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string SettlementId { get; set; }
        public string LocationId { get; set; }
        public string ShopId { get; set; }
        public string OwnerNpcId { get; set; }
        public string FactionId { get; set; }
        public string LayoutKey { get; set; }
        public string Condition { get; set; }
        public string EstablishedOn { get; set; }
    }
}

namespace Realmkeeper.Infrastructure.Persistence
{
    using System.Collections.Generic;

    /// <summary>Schema v11 (Phase 9): the character's prepared spells and rests (spell picks are in <c>choices</c>).</summary>
    public sealed class SpellcastingDto
    {
        public List<string> Prepared { get; set; }
        public int PreparationChanges { get; set; } = -1;
        public long LastLongRestEnded { get; set; } = -1;
        public List<RestRecordDto> RestLog { get; set; }
    }

    public sealed class RestRecordDto
    {
        public string Key { get; set; }
        public string Kind { get; set; }
        public long EndedAt { get; set; }
        public string Summary { get; set; }
    }
}

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v11 (Phase 9): what a combatant concentrates on.</summary>
    public sealed class ConcentrationDto
    {
        public int InstanceId { get; set; }
        public string SpellId { get; set; }
        public string Name { get; set; }
        public int StartedRound { get; set; }
    }

    /// <summary>Schema v11 (Phase 9): a spell or feature effect in force.</summary>
    public sealed class ActiveEffectDto
    {
        public int Id { get; set; }
        public int InstanceId { get; set; }
        public string Origin { get; set; }
        public string Name { get; set; }
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string RelatedId { get; set; }
        public string Kind { get; set; }
        public int Value { get; set; }
        public int DiceCount { get; set; }
        public int DiceSides { get; set; }
        public string DamageType { get; set; }
        public int SpellLevel { get; set; }
        public bool Concentration { get; set; }
        public string Expiry { get; set; }
        public string ExpiresOn { get; set; }
        public int ExpiresRound { get; set; }
        public bool SingleUse { get; set; }
    }
}

namespace Realmkeeper.Infrastructure.Persistence
{
    using System.Collections.Generic;

    /// <summary>Schema v13 (Phase 11): a container and what it holds.</summary>
    public sealed class ContainerDto
    {
        public string Key { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string SettlementId { get; set; }
        public string LayoutKey { get; set; }
        public bool PlayerAccess { get; set; }
        public bool AcceptsDeposits { get; set; }
        public List<ItemStackDto> Items { get; set; }
        public int Coin { get; set; }
        public int CapacityGrams { get; set; }
        public string SourceKey { get; set; }
        public long CreatedAt { get; set; } = -1;
        public bool Opened { get; set; }

        // Schema v15 (Phase 13): the place and feature it stands at.
        public string PlaceId { get; set; }
        public string FeatureId { get; set; }
    }

    /// <summary>Schema v13 (Phase 11): the player's work in progress (or finished, kept briefly as a record).</summary>
    public sealed class WorkJobDto
    {
        public string Key { get; set; }
        public string Kind { get; set; }
        public string SettlementId { get; set; }
        public string RecipeId { get; set; }
        public string SiteKey { get; set; }
        public string StackId { get; set; }
        public List<ItemStackDto> Held { get; set; }
        public long StartedAt { get; set; }
        public long ReadyAt { get; set; }
        public int Roll { get; set; }
        public int Total { get; set; }
        public int Dc { get; set; }
        public bool Succeeded { get; set; }
        public string OutputItemId { get; set; }
        public int OutputQuantity { get; set; }
        public string OutputQuality { get; set; }
        public string Outcome { get; set; }
        public bool Done { get; set; }
    }

    /// <summary>Schema v13 (Phase 11): a place to gather a material.</summary>
    public sealed class GatheringSiteDto
    {
        public string Key { get; set; }
        public string SettlementId { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string ItemId { get; set; }
        public string ToolDiscipline { get; set; }
        public int Available { get; set; }
        public int Capacity { get; set; }
        public int PerWeek { get; set; }
        public int Yield { get; set; }
        public int Minutes { get; set; }
    }
}

namespace Realmkeeper.Infrastructure.Persistence
{
    using System.Collections.Generic;

    /// <summary>Schema v14 (Phase 12): a quest instance.</summary>
    public sealed class QuestInstanceDto
    {
        public string InstanceId { get; set; }
        public string DefinitionId { get; set; }
        public int DefinitionVersion { get; set; }
        public string State { get; set; }
        public Dictionary<string, string> Bindings { get; set; }
        public string SourceKey { get; set; }
        public long CreatedAt { get; set; } = -1;
        public long DiscoveredAt { get; set; } = -1;
        public long OfferedAt { get; set; } = -1;
        public long AcceptedAt { get; set; } = -1;
        public long ClosedAt { get; set; } = -1;
        public long ExpiresAt { get; set; } = -1;
        public List<ObjectiveProgressDto> Objectives { get; set; }
        public List<string> Decisions { get; set; }
        public Dictionary<string, string> Variables { get; set; }
        public string Outcome { get; set; }
        public bool RewardsClaimed { get; set; }
        public List<string> AppliedConsequences { get; set; }
        public string FailureReason { get; set; }
        public List<QuestHistoryDto> History { get; set; }
        public bool Tracked { get; set; }
    }

    public sealed class ObjectiveProgressDto
    {
        public string Id { get; set; }
        public int Count { get; set; }
        public bool Done { get; set; }
        public bool Failed { get; set; }
        public bool Revealed { get; set; }
        public bool Unavailable { get; set; }
        public long DoneAt { get; set; } = -1;
    }

    public sealed class QuestHistoryDto
    {
        public long At { get; set; }
        public string Text { get; set; }
    }

    public sealed class ConversationDto
    {
        public string Key { get; set; }
        public string NpcId { get; set; }
        public string DialogueId { get; set; }
        public string NodeId { get; set; }
        public string QuestInstanceId { get; set; }
        public long StartedAt { get; set; }
        public int Turn { get; set; }
        public Dictionary<string, string> Variables { get; set; }
        public List<TranscriptLineDto> Transcript { get; set; }
    }

    public sealed class TranscriptLineDto
    {
        public string Speaker { get; set; }
        public string Text { get; set; }
    }

    public sealed class PlayerSocialDto
    {
        public List<FactionStandingDto> FactionStandings { get; set; }
        public List<SettlementStandingDto> SettlementStandings { get; set; }
        public int Notoriety { get; set; }
        public List<ReputationChangeDto> Ledger { get; set; }
        public List<string> KnownPlaces { get; set; }
        public List<string> VisitedSettlements { get; set; }
        public List<DiscoveryDto> Discoveries { get; set; }
        public List<RumorDto> Rumors { get; set; }
        public List<DecisionDto> Decisions { get; set; }
        public List<string> AppliedKeys { get; set; }
    }

    public sealed class FactionStandingDto
    {
        public string FactionId { get; set; }
        public int Standing { get; set; }
    }

    public sealed class SettlementStandingDto
    {
        public string SettlementId { get; set; }
        public int Reputation { get; set; }
    }

    public sealed class ReputationChangeDto
    {
        public string Key { get; set; }
        public long At { get; set; }
        public string Target { get; set; }
        public string Dimension { get; set; }
        public int Amount { get; set; }
        public string Reason { get; set; }
    }

    public sealed class DiscoveryDto
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public long At { get; set; }
        public string Source { get; set; }

        // Phase 18 (ADR-0088): absent in older saves (read as none, confirmed, not shared).
        public int Category { get; set; }
        public int Certainty { get; set; }
        public string PlaceId { get; set; }
        public string Subject { get; set; }
        public bool Major { get; set; }
        public string ConfirmedBy { get; set; }
        public List<string> SharedIn { get; set; }
    }

    public sealed class RumorDto
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public long At { get; set; }
        public string SourceNpcId { get; set; }
        public int Confidence { get; set; }
        public bool Verified { get; set; }
    }

    public sealed class DecisionDto
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public long At { get; set; }
        public string QuestInstanceId { get; set; }
    }
}
