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

    /// <summary>Saves the active world session. Dispatched through the command registry.</summary>
    public sealed class SaveWorldCommand : ICommand
    {
        public SaveWorldCommand(string slotName) { SlotName = slotName; }
        public string SlotName { get; }
    }

    public sealed class LoadWorldCommand
    {
        public LoadWorldCommand(string slotName) { SlotName = slotName; }
        public string SlotName { get; }
    }

    public sealed class SetSimulationSpeedCommand : ICommand
    {
        public SetSimulationSpeedCommand(SimulationSpeed speed) { Speed = speed; }
        public SimulationSpeed Speed { get; }
    }

    /// <summary>Outcome category of a command. Every failure says which kind it is.</summary>
    public enum CommandStatus
    {
        Success = 0,

        /// <summary>The command's inputs, or the state it would produce, are invalid. Nothing changed.</summary>
        ValidationFailed = 1,

        /// <summary>A referenced world, region, settlement, character or person does not exist.</summary>
        NotFound = 2,

        /// <summary>The session is not in a state that allows the command (no world, wrong location, closed session).</summary>
        InvalidSessionState = 3,

        /// <summary>No handler is registered for the command type.</summary>
        Unsupported = 4,

        /// <summary>The handler failed unexpectedly. Nothing changed.</summary>
        Failed = 5,
    }

    public class CommandResult
    {
        protected CommandResult(CommandStatus status, string message, IReadOnlyList<string> errors)
        {
            Status = status;
            Message = message ?? string.Empty;
            Errors = errors ?? Array.Empty<string>();
        }

        protected CommandResult(bool success, string message, IReadOnlyList<string> errors)
            : this(success ? CommandStatus.Success : CommandStatus.Failed, message, errors)
        {
        }

        public CommandStatus Status { get; }
        public bool IsSuccess => Status == CommandStatus.Success;
        public string Message { get; }
        public IReadOnlyList<string> Errors { get; }

        public static CommandResult Ok(string message = null) => new CommandResult(CommandStatus.Success, message, null);
        public static CommandResult Fail(string message, IReadOnlyList<string> errors = null) => new CommandResult(CommandStatus.Failed, message, errors);
        public static CommandResult Invalid(string message, IReadOnlyList<string> errors = null) => new CommandResult(CommandStatus.ValidationFailed, message, errors);
        public static CommandResult NotFound(string message) => new CommandResult(CommandStatus.NotFound, message, null);
        public static CommandResult InvalidState(string message) => new CommandResult(CommandStatus.InvalidSessionState, message, null);
        public static CommandResult Unsupported(string message) => new CommandResult(CommandStatus.Unsupported, message, null);

        public override string ToString() => Errors.Count == 0 ? $"{Status}: {Message}" : $"{Status}: {Message} ({string.Join("; ", Errors)})";
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
