using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The encounter panel (Phase 8), docked along the bottom of the screen so the fight stays visible: the round and
    /// whose turn it is, the initiative order (hit points, armour class, state, conditions), a small tactical grid
    /// (5-ft squares; click a free square to move there), the actions available now — each unavailable one with the
    /// reason — the last result explained roll by roll, an inspector for any combatant, and the combat log. It only
    /// shows the engine's snapshot and raises requests; the controller dispatches commands. Nothing here changes the world.
    /// </summary>
    public sealed partial class CombatWindow
    {
        public const int GridColumns = 15;
        public const int GridRows = 11;

        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly Label _turn;
        private readonly VisualElement _order;
        private readonly VisualElement _grid;
        private readonly ScrollView _actions;
        private readonly Label _result;
        private readonly ScrollView _inspector;
        private readonly ScrollView _log;
        private readonly Button _withdraw;
        private readonly Button _callOff;
        private readonly Button _close;
        private readonly Button _continue;

        public CombatWindow(VisualElement hudRoot)
        {
            Root = new VisualElement { name = "xk-combat" };
            Root.AddToClassList("xk-window");
            Root.AddToClassList("xk-combat");
            Root.AddToClassList("xk-hidden");

            var head = TradeWindow.Row("xk-w-head");
            var titles = new VisualElement();
            titles.style.flexGrow = 1;
            titles.style.flexShrink = 1; // Phase 17: a long encounter name wraps instead of running under the turn label
            titles.style.minWidth = 0;
            _title = TradeWindow.Text("-", "xk-w-title");
            _title.name = "xk-combat-title";
            _title.style.whiteSpace = WhiteSpace.Normal;
            _subtitle = TradeWindow.Text("-", "xk-w-subtitle");
            _subtitle.name = "xk-combat-subtitle";
            titles.Add(_title);
            titles.Add(_subtitle);
            head.Add(titles);
            _turn = TradeWindow.Text("-", "xk-combat-turn");
            _turn.name = "xk-combat-turn";
            _turn.style.flexShrink = 0;
            head.Add(_turn);
            _outcome = TradeWindow.Text(string.Empty, "rk-badge"); // Phase 26
            _outcome.name = "xk-combat-outcome";
            _outcome.AddToClassList("xk-combat-outcome");
            _outcome.AddToClassList("xk-hidden");
            head.Add(_outcome);
            _continue = HeadButton("Continue", "xk-combat-continue", () => ContinueRequested?.Invoke());
            _continue.tooltip = "Let the next computer-controlled combatant act now.";
            head.Add(_continue);
            _withdraw = HeadButton("Withdraw", "xk-combat-withdraw", () => WithdrawRequested?.Invoke());
            _withdraw.tooltip = "Leave the fight with your side. Not while an enemy stands next to you.";
            head.Add(_withdraw);
            _callOff = HeadButton("Call off", "xk-combat-calloff", () => CallOffRequested?.Invoke());
            _callOff.tooltip = "End this practice fight where it stands (hit points stay as they are).";
            head.Add(_callOff);
            _close = HeadButton("Close", "xk-combat-close", () => CloseRequested?.Invoke());
            head.Add(_close);
            Root.Add(head);

            // Combat Overhaul: the window is a set of compact pieces over a see-through layer, not one opaque panel. The
            // top strip (round, turn, the initiative order); the bar at the bottom centre (action, bonus action, reaction,
            // movement, the groups of choices, Dash, End turn) with a pop-up above it for the open group; the selected
            // character's card at the lower left (details on demand); the log at the lower right, collapsed by default;
            // and the tactical grid as an optional panel (Tab). The battlefield between them stays clear and clickable.
            Root.AddToClassList("xk-cbt");
            Root.pickingMode = PickingMode.Ignore;
            head.name = "xk-cbt-head";
            head.AddToClassList("xk-cbt-head");

            var top = new VisualElement { name = "xk-cbt-top" };
            top.AddToClassList("xk-cbt-top");
            top.Add(head);
            _order = new ScrollView(ScrollViewMode.Horizontal) { name = "xk-combat-order" };
            _order.AddToClassList("xk-cbt-order");
            top.Add(_order);
            Root.Add(top);

            var gridCol = TradeWindow.Column("xk-cbt-grid-panel");
            gridCol.name = "xk-cbt-grid-panel";
            gridCol.AddToClassList("xk-hidden");
            gridCol.Add(TradeWindow.Text("Tactical grid (5-ft squares; Tab hides it)", "xk-w-heading"));
            _grid = new VisualElement { name = "xk-combat-grid" };
            _grid.AddToClassList("xk-combat-grid");
            gridCol.Add(_grid);
            _gridPanel = gridCol;
            Root.Add(gridCol);

            var card = TradeWindow.Column("xk-cbt-card");
            card.name = "xk-cbt-card";
            _cardName = TradeWindow.Text("-", "xk-cbt-card-name");
            _cardName.name = "xk-cbt-card-name";
            card.Add(_cardName);
            var hpBack = new VisualElement { name = "xk-cbt-hp" };
            hpBack.AddToClassList("xk-cbt-hp");
            _hpFill = new VisualElement();
            _hpFill.AddToClassList("xk-cbt-hp-fill");
            hpBack.Add(_hpFill);
            card.Add(hpBack);
            _cardLine = TradeWindow.Text("-", "xk-cbt-card-line");
            _cardLine.name = "xk-cbt-card-line";
            card.Add(_cardLine);
            _cardConditions = TradeWindow.Row("rk-vitals-conditions"); // Phase 26: conditions as word badges
            _cardConditions.name = "xk-cbt-card-conditions";
            card.Add(_cardConditions);
            _result = TradeWindow.Text("Nothing yet.", "xk-combat-result");
            _result.name = "xk-combat-result";
            card.Add(_result);
            var details = new Button(() => { _detailsOpen = !_detailsOpen; _inspector.EnableInClassList("xk-hidden", !_detailsOpen); }) { text = "Details", name = "xk-cbt-details" };
            details.AddToClassList("xk-button");
            details.AddToClassList("xk-cbt-small");
            card.Add(details);
            _inspector = new ScrollView { name = "xk-combat-inspector" };
            _inspector.AddToClassList("xk-combat-inspector");
            _inspector.AddToClassList("xk-hidden");
            card.Add(_inspector);
            Root.Add(card);

            var actCol = TradeWindow.Column("xk-cbt-pop");
            actCol.name = "xk-cbt-pop";
            actCol.AddToClassList("xk-hidden");
            _actions = new ScrollView { name = "xk-combat-actions" };
            _actions.AddToClassList("xk-w-list");
            actCol.Add(_actions);
            _pop = actCol;
            Root.Add(actCol);

            _bar = new VisualElement { name = "xk-cbt-bar" };
            _bar.AddToClassList("xk-cbt-bar");
            Root.Add(_bar);

            var logCol = TradeWindow.Column("xk-cbt-log");
            logCol.name = "xk-cbt-log";
            var logHead = TradeWindow.Row("xk-cbt-log-head");
            logHead.Add(TradeWindow.Text("Combat log", "xk-w-heading"));
            _logToggle = new Button(() => { _logOpen = !_logOpen; logCol.EnableInClassList("xk-cbt-log--open", _logOpen); _logToggle.text = _logOpen ? "Less" : "More"; }) { text = "More", name = "xk-cbt-log-toggle" };
            _logToggle.AddToClassList("xk-button");
            _logToggle.AddToClassList("xk-cbt-small");
            logHead.Add(_logToggle);
            logCol.Add(logHead);
            _log = new ScrollView { name = "xk-combat-log" };
            _log.AddToClassList("xk-combat-log");
            logCol.Add(_log);
            Root.Add(logCol);

            // Movement: the label beside the cursor, and the question before Dash is spent.
            _hover = TradeWindow.Text(string.Empty, "xk-cbt-hover");
            _hover.name = "xk-cbt-hover";
            _hover.pickingMode = PickingMode.Ignore;
            _hover.AddToClassList("xk-hidden");
            Root.Add(_hover);
            _confirm = TradeWindow.Column("xk-cbt-confirm");
            _confirm.name = "xk-cbt-confirm";
            _confirm.AddToClassList("xk-hidden");
            _confirmText = TradeWindow.Text("-", "xk-cbt-confirm-text");
            _confirm.Add(_confirmText);
            var buttons = TradeWindow.Row("xk-cbt-confirm-buttons");
            _confirmYes = new Button(() => { var a = _onConfirm; HideConfirm(); a?.Invoke(); }) { text = "Dash and move", name = "xk-cbt-confirm-yes" };
            _confirmYes.AddToClassList("xk-button");
            _confirmYes.AddToClassList("xk-button--primary");
            var no = new Button(HideConfirm) { text = "Cancel (Esc)", name = "xk-cbt-confirm-no" };
            no.AddToClassList("xk-button");
            buttons.Add(_confirmYes);
            buttons.Add(no);
            _confirm.Add(buttons);
            Root.Add(_confirm);
            hudRoot.Add(Root);
        }

        // ----- Combat Overhaul: the compact HUD's own state -------------------------------------------------

        private readonly VisualElement _gridPanel;
        private readonly VisualElement _pop;
        private readonly VisualElement _bar;
        private readonly Label _cardName;
        private readonly Label _cardLine;
        private readonly VisualElement _cardConditions; // Phase 26
        private readonly Label _outcome; // Phase 26

        /// <summary>Phase 26: the end of a fight in a word, from the player's side.</summary>
        public static string OutcomeWord(EncounterSnapshot s)
        {
            if (s == null) return null;
            switch (s.Outcome)
            {
                case EncounterOutcome.Victory:
                    var mine = s.Player != null && s.WinningSide == s.Player.Side;
                    return Realmkeeper.UI.RkText.Get(mine ? "combat.won" : "combat.lost");
                case EncounterOutcome.Withdrawn: return Realmkeeper.UI.RkText.Get("combat.withdrawn");
                case EncounterOutcome.Stalemate: return Realmkeeper.UI.RkText.Get("combat.stalemate");
                default: return Realmkeeper.UI.RkText.Get("combat.calledoff");
            }
        }

        /// <summary>Phase 26: the outcome banner's text (tests).</summary>
        public string OutcomeText => _outcome.text;
        private readonly VisualElement _hpFill;
        private readonly Button _logToggle;
        private readonly Label _hover;
        private readonly VisualElement _confirm;
        private readonly Label _confirmText;
        private readonly Button _confirmYes;
        private Action _onConfirm;
        private bool _detailsOpen;
        private bool _logOpen;

        /// <summary>The open group of choices in the pop-up above the bar (null: closed).</summary>
        public string OpenGroup { get; private set; }

        public bool GridVisible => !_gridPanel.ClassListContains("xk-hidden");
        public bool ConfirmOpen => !_confirm.ClassListContains("xk-hidden");
        public string ConfirmText => _confirmText.text;
        public string HoverText => _hover.ClassListContains("xk-hidden") ? string.Empty : _hover.text;
        public bool LogOpen => _logOpen;

        /// <summary>Shows or hides the tactical grid (Tab). The world is where moving happens; the grid is for reading.</summary>
        public void ToggleGrid() => _gridPanel.EnableInClassList("xk-hidden", GridVisible);

        /// <summary>Opens a group of choices above the bar, or closes it when it is already open.</summary>
        public void OpenGroupPopup(string group)
        {
            OpenGroup = OpenGroup == group ? null : group;
            if (Snapshot != null) { FillActions(Snapshot); FillBar(Snapshot); }
        }

        public void CloseGroupPopup()
        {
            if (OpenGroup == null) return;
            OpenGroup = null;
            if (Snapshot != null) { FillActions(Snapshot); FillBar(Snapshot); }
        }

        /// <summary>The label beside the cursor while a destination is hovered (panel coordinates); null hides it.</summary>
        public void ShowHover(string text, UnityEngine.Vector2 at, bool ok)
        {
            if (string.IsNullOrEmpty(text)) { _hover.AddToClassList("xk-hidden"); return; }
            _hover.text = text;
            _hover.RemoveFromClassList("xk-hidden");
            _hover.EnableInClassList("xk-cbt-hover--bad", !ok);
            var panel = Root.worldBound;
            _hover.style.left = UnityEngine.Mathf.Clamp(at.x + 18f, 0f, UnityEngine.Mathf.Max(0f, panel.width - 300f));
            _hover.style.top = UnityEngine.Mathf.Clamp(at.y + 18f, 0f, UnityEngine.Mathf.Max(0f, panel.height - 120f));
        }

        /// <summary>Asks before something costly (Dash): nothing is spent until the player says yes.</summary>
        public void AskConfirm(string text, string yes, Action onYes)
        {
            _confirmText.text = text;
            _confirmYes.text = yes;
            _onConfirm = onYes;
            _confirm.RemoveFromClassList("xk-hidden");
        }

        public void HideConfirm()
        {
            _onConfirm = null;
            _confirm.AddToClassList("xk-hidden");
        }

        /// <summary>Presses the confirmation's yes (tests and the Enter key).</summary>
        public void Confirm() => _confirmYes.SendEvent(new NavigationSubmitEvent { target = _confirmYes });

        private static string GroupOf(CombatOption o) =>
            o.Action is EndTurnAction ? "end" : o.Action is AttackAction ? "attacks" : o.Action is DashAction ? "move" : "actions";

        /// <summary>The bar: the turn's economy as slots, the groups (with how many choices each holds), Dash and End turn.</summary>
        private void FillBar(EncounterSnapshot s)
        {
            _bar.Clear();
            var active = s.Status == EncounterStatus.Active;
            _bar.EnableInClassList("xk-hidden", !active);
            if (!active) return;
            var p = s.Player;
            var mine = s.IsPlayersTurn && s.Prompt == null && p != null;
            var slots = TradeWindow.Row("xk-cbt-slots");
            Slot(slots, "Action", p != null && (!p.ActionUsed || p.AttacksLeft > 0), p != null && p.ActionUsed && p.AttacksLeft > 0 ? $"{p.AttacksLeft} attack(s) left" : null, "xk-cbt-slot--action");
            Slot(slots, "Bonus", p != null && !p.BonusActionUsed, null, "xk-cbt-slot--bonus");
            Slot(slots, "Reaction", p != null && !p.ReactionUsed, null, "xk-cbt-slot--reaction");
            var move = TradeWindow.Text(p == null ? "-" : $"{p.MovementLeft} ft", "xk-cbt-move");
            move.name = "xk-cbt-move";
            move.tooltip = p == null ? string.Empty : $"Movement left this turn (speed {p.Speed} ft). Click the ground to move; Dash adds your speed.";
            slots.Add(move);
            _bar.Add(slots);

            var groups = TradeWindow.Row("xk-cbt-groups");
            var counts = new Dictionary<string, int>();
            foreach (var o in s.PlayerOptions) { var g = GroupOf(o); counts[g] = (counts.TryGetValue(g, out var n) ? n : 0) + (o.Unavailable == null ? 1 : 0); }
            GroupButton(groups, "attacks", "1 Attack", counts, mine);
            GroupButton(groups, "actions", "2 Actions", counts, mine);
            if (s.PlayerSpells.Count > 0) GroupButton(groups, "spells", "3 Spells", new Dictionary<string, int> { ["spells"] = s.PlayerSpells.Count(x => x.Unavailable == null) }, mine);
            if (s.PlayerFeatures.Count > 0) GroupButton(groups, "features", "4 Features", new Dictionary<string, int> { ["features"] = s.PlayerFeatures.Count(x => x.Unavailable == null) }, mine);
            _bar.Add(groups);

            var ends = TradeWindow.Row("xk-cbt-ends");
            var dash = s.PlayerOptions.FirstOrDefault(o => o.Action is DashAction);
            var dashButton = new Button(() => { if (dash != null) ActionRequested?.Invoke(dash.Action); }) { text = "Dash", name = "xk-cbt-dash" };
            dashButton.AddToClassList("xk-button");
            dashButton.SetEnabled(mine && dash != null && dash.Unavailable == null);
            dashButton.tooltip = dash?.Unavailable ?? $"Use your action to gain {p?.Speed ?? 0} ft more movement this turn.";
            ends.Add(dashButton);
            var grid = new Button(ToggleGrid) { text = "Grid (Tab)", name = "xk-cbt-grid-toggle" };
            grid.AddToClassList("xk-button");
            ends.Add(grid);
            var end = s.PlayerOptions.FirstOrDefault(o => o.Action is EndTurnAction);
            var endButton = new Button(() => { if (end != null) ActionRequested?.Invoke(end.Action); }) { text = "End turn (Space)", name = "xk-cbt-end" };
            endButton.AddToClassList("xk-button");
            endButton.AddToClassList("xk-button--primary");
            endButton.SetEnabled(mine && end != null && end.Unavailable == null);
            ends.Add(endButton);
            _bar.Add(ends);
        }

        private static void Slot(VisualElement row, string name, bool ready, string note, string cls)
        {
            var l = TradeWindow.Text(name, "xk-cbt-slot");
            l.name = "xk-cbt-slot-" + name.ToLowerInvariant();
            l.AddToClassList(cls);
            l.EnableInClassList("xk-cbt-slot--used", !ready);
            // Phase 19: each resource has its own icon, so the three are not told apart by colour alone (a used one is dimmed and italic).
            Realmkeeper.UI.RkIcons.Lead(l, name == "Action" ? "action" : name == "Bonus" ? "bonus" : "reaction", "s", "ink");
            l.tooltip = ready ? $"{name}: ready" + (note != null ? $" ({note})" : string.Empty) : $"{name}: used this turn";
            row.Add(l);
        }

        private void GroupButton(VisualElement row, string group, string label, Dictionary<string, int> counts, bool mine)
        {
            var n = counts.TryGetValue(group, out var c) ? c : 0;
            var b = new Button(() => OpenGroupPopup(group)) { text = $"{label} ({n})", name = "xk-cbt-group-" + group };
            b.AddToClassList("xk-button");
            b.AddToClassList("xk-cbt-group");
            b.EnableInClassList("xk-cbt-group--open", OpenGroup == group);
            b.SetEnabled(mine);
            b.tooltip = n == 0 ? "Nothing in this group can be used now: open it to see why." : $"{n} choice(s) you can use now.";
            row.Add(b);
        }

        /// <summary>The selected combatant at a glance: name, hit points as a bar, AC, conditions, movement.</summary>
        private void FillCard(EncounterSnapshot s)
        {
            var c = Find(s, Inspected) ?? s.Player;
            if (c == null) return;
            _cardName.text = c.Name + (c.IsActive ? " · acting" : string.Empty);
            var share = c.MaxHp > 0 ? UnityEngine.Mathf.Clamp01((float)c.Hp / c.MaxHp) : 0f;
            _hpFill.style.width = new Length(share * 100f, LengthUnit.Percent);
            _hpFill.EnableInClassList("xk-cbt-hp-fill--low", share <= 0.34f);
            var state = c.State == VitalState.Conscious ? string.Empty : " · " + c.State.ToString().ToLowerInvariant();
            var cover = c.Kind == CombatantKind.Player || string.IsNullOrEmpty(c.CoverFromPlayer) ? string.Empty : " · from you: " + c.CoverFromPlayer;
            _cardLine.text = $"{c.Hp}/{c.MaxHp} HP{(c.TempHp > 0 ? $" +{c.TempHp}" : string.Empty)} · AC {c.ArmorClass} · {c.Speed} ft{state}" + cover;
            _cardConditions.Clear();
            foreach (var cond in c.Conditions)
            {
                var badge = TradeWindow.Text(cond, "rk-badge");
                badge.AddToClassList("rk-badge--warn");
                _cardConditions.Add(badge);
            }

            if (c.Concentration != null)
            {
                var conc = TradeWindow.Text("Concentrating: " + c.Concentration, "rk-badge");
                conc.AddToClassList("rk-badge--info");
                _cardConditions.Add(conc);
            }
        }

        public VisualElement Root { get; }
        public bool IsOpen => !Root.ClassListContains("xk-hidden");
        public EncounterSnapshot Snapshot { get; private set; }
        public CombatantId Inspected { get; private set; }
        public string ResultText => _result.text;
        public string InspectorText { get; private set; } = string.Empty;

        public event Action<CombatAction> ActionRequested;
        public event Action<GridPos> MoveRequested;
        public event Action WithdrawRequested;
        public event Action CallOffRequested;
        public event Action ContinueRequested;
        public event Action CloseRequested;

        /// <summary>Phase 11: open the loot a finished fight left.</summary>
        public event Action<string> LootRequested;

        /// <summary>Why the player cannot move to a square (null: they can); the controller asks the session.</summary>
        public Func<GridPos, string> MoveProblem { get; set; }

        public void Show(EncounterSnapshot s, string result = null, bool failed = false)
        {
            Snapshot = s;
            Root.RemoveFromClassList("xk-hidden");
            if (s == null) return;
            if (result != null)
            {
                _result.text = result.Length > 0 ? result : "Done.";
                _result.EnableInClassList("xk-message--error", failed);
            }

            var active = s.Status == EncounterStatus.Active;
            _title.text = s.Name;
            _subtitle.text = active
                ? $"Round {s.Round.ToString(CultureInfo.InvariantCulture)} · {(s.Lethality == Lethality.Lethal ? "lethal: the fallen can die" : "non-lethal: the fallen are knocked out")} · time stands still"
                : WorldSession.Summary(s);
            _turn.text = !active ? "The fight is over" : s.IsPlayersTurn ? "Your turn" : $"{s.Active?.Name}'s turn";
            _turn.EnableInClassList("xk-combat-turn--yours", s.IsPlayersTurn);
            // Phase 26: the result as a banner, in words and colour.
            var outcome = active ? null : OutcomeWord(s);
            _outcome.text = outcome ?? string.Empty;
            _outcome.EnableInClassList("xk-hidden", outcome == null);
            _outcome.EnableInClassList("rk-badge--good", outcome == Realmkeeper.UI.RkText.Get("combat.won"));
            _outcome.EnableInClassList("rk-badge--bad", outcome == Realmkeeper.UI.RkText.Get("combat.lost"));
            _outcome.EnableInClassList("rk-badge--info", outcome != null && outcome != Realmkeeper.UI.RkText.Get("combat.won") && outcome != Realmkeeper.UI.RkText.Get("combat.lost"));
            _continue.EnableInClassList("xk-hidden", !active || s.IsPlayersTurn);
            _withdraw.EnableInClassList("xk-hidden", !active);
            _callOff.EnableInClassList("xk-hidden", !active || s.Kind != EncounterKind.Practice);
            _close.EnableInClassList("xk-hidden", active);

            if (Inspected.IsEmpty || Find(s, Inspected) == null) Inspected = s.Player?.Id ?? (s.Order.Count > 0 ? s.Order[0].Id : default);
            FillPrompt(s); // Phase 10: a question waiting on the player comes first
            FillOrder(s);
            FillGrid(s);
            if (!s.IsPlayersTurn) OpenGroup = null; // a new turn starts with the pop-up closed
            FillActions(s);
            FillBar(s);
            FillCard(s);
            FillInspector(s);
            FillLog(s);
        }

        public void Hide()
        {
            Root.AddToClassList("xk-hidden");
            HideConfirm();
            ShowHover(null, default, true);
            OpenGroup = null;
            Snapshot = null;
            Inspected = default;
            _result.text = "Nothing yet.";
        }

        /// <summary>Shows a combatant in the inspector (as clicking their row or square does).</summary>
        public void Inspect(CombatantId id)
        {
            Inspected = id;
            if (Snapshot != null)
            {
                FillOrder(Snapshot);
                FillCard(Snapshot);
                FillInspector(Snapshot);
            }
        }

        // ----- Parts ---------------------------------------------------------------------------------------

        private void FillOrder(EncounterSnapshot s)
        {
            _order.Clear();
            for (var i = 0; i < s.Order.Count; i++)
            {
                var c = s.Order[i];
                var id = c.Id;
                var row = new Button(() => Inspect(id)) { name = "xk-combat-order-" + i };
                row.AddToClassList("xk-combat-row");
                row.EnableInClassList("xk-combat-row--active", c.IsActive);
                row.EnableInClassList("xk-combat-row--selected", c.Id == Inspected);
                row.EnableInClassList("xk-combat-row--down", c.State != VitalState.Conscious);
                row.AddToClassList(SideClass(c));
                var hp = c.State == VitalState.Dead ? "dead" : c.State == VitalState.Fled ? "fled" : $"{c.Hp}/{c.MaxHp} HP{(c.TempHp > 0 ? $" +{c.TempHp}" : string.Empty)}";
                var state = c.State == VitalState.Dying ? $" · dying ({c.DeathSaveSuccesses}✓ {c.DeathSaveFailures}✗)" : c.State == VitalState.Stable ? " · unconscious, stable" : string.Empty;
                var conditions = c.Conditions.Count > 0 ? " · " + string.Join(", ", c.Conditions) : string.Empty;
                var magic = (c.Concentration != null ? " · concentrating on " + c.Concentration : string.Empty) + (c.Effects.Count > 0 ? " · " + string.Join(", ", c.Effects) : string.Empty);
                var cover = string.IsNullOrEmpty(c.CoverFromPlayer) || c.CoverFromPlayer == "no cover" ? string.Empty : " · " + c.CoverFromPlayer; // Phase 14
                // Combat Overhaul: a compact chip in the top strip; the rest of the line is its tooltip (and the card shows it all).
                row.text = $"{(c.IsActive ? "▶ " : string.Empty)}{c.Name}\n{hp}{(c.Conditions.Count > 0 ? " · " + c.Conditions.Count + " cond." : string.Empty)}";
                row.tooltip = $"Initiative {c.Initiative} · {hp} · AC {c.ArmorClass}{state}{conditions}{magic}{cover}";
                HealthLine(row, c.State == VitalState.Conscious && c.MaxHp > 0 ? (float)c.Hp / c.MaxHp : 0f); // Phase 25: health at a glance (the numbers stay in the text)
                _order.Add(row);
            }
        }

        /// <summary>
        /// Phase 25: a thin health line along the bottom of a turn-order card, painted (a child element would stop the
        /// button measuring its text). Above half it is green, above a quarter ochre, below that red; the card's text
        /// gives the numbers, so colour is never the only signal.
        /// </summary>
        private static void HealthLine(VisualElement row, float share)
        {
            share = UnityEngine.Mathf.Clamp01(share);
            var tone = share > 0.5f ? Realmkeeper.UI.RkIcons.Tone("good") : share > 0.25f ? Realmkeeper.UI.RkIcons.Tone("warn") : Realmkeeper.UI.RkIcons.Tone("bad");
            row.generateVisualContent += ctx =>
            {
                var w = ctx.visualElement.layout.width;
                var h = ctx.visualElement.layout.height;
                if (float.IsNaN(w) || w < 12f) return;
                var p = ctx.painter2D;
                Bar(p, 5f, h - 5f, w - 10f, 3f, new UnityEngine.Color(0f, 0f, 0f, 0.55f));
                if (share > 0f) Bar(p, 5f, h - 5f, (w - 10f) * share, 3f, tone);
            };
            row.MarkDirtyRepaint();
        }

        private static void Bar(Painter2D p, float x, float y, float w, float h, UnityEngine.Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(new UnityEngine.Vector2(x, y));
            p.LineTo(new UnityEngine.Vector2(x + w, y));
            p.LineTo(new UnityEngine.Vector2(x + w, y + h));
            p.LineTo(new UnityEngine.Vector2(x, y + h));
            p.ClosePath();
            p.Fill();
        }

        private void FillGrid(EncounterSnapshot s)
        {
            _grid.Clear();
            var centre = s.Player?.Position ?? (s.Order.Count > 0 ? s.Order[0].Position : default);
            var x0 = centre.X - GridColumns / 2;
            var y0 = centre.Y + GridRows / 2;
            var at = new Dictionary<(int, int), CombatantView>();
            foreach (var c in s.Order)
                if (c.State != VitalState.Fled && (c.State != VitalState.Dead || !at.ContainsKey((c.Position.X, c.Position.Y)))) at[(c.Position.X, c.Position.Y)] = c;
            var yourTurn = s.IsPlayersTurn;
            for (var r = 0; r < GridRows; r++)
            {
                var line = TradeWindow.Row("xk-combat-grid-row");
                for (var col = 0; col < GridColumns; col++)
                {
                    var pos = new GridPos(x0 + col, y0 - r);
                    var cell = new Button { name = $"xk-combat-cell-{pos.X}-{pos.Y}" };
                    cell.AddToClassList("xk-combat-cell");
                    if (InPreview(pos)) cell.AddToClassList("xk-combat-cell--area");
                    if ((Composing && NeedsPoint || AimingFeature) && yourTurn) { var aim = pos; cell.clicked += () => Aim(aim); } // Phase 20A: or a feature's square
                    if (s.DifficultTerrainContains(pos)) cell.AddToClassList("xk-combat-cell--difficult");
                    if (s.BlockedContains(pos)) cell.AddToClassList("xk-combat-cell--wall"); // Phase 13
                    string terrain = null; // Phase 14: doors and low cover on the grid
                    if (s.Doors != null)
                        foreach (var d in s.Doors)
                            if (d.Covers(pos))
                            {
                                cell.AddToClassList(d.Open ? "xk-combat-cell--door-open" : "xk-combat-cell--door");
                                terrain = $"{d.Name} ({(d.Open ? "open" : d.Locked ? "locked" : "shut")})";
                            }

                    if (s.LowCover != null)
                        foreach (var lc in s.LowCover)
                            if (lc.X == pos.X && lc.Y == pos.Y)
                            {
                                cell.AddToClassList("xk-combat-cell--cover");
                                terrain = lc.Name + " (half cover to anyone behind it)";
                            }

                    if (terrain != null) cell.tooltip = terrain;
                    if (at.TryGetValue((pos.X, pos.Y), out var who))
                    {
                        cell.text = Initials(who.Name);
                        cell.AddToClassList(SideClass(who));
                        cell.EnableInClassList("xk-combat-cell--active", who.IsActive);
                        cell.EnableInClassList("xk-combat-cell--down", who.State != VitalState.Conscious);
                        cell.EnableInClassList("xk-combat-cell--hidden", !who.InPlayersLine);
                        cell.tooltip = $"{who.Name}: {who.Hp}/{who.MaxHp} HP, AC {who.ArmorClass}, {who.State.ToString().ToLowerInvariant()}" +
                                       (string.IsNullOrEmpty(who.CoverFromPlayer) ? string.Empty : " · from you: " + who.CoverFromPlayer);
                        var id = who.Id;
                        cell.clicked += () => Inspect(id);
                    }
                    else if (Composing && NeedsPoint || AimingFeature) { }
                    else if (yourTurn && s.Status == EncounterStatus.Active)
                    {
                        var problem = MoveProblem?.Invoke(pos);
                        cell.EnableInClassList("xk-combat-cell--reachable", problem == null);
                        cell.tooltip = (terrain != null ? terrain + ". " : string.Empty) + (problem ?? $"Move to {pos} ({s.Player.Position.DistanceFeet(pos)} ft away).");
                        var p = pos;
                        if (problem == null) cell.clicked += () => MoveRequested?.Invoke(p);
                    }

                    line.Add(cell);
                }

                _grid.Add(line);
            }

            if (s.Player != null && s.IsPlayersTurn)
                _grid.Add(TradeWindow.Text($"{s.Player.MovementLeft} ft of movement left (speed {s.Player.Speed} ft). Green squares are reachable; shaded ones are difficult terrain (double cost)." +
                                                 (s.Doors.Count > 0 || s.LowCover.Count > 0 ? " Brown squares are doors (a shut door blocks sight); tan ones are low cover. Faded figures are out of your line of sight." : string.Empty), "xk-w-note"));
        }

        private void FillActions(EncounterSnapshot s)
        {
            _actions.Clear();
            // The pop-up shows for the open group, while aiming a spell, and for a finished fight's summary and loot.
            _pop.EnableInClassList("xk-hidden", s.Status == EncounterStatus.Active && !(s.IsPlayersTurn && s.Prompt == null && (OpenGroup != null || Composing)));
            if (s.Status != EncounterStatus.Active)
            {
                _actions.Add(TradeWindow.Text(WorldSession.Summary(s), "xk-w-note"));
                if (s.LootKey != null)
                {
                    var key = s.LootKey;
                    var loot = new Button(() => LootRequested?.Invoke(key)) { text = "Look over what they left (" + s.LootSummary + ")", name = "xk-combat-loot" };
                    loot.AddToClassList("xk-button");
                    loot.AddToClassList("xk-button--primary");
                    _actions.Add(loot);
                }

                return;
            }

            if (!s.IsPlayersTurn)
            {
                CancelCast();
                _actions.Add(TradeWindow.Text($"{s.Active?.Name} is acting. Your options return on your turn.", "xk-w-empty"));
                return;
            }

            if (s.Prompt != null)
            {
                _actions.Add(TradeWindow.Text("Answer the question above first: the action it interrupted waits on your answer.", "xk-w-note"));
                return;
            }

            if (Composing) { FillComposer(s); return; } // Phase 9: aiming a spell

            var p = s.Player;
            _actions.Add(TradeWindow.Text(
                $"Action {(p.ActionUsed ? (p.AttacksLeft > 0 ? $"attacking ({p.AttacksLeft} left)" : "used") : "ready")} · bonus action {(p.BonusActionUsed ? "used" : "ready")} · reaction {(p.ReactionUsed ? "used" : "ready")}",
                "xk-combat-economy"));
            var basics = Fold("Attacks and actions", "xk-combat-fold-actions");
            basics.AddToClassList("xk-combat-fold");
            _actions.Add(basics);
            for (var i = 0; i < s.PlayerOptions.Count; i++)
            {
                var o = s.PlayerOptions[i];
                var row = TradeWindow.Column("xk-combat-option");
                var action = o.Action;
                var b = new Button(() => ActionRequested?.Invoke(action)) { text = (o.IsBonus ? "Bonus: " : string.Empty) + o.Label, name = "xk-combat-act-" + i };
                b.AddToClassList("xk-button");
                b.AddToClassList("xk-combat-act");
                if (o.Action is EndTurnAction) b.AddToClassList("xk-button--primary");
                b.SetEnabled(o.Unavailable == null);
                b.tooltip = o.Unavailable ?? o.Action.Describe;
                row.Add(b);
                if (o.Unavailable != null) row.Add(TradeWindow.Text(o.Unavailable, "xk-combat-why"));
                row.EnableInClassList("xk-hidden", GroupOf(o) != OpenGroup); // in the hierarchy, shown with its group
                basics.Add(row);
            }

            FillMagic(s); // Phase 9: spells and class features
            basics.EnableInClassList("xk-hidden", OpenGroup != "attacks" && OpenGroup != "actions"); // Combat Overhaul: one group at a time
            _actions.Q<VisualElement>("xk-combat-fold-spells")?.EnableInClassList("xk-hidden", OpenGroup != "spells");
            _actions.Q<VisualElement>("xk-combat-fold-features")?.EnableInClassList("xk-hidden", OpenGroup != "features");
        }

        private void FillInspector(EncounterSnapshot s)
        {
            _inspector.Clear();
            var c = Find(s, Inspected);
            if (c == null) return;
            var lines = new List<string>
            {
                $"{c.Name} — {c.Kind}, side \"{c.Side}\"{(c.HostileToPlayer ? " (hostile)" : string.Empty)}",
                $"Hit points {c.Hp}/{c.MaxHp}{(c.TempHp > 0 ? $" (+{c.TempHp} temporary)" : string.Empty)} · {c.State.ToString().ToLowerInvariant()}",
                $"Armour class {c.ArmorClass}: {c.ArmorClassText}",
                $"Speed {c.Speed} ft · initiative {c.Initiative} · at {c.Position}",
                $"Attacks: {(string.IsNullOrEmpty(c.Attacks) ? "none" : c.Attacks)}",
                c.Stats,
            };
            if (c.State == VitalState.Dying) lines.Add($"Death saves: {c.DeathSaveSuccesses} successes, {c.DeathSaveFailures} failures (three of either decides).");
            lines.Add(c.Conditions.Count > 0 ? "Conditions: " + string.Join(", ", c.Conditions) : "No conditions.");
            if (c.Concentration != null) lines.Add("Concentrating on " + c.Concentration + " (a Constitution save when damaged keeps it).");
            if (c.Effects.Count > 0) lines.Add("Effects: " + string.Join(", ", c.Effects));
            if (c.IsCaster) lines.Add($"Spell save DC {c.SpellSaveDc}, spell attack {(c.SpellAttack >= 0 ? "+" : string.Empty)}{c.SpellAttack}");
            if (!string.IsNullOrEmpty(c.Resources)) lines.Add("Resources: " + c.Resources);
            if (!string.IsNullOrEmpty(c.Bestiary)) lines.Add("Bestiary: " + c.Bestiary); // Phase 10
            InspectorText = string.Join("\n", lines);
            foreach (var l in lines) _inspector.Add(TradeWindow.Text(l, "xk-combat-inspect-line"));
        }

        private void FillLog(EncounterSnapshot s)
        {
            _log.Clear();
            var from = Math.Max(0, s.Log.Count - 40);
            for (var i = from; i < s.Log.Count; i++)
            {
                var e = s.Log[i];
                var line = TradeWindow.Text(e.Text, "xk-combat-log-line");
                line.AddToClassList("xk-combat-log--" + e.Kind.ToString().ToLowerInvariant());
                _log.Add(line);
            }

            _log.schedule.Execute(() => _log.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue));
        }

        // ----- Bits ----------------------------------------------------------------------------------------

        private static CombatantView Find(EncounterSnapshot s, CombatantId id)
        {
            foreach (var c in s.Order) if (c.Id == id) return c;
            return null;
        }

        private static string SideClass(CombatantView c) =>
            c.Kind == CombatantKind.Player ? "xk-combat--player" : c.HostileToPlayer ? "xk-combat--enemy" : "xk-combat--ally";

        private static string Initials(string name)
        {
            var parts = name.Split(' ');
            return parts.Length > 1 ? (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant() : name.Substring(0, Math.Min(2, name.Length));
        }

        private Button HeadButton(string text, string name, Action act)
        {
            var b = new Button(act) { text = text, name = name };
            b.AddToClassList("xk-button");
            b.AddToClassList("xk-combat-head-button");
            return b;
        }
    }
}
