using System.Collections.Generic;
using UnityEngine;

namespace Realmkeeper.UI
{
    /// <summary>What the music is for at a moment of play.</summary>
    public enum MusicMood
    {
        None = 0,
        Title = 1,
        Strategic = 2,
        Country = 3,
        Settlement = 4,
        Night = 5,
        Place = 6,
        Fight = 7,
    }

    /// <summary>
    /// 2026-11-15 audio pass: the game's music (Kevin MacLeod, incompetech.com, CC BY 4.0; credited in CREDITS.md and the
    /// Info section). Each mood has a playlist, played in turn; a change of mood crossfades over <see cref="FadeSeconds"/>,
    /// and a track that ends is followed by the next in the same playlist. Presentation only: nothing in the world depends on
    /// it. The title screen sets <see cref="TitleOpen"/>; exploration sets <see cref="ExplorationMood"/> every frame it runs
    /// (null when it is not showing the world); otherwise the strategic map's music plays. Volume is the sound volume times
    /// the music volume (<see cref="RkSettings.MusicVolume"/>), so either at 0 silences it.
    /// </summary>
    public static class RkMusic
    {
        public const string ResourceFolder = "RkMusic/";
        public const float FadeSeconds = 2.5f;

        /// <summary>Mood → its tracks (file names under Resources/RkMusic), in playing order.</summary>
        public static readonly IReadOnlyDictionary<MusicMood, string[]> Playlists = new Dictionary<MusicMood, string[]>
        {
            [MusicMood.Title] = new[] { "lord-of-the-land" },
            [MusicMood.Strategic] = new[] { "teller-of-the-tales", "achaidh-cheide" },
            [MusicMood.Country] = new[] { "celtic-impulse", "achaidh-cheide", "teller-of-the-tales" },
            [MusicMood.Settlement] = new[] { "thatched-villagers", "village-consort", "minstrel-guild", "master-of-the-feast", "pippin-the-hunchback" },
            [MusicMood.Night] = new[] { "midnight-tale" },
            [MusicMood.Place] = new[] { "midnight-tale" },
            [MusicMood.Fight] = new[] { "five-armies", "crossing-the-chasm" },
        };

        /// <summary>Set by the title screen while it is open.</summary>
        public static bool TitleOpen { get; set; }

        /// <summary>Set by exploration every frame it shows the world; null otherwise.</summary>
        public static MusicMood? ExplorationMood { get; set; }

        /// <summary>The mood the music follows now.</summary>
        public static MusicMood Current => Choose(TitleOpen, ExplorationMood);

        /// <summary>The track playing (or fading in), or null. Tests read it; it is chosen even when the volume is 0.</summary>
        public static string Playing { get; private set; }

        /// <summary>The title screen first, then whatever exploration shows, otherwise the strategic map. Pure, for tests.</summary>
        public static MusicMood Choose(bool titleOpen, MusicMood? exploration) =>
            titleOpen ? MusicMood.Title : exploration ?? MusicMood.Strategic;

        /// <summary>
        /// Exploration's mood: a fight above all, then a place (mine, crypt, cave), then night (when <paramref name="night"/>
        /// is at least 0.6, in town or country), then the open country or the settlement. Pure, for tests.
        /// </summary>
        public static MusicMood ExplorationMoodFor(bool inFight, bool inPlace, bool inOverland, float night)
        {
            if (inFight) return MusicMood.Fight;
            if (inPlace) return MusicMood.Place;
            if (night >= 0.6f) return MusicMood.Night;
            return inOverland ? MusicMood.Country : MusicMood.Settlement;
        }

        /// <summary>The clip of one track, or null when it is not in the project.</summary>
        public static AudioClip Clip(string track) => Resources.Load<AudioClip>(ResourceFolder + track);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            TitleOpen = false;
            ExplorationMood = null;
            Playing = null;
            if (Object.FindAnyObjectByType<Player>() != null) return;
            var go = new GameObject("Realmkeeper music");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Player>();
        }

        /// <summary>Two sources: one fading out while the other fades in.</summary>
        private sealed class Player : MonoBehaviour
        {
            private readonly Dictionary<MusicMood, int> _next = new Dictionary<MusicMood, int>();
            private AudioSource _a, _b, _on;
            private MusicMood _mood = MusicMood.None;
            private float _fade = 1f;

            private void Awake()
            {
                _a = Source();
                _b = Source();
            }

            private AudioSource Source()
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
                return s;
            }

            private void Update()
            {
                var mood = Current;
                var ended = _on != null && _on.clip != null && !_on.isPlaying && _fade >= 1f && _on.time <= 0f;
                if (mood != _mood || ended) Begin(mood);

                var volume = RkSettings.SoundVolume * RkSettings.MusicVolume;
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / FadeSeconds);
                foreach (var s in new[] { _a, _b })
                {
                    var share = s == _on ? _fade : 1f - _fade;
                    s.volume = volume * share;
                    if (s != _on && _fade >= 1f && s.isPlaying) s.Stop();
                }
            }

            private void Begin(MusicMood mood)
            {
                _mood = mood;
                if (!Playlists.TryGetValue(mood, out var list) || list.Length == 0) { Playing = null; _on = null; _fade = 0f; return; }
                _next.TryGetValue(mood, out var i);
                _next[mood] = (i + 1) % list.Length;
                Playing = list[i % list.Length];
                var clip = Clip(Playing);
                var into = _on == _a ? _b : _a;
                into.Stop();
                into.clip = clip;
                into.volume = 0f;
                if (clip != null) into.Play();
                _on = into;
                _fade = 0f;
            }
        }
    }
}
