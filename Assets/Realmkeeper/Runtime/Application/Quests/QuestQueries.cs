using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application
{
    public sealed class QuestSummaryView
    {
        public string InstanceId { get; internal set; }
        public string Name { get; internal set; }
        public QuestCategory Category { get; internal set; }
        public QuestState State { get; internal set; }
        public string Giver { get; internal set; }
        public string Settlement { get; internal set; }
        public bool Tracked { get; internal set; }

        /// <summary>The next thing to do (the first visible, unfinished required objective), or the ending.</summary>
        public string Next { get; internal set; }
    }

    public sealed class ObjectiveView
    {
        public string Id { get; internal set; }
        public string Text { get; internal set; }
        public bool Done { get; internal set; }
        public bool Failed { get; internal set; }
        public bool Optional { get; internal set; }
        public bool Unavailable { get; internal set; }

        /// <summary>A later stage, not open yet (shown greyed).</summary>
        public bool Locked { get; internal set; }

        /// <summary>"1 / 2" for counted objectives, or null.</summary>
        public string Progress { get; internal set; }

        /// <summary>One of several ways to finish (an alternative).</summary>
        public bool Alternative { get; internal set; }
    }

    public sealed class QuestDetailView
    {
        public string InstanceId { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public QuestCategory Category { get; internal set; }
        public QuestState State { get; internal set; }
        public string Giver { get; internal set; }
        public bool Tracked { get; internal set; }
        public string Outcome { get; internal set; }
        public string FailureReason { get; internal set; }
        public List<ObjectiveView> Objectives { get; } = new List<ObjectiveView>();

        /// <summary>People involved whom the player knows of: (role in words, name, where they are).</summary>
        public List<(string Role, string Name, string Where)> People { get; } = new List<(string, string, string)>();

        /// <summary>Places the player knows of, with whether they have been there.</summary>
        public List<(string Name, bool Visited)> Places { get; } = new List<(string, bool)>();

        public List<string> Rewards { get; } = new List<string>();
        public List<string> FailsIf { get; } = new List<string>();
        public List<string> Decisions { get; } = new List<string>();
        public List<(string When, string Text)> History { get; } = new List<(string, string)>();
    }

    public sealed class PersonStandingView
    {
        public NpcId NpcId { get; internal set; }
        public string Name { get; internal set; }
        public ReputationTier Tier { get; internal set; }
        public int Familiarity { get; internal set; }
        public int Affinity { get; internal set; }
        public int Trust { get; internal set; }
        public int Respect { get; internal set; }
        public int Fear { get; internal set; }
        public int Gratitude { get; internal set; }
        public int Hostility { get; internal set; }
    }

    public sealed class ReputationView
    {
        public List<(string Faction, int Standing, ReputationTier Tier)> Factions { get; } = new List<(string, int, ReputationTier)>();
        public List<(string Settlement, int Reputation, ReputationTier Tier)> Settlements { get; } = new List<(string, int, ReputationTier)>();
        public int Notoriety { get; internal set; }
        public List<PersonStandingView> People { get; } = new List<PersonStandingView>();

        /// <summary>The latest changes, newest first, in words.</summary>
        public List<string> Recent { get; } = new List<string>();
    }

    public sealed class JournalView
    {
        public List<QuestSummaryView> Active { get; } = new List<QuestSummaryView>();
        public List<QuestSummaryView> Available { get; } = new List<QuestSummaryView>();
        public List<QuestSummaryView> Completed { get; } = new List<QuestSummaryView>();
        public List<QuestSummaryView> Failed { get; } = new List<QuestSummaryView>();
        public List<QuestSummaryView> Abandoned { get; } = new List<QuestSummaryView>();
        public List<(string When, string Text)> Rumors { get; } = new List<(string, string)>();
        public List<(string When, string Text)> Discoveries { get; } = new List<(string, string)>();
        public List<(string When, string Text)> Decisions { get; } = new List<(string, string)>();

        /// <summary>Chronicle entries the player can know: their own deeds and events in places they have been.</summary>
        public List<(string When, string Text)> Events { get; } = new List<(string, string)>();
        public int TotalQuests { get; internal set; }
    }

    public sealed partial class WorldSession
    {
        public static string When(long minute)
        {
            if (minute < 0) return string.Empty;
            var date = WorldDate.FromDayNumber(minute / WorldTime.MinutesPerDay);
            return date + " " + WorldTime.Clock((int)(minute % WorldTime.MinutesPerDay));
        }

        public DialogueView GetConversation() => HasWorld ? DialogueEngine.View(_world) : null;

        private QuestSummaryView Summary(QuestInstanceData q)
        {
            var def = QuestCatalog.Find(q.DefinitionId);
            var giver = QuestEffects.Person(_world, q, "giver");
            var s = QuestEffects.SettlementOf(_world, q, "settlement");
            return new QuestSummaryView
            {
                InstanceId = q.InstanceId, Name = QuestText.Fill(_world, q, def?.Name ?? q.DefinitionId), Category = def?.Category ?? QuestCategory.Side, State = q.State,
                Giver = giver?.Name, Settlement = s?.Name, Tracked = q.Tracked, Next = NextStep(q, def),
            };
        }

        private string NextStep(QuestInstanceData q, QuestDefinition def)
        {
            if (def == null) return null;
            if (q.State == QuestState.Completed) return "Completed" + (q.Outcome != null ? " (" + q.Outcome + ")" : string.Empty) + ".";
            if (q.State == QuestState.Failed) return q.FailureReason;
            if (!q.IsOpen) return QuestText.Fill(_world, q, def.Description);
            foreach (var o in def.Objectives)
            {
                var p = q.Progress(o.Id);
                if (o.Optional || p == null || !p.Revealed || p.Done || p.Failed || p.Unavailable || !QuestObjectives.StageOpen(def, q, o.Stage)) continue;
                return QuestText.Fill(_world, q, o.Text) + (o.Quantity > 1 ? $" ({p.Count}/{o.Quantity})" : string.Empty);
            }

            return null;
        }

        /// <summary>The journal (ADR-0055 for screens; ADR-0060 for what the player knows): only what the player has learned is shown.</summary>
        public JournalView GetJournal()
        {
            var j = new JournalView();
            var p = _world?.Player;
            if (p == null) return j;
            foreach (var q in _world.Quests)
            {
                switch (q.State)
                {
                    case QuestState.Active:
                    case QuestState.Accepted: j.Active.Add(Summary(q)); break;
                    case QuestState.Discovered:
                    case QuestState.Offered: j.Available.Add(Summary(q)); break;
                    case QuestState.Completed: j.Completed.Add(Summary(q)); break;
                    case QuestState.Failed: j.Failed.Add(Summary(q)); break;
                    case QuestState.Abandoned: j.Abandoned.Add(Summary(q)); break;
                    default: continue; // Unavailable and undiscovered quests are the world's, not the player's
                }

                j.TotalQuests++;
            }

            var s = p.Social;
            for (var i = s.Rumors.Count - 1; i >= 0; i--) j.Rumors.Add((When(s.Rumors[i].At), s.Rumors[i].Text + (s.Rumors[i].Verified ? " (confirmed)" : string.Empty)));
            for (var i = s.Discoveries.Count - 1; i >= 0; i--) j.Discoveries.Add((When(s.Discoveries[i].At), s.Discoveries[i].Text));
            for (var i = s.Decisions.Count - 1; i >= 0; i--) j.Decisions.Add((When(s.Decisions[i].At), s.Decisions[i].Text));
            for (var i = _world.HistoricalEvents.Count - 1; i >= 0 && j.Events.Count < 40; i--)
            {
                var e = _world.HistoricalEvents[i];
                if (!PlayerKnows(e, s)) continue;
                j.Events.Add((e.Date.ToString(), EventFormatter.Format(e, NameOf, FactionNameOf)));
            }

            return j;
        }

        /// <summary>The Chronicle as the player can know it: their own doings, and what happened where they have been (ADR-0060).</summary>
        private static bool PlayerKnows(HistoricalEvent e, PlayerSocialData s)
        {
            var source = e.Get("source");
            if (source == "player" || source == "quest" || source == "combat") return true;
            if (e.Importance == EventImportance.Minor) return false;
            foreach (var id in e.SettlementIds) if (s.VisitedSettlements.Contains(id)) return true;
            return false;
        }

        public QuestDetailView GetQuest(string instanceId)
        {
            var q = _world?.FindQuest(instanceId);
            if (q == null || q.State == QuestState.Unavailable || q.State == QuestState.Available) return null; // the player does not know of it
            var def = QuestCatalog.Find(q.DefinitionId);
            var social = _world.Player.Social;
            var giver = QuestEffects.Person(_world, q, "giver");
            var v = new QuestDetailView
            {
                InstanceId = q.InstanceId, Name = QuestText.Fill(_world, q, def?.Name), Description = QuestText.Fill(_world, q, def?.Description), Category = def?.Category ?? QuestCategory.Side,
                State = q.State, Giver = giver?.Name, Tracked = q.Tracked, Outcome = q.Outcome, FailureReason = q.FailureReason,
            };
            if (def == null) return v;
            var mentioned = new HashSet<string> { "giver" };
            foreach (var o in def.Objectives)
            {
                var p = q.Progress(o.Id);
                if (p == null || !p.Revealed) continue; // hidden objectives stay hidden until revealed
                v.Objectives.Add(new ObjectiveView
                {
                    Id = o.Id, Text = QuestText.Fill(_world, q, o.Text), Done = p.Done, Failed = p.Failed, Optional = o.Optional, Unavailable = p.Unavailable,
                    Locked = q.IsOpen && !QuestObjectives.StageOpen(def, q, o.Stage), Progress = o.Quantity > 1 ? $"{p.Count} / {o.Quantity}" : null, Alternative = o.Group != null,
                });
                if (o.Target != null && o.Target.StartsWith("role:", StringComparison.Ordinal)) mentioned.Add(o.Target.Substring(5));
                if (o.To != null) mentioned.Add(o.To);
            }

            foreach (var spec in def.Roles)
            {
                if (!mentioned.Contains(spec.Name)) continue;
                if (spec.Kind == RoleKind.Npc)
                {
                    var npc = QuestEffects.Person(_world, q, spec.Name);
                    if (npc == null) continue;
                    var knowsWhere = social.Knows("whereabouts:" + npc.NpcId) || npc.HasMetPlayer;
                    var where = knowsWhere ? _world.FindSettlement(npc.HomeSettlementId)?.Name : "where, you do not know yet";
                    v.People.Add((spec.Name == "giver" ? "asked for help" : spec.Name.Replace('_', ' '), npc.Name, where));
                }
                else if (spec.Kind == RoleKind.Settlement && q.Bound(spec.Name) is string sid && social.KnownPlaces.Contains(sid))
                {
                    var settlement = Guid.TryParse(sid, out var g) ? _world.FindSettlement(new SettlementId(g)) : null;
                    if (settlement != null) v.Places.Add((settlement.Name, social.VisitedSettlements.Contains(settlement.SettlementId)));
                }
            }

            foreach (var spec in def.Roles)
                if (spec.Kind == RoleKind.Settlement && q.Bound(spec.Name) is string sid2 && social.KnownPlaces.Contains(sid2) && !mentioned.Contains(spec.Name))
                {
                    var settlement = Guid.TryParse(sid2, out var g2) ? _world.FindSettlement(new SettlementId(g2)) : null;
                    if (settlement != null && !v.Places.Exists(x => x.Name == settlement.Name)) v.Places.Add((settlement.Name, social.VisitedSettlements.Contains(settlement.SettlementId)));
                }

            foreach (var r in def.Rewards)
                if (r.Shown != null && (r.Outcome == null || q.Outcome == null || r.Outcome == q.Outcome))
                {
                    var text = QuestText.Fill(_world, q, r.Shown);
                    if (!v.Rewards.Contains(text)) v.Rewards.Add(text);
                }

            foreach (var f in def.Failures) v.FailsIf.Add(QuestText.Fill(_world, q, f.Text));
            foreach (var d in social.Decisions) if (d.QuestInstanceId == q.InstanceId) v.Decisions.Add(d.Text);
            foreach (var h in q.History) v.History.Add((When(h.At), h.Text));
            return v;
        }

        // ----- For tools, captures and tests (the UI uses the journal, which hides what the player does not know) -----

        /// <summary>The first instance of a definition that is not closed (authored before emergent), whether the player knows of it or not.</summary>
        public string FindQuestInstance(string definitionId)
        {
            if (!HasWorld) return null;
            foreach (var q in _world.Quests) if (q.DefinitionId == definitionId && q.SourceKey == "authored" && !q.IsClosed && q.State != QuestState.Unavailable) return q.InstanceId;
            foreach (var q in _world.Quests) if (q.DefinitionId == definitionId && !q.IsClosed && q.State != QuestState.Unavailable) return q.InstanceId;
            return null;
        }

        /// <summary>An instance's roles and what fills them (IDs as text).</summary>
        public IReadOnlyDictionary<string, string> GetQuestBindings(string instanceId) => _world?.FindQuest(instanceId) is QuestInstanceData q ? new Dictionary<string, string>(q.Bindings) : null;

        public SettlementId HomeOf(NpcId npc) => _world?.FindNpc(npc)?.HomeSettlementId ?? default;

        /// <summary>Quests the player tracks, with their next step, for the exploration HUD.</summary>
        public IReadOnlyList<QuestSummaryView> GetTrackedQuests()
        {
            var list = new List<QuestSummaryView>();
            if (!HasWorld) return list;
            foreach (var q in _world.Quests) if (q.Tracked && q.IsOpen) list.Add(Summary(q));
            return list;
        }

        public ReputationView GetReputation()
        {
            var v = new ReputationView();
            var p = _world?.Player;
            if (p == null) return v;
            var t = ReputationThresholds.Default;
            var s = p.Social;
            v.Notoriety = s.Notoriety;
            foreach (var f in s.FactionStandings) v.Factions.Add((_world.FindFaction(f.FactionId)?.Name ?? "a faction", f.Standing, t.ForStanding(f.Standing, s.Notoriety)));
            foreach (var r in s.SettlementStandings) v.Settlements.Add((_world.FindSettlement(r.SettlementId)?.Name ?? "a settlement", r.Reputation, t.ForStanding(r.Reputation, s.Notoriety)));
            foreach (var n in _world.Npcs)
            {
                var r = n.PlayerRegard;
                if (n.IsDeceased || (r.Familiarity == 0 && r.Trust == 0 && r.Respect == 0 && r.Fear == 0 && r.Gratitude == 0 && r.Hostility == 0 && r.Affinity == 0)) continue;
                v.People.Add(new PersonStandingView
                {
                    NpcId = n.NpcId, Name = n.Name, Tier = Reputation.Of(r), Familiarity = r.Familiarity, Affinity = r.Affinity, Trust = r.Trust, Respect = r.Respect, Fear = r.Fear,
                    Gratitude = r.Gratitude, Hostility = r.Hostility,
                });
            }

            v.People.Sort((a, b) => b.Familiarity.CompareTo(a.Familiarity));
            for (var i = s.Ledger.Count - 1; i >= 0 && v.Recent.Count < 15; i--)
            {
                var e = s.Ledger[i];
                v.Recent.Add($"{Reputation.TargetName(_world, e.Target)}: {e.Dimension.ToLowerInvariant()} {(e.Amount >= 0 ? "+" : string.Empty)}{e.Amount}" + (string.IsNullOrEmpty(e.Reason) ? string.Empty : $" ({e.Reason})"));
            }

            return v;
        }
    }
}
