using System;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 19: the one confirmation dialog. A scrim covers the host (blocking clicks behind it), the dialog states
    /// what will happen, and the buttons say the action ("Delete world", not "OK"). Esc and the cancel button close it
    /// without acting; the confirm action runs at most once.
    /// </summary>
    public sealed class RkModal
    {
        public VisualElement Scrim { get; }
        public Button ConfirmButton { get; }
        public Button CancelButton { get; }
        public bool IsOpen => Scrim.parent != null;

        private Action _onConfirm;

        private RkModal(VisualElement host, string title, string body, string confirm, string cancel, bool danger, Action onConfirm)
        {
            _onConfirm = onConfirm;
            Scrim = new VisualElement { name = "rk-modal" };
            Scrim.AddToClassList("rk-scrim");
            var box = new VisualElement();
            box.AddToClassList("rk-modal");
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("rk-frame");
            box.Add(frame);
            var heading = new Label(title);
            heading.AddToClassList("rk-h1");
            box.Add(heading);
            var text = new Label(body);
            text.AddToClassList("rk-wrap");
            text.style.marginTop = 10;
            text.style.color = new StyleColor(StyleKeyword.Null);
            box.Add(text);
            var buttons = new VisualElement();
            buttons.AddToClassList("rk-modal-buttons");
            CancelButton = new Button(Close) { text = cancel, name = "modal-cancel" };
            CancelButton.AddToClassList("rk-btn");
            ConfirmButton = new Button(Confirm) { text = confirm, name = "modal-confirm" };
            ConfirmButton.AddToClassList("rk-btn");
            ConfirmButton.AddToClassList(danger ? "rk-btn--danger" : "rk-btn--primary");
            buttons.Add(CancelButton);
            buttons.Add(ConfirmButton);
            box.Add(buttons);
            Scrim.Add(box);
            Scrim.focusable = true;
            Scrim.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == UnityEngine.KeyCode.Escape) { Close(); e.StopPropagation(); }
                else if (e.keyCode == UnityEngine.KeyCode.Return || e.keyCode == UnityEngine.KeyCode.KeypadEnter) { Confirm(); e.StopPropagation(); }
            });
            host.Add(Scrim);
            Scrim.Focus();
        }

        /// <summary>Opens a confirmation over the host. The action runs only on confirm, once.</summary>
        public static RkModal Confirm(VisualElement host, string title, string body, string confirm, Action onConfirm,
            bool danger = false, string cancel = "Cancel") =>
            new RkModal(host, title, body, confirm, cancel, danger, onConfirm);

        public void Confirm()
        {
            var action = _onConfirm;
            _onConfirm = null;
            Close();
            action?.Invoke();
        }

        public void Close()
        {
            _onConfirm = null;
            Scrim.RemoveFromHierarchy();
        }
    }
}
