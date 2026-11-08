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

        public HistoricalEvent ReportEvent(HistoricalEventType type, EventSeverity severity, SettlementId settlementId, string description)
        {
            var e = new HistoricalEvent(new EventId(DeterministicGuid.Next(_eventIdRandom)), Date, type, severity, settlementId, description);
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
