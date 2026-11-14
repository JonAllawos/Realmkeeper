using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 23 (ADR-0112): the loading screen of a transition — the destination's name, what is happening, a progress bar,
    /// and a failure message with the way back. It lives on its own document, outside the scenes it covers (the environment
    /// it hides is unloaded and rebuilt beneath it). Deliberately plain: artwork and context come later.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private Label _title;
        private Label _destination;
        private Label _status;
        private VisualElement _fill;

        public bool IsShown => _root != null && _root.style.display == DisplayStyle.Flex;
        public string Destination => _destination?.text;
        public string Status => _status?.text;

        /// <summary>How many times it has been shown (tests and diagnostics).</summary>
        public int Shown { get; private set; }

        public static LoadingScreen Create(Transform parent)
        {
            var go = new GameObject("Loading screen");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<LoadingScreen>();
            s._document = go.AddComponent<UIDocument>();
            s._document.panelSettings = Realmkeeper.UI.RkSettings.Panel;
            s._document.sortingOrder = 100; // above every window and the HUD
            go.SetActive(true);
            s.BuildUi();
            return s;
        }

        private void BuildUi()
        {
            var doc = _document.rootVisualElement;
            _root = new VisualElement { name = "rk-loading" };
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0.07f, 0.06f, 0.05f, 1f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.display = DisplayStyle.None;
            _title = Text("TRAVELLING", 14, new Color(0.72f, 0.54f, 0.29f));
            _title.style.letterSpacing = 4;
            _destination = Text(string.Empty, 34, new Color(0.91f, 0.81f, 0.61f));
            _destination.name = "rk-loading-destination";
            _status = Text(string.Empty, 15, new Color(0.92f, 0.89f, 0.80f));
            _status.name = "rk-loading-status";
            var bar = new VisualElement();
            bar.style.width = 420;
            bar.style.height = 6;
            bar.style.marginTop = 18;
            bar.style.backgroundColor = new Color(0.18f, 0.15f, 0.12f);
            _fill = new VisualElement { name = "rk-loading-fill" };
            _fill.style.height = Length.Percent(100);
            _fill.style.width = Length.Percent(0);
            _fill.style.backgroundColor = new Color(0.72f, 0.54f, 0.29f);
            bar.Add(_fill);
            _root.Add(_title);
            _root.Add(_destination);
            _root.Add(_status);
            _root.Add(bar);
            doc.Add(_root);
        }

        private static Label Text(string text, int size, Color colour)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = colour;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.marginBottom = 6;
            return l;
        }

        public void Show(string destination, string status)
        {
            Shown++;
            _destination.text = destination;
            _status.text = status;
            _title.text = "TRAVELLING";
            _fill.style.width = Length.Percent(5);
            _root.style.display = DisplayStyle.Flex;
        }

        public void Progress(float fraction, string status)
        {
            _fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
            if (status != null) _status.text = status;
        }

        public void Fail(string why)
        {
            _title.text = "COULD NOT TRAVEL";
            _status.text = why + "  You stay where you were.";
        }

        public void Hide() => _root.style.display = DisplayStyle.None;
    }
}
