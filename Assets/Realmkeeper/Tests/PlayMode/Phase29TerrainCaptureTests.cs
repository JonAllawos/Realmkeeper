using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
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
    /// Phase 29 captures (capture-only: they run when RK_CAPTURE_DIR is set, or when Temp/rk-capture-phase29 exists, and write
    /// to Documentation/Screenshots/Phase29; otherwise they are ignored). The same views of the same country, drawn once as
    /// the Unity Terrain and once as the old mesh ground, at 10:00: countryside, steep ground, a bridge, the approach to the
    /// town, and the ground wet and under snow. The views are chosen from the terrain itself, so both runs frame the same
    /// place. Nothing here asserts that the two look alike: the pictures are for a person to compare.
    /// </summary>
    public sealed class Phase29TerrainCaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const string Marker = "Temp/rk-capture-phase29";
        private InputSettings.BackgroundBehavior _background;
        private string _dir;
        private readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir) && File.Exists(Marker)) _dir = "Documentation/Screenshots/Phase29";
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
            if (!string.IsNullOrEmpty(_dir) && _log.Count > 0) File.AppendAllLines(Path.Combine(_dir, "p29_terrain_log.txt"), _log);
            OverlandWorld.UseUnityTerrain = true;
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheUnityTerrain_Captured() => Capture(true, "p29_terrain");

        [UnityTest]
        public IEnumerator TheOldMeshGround_Captured() => Capture(false, "p29_mesh");

        private IEnumerator Capture(bool unityTerrain, string prefix)
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Capture-only: set RK_CAPTURE_DIR or create " + Marker + ".");
            _log.Clear();
            OverlandWorld.UseUnityTerrain = unityTerrain;
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            c.ManageCursor = false;
            c.SetInput(new ScriptedExplorationInput());
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            for (var i = 0; i < 6; i++) yield return null;
            runtime.Playback.Pause();
            Assert.That(session.Dispatch(new CreateCharacterCommand(LevelUpDriver.Choices("fighter"))).IsSuccess, Is.True);
            yield return null;
            c.GoOutIntoCountry();
            for (var i = 0; i < 600 && c.Travelling; i++) yield return null;
            Assert.That(c.InOverland, Is.True);
            Assert.That(c.Overland.UnityTerrain != null, Is.EqualTo(unityTerrain), "The ground drawn the way this run asked.");
            var minute = session.GetWorldTime().MinuteOfDay;
            session.Execute(new AdvanceTimeCommand((10 * 60 - minute + 1440) % 1440));
            for (var i = 0; i < 10; i++) yield return null;

            var t = c.OverlandView.Terrain;
            var root = c.Overland.transform;
            Vector3 W(float x, float z, float up = 0f) => root.TransformPoint(new Vector3(x, t.HeightAt(x, z) + up, z));
            // Anchored on the arrival cell's centre, not where the body settled, so both runs frame exactly the same places.
            var settled = root.InverseTransformPoint(c.Player.transform.position);
            var anchorCell = OverlandScale.CellAt(t.Map, settled.x, settled.z);
            var (ax, az) = OverlandScale.CellCentre(anchorCell);
            var p = new Vector3(ax, 0f, az);

            // The places, chosen from the terrain (the same in both runs): the nearest of each kind to the arrival point.
            (int x, int z)? Nearest(Func<OverlandCell, bool> what, float minMetres = 0f)
            {
                (int, int)? best = null;
                var bd = float.MaxValue;
                for (var z = 1; z < t.Map.Depth - 1; z++)
                    for (var x = 1; x < t.Map.Width - 1; x++)
                    {
                        if (!what(t.Cell(x, z))) continue;
                        var (cx, cz) = OverlandScale.CellCentre(new MapCoord(x, z));
                        var d = Vector2.Distance(new Vector2(cx, cz), new Vector2(p.x, p.z));
                        if (d >= minMetres && d < bd) { bd = d; best = (x, z); }
                    }

                return best;
            }

            Vector3 Centre((int x, int z) cell)
            {
                var (cx, cz) = OverlandScale.CellCentre(new MapCoord(cell.x, cell.z));
                return W(cx, cz);
            }

            var steep = Nearest(cell => cell.Biome == Biome.Mountain || cell.Biome == Biome.Rocky, 300f);
            var bridge = Nearest(cell => cell.Road && (cell.Biome == Biome.River || cell.Biome == Biome.Shallows));
            var field = Nearest(cell => cell.Biome == Biome.Meadow || cell.Biome == Biome.Grassland, 500f);

            // Deterministic weather (GroundWeatherFixtures): every view is taken under a stated fixture, shown through the game's
            // own path (the conditions applied as the controller does, then the atmosphere's pass with a matching, overcast sky,
            // nothing falling). Playback is paused, so the live simulation cannot change a view's conditions part way.
            var live = session.GetWeather();
            void Weather(GroundWeatherFixtures.Fixture fx) => GroundWeatherFixtures.Show(c, fx, live);

            Weather(GroundWeatherFixtures.Dry);
            var arrival = W(p.x, p.z);
            yield return Free(c, prefix + "_01_settlement_approach", arrival + new Vector3(-150f, 120f, -150f), arrival + new Vector3(60f, 0f, 60f), 50f,
                "The arrival outside the town: its roads leading out across the country (dry).");
            if (field != null)
            {
                var f = Centre(field.Value);
                yield return Free(c, prefix + "_02_countryside", f + new Vector3(-220f, 150f, -220f), f, 45f, "Representative countryside: grassland and meadow, forest edges (dry).");
                Weather(GroundWeatherFixtures.Muddy);
                yield return Free(c, prefix + "_05_wet_ground", f + new Vector3(-220f, 150f, -220f), f, 45f, "The same view with the ground wet (fixture: muddy, wetness 78).");
                Weather(GroundWeatherFixtures.DeepSnow);
                yield return Free(c, prefix + "_06_snow", f + new Vector3(-220f, 150f, -220f), f, 45f, "The same view under deep snow (fixture: snow cover 70: level ground whitens).");
                Weather(GroundWeatherFixtures.Dry);
            }

            if (steep != null)
            {
                var s = Centre(steep.Value);
                yield return Free(c, prefix + "_03_hills_and_steep_ground", s + new Vector3(-320f, 220f, -320f), s, 50f, "Hills and steep ground: rocky slopes and crags (dry).");
            }

            if (bridge != null)
            {
                var b = Centre(bridge.Value);
                yield return Free(c, prefix + "_04_road_and_bridge", b + new Vector3(-90f, 70f, -90f), b, 45f, "A road crossing water by its bridge or causeway (dry).");
                Weather(GroundWeatherFixtures.Muddy);
                yield return Free(c, prefix + "_08_road_and_bridge_wet", b + new Vector3(-90f, 70f, -90f), b, 45f, "The road and bridge with the ground wet (fixture: muddy).");
                Weather(GroundWeatherFixtures.DeepSnow);
                yield return Free(c, prefix + "_09_road_and_bridge_snow", b + new Vector3(-90f, 70f, -90f), b, 45f, "The road and bridge under deep snow (fixture).");
                Weather(GroundWeatherFixtures.Dry);
            }

            yield return Free(c, prefix + "_07_region_from_above", W(t.SizeX * 0.5f, -350f, 0f) + new Vector3(0f, 1700f, 0f), W(t.SizeX * 0.5f, t.SizeZ * 0.55f), 55f,
                "The whole region from above (5.12 km a side), haze as in play.");
            _log.Add($"{prefix}: {(unityTerrain ? "Unity Terrain" : "mesh ground")}; arrival ({p.x:0},{p.z:0}); steep {steep}; bridge {bridge}; field {field}.");
        }

        /// <summary>A view from anywhere, without the interface; the country streams its detail around what is looked at.</summary>
        private IEnumerator Free(ExplorationController c, string name, Vector3 eye, Vector3 look, float fov, string note)
        {
            var cam = c.CameraRig.Camera;
            var tr = cam.transform;
            var (pos, rot, oldFov, rig, far) = (tr.position, tr.rotation, cam.fieldOfView, c.CameraRig.enabled, cam.farClipPlane);
            c.CameraRig.enabled = false;
            tr.position = eye;
            tr.rotation = Quaternion.LookRotation(look - eye);
            cam.fieldOfView = fov;
            cam.farClipPlane = 9000f;
            var focus = new GameObject("Shot focus").transform;
            focus.position = look;
            c.Overland.Follow = focus;
            c.Overland.Prewarm(look);
            for (var i = 0; i < 4; i++) yield return null;
            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".jpg"), tex.EncodeToJPG(88));
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(focus.gameObject);
            c.Overland.Follow = c.Player.transform;
            tr.position = pos;
            tr.rotation = rot;
            cam.fieldOfView = oldFov;
            cam.farClipPlane = far;
            c.CameraRig.enabled = rig;
            _log.Add($"{name}: {note}");
        }
    }
}
