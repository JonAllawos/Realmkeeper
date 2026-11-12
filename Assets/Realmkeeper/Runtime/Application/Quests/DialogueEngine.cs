using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Simulation.Encounters;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application
{
    /// <summary>One response as the dialogue window shows it.</summary>
    public sealed class ResponseView
    {
        public string Id { get; internal set; }
        public string Text { get; internal set; }
        public ResponseKind Kind { get; internal set; }

        /// <summary>The check, in words ("Persuasion DC 13"), or null.</summary>
        public string Check { get; internal set; }

        /// <summary>The quest this response concerns, or null.</summary>
        public string QuestInstanceId { get; internal set; }

        /// <summary>For a Trade response: the shop to open.</summary>
        public ShopId ShopId { get; internal set; }

        /// <summary>Phase 17: false when the response is shown but cannot be chosen now (see <see cref="Why"/>).</summary>
        public bool Enabled { get; internal set; } = true;

        /// <summary>Phase 17: why a disabled response cannot be chosen, in the story's words.</summary>
        public string Why { get; internal set; }

        /// <summary>Phase 17: the choice is a campaign decision (its consequences last).</summary>
        public bool Decision { get; internal set; }
    }

    /// <summary>The conversation in progress as the dialogue window shows it (ADR-0058).</summary>
    public sealed class DialogueView
    {
        public NpcId NpcId { get; internal set; }
        public string Name { get; internal set; }
        public string Role { get; internal set; }
        public string Activity { get; internal set; }

        /// <summary>How they regard the player, as a tier word (Unknown, Recognized, Respected, Trusted, Honored, Feared, Hostile).</summary>
        public ReputationTier Tier { get; internal set; }

        /// <summary>The one or two dimensions worth showing (e.g. "grateful", "wary"), or empty.</summary>
        public string Mood { get; internal set; }

        public string QuestName { get; internal set; }
        public string NpcLine { get; internal set; }
        public List<(string Speaker, string Text)> Transcript { get; } = new List<(string, string)>();
        public List<ResponseView> Responses { get; } = new List<ResponseView>();
    }

    /// <summary>
    /// The dialogue engine (ADR-0058): which line a person says, which responses the player has (conditions, once-only
    /// keys, content rules, and the engine's hooks into quests and trade), and what a choice does — a check rolled with the
    /// world's dice, then its consequences, applied inside the choose command so they commit together or not at all.
    /// </summary>
    public static class DialogueEngine
    {
        public const string HookPrefix = "@quest:";
        public const string DialoguePrefix = "@dialogue:";

        public static string Now(WorldData w) => QuestClock.Now(w).ToString(CultureInfo.InvariantCulture);

        public static DialogueDefinition Definition(ConversationData c) => DialogueCatalog.Find(c.DialogueId);

        public static QuestInstanceData Quest(WorldData w, ConversationData c) => c.QuestInstanceId == null ? null : w.FindQuest(c.QuestInstanceId);

        public static string Fill(WorldData w, ConversationData c, NpcData npc, string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            var q = Quest(w, c);
            var result = text.Replace("{npc}", npc?.Name ?? "they");
            if (c.Variables.TryGetValue("answer", out var answer)) result = result.Replace("{answer}", answer);
            if (c.Variables.TryGetValue("remark", out var remark)) result = result.Replace("{remark}", remark); // Phase 17
            result = QuestText.Fill(w, q, result);
            var campaign = Simulation.Campaigns.CampaignEngine.OfQuest(w, q);
            return campaign == null ? result : Simulation.Campaigns.CampaignEngine.Text(w, campaign, result); // Phase 17: campaign roles
        }

        /// <summary>The line said at the current node: the first whose conditions hold and that the content rules allow.</summary>
        public static string Line(WorldData w, ConversationData c, NpcData npc)
        {
            var node = Definition(c)?.Find(c.NodeId);
            if (node == null) return "…";
            var ctx = new ConditionContext(w, npc, Quest(w, c), c);
            foreach (var line in node.Lines)
            {
                if (!QuestConditions.All(ctx, line.Conditions)) continue;
                var presentation = w.WorldSettings.Content.For(line.Tags);
                if (presentation == ContentPresentation.Hide) continue;
                var text = presentation == ContentPresentation.Soften ? line.Softened : line.Text;
                if (text == null) continue;
                return Fill(w, c, npc, text);
            }

            return "…";
        }

        private static string OnceKey(ConversationData c, DialogueResponse r) => "once:" + c.NpcId + ":" + c.DialogueId + ":" + c.NodeId + ":" + r.Id + ":" + (c.QuestInstanceId ?? string.Empty);

        /// <summary>The responses open to the player now, with the engine's hooks at the start of a person's general talk.</summary>
        public static List<(DialogueResponse Response, ResponseView View)> Responses(WorldData w, ConversationData c, NpcData npc)
        {
            var list = new List<(DialogueResponse, ResponseView)>();
            var def = Definition(c);
            var node = def?.Find(c.NodeId);
            if (node == null || npc == null) return list;
            var ctx = new ConditionContext(w, npc, Quest(w, c), c);
            var social = w.Player.Social;

            // Hooks first: quests this person is part of, then their shop.
            if (c.DialogueId == DialogueCatalog.General && c.NodeId == "start")
            {
                foreach (var q in w.Quests)
                {
                    var qd = QuestCatalog.Find(q.DefinitionId);
                    var qdlg = DialogueCatalog.Find(qd?.DialogueId);
                    if (qd == null || qdlg == null) continue;
                    var name = QuestText.Fill(w, q, qd.Name);
                    foreach (var pair in q.Bindings)
                    {
                        if (pair.Value != npc.NpcId.ToString() || qdlg.Find(pair.Key) == null) continue;
                        var role = pair.Key;
                        string text = null;
                        if (role == "giver" && (q.State == QuestState.Available || q.State == QuestState.Discovered || q.State == QuestState.Offered))
                            text = q.State == QuestState.Available ? "You look troubled. Is something wrong?" : "About what I heard — " + name + ".";
                        else if (q.IsOpen && (role == "giver" || Involves(qd, q, role)))
                            text = "About " + name + "…";
                        if (text == null) continue;
                        var r = new DialogueResponse { Id = "hook:" + q.InstanceId + ":" + role, Text = text, Kind = ResponseKind.Continue, Next = HookPrefix + q.InstanceId + ":" + role };
                        list.Add((r, new ResponseView { Id = r.Id, Text = r.Text, Kind = r.Kind, QuestInstanceId = q.InstanceId }));
                    }
                }

                // Phase 13 (ADR-0066): a guard holding a place's way asks the player's business.
                var gate = Realmkeeper.Application.Places.PlaceRules.GateHeldBy(w, npc.NpcId);
                if (gate != null)
                {
                    var r = new DialogueResponse { Id = "gate:" + gate, Text = "I want to go in.", Kind = ResponseKind.Continue, Next = DialoguePrefix + "dlg_gate:start" };
                    list.Add((r, new ResponseView { Id = r.Id, Text = r.Text, Kind = r.Kind }));
                }

                foreach (var shop in w.Shops)
                {
                    if (shop.ShopkeeperNpcId != npc.NpcId || shop.SettlementId != w.Player.SettlementId) continue;
                    if (social.StandingWith(npc.FactionId) <= -50 || npc.PlayerRegard.Hostility >= 70) continue; // refused service (ADR-0059)
                    var r = new DialogueResponse { Id = "trade:" + shop.ShopId, Text = "Let's trade.", Kind = ResponseKind.Trade, Next = "end" };
                    list.Add((r, new ResponseView { Id = r.Id, Text = r.Text, Kind = r.Kind, ShopId = shop.ShopId }));
                }

                // Phase 18 (ADR-0088): findings this person would want to hear.
                foreach (var d in DiscoveryRules.Offerable(w, npc))
                {
                    var r = new DialogueResponse
                    {
                        Id = "share:" + d.Key, Text = DiscoveryRules.Offer(d), Kind = ResponseKind.Continue, Next = "topics",
                        Consequences = new[] { DialogueConsequence.Of(DialogueConsequenceKind.ShareDiscovery, d.Key, 0, null, null, null) },
                    };
                    list.Add((r, new ResponseView { Id = r.Id, Text = r.Text, Kind = r.Kind }));
                }
            }

            foreach (var r in node.Responses)
            {
                if (!QuestConditions.All(ctx, r.Conditions)) continue;
                if (r.Once && social.Applied(OnceKey(c, r))) continue;
                if (w.WorldSettings.Content.For(r.Tags) == ContentPresentation.Hide) continue;
                var met = QuestConditions.All(ctx, r.Requirements); // Phase 17: shown but disabled, with the reason
                if (!met && !r.ShowWhenUnavailable) continue;
                var decision = false;
                foreach (var x in r.Consequences) if (x.Kind == DialogueConsequenceKind.CampaignDecision) decision = true;
                list.Add((r, new ResponseView
                {
                    Id = r.Id, Text = Fill(w, c, npc, r.Text), Kind = r.Kind, QuestInstanceId = c.QuestInstanceId,
                    Check = r.Check == null ? null : $"{r.Check.Skill} DC {Dc(npc, r.Check)}",
                    Enabled = met, Why = met ? null : Fill(w, c, npc, r.UnavailableText ?? "You cannot say that now."), Decision = decision,
                }));
            }

            return list;
        }

        /// <summary>True when a role has a visible, unfinished objective about it (so the person has something to say about the quest).</summary>
        private static bool Involves(QuestDefinition def, QuestInstanceData q, string role)
        {
            foreach (var o in def.Objectives)
            {
                var p = q.Progress(o.Id);
                if (p == null || p.Done || p.Failed || !p.Revealed) continue;
                if (o.Target == "role:" + role || o.To == role) return true;
            }

            return false;
        }

        public static int Dc(NpcData npc, DialogueCheck check)
        {
            var dc = check.Dc;
            if (npc != null && !string.IsNullOrEmpty(check.ResistTrait))
            {
                var inverse = check.ResistTrait.StartsWith("-", StringComparison.Ordinal);
                if (Enum.TryParse<NpcTrait>(inverse ? check.ResistTrait.Substring(1) : check.ResistTrait, out var trait))
                    dc += (inverse ? -1 : 1) * (npc.Personality[trait] - 50) / 10;
            }

            return Math.Max(1, dc);
        }

        public static DialogueView View(WorldData w)
        {
            var c = w?.Conversation;
            if (c == null) return null;
            var npc = w.FindNpc(c.NpcId);
            var q = Quest(w, c);
            var r = npc?.PlayerRegard ?? new PlayerRegardData();
            var view = new DialogueView
            {
                NpcId = c.NpcId, Name = npc?.Name, Role = npc == null ? null : (npc.Character?.Occupation ?? npc.Role.ToString()),
                Activity = npc == null ? null : NpcActivityPlan.Now(w, npc).Activity.ToString(), Tier = Reputation.Of(r), Mood = Mood(r),
                QuestName = q == null ? null : QuestText.Fill(w, q, QuestCatalog.Find(q.DefinitionId)?.Name), NpcLine = Line(w, c, npc),
            };
            foreach (var t in c.Transcript) view.Transcript.Add((t.Speaker, t.Text));
            foreach (var (_, rv) in Responses(w, c, npc)) view.Responses.Add(rv);
            return view;
        }

        private static string Mood(PlayerRegardData r)
        {
            var parts = new List<string>();
            if (r.Hostility >= 30) parts.Add("hostile");
            if (r.Fear >= 30) parts.Add("wary of you");
            if (r.Gratitude >= 20) parts.Add("grateful");
            if (r.Trust >= 40) parts.Add("trusts you");
            if (r.Respect >= 30) parts.Add("respects you");
            return string.Join(", ", parts);
        }

        // ----- Choosing --------------------------------------------------------------------------------------

        /// <summary>Rolls a check: d20 + the player's skill bonus (the rules engine's) against the DC, with the world's dice.</summary>
        public static (int Roll, int Total, int Dc, bool Success) Roll(CommandContext context, WorldData w, ConversationData c, NpcData npc, DialogueCheck check)
        {
            var dc = Dc(npc, check);
            var bonus = 0;
            var p = w.Player;
            if (p.Rpg != null && p.Rpg.State == CreationState.Complete && Enum.TryParse<Skill>(check.Skill, out var skill))
                bonus = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p)).SkillBonus(skill).Total;
            var die = SpellCommandHandlers.Dice(w, "dialogue:" + c.Key + ":" + c.Turn.ToString(CultureInfo.InvariantCulture));
            var roll = die(20);
            var total = roll + bonus;
            return (roll, total, dc, roll == 20 || (roll != 1 && total >= dc));
        }

        /// <summary>
        /// Phase 16 (ADR-0080): what this person has heard of the wider world (world events they know of, by the people who
        /// told them). The player now holds it as a rumour, as of today, from this person. The world itself is unchanged.
        /// </summary>
        private static string MentionWorldEvents(WorldData w, NpcData npc, string answer)
        {
            var told = new List<string>();
            var now = QuestClock.Now(w);
            foreach (var e in w.WorldEvents)
            {
                if (told.Count >= 2 || !e.IsOpen) continue;
                if (Simulation.Npcs.NpcMind.Find(npc, KnowledgeTopic.RecentEvent, e.EventId) == null) continue;
                Simulation.Atlas.WorldEvents.PlayerLearns(e, EventKnowledge.Rumored, now, npc.Name);
                told.Add(Simulation.Atlas.WorldEvents.Telling(w, e));
            }

            return told.Count == 0 ? answer : answer + " They say " + string.Join("; and ", told) + ".";
        }

        /// <summary>Applies one consequence (inside the choose command). Returns a failure, or null.</summary>
        public static CommandResult Apply(CommandContext context, WorldData w, ConversationData c, NpcData npc, DialogueConsequence x, string key, List<QuestEvent> events)
        {
            var q = Quest(w, c);
            var p = w.Player;
            var report = QuestRuntime.Reporter(context);
            NpcData Who() => x.Role != null && q != null ? QuestEffects.Person(w, q, x.Role) ?? npc : npc;
            switch (x.Kind)
            {
                case DialogueConsequenceKind.AskTopic:
                {
                    if (!Enum.TryParse<ConversationTopic>(x.Key, out var topic)) return CommandResult.Invalid("Unknown topic.");
                    var answer = NpcDialogue.Answer(w, npc, topic, default, p.Name);
                    if (topic == ConversationTopic.Rumours || topic == ConversationTopic.News) answer = MentionQuests(w, npc, answer, report);
                    if (topic == ConversationTopic.Rumours) answer = MentionPlaces(w, npc, answer, report, events); // Phase 13
                    if (topic == ConversationTopic.Rumours || topic == ConversationTopic.News) answer = MentionWorldEvents(w, npc, answer); // Phase 16
                    c.Variables["answer"] = answer;
                    c.Variables["answered"] = "yes";
                    if (topic == ConversationTopic.Rumours && answer.StartsWith("Word is", StringComparison.Ordinal))
                        QuestRuntime.Rumor(w, "rumour:" + npc.NpcId + ":" + w.CurrentSimulationDate.ToDayNumber(), answer, npc.NpcId, 50);
                    break;
                }
                case DialogueConsequenceKind.OfferQuest:
                    if (q != null) Offer(w, q, report);
                    break;
                case DialogueConsequenceKind.AcceptQuest:
                {
                    if (q == null) return CommandResult.InvalidState("There is no quest to accept here.");
                    if (q.IsOpen) break;
                    Offer(w, q, report);
                    if (!QuestLifecycle.Move(w, q, QuestState.Accepted, null, report) || !QuestLifecycle.Move(w, q, QuestState.Active, null, report))
                        return CommandResult.InvalidState("That quest cannot be accepted now.");
                    var tracked = false;
                    foreach (var other in w.Quests) if (other.Tracked) tracked = true;
                    if (!tracked) q.Tracked = true;
                    QuestEffects.ApplyAll(w, q, ConsequenceTiming.OnAccept, report);
                    NpcMind.Remember(npc, new NpcMemoryData
                    {
                        MemoryId = new MemoryId(context.NextGuid("npc-memory")), Date = w.CurrentSimulationDate, Category = MemoryCategory.Work,
                        Subject = "Asked " + p.Name + " for help: " + QuestText.Fill(w, q, QuestCatalog.Find(q.DefinitionId)?.Name), SettlementId = npc.HomeSettlementId, Salience = 45,
                        Source = MemorySource.Experienced,
                    });
                    break;
                }
                case DialogueConsequenceKind.DeclineQuest:
                {
                    if (q == null) break;
                    Offer(w, q, report);
                    if (QuestLifecycle.Move(w, q, QuestState.Discovered, "declined", null))
                        report(HistoricalEventType.QuestDeclined, EventSeverity.Info, EventImportance.Minor, new[] { npc.HomeSettlementId },
                            new Dictionary<string, string> { ["source"] = "player", ["quest"] = QuestCatalog.Find(q.DefinitionId)?.Name, ["instance"] = q.InstanceId, ["giver"] = npc.Name }, new[] { npc.NpcId });
                    Record(w, q, "declined:" + q.DefinitionId, "Declined " + QuestText.Fill(w, q, QuestCatalog.Find(q.DefinitionId)?.Name) + " for " + npc.Name, report, events);
                    break;
                }
                case DialogueConsequenceKind.AbandonQuest:
                    if (q != null && q.IsOpen) QuestLifecycle.Move(w, q, QuestState.Abandoned, "given up", report);
                    break;
                case DialogueConsequenceKind.Decision:
                    if (q != null && !q.Decisions.Contains(x.Key)) q.Decisions.Add(x.Key);
                    Record(w, q, x.Key, Fill(w, c, npc, x.Value ?? x.Key), report, events);
                    break;
                case DialogueConsequenceKind.SetVariable:
                    c.Variables[x.Key] = x.Value;
                    if (q != null) q.Variables[x.Key] = x.Value;
                    break;
                case DialogueConsequenceKind.Regard:
                {
                    var who = Who();
                    if (who != null) Reputation.Change(w, "npc:" + who.NpcId, x.Key, x.Amount, key, "conversation", report);
                    break;
                }
                case DialogueConsequenceKind.FactionStanding:
                {
                    var who = Who();
                    if (who != null && !who.FactionId.IsEmpty) Reputation.Change(w, "faction:" + who.FactionId, "Standing", x.Amount, key, "conversation", report);
                    break;
                }
                case DialogueConsequenceKind.SettlementReputation:
                    if (!npc.HomeSettlementId.IsEmpty) Reputation.Change(w, "settlement:" + npc.HomeSettlementId, "Reputation", x.Amount, key, "conversation", report);
                    break;
                case DialogueConsequenceKind.Reveal:
                    if (QuestRuntime.Discover(w, x.Key, Fill(w, c, npc, x.Value), "npc:" + npc.NpcId, report))
                        events.Add(new QuestEvent { Kind = QuestEventKind.Discovered, Subject = x.Key });
                    break;
                case DialogueConsequenceKind.Rumor:
                    QuestRuntime.Rumor(w, x.Key, Fill(w, c, npc, x.Value), npc.NpcId, x.Amount > 0 ? x.Amount : 50);
                    break;
                case DialogueConsequenceKind.NpcMemory:
                {
                    var who = Who();
                    if (who == null) break;
                    NpcMind.Remember(who, new NpcMemoryData
                    {
                        MemoryId = new MemoryId(context.NextGuid("npc-memory")), Date = w.CurrentSimulationDate, Category = MemoryCategory.Personal, Subject = Fill(w, c, npc, x.Key),
                        SettlementId = who.HomeSettlementId, Salience = x.Amount > 0 ? x.Amount : 50, Source = MemorySource.Experienced,
                    });
                    break;
                }
                case DialogueConsequenceKind.PlayerDeed:
                {
                    var who = Who();
                    if (who != null) Deeds.Witness(w, who, x.Key, q?.InstanceId ?? "talk", key);
                    break;
                }
                case DialogueConsequenceKind.GiveItem:
                {
                    var failure = Give(context, w, c, npc, q, x, events, report);
                    if (failure != null) return failure;
                    break;
                }
                case DialogueConsequenceKind.ReceiveItem:
                {
                    var item = ItemCatalog.Find(x.Key);
                    if (item == null) return CommandResult.Fail($"Unknown item '{x.Key}'.");
                    var heavy = ItemCommandHandlers.WeightProblem(p, item.WeightGrams * Math.Max(1, x.Amount));
                    if (heavy != null) return CommandResult.InvalidState(heavy);
                    var stack = ItemStacks.Create(ItemCommandHandlers.NewStackId(context), item, Math.Max(1, x.Amount), "From " + npc.Name, QuestClock.Now(w));
                    if (x.Value != null && q != null) q.Variables[x.Value] = stack.StackId.ToString();
                    ItemCommandHandlers.AddStack(p, stack);
                    break;
                }
                case DialogueConsequenceKind.PayCoin:
                    if (p.Purse < x.Amount) return CommandResult.InvalidState($"You need {x.Amount} coin and have {p.Purse}.");
                    p.Purse -= x.Amount;
                    break;
                case DialogueConsequenceKind.ReceiveCoin:
                    p.Purse += x.Amount;
                    break;
                case DialogueConsequenceKind.RevealLocation:
                {
                    var who = Who();
                    if (who == null) break;
                    var place = w.FindLocation(who.WorkplaceId) ?? w.FindLocation(who.ResidenceId);
                    var settlement = w.FindSettlement(who.HomeSettlementId);
                    QuestRuntime.Know(w, who.HomeSettlementId.ToString());
                    if (place != null) QuestRuntime.Know(w, place.LocationId.ToString());
                    QuestRuntime.Discover(w, "whereabouts:" + who.NpcId, $"{who.Name} can be found at the {place?.Name ?? "their home"} in {settlement?.Name ?? "their settlement"}.", "npc:" + npc.NpcId);
                    break;
                }
                case DialogueConsequenceKind.StartEncounter:
                {
                    if (!Enum.TryParse<EncounterPurpose>(x.Key, out var purpose)) return CommandResult.Invalid("Unknown outing.");
                    var seek = q?.Bound("creature");
                    w.Conversation = null;
                    var started = WorldEncounters.Start(new StartWorldEncounterCommand(purpose, seek), context);
                    if (!started.IsSuccess) return started;
                    break;
                }
                case DialogueConsequenceKind.SetOutcome:
                    if (q != null) q.Outcome = x.Key;
                    break;
                case DialogueConsequenceKind.ShareDiscovery: // Phase 18 (ADR-0088)
                    c.Variables["answer"] = DiscoveryRules.Share(w, npc, x.Key, key, report);
                    c.Variables["answered"] = "yes";
                    break;
                case DialogueConsequenceKind.CompleteObjective:
                    if (q != null) events.Add(new QuestEvent { Kind = QuestEventKind.ObjectiveCompleted, Target = q.InstanceId, Detail = x.Key, Subject = npc.NpcId.ToString() });
                    break;
                case DialogueConsequenceKind.RevealObjective:
                {
                    var progress = q?.Progress(x.Key);
                    if (progress != null) progress.Revealed = true;
                    break;
                }
                case DialogueConsequenceKind.RevealPlace:
                {
                    // Phase 13 (ADR-0065): a name and a direction, or the way there.
                    var place = w.FindPlace(q?.Bound(x.Key) ?? x.Key);
                    if (place == null) return CommandResult.Invalid("That place is not known to the world.");
                    var level = Enum.TryParse<Realmkeeper.Domain.Places.PlaceDiscovery>(x.Value ?? "Discovered", out var lv) ? lv : Realmkeeper.Domain.Places.PlaceDiscovery.Discovered;
                    Realmkeeper.Simulation.Places.PlaceKnowledge.Raise(w, place, level, report, "told by " + npc?.Name);
                    if (level >= Realmkeeper.Domain.Places.PlaceDiscovery.Discovered) events.Add(new QuestEvent { Kind = QuestEventKind.PlaceDiscovered, Subject = place.PlaceId });
                    break;
                }
                case DialogueConsequenceKind.Recruit: // Phase 15 (ADR-0073)
                {
                    var temporary = x.Value == "temporary";
                    var why = Party.RecruitRules.Problem(w, npc, temporary);
                    c.Say("narration", why != null ? npc.Name + " shakes their head. " + why : Party.PartyCommandHandlers.Recruit(context, w, npc.NpcId, temporary));
                    break;
                }

                case DialogueConsequenceKind.Dismiss:
                    if (w.ActiveEncounter == null && w.Party.Contains(npc.NpcId))
                    {
                        Party.PartyCommandHandlers.Leave(context, w, npc, CompanionStanding.Dismissed, "sent home");
                        c.Say("narration", npc.Name + " nods, and turns for home.");
                    }

                    break;

                case DialogueConsequenceKind.SettleCrime: // Phase 14 (ADR-0069)
                {
                    var owed = Places.CrimeRules.Owed(Places.CrimeRules.KnownTo(w, npc));
                    var said = Places.CrimeRules.Settle(context, w, npc);
                    if (said == null) return CommandResult.InvalidState($"You cannot pay what is owed: {owed} coin, and you have {w.Player.Purse}.");
                    c.Say("narration", said);
                    break;
                }

                case DialogueConsequenceKind.GrantPassage:
                {
                    var place = w.FindPlace(q?.Bound(x.Key) ?? x.Key) ?? (p.IsInPlace ? w.FindPlace(p.PlaceId) : null);
                    if (place == null) return CommandResult.Invalid("There is nowhere to pass.");
                    place.Variables["passage:" + x.Value] = "granted";
                    var g = place.GroupOf(x.Value);
                    if (g.State == Realmkeeper.Domain.Places.GroupState.Present) g.State = Realmkeeper.Domain.Places.GroupState.Peaceful;
                    place.Note((npc?.Name ?? "The guard") + " let " + p.Name + " pass.");
                    break;
                }
                case DialogueConsequenceKind.NpcMorale:
                    npc.Condition.Morale += x.Amount;
                    break;
                case DialogueConsequenceKind.Notoriety:
                    Reputation.Change(w, "notoriety", "Notoriety", x.Amount, key, "conversation", report);
                    break;
                case DialogueConsequenceKind.EndConversation:
                case DialogueConsequenceKind.OpenTrade:
                    break;

                // ----- Phase 17 (ADR-0083, ADR-0087) -----
                case DialogueConsequenceKind.CampaignDecision:
                {
                    var campaign = Simulation.Campaigns.CampaignEngine.OfQuest(w, q) ?? Simulation.Campaigns.CampaignEngine.Sole(w);
                    if (campaign == null) return CommandResult.InvalidState("There is no story for that decision.");
                    var why = Simulation.Campaigns.CampaignEngine.Decide(w, campaign, x.Key, x.Value, npc?.Name, report);
                    if (why != null) return CommandResult.InvalidState(why);
                    var choice = Simulation.Campaigns.CampaignEngine.Definition(campaign).Decision(x.Key).Choice(x.Value);
                    Record(w, q, "campaign:" + campaign.CampaignId + ":" + x.Key, Simulation.Campaigns.CampaignEngine.Text(w, campaign, choice.Summary), report, events);
                    events.Add(new QuestEvent { Kind = QuestEventKind.Decision, Subject = x.Key });
                    break;
                }
                case DialogueConsequenceKind.CampaignFlag:
                {
                    var campaign = Simulation.Campaigns.CampaignEngine.OfQuest(w, q) ?? Simulation.Campaigns.CampaignEngine.Sole(w);
                    if (campaign == null || campaign.State != Domain.Campaigns.CampaignState.Active) break;
                    Simulation.Campaigns.CampaignEngine.ApplyEffect(w, campaign, Domain.Campaigns.CampaignEffect.Of(Domain.Campaigns.CampaignEffectKind.Flag, key: x.Key, value: x.Value), "dialogue:flag:" + x.Key, report);
                    break;
                }
                case DialogueConsequenceKind.HearRemark:
                {
                    var said = Simulation.Party.CompanionStories.Hear(w, npc.NpcId);
                    c.Variables["remark"] = said ?? "\"Nothing that won't keep.\"";
                    c.Variables["heard"] = "yes";
                    break;
                }
                case DialogueConsequenceKind.Reconcile:
                {
                    var said = Simulation.Party.CompanionStories.Reconcile(w, npc, x.Value != "failed", report);
                    if (said != null) c.Say("narration", said);
                    break;
                }
                case DialogueConsequenceKind.Relation:
                {
                    var a = Who();
                    var b = q == null ? null : QuestEffects.Person(w, q, x.Value);
                    if (a == null || b == null || a.NpcId == b.NpcId) break;
                    var cause = x.Amount < 0 && x.Key != "Rivalry" || x.Amount > 0 && x.Key == "Rivalry" ? NpcRelationCause.Quarrel : NpcRelationCause.Kindness;
                    var r = NpcMind.Relate(w, a.NpcId, b.NpcId, cause, NpcRelationKind.Acquaintance, w.CurrentSimulationDate);
                    if (r != null && !w.Player.Social.Applied("relation:" + key))
                    {
                        Simulation.Campaigns.Relations.Shift(r, x.Key, x.Amount, cause, w.CurrentSimulationDate);
                        w.Player.Social.AppliedKeys.Add("relation:" + key);
                    }

                    break;
                }
            }

            return null;
        }

        /// <summary>A quest becomes offered (Available → Offered, or Discovered → Offered), once.</summary>
        public static void Offer(WorldData w, QuestInstanceData q, QuestReporter report)
        {
            if (q.State == QuestState.Available || q.State == QuestState.Discovered) QuestLifecycle.Move(w, q, QuestState.Offered, null, report);
        }

        private static void Record(WorldData w, QuestInstanceData q, string key, string text, QuestReporter report, List<QuestEvent> events)
        {
            var social = w.Player.Social;
            foreach (var d in social.Decisions) if (d.Key == key && d.QuestInstanceId == q?.InstanceId) return;
            social.Decisions.Add(new DecisionData { Key = key, Text = text, At = QuestClock.Now(w), QuestInstanceId = q?.InstanceId });
            while (social.Decisions.Count > PlayerSocialData.MaxDecisions) social.Decisions.RemoveAt(0);
            report(HistoricalEventType.DialogueDecision, EventSeverity.Info, EventImportance.Minor, w.Player.IsInSettlement ? new[] { w.Player.SettlementId } : Array.Empty<SettlementId>(),
                new Dictionary<string, string> { ["source"] = "player", ["decision"] = text, ["instance"] = q?.InstanceId ?? string.Empty }, Array.Empty<NpcId>());
            events.Add(new QuestEvent { Kind = QuestEventKind.Decision, Subject = key, Target = q?.InstanceId });
        }

        /// <summary>A person mentions quests that need doing where they live (a discovery source): Available becomes Discovered.</summary>
        /// <summary>
        /// Phase 13 (ADR-0065): a resident's rumours include a place near their home the player does not know well: first its
        /// name and rough direction (Rumored: no marker), asked again the way there (Discovered: on the map). One place per answer.
        /// </summary>
        public static string MentionPlaces(WorldData w, NpcData npc, string answer, QuestReporter report, List<QuestEvent> events)
        {
            if (npc == null) return answer;
            Realmkeeper.Domain.Places.PlaceData pick = null;
            foreach (var place in w.Places)
            {
                if (place.Category == Realmkeeper.Domain.Places.PlaceCategory.Interior || place.SettlementId != npc.HomeSettlementId) continue;
                if (place.Discovery >= Realmkeeper.Domain.Places.PlaceDiscovery.Discovered) continue;
                if (pick == null || string.CompareOrdinal(place.PlaceId, pick.PlaceId) < 0) pick = place;
            }

            if (pick == null) return answer;
            var where = Realmkeeper.Simulation.Places.PlaceKnowledge.Direction(w, pick);
            if (pick.Discovery == Realmkeeper.Domain.Places.PlaceDiscovery.Unknown)
            {
                Realmkeeper.Simulation.Places.PlaceKnowledge.Raise(w, pick, Realmkeeper.Domain.Places.PlaceDiscovery.Rumored, report, "rumour");
                return answer + " And folk talk of " + pick.Name + ", somewhere " + where.Split(',')[0] + ". Nobody goes there now.";
            }

            Realmkeeper.Simulation.Places.PlaceKnowledge.Raise(w, pick, Realmkeeper.Domain.Places.PlaceDiscovery.Discovered, report, "told by " + npc.Name);
            events?.Add(new QuestEvent { Kind = QuestEventKind.PlaceDiscovered, Subject = pick.PlaceId });
            return answer + " " + pick.Name + "? It lies " + where + ". You can't miss it once you're off the road.";
        }

        private static string MentionQuests(WorldData w, NpcData npc, string answer, QuestReporter report)
        {
            foreach (var q in w.Quests)
            {
                if (q.State != QuestState.Available) continue;
                var giver = QuestEffects.Person(w, q, "giver");
                if (giver == null || giver.NpcId == npc.NpcId || giver.HomeSettlementId != npc.HomeSettlementId) continue;
                var name = QuestText.Fill(w, q, QuestCatalog.Find(q.DefinitionId)?.Name);
                QuestLifecycle.Move(w, q, QuestState.Discovered, "heard from " + npc.Name, report);
                var text = $"{giver.Name} could use help, they say — \"{name}\".";
                QuestRuntime.Rumor(w, "quest-rumour:" + q.InstanceId, text, npc.NpcId, 70);
                return answer + " And " + text.Substring(0, 1).ToLowerInvariant() + text.Substring(1);
            }

            return answer;
        }

        /// <summary>Gives items to a person: a particular instance, any food, a role's item, or an item ID. A shop's keeper stocks them.</summary>
        private static CommandResult Give(CommandContext context, WorldData w, ConversationData c, NpcData npc, QuestInstanceData q, DialogueConsequence x, List<QuestEvent> events, QuestReporter report)
        {
            var p = w.Player;
            var recipient = x.Role != null && q != null ? QuestEffects.Person(w, q, x.Role) ?? npc : npc;
            var given = new List<ItemStackData>();
            if (x.Key.StartsWith("var:", StringComparison.Ordinal))
            {
                if (q == null || !q.Variables.TryGetValue(x.Key.Substring(4), out var stackId)) return CommandResult.InvalidState("You do not have it.");
                var i = p.Inventory.FindIndex(s => s.StackId.ToString() == stackId);
                if (i < 0) return CommandResult.InvalidState("You no longer have it.");
                given.Add(ItemCommandHandlers.TakeFrom(p.Inventory, p.Inventory[i], 1, context));
            }
            else if (x.Key == "@food")
            {
                var left = Math.Max(1, x.Amount);
                foreach (var s in new List<ItemStackData>(p.Inventory))
                {
                    if (left == 0) break;
                    if (!QuestObjectives.IsFood(s.ItemId) || s.Attuned || s.Bound) continue;
                    var n = Math.Min(left, s.Quantity);
                    given.Add(ItemCommandHandlers.TakeFrom(p.Inventory, s, n, context));
                    left -= n;
                }

                if (given.Count == 0) return CommandResult.InvalidState("You have no food to give.");
            }
            else
            {
                var itemId = x.Key.StartsWith("role:", StringComparison.Ordinal) ? q?.Bound(x.Key.Substring(5)) : x.Key;
                var have = ItemCommandHandlers.CountInPack(p, itemId);
                var want = Math.Max(1, x.Amount);
                if (have < want) return CommandResult.InvalidState($"You need {want} {ItemCatalog.Find(itemId)?.Name.ToLowerInvariant() ?? itemId} and have {have}.");
                given.AddRange(ItemCommandHandlers.Consume(p, itemId, want, context));
            }

            // Where it goes: a shopkeeper puts goods on the shelf; otherwise it is the person's (people keep no inventories yet).
            ShopData shop = null;
            foreach (var s in w.Shops) if (s.ShopkeeperNpcId == recipient.NpcId) shop = s;
            foreach (var s in given)
            {
                var def = ItemCatalog.Find(s.ItemId);
                if (shop != null && def != null && CommerceMath.Sells(shop, def) && ItemStacks.IsPlain(s, def)) CommerceMath.AddStock(shop, s.ItemId, s.Quantity);
                events.Add(new QuestEvent { Kind = QuestEventKind.Delivered, Subject = s.ItemId, Target = recipient.NpcId.ToString(), Quantity = s.Quantity, Detail = s.StackId.ToString(), Settlement = recipient.HomeSettlementId });
                if (q != null)
                    report(HistoricalEventType.QuestItemDelivered, EventSeverity.Info, EventImportance.Minor, new[] { recipient.HomeSettlementId },
                        new Dictionary<string, string>
                        {
                            ["source"] = "player", ["item"] = def?.Name ?? s.ItemId, ["quantity"] = s.Quantity.ToString(CultureInfo.InvariantCulture), ["to"] = recipient.Name,
                            ["quest"] = QuestCatalog.Find(q.DefinitionId)?.Name ?? string.Empty, ["instance"] = q.InstanceId,
                        }, new[] { recipient.NpcId });
            }

            return null;
        }
    }
}
