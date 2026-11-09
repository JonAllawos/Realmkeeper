using System;
using System.Collections.Generic;
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

    /// <summary>Event kinds. Persisted by name; append new values, never reorder.</summary>
    public enum HistoricalEventType
    {
        WorldFounded = 0,
        FoodShortageBegan = 1,
        FoodShortageEnded = 2,
        FoodReservesExhausted = 3,
        StarvationBegan = 4,
        ResourceShortageBegan = 5,
        ResourceShortageEnded = 6,
        TradeShipmentDelivered = 7,
        TradeShipmentReturned = 8,
        ProsperityChanged = 9,
        StabilityChanged = 10,
        SettlementGrew = 11,
        SettlementDeclined = 12,
        ProductionMilestone = 13,
        EconomicMilestone = 14,
    }

    /// <summary>Tone of an event (drives colour). Persisted by name.</summary>
    public enum EventSeverity
    {
        Info = 0,
        Warning = 1,
        Critical = 2,
        Positive = 3,
    }

    /// <summary>How much an event matters to understanding the world. Persisted by name.</summary>
    public enum EventImportance
    {
        Minor = 0,
        Notable = 1,
        Major = 2,
    }

    /// <summary>
    /// Immutable chronicle entry produced by generation or simulation. New events carry
    /// structured facts (<see cref="SettlementIds"/>, <see cref="Data"/>) and no prose; the
    /// application layer formats them. <see cref="Description"/> remains for events from
    /// older saves that only have text.
    /// </summary>
    public sealed class HistoricalEvent
    {
        private static readonly IReadOnlyDictionary<string, string> NoData = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public HistoricalEvent(
            EventId eventId,
            WorldDate date,
            HistoricalEventType eventType,
            EventSeverity severity,
            SettlementId settlementId,
            string description)
            : this(eventId, date, eventType, severity, EventImportance.Notable,
                settlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { settlementId }, null, description)
        {
        }

        public HistoricalEvent(
            EventId eventId,
            WorldDate date,
            HistoricalEventType eventType,
            EventSeverity severity,
            EventImportance importance,
            IReadOnlyList<SettlementId> settlementIds,
            IEnumerable<KeyValuePair<string, string>> data,
            string description = null)
        {
            EventId = eventId;
            Date = date;
            EventType = eventType;
            Severity = severity;
            Importance = importance;
            SettlementIds = settlementIds != null ? new List<SettlementId>(settlementIds) : new List<SettlementId>();
            if (data != null)
            {
                var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in data) copy[pair.Key] = pair.Value ?? string.Empty;
                Data = copy;
            }
            else
            {
                Data = NoData;
            }

            Description = description ?? string.Empty;
        }

        public EventId EventId { get; }
        public WorldDate Date { get; }
        public HistoricalEventType EventType { get; }
        public EventSeverity Severity { get; }
        public EventImportance Importance { get; }

        /// <summary>Settlements involved, primary first. Empty for world-wide events.</summary>
        public IReadOnlyList<SettlementId> SettlementIds { get; }

        /// <summary>Primary settlement; <see cref="SettlementId.Empty"/> for world-wide events.</summary>
        public SettlementId SettlementId => SettlementIds.Count > 0 ? SettlementIds[0] : SettlementId.Empty;

        /// <summary>Structured facts (ordinal-sorted keys; values are invariant-culture strings).</summary>
        public IReadOnlyDictionary<string, string> Data { get; }

        /// <summary>Legacy prose for events that predate structured data; empty otherwise.</summary>
        public string Description { get; }

        public string Get(string key) => Data.TryGetValue(key, out var v) ? v : null;

        public int GetInt(string key, int fallback = 0) =>
            Data.TryGetValue(key, out var v) && int.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }
}
