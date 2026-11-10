using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.Presentation
{
    /// <summary>Stable display colours for factions (by their position in the world's faction list).</summary>
    public static class FactionPalette
    {
        private static readonly Color[] Colors =
        {
            new Color(0.78f, 0.25f, 0.22f), // crimson
            new Color(0.25f, 0.45f, 0.78f), // blue
            new Color(0.85f, 0.68f, 0.22f), // gold
            new Color(0.36f, 0.62f, 0.36f), // green
            new Color(0.62f, 0.38f, 0.72f), // violet
            new Color(0.22f, 0.66f, 0.68f), // teal
            new Color(0.88f, 0.50f, 0.22f), // orange
            new Color(0.80f, 0.80f, 0.84f), // silver
        };

        public static readonly Color Independent = new Color(0.55f, 0.53f, 0.50f);

        public static Color ColorFor(int index) => index < 0 ? Independent : Colors[index % Colors.Length];
    }

    /// <summary>
    /// The political part of the HUD: the Factions list, the faction inspector, the political
    /// section of the settlement inspector and the map-overlay toggle. It shows query snapshots
    /// and sends typed commands; it never touches domain objects.
    /// </summary>
    public sealed partial class RealmkeeperHud
    {
        /// <summary>Recent chronicle entries shown in the faction inspector.</summary>
        public const int FactionEventCount = 5;

        private readonly Dictionary<FactionId, Button> _factionButtons = new Dictionary<FactionId, Button>();
        private FactionId _selectedFaction;
        private bool _managePolitics;

        /// <summary>True while the settlement inspector shows its political action buttons.</summary>
        public bool ManagingPolitics => _managePolitics;

        /// <summary>The faction whose inspector is open (empty: none).</summary>
        public FactionId SelectedFactionId => _selectedFaction;

        public VisualElement FactionInspector { get; private set; }
        public Label FactionInspectorName { get; private set; }
        public VisualElement FactionInspectorBody => Root?.Q<ScrollView>("finsp-body");
        public VisualElement FactionList => Root?.Q<ScrollView>("faction-list");

        /// <summary>The "Politics" lines of the settlement inspector.</summary>
        public VisualElement SettlementPolitics => Root?.Q<VisualElement>("insp-politics");

        public Button PoliticsToggle { get; private set; }

        public bool FactionInspectorVisible => FactionInspector != null && !FactionInspector.ClassListContains("rk-hidden");

        /// <summary>Whether the political map overlay is switched on.</summary>
        public bool PoliticalOverlayEnabled { get; private set; }

        /// <summary>Raised when the overlay toggle changes.</summary>
        public event Action<bool> PoliticalOverlayChanged;

        private void BindPolitics()
        {
            FactionInspector = Root.Q<VisualElement>("faction-inspector");
            FactionInspectorName = Root.Q<Label>("finsp-name");
            PoliticsToggle = Root.Q<Button>("toggle-politics");
            var list = Root.Q<ScrollView>("faction-list");
            Root.Q<Button>("nav-factions").clicked += () => list.ToggleInClassList("rk-hidden");
            Root.Q<Button>("finsp-close").clicked += () => SelectFaction(default);
            PoliticsToggle.clicked += () => SetPoliticalOverlay(!PoliticalOverlayEnabled);
            var manage = Root.Q<Button>("insp-politics-manage");
            manage.clicked += () => SetManagingPolitics(!_managePolitics);
        }

        /// <summary>Shows or hides the political action buttons in the settlement inspector.</summary>
        public void SetManagingPolitics(bool manage)
        {
            _managePolitics = manage;
            var button = Root?.Q<Button>("insp-politics-manage");
            if (button != null) button.text = manage ? "Done" : "Manage";
            RefreshInspector();
        }

        public void SetPoliticalOverlay(bool enabled)
        {
            if (PoliticalOverlayEnabled == enabled) return;
            PoliticalOverlayEnabled = enabled;
            PoliticsToggle?.EnableInClassList("rk-button--primary", enabled);
            PoliticalOverlayChanged?.Invoke(enabled);
        }

        /// <summary>Opens a faction's inspector (and closes the settlement inspector); empty closes it.</summary>
        public void SelectFaction(FactionId id)
        {
            if (!id.IsEmpty && (_session == null || _session.FindFaction(id) == null)) id = default;
            _selectedFaction = id;
            if (!id.IsEmpty) _selection.Clear();
            if (!id.IsEmpty) CloseNpcInspectorForOtherSelection();
            foreach (var pair in _factionButtons) pair.Value.EnableInClassList("rk-settlement-item--selected", pair.Key == id);
            RefreshFactionInspector();
        }

        private void OnSettlementSelectedForPolitics(SettlementId id)
        {
            if (id.IsEmpty || _selectedFaction.IsEmpty) return;
            _selectedFaction = default; // one inspector at a time
            foreach (var pair in _factionButtons) pair.Value.RemoveFromClassList("rk-settlement-item--selected");
            RefreshFactionInspector();
        }

        private void RefreshPolitics()
        {
            if (Root == null || FactionInspector == null) return;
            RebuildFactionList();
            RefreshFactionInspector();
        }

        private IReadOnlyList<FactionSummary> Factions() =>
            _session != null && _session.HasWorld ? _session.GetFactions() : (IReadOnlyList<FactionSummary>)Array.Empty<FactionSummary>();

        private int FactionIndex(FactionId id)
        {
            var factions = Factions();
            for (var i = 0; i < factions.Count; i++)
            {
                if (factions[i].FactionId == id) return i;
            }

            return -1;
        }

        private void RebuildFactionList()
        {
            var list = Root.Q<ScrollView>("faction-list");
            list.Clear();
            _factionButtons.Clear();
            var factions = Factions();
            for (var i = 0; i < factions.Count; i++)
            {
                var f = factions[i];
                var id = f.FactionId;
                var item = new Button(() => SelectFaction(id)) { name = "faction-" + id };
                item.AddToClassList("rk-settlement-item");
                item.AddToClassList("rk-faction-item");
                item.Add(Swatch(i));
                var text = new VisualElement();
                text.AddToClassList("rk-faction-text");
                var name = new Label(f.Name);
                name.AddToClassList("rk-faction-name");
                text.Add(name);
                var detail = new Label(f.IsDissolved ? "Dissolved" : $"{PoliticalText.TypeName(f.Type)} · influence {f.Influence}");
                detail.AddToClassList("rk-faction-detail");
                text.Add(detail);
                item.Add(text);
                item.EnableInClassList("rk-faction-item--dissolved", f.IsDissolved);
                item.EnableInClassList("rk-settlement-item--selected", id == _selectedFaction);
                item.tooltip = $"{f.Status}. Leader: {f.LeaderName ?? "vacant"}. Goal: {f.PrimaryGoal?.Description ?? "none"}. Holds: {(f.HoldingNames.Count > 0 ? string.Join(", ", f.HoldingNames) : "nothing")}.";
                list.Add(item);
                _factionButtons[id] = item;
            }
        }

        private static VisualElement Swatch(int index)
        {
            var swatch = new VisualElement();
            swatch.AddToClassList("rk-swatch");
            swatch.style.backgroundColor = FactionPalette.ColorFor(index);
            return swatch;
        }

        // ----- Faction inspector ----------------------------------------------------------------

        private void RefreshFactionInspector()
        {
            if (Root == null || FactionInspector == null) return;
            var f = !_selectedFaction.IsEmpty && _session != null && _session.HasWorld ? _session.FindFaction(_selectedFaction) : null;
            FactionInspector.EnableInClassList("rk-hidden", f == null);
            if (f == null) return;

            FactionInspectorName.text = f.Name;
            Root.Q<Label>("finsp-type").text = $"{PoliticalText.TypeName(f.Type)} — {f.Status}";
            var body = Root.Q<ScrollView>("finsp-body");
            body.Clear();

            if (!string.IsNullOrEmpty(f.Description)) body.Add(Cell(f.Description, "rk-trade-line"));
            body.Add(Row("Founded", f.FoundedOn.ToString()));
            if (f.DissolvedOn.HasValue) body.Add(Row("Dissolved", f.DissolvedOn.Value.ToString()));
            body.Add(Row("Leader", f.LeaderName ?? "Vacant"));
            body.Add(Row("Following", $"{N(f.TotalMembers)} people"));
            body.Add(Row("Treasury", $"{N(f.Treasury)} coin"));

            body.Add(Section("Influence"));
            var trend = f.InfluenceTrend == RatingTrend.Rising ? $"rising toward {f.InfluenceTarget}"
                : f.InfluenceTrend == RatingTrend.Falling ? $"falling toward {f.InfluenceTarget}" : "steady";
            var influence = Row($"{f.Influence} / 100", trend);
            influence.name = "finsp-influence";
            body.Add(influence);
            var why = Cell(InfluenceWhy(f.InfluenceContributions), "rk-why");
            why.name = "finsp-influence-why";
            body.Add(why);

            body.Add(Section("Goals"));
            if (f.Goals.Count == 0) body.Add(Cell("No goals.", "rk-trade-line"));
            foreach (var g in f.Goals)
            {
                var state = g.Status == GoalStatus.Active ? $"{g.Progress}%" : g.Status == GoalStatus.Completed ? "done" : $"abandoned ({ReasonText(g.CloseReason)})";
                var line = Cell($"{g.Description} — {state}{(g.IsManual ? " · set by you" : string.Empty)}", "rk-trade-line");
                line.EnableInClassList("rk-trade-line--suspended", g.Status != GoalStatus.Active);
                var row = RouteRow(line);
                if (g.Status == GoalStatus.Active)
                {
                    var goalId = g.GoalId;
                    var factionId = f.FactionId;
                    row.Add(SmallButton("Abandon", "goal-abandon-" + goalId, () => Do(new AbandonFactionGoalCommand(factionId, goalId))));
                }

                body.Add(row);
            }

            body.Add(Section("Presence and holdings"));
            if (f.Presence.Count == 0) body.Add(Cell("Present nowhere.", "rk-trade-line"));
            foreach (var p in f.Presence)
            {
                var role = p.IsGovernor ? " · governs" : p.IsAdministrator ? " · administers" : string.Empty;
                body.Add(Cell($"{p.SettlementName}: strength {p.Strength}, {N(p.Members)} members ({p.SharePerMille / 10}%){role}", "rk-trade-line"));
            }

            foreach (var c in f.Claims)
            {
                if (c.IsOpen) body.Add(Cell($"Claims {c.SettlementName}: {c.Status.ToString().ToLowerInvariant()}", "rk-trade-line"));
            }

            body.Add(Section("Relationships"));
            if (f.Relations.Count == 0) body.Add(Cell("No relationships.", "rk-trade-line"));
            foreach (var r in f.Relations)
            {
                if (r.InvolvesDissolved) continue;
                var other = r.Other(f.FactionId);
                var agreement = r.Agreement == AgreementType.None ? string.Empty : " · " + PoliticalText.Agreement(r.Agreement);
                var heading = r.OpinionTarget > r.Opinion ? "warming" : r.OpinionTarget < r.Opinion ? "cooling" : "steady";
                var line = Cell($"{r.OtherName(f.FactionId)}: {r.State} ({PoliticalText.Signed(r.Opinion)}, {heading}; rivalry {r.Rivalry}){agreement}", "rk-trade-line");
                line.tooltip = $"Trust {r.Trust}. Mostly driven by {PoliticalText.Cause(r.LastCause)}.";
                line.EnableInClassList("rk-trade-line--blocked", r.State == RelationState.Hostile || r.State == RelationState.Rival);
                var row = RouteRow(line);
                var self = f.FactionId;
                var opinion = r.Opinion;
                row.Add(SmallButton("−10", "relation-worsen-" + other, () => Do(new SetFactionRelationCommand(self, other, Mathf.Max(-100, opinion - 10)))));
                row.Add(SmallButton("+10", "relation-improve-" + other, () => Do(new SetFactionRelationCommand(self, other, Mathf.Min(100, opinion + 10)))));
                body.Add(row);
            }

            body.Add(Section("Recent history"));
            var events = _session.GetFactionEvents(f.FactionId, FactionEventCount);
            if (events.Count == 0) body.Add(Cell("Nothing recorded yet.", "rk-trade-line"));
            foreach (var e in events) body.Add(Cell($"{e.Date}: {e.Summary}", "rk-trade-line"));
        }

        /// <summary>The terms behind an influence target, largest first: "Why: Holdings +30 · Authority +12 · …".</summary>
        public static string InfluenceWhy(IReadOnlyList<InfluenceContribution> parts)
        {
            var shown = new List<InfluenceContribution>();
            foreach (var p in parts)
            {
                if (p.Points != 0) shown.Add(p);
            }

            shown.Sort((a, b) => b.Hundredths.CompareTo(a.Hundredths));
            if (shown.Count == 0) return "Why: nothing supports it.";
            var text = new List<string>();
            foreach (var p in shown) text.Add($"{p.Factor} +{p.Points}");
            return "Why: " + string.Join(" · ", text);
        }

        private static string ReasonText(GoalCloseReason reason)
        {
            switch (reason)
            {
                case GoalCloseReason.AbandonedByPlayer: return "by you";
                case GoalCloseReason.TargetLost: return "target lost";
                case GoalCloseReason.FactionDissolved: return "faction dissolved";
                default: return reason.ToString().ToLowerInvariant();
            }
        }

        // ----- Settlement inspector: politics ----------------------------------------------------

        private void RefreshSettlementPolitics(SettlementSummary s)
        {
            var box = Root.Q<VisualElement>("insp-politics");
            if (box == null) return;
            box.Clear();
            var politics = _session.GetSettlementPolitics(s.SettlementId);
            if (politics == null) return;
            var settlementId = s.SettlementId;
            var manage = _managePolitics;
            var factions = Factions();

            // Who holds it: one row, like the economy rows above.
            box.Add(Row("Governed by", politics.IsIndependent ? "Independent" : politics.GoverningFactionName));
            if (!politics.AdministratorFactionId.IsEmpty) box.Add(Row("Administered by", politics.AdministratorFactionName));
            var share = politics.Population > 0 ? politics.AffiliatedPopulation * 100 / politics.Population : 0;
            box.Add(Row("Affiliated", $"{N(politics.AffiliatedPopulation)} ({share}%)"));
            if (manage && !politics.IsIndependent)
                box.Add(Actions(SmallButton("Set independent", "gov-independent", () => Do(new SetSettlementGovernanceCommand(settlementId, default)))));

            // Factions present: name (with its colour) on the left, standing on the right.
            var present = new HashSet<FactionId>();
            var claimants = new HashSet<FactionId>();
            foreach (var c in politics.OpenClaims) claimants.Add(c.ClaimantId);
            foreach (var p in politics.Presence)
            {
                present.Add(p.FactionId);
                var role = p.IsGovernor ? "governs · " : p.IsAdministrator ? "administers · " : string.Empty;
                var row = FactionRow(FactionIndexIn(factions, p.FactionId), p.FactionName, $"{role}{p.SharePerMille / 10}%");
                row.name = "faction-link-" + p.FactionId;
                row.tooltip = $"{p.FactionName}: strength {p.Strength} ({p.Status.ToString().ToLowerInvariant()}), {N(p.Members)} people.";
                var factionId = p.FactionId;
                row.RegisterCallback<ClickEvent>(_ => SelectFaction(factionId));
                row.EnableInClassList("rk-faction-row--waning", p.Status == PresenceStatus.Waning);
                box.Add(row);

                if (!manage) continue;
                var actions = new List<Button>();
                if (!p.IsGovernor && FactionRules.CanGovern(p.FactionType))
                    actions.Add(SmallButton("Make governor", "gov-make-" + factionId, () => Do(new SetSettlementGovernanceCommand(settlementId, factionId))));
                if (!p.IsGovernor && FactionRules.CanClaimTerritory(p.FactionType) && !claimants.Contains(factionId))
                    actions.Add(SmallButton("Claim", "claim-file-" + factionId, () => Do(new FileTerritorialClaimCommand(factionId, settlementId))));
                if (!p.IsGovernor && !p.IsAdministrator)
                    actions.Add(SmallButton("Remove", "presence-remove-" + factionId, () => Do(new RemoveFactionPresenceCommand(factionId, settlementId))));
                if (actions.Count > 0) box.Add(Actions(actions.ToArray()));
            }

            foreach (var c in politics.OpenClaims)
            {
                var row = Row("Claimed by " + c.ClaimantName, c.Status.ToString() + (c.IsManual ? " (you)" : string.Empty));
                row.EnableInClassList("rk-row--contested", c.Status == ClaimStatus.Contested);
                box.Add(row);
                if (!manage) continue;
                var claimId = c.ClaimId;
                var actions = new List<Button>();
                if (c.Status != ClaimStatus.Recognized) actions.Add(SmallButton("Recognize", "claim-recognize-" + claimId, () => Do(new ResolveTerritorialClaimCommand(claimId, ClaimStatus.Recognized))));
                if (c.Status != ClaimStatus.Contested) actions.Add(SmallButton("Contest", "claim-contest-" + claimId, () => Do(new ResolveTerritorialClaimCommand(claimId, ClaimStatus.Contested))));
                actions.Add(SmallButton("Withdraw", "claim-withdraw-" + claimId, () => Do(new ResolveTerritorialClaimCommand(claimId, ClaimStatus.Relinquished))));
                box.Add(Actions(actions.ToArray()));
            }

            if (!manage) return;

            // Factions with no presence here can be invited in.
            var invite = new List<Button>();
            foreach (var f in factions)
            {
                if (f.IsDissolved || present.Contains(f.FactionId)) continue;
                var factionId = f.FactionId;
                invite.Add(SmallButton("Invite " + f.Name, "presence-add-" + factionId, () => Do(new EstablishFactionPresenceCommand(factionId, settlementId))));
            }

            if (invite.Count > 0) box.Add(Actions(invite.ToArray()));
        }

        private static int FactionIndexIn(IReadOnlyList<FactionSummary> factions, FactionId id)
        {
            for (var i = 0; i < factions.Count; i++)
            {
                if (factions[i].FactionId == id) return i;
            }

            return -1;
        }

        /// <summary>A key/value row whose key starts with the faction's colour swatch.</summary>
        private static VisualElement FactionRow(int colorIndex, string name, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("rk-row");
            row.AddToClassList("rk-faction-row");
            var left = new VisualElement();
            left.AddToClassList("rk-faction-key");
            left.Add(Swatch(colorIndex));
            left.Add(Cell(name, "rk-key", "rk-faction-row-name"));
            row.Add(left);
            row.Add(Cell(value, "rk-value"));
            return row;
        }

        /// <summary>A wrapping row of small action buttons under the line they act on.</summary>
        private static VisualElement Actions(params Button[] buttons)
        {
            var row = new VisualElement();
            row.AddToClassList("rk-actions");
            foreach (var b in buttons) row.Add(b);
            return row;
        }

        // ----- Small building blocks --------------------------------------------------------------

        private void Do<TCommand>(TCommand command) where TCommand : ICommand
        {
            if (_session == null)
            {
                ShowMessage(NoSession());
                return;
            }

            ShowMessage(_session.Dispatch(command));
        }

        private static VisualElement Row(string key, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("rk-row");
            row.Add(Cell(key, "rk-key"));
            row.Add(Cell(value, "rk-value"));
            return row;
        }

        private static Label Section(string title) => Cell(title, "rk-section");

        private static VisualElement RouteRow(VisualElement line)
        {
            var row = new VisualElement();
            row.AddToClassList("rk-route-row");
            row.Add(line);
            return row;
        }

        private static Button SmallButton(string text, string name, Action onClick)
        {
            var button = new Button(onClick) { text = text, name = name };
            button.AddToClassList("rk-button");
            button.AddToClassList("rk-route-toggle");
            return button;
        }
    }
}
