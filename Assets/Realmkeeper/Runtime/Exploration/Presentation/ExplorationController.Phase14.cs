using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Rpg;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 14 (ADR-0068, ADR-0070): what the HUD says about lasting conditions (what each does and how it ends, with
    /// Break free and Lesser Restoration where they apply), who follows or waits and why, and whether this is a place to
    /// rest. Shown in settlements and in places alike; every button goes through the session's commands.
    /// </summary>
    public sealed partial class ExplorationController
    {
        private VisualElement _conditionsPanel;
        private string _conditionsSignature;

        /// <summary>The last text the conditions panel showed (for tests and captures).</summary>
        public string ConditionsText { get; private set; } = string.Empty;

        private void RefreshConditionsHud()
        {
            if (Hud?.Root == null || _session == null || !_session.HasWorld) return;
            var conditions = _session.GetConditions();
            var followers = _session.GetFollowers();
            var rest = InFight ? null : _session.RestHere(RestKind.Long);
            var sig = string.Join("|", conditions.Select(c => c.Name + c.HowToEnd)) + "#" + string.Join("|", followers.Select(f => f.Name + f.State)) + "#" + (rest?.Describe ?? string.Empty);
            if (sig == _conditionsSignature && _conditionsPanel != null && _conditionsPanel.panel != null && _conditionsPanel.parent == Hud.Root) return;
            _conditionsSignature = sig;
            _conditionsPanel?.RemoveFromHierarchy();
            _conditionsPanel = null;
            if (conditions.Count == 0 && followers.Count == 0 && !InPlace) { ConditionsText = string.Empty; return; }
            var panel = new VisualElement { name = "xk-conditions" };
            panel.AddToClassList("xk-conditions");
            var text = new System.Text.StringBuilder();
            foreach (var c in conditions)
            {
                var name = new Label(c.Name + (string.IsNullOrEmpty(c.Reason) ? string.Empty : " — " + c.Reason));
                name.AddToClassList("xk-condition-name");
                var effect = new Label(c.Effect);
                effect.AddToClassList("xk-condition-effect");
                var end = new Label("Ends: " + c.HowToEnd);
                end.AddToClassList("xk-condition-end");
                panel.Add(name);
                panel.Add(effect);
                panel.Add(end);
                text.AppendLine(c.Name + (string.IsNullOrEmpty(c.Reason) ? string.Empty : " (" + c.Reason + ")") + ": " + c.Effect + " Ends: " + c.HowToEnd);
                if (c.CanBreakFree)
                {
                    var b = new Button(() => { var r = _session.BreakFree(); Hud?.ShowMessage(r); _conditionsSignature = null; RefreshConditionsHud(); }) { text = "Break free" };
                    b.AddToClassList("xk-condition-button");
                    panel.Add(b);
                }

                if (c.CanRestore)
                {
                    var b = new Button(() => { var r = _session.Dispatch(new CastHealingOutsideCombatCommand("lesser_restoration", 2)); Hud?.ShowMessage(r); _conditionsSignature = null; RefreshConditionsHud(); }) { text = "Cast Lesser Restoration" };
                    b.AddToClassList("xk-condition-button");
                    panel.Add(b);
                }
            }

            foreach (var f in followers)
            {
                var l = new Label(f.Name + ": " + f.State + (f.Room != null ? " (" + f.Room + ")" : string.Empty));
                l.AddToClassList("xk-followers");
                panel.Add(l);
                text.AppendLine(f.Name + ": " + f.State);
            }

            if (InPlace && rest != null)
            {
                var l = new Label("Rest: " + rest.Describe);
                l.AddToClassList("xk-rest");
                panel.Add(l);
                text.AppendLine("Rest: " + rest.Describe);
            }

            ConditionsText = text.ToString();
            if (panel.childCount == 0) return;
            _conditionsPanel = panel;
            Hud.Root.Add(panel);
        }
    }
}
