using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 19: runtime tooltips. UI Toolkit shows <see cref="VisualElement.tooltip"/> only in the Editor, so the
    /// many explanatory tooltips written since Phase 2 were never visible in the game. This shows them: hover an
    /// element (or a child of one) that has tooltip text and, after a short delay, a styled box appears beside the
    /// pointer, kept inside the screen. One instance per document root; nothing runs while no tooltip is pending.
    /// </summary>
    public sealed class RkTooltips
    {
        public const long DelayMs = 450;

        private readonly VisualElement _root;
        private readonly Label _box;
        private VisualElement _owner;
        private Vector2 _pointer;
        private IVisualElementScheduledItem _pending;

        private RkTooltips(VisualElement root)
        {
            _root = root;
            _box = new Label { name = "rk-tooltip", pickingMode = PickingMode.Ignore };
            _box.AddToClassList("rk-tooltip");
            _box.style.display = DisplayStyle.None;
            root.Add(_box);
            root.RegisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(e => _pointer = e.position, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(_ => Hide(), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(_ => Hide());
        }

        /// <summary>The visible tooltip text, or null when none is shown (for tests).</summary>
        public string Shown => _box.style.display == DisplayStyle.Flex ? _box.text : null;

        /// <summary>Adds tooltips to a document root (once; a second call returns the first instance).</summary>
        public static RkTooltips Attach(VisualElement root)
        {
            if (root == null) return null;
            if (root.userData is RkTooltips existing) return existing;
            var tips = new RkTooltips(root);
            root.userData = tips;
            return tips;
        }

        /// <summary>Shows the tooltip of an element at once (keyboard focus, tests).</summary>
        public void ShowFor(VisualElement element, Vector2 panelPosition)
        {
            var text = element?.tooltip;
            if (string.IsNullOrEmpty(text)) { Hide(); return; }
            _owner = element;
            _pointer = panelPosition;
            Show();
        }

        private void OnOver(PointerOverEvent e)
        {
            _pointer = e.position;
            var owner = FindOwner(e.target as VisualElement);
            if (owner == _owner) return;
            Hide();
            if (owner == null) return;
            _owner = owner;
            _pending = _root.schedule.Execute(Show).StartingIn(DelayMs);
        }

        private VisualElement FindOwner(VisualElement element)
        {
            for (var e = element; e != null && e != _root; e = e.parent)
                if (!string.IsNullOrEmpty(e.tooltip)) return e;
            return null;
        }

        private void Show()
        {
            _pending = null;
            if (_owner == null || _owner.panel == null || !_owner.enabledInHierarchy && string.IsNullOrEmpty(_owner.tooltip)) return;
            _box.text = _owner.tooltip;
            _box.style.display = DisplayStyle.Flex;
            _box.BringToFront();
            // Place below-right of the pointer, flipped to stay inside the root.
            _box.schedule.Execute(Place);
            Place();
        }

        private void Place()
        {
            var bounds = _root.layout;
            var size = new Vector2(Mathf.Max(_box.layout.width, 40f), Mathf.Max(_box.layout.height, 20f));
            var local = _root.WorldToLocal(_pointer);
            var x = local.x + 16f;
            var y = local.y + 20f;
            if (x + size.x > bounds.width - 6f) x = local.x - size.x - 10f;
            if (y + size.y > bounds.height - 6f) y = local.y - size.y - 10f;
            _box.style.left = Mathf.Max(6f, x);
            _box.style.top = Mathf.Max(6f, y);
        }

        public void Hide()
        {
            _pending?.Pause();
            _pending = null;
            _owner = null;
            _box.style.display = DisplayStyle.None;
        }
    }
}
