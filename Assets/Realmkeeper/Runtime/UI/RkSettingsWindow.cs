using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 19: the settings screen. Only settings that work are shown (see <see cref="RkSettings"/>); each applies
    /// at once and is kept between sessions. What the game does not support yet (audio, key rebinding, colour-blind
    /// palettes, a separate text size) is listed as such, in words, never as a control that does nothing.
    /// </summary>
    public sealed class RkSettingsWindow
    {
        public VisualElement Root { get; }
        public DropdownField DisplayMode { get; }
        public DropdownField Resolution { get; }
        public Toggle VSync { get; }
        public Slider Volume { get; }
        public DropdownField Quality { get; }
        public DropdownField UiScale { get; }
        public Button CloseButton { get; }
        public Label Status { get; }
        public bool IsOpen => Root.parent != null;

        private readonly List<Vector2Int> _resolutions;

        /// <summary>The controls reference, as the game binds them (strategic map, exploration, combat).</summary>
        public static readonly (string group, (string keys, string action)[] rows)[] Controls =
        {
            ("World map", new[]
            {
                ("W A S D / arrows", "Pan the camera"), ("Q / E", "Rotate the camera"), ("Mouse wheel", "Zoom"),
                ("Left click", "Select a settlement"), ("Esc", "Clear the selection"),
            }),
            ("In a settlement or place", new[]
            {
                ("W A S D", "Walk"), ("Shift", "Sprint"), ("Space", "Jump"), ("Mouse", "Look"), ("V", "First or third person"),
                ("E", "Interact with what you look at"), ("H", "Player hub"), ("K", "Party"), ("P", "Pause or resume time"),
                ("T", "Wait one hour"), ("F5", "Save"), ("M", "Return to the world map"), ("N", "Map of the place"),
                ("R", "Search the room"), ("1–9", "Choose a numbered option"), ("Esc", "Close a window, or open the menu (Resume, Save, Settings, Main menu)"),
            }),
            ("Combat", new[]
            {
                ("Left click", "Move to a square, or choose a target"), ("1 / 2 / 3 / 4", "Attacks, actions, spells, features"),
                ("Space", "End your turn"), ("Tab", "Show the grid"), ("Z", "Focus on the active combatant"),
                ("Enter", "Confirm"), ("Esc", "Cancel"),
            }),
        };

        /// <summary>Not available yet: stated, not faked (Phase 25: the stale sound and text-size entries were removed).</summary>
        public static readonly string[] NotYet =
        {
            "Key rebinding: the controls are fixed for now (listed above).",
            "Game controllers: the game is played with keyboard and mouse; controllers are not supported yet.",
            "Colour-blind palettes: not needed for meaning, because states always carry an icon or words as well as colour.",
            "Music volume: the game has no music yet.",
            "Languages: the game is in English only.",
        };

        public DropdownField TextSize { get; private set; }
        public Toggle Contrast { get; private set; }
        public Toggle ReducedMotion { get; private set; }
        public Slider UiVolume { get; private set; }

        public RkSettingsWindow(VisualElement host, System.Action onClose = null)
        {
            Root = new VisualElement { name = "rk-settings" };
            Root.AddToClassList("rk-scrim");
            var window = new VisualElement();
            window.AddToClassList("rk-window");
            window.style.width = 760;
            window.style.maxHeight = new StyleLength(new Length(90, LengthUnit.Percent));
            window.style.paddingLeft = window.style.paddingRight = 24;
            window.style.paddingTop = window.style.paddingBottom = 20;
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("rk-frame");
            window.Add(frame);

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            var titleIcon = RkIcons.Make("settings", "l", "accent");
            titleIcon.style.marginRight = 10;
            head.Add(titleIcon);
            var title = new Label("Settings");
            title.AddToClassList("rk-title");
            title.style.flexGrow = 1;
            head.Add(title);
            CloseButton = new Button(() => { Close(); onClose?.Invoke(); }) { name = "settings-close", text = "Close (Esc)" };
            CloseButton.AddToClassList("rk-btn");
            head.Add(CloseButton);
            window.Add(head);
            var rule = new VisualElement();
            rule.AddToClassList("rk-rule");
            window.Add(rule);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            window.Add(scroll);

            // Display
            scroll.Add(Section("Display"));
            DisplayMode = new DropdownField("Display mode", RkSettings.DisplayModes.Select(m => m.label).ToList(),
                Mathf.Max(0, System.Array.FindIndex(RkSettings.DisplayModes, m => m.mode == Screen.fullScreenMode))) { name = "settings-display-mode" };
            _resolutions = RkSettings.Resolutions();
            var current = new Vector2Int(Screen.width, Screen.height);
            Resolution = new DropdownField("Resolution", _resolutions.Select(r => r.x + " × " + r.y).ToList(), Mathf.Max(0, _resolutions.IndexOf(current))) { name = "settings-resolution" };
            DisplayMode.RegisterValueChangedCallback(_ => ApplyDisplay());
            Resolution.RegisterValueChangedCallback(_ => ApplyDisplay());
            scroll.Add(Row(DisplayMode, "Fullscreen uses the whole display; borderless fills it as a window; windowed can be resized."));
            scroll.Add(Row(Resolution, "The display's own resolutions."));

            // Graphics
            scroll.Add(Section("Graphics"));
            Quality = new DropdownField("Quality", RkSettings.QualityNames.ToList(), RkSettings.Quality) { name = "settings-quality" };
            Quality.RegisterValueChangedCallback(_ => { RkSettings.Quality = Quality.index; VSync.SetValueWithoutNotify(RkSettings.VSync); Say("Graphics quality: " + Quality.value + "."); });
            VSync = new Toggle("Vertical sync") { name = "settings-vsync", value = RkSettings.VSync };
            VSync.RegisterValueChangedCallback(e => { RkSettings.VSync = e.newValue; Say(e.newValue ? "Vertical sync on: frames match the display, without tearing." : "Vertical sync off."); });
            scroll.Add(Row(Quality, "Shadows, anti-aliasing and render scale, from the project's quality levels."));
            scroll.Add(Row(VSync, "Matches the frame rate to the display to prevent tearing."));

            // Sound (Phase 20A assets)
            scroll.Add(Section("Sound"));
            Volume = new Slider("Sound volume", 0f, 1f) { name = "settings-volume", value = RkSettings.SoundVolume, showInputField = false };
            Volume.RegisterValueChangedCallback(e => { RkSettings.SoundVolume = e.newValue; Say("Sound volume: " + Mathf.RoundToInt(e.newValue * 100f) + " %."); });
            scroll.Add(Row(Volume, "Every sound the game makes: footsteps, doors, coins, pages and the interface. 0 is silent."));
            UiVolume = new Slider("Interface sounds", 0f, 1f) { name = "settings-ui-volume", value = RkSettings.UiSoundVolume, showInputField = false };
            UiVolume.RegisterValueChangedCallback(e => { RkSettings.UiSoundVolume = e.newValue; RkUiSound.Play("toggle"); Say(e.newValue <= 0f ? "Interface sounds off." : "Interface sounds: " + Mathf.RoundToInt(e.newValue * 100f) + " % of the sound volume."); });
            scroll.Add(Row(UiVolume, "Clicks, tabs, notifications and menus, under the sound volume. 0 turns them off; nothing is said by sound alone."));

            // Interface
            scroll.Add(Section("Interface"));
            var scaleChoices = RkSettings.Scales.Select(RkSettings.ScaleLabel).ToList();
            UiScale = new DropdownField("Interface scale", scaleChoices, Mathf.Max(0, System.Array.IndexOf(RkSettings.Scales, RkSettings.UiScaleChoice))) { name = "settings-ui-scale" };
            UiScale.RegisterValueChangedCallback(_ =>
            {
                RkSettings.UiScaleChoice = RkSettings.Scales[UiScale.index];
                Say($"Interface scale: {UiScale.value} (in effect: {Mathf.RoundToInt(RkSettings.EffectiveUiScale * 100f)} %).");
            });
            scroll.Add(Row(UiScale, "Enlarges all text and panels together. Auto enlarges them on screens shorter than 1000 pixels."));

            // Accessibility (Phase 25)
            scroll.Add(Section("Accessibility"));
            var sizes = RkSettings.TextSizes.Select(RkSettings.TextSizeLabel).ToList();
            TextSize = new DropdownField("Text size", sizes, Mathf.Max(0, System.Array.IndexOf(RkSettings.TextSizes, RkSettings.TextSize))) { name = "settings-text-size" };
            TextSize.RegisterValueChangedCallback(_ => { RkSettings.TextSize = RkSettings.TextSizes[TextSize.index]; Say("Text size: " + TextSize.value + "."); });
            scroll.Add(Row(TextSize, "Enlarges text only; panels keep their size and wrap. Combine with Interface scale for larger panels too."));
            Contrast = new Toggle("High contrast") { name = "settings-contrast", value = RkSettings.HighContrast };
            Contrast.RegisterValueChangedCallback(e => { RkSettings.HighContrast = e.newValue; Say(e.newValue ? "High contrast on: brighter secondary text, stronger lines, solid panels." : "High contrast off."); });
            scroll.Add(Row(Contrast, "Brighter secondary text, stronger borders and solid panels behind text."));
            ReducedMotion = new Toggle("Reduce motion") { name = "settings-reduced-motion", value = RkSettings.ReducedMotion };
            ReducedMotion.RegisterValueChangedCallback(e => { RkSettings.ReducedMotion = e.newValue; Say(e.newValue ? "Motion reduced: no transitions or sliding notices." : "Motion as designed."); });
            scroll.Add(Row(ReducedMotion, "Turns off interface transitions and sliding notifications."));

            // Controls
            scroll.Add(Section("Controls"));
            foreach (var (group, rows) in Controls)
            {
                var g = new Label(group);
                g.AddToClassList("rk-h2");
                g.style.marginTop = 6;
                scroll.Add(g);
                foreach (var (keys, action) in rows)
                {
                    var line = new VisualElement();
                    line.style.flexDirection = FlexDirection.Row;
                    line.style.minHeight = 22;
                    var k = new Label(keys);
                    k.AddToClassList("rk-sans-bold");
                    k.style.width = 180;
                    k.style.color = new StyleColor(new Color(0.87f, 0.69f, 0.42f));
                    var a = new Label(action);
                    a.AddToClassList("rk-secondary");
                    line.Add(k);
                    line.Add(a);
                    scroll.Add(line);
                }
            }

            scroll.Add(Section("Not available yet"));
            foreach (var text in NotYet)
            {
                var l = new Label("• " + text);
                l.AddToClassList("rk-hint");
                scroll.Add(l);
            }

            Status = new Label { name = "settings-status" };
            Status.AddToClassList("rk-hint");
            Status.style.marginTop = 10;
            window.Add(Status);

            Root.Add(window);
            Root.focusable = true;
            Root.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) { Close(); onClose?.Invoke(); e.StopPropagation(); } });
            host.Add(Root);
            Root.Focus();
        }

        private void ApplyDisplay()
        {
            var mode = RkSettings.DisplayModes[Mathf.Clamp(DisplayMode.index, 0, RkSettings.DisplayModes.Length - 1)];
            var size = _resolutions[Mathf.Clamp(Resolution.index, 0, _resolutions.Count - 1)];
            RkSettings.SetDisplay(size, mode.mode);
            Say(Application.isEditor
                ? $"Display set to {mode.label}, {size.x} × {size.y}. (The editor's Game view keeps its own size; the built game applies it.)"
                : $"Display set to {mode.label}, {size.x} × {size.y}.");
        }

        private void Say(string text) => Status.text = text;

        public void Close() => Root.RemoveFromHierarchy();

        private static Label Section(string text)
        {
            var l = new Label(text);
            l.AddToClassList("rk-h1");
            l.style.marginTop = 16;
            l.style.marginBottom = 4;
            return l;
        }

        private static VisualElement Row(VisualElement control, string hint)
        {
            var row = new VisualElement();
            row.style.marginBottom = 8;
            control.style.marginLeft = 0;
            row.Add(control);
            var h = new Label(hint);
            h.AddToClassList("rk-hint");
            h.style.marginLeft = 2;
            row.Add(h);
            return row;
        }
    }
}
