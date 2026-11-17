using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
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
    /// Visual-quality pass (Briefs/Visual-Quality-Pass-Brief.md): evidence, not a pass/fail test. The same town and country
    /// views, at a fixed time and weather, rendered through the exploration camera (its own post-processing) before and after
    /// the pass, with the engine's counters per view. Poses are derived from the layout (the main gate, the plaza, a house),
    /// so they suit today's walled towns. Runs only when RK_CAPTURE_DIR is set; RK_CAPTURE_TAG prefixes the files
    /// (default "vp"). Writes &lt;tag&gt;_&lt;view&gt;.jpg and &lt;tag&gt;_stats.txt.
    /// </summary>
    public sealed class VisualPassCaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const int Width = 1920;
        private const int Height = 1080;

        private string _dir;
        private string _tag;
        private InputSettings.BackgroundBehavior _background;
        private readonly List<string> _stats = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) yield break;
            _tag = Environment.GetEnvironmentVariable("RK_CAPTURE_TAG");
            if (string.IsNullOrEmpty(_tag)) _tag = "vp";
            Directory.CreateDirectory(_dir);
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            File.WriteAllLines(Path.Combine(_dir, _tag + "_stats.txt"), _stats);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();

        private static WeatherSummary Weather(WeatherKind kind, int intensity, int cloud, int wet = 0, int month = 5)
        {
            var date = new WorldDate(412, month, 15);
            return new WeatherSummary
            {
                Kind = kind, Name = WeatherWords.Name(kind), Intensity = intensity, Cloud = cloud, TemperatureC = 15, WindKph = 12, GroundWetness = wet,
                Sunrise = SeasonRules.Sunrise(date), Sunset = SeasonRules.Sunset(date), NoonElevation = SeasonRules.NoonElevation(date),
                Season = SeasonRules.Of(date), SeasonName = SeasonRules.Of(date).ToString(),
            };
        }

        private static IEnumerator Seconds(float s)
        {
            var t = Time.unscaledTime;
            while (Time.unscaledTime - t < s) yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_TownAndCountry()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return Seconds(2f);
            var a = c.Atmosphere;
            Assert.That(a, Is.Not.Null);

            var layout = c.Streamer.Current;
            var root = c.EnvironmentRoot.transform;
            float Ground(float x, float z) => LocalPaths.StandingHeight(layout, x, z);
            Vector3 W(Vector2 p, float up) => root.TransformPoint(new Vector3(p.x, Ground(p.x, p.y) + up, p.y));
            var main = layout.Entrances.FirstOrDefault(e => e.IsMain);
            var gate = main != null ? new Vector2(main.Gate.X, main.Gate.Z) : new Vector2(0f, -layout.HalfExtent + 4f);
            var inward = main != null ? new Vector2(main.InX, main.InZ).normalized : Vector2.up;
            var side = new Vector2(-inward.y, inward.x);
            var centre = Vector2.zero;
            var house = layout.Buildings.Where(b => b.Kind == BuildingKind.House)
                .OrderBy(b => (new Vector2(b.Center.X, b.Center.Z) - (gate + inward * 30f)).sqrMagnitude).First();
            var houseC = new Vector2(house.Center.X, house.Center.Z);
            var door = new Vector2(Mathf.Sin(house.Yaw * Mathf.Deg2Rad), -Mathf.Cos(house.Yaw * Mathf.Deg2Rad));
            var hside = new Vector2(-door.y, door.x);
            var toCentre = (centre - gate).magnitude;
            var street = gate + inward * Mathf.Min(28f, toCentre * 0.45f);
            var plazaEdge = centre - inward * (layout.PlazaRadius + 7f);

            var views = new List<(string name, Vector3 eye, Vector3 look)>
            {
                ("gate_inside", W(gate + inward * 16f - side * 2.5f, 1.7f), W(gate, 3.5f)),
                ("street", W(street + side * 1.5f, 1.7f), W(centre, 2.5f)),
                ("plaza", W(plazaEdge + side * 4f, 4.5f), W(centre, 1f)),
                ("house", W(houseC + door * (house.Depth / 2f + 7f) + hside * 3f, 2.2f), W(houseC, 3.2f)),
                ("approach", W(gate - inward * 40f + side * 6f, 5f), W(gate + inward * 10f, 3f)),
                ("overview", W(gate - inward * 70f, 45f), W(centre, 0f)),
            };

            var times = new (string name, WeatherSummary w, int minute)[]
            {
                ("clear_noon", Weather(WeatherKind.Clear, 0, 10), 12 * 60 + 30),
                ("overcast", Weather(WeatherKind.Overcast, 0, 88), 11 * 60),
                ("rain", Weather(WeatherKind.LightRain, 40, 90, wet: 50), 11 * 60),
                ("dusk", Weather(WeatherKind.PartlyCloudy, 0, 30), 19 * 60 + 15),
            };

            c.CameraRig.enabled = false; // poses are set by hand
            var camera = c.CameraRig.Camera;
            foreach (var (tname, w, minute) in times)
            {
                a.Weather?.Clear();
                a.SetWeather(w);
                a.SetTime(minute);
                yield return Seconds(1.2f);
                a.SetTime(minute);
                foreach (var (vname, eye, look) in views)
                {
                    // Every view in clear noon; the street and plaza in the other lights.
                    if (tname != "clear_noon" && vname != "street" && vname != "plaza") continue;
                    yield return Shoot(camera, eye, look, tname + "_town_" + vname);
                }
            }

            // The country around the town, at eye level and from above.
            c.CameraRig.enabled = true;
            c.GoOutIntoCountry();
            for (var i = 0; i < 1200 && !(c.InOverland && !c.Travelling); i++) yield return null;
            if (!c.InOverland) { _stats.Add("country: not reached"); yield break; }
            yield return Seconds(4f); // vegetation streams in
            a = c.Atmosphere;
            c.CameraRig.enabled = false;
            camera = c.CameraRig.Camera;
            var p = c.Player.transform.position;
            foreach (var (tname, w, minute) in new[] { times[0], times[2] })
            {
                a.Weather?.Clear();
                a.SetWeather(w);
                a.SetTime(minute);
                yield return Seconds(1.2f);
                a.SetTime(minute);
                for (var k = 0; k < 3; k++)
                {
                    if (tname != "clear_noon" && k > 0) continue;
                    var dir = Quaternion.Euler(0f, 30f + 120f * k, 0f) * Vector3.forward;
                    yield return Shoot(camera, p - dir * 3f + Vector3.up * 2.1f, p + dir * 40f + Vector3.up * 1.2f, tname + "_country_eye_" + k);
                }

                if (tname == "clear_noon") yield return Shoot(camera, p + new Vector3(-30f, 60f, -30f), p + new Vector3(60f, 0f, 60f), tname + "_country_above");
            }

            c.CameraRig.enabled = true;
        }

        private IEnumerator Shoot(Camera camera, Vector3 eye, Vector3 look, string name)
        {
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            for (var i = 0; i < 4; i++) yield return null;
            var total = 0f;
            const int frames = 20;
            for (var i = 0; i < frames; i++)
            {
                yield return null;
                total += Time.unscaledDeltaTime;
            }

            var line = string.Format(CultureInfo.InvariantCulture, "{0,-32} frame {1,6:F2} ms", name, total * 1000f / frames);
#if UNITY_EDITOR
            line += string.Format(CultureInfo.InvariantCulture, "  batches {0,5}  set-pass {1,4}  triangles {2,9}  shadow casters {3,5}",
                UnityEditor.UnityStats.batches, UnityEditor.UnityStats.setPassCalls, UnityEditor.UnityStats.triangles, UnityEditor.UnityStats.shadowCasters);
#endif
            _stats.Add(line);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var old = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = old;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, _tag + "_" + name + ".jpg"), tex.EncodeToJPG(90));
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
