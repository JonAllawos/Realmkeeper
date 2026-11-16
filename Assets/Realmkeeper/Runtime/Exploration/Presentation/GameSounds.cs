using System.Collections.Generic;
using Realmkeeper.UI;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The game's first sounds (Kenney RPG Audio, CC0): footsteps under the player, a door when a building is entered or
    /// left, coins when a trade goes through, pages when the character sheet opens and closes. Presentation only: nothing
    /// in the world depends on a sound, and with no audio device (batch runs) every call does nothing. Each sound has
    /// numbered takes, played in turn so repeats do not sound identical. Volume is the player's setting
    /// (<see cref="RkSettings.SoundVolume"/>).
    /// </summary>
    public static class GameSounds
    {
        public const string ResourceFolder = "RkAudio/";

        /// <summary>Sound → its takes (file names under <see cref="ResourceFolder"/>).</summary>
        public static readonly IReadOnlyDictionary<string, string[]> Takes = new Dictionary<string, string[]>
        {
            ["footstep"] = new[] { "footstep00", "footstep01", "footstep02", "footstep03", "footstep04", "footstep05", "footstep06", "footstep07", "footstep08", "footstep09" },
            ["door-open"] = new[] { "doorOpen_1", "doorOpen_2" },
            ["door-close"] = new[] { "doorClose_1", "doorClose_2", "doorClose_3", "doorClose_4" },
            ["coins"] = new[] { "handleCoins", "handleCoins2" },
            ["book-open"] = new[] { "bookOpen" },
            ["book-close"] = new[] { "bookClose" },
            ["page"] = new[] { "bookFlip1", "bookFlip2", "bookFlip3" },
            ["cloth"] = new[] { "cloth1", "cloth2", "cloth3", "cloth4" },
            ["draw"] = new[] { "drawKnife1", "drawKnife2", "drawKnife3" },
            // 2026-11-15 audio pass (Kenney Impact Sounds, CC0): footsteps by surface, blows in a fight.
            ["footstep-grass"] = Numbered("footstep_grass_00", 5),
            ["footstep-wood"] = Numbered("footstep_wood_00", 5),
            ["footstep-stone"] = Numbered("footstep_concrete_00", 5),
            ["footstep-snow"] = Numbered("footstep_snow_00", 5),
            ["hit"] = Numbered("impactPunch_medium_00", 5),
            ["hit-heavy"] = Numbered("impactPunch_heavy_00", 5),
            ["block"] = Numbered("impactPlate_light_00", 5),
            // Kenney Music Jingles (CC0): short cues for news worth a moment.
            ["quest-done"] = new[] { "jingles_PIZZI01" },
            ["quest-failed"] = new[] { "jingles_PIZZI10" },
            ["level-up"] = new[] { "jingles_STEEL01" },
        };

        /// <summary>The ground the player walks on: "grass", "wood", "stone", "snow", or null for the default (earth and gravel).</summary>
        public static string Surface { get; set; }

        private static string[] Numbered(string prefix, int count)
        {
            var takes = new string[count];
            for (var i = 0; i < count; i++) takes[i] = prefix + i;
            return takes;
        }

        /// <summary>
        /// The ground under the player: stone inside places (mines, crypts, caves), snow when it falls thickly, grass in the
        /// open country, otherwise the default (the settlement's earth and gravel). Pure, for tests.
        /// </summary>
        public static string SurfaceFor(bool inPlace, bool inOverland, float snow)
        {
            if (inPlace) return "stone";
            if (snow >= 0.35f) return "snow";
            return inOverland ? "grass" : null;
        }

        private static readonly List<(float At, string Sound, float Gain)> Pending = new List<(float, string, float)>();

        /// <summary>Plays a sound after a delay in seconds (so a blow is heard when it lands, not when it is decided).</summary>
        public static void PlayAfter(string sound, float delay, float gain = 1f)
        {
            if (delay <= 0f || !UnityEngine.Application.isPlaying) { Play(sound, gain); return; }
            Pending.Add((Time.unscaledTime + delay, sound, gain));
            Source(); // makes sure the runner exists
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                for (var i = Pending.Count - 1; i >= 0; i--)
                {
                    if (Pending[i].At > Time.unscaledTime) continue;
                    var p = Pending[i];
                    Pending.RemoveAt(i);
                    Play(p.Sound, p.Gain);
                }
            }
        }

        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, int> Next = new Dictionary<string, int>();
        private static AudioSource _source;

        /// <summary>How many sounds were asked for since the last reset (tests read this; it counts even when silent).</summary>
        public static int Requested { get; private set; }

        /// <summary>The last sound asked for, or null.</summary>
        public static string Last { get; private set; }

        public static void ResetCounters()
        {
            Requested = 0;
            Last = null;
        }

        /// <summary>Plays the next take of a sound at the player's volume. Unknown names and missing clips are ignored.</summary>
        public static void Play(string sound, float gain = 1f)
        {
            if (sound == "footstep" && Surface != null && Takes.ContainsKey("footstep-" + Surface)) sound = "footstep-" + Surface;
            if (!Takes.TryGetValue(sound, out var takes) || takes.Length == 0) return;
            Requested++;
            Last = sound;
            var volume = RkSettings.SoundVolume * Mathf.Clamp01(gain);
            if (volume <= 0f || !UnityEngine.Application.isPlaying) return;
            Next.TryGetValue(sound, out var i);
            Next[sound] = (i + 1) % takes.Length;
            var clip = Clip(takes[i % takes.Length]);
            if (clip == null) return;
            var source = Source();
            if (source != null) source.PlayOneShot(clip, volume);
        }

        /// <summary>The clip of one take, or null when it is not in the project.</summary>
        public static AudioClip Clip(string take)
        {
            if (Clips.TryGetValue(take, out var c) && c != null) return c;
            c = Resources.Load<AudioClip>(ResourceFolder + take);
            Clips[take] = c;
            return c;
        }

        private static AudioSource Source()
        {
            if (_source != null) return _source;
            var go = new GameObject("Realmkeeper sounds");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // heard by the player wherever the camera is
            return _source;
        }
    }
}
