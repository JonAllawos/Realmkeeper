using System;
using System.Collections.Generic;
using System.Text;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Application
{
    /// <summary>Starts an encounter (Phase 8). Refused while another is going on.</summary>
    public sealed class StartEncounterCommand : ICommand
    {
        public StartEncounterCommand(EncounterSetup setup) { Setup = setup; }
        public EncounterSetup Setup { get; }
    }

    /// <summary>
    /// Starts the labelled practice encounter in the player's settlement (no encounter generator exists yet): two
    /// bandits at the edge of town, and the settlement's guard, if one is on duty nearby, fighting alongside the player.
    /// </summary>
    public sealed class StartPracticeEncounterCommand : ICommand
    {
        public StartPracticeEncounterCommand(bool lethal = true, bool withCaster = false)
        {
            Lethal = lethal;
            WithCaster = withCaster;
        }

        public bool Lethal { get; }

        /// <summary>Phase 9: a Cult Fanatic (an SRD spellcaster) joins the bandits.</summary>
        public bool WithCaster { get; }
    }

    /// <summary>One action of the active combatant. <see cref="ExpectedSequence"/>, when given, must match: a stale or repeated action is refused.</summary>
    public sealed class CombatActionCommand : ICommand
    {
        public CombatActionCommand(EncounterId encounterId, CombatantId actorId, CombatAction action, int? expectedSequence = null)
        {
            EncounterId = encounterId;
            ActorId = actorId;
            Action = action;
            ExpectedSequence = expectedSequence;
        }

        public EncounterId EncounterId { get; }
        public CombatantId ActorId { get; }
        public CombatAction Action { get; }
        public int? ExpectedSequence { get; }
    }

    /// <summary>
    /// Phase 10: the player's answer to the question an action is waiting on (ADR-0045): take the reaction (or make the
    /// decision) or let it pass. The action then finishes, or stops at its next question. Answered once.
    /// </summary>
    public sealed class RespondToReactionCommand : ICommand
    {
        public RespondToReactionCommand(EncounterId encounterId, int reactionId, bool accept)
        {
            EncounterId = encounterId;
            ReactionId = reactionId;
            Accept = accept;
        }

        public EncounterId EncounterId { get; }
        public int ReactionId { get; }
        public bool Accept { get; }
    }

    /// <summary>Lets the active computer-controlled combatant take its whole turn (the AI proposes; the engine resolves).</summary>
    public sealed class RunAiTurnCommand : ICommand
    {
        public RunAiTurnCommand(EncounterId encounterId, int? expectedSequence = null)
        {
            EncounterId = encounterId;
            ExpectedSequence = expectedSequence;
        }

        public EncounterId EncounterId { get; }
        public int? ExpectedSequence { get; }
    }

    /// <summary>Adds a combatant to a running encounter (reinforcements).</summary>
    public sealed class JoinEncounterCommand : ICommand
    {
        public JoinEncounterCommand(EncounterId encounterId, ParticipantSetup participant)
        {
            EncounterId = encounterId;
            Participant = participant;
        }

        public EncounterId EncounterId { get; }
        public ParticipantSetup Participant { get; }
    }

    /// <summary>Applies a condition from a scripted or narrative source (spells and traps will use it later). Rounds 0 = until removed.</summary>
    public sealed class ApplyConditionCommand : ICommand
    {
        public ApplyConditionCommand(EncounterId encounterId, CombatantId targetId, ConditionType type, string reason, int rounds = 0, int level = 1)
        {
            EncounterId = encounterId;
            TargetId = targetId;
            Type = type;
            Reason = reason;
            Rounds = rounds;
            Level = level;
        }

        public EncounterId EncounterId { get; }
        public CombatantId TargetId { get; }
        public ConditionType Type { get; }
        public string Reason { get; }
        public int Rounds { get; }
        public int Level { get; }
    }

    public sealed class RemoveConditionCommand : ICommand
    {
        public RemoveConditionCommand(EncounterId encounterId, CombatantId targetId, ConditionType type, string reason)
        {
            EncounterId = encounterId;
            TargetId = targetId;
            Type = type;
            Reason = reason;
        }

        public EncounterId EncounterId { get; }
        public CombatantId TargetId { get; }
        public ConditionType Type { get; }
        public string Reason { get; }
    }

    /// <summary>Heals a combatant from a scripted source (potions and spells will use it later).</summary>
    public sealed class HealCombatantCommand : ICommand
    {
        public HealCombatantCommand(EncounterId encounterId, CombatantId targetId, int amount, string source)
        {
            EncounterId = encounterId;
            TargetId = targetId;
            Amount = amount;
            Source = source;
        }

        public EncounterId EncounterId { get; }
        public CombatantId TargetId { get; }
        public int Amount { get; }
        public string Source { get; }
    }

    /// <summary>Ends an encounter: the player's side withdraws (no enemy may be next to the player), or it is called off.</summary>
    public sealed class EndEncounterCommand : ICommand
    {
        public EndEncounterCommand(EncounterId encounterId, bool withdraw)
        {
            EncounterId = encounterId;
            Withdraw = withdraw;
        }

        public EncounterId EncounterId { get; }
        public bool Withdraw { get; }
    }

    public static partial class CombatCommandHandlers
    {
        public const string PlayerSide = "player";

        public static void Register(CommandRegistry commands)
        {
            commands.Register<StartEncounterCommand>(new StartHandler());
            commands.Register<StartPracticeEncounterCommand>(new PracticeHandler());
            commands.Register<CombatActionCommand>(new ActionHandler());
            commands.Register<RunAiTurnCommand>(new AiHandler());
            commands.Register<RespondToReactionCommand>(new RespondHandler()); // Phase 10
            commands.Register<StartWorldEncounterCommand>(new WorldEncounterHandler()); // Phase 10
            commands.Register<JoinEncounterCommand>(new JoinHandler());
            commands.Register<ApplyConditionCommand>(new ApplyConditionHandler());
            commands.Register<RemoveConditionCommand>(new RemoveConditionHandler());
            commands.Register<HealCombatantCommand>(new HealHandler());
            commands.Register<EndEncounterCommand>(new EndHandler());
        }

        /// <summary>The practice encounter's setup for the player's current settlement, or null with the reason.</summary>
        public static EncounterSetup PracticeSetup(WorldData world, bool lethal, out string error, bool withCaster = false)
        {
            error = null;
            var p = world.Player;
            if (p == null || !p.IsInSettlement) { error = "Practice encounters happen in a settlement."; return null; }
            var setup = new EncounterSetup
            {
                Name = "Practice: bandits at the edge of town", Kind = EncounterKind.Practice, Lethality = lethal ? Lethality.Lethal : Lethality.NonLethal,
                SettlementId = p.SettlementId,
                Participants =
                {
                    new ParticipantSetup { Kind = CombatantKind.Player, Side = PlayerSide, Position = new GridPos(0, 0), Control = CombatControl.Player },
                    new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "bandit", Name = "Bandit", Side = "bandits", Position = new GridPos(3, 5) },
                    new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "bandit", Name = "Bandit archer", Side = "bandits", Position = new GridPos(-3, 8) },
                },
                DifficultTerrain = { new GridPos(0, 3), new GridPos(1, 3) },
            };
            // The guard on duty beside you: a rank-and-file guard rather than the captain (Phase 10 adds captains, who hold
            // higher-level sheets), the lowest ID among them for determinism.
            NpcData guard = null;
            foreach (var n in world.Npcs)
            {
                if (n.IsDeceased || n.Role != NpcRole.Guard || n.HomeSettlementId != p.SettlementId) continue;
                var better = guard == null || (n.Tier != NpcTier.Notable && guard.Tier == NpcTier.Notable) ||
                             ((n.Tier == NpcTier.Notable) == (guard.Tier == NpcTier.Notable) && n.NpcId.CompareTo(guard.NpcId) < 0);
                if (better) guard = n;
            }
            if (guard != null) setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Npc, NpcId = guard.NpcId, Side = PlayerSide, Position = new GridPos(-1, 0) });
            if (withCaster)
            {
                setup.Name = "Practice: a cult fanatic and bandits";
                setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "cult_fanatic", Name = "Cult Fanatic", Side = "bandits", Position = new GridPos(1, 9) });
            }

            return setup;
        }

        private static EncounterData Active(CommandContext context, EncounterId id, out CommandResult refused)
        {
            refused = null;
            if (!context.HasWorld) { refused = CommandResult.InvalidState("There is no active world."); return null; }
            foreach (var e in context.Committed.Encounters)
                if (e.EncounterId == id)
                {
                    if (!e.IsActive) refused = CommandResult.InvalidState($"{e.Name} is over.");
                    return e;
                }

            refused = CommandResult.NotFound("No such encounter.");
            return null;
        }

        private static (CombatEngine engine, WorldData world) Working(CommandContext context, EncounterId id)
        {
            var world = context.Edit();
            foreach (var e in world.Encounters) if (e.EncounterId == id) return (new CombatEngine(world, e), world);
            return (null, world);
        }

        /// <summary>Reports the engine's chronicle notes, applies residents' deaths through the shared routine, and trims old encounters.</summary>
        private static void Settle(CommandContext context, CombatEngine engine)
        {
            var world = engine.World;
            var where = engine.Encounter.SettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { engine.Encounter.SettlementId };
            foreach (var n in engine.Chronicle)
            {
                var importance = n.Type == HistoricalEventType.EncounterStarted || n.Type == HistoricalEventType.EncounterEnded ? EventImportance.Minor : EventImportance.Notable;
                var facts = new Dictionary<string, string>(n.Facts) { ["source"] = "combat" };
                context.ReportEvent(n.Type, EventSeverity.Info, importance, where, facts, npcIds: n.NpcId.IsEmpty ? null : new[] { n.NpcId });
            }

            foreach (var id in engine.ResidentDeaths)
            {
                var npc = world.FindNpc(id);
                if (npc == null || npc.IsDeceased) continue;
                context.ReportEvent(HistoricalEventType.NpcDied, EventSeverity.Critical, EventImportance.Notable, where,
                    new Dictionary<string, string> { ["source"] = "combat", ["npc"] = npc.NpcId.ToString(), ["name"] = npc.Name, ["role"] = npc.Role.ToString(), ["cause"] = "combat" },
                    npcIds: new[] { npc.NpcId });
                NpcSystem.RecordDeath(world, npc, world.FindSettlement(npc.HomeSettlementId), world.CurrentSimulationDate);
            }

            if (!engine.Encounter.IsActive) Party.PartyAftermath.Apply(world, engine.Encounter, QuestRuntime.Reporter(context)); // Phase 15: what the fight meant to the party
            if (!engine.Encounter.IsActive) WorldEncounters.Resolve(context, world, engine.Encounter); // Phase 10: the aftermath, once
            engine.Chronicle.Clear();
            engine.ResidentDeaths.Clear();
            var ended = 0;
            for (var i = world.Encounters.Count - 1; i >= 0; i--)
                if (!world.Encounters[i].IsActive && ++ended > WorldData.MaxEndedEncounters) world.Encounters.RemoveAt(i);
        }

        /// <summary>A readable account of an action's attacks, for the UI's result panel.</summary>
        public static string Describe(ActionOutcome o)
        {
            var sb = new StringBuilder();
            if (o.Pending != null) return $"Waiting for {o.WaitingOn ?? "the player"}: {o.Pending.Opportunity.Name} — {o.Pending.Opportunity.Trigger}"; // Phase 10
            foreach (var e in o.Entries)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(e.Text);
            }

            foreach (var a in o.Attacks)
            {
                if (a.Kind == AttackKind.Grapple || a.Kind == AttackKind.Shove || !a.Hit || a.DamageBreakdown == null) continue;
                sb.Append('\n').Append("Damage: ").Append(a.DamageBreakdown.Text).Append(" = ").Append(a.DamageBreakdown.Total);
            }

            return sb.ToString();
        }

        private static CommandResult Start(CommandContext context, EncounterSetup setup)
        {
            if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
            var world = context.Edit();
            Party.PartyCombat.Join(world, setup); // Phase 15: the party fights beside the player
            var enc = CombatEngine.Create(world, setup, context.NextGuid, out var errors);
            if (enc == null) return CommandResult.Invalid("The encounter cannot start.", errors);
            world.Encounters.Add(enc);
            var engine = new CombatEngine(world, enc);
            engine.Begin();
            if (world.Player?.Rpg?.Spellcasting != null && enc.Combatants.Exists(c => c.Kind == CombatantKind.Player)) world.Player.Rpg.Spellcasting.PreparationChanges = 0; // Phase 9: a fight closes the preparation window
            Settle(context, engine);
            return CommandResult.Ok($"{enc.Name}: round {enc.Round}, {enc.Active?.Name ?? "nobody"}'s turn.");
        }

        /// <summary>Phase 10: starts a generated encounter (its individuals already drawn) and describes it.</summary>
        internal static CommandResult StartGenerated(CommandContext context, EncounterSetup setup, string description)
        {
            var r = Start(context, setup);
            return r.IsSuccess ? CommandResult.Ok(description + Environment.NewLine + r.Message) : r;
        }

        private sealed class WorldEncounterHandler : ICommandHandler<StartWorldEncounterCommand>
        {
            public CommandResult Handle(StartWorldEncounterCommand command, CommandContext context) => WorldEncounters.Start(command, context);
        }

        private sealed class StartHandler : ICommandHandler<StartEncounterCommand>
        {
            public CommandResult Handle(StartEncounterCommand command, CommandContext context) => Start(context, command.Setup);
        }

        private sealed class PracticeHandler : ICommandHandler<StartPracticeEncounterCommand>
        {
            public CommandResult Handle(StartPracticeEncounterCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var setup = PracticeSetup(context.Committed, command.Lethal, out var error, command.WithCaster);
                return setup == null ? CommandResult.InvalidState(error) : Start(context, setup);
            }
        }

        private sealed class ActionHandler : ICommandHandler<CombatActionCommand>
        {
            public CommandResult Handle(CombatActionCommand command, CommandContext context)
            {
                var committed = Active(context, command.EncounterId, out var refused);
                if (refused != null) return refused;
                if (command.ExpectedSequence.HasValue && command.ExpectedSequence.Value != committed.ActionSequence)
                    return CommandResult.InvalidState("That action was already processed or is out of date; the encounter has moved on.");
                if (command.Action == null) return CommandResult.Invalid("No action.");
                var (engine, _) = Working(context, command.EncounterId);
                var outcome = engine.Act(command.ActorId, command.Action);
                if (!outcome.Ok) return CommandResult.Invalid(outcome.Error, new[] { outcome.Error });
                Settle(context, engine);
                return CommandResult.Ok(Describe(outcome));
            }
        }

        private sealed class AiHandler : ICommandHandler<RunAiTurnCommand>
        {
            public CommandResult Handle(RunAiTurnCommand command, CommandContext context)
            {
                var committed = Active(context, command.EncounterId, out var refused);
                if (refused != null) return refused;
                if (command.ExpectedSequence.HasValue && command.ExpectedSequence.Value != committed.ActionSequence)
                    return CommandResult.InvalidState("That turn was already processed or is out of date.");
                var active = committed.Active;
                if (active == null || active.Control != CombatControl.Ai) return CommandResult.InvalidState("It is not a computer-controlled combatant's turn.");
                var (engine, _) = Working(context, command.EncounterId);
                var actor = engine.Encounter.Find(active.CombatantId);
                var lines = new List<string>();
                var companion = CombatAi.IsCompanion(engine, actor); // Phase 15: the companion layer, with its reasons
                string said = null;
                for (var guard = 0; guard < 30 && engine.Encounter.IsActive && engine.Encounter.Active == actor; guard++)
                {
                    string why = null;
                    var proposal = companion ? CombatAi.ForCompanion(engine, actor, out why) : CombatAi.Next(engine, actor);
                    if (why != null && why != said) { engine.Explain(actor, default, why); said = why; }
                    var outcome = engine.Act(actor.CombatantId, proposal);
                    if (outcome.Pending != null) { lines.Add(Describe(outcome)); break; } // Phase 10: the player is asked to react
                    if (!outcome.Ok) outcome = engine.Act(actor.CombatantId, new EndTurnAction()); // never stuck on a refused proposal
                    var text = Describe(outcome);
                    if (text.Length > 0) lines.Add(text);
                    if (proposal is EndTurnAction) break;
                }

                if (engine.Encounter.IsActive && engine.Encounter.Active == actor && engine.Encounter.PendingReaction == null) engine.Act(actor.CombatantId, new EndTurnAction());
                Settle(context, engine);
                return CommandResult.Ok(string.Join("\n", lines));
            }
        }

        private sealed class RespondHandler : ICommandHandler<RespondToReactionCommand>
        {
            public CommandResult Handle(RespondToReactionCommand command, CommandContext context)
            {
                Active(context, command.EncounterId, out var refused);
                if (refused != null) return refused;
                var (engine, _) = Working(context, command.EncounterId);
                var outcome = engine.Respond(command.ReactionId, command.Accept);
                if (!outcome.Ok) return CommandResult.InvalidState(outcome.Error);
                Settle(context, engine);
                return CommandResult.Ok(Describe(outcome));
            }
        }

        private sealed class JoinHandler : ICommandHandler<JoinEncounterCommand>
        {
            public CommandResult Handle(JoinEncounterCommand command, CommandContext context)
            {
                Active(context, command.EncounterId, out var refused);
                if (refused != null) return refused;
                var (engine, _) = Working(context, command.EncounterId);
                var error = engine.Join(command.Participant, context.NextGuid);
                if (error != null) return CommandResult.Invalid(error, new[] { error });
                engine.Encounter.ActionSequence++;
                Settle(context, engine);
                return CommandResult.Ok("Joined.");
            }
        }

        private sealed class ApplyConditionHandler : ICommandHandler<ApplyConditionCommand>
        {
            public CommandResult Handle(ApplyConditionCommand command, CommandContext context)
            {
                Active(context, command.EncounterId, out var refused);
                if (refused != null) return refused;
                if (!Enum.IsDefined(typeof(ConditionType), command.Type)) return CommandResult.Invalid("Unknown condition.");
                if (command.Rounds < 0 || command.Rounds > 100 || command.Level < 1 || command.Level > 6) return CommandResult.Invalid("Rounds must be 0–100 and exhaustion 1–6.");
                var (engine, _) = Working(context, command.EncounterId);
                var target = engine.Encounter.Find(command.TargetId);
                if (target == null) return CommandResult.NotFound("No such combatant.");
                var error = engine.ApplyCondition(target, command.Type, default, command.Reason ?? "applied", command.Rounds > 0 ? ConditionDuration.StartOfTurn : ConditionDuration.UntilRemoved,
                    command.Rounds > 0 ? target.CombatantId : default, engine.Encounter.Round + command.Rounds, command.Level);
                if (error != null) return CommandResult.Invalid(error, new[] { error });
                engine.Encounter.ActionSequence++;
                engine.CheckEnd();
                Settle(context, engine);
                return CommandResult.Ok($"{target.Name} is {CombatEngine.Describe(command.Type)}.");
            }
        }

        private sealed class RemoveConditionHandler : ICommandHandler<RemoveConditionCommand>
        {
            public CommandResult Handle(RemoveConditionCommand command, CommandContext context)
            {
                Active(context, command.EncounterId, out var refused);
                if (refused != null) return refused;
                var (engine, _) = Working(context, command.EncounterId);
                var target = engine.Encounter.Find(command.TargetId);
                if (target == null) return CommandResult.NotFound("No such combatant.");
                if (command.Type == ConditionType.Unconscious && target.State != Domain.Combat.VitalState.Conscious) return CommandResult.Invalid("Waking someone at 0 hit points needs healing.");
                if (!engine.RemoveCondition(target, command.Type, command.Reason ?? "removed")) return CommandResult.Invalid($"{target.Name} is not {CombatEngine.Describe(command.Type)}.");
                engine.Encounter.ActionSequence++;
                Settle(context, engine);
                return CommandResult.Ok("Removed.");
            }
        }

        private sealed class HealHandler : ICommandHandler<HealCombatantCommand>
        {
            public CommandResult Handle(HealCombatantCommand command, CommandContext context)
            {
                Active(context, command.EncounterId, out var refused);
                if (refused != null) return refused;
                if (command.Amount < 1 || command.Amount > 1000) return CommandResult.Invalid("Heal 1–1000 hit points.");
                var (engine, _) = Working(context, command.EncounterId);
                var target = engine.Encounter.Find(command.TargetId);
                if (target == null) return CommandResult.NotFound("No such combatant.");
                var error = engine.Heal(target, command.Amount, command.Source ?? "healing");
                if (error != null) return CommandResult.Invalid(error, new[] { error });
                engine.Encounter.ActionSequence++;
                Settle(context, engine);
                return CommandResult.Ok("Healed.");
            }
        }

        private sealed class EndHandler : ICommandHandler<EndEncounterCommand>
        {
            public CommandResult Handle(EndEncounterCommand command, CommandContext context)
            {
                Active(context, command.EncounterId, out var refused);
                if (refused != null) return refused;
                var (engine, _) = Working(context, command.EncounterId);
                var e = engine.Encounter;
                if (command.Withdraw)
                {
                    CombatantData player = null;
                    foreach (var c in e.Combatants) if (c.Kind == CombatantKind.Player) player = c;
                    if (player == null || player.State != Domain.Combat.VitalState.Conscious) return CommandResult.InvalidState("Only a conscious player can lead a withdrawal.");
                    foreach (var c in e.Combatants)
                        if (CombatEngine.Hostile(player, c) && c.State == Domain.Combat.VitalState.Conscious && c.Position.IsAdjacent(player.Position))
                            return CommandResult.InvalidState($"{c.Name} is right next to you: get clear (Disengage and move) before withdrawing.");
                    foreach (var c in e.Combatants) if (c.Side == player.Side && c.State == Domain.Combat.VitalState.Conscious) c.State = Domain.Combat.VitalState.Fled;
                    engine.Finish(EncounterOutcome.Withdrawn, null);
                }
                else engine.Finish(EncounterOutcome.Ended, null);

                e.ActionSequence++;
                Settle(context, engine);
                return CommandResult.Ok(command.Withdraw ? "You withdraw from the fight." : "The fight is called off.");
            }
        }
    }
}
