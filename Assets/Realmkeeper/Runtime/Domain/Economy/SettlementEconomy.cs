using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Economy
{
    /// <summary>Kinds of work that produce resources. Values are persisted by name.</summary>
    public enum ProductionSourceType
    {
        Farming = 0,
        Fishing = 1,
        Hunting = 2,
        Logging = 3,
        Quarrying = 4,
        Mining = 5,
        Trade = 6,
    }

    /// <summary>A settlement's economic identity, derived from its surroundings.</summary>
    public enum SettlementSpecialization
    {
        None = 0,
        Agricultural = 1,
        Forestry = 2,
        Quarrying = 3,
        Mining = 4,
        Trade = 5,
    }

    public static class ProductionSources
    {
        /// <summary>Every source type in declaration order (the order systems iterate them).</summary>
        public static IReadOnlyList<ProductionSourceType> All { get; } = new[]
        {
            ProductionSourceType.Farming, ProductionSourceType.Fishing, ProductionSourceType.Hunting,
            ProductionSourceType.Logging, ProductionSourceType.Quarrying, ProductionSourceType.Mining,
            ProductionSourceType.Trade,
        };

        public static ResourceTypeId OutputOf(ProductionSourceType source)
        {
            switch (source)
            {
                case ProductionSourceType.Farming:
                case ProductionSourceType.Fishing:
                case ProductionSourceType.Hunting:
                    return ResourceTypes.Food;
                case ProductionSourceType.Logging: return ResourceTypes.Timber;
                case ProductionSourceType.Quarrying: return ResourceTypes.Stone;
                case ProductionSourceType.Mining: return ResourceTypes.Iron;
                case ProductionSourceType.Trade: return ResourceTypes.Coin;
                default: throw new ArgumentOutOfRangeException(nameof(source));
            }
        }

        /// <summary>The specialization a source belongs to (fishing and hunting count as agricultural).</summary>
        public static SettlementSpecialization SpecializationOf(ProductionSourceType source)
        {
            switch (source)
            {
                case ProductionSourceType.Farming:
                case ProductionSourceType.Fishing:
                case ProductionSourceType.Hunting:
                    return SettlementSpecialization.Agricultural;
                case ProductionSourceType.Logging: return SettlementSpecialization.Forestry;
                case ProductionSourceType.Quarrying: return SettlementSpecialization.Quarrying;
                case ProductionSourceType.Mining: return SettlementSpecialization.Mining;
                case ProductionSourceType.Trade: return SettlementSpecialization.Trade;
                default: throw new ArgumentOutOfRangeException(nameof(source));
            }
        }
    }

    /// <summary>
    /// One production capability of a settlement. <see cref="WorkerCapacity"/> and
    /// <see cref="EfficiencyPercent"/> come from the land; <see cref="AllocationWeight"/> is
    /// labour policy (editable later without changing the economy system); <see cref="Workers"/>
    /// is assigned each day by the labour system.
    /// </summary>
    public sealed class ProductionAssignment
    {
        public ProductionSourceType SourceType { get; set; }

        /// <summary>Relative share of the workforce this source asks for (0 = unstaffed).</summary>
        public int AllocationWeight { get; set; }

        /// <summary>Maximum workers the surrounding land or infrastructure can employ.</summary>
        public int WorkerCapacity { get; set; }

        /// <summary>Output multiplier in percent (100 = baseline). Includes specialization bonuses.</summary>
        public int EfficiencyPercent { get; set; } = 100;

        /// <summary>Workers currently assigned (0..WorkerCapacity).</summary>
        public int Workers { get; set; }

        public ResourceTypeId Output => ProductionSources.OutputOf(SourceType);

        public ProductionAssignment Clone() => new ProductionAssignment
        {
            SourceType = SourceType,
            AllocationWeight = AllocationWeight,
            WorkerCapacity = WorkerCapacity,
            EfficiencyPercent = EfficiencyPercent,
            Workers = Workers,
        };
    }

    /// <summary>One day's movements of one resource in one settlement (all integral units).</summary>
    public sealed class ResourceFlow
    {
        public ResourceTypeId ResourceTypeId { get; set; }

        /// <summary>Output that reached storage.</summary>
        public int Produced { get; set; }

        /// <summary>Output discarded because storage was full.</summary>
        public int Wasted { get; set; }

        /// <summary>Amount the population needed today.</summary>
        public int Demand { get; set; }

        /// <summary>Amount actually consumed (Demand minus Shortfall).</summary>
        public int Consumed { get; set; }

        /// <summary>Unmet demand.</summary>
        public int Shortfall { get; set; }

        public int Imported { get; set; }

        public int Exported { get; set; }

        /// <summary>Change in stored quantity today.</summary>
        public int NetChange => Produced + Imported - Consumed - Exported;

        public ResourceFlow Clone() => new ResourceFlow
        {
            ResourceTypeId = ResourceTypeId,
            Produced = Produced,
            Wasted = Wasted,
            Demand = Demand,
            Consumed = Consumed,
            Shortfall = Shortfall,
            Imported = Imported,
            Exported = Exported,
        };
    }

    /// <summary>Output of one production source on the last simulated day.</summary>
    public sealed class SourceOutput
    {
        public ProductionSourceType SourceType { get; set; }
        public int Workers { get; set; }
        public int Amount { get; set; }

        public SourceOutput Clone() => new SourceOutput { SourceType = SourceType, Workers = Workers, Amount = Amount };
    }

    /// <summary>Shortage tracking for one non-food resource (food uses SettlementData.IsInFoodShortage).</summary>
    public sealed class ResourceStatus
    {
        public ResourceTypeId ResourceTypeId { get; set; }
        public bool InShortage { get; set; }

        /// <summary>Consecutive days the resource has been below its shortage line (or in shortage).</summary>
        public int DaysBelowThreshold { get; set; }

        public ResourceStatus Clone() => new ResourceStatus { ResourceTypeId = ResourceTypeId, InShortage = InShortage, DaysBelowThreshold = DaysBelowThreshold };
    }

    /// <summary>Values last reported to the chronicle, so milestones are recorded once, not every day.</summary>
    public sealed class ChronicleMarkers
    {
        public int ReportedProsperity { get; set; }
        public int ReportedStability { get; set; }
        public int ReportedPopulation { get; set; }

        /// <summary>Highest treasury milestone already reported (0 = none).</summary>
        public int CoinMilestone { get; set; }

        /// <summary>Day numbers of the last food-crisis reports (-1 = never), so a flickering crisis is not reported daily.</summary>
        public long LastFoodExhaustedReportDay { get; set; } = -1;
        public long LastStarvationReportDay { get; set; } = -1;

        /// <summary>Index into the production milestone ladder, per resource.</summary>
        public SortedDictionary<ResourceTypeId, int> ProductionMilestoneIndex { get; set; } = new SortedDictionary<ResourceTypeId, int>();

        public ChronicleMarkers Clone() => new ChronicleMarkers
        {
            ReportedProsperity = ReportedProsperity,
            ReportedStability = ReportedStability,
            ReportedPopulation = ReportedPopulation,
            CoinMilestone = CoinMilestone,
            LastFoodExhaustedReportDay = LastFoodExhaustedReportDay,
            LastStarvationReportDay = LastStarvationReportDay,
            ProductionMilestoneIndex = new SortedDictionary<ResourceTypeId, int>(ProductionMilestoneIndex),
        };
    }

    /// <summary>
    /// Authoritative economic state of one settlement. Written only by world generation and
    /// simulation systems; the UI reads it through query snapshots.
    /// </summary>
    public sealed class SettlementEconomy
    {
        public SettlementSpecialization Specialization { get; set; }
        public SettlementSpecialization SecondarySpecialization { get; set; }

        /// <summary>Production capabilities, one per source type, kept in source-type order.</summary>
        public List<ProductionAssignment> Production { get; set; } = new List<ProductionAssignment>();

        /// <summary>Maximum stored quantity per resource. A resource without an entry has no limit.</summary>
        public ResourceInventory StorageCapacity { get; set; } = new ResourceInventory();

        /// <summary>Day number (WorldDate.ToDayNumber) that <see cref="LastDay"/> describes.</summary>
        public long LedgerDay { get; set; } = -1;

        /// <summary>Movements of each resource on the last simulated day (or the projection at founding).</summary>
        public List<ResourceFlow> LastDay { get; set; } = new List<ResourceFlow>();

        /// <summary>Per-source output on the last simulated day.</summary>
        public List<SourceOutput> LastDaySources { get; set; } = new List<SourceOutput>();

        public List<ResourceStatus> ResourceStatuses { get; set; } = new List<ResourceStatus>();

        /// <summary>Total produced since founding, per resource (drives production milestones).</summary>
        public ResourceInventory LifetimeProduced { get; set; } = new ResourceInventory();

        /// <summary>Consecutive days on which food demand went unmet.</summary>
        public int DaysWithoutFood { get; set; }

        /// <summary>Fractional population change carried between days, in thousandths of a person.</summary>
        public int PopulationProgressMilli { get; set; }

        /// <summary>Where prosperity is heading under current conditions (0..100).</summary>
        public int ProsperityTarget { get; set; }

        /// <summary>Where stability is heading under current conditions (0..100).</summary>
        public int StabilityTarget { get; set; }

        /// <summary>Decaying measure of recent trade (goods value moved in or out, about the last month).</summary>
        public int TradeActivity { get; set; }

        public ChronicleMarkers Markers { get; set; } = new ChronicleMarkers();

        /// <summary>False for a settlement loaded from a pre-economy save; the session initializes it from the terrain.</summary>
        public bool IsInitialized => Production.Count > 0 || StorageCapacity.Count > 0;

        public int TotalWorkers
        {
            get
            {
                var total = 0;
                foreach (var p in Production) total += p.Workers;
                return total;
            }
        }

        public ProductionAssignment FindProduction(ProductionSourceType source)
        {
            foreach (var p in Production)
            {
                if (p.SourceType == source) return p;
            }

            return null;
        }

        public ResourceFlow FindFlow(ResourceTypeId resource)
        {
            foreach (var f in LastDay)
            {
                if (f.ResourceTypeId == resource) return f;
            }

            return null;
        }

        public ResourceStatus FindStatus(ResourceTypeId resource)
        {
            foreach (var s in ResourceStatuses)
            {
                if (s.ResourceTypeId == resource) return s;
            }

            return null;
        }

        /// <summary>Capacity for a resource, or int.MaxValue when unlimited.</summary>
        public int CapacityOf(ResourceTypeId resource)
        {
            foreach (var stack in StorageCapacity.Stacks)
            {
                if (stack.ResourceTypeId == resource) return stack.Quantity;
            }

            return int.MaxValue;
        }

        public SettlementEconomy Clone()
        {
            var copy = new SettlementEconomy
            {
                Specialization = Specialization,
                SecondarySpecialization = SecondarySpecialization,
                Production = new List<ProductionAssignment>(Production.Count),
                StorageCapacity = StorageCapacity.Clone(),
                LedgerDay = LedgerDay,
                LastDay = new List<ResourceFlow>(LastDay.Count),
                LastDaySources = new List<SourceOutput>(LastDaySources.Count),
                ResourceStatuses = new List<ResourceStatus>(ResourceStatuses.Count),
                LifetimeProduced = LifetimeProduced.Clone(),
                DaysWithoutFood = DaysWithoutFood,
                PopulationProgressMilli = PopulationProgressMilli,
                ProsperityTarget = ProsperityTarget,
                StabilityTarget = StabilityTarget,
                TradeActivity = TradeActivity,
                Markers = Markers?.Clone() ?? new ChronicleMarkers(),
            };

            foreach (var p in Production) copy.Production.Add(p.Clone());
            foreach (var f in LastDay) copy.LastDay.Add(f.Clone());
            foreach (var s in LastDaySources) copy.LastDaySources.Add(s.Clone());
            foreach (var s in ResourceStatuses) copy.ResourceStatuses.Add(s.Clone());
            return copy;
        }
    }
}
