using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Quests;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 12 (ADR-0055, ADR-0060): the Journal section of the Player Hub — quests by state, the selected quest's
    /// objectives (hidden ones stay hidden), the people and places it involves as far as the player knows them, its rewards
    /// where known and its history; rumours, discoveries, decisions, the events the player can know of, and reputation.
    /// Tracking and abandoning go through commands (via <see cref="ItemActionRequested"/>).
    /// </summary>
    public sealed partial class PlayerHubWindow
    {
        public Func<JournalView> JournalSource { get; set; }
        public Func<string, QuestDetailView> QuestSource { get; set; }
        public Func<ReputationView> ReputationSource { get; set; }
        public Func<IReadOnlyList<PlaceSummaryView>> PlacesSource { get; set; } // Phase 13
        public Func<List<CrimeView>> CrimesSource { get; set; } // Phase 14

        public static readonly IReadOnlyList<string> JournalTabs = new[] { "Active", "Available", "Completed", "Failed", "Abandoned", "Rumours", "Discoveries", "Decisions", "Events", "Places", "Crimes", "Reputation" }; // Phase 14: Crimes

        private string _journalTab = "Active";
        private string _selectedQuest;

        public string JournalTab => _journalTab;
        public string SelectedQuest => _selectedQuest;

        public void SelectJournal(string tab, string questId = null)
        {
            if (tab != null) _journalTab = tab;
            _selectedQuest = questId ?? _selectedQuest;
            Section = "Journal";
            Render();
        }

        private void Journal()
        {
            var j = JournalSource?.Invoke();
            if (j == null)
            {
                _content.Add(Text("The journal is not available.", "xk-w-empty"));
                return;
            }

            var tabs = Row("xk-inv-groups");
            foreach (var t in JournalTabs)
            {
                var tab = t;
                var count = t == "Active" ? j.Active.Count : t == "Available" ? j.Available.Count : t == "Completed" ? j.Completed.Count : t == "Failed" ? j.Failed.Count
                    : t == "Abandoned" ? j.Abandoned.Count : t == "Rumours" ? j.Rumors.Count : t == "Discoveries" ? j.Discoveries.Count : t == "Decisions" ? j.Decisions.Count : -1;
                var b = Act(count >= 0 ? $"{t} ({count})" : t, () => { _journalTab = tab; Render(); });
                b.name = "xk-journal-tab-" + t;
                b.EnableInClassList("xk-w-choice--on", _journalTab == t);
                tabs.Add(b);
            }

            _content.Add(tabs);
            var questTab = _journalTab == "Active" || _journalTab == "Available" || _journalTab == "Completed" || _journalTab == "Failed" || _journalTab == "Abandoned";
            if (_selectedQuest != null && questTab) QuestDetail(_selectedQuest); // the detail belongs with the quest lists

            switch (_journalTab)
            {
                case "Active": QuestList(j.Active, "No quests in progress. Talk to people, read notice boards, listen to rumours."); break;
                case "Available": QuestList(j.Available, "Nothing you have heard of waits for an answer."); break;
                case "Completed": QuestList(j.Completed, "None yet."); break;
                case "Failed": QuestList(j.Failed, "None."); break;
                case "Abandoned": QuestList(j.Abandoned, "None."); break;
                case "Rumours": Lines(j.Rumors, "No rumours heard. Ask people what they have heard."); break;
                case "Discoveries": Lines(j.Discoveries, "Nothing found out yet."); break;
                case "Decisions": Lines(j.Decisions, "No decisions that matter yet."); break;
                case "Events": Lines(j.Events, "Nothing yet."); break;
                case "Reputation": ReputationPanel(); break;
                case "Places": PlacesPanel(); break;
                case "Crimes": CrimesPanel(); break; // Phase 14
            }
        }

        private void QuestList(List<QuestSummaryView> list, string empty)
        {
            if (list.Count == 0) _content.Add(Text(empty, "xk-w-empty"));
            foreach (var q in list)
            {
                var id = q.InstanceId;
                var row = Row("xk-w-row");
                row.name = "xk-journal-quest-" + q.InstanceId;
                row.EnableInClassList("xk-w-row--selected", q.InstanceId == _selectedQuest);
                row.Add(Text((q.Tracked ? "◆ " : string.Empty) + q.Name, "xk-w-name"));
                row.Add(Text(q.Giver ?? string.Empty, "xk-w-price"));
                row.Add(Act("Open", () => { _selectedQuest = id; Render(); }));
                _content.Add(row);
                if (!string.IsNullOrEmpty(q.Next)) _content.Add(Text(q.Next, "xk-w-desc"));
            }
        }

        private void Lines(List<(string When, string Text)> lines, string empty)
        {
            if (lines.Count == 0) _content.Add(Text(empty, "xk-w-empty"));
            foreach (var (when, text) in lines) _content.Add(Text(when + " — " + text, "xk-w-ledger-line"));
        }

        private void QuestDetail(string id)
        {
            var q = QuestSource?.Invoke(id);
            if (q == null) return;
            var panel = Column("xk-inv-details");
            panel.name = "xk-journal-detail";
            var head = Row("xk-w-head");
            var title = Text(q.Name, "xk-w-heading");
            title.style.flexGrow = 1;
            head.Add(title);
            if (q.State == QuestState.Active)
            {
                var track = Act(q.Tracked ? "Untrack" : "Track", () => Run(s => s.Dispatch(new TrackQuestCommand(id, !q.Tracked))));
                track.name = "xk-journal-track";
                head.Add(track);
                var abandon = Act("Abandon", () => Run(s => s.Dispatch(new AbandonQuestCommand(id))));
                abandon.name = "xk-journal-abandon";
                head.Add(abandon);
            }

            head.Add(Act("Close", () => { _selectedQuest = null; Render(); }));
            panel.Add(head);
            panel.Add(Text($"{q.Category} · {q.State}" + (q.Giver != null ? " · for " + q.Giver : string.Empty) + (q.Outcome != null ? " · ended: " + q.Outcome : string.Empty), "xk-w-subtitle"));
            panel.Add(Text(q.Description, "xk-w-desc"));
            if (!string.IsNullOrEmpty(q.FailureReason)) panel.Add(Text("Failed: " + q.FailureReason, "xk-inv-warning"));
            panel.Add(Text("Objectives", "xk-w-key"));
            foreach (var o in q.Objectives)
            {
                var mark = o.Done ? "✓" : o.Failed ? "✗" : o.Unavailable ? "–" : o.Locked ? "·" : "○";
                var line = Text($"{mark} {(o.Alternative ? "or: " : string.Empty)}{o.Text}{(o.Progress != null ? "  (" + o.Progress + ")" : string.Empty)}{(o.Optional ? "  (optional)" : string.Empty)}", "xk-journal-objective");
                line.name = "xk-journal-objective-" + o.Id;
                line.EnableInClassList("xk-journal-objective--done", o.Done);
                line.EnableInClassList("xk-journal-objective--locked", o.Locked || o.Unavailable);
                panel.Add(line);
            }

            if (q.People.Count > 0)
            {
                panel.Add(Text("People", "xk-w-key"));
                foreach (var (role, name, where) in q.People) panel.Add(Text($"{name} ({role}) — {where}", "xk-w-desc"));
            }

            if (q.Places.Count > 0)
            {
                panel.Add(Text("Places", "xk-w-key"));
                foreach (var (name, visited) in q.Places) panel.Add(Text(name + (visited ? " (visited)" : " (not yet visited)"), "xk-w-desc"));
            }

            if (q.Rewards.Count > 0) panel.Add(Pair("Rewards", string.Join(" · ", q.Rewards)));
            if (q.FailsIf.Count > 0 && q.State == QuestState.Active) panel.Add(Pair("Fails if", string.Join(" ", q.FailsIf)));
            foreach (var d in q.Decisions) panel.Add(Pair("Decision", d));
            if (q.History.Count > 0)
            {
                panel.Add(Text("History", "xk-w-key"));
                for (var i = q.History.Count - 1; i >= 0 && i >= q.History.Count - 6; i--) panel.Add(Text(q.History[i].When + " — " + q.History[i].Text, "xk-w-ledger-line"));
            }

            _content.Add(panel);
        }

        /// <summary>Phase 13 (ADR-0065): the places the player knows of — a rumour gives only a name and a direction.</summary>
        private void CrimesPanel()
        {
            var crimes = CrimesSource?.Invoke();
            if (crimes == null || crimes.Count == 0)
            {
                _content.Add(Text("No crimes of yours that anyone knows of.", "xk-w-empty"));
                return;
            }

            var box = Column("xk-store");
            box.name = "xk-journal-crimes";
            foreach (var c in crimes)
            {
                var row = Row("xk-w-row");
                row.name = "xk-journal-crime-" + c.Id;
                row.Add(Text(c.What, "xk-w-name"));
                row.Add(Text(c.Status.ToString() + (c.Known ? " · known to be you" : string.Empty) + (c.Bounty > 0 ? $" · bounty {c.Bounty}" : string.Empty), "xk-w-price"));
                box.Add(row);
                box.Add(Text(c.Where, "xk-w-desc"));
                foreach (var s in c.Seen) box.Add(Text("· " + s, "xk-w-desc"));
                if (!string.IsNullOrEmpty(c.Consequence)) box.Add(Text(c.Consequence, "xk-w-note"));
            }

            _content.Add(box);
        }

        private void PlacesPanel()
        {
            var places = PlacesSource?.Invoke();
            if (places == null || places.Count == 0)
            {
                _content.Add(Text("You know of no places beyond the towns. Ask people what they have heard.", "xk-w-empty"));
                return;
            }

            var box = Column("xk-store");
            box.name = "xk-journal-places";
            foreach (var p in places)
            {
                var state = p.Discovery == Realmkeeper.Domain.Places.PlaceDiscovery.Rumored ? "Heard of" : p.Discovery.ToString();
                var row = Pair(p.Name, $"{p.Kind} · {state} · {p.Direction}" + (p.Visits > 0 ? $" · visited {p.Visits}× · {p.Threat}" : string.Empty) + (p.State.Count > 0 ? " · " + string.Join(", ", p.State) : string.Empty)); // Phase 14
                row.name = "xk-journal-place-" + p.PlaceId;
                box.Add(row);
                if (p.Discovery >= Realmkeeper.Domain.Places.PlaceDiscovery.Discovered) box.Add(Text(p.Description, "xk-w-desc"));
            }

            _content.Add(box);
        }

        private void ReputationPanel()
        {
            var r = ReputationSource?.Invoke();
            if (r == null) return;
            var box = Column("xk-store");
            box.name = "xk-journal-reputation";
            box.Add(Pair("Notoriety", r.Notoriety + " / 100"));
            box.Add(Text("Factions", "xk-w-key"));
            if (r.Factions.Count == 0) box.Add(Text("No faction has an opinion of you yet.", "xk-w-empty"));
            foreach (var (name, standing, tier) in r.Factions) box.Add(Pair(name, $"{DialogueWindow.Tier(tier)} ({(standing >= 0 ? "+" : string.Empty)}{standing})"));
            box.Add(Text("Settlements", "xk-w-key"));
            if (r.Settlements.Count == 0) box.Add(Text("No settlement knows your name yet.", "xk-w-empty"));
            foreach (var (name, rep, tier) in r.Settlements) box.Add(Pair(name, $"{DialogueWindow.Tier(tier)} ({(rep >= 0 ? "+" : string.Empty)}{rep})"));
            box.Add(Text("People", "xk-w-key"));
            foreach (var p in r.People)
            {
                var row = Pair(p.Name, $"{DialogueWindow.Tier(p.Tier)} · familiarity {p.Familiarity}, liking {p.Affinity}, trust {p.Trust}, respect {p.Respect}, fear {p.Fear}, gratitude {p.Gratitude}, hostility {p.Hostility}");
                row.name = "xk-journal-person-" + p.NpcId;
                box.Add(row);
            }

            _content.Add(box);
            if (r.Recent.Count > 0)
            {
                _content.Add(Text("Recent changes", "xk-w-heading"));
                foreach (var line in r.Recent) _content.Add(Text(line, "xk-w-ledger-line"));
            }
        }
    }
}
