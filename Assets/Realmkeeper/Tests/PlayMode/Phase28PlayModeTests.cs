using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 28 in the shipped scene (ADR-0120): from the strategic map into a walled town, the player stands just inside its
    /// front gate, on the ground and clear of everything; a player-sized capsule passes the gate; the wall meets the
    /// gatehouse with no gap; the portcullis hangs above head height; the watch stands beside the road facing out; the local
    /// map shows where the player is; and leaving for the country, coming back on foot, saving and loading, and entering
    /// again all land at the same gate. A village and a hamlet arrive at their own ways in.
    /// </summary>
    public sealed class Phase28PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private static readonly List<string> Log = new List<string>();

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
            foreach (var l in Log) Debug.Log("Realmkeeper Phase 28: " + l);
            Log.Clear();
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

        private static IEnumerator Frames(int n)
        {
            for (var i = 0; i < n; i++) yield return null;
        }

        private static IEnumerator Seconds(float s)
        {
            var t = Time.unscaledTime;
            while (Time.unscaledTime - t < s) yield return null;
        }

        private static Vector3 Local(ExplorationController c, Vector3 world) => c.EnvironmentRoot.transform.InverseTransformPoint(world);

        private static Vector3 World(ExplorationController c, float x, float z, float up) =>
            c.EnvironmentRoot.transform.TransformPoint(new Vector3(x, LocalPaths.StandingHeight(c.Streamer.Current, x, z) + up, z));

        /// <summary>Colliders that would stop a walker here: the town's own, not the ground, the player or the people.</summary>
        private static bool Blocks(ExplorationController c, Collider k)
        {
            if (k == null || k.isTrigger) return false;
            if (c.Player != null && k.transform.IsChildOf(c.Player.transform)) return false;
            var n = k.name;
            var parent = k.transform.parent != null ? k.transform.parent.name : string.Empty;
            if (n.StartsWith("Terrain", StringComparison.Ordinal) || n.StartsWith("Ground", StringComparison.Ordinal) || n.StartsWith("Road", StringComparison.Ordinal) || parent.StartsWith("Terrain", StringComparison.Ordinal)) return false;
            if (k.GetComponentInParent<ResidentView>() != null) return false; // people step aside; the arrival is about the place
            return true;
        }

        private static string Name(Collider k) => k.name + " < " + (k.transform.parent != null ? k.transform.parent.name : "-");

        /// <summary>The way along the road from <paramref name="from"/> to <paramref name="to"/>, by a player-sized capsule, 1 m at a time.</summary>
        private static List<string> Pass(ExplorationController c, Vector2 from, Vector2 to)
        {
            var hits = new List<string>();
            var steps = Mathf.CeilToInt(Vector2.Distance(from, to));
            for (var i = 0; i < steps; i++)
            {
                var a = Vector2.Lerp(from, to, (float)i / steps);
                var b = Vector2.Lerp(from, to, (float)(i + 1) / steps);
                var start = World(c, a.x, a.y, 0.35f);
                var end = World(c, b.x, b.y, 0.35f);
                var dir = end - start;
                foreach (var h in Physics.CapsuleCastAll(start, start + Vector3.up * 1.1f, 0.3f, dir.normalized, dir.magnitude))
                    if (Blocks(c, h.collider)) hits.Add($"{Name(h.collider)} at ({a.x:0.0},{a.y:0.0})");
            }

            return hits;
        }

        private static (float opening, float tower) Spec(SettlementType t) =>
            t == SettlementType.Fortress ? (7f, 5.5f) : t == SettlementType.City ? (8f, 5f) : (7.5f, 4f);

        private static IEnumerator CheckArrival(ExplorationController c, string where)
        {
            yield return Frames(4);
            var layout = c.Streamer.Current;
            Assert.That(layout.Plan, Is.Not.Null, where + ": a planned settlement.");
            Assert.That(layout.ArrivalIsFallback, Is.False, where + ": arrived at an entrance.");
            var main = layout.Entrances.First(e => e.IsMain);
            var p = Local(c, c.Player.transform.position);
            Assert.That(new Vector2(p.x - layout.ArrivalPose.X, p.z - layout.ArrivalPose.Z).magnitude, Is.LessThan(0.6f), where + $": the player stands at the arrival ({p.x:0.0},{p.z:0.0}) vs ({layout.ArrivalPose.X:0.0},{layout.ArrivalPose.Z:0.0}).");
            Assert.That(new Vector2(p.x - main.Arrival.X, p.z - main.Arrival.Z).magnitude, Is.LessThan(0.6f), where + ": that is just inside the front gate.");
            var ground = LocalPaths.StandingHeight(layout, p.x, p.z);
            Assert.That(Mathf.Abs(p.y - ground), Is.LessThan(0.35f), where + $": on the ground ({p.y:0.00} vs {ground:0.00}).");
            var w = c.Player.transform.position;
            var overlap = Physics.OverlapCapsule(w + Vector3.up * 0.45f, w + Vector3.up * 1.6f, 0.32f).Where(k => Blocks(c, k)).Select(Name).ToList();
            Assert.That(overlap, Is.Empty, where + ": nothing at the arrival point: " + string.Join("; ", overlap));
            var fwd = c.Player.transform.forward;
            var toCentre = (c.EnvironmentRoot.transform.TransformPoint(Vector3.zero) - w);
            toCentre.y = 0f;
            Assert.That(Vector3.Dot(new Vector3(fwd.x, 0f, fwd.z).normalized, toCentre.normalized), Is.GreaterThan(0.3f), where + ": facing into the settlement.");

            // Walk in from 16 m outside the entrance to the arrival point: nothing of the town stops a player-sized capsule.
            var outside = new Vector2(main.Gate.X - main.InX * 16f, main.Gate.Z - main.InZ * 16f);
            var blocked = Pass(c, outside, new Vector2(main.Arrival.X, main.Arrival.Z));
            Assert.That(blocked, Is.Empty, where + ": the way in is open: " + string.Join("; ", blocked.Take(6)));

            // The local map draws the player where they are (not where they were in the last place).
            Assert.That(c.LocalMap, Is.Not.Null);
            Assert.That(Vector2.Distance(c.LocalMap.PlayerAt, new Vector2(w.x, w.z)), Is.LessThan(0.5f), where + ": the local map shows the player at the gate.");
            Log.Add($"{where}: arrived at ({p.x:0.0},{p.z:0.0}), {Mathf.Sqrt(p.x * p.x + p.z * p.z):0.0} m from the centre by the {(main.Walled ? "gate" : "way in")} at ({main.Gate.X:0.0},{main.Gate.Z:0.0}); the capsule passed {Vector2.Distance(outside, new Vector2(main.Arrival.X, main.Arrival.Z)):0} m.");
        }

        [UnityTest]
        public IEnumerator FromTheMap_IntoAWalledTown_AtItsFrontGate_ThroughIt_WatchedAndConsistent()
        {
            var b = Bootstrap;
            var c = Controller;
            yield return Frames(8);
            yield return CombatDriver.EnterTownAsFighter(b, Runtime, c);
            var s = b.Session;
            yield return CheckArrival(c, "Town from the map");
            var layout = c.Streamer.Current;
            Assume.That(layout.HasWalls, Is.True, "The test town is walled.");
            var main = layout.Entrances.First(e => e.IsMain);
            var root = c.EnvironmentRoot.transform;

            // The wall meets the gatehouse: along the wall line either side, towers then wall, no gap a person could pass.
            var (opening, tower) = Spec(layout.Type);
            // Sampled along the wall's own line (a 32-sided circuit, so it bends away from the gate's tangent past a corner).
            var gaps = new List<string>();
            var wallLine = layout.Plan.Wall;
            var gate2 = new Vector2(main.Gate.X, main.Gate.Z);
            var sampled = 0;
            for (var i = 0; i < wallLine.Count; i++)
            {
                var a = new Vector2(wallLine[i].X, wallLine[i].Z);
                var q = wallLine[(i + 1) % wallLine.Count];
                var bb = new Vector2(q.X, q.Z);
                var len = Vector2.Distance(a, bb);
                for (var t = 0f; t <= len; t += 0.8f)
                {
                    var pt = Vector2.Lerp(a, bb, t / len);
                    var d = Vector2.Distance(pt, gate2);
                    if (d < opening / 2f + 0.6f || d > opening / 2f + tower + 6f) continue;
                    if (SettlementTerrain.InWater(layout.Plan, pt.x, pt.y, 1f)) continue;
                    sampled++;
                    var at = World(c, pt.x, pt.y, 2.2f);
                    var hit = Physics.OverlapSphere(at, 0.6f).Any(k => k.name == "Tower" || k.name == "Wall");
                    if (!hit) gaps.Add($"({pt.x:0.0},{pt.y:0.0}) {d:0.0} m from the gate");
                }
            }

            Assert.That(sampled, Is.GreaterThan(10), "The wall either side of the gate was sampled.");

            Assert.That(gaps, Is.Empty, "The wall closes on the gatehouse: open at " + string.Join(", ", gaps));

            // The gate: a raised portcullis above head height and two open leaves at the passage sides.
            var house = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("(gatehouse)", StringComparison.Ordinal))
                .OrderBy(t => Vector3.Distance(t.position, World(c, main.Gate.X, main.Gate.Z, 0f))).First();
            var grid = house.Find("Portcullis");
            Assert.That(grid, Is.Not.Null, "A portcullis.");
            // (Static geometry is merged after the build, so measure from the grid's own frame: its bars reach 0.4 m below it.)
            var lowest = grid.position.y - 0.4f;
            Assert.That(lowest - World(c, main.Gate.X, main.Gate.Z, 0f).y, Is.GreaterThan(3.0f), "Raised: its teeth well above any head.");
            Assert.That(house.Cast<Transform>().Count(t => t.name == "Gate leaf"), Is.EqualTo(2), "Two open leaves.");

            // Every gatehouse tower stands on its ground: its foundation reaches below the ground at each corner.
            var towers = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(k => k.name == "Tower" && k.transform.parent != null && k.transform.parent.name.EndsWith(" colliders", StringComparison.Ordinal)).ToList();
            Assert.That(towers.Count, Is.GreaterThanOrEqualTo(2 * layout.Entrances.Count), "Two towers at every gate.");
            foreach (var k in towers)
            {
                var bnd = k.bounds;
                foreach (var (x, z) in new[] { (bnd.min.x, bnd.min.z), (bnd.min.x, bnd.max.z), (bnd.max.x, bnd.min.z), (bnd.max.x, bnd.max.z) })
                {
                    var lp = root.InverseTransformPoint(new Vector3(x, 0f, z));
                    var ground = World(c, lp.x, lp.z, 0f).y;
                    Assert.That(bnd.min.y, Is.LessThan(ground - 0.3f), $"{k.transform.parent.name}: a tower's foundation is below the ground at its corner ({bnd.min.y:0.00} vs {ground:0.00}).");
                }
            }

            // The watch: both main-gate posts manned (day) or held (night), beside the road and facing out.
            yield return Seconds(3f);
            var guards = c.Townsfolk.Where(x => x.Activity == TownsfolkActivity.Guarding).ToList();
            // A post is held by the unnamed watch or by a named gate guard standing at it, never both.
            var namedViews = c.Residents.Values.Where(r => r != null && !r.IsHidden).Select(r => Local(c, r.transform.position)).ToList();
            Assert.That(guards.Count + Mathf.Min(layout.NamedAtGate, 2), Is.GreaterThanOrEqualTo(2), "The main gate is watched.");
            foreach (var p in main.Posts)
            {
                var byWatch = guards.Any(g => Mathf.Abs(g.Route[0].X - p.X) + Mathf.Abs(g.Route[0].Z - p.Z) < 0.01f);
                var byNamed = layout.Residents.Any(r => r.SiteKey == SettlementSites.Gate && Mathf.Abs(r.Position.X - p.X) + Mathf.Abs(r.Position.Z - p.Z) < 1.0f);
                Assert.That(byWatch || byNamed, Is.True, "Each main-gate post manned.");
                Assert.That(byWatch && byNamed, Is.False, "Never two to a post.");
            }

            Log.Add($"Named at the gate: {layout.NamedAtGate}; named people drawn within 3 m of a main-gate post: {namedViews.Count(v => main.Posts.Any(p => Vector2.Distance(new Vector2(v.x, v.z), new Vector2(p.X, p.Z)) < 3f))}.");
            var road = layout.Plan.Roads.First(r => r.Key == main.RoadKey);
            var shown = 0;
            foreach (var g in guards.Where(g => main.Posts.Any(p => Mathf.Abs(g.Route[0].X - p.X) + Mathf.Abs(g.Route[0].Z - p.Z) < 0.01f)))
            {
                var fig = GameObject.Find("Townsperson " + g.Key);
                if (fig == null || !fig.activeInHierarchy) continue;
                shown++;
                var lp = Local(c, fig.transform.position);
                var off = road.Line.Zip(road.Line.Skip(1), (a, q) => DistanceToSegment(new Vector2(lp.x, lp.z), new Vector2(a.X, a.Z), new Vector2(q.X, q.Z))).Min();
                Assert.That(off, Is.GreaterThan(road.Width / 2f + 0.5f), $"{g.Key} stands beside the road ({off:0.0} m from its centre line), not in it.");
                var facing = root.InverseTransformDirection(fig.transform.forward);
                Assert.That(Vector2.Dot(new Vector2(facing.x, facing.z).normalized, new Vector2(-main.InX, -main.InZ)), Is.GreaterThan(0.8f), g.Key + " faces out of the gate.");
            }

            // Named gate guards drawn at the main gate's posts: beside the road too.
            foreach (var v in namedViews.Where(v => main.Posts.Any(p => Vector2.Distance(new Vector2(v.x, v.z), new Vector2(p.X, p.Z)) < 1.5f)))
            {
                shown++;
                var off = road.Line.Zip(road.Line.Skip(1), (a, q) => DistanceToSegment(new Vector2(v.x, v.z), new Vector2(a.X, a.Z), new Vector2(q.X, q.Z))).Min();
                Assert.That(off, Is.GreaterThan(road.Width / 2f + 0.3f), $"A named gate guard stands beside the road ({off:0.0} m from its centre line), not in it.");
            }

            Assert.That(shown, Is.GreaterThanOrEqualTo(1), "The gate guards are drawn when the player is at the gate.");
            Assert.That(c.Townsfolk.Select(x => x.Key).Distinct().Count(), Is.EqualTo(c.Townsfolk.Count), "No one twice.");
            Log.Add($"Watch: {guards.Count} posted, {c.Townsfolk.Count(x => x.Activity == TownsfolkActivity.Patrolling)} patrolling, {shown} main-gate guards drawn; wall checked {opening / 2f + 0.6f:0.0}–{opening / 2f + tower + 6f:0.0} m either side.");

            // Walking in with the player's own controls.
            var input = new ScriptedExplorationInput();
            c.SetInput(input);
            var before = Local(c, c.Player.transform.position);
            input.Move = new Vector2(0f, 1f);
            yield return Seconds(2.5f);
            input.Move = Vector2.zero;
            yield return Frames(2);
            var after = Local(c, c.Player.transform.position);
            Assert.That(Vector2.Distance(new Vector2(before.x, before.z), new Vector2(after.x, after.z)), Is.GreaterThan(2.5f), "The player walks on in.");
            Assert.That(new Vector2(after.x, after.z).magnitude, Is.LessThan(new Vector2(before.x, before.z).magnitude), "Toward the centre.");

            // Out into the country, then back in on foot: by the road to the main gate, at the main gate.
            var id = layout.SettlementId;
            var outRes = s.Dispatch(new EnterOverlandCommand());
            Assert.That(outRes.IsSuccess, Is.True, outRes.Message);
            yield return Seconds(1.5f);
            var inRes = s.Dispatch(new EnterSettlementFromOverlandCommand(id));
            Assert.That(inRes.IsSuccess, Is.True, inRes.Message);
            yield return Seconds(1.5f);
            yield return CheckArrival(c, "Town from the country");

            // Saved and loaded: still at the gate.
            var pose = Local(c, c.Player.transform.position);
            Assert.That(c.SaveNow().IsSuccess, Is.True);
            Assert.That(s.Execute(new LoadWorldCommand(Runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            yield return Seconds(1.5f);
            var loaded = Local(c, c.Player.transform.position);
            Assert.That(Vector2.Distance(new Vector2(pose.x, pose.z), new Vector2(loaded.x, loaded.z)), Is.LessThan(0.6f), "After a load, where the save was made.");
            Assert.That(Vector2.Distance(c.LocalMap.PlayerAt, new Vector2(c.Player.transform.position.x, c.Player.transform.position.z)), Is.LessThan(0.5f), "The local map follows the load.");

            // To the world map and in again from it: the same gate.
            c.ReturnToWorldMap();
            yield return Seconds(1f);
            b.Selection.Select(id);
            Assert.That(b.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return Seconds(1.5f);
            yield return CheckArrival(c, "Town again from the map");
        }

        [UnityTest]
        public IEnumerator AVillageAndAHamlet_ArriveAtTheirOwnWaysIn_AndTheMapIsNeverStale()
        {
            var b = Bootstrap;
            var c = Controller;
            yield return Frames(8);
            yield return CombatDriver.EnterTownAsFighter(b, Runtime, c);
            var s = b.Session;
            var tested = 0;
            foreach (var type in new[] { SettlementType.Village, SettlementType.Hamlet })
            {
                var other = s.GetSettlementsInView().FirstOrDefault(x => x.SettlementType == type);
                if (other == null) { Log.Add("No " + type + " in view."); continue; }
                c.ReturnToWorldMap();
                yield return Seconds(1f);
                b.Selection.Select(other.SettlementId);
                var entered = b.Hud.EnterSelectedSettlement();
                Assert.That(entered.IsSuccess, Is.True, entered.Message);
                yield return Seconds(1.5f);
                Runtime.Playback.Pause();
                yield return CheckArrival(c, $"{other.Name} ({type})");
                var layout = c.Streamer.Current;
                Assert.That(layout.HasWalls, Is.False);
                var wayIn = c.EnvironmentRoot.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Way in ", StringComparison.Ordinal));
                Assert.That(wayIn, Is.True, type + ": its way in is marked (a gateway or a waymarker).");
                tested++;
            }

            Assert.That(tested, Is.GreaterThan(0), "At least one smaller settlement in view.");
        }

        /// <summary>
        /// Performance (editor, batch mode): entering the town from the map, timed, five times over; the scene's object count and
        /// managed memory after each entry must not grow (no duplicate gatehouses, guards or townsfolk left behind).
        /// </summary>
        [UnityTest]
        public IEnumerator RepeatedEntry_IsTimed_AndLeavesNothingBehind()
        {
            var b = Bootstrap;
            var c = Controller;
            yield return Frames(8);
            yield return CombatDriver.EnterTownAsFighter(b, Runtime, c);
            yield return Seconds(1f);
            var id = c.Streamer.Current.SettlementId;
            var times = new List<double>();
            var objects = new List<int>();
            var memory = new List<long>();
            var gateObjects = 0;
            for (var k = 0; k < 5; k++)
            {
                c.ReturnToWorldMap();
                yield return Seconds(0.5f);
                b.Selection.Select(id);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Assert.That(b.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
                yield return null; // the first frame of the settlement drawn
                watch.Stop();
                times.Add(watch.Elapsed.TotalMilliseconds);
                Runtime.Playback.Pause();
                yield return Seconds(2f); // the nearest townsfolk given figures
                objects.Add(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
                memory.Add(GC.GetTotalMemory(true));
                if (k == 0)
                    gateObjects = c.EnvironmentRoot.GetComponentsInChildren<Transform>(true).Count(t => t.name.EndsWith("(gatehouse)", StringComparison.Ordinal) || t.name == "Portcullis" || t.name == "Gate leaf" || t.name.StartsWith("Way in", StringComparison.Ordinal));
                Assert.That(c.Townsfolk.Select(x => x.Key).Distinct().Count(), Is.EqualTo(c.Townsfolk.Count), "No one twice.");
            }

            Log.Add($"Entry from the map (EnterSelectedSettlement to the first frame, editor): {string.Join(", ", times.Select(t => t.ToString("0")))} ms; objects after each entry {string.Join(", ", objects)}; managed memory {string.Join(", ", memory.Select(m => (m / 1048576.0).ToString("0.0")))} MB; gate roots, portcullises and leaves kept as objects: {gateObjects}.");
            var settled = objects.Skip(1).ToList();
            Assert.That(settled.Max() - settled.Min(), Is.LessThan(Math.Max(50, settled.Min() / 50)), "Entering again and again leaves nothing behind (object count steady within 2 %).");
            Assert.That(memory.Last() - memory[1], Is.LessThan(64L * 1024 * 1024), "Managed memory does not climb with each entry.");
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
