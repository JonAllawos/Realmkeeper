using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Application
{
    /// <summary>Marker for a typed application command: an explicit request with defined inputs.</summary>
    public interface ICommand
    {
    }

    /// <summary>Handles one command type. Handlers validate first, then change state only through <see cref="CommandContext.Edit"/>.</summary>
    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        CommandResult Handle(TCommand command, CommandContext context);
    }

    /// <summary>
    /// Typed registry: one handler per command type, resolved by type. No strings, no reflection
    /// over domain objects. Modules (for example Exploration.Application) register their handlers
    /// at composition time.
    /// </summary>
    public sealed class CommandRegistry
    {
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();

        public void Register<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handlers.ContainsKey(typeof(TCommand)))
                throw new InvalidOperationException($"A handler for {typeof(TCommand).Name} is already registered.");
            _handlers.Add(typeof(TCommand), handler);
        }

        public bool IsRegistered<TCommand>() where TCommand : ICommand => _handlers.ContainsKey(typeof(TCommand));

        public IReadOnlyCollection<Type> RegisteredCommandTypes => _handlers.Keys;

        internal ICommandHandler<TCommand> Resolve<TCommand>() where TCommand : ICommand =>
            _handlers.TryGetValue(typeof(TCommand), out var handler) ? (ICommandHandler<TCommand>)handler : null;
    }

    /// <summary>
    /// What a handler may use while executing. Reads come from the committed world. The first
    /// call to <see cref="Edit"/> makes a working copy; the session validates and commits it only
    /// if the handler succeeds (ADR-0008 for commands). Events reported here are appended on commit.
    /// </summary>
    public sealed class CommandContext
    {
        private readonly IRandomSourceFactory _random;
        private readonly List<HistoricalEvent> _events = new List<HistoricalEvent>();
        private WorldData _working;

        internal CommandContext(WorldSession session, WorldData committed, IRandomSourceFactory random)
        {
            Session = session;
            Committed = committed;
            _random = random;
        }

        public WorldSession Session { get; }

        /// <summary>The committed world (null when the session has no world). Treat as read-only.</summary>
        public WorldData Committed { get; }

        public bool HasWorld => Committed != null;

        public RegionMap Map => Session.Map;

        public EconomyRules Rules => Session.Rules;

        public bool HasEdits => _working != null;

        internal WorldData Working => _working;

        internal IReadOnlyList<HistoricalEvent> Events => _events;

        /// <summary>The working copy to change. Created on first use; discarded unless the command succeeds and validates.</summary>
        public WorldData Edit()
        {
            if (Committed == null) throw new InvalidOperationException("There is no world to edit.");
            return _working ?? (_working = Committed.DeepClone());
        }

        private int _idCount;

        /// <summary>A deterministic new ID for an entity this command creates (world seed, date, event count, purpose, call order).</summary>
        public Guid NextGuid(string purpose)
        {
            var world = Edit();
            var seed = SeedMixer.Combine(SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), (ulong)world.CurrentSimulationDate.ToDayNumber()),
                "command-id:" + purpose + ":" + world.HistoricalEvents.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + (_idCount++).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return DeterministicGuid.Next(_random.Create(seed));
        }

        /// <summary>Records a structured chronicle event with a deterministic ID (world seed, date, event count).</summary>
        public HistoricalEvent ReportEvent(HistoricalEventType type, EventSeverity severity, EventImportance importance,
            IReadOnlyList<SettlementId> settlementIds, IEnumerable<KeyValuePair<string, string>> data,
            IReadOnlyList<FactionId> factionIds = null, IReadOnlyList<NpcId> npcIds = null)
        {
            var world = Edit();
            var seed = SeedMixer.Combine(SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), (ulong)world.CurrentSimulationDate.ToDayNumber()),
                "command-event:" + (world.HistoricalEvents.Count + _events.Count).ToString(System.Globalization.CultureInfo.InvariantCulture));
            var e = new HistoricalEvent(new EventId(DeterministicGuid.Next(_random.Create(seed))), world.CurrentSimulationDate, type, severity,
                importance, settlementIds, data, null, factionIds, npcIds);
            _events.Add(e);
            return e;
        }
    }
}
