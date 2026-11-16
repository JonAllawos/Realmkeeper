using System;
using System.Globalization;
using System.Text;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>
    /// M1.1: adds active play time to the world's counter (<see cref="WorldData.PlaytimeSeconds"/>). The presentation
    /// counts the seconds and records them just before each save, so the counter never changes every frame.
    /// </summary>
    public sealed class RecordPlaytimeCommand : ICommand
    {
        /// <summary>The most one record may add (a week): anything larger is a bug, not play.</summary>
        public const double MaxSeconds = 7 * 24 * 3600;

        public RecordPlaytimeCommand(double seconds) { Seconds = seconds; }

        public double Seconds { get; }
    }

    internal sealed class RecordPlaytimeHandler : ICommandHandler<RecordPlaytimeCommand>
    {
        public CommandResult Handle(RecordPlaytimeCommand command, CommandContext context)
        {
            if (!context.HasWorld) return CommandResult.InvalidState("There is no world.");
            var s = command.Seconds;
            if (double.IsNaN(s) || double.IsInfinity(s) || s <= 0 || s > RecordPlaytimeCommand.MaxSeconds)
                return CommandResult.Invalid("Play time to record must be a positive number of seconds (at most a week).");
            context.Edit().PlaytimeSeconds += s;
            return CommandResult.Ok();
        }
    }

    public sealed partial class WorldSession
    {
        /// <summary>M1.1: true while a command is being applied (an autosave never runs then).</summary>
        public bool IsApplyingCommand => _dispatching;

        /// <summary>M1.1: seconds of active play recorded in the world (0 for saves from before v28).</summary>
        public double GetPlaytimeSeconds() => HasWorld ? _world.PlaytimeSeconds : 0;

        /// <summary>M1.1: true while a fight is going on (an encounter or a fight on the road).</summary>
        public bool IsInFight() => HasWorld && (_world.ActiveEncounter != null || GetRoadFight() != null);

        /// <summary>M1.1: true while a conversation is open.</summary>
        public bool IsInConversation() => HasWorld && _world.Conversation != null;

        /// <summary>
        /// M1.1 (ADR-0123): why the player may not save now, or null. Saving is refused during a fight and while a
        /// conversation is open, so a save never captures either half-way.
        /// </summary>
        public string GetSaveBlocker()
        {
            if (!HasWorld) return SaveBlockerFor(null);
            if (GetRoadFight() != null) return FightBlocker;
            return SaveBlockerFor(_world);
        }

        public const string FightBlocker = "You cannot save during a fight.";
        public const string ConversationBlocker = "You cannot save while a conversation is open.";

        /// <summary>M1.1: the save rule on a world (an encounter going on, or a conversation open), or null.</summary>
        public static string SaveBlockerFor(WorldData world)
        {
            if (world == null) return "There is no world to save.";
            if (world.ActiveEncounter != null) return FightBlocker;
            if (world.Conversation != null) return ConversationBlocker;
            return null;
        }

        /// <summary>
        /// M1.1: a fingerprint of campaign progress (each campaign's state, chapters and decisions). It changes exactly when
        /// a decision or a chapter change is recorded, which is when an autosave is wanted.
        /// </summary>
        public string GetCampaignProgressSignature()
        {
            if (!HasWorld || _world.Campaigns == null || _world.Campaigns.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var c in _world.Campaigns)
            {
                if (c == null) continue;
                sb.Append(c.CampaignId).Append(':').Append(c.State.ToString()).Append(':').Append((c.Decisions?.Count ?? 0).ToString(CultureInfo.InvariantCulture));
                if (c.Chapters != null) foreach (var ch in c.Chapters) if (ch != null) sb.Append('|').Append(ch.Id).Append('=').Append(ch.State.ToString());
                sb.Append(';');
            }

            return sb.ToString();
        }
    }
}
