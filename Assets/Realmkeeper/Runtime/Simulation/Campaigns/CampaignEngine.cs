using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Campaigns;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Simulation.Campaigns
{
    /// <summary>
    /// Phase 17 (ADR-0082, ADR-0083): the campaign engine. Pure functions over <see cref="WorldData"/>, run inside a
    /// command's transaction or the day's simulation. It is event-driven: a campaign is re-read when one of its quests
    /// changes state, when a decision is made, and once a day (time limits, delayed consequences, world-state
    /// objectives); never per frame. Everything it applies is keyed, so a reload or a repeated command never applies it twice.
    /// </summary>
    public static class CampaignEngine
    {
        public const string QuestSourcePrefix = "campaign:";
        public const int MaxPasses = 8;

        public static string InstanceIdFor(string campaignId) => "campaign:" + campaignId;

        public static CampaignDefinition Definition(CampaignInstanceData c) => c == null ? null : CampaignCatalog.Find(c.CampaignId);

        public static CampaignInstanceData Find(WorldData w, string instanceId)
        {
            if (w?.Campaigns == null || instanceId == null) return null;
            foreach (var c in w.Campaigns) if (c.InstanceId == instanceId || c.CampaignId == instanceId) return c;
            return null;
        }

        /// <summary>The campaign a quest belongs to (its source key is "campaign:{campaign}:{quest key}"), or null.</summary>
        public static CampaignInstanceData OfQuest(WorldData w, QuestInstanceData q)
        {
            var key = q?.SourceKey;
            if (key == null || !key.StartsWith(QuestSourcePrefix, StringComparison.Ordinal)) return null;
            var rest = key.Substring(QuestSourcePrefix.Length);
            var colon = rest.IndexOf(':');
            return Find(w, InstanceIdFor(colon < 0 ? rest : rest.Substring(0, colon)));
        }

        /// <summary>The one open campaign, when exactly one is open (general talk reads it); otherwise null.</summary>
        public static CampaignInstanceData Sole(WorldData w)
        {
            CampaignInstanceData open = null;
            if (w?.Campaigns == null) return null;
            foreach (var c in w.Campaigns)
            {
                if (!c.IsOpen && c.State != CampaignState.Completed) continue;
                if (open != null) return null;
                open = c;
            }

            return open;
        }

        private static string QuestKeyOf(QuestInstanceData q)
        {
            var rest = q.SourceKey.Substring(QuestSourcePrefix.Length);
            var colon = rest.IndexOf(':');
            return colon < 0 ? null : rest.Substring(colon + 1);
        }

        // ----- Starting ----------------------------------------------------------------------------------

        /// <summary>Why this campaign cannot begin in this world, in plain words; null when it can.</summary>
        public static string Compatibility(WorldData w, CampaignDefinition def)
        {
            if (w == null) return "There is no world.";
            if (def == null) return "That campaign is not known.";
            if (CampaignCatalog.Rejected.TryGetValue(def.Id, out var rejected)) return "That campaign's content is malformed: " + rejected[0];
            var errors = CampaignValidator.Validate(def);
            if (errors.Count > 0) return "That campaign's content is malformed: " + errors[0];
            if (w.Player == null) return "This world has no player character.";
            if (Find(w, InstanceIdFor(def.Id)) is CampaignInstanceData existing)
                return existing.IsClosed ? $"\"{def.Title}\" has already been played to its end in this world ({existing.State.ToString().ToLowerInvariant()})." : $"\"{def.Title}\" is already under way.";
            var bindings = CampaignBinder.Bind(w, def, out var problem);
            if (bindings == null) return problem + (def.Requires != null ? " " + def.Requires : string.Empty);
            var probe = new CampaignInstanceData { CampaignId = def.Id, Bindings = bindings, State = CampaignState.Active };
            foreach (var c in def.StartConditions)
                if (!CampaignConditions.Holds(w, probe, null, null, c)) return "The world is not as this campaign needs it (" + c + ")." + (def.Requires != null ? " " + def.Requires : string.Empty);
            return null;
        }

        /// <summary>
        /// Starts a campaign in this world: binds its roles to the world as it stands (nothing is rewritten), opens its
        /// first chapter and arcs, and makes their quests available. Returns null with a reason when it cannot.
        /// </summary>
        public static CampaignInstanceData Start(WorldData w, CampaignDefinition def, QuestReporter report, out string problem)
        {
            problem = Compatibility(w, def);
            if (problem != null) return null;
            var now = QuestClock.Now(w);
            var c = new CampaignInstanceData
            {
                InstanceId = InstanceIdFor(def.Id), CampaignId = def.Id, Version = def.Version, WorldId = w.WorldId.ToString(), State = CampaignState.Active,
                Bindings = CampaignBinder.Bind(w, def, out _), StartedAt = now,
            };
            foreach (var ch in def.Chapters) c.Chapters.Add(new ChapterProgressData { Id = ch.Id, State = ChapterState.Locked });
            foreach (var a in def.Arcs)
            {
                var p = new ArcProgressData { Id = a.Id, State = ArcState.Locked };
                foreach (var o in a.Objectives) p.Objectives.Add(new ArcObjectiveProgressData { Id = o.Id, Revealed = !o.Hidden });
                c.Arcs.Add(p);
            }

            w.Campaigns.Add(c);
            c.Note(now, "Began: " + def.Title + ".");
            Report(w, c, report, HistoricalEventType.CampaignBegan, EventSeverity.Info, EventImportance.Notable, "began", Settlement(w, c));
            Evaluate(w, c, report);
            return c;
        }

        // ----- State ----------------------------------------------------------------------------------------

        /// <summary>Suspends, resumes or abandons a campaign (checked). Returns a reason it cannot, or null.</summary>
        public static string Move(WorldData w, CampaignInstanceData c, CampaignState to, string why, QuestReporter report)
        {
            if (c == null) return "There is no such campaign.";
            if (!CampaignStates.CanMove(c.State, to)) return $"A campaign that is {c.State.ToString().ToLowerInvariant()} cannot be {Verb(to)}.";
            var now = QuestClock.Now(w);
            c.State = to;
            switch (to)
            {
                case CampaignState.Suspended:
                    c.SuspendedAt = now;
                    c.Note(now, "Set aside" + (why == null ? "." : ": " + why + "."));
                    break;
                case CampaignState.Active:
                    c.SuspendedAt = -1;
                    c.Note(now, "Taken up again.");
                    Evaluate(w, c, report);
                    break;
                default:
                    c.ClosedAt = now;
                    foreach (var a in c.Arcs)
                        if (!a.IsClosed && CampaignStates.CanMove(a.State, ArcState.Abandoned)) { a.State = ArcState.Abandoned; a.ClosedAt = now; a.Reason = "the campaign was given up"; }
                    c.Note(now, (to == CampaignState.Abandoned ? "Given up" : to.ToString()) + (why == null ? "." : ": " + why + "."));
                    Report(w, c, report, HistoricalEventType.CampaignEnded, to == CampaignState.Completed ? EventSeverity.Positive : EventSeverity.Info, EventImportance.Notable,
                        to == CampaignState.Abandoned ? "was given up" : "ended: " + (why ?? to.ToString().ToLowerInvariant()), Settlement(w, c));
                    break;
            }

            return null;
        }

        private static string Verb(CampaignState s) => s == CampaignState.Suspended ? "set aside" : s == CampaignState.Active ? "taken up again" : s == CampaignState.Abandoned ? "given up" : s.ToString().ToLowerInvariant();

        /// <summary>The player refuses an arc (optional, or one that allows it). Its quests not yet taken lapse.</summary>
        public static string Refuse(WorldData w, CampaignInstanceData c, string arcId, QuestReporter report)
        {
            var def = Definition(c);
            var ad = def?.Arc(arcId);
            var a = c?.Arc(arcId);
            if (ad == null || a == null) return "There is no such part of the story.";
            if (c.State != CampaignState.Active) return "The campaign is not under way.";
            if (!ad.Optional && !ad.MayRefuse) return $"\"{ad.Title}\" cannot be refused; you may set the whole campaign aside instead.";
            if (!CampaignStates.CanMove(a.State, ArcState.Abandoned)) return $"\"{ad.Title}\" is already {a.State.ToString().ToLowerInvariant()}.";
            MoveArc(w, c, ad, a, ArcState.Abandoned, null, "you refused it", report);
            foreach (var key in ad.StartQuests)
                if (c.Quests.TryGetValue(key, out var id) && w.FindQuest(id) is QuestInstanceData q && !q.IsOpen && !q.IsClosed && q.State != QuestState.Unavailable)
                    QuestLifecycle.Move(w, q, QuestState.Unavailable, "you refused it", report);
            Evaluate(w, c, report);
            return null;
        }

        // ----- Evaluation ----------------------------------------------------------------------------------

        /// <summary>Every open campaign: delayed consequences that are due, then progress (the day's pass and commands call this).</summary>
        public static void EvaluateAll(WorldData w, QuestReporter report)
        {
            if (w?.Campaigns == null) return;
            foreach (var c in new List<CampaignInstanceData>(w.Campaigns))
            {
                FireDue(w, c, report);
                if (c.State == CampaignState.Active) Evaluate(w, c, report);
            }
        }

        /// <summary>A quest changed state: the campaign it belongs to (if any) is re-read.</summary>
        public static void OnQuestMoved(WorldData w, QuestInstanceData q, QuestReporter report)
        {
            var c = OfQuest(w, q);
            if (c != null && c.State == CampaignState.Active) Evaluate(w, c, report);
        }

        /// <summary>Re-reads one campaign until nothing more changes (bounded): chapters, arcs, objectives, the end.</summary>
        public static void Evaluate(WorldData w, CampaignInstanceData c, QuestReporter report)
        {
            var def = Definition(c);
            if (def == null || c.State != CampaignState.Active) return;
            var now = QuestClock.Now(w);
            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var changed = false;

                foreach (var chd in def.Chapters)
                {
                    var ch = c.Chapter(chd.Id);
                    if (ch == null || ch.State != ChapterState.Locked) continue;
                    var ready = true;
                    foreach (var after in chd.After) if (c.Chapter(after)?.State != ChapterState.Completed) ready = false;
                    if (!ready) continue;
                    ch.State = ChapterState.Active;
                    ch.StartedAt = now;
                    c.Note(now, "Chapter: " + chd.Title + ".");
                    Report(w, c, report, HistoricalEventType.CampaignChapter, EventSeverity.Info, EventImportance.Minor, "entered a new chapter, \"" + chd.Title + "\"", Settlement(w, c));
                    changed = true;
                }

                foreach (var ad in def.Arcs)
                {
                    var a = c.Arc(ad.Id);
                    if (a == null) continue;
                    if (a.State == ArcState.Locked)
                    {
                        if (c.Chapter(ad.ChapterId)?.State == ChapterState.Locked) continue;
                        var ready = true;
                        foreach (var after in ad.After) if (c.Arc(after)?.IsClosed != true) ready = false;
                        if (!ready || !CampaignConditions.All(w, c, null, null, ad.Conditions)) continue;
                        Begin(w, c, ad, a, report);
                        changed = true;
                        continue;
                    }

                    if (a.State != ArcState.Active) continue;
                    foreach (var f in ad.Fallbacks)
                    {
                        if (c.Quests.ContainsKey(f.Quest) || !CampaignConditions.All(w, c, null, null, f.When)) continue;
                        if (f.Note != null) c.Note(now, Text(w, c, f.Note));
                        StartQuest(w, c, f.Quest, report);
                        changed = true;
                    }

                    if (Progress(w, c, ad, a, now)) changed = true;
                    var why = FailureNow(w, c, ad, a, now);
                    if (why != null) { MoveArc(w, c, ad, a, ArcState.Failed, null, why, report); changed = true; continue; }
                    if (AllDone(ad, a)) { MoveArc(w, c, ad, a, ArcState.Completed, a.Outcome, null, report); changed = true; }
                }

                foreach (var chd in def.Chapters)
                {
                    var ch = c.Chapter(chd.Id);
                    if (ch == null || ch.State != ChapterState.Active) continue;
                    var done = true;
                    foreach (var ad in def.Arcs)
                    {
                        if (ad.ChapterId != chd.Id || ad.Optional) continue;
                        if (c.Arc(ad.Id)?.IsClosed != true) done = false;
                    }

                    if (!done) continue;
                    ch.State = ChapterState.Completed;
                    ch.ClosedAt = now;
                    c.Note(now, "Chapter closed: " + chd.Title + ".");
                    changed = true;
                }

                if (c.Arc(def.FinalArc)?.IsClosed == true)
                {
                    Finish(w, c, def, report);
                    return;
                }

                if (!changed) break;
            }
        }

        private static void Begin(WorldData w, CampaignInstanceData c, ArcDefinition ad, ArcProgressData a, QuestReporter report)
        {
            var now = QuestClock.Now(w);
            a.State = ArcState.Active;
            a.StartedAt = now;
            c.Note(now, "Began: " + ad.Title + ".");
            foreach (var key in ad.StartQuests) StartQuest(w, c, key, report);
            Reveal(Definition(c), ad, a);
        }

        /// <summary>Makes a campaign quest available, bound to the campaign's people (once per key).</summary>
        public static QuestInstanceData StartQuest(WorldData w, CampaignInstanceData c, string key, QuestReporter report)
        {
            if (c.Quests.TryGetValue(key, out var existing)) return w.FindQuest(existing);
            var def = Definition(c);
            var qr = def?.Quest(key);
            var qd = qr == null ? null : QuestCatalog.Find(qr.QuestId);
            if (qd == null) return null;
            var given = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in qr.Roles)
            {
                var id = Resolve(w, c, pair.Value);
                if (id != null) given[pair.Key] = id;
            }

            if (!given.ContainsKey("giver"))
            {
                c.Note(QuestClock.Now(w), $"Nobody is left to ask about \"{qd.Name}\".");
                c.Quests[key] = string.Empty; // never retried: the person it needed is gone
                return null;
            }

            var q = QuestLifecycle.Create(w, qd, given, QuestSourcePrefix + c.CampaignId + ":" + key, null, report, out var problem);
            c.Quests[key] = q?.InstanceId ?? string.Empty;
            if (q == null) c.Note(QuestClock.Now(w), $"\"{qd.Name}\" could not be offered: {problem}");
            return q;
        }

        /// <summary>Objective progress from the world (quests, decisions, conditions). True when anything changed.</summary>
        private static bool Progress(WorldData w, CampaignInstanceData c, ArcDefinition ad, ArcProgressData a, long now)
        {
            var changed = false;
            foreach (var o in ad.Objectives)
            {
                var p = a.Objective(o.Id);
                if (p == null || p.Done || p.Failed) continue;
                if (o.UnavailableWhen.Count > 0 && CampaignConditions.All(w, c, null, null, o.UnavailableWhen))
                {
                    p.Failed = true;
                    changed = true;
                    continue;
                }

                if (!StageOpen(ad, a, o.Stage)) continue;
                var done = false;
                var failed = false;
                switch (o.Kind)
                {
                    case ArcObjectiveKind.Quest:
                    {
                        var q = c.Quests.TryGetValue(o.QuestKey ?? string.Empty, out var id) ? w.FindQuest(id) : null;
                        if (c.Quests.TryGetValue(o.QuestKey ?? string.Empty, out var none) && none.Length == 0) failed = true; // it could never be offered
                        if (q == null) break;
                        if (q.State == QuestState.Completed) { if (o.QuestOutcome == null || o.QuestOutcome == q.Outcome) done = true; else failed = true; }
                        else if (q.State == QuestState.Failed || q.State == QuestState.Abandoned) failed = true;
                        break;
                    }
                    case ArcObjectiveKind.Decision:
                    {
                        var d = c.Decision(o.DecisionId);
                        if (d == null) break;
                        if (o.ChoiceId == null || o.ChoiceId == d.ChoiceId) done = true;
                        else failed = true;
                        break;
                    }
                    case ArcObjectiveKind.State:
                        done = CampaignConditions.All(w, c, null, null, o.Conditions);
                        break;
                }

                if (done)
                {
                    p.Done = true;
                    p.Revealed = true;
                    p.DoneAt = now;
                    if (o.Outcome != null && a.Outcome == null) a.Outcome = o.Outcome;
                    c.Note(now, "Done: " + Text(w, c, o.Text) + ".");
                    changed = true;
                }
                else if (failed)
                {
                    p.Failed = true;
                    changed = true;
                }
            }

            if (Reveal(Definition(c), ad, a)) changed = true;
            return changed;
        }

        private static bool Reveal(CampaignDefinition def, ArcDefinition ad, ArcProgressData a)
        {
            var changed = false;
            foreach (var o in ad.Objectives)
            {
                var p = a.Objective(o.Id);
                if (p == null || p.Revealed) continue;
                if (o.Hidden && !StageOpen(ad, a, o.Stage)) continue;
                if (o.Hidden && o.Group != null) continue; // an alternative shows itself when the way to it opens (a dialogue, a condition)
                p.Revealed = true;
                changed = true;
            }

            return changed;
        }

        /// <summary>A stage is open when every required group of each earlier stage is done (any member of a group does it).</summary>
        public static bool StageOpen(ArcDefinition ad, ArcProgressData a, int stage)
        {
            for (var s = 0; s < stage; s++)
                if (!StageDone(ad, a, s)) return false;
            return true;
        }

        private static bool StageDone(ArcDefinition ad, ArcProgressData a, int stage)
        {
            var groups = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var o in ad.Objectives)
            {
                if (o.Stage != stage || o.Optional) continue;
                var key = o.Group ?? "#" + o.Id;
                var done = a.Objective(o.Id)?.Done == true;
                groups[key] = (groups.TryGetValue(key, out var was) && was) || done;
            }

            foreach (var g in groups.Values) if (!g) return false;
            return true;
        }

        public static bool AllDone(ArcDefinition ad, ArcProgressData a)
        {
            var last = 0;
            foreach (var o in ad.Objectives) last = Math.Max(last, o.Stage);
            for (var s = 0; s <= last; s++) if (!StageDone(ad, a, s)) return false;
            return true;
        }

        /// <summary>Why an active arc has failed now, or null: its failure conditions, the world's patience, or no way left.</summary>
        private static string FailureNow(WorldData w, CampaignInstanceData c, ArcDefinition ad, ArcProgressData a, long now)
        {
            if (ad.FailWhen.Count > 0 && CampaignConditions.All(w, c, null, null, ad.FailWhen)) return Text(w, c, ad.FailText ?? "it can no longer be done");
            if (ad.TimeLimitDays > 0 && a.StartedAt >= 0 && now - a.StartedAt > ad.TimeLimitDays * (long)Domain.Calendar.WorldTime.MinutesPerDay)
                return Text(w, c, ad.ExpiryText ?? "the world did not wait");
            var groups = new Dictionary<string, (bool open, ArcObjectiveDefinition first)>(StringComparer.Ordinal);
            foreach (var o in ad.Objectives)
            {
                if (o.Optional) continue;
                var key = o.Group ?? "#" + o.Id;
                var p = a.Objective(o.Id);
                var open = p != null && !p.Failed;
                groups[key] = groups.TryGetValue(key, out var was) ? (was.open || open, was.first) : (open, o);
            }

            foreach (var g in groups.Values)
                if (!g.open) return "no way remains to " + Lower(Text(w, c, g.first.Text));
            return null;
        }

        private static string Lower(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        private static void MoveArc(WorldData w, CampaignInstanceData c, ArcDefinition ad, ArcProgressData a, ArcState to, string outcome, string why, QuestReporter report)
        {
            if (!CampaignStates.CanMove(a.State, to)) return;
            var now = QuestClock.Now(w);
            if (a.State == ArcState.Locked && to != ArcState.Abandoned && to != ArcState.Failed) return;
            a.State = to;
            a.ClosedAt = now;
            if (outcome != null) a.Outcome = outcome;
            a.Reason = why;
            var effects = to == ArcState.Completed ? ad.OnComplete : to == ArcState.Failed ? ad.OnFail : null;
            if (effects != null)
                for (var i = 0; i < effects.Count; i++)
                    ApplyEffect(w, c, effects[i], "arc:" + ad.Id + ":" + to + ":" + i.ToString(CultureInfo.InvariantCulture), report);
            c.Note(now, (to == ArcState.Completed ? "Completed: " : to == ArcState.Failed ? "Failed: " : "Refused: ") + ad.Title + (why == null ? "." : " — " + why + "."));
            if (to == ArcState.Failed)
                Report(w, c, report, HistoricalEventType.CampaignConsequence, EventSeverity.Warning, EventImportance.Minor, "\"" + ad.Title + "\" came to nothing: " + why, Settlement(w, c));
        }

        private static void Finish(WorldData w, CampaignInstanceData c, CampaignDefinition def, QuestReporter report)
        {
            EndingDefinition ending = null;
            foreach (var e in def.Endings)
                if (CampaignConditions.All(w, c, null, null, e.Conditions)) { ending = e; break; }
            c.Ending = ending?.Id;
            var now = QuestClock.Now(w);
            if (ending != null) c.Note(now, Text(w, c, ending.Title) + ": " + Text(w, c, ending.Text));
            Move(w, c, ending?.Failure == true ? CampaignState.Failed : CampaignState.Completed, ending == null ? null : Text(w, c, ending.Title), report);
        }

        // ----- Decisions --------------------------------------------------------------------------------------

        /// <summary>Why a choice cannot be made now (in plain words), or null when it can.</summary>
        public static string CanDecide(WorldData w, CampaignInstanceData c, string decisionId, string choiceId)
        {
            var def = Definition(c);
            var d = def?.Decision(decisionId);
            if (d == null) return "There is no such decision.";
            if (c.State == CampaignState.Suspended) return "The campaign is set aside; take it up again first.";
            if (c.State != CampaignState.Active) return "The campaign is over.";
            if (c.Decision(decisionId) != null) return "That has already been decided.";
            if (c.Arc(d.ArcId)?.State != ArcState.Active) return "That question is not before you yet.";
            if (!CampaignConditions.All(w, c, null, null, d.Conditions)) return "That question is not before you yet.";
            var choice = d.Choice(choiceId);
            if (choice == null) return "That is not one of the choices.";
            if (!CampaignConditions.All(w, c, null, null, choice.Conditions)) return Text(w, c, choice.UnavailableText ?? "That choice is not open to you.");
            return null;
        }

        /// <summary>
        /// Makes a decision (ADR-0083): recorded once, its effects applied once each (keyed), its delayed consequences
        /// scheduled, the companions present reacting, and the campaign re-read. Returns a reason it cannot, or null.
        /// </summary>
        public static string Decide(WorldData w, CampaignInstanceData c, string decisionId, string choiceId, string source, QuestReporter report)
        {
            var why = CanDecide(w, c, decisionId, choiceId);
            if (why != null) return why;
            var d = Definition(c).Decision(decisionId);
            var choice = d.Choice(choiceId);
            var now = QuestClock.Now(w);
            c.Decisions.Add(new DecisionRecordData { DecisionId = d.Id, ChoiceId = choice.Id, At = now, Source = source ?? "the journal" });
            c.Note(now, "Decided: " + Text(w, c, choice.Summary) + ".");
            Report(w, c, report, HistoricalEventType.CampaignDecision, EventSeverity.Info, EventImportance.Notable, "decided: " + Text(w, c, choice.Summary), Settlement(w, c));
            for (var i = 0; i < choice.Effects.Count; i++)
                ApplyEffect(w, c, choice.Effects[i], d.Id + ":" + choice.Id + ":" + i.ToString(CultureInfo.InvariantCulture), report);
            foreach (var x in choice.Delayed)
            {
                var key = d.Id + ":" + choice.Id + ":" + x.Id;
                if (c.Pending.Exists(p => p.Key == key)) continue;
                c.Pending.Add(new PendingConsequenceData { Key = key, DecisionId = d.Id, ChoiceId = choice.Id, DelayedId = x.Id, DueAt = now + x.AfterDays * (long)Domain.Calendar.WorldTime.MinutesPerDay });
            }

            var arc = c.Arc(d.ArcId);
            if (choice.Outcome != null && arc != null && arc.Outcome == null) arc.Outcome = choice.Outcome;
            Party.CompanionStories.React(w, c, d, choice, report);
            Evaluate(w, c, report);
            return null;
        }

        /// <summary>Delayed consequences whose time has come and whose conditions hold: each once, whatever the campaign's state now.</summary>
        public static void FireDue(WorldData w, CampaignInstanceData c, QuestReporter report)
        {
            var def = Definition(c);
            if (def == null) return;
            var now = QuestClock.Now(w);
            foreach (var p in c.Pending)
            {
                if (p.Fired || p.Lapsed || now < p.DueAt) continue;
                var x = Delayed(def, p);
                if (x == null) { p.Lapsed = true; continue; }
                if (!CampaignConditions.All(w, c, null, null, x.When)) continue;
                p.Fired = true;
                p.FiredAt = now;
                for (var i = 0; i < x.Effects.Count; i++)
                    ApplyEffect(w, c, x.Effects[i], p.Key + ":" + i.ToString(CultureInfo.InvariantCulture), report);
                var text = Text(w, c, x.Text);
                c.Note(now, "What came of it: " + text);
                Report(w, c, report, HistoricalEventType.CampaignConsequence, EventSeverity.Info, EventImportance.Notable, text, Settlement(w, c));
            }
        }

        public static DelayedConsequence Delayed(CampaignDefinition def, PendingConsequenceData p)
        {
            var choice = def?.Decision(p.DecisionId)?.Choice(p.ChoiceId);
            if (choice == null) return null;
            foreach (var x in choice.Delayed) if (x.Id == p.DelayedId) return x;
            return null;
        }

        // ----- Effects ----------------------------------------------------------------------------------------

        /// <summary>One typed effect, once (by key), through the authoritative paths of the systems it touches.</summary>
        public static void ApplyEffect(WorldData w, CampaignInstanceData c, CampaignEffect e, string key, QuestReporter report)
        {
            var full = c.InstanceId + ":" + key;
            if (c.Applied.Contains(full)) return;
            c.Applied.Add(full);
            var def = Definition(c);
            var reason = def?.Title ?? c.CampaignId;
            var now = QuestClock.Now(w);
            switch (e.Kind)
            {
                case CampaignEffectKind.Regard:
                {
                    var npc = Npc(w, c, e.Role);
                    if (npc != null) Reputation.Change(w, "npc:" + npc.NpcId, e.Key, e.Amount, full, reason, report);
                    break;
                }
                case CampaignEffectKind.FactionStanding:
                {
                    var f = Faction(w, c, e.Role);
                    if (f != null) Reputation.Change(w, "faction:" + f.FactionId, "Standing", e.Amount, full, reason, report);
                    break;
                }
                case CampaignEffectKind.FactionInfluence:
                {
                    var f = Faction(w, c, e.Role);
                    if (f != null) f.Influence = Math.Max(0, f.Influence + e.Amount);
                    break;
                }
                case CampaignEffectKind.FactionOpinion:
                {
                    var a = Faction(w, c, e.Role);
                    var b = Faction(w, c, e.Key);
                    if (a == null || b == null || a.FactionId == b.FactionId) break;
                    var r = w.FindRelation(a.FactionId, b.FactionId);
                    if (r != null) r.Opinion = Reputation.Clamp(r.Opinion + e.Amount, -100, 100);
                    break;
                }
                case CampaignEffectKind.SettlementStability:
                {
                    var s = Settlement(w, c, e.Role);
                    if (s != null) s.Stability = Reputation.Clamp(s.Stability + e.Amount, 0, 100);
                    break;
                }
                case CampaignEffectKind.SettlementProsperity:
                {
                    var s = Settlement(w, c, e.Role);
                    if (s != null) s.Prosperity = Reputation.Clamp(s.Prosperity + e.Amount, 0, 100);
                    break;
                }
                case CampaignEffectKind.SettlementResource:
                {
                    var s = Settlement(w, c, e.Role);
                    if (s == null) break;
                    var type = new ResourceTypeId(e.Key);
                    s.ResourceInventory.Set(type, Math.Max(0, s.ResourceInventory.Get(type) + e.Amount));
                    break;
                }
                case CampaignEffectKind.NpcMemory:
                {
                    var npc = Npc(w, c, e.Role);
                    if (npc == null || npc.IsDeceased) break;
                    NpcMind.Remember(npc, new NpcMemoryData
                    {
                        MemoryId = new MemoryId(NpcHash.Guid(SeedMixer.Combine(SeedMixer.FromWorldSeed(w.Seed), "campaign-memory:" + full))),
                        Date = w.CurrentSimulationDate, Category = MemoryCategory.Personal, Subject = Text(w, c, e.Text), SettlementId = npc.HomeSettlementId,
                        Salience = e.Amount > 0 ? e.Amount : 60, Source = MemorySource.Experienced,
                    });
                    break;
                }
                case CampaignEffectKind.NpcRelation:
                {
                    var a = Npc(w, c, e.Role);
                    var b = Npc(w, c, e.Key);
                    if (a == null || b == null) break;
                    var r = NpcMind.Relate(w, a.NpcId, b.NpcId, e.Amount < 0 ? NpcRelationCause.Quarrel : NpcRelationCause.Kindness, NpcRelationKind.Acquaintance, w.CurrentSimulationDate);
                    if (r != null) Relations.Shift(r, e.Value, e.Amount, e.Amount < 0 ? NpcRelationCause.Quarrel : NpcRelationCause.Kindness, w.CurrentSimulationDate);
                    break;
                }
                case CampaignEffectKind.CompanionLoyalty:
                {
                    var who = new List<NpcData>();
                    if (e.Role == "party") who.AddRange(Party.CompanionLoyalty.Present(w));
                    else if (Npc(w, c, e.Role) is NpcData one && w.FindCompanion(one.NpcId) != null) who.Add(one);
                    foreach (var n in who) Party.CompanionLoyalty.Apply(w, n.NpcId, full, e.Amount, Text(w, c, e.Text ?? reason), report);
                    break;
                }
                case CampaignEffectKind.StartQuest:
                    if (c.State == CampaignState.Active) StartQuest(w, c, e.Key, report);
                    break;
                case CampaignEffectKind.Discovery:
                    Discover(w, "campaign:" + c.CampaignId + ":" + e.Key, Text(w, c, e.Text));
                    break;
                case CampaignEffectKind.Spread:
                {
                    var s = Settlement(w, c, e.Role);
                    if (s == null) break;
                    var text = Text(w, c, e.Text);
                    foreach (var n in w.Npcs)
                    {
                        if (n.IsDeceased || n.HomeSettlementId != s.SettlementId) continue;
                        var npc = n;
                        NpcMind.Learn(npc, KnowledgeTopic.RecentEvent, "campaign:" + full, text, KnowledgeStatus.Known, 70, w.CurrentSimulationDate, default, 0, default,
                            () => new KnowledgeId(NpcHash.Guid(NpcHash.Of(w.Seed, "campaign-knowledge", full + "|" + npc.NpcId))));
                    }

                    break;
                }
                case CampaignEffectKind.RaiseWorldEvent:
                {
                    if (!Enum.TryParse<WorldEventType>(e.Key, out var type)) break;
                    var route = Route(w, c, e.Role);
                    var s = route == null ? Settlement(w, c, e.Role) : null;
                    var touches = new List<SettlementId>();
                    var ev = WorldEvents.Raise(w, type, "campaign:" + full, now, Math.Max(1, e.Amount), Text(w, c, e.Text), x =>
                    {
                        if (route != null) { x.RouteId = route.RouteId; x.RegionId = route.RegionA; touches.Add(route.A); touches.Add(route.B); }
                        else if (s != null) { x.SettlementId = s.SettlementId; x.RegionId = s.RegionId; touches.Add(s.SettlementId); }
                        x.ExpiresAt = now + 10L * Domain.Calendar.WorldTime.MinutesPerDay;
                        if (type == WorldEventType.FactionConflict)
                        {
                            var fa = Faction(w, c, e.Role);
                            var fb = Faction(w, c, e.Value);
                            if (fa != null) x.FactionA = fa.FactionId;
                            if (fb != null) x.FactionB = fb.FactionId;
                        }
                    }, out var created);
                    if (created) WorldEvents.TellSettlements(w, ev, touches, w.CurrentSimulationDate.ToDayNumber());
                    break;
                }
                case CampaignEffectKind.RouteCondition:
                {
                    var route = Route(w, c, e.Role);
                    if (route != null && Enum.TryParse<RouteCondition>(e.Value, out var cond) && route.BlockedBy == null) route.Condition = cond;
                    break;
                }
                case CampaignEffectKind.Flag:
                    c.Flags[e.Key] = e.Value ?? "yes";
                    break;
                case CampaignEffectKind.ArcState:
                {
                    var ad = def?.Arc(e.Key);
                    var a = c.Arc(e.Key);
                    if (ad == null || a == null || !Enum.TryParse<ArcState>(e.Value, out var to)) break;
                    if (to == ArcState.Active) { if (a.State == ArcState.Locked) Begin(w, c, ad, a, report); }
                    else MoveArc(w, c, ad, a, to, null, Text(w, c, e.Text), report);
                    break;
                }
                case CampaignEffectKind.Experience:
                {
                    var p = w.Player;
                    if (p?.Rpg == null || p.Rpg.State != CreationState.Complete || p.Rpg.IsDead || e.Amount <= 0) break;
                    foreach (var x in p.Rpg.ExperienceLog) if (x.Key == full) return;
                    p.Rpg.ExperienceLog.Add(new ExperienceAward { Key = full, Source = ExperienceSource.Quest, SourceId = c.InstanceId, Amount = e.Amount, Date = w.CurrentSimulationDate, Reason = reason });
                    p.Rpg.Experience += e.Amount;
                    break;
                }
                case CampaignEffectKind.Coin:
                    if (w.Player != null) w.Player.Purse = Math.Max(0, w.Player.Purse + e.Amount);
                    break;
                case CampaignEffectKind.Chronicle:
                    Report(w, c, report, HistoricalEventType.CampaignConsequence, EventSeverity.Info, EventImportance.Minor, Text(w, c, e.Text), Settlement(w, c, e.Role) ?? Settlement(w, c));
                    break;
                case CampaignEffectKind.NpcMorale:
                {
                    var npc = Npc(w, c, e.Role);
                    if (npc != null && !npc.IsDeceased) npc.Condition.Morale += e.Amount;
                    break;
                }
                case CampaignEffectKind.PlayerDeed:
                {
                    var npc = Npc(w, c, e.Role);
                    if (npc != null && !npc.IsDeceased) Deeds.Witness(w, npc, e.Text, c.InstanceId, full);
                    break;
                }
            }
        }

        /// <summary>The player learns a fact, once (by key).</summary>
        public static bool Discover(WorldData w, string key, string text)
        {
            var social = w.Player?.Social;
            if (social == null || key == null || social.Knows(key)) return false;
            social.Discoveries.Add(new DiscoveryData { Key = key, Text = text, At = QuestClock.Now(w), Source = "campaign" });
            while (social.Discoveries.Count > PlayerSocialData.MaxDiscoveries) social.Discoveries.RemoveAt(0);
            return true;
        }

        // ----- Roles ------------------------------------------------------------------------------------------

        /// <summary>The entity a role names now (as text): bound once, or (leaders, first-available) read from the world when asked.</summary>
        public static string Resolve(WorldData w, CampaignInstanceData c, string role)
        {
            if (c == null || role == null) return null;
            var spec = Definition(c)?.Role(role);
            if (spec == null) return c.Bound(role);
            switch (spec.Kind)
            {
                case CampaignRoleKind.FactionLeader:
                {
                    var f = Faction(w, c, spec.Of);
                    return f == null || f.LeaderNpcId.IsEmpty ? null : f.LeaderNpcId.ToString();
                }
                case CampaignRoleKind.FirstAvailable:
                {
                    foreach (var candidate in spec.Candidates)
                    {
                        var npc = NpcById(w, Resolve(w, c, candidate));
                        if (npc == null) continue;
                        var status = CampaignCharacters.StatusOf(w, npc);
                        if (status == CharacterStatus.Available || status == CharacterStatus.Allied || status == CharacterStatus.Injured) return npc.NpcId.ToString();
                    }

                    return null;
                }
                default:
                    return c.Bound(role);
            }
        }

        public static NpcData Npc(WorldData w, CampaignInstanceData c, string role) => NpcById(w, Resolve(w, c, role));

        public static NpcData NpcById(WorldData w, string id) => id != null && Guid.TryParse(id, out var g) ? w.FindNpc(new NpcId(g)) : null;

        /// <summary>A settlement role, or a person's home, or a route's first end.</summary>
        public static SettlementData Settlement(WorldData w, CampaignInstanceData c, string role)
        {
            var id = Resolve(w, c, role);
            if (id == null) return null;
            if (Guid.TryParse(id, out var g))
            {
                if (w.FindSettlement(new SettlementId(g)) is SettlementData s) return s;
                if (w.FindNpc(new NpcId(g)) is NpcData n) return w.FindSettlement(n.HomeSettlementId);
            }

            return null;
        }

        /// <summary>A faction role, or a person's faction, or a settlement's.</summary>
        public static FactionData Faction(WorldData w, CampaignInstanceData c, string role)
        {
            var id = Resolve(w, c, role);
            if (id == null || !Guid.TryParse(id, out var g)) return null;
            if (w.FindFaction(new FactionId(g)) is FactionData f) return f;
            if (w.FindNpc(new NpcId(g)) is NpcData n && !n.FactionId.IsEmpty) return w.FindFaction(n.FactionId);
            if (w.FindSettlement(new SettlementId(g)) is SettlementData s && !s.FactionId.IsEmpty) return w.FindFaction(s.FactionId);
            return null;
        }

        public static RouteSegmentData Route(WorldData w, CampaignInstanceData c, string role) => w.FindRoute(Resolve(w, c, role));

        /// <summary>The campaign's home settlement (its first settlement role), for the Chronicle.</summary>
        private static SettlementData Settlement(WorldData w, CampaignInstanceData c)
        {
            foreach (var spec in Definition(c)?.Roles ?? Array.Empty<CampaignRoleSpec>())
                if (spec.Kind == CampaignRoleKind.Settlement) return Settlement(w, c, spec.Name);
            return null;
        }

        /// <summary>Fills {role} tokens with the names of the world's own people and places, and {player}.</summary>
        public static string Text(WorldData w, CampaignInstanceData c, string text)
        {
            if (string.IsNullOrEmpty(text) || c == null) return text ?? string.Empty;
            var result = text.Replace("{player}", w.Player?.Name ?? "you");
            var def = Definition(c);
            if (def == null || result.IndexOf('{') < 0) return result;
            foreach (var spec in def.Roles)
            {
                var token = "{" + spec.Name + "}";
                if (result.IndexOf(token, StringComparison.Ordinal) < 0) continue;
                result = result.Replace(token, NameOf(w, Resolve(w, c, spec.Name)));
            }

            return result;
        }

        public static string NameOf(WorldData w, string id)
        {
            if (string.IsNullOrEmpty(id)) return "nobody";
            if (w.FindRoute(id) is RouteSegmentData r) return $"the road between {w.FindSettlement(r.A)?.Name} and {w.FindSettlement(r.B)?.Name}";
            return QuestText.Name(w, id);
        }

        private static void Report(WorldData w, CampaignInstanceData c, QuestReporter report, HistoricalEventType type, EventSeverity severity, EventImportance importance, string what, SettlementData where)
        {
            report?.Invoke(type, severity, importance, where == null ? Array.Empty<SettlementId>() : new[] { where.SettlementId },
                new Dictionary<string, string> { ["source"] = "campaign", ["campaign"] = Definition(c)?.Title ?? c.CampaignId, ["instance"] = c.InstanceId, ["what"] = what },
                Array.Empty<NpcId>());
        }
    }

    /// <summary>Small helpers for person-to-person bonds (the Phase 4 records).</summary>
    public static class Relations
    {
        public static void Shift(NpcRelationData r, string dimension, int amount, NpcRelationCause cause, Domain.Calendar.WorldDate today)
        {
            switch (dimension)
            {
                case "Trust": r.Trust += amount; break;
                case "Respect": r.Respect += amount; break;
                case "Fear": r.Fear += amount; break;
                case "Rivalry": r.Rivalry += amount; break;
                default: r.Affinity += amount; break;
            }

            r.LastCause = cause;
            r.LastChanged = today;
            r.Interactions++;
        }

        public static int Read(NpcRelationData r, string dimension)
        {
            if (r == null) return 0;
            switch (dimension)
            {
                case "Trust": return r.Trust;
                case "Respect": return r.Respect;
                case "Fear": return r.Fear;
                case "Rivalry": return r.Rivalry;
                default: return r.Affinity;
            }
        }
    }
}
