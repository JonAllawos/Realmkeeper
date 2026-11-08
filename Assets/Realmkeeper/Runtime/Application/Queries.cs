using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    // Query results are immutable snapshots. The presentation layer never receives mutable
    // domain objects, so it cannot change world state except through commands.

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

    public sealed class SettlementSummary
    {
        internal SettlementSummary(SettlementData s, string factionName, int dailyConsumption)
        {
            SettlementId = s.SettlementId;
            Name = s.Name;
            SettlementType = s.SettlementType;
            Position = s.Position;
            Population = s.Population;
            Prosperity = s.Prosperity;
            Stability = s.Stability;
            FoodReserves = s.FoodReserves;
            FoodProductionPerDay = s.FoodProductionPerDay;
            FoodConsumptionPerDay = dailyConsumption;
            IsInFoodShortage = s.IsInFoodShortage;
            FactionId = s.FactionId;
            FactionName = factionName;
            Resources = new List<ResourceStack>(s.ResourceInventory.Stacks);
        }

        public SettlementId SettlementId { get; }
        public string Name { get; }
        public SettlementType SettlementType { get; }
        public MapCoord Position { get; }
        public int Population { get; }
        public int Prosperity { get; }
        public int Stability { get; }
        public int FoodReserves { get; }
        public int FoodProductionPerDay { get; }
        public int FoodConsumptionPerDay { get; }
        public int NetFoodPerDay => FoodProductionPerDay - FoodConsumptionPerDay;

        /// <summary>Whole days of reserves at the current net rate; null when not in deficit.</summary>
        public int? DaysOfFoodRemaining => NetFoodPerDay < 0 ? FoodReserves / -NetFoodPerDay : (int?)null;

        public bool IsInFoodShortage { get; }
        public FactionId FactionId { get; }
        public string FactionName { get; }
        public IReadOnlyList<ResourceStack> Resources { get; }
    }

    public sealed class EventSummary
    {
        internal EventSummary(HistoricalEvent e, string settlementName)
        {
            EventId = e.EventId;
            Date = e.Date;
            EventType = e.EventType;
            Severity = e.Severity;
            SettlementId = e.SettlementId;
            SettlementName = settlementName;
            Description = e.Description;
        }

        public EventId EventId { get; }
        public WorldDate Date { get; }
        public HistoricalEventType EventType { get; }
        public EventSeverity Severity { get; }
        public SettlementId SettlementId { get; }
        public string SettlementName { get; }
        public string Description { get; }
    }
}
