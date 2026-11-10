using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Application
{
    // The player's political interventions. Each is a typed command dispatched through the
    // session's registry: validated first, applied to a working copy, validated again as a
    // whole world, then committed. Every one records a chronicle event whose "source" fact is
    // "player", so manual changes can always be told apart from the simulation's.

    /// <summary>
    /// Sets the opinion between two factions and/or their agreement. Leaving a value null keeps it.
    /// Ending an agreement (AgreementType.None) breaks it: opinion and trust fall.
    /// </summary>
    public sealed class SetFactionRelationCommand : ICommand
    {
        public SetFactionRelationCommand(FactionId factionA, FactionId factionB, int? opinion = null, AgreementType? agreement = null)
        {
            FactionA = factionA;
            FactionB = factionB;
            Opinion = opinion;
            Agreement = agreement;
        }

        public FactionId FactionA { get; }
        public FactionId FactionB { get; }
        public int? Opinion { get; }
        public AgreementType? Agreement { get; }
    }

    /// <summary>Files a claim by a faction on a settlement. A claim never changes who governs.</summary>
    public sealed class FileTerritorialClaimCommand : ICommand
    {
        public FileTerritorialClaimCommand(FactionId claimantId, SettlementId settlementId)
        {
            ClaimantId = claimantId;
            SettlementId = settlementId;
        }

        public FactionId ClaimantId { get; }
        public SettlementId SettlementId { get; }
    }

    /// <summary>Recognizes, contests or withdraws (Relinquished) an open claim.</summary>
    public sealed class ResolveTerritorialClaimCommand : ICommand
    {
        public ResolveTerritorialClaimCommand(ClaimId claimId, ClaimStatus status)
        {
            ClaimId = claimId;
            Status = status;
        }

        public ClaimId ClaimId { get; }
        public ClaimStatus Status { get; }
    }

    public sealed class EstablishFactionPresenceCommand : ICommand
    {
        public EstablishFactionPresenceCommand(FactionId factionId, SettlementId settlementId, int strength = 20)
        {
            FactionId = factionId;
            SettlementId = settlementId;
            Strength = strength;
        }

        public FactionId FactionId { get; }
        public SettlementId SettlementId { get; }
        public int Strength { get; }
    }

    public sealed class RemoveFactionPresenceCommand : ICommand
    {
        public RemoveFactionPresenceCommand(FactionId factionId, SettlementId settlementId)
        {
            FactionId = factionId;
            SettlementId = settlementId;
        }

        public FactionId FactionId { get; }
        public SettlementId SettlementId { get; }
    }

    /// <summary>
    /// Sets a settlement's governing authority (empty: independent) and, optionally, the noble
    /// house administering it for that authority.
    /// </summary>
    public sealed class SetSettlementGovernanceCommand : ICommand
    {
        public SetSettlementGovernanceCommand(SettlementId settlementId, FactionId governorId, FactionId administratorId = default)
        {
            SettlementId = settlementId;
            GovernorId = governorId;
            AdministratorId = administratorId;
        }

        public SettlementId SettlementId { get; }
        public FactionId GovernorId { get; }
        public FactionId AdministratorId { get; }
    }

    public sealed class CreateFactionGoalCommand : ICommand
    {
        public CreateFactionGoalCommand(FactionId factionId, GoalType type, int targetValue = 0, int priority = 3,
            SettlementId targetSettlementId = default, FactionId targetFactionId = default)
        {
            FactionId = factionId;
            Type = type;
            TargetValue = targetValue;
            Priority = priority;
            TargetSettlementId = targetSettlementId;
            TargetFactionId = targetFactionId;
        }

        public FactionId FactionId { get; }
        public GoalType Type { get; }
        public int TargetValue { get; }
        public int Priority { get; }
        public SettlementId TargetSettlementId { get; }
        public FactionId TargetFactionId { get; }
    }

    public sealed class AbandonFactionGoalCommand : ICommand
    {
        public AbandonFactionGoalCommand(FactionId factionId, GoalId goalId)
        {
            FactionId = factionId;
            GoalId = goalId;
        }

        public FactionId FactionId { get; }
        public GoalId GoalId { get; }
    }

    public sealed class SetFactionGoalPriorityCommand : ICommand
    {
        public SetFactionGoalPriorityCommand(FactionId factionId, GoalId goalId, int priority)
        {
            FactionId = factionId;
            GoalId = goalId;
            Priority = priority;
        }

        public FactionId FactionId { get; }
        public GoalId GoalId { get; }
        public int Priority { get; }
    }

    /// <summary>Registers the political command handlers (built into every session).</summary>
    internal static class PoliticalCommandHandlers
    {
        /// <summary>Value of the "source" fact on events caused by a command.</summary>
        public const string PlayerSource = "player";

        public static void Register(CommandRegistry commands)
        {
            commands.Register<SetFactionRelationCommand>(new SetRelationHandler());
            commands.Register<FileTerritorialClaimCommand>(new FileClaimHandler());
            commands.Register<ResolveTerritorialClaimCommand>(new ResolveClaimHandler());
            commands.Register<EstablishFactionPresenceCommand>(new EstablishPresenceHandler());
            commands.Register<RemoveFactionPresenceCommand>(new RemovePresenceHandler());
            commands.Register<SetSettlementGovernanceCommand>(new SetGovernanceHandler());
            commands.Register<CreateFactionGoalCommand>(new CreateGoalHandler());
            commands.Register<AbandonFactionGoalCommand>(new AbandonGoalHandler());
            commands.Register<SetFactionGoalPriorityCommand>(new SetGoalPriorityHandler());
        }

        private static Dictionary<string, string> Facts() => new Dictionary<string, string> { ["source"] = PlayerSource };

        private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>Finds a living faction, or explains why not.</summary>
        private static FactionData Living(WorldData world, FactionId id, string role, out CommandResult failure)
        {
            failure = null;
            if (id.IsEmpty) { failure = CommandResult.Invalid($"A {role} faction ID is required."); return null; }
            var f = world.FindFaction(id);
            if (f == null) { failure = CommandResult.NotFound($"No faction has ID {id}."); return null; }
            if (f.IsDissolved) { failure = CommandResult.InvalidState($"{f.Name} has been dissolved."); return null; }
            return f;
        }

        private static SettlementData Settlement(WorldData world, SettlementId id, out CommandResult failure)
        {
            failure = null;
            if (id.IsEmpty) { failure = CommandResult.Invalid("A settlement ID is required."); return null; }
            var s = world.FindSettlement(id);
            if (s == null) failure = CommandResult.NotFound($"No settlement has ID {id}.");
            return s;
        }

        private sealed class SetRelationHandler : ICommandHandler<SetFactionRelationCommand>
        {
            public CommandResult Handle(SetFactionRelationCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var world = context.Committed;
                var a = Living(world, command.FactionA, "first", out var failure);
                if (a == null) return failure;
                var b = Living(world, command.FactionB, "second", out failure);
                if (b == null) return failure;
                if (a.FactionId == b.FactionId) return CommandResult.Invalid("A faction cannot have a relationship with itself.");
                if (!command.Opinion.HasValue && !command.Agreement.HasValue) return CommandResult.Invalid("Give an opinion, an agreement, or both.");
                if (command.Opinion.HasValue && (command.Opinion.Value < FactionRelationData.MinOpinion || command.Opinion.Value > FactionRelationData.MaxOpinion))
                    return CommandResult.Invalid($"Opinion {command.Opinion.Value} is outside -100..100.");
                if (command.Agreement.HasValue && !Enum.IsDefined(typeof(AgreementType), command.Agreement.Value)) return CommandResult.Invalid("Unknown agreement type.");

                var existing = world.FindRelation(a.FactionId, b.FactionId);
                var opinionChanges = command.Opinion.HasValue && (existing == null || existing.Opinion != command.Opinion.Value);
                var agreementChanges = command.Agreement.HasValue && (existing?.Agreement ?? AgreementType.None) != command.Agreement.Value;
                if (!opinionChanges && !agreementChanges) return CommandResult.Invalid($"The relationship between {a.Name} and {b.Name} is already as requested.");

                var edit = context.Edit();
                var r = edit.FindRelation(a.FactionId, b.FactionId);
                if (r == null)
                {
                    var (first, second) = PoliticalMath.OrderPair(a.FactionId, b.FactionId);
                    r = new FactionRelationData { FactionA = first, FactionB = second, Trust = 50 };
                    edit.Relations.Add(r);
                }

                var rules = context.Session.PoliticalRules;
                var factions = new[] { a.FactionId, b.FactionId };
                var before = PoliticalMath.StateOf(r);
                var today = edit.CurrentSimulationDate;
                if (opinionChanges)
                {
                    r.Opinion = command.Opinion.Value;
                    r.LastCause = RelationCause.ManualIntervention;
                }

                if (agreementChanges)
                {
                    var old = r.Agreement;
                    r.Agreement = command.Agreement.Value;
                    if (r.Agreement == AgreementType.None)
                    {
                        r.AgreementSince = default;
                        if (!opinionChanges) r.Opinion = PoliticalMath.Clamp(r.Opinion - rules.BrokenAgreementOpinion, FactionRelationData.MinOpinion, FactionRelationData.MaxOpinion);
                        r.Trust = Math.Max(0, r.Trust - rules.BrokenAgreementTrust);
                        r.LastCause = RelationCause.BrokenAgreement;
                        var facts = Facts();
                        facts["agreement"] = old.ToString();
                        context.ReportEvent(HistoricalEventType.AgreementBroken, EventSeverity.Warning, EventImportance.Notable, Array.Empty<SettlementId>(), facts, factions);
                    }
                    else
                    {
                        r.AgreementSince = today;
                        if (!opinionChanges) r.LastCause = RelationCause.Agreement;
                        var facts = Facts();
                        facts["agreement"] = r.Agreement.ToString();
                        context.ReportEvent(HistoricalEventType.AgreementEstablished, EventSeverity.Positive, EventImportance.Notable, Array.Empty<SettlementId>(), facts, factions);
                    }
                }

                var after = PoliticalMath.StateOf(r);
                if (opinionChanges || after != before)
                {
                    var facts = Facts();
                    facts["from"] = before.ToString();
                    facts["to"] = after.ToString();
                    facts["cause"] = r.LastCause.ToString();
                    facts["opinion"] = I(r.Opinion);
                    context.ReportEvent(HistoricalEventType.RelationshipChanged, after >= before ? EventSeverity.Info : EventSeverity.Warning,
                        EventImportance.Notable, Array.Empty<SettlementId>(), facts, factions);
                }

                r.LastReportedState = after;
                return CommandResult.Ok($"{a.Name} and {b.Name}: {after}, opinion {PoliticalText.Signed(r.Opinion)}, {PoliticalText.Agreement(r.Agreement)}.");
            }
        }

        private sealed class FileClaimHandler : ICommandHandler<FileTerritorialClaimCommand>
        {
            public CommandResult Handle(FileTerritorialClaimCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var world = context.Committed;
                var f = Living(world, command.ClaimantId, "claimant", out var failure);
                if (f == null) return failure;
                var s = Settlement(world, command.SettlementId, out failure);
                if (s == null) return failure;
                if (!FactionRules.CanClaimTerritory(f.Type)) return CommandResult.Invalid($"{f.Name} is a {PoliticalText.TypeName(f.Type).ToLowerInvariant()} and cannot claim territory.");
                if (s.FactionId == f.FactionId) return CommandResult.Invalid($"{f.Name} already governs {s.Name}.");
                foreach (var c in world.Claims)
                {
                    if (c.IsOpen && c.ClaimantId == f.FactionId && c.SettlementId == s.SettlementId)
                        return CommandResult.Invalid($"{f.Name} already has an open claim on {s.Name}.");
                }

                var edit = context.Edit();
                var claim = new TerritorialClaimData
                {
                    ClaimId = new ClaimId(context.NextGuid($"claim:{f.FactionId}:{s.SettlementId}")),
                    ClaimantId = f.FactionId,
                    SettlementId = s.SettlementId,
                    Status = ClaimStatus.Unresolved,
                    FiledOn = edit.CurrentSimulationDate,
                    UpdatedOn = edit.CurrentSimulationDate,
                    IsManual = true,
                };
                edit.Claims.Add(claim);
                var facts = Facts();
                facts["claim"] = claim.ClaimId.ToString();
                context.ReportEvent(HistoricalEventType.TerritorialClaimFiled, EventSeverity.Info, EventImportance.Notable,
                    new[] { s.SettlementId }, facts, PoliticalSystem.ClaimFactions(f.FactionId, s));
                return CommandResult.Ok($"{f.Name} now claims {s.Name}. The claim does not change who governs it.");
            }
        }

        private sealed class ResolveClaimHandler : ICommandHandler<ResolveTerritorialClaimCommand>
        {
            public CommandResult Handle(ResolveTerritorialClaimCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                if (command.ClaimId.IsEmpty) return CommandResult.Invalid("A claim ID is required.");
                if (command.Status != ClaimStatus.Recognized && command.Status != ClaimStatus.Contested && command.Status != ClaimStatus.Relinquished)
                    return CommandResult.Invalid("A claim can be recognized, contested or relinquished.");
                var world = context.Committed;
                var claim = world.FindClaim(command.ClaimId);
                if (claim == null) return CommandResult.NotFound($"No claim has ID {command.ClaimId}.");
                var claimant = world.FindFaction(claim.ClaimantId);
                var s = world.FindSettlement(claim.SettlementId);
                if (!claim.IsOpen) return CommandResult.InvalidState($"That claim is closed ({claim.Status}).");
                if (claim.Status == command.Status) return CommandResult.Invalid($"The claim is already {command.Status.ToString().ToLowerInvariant()}.");

                var edit = context.Edit();
                var c = edit.FindClaim(claim.ClaimId);
                c.Status = command.Status;
                c.UpdatedOn = edit.CurrentSimulationDate;
                var facts = Facts();
                facts["claim"] = c.ClaimId.ToString();
                facts["outcome"] = c.Status.ToString();
                facts["cause"] = "decision";
                var contested = c.Status == ClaimStatus.Contested;
                context.ReportEvent(contested ? HistoricalEventType.TerritorialClaimContested : HistoricalEventType.TerritorialClaimResolved,
                    contested ? EventSeverity.Warning : EventSeverity.Info, EventImportance.Notable,
                    new[] { s.SettlementId }, facts, PoliticalSystem.ClaimFactions(claim.ClaimantId, s));
                return CommandResult.Ok($"{claimant?.Name}'s claim on {s.Name} is now {c.Status.ToString().ToLowerInvariant()}. {s.Name} is still governed as before.");
            }
        }

        private sealed class EstablishPresenceHandler : ICommandHandler<EstablishFactionPresenceCommand>
        {
            public CommandResult Handle(EstablishFactionPresenceCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var world = context.Committed;
                var f = Living(world, command.FactionId, "target", out var failure);
                if (f == null) return failure;
                var s = Settlement(world, command.SettlementId, out failure);
                if (s == null) return failure;
                if (command.Strength < 1 || command.Strength > 100) return CommandResult.Invalid($"Presence strength {command.Strength} is outside 1..100.");
                if (f.FindPresence(s.SettlementId) != null) return CommandResult.Invalid($"{f.Name} is already present in {s.Name}.");

                var edit = context.Edit();
                edit.FindFaction(f.FactionId).Presence.Add(new FactionPresenceData
                {
                    SettlementId = s.SettlementId,
                    Strength = command.Strength,
                    Status = command.Strength >= context.Session.PoliticalRules.EstablishedStrength ? PresenceStatus.Established : PresenceStatus.Waning,
                    EstablishedOn = edit.CurrentSimulationDate,
                });
                var facts = Facts();
                facts["strength"] = I(command.Strength);
                context.ReportEvent(HistoricalEventType.FactionPresenceEstablished, EventSeverity.Info, EventImportance.Notable,
                    new[] { s.SettlementId }, facts, new[] { f.FactionId });
                return CommandResult.Ok($"{f.Name} is now present in {s.Name}.");
            }
        }

        private sealed class RemovePresenceHandler : ICommandHandler<RemoveFactionPresenceCommand>
        {
            public CommandResult Handle(RemoveFactionPresenceCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var world = context.Committed;
                var f = Living(world, command.FactionId, "target", out var failure);
                if (f == null) return failure;
                var s = Settlement(world, command.SettlementId, out failure);
                if (s == null) return failure;
                var presence = f.FindPresence(s.SettlementId);
                if (presence == null) return CommandResult.Invalid($"{f.Name} has no presence in {s.Name}.");
                if (s.FactionId == f.FactionId) return CommandResult.InvalidState($"{f.Name} governs {s.Name}; change the governance first.");
                if (s.AdministratorFactionId == f.FactionId) return CommandResult.InvalidState($"{f.Name} administers {s.Name}; change the governance first.");

                var edit = context.Edit();
                var ef = edit.FindFaction(f.FactionId);
                ef.Presence.Remove(ef.FindPresence(s.SettlementId));
                var facts = Facts();
                facts["members"] = I(presence.Members);
                context.ReportEvent(HistoricalEventType.FactionPresenceWithdrawn, EventSeverity.Warning, EventImportance.Notable,
                    new[] { s.SettlementId }, facts, new[] { f.FactionId });
                return CommandResult.Ok($"{f.Name} has withdrawn from {s.Name}.");
            }
        }

        private sealed class SetGovernanceHandler : ICommandHandler<SetSettlementGovernanceCommand>
        {
            public CommandResult Handle(SetSettlementGovernanceCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var world = context.Committed;
                var s = Settlement(world, command.SettlementId, out var failure);
                if (s == null) return failure;

                FactionData governor = null;
                FactionData administrator = null;
                if (!command.GovernorId.IsEmpty)
                {
                    governor = Living(world, command.GovernorId, "governing", out failure);
                    if (governor == null) return failure;
                    if (!FactionRules.CanGovern(governor.Type))
                        return CommandResult.Invalid($"{governor.Name} is a {PoliticalText.TypeName(governor.Type).ToLowerInvariant()} and cannot govern a settlement.");
                }

                if (!command.AdministratorId.IsEmpty)
                {
                    if (governor == null) return CommandResult.Invalid("An independent settlement has no authority to administer it for.");
                    administrator = Living(world, command.AdministratorId, "administering", out failure);
                    if (administrator == null) return failure;
                    if (administrator.FactionId == governor.FactionId) return CommandResult.Invalid("The governing authority cannot also be its own administrator.");
                    if (!FactionRules.CanAdminister(administrator.Type))
                        return CommandResult.Invalid($"{administrator.Name} is a {PoliticalText.TypeName(administrator.Type).ToLowerInvariant()}; only a noble house can administer a settlement.");
                }

                if (s.FactionId == command.GovernorId && s.AdministratorFactionId == command.AdministratorId)
                    return CommandResult.Invalid($"{s.Name} is already governed that way.");

                var edit = context.Edit();
                var es = edit.FindSettlement(s.SettlementId);
                var today = edit.CurrentSimulationDate;
                var rules = context.Session.PoliticalRules;
                es.FactionId = command.GovernorId;
                es.AdministratorFactionId = command.AdministratorId;

                // Authority needs an organisation on the ground.
                foreach (var (faction, strength) in new[] { (governor, 40), (administrator, 30) })
                {
                    if (faction == null) continue;
                    var ef = edit.FindFaction(faction.FactionId);
                    if (ef.FindPresence(s.SettlementId) != null) continue;
                    ef.Presence.Add(new FactionPresenceData
                    {
                        SettlementId = s.SettlementId,
                        Strength = strength,
                        Status = strength >= rules.EstablishedStrength ? PresenceStatus.Established : PresenceStatus.Waning,
                        EstablishedOn = today,
                    });
                }

                // A claimant that now governs has what it claimed.
                foreach (var c in edit.Claims)
                {
                    if (!c.IsOpen || c.SettlementId != s.SettlementId || c.ClaimantId != command.GovernorId) continue;
                    c.Status = ClaimStatus.Fulfilled;
                    c.UpdatedOn = today;
                }

                var factions = new List<FactionId>();
                foreach (var id in new[] { command.GovernorId, s.FactionId, command.AdministratorId, s.AdministratorFactionId })
                {
                    if (!id.IsEmpty && !factions.Contains(id)) factions.Add(id);
                }

                var facts = Facts();
                facts["from"] = s.FactionId.IsEmpty ? string.Empty : s.FactionId.ToString();
                facts["to"] = command.GovernorId.IsEmpty ? string.Empty : command.GovernorId.ToString();
                facts["administrator"] = command.AdministratorId.IsEmpty ? string.Empty : command.AdministratorId.ToString();
                context.ReportEvent(HistoricalEventType.GovernanceChanged, EventSeverity.Warning, EventImportance.Major, new[] { s.SettlementId }, facts, factions);

                var by = governor == null ? "independent" : "governed by " + governor.Name;
                if (administrator != null) by += ", administered by " + administrator.Name;
                return CommandResult.Ok($"{s.Name} is now {by}.");
            }
        }

        private sealed class CreateGoalHandler : ICommandHandler<CreateFactionGoalCommand>
        {
            public CommandResult Handle(CreateFactionGoalCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var world = context.Committed;
                var f = Living(world, command.FactionId, "goal", out var failure);
                if (f == null) return failure;
                if (!Enum.IsDefined(typeof(GoalType), command.Type)) return CommandResult.Invalid("Unknown goal type.");
                if (command.Priority < FactionRules.MinPriority || command.Priority > FactionRules.MaxPriority) return CommandResult.Invalid($"Priority {command.Priority} is outside 1..5.");

                SettlementData s = null;
                FactionData target = null;
                var settlementId = SettlementId.Empty;
                var factionId = FactionId.Empty;
                var value = 0;
                switch (command.Type)
                {
                    case GoalType.GainInfluence:
                        if (command.TargetValue < 1 || command.TargetValue > 100) return CommandResult.Invalid("An influence goal needs a target of 1..100.");
                        value = command.TargetValue;
                        break;
                    case GoalType.IncreaseMembership:
                        if (command.TargetValue < 1) return CommandResult.Invalid("A membership goal needs a target of at least 1 member.");
                        value = command.TargetValue;
                        break;
                    case GoalType.EstablishPresence:
                        s = Settlement(world, command.TargetSettlementId, out failure);
                        if (s == null) return failure;
                        if (command.TargetValue < 1 || command.TargetValue > 100) return CommandResult.Invalid("A presence goal needs a target strength of 1..100.");
                        settlementId = s.SettlementId;
                        value = command.TargetValue;
                        break;
                    case GoalType.ImproveRelationship:
                        target = Living(world, command.TargetFactionId, "target", out failure);
                        if (target == null) return failure;
                        if (target.FactionId == f.FactionId) return CommandResult.Invalid("A faction cannot improve its relationship with itself.");
                        if (command.TargetValue < FactionRelationData.MinOpinion || command.TargetValue > FactionRelationData.MaxOpinion)
                            return CommandResult.Invalid("A relationship goal needs a target opinion of -100..100.");
                        factionId = target.FactionId;
                        value = command.TargetValue;
                        break;
                    case GoalType.SecureClaim:
                        s = Settlement(world, command.TargetSettlementId, out failure);
                        if (s == null) return failure;
                        if (!FactionRules.CanClaimTerritory(f.Type)) return CommandResult.Invalid($"{f.Name} is a {PoliticalText.TypeName(f.Type).ToLowerInvariant()} and cannot claim territory.");
                        if (s.FactionId == f.FactionId) return CommandResult.Invalid($"{f.Name} already governs {s.Name}.");
                        settlementId = s.SettlementId;
                        break;
                    default:
                        if (!PoliticalMath.GovernsAnything(world, f)) return CommandResult.Invalid($"{f.Name} governs nothing to keep control of.");
                        break;
                }

                foreach (var g in f.Goals)
                {
                    if (g.IsOpen && g.Type == command.Type && g.TargetSettlementId == settlementId && g.TargetFactionId == factionId)
                        return CommandResult.Invalid($"{f.Name} already has that goal.");
                }

                var edit = context.Edit();
                var ef = edit.FindFaction(f.FactionId);
                var goal = new FactionGoalData
                {
                    GoalId = new GoalId(context.NextGuid($"goal:{f.FactionId}:{command.Type}:{settlementId}:{factionId}")),
                    Type = command.Type,
                    TargetSettlementId = settlementId,
                    TargetFactionId = factionId,
                    TargetValue = value,
                    Priority = command.Priority,
                    Status = GoalStatus.Active,
                    CreatedOn = edit.CurrentSimulationDate,
                    IsManual = true,
                };
                var progress = PoliticalMath.GoalProgress(edit, ef, goal, context.Session.PoliticalRules);
                goal.Progress = command.Type == GoalType.MaintainControl ? progress : Math.Min(99, progress); // completion is decided on a political tick
                ef.Goals.Add(goal);

                var factions = new List<FactionId> { f.FactionId };
                if (!factionId.IsEmpty) factions.Add(factionId);
                var facts = Facts();
                facts["goal"] = goal.GoalId.ToString();
                facts["goalType"] = goal.Type.ToString();
                facts["target"] = I(value);
                context.ReportEvent(HistoricalEventType.FactionGoalSet, EventSeverity.Info, EventImportance.Minor,
                    settlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { settlementId }, facts, factions);
                return CommandResult.Ok($"{f.Name} has a new goal: {PoliticalText.Goal(goal.Type, value, s?.Name, target?.Name)}.");
            }
        }

        private static FactionGoalData OpenGoal(WorldData world, FactionId factionId, GoalId goalId, out FactionData faction, out CommandResult failure)
        {
            faction = Living(world, factionId, "goal", out failure);
            if (faction == null) return null;
            if (goalId.IsEmpty) { failure = CommandResult.Invalid("A goal ID is required."); return null; }
            var goal = faction.FindGoal(goalId);
            if (goal == null) { failure = CommandResult.NotFound($"{faction.Name} has no goal with ID {goalId}."); return null; }
            if (!goal.IsOpen) { failure = CommandResult.InvalidState($"That goal is already {goal.Status.ToString().ToLowerInvariant()}."); return null; }
            return goal;
        }

        private sealed class AbandonGoalHandler : ICommandHandler<AbandonFactionGoalCommand>
        {
            public CommandResult Handle(AbandonFactionGoalCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var goal = OpenGoal(context.Committed, command.FactionId, command.GoalId, out var f, out var failure);
                if (goal == null) return failure;

                var edit = context.Edit();
                var g = edit.FindFaction(f.FactionId).FindGoal(goal.GoalId);
                g.Status = GoalStatus.Abandoned;
                g.CloseReason = GoalCloseReason.AbandonedByPlayer;
                g.ClosedOn = edit.CurrentSimulationDate;

                var factions = new List<FactionId> { f.FactionId };
                if (!g.TargetFactionId.IsEmpty) factions.Add(g.TargetFactionId);
                var facts = Facts();
                facts["goal"] = g.GoalId.ToString();
                facts["goalType"] = g.Type.ToString();
                facts["reason"] = g.CloseReason.ToString();
                facts["target"] = I(g.TargetValue);
                context.ReportEvent(HistoricalEventType.FactionGoalAbandoned, EventSeverity.Info, EventImportance.Minor,
                    g.TargetSettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { g.TargetSettlementId }, facts, factions);
                return CommandResult.Ok($"{f.Name} has abandoned a goal.");
            }
        }

        private sealed class SetGoalPriorityHandler : ICommandHandler<SetFactionGoalPriorityCommand>
        {
            public CommandResult Handle(SetFactionGoalPriorityCommand command, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var goal = OpenGoal(context.Committed, command.FactionId, command.GoalId, out var f, out var failure);
                if (goal == null) return failure;
                if (command.Priority < FactionRules.MinPriority || command.Priority > FactionRules.MaxPriority) return CommandResult.Invalid($"Priority {command.Priority} is outside 1..5.");
                if (goal.Priority == command.Priority) return CommandResult.Invalid("The goal already has that priority.");
                context.Edit().FindFaction(f.FactionId).FindGoal(goal.GoalId).Priority = command.Priority;
                return CommandResult.Ok($"Goal priority set to {command.Priority}.");
            }
        }
    }
}
