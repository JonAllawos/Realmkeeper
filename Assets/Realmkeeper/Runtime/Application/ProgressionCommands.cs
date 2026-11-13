using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Awards experience to the player (Phase 7). Quests, encounters, discoveries and other systems call it with a
    /// source and, ideally, the ID of the thing rewarded: the award's key is "{source}:{sourceId}" (or
    /// <see cref="AwardKey"/>), and a key can be awarded only once, so the same quest or discovery never pays twice.
    /// Without a source ID each award gets a fresh key. Awarding never levels the character up; it makes them
    /// eligible.
    /// </summary>
    public sealed class AwardExperienceCommand : ICommand
    {
        public AwardExperienceCommand(int amount, ExperienceSource source, string sourceId = null, string reason = null, string awardKey = null)
        {
            Amount = amount;
            Source = source;
            SourceId = sourceId;
            Reason = reason;
            AwardKey = awardKey;
        }

        public int Amount { get; }
        public ExperienceSource Source { get; }
        public string SourceId { get; }
        public string Reason { get; }
        public string AwardKey { get; }
    }

    /// <summary>Applies one or more level-ups to the player in one transaction, each with its choices (Phase 7).</summary>
    public sealed class AdvanceCharacterCommand : ICommand
    {
        public AdvanceCharacterCommand(params LevelChoices[] levels) { Levels = levels ?? Array.Empty<LevelChoices>(); }
        public IReadOnlyList<LevelChoices> Levels { get; }
    }

    /// <summary>
    /// Phase 22 (ADR-0109): keeps the player's progress on the next level-up — the picks made so far — on the sheet, so it
    /// survives closing the window and saving. Nothing in it applies; null or an empty draft clears it. Refused when the
    /// picks are not choices of that level, exceed a choice's count, or name an option that does not exist.
    /// </summary>
    public sealed class SaveAdvancementDraftCommand : ICommand
    {
        public SaveAdvancementDraftCommand(LevelChoices draft) { Draft = draft; }
        public LevelChoices Draft { get; }
    }

    /// <summary>
    /// Phase 22 (ADR-0109): the player has been shown that a new level is available. Records the highest level they can now
    /// take, so the notice is shown once per level, and writes it to the chronicle. Refused when there is nothing new to tell.
    /// </summary>
    public sealed class AcknowledgeLevelUpCommand : ICommand
    {
    }

    /// <summary>Spends uses of one of the player's resources (Phase 7; combat, magic and other systems will call it).</summary>
    public sealed class SpendResourceCommand : ICommand
    {
        public SpendResourceCommand(string resourceId, int amount = 1)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        public string ResourceId { get; }
        public int Amount { get; }
    }

    /// <summary>Restores uses of one of the player's resources, up to its maximum (null amount = all).</summary>
    public sealed class RestoreResourceCommand : ICommand
    {
        public RestoreResourceCommand(string resourceId, int? amount = null)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        public string ResourceId { get; }
        public int? Amount { get; }
    }

    /// <summary>Restores what a short or long rest restores (the hook for the rest phase; it passes no time).</summary>
    public sealed class RecoverResourcesCommand : ICommand
    {
        public RecoverResourcesCommand(ResourceRecovery rest) { Rest = rest; }
        public ResourceRecovery Rest { get; }
    }

    /// <summary>Damages (negative) or heals (positive) the player, or grants temporary hit points.</summary>
    public sealed class ChangeHitPointsCommand : ICommand
    {
        public ChangeHitPointsCommand(int change, bool temporary = false)
        {
            Change = change;
            Temporary = temporary;
        }

        public int Change { get; }
        public bool Temporary { get; }
    }

    public static class ProgressionCommandHandlers
    {
        public static void Register(CommandRegistry commands)
        {
            commands.Register<AwardExperienceCommand>(new AwardHandler());
            commands.Register<AdvanceCharacterCommand>(new AdvanceHandler());
            commands.Register<SaveAdvancementDraftCommand>(new DraftHandler());
            commands.Register<AcknowledgeLevelUpCommand>(new AcknowledgeHandler());
            commands.Register<SpendResourceCommand>(new SpendHandler());
            commands.Register<RestoreResourceCommand>(new RestoreHandler());
            commands.Register<RecoverResourcesCommand>(new RecoverHandler());
            commands.Register<ChangeHitPointsCommand>(new HitPointsHandler());
        }

        /// <summary>The key an award is recorded under.</summary>
        public static string KeyFor(AwardExperienceCommand c) =>
            !string.IsNullOrWhiteSpace(c.AwardKey) ? c.AwardKey.Trim() : !string.IsNullOrWhiteSpace(c.SourceId) ? c.Source.ToString().ToLowerInvariant() + ":" + c.SourceId.Trim() : null;

        private static CommandResult Player(CommandContext context, out PlayerCharacterData player)
        {
            player = null;
            if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
            player = context.Committed.Player;
            if (player?.Rpg == null) return CommandResult.NotFound("This world has no player character.");
            return null;
        }

        private sealed class AwardHandler : ICommandHandler<AwardExperienceCommand>
        {
            public CommandResult Handle(AwardExperienceCommand command, CommandContext context)
            {
                var refused = Player(context, out var current);
                if (refused != null) return refused;
                // Phase 22 (owner decision D4): experience is earned by a created character only, so every new character
                // starts at exactly 0. Experience an older save already holds from before creation is kept as it was.
                if (current.Rpg.State != CreationState.Complete)
                    return CommandResult.InvalidState("Experience is earned once your character is created.");
                if (command.Amount < 1 || command.Amount > ProgressionRules.MaxAward)
                    return CommandResult.Invalid($"An award is 1–{ProgressionRules.MaxAward} experience.", new[] { $"Amount {command.Amount}." });
                if (!Enum.IsDefined(typeof(ExperienceSource), command.Source)) return CommandResult.Invalid("Unknown experience source.");
                var key = KeyFor(command);
                foreach (var a in current.Rpg.ExperienceLog)
                    if (key != null && a.Key == key) return CommandResult.InvalidState($"That award ({key}) was already given; it is not given twice.");
                if ((long)current.Rpg.Experience + command.Amount > ProgressionRules.MaxExperience)
                    return CommandResult.Invalid($"Experience cannot pass {ProgressionRules.MaxExperience}.");

                var edit = context.Edit();
                var s = edit.Player.Rpg;
                var award = new ExperienceAward
                {
                    Key = key ?? NextLedgerKey(s),
                    Source = command.Source,
                    SourceId = command.SourceId,
                    Amount = command.Amount,
                    Date = edit.CurrentSimulationDate,
                    Reason = (command.Reason ?? string.Empty).Trim(),
                };
                s.ExperienceLog.Add(award);
                s.Experience += command.Amount;
                var available = CharacterAdvancement.LevelsAvailable(s);
                return CommandResult.Ok($"+{command.Amount.ToString(CultureInfo.InvariantCulture)} experience" + (award.Reason.Length > 0 ? $" ({award.Reason})" : string.Empty) +
                                        (available > 0 ? $". Ready to advance to level {s.Level + 1}." : "."));
            }
        }

        /// <summary>A deterministic key for an award without a source ID: the next free "award-{n}" in the ledger.</summary>
        private static string NextLedgerKey(CharacterSheetData s)
        {
            var n = s.ExperienceLog.Count + 1;
            while (s.ExperienceLog.Exists(a => a.Key == "award-" + n.ToString(CultureInfo.InvariantCulture))) n++;
            return "award-" + n.ToString(CultureInfo.InvariantCulture);
        }

        private sealed class AdvanceHandler : ICommandHandler<AdvanceCharacterCommand>
        {
            public CommandResult Handle(AdvanceCharacterCommand command, CommandContext context)
            {
                var refused = Player(context, out var current);
                if (refused != null) return refused;
                if (current.Rpg.State != CreationState.Complete) return CommandResult.InvalidState("Create your character before advancing.");
                if (!CharacterAdvancement.CanAdvance(current.Rpg))
                    return CommandResult.InvalidState(current.Rpg.Level >= AbilityRules.MaxLevel
                        ? "Level 20 is the highest level."
                        : $"Level {current.Rpg.Level + 1} needs {ProgressionRules.ExperienceFor(current.Rpg.Level + 1)} experience (you have {current.Rpg.Experience}).");

                var edit = context.Edit();
                var advanced = CharacterAdvancement.Apply(current.Rpg, command.Levels, edit.CurrentSimulationDate, out var errors);
                if (advanced == null) return CommandResult.Invalid("The advancement is not valid.", errors);
                var p = edit.Player;
                var from = p.Rpg.Level;
                p.Rpg = advanced;
                var rules = new CharacterRules(advanced);
                var facts = new Dictionary<string, string>
                {
                    ["source"] = "player",
                    ["player"] = p.Name,
                    ["class"] = rules.Class.Name,
                    ["from"] = from.ToString(CultureInfo.InvariantCulture),
                    ["to"] = advanced.Level.ToString(CultureInfo.InvariantCulture),
                    ["subclass"] = rules.Subclass?.Name ?? string.Empty,
                };
                var where = p.IsInSettlement ? new[] { p.SettlementId } : Array.Empty<SettlementId>();
                context.ReportEvent(HistoricalEventType.PlayerLevelAdvanced, EventSeverity.Info, EventImportance.Minor, where, facts);
                var last = advanced.Advancements[advanced.Advancements.Count - 1];
                return CommandResult.Ok($"{p.Name} is now a level-{advanced.Level} {rules.Class.Name.ToLowerInvariant()}. {last.Summary}.");
            }
        }

        /// <summary>The highest level the player's experience allows now (their level when none is available).</summary>
        public static int EligibleLevel(CharacterSheetData s) => s == null ? 0 : s.Level + CharacterAdvancement.LevelsAvailable(s);

        /// <summary>Phase 22: true when a level is available that the player has not yet been told about.</summary>
        public static bool LevelUpUnannounced(CharacterSheetData s) =>
            s != null && s.State == CreationState.Complete && !s.IsDead && CharacterAdvancement.LevelsAvailable(s) > 0 && EligibleLevel(s) > Math.Max(s.LevelUpNoticed, s.Level);

        private sealed class DraftHandler : ICommandHandler<SaveAdvancementDraftCommand>
        {
            public CommandResult Handle(SaveAdvancementDraftCommand command, CommandContext context)
            {
                var refused = Player(context, out var current);
                if (refused != null) return refused;
                if (current.Rpg.State != CreationState.Complete) return CommandResult.InvalidState("Create your character before advancing.");
                var draft = command.Draft;
                var empty = draft == null || (draft.SubclassId == null && !draft.Choices.Values.Any(v => v != null && v.Count > 0));
                if (empty)
                {
                    if (current.Rpg.PendingAdvancement == null) return CommandResult.Ok("Nothing to keep.");
                    context.Edit().Player.Rpg.PendingAdvancement = null;
                    return CommandResult.Ok("The level-up in progress was cleared.");
                }

                if (!CharacterAdvancement.CanAdvance(current.Rpg)) return CommandResult.InvalidState($"Level {current.Rpg.Level + 1} needs {ProgressionRules.ExperienceFor(current.Rpg.Level + 1)} experience.");
                var clean = new LevelChoices { Level = draft.Level, SubclassId = draft.SubclassId };
                foreach (var pair in draft.Choices) if (pair.Value != null && pair.Value.Count > 0) clean.Choices[pair.Key] = new List<string>(pair.Value);
                var problems = CharacterAdvancement.DraftProblems(current.Rpg, clean);
                if (problems.Count > 0) return CommandResult.Invalid("Those picks cannot be kept.", problems);
                context.Edit().Player.Rpg.PendingAdvancement = clean;
                var open = CharacterAdvancement.OpenChoices(current.Rpg, clean);
                return CommandResult.Ok(open == 0 ? $"Your level {clean.Level} choices are all made; confirm them to rise." : $"Kept: {open} choice{(open == 1 ? string.Empty : "s")} still to make for level {clean.Level}.");
            }
        }

        private sealed class AcknowledgeHandler : ICommandHandler<AcknowledgeLevelUpCommand>
        {
            public CommandResult Handle(AcknowledgeLevelUpCommand command, CommandContext context)
            {
                var refused = Player(context, out var current);
                if (refused != null) return refused;
                if (!LevelUpUnannounced(current.Rpg)) return CommandResult.InvalidState("No new level to announce.");
                var edit = context.Edit();
                var p = edit.Player;
                var to = EligibleLevel(p.Rpg);
                p.Rpg.LevelUpNoticed = to;
                var facts = new Dictionary<string, string>
                {
                    ["source"] = "player",
                    ["player"] = p.Name,
                    ["from"] = p.Rpg.Level.ToString(CultureInfo.InvariantCulture),
                    ["to"] = to.ToString(CultureInfo.InvariantCulture),
                };
                var where = p.IsInSettlement ? new[] { p.SettlementId } : Array.Empty<SettlementId>();
                context.ReportEvent(HistoricalEventType.PlayerLevelReady, EventSeverity.Positive, EventImportance.Minor, where, facts);
                return CommandResult.Ok($"Level {to} is yours to take.");
            }
        }

        private sealed class SpendHandler : ICommandHandler<SpendResourceCommand>
        {
            public CommandResult Handle(SpendResourceCommand command, CommandContext context)
            {
                var refused = Player(context, out var current);
                if (refused != null) return refused;
                var check = current.Rpg.Clone();
                var problem = CharacterVitals.Spend(check, command.ResourceId, command.Amount);
                if (problem != null) return CommandResult.Invalid(problem);
                var edit = context.Edit();
                CharacterVitals.Spend(edit.Player.Rpg, command.ResourceId, command.Amount);
                return CommandResult.Ok($"Spent {command.Amount}; {edit.Player.Rpg.Resources[command.ResourceId]} left.");
            }
        }

        private sealed class RestoreHandler : ICommandHandler<RestoreResourceCommand>
        {
            public CommandResult Handle(RestoreResourceCommand command, CommandContext context)
            {
                var refused = Player(context, out var current);
                if (refused != null) return refused;
                var problem = CharacterVitals.Restore(current.Rpg.Clone(), command.ResourceId, command.Amount);
                if (problem != null) return CommandResult.Invalid(problem);
                var edit = context.Edit();
                CharacterVitals.Restore(edit.Player.Rpg, command.ResourceId, command.Amount);
                return CommandResult.Ok($"{edit.Player.Rpg.Resources[command.ResourceId]} now.");
            }
        }

        private sealed class RecoverHandler : ICommandHandler<RecoverResourcesCommand>
        {
            public CommandResult Handle(RecoverResourcesCommand command, CommandContext context)
            {
                var refused = Player(context, out var current);
                if (refused != null) return refused;
                if (current.Rpg.State != CreationState.Complete) return CommandResult.InvalidState("Create your character first.");
                var edit = context.Edit();
                var changes = CharacterVitals.Recover(edit.Player.Rpg, command.Rest);
                return CommandResult.Ok(changes.Count == 0 ? "Nothing to recover." : "Recovered: " + string.Join(", ", changes) + ".");
            }
        }

        private sealed class HitPointsHandler : ICommandHandler<ChangeHitPointsCommand>
        {
            public CommandResult Handle(ChangeHitPointsCommand command, CommandContext context)
            {
                var refused = Player(context, out var current);
                if (refused != null) return refused;
                if (current.Rpg.State != CreationState.Complete) return CommandResult.InvalidState("Create your character first.");
                var check = current.Rpg.Clone();
                var problem = command.Temporary ? CharacterVitals.GrantTemporaryHitPoints(check, command.Change) : CharacterVitals.ChangeHitPoints(check, command.Change);
                if (problem != null) return CommandResult.Invalid(problem);
                var edit = context.Edit();
                var s = edit.Player.Rpg;
                if (command.Temporary) CharacterVitals.GrantTemporaryHitPoints(s, command.Change);
                else CharacterVitals.ChangeHitPoints(s, command.Change);
                return CommandResult.Ok($"Hit points {s.CurrentHp}" + (s.TemporaryHp > 0 ? $" (+{s.TemporaryHp} temporary)" : string.Empty) + ".");
            }
        }
    }
}
