using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application
{
    // Phase 12 (ADR-0056, ADR-0058, ADR-0060): every change the journal, the dialogue window and the world's objects ask
    // for is one of these commands. A UI button never edits the world.

    public sealed class StartConversationCommand : ICommand
    {
        public StartConversationCommand(NpcId npcId) { NpcId = npcId; }
        public NpcId NpcId { get; }
    }

    /// <summary>Chooses a response in the conversation in progress (by the response's ID).</summary>
    public sealed class ChooseResponseCommand : ICommand
    {
        public ChooseResponseCommand(string responseId) { ResponseId = responseId; }
        public string ResponseId { get; }
    }

    public sealed class EndConversationCommand : ICommand
    {
    }

    public sealed class AbandonQuestCommand : ICommand
    {
        public AbandonQuestCommand(string instanceId) { InstanceId = instanceId; }
        public string InstanceId { get; }
    }

    public sealed class TrackQuestCommand : ICommand
    {
        public TrackQuestCommand(string instanceId, bool tracked)
        {
            InstanceId = instanceId;
            Tracked = tracked;
        }

        public string InstanceId { get; }
        public bool Tracked { get; }
    }

    /// <summary>Examines a world object by its stable key ("ledger:{settlement}", "notice:{settlement}"): one command for every object kind.</summary>
    public sealed class InspectObjectCommand : ICommand
    {
        public InspectObjectCommand(string objectKey) { ObjectKey = objectKey; }
        public string ObjectKey { get; }
    }

    /// <summary>Reads an item the player carries (a letter, a book). Reading a sealed letter breaks its seal.</summary>
    public sealed class ReadItemCommand : ICommand
    {
        public ReadItemCommand(ItemStackId stackId) { StackId = stackId; }
        public ItemStackId StackId { get; }
    }

    /// <summary>Sets how content with a tag is presented (ADR-0061).</summary>
    public sealed class SetContentPresentationCommand : ICommand
    {
        public SetContentPresentationCommand(ContentTags tag, ContentPresentation presentation)
        {
            Tag = tag;
            Presentation = presentation;
        }

        public ContentTags Tag { get; }
        public ContentPresentation Presentation { get; }
    }

    public static class QuestCommandHandlers
    {
        public static void Register(CommandRegistry commands)
        {
            commands.Register<StartConversationCommand>(new StartHandler());
            commands.Register<ChooseResponseCommand>(new ChooseHandler());
            commands.Register<EndConversationCommand>(new EndHandler());
            commands.Register<AbandonQuestCommand>(new AbandonHandler());
            commands.Register<TrackQuestCommand>(new TrackHandler());
            commands.Register<InspectObjectCommand>(new InspectHandler());
            commands.Register<ReadItemCommand>(new ReadHandler());
            commands.Register<SetContentPresentationCommand>(new ContentHandler());
        }

        private static CommandResult Ready(CommandContext context, out PlayerCharacterData p)
        {
            p = null;
            if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
            p = context.Committed.Player;
            if (p == null) return CommandResult.NotFound("This world has no player character.");
            if (context.Committed.ActiveEncounter != null) return CommandResult.InvalidState("Not during a fight.");
            return null;
        }

        /// <summary>The person, alive and here and awake, or the reason why not (as Phase 4's conversation required).</summary>
        private static NpcData Present(WorldData w, NpcId id, out CommandResult failure)
        {
            failure = null;
            var npc = NpcCommandHandlers.Living(w, id, out failure);
            if (npc == null) return null;
            var p = w.Player;
            if (Realmkeeper.Application.Places.PlaceRules.Beside(w, npc)) return npc; // Phase 13: held, following, posted or at home in the same place
            if (p.IsInPlace) { failure = CommandResult.Invalid($"{npc.Name} is not here."); return null; }
            if (!p.IsInSettlement) { failure = CommandResult.InvalidState($"{p.Name} is not in a settlement."); return null; }
            var now = NpcActivityPlan.Now(w, npc);
            var here = NpcMind.SettlementAt(w, now.IsKnown ? now.Location : npc.CurrentLocationId)?.SettlementId ?? npc.HomeSettlementId;
            if (here != p.SettlementId) { failure = CommandResult.Invalid($"{npc.Name} is not here just now."); return null; }
            if (now.Activity == ActivityKind.Sleeping) { failure = CommandResult.InvalidState($"{npc.Name} is asleep."); return null; }
            return npc;
        }

        private sealed class StartHandler : ICommandHandler<StartConversationCommand>
        {
            public CommandResult Handle(StartConversationCommand c, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                var npc = Present(context.Committed, c.NpcId, out var failure);
                if (npc == null) return failure;
                var w = context.Edit();
                var e = w.FindNpc(npc.NpcId);
                var today = w.CurrentSimulationDate;
                var first = !e.HasMetPlayer;
                e.ConversationCount++;
                PlayerCommandHandlers.Acquaint(e, today, false);
                if (first)
                {
                    e.MetPlayerOn = today;
                    context.ReportEvent(HistoricalEventType.PlayerMetResident, EventSeverity.Info, EventImportance.Minor, new[] { w.Player.SettlementId.IsEmpty ? e.HomeSettlementId : w.Player.SettlementId },
                        new Dictionary<string, string> { ["source"] = "player", ["npc"] = e.Name, ["role"] = e.Role.ToString(), ["player"] = player.Name }, null, new[] { e.NpcId });
                }

                var conv = new ConversationData
                {
                    Key = "conv:" + e.NpcId + ":" + DialogueEngine.Now(w) + ":" + e.ConversationCount.ToString(CultureInfo.InvariantCulture),
                    NpcId = e.NpcId, DialogueId = DialogueCatalog.General, NodeId = "start", StartedAt = QuestClock.Now(w),
                };
                w.Conversation = conv;
                var line = DialogueEngine.Line(w, conv, e);
                conv.Say(e.Name, line);
                QuestRuntime.Know(w, e.HomeSettlementId.ToString());
                context.Quest(new QuestEvent { Kind = QuestEventKind.Talked, Subject = e.NpcId.ToString(), Settlement = e.HomeSettlementId });
                return CommandResult.Ok(line);
            }
        }

        private sealed class ChooseHandler : ICommandHandler<ChooseResponseCommand>
        {
            public CommandResult Handle(ChooseResponseCommand cmd, CommandContext context)
            {
                var refused = Ready(context, out _);
                if (refused != null) return refused;
                var committed = context.Committed;
                if (committed.Conversation == null) return CommandResult.InvalidState("You are not talking to anyone.");
                var npc0 = Present(committed, committed.Conversation.NpcId, out var failure);
                if (npc0 == null) return failure;
                var options = DialogueEngine.Responses(committed, committed.Conversation, npc0);
                DialogueResponse chosen = null;
                foreach (var (r, _) in options) if (r.Id == cmd.ResponseId) chosen = r;
                if (chosen == null) return CommandResult.Invalid("That is not something you can say now.");

                var w = context.Edit();
                var conv = w.Conversation;
                var npc = w.FindNpc(conv.NpcId);
                var player = w.Player;
                var events = new List<Realmkeeper.Simulation.Quests.QuestEvent>();
                conv.Say(player.Name, DialogueEngine.Fill(w, conv, npc, chosen.Text));
                bool? success = null;
                var outcome = string.Empty;
                if (chosen.Check != null)
                {
                    var (roll, total, dc, ok) = DialogueEngine.Roll(context, w, conv, npc, chosen.Check);
                    success = ok;
                    outcome = $" [{chosen.Check.Skill}: d20 {roll} → {total} vs DC {dc}: {(ok ? "success" : "failure")}]";
                    conv.Say("check", $"{chosen.Check.Skill} {total} vs {dc} — {(ok ? "success" : "failure")}");
                }

                if (chosen.Once) Remember(player.Social, "once:" + conv.NpcId + ":" + conv.DialogueId + ":" + conv.NodeId + ":" + chosen.Id + ":" + (conv.QuestInstanceId ?? string.Empty));
                var turn = conv.Turn;
                for (var i = 0; i < chosen.Consequences.Count; i++)
                {
                    var x = chosen.Consequences[i];
                    if (x.OnSuccess.HasValue && success.HasValue && x.OnSuccess.Value != success.Value) continue;
                    var key = conv.Key + ":" + turn.ToString(CultureInfo.InvariantCulture) + ":" + i.ToString(CultureInfo.InvariantCulture);
                    var problem = DialogueEngine.Apply(context, w, conv, npc, x, key, events);
                    if (problem != null) return problem;
                    if (w.Conversation == null) break; // a fight began
                }

                // Quest progress from this choice now, so the next line already knows it (and the session does not count it again).
                var questFailure = QuestRuntime.Apply(context, events);
                if (questFailure != null) return questFailure;
                if (w.Conversation == null) return CommandResult.Ok("The conversation ends." + outcome);

                // Where the conversation goes next.
                var next = success == false && chosen.NextOnFailure != null ? chosen.NextOnFailure : chosen.Next;
                conv.Turn++;
                if (next == "end" || chosen.Kind == ResponseKind.Trade)
                {
                    w.Conversation = null;
                    return CommandResult.Ok("The conversation ends." + outcome);
                }

                if (next != null && next.StartsWith(DialogueEngine.DialoguePrefix, StringComparison.Ordinal))
                {
                    // Phase 13: a dialogue of its own (a gate's watch): "@dialogue:{id}:{node}".
                    var rest = next.Substring(DialogueEngine.DialoguePrefix.Length);
                    var colon = rest.LastIndexOf(':');
                    var dlg = DialogueCatalog.Find(rest.Substring(0, colon));
                    if (dlg == null) return CommandResult.Fail("That conversation is missing.");
                    conv.DialogueId = dlg.Id;
                    conv.QuestInstanceId = null;
                    conv.NodeId = rest.Substring(colon + 1);
                }
                else if (next != null && next.StartsWith(DialogueEngine.HookPrefix, StringComparison.Ordinal))
                {
                    var rest = next.Substring(DialogueEngine.HookPrefix.Length);
                    var colon = rest.LastIndexOf(':');
                    var instance = w.FindQuest(rest.Substring(0, colon));
                    var dlg = DialogueCatalog.Find(QuestCatalog.Find(instance?.DefinitionId)?.DialogueId);
                    if (instance == null || dlg == null) return CommandResult.Fail("That quest's conversation is missing.");
                    conv.DialogueId = dlg.Id;
                    conv.QuestInstanceId = instance.InstanceId;
                    conv.NodeId = rest.Substring(colon + 1);
                    if (instance.State == QuestState.Available) DialogueEngine.Offer(w, instance, QuestRuntime.Reporter(context));
                }
                else if (next == null)
                {
                    conv.DialogueId = DialogueCatalog.General;
                    conv.QuestInstanceId = null;
                    conv.NodeId = "start";
                }
                else conv.NodeId = next;

                var line = DialogueEngine.Line(w, conv, npc);
                conv.Say(npc.Name, line);
                return CommandResult.Ok(line + outcome);
            }

            private static void Remember(PlayerSocialData social, string key)
            {
                if (social.AppliedKeys.Contains(key)) return;
                social.AppliedKeys.Add(key);
                while (social.AppliedKeys.Count > PlayerSocialData.MaxAppliedKeys) social.AppliedKeys.RemoveAt(0);
            }
        }

        private sealed class EndHandler : ICommandHandler<EndConversationCommand>
        {
            public CommandResult Handle(EndConversationCommand c, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                if (context.Committed.Conversation == null) return CommandResult.Ok("No conversation.");
                context.Edit().Conversation = null;
                return CommandResult.Ok("The conversation ends.");
            }
        }

        private sealed class AbandonHandler : ICommandHandler<AbandonQuestCommand>
        {
            public CommandResult Handle(AbandonQuestCommand c, CommandContext context)
            {
                var refused = Ready(context, out _);
                if (refused != null) return refused;
                var q = context.Committed.FindQuest(c.InstanceId);
                if (q == null) return CommandResult.NotFound("No such quest.");
                if (!q.IsOpen) return CommandResult.InvalidState("Only a quest in progress can be abandoned.");
                var w = context.Edit();
                var eq = w.FindQuest(c.InstanceId);
                QuestLifecycle.Move(w, eq, QuestState.Abandoned, "given up", QuestRuntime.Reporter(context));
                var giver = QuestEffects.Person(w, eq, "giver");
                if (giver != null) Reputation.Change(w, "npc:" + giver.NpcId, "Trust", -10, eq.InstanceId + ":abandoned", "abandoned " + QuestCatalog.Find(eq.DefinitionId)?.Name, QuestRuntime.Reporter(context));
                return CommandResult.Ok($"Abandoned {QuestText.Fill(w, eq, QuestCatalog.Find(eq.DefinitionId)?.Name)}.");
            }
        }

        private sealed class TrackHandler : ICommandHandler<TrackQuestCommand>
        {
            public const int MaxTracked = 3;

            public CommandResult Handle(TrackQuestCommand c, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var q = context.Committed.FindQuest(c.InstanceId);
                if (q == null) return CommandResult.NotFound("No such quest.");
                if (c.Tracked && !q.IsOpen) return CommandResult.InvalidState("Only a quest in progress can be tracked.");
                var tracked = 0;
                foreach (var x in context.Committed.Quests) if (x.Tracked && x.InstanceId != q.InstanceId) tracked++;
                if (c.Tracked && tracked >= MaxTracked) return CommandResult.InvalidState($"At most {MaxTracked} quests can be tracked at once.");
                context.Edit().FindQuest(c.InstanceId).Tracked = c.Tracked;
                return CommandResult.Ok(c.Tracked ? "Tracking it." : "No longer tracked.");
            }
        }

        // ----- World objects (ADR-0060) -----------------------------------------------------------------

        /// <summary>The kinds of object a key names, and what examining each does. One command, one switch on the key's kind.</summary>
        private sealed class InspectHandler : ICommandHandler<InspectObjectCommand>
        {
            public CommandResult Handle(InspectObjectCommand c, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                var key = c.ObjectKey ?? string.Empty;
                var colon = key.IndexOf(':');
                if (colon < 0 || !Guid.TryParse(key.Substring(colon + 1), out var sg)) return CommandResult.Invalid("Unknown object.");
                var kind = key.Substring(0, colon);
                var sid = new SettlementId(sg);
                var s = context.Committed.FindSettlement(sid);
                if (s == null) return CommandResult.NotFound("Unknown place.");
                if (!player.IsInSettlement || player.SettlementId != sid) return CommandResult.InvalidState($"That is in {s.Name}.");
                var w = context.Edit();
                var report = QuestRuntime.Reporter(context);
                string text;
                switch (kind)
                {
                    case "ledger":
                    {
                        var food = s.ResourceInventory.Get(Domain.Economy.ResourceTypes.Food);
                        text = $"The granary ledger of {s.Name}: {food.ToString("N0", CultureInfo.InvariantCulture)} measures in store.";
                        foreach (var q in w.Quests)
                        {
                            if (q.DefinitionId != QuestCatalog.GranaryShortfall || q.Bound("settlement") != sid.ToString() || q.IsClosed) continue;
                            text += $" The tally for the last fortnight is short by {q.Variables.GetValueOrDefault("missing")} measures, and the last entry is in a hurried hand that is not the keeper's.";
                            QuestRuntime.Discover(w, "ledger_short:" + sid, $"The {s.Name} ledger is short by {q.Variables.GetValueOrDefault("missing")} measures; the last entry is not in the keeper's hand.", "object:" + key, report);
                            if (q.State == QuestState.Available) QuestLifecycle.Move(w, q, QuestState.Discovered, "found in the ledger", report);
                        }

                        break;
                    }
                    case "notice":
                    {
                        var notices = new List<string>();
                        foreach (var q in w.Quests)
                        {
                            var def = QuestCatalog.Find(q.DefinitionId);
                            var giver = QuestEffects.Person(w, q, "giver");
                            if (def == null || giver == null || giver.HomeSettlementId != sid) continue;
                            if (q.State != QuestState.Available && q.State != QuestState.Discovered && q.State != QuestState.Offered) continue;
                            if (def.Category != QuestCategory.Emergent && def.Category != QuestCategory.Hunting && def.Category != QuestCategory.BountyContract && def.Category != QuestCategory.CraftingGathering && def.Category != QuestCategory.Settlement) continue;
                            notices.Add($"\"{QuestText.Fill(w, q, def.Name)}\" — {QuestText.Fill(w, q, def.Description)} Ask {giver.Name}.");
                            if (q.State == QuestState.Available) QuestLifecycle.Move(w, q, QuestState.Discovered, "read on the notice board", report);
                        }

                        text = notices.Count == 0 ? $"The notice board in {s.Name}: nothing posted but old labour notices." : $"The notice board in {s.Name}: " + string.Join(" ", notices);
                        break;
                    }
                    default:
                        return CommandResult.Invalid("Unknown object.");
                }

                QuestRuntime.Visit(w, sid);
                context.Quest(new QuestEvent { Kind = QuestEventKind.Investigated, Subject = key, Settlement = sid });
                return CommandResult.Ok(text);
            }
        }

        private sealed class ReadHandler : ICommandHandler<ReadItemCommand>
        {
            public CommandResult Handle(ReadItemCommand c, CommandContext context)
            {
                var refused = Ready(context, out var player);
                if (refused != null) return refused;
                var i = PlayerCommandHandlers.IndexOf(player, c.StackId);
                if (i < 0) return CommandResult.NotFound("You are not carrying that.");
                var stack = player.Inventory[i];
                var def = ItemCatalog.Find(stack.ItemId);
                if (def == null || (def.Category != ItemCategory.Book && def.Category != ItemCategory.Quest)) return CommandResult.Invalid($"There is nothing to read on {def?.Name.ToLowerInvariant() ?? "that"}.");
                var w = context.Edit();
                var report = QuestRuntime.Reporter(context);
                var es = w.Player.Inventory[i];
                if (def.Id == "sealed_letter")
                {
                    foreach (var q in w.Quests)
                    {
                        if (q.DefinitionId != QuestCatalog.SealedLetter || !q.Variables.TryGetValue("letter_stack", out var id) || id != es.StackId.ToString()) continue;
                        var opened = q.Variables.GetValueOrDefault("opened") == "yes";
                        q.Variables["opened"] = "yes";
                        es.Provenance = (es.Provenance ?? "A letter") + " — seal broken";
                        var text = QuestText.Fill(w, q, "{giver} writes to {recipient} about money owed between them since last harvest, and asks, in a careful hand, that it be settled before the spring fairs — and that nobody else hear of it.");
                        q.Variables["letter_contents_text"] = text;
                        QuestRuntime.Discover(w, "letter_contents", text, "item:" + es.StackId, report);
                        context.Quest(new QuestEvent { Kind = QuestEventKind.Discovered, Subject = "letter_contents" });
                        if (!opened) q.Note(QuestClock.Now(w), "You broke the seal.");
                        return CommandResult.Ok("You break the seal and read: " + text);
                    }

                    return CommandResult.Ok("The letter is addressed to someone you do not know; you leave the seal alone.");
                }

                var read = def.Id == "almanac"
                    ? $"An almanac of {w.CurrentSimulationDate.Year}: sowing and reaping days, fair days, and the phases of the moon."
                    : def.Description;
                QuestRuntime.Discover(w, "read:" + def.Id, read, "item:" + def.Id);
                return CommandResult.Ok(read);
            }
        }

        private sealed class ContentHandler : ICommandHandler<SetContentPresentationCommand>
        {
            public CommandResult Handle(SetContentPresentationCommand c, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                if (c.Tag == ContentTags.None || !Enum.IsDefined(typeof(ContentTags), c.Tag)) return CommandResult.Invalid("Choose one content tag.");
                context.Edit().WorldSettings.Content.Rules[c.Tag] = c.Presentation;
                return CommandResult.Ok($"{c.Tag}: {c.Presentation}.");
            }
        }
    }
}
