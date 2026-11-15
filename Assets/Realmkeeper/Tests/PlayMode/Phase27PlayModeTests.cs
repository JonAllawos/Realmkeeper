using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
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
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 27 in the shipped scene (ADR-0119). The first test verifies and measures:
    /// <list type="bullet">
    /// <item>the street population against the plan (abstract tier) and the shown figures (detailed tier);</item>
    /// <item>the frame with and without the townsfolk (editor, batch mode);</item>
    /// <item>a new hour's people, shopkeeper callouts and their cooldown, grazing animals inside their pastures;</item>
    /// <item>dressing kept off the doors, and the kit's door leaf against its frame.</item>
    /// </list>
    /// The second writes the screenshots when RK_CAPTURE_DIR is set.
    /// </summary>
    public sealed class Phase27PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private bool _motion;
        private string _dir;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _motion = RkSettings.ReducedMotion;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (!string.IsNullOrEmpty(_dir) && Log.Count > 0) { Directory.CreateDirectory(_dir); File.AppendAllLines(Path.Combine(_dir, "p27_log.txt"), Log); }
            foreach (var l in Log) Debug.Log("Phase 27: " + l);
            Log.Clear();
            RkSettings.ReducedMotion = _motion;
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
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

        private static IEnumerator Frames(int n)
        {
            for (var i = 0; i < n; i++) yield return null;
        }

        private static void Stand(ExplorationController c, float x, float z, float heading)
        {
            var layout = c.Streamer.Current;
            c.Player.Teleport(new LocalPose(x, LocalPaths.StandingHeight(layout, x, z) + 0.3f, z, heading));
            c.CameraRig.SetView(heading, 12f);
        }

        private static IEnumerator Noon(ExplorationController c, RealmkeeperBootstrap b)
        {
            var now = b.Session.GetWorldTime().MinuteOfDay;
            var to = (12 * 60 - now + 24 * 60) % (24 * 60);
            if (to > 0) c.WaitHour(to);
            yield return Frames(6);
        }

        [UnityTest]
        public IEnumerator StreetLife_IsDrawnFromThePopulation_StaysCheap_AndKeepsTheWaysClear()
        {
            var b = Bootstrap;
            var c = Controller;
            yield return Frames(8);
            var enter = Stopwatch.StartNew();
            yield return CombatDriver.EnterTownAsFighter(b, Runtime, c);
            Log.Add($"Entering the town (layout, build, residents, first frames): {enter.Elapsed.TotalMilliseconds:0} ms (editor, batch mode).");
            yield return Noon(c, b);
            var s = b.Session;
            var layout = c.Streamer.Current;
            var life = s.GetStreetLife(layout.SettlementId);
            Stand(c, 0f, -(layout.PlazaRadius + 4f), 0f);
            yield return Seconds(6f); // figures are built two at a time: let the street fill

            // The abstract tier: the plan for this hour, from the population.
            var expected = TownsfolkPlanner.OnStreets(new TownsfolkInput { Population = life.Population, Type = life.Type, MinuteOfDay = s.GetWorldTime().MinuteOfDay, NamedOut = c.Residents.Values.Count(r => r != null && !r.IsHidden) });
            var unnamed = c.Townsfolk.Count(x => !x.Key.StartsWith("watch", StringComparison.Ordinal));
            var watch = c.Townsfolk.Count - unnamed;
            Log.Add($"{life.Population} inhabitants; named out {c.Residents.Values.Count(r => r != null && !r.IsHidden)}; unnamed planned out at noon {unnamed} (the rule gives {expected}); watch {watch}; figures shown {c.TownsfolkFigures} (at most {ExplorationController.TownsfolkShown} within {ExplorationController.TownsfolkRange} m).");
            Assert.That(unnamed, Is.GreaterThanOrEqualTo((int)(expected * 0.9)), "Nearly everyone the rule puts out is planned.");
            Assert.That(c.TownsfolkFigures, Is.GreaterThan(5), "People are shown in the street near the square.");
            Assert.That(c.TownsfolkFigures, Is.LessThanOrEqualTo(ExplorationController.TownsfolkShown));
            var figures = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Townsperson ", StringComparison.Ordinal) && t.gameObject.activeInHierarchy).ToList();
            foreach (var f in figures)
            {
                Assert.That(f.GetComponentsInChildren<Collider>().Length, Is.EqualTo(0), "Townsfolk never block a way.");
                Assert.That(Vector3.Distance(f.position, c.Player.transform.position), Is.LessThan(ExplorationController.TownsfolkRange + 15f));
            }

            // Measurements (editor, batch mode): the frame with and without the townsfolk, here by the square at noon.
            float Average(List<float> l) => l.Count == 0 ? 0f : l.Average();
            var on = new List<float>();
            var cost = new List<double>();
            for (var i = 0; i < 240; i++) { yield return null; on.Add(Time.unscaledDeltaTime * 1000f); cost.Add(c.TownsfolkFrameMs); }
            c.TownsfolkEnabled = false;
            yield return Frames(5);
            var off = new List<float>();
            for (var i = 0; i < 240; i++) { yield return null; off.Add(Time.unscaledDeltaTime * 1000f); }
            c.TownsfolkEnabled = true;
            yield return Seconds(1.2f);
            var memory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576;
            Log.Add($"Frame by the square at noon (editor, batch mode; not a player build): with townsfolk {Average(on):0.00} ms mean, {on.Max():0.0} ms worst; without {Average(off):0.00} ms mean, {off.Max():0.0} ms worst. Townsfolk update {cost.Average():0.000} ms mean, {cost.Max():0.000} ms worst; plan for the hour {c.TownsfolkPlanMs:0.0} ms; figures built so far {c.TownsfolkBuilt}, slowest build {c.TownsfolkBuildMsMax:0.0} ms; allocated memory {memory} MB.");
            Assert.That(cost.Average(), Is.LessThan(2.0), "Moving the shown townsfolk costs little (editor).");

            // A new hour: the plan follows the hour's share of the population.
            var before = c.Townsfolk.Count;
            c.WaitHour(10 * 60); // 22:00
            yield return Seconds(1f);
            Log.Add($"At 22:00: {c.Townsfolk.Count} people out (noon: {before}); figures shown {c.TownsfolkFigures}.");
            Assert.That(c.Townsfolk.Count, Is.LessThan(before), "Fewer people out at night.");
            c.WaitHour(14 * 60); // back to noon next day
            yield return Seconds(1f);

            // Shopkeeper callouts: by an open shop whose keeper is in sight, once, then the gap holds.
            var callShop = (ShopInteriorView)null;
            ResidentView keeper = null;
            foreach (var v in c.Shops.Values.Where(v => v != null && v.IsOpen))
            {
                var k = s.GetShopDetail(v.ShopId)?.Summary?.KeeperId;
                if (k == null || !c.Residents.TryGetValue(k.Value, out var r) || r == null || r.IsHidden) continue;
                callShop = v;
                keeper = r;
                break;
            }

            if (keeper != null)
            {
                var kp = keeper.transform.position;
                var door = LocalPaths.DoorOutside(callShop.Building, 0f);
                var away = new Vector2(door.X - kp.x, door.Z - kp.z).normalized;
                Stand(c, kp.x + away.x * 7f, kp.z + away.y * 7f, 0f);
                var count = c.CalloutCount;
                var waited = Time.unscaledTime;
                while (c.CalloutCount == count && Time.unscaledTime - waited < 17f) yield return null; // the 14 s gap since any earlier call
                Assert.That(c.CalloutCount, Is.EqualTo(count + 1), $"The keeper calls out to the player nearby (keeper {Vector3.Distance(keeper.transform.position, c.Player.transform.position):0.0} m away, hidden {keeper.IsHidden}; last check: {c.CalloutWhyNot}).");
                Assert.That(c.LastCallout.shop, Is.EqualTo(callShop.ShopId));
                Log.Add($"Callout from {keeper.DisplayName} at {callShop.ShopName}: \"{c.LastCallout.line}\" after {Time.unscaledTime - waited:0.0} s.");
                yield return Seconds(4f);
                Assert.That(c.CalloutCount, Is.EqualTo(count + 1), "No second call within the gap.");
                c.ToggleHub();
                yield return Seconds(15.5f); // past the gap: only the open window can hold the next call
                Assert.That(c.CalloutCount, Is.EqualTo(count + 1), "No call while a window is open.");
                c.CloseWindows();
            }
            else Log.Add("Callouts: no open shop had its keeper out at this hour in this world; not exercised.");

            // Animals: a pasture's animals graze, inside their field.
            var wanderers = Object.FindObjectsByType<AmbientWander>(FindObjectsSortMode.None).ToList();
            if (wanderers.Count > 0)
            {
                var a = wanderers[0];
                Stand(c, a.transform.position.x + 6f, a.transform.position.z + 6f, 0f);
                yield return Seconds(6f);
                var moved = wanderers.Where(w => (w.transform.position - c.Player.transform.position).magnitude < AmbientWander.AwakeRange).ToList();
                Log.Add($"Animals: {wanderers.Count} in pastures; {moved.Count(w => w.Travelled > 0f)} of {moved.Count} near the player moved; all inside their fields: {moved.All(w => w.InsideField(w.transform.localPosition))}.");
                Assert.That(moved.Any(w => w.Travelled > 0f), Is.True, "Animals near the player move.");
                Assert.That(moved.All(w => w.InsideField(w.transform.localPosition)), Is.True, "Inside their pastures.");
            }
            else Log.Add("Animals: this town has no herds in its pastures.");

            // Dressing: in the scene, and no dressing collider stands in any doorway's approach.
            var dressing = GameObject.Find("Phase 27 dressing");
            Assert.That(dressing, Is.Not.Null);
            var colliders = dressing.GetComponentsInChildren<BoxCollider>().ToList();
            var blocked = 0;
            foreach (var bld in layout.Buildings)
            {
                var d = LocalPaths.DoorOutside(bld, 0f);
                var centre = c.EnvironmentRoot.transform.TransformPoint(new Vector3(d.X, LocalPaths.StandingHeight(layout, d.X, d.Z) + 1f, d.Z));
                foreach (var hit in Physics.OverlapBox(centre, new Vector3(0.6f, 0.8f, 0.6f), Quaternion.Euler(0f, -bld.Yaw, 0f)))
                    if (colliders.Contains(hit as BoxCollider)) blocked++;
            }

            Log.Add($"Dressing: {layout.Dressing.Count} items planned, {dressing.transform.childCount} placed, {colliders.Count} solid; doorways blocked: {blocked}.");
            Assert.That(blocked, Is.EqualTo(0), "Nothing stands in front of a door.");

            // The kit's door leaf against its frame: the opening measured by rays through the wall.
            foreach (var (wallId, leafId) in new[] { ("Wall_Plaster_Door_Flat", "Door_1_Flat"), ("Wall_Plaster_Door_Round", "Door_1_Round") })
            {
                var wallE = c.Catalog.Module(wallId);
                var leafE = c.Catalog.Module(leafId);
                if (wallE == null || leafE == null) { Log.Add($"Door check: {wallId} or {leafId} not in the catalog."); continue; }
                var wall = Object.Instantiate(wallE.Prefab); // its own root rotation and scale, as the kit places it
                wall.transform.position = new Vector3(5000f, 0f, 5000f);
                foreach (var mf in wall.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                var leaf = Object.Instantiate(leafE.Prefab);
                leaf.transform.position = new Vector3(6000f, 0f, 6000f);
                Physics.SyncTransforms();
                Bounds World(GameObject g) { var rs = g.GetComponentsInChildren<Renderer>(); var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); return bb; }
                var lb = World(leaf);
                var wb = World(wall); // world space: these kit models import at scale 100
                float open = 0f, top = 0f, first = float.NaN, last = float.NaN;
                for (var x = wb.min.x; x <= wb.max.x; x += 0.01f)
                    if (!Physics.Raycast(new Vector3(x, wb.min.y + 1.0f, wb.min.z - 1f), Vector3.forward, wb.size.z + 2f)) { open += 0.01f; if (float.IsNaN(first)) first = x - 5000f; last = x - 5000f; }
                for (var y = wb.min.y + 0.05f; y <= wb.max.y; y += 0.01f)
                    if (!Physics.Raycast(new Vector3(wb.center.x, y, wb.min.z - 1f), Vector3.forward, wb.size.z + 2f)) top = y - wb.min.y;
                Log.Add($"Door check {wallId} + {leafId}: wall {wb.size.x:0.00} × {wb.size.y:0.00} × {wb.size.z:0.00} m; opening {open:0.00} m wide at 1 m, {top:0.00} m high at the centre; leaf {lb.size.x:0.00} × {lb.size.y:0.00} × {lb.size.z:0.00} m; opening x {first:0.00} to {last:0.00} from the wall's origin; leaf x {lb.min.x - 6000f:0.00} to {lb.max.x - 6000f:0.00} from its origin; leaf y {lb.min.y:0.00} to {lb.max.y:0.00}.");
                var from = BuildingKitPlanner.DoorLeafAlong + (lb.min.x - 6000f) * BuildingKitPlanner.DoorLeafWidth;
                var to = BuildingKitPlanner.DoorLeafAlong + (lb.max.x - 6000f) * BuildingKitPlanner.DoorLeafWidth;
                Log.Add($"As the kit hangs it: the leaf spans {from:0.00} to {to:0.00}; the opening {first:0.00} to {last:0.00}.");
                Assert.That(from, Is.LessThanOrEqualTo(first + 0.03f), wallId + ": the leaf reaches the frame's near side.");
                Assert.That(to, Is.GreaterThanOrEqualTo(last - 0.03f), wallId + ": the leaf reaches the frame's far side.");
                Object.Destroy(wall);
                Object.Destroy(leaf);
            }
        }
    }
}
