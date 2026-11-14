using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 19: the player's display and interface settings, the only ones the game supports. Stored in PlayerPrefs
    /// (per machine, not in a world save) and applied at once:
    /// - interface scale (Auto, 90–130 %), applied to the shared panel so every screen and its text scale together;
    /// - display mode (fullscreen, borderless, windowed) and resolution, through <see cref="Screen.SetResolution(int,int,FullScreenMode)"/>;
    /// - vertical sync and graphics quality level, through <see cref="QualitySettings"/>.
    /// The shared panel is a runtime copy of the RealmkeeperPanelSettings asset, so scaling never edits the asset.
    /// </summary>
    public static class RkSettings
    {
        public const string PanelSettingsResource = "RealmkeeperPanelSettings";
        public const string ThemeResource = "RealmkeeperTheme";

        private const string ScaleKey = "rk.ui.scale";
        private const string VSyncKey = "rk.display.vsync";
        private const string QualityKey = "rk.display.quality";

        /// <summary>Choices for the interface scale; 0 means Auto.</summary>
        public static readonly float[] Scales = { 0f, 0.9f, 1f, 1.15f, 1.3f };

        private static PanelSettings _panel;

        public static event Action Changed;

        /// <summary>
        /// The panel every Realmkeeper document uses: one shared runtime copy, so the strategic HUD, the exploration
        /// HUD and the title screen render in one panel with one scale.
        /// </summary>
        public static PanelSettings Panel
        {
            get
            {
                if (_panel != null) return _panel;
                var asset = Resources.Load<PanelSettings>(PanelSettingsResource);
                if (asset != null)
                {
                    _panel = UnityEngine.Object.Instantiate(asset);
                    _panel.name = asset.name + " (runtime)";
                }
                else
                {
                    _panel = ScriptableObject.CreateInstance<PanelSettings>();
                    _panel.name = "Realmkeeper Runtime Panel";
                    _panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResource);
                    _panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    _panel.referenceResolution = new Vector2Int(1920, 1080);
                    _panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                    _panel.match = 0.5f;
                }

                ApplyScale();
                return _panel;
            }
        }

        /// <summary>The chosen interface scale (0 = Auto).</summary>
        public static float UiScaleChoice
        {
            get => PlayerPrefs.GetFloat(ScaleKey, 0f);
            set
            {
                PlayerPrefs.SetFloat(ScaleKey, Scales.Contains(value) ? value : 0f);
                PlayerPrefs.Save();
                ApplyScale();
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// The scale in effect. Auto enlarges the interface on short screens, where scaling to the 1080p reference
        /// would shrink 15 px body text below 11 px (1366×768 gives 0.71): 1.25 up to 800 px tall, 1.1 up to 1000.
        /// </summary>
        public static float EffectiveUiScale => EffectiveFor(UiScaleChoice, Screen.height);

        public static float EffectiveFor(float choice, int screenHeight)
        {
            if (choice > 0f) return choice;
            return screenHeight <= 800 ? 1.25f : screenHeight <= 1000 ? 1.1f : 1f;
        }

        public static string ScaleLabel(float choice) => choice <= 0f ? "Auto" : Mathf.RoundToInt(choice * 100f) + " %";

        public static void ApplyScale()
        {
            if (_panel != null) _panel.scale = EffectiveUiScale;
        }

        private const string VolumeKey = "rk.audio.volume";

        /// <summary>Phase 20A assets: the volume of the game's sounds, 0 (silent) to 1. Default 0.7.</summary>
        public static float SoundVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.7f));
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        // ----- Phase 25: accessibility and interface sounds ---------------------------------------------------------

        private const string TextSizeKey = "rk.ui.text";
        private const string ContrastKey = "rk.ui.contrast";
        private const string MotionKey = "rk.ui.reducedMotion";
        private const string UiSoundKey = "rk.audio.ui";

        /// <summary>Text size choices: normal, large, largest (multipliers of the type tokens).</summary>
        public static readonly float[] TextSizes = { 1f, 1.15f, 1.3f };

        public static string TextSizeLabel(float size) => size >= 1.3f ? "Largest (130 %)" : size >= 1.15f ? "Large (115 %)" : "Normal";

        /// <summary>Phase 25: the text size, separate from the interface scale (panels keep their size; text grows).</summary>
        public static float TextSize
        {
            get
            {
                var v = PlayerPrefs.GetFloat(TextSizeKey, 1f);
                return TextSizes.Contains(v) ? v : 1f;
            }
            set
            {
                PlayerPrefs.SetFloat(TextSizeKey, TextSizes.Contains(value) ? value : 1f);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Phase 25: high contrast (brighter secondary text, stronger lines, opaque panels).</summary>
        public static bool HighContrast
        {
            get => PlayerPrefs.GetInt(ContrastKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ContrastKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Phase 25: reduced motion (no transitions, toasts appear and leave without fading).</summary>
        public static bool ReducedMotion
        {
            get => PlayerPrefs.GetInt(MotionKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MotionKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Phase 25: the volume of interface sounds (clicks, tabs, notifications), 0 (off) to 1, under the sound volume. Default 0.6.</summary>
        public static float UiSoundVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(UiSoundKey, 0.6f));
            set
            {
                PlayerPrefs.SetFloat(UiSoundKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
                PlayerPrefs.Save();
                QualitySettings.vSyncCount = value ? 1 : 0;
                Changed?.Invoke();
            }
        }

        public static string[] QualityNames => QualitySettings.names;

        public static int Quality
        {
            get => QualitySettings.GetQualityLevel();
            set
            {
                if (value < 0 || value >= QualitySettings.names.Length) return;
                PlayerPrefs.SetInt(QualityKey, value);
                PlayerPrefs.Save();
                QualitySettings.SetQualityLevel(value, true);
                VSync = VSync; // the quality level carries its own vSync; keep the player's choice
            }
        }

        public static readonly (FullScreenMode mode, string label)[] DisplayModes =
        {
            (FullScreenMode.ExclusiveFullScreen, "Fullscreen"),
            (FullScreenMode.FullScreenWindow, "Borderless window"),
            (FullScreenMode.Windowed, "Windowed"),
        };

        /// <summary>The resolutions the display offers (distinct sizes, largest first).</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct()
                .OrderByDescending(r => r.x).ThenByDescending(r => r.y).ToList();
            var current = new Vector2Int(Screen.width, Screen.height);
            if (!list.Contains(current)) list.Insert(0, current);
            return list;
        }

        public static void SetDisplay(Vector2Int size, FullScreenMode mode)
        {
            Screen.SetResolution(size.x, size.y, mode);
            Changed?.Invoke();
        }

        /// <summary>Applies the stored vSync and quality at start-up (the scale is applied with the panel).</summary>
        public static void ApplyStored()
        {
            if (PlayerPrefs.HasKey(QualityKey))
            {
                var q = PlayerPrefs.GetInt(QualityKey);
                if (q >= 0 && q < QualitySettings.names.Length) QualitySettings.SetQualityLevel(q, true);
            }

            if (PlayerPrefs.HasKey(VSyncKey)) QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey) == 1 ? 1 : 0;
            ApplyScale();
        }

        /// <summary>Re-evaluates Auto after the window size changes (called by the documents' geometry events).</summary>
        public static void OnScreenResized()
        {
            var before = _panel != null ? _panel.scale : 0f;
            ApplyScale();
            if (_panel != null && !Mathf.Approximately(before, _panel.scale)) Changed?.Invoke();
        }
    }
}
