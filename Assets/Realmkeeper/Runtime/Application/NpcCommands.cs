using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Application
{
    // Phase 4 commands on named people. The player can only ask (answers come from the person's
    // own knowledge). The DM can edit identity, occupation, schedule, goals, relationships, memories
    // and knowledge, move people and change their affiliation. Every command is validated first and
    // applied to a working copy that is validated whole before it commits (atomic). Each records a
    // chronicle event whose "source" fact says who did it ("player" or "dm").

    /// <summary>Base for DM-capable commands: who issues them (DM by default).</summary>
    public abstract class NpcCommandBase : ICommand
    {
        protected NpcCommandBase(NpcId npcId, ChangeSource source)
        {
            NpcId = npcId;
            Source = source;
        }

        public NpcId NpcId { get; }
        public ChangeSource Source { get; }
    }

    /// <summary>The player asks a person about a topic. Allowed only where the person is today.</summary>
    public sealed class AskNpcCommand : ICommand
    {
        public AskNpcCommand(NpcId npcId, ConversationTopic topic, NpcId aboutPerson = default)
        {
            NpcId = npcId;
            Topic = topic;
            AboutPerson = aboutPerson;
        }

        public NpcId NpcId { get; }
        public ConversationTopic Topic { get; }

        /// <summary>For <see cref="ConversationTopic.Person"/>: whom they are asked about.</summary>
        public NpcId AboutPerson { get; }
    }

    public sealed class EditNpcIdentityCommand : NpcCommandBase
    {
        public EditNpcIdentityCommand(NpcId npcId, string name = null, NpcSex? sex = null, WorldDate? birthDate = null, ChangeSource source = ChangeSource.DungeonMaster)
            : base(npcId, source)
        {
            Name = name;
            Sex = sex;
            BirthDate = birthDate;
        }

        public string Name { get; }
        public NpcSex? Sex { get; }
        public WorldDate? BirthDate { get; }
    }

    /// <summary>Changes a person's occupation and (optionally) workplace; their routine follows.</summary>
    public sealed class SetNpcOccupationCommand : NpcCommandBase
    {
        public SetNpcOccupationCommand(NpcId npcId, NpcRole role, LocationId workplace = default, ChangeSource source = ChangeSource.DungeonMaster) : base(npcId, source)
        {
            Role = role;
            Workplace = workplace;
        }

        public NpcRole Role { get; }
        public LocationId Workplace { get; }
    }

    /// <summary>Replaces a person's weekly routine: seven locations, one per weekday.</summary>
    public sealed class SetNpcScheduleCommand : NpcCommandBase
    {
        public SetNpcScheduleCommand(NpcId npcId, IReadOnlyList<LocationId> days, ChangeSource source = ChangeSource.DungeonMaster) : base(npcId, source)
        {
            Days = days;
        }

        public IReadOnlyList<LocationId> Days { get; }
    }

    /// <summary>Gives a person a goal (or reprioritises the one they have of that type).</summary>
    public sealed class SetNpcGoalCommand : NpcCommandBase
    {
        public SetNpcGoalCommand(NpcId npcId, NpcGoalType type, int priority, NpcSkill? skill = null, ChangeSource source = ChangeSource.DungeonMaster) : base(npcId, source)
        {
            Type = type;
            Priority = priority;
            Skill = skill;
        }

        public NpcGoalType Type { get; }
        public int Priority { get; }
        public NpcSkill? Skill { get; }
    }

    public sealed class AbandonNpcGoalCommand : NpcCommandBase
    {
        public AbandonNpcGoalCommand(NpcId npcId, GoalId goalId, ChangeSource source = ChangeSource.DungeonMaster) : base(npcId, source)
        {
            GoalId = goalId;
        }

        public GoalId GoalId { get; }
    }

    /// <summary>Sets how two people regard each other; values left null are kept. Creates the tie if needed.</summary>
    public sealed class SetNpcRelationshipCommand : NpcCommandBase
    {
        public SetNpcRelationshipCommand(NpcId npcId, NpcId other, int? affinity = null, int? trust = null, int? respect = null, int? fear = null,
            int? rivalry = null, ChangeSource source = ChangeSource.DungeonMaster) : base(npcId, source)
        {
            Other = other;
            Affinity = affinity;
            Trust = trust;
            Respect = respect;
            Fear = fear;
            Rivalry = rivalry;
        }

        public NpcId Other { get; }
        public int? Affinity { get; }
        public int? Trust { get; }
        public int? Respect { get; }
        public int? Fear { get; }
        public int? Rivalry { get; }
    }

    public sealed class AddNpcMemoryCommand : NpcCommandBase
    {
        public AddNpcMemoryCommand(NpcId npcId, MemoryCategory category, string subject, int salience, EventId eventId = default,
            IReadOnlyList<NpcId> people = null, MemorySource memorySource = MemorySource.Experienced, ChangeSource source = ChangeSource.DungeonMaster) : base(npcId, source)
        {
            Category = category;
            Subject = subject;
            Salience = salience;
            EventId = eventId;
            People = people ?? Array.Empty<NpcId>();
            MemorySource = memorySource;
        }

        public MemoryCategory Category { get; }
        public string Subject { get; }
        public int Salience { get; }
        public EventId EventId { get; }
        public IReadOnlyList<NpcId> People { get; }
        public MemorySource MemorySource { get; }
    }

    public sealed class AddNpcKnowledgeCommand : NpcCommandBase
    {
        public AddNpcKnowledgeCommand(NpcId npcId, KnowledgeTopic topic, string subjectId, string value, KnowledgeStatus status, int confidence,
            ChangeSource source = ChangeSource.DungeonMaster) : base(npcId, source)
        {
            Topic = topic;
            SubjectId = subjectId;
            Value = value;
            Status = status;
            Confidence = confidence;
        }

        public KnowledgeTopic Topic { get; }
        public string SubjectId { get; }
        public string Value { get; }
        public KnowledgeStatus Status { get; }
        public int Confidence { get; }
    }

    /// <summary>
    /// Moves a person to a location. Within their settlement it changes only where they are today.
    /// To another settlement it relocates them: one person of the population moves with them (no
    /// one is counted twice), and they get a home, workplace and routine there.
    /// </summary>
    public sealed class MoveNpcCommand : NpcCommandBase
    {
        public MoveNpcCommand(NpcId npcId, LocationId location, ChangeSource source = ChangeSource.DungeonMaster) : base(npcId, source)
        {
            Location = location;
        }

        public LocationId Location { get; }
    }

    /// <summary>Changes a person's faction (empty: none). A faction's leader must be replaced first.</summary>
    public sealed class ChangeNpcAffiliationCommand : NpcCommandBase
    {
        public ChangeNpcAffiliationCommand(NpcId npcId, FactionId faction, ChangeSource source = ChangeSource.DungeonMaster) : base(npcId, source)
        {
            Faction = faction;
        }

        public FactionId Faction { get; }
    }

    public static class NpcCommandHandlers
    {
        public const int MaxSubjectLength = 120;

        public static void Register(CommandRegistry commands)
        {
            commands.Register<AskNpcCommand>(new AskHandler());
            commands.Register<EditNpcIdentityCommand>(new IdentityHandler());
            commands.Register<SetNpcOccupationCommand>(new OccupationHandler());
            commands.Register<SetNpcScheduleCommand>(new ScheduleHandler());
            commands.Register<SetNpcGoalCommand>(new GoalHandler());
            commands.Register<AbandonNpcGoalCommand>(new AbandonGoalHandler());
            commands.Register<SetNpcRelationshipCommand>(new RelationshipHandler());
            commands.Register<AddNpcMemoryCommand>(new MemoryHandler());
            commands.Register<AddNpcKnowledgeCommand>(new KnowledgeHandler());
            commands.Register<MoveNpcCommand>(new MoveHandler());
            commands.Register<ChangeNpcAffiliationCommand>(new AffiliationHandler());
        }

        internal static string SourceFact(ChangeSource s) => s == ChangeSource.Player ? "player" : s == ChangeSource.DungeonMaster ? "dm" : "simulation";

        private static Dictionary<string, string> Facts(ChangeSource source, NpcData n) => new Dictionary<string, string>
        {
            ["source"] = SourceFact(source),
            ["npc"] = n.NpcId.ToString(),
            ["name"] = n.Name,
        };

        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>A living person, or the reason why not.</summary>
        internal static NpcData Living(WorldData world, NpcId id, out CommandResult failure)
        {
            failure = null;
            if (id.IsEmpty) { failure = CommandResult.Invalid("A person's ID is required."); return null; }
            var n = world.FindNpc(id);
            if (n == null) { failure = CommandResult.NotFound($"No named person has ID {id}."); return null; }
            if (n.IsDeceased) { failure = CommandResult.InvalidState($"{n.Name} is dead."); return null; }
            if (n.Tier == NpcTier.Unassigned) { failure = CommandResult.InvalidState($"{n.Name} has not been initialised by the NPC engine."); return null; }
            return n;
        }

        private static CommandResult NoWorld(CommandContext context) => context.HasWorld ? null : CommandResult.InvalidState("There is no active world.");

        private static CommandResult CheckSource(ChangeSource source) =>
            source == ChangeSource.Player || source == ChangeSource.DungeonMaster ? null : CommandResult.Invalid("Commands are issued by the player or the DM.");

        private static IReadOnlyList<SettlementId> Where(NpcData n) => n.HomeSettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { n.HomeSettlementId };

        private static GoalId NewGoalId(CommandContext context) => new GoalId(context.NextGuid("npc-goal"));

        // ----- Ask ------------------------------------------------------------------------------------

        private sealed class AskHandler : ICommandHandler<AskNpcCommand>
        {
            public CommandResult Handle(AskNpcCommand command, CommandContext context)
            {
                var failure = NoWorld(context);
                if (failure != null) return failure;
                var world = context.Committed;
                var player = world.Player;
                if (player == null) return CommandResult.NotFound("This world has no player character.");
                if (!player.IsInSettlement) return CommandResult.InvalidState($"{player.Name} is not in a settlement.");
                if (!Enum.IsDefined(typeof(ConversationTopic), command.Topic)) return CommandResult.Invalid("Unknown topic.");
                var npc = Living(world, command.NpcId, out failure);
                if (npc == null) return failure;
                // Where they are this hour (Phase 5), not just today.
                var now = NpcActivityPlan.Now(world, npc);
                var here = NpcMind.SettlementAt(world, now.IsKnown ? now.Location : npc.CurrentLocationId)?.SettlementId ?? npc.HomeSettlementId;
                if (here != player.SettlementId) return CommandResult.Invalid($"{npc.Name} is not here just now.");
                if (now.Activity == ActivityKind.Sleeping) return CommandResult.InvalidState($"{npc.Name} is asleep.");
                if (command.Topic == ConversationTopic.Person && world.FindNpc(command.AboutPerson) == null)
                    return CommandResult.Invalid("Ask about whom? That person is unknown.");

                var answer = NpcDialogue.Answer(world, npc, command.Topic, command.AboutPerson, player.Name);
                var edit = context.Edit();
                var e = edit.FindNpc(npc.NpcId);
                var today = edit.CurrentSimulationDate;
                var first = !e.HasMetPlayer;
                e.ConversationCount++;
                PlayerCommandHandlers.Acquaint(e, today, false);
                if (first)
                {
                    e.MetPlayerOn = today;
                    var facts = Facts(ChangeSource.Player, e);
                    facts["role"] = e.Role.ToString();
                    facts["player"] = player.Name;
                    context.ReportEvent(HistoricalEventType.PlayerMetResident, EventSeverity.Info, EventImportance.Minor, new[] { player.SettlementId }, facts, null, new[] { e.NpcId });
                }

                // They remember the conversation (private: no chronicle entry beyond the first meeting).
                NpcMind.Remember(e, new NpcMemoryData
                {
                    MemoryId = new MemoryId(context.NextGuid("npc-memory")),
                    Date = today,
                    Category = MemoryCategory.Conversation,
                    Subject = "Talked with " + player.Name + " about " + NpcText.Topic(command.Topic.ToString()),
                    SettlementId = player.SettlementId,
                    Salience = first ? 40 : 25,
                    Source = MemorySource.Experienced,
                });
                return CommandResult.Ok(answer);
            }
        }

        // ----- DM edits -------------------------------------------------------------------------------

        private sealed class IdentityHandler : ICommandHandler<EditNpcIdentityCommand>
        {
            public CommandResult Handle(EditNpcIdentityCommand c, CommandContext context)
            {
                var failure = NoWorld(context) ?? CheckSource(c.Source);
                if (failure != null) return failure;
                var n = Living(context.Committed, c.NpcId, out failure);
                if (n == null) return failure;
                if (c.Name == null && !c.Sex.HasValue && !c.BirthDate.HasValue) return CommandResult.Invalid("Give a name, a sex or a birth date.");
                if (c.Name != null && (string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > NpcData.MaxNameLength))
                    return CommandResult.Invalid($"A name must be 1–{NpcData.MaxNameLength} characters.");
                if (c.Sex.HasValue && !Enum.IsDefined(typeof(NpcSex), c.Sex.Value)) return CommandResult.Invalid("Unknown sex.");
                var today = context.Committed.CurrentSimulationDate;
                if (c.BirthDate.HasValue && (!c.BirthDate.Value.IsValidDate || c.BirthDate.Value > today)) return CommandResult.Invalid("The birth date must be a valid date that has passed.");

                var e = context.Edit().FindNpc(n.NpcId);
                var fields = new List<string>();
                var oldName = e.Name;
                if (c.Name != null && c.Name.Trim() != e.Name) { e.Name = c.Name.Trim(); fields.Add("name"); }
                if (c.Sex.HasValue && c.Sex.Value != e.Sex) { e.Sex = c.Sex.Value; fields.Add("sex"); }
                if (c.BirthDate.HasValue && c.BirthDate.Value != e.BirthDate) { e.BirthDate = c.BirthDate.Value; fields.Add("birth date"); }
                if (fields.Count == 0) return CommandResult.Invalid($"{n.Name} already has those details.");
                var facts = Facts(c.Source, e);
                facts["fields"] = string.Join(", ", fields);
                if (oldName != e.Name) facts["oldName"] = oldName;
                context.ReportEvent(HistoricalEventType.NpcIdentityChanged, EventSeverity.Info, EventImportance.Minor, Where(e), facts, null, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name}: changed {string.Join(", ", fields)}.");
            }
        }

        private sealed class OccupationHandler : ICommandHandler<SetNpcOccupationCommand>
        {
            public CommandResult Handle(SetNpcOccupationCommand c, CommandContext context)
            {
                var failure = NoWorld(context) ?? CheckSource(c.Source);
                if (failure != null) return failure;
                var world = context.Committed;
                var n = Living(world, c.NpcId, out failure);
                if (n == null) return failure;
                if (!Enum.IsDefined(typeof(NpcRole), c.Role)) return CommandResult.Invalid("Unknown occupation.");
                LocationData place = null;
                if (!c.Workplace.IsEmpty)
                {
                    place = world.FindLocation(c.Workplace);
                    if (place == null) return CommandResult.NotFound("No location has that ID.");
                    if (place.SettlementId != n.HomeSettlementId) return CommandResult.Invalid($"{place.Name} is not in {n.Name}'s settlement.");
                }

                place = place ?? NpcGenesis.WorkplaceFor(world, n.HomeSettlementId, c.Role);
                if (c.Role == n.Role && (place?.LocationId ?? default) == n.WorkplaceId) return CommandResult.Invalid($"{n.Name} already works as a {c.Role} there.");

                var edit = context.Edit();
                var e = edit.FindNpc(n.NpcId);
                e.Role = c.Role;
                var oldWork = e.WorkplaceId;
                e.WorkplaceId = place?.LocationId ?? default;
                for (var d = 0; d < NpcScheduleData.Days; d++) if (e.Schedule[d] == oldWork || e.Schedule[d].IsEmpty) e.Schedule[d] = e.WorkplaceId.IsEmpty ? e.ResidenceId : e.WorkplaceId;
                var facts = Facts(c.Source, e);
                facts["role"] = c.Role.ToString();
                facts["place"] = place?.Name ?? "nowhere";
                context.ReportEvent(HistoricalEventType.NpcOccupationChanged, EventSeverity.Info, EventImportance.Minor, Where(e), facts, null, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name} is now a {c.Role}" + (place != null ? " at the " + place.Name : string.Empty) + ".");
            }
        }

        private sealed class ScheduleHandler : ICommandHandler<SetNpcScheduleCommand>
        {
            public CommandResult Handle(SetNpcScheduleCommand c, CommandContext context)
            {
                var failure = NoWorld(context) ?? CheckSource(c.Source);
                if (failure != null) return failure;
                var world = context.Committed;
                var n = Living(world, c.NpcId, out failure);
                if (n == null) return failure;
                if (c.Days == null || c.Days.Count != NpcScheduleData.Days) return CommandResult.Invalid($"A routine has {NpcScheduleData.Days} days.");
                foreach (var id in c.Days)
                {
                    var l = world.FindLocation(id);
                    if (l == null) return CommandResult.NotFound("A scheduled location does not exist.");
                    if (l.SettlementId != n.HomeSettlementId) return CommandResult.Invalid($"{l.Name} is not in {n.Name}'s settlement.");
                }

                var same = true;
                for (var d = 0; d < NpcScheduleData.Days; d++) if (n.Schedule[d] != c.Days[d]) same = false;
                if (same) return CommandResult.Invalid($"{n.Name} already keeps that routine.");

                var e = context.Edit().FindNpc(n.NpcId);
                for (var d = 0; d < NpcScheduleData.Days; d++) e.Schedule[d] = c.Days[d];
                e.CurrentLocationId = e.Schedule.For(context.Committed.CurrentSimulationDate);
                context.ReportEvent(HistoricalEventType.NpcScheduleChanged, EventSeverity.Info, EventImportance.Minor, Where(e), Facts(c.Source, e), null, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name}'s routine changed; today they are at the {context.Edit().FindLocation(e.CurrentLocationId)?.Name}.");
            }
        }

        private sealed class GoalHandler : ICommandHandler<SetNpcGoalCommand>
        {
            public CommandResult Handle(SetNpcGoalCommand c, CommandContext context)
            {
                var failure = NoWorld(context) ?? CheckSource(c.Source);
                if (failure != null) return failure;
                var n = Living(context.Committed, c.NpcId, out failure);
                if (n == null) return failure;
                if (!Enum.IsDefined(typeof(NpcGoalType), c.Type)) return CommandResult.Invalid("Unknown goal.");
                if (c.Priority < 0 || c.Priority > 100) return CommandResult.Invalid("Priority is 0–100.");
                if (c.Type == NpcGoalType.LearnSkill && !c.Skill.HasValue) return CommandResult.Invalid("Which skill?");
                NpcGoalData existing = null;
                var active = 0;
                foreach (var g in n.Goals)
                {
                    if (!g.IsActive) continue;
                    active++;
                    if (g.Type == c.Type) existing = g;
                }

                if (existing == null && active >= NpcBounds.MaxActiveGoals) return CommandResult.InvalidState($"{n.Name} already pursues {NpcBounds.MaxActiveGoals} goals; abandon one first.");
                if (existing != null && existing.Priority == c.Priority) return CommandResult.Invalid($"{n.Name} already has that goal at that priority.");

                var e = context.Edit().FindNpc(n.NpcId);
                NpcGoalData goal;
                if (existing != null)
                {
                    goal = e.Goals.Find(g => g.GoalId == existing.GoalId);
                    goal.Priority = c.Priority;
                    goal.Source = c.Source;
                }
                else
                {
                    goal = new NpcGoalData
                    {
                        GoalId = NewGoalId(context), Type = c.Type, Priority = c.Priority, Status = NpcGoalStatus.Active,
                        CreatedOn = context.Committed.CurrentSimulationDate, Source = c.Source, TargetSkill = c.Skill,
                    };
                    e.Goals.Add(goal);
                }

                var facts = Facts(c.Source, e);
                facts["goal"] = c.Type.ToString();
                facts["priority"] = I(c.Priority);
                context.ReportEvent(HistoricalEventType.NpcGoalSet, EventSeverity.Info, EventImportance.Minor, Where(e), facts, null, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name} will try to {NpcText.Goal(c.Type.ToString())} (priority {c.Priority}).");
            }
        }

        private sealed class AbandonGoalHandler : ICommandHandler<AbandonNpcGoalCommand>
        {
            public CommandResult Handle(AbandonNpcGoalCommand c, CommandContext context)
            {
                var failure = NoWorld(context) ?? CheckSource(c.Source);
                if (failure != null) return failure;
                var n = Living(context.Committed, c.NpcId, out failure);
                if (n == null) return failure;
                var goal = n.Goals.Find(g => g.GoalId == c.GoalId);
                if (goal == null) return CommandResult.NotFound($"{n.Name} has no such goal.");
                if (!goal.IsActive) return CommandResult.InvalidState("That goal is already closed.");
                var e = context.Edit().FindNpc(n.NpcId);
                var g2 = e.Goals.Find(g => g.GoalId == c.GoalId);
                g2.Status = NpcGoalStatus.Abandoned;
                g2.ClosedOn = context.Committed.CurrentSimulationDate;
                g2.Source = c.Source;
                var facts = Facts(c.Source, e);
                facts["goal"] = g2.Type.ToString();
                context.ReportEvent(HistoricalEventType.NpcGoalAbandoned, EventSeverity.Info, EventImportance.Minor, Where(e), facts, null, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name} gave up trying to {NpcText.Goal(g2.Type.ToString())}.");
            }
        }

        private sealed class RelationshipHandler : ICommandHandler<SetNpcRelationshipCommand>
        {
            public CommandResult Handle(SetNpcRelationshipCommand c, CommandContext context)
            {
                var failure = NoWorld(context) ?? CheckSource(c.Source);
                if (failure != null) return failure;
                var world = context.Committed;
                var a = Living(world, c.NpcId, out failure);
                if (a == null) return failure;
                var b = Living(world, c.Other, out failure);
                if (b == null) return failure;
                if (a.NpcId == b.NpcId) return CommandResult.Invalid("A person has no relationship with themselves.");
                if (!c.Affinity.HasValue && !c.Trust.HasValue && !c.Respect.HasValue && !c.Fear.HasValue && !c.Rivalry.HasValue)
                    return CommandResult.Invalid("Give at least one value.");
                if (c.Affinity.HasValue && (c.Affinity < -100 || c.Affinity > 100)) return CommandResult.Invalid("Affinity is −100…100.");
                foreach (var v in new[] { c.Trust, c.Respect, c.Fear, c.Rivalry })
                    if (v.HasValue && (v < 0 || v > 100)) return CommandResult.Invalid("Trust, respect, fear and rivalry are 0…100.");

                var edit = context.Edit();
                var today = edit.CurrentSimulationDate;
                var existed = edit.FindNpcRelation(a.NpcId, b.NpcId) != null;
                var r = NpcMind.Relate(edit, a.NpcId, b.NpcId, NpcRelationCause.ManualIntervention, NpcRelationKind.Acquaintance, today);
                if (r == null) return CommandResult.InvalidState($"{a.Name} or {b.Name} already has {NpcBounds.MaxRelationsPerNpc} relationships.");
                var before = r.Kind;
                if (c.Affinity.HasValue) r.Affinity = c.Affinity.Value;
                if (c.Trust.HasValue) r.Trust = c.Trust.Value;
                if (c.Respect.HasValue) r.Respect = c.Respect.Value;
                if (c.Fear.HasValue) r.Fear = c.Fear.Value;
                if (c.Rivalry.HasValue) r.Rivalry = c.Rivalry.Value;
                r.LastCause = NpcRelationCause.ManualIntervention;
                r.LastChanged = today;
                r.Kind = NpcMind.KindFor(r, context.Session.NpcRules);
                var facts = Facts(c.Source, a);
                facts["nameA"] = a.Name;
                facts["nameB"] = b.Name;
                facts["from"] = existed ? before.ToString() : "none";
                facts["to"] = r.Kind.ToString();
                facts["cause"] = NpcRelationCause.ManualIntervention.ToString();
                context.ReportEvent(existed ? HistoricalEventType.NpcRelationshipChanged : HistoricalEventType.NpcRelationshipFormed, EventSeverity.Info,
                    EventImportance.Minor, Where(a), facts, null, new[] { r.NpcA, r.NpcB });
                return CommandResult.Ok($"{a.Name} and {b.Name}: {r.Kind}, affinity {NpcText.Signed(r.Affinity)}, trust {r.Trust}, respect {r.Respect}.");
            }
        }

        private sealed class MemoryHandler : ICommandHandler<AddNpcMemoryCommand>
        {
            public CommandResult Handle(AddNpcMemoryCommand c, CommandContext context)
            {
                var failure = NoWorld(context) ?? CheckSource(c.Source);
                if (failure != null) return failure;
                var world = context.Committed;
                var n = Living(world, c.NpcId, out failure);
                if (n == null) return failure;
                if (!Enum.IsDefined(typeof(MemoryCategory), c.Category) || !Enum.IsDefined(typeof(MemorySource), c.MemorySource)) return CommandResult.Invalid("Unknown category or source.");
                if (string.IsNullOrWhiteSpace(c.Subject) || c.Subject.Length > MaxSubjectLength) return CommandResult.Invalid($"Describe the memory in 1–{MaxSubjectLength} characters.");
                if (c.Salience < 1 || c.Salience > 100) return CommandResult.Invalid("Salience is 1–100.");
                HistoricalEvent linked = null;
                if (!c.EventId.IsEmpty)
                {
                    foreach (var ev in world.HistoricalEvents) if (ev.EventId == c.EventId) linked = ev;
                    if (linked == null) return CommandResult.NotFound("No chronicle event has that ID.");
                }

                foreach (var p in c.People) if (world.FindNpc(p) == null) return CommandResult.NotFound("A person in the memory does not exist.");

                var e = context.Edit().FindNpc(n.NpcId);
                var today = world.CurrentSimulationDate;
                var kept = NpcMind.Remember(e, new NpcMemoryData
                {
                    MemoryId = new MemoryId(context.NextGuid("npc-memory")),
                    EventId = c.EventId,
                    Date = linked?.Date ?? today,
                    Category = c.Category,
                    Subject = c.Subject.Trim(),
                    SettlementId = linked?.SettlementId ?? e.HomeSettlementId,
                    NpcIds = new List<NpcId>(c.People),
                    Salience = c.Salience,
                    Source = c.MemorySource,
                });
                if (kept == null) return CommandResult.Invalid($"{e.Name}'s memory is full of things that matter more (salience {c.Salience} is too low to keep).");
                var facts = Facts(c.Source, e);
                facts["subject"] = c.Subject.Trim();
                facts["category"] = c.Category.ToString();
                context.ReportEvent(HistoricalEventType.NpcMemoryAdded, EventSeverity.Info, EventImportance.Minor, Where(e), facts, null, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name} now remembers: {c.Subject.Trim()}.");
            }
        }

        private sealed class KnowledgeHandler : ICommandHandler<AddNpcKnowledgeCommand>
        {
            public CommandResult Handle(AddNpcKnowledgeCommand c, CommandContext context)
            {
                var failure = NoWorld(context) ?? CheckSource(c.Source);
                if (failure != null) return failure;
                var n = Living(context.Committed, c.NpcId, out failure);
                if (n == null) return failure;
                if (!Enum.IsDefined(typeof(KnowledgeTopic), c.Topic) || !Enum.IsDefined(typeof(KnowledgeStatus), c.Status)) return CommandResult.Invalid("Unknown topic or status.");
                if (string.IsNullOrWhiteSpace(c.SubjectId) || c.Value == null) return CommandResult.Invalid("A subject and a value are required.");
                if (c.Confidence < 1 || c.Confidence > 100) return CommandResult.Invalid("Confidence is 1–100.");
                var e = context.Edit().FindNpc(n.NpcId);
                var today = context.Committed.CurrentSimulationDate;
                var k = NpcMind.Learn(e, c.Topic, c.SubjectId, c.Value, c.Status, c.Confidence, today, default, c.Status == KnowledgeStatus.Known ? 0 : 1, default,
                    () => new KnowledgeId(context.NextGuid("npc-knowledge")));
                if (k == null || k.Value != c.Value) return CommandResult.Invalid($"{e.Name} holds firmer knowledge about that already.");
                var facts = Facts(c.Source, e);
                facts["topic"] = c.Topic.ToString();
                context.ReportEvent(HistoricalEventType.NpcKnowledgeAdded, EventSeverity.Info, EventImportance.Minor, Where(e), facts, null, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name} now holds this as {NpcText.Status(c.Status)} ({c.Confidence}% sure).");
            }
        }

        private sealed class MoveHandler : ICommandHandler<MoveNpcCommand>
        {
            public CommandResult Handle(MoveNpcCommand c, CommandContext context)
            {
                var failure = NoWorld(context) ?? CheckSource(c.Source);
                if (failure != null) return failure;
                var world = context.Committed;
                var n = Living(world, c.NpcId, out failure);
                if (n == null) return failure;
                var place = world.FindLocation(c.Location);
                if (place == null) return CommandResult.NotFound("No location has that ID.");
                if (place.LocationId == n.CurrentLocationId) return CommandResult.Invalid($"{n.Name} is already there.");
                var edit = context.Edit();
                var e = edit.FindNpc(n.NpcId);
                var facts = Facts(c.Source, e);
                facts["place"] = place.Name;
                if (place.SettlementId == n.HomeSettlementId)
                {
                    e.CurrentLocationId = place.LocationId;
                    context.ReportEvent(HistoricalEventType.NpcMoved, EventSeverity.Info, EventImportance.Minor, Where(e), facts, null, new[] { e.NpcId });
                    return CommandResult.Ok($"{e.Name} is at the {place.Name} today.");
                }

                // Relocation: the person and one member of the population move together.
                var from = edit.FindSettlement(n.HomeSettlementId);
                var to = edit.FindSettlement(place.SettlementId);
                var namedThere = 0;
                foreach (var other in edit.Npcs) if (other.IsAlive && other.HomeSettlementId == from.SettlementId) namedThere++;
                if (from.Population - 1 < namedThere - 1) return CommandResult.InvalidState($"{from.Name} has no one to spare.");
                foreach (var f in edit.Factions)
                    if (f.LeaderNpcId == e.NpcId && !f.IsDissolved && f.FactionId == from.FactionId)
                        return CommandResult.InvalidState($"{e.Name} leads {f.Name}, which governs {from.Name}; appoint another leader first.");
                from.Population -= 1;
                to.Population += 1;
                e.HomeSettlementId = to.SettlementId;
                var homes = NpcGenesis.LocationsOf(edit, to.SettlementId, LocationKind.Residence);
                e.ResidenceId = homes.Count > 0 ? homes[NpcHash.Roll(NpcHash.Of(edit.Seed, "relocate", e.NpcId.ToString(), edit.CurrentSimulationDate.ToDayNumber()), homes.Count)].LocationId : default;
                e.WorkplaceId = NpcGenesis.WorkplaceFor(edit, to.SettlementId, e.Role)?.LocationId ?? default;
                NpcGenesis.PlanSchedule(edit, e, to.SettlementId);
                e.CurrentLocationId = place.LocationId;
                facts["fromSettlement"] = from.Name;
                context.ReportEvent(HistoricalEventType.NpcMoved, EventSeverity.Info, EventImportance.Notable, new[] { to.SettlementId, from.SettlementId }, facts, null, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name} moved from {from.Name} to {to.Name} (the {place.Name}).");
            }
        }

        private sealed class AffiliationHandler : ICommandHandler<ChangeNpcAffiliationCommand>
        {
            public CommandResult Handle(ChangeNpcAffiliationCommand c, CommandContext context)
            {
                var failure = NoWorld(context) ?? CheckSource(c.Source);
                if (failure != null) return failure;
                var world = context.Committed;
                var n = Living(world, c.NpcId, out failure);
                if (n == null) return failure;
                if (n.FactionId == c.Faction) return CommandResult.Invalid($"{n.Name} already belongs there.");
                if (!c.Faction.IsEmpty)
                {
                    var f = world.FindFaction(c.Faction);
                    if (f == null) return CommandResult.NotFound("No faction has that ID.");
                    if (f.IsDissolved) return CommandResult.InvalidState($"{f.Name} has been dissolved.");
                }

                foreach (var f in world.Factions)
                    if (f.LeaderNpcId == n.NpcId && !f.IsDissolved) return CommandResult.InvalidState($"{n.Name} leads {f.Name}; appoint another leader first.");

                var e = context.Edit().FindNpc(n.NpcId);
                e.FactionId = c.Faction;
                var facts = Facts(c.Source, e);
                facts["faction"] = c.Faction.IsEmpty ? string.Empty : c.Faction.ToString();
                context.ReportEvent(HistoricalEventType.NpcAffiliationChanged, EventSeverity.Info, EventImportance.Minor, Where(e), facts,
                    c.Faction.IsEmpty ? null : new[] { c.Faction }, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name} now belongs to {(c.Faction.IsEmpty ? "no faction" : world.FindFaction(c.Faction).Name)}.");
            }
        }
    }

    /// <summary>
    /// Builds a person's answer from their own knowledge, memories and self. Never from the world's
    /// truth: if they do not know something, they say so; if what they hold is a belief or a rumour,
    /// the answer says so; if it is out of date, they say it anyway, as they believe it.
    /// </summary>
    public static class NpcDialogue
    {
        public static string Answer(WorldData world, NpcData npc, ConversationTopic topic, NpcId about, string playerName)
        {
            var here = NpcMind.SettlementAt(world, npc.CurrentLocationId) ?? world.FindSettlement(npc.HomeSettlementId);
            switch (topic)
            {
                case ConversationTopic.Themselves:
                    return Themselves(world, npc);
                case ConversationTopic.Settlement:
                    if (here == null) return "I couldn't tell you much about this place.";
                    var food = NpcMind.Find(npc, KnowledgeTopic.SettlementFood, here.SettlementId.ToString());
                    var state = NpcMind.Find(npc, KnowledgeTopic.SettlementProsperity, here.SettlementId.ToString());
                    if (food == null && state == null) return "I couldn't say how things stand here.";
                    var parts = new List<string>();
                    if (food != null) parts.Add(Hedge(food) + (food.Value == "plenty" ? "there's enough bread to go round" : food.Value == "short" ? "the granary is running low" : "people are going hungry"));
                    if (state != null) parts.Add("and " + here.Name + " is " + state.Value);
                    return Capital(string.Join(" ", parts)) + ".";
                case ConversationTopic.Governor:
                    if (here == null) return "I don't know who rules here.";
                    var gov = NpcMind.Find(npc, KnowledgeTopic.SettlementGovernor, here.SettlementId.ToString());
                    if (gov == null) return "Couldn't tell you who's in charge.";
                    if (string.IsNullOrEmpty(gov.Value)) return Capital(Hedge(gov) + "nobody governs " + here.Name + ".");
                    return Capital(Hedge(gov) + FactionName(world, gov.Value) + " governs " + here.Name + ".");
                case ConversationTopic.FactionLeader:
                    if (npc.FactionId.IsEmpty) return "I keep to myself; I'm no one's follower.";
                    var leader = NpcMind.Find(npc, KnowledgeTopic.FactionLeader, npc.FactionId.ToString());
                    if (leader == null) return "I'm not sure who leads us these days.";
                    if (Guid.TryParse(leader.Value, out var lid) && new NpcId(lid) == npc.NpcId) return "I lead " + FactionName(world, npc.FactionId.ToString()) + ", as it happens.";
                    return Capital(Hedge(leader) + PersonName(world, leader.Value) + " leads " + FactionName(world, npc.FactionId.ToString()) + ".");
                case ConversationTopic.News:
                    NpcMemoryData best = null;
                    foreach (var m in npc.Memories)
                        if (m.Category != MemoryCategory.Conversation && (best == null || m.Salience > best.Salience || m.Salience == best.Salience && m.Date > best.Date)) best = m;
                    if (best == null) return "Nothing worth telling. Quiet times.";
                    return (best.Source == MemorySource.Learned ? "I heard that " : best.Source == MemorySource.Witnessed ? "I saw it myself: " : "We lived through it: ") + NewsText(world, best) + ".";
                case ConversationTopic.Rumours:
                    NpcKnowledgeData rumour = null;
                    foreach (var k in npc.Knowledge)
                        if (k.Hops > 0 && (rumour == null || k.Confidence > rumour.Confidence)) rumour = k;
                    if (rumour == null) return "I don't hold with gossip. Or nobody tells me any.";
                    return "Word is " + RumourText(world, rumour) + " — or so " + PersonName(world, rumour.SourceNpcId.ToString()) + " told me" +
                           (rumour.Confidence < 50 ? ", though I'd not swear to it." : ".");
                case ConversationTopic.Person:
                    var who = world.FindNpc(about);
                    if (who == null) return "Who?";
                    if (who.NpcId == npc.NpcId) return "That's me.";
                    var seen = NpcMind.Find(npc, KnowledgeTopic.Whereabouts, about.ToString());
                    var tie = world.FindNpcRelation(npc.NpcId, about);
                    var opinion = tie == null ? string.Empty : tie.Affinity >= 40 ? " A good friend." : tie.Affinity <= -35 ? " Can't stand them." : string.Empty;
                    if (seen == null) return (tie == null ? "Never met " + who.Name + "." : "I know " + who.Name + ", but I couldn't say where they are.") + opinion;
                    var place = Guid.TryParse(seen.Value, out var pl) ? world.FindLocation(new LocationId(pl))?.Name : null;
                    return "Last I saw " + who.Name + ", they were at the " + (place ?? "usual place") + " — " + Ago(world, seen.LearnedOn) + "." + opinion;
                case ConversationTopic.Shops:
                    return TradeTalk.Shops(world, npc, here);
                case ConversationTopic.Goods:
                    return TradeTalk.Goods(world, npc, here);
                case ConversationTopic.Prices:
                    return TradeTalk.Prices(world, npc, here);
                case ConversationTopic.Factions:
                    return TradeTalk.Factions(world, npc, here);
                case ConversationTopic.Work:
                    return TradeTalk.Work(world, npc);
                default:
                    return Greeting(world, npc, playerName);
            }
        }

        private static string Greeting(WorldData world, NpcData npc, string playerName)
        {
            var mood = npc.Condition.Morale;
            var opener = mood >= 70 ? "Good day to you" : mood >= 45 ? "Well met" : mood >= 25 ? "What is it" : "Leave me be, unless it's important";
            return opener + (npc.HasMetPlayer ? ", " + playerName : string.Empty) + ". I'm " + npc.Name + ", " + Article(npc.Role.ToString().ToLowerInvariant()) + ".";
        }

        private static string Themselves(WorldData world, NpcData npc)
        {
            var work = world.FindLocation(npc.WorkplaceId);
            NpcGoalData top = null;
            foreach (var g in npc.Goals) if (g.IsActive && (top == null || g.Priority > top.Priority)) top = g;
            var c = npc.Condition;
            var feeling = c.Satiation < 30 ? "I'm hungry, truth be told" : c.Health < 50 ? "I've been poorly" : c.Morale >= 70 ? "Life's treating me well" : c.Morale >= 45 ? "Can't complain" : "It's been a hard season";
            return "I'm " + Article(npc.Role.ToString().ToLowerInvariant()) + (work != null ? ", at the " + work.Name : string.Empty) + ". " + feeling + "." +
                   (top != null ? " Mostly I'm trying to " + NpcText.Goal(top.Type.ToString()) + "." : string.Empty);
        }

        internal static string Hedge(NpcKnowledgeData k) =>
            k.Status == KnowledgeStatus.Known ? string.Empty : k.Status == KnowledgeStatus.Believed ? "last I knew, " : "I've heard ";

        internal static string NewsText(WorldData world, NpcMemoryData m)
        {
            var place = world.FindSettlement(m.SettlementId)?.Name ?? "these parts";
            switch (m.Subject)
            {
                case nameof(HistoricalEventType.FoodShortageBegan): return "food ran short in " + place;
                case nameof(HistoricalEventType.FoodShortageEnded): return "the granaries of " + place + " filled again";
                case nameof(HistoricalEventType.StarvationBegan): return "people starved in " + place;
                case nameof(HistoricalEventType.FoodReservesExhausted): return "the stores of " + place + " ran dry";
                case nameof(HistoricalEventType.NpcDied): return (m.NpcIds.Count > 0 ? world.FindNpc(m.NpcIds[0])?.Name ?? "someone" : "someone") + " died";
                case nameof(HistoricalEventType.TradeShipmentDelivered): return "a shipment reached " + place;
                case nameof(HistoricalEventType.ProsperityChanged): return (m.Category == MemoryCategory.Prosperity ? "times got better in " : "times got harder in ") + place;
                case nameof(HistoricalEventType.StabilityChanged): return (m.Category == MemoryCategory.Prosperity ? "things settled down in " : "there was unrest in ") + place;
                case nameof(HistoricalEventType.SettlementGrew): return place + " has grown";
                case nameof(HistoricalEventType.EconomicMilestone): return place + "'s treasury is fuller than ever";
                case nameof(HistoricalEventType.FactionInfluenceChanged): return "the factions' fortunes shifted";
                case nameof(HistoricalEventType.AgreementBroken): return "an agreement between the factions was broken";
                case nameof(HistoricalEventType.AgreementEstablished): return "the factions came to an agreement";
                default: return (m.Subject ?? "something happened").ToLowerInvariant().Replace('_', ' ') + " (" + place + ")";
            }
        }

        private static string RumourText(WorldData world, NpcKnowledgeData k)
        {
            var subject = Guid.TryParse(k.SubjectId, out var g) ? g : Guid.Empty;
            switch (k.Topic)
            {
                case KnowledgeTopic.SettlementFood: return (world.FindSettlement(new SettlementId(subject))?.Name ?? "a settlement") + " is " + NpcText.Food(k.Value);
                case KnowledgeTopic.SettlementProsperity: return (world.FindSettlement(new SettlementId(subject))?.Name ?? "a settlement") + " is " + k.Value;
                case KnowledgeTopic.SettlementGovernor: return FactionName(world, k.Value) + " governs " + (world.FindSettlement(new SettlementId(subject))?.Name ?? "somewhere");
                case KnowledgeTopic.FactionLeader: return PersonName(world, k.Value) + " leads " + FactionName(world, k.SubjectId);
                case KnowledgeTopic.Whereabouts:
                    var loc = Guid.TryParse(k.Value, out var l) ? world.FindLocation(new LocationId(l))?.Name : null;
                    return PersonName(world, k.SubjectId) + " is often at the " + (loc ?? "usual place");
                default: return "something happened (" + k.Value + ")";
            }
        }

        internal static string FactionName(WorldData world, string id) => Guid.TryParse(id, out var g) ? world.FindFaction(new FactionId(g))?.Name ?? "some faction" : "some faction";

        private static string PersonName(WorldData world, string id) => Guid.TryParse(id, out var g) ? world.FindNpc(new NpcId(g))?.Name ?? "someone" : "someone";

        private static string Ago(WorldData world, WorldDate when)
        {
            var days = when.IsValidDate ? when.DaysUntil(world.CurrentSimulationDate) : -1;
            return days < 0 ? "a while back" : days == 0 ? "today" : days == 1 ? "yesterday" : days < 14 ? days + " days ago" : "some weeks ago";
        }

        private static string Article(string noun) => ("aeiou".IndexOf(char.ToLowerInvariant(noun[0])) >= 0 ? "an " : "a ") + noun;

        internal static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
