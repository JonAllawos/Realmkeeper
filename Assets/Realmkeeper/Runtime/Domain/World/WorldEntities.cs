using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>Political grouping. Phase 1 carries identity only; behaviour arrives with the faction system.</summary>
    public sealed class FactionData
    {
        public FactionId FactionId { get; set; }
        public string Name { get; set; }

        public FactionData Clone() => new FactionData { FactionId = FactionId, Name = Name };
    }

    /// <summary>Character record. Model only in Phase 1; no NPCs are generated yet.</summary>
    public sealed class NpcData
    {
        public NpcId NpcId { get; set; }
        public string Name { get; set; }
        public SettlementId HomeSettlementId { get; set; }
        public FactionId FactionId { get; set; }

        public NpcData Clone() => new NpcData
        {
            NpcId = NpcId,
            Name = Name,
            HomeSettlementId = HomeSettlementId,
            FactionId = FactionId,
        };
    }

    public enum HistoricalEventType
    {
        WorldFounded = 0,
        FoodShortageBegan = 1,
        FoodShortageEnded = 2,
        FoodReservesExhausted = 3,
    }

    public enum EventSeverity
    {
        Info = 0,
        Warning = 1,
        Critical = 2,
    }

    /// <summary>Immutable chronicle entry produced by generation or simulation.</summary>
    public sealed class HistoricalEvent
    {
        public HistoricalEvent(
            EventId eventId,
            WorldDate date,
            HistoricalEventType eventType,
            EventSeverity severity,
            SettlementId settlementId,
            string description)
        {
            EventId = eventId;
            Date = date;
            EventType = eventType;
            Severity = severity;
            SettlementId = settlementId;
            Description = description ?? string.Empty;
        }

        public EventId EventId { get; }
        public WorldDate Date { get; }
        public HistoricalEventType EventType { get; }
        public EventSeverity Severity { get; }

        /// <summary>Settlement the event concerns; <see cref="SettlementId.Empty"/> for world-wide events.</summary>
        public SettlementId SettlementId { get; }

        public string Description { get; }
    }
}
