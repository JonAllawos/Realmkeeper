using System;
using System.Collections.Generic;
using Realmkeeper.UI;
using UnityEngine.UIElements;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Phase 19: the strategic HUD's shared visual language. Icons from the one family lead the controls and the
    /// inspector's sections, headings use the serif face, tooltips (written since Phase 2 but invisible at run time)
    /// are shown, and the top bar gains Settings and Menu. Behaviour and data are unchanged: every control still runs
    /// the same command it did.
    /// </summary>
    public sealed partial class RealmkeeperHud
    {
        /// <summary>The top bar's Menu button: the bootstrap opens the title screen as a pause menu.</summary>
        public event Action MenuRequested;

        public Button MenuButton { get; private set; }
        public Button SettingsButton { get; private set; }

        /// <summary>The open settings window, or null.</summary>
        public RkSettingsWindow Settings { get; private set; }

        public RkTooltips Tooltips { get; private set; }

        /// <summary>Inspector headings and keys → icon meaning.</summary>
        private static readonly Dictionary<string, string> InspectorIcons = new Dictionary<string, string>
        {
            { "Population", "population" }, { "Workers", "crafting" }, { "Faction", "faction" },
            { "Prosperity", "prosperity" }, { "Stability", "stability" }, { "Food", "food" },
            { "Economy (last day)", "trade" }, { "Production", "production" }, { "Trade routes", "caravan" },
            { "Residents", "people" }, { "Structures", "structures" }, { "Politics", "politics" },
            { "Recent events", "events" }, { "Simulation", "time" },
        };

        private void DressPhase19()
        {
            Tooltips = RkTooltips.Attach(_document.rootVisualElement);

            Root.Q<Label>(className: "rk-wordmark")?.AddToClassList("rk-serif");
            WorldNameLabel.AddToClassList("rk-serif");
            InspectorName.AddToClassList("rk-serif");
            Root.Q<Label>("finsp-name")?.AddToClassList("rk-serif");
            Root.Q<Label>("ninsp-name")?.AddToClassList("rk-serif");

            RkIcons.Lead(AdvanceDayButton, "time", "s");
            AdvanceDayButton.tooltip = "Advance the world by one day.";
            AdvanceWeekButton.tooltip = "Advance the world by seven days.";
            AdvanceMonthButton.tooltip = "Advance the world by thirty days.";
            RkIcons.Lead(SaveButton, "save", "s");
            SaveButton.tooltip = "Save the world to its slot.";
            RkIcons.Lead(LoadButton, "load", "s");
            LoadButton.tooltip = "Reload the world from its last save (progress since then is lost).";
            var politics = Root.Q<Button>("toggle-politics");
            if (politics != null) RkIcons.Lead(politics, "politics", "s");
            SpeedField.tooltip = "How fast days pass while the world plays.";

            var bar = Root.Q<VisualElement>("top-bar");
            SettingsButton = new Button(OpenSettings) { name = "settings", text = "Settings", tooltip = "Display, graphics, interface scale and the controls." };
            SettingsButton.AddToClassList("rk-button");
            RkIcons.Lead(SettingsButton, "settings", "s");
            MenuButton = new Button(() => MenuRequested?.Invoke()) { name = "menu", text = "Menu", tooltip = "Worlds, campaigns, settings and credits (pauses the world)." };
            MenuButton.AddToClassList("rk-button");
            RkIcons.Lead(MenuButton, "book", "s");
            bar.Add(SettingsButton);
            bar.Add(MenuButton);

            Lead("nav-world", "world");
            Lead("nav-settlements", "settlement");
            Lead("nav-factions", "faction");
            Lead("nav-people", "people");
            Lead("nav-atlas", "map");
            Lead("nav-places", "place");

            foreach (var label in Root.Query<Label>(className: "rk-section").ToList())
                if (InspectorIcons.TryGetValue(label.text, out var key)) RkIcons.Lead(label, key, "s", "accent");
            foreach (var label in Root.Query<Label>(className: "rk-key").ToList())
                if (InspectorIcons.TryGetValue(label.text, out var key)) RkIcons.Lead(label, key, "s");
            RefreshPlayIcon();
        }

        private void Lead(string name, string icon)
        {
            var b = Root.Q<Button>(name);
            if (b != null) RkIcons.Lead(b, icon, "s");
        }

        private void RefreshPlayIcon()
        {
            if (PlayPauseButton == null) return;
            var playing = _playback != null && _playback.IsPlaying;
            RkIcons.Lead(PlayPauseButton, playing ? "pause" : "play", "s");
            PlayPauseButton.tooltip = playing ? "Pause the world." : "Let days pass at the chosen speed.";
        }

        /// <summary>Opens the settings window over the map (closing it returns to the map).</summary>
        public void OpenSettings()
        {
            if (Settings != null && Settings.IsOpen) return;
            Settings = new RkSettingsWindow(_document.rootVisualElement, () => SettingsButton?.Focus());
        }
    }
}
