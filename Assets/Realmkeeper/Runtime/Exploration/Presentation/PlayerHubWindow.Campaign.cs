using System;
using Realmkeeper.Application.Campaigns;
using Realmkeeper.Domain.Campaigns;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 17: the Campaign tab of the journal (the existing journal, not a second one). With no campaign it says the
    /// world is open-ended and lists what could be begun here, with the reason when something cannot; with one it shows
    /// the premise, the chapters and arcs begun so far with their objectives (hidden ones stay hidden), the decisions
    /// made and what has come of them, decisions waiting, the people in the story as far as the player knows them, and
    /// its history. Starting, setting aside, taking up, giving up and refusing go through commands.
    /// </summary>
    public sealed partial class PlayerHubWindow
    {
        public Func<CampaignsView> CampaignSource { get; set; }

        private string _campaignView = "overview";

        private void CampaignPanel()
        {
            var all = CampaignSource?.Invoke();
            if (all == null)
            {
                _content.Add(Text("The campaign journal is not available.", "xk-w-empty"));
                return;
            }

            var box = Column("xk-store");
            box.name = "xk-journal-campaign";
            if (all.Campaigns.Count == 0)
                box.Add(Text("No campaign is under way. This world is open-ended: its people, factions and roads go on whether or not you follow a story.", "xk-w-desc"));

            foreach (var c in all.Campaigns) Campaign(box, c);

            if (all.Offers.Count > 0)
            {
                box.Add(Text("Stories that could begin here", "xk-w-key"));
                foreach (var o in all.Offers)
                {
                    var row = Row("xk-w-row");
                    row.name = "xk-campaign-offer-" + o.CampaignId;
                    row.Add(Text(o.Title + (o.Provisional ? "  (provisional demonstration)" : string.Empty), "xk-w-name"));
                    if (o.CanStart)
                    {
                        var id = o.CampaignId;
                        var start = Act("Begin", () => Run(s => s.Dispatch(new StartCampaignCommand(id))));
                        start.name = "xk-campaign-start-" + o.CampaignId;
                        row.Add(start);
                    }

                    box.Add(row);
                    box.Add(Text(o.Summary, "xk-w-desc"));
                    if (!o.CanStart) box.Add(Text("Not here: " + o.Why, "xk-w-note"));
                }
            }

            _content.Add(box);
        }

        private void Campaign(VisualElement box, CampaignView c)
        {
            var head = Row("xk-w-head");
            var title = Text(c.Title, "xk-w-heading");
            title.style.flexGrow = 1;
            head.Add(title);
            var id = c.CampaignId;
            if (c.State == CampaignState.Active)
            {
                var pause = Act("Set aside", () => Run(s => s.Dispatch(new SetCampaignStateCommand(id, CampaignAction.Suspend))));
                pause.name = "xk-campaign-suspend";
                head.Add(pause);
            }

            if (c.State == CampaignState.Suspended)
            {
                var resume = Act("Take up again", () => Run(s => s.Dispatch(new SetCampaignStateCommand(id, CampaignAction.Resume))));
                resume.name = "xk-campaign-resume";
                head.Add(resume);
            }

            if (c.State == CampaignState.Active || c.State == CampaignState.Suspended)
            {
                var quit = Act("Give up", () => Run(s => s.Dispatch(new SetCampaignStateCommand(id, CampaignAction.Abandon))));
                quit.name = "xk-campaign-abandon";
                head.Add(quit);
            }

            box.Add(head);
            var tabs = Row("xk-inv-groups");
            foreach (var t in new[] { "overview", "story", "decisions", "people", "history" })
            {
                var tab = t;
                var b = Act(char.ToUpperInvariant(t[0]) + t.Substring(1), () => { _campaignView = tab; Render(); });
                b.name = "xk-campaign-view-" + t;
                b.EnableInClassList("xk-w-choice--on", _campaignView == t);
                tabs.Add(b);
            }

            box.Add(tabs);
            var state = c.State == CampaignState.Suspended ? "set aside" : c.State.ToString().ToLowerInvariant();
            box.Add(Text($"{state} · begun {c.Started}" + (c.CurrentChapter != null ? " · chapter: " + c.CurrentChapter : string.Empty) + (c.Provisional ? " · provisional demonstration content, not setting canon" : string.Empty), "xk-w-subtitle"));
            if (c.DefinitionMissing)
            {
                box.Add(Text("This build does not have this campaign's content. Its record is kept as it was, and nothing in it advances.", "xk-inv-warning"));
                return;
            }

            switch (_campaignView)
            {
                case "story": Story(box, c); break;
                case "decisions": Decisions(box, c); break;
                case "people": People(box, c); break;
                case "history":
                    foreach (var line in c.History) box.Add(Text(line, "xk-w-ledger-line"));
                    break;
                default: Overview(box, c); break;
            }
        }

        private void Overview(VisualElement box, CampaignView c)
        {
            var premise = Text(c.Premise, "xk-w-desc");
            premise.name = "xk-campaign-premise";
            box.Add(premise);
            if (c.Ending != null)
            {
                box.Add(Text("Ending: " + c.Ending, "xk-w-key"));
                box.Add(Text(c.EndingText, "xk-w-desc"));
                box.Add(Text("The world goes on. Everything that happened in the story stays happened.", "xk-w-note"));
            }

            foreach (var ch in c.Chapters)
            {
                if (ch.State != ChapterState.Active) continue;
                box.Add(Text("Now: " + ch.Title, "xk-w-key"));
                box.Add(Text(ch.Summary, "xk-w-desc"));
                foreach (var a in ch.Arcs)
                    if (a.State == ArcState.Active) box.Add(Text("· " + a.Title + (a.DaysLeft >= 0 ? $" ({a.DaysLeft} days before the world stops waiting)" : string.Empty), "xk-w-desc"));
            }

            foreach (var p in c.Pending)
                box.Add(Text(p.AskedBy != null ? $"{p.AskedBy} will want an answer: {p.Prompt}" : "To decide: " + p.Prompt, "xk-w-note"));
        }

        private void Story(VisualElement box, CampaignView c)
        {
            foreach (var ch in c.Chapters)
            {
                var chapter = Text(ch.Title + (ch.State == ChapterState.Completed ? "  (closed)" : string.Empty), "xk-w-key");
                chapter.name = "xk-campaign-chapter-" + ch.Id;
                box.Add(chapter);
                foreach (var a in ch.Arcs)
                {
                    var row = Row("xk-w-row");
                    row.name = "xk-campaign-arc-" + a.Id;
                    var mark = a.State == ArcState.Completed ? "✓" : a.State == ArcState.Failed ? "✗" : a.State == ArcState.Abandoned ? "–" : a.State == ArcState.Locked ? "·" : "○";
                    row.Add(Text($"{mark} {a.Title}{(a.Optional ? "  (optional)" : string.Empty)}", "xk-w-name"));
                    row.Add(Text(a.State == ArcState.Locked ? "not yet" : a.State.ToString().ToLowerInvariant(), "xk-w-price"));
                    if (a.MayRefuse && (a.State == ArcState.Active || a.State == ArcState.Locked) && c.State == CampaignState.Active)
                    {
                        var arc = a.Id;
                        var refuse = Act("Refuse", () => Run(s => s.Dispatch(new RefuseArcCommand(c.CampaignId, arc))));
                        refuse.name = "xk-campaign-refuse-" + a.Id;
                        row.Add(refuse);
                    }

                    box.Add(row);
                    box.Add(Text(a.Summary, "xk-w-desc"));
                    if (a.Reason != null) box.Add(Text(a.Reason, "xk-w-note"));
                    foreach (var o in a.Objectives)
                    {
                        var m = o.Done ? "✓" : o.Failed ? "✗" : "○";
                        var line = Text($"   {m} {o.Text}{(o.Optional ? "  (optional)" : string.Empty)}{(o.Quest != null ? "  — " + o.Quest : string.Empty)}", "xk-journal-objective");
                        line.name = "xk-campaign-objective-" + a.Id + "-" + o.Id;
                        line.EnableInClassList("xk-journal-objective--done", o.Done);
                        box.Add(line);
                    }
                }
            }
        }

        private void Decisions(VisualElement box, CampaignView c)
        {
            if (c.Decisions.Count == 0 && c.Pending.Count == 0) box.Add(Text("No decisions yet.", "xk-w-empty"));
            foreach (var p in c.Pending)
            {
                box.Add(Text(p.Title, "xk-w-key"));
                box.Add(Text(p.Prompt + (p.AskedBy != null ? $" ({p.AskedBy} will put it to you.)" : string.Empty), "xk-w-desc"));
                foreach (var ch in p.Choices)
                {
                    if (p.AskedBy != null)
                    {
                        // A decision a person will put: its choices as the player knows them, not buttons (it is made face to face).
                        var line = Text("   · " + ch.Text, "xk-w-desc");
                        line.name = "xk-campaign-choice-" + p.DecisionId + "-" + ch.ChoiceId;
                        box.Add(line);
                        if (ch.Why != null) box.Add(Text("      " + ch.Why, "xk-w-note"));
                        continue;
                    }

                    var decision = p.DecisionId;
                    var choice = ch.ChoiceId;
                    var b = Act(ch.Text, () => Run(s => s.Dispatch(new MakeCampaignDecisionCommand(c.CampaignId, decision, choice))));
                    b.name = "xk-campaign-choice-" + p.DecisionId + "-" + ch.ChoiceId;
                    b.SetEnabled(ch.Enabled);
                    box.Add(b);
                    if (ch.Why != null) box.Add(Text("   " + ch.Why, "xk-w-note"));
                }
            }

            foreach (var d in c.Decisions)
            {
                var row = Text($"{d.When} — {d.Title}: {d.Summary}", "xk-w-name");
                row.name = "xk-campaign-decision-" + d.DecisionId;
                box.Add(row);
                foreach (var h in d.Happened) box.Add(Text("   What came of it: " + h, "xk-w-desc"));
                foreach (var e in d.Expected) box.Add(Text("   " + e, "xk-w-note"));
            }
        }

        private void People(VisualElement box, CampaignView c)
        {
            if (c.Characters.Count == 0) box.Add(Text("You have not met anyone in this story yet.", "xk-w-empty"));
            foreach (var p in c.Characters)
            {
                var row = Pair(p.Name, p.Part + " · " + (p.Knowledge == "met" ? (p.Status ?? "met") : "heard of, not met"));
                row.name = "xk-campaign-person-" + p.Role;
                box.Add(row);
            }
        }
    }
}
