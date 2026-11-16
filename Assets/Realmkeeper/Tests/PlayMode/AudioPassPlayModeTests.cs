using System.Collections;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// 2026-11-15 audio pass: music by mood (Kevin MacLeod, CC BY 4.0), footsteps by ground, blows in a fight and quest and
    /// level cues (Kenney Impact Sounds and Music Jingles, CC0). Nothing here is heard: the tests check what is chosen.
    /// </summary>
    public sealed class AudioPassPlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        [Test]
        public void EveryTrack_IsInTheProject_AndEveryMoodHasMusic()
        {
            foreach (var mood in new[] { MusicMood.Title, MusicMood.Strategic, MusicMood.Country, MusicMood.Settlement, MusicMood.Night, MusicMood.Place, MusicMood.Fight })
            {
                Assert.That(RkMusic.Playlists.ContainsKey(mood) && RkMusic.Playlists[mood].Length > 0, Is.True, mood.ToString());
                foreach (var track in RkMusic.Playlists[mood]) Assert.That(RkMusic.Clip(track), Is.Not.Null, mood + ": " + track);
            }

            // CC BY 4.0 asks for the credit: every track is named in the game's credits, with the licence.
            var music = Realmkeeper.Application.Credits.Assets.Single(a => a.Author.StartsWith("Kevin MacLeod"));
            Assert.That(music.Licence, Does.Contain("Attribution 4.0"));
            foreach (var track in RkMusic.Playlists.Values.SelectMany(t => t).Distinct())
            {
                var title = string.Join(" ", track.Split('-').Select(w => w == "of" || w == "the" ? w : char.ToUpperInvariant(w[0]) + w.Substring(1)));
                title = char.ToUpperInvariant(title[0]) + title.Substring(1);
                Assert.That(music.Work, Does.Contain("\"" + title + "\""), track);
            }
        }

        [Test]
        public void TheMood_FollowsWhatIsShown()
        {
            Assert.That(RkMusic.Choose(true, MusicMood.Fight), Is.EqualTo(MusicMood.Title), "The title screen covers everything.");
            Assert.That(RkMusic.Choose(false, null), Is.EqualTo(MusicMood.Strategic));
            Assert.That(RkMusic.Choose(false, MusicMood.Country), Is.EqualTo(MusicMood.Country));

            Assert.That(RkMusic.ExplorationMoodFor(true, true, false, 1f), Is.EqualTo(MusicMood.Fight), "A fight above all.");
            Assert.That(RkMusic.ExplorationMoodFor(false, true, false, 1f), Is.EqualTo(MusicMood.Place));
            Assert.That(RkMusic.ExplorationMoodFor(false, false, true, 0.8f), Is.EqualTo(MusicMood.Night));
            Assert.That(RkMusic.ExplorationMoodFor(false, false, true, 0.2f), Is.EqualTo(MusicMood.Country));
            Assert.That(RkMusic.ExplorationMoodFor(false, false, false, 0.59f), Is.EqualTo(MusicMood.Settlement));
        }

        [Test]
        public void Footsteps_FollowTheGround()
        {
            Assert.That(GameSounds.SurfaceFor(true, false, 1f), Is.EqualTo("stone"), "Places are stone, even in snow.");
            Assert.That(GameSounds.SurfaceFor(false, true, 0.5f), Is.EqualTo("snow"));
            Assert.That(GameSounds.SurfaceFor(false, true, 0f), Is.EqualTo("grass"));
            Assert.That(GameSounds.SurfaceFor(false, false, 0f), Is.Null, "Settlements keep the earth-and-gravel steps.");

            var volume = RkSettings.SoundVolume;
            try
            {
                RkSettings.SoundVolume = 0f;
                GameSounds.ResetCounters();
                GameSounds.Surface = "grass";
                GameSounds.Play("footstep");
                Assert.That(GameSounds.Last, Is.EqualTo("footstep-grass"));
                GameSounds.Surface = null;
                GameSounds.Play("footstep");
                Assert.That(GameSounds.Last, Is.EqualTo("footstep"));
                GameSounds.Surface = "no-such-ground";
                GameSounds.Play("footstep");
                Assert.That(GameSounds.Last, Is.EqualTo("footstep"), "An unknown ground keeps the default.");
            }
            finally
            {
                GameSounds.Surface = null;
                RkSettings.SoundVolume = volume;
            }
        }

        [Test]
        public void TheMusicVolume_IsASetting()
        {
            var music = RkSettings.MusicVolume;
            try
            {
                RkSettings.MusicVolume = 2f;
                Assert.That(RkSettings.MusicVolume, Is.EqualTo(1f), "Clamped.");
                RkSettings.MusicVolume = 0.25f;
                Assert.That(RkSettings.MusicVolume, Is.EqualTo(0.25f).Within(1e-4f));
            }
            finally
            {
                RkSettings.MusicVolume = music;
            }
        }

        [UnityTest]
        public IEnumerator InATown_TheSettlementMusicPlays_AndTheStepsAreTheTowns()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var town = bootstrap.Session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            for (var i = 0; i < 8; i++) yield return null;

            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            Assert.That(controller.IsExploring, Is.True);
            var expected = controller.Atmosphere?.Weather != null && controller.Atmosphere.Weather.Look.Night >= 0.6f ? MusicMood.Night : MusicMood.Settlement;
            Assert.That(RkMusic.ExplorationMood, Is.EqualTo(expected));
            if (!RkMusic.TitleOpen)
            {
                yield return null;
                Assert.That(RkMusic.Playlists[expected], Does.Contain(RkMusic.Playing), "The player picked a track of the mood.");
            }

            Assert.That(GameSounds.Surface, Is.Null.Or.EqualTo("snow"));
        }
    }
}
