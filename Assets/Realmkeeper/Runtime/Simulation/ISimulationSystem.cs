using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation
{
    /// <summary>
    /// One daily simulation rule set. Systems are plain C#: they receive a working copy of
    /// the world and must be deterministic given (world state, context.Random).
    /// Rules: no wall-clock time, no System.Random, no static mutable state, no iteration
    /// over unordered collections whose order can vary between runs.
    /// </summary>
    public interface ISimulationSystem
    {
        /// <summary>Stable, unique key (e.g. "realmkeeper.food"). Also seeds the system's random stream.</summary>
        string SystemId { get; }

        /// <summary>Ascending execution order within a day. Must be unique per scheduler.</summary>
        int ExecutionOrder { get; }

        void Execute(SimulationDayContext context);
    }

    /// <summary>Small builder for structured event facts, always formatted with the invariant culture.</summary>
    public sealed class EventData
    {
        private readonly SortedDictionary<string, string> _values = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public IEnumerable<KeyValuePair<string, string>> Values => _values;

        public EventData Set(string key, string value)
        {
            _values[key] = value ?? string.Empty;
            return this;
        }

        public EventData Set(string key, long value)
        {
            _values[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return this;
        }
    }

    /// <summary>
    /// Per-day execution context. The scheduler gives every system its own random stream
    /// derived from (world seed, day number, system ID), so adding or removing one system
    /// never changes the random outcomes of another.
    /// </summary>
    public sealed class SimulationDayContext
    {
        private readonly IRandomSourceFactory _randomFactory;
        private readonly ulong _daySeed;
        private readonly IRandomSource _eventIdRandom;
        private readonly List<HistoricalEvent> _events = new List<HistoricalEvent>();

        internal SimulationDayContext(WorldData workingWorld, WorldDate date, IRandomSourceFactory randomFactory)
        {
            World = workingWorld;
            Date = date;
            _randomFactory = randomFactory;
            _daySeed = SeedMixer.Combine(SeedMixer.FromWorldSeed(workingWorld.Seed), (ulong)date.ToDayNumber());
            _eventIdRandom = randomFactory.Create(SeedMixer.Combine(_daySeed, "realmkeeper.event-ids"));
        }

        /// <summary>Working copy of the world. Changes are committed only if the whole day validates.</summary>
        public WorldData World { get; }

        /// <summary>The day being simulated (already set as the working world's current date).</summary>
        public WorldDate Date { get; }

        /// <summary>Deterministic random stream for the currently executing system.</summary>
        public IRandomSource Random { get; private set; }

        public string CurrentSystemId { get; private set; }

        public IReadOnlyList<HistoricalEvent> Events => _events;

        /// <summary>Legacy text event (kept for systems and tests that predate structured events).</summary>
        public HistoricalEvent ReportEvent(HistoricalEventType type, EventSeverity severity, SettlementId settlementId, string description)
        {
            var e = new HistoricalEvent(new EventId(DeterministicGuid.Next(_eventIdRandom)), Date, type, severity, settlementId, description);
            _events.Add(e);
            return e;
        }

        /// <summary>
        /// Records a structured event: facts only, no prose. The application layer formats it.
        /// Data values must already be invariant-culture strings (use <see cref="EventData"/>).
        /// </summary>
        public HistoricalEvent ReportEvent(HistoricalEventType type, EventSeverity severity, EventImportance importance,
            IReadOnlyList<SettlementId> settlementIds, EventData data = null)
        {
            var e = new HistoricalEvent(new EventId(DeterministicGuid.Next(_eventIdRandom)), Date, type, severity, importance,
                settlementIds, data?.Values);
            _events.Add(e);
            return e;
        }

        /// <summary>Records a structured event that involves factions (Phase 3). Dissolved factions remain valid references.</summary>
        public HistoricalEvent ReportEvent(HistoricalEventType type, EventSeverity severity, EventImportance importance,
            IReadOnlyList<SettlementId> settlementIds, IReadOnlyList<FactionId> factionIds, EventData data = null)
        {
            var e = new HistoricalEvent(new EventId(DeterministicGuid.Next(_eventIdRandom)), Date, type, severity, importance,
                settlementIds, data?.Values, null, factionIds);
            _events.Add(e);
            return e;
        }

        internal void BeginSystem(ISimulationSystem system)
        {
            CurrentSystemId = system.SystemId ?? throw new InvalidOperationException("System ID is null.");
            Random = _randomFactory.Create(SeedMixer.Combine(_daySeed, system.SystemId));
        }
    }
}
