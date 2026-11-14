using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 23 (ADR-0110, ADR-0111, ADR-0112) in the shipped scene, through the real controls: out of a town into the
    /// country; walking it (the clock follows the travel rules); a settlement approached and entered and left; a place found
    /// by walking up to it, entered (its own environment) and left back into the country; the road on into the next region;
    /// the gateway into the ancient realm and back; the region map; saving and loading in the country. Measures each
    /// transition, the frame time while walking, and memory and object counts over repeated transitions. With RK_CAPTURE_DIR
    /// set, writes the Phase 23 screenshots.
    /// </summary>
    public sealed class Phase23PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private string _dir;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
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
            if (!string.IsNullOrEmpty(_dir)) File.AppendAllLines(Path.Combine(_dir, "p23_log.txt"), Log);
            Log.Clear();
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static IEnumerator Settle(int frames = 4)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        private static IEnumerator WaitTravel(ExplorationController c)
        {
            for (var i = 0; i < 600 && c.Travelling; i++) yield return null;
            Assert.That(c.Travelling, Is.False, "The transition finished.");
            yield return Settle(4);
        }

        private static void Place(ExplorationController c, float x, float z, float yaw = 0f)
        {
            var t = c.OverlandView.Terrain;
            c.Player.Teleport(new LocalPose(x, t.HeightAt(x, z) + 0.2f, z, yaw));
            Assert.That(c.RecordPose().IsSuccess, Is.True, "Standing on walkable ground.");
        }

        /// <summary>Stands the player beside a site (a few metres short, facing it) so its interaction is the target.</summary>
        private static IEnumerator Approach(ExplorationController c, OverlandSite site)
        {
            var t = c.OverlandView.Terrain;
            var dir = new Vector2(1f, 0.4f).normalized;
            for (var i = 0; i < 16; i++)
            {
                var a = i * 22.5f * Mathf.Deg2Rad;
                dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (t.Walkable(site.X + dir.x * 4f, site.Z + dir.y * 4f)) break;
            }

            var yaw = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg;
            Place(c, site.X + dir.x * 4f, site.Z + dir.y * 4f, yaw);
            c.CameraRig.SetView(yaw, 8f);
            c.CameraRig.Snap();
            yield return Settle(6);
            var step = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - step < 0.7f) yield return null; // a walking step: the nearby town is shown in its own buildings
        }

        /// <summary>Waits (game time) for the morning when the test's teleports have run the clock into the dark, so a capture shows the place.</summary>
        private static IEnumerator Daylight(WorldSession s)
        {
            var minute = s.GetWorldTime().MinuteOfDay;
            if (minute < 8 * 60 || minute > 17 * 60) s.Execute(new AdvanceTimeCommand((10 * 60 - minute + 1440) % 1440));
            yield return Settle(10);
        }

        private static OverlandSiteView View(ExplorationController c, OverlandSiteKind kind, string id = null) =>
            c.OverlandSites.First(v => v.Site.Kind == kind && (id == null || v.Site.Id == id));

        private static int Objects() => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        [UnityTest]
        public IEnumerator TheCountry_IsWalked_EnteredAndLeft_ThroughEveryKindOfTransition()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            c.ManageCursor = false;
            var input = new ScriptedExplorationInput();
            c.SetInput(input);
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return Settle(6);
            runtime.Playback.Pause();
            Assert.That(session.Dispatch(new CreateCharacterCommand(LevelUpDriver.Choices("fighter"))).IsSuccess, Is.True);
            yield return Settle();

            // ----- Out into the country -----
            Assert.That(c.IsInTownForCountry, Is.True);
            c.GoOutIntoCountry();
            Assert.That(c.Travelling, Is.True, "The loading screen is up.");
            yield return LoadingShot("p23_01_loading_screen", "The loading screen of a transition: destination, what is happening, progress.");
            yield return WaitTravel(c);
            Assert.That(c.InOverland, Is.True);
            Assert.That(session.GetPlayer().IsOverland, Is.True);
            Log.Add($"Out into the country: {c.LastTransitionMs:0} ms; {c.Overland.DetailChunks} detailed chunks ready, {c.Overland.BarrierColliders} barrier colliders, {c.Overland.RoadKm:0.0} km of road drawn; {c.KitSilhouettes} settlement shown in its kit buildings ({c.LastSilhouetteMs:0} ms).");
            Assert.That(c.KitSilhouettes, Is.GreaterThanOrEqualTo(1), "The town beside the arrival is shown in its own buildings.");
            var view = c.OverlandView;
            Assert.That(view.Terrain.Walkable(c.Player.transform.position.x, c.Player.transform.position.z), Is.True);
            Assert.That(c.Player.transform.position.y, Is.EqualTo(view.Terrain.HeightAt(c.Player.transform.position.x, c.Player.transform.position.z)).Within(2.5f), "On the ground, not under or over it.");

            // ----- The country's scale and look -----
            var p0 = c.Player.transform.position;
            yield return FreeShot(c, "p23_02_region_from_above", new Vector3(view.Terrain.SizeX * 0.5f, 1700f, -350f), new Vector3(view.Terrain.SizeX * 0.5f, 0f, view.Terrain.SizeZ * 0.55f), 55f, false,
                "The whole region from above (5.12 km a side): ground by biome, lake and river, forests, mountains, roads (haze off for the shot).");
            yield return FreeShot(c, "p23_03_roads_and_settlement", p0 + new Vector3(-260f, 260f, -260f), p0 + new Vector3(60f, 0f, 60f), 50f, true,
                "A settlement's silhouette from the country, with its roads leading out.");
            input.Move = new Vector2(0f, 1f);
            input.Sprint = true;
            var start = session.GetWorldTime();
            var frames = new List<float>();
            yield return null; // the first frame after the shots compiles the instanced shaders in the editor: not counted
            var walkStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - walkStart < 4f) { frames.Add(Time.unscaledDeltaTime * 1000f); yield return null; }
            input.Move = Vector2.zero;
            input.Sprint = false;
            yield return Settle(30);
            var walked = Vector3.Distance(new Vector3(p0.x, 0, p0.z), new Vector3(c.Player.transform.position.x, 0, c.Player.transform.position.z));
            frames.Sort();
            Log.Add($"Walking 4 s ({frames.Count} frames): {walked:0.0} m; frame time median {frames[frames.Count / 2]:0.0} ms, 95th {frames[(int)(frames.Count * 0.95f)]:0.0} ms, worst {frames[frames.Count - 1]:0.0} ms; {c.Overland.TreesDrawn} trees and rocks drawn, {c.Overland.DetailChunks} detailed chunks, last chunk built in {c.Overland.LastChunkBuildMs:0.0} ms; clock {start.Clock} → {session.GetWorldTime().Clock}, {c.OwedMinutes:0.00} min owed.");
            Assert.That(walked, Is.GreaterThan(5f), "The player walked.");
            yield return Shot(c, "p23_04_player_in_the_country", "The player walking the country (third person): textured ground, grass, trees, the compass and the location line.");
            Log.Add($"Ground cover: {c.Overland.GrassDrawn} grass, fern and flower instances drawn around the player; {c.Overland.SolidColliders} trunk and rock colliders near them.");
            Assert.That(c.Overland.GrassDrawn, Is.GreaterThan(0), "Ground cover grows around the player.");

            // ----- A tree trunk stops the player (follow-up: the forest is solid where it can be touched) -----
            var trunk = c.Overland.NearestTrunk(c.Player.transform.position);
            Assert.That(trunk.HasValue, Is.True, "A tree near enough to walk into.");
            var tp = trunk.Value;
            var away = Vector3.zero;
            for (var i = 0; i < 16; i++)
            {
                var a = i * 22.5f * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var probe = tp + d * 6f;
                if (view.Terrain.Walkable(probe.x, probe.z) && view.Terrain.Walkable(tp.x + d.x * 3f, tp.z + d.z * 3f)) { away = d; break; }
            }

            Assert.That(away, Is.Not.EqualTo(Vector3.zero), "Open ground beside the tree.");
            var faceYaw = Mathf.Atan2(-away.x, -away.z) * Mathf.Rad2Deg;
            Place(c, tp.x + away.x * 6f, tp.z + away.z * 6f, faceYaw);
            c.CameraRig.SetView(faceYaw, 8f);
            c.CameraRig.Snap();
            var waitColliders = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - waitColliders < 0.5f) yield return null; // a streaming tick places the trunks' colliders
            var from = c.Player.transform.position;
            input.Move = new Vector2(0f, 1f);
            var push = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - push < 3f) yield return null;
            input.Move = Vector2.zero;
            yield return Settle(10);
            var progress = Vector3.Dot(c.Player.transform.position - from, -away);
            var sideways = Vector3.Distance(new Vector3(c.Player.transform.position.x, 0, c.Player.transform.position.z), new Vector3(tp.x, 0, tp.z));
            Log.Add($"Walking into a trunk from 6 m for 3 s: {progress:0.0} m forward, {sideways:0.0} m from the trunk's centre at the end; {c.Overland.SolidColliders} colliders near.");
            Assert.That(progress, Is.LessThan(5.9f), "The trunk stopped the player (or turned them aside): they did not walk through it.");
            Assert.That(sideways, Is.GreaterThan(0.3f), "Not standing inside the trunk.");
            yield return Shot(c, "p23_12_grass_and_trees", "Close to the ground: grass and ferns from the owned nature kit, the textured ground, a solid trunk ahead.");

            // ----- The region map -----
            c.ToggleRegionMap();
            yield return Settle(3);
            Assert.That(c.RegionMapShown, Is.True);
            yield return Shot(c, "p23_05_region_map", "The region map (N): the ground by biome, roads, water, known sites and the player.");
            c.ToggleRegionMap();

            // ----- A settlement approached, entered, left -----
            var other = view.Sites.Where(s => s.Kind == OverlandSiteKind.Settlement && s.Id != town.SettlementId.ToString()).OrderBy(s => Vector2.Distance(new Vector2(s.X, s.Z), new Vector2(p0.x, p0.z))).First();
            yield return Approach(c, other);
            yield return Daylight(session);
            yield return FreeShot(c, "p23_06_settlement_approached", c.Player.transform.position + new Vector3(0, 9f, 0) - c.CameraRig.transform.forward * 18f, new Vector3(other.X, c.Player.transform.position.y + 4f, other.Z), 60f, true,
                $"{other.Name} approached from the country: its buildings at their true places, and the banner at the way in.");
            Assert.That(c.CurrentTarget, Is.InstanceOf<OverlandSiteView>(), "The way in is the target.");
            var before = Objects();
            c.Interact();
            yield return WaitTravel(c);
            Assert.That(session.GetPlayer().IsInSettlement, Is.True, c.LastTransitionMessage);
            Assert.That(session.GetPlayer().SettlementId.ToString(), Is.EqualTo(other.Id));
            Log.Add($"Into {other.Name}: {c.LastTransitionMs:0} ms.");
            yield return Shot(c, "p23_07_settlement_entered", $"Inside {other.Name}, loaded as its own environment from the country.");
            c.GoOutIntoCountry();
            yield return WaitTravel(c);
            Assert.That(c.InOverland, Is.True);
            Log.Add($"Back out: {c.LastTransitionMs:0} ms; objects {before} before entering, {Objects()} after coming back.");

            // ----- A place found by walking, entered, left -----
            var place = c.OverlandView.Sites.Where(s => s.Kind == OverlandSiteKind.Place).OrderBy(s => Vector2.Distance(new Vector2(s.X, s.Z), new Vector2(other.X, other.Z))).First();
            yield return Approach(c, place);
            yield return Settle(4);
            var found = c.OverlandView.Sites.First(s => s.Kind == OverlandSiteKind.Place && s.Id == place.Id);
            Assert.That(found.Known, Is.True, "Walking up to it found it.");
            yield return Approach(c, found);
            c.Interact();
            yield return WaitTravel(c);
            Assert.That(session.GetPlayer().LocationKind, Is.EqualTo(CharacterLocationKind.Place), c.LastTransitionMessage);
            Log.Add($"Into {found.Name}: {c.LastTransitionMs:0} ms.");
            yield return Shot(c, "p23_08_place_interior", $"{found.Name}: a separately loaded environment entered from the country.");
            Assert.That(c.LeavePlace().IsSuccess, Is.True);
            yield return Settle(8);
            Assert.That(session.GetPlayer().IsOverland, Is.True, "Left back into the country.");

            // ----- Saving and loading in the country -----
            var pose = session.GetPlayer().Pose;
            Assert.That(session.Execute(new SaveWorldCommand("p23")).IsSuccess, Is.True);
            Assert.That(session.Execute(new LoadWorldCommand("p23")).IsSuccess, Is.True);
            yield return Settle(10);
            Assert.That(c.InOverland, Is.True, "Loaded into the country.");
            Assert.That(Vector3.Distance(new Vector3(pose.X, 0, pose.Z), new Vector3(c.Player.transform.position.x, 0, c.Player.transform.position.z)), Is.LessThan(1f));

            // ----- The road on into the next region, and back, twice: nothing accumulates -----
            var home = session.GetPlayer().RegionId;
            long memoryFirst = 0;
            var objectsFirst = 0;
            for (var round = 0; round < 2; round++)
            {
                var exit = c.OverlandView.Sites.First(s => s.Kind == OverlandSiteKind.RegionExit && s.Usable);
                yield return Approach(c, exit);
                c.Interact();
                yield return WaitTravel(c);
                Assert.That(session.GetPlayer().RegionId.ToString(), Is.EqualTo(exit.Id), c.LastTransitionMessage);
                Log.Add($"Round {round + 1}: into {c.OverlandView.RegionName} in {c.LastTransitionMs:0} ms.");
                if (round == 0) yield return Daylight(session);
                if (round == 0) yield return Shot(c, "p23_09_next_region", $"Arrived in {c.OverlandView.RegionName} by the road on: a region transition.");
                var back = View(c, OverlandSiteKind.RegionExit, home.ToString()).Site;
                yield return Approach(c, back);
                c.Interact();
                yield return WaitTravel(c);
                Assert.That(session.GetPlayer().RegionId, Is.EqualTo(home));
                yield return Settle(20);
                System.GC.Collect();
                Resources.UnloadUnusedAssets();
                yield return Settle(10);
                var memory = Profiler.GetTotalAllocatedMemoryLong();
                var objects = Objects();
                Log.Add($"Round {round + 1} back home: {SceneManager.sceneCount} scenes loaded, {objects} objects, {memory / 1048576} MB allocated.");
                if (round == 0) { memoryFirst = memory; objectsFirst = objects; }
                else
                {
                    Assert.That(objects, Is.LessThanOrEqualTo(objectsFirst + 40), "No objects pile up across transitions.");
                    Assert.That(memory, Is.LessThan(memoryFirst + 96L * 1048576), "No uncontrolled memory growth across transitions.");
                }
            }

            Assert.That(SceneManager.sceneCount, Is.LessThanOrEqualTo(2), "One active environment, never two.");

            // ----- The gateway into the ancient realm (opened through the campaign's hook) and back -----
            var gate = c.OverlandView.Sites.First(s => s.Kind == OverlandSiteKind.Gateway);
            Assert.That(session.Dispatch(new SetGatewayStateCommand(gate.Id, GatewayState.Open)).IsSuccess, Is.True);
            yield return Settle(4);
            gate = c.OverlandView.Sites.First(s => s.Kind == OverlandSiteKind.Gateway);
            yield return Approach(c, gate);
            yield return Daylight(session);
            yield return FreeShot(c, "p23_10_gateway_open", new Vector3(gate.X - 20f, c.OverlandView.Terrain.HeightAt(gate.X, gate.Z) + 12f, gate.Z - 20f), new Vector3(gate.X, c.OverlandView.Terrain.HeightAt(gate.X, gate.Z) + 2f, gate.Z), 55f, true,"The prototype gateway, opened through the campaign's command: standing stones around a lit ring.");
            yield return Approach(c, gate); // the free shot moved the camera: stand at the gateway again so it is the target
            c.Interact();
            yield return WaitTravel(c);
            Assert.That(session.GetPlayer().RealmId, Is.EqualTo(RealmIds.Ancient), c.LastTransitionMessage);
            Log.Add($"Into the ancient realm: {c.LastTransitionMs:0} ms.");
            yield return Daylight(session);
            yield return Shot(c, "p23_11_ancient_realm", "The ancient realm's prototype region: its own geography and provisional light; no inhabitants (undecided).");
            var far = View(c, OverlandSiteKind.Gateway).Site;
            yield return Approach(c, far);
            c.Interact();
            yield return WaitTravel(c);
            Assert.That(RealmIds.IsSurface(session.GetPlayer().RealmId), Is.True);

            // ----- Back to the map -----
            c.ReturnToWorldMap();
            yield return WaitTravel(c);
            Assert.That(c.IsExploring, Is.False, "On the map: no environment loaded.");
            Assert.That(session.GetPlayer().LocationKind, Is.EqualTo(CharacterLocationKind.WorldMap));

            // ----- Straight from the map into the country (follow-up), behind the loading screen -----
            yield return Settle(6);
            bootstrap.Selection.Select(town.SettlementId);
            yield return Settle(2);
            var screens = c.Loading.Shown;
            var fromMap = bootstrap.Hud.WalkCountryBySelected();
            Assert.That(fromMap.IsSuccess, Is.True, fromMap.Message);
            for (var i = 0; i < 600 && !(c.InOverland && !c.Travelling); i++) yield return null;
            Assert.That(c.InOverland, Is.True, "Into the country from the map.");
            Assert.That(c.Loading.Shown, Is.GreaterThan(screens), "Behind the loading screen.");
            Log.Add($"From the map into the country by {town.Name}: {c.LastTransitionMs:0} ms.");
            yield return Settle(10);
            yield return Shot(c, "p23_13_from_the_map", $"Out into the country by {town.Name}, straight from the world map's \"Walk the country\".");
            c.ReturnToWorldMap();
            yield return WaitTravel(c);
            Assert.That(c.IsExploring, Is.False);
        }

        // ----- Captures ---------------------------------------------------------------------------------------

        /// <summary>The loading screen alone: a camera of its own (the environment's camera is destroyed mid-transition).</summary>
        private IEnumerator LoadingShot(string name, string note)
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            var go = new GameObject("Loading shot camera");
            Object.DontDestroyOnLoad(go);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            yield return Render(cam, name, note, true);
            Object.Destroy(go);
        }

        private IEnumerator Shot(ExplorationController c, string name, string note)
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            yield return Render(c.CameraRig.Camera, name, note, true);
        }

        /// <summary>A view from anywhere (the exploration camera moved for the shot, then put back), without the HUD.</summary>
        private IEnumerator FreeShot(ExplorationController c, string name, Vector3 eye, Vector3 look, float fov, bool fog, string note)
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            var cam = c.CameraRig.Camera;
            var t = cam.transform;
            var (pos, rot, oldFov, rig) = (t.position, t.rotation, cam.fieldOfView, c.CameraRig.enabled);
            var hadFog = RenderSettings.fog;
            c.CameraRig.enabled = false;
            t.position = eye;
            t.rotation = Quaternion.LookRotation(look - eye);
            cam.fieldOfView = fov;
            cam.farClipPlane = 9000f;
            RenderSettings.fog = fog && hadFog;
            var focus = new GameObject("Shot focus").transform; // the country streams and draws its detail around what is looked at
            focus.position = look;
            c.Overland.Follow = focus;
            c.Overland.Prewarm(look);
            yield return null;
            yield return Render(cam, name, note, false);
            Object.Destroy(focus.gameObject);
            RenderSettings.fog = hadFog;
            cam.farClipPlane = 2600f;
            c.Overland.Follow = c.Player.transform;
            t.position = pos;
            t.rotation = rot;
            cam.fieldOfView = oldFov;
            c.CameraRig.enabled = rig;
        }

        private IEnumerator Render(Camera camera, string name, string note, bool ui)
        {
            yield return null;
            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var panels = ui ? Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).Select(d => d.panelSettings).Distinct().ToList() : new List<PanelSettings>();
            var previous = panels.Select(x => (x, x.targetTexture, x.clearColor, x.scale)).ToList();
            foreach (var x in panels) { x.targetTexture = rt; x.clearColor = false; x.scale = RkSettings.EffectiveFor(RkSettings.UiScaleChoice, h); }
            yield return null;
            yield return null;
            var target = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = target;
            yield return null;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".jpg"), tex.EncodeToJPG(88));
            Object.Destroy(tex);
            foreach (var (x, t, cl, sc) in previous) { x.targetTexture = t; x.clearColor = cl; x.scale = sc; }
            rt.Release();
            Object.Destroy(rt);
            Log.Add($"{name}: {note}");
        }
    }
}
