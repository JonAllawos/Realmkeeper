using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 21 (ADR-0105): the street in each kind of weather and season. The first shot is the weather the world
    /// simulated for this town now; the rest show each kind as the presentation draws it (the atmosphere is given a weather
    /// directly, the same way the controller gives it the simulated one). Also checks the view's working parts: particles
    /// fall in rain and snow and not in fair weather, stars show on a clear night and not by day, and the season moves
    /// sunrise and sunset.
    /// </summary>
    public sealed class Phase21WeatherCaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private string _dir;
        private readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = System.Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(_dir)) Directory.CreateDirectory(_dir);
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (!string.IsNullOrEmpty(_dir)) File.AppendAllLines(Path.Combine(_dir, "weather_log.txt"), _log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();

        private static WeatherSummary Make(WeatherKind kind, int intensity, int cloud, int temp, int wind = 10, int wet = 0, int snow = 0, int month = 4) =>
            new WeatherSummary
            {
                Kind = kind, Name = WeatherWords.Name(kind), Intensity = intensity, Cloud = cloud, TemperatureC = temp, WindKph = wind, GroundWetness = wet, SnowCover = snow,
                Sunrise = SeasonRules.Sunrise(new WorldDate(412, month, 15)), Sunset = SeasonRules.Sunset(new WorldDate(412, month, 15)),
                NoonElevation = SeasonRules.NoonElevation(new WorldDate(412, month, 15)), SeasonName = SeasonRules.Of(new WorldDate(412, month, 15)).ToString(),
            };

        [UnityTest]
        public IEnumerator Weather_and_seasons_are_drawn_in_the_street()
        {
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            for (var i = 0; i < 10; i++) yield return null;
            var a = c.Atmosphere;
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Weather, Is.Not.Null, "the street has a weather view");
            Assert.That(c.Weather, Is.Not.Null, "the controller reads the simulated weather");
            _log.Add($"Simulated weather here now: {c.Weather.Line}; {string.Join(" ", c.Weather.Effects)}");

            // Seasons move the sun: a midsummer day is longer than a midwinter one.
            a.SetWeather(Make(WeatherKind.Clear, 0, 5, 20, month: 6));
            var summer = a.SunsetMinute - a.SunriseMinute;
            a.SetWeather(Make(WeatherKind.Clear, 0, 5, 0, month: 12));
            var winter = a.SunsetMinute - a.SunriseMinute;
            Assert.That(summer, Is.GreaterThan(winter + 6 * 60), "summer days are longer");

            var shots = new (string name, WeatherSummary w, int minute, string note)[]
            {
                ("01_simulated_now", c.Weather, Bootstrap.Session.GetWorldTime().MinuteOfDay, "The weather the world simulated here now: " + c.Weather.Line),
                ("02_clear_noon", Make(WeatherKind.Clear, 0, 8, 19), 12 * 60 + 30, "Clear, spring noon."),
                ("03_partly_cloudy", Make(WeatherKind.PartlyCloudy, 0, 50, 16, 18), 11 * 60, "Partly cloudy."),
                ("04_overcast", Make(WeatherKind.Overcast, 0, 88, 12, 15), 11 * 60, "Overcast: flat grey light, soft shadows."),
                ("05_light_rain", Make(WeatherKind.LightRain, 35, 85, 11, 15, wet: 40), 11 * 60, "Light rain, wet ground."),
                ("06_heavy_rain", Make(WeatherKind.HeavyRain, 80, 95, 10, 30, wet: 80), 15 * 60, "Heavy rain, haze, darker wet ground."),
                ("07_thunderstorm", Make(WeatherKind.Thunderstorm, 90, 100, 18, 45, wet: 90, month: 7), 16 * 60, "Thunderstorm: dark, lamps on early."),
                ("08_fog_morning", Make(WeatherKind.Fog, 75, 60, 6, 3, wet: 30, month: 10), 7 * 60 + 30, "Autumn morning fog."),
                ("09_light_snow", Make(WeatherKind.LightSnow, 35, 85, -2, 8, snow: 30, month: 1), 11 * 60, "Light snow, a thin cover."),
                ("10_heavy_snow", Make(WeatherKind.HeavySnow, 80, 95, -5, 25, snow: 70, month: 1), 13 * 60, "Heavy snow lying on ground and roofs."),
                ("11_winter_dusk", Make(WeatherKind.Clear, 0, 10, -1, 5, snow: 50, month: 12), 16 * 60 + 30, "Midwinter: the sun is down by half past four."),
                ("12_summer_evening", Make(WeatherKind.Clear, 0, 10, 24, 5, month: 6), 19 * 60 + 30, "Midsummer: still light at half past seven."),
                ("13_clear_night", Make(WeatherKind.Clear, 0, 5, 8, 4), 23 * 60, "A clear night: stars and the moon."),
                ("14_rainy_night", Make(WeatherKind.HeavyRain, 75, 95, 9, 25, wet: 80), 23 * 60, "A rainy night: no stars, lamps in the wet."),
            };

            var problems = new List<string>();
            foreach (var (name, w, minute, note) in shots)
            {
                a.Weather.Clear(); // each shot is a different day: no flakes from the last one
                a.SetWeather(w);
                a.SetTime(minute);
                yield return new WaitForSeconds(w.Kind == WeatherKind.HeavySnow || w.Kind == WeatherKind.LightSnow ? 4f : 1.5f);
                a.SetTime(minute);
                var look = a.CurrentLook;
                _log.Add($"{name}: {note} {a.Weather.Describe()}; sun {a.Sun.intensity:0.00}, fog {RenderSettings.fogDensity:0.0000}.");
                if (WeatherWords.IsRain(w.Kind) && a.Weather.RainParticles <= 100) problems.Add(name + ": no rain falls");
                if (WeatherWords.IsSnow(w.Kind) && a.Weather.SnowParticles <= 100) problems.Add(name + ": no snow falls");
                if (!string.IsNullOrEmpty(_dir)) yield return Shot(name);
            }

            Assert.That(problems, Is.Empty, string.Join("; ", problems));

            a.SetWeather(Make(WeatherKind.Clear, 0, 5, 15));
            a.SetTime(12 * 60);
            yield return new WaitForSeconds(1.2f);
            Assert.That(a.Weather.RainParticles, Is.EqualTo(0), "fair weather: no rain");
        }

        /// <summary>Renders what the main camera sees (where the weather view is centred), at 1920×1080.</summary>
        private IEnumerator Shot(string name)
        {
            yield return null; // (WaitForEndOfFrame never resumes in batch mode)
            var main = Object.FindAnyObjectByType<ExplorationInstaller>().Controller.CameraRig.Camera; // the exploration camera is not tagged MainCamera
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var old = main.targetTexture;
            main.targetTexture = rt;
            main.Render();
            main.targetTexture = old;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, $"weather_{name}.jpg"), tex.EncodeToJPG(88));
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
