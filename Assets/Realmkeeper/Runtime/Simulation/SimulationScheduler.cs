using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation
{
    public sealed class SimulationStepResult
    {
        private SimulationStepResult(bool committed, WorldData world, WorldDate date, IReadOnlyList<HistoricalEvent> events, IReadOnlyList<string> errors)
        {
            Committed = committed;
            World = world;
            Date = date;
            Events = events;
            Errors = errors;
        }

        /// <summary>True if the day validated and <see cref="World"/> is the new state.</summary>
        public bool Committed { get; }

        /// <summary>The new world when committed; the untouched input world otherwise.</summary>
        public WorldData World { get; }

        public WorldDate Date { get; }
        public IReadOnlyList<HistoricalEvent> Events { get; }
        public IReadOnlyList<string> Errors { get; }

        internal static SimulationStepResult Success(WorldData world, IReadOnlyList<HistoricalEvent> events) =>
            new SimulationStepResult(true, world, world.CurrentSimulationDate, events, Array.Empty<string>());

        internal static SimulationStepResult Failure(WorldData original, IReadOnlyList<string> errors) =>
            new SimulationStepResult(false, original, original?.CurrentSimulationDate ?? default, Array.Empty<HistoricalEvent>(), errors);
    }

    public sealed class SimulationRunResult
    {
        internal SimulationRunResult(int requested, int completed, WorldData world, IReadOnlyList<HistoricalEvent> events, IReadOnlyList<string> errors)
        {
            DaysRequested = requested;
            DaysCompleted = completed;
            World = world;
            Events = events;
            Errors = errors;
        }

        public int DaysRequested { get; }
        public int DaysCompleted { get; }
        public bool Completed => DaysCompleted == DaysRequested;

        /// <summary>State after the last committed day.</summary>
        public WorldData World { get; }

        public IReadOnlyList<HistoricalEvent> Events { get; }
        public IReadOnlyList<string> Errors { get; }
    }

    /// <summary>
    /// Daily scheduler. Each day: validate input, deep-copy, run enabled systems in
    /// ascending <see cref="ISimulationSystem.ExecutionOrder"/>, append reported events,
    /// validate, and only then commit. The input world is never mutated.
    /// </summary>
    public sealed class SimulationScheduler
    {
        public const int MaxDaysPerRun = 3600;

        private readonly IRandomSourceFactory _randomFactory;
        private readonly List<ISimulationSystem> _systems = new List<ISimulationSystem>();

        public SimulationScheduler(IRandomSourceFactory randomFactory)
        {
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
        }

        /// <summary>Registered systems in execution order.</summary>
        public IReadOnlyList<ISimulationSystem> Systems => _systems;

        public void Register(ISimulationSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (string.IsNullOrWhiteSpace(system.SystemId)) throw new ArgumentException("System ID is required.", nameof(system));

            foreach (var existing in _systems)
            {
                if (string.Equals(existing.SystemId, system.SystemId, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Simulation system '{system.SystemId}' is already registered.");
                if (existing.ExecutionOrder == system.ExecutionOrder)
                    throw new InvalidOperationException(
                        $"Systems '{existing.SystemId}' and '{system.SystemId}' share execution order {system.ExecutionOrder}; order must be explicit.");
            }

            var index = 0;
            while (index < _systems.Count && _systems[index].ExecutionOrder < system.ExecutionOrder) index++;
            _systems.Insert(index, system);
        }

        public SimulationStepResult AdvanceDay(WorldData world)
        {
            if (world == null) return SimulationStepResult.Failure(null, new[] { "World is null." });

            var inputValidation = WorldValidator.Validate(world);
            if (!inputValidation.IsValid)
            {
                return SimulationStepResult.Failure(world, Prefix("Input world is invalid: ", inputValidation.Errors));
            }

            WorldData working;
            SimulationDayContext context;
            try
            {
                working = world.DeepClone();
                working.CurrentSimulationDate = world.CurrentSimulationDate.AddDays(1);
                context = new SimulationDayContext(working, working.CurrentSimulationDate, _randomFactory);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is OverflowException || ex is InvalidOperationException)
            {
                return SimulationStepResult.Failure(world, new[] { "Cannot advance date: " + ex.Message });
            }

            var enabled = world.WorldSettings.EnabledSimulationSystems;
            foreach (var system in _systems)
            {
                if (!enabled.Contains(system.SystemId)) continue;

                try
                {
                    context.BeginSystem(system);
                    system.Execute(context);
                }
                catch (Exception ex)
                {
                    // Systems are isolated: one failure rejects the whole day, nothing is committed.
                    return SimulationStepResult.Failure(world, new[] { $"System '{system.SystemId}' failed on {context.Date.ToIsoString()}: {ex.Message}" });
                }
            }

            working.HistoricalEvents.AddRange(context.Events);

            var outputValidation = WorldValidator.Validate(working);
            if (!outputValidation.IsValid)
            {
                return SimulationStepResult.Failure(world, Prefix($"Day {context.Date.ToIsoString()} produced invalid state: ", outputValidation.Errors));
            }

            return SimulationStepResult.Success(working, context.Events);
        }

        /// <summary>
        /// Advances day by day. Each day commits atomically; on the first failure the run stops
        /// and returns the state after the last successful day.
        /// </summary>
        public SimulationRunResult AdvanceDays(WorldData world, int days)
        {
            if (days < 1 || days > MaxDaysPerRun)
                throw new ArgumentOutOfRangeException(nameof(days), $"Days must be between 1 and {MaxDaysPerRun}.");

            var events = new List<HistoricalEvent>();
            var current = world;
            for (var i = 0; i < days; i++)
            {
                var step = AdvanceDay(current);
                if (!step.Committed)
                {
                    return new SimulationRunResult(days, i, current, events, step.Errors);
                }

                current = step.World;
                events.AddRange(step.Events);
            }

            return new SimulationRunResult(days, days, current, events, Array.Empty<string>());
        }

        private static string[] Prefix(string prefix, IReadOnlyList<string> errors)
        {
            var result = new string[errors.Count];
            for (var i = 0; i < errors.Count; i++) result[i] = prefix + errors[i];
            return result;
        }
    }
}
