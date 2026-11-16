using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
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
    /// Phase 28 screenshots from the running game (no mockups): arriving from the strategic map at a town's front gate (with
    /// the interface and the local map), the gate from outside and in, the approach, the watch at the gate and on patrol,
    /// the owner's colours, the road meeting the gate, the approach's climbs, and a village's and a hamlet's ways in.
    /// Runs only when RK_CAPTURE_DIR is set; writes JPEGs and p28_capture_log.txt.
    /// </summary>
    public sealed class Phase28CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private string _dir;
        private InputSettings.BackgroundBehavior _background;
        private readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) yield break;
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
            File.WriteAllLines(Path.Combine(_dir, "p28_capture_log.txt"), _log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static IEnumerator Seconds(float s)
        {
            var t = Time.unscaledTime;
            while (Time.unscaledTime - t < s) yield return null;
        }

        private static float Ground(ExplorationController c, float x, float z) => LocalPaths.StandingHeight(c.Streamer.Current, x, z);

        private static IEnumerator Near(ExplorationController c, float x, float z, float wait = 2.5f)
        {
            c.Player.Teleport(new LocalPose(x, Ground(c, x, z) + 0.3f, z, 0f));
            yield return Seconds(wait);
        }

        [UnityTest]
        public IEnumerator Capture_Phase28_ArrivalAndGates()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            RkSettings.TextSize = 1f;
            RkSettings.HighContrast = false;
            RkSettings.ReducedMotion = true;
            var b = Bootstrap;
            var c = Controller;
            for (var i = 0; i < 8; i++) yield return null;
            yield return CombatDriver.EnterTownAsFighter(b, Runtime, c);
            var s = b.Session;
            var now = s.GetWorldTime().MinuteOfDay;
            c.WaitHour((11 * 60 - now + 24 * 60) % (24 * 60));
            yield return Seconds(1.5f);
            var layout = c.Streamer.Current;
            var plan = layout.Plan;
            var town = s.GetSettlementsInView().First(x => x.SettlementId == layout.SettlementId);
            var life = s.GetStreetLife(layout.SettlementId);
            _log.Add($"Town: {town.Name} ({town.SettlementType}), population {life.Population}, owner {life.OwnerName ?? "independent"}; {layout.Entrances.Count} gates; arrival {(layout.ArrivalIsFallback ? "FALLBACK" : "at the main gate")}.");
            var root = c.EnvironmentRoot.transform;
            Vector3 W(float x, float z, float up) => root.TransformPoint(new Vector3(x, Ground(c, x, z) + up, z));
            var main = layout.Entrances.First(e => e.IsMain);
            var gate = new Vector2(main.Gate.X, main.Gate.Z);
            var inward = new Vector2(main.InX, main.InZ);
            var side = new Vector2(-inward.y, inward.x);

            // 12–13: the arrival from the strategic map, as the player sees it, with the interface and the local map.
            c.Player.Teleport(layout.ArrivalPose);
            c.CameraRig.SetView(layout.ArrivalPose.Yaw, 12f);
            c.CameraRig.Snap();
            yield return Seconds(1.5f);
            yield return Shoot("p28_12_map_to_settlement_arrival", $"Entering {town.Name} from the strategic map: the player stands just inside the front gate, facing into the town (the live camera and interface).");
            var mapBox = c.LocalMap.Root.worldBound;
            _log.Add($"p28_13_local_map_after_arrival: local map at {mapBox}; it draws the player at ({c.LocalMap.PlayerAt.x:0.0},{c.LocalMap.PlayerAt.y:0.0}), the player is at ({c.Player.transform.position.x:0.0},{c.Player.transform.position.z:0.0}). Same image as p28_12 (the map is its bottom right); also saved cropped.");
            yield return ShootCrop("p28_13_local_map_after_arrival", "The local HUD map right after arrival: the player at the gate (the mark at the top of the map's arrow), the gate marks and the shops ahead.", mapBox);

            // 04, 05: the gate from outside and from inside.
            var outside = gate - inward * 22f;
            yield return Near(c, outside.x, outside.y, 2f);
            var eye = gate - inward * 20f + side * 3f;
            yield return Shot("p28_04_gate_from_outside", W(eye.x, eye.y, 1.8f), W(gate.x, gate.y, 4.5f), $"The front gate of {town.Name} from the road outside: the gatehouse in the wall, its portcullis raised, the gate open, lanterns, the owner's banners.");
            var inside = gate + inward * 16f;
            yield return Near(c, inside.x + side.x * 30f, inside.y + side.y * 30f, 1.5f); // the player out of the shot
            yield return Shot("p28_05_gate_from_inside", W(inside.x - side.x * 2.5f, inside.y - side.y * 2.5f, 1.8f), W(gate.x, gate.y, 3.5f), "The same gate from inside: the leaves laid back against the passage, the wall closing on the towers.");

            // 06: the approach from well outside, a little above the road.
            var far = gate - inward * 55f;
            yield return Near(c, far.x + side.x * 15f, far.y + side.y * 15f, 1.5f);
            yield return Shot("p28_06_exterior_approach", W(far.x, far.y, 6f), W(gate.x, gate.y, 3f), "The approach: the main road leading straight to the front gate.");

            // 10: the road meeting the gate, from above and to one side.
            var oblique = gate - inward * 12f + side * 14f;
            yield return Shot("p28_10_road_to_gate", W(oblique.x, oblique.y, 11f), W(gate.x + inward.x * 3f, gate.y + inward.y * 3f, 0f), "The road meeting the gate: it runs on through the passage, draped on the ground; the wall meets the towers.");

            // 03: the town entrance as a traveller arrives, at eye height.
            var at = gate - inward * 9f;
            yield return Shot("p28_03_town_entrance", W(at.x, at.y, 1.7f), W(gate.x + inward.x * 20f, gate.y + inward.y * 20f, 2f), $"{town.Name} ({town.SettlementType}): walking up to its front gate.");

            // 07, 09: the watch at the gate in the owner's colours.
            var post = main.Posts.Length > 0 ? new Vector2(main.Posts[0].X, main.Posts[0].Z) : gate + inward * 7f;
            yield return Near(c, post.x + inward.x * 25f, post.y + inward.y * 25f, 3f);
            var figure = c.Townsfolk.Where(x => x.Activity == TownsfolkActivity.Guarding).Select(x => GameObject.Find("Townsperson " + x.Key)).FirstOrDefault(g => g != null && g.activeInHierarchy);
            _log.Add($"The watch: {c.Townsfolk.Count(x => x.Activity == TownsfolkActivity.Guarding)} at posts, {c.Townsfolk.Count(x => x.Activity == TownsfolkActivity.Patrolling)} on patrol.");
            var lookAt = W(gate.x + inward.x * 7f, gate.y + inward.y * 7f, 1.2f);
            var guardEye = gate + inward * 15f + side * 4f;
            yield return Shot("p28_07_guards_at_entrance", W(guardEye.x, guardEye.y, 2.2f), lookAt, "The watch at the front gate: a pair beside the road just inside, facing out of the gate, clear of the way.");
            if (figure != null)
            {
                var g = figure.transform.position;
                yield return Shot("p28_09_faction_identity", g + root.TransformDirection(new Vector3(inward.x, 0f, inward.y)) * -3.2f + Vector3.up * 1.7f + root.TransformDirection(new Vector3(side.x, 0f, side.y)) * 1.5f, g + Vector3.up * 1.2f,
                    $"Faction identity: a gate guard in the owner's colour ({life.OwnerName ?? "independent"}), the banners behind.");
            }

            // 08: a patrol.
            var patrol = c.Townsfolk.FirstOrDefault(x => x.Activity == TownsfolkActivity.Patrolling);
            if (patrol != null)
            {
                var p0 = TownsfolkPlanner.PositionAt(patrol, 0f, out _, out _);
                var mid = patrol.Route[patrol.Route.Count / 2];
                yield return Near(c, mid.X, mid.Z, 4f);
                var pf = GameObject.Find("Townsperson " + patrol.Key);
                if (pf != null && pf.activeInHierarchy)
                    yield return Shot("p28_08_guards_on_patrol", pf.transform.position + new Vector3(5f, 2.4f, 5f), pf.transform.position + Vector3.up * 1.1f, "The watch on patrol between the gates and the civic buildings.");
                else _log.Add("p28_08_guards_on_patrol: the patrol's figure was not drawn at the moment of capture.");
            }

            // 11, 14: the approach road's steepest stretch, uphill and down (outside the walls, on the main roads).
            float slope = 0f;
            Vector2 best = default, dir = default;
            foreach (var e in layout.Entrances)
            {
                var r = plan.Roads.First(x => x.Key == e.RoadKey);
                for (var i = 0; i + 1 < r.Line.Count; i++)
                {
                    var a = new Vector2(r.Line[i].X, r.Line[i].Z);
                    if (a.magnitude < plan.CoreRadius + 6f || a.magnitude > plan.HalfExtent - 25f) continue;
                    var k = i + 1;
                    while (k + 1 < r.Line.Count && Vector2.Distance(a, new Vector2(r.Line[k].X, r.Line[k].Z)) < 15f) k++;
                    var q = new Vector2(r.Line[k].X, r.Line[k].Z);
                    var len = Vector2.Distance(a, q);
                    if (len < 10f) continue;
                    var gr = Mathf.Abs(Ground(c, q.x, q.y) - Ground(c, a.x, a.y)) / len;
                    if (gr > slope && !SettlementTerrain.InWater(plan, a.x, a.y, 3f)) { slope = gr; best = a; dir = (q - a).normalized * Mathf.Sign(Ground(c, q.x, q.y) - Ground(c, a.x, a.y)); }
                }
            }

            if (slope > 0f)
            {
                yield return Near(c, best.x + dir.y * 20f, best.y - dir.x * 20f, 1f);
                yield return Shot("p28_11_road_uphill", W(best.x - dir.x * 4f, best.y - dir.y * 4f, 1.7f), W(best.x + dir.x * 20f, best.y + dir.y * 20f, 1.2f), $"An approach road climbing ({slope * 100f:0} % over its stretch) toward the walls: the paving follows the ground.");
                var top = best + dir * 18f;
                yield return Shot("p28_14_road_downhill", W(top.x + dir.x * 4f, top.y + dir.y * 4f, 1.7f), W(top.x - dir.x * 20f, top.y - dir.y * 20f, 0.6f), "The same approach looking downhill, away from the town.");
            }
            else _log.Add("p28_11/14: the approach roads here are level.");

            // 02, 01: a village's and a hamlet's ways in, arriving from the strategic map.
            foreach (var (name, type) in new[] { ("p28_02_village_entrance", SettlementType.Village), ("p28_01_hamlet_entrance", SettlementType.Hamlet) })
            {
                var other = s.GetSettlementsInView().FirstOrDefault(x => x.SettlementType == type);
                if (other == null) { _log.Add(name + ": no " + type + " in view."); continue; }
                c.ReturnToWorldMap();
                yield return Seconds(1f);
                b.Selection.Select(other.SettlementId);
                var entered = b.Hud.EnterSelectedSettlement();
                yield return Seconds(1.5f);
                if (!entered.IsSuccess) { _log.Add($"{name}: could not enter {other.Name}: {entered.Message}"); continue; }
                Runtime.Playback.Pause();
                layout = c.Streamer.Current;
                root = c.EnvironmentRoot.transform;
                var m = layout.Entrances.First(e => e.IsMain);
                var g2 = new Vector2(m.Gate.X, m.Gate.Z);
                var in2 = new Vector2(m.InX, m.InZ);
                var s2 = new Vector2(-in2.y, in2.x);
                var e2 = g2 - in2 * 13f + s2 * 2f;
                yield return Near(c, g2.x + in2.x * 30f + s2.x * 20f, g2.y + in2.y * 30f + s2.y * 20f, 1.5f);
                Vector3 W2(float x, float z, float up) => root.TransformPoint(new Vector3(x, Ground(c, x, z) + up, z));
                yield return Shot(name, W2(e2.x, e2.y, 1.8f), W2(g2.x + in2.x * 15f, g2.y + in2.y * 15f, 1.6f),
                    $"{other.Name} ({type}, population {s.GetStreetLife(other.SettlementId).Population}): its way in from the road ({(type == SettlementType.Village ? "a timber gateway and lantern" : "a waymarker and a lantern")}); no wall, {c.Townsfolk.Count(x => x.Activity == TownsfolkActivity.Guarding)} on watch.");
            }

            _log.Add("p28_15_major_city_entrance: " + (s.GetSettlementsInView().Any(x => x.SettlementType >= SettlementType.City) ? "see the town shots" : "no city or fortress among the settlements in view of this test world; the larger gatehouse proportions (city 8 m opening, 5 m towers, +2.5 m; fortress 5.5 m towers, +4 m) are not captured."));
        }

        private IEnumerator Shot(string name, Vector3 eye, Vector3 look, string note, float fov = 55f)
        {
            var go = new GameObject("capture camera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1500f;
            var main = Camera.main;
            if (main != null)
            {
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
                var src = main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                var dst = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() ?? go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (src != null) dst.renderPostProcessing = src.renderPostProcessing;
            }

            go.transform.position = eye;
            go.transform.LookAt(look);
            yield return null;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            Save(name, rt, new RectInt(0, 0, 1920, 1080));
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(go);
            _log.Add($"{name}: 1920x1080. {note}");
        }

        /// <summary>Through the live camera with the interface drawn on top (as the player sees it).</summary>
        private IEnumerator Shoot(string name, string note) => Live(name, note, null);

        private IEnumerator ShootCrop(string name, string note, Rect panelBox) => Live(name, note, panelBox);

        private IEnumerator Live(string name, string note, Rect? crop)
        {
            const int width = 1920, height = 1080;
            yield return null;
            var camera = Controller.CameraRig.Camera;
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
            var cameraTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = cameraTarget;
            yield return null;
            var area = new RectInt(0, 0, width, height);
            if (crop.HasValue)
            {
                // The map's box in panel space (top-left origin) → pixels (bottom-left origin), with a margin.
                var k = RkSettings.EffectiveFor(RkSettings.UiScaleChoice, height);
                var r = crop.Value;
                var x0 = Mathf.Clamp(Mathf.RoundToInt(r.xMin * k) - 40, 0, width - 1);
                var y0 = Mathf.Clamp(height - Mathf.RoundToInt(r.yMax * k) - 40, 0, height - 1);
                var w = Mathf.Clamp(Mathf.RoundToInt(r.width * k) + 80, 16, width - x0);
                var h = Mathf.Clamp(Mathf.RoundToInt(r.height * k) + 80, 16, height - y0);
                area = new RectInt(x0, y0, w, h);
            }

            Save(name, rt, area);
            foreach (var (x, target, clear, scale) in previous)
            {
                x.targetTexture = target;
                x.clearColor = clear;
                x.scale = scale;
            }

            rt.Release();
            Object.Destroy(rt);
            _log.Add($"{name}: {area.width}x{area.height}. {note}");
        }

        private void Save(string name, RenderTexture rt, RectInt area)
        {
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(area.width, area.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(area.x, area.y, area.width, area.height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".jpg"), tex.EncodeToJPG(88));
            Object.Destroy(tex);
        }
    }
}
