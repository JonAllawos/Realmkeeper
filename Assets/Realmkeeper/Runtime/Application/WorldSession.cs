using System;
using System.Collections.Generic;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Application
{
    public enum WorldChangeReason
    {
        Created = 0,
        Loaded = 1,
        Advanced = 2,
        SettingsChanged = 3,
    }

    public sealed class WorldChange
    {
        internal WorldChange(WorldChangeReason reason, IReadOnlyList<HistoricalEvent> newEvents)
        {
            Reason = reason;
            NewEvents = newEvents ?? Array.Empty<HistoricalEvent>();
        }

        public WorldChangeReason Reason { get; }
        public IReadOnlyList<HistoricalEvent> NewEvents { get; }

        /// <summary>True when geography was replaced and visuals must be rebuilt.</summary>
        public bool GeographyChanged => Reason == WorldChangeReason.Created || Reason == WorldChangeReason.Loaded;
    }

    /// <summary>
    /// Application service that owns the active world. All state changes go through
    /// Execute(command); presentation reads through queries and listens to WorldChanged.
    /// Single-threaded by design: call from one thread (Unity's main thread).
    /// </summary>
    public sealed class WorldSession
    {
        private readonly SimulationScheduler _scheduler;
        private readonly IWorldGenerator _generator;
        private readonly IWorldStore _store;
        private readonly FoodEconomyRules _foodRules;
        private WorldData _world;

        public WorldSession(SimulationScheduler scheduler, IWorldGenerator generator, IWorldStore store, FoodEconomyRules foodRules = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _foodRules = foodRules ?? new FoodEconomyRules();
        }

        public event Action<WorldChange> WorldChanged;

        public bool HasWorld => _world != null;

        /// <summary>Geography for rendering. Read-only by convention; edits go through IRegionMapEditor.</summary>
        public RegionMap Map { get; private set; }

        // ----- Commands ------------------------------------------------------------------

        public CommandResult Execute(CreateWorldCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var settings = command.Settings?.Clone() ?? DefaultSettings();
            try
            {
                var generated = _generator.Generate(new WorldGenerationRequest(command.WorldName, command.Seed, settings));
                Replace(generated.World, generated.Map, WorldChangeReason.Created);
                return CommandResult.Ok($"Created '{generated.World.WorldName}' from seed {command.Seed}.");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is NotSupportedException || ex is WorldGenerationException)
            {
                return CommandResult.Fail("World generation failed.", new[] { ex.Message });
            }
        }

        public AdvanceResult Execute(AdvanceDayCommand command) => Advance(1);

        public AdvanceResult Execute(AdvanceDaysCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            return Advance(command.Days);
        }

        public CommandResult Execute(SaveWorldCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!HasWorld) return CommandResult.Fail("There is no world to save.");
            var result = _store.Save(_world, command.SlotName);
            return result.IsSuccess ? CommandResult.Ok($"Saved to {result.Location}.") : CommandResult.Fail("Save failed.", new[] { result.Error });
        }

        /// <summary>Loads a world. On any failure the current world stays active and unchanged.</summary>
        public CommandResult Execute(LoadWorldCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var load = _store.Load(command.SlotName);
            if (!load.IsSuccess) return CommandResult.Fail(DescribeLoadFailure(load.Status), load.Errors);

            if (!_generator.SupportsVersion(load.World.GeneratorVersion))
            {
                return CommandResult.Fail("Load failed: unsupported generator version.",
                    new[] { $"The save uses generator v{load.World.GeneratorVersion}; this build has v{_generator.GeneratorVersion}." });
            }

            RegionMap map;
            try
            {
                map = _generator.RegenerateRegion(load.World);
            }
            catch (Exception ex)
            {
                return CommandResult.Fail("Load failed: geography could not be rebuilt.", new[] { ex.Message });
            }

            var placement = RegionMapValidator.Validate(map, load.World.Settlements);
            if (!placement.IsValid) return CommandResult.Fail("Load failed: settlements do not match the regenerated region.", placement.Errors);

            Replace(load.World, map, WorldChangeReason.Loaded);
            return CommandResult.Ok($"Loaded '{load.World.WorldName}'.");
        }

        public CommandResult Execute(SetSimulationSpeedCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!HasWorld) return CommandResult.Fail("There is no active world.");
            if (!Enum.IsDefined(typeof(SimulationSpeed), command.Speed)) return CommandResult.Fail("Unknown simulation speed.");
            var next = _world.DeepClone();
            next.WorldSettings.SimulationSpeed = command.Speed;
            _world = next;
            WorldChanged?.Invoke(new WorldChange(WorldChangeReason.SettingsChanged, null));
            return CommandResult.Ok();
        }

        // ----- Queries -------------------------------------------------------------------

        public WorldOverview GetOverview()
        {
            if (!HasWorld) return null;
            return new WorldOverview(_world.WorldId, _world.WorldName, _world.Seed, _world.CurrentSimulationDate,
                _world.WorldSettings.SimulationSpeed, _world.Settlements.Count, _world.HistoricalEvents.Count);
        }

        public IReadOnlyList<SettlementSummary> GetSettlements()
        {
            var list = new List<SettlementSummary>();
            if (!HasWorld) return list;
            foreach (var s in _world.Settlements) list.Add(Summarize(s));
            return list;
        }

        /// <summary>Returns null when no settlement has the given ID.</summary>
        public SettlementSummary FindSettlement(SettlementId id)
        {
            if (!HasWorld) return null;
            var s = _world.FindSettlement(id);
            return s == null ? null : Summarize(s);
        }

        /// <summary>Most recent first.</summary>
        public IReadOnlyList<EventSummary> GetRecentEvents(int maxCount)
        {
            var list = new List<EventSummary>();
            if (!HasWorld || maxCount <= 0) return list;
            for (var i = _world.HistoricalEvents.Count - 1; i >= 0 && list.Count < maxCount; i--)
            {
                var e = _world.HistoricalEvents[i];
                list.Add(new EventSummary(e, _world.FindSettlement(e.SettlementId)?.Name));
            }

            return list;
        }

        // ----- Internals -----------------------------------------------------------------

        public static WorldSettings DefaultSettings()
        {
            var settings = new WorldSettings();
            settings.EnabledSimulationSystems.AddRange(StandardSimulationSystems.AllIds);
            return settings;
        }

        private AdvanceResult Advance(int days)
        {
            if (!HasWorld) return new AdvanceResult(false, "There is no active world.", null, 0, null);
            if (days > SimulationScheduler.MaxDaysPerRun)
                return new AdvanceResult(false, $"Cannot advance more than {SimulationScheduler.MaxDaysPerRun} days at once.", null, 0, null);

            var run = _scheduler.AdvanceDays(_world, days);
            if (run.DaysCompleted > 0)
            {
                _world = run.World;
                WorldChanged?.Invoke(new WorldChange(WorldChangeReason.Advanced, run.Events));
            }

            return run.Completed
                ? new AdvanceResult(true, $"Advanced {days} day(s).", null, days, run.Events)
                : new AdvanceResult(false, $"Simulation stopped after {run.DaysCompleted} of {days} day(s).", run.Errors, run.DaysCompleted, run.Events);
        }

        private void Replace(WorldData world, RegionMap map, WorldChangeReason reason)
        {
            _world = world;
            Map = map;
            WorldChanged?.Invoke(new WorldChange(reason, null));
        }

        private SettlementSummary Summarize(SettlementData s) =>
            new SettlementSummary(s, _world.FindFaction(s.FactionId)?.Name, _foodRules.DailyConsumption(s.Population));

        private static string DescribeLoadFailure(LoadStatus status)
        {
            switch (status)
            {
                case LoadStatus.NotFound: return "No saved world was found in that slot.";
                case LoadStatus.Corrupt: return "The save file is damaged or is not a Realmkeeper world.";
                case LoadStatus.UnsupportedVersion: return "The save file was written by an unsupported version.";
                case LoadStatus.Invalid: return "The save file contains invalid world data.";
                default: return "Load failed.";
            }
        }
    }
}
