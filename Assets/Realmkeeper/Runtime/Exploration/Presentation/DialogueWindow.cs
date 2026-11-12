using System;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The conversation (Phase 12, ADR-0058): who you are talking to and how they regard you, what was said, what they say
    /// now, and the responses open to you — numbered for the keyboard (1–9), with any skill check named before you choose.
    /// It shows a <see cref="DialogueView"/> and raises events; the controller dispatches the commands. Nothing here
    /// changes the world.
    /// </summary>
    public sealed class DialogueWindow
    {
        private readonly Label _name;
        private readonly Label _standing;
        private readonly Label _quest;
        private readonly VisualElement _transcript;
        private readonly Label _line;
        private readonly VisualElement _responses;
        private readonly Label _message;

        public DialogueWindow(VisualElement hudRoot)
        {
            Root = new VisualElement { name = "xk-dialogue" };
            Root.AddToClassList("xk-window");
            Root.AddToClassList("xk-dialogue");
            Root.AddToClassList("xk-hidden");

            var head = Row("xk-w-head");
            var titles = new VisualElement();
            titles.style.flexGrow = 1;
            _name = Text("-", "xk-w-title");
            _name.name = "xk-dialogue-name";
            _standing = Text("-", "xk-w-subtitle");
            _standing.name = "xk-dialogue-standing";
            titles.Add(_name);
            titles.Add(_standing);
            head.Add(titles);
            var close = new Button(() => EndRequested?.Invoke()) { text = "Leave (Esc)", name = "xk-dialogue-close" };
            close.AddToClassList("xk-button");
            head.Add(close);
            Root.Add(head);

            _quest = Text(string.Empty, "xk-dialogue-quest");
            _quest.name = "xk-dialogue-quest";
            Root.Add(_quest);
            _transcript = new ScrollView { name = "xk-dialogue-transcript" };
            _transcript.AddToClassList("xk-dialogue-transcript");
            Root.Add(_transcript);
            _line = Text("-", "xk-dialogue-line");
            _line.name = "xk-dialogue-line";
            Root.Add(_line);
            _responses = Column("xk-dialogue-responses");
            _responses.name = "xk-dialogue-responses";
            Root.Add(_responses);
            _message = Text(string.Empty, "xk-w-message");
            _message.name = "xk-dialogue-message";
            Root.Add(_message);
            hudRoot.Add(Root);
        }

        public VisualElement Root { get; }
        public bool IsOpen => !Root.ClassListContains("xk-hidden");
        public DialogueView View { get; private set; }
        public string LastMessage => _message.text;

        public event Action<string> ChooseRequested;
        public event Action<ShopId> TradeRequested;
        public event Action EndRequested;

        public void Show(DialogueView view, string message = null, bool failed = false)
        {
            View = view;
            if (view == null)
            {
                Hide();
                return;
            }

            Root.RemoveFromClassList("xk-hidden");
            _name.text = view.Name + (string.IsNullOrEmpty(view.Role) ? string.Empty : " — " + view.Role);
            _standing.text = Tier(view.Tier) + (string.IsNullOrEmpty(view.Mood) ? string.Empty : " · " + view.Mood) + (string.IsNullOrEmpty(view.Activity) ? string.Empty : " · " + view.Activity.ToLowerInvariant());
            _quest.text = view.QuestName == null ? string.Empty : "Concerning: " + view.QuestName;
            _quest.EnableInClassList("xk-hidden", view.QuestName == null);
            _transcript.Clear();
            var start = Math.Max(0, view.Transcript.Count - 7);
            for (var i = start; i < view.Transcript.Count - 1; i++)
            {
                var (speaker, text) = view.Transcript[i];
                var row = Text((speaker == "check" ? "  " : speaker + ": ") + text, speaker == "check" ? "xk-dialogue-check" : "xk-dialogue-said");
                _transcript.Add(row);
            }

            _line.text = view.NpcLine;
            _responses.Clear();
            for (var i = 0; i < view.Responses.Count; i++)
            {
                var r = view.Responses[i];
                // Phase 17: a campaign decision is marked as one (its consequences last); a choice that is not open now is
                // shown disabled with the story's reason, so the player knows it exists and what it would take.
                var label = (i < 9 ? (i + 1) + ".  " : "    ") + (r.Decision ? "◆ " : string.Empty) + r.Text + (r.Check != null ? $"   [{r.Check}]" : string.Empty)
                            + (r.Decision ? "   [decision]" : string.Empty);
                var b = new Button(() => Pick(r)) { text = label, name = "xk-dialogue-response-" + r.Id };
                b.AddToClassList("xk-button");
                b.AddToClassList("xk-dialogue-response");
                b.EnableInClassList("xk-dialogue-response--quest", r.QuestInstanceId != null);
                b.EnableInClassList("xk-dialogue-response--social", r.Kind == ResponseKind.Persuade || r.Kind == ResponseKind.Intimidate || r.Kind == ResponseKind.Deceive || r.Kind == ResponseKind.Bribe || r.Kind == ResponseKind.Negotiate);
                b.EnableInClassList("xk-dialogue-response--decision", r.Decision);
                b.SetEnabled(r.Enabled);
                if (!r.Enabled) b.tooltip = r.Why;
                _responses.Add(b);
                if (!r.Enabled && r.Why != null)
                {
                    var why = Text("      " + r.Why, "xk-dialogue-why");
                    why.name = "xk-dialogue-why-" + r.Id;
                    _responses.Add(why);
                }
            }

            if (message != null)
            {
                _message.text = message;
                _message.EnableInClassList("xk-message--error", failed);
            }
        }

        /// <summary>Chooses the n-th response (1-based), as the number keys do.</summary>
        public bool PickNumber(int n)
        {
            if (View == null || n < 1 || n > View.Responses.Count) return false;
            if (!View.Responses[n - 1].Enabled) return false; // Phase 17: shown, not open
            Pick(View.Responses[n - 1]);
            return true;
        }

        private void Pick(ResponseView r)
        {
            if (r.Kind == ResponseKind.Trade) TradeRequested?.Invoke(r.ShopId);
            else ChooseRequested?.Invoke(r.Id);
        }

        public void Hide()
        {
            Root.AddToClassList("xk-hidden");
            View = null;
            _message.text = string.Empty;
        }

        public static string Tier(ReputationTier t)
        {
            switch (t)
            {
                case ReputationTier.Unknown: return "A stranger to you";
                case ReputationTier.Recognized: return "Knows you";
                case ReputationTier.Respected: return "Respects you";
                case ReputationTier.Trusted: return "Trusts you";
                case ReputationTier.Honored: return "Holds you in honour";
                case ReputationTier.Feared: return "Fears you";
                default: return "Hostile to you";
            }
        }
    }
}
