using System;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 25: the one inspector layout, for items, equipment, creatures, people, places and conditions. It is built
    /// top to bottom in this order:
    /// <list type="number">
    /// <item>a header: icon, name, a subtitle and word badges;</item>
    /// <item>the primary statistics as key–value rows;</item>
    /// <item>comparisons ("if equipped");</item>
    /// <item>text sections (effects, requirements, description);</item>
    /// <item>the actions, each a real command supplied by the caller.</item>
    /// </list>
    /// Disabled actions say why in their tooltip and in a line under the buttons. Nothing here reads the world: callers
    /// pass what the game's queries returned.
    /// </summary>
    public sealed class RkInspector
    {
        public VisualElement Root { get; }
        private VisualElement _actions;
        private Label _why;

        public RkInspector(string name)
        {
            Root = new VisualElement { name = name };
            Root.AddToClassList("rk-inspector");
        }

        public RkInspector Header(string icon, string title, string subtitle = null, string tone = null)
        {
            var head = new VisualElement();
            head.AddToClassList("rk-inspector-head");
            if (!string.IsNullOrEmpty(icon))
            {
                var frame = new VisualElement();
                frame.AddToClassList("rk-inspector-icon");
                frame.Add(RkIcons.Make(icon, "xl", tone));
                head.Add(frame);
            }

            var text = new VisualElement();
            text.AddToClassList("rk-inspector-titles");
            var t = new Label(title) { name = Root.name + "-title" };
            t.AddToClassList("rk-inspector-title");
            text.Add(t);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = new Label(subtitle);
                s.AddToClassList("rk-inspector-subtitle");
                text.Add(s);
            }

            head.Add(text);
            Root.Add(head);
            return this;
        }

        /// <summary>A word badge: good, warn, bad, info or null. The word carries the meaning; the colour only helps.</summary>
        public RkInspector Badge(string text, string tone = null)
        {
            if (string.IsNullOrEmpty(text)) return this;
            var row = Root.Q<VisualElement>(className: "rk-inspector-badges");
            if (row == null)
            {
                row = new VisualElement();
                row.AddToClassList("rk-inspector-badges");
                Root.Add(row);
            }

            var b = new Label(text);
            b.AddToClassList("rk-badge");
            if (!string.IsNullOrEmpty(tone)) b.AddToClassList("rk-badge--" + tone);
            row.Add(b);
            return this;
        }

        public RkInspector Stat(string key, string value, string icon = null, string tip = null)
        {
            if (string.IsNullOrEmpty(value)) return this;
            var row = new VisualElement();
            row.AddToClassList("rk-inspector-stat");
            var k = new Label(key);
            k.AddToClassList("rk-inspector-key");
            if (icon != null) RkIcons.Lead(k, icon, "s");
            var v = new Label(value);
            v.AddToClassList("rk-inspector-value");
            row.Add(k);
            row.Add(v);
            if (tip != null) row.tooltip = tip;
            Root.Add(row);
            return this;
        }

        /// <summary>
        /// A comparison row: "Armour class 15 → 17 (better)". <paramref name="change"/> is positive when the change helps
        /// the player, negative when it hurts, 0 when neutral or unknown. The word says which, so colour is never alone.
        /// </summary>
        public RkInspector Compare(string key, string before, string after, int change, string tip = null)
        {
            var row = new VisualElement();
            row.AddToClassList("rk-inspector-compare");
            var k = new Label(key);
            k.AddToClassList("rk-inspector-key");
            var v = new Label(before == after ? before + " (no change)" : before + " → " + after + (change > 0 ? "  ▲ better" : change < 0 ? "  ▼ worse" : string.Empty));
            v.AddToClassList("rk-inspector-value");
            v.AddToClassList(change > 0 ? "rk-compare--better" : change < 0 ? "rk-compare--worse" : "rk-compare--same");
            row.Add(k);
            row.Add(v);
            if (tip != null) row.tooltip = tip;
            Root.Add(row);
            return this;
        }

        public RkInspector Section(string heading)
        {
            var h = new Label(heading);
            h.AddToClassList("rk-inspector-section");
            Root.Add(h);
            return this;
        }

        public RkInspector Text(string text, string extraClass = null)
        {
            if (string.IsNullOrEmpty(text)) return this;
            var l = new Label(text);
            l.AddToClassList("rk-inspector-text");
            if (extraClass != null) l.AddToClassList(extraClass);
            Root.Add(l);
            return this;
        }

        public RkInspector Note(string text, string tone)
        {
            if (string.IsNullOrEmpty(text)) return this;
            var l = new Label(text);
            l.AddToClassList("rk-inspector-note");
            if (tone != null) l.AddToClassList("rk-inspector-note--" + tone);
            Root.Add(l);
            return this;
        }

        public RkInspector Action(string text, string name, Action act, bool enabled = true, string reason = null, bool primary = false, bool danger = false)
        {
            if (_actions == null)
            {
                _actions = new VisualElement();
                _actions.AddToClassList("rk-inspector-actions");
                Root.Add(_actions);
            }

            var b = new Button(act) { text = text, name = name };
            b.AddToClassList("rk-btn");
            if (primary) b.AddToClassList("rk-btn--primary");
            if (danger) b.AddToClassList("rk-btn--danger");
            b.SetEnabled(enabled);
            if (!enabled && reason != null)
            {
                b.tooltip = reason;
                if (_why == null)
                {
                    _why = new Label();
                    _why.AddToClassList("rk-inspector-why");
                    Root.Add(_why);
                }

                _why.text = (string.IsNullOrEmpty(_why.text) ? string.Empty : _why.text + "\n") + text + ": " + reason;
            }

            _actions.Add(b);
            return this;
        }

        /// <summary>A placeholder for an empty selection, in words.</summary>
        public static VisualElement Empty(string name, string text)
        {
            var root = new VisualElement { name = name };
            root.AddToClassList("rk-inspector");
            root.AddToClassList("rk-inspector--empty");
            var l = new Label(text);
            l.AddToClassList("rk-empty");
            root.Add(l);
            return root;
        }
    }
}
