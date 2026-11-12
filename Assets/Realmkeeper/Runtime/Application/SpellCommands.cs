using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>Phase 9: replace the prepared list (prepared casters and the wizard), within the class's rules and the preparation window.</summary>
    public sealed class PrepareSpellsCommand : ICommand
    {
        public PrepareSpellsCommand(IEnumerable<string> prepared) { Prepared = new List<string>(prepared ?? Array.Empty<string>()); }
        public IReadOnlyList<string> Prepared { get; }
    }

    /// <summary>Phase 9: make one open spell pick (a cantrip, a known spell, a spellbook entry) left open at creation, level-up or migration.</summary>
    public sealed class ChooseSpellCommand : ICommand
    {
        public ChooseSpellCommand(string choiceId, string spellId)
        {
            ChoiceId = choiceId;
            SpellId = spellId;
        }

        public string ChoiceId { get; }
        public string SpellId { get; }
    }

    /// <summary>Phase 9: cast a healing spell on yourself outside a fight (instantaneous; the clock does not move).</summary>
    public sealed class CastHealingOutsideCombatCommand : ICommand
    {
        public CastHealingOutsideCombatCommand(string spellId, int slotLevel)
        {
            SpellId = spellId;
            SlotLevel = slotLevel;
        }

        public string SpellId { get; }
        public int SlotLevel { get; }
    }

    /// <summary>Phase 9: the recovery half of a rest, after the clock has moved (dispatched by <see cref="WorldSession.ShortRest"/> and <see cref="WorldSession.LongRest"/>).</summary>
    public sealed class FinishRestCommand : ICommand
    {
        public FinishRestCommand(RestKind kind, int hitDice, string key)
        {
            Kind = kind;
            HitDice = hitDice;
            Key = key;
        }

        public RestKind Kind { get; }
        public int HitDice { get; }
        public string Key { get; }
    }

    /// <summary>A rest's outcome: whether it happened, the time it took, and what it gave back.</summary>
    public sealed class RestResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public IReadOnlyList<string> Lines { get; set; } = Array.Empty<string>();
        public int MinutesPassed { get; set; }
    }

    public static class SpellCommandHandlers
    {
        public const string NotInFights = "Not during a fight.";

        public static void Register(CommandRegistry commands)
        {
            commands.Register<PrepareSpellsCommand>(new PrepareHandler());
            commands.Register<ChooseSpellCommand>(new ChooseHandler());
            commands.Register<CastHealingOutsideCombatCommand>(new HealOutsideHandler());
            commands.Register<FinishRestCommand>(new FinishRestHandler());
        }

        private static CommandResult Ready(CommandContext context, out PlayerCharacterData player)
        {
            player = null;
            if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
            player = context.Committed.Player;
            if (player?.Rpg == null || player.Rpg.State != CreationState.Complete) return CommandResult.InvalidState("Create your character first.");
            if (context.Committed.ActiveEncounter != null) return CommandResult.InvalidState(NotInFights);
            return null;
        }

        /// <summary>Deterministic dice for an operation (rest or out-of-fight cast), from the world seed and a key.</summary>
        internal static Func<int, int> Dice(WorldData world, string key)
        {
            var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "dice:" + key);
            var n = 0UL;
            return sides => sides <= 1 ? 1 : (int)(SeedMixer.Mix(SeedMixer.Combine(seed, ++n)) % (ulong)sides) + 1;
        }

        internal static long Now(WorldData w) => w.CurrentSimulationDate.ToDayNumber() * WorldTime.MinutesPerDay + w.MinuteOfDay;

        private sealed class PrepareHandler : ICommandHandler<PrepareSpellsCommand>
        {
            public CommandResult Handle(PrepareSpellsCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                var access = new SpellAccess(new CharacterRules(player.Rpg));
                var problem = access.PreparationProblem(command.Prepared);
                if (problem != null) return CommandResult.Invalid(problem, new[] { problem });
                var removed = 0;
                foreach (var id in player.Rpg.Spellcasting.Prepared) if (!Contains(command.Prepared, id)) removed++;
                var sheet = context.Edit().Player.Rpg;
                sheet.Spellcasting.Prepared = new List<string>(command.Prepared);
                if (removed > 0 && sheet.Spellcasting.PreparationChanges > 0) sheet.Spellcasting.PreparationChanges = Math.Max(0, sheet.Spellcasting.PreparationChanges - removed);
                return CommandResult.Ok($"{command.Prepared.Count} of {access.PreparedLimit} spells prepared.");
            }
        }

        private sealed class ChooseHandler : ICommandHandler<ChooseSpellCommand>
        {
            public CommandResult Handle(ChooseSpellCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                var slot = OpenPick(player.Rpg, command.ChoiceId);
                if (slot == null) return CommandResult.Invalid("That is not an open spell pick.");
                var at = slot.Value;
                var next = player.Rpg.Clone();
                if (!next.Choices.TryGetValue(command.ChoiceId, out var list)) next.Choices[command.ChoiceId] = list = new List<string>();
                if (list.Contains(command.SpellId)) return CommandResult.Invalid("Already chosen.");
                list.Add(command.SpellId);
                var problems = CharacterValidation.PickProblems(next, at.Level);
                if (problems.TryGetValue(command.ChoiceId, out var p) && p.Count > 0) return CommandResult.Invalid(p[0], p);
                var errors = CharacterValidation.CheckSheet(next);
                if (errors.Count > 0) return CommandResult.Invalid(errors[0], errors);
                context.Edit().Player.Rpg = next;
                return CommandResult.Ok($"{SpellCatalog.Find(command.SpellId)?.Name ?? command.SpellId} added.");
            }
        }

        /// <summary>The open "up to" spell pick with this ID at the character's level, or null.</summary>
        public static ChoiceSlot? OpenPick(CharacterSheetData sheet, string choiceId)
        {
            foreach (var slot in CharacterProgression.Choices(sheet, sheet.Level))
                if (slot.Choice.Id == choiceId && slot.Choice.UpTo && sheet.Chosen(choiceId).Count < slot.Choice.Count) return slot;
            return null;
        }

        private sealed class HealOutsideHandler : ICommandHandler<CastHealingOutsideCombatCommand>
        {
            public CommandResult Handle(CastHealingOutsideCombatCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                var s = SpellCatalog.Find(command.SpellId);
                var access = new SpellAccess(new CharacterRules(player.Rpg));
                if (s == null || !access.IsReady(s.Id)) return CommandResult.Invalid("You do not have that spell ready.");
                if (s.Id == "lesser_restoration")
                {
                    // Phase 14 (ADR-0068): Lesser Restoration ends a lasting Blinded, Deafened, Paralyzed or Poisoned.
                    var at = Now(context.Committed);
                    var curable = false;
                    foreach (var k in LastingConditions.Active(player.Rpg, at))
                        if ((k.Remedies & ConditionRemedy.Restoration) != 0) curable = true;
                    if (!curable) return CommandResult.Invalid("Lesser Restoration would end nothing: you are not blinded, deafened, paralysed or poisoned.");
                    if (!access.CastLevels(s).Contains(command.SlotLevel)) return CommandResult.Invalid($"No level {command.SlotLevel} slot is left for {s.Name}.");
                    var rw = context.Edit();
                    var rs = rw.Player.Rpg;
                    rs.Resources[access.SlotResource(command.SlotLevel)] -= 1;
                    var said = LastingConditions.Restore(rs, at);
                    return CommandResult.Ok($"{s.Name}: {said}");
                }

                var heals = s.Effects.Count > 0;
                foreach (var e in s.Effects) if (e.Kind != CombatEffectKind.Healing) heals = false;
                if (!heals || !s.Castable) return CommandResult.Invalid($"Outside fights only instantaneous healing spells can be cast; {s.Name} is not one.");
                if (!access.CastLevels(s).Contains(command.SlotLevel)) return CommandResult.Invalid($"No level {command.SlotLevel} slot is left for {s.Name}.");
                if (player.Rpg.IsDead) return CommandResult.InvalidState("The dead cannot be healed.");
                var world = context.Edit();
                var sheet = world.Player.Rpg;
                var key = $"cast:{Now(world)}:{sheet.Spellcasting.RestLog.Count}:{s.Id}:{sheet.Resources[access.SlotResource(command.SlotLevel)]}";
                var die = Dice(world, key);
                var rules = new CharacterRules(sheet);
                var e0 = s.Effects[0];
                var n = e0.Dice.Count + Math.Max(0, command.SlotLevel - s.Level) * e0.PerSlotAbove.Count;
                var sum = 0;
                for (var i = 0; i < n; i++) sum += die(e0.Dice.Sides);
                var mod = AbilityRules.Modifier(rules.Score(rules.Class.Spellcasting.Ability));
                var life = rules.HasFeature("disciple_of_life") ? 2 + command.SlotLevel : 0;
                var amount = Math.Max(1, sum + mod + life);
                sheet.Resources[access.SlotResource(command.SlotLevel)] -= 1;
                var before = sheet.CurrentHp;
                CharacterVitals.ChangeHitPoints(sheet, amount);
                return CommandResult.Ok($"{s.Name} ({n}d{e0.Dice.Sides} + {mod}{(life > 0 ? $" + Disciple of Life {life}" : string.Empty)} = {amount}): {before} → {sheet.CurrentHp} hit points.");
            }
        }

        private sealed class FinishRestHandler : ICommandHandler<FinishRestCommand>
        {
            public CommandResult Handle(FinishRestCommand command, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                foreach (var r in player.Rpg.Spellcasting.RestLog) if (r.Key == command.Key) return CommandResult.InvalidState("That rest is already recorded.");
                var now = Now(context.Committed);
                var problem = command.Kind == RestKind.Short ? CharacterRest.ShortRestProblem(player.Rpg, command.HitDice)
                    : CharacterRest.LongRestProblem(player.Rpg, now - CharacterRest.LongRestMinutes);
                if (problem != null) return CommandResult.Invalid(problem, new[] { problem });
                var world = context.Edit();
                var lines = command.Kind == RestKind.Short
                    ? CharacterRest.ShortRest(world.Player.Rpg, command.HitDice, Dice(world, command.Key), now, command.Key)
                    : CharacterRest.LongRest(world.Player.Rpg, now, command.Key);
                if (command.Kind == RestKind.Long) lines.AddRange(RechargeItems(world.Player, Dice(world, command.Key + ":recharge")));
                if (command.Kind == RestKind.Long && LastingConditions.LongRest(world.Player.Rpg) is string ex) lines.Add(ex); // Phase 14: one exhaustion level per long rest
                var still = LastingConditions.Active(world.Player.Rpg, now);
                if (still.Count > 0) lines.Add("Still: " + string.Join(", ", still.ConvertAll(k => LastingConditions.Name(k.Type).ToLowerInvariant())) + " (a rest does not end those).");
                return CommandResult.Ok(string.Join("\n", lines));
            }
        }

        /// <summary>
        /// Phase 11: items with charges regain them after a long rest (their rolled recharge, up to the maximum). SRD 5.2.1
        /// says "daily at dawn"; Realmkeeper ties it to the long rest so it cannot be farmed by waiting (ADR-0052).
        /// </summary>
        internal static List<string> RechargeItems(PlayerCharacterData p, Func<int, int> die)
        {
            var lines = new List<string>();
            var stacks = new List<ItemStackData>(p.Inventory);
            stacks.AddRange(p.Equipment.Values);
            foreach (var s in stacks)
            {
                var m = ItemCatalog.Find(s.ItemId)?.Magic;
                if (m == null || m.MaxCharges <= 0 || m.Recharge.IsEmpty || s.Charges >= m.MaxCharges) continue;
                var roll = m.Recharge.Bonus;
                for (var i = 0; i < m.Recharge.Count; i++) roll += die(m.Recharge.Sides);
                var before = Math.Max(0, s.Charges);
                s.Charges = Math.Min(m.MaxCharges, before + Math.Max(0, roll));
                lines.Add($"The {ItemCatalog.Find(s.ItemId).Name} regains charges ({m.Recharge} = {roll}): {before} → {s.Charges}.");
            }

            return lines;
        }

        private static bool Contains(IReadOnlyList<string> list, string x)
        {
            foreach (var y in list) if (y == x) return true;
            return false;
        }
    }

    public sealed partial class WorldSession
    {
        /// <summary>
        /// A short rest: an hour passes (the world lives through it), then hit dice are spent and short-rest resources
        /// return. Refused during a fight. If the hour cannot pass, nothing is recovered.
        /// </summary>
        public RestResult ShortRest(int hitDice = 0) => Rest(RestKind.Short, hitDice);

        /// <summary>A long rest: eight hours pass, then hit points, resources and hit dice return; once per 24 hours.</summary>
        public RestResult LongRest() => Rest(RestKind.Long, 0);

        private RestResult Rest(RestKind kind, int hitDice)
        {
            if (!HasWorld) return new RestResult { Message = "There is no active world." };
            if (_world.ActiveEncounter != null) return new RestResult { Message = EncounterBlocksTime };
            var sheet = _world.Player?.Rpg;
            var problem = kind == RestKind.Short ? CharacterRest.ShortRestProblem(sheet, hitDice) : CharacterRest.LongRestProblem(sheet, SpellCommandHandlers.Now(_world));
            if (problem != null) return new RestResult { Message = problem };
            var where = Places.RestRules.Check(_world, kind); // Phase 14 (ADR-0068): where you rest matters
            if (where.Problem != null) return new RestResult { Message = where.Problem };
            var minutes = kind == RestKind.Short ? CharacterRest.ShortRestMinutes : CharacterRest.LongRestMinutes;
            if (kind == RestKind.Long && where.Interrupter != null)
            {
                // Inhabitants still stand in this place: half the night passes, then they find the sleeper. No rest is gained.
                var half = Execute(new AdvanceTimeCommand(minutes / 2));
                if (!half.IsSuccess) return new RestResult { Message = "The rest was interrupted: " + half.Message };
                var woken = Dispatch(new Places.InterruptRestCommand(where.Interrupter));
                return new RestResult { Message = $"Your rest is broken after {minutes / 120} hours. " + woken.Message, MinutesPassed = minutes / 2 };
            }

            var time = Execute(new AdvanceTimeCommand(minutes));
            if (!time.IsSuccess) return new RestResult { Message = "The rest was interrupted: " + time.Message };
            var key = kind + ":" + SpellCommandHandlers.Now(_world).ToString(CultureInfo.InvariantCulture);
            var done = Dispatch(new FinishRestCommand(kind, hitDice, key));
            if (!done.IsSuccess) return new RestResult { Message = $"{minutes / 60} hour(s) passed, but nothing was recovered: {done.Message}", MinutesPassed = minutes };
            return new RestResult { IsSuccess = true, Message = (kind == RestKind.Short ? "Short rest (1 hour)." : "Long rest (8 hours)."), Lines = done.Message.Split('\n'), MinutesPassed = minutes };
        }

        /// <summary>Why a rest cannot be taken now (null when it can).</summary>
        public string RestProblem(RestKind kind, int hitDice = 0)
        {
            if (!HasWorld) return "There is no active world.";
            if (_world.ActiveEncounter != null) return EncounterBlocksTime;
            var sheet = _world.Player?.Rpg;
            return (kind == RestKind.Short ? CharacterRest.ShortRestProblem(sheet, hitDice) : CharacterRest.LongRestProblem(sheet, SpellCommandHandlers.Now(_world)))
                   ?? Places.RestRules.Check(_world, kind).Problem;
        }

        /// <summary>Phase 14: what resting here would mean, for the HUD (allowed, refused and why, or a broken night ahead).</summary>
        public Places.RestCheck RestHere(RestKind kind) => HasWorld ? Places.RestRules.Check(_world, kind) : new Places.RestCheck { Problem = "There is no active world." };

        /// <summary>
        /// Phase 14 (ADR-0068): one try at breaking free of a lasting restraint; each try takes a minute, and working free
        /// once the tries are spent takes ten.
        /// </summary>
        public CommandResult BreakFree()
        {
            if (!HasWorld) return CommandResult.InvalidState("There is no active world.");
            var held = _world.Player?.Rpg == null ? null : LastingConditions.Find(_world.Player.Rpg, Realmkeeper.Domain.Combat.ConditionType.Restrained, SpellCommandHandlers.Now(_world));
            var working = held != null && held.EscapeTries >= LastingConditions.EscapeTries;
            var r = Dispatch(new Places.BreakFreeCommand());
            if (r.IsSuccess) Execute(new AdvanceTimeCommand(working ? LastingConditions.WorkFreeMinutes : LastingConditions.EscapeMinutes));
            return r;
        }
    }
}
