using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 25: the pause menu. A scrim and a centred column of entries, each a real action supplied by the caller.
    /// An entry that cannot be used now is shown disabled, with the reason as its hint, never hidden or faked.
    /// <list type="bullet">
    /// <item>Esc runs the first entry (Resume).</item>
    /// <item>Up and Down move the keyboard focus between the entries, and Enter or Space runs the focused one.</item>
    /// </list>
    /// A side column lists the recent notices.
    /// </summary>
    public sealed class RkMenuOverlay
    {
        public sealed class Entry
        {
            public string Name;
            public string Text;
            public string Icon;
            public Action Act;
            public bool Enabled = true;
            public string Hint;
            public bool Danger;
            public bool Primary;
        }

        public VisualElement Root { get; }
        public IReadOnlyList<Button> Buttons => _buttons;
        public bool IsOpen => Root.parent != null;

        private readonly List<Button> _buttons = new List<Button>();
        private readonly Action _onResume;
        private readonly float _openedAt = Time.realtimeSinceStartup;
        private readonly List<Entry> _entries = new List<Entry>();

        public RkMenuOverlay(VisualElement host, string title, string subtitle, IReadOnlyList<Entry> entries, IReadOnlyList<RkToasts.Notice> recent = null)
        {
            Root = new VisualElement { name = "rk-pause" };
            Root.AddToClassList("rk-scrim");
            Root.AddToClassList("rk-pause");
            var body = new VisualElement();
            body.AddToClassList("rk-pause-body");

            var menu = new VisualElement { name = "rk-pause-menu" };
            menu.AddToClassList("rk-window");
            menu.AddToClassList("rk-pause-menu");
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("rk-frame");
            menu.Add(frame);
            var t = new Label(title) { name = "rk-pause-title" };
            t.AddToClassList("rk-title");
            menu.Add(t);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = new Label(subtitle) { name = "rk-pause-subtitle" };
                s.AddToClassList("rk-hint");
                s.AddToClassList("rk-wrap");
                menu.Add(s);
            }

            var rule = new VisualElement();
            rule.AddToClassList("rk-rule");
            menu.Add(rule);

            foreach (var e in entries)
            {
                var entry = e;
                var b = new Button(() => { if (entry.Enabled) entry.Act?.Invoke(); }) { name = "pause-" + entry.Name, text = entry.Text };
                b.AddToClassList("rk-btn");
                b.AddToClassList("rk-btn--menu");
                if (entry.Primary) b.AddToClassList("rk-btn--primary");
                if (entry.Danger) b.AddToClassList("rk-btn--danger");
                if (!string.IsNullOrEmpty(entry.Icon)) RkIcons.Lead(b, entry.Icon, "m");
                b.SetEnabled(entry.Enabled);
                b.tooltip = entry.Hint;
                menu.Add(b);
                if (!entry.Enabled && !string.IsNullOrEmpty(entry.Hint))
                {
                    var why = new Label(entry.Hint);
                    why.AddToClassList("rk-hint");
                    why.AddToClassList("rk-pause-why");
                    menu.Add(why);
                }

                _buttons.Add(b);
                _entries.Add(entry);
            }

            _onResume = entries.Count > 0 ? entries[0].Act : null;
            body.Add(menu);

            if (recent != null)
            {
                var side = new VisualElement { name = "rk-pause-recent" };
                side.AddToClassList("rk-window");
                side.AddToClassList("rk-pause-recent");
                var h = new Label(RkText.Get("pause.recent"));
                h.AddToClassList("rk-h2");
                side.Add(h);
                var list = new ScrollView(ScrollViewMode.Vertical);
                list.AddToClassList("rk-pause-recent-list");
                if (recent.Count == 0)
                {
                    var none = new Label(RkText.Get("pause.norecent"));
                    none.AddToClassList("rk-empty");
                    list.Add(none);
                }

                foreach (var n in recent)
                {
                    var row = new VisualElement();
                    row.AddToClassList("rk-pause-notice");
                    var (icon, tone) = RkToasts.Look(n.Kind);
                    row.Add(RkIcons.Make(icon, "s", tone));
                    var l = new Label(n.Title + (string.IsNullOrEmpty(n.Body) ? string.Empty : " — " + n.Body));
                    l.AddToClassList("rk-wrap");
                    l.AddToClassList("rk-small");
                    row.Add(l);
                    list.Add(row);
                }

                side.Add(list);
                body.Add(side);
            }

            Root.Add(body);
            Root.focusable = true;
            Root.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            host.Add(Root);
            RkUiSound.Play("open");
            var first = _buttons.Find(x => x.enabledSelf);
            (first ?? (Focusable)Root).Focus();
        }

        private void OnKey(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                e.StopPropagation();
                if (Time.realtimeSinceStartup - _openedAt > 0.25f) _onResume?.Invoke(); // the Esc that opened the menu does not also close it
                return;
            }

            if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.UpArrow)
            {
                var step = e.keyCode == KeyCode.DownArrow ? 1 : -1;
                var at = _buttons.FindIndex(b => b.focusController?.focusedElement == b);
                for (var i = 1; i <= _buttons.Count; i++)
                {
                    var next = _buttons[((at < 0 ? -1 : at) + step * i + _buttons.Count * 4) % _buttons.Count];
                    if (!next.enabledSelf) continue;
                    next.Focus();
                    break;
                }

                e.StopPropagation();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                var at = _buttons.FindIndex(b => b.focusController?.focusedElement == b);
                if (at >= 0 && _entries[at].Enabled)
                {
                    _entries[at].Act?.Invoke();
                    e.StopPropagation();
                }
            }
        }

        public void Close()
        {
            if (Root.parent == null) return;
            Root.RemoveFromHierarchy();
            RkUiSound.Play("close");
        }
    }
}
