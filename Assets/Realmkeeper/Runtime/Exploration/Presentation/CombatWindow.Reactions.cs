using System;
using Realmkeeper.Application;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 10 (ADR-0045): the question the player is asked — a reaction (Shield, an opportunity attack, Parry …) or a
    /// decision (spend Indomitable, use Bardic Inspiration). A banner across the top of the encounter panel, above every
    /// column, saying what happened, who would react, what it costs and what it does, with Accept and Decline. While it
    /// waits, nothing else can be done in the fight. It only raises the answer; the controller dispatches it.
    /// </summary>
    public sealed partial class CombatWindow
    {
        private VisualElement _prompt;
        private Label _promptTitle;
        private Label _promptBody;
        private Button _accept;
        private Button _decline;

        /// <summary>The player's answer to the waiting question: its ID, and yes or no.</summary>
        public event Action<int, bool> ReactionAnswered;

        /// <summary>The question shown now, or null.</summary>
        public ReactionPromptView Prompt { get; private set; }

        public string PromptText => _prompt == null || _prompt.ClassListContains("xk-hidden") ? string.Empty : _promptTitle.text + " " + _promptBody.text;

        private void EnsurePrompt()
        {
            if (_prompt != null) return;
            _prompt = new VisualElement { name = "xk-combat-prompt" };
            _prompt.AddToClassList("xk-combat-prompt");
            _prompt.AddToClassList("xk-hidden");
            var text = new VisualElement();
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            _promptTitle = TradeWindow.Text("-", "xk-combat-prompt-title");
            _promptTitle.name = "xk-combat-prompt-title";
            _promptBody = TradeWindow.Text("-", "xk-combat-prompt-body");
            _promptBody.name = "xk-combat-prompt-body";
            text.Add(_promptTitle);
            text.Add(_promptBody);
            _prompt.Add(text);
            _accept = new Button(() => Answer(true)) { name = "xk-combat-prompt-accept", text = "Accept" };
            _accept.AddToClassList("xk-button");
            _accept.AddToClassList("xk-button--primary");
            _decline = new Button(() => Answer(false)) { name = "xk-combat-prompt-decline", text = "Decline" };
            _decline.AddToClassList("xk-button");
            _prompt.Add(_accept);
            _prompt.Add(_decline);
            Root.Insert(Math.Min(1, Root.childCount), _prompt); // under the title row, above every column
        }

        private void Answer(bool accept)
        {
            var p = Prompt;
            if (p == null) return;
            _accept.SetEnabled(false);
            _decline.SetEnabled(false);
            ReactionAnswered?.Invoke(p.Id, accept);
        }

        private void FillPrompt(EncounterSnapshot s)
        {
            EnsurePrompt();
            Prompt = s?.Status == Realmkeeper.Domain.Combat.EncounterStatus.Active ? s.Prompt : null;
            _prompt.EnableInClassList("xk-hidden", Prompt == null);
            if (Prompt == null) return;
            var p = Prompt;
            _promptTitle.text = p.IsDecision ? $"{p.Reactor}: use {p.Name}?" : $"{p.Reactor} can react — {p.Name}?";
            _promptBody.text = $"{p.Trigger} Cost: {p.Cost}. {p.Consequence}";
            _accept.text = p.IsDecision ? $"Use {p.Name}" : $"React: {p.Name}";
            _decline.text = p.IsDecision ? "Not now" : "Let it pass";
            _accept.SetEnabled(true);
            _decline.SetEnabled(true);
        }
    }
}
