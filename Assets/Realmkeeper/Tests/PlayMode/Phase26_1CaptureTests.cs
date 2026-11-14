using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 26.1 screenshots from the running game (no mockups): the local HUD map in town and country, turned, moving,
    /// with people, shops, companions and a found landmark, in high contrast and at 130 % text, at the eight sizes; and
    /// the post-play fixes (a road on sloping ground, a gate, a numbered conversation). Runs only when RK_CAPTURE_DIR
    /// is set; writes JPEGs and p261_capture_log.txt.
    /// </summary>
    public sealed class Phase26_1CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private string _dir;
        private InputSettings.BackgroundBehavior _background;
        private float _text;
        private bool _contrast;
        private bool _motion;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) yield break;
            Directory.CreateDirectory(_dir);
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _text = RkSettings.TextSize;
            _contrast = RkSettings.HighContrast;
            _motion = RkSettings.ReducedMotion;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            File.WriteAllLines(Path.Combine(_dir, "p261_capture_log.txt"), Log);
            Log.Clear();
            RkSettings.TextSize = _text;
            RkSettings.HighContrast = _contrast;
            RkSettings.ReducedMotion = _motion;
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static IEnumerator Settle(int frames = 6)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        private static IEnumerator Seconds(float s)
        {
            var t = Time.unscaledTime;
            while (Time.unscaledTime - t < s) yield return null;
        }

        private static float TerrainY(float x, float z)
        {
            var best = float.NaN;
            foreach (var h in Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 400f))
                if (h.collider != null && h.collider.name.StartsWith("Terrain collider", StringComparison.Ordinal) && (float.IsNaN(best) || h.point.y > best)) best = h.point.y;
            return best;
        }

        private static void Stand(ExplorationController c, float x, float z, float heading)
        {
            var y = TerrainY(x, z);
            c.Player.Teleport(new LocalPose(x, (float.IsNaN(y) ? c.Player.transform.position.y : y) + 0.2f, z, heading));
            c.CameraRig.SetView(heading, 12f);
            Log.Add($"Stand at ({x:0.0}, {z:0.0}) ground {y:0.00}; player {c.Player.transform.position}, camera {c.CameraRig.Camera.transform.position} enabled {c.CameraRig.Camera.enabled}, environment {c.EnvironmentRoot?.name} active {c.EnvironmentRoot?.activeInHierarchy}.");
        }

        private static readonly (int, int)[] All = { (1920, 1080), (1366, 768), (1280, 720), (1024, 768), (1280, 800), (2560, 1440), (3440, 1440), (700, 1000) };

        [UnityTest]
        public IEnumerator Capture_Phase26_1_LocalMap_AndFixes()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            RkSettings.TextSize = 1f;
            RkSettings.HighContrast = false;
            RkSettings.ReducedMotion = true;
            var b = Bootstrap;
            var rt = Runtime;
            yield return Settle(10);
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(b, rt, c);
            yield return Settle(8);
            var s = b.Session;
            var cam = c.CameraRig.Camera;
            var plan = c.Streamer.Current.Plan;
            var map = c.LocalMap;

            // Companions, so the map shows them (willing townsfolk asked in conversation, as in Phase 26).
            var ids = new List<NpcId>();
            yield return PartyDriver.Willing(rt, c, s, 1, ids);
            yield return Settle(10);

            // 1, 13–19: the town by the square, at every size.
            var nearest = c.Shops.Values.Where(v => v != null).OrderBy(v => v.Building.Center.X * v.Building.Center.X + v.Building.Center.Z * v.Building.Center.Z).First();
            var door = LocalPaths.ToWorld(nearest.Building, 0f, -nearest.Building.Depth / 2f - 6f);
            Stand(c, door.X, door.Z, nearest.Building.Yaw);
            yield return Seconds(1f); // the moving marks refresh every 0.4 s in real time
            Describe(map, "town");
            yield return Sizes("p261_01_settlement", "The exploration HUD in a town: the local map at the bottom right, heading-up.", cam, All);

            // 2: shops and their labels.
            yield return Shoot("p261_02_shops", "Shops at their doors (amber squares) with their names; services (blue squares).", cam, 1920, 1080);

            // 3, 4: people and companions; several kinds together (by the square).
            Stand(c, 0f, -(plan.PlazaRadius + 4f), 0f);
            yield return Seconds(1f); // the moving marks refresh every 0.4 s in real time
            Describe(map, "square");
            yield return Shoot("p261_03_people", "People (teal dots, named once met, otherwise by trade), merchants (amber dots), a companion (ringed green).", cam, 1920, 1080);
            yield return Shoot("p261_04_categories", "Several kinds of mark together, labels decluttered (at most five, never over the player).", cam, 1920, 1080);

            // 5, 6: turned 90 and 180 degrees from the last view, standing still.
            var h0 = c.Player.Heading;

            c.CameraRig.SetView(h0 + 90f, 12f);
            yield return Settle(10);
            yield return Shoot("p261_05_turned_90", "Turned 90 degrees to the right in place: the map turns 90 degrees beneath the player; N moves round the rim.", cam, 1920, 1080);

            c.CameraRig.SetView(h0 + 180f, 12f);
            yield return Settle(10);
            yield return Shoot("p261_06_turned_180", "Turned 180 degrees: what was ahead is now behind.", cam, 1920, 1080);

            // 7: moving (walked forward, scripted).
            var input = new ScriptedExplorationInput();
            c.SetInput(input);
            input.Move = new Vector2(0f, 1f);
            yield return Settle(40);
            yield return Shoot("p261_07_moving", "Walking: the marks slide past beneath the player.", cam, 1920, 1080);
            input.Move = Vector2.zero;

            // 10: the densest spot (most people out within 40 m).
            var people = c.Residents.Values.Where(r => r != null && !r.IsHidden).Select(r => r.transform.position).ToList();
            var dense = people.OrderByDescending(p => people.Count(q => (q - p).sqrMagnitude < 1600f)).FirstOrDefault();
            Stand(c, dense.x, dense.z - 6f, 0f);
            yield return Seconds(1f); // the moving marks refresh every 0.4 s in real time
            Describe(map, "dense");
            yield return Shoot("p261_10_dense", "The busiest street: many people near, labels kept to the most important.", cam, 1920, 1080);

            // 11, 12: high contrast; 130 % text.
            RkSettings.HighContrast = true;
            yield return Settle(4);
            yield return Shoot("p261_11_high_contrast", "High contrast: opaque map frame and labels.", cam, 1920, 1080);
            RkSettings.HighContrast = false;
            RkSettings.TextSize = 1.3f;
            yield return Settle(6);
            yield return Shoot("p261_12_text_130", "Text at 130 %: labels grow and are fewer when they would collide.", cam, 1920, 1080);
            RkSettings.TextSize = 1f;
            yield return Settle(4);

            // Fixes: a road on sloping ground, low and close; a gate; a numbered conversation.
            var slopeAt = Vector3.zero;
            var best = 0f;
            foreach (var road in plan.Roads)
                foreach (var p in road.Line)
                {
                    var g = Mathf.Abs(TerrainY(p.X + 1f, p.Z) - TerrainY(p.X - 1f, p.Z)) / 2f + Mathf.Abs(TerrainY(p.X, p.Z + 1f) - TerrainY(p.X, p.Z - 1f)) / 2f;
                    if (g > best && g < 0.5f) { best = g; slopeAt = new Vector3(p.X, 0f, p.Z); }
                }

            Stand(c, slopeAt.x, slopeAt.z, 90f);
            c.CameraRig.SetView(90f, 4f);
            yield return Settle(20);
            yield return Shoot("p261_20_road_slope", $"A road across sloping ground ({best:0.00} m per m): the paving lies on the ground the player stands on.", cam, 1920, 1080);

            var field = plan.Fields.Where(f => f.Fenced).OrderBy(f => f.Center.X * f.Center.X + f.Center.Z * f.Center.Z).FirstOrDefault();
            if (field != null)
            {
                var outside = Realmkeeper.Domain.Settlements.SettlementLayoutGenerator.Local(field.Center, field.Yaw, 0f, -field.Depth / 2f - 6f);
                Stand(c, outside.X, outside.Z, field.Yaw);
                c.CameraRig.SetView(field.Yaw, 7f);
                yield return Settle(20);
                yield return Shoot("p261_21_gate", $"A fenced {field.Kind.ToString().ToLowerInvariant()} with its gate: an opening between two taller posts, facing the way in.", cam, 1920, 1080);
            }

            var resident = c.Residents.Values.Where(r => r != null && !r.IsHidden).OrderBy(r => r.DisplayName, StringComparer.Ordinal).First();
            resident.enabled = false;
            var at = resident.transform.position;
            c.Player.Teleport(new LocalPose(at.x, at.y + 0.1f, at.z - 1.3f, 0f));
            c.CameraRig.SetView(0f, 5f);
            yield return Settle(6);
            if (c.CurrentTarget == (object)resident)
            {
                c.Interact();
                yield return Settle(4);
                yield return Shoot("p261_22_numbered_topics", "A street conversation: each button is numbered, and that number key chooses it (1 is Talk…).", cam, 1920, 1080);
            }

            // 8, 9: the country, and a found landmark.
            c.CloseWindows();
            c.GoOutIntoCountry();
            for (var i = 0; i < 900 && !(c.InOverland && !c.Travelling); i++) yield return null;
            if (!c.InOverland) yield break;
            yield return Settle(20);
            cam = c.CameraRig.Camera;
            Describe(map, "country");
            yield return Shoot("p261_08_country", "The country: the region's ground and roads, known settlements (blue diamonds), the road ahead.", cam, 1920, 1080);
            var here = c.Player.transform.position;
            var stone = c.LandmarkViews.OrderBy(v => Vector2.Distance(new Vector2(v.Landmark.X, v.Landmark.Z), new Vector2(here.x, here.z))).FirstOrDefault();
            if (stone != null)
            {
                Stand(c, stone.Landmark.X + 3f, stone.Landmark.Z - 3f, 0f);
                yield return Settle(10);
                stone.Interact(s, c.Player.transform);
                c.CloseWindows();
                Stand(c, stone.Landmark.X + 25f, stone.Landmark.Z - 25f, 315f);
                var waited = Time.unscaledTime;
                while (Time.unscaledTime - waited < 2.6f) yield return null; // found landmarks are re-read every 2 s
                Describe(map, "landmark");
                yield return Shoot("p261_09_landmark", $"After finding {stone.Landmark.Name}: its mark (violet diamond) is now on the map.", cam, 1920, 1080);
            }
        }

        private static void Describe(LocalHudMap map, string where)
        {
            var kinds = string.Join(", ", map.Marks.Where(m => m.Drawn).GroupBy(m => m.Data.Kind).Select(g => g.Key + " " + g.Count()));
            var r = map.Root;
            Assert.That(r.panel, Is.Not.Null, "The map is on the live HUD panel (" + where + ").");
            Log.Add($"[{where}] range {map.Radius} m, marks drawn: {kinds}; labels {map.LabelsShown}. Map box {r.worldBound}, display {r.resolvedStyle.display}, visibility {r.resolvedStyle.visibility}, opacity {r.resolvedStyle.opacity}, width {r.resolvedStyle.width}, classes [{string.Join(" ", r.GetClasses())}], parent {r.parent?.name}, panel {r.panel != null}.");
        }

        private IEnumerator Sizes(string name, string note, Camera camera, params (int w, int h)[] sizes)
        {
            foreach (var (w, h) in sizes)
                yield return Shoot(w == 1920 && h == 1080 ? name : $"{name}_{w}x{h}", note, camera, w, h);
        }

        private IEnumerator Shoot(string name, string note, Camera camera, int width, int height)
        {
            yield return null;
            var live = Controller != null && Controller.CameraRig != null ? Controller.CameraRig.Camera : null;
            if (live != null) camera = live; // the rig's camera can be rebuilt after the first entry: always shoot through the live one
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).ToList();
            var panels = docs.Select(d => d.panelSettings).Distinct().ToList();
            var previous = panels.Select(x => (x, x.targetTexture, x.clearColor, x.scale)).ToList();
            foreach (var x in panels)
            {
                x.targetTexture = rt;
                x.clearColor = false;
                x.scale = RkSettings.EffectiveFor(RkSettings.UiScaleChoice, height);
            }

            for (var i = 0; i < 6; i++) yield return null;
            if (camera != null)
            {
                var cameraTarget = camera.targetTexture;
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = cameraTarget;
            }

            yield return null;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".jpg"), tex.EncodeToJPG(88));
            Object.Destroy(tex);
            var classes = string.Join(", ", docs.Where(d => d.rootVisualElement != null).Select(d => d.name + "=" + RkRoot.WidthClass(d.rootVisualElement)).Distinct());
            foreach (var (x, target, clear, scale) in previous)
            {
                x.targetTexture = target;
                x.clearColor = clear;
                x.scale = scale;
            }

            rt.Release();
            Object.Destroy(rt);
            for (var i = 0; i < 2; i++) yield return null;
            Log.Add($"{name}: {width}x{height}, interface scale {RkSettings.EffectiveFor(RkSettings.UiScaleChoice, height):0.00}, width classes [{classes}]. {note}");
        }
    }
}
