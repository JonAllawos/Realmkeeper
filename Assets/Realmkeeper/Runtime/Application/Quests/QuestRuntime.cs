using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application
{
    /// <summary>
    /// The quest engine inside a command's transaction (ADR-0057): applies the command's quest events to the open quests,
    /// re-checks state objectives, fails what has failed, and completes what is done — with rewards through the existing
    /// paths (the experience ledger, the purse, the item factory and holder rules, the reputation ledger). Everything is
    /// keyed: a quest completes once, a reward is given once, a consequence happens once.
    /// </summary>
    public static class QuestRuntime
    {
        /// <summary>A reporter that records Chronicle events through the command (they commit with it).</summary>
        public static QuestReporter Reporter(CommandContext context) => (type, severity, importance, settlements, facts, npcs) =>
            context.ReportEvent(type, severity, importance, settlements, facts, null, npcs);

        /// <summary>Applies the command's quest events; returns a failure (the whole command then fails) or null.</summary>
        public static CommandResult Process(CommandContext context) => Apply(context, context.QuestEvents);

        /// <summary>Applies events now (a command that must see the result before it finishes — a dialogue choice — calls this itself).</summary>
        public static CommandResult Apply(CommandContext context, IEnumerable<QuestEvent> events)
        {
            var w = context.Edit();
            var report = Reporter(context);
            var queue = new Queue<QuestEvent>(events);
            var guard = 0;
            while (queue.Count > 0 && guard++ < 64)
            {
                var e = queue.Dequeue();
                foreach (var q in w.Quests)
                {
                    if (!q.IsOpen) continue;
                    QuestObjectives.Observe(w, q, e);
                }

                // Phase 17 (ADR-0087): companions arriving where their own people live say so.
                if (e.Kind == QuestEventKind.ReachedSettlement && Guid.TryParse(e.Subject, out var reached))
                    Simulation.Party.CompanionStories.Arrived(w, new SettlementId(reached));

                foreach (var q in new List<QuestInstanceData>(w.Quests))
                {
                    if (!q.IsOpen) continue;
                    Refresh(w, q);
                    QuestObjectives.CheckTimers(w, q);
                    var why = QuestLifecycle.FailureNow(w, q);
                    if (why != null)
                    {
                        QuestLifecycle.Move(w, q, QuestState.Failed, why, report);
                        continue;
                    }

                    var def = QuestCatalog.Find(q.DefinitionId);
                    if (def == null || !QuestObjectives.AllDone(def, q)) continue;
                    var failure = Complete(context, w, q, def, report);
                    if (failure != null) return failure;
                    queue.Enqueue(new QuestEvent { Kind = QuestEventKind.QuestCompleted, Subject = def.Id });
                }
            }

            return null;
        }

        /// <summary>State objectives (ADR-0057): re-read from the world when an event arrives, never per frame.</summary>
        public static void Refresh(WorldData w, QuestInstanceData q)
        {
            var def = QuestCatalog.Find(q.DefinitionId);
            if (def == null) return;
            var now = QuestClock.Now(w);
            foreach (var o in def.Objectives)
            {
                var p = q.Progress(o.Id);
                if (p == null || p.Done || p.Failed || p.Unavailable) continue;
                var ctx = new ConditionContext(w, null, q, null);
                foreach (var c in o.UnavailableWhen)
                    if (QuestConditions.Holds(ctx, c))
                    {
                        p.Unavailable = true;
                        q.Note(now, "No longer possible: " + QuestText.Fill(w, q, o.Text));
                    }

                if (p.Unavailable || !QuestObjectives.StageOpen(def, q, o.Stage)) continue;
                int have;
                switch (o.Kind)
                {
                    case ObjectiveKind.Collect:
                    case ObjectiveKind.Acquire:
                        have = ItemCommandHandlers.CountInPack(w.Player, ResolveItem(q, o.Target));
                        break;
                    case ObjectiveKind.FactionApproval:
                        var npc = QuestEffects.Person(w, q, o.Target?.StartsWith("role:", StringComparison.Ordinal) == true ? o.Target.Substring(5) : "giver");
                        have = npc == null ? 0 : w.Player.Social.StandingWith(npc.FactionId) >= o.Quantity ? o.Quantity : 0;
                        break;
                    case ObjectiveKind.ResolveSettlementProblem:
                        have = QuestCauses.Holds(w, q) ? 0 : o.Quantity;
                        break;
                    default:
                        continue;
                }

                p.Count = Math.Min(o.Quantity, have);
                if (p.Count >= o.Quantity)
                {
                    p.Done = true;
                    p.Revealed = true;
                    p.DoneAt = now;
                    if (o.Outcome != null && q.Outcome == null) q.Outcome = o.Outcome;
                    q.Note(now, "Done: " + QuestText.Fill(w, q, o.Text));
                }
            }

            QuestObjectives.RevealOpenStages(def, q);
        }

        private static string ResolveItem(QuestInstanceData q, string target)
        {
            if (target == null) return null;
            if (target.StartsWith("role:", StringComparison.Ordinal)) return q.Bound(target.Substring(5));
            if (target.StartsWith("item:", StringComparison.Ordinal)) return target.Substring(5);
            return target;
        }

        /// <summary>Completes a quest (atomic with the command): rewards, completion consequences, follow-ups, then the state.</summary>
        internal static CommandResult Complete(CommandContext context, WorldData w, QuestInstanceData q, QuestDefinition def, QuestReporter report)
        {
            if (q.State == QuestState.Completed || q.RewardsClaimed) return null;
            var failure = GiveRewards(context, w, q, def, report);
            if (failure != null) return failure;
            QuestEffects.ApplyAll(w, q, ConsequenceTiming.OnComplete, report);
            foreach (var next in def.FollowUps)
            {
                var nd = QuestCatalog.Find(next);
                if (nd != null) QuestLifecycle.Create(w, nd, new Dictionary<string, string>(q.Bindings), "after:" + q.InstanceId, null, report, out _);
            }

            QuestLifecycle.Move(w, q, QuestState.Completed, q.Outcome == null ? null : "ended: " + q.Outcome, report);
            return null;
        }

        /// <summary>Rewards through the existing authoritative paths, each once (ADR-0056).</summary>
        public static CommandResult GiveRewards(CommandContext context, WorldData w, QuestInstanceData q, QuestDefinition def, QuestReporter report)
        {
            var p = w.Player;
            var now = QuestClock.Now(w);
            for (var i = 0; i < def.Rewards.Count; i++)
            {
                var r = def.Rewards[i];
                if (r.Outcome != null && r.Outcome != q.Outcome) continue;
                var key = q.InstanceId + ":reward:" + i.ToString(CultureInfo.InvariantCulture);
                switch (r.Kind)
                {
                    case RewardKind.Experience:
                        if (p.Rpg == null || p.Rpg.State != CreationState.Complete || p.Rpg.IsDead) break;
                        var xpKey = "quest:" + q.InstanceId + ":" + i.ToString(CultureInfo.InvariantCulture);
                        var had = false;
                        foreach (var a in p.Rpg.ExperienceLog) if (a.Key == xpKey) had = true;
                        if (had) break;
                        p.Rpg.ExperienceLog.Add(new ExperienceAward { Key = xpKey, Source = ExperienceSource.Quest, SourceId = q.InstanceId, Amount = r.Amount, Date = w.CurrentSimulationDate, Reason = def.Name });
                        p.Rpg.Experience += r.Amount;
                        q.Note(now, $"+{r.Amount} experience.");
                        break;
                    case RewardKind.Coin:
                    {
                        var amount = r.Amount;
                        if (r.Key != null && r.Key.StartsWith("var:", StringComparison.Ordinal) && q.Variables.TryGetValue(r.Key.Substring(4), out var v) && int.TryParse(v, out var n)) amount += n;
                        if (q.Variables.TryGetValue("bonus", out var b) && int.TryParse(b, out var bonus) && i == FirstCoin(def, q)) amount += bonus;
                        if (amount <= 0) break;
                        p.Purse += amount;
                        q.Note(now, $"+{amount} coin.");
                        break;
                    }
                    case RewardKind.Item:
                    {
                        var item = ItemCatalog.Find(r.Key);
                        if (item == null) return CommandResult.Fail($"The reward '{r.Key}' is not an item.");
                        var heavy = ItemCommandHandlers.WeightProblem(p, item.WeightGrams * Math.Max(1, r.Amount));
                        if (heavy != null) return CommandResult.InvalidState(heavy + " Make room for your reward first; nothing was changed.");
                        ItemCommandHandlers.AddStack(p, ItemStacks.Create(ItemCommandHandlers.NewStackId(context), item, Math.Max(1, r.Amount), "Reward: " + def.Name, now));
                        q.Note(now, $"Received {item.Name.ToLowerInvariant()}.");
                        break;
                    }
                    case RewardKind.Regard:
                    {
                        var npc = QuestEffects.Person(w, q, r.Role);
                        if (npc != null) Reputation.Change(w, "npc:" + npc.NpcId, r.Key, r.Amount, key, def.Name, report);
                        break;
                    }
                    case RewardKind.FactionStanding:
                    {
                        var npc = QuestEffects.Person(w, q, r.Role);
                        if (npc != null && !npc.FactionId.IsEmpty) Reputation.Change(w, "faction:" + npc.FactionId, "Standing", r.Amount, key, def.Name, report);
                        break;
                    }
                    case RewardKind.SettlementReputation:
                    {
                        var s = QuestEffects.SettlementOf(w, q, r.Role ?? "settlement");
                        if (s != null) Reputation.Change(w, "settlement:" + s.SettlementId, "Reputation", r.Amount, key, def.Name, report);
                        break;
                    }
                    case RewardKind.Information:
                        Discover(w, r.Key, QuestText.Fill(w, q, r.Shown ?? q.Variables.GetValueOrDefault(r.Key + "_text") ?? r.Key), "quest:" + q.InstanceId, report);
                        break;
                    case RewardKind.Location:
                        Know(w, q.Bound(r.Role));
                        break;
                    case RewardKind.FollowUpQuest:
                    {
                        var nd = QuestCatalog.Find(r.Key);
                        if (nd != null) QuestLifecycle.Create(w, nd, new Dictionary<string, string>(q.Bindings), "after:" + q.InstanceId, null, report, out _);
                        break;
                    }
                    case RewardKind.MerchantAccess:
                        Discover(w, "access:" + q.Bound(r.Role), QuestText.Fill(w, q, r.Shown ?? "{giver} will trade with you."), "quest:" + q.InstanceId, report);
                        break;
                }
            }

            q.RewardsClaimed = true;
            return null;
        }

        private static int FirstCoin(QuestDefinition def, QuestInstanceData q)
        {
            for (var i = 0; i < def.Rewards.Count; i++)
                if (def.Rewards[i].Kind == RewardKind.Coin && (def.Rewards[i].Outcome == null || def.Rewards[i].Outcome == q.Outcome)) return i;
            return -1;
        }

        // ----- Discovery (ADR-0060) -------------------------------------------------------------------

        /// <summary>The player learns a fact, once (by key). Important discoveries reach the Chronicle.</summary>
        public static bool Discover(WorldData w, string key, string text, string source, QuestReporter report = null)
        {
            var social = w.Player?.Social;
            if (social == null || key == null || social.Knows(key)) return false;
            social.Discoveries.Add(new DiscoveryData { Key = key, Text = text, At = QuestClock.Now(w), Source = source });
            while (social.Discoveries.Count > PlayerSocialData.MaxDiscoveries) social.Discoveries.RemoveAt(0);
            report?.Invoke(HistoricalEventType.PlayerDiscovery, EventSeverity.Info, EventImportance.Minor, w.Player.IsInSettlement ? new[] { w.Player.SettlementId } : Array.Empty<SettlementId>(),
                new Dictionary<string, string> { ["source"] = "player", ["key"] = key, ["what"] = text }, Array.Empty<NpcId>());
            return true;
        }

        /// <summary>The player now knows of a place (a settlement, a location, a person's whereabouts) — not that they have been there.</summary>
        public static void Know(WorldData w, string placeId)
        {
            var social = w.Player?.Social;
            if (social == null || string.IsNullOrEmpty(placeId) || social.KnownPlaces.Contains(placeId)) return;
            social.KnownPlaces.Add(placeId);
        }

        public static void Visit(WorldData w, SettlementId s)
        {
            var social = w.Player?.Social;
            if (social == null || s.IsEmpty) return;
            if (!social.VisitedSettlements.Contains(s)) social.VisitedSettlements.Add(s);
            Know(w, s.ToString());
        }

        public static void Rumor(WorldData w, string key, string text, NpcId source, int confidence)
        {
            var social = w.Player?.Social;
            if (social == null || key == null) return;
            foreach (var r in social.Rumors) if (r.Key == key) return;
            social.Rumors.Add(new RumorData { Key = key, Text = text, At = QuestClock.Now(w), SourceNpcId = source, Confidence = confidence });
            while (social.Rumors.Count > PlayerSocialData.MaxRumors) social.Rumors.RemoveAt(0);
        }
    }

    /// <summary>What a condition is evaluated against: the world, the person spoken to, the quest, the conversation.</summary>
    public sealed class ConditionContext
    {
        public ConditionContext(WorldData world, NpcData npc, QuestInstanceData quest, ConversationData conversation)
        {
            World = world;
            Npc = npc;
            Quest = quest;
            Conversation = conversation;
        }

        public WorldData World { get; }
        public NpcData Npc { get; }
        public QuestInstanceData Quest { get; }
        public ConversationData Conversation { get; }

        /// <summary>Phase 17: the campaign the conditions are read against — the quest's own, else the one open campaign.</summary>
        public Domain.Campaigns.CampaignInstanceData Campaign =>
            Simulation.Campaigns.CampaignEngine.OfQuest(World, Quest) ?? Simulation.Campaigns.CampaignEngine.Sole(World);
    }

    /// <summary>The condition evaluator shared by quests and dialogue (ADR-0058).</summary>
    public static class QuestConditions
    {
        public static bool All(ConditionContext c, IReadOnlyList<QuestCondition> conditions)
        {
            if (conditions == null) return true;
            foreach (var x in conditions) if (!Holds(c, x)) return false;
            return true;
        }

        public static bool Holds(ConditionContext c, QuestCondition x)
        {
            var result = Raw(c, x);
            return x.Negate ? !result : result;
        }

        private static bool InRange(QuestCondition x, int v) => v >= x.Min && v <= x.Max;

        private static bool Raw(ConditionContext c, QuestCondition x)
        {
            var w = c.World;
            var p = w.Player;
            switch (x.Kind)
            {
                case ConditionKind.Always: return true;
                case ConditionKind.QuestState:
                {
                    QuestInstanceData q = null;
                    if (x.Key == "this" || x.Key == null) q = c.Quest;
                    else foreach (var i in w.Quests) if (i.DefinitionId == x.Key) q = i;
                    var state = q?.State.ToString() ?? "None";
                    foreach (var v in (x.Value ?? string.Empty).Split('|')) if (v == state) return true;
                    return false;
                }
                case ConditionKind.Decision:
                    return c.Quest != null && c.Quest.Decisions.Contains(x.Key);
                case ConditionKind.Regard:
                    return c.Npc != null && InRange(x, Reputation.Dimension(c.Npc.PlayerRegard, x.Key));
                case ConditionKind.FactionStanding:
                {
                    var who = x.Key == null || x.Key == "npc" ? c.Npc : QuestEffects.Person(w, c.Quest, x.Key);
                    return who != null && !who.FactionId.IsEmpty && InRange(x, p.Social.StandingWith(who.FactionId));
                }
                case ConditionKind.HasItem:
                {
                    var need = x.Min == int.MinValue ? 1 : x.Min;
                    if (x.Key == "@food")
                    {
                        var n = 0;
                        foreach (var s in p.Inventory) if (QuestObjectives.IsFood(s.ItemId)) n += s.Quantity;
                        return n >= need;
                    }

                    if (x.Key != null && x.Key.StartsWith("var:", StringComparison.Ordinal))
                    {
                        if (c.Quest == null || !c.Quest.Variables.TryGetValue(x.Key.Substring(4), out var stack)) return false;
                        foreach (var s in p.Inventory) if (s.StackId.ToString() == stack) return true;
                        return false;
                    }

                    var item = x.Key != null && x.Key.StartsWith("role:", StringComparison.Ordinal) ? c.Quest?.Bound(x.Key.Substring(5)) : x.Key;
                    return ItemCommandHandlers.CountInPack(p, item) >= need;
                }
                case ConditionKind.TimeOfDay:
                    return InRange(x, w.MinuteOfDay);
                case ConditionKind.NpcActivity:
                    return c.Npc != null && NpcActivityPlan.Now(w, c.Npc).Activity.ToString() == x.Value;
                case ConditionKind.NpcTrait:
                    return c.Npc != null && Enum.TryParse<NpcTrait>(x.Key, out var trait) && InRange(x, c.Npc.Personality[trait]);
                case ConditionKind.NpcKnows:
                {
                    if (c.Npc == null || !Enum.TryParse<KnowledgeTopic>(x.Key, out var topic)) return false;
                    var subject = x.Value != null && x.Value.StartsWith("role:", StringComparison.Ordinal) ? c.Quest?.Bound(x.Value.Substring(5)) : x.Value;
                    foreach (var k in c.Npc.Knowledge) if (k.Topic == topic && (subject == null || k.SubjectId == subject)) return true;
                    return false;
                }
                case ConditionKind.NpcRemembers:
                {
                    if (c.Npc == null) return false;
                    foreach (var m in c.Npc.Memories) if (m.Subject != null && m.Subject.IndexOf(x.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    return false;
                }
                case ConditionKind.PlayerSkill:
                {
                    if (p.Rpg == null || p.Rpg.State != CreationState.Complete || !Enum.TryParse<Skill>(x.Key, out var skill)) return false;
                    var rules = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p));
                    return rules.SkillRank(skill) != ProficiencyRank.None;
                }
                case ConditionKind.Variable:
                {
                    string v = null;
                    if (c.Conversation != null && c.Conversation.Variables.TryGetValue(x.Key, out var cv)) v = cv;
                    else if (c.Quest != null && c.Quest.Variables.TryGetValue(x.Key, out var qv)) v = qv;
                    return v == x.Value;
                }
                case ConditionKind.SettlementFoodShort:
                {
                    var s = x.Key == "here" || x.Key == null ? w.FindSettlement(p.SettlementId) : QuestEffects.SettlementOf(w, c.Quest, x.Key);
                    return s != null && s.IsInFoodShortage;
                }
                case ConditionKind.ObjectiveDone:
                    return c.Quest?.Progress(x.Key)?.Done == true;
                case ConditionKind.NpcDead:
                    return QuestEffects.Person(w, c.Quest, x.Key)?.IsDeceased == true;
                case ConditionKind.SettlementReputation:
                {
                    var s = x.Key == "here" || x.Key == null ? p.SettlementId : QuestEffects.SettlementOf(w, c.Quest, x.Key)?.SettlementId ?? default;
                    return InRange(x, p.Social.ReputationIn(s));
                }
                case ConditionKind.DaysSinceAccepted:
                    return c.Quest != null && c.Quest.AcceptedAt >= 0 && InRange(x, (int)((QuestClock.Now(w) - c.Quest.AcceptedAt) / Domain.Calendar.WorldTime.MinutesPerDay));
                case ConditionKind.PlayerLevel:
                    return p.Rpg != null && InRange(x, p.Rpg.Level);
                case ConditionKind.NpcMorale:
                    return c.Npc != null && InRange(x, c.Npc.Condition.Morale);
                case ConditionKind.KnowsCrime:
                    return c.Npc != null && Places.CrimeRules.KnownTo(w, c.Npc).Count > 0;
                case ConditionKind.InParty: // Phase 15
                    return c.Npc != null && w.Party.Contains(c.Npc.NpcId);
                case ConditionKind.PlayerCoin: // Phase 17
                    return p != null && p.Purse >= (x.Min == int.MinValue ? 1 : x.Min);
                case ConditionKind.CompanionQuarrel: // Phase 17 (ADR-0087)
                    return c.Npc != null && Simulation.Party.CompanionStories.QuarrelWith(w, c.Npc) != null;
                default:
                    // Phase 17 (ADR-0083): campaign, character-status and world kinds are the simulation's.
                    return Simulation.Campaigns.CampaignConditions.Handles(x.Kind) && Simulation.Campaigns.CampaignConditions.Holds(w, c.Campaign, c.Quest, c.Npc, new QuestCondition
                    {
                        Kind = x.Kind, Key = x.Key, Value = x.Value, Min = x.Min, Max = x.Max, Negate = false,
                    });
            }
        }
    }
}
