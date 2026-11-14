using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 25: the one notification surface. Toasts stack at the top right of a document, newest first.
    /// <list type="bullet">
    /// <item>At most <see cref="MaxVisible"/> show at once; the rest wait their turn. If more than
    /// <see cref="MaxQueued"/> are waiting, the oldest are folded into one "N more" note, so the screen is never flooded.</item>
    /// <item>A notice with the same key as one shown in the last <see cref="RepeatSeconds"/> is not shown again; one
    /// still on screen is updated in place instead.</item>
    /// <item>Each leaves after <see cref="Seconds"/> (important ones after <see cref="ImportantSeconds"/>), or on a click.</item>
    /// <item>Every notice says what happened in words, with an icon; the kind's colour is never the only signal.</item>
    /// </list>
    /// The recent notices are kept (<see cref="History"/>) for the pause menu's list.
    /// </summary>
    public sealed class RkToasts
    {
        public const int MaxVisible = 4;
        public const int MaxQueued = 8;
        public const float Seconds = 6f;
        public const float ImportantSeconds = 10f;
        public const float RepeatSeconds = 45f;
        public const int HistorySize = 40;

        public sealed class Notice
        {
            public string Key;
            public string Kind;
            public string Title;
            public string Body;
            public string Icon;
            public bool Important;
            public DateTime At;
        }

        private readonly VisualElement _stack;
        private readonly Queue<Notice> _waiting = new Queue<Notice>();
        private readonly Dictionary<string, float> _lastShown = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> _onScreen = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private readonly List<Notice> _history = new List<Notice>();
        private int _folded;

        public RkToasts(VisualElement host)
        {
            _stack = new VisualElement { name = "rk-toasts", pickingMode = PickingMode.Ignore };
            _stack.AddToClassList("rk-toasts");
            host.Add(_stack);
        }

        public VisualElement Stack => _stack;

        /// <summary>Notices shown since creation (tests).</summary>
        public int Shown { get; private set; }

        /// <summary>Notices not shown because the same one was shown recently (tests).</summary>
        public int Suppressed { get; private set; }

        public int Visible => _stack.childCount;

        public IReadOnlyList<Notice> History => _history;

        /// <summary>Kind → icon meaning and tone.</summary>
        public static (string icon, string tone) Look(string kind) => kind switch
        {
            "quest" => ("quest", "accent"),
            "discovery" => ("discovery", "info"),
            "level" => ("bonus", "accent"),
            "item" => ("inventory", null),
            "equipment" => ("equipment", null),
            "reputation" => ("reputation", "good"),
            "reputation-down" => ("disfavour", "bad"),
            "world" => ("events", "warn"),
            "encounter" => ("threats", "bad"),
            "travel" => ("travel", null),
            "bestiary" => ("bestiary", "info"),
            "saved" => ("save", "good"),
            "error" => ("warning", "bad"),
            _ => ("notify", null),
        };

        public void Push(string key, string kind, string title, string body = null, bool important = false)
        {
            if (string.IsNullOrEmpty(title)) return;
            key ??= kind + ":" + title;
            var now = Time.realtimeSinceStartup;
            if (_onScreen.TryGetValue(key, out var existing))
            {
                existing.Q<Label>(className: "rk-toast-body")?.SetBody(body);
                Suppressed++;
                return;
            }

            if (_lastShown.TryGetValue(key, out var at) && now - at < RepeatSeconds)
            {
                Suppressed++;
                return;
            }

            _lastShown[key] = now;
            var notice = new Notice { Key = key, Kind = kind, Title = title, Body = body, Important = important, At = DateTime.Now };
            _history.Insert(0, notice);
            if (_history.Count > HistorySize) _history.RemoveAt(_history.Count - 1);

            if (_stack.childCount < MaxVisible) Show(notice);
            else
            {
                _waiting.Enqueue(notice);
                while (_waiting.Count > MaxQueued)
                {
                    _waiting.Dequeue();
                    _folded++;
                }
            }
        }

        private void Show(Notice n)
        {
            Shown++;
            var (icon, tone) = Look(n.Kind);
            var card = new VisualElement { name = "rk-toast-" + Shown };
            card.AddToClassList("rk-toast");
            card.AddToClassList("rk-toast--" + n.Kind);
            if (n.Important) card.AddToClassList("rk-toast--important");
            card.Add(RkIcons.Make(icon, "m", tone));
            var text = new VisualElement();
            text.AddToClassList("rk-toast-text");
            var title = new Label(n.Title);
            title.AddToClassList("rk-toast-title");
            text.Add(title);
            var body = new Label();
            body.AddToClassList("rk-toast-body");
            body.SetBody(n.Body);
            text.Add(body);
            card.Add(text);
            card.tooltip = RkText.Get("toast.dismiss");
            card.RegisterCallback<ClickEvent>(_ => Dismiss(card, n.Key));
            _stack.Insert(0, card);
            _onScreen[n.Key] = card;
            if (!RkSettings.ReducedMotion)
            {
                card.AddToClassList("rk-toast--enter");
                card.schedule.Execute(() => card.RemoveFromClassList("rk-toast--enter")).StartingIn(16);
            }

            RkUiSound.Play("notify");
            var ms = (long)((n.Important ? ImportantSeconds : Seconds) * 1000f);
            card.schedule.Execute(() => Dismiss(card, n.Key)).StartingIn(ms);
        }

        private void Dismiss(VisualElement card, string key)
        {
            if (card.parent == null) return;
            card.RemoveFromHierarchy();
            if (_onScreen.TryGetValue(key, out var c) && c == card) _onScreen.Remove(key);
            if (_waiting.Count > 0) Show(_waiting.Dequeue());
            else if (_folded > 0)
            {
                var n = _folded;
                _folded = 0;
                Show(new Notice { Key = "folded:" + Time.realtimeSinceStartup, Kind = "notify", Title = RkText.Format("toast.more", n), Body = RkText.Get("toast.more.body") });
            }
        }

        /// <summary>Removes every toast and clears the waiting line (a scene change).</summary>
        public void Clear()
        {
            _stack.Clear();
            _onScreen.Clear();
            _waiting.Clear();
            _folded = 0;
        }
    }

    internal static class RkToastLabels
    {
        public static void SetBody(this Label label, string body)
        {
            label.text = body ?? string.Empty;
            label.style.display = string.IsNullOrEmpty(body) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
