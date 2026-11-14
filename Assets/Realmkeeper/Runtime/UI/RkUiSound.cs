using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 25: restrained interface sounds (Kenney Interface Sounds, CC0; Art/ThirdParty/Kenney/InterfaceSounds).
    /// One short sound per kind of feedback, at the sound volume times the interface-sound volume, so either setting at
    /// 0 silences them. Nothing is said by sound alone: every sound accompanies a visible change. A sound is not repeated
    /// within <see cref="MinGapSeconds"/>, so a burst of changes is heard once.
    /// </summary>
    public static class RkUiSound
    {
        public const string ResourceFolder = "RkUiAudio/";
        public const float MinGapSeconds = 0.06f;

        /// <summary>Feedback kind → file (under Resources/RkUiAudio).</summary>
        public static readonly IReadOnlyDictionary<string, string> Files = new Dictionary<string, string>
        {
            ["click"] = "click_002",
            ["select"] = "select_002",
            ["confirm"] = "confirmation_002",
            ["cancel"] = "back_002",
            ["open"] = "open_002",
            ["close"] = "close_002",
            ["error"] = "error_004",
            ["notify"] = "bong_001",
            ["item"] = "drop_002",
            ["toggle"] = "switch_002",
        };

        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, float> LastAt = new Dictionary<string, float>();
        private static AudioSource _source;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, string> Attached = new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, string>();

        /// <summary>Sounds asked for since the last reset (tests read this; counts even when silent or in batch mode).</summary>
        public static int Requested { get; private set; }

        public static string Last { get; private set; }

        public static void ResetCounters()
        {
            Requested = 0;
            Last = null;
        }

        public static float Volume => RkSettings.SoundVolume * RkSettings.UiSoundVolume;

        public static void Play(string kind)
        {
            if (kind == null || !Files.TryGetValue(kind, out var file)) return;
            var now = Time.realtimeSinceStartup;
            if (LastAt.TryGetValue(kind, out var at) && now - at < MinGapSeconds) return;
            LastAt[kind] = now;
            Requested++;
            Last = kind;
            var volume = Volume;
            if (volume <= 0f || !UnityEngine.Application.isPlaying) return;
            if (!Clips.TryGetValue(file, out var clip) || clip == null) Clips[file] = clip = Resources.Load<AudioClip>(ResourceFolder + file);
            if (clip == null) return;
            if (_source == null)
            {
                var go = new GameObject("Realmkeeper interface sounds");
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }

            _source.PlayOneShot(clip, volume * 0.8f);
        }

        /// <summary>
        /// Gives every button under a root a click (tabs and chips a selection sound), once per root. Buttons added later
        /// are covered too, because the callback sits on the root and reads the clicked element.
        /// </summary>
        public static void Attach(VisualElement root)
        {
            if (root == null || Attached.TryGetValue(root, out _)) return;
            Attached.Add(root, "rk-ui-sound");
            root.RegisterCallback<ClickEvent>(e =>
            {
                if (!(e.target is VisualElement ve)) return;
                var b = ve as Button ?? ve.GetFirstAncestorOfType<Button>();
                if (b == null || !b.enabledInHierarchy) return;
                var tab = b.ClassListContains("rk-tab") || b.ClassListContains("xk-hub-tab") || b.ClassListContains("rk-chip");
                Play(tab ? "select" : "click");
            }, TrickleDown.TrickleDown);
        }
    }
}
