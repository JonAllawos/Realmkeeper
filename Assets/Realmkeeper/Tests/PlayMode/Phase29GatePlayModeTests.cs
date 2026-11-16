using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Exploration.Domain;
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
    /// Phase 29 (ADR-0121): the owner's open gate with stone across the way out. In the running game, with the colliders the
    /// game actually builds: every gate of a walled town is swept along its own road, inside to outside and back, by a
    /// player-sized capsule at every lateral offset the road allows; the passage under each arch holds no collider; and the
    /// player walks out of the front gate by scripted input, 14 m into the fields, and back in to where they arrived.
    /// </summary>
    public sealed class Phase29GatePlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private ScriptedExplorationInput _input;
        public static readonly List<string> Log = new List<string>();

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

        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static Vector3 World(ExplorationController c, float x, float z, float up) =>
            c.EnvironmentRoot.transform.TransformPoint(new Vector3(x, LocalPaths.StandingHeight(c.Streamer.Current, x, z) + up, z));

        private static bool Blocks(ExplorationController c, Collider k)
        {
            if (k == null || k.isTrigger) return false;
            if (c.Player != null && k.transform.IsChildOf(c.Player.transform)) return false;
            var n = k.name;
            var parent = k.transform.parent != null ? k.transform.parent.name : string.Empty;
            if (n.StartsWith("Terrain", StringComparison.Ordinal) || n.StartsWith("Ground", StringComparison.Ordinal) || n.StartsWith("Road", StringComparison.Ordinal) || parent.StartsWith("Terrain", StringComparison.Ordinal)) return false;
            if (k.GetComponentInParent<ResidentView>() != null) return false; // people step aside; this is about the stone
            return true;
        }

        /// <summary>Points along a road, 14 m either side of the gate, every metre.</summary>
        private static List<(Vector2 p, Vector2 dir)> RoadThrough(SettlementLayoutData plan, Vector2 gate)
        {
            RoadData road = null;
            var best = 4f;
            foreach (var r in plan.Roads)
                for (var i = 0; i + 1 < r.Line.Count; i++)
                {
                    var d = SettlementTerrain.SegmentDistance(r.Line[i].X, r.Line[i].Z, r.Line[i + 1].X, r.Line[i + 1].Z, gate.x, gate.y, out _, out _);
                    if (d < best) { best = d; road = r; }
                }

            var pts = new List<(Vector2, Vector2)>();
            if (road == null) return pts;
            var line = road.Line.Select(q => new Vector2(q.X, q.Z)).ToList();
            var arc = new float[line.Count];
            for (var i = 1; i < line.Count; i++) arc[i] = arc[i - 1] + Vector2.Distance(line[i - 1], line[i]);
            var s0 = 0f;
            var bd = float.MaxValue;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                SettlementTerrain.SegmentDistance(line[i].x, line[i].y, line[i + 1].x, line[i + 1].y, gate.x, gate.y, out var px, out var pz);
                var d = Vector2.Distance(new Vector2(px, pz), gate);
                if (d < bd) { bd = d; s0 = arc[i] + Vector2.Distance(line[i], new Vector2(px, pz)); }
            }

            for (var s = Mathf.Max(0f, s0 - 14f); s <= Mathf.Min(arc[line.Count - 1], s0 + 14f); s += 1f)
                for (var i = 0; i + 1 < line.Count; i++)
                    if (s <= arc[i + 1] || i + 2 == line.Count)
                    {
                        var t = Mathf.Clamp01((s - arc[i]) / Mathf.Max(1e-4f, arc[i + 1] - arc[i]));
                        pts.Add((Vector2.Lerp(line[i], line[i + 1], t), (line[i + 1] - line[i]).normalized));
                        break;
                    }

            return pts;
        }

        /// <summary>Sweeps a player capsule along the points, at lateral offsets across the road; returns what stops it.</summary>
        private static List<string> Sweep(ExplorationController c, List<(Vector2 p, Vector2 dir)> pts, float lateral)
        {
            var hits = new List<string>();
            for (var o = -lateral; o <= lateral + 1e-3f; o += Mathf.Max(0.5f, lateral / 3f))
                for (var i = 0; i + 1 < pts.Count; i++)
                {
                    var na = new Vector2(pts[i].dir.y, -pts[i].dir.x) * o;
                    var nb = new Vector2(pts[i + 1].dir.y, -pts[i + 1].dir.x) * o;
                    var a = World(c, pts[i].p.x + na.x, pts[i].p.y + na.y, 0.35f);
                    var b = World(c, pts[i + 1].p.x + nb.x, pts[i + 1].p.y + nb.y, 0.35f);
                    var d = b - a;
                    if (d.magnitude < 1e-3f) continue;
                    foreach (var h in Physics.CapsuleCastAll(a, a + Vector3.up * 1.1f, 0.3f, d.normalized, d.magnitude))
                        if (Blocks(c, h.collider)) hits.Add($"{h.collider.name} < {(h.collider.transform.parent != null ? h.collider.transform.parent.name : "-")} at ({pts[i].p.x:0.0},{pts[i].p.y:0.0}) offset {o:0.0}");
                }

            return hits;
        }

        /// <summary>Walks the player toward a point by scripted, camera-relative input; true when within 0.7 m.</summary>
        private IEnumerator WalkTo(ExplorationController c, Vector2 local, float seconds, Action<bool> done)
        {
            var target = World(c, local.x, local.y, 0f);
            var until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                var p = c.Player.transform.position;
                var to = new Vector3(target.x - p.x, 0f, target.z - p.z);
                if (to.magnitude < 0.7f) { _input.Move = Vector2.zero; done(true); yield break; }
                var cam = c.CameraRig.Camera.transform;
                var f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                var r = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
                var dir = to.normalized;
                _input.Move = new Vector2(Vector3.Dot(dir, r), Vector3.Dot(dir, f)).normalized;
                yield return null;
            }

            _input.Move = Vector2.zero;
            done(false);
        }

        [UnityTest]
        public IEnumerator EveryGateOfAWalledTown_IsClearAlongItsRoad_BothWays_AndThePlayerWalksOutAndBackIn()
        {
            Log.Clear();
            var b = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var c = Controller;
            for (var i = 0; i < 8; i++) yield return null;
            yield return CombatDriver.EnterTownAsFighter(b, Object.FindAnyObjectByType<RealmkeeperRuntime>(), c);
            _input = new ScriptedExplorationInput();
            c.SetInput(_input);
            for (var i = 0; i < 10; i++) yield return null;

            var layout = c.Streamer.Current;
            var plan = layout.Plan;
            Assume.That(plan != null && plan.HasWalls, Is.True, "The test town is walled.");
            var gates = SettlementWalls.Gates(plan, layout.Type);
            Assert.That(gates, Is.Not.Empty);

            foreach (var g in gates)
            {
                var pts = RoadThrough(plan, new Vector2(g.X, g.Z));
                Assert.That(pts.Count, Is.GreaterThan(20), g.Key + ": a road through it.");
                var road = plan.Roads.OrderBy(r => r.Line.Zip(r.Line.Skip(1), (p, q) => SettlementTerrain.SegmentDistance(p.X, p.Z, q.X, q.Z, g.X, g.Z, out _, out _)).Min()).First();
                var lateral = Mathf.Max(0f, Mathf.Min(road.Width / 2f, g.Opening / 2f) - 0.5f);
                var outward = Sweep(c, pts, lateral); // the road's own order
                pts.Reverse();
                var inward = Sweep(c, pts, lateral);
                Assert.That(outward, Is.Empty, $"{g.Key}: along the road one way: " + string.Join("; ", outward.Take(6)));
                Assert.That(inward, Is.Empty, $"{g.Key}: and the other: " + string.Join("; ", inward.Take(6)));

                // Under the arch: no collider in the passage's volume, through the whole wall and gatehouse depth.
                var centre = World(c, g.X, g.Z, 1.6f);
                var rot = c.EnvironmentRoot.transform.rotation * Quaternion.LookRotation(new Vector3(g.DirX, 0f, g.DirZ), Vector3.up);
                var inside = Physics.OverlapBox(centre, new Vector3(g.Opening / 2f - 0.5f, 1.25f, g.Tower / 2f + 1.5f), rot).Where(k => Blocks(c, k)).Select(k => k.name + " < " + k.transform.parent?.name).ToList();
                Assert.That(inside, Is.Empty, $"{g.Key}: the passage holds no collider: " + string.Join("; ", inside));
                Log.Add($"{g.Key}: road swept {pts.Count} m both ways at ±{lateral:0.0} m; passage {g.Opening:0.0} m wide, clear.");
            }

            // The player walks out through the front gate into the fields, and back in.
            var main = layout.Entrances.First(e => e.IsMain);
            var mainGate = gates.OrderBy(g => Mathf.Abs(g.X - main.Gate.X) + Mathf.Abs(g.Z - main.Gate.Z)).First();
            var route = RoadThrough(plan, new Vector2(mainGate.X, mainGate.Z));
            var inFirst = Vector2.Dot(route[0].p - new Vector2(mainGate.X, mainGate.Z), new Vector2(main.InX, main.InZ)) > 0f;
            if (inFirst == false) route.Reverse(); // inside first
            var start = c.Player.transform.position;
            var ok = true;
            for (var i = 0; i < route.Count && ok; i += 2)
                yield return WalkTo(c, route[i].p, 6f, r => ok = r);
            var outsideAt = c.EnvironmentRoot.transform.InverseTransformPoint(c.Player.transform.position);
            Assert.That(ok, Is.True, $"Walked out along the road (stopped at ({outsideAt.x:0.0},{outsideAt.z:0.0})).");
            var beyond = Vector2.Dot(new Vector2(outsideAt.x - mainGate.X, outsideAt.z - mainGate.Z), new Vector2(main.InX, main.InZ));
            Assert.That(beyond, Is.LessThan(-8f), "Out past the wall, in the fields.");

            for (var i = route.Count - 1; i >= 0 && ok; i -= 2)
                yield return WalkTo(c, route[i].p, 6f, r => ok = r);
            var backAt = c.EnvironmentRoot.transform.InverseTransformPoint(c.Player.transform.position);
            Assert.That(ok, Is.True, $"Walked back in (stopped at ({backAt.x:0.0},{backAt.z:0.0})).");
            Assert.That(Vector2.Dot(new Vector2(backAt.x - mainGate.X, backAt.z - mainGate.Z), new Vector2(main.InX, main.InZ)), Is.GreaterThan(8f), "Back inside the wall.");
            Log.Add($"Walked out through {mainGate.Key} to {-beyond:0.0} m beyond the gate and back in, by scripted input (start {start}).");
            foreach (var l in Log) TestContext.WriteLine(l);
        }
    }
}
