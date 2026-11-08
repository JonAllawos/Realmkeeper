using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    // Commands are explicit request objects. They give one entry point per state change,
    // which future features (command log, replay, undo, networking) can intercept.

    public sealed class CreateWorldCommand
    {
        public CreateWorldCommand(string worldName, long seed, WorldSettings settings = null)
        {
            WorldName = worldName;
            Seed = seed;
            Settings = settings;
        }

        public string WorldName { get; }
        public long Seed { get; }

        /// <summary>Optional. Defaults to the standard start date with all standard systems enabled.</summary>
        public WorldSettings Settings { get; }
    }

    public sealed class AdvanceDayCommand
    {
    }

    public sealed class AdvanceDaysCommand
    {
        public AdvanceDaysCommand(int days)
        {
            if (days < 1) throw new ArgumentOutOfRangeException(nameof(days), "Days must be at least 1.");
            Days = days;
        }

        public int Days { get; }
    }

    public sealed class SaveWorldCommand
    {
        public SaveWorldCommand(string slotName) { SlotName = slotName; }
        public string SlotName { get; }
    }

    public sealed class LoadWorldCommand
    {
        public LoadWorldCommand(string slotName) { SlotName = slotName; }
        public string SlotName { get; }
    }

    public sealed class SetSimulationSpeedCommand
    {
        public SetSimulationSpeedCommand(SimulationSpeed speed) { Speed = speed; }
        public SimulationSpeed Speed { get; }
    }

    public class CommandResult
    {
        protected CommandResult(bool success, string message, IReadOnlyList<string> errors)
        {
            IsSuccess = success;
            Message = message ?? string.Empty;
            Errors = errors ?? Array.Empty<string>();
        }

        public bool IsSuccess { get; }
        public string Message { get; }
        public IReadOnlyList<string> Errors { get; }

        public static CommandResult Ok(string message = null) => new CommandResult(true, message, null);
        public static CommandResult Fail(string message, IReadOnlyList<string> errors = null) => new CommandResult(false, message, errors);
    }

    public sealed class AdvanceResult : CommandResult
    {
        internal AdvanceResult(bool success, string message, IReadOnlyList<string> errors, int daysAdvanced, IReadOnlyList<HistoricalEvent> events)
            : base(success, message, errors)
        {
            DaysAdvanced = daysAdvanced;
            Events = events ?? Array.Empty<HistoricalEvent>();
        }

        public int DaysAdvanced { get; }
        public IReadOnlyList<HistoricalEvent> Events { get; }
    }
}
