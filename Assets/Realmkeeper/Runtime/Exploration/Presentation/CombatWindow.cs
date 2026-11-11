using System;
using System.Collections.Generic;
using System.Globalization;
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
            _title = TradeWindow.Text("-", "xk-w-title");
            _title.name = "xk-combat-title";
            _subtitle = TradeWindow.Text("-", "xk-w-subtitle");
            _subtitle.name = "xk-combat-subtitle";
            titles.Add(_title);
            titles.Add(_subtitle);
            head.Add(titles);
            _turn = TradeWindow.Text("-", "xk-combat-turn");
            _turn.name = "xk-combat-turn";
            head.Add(_turn);
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

            var body = TradeWindow.Row("xk-combat-body");
            var orderCol = TradeWindow.Column("xk-combat-col");
            orderCol.AddToClassList("xk-combat-col--order");
            orderCol.Add(TradeWindow.Text("Initiative", "xk-w-heading"));
            _order = new ScrollView { name = "xk-combat-order" };
            _order.AddToClassList("xk-w-list");
            orderCol.Add(_order);
            body.Add(orderCol);

            var gridCol = TradeWindow.Column("xk-combat-col");
            gridCol.AddToClassList("xk-combat-col--grid");
            gridCol.Add(TradeWindow.Text("Field (5-ft squares; click to move)", "xk-w-heading"));
            _grid = new VisualElement { name = "xk-combat-grid" };
            _grid.AddToClassList("xk-combat-grid");
            gridCol.Add(_grid);
            body.Add(gridCol);

            var actCol = TradeWindow.Column("xk-combat-col");
            actCol.AddToClassList("xk-combat-col--actions");
            actCol.Add(TradeWindow.Text("Your turn", "xk-w-heading"));
            _actions = new ScrollView { name = "xk-combat-actions" };
            _actions.AddToClassList("xk-w-list");
            actCol.Add(_actions);
            body.Add(actCol);

            var infoCol = TradeWindow.Column("xk-combat-col");
            infoCol.AddToClassList("xk-combat-col--info");
            infoCol.Add(TradeWindow.Text("Result", "xk-w-heading"));
            _result = TradeWindow.Text("Nothing yet.", "xk-combat-result");
            _result.name = "xk-combat-result";
            infoCol.Add(_result);
            infoCol.Add(TradeWindow.Text("Inspect", "xk-w-heading"));
            _inspector = new ScrollView { name = "xk-combat-inspector" };
            _inspector.AddToClassList("xk-combat-inspector");
            infoCol.Add(_inspector);
            body.Add(infoCol);

            var logCol = TradeWindow.Column("xk-combat-col");
            logCol.AddToClassList("xk-combat-col--log");
            logCol.Add(TradeWindow.Text("Combat log", "xk-w-heading"));
            _log = new ScrollView { name = "xk-combat-log" };
            _log.AddToClassList("xk-combat-log");
            logCol.Add(_log);
            body.Add(logCol);
            Root.Add(body);
            hudRoot.Add(Root);
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
            _continue.EnableInClassList("xk-hidden", !active || s.IsPlayersTurn);
            _withdraw.EnableInClassList("xk-hidden", !active);
            _callOff.EnableInClassList("xk-hidden", !active || s.Kind != EncounterKind.Practice);
            _close.EnableInClassList("xk-hidden", active);

            if (Inspected.IsEmpty || Find(s, Inspected) == null) Inspected = s.Player?.Id ?? (s.Order.Count > 0 ? s.Order[0].Id : default);
            FillPrompt(s); // Phase 10: a question waiting on the player comes first
            FillOrder(s);
            FillGrid(s);
            FillActions(s);
            FillInspector(s);
            FillLog(s);
        }

        public void Hide()
        {
            Root.AddToClassList("xk-hidden");
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
                row.text = $"{(c.IsActive ? "▶ " : string.Empty)}{c.Initiative,2}  {c.Name}\n     {hp} · AC {c.ArmorClass}{state}{conditions}{magic}";
                _order.Add(row);
            }
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
                    if (Composing && NeedsPoint && yourTurn) { var aim = pos; cell.clicked += () => Aim(aim); }
                    if (s.DifficultTerrainContains(pos)) cell.AddToClassList("xk-combat-cell--difficult");
                    if (at.TryGetValue((pos.X, pos.Y), out var who))
                    {
                        cell.text = Initials(who.Name);
                        cell.AddToClassList(SideClass(who));
                        cell.EnableInClassList("xk-combat-cell--active", who.IsActive);
                        cell.EnableInClassList("xk-combat-cell--down", who.State != VitalState.Conscious);
                        cell.tooltip = $"{who.Name}: {who.Hp}/{who.MaxHp} HP, AC {who.ArmorClass}, {who.State.ToString().ToLowerInvariant()}";
                        var id = who.Id;
                        cell.clicked += () => Inspect(id);
                    }
                    else if (Composing && NeedsPoint) { }
                    else if (yourTurn && s.Status == EncounterStatus.Active)
                    {
                        var problem = MoveProblem?.Invoke(pos);
                        cell.EnableInClassList("xk-combat-cell--reachable", problem == null);
                        cell.tooltip = problem ?? $"Move to {pos} ({s.Player.Position.DistanceFeet(pos)} ft away).";
                        var p = pos;
                        if (problem == null) cell.clicked += () => MoveRequested?.Invoke(p);
                    }

                    line.Add(cell);
                }

                _grid.Add(line);
            }

            if (s.Player != null && s.IsPlayersTurn)
                _grid.Add(TradeWindow.Text($"{s.Player.MovementLeft} ft of movement left (speed {s.Player.Speed} ft). Green squares are reachable; shaded ones are difficult terrain (double cost).", "xk-w-note"));
        }

        private void FillActions(EncounterSnapshot s)
        {
            _actions.Clear();
            if (s.Status != EncounterStatus.Active)
            {
                _actions.Add(TradeWindow.Text(WorldSession.Summary(s), "xk-w-note"));
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
                basics.Add(row);
            }

            FillMagic(s); // Phase 9: spells and class features
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
