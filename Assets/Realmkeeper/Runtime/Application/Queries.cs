using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Application
{
    // Query results are immutable snapshots. The presentation layer never receives mutable
    // domain objects, so it cannot change world state except through commands. Every number
    // here is read from authoritative simulation data; nothing is recomputed for display
    // except plain arithmetic on those values (net change, days of cover).

    public sealed class WorldOverview
    {
        public WorldOverview(WorldId worldId, string worldName, long seed, WorldDate date, SimulationSpeed speed, int settlementCount, int eventCount)
        {
            WorldId = worldId;
            WorldName = worldName;
            Seed = seed;
            Date = date;
            Speed = speed;
            SettlementCount = settlementCount;
            EventCount = eventCount;
        }

        public WorldId WorldId { get; }
        public string WorldName { get; }
        public long Seed { get; }
        public WorldDate Date { get; }
        public SimulationSpeed Speed { get; }
        public int SettlementCount { get; }
        public int EventCount { get; }
    }

    public enum RatingTrend
    {
        Falling = -1,
        Steady = 0,
        Rising = 1,
    }

    /// <summary>One-line economic condition, most severe first.</summary>
    public enum EconomicStatus
    {
        Starving = 0,
        Shortage = 1,
        Strained = 2,
        Stable = 3,
        Thriving = 4,
    }

    /// <summary>Last day's movement of one resource in one settlement.</summary>
    public sealed class ResourceSummary
    {
        internal ResourceSummary(ResourceTypeId resource, int stock, int capacity, ResourceFlow flow, bool inShortage)
        {
            ResourceTypeId = resource;
            Stock = stock;
            Capacity = capacity;
            Produced = flow?.Produced ?? 0;
            Wasted = flow?.Wasted ?? 0;
            Demand = flow?.Demand ?? 0;
            Consumed = flow?.Consumed ?? 0;
            Shortfall = flow?.Shortfall ?? 0;
            Imported = flow?.Imported ?? 0;
            Exported = flow?.Exported ?? 0;
            InShortage = inShortage;
        }

        public ResourceTypeId ResourceTypeId { get; }
        public int Stock { get; }

        /// <summary>Storage limit, or int.MaxValue when unlimited.</summary>
        public int Capacity { get; }

        public int Produced { get; }
        public int Wasted { get; }
        public int Demand { get; }
        public int Consumed { get; }
        public int Shortfall { get; }
        public int Imported { get; }
        public int Exported { get; }
        public bool InShortage { get; }

        /// <summary>Own production minus own needs (excludes trade).</summary>
        public int NetProduction => Produced - Demand;

        /// <summary>Total change in stock last day, including trade.</summary>
        public int NetChange => Produced + Imported - Consumed - Exported;

        /// <summary>Days until exhaustion if own production stays below need; null when not in deficit.</summary>
        public int? DaysRemaining => NetProduction < 0 ? Stock / -NetProduction : (int?)null;
    }

    public sealed class ProductionSummary
    {
        internal ProductionSummary(ProductionAssignment p, int lastOutput)
        {
            SourceType = p.SourceType;
            Output = p.Output;
            Workers = p.Workers;
            WorkerCapacity = p.WorkerCapacity;
            EfficiencyPercent = p.EfficiencyPercent;
            LastOutput = lastOutput;
        }

        public ProductionSourceType SourceType { get; }
        public ResourceTypeId Output { get; }
        public int Workers { get; }
        public int WorkerCapacity { get; }
        public int EfficiencyPercent { get; }
        public int LastOutput { get; }
    }

    /// <summary>Conditions shown as small markers above a settlement on the strategic map.</summary>
    [Flags]
    public enum SettlementIndicators
    {
        None = 0,

        /// <summary>In food shortage, or already out of food.</summary>
        FoodShortage = 1,

        /// <summary>Last day's production value per person scores at least <see cref="SettlementSummary.StrongOutputScore"/>.</summary>
        StrongProduction = 2,

        /// <summary>A shipment to or from the settlement is on the road.</summary>
        ActiveTrade = 4,

        /// <summary>Stability is falling toward a target at least <see cref="SettlementSummary.DecliningStabilityGap"/> points lower.</summary>
        DecliningStability = 8,
    }

    /// <summary>How a food shortage is developing (judged from the last day's change in reserves).</summary>
    public enum ShortageOutlook
    {
        /// <summary>No food shortage.</summary>
        None = 0,
        Easing = 1,
        Unchanged = 2,
        Worsening = 3,
    }

    /// <summary>Why a settlement's prosperity and stability targets are what they are.</summary>
    public sealed class WellbeingBreakdown
    {
        internal WellbeingBreakdown(IReadOnlyList<WellbeingContribution> prosperity, IReadOnlyList<WellbeingContribution> stability)
        {
            Prosperity = prosperity;
            Stability = stability;
            ProsperityTarget = EconomyMath.TargetFrom(prosperity);
            StabilityTarget = EconomyMath.TargetFrom(stability);
        }

        /// <summary>Terms of the prosperity target (weights in Documentation/Systems/Economy.md).</summary>
        public IReadOnlyList<WellbeingContribution> Prosperity { get; }

        public IReadOnlyList<WellbeingContribution> Stability { get; }

        /// <summary>The prosperity target these terms add up to (equal to the simulation's formula).</summary>
        public int ProsperityTarget { get; }

        public int StabilityTarget { get; }
    }

    public sealed class SettlementSummary
    {
        /// <summary>Output score (0–100) from which a settlement counts as strongly productive on the map.</summary>
        public const int StrongOutputScore = 75;

        /// <summary>How far below current stability the target must be for the map to flag a decline.</summary>
        public const int DecliningStabilityGap = 5;

        internal SettlementSummary(SettlementData s, string factionName, EconomyRules rules, bool hasTradeOnRoad = false)
        {
            var e = s.Economy;
            SettlementId = s.SettlementId;
            Name = s.Name;
            SettlementType = s.SettlementType;
            Position = s.Position;
            Population = s.Population;
            Prosperity = s.Prosperity;
            Stability = s.Stability;
            ProsperityTarget = e.ProsperityTarget;
            StabilityTarget = e.StabilityTarget;
            IsInFoodShortage = s.IsInFoodShortage;
            DaysWithoutFood = e.DaysWithoutFood;
            FactionId = s.FactionId;
            FactionName = factionName;
            Specialization = e.Specialization;
            SecondarySpecialization = e.SecondarySpecialization;
            LabourFocus = e.LabourFocus;
            RegionId = s.RegionId;
            Workforce = EconomyMath.Workforce(s.Population, rules);
            Employed = e.TotalWorkers;
            TradeActivity = e.TradeActivity;
            Resources = new List<ResourceStack>(s.ResourceInventory.Stacks);

            var rows = new List<ResourceSummary>();
            foreach (var r in ResourceTypes.All)
            {
                var inShortage = r == ResourceTypes.Food ? s.IsInFoodShortage : e.FindStatus(r)?.InShortage == true;
                rows.Add(new ResourceSummary(r, s.ResourceInventory.Get(r), e.CapacityOf(r), e.FindFlow(r), inShortage));
            }

            Economy = rows;

            var production = new List<ProductionSummary>();
            foreach (var p in e.Production)
            {
                var output = 0;
                foreach (var so in e.LastDaySources)
                {
                    if (so.SourceType == p.SourceType) output = so.Amount;
                }

                production.Add(new ProductionSummary(p, output));
            }

            Production = production;
            Status = ComputeStatus(this);

            var factors = EconomyMath.Factors(s, rules);
            Wellbeing = new WellbeingBreakdown(EconomyMath.ProsperityContributions(factors), EconomyMath.StabilityContributions(factors, s.Prosperity));

            var food = Food;
            FoodReserveTrend = food.NetChange > 0 ? RatingTrend.Rising : food.NetChange < 0 ? RatingTrend.Falling : RatingTrend.Steady;
            FoodShortageOutlook = OutlookFor(s.IsInFoodShortage, e.DaysWithoutFood, FoodReserveTrend);
            Indicators = IndicatorsFor(s.IsInFoodShortage, e.DaysWithoutFood, factors.Output, hasTradeOnRoad, Stability, StabilityTarget);
        }

        /// <summary>
        /// Shortage outlook rule: no shortage (and food not run out) → None; reserves rose last day →
        /// Easing; reserves fell, or people are going without food → Worsening; otherwise Unchanged.
        /// </summary>
        public static ShortageOutlook OutlookFor(bool inFoodShortage, int daysWithoutFood, RatingTrend reserveTrend)
        {
            if (!inFoodShortage && daysWithoutFood == 0) return ShortageOutlook.None;
            if (reserveTrend == RatingTrend.Rising) return ShortageOutlook.Easing;
            if (reserveTrend == RatingTrend.Falling || daysWithoutFood > 0) return ShortageOutlook.Worsening;
            return ShortageOutlook.Unchanged;
        }

        /// <summary>Map indicator rule (see <see cref="SettlementIndicators"/>).</summary>
        public static SettlementIndicators IndicatorsFor(bool inFoodShortage, int daysWithoutFood, int outputScore, bool hasTradeOnRoad,
            int stability, int stabilityTarget)
        {
            var indicators = SettlementIndicators.None;
            if (inFoodShortage || daysWithoutFood > 0) indicators |= SettlementIndicators.FoodShortage;
            if (outputScore >= StrongOutputScore) indicators |= SettlementIndicators.StrongProduction;
            if (hasTradeOnRoad) indicators |= SettlementIndicators.ActiveTrade;
            if (stability - stabilityTarget >= DecliningStabilityGap) indicators |= SettlementIndicators.DecliningStability;
            return indicators;
        }

        /// <summary>Why prosperity and stability are heading where they are.</summary>
        public WellbeingBreakdown Wellbeing { get; }

        /// <summary>Direction of the food reserves over the last day (production, consumption and trade together).</summary>
        public RatingTrend FoodReserveTrend { get; }

        /// <summary>Whether a food shortage is easing or worsening (None when there is no shortage).</summary>
        public ShortageOutlook FoodShortageOutlook { get; }

        /// <summary>True when the settlement's own food production covered its demand last day.</summary>
        public bool FoodProductionMeetsDemand => Food.Produced >= Food.Demand;

        /// <summary>Map markers for this settlement, from authoritative simulation data.</summary>
        public SettlementIndicators Indicators { get; }

        public SettlementId SettlementId { get; }
        public string Name { get; }
        public SettlementType SettlementType { get; }
        public MapCoord Position { get; }
        public int Population { get; }
        public int Prosperity { get; }
        public int Stability { get; }
        public int ProsperityTarget { get; }
        public int StabilityTarget { get; }
        public RatingTrend ProsperityTrend => Trend(Prosperity, ProsperityTarget);
        public RatingTrend StabilityTrend => Trend(Stability, StabilityTarget);
        public bool IsInFoodShortage { get; }
        public int DaysWithoutFood { get; }
        public FactionId FactionId { get; }
        public string FactionName { get; }
        public SettlementSpecialization Specialization { get; }
        public SettlementSpecialization SecondarySpecialization { get; }

        /// <summary>Production source favoured by a posted labour notice, if any.</summary>
        public ProductionSourceType? LabourFocus { get; }

        public RegionId RegionId { get; }
        public int Workforce { get; }
        public int Employed { get; }
        public int TradeActivity { get; }
        public EconomicStatus Status { get; }

        /// <summary>One row per simulated resource, in display order.</summary>
        public IReadOnlyList<ResourceSummary> Economy { get; }

        public IReadOnlyList<ProductionSummary> Production { get; }

        public IReadOnlyList<ResourceStack> Resources { get; }

        public ResourceSummary Food => Find(ResourceTypes.Food);

        public int FoodReserves => Food.Stock;
        public int FoodProductionPerDay => Food.Produced;
        public int FoodConsumptionPerDay => Food.Demand;
        public int NetFoodPerDay => Food.NetProduction;

        /// <summary>Whole days of reserves at the current own-production deficit; null when not in deficit.</summary>
        public int? DaysOfFoodRemaining => Food.DaysRemaining;

        public ResourceSummary Find(ResourceTypeId resource)
        {
            foreach (var row in Economy)
            {
                if (row.ResourceTypeId == resource) return row;
            }

            return null;
        }

        private static RatingTrend Trend(int current, int target) =>
            target > current ? RatingTrend.Rising : target < current ? RatingTrend.Falling : RatingTrend.Steady;

        private static EconomicStatus ComputeStatus(SettlementSummary s)
        {
            if (s.DaysWithoutFood > 0) return EconomicStatus.Starving;
            foreach (var row in s.Economy)
            {
                if (row.InShortage) return EconomicStatus.Shortage;
            }

            var foodDeficit = s.Food.NetChange < 0 && s.Food.NetProduction < 0;
            if (foodDeficit || s.ProsperityTrend == RatingTrend.Falling && s.ProsperityTarget < s.Prosperity - 5) return EconomicStatus.Strained;
            if (s.Prosperity >= 65 && s.ProsperityTrend != RatingTrend.Falling) return EconomicStatus.Thriving;
            return EconomicStatus.Stable;
        }
    }

    public sealed class TradeRouteSummary
    {
        internal TradeRouteSummary(TradeRouteData r, string originName, string destinationName, ShipmentData shipment, WorldDate today)
        {
            TradeRouteId = r.TradeRouteId;
            OriginId = r.OriginId;
            DestinationId = r.DestinationId;
            OriginName = originName;
            DestinationName = destinationName;
            Status = r.Status;
            IsSuspended = r.IsSuspended;
            CapacityPerShipment = r.CapacityPerShipment;
            TravelDays = r.TravelDays;
            FrequencyDays = r.FrequencyDays;
            EfficiencyPercent = r.EfficiencyPercent;
            ShipmentsDelivered = r.ShipmentsDelivered;
            Delivered = new List<ResourceStack>(r.Delivered.Stacks);
            if (r.LastDeliveryDay >= 0)
            {
                LastDeliveryDate = WorldDate.FromDayNumber(r.LastDeliveryDay);
                LastDeliveryResource = r.LastDeliveryResource;
                LastDeliveryQuantity = r.LastDeliveryQuantity;
                LastDeliveryPayment = r.LastDeliveryPayment;
            }

            if (shipment != null)
            {
                InTransitResource = shipment.ResourceTypeId;
                InTransitQuantity = shipment.Quantity;
                InTransitReturning = shipment.IsReturning;
                DaysUntilArrival = (int)Math.Max(0, shipment.ArrivalDay - today.ToDayNumber());
            }
        }

        public TradeRouteId TradeRouteId { get; }
        public SettlementId OriginId { get; }
        public SettlementId DestinationId { get; }
        public string OriginName { get; }
        public string DestinationName { get; }
        public TradeRouteStatus Status { get; }

        /// <summary>Suspended by command: no new shipments leave (one already on the road still arrives).</summary>
        public bool IsSuspended { get; }

        public int CapacityPerShipment { get; }
        public int TravelDays { get; }
        public int FrequencyDays { get; }
        public int EfficiencyPercent { get; }
        public int ShipmentsDelivered { get; }
        public IReadOnlyList<ResourceStack> Delivered { get; }
        public WorldDate? LastDeliveryDate { get; }
        public ResourceTypeId LastDeliveryResource { get; }
        public int LastDeliveryQuantity { get; }
        public int LastDeliveryPayment { get; }
        public bool HasShipmentOnRoad => InTransitQuantity > 0;
        public ResourceTypeId InTransitResource { get; }
        public int InTransitQuantity { get; }
        public bool InTransitReturning { get; }
        public int DaysUntilArrival { get; }
    }

    public sealed class RegionSummary
    {
        internal RegionSummary(RegionData r, int settlementCount)
        {
            RegionId = r.RegionId;
            Name = r.Name;
            Origin = r.Origin;
            Width = r.Width;
            Depth = r.Depth;
            SettlementCount = settlementCount;
            Kind = r.Kind; // Phase 21 assets: trees by region
        }

        public RegionId RegionId { get; }
        public string Name { get; }

        /// <summary>The kind of country (vale, highlands, woodland …).</summary>
        public RegionKind Kind { get; }
        public MapCoord Origin { get; }
        public int Width { get; }
        public int Depth { get; }
        public int SettlementCount { get; }
    }

    /// <summary>Snapshot of the player character for both presentations.</summary>
    public sealed class PlayerSummary
    {
        internal PlayerSummary(PlayerCharacterData p, string settlementName)
        {
            CharacterId = p.CharacterId;
            Name = p.Name;
            LocationKind = p.LocationKind;
            SettlementId = p.SettlementId;
            SettlementName = settlementName;
            RegionId = p.RegionId;
            Pose = p.Pose;
            Perspective = p.Perspective;
            LastSettlementId = p.LastSettlementId;
        }

        public CharacterId CharacterId { get; }
        public string Name { get; }
        public CharacterLocationKind LocationKind { get; }
        public bool IsInSettlement => LocationKind == CharacterLocationKind.Settlement;
        public SettlementId SettlementId { get; }
        public string SettlementName { get; }
        public RegionId RegionId { get; }
        public LocalPose Pose { get; }
        public CameraPerspective Perspective { get; }
        public SettlementId LastSettlementId { get; }
    }

    public sealed class NpcSummary
    {
        internal NpcSummary(NpcData n, LocationData location = null)
        {
            Sex = n.Sex;
            Tier = n.Tier;
            IsAlive = n.IsAlive;
            CurrentLocationId = n.CurrentLocationId;
            LocationKind = location?.Kind;
            LocationKey = location?.LayoutKey;
            LocationName = location?.Name;
            NpcId = n.NpcId;
            Name = n.Name;
            HomeSettlementId = n.HomeSettlementId;
            Role = n.Role;
            HasMetPlayer = n.HasMetPlayer;
            MetPlayerOn = n.MetPlayerOn;
            ConversationCount = n.ConversationCount;
        }

        public NpcId NpcId { get; }
        public string Name { get; }
        public SettlementId HomeSettlementId { get; }
        public NpcRole Role { get; }
        public bool HasMetPlayer { get; }
        public WorldDate MetPlayerOn { get; }
        public int ConversationCount { get; }

        // Phase 4.
        public NpcSex Sex { get; }
        public NpcTier Tier { get; }
        public bool IsAlive { get; }
        public LocationId CurrentLocationId { get; }
        public LocationKind? LocationKind { get; }

        /// <summary>The layout site of today's location (e.g. "hall", "plaza"), when known.</summary>
        public string LocationKey { get; }
        public string LocationName { get; }
    }

    public sealed class EventSummary
    {
        internal EventSummary(HistoricalEvent e, string settlementName, string summary)
        {
            EventId = e.EventId;
            Date = e.Date;
            EventType = e.EventType;
            Severity = e.Severity;
            Importance = e.Importance;
            SettlementId = e.SettlementId;
            SettlementIds = e.SettlementIds;
            FactionIds = e.FactionIds;
            IsManual = EventFormatter.IsManual(e);
            SettlementName = settlementName;
            Data = e.Data;
            Summary = summary;
        }

        public EventId EventId { get; }
        public WorldDate Date { get; }
        public HistoricalEventType EventType { get; }
        public EventSeverity Severity { get; }
        public EventImportance Importance { get; }
        public SettlementId SettlementId { get; }
        public IReadOnlyList<SettlementId> SettlementIds { get; }

        /// <summary>Factions involved, primary first (still valid after a faction dissolves).</summary>
        public IReadOnlyList<FactionId> FactionIds { get; }

        /// <summary>True when a player command caused the event, false when the simulation did.</summary>
        public bool IsManual { get; }

        public string SettlementName { get; }
        public IReadOnlyDictionary<string, string> Data { get; }

        /// <summary>Human-readable sentence built by <see cref="EventFormatter"/> from the event's facts.</summary>
        public string Summary { get; }

        /// <summary>Same as <see cref="Summary"/> (kept for Phase 1 callers).</summary>
        public string Description => Summary;
    }
}
