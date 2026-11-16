using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Tests.EditMode.Atlas;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Living
{
    /// <summary>
    /// Phase 28 (ADR-0120): arriving at a settlement's real front gate. Every plan of six generated worlds has a main entrance
    /// (its own first main road, the plan's Gates[0] where walled); the traveller stands just inside it on the road, clear of
    /// buildings, fields and water, facing in; posts stand beside the road; fallbacks are deterministic; and entering from the
    /// map, from the country, again, and after a save all land there.
    /// </summary>
    public sealed class Phase28SettlementArrivalTests : Phase16Fixture
    {
        private static IEnumerable<(SettlementData s, SettlementLayoutData plan)> Plans()
        {
            foreach (var seed in new long[] { 123456789, 42, 777, 2026, 90210, 31337 })
            {
                var g = Phase2Worlds.Generate(seed);
                foreach (var s in g.World.Settlements) yield return (s, SettlementLayoutGenerator.Generate(g.World, g.Map, s));
            }
        }

        private static float ToLine(List<PlanPoint> line, PlanPoint p)
        {
            var best = float.MaxValue;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                var dx = b.X - a.X;
                var dz = b.Z - a.Z;
                var len2 = Math.Max(1e-6f, dx * dx + dz * dz);
                var t = Math.Max(0f, Math.Min(1f, ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / len2));
                var cx = a.X + dx * t - p.X;
                var cz = a.Z + dz * t - p.Z;
                best = Math.Min(best, (float)Math.Sqrt(cx * cx + cz * cz));
            }

            return best;
        }

        [Test]
        public void EveryPlan_ArrivesJustInsideItsFrontGate_OnTheRoad_ClearAndFacingIn()
        {
            var lines = new List<string>();
            int plans = 0, walled = 0, secondary = 0;
            foreach (var (s, plan) in Plans().Concat(W.Settlements.Select(x => (x, W.FindLayout(x.SettlementId))).Where(x => x.Item2 != null)))
            {
                plans++;
                var all = SettlementEntrances.Of(plan);
                Assert.That(all, Is.Not.Empty, $"{s.Name}: a way in.");
                Assert.That(all.Count(e => e.IsMain), Is.EqualTo(1), $"{s.Name}: one main entrance.");
                Assert.That(all[0].IsMain, Is.True, "Main first (rank order).");
                var (at, yaw, by, fallback) = SettlementEntrances.Arrival(plan);
                Assert.That(fallback, Is.False, $"{s.Name} ({s.SettlementType}): arrives at an entrance, never at a generic point.");
                if (!by.IsMain) secondary++;

                var road = plan.Roads.Single(r => r.Key == by.RoadKey);
                var radius = plan.CoreRadius;
                var r0 = (float)Math.Sqrt(at.X * at.X + at.Z * at.Z);
                Assert.That(r0, Is.LessThan(plan.HasWalls ? radius : radius + 9f).And.GreaterThan(radius - 32f), $"{s.Name}: just inside ({r0:0.0} m from the centre, the {(plan.HasWalls ? "wall" : "core")} at {radius:0} m).");
                Assert.That(ToLine(road.Line, at), Is.LessThanOrEqualTo(road.Width / 2f + 0.01f), $"{s.Name}: on the road.");
                Assert.That(SettlementEntrances.Clear(plan, at, 1.0f), Is.True, $"{s.Name}: clear of buildings, fields and water.");
                var fx = (float)Math.Sin(yaw * Math.PI / 180.0);
                var fz = (float)Math.Cos(yaw * Math.PI / 180.0);
                Assert.That((-at.X * fx - at.Z * fz) / Math.Max(1f, r0), Is.GreaterThan(0.3f), $"{s.Name}: facing into the settlement.");

                if (plan.HasWalls)
                {
                    walled++;
                    Assert.That(all.Count, Is.EqualTo(plan.Gates.Count), $"{s.Name}: one entrance per gate in the wall.");
                    Assert.That(all[0].Gate.DistanceTo(plan.Gates[0]), Is.LessThan(0.01f), $"{s.Name}: the main entrance is the plan's own town gate (Gates[0]).");
                    Assert.That(by.Gate.DistanceTo(plan.Gates[all.FindIndex(e => e.RoadKey == by.RoadKey)]), Is.LessThan(0.01f));
                }

                foreach (var e in all)
                    foreach (var p in e.Posts)
                    {
                        var rd = plan.Roads.Single(r => r.Key == e.RoadKey);
                        Assert.That(ToLine(rd.Line, p), Is.GreaterThanOrEqualTo(rd.Width / 2f + 1.0f), $"{s.Name}: a watch post beside the road, not in it.");
                        Assert.That(SettlementEntrances.Clear(plan, p, 0.6f), Is.True, $"{s.Name}: a post clear of buildings.");
                    }

                lines.Add($"{s.Name} ({s.SettlementType}, {(plan.HasWalls ? "walled" : "open")}): {all.Count} entrances, arrives {r0:0.0} m from the centre by {(by.IsMain ? "the main" : "a secondary")} entrance; posts {all.Sum(e => e.Posts.Length)}");
            }

            // Cost (Release, .NET): deriving every plan's entrances and arrival, 20 times over.
            var all45 = Plans().Concat(W.Settlements.Select(x => (x, W.FindLayout(x.SettlementId))).Where(x => x.Item2 != null)).Select(x => x.Item2).ToList();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var k = 0; k < 20; k++) foreach (var p in all45) SettlementEntrances.Arrival(p);
            sw.Stop();
            TestContext.WriteLine($"Entrances and arrival: {sw.Elapsed.TotalMilliseconds * 1000.0 / (20 * all45.Count):0} µs per settlement ({all45.Count} plans, 20 rounds).");
            TestContext.WriteLine($"{plans} plans, {walled} walled; {secondary} arrive by a secondary entrance (the main one had no clear standing point).\n" + string.Join("\n", lines.Take(40)));
            Assert.That(plans, Is.GreaterThan(40), "Six small worlds and the 27 settlements of the Phase 16 world.");
            Assert.That(walled, Is.GreaterThan(0), "Walled towns among them.");
            Assert.That(secondary, Is.LessThan(plans / 10 + 1), "Nearly always the main gate.");
        }

        [Test]
        public void TheApproach_RunsOnThroughTheGate_AndTheGatehouseStandsOnItsGround()
        {
            var lines = new List<string>();
            float worstTurn = 0f, worstSpan = 0f, worstCrease = 0f;
            var sharp = new List<string>();
            var uneven = new List<string>();
            var creases = new List<string>();
            int gates = 0;
            foreach (var (s, plan) in Plans().Concat(W.Settlements.Select(x => (x, W.FindLayout(x.SettlementId))).Where(x => x.Item2 != null)))
                foreach (var e in SettlementEntrances.Of(plan))
                {
                    gates++;
                    var road = plan.Roads.Single(r => r.Key == e.RoadKey);
                    // The road's heading along 1 m steps within 15 m either side of the gate: the sharpest turn.
                    var pts = Resample(road.Line, 1f);
                    var gi = pts.Select((p, i) => (p, i)).OrderBy(x => x.p.DistanceTo(e.Gate)).First().i;
                    var turn = 0f;
                    for (var i = Math.Max(1, gi - 15); i < Math.Min(pts.Count - 1, gi + 15); i++)
                    {
                        var a = Math.Atan2(pts[i].Z - pts[i - 1].Z, pts[i].X - pts[i - 1].X);
                        var b = Math.Atan2(pts[i + 1].Z - pts[i].Z, pts[i + 1].X - pts[i].X);
                        var d = Math.Abs(a - b) % (2 * Math.PI);
                        if (d > Math.PI) d = 2 * Math.PI - d;
                        turn = Math.Max(turn, (float)(d * 180 / Math.PI));
                    }

                    // The ground under a gatehouse's footprint (the towers reach 2 m below their base).
                    var tx = -e.InZ;
                    var tz = e.InX;
                    var g0 = plan.HeightAt(e.Gate.X, e.Gate.Z);
                    var span = 0f;
                    if (e.Walled)
                        foreach (var u in new[] { -9.25f, -4f, 0f, 4f, 9.25f })
                            foreach (var v in new[] { -2.75f, 0f, 2.75f })
                                span = Math.Max(span, Math.Abs(plan.HeightAt(e.Gate.X + tx * u + e.InX * v, e.Gate.Z + tz * u + e.InZ * v) - g0));

                    // Creases on the approach (outside, to the plan's edge): the change of grade between 2 m stretches, in degrees.
                    var crease = 0f;
                    var creaseAt = default(PlanPoint);
                    for (var i = gi; i + 4 < pts.Count; i += 2)
                    {
                        float H(PlanPoint p) => plan.HeightAt(p.X, p.Z);
                        if (SettlementTerrain.InWater(plan, pts[i + 2].X, pts[i + 2].Z, 4f)) continue; // a bridge's banks are its own
                        var g1 = Math.Atan2(H(pts[i + 2]) - H(pts[i]), 2.0);
                        var g2 = Math.Atan2(H(pts[i + 4]) - H(pts[i + 2]), 2.0);
                        var c0 = (float)(Math.Abs(g1 - g2) * 180 / Math.PI);
                        if (c0 > crease) { crease = c0; creaseAt = pts[i + 2]; }
                    }

                    if (crease > 12f) creases.Add($"{s.Name} {e.RoadKey} at {creaseAt} ({(float)Math.Sqrt(creaseAt.X * creaseAt.X + creaseAt.Z * creaseAt.Z):0} m from the centre, edge {plan.HalfExtent:0}): {crease:0.0}°");

                    worstTurn = Math.Max(worstTurn, turn);
                    worstSpan = Math.Max(worstSpan, span);
                    worstCrease = Math.Max(worstCrease, crease);
                    lines.Add($"{s.Name} {e.RoadKey}{(e.IsMain ? " (main)" : "")}: turn within 15 m {turn:0}°, ground under the gatehouse ±{span:0.00} m, sharpest crease on the approach {crease:0.0}°");
                    if (e.Walled && span >= 1.9f) uneven.Add(lines[lines.Count - 1]);
                    if (!e.Walled && SettlementEntrances.TurnAt(road.Line, e.Gate, 4f) >= 25f) sharp.Add(lines[lines.Count - 1] + $" (at the way in {SettlementEntrances.TurnAt(road.Line, e.Gate, 4f):0}°)"); // an unwalled way in is moved off a bend
                }

            TestContext.WriteLine("Sharp turns near a gate: " + string.Join(" | ", sharp));
            TestContext.WriteLine("Uneven ground under a gatehouse (over 1.9 m): " + string.Join(" | ", uneven));
            TestContext.WriteLine("Approach creases over 12°: " + string.Join(" | ", creases));
            Assert.That(sharp, Is.Empty, "Unwalled ways in stand on a straight stretch of their road (a wall gate is where the wall is).");
            Assert.That(creases, Is.Empty, "Approach roads, away from bridge banks, change grade by under 12° per 2 m.");
            TestContext.WriteLine($"{gates} entrances: sharpest turn near a gate {worstTurn:0}°, ground under a gatehouse ±{worstSpan:0.00} m, sharpest approach crease {worstCrease:0.0}°.\n" + string.Join("\n", lines.Take(30)));
        }

        private static List<PlanPoint> Resample(List<PlanPoint> line, float step)
        {
            var list = new List<PlanPoint> { line[0] };
            var carry = 0f;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                var len = a.DistanceTo(b);
                var t = step - carry;
                while (t <= len)
                {
                    list.Add(new PlanPoint(a.X + (b.X - a.X) * t / len, a.Z + (b.Z - a.Z) * t / len));
                    t += step;
                }

                carry = len - (t - step);
            }

            return list;
        }

        [Test]
        public void Entrances_AreDeterministic_FallBackInRank_AndFollowTheWayCome()
        {
            var (s, plan) = Plans().First(x => x.plan.HasWalls && SettlementEntrances.Of(x.plan).Count >= 2);
            var a = SettlementEntrances.Of(plan);
            var b = SettlementEntrances.Of(plan.Clone());
            Assert.That(b.Select(e => e.Arrival.ToString() + e.ArrivalYaw + e.RoadKey), Is.EqualTo(a.Select(e => e.Arrival.ToString() + e.ArrivalYaw + e.RoadKey)), "Same plan, same entrances.");

            // From the bearing of the second gate: in by that gate.
            var second = a[1];
            var (_, _, by, _) = SettlementEntrances.Arrival(plan, second.Angle);
            Assert.That(by.RoadKey, Is.EqualTo(second.RoadKey), "In by the gate facing the way the traveller came.");

            // The main gate's standing points blocked (a cart-sized building on its road just inside): the next gate, never a generic point.
            var blocked = plan.Clone();
            foreach (var d in new[] { 9f, 11f, 14f, 18f, 7f })
            {
                var p = new PlanPoint(a[0].Gate.X + a[0].InX * d, a[0].Gate.Z + a[0].InZ * d);
                blocked.Buildings.Add(new BuildingData { Key = "block" + d, Center = p, Width = 6f, Depth = 6f, Function = BuildingFunction.Warehouse });
            }

            var (_, _, by2, fb2) = SettlementEntrances.Arrival(blocked);
            Assert.That(fb2, Is.False);
            Assert.That(by2.IsMain, Is.False, "The main gate blocked: the next entrance in rank.");
            Assert.That(by2.RoadKey, Is.EqualTo(SettlementEntrances.Of(blocked).First(e => e.ArrivalValid).RoadKey));

            // Every entrance blocked: the plan's own edge point, reported as a fallback.
            var sealedPlan = plan.Clone();
            foreach (var e in a)
                foreach (var d in new[] { 9f, 11f, 14f, 18f, 7f })
                    sealedPlan.Buildings.Add(new BuildingData { Key = "x" + e.RoadKey + d, Center = new PlanPoint(e.Gate.X + e.InX * d, e.Gate.Z + e.InZ * d), Width = 6f, Depth = 6f, Function = BuildingFunction.Warehouse });
            var (at3, _, _, fb3) = SettlementEntrances.Arrival(sealedPlan);
            Assert.That(fb3, Is.True);
            Assert.That(at3, Is.EqualTo(plan.Arrival), "Reported, and the plan's own road point, not an invented one.");
        }

        [Test]
        public void EnteringFromTheMap_AgainAndAfterASave_LandsAtTheFrontGate()
        {
            var home = Realmkeeper.Simulation.Atlas.SimulationTiers.PlayersRegion(W);
            if (W.Player.IsInSettlement) Ok(S.Dispatch(new ExitSettlementCommand()));
            var targets = W.Settlements.Where(x => x.RegionId == home).OrderByDescending(x => x.Population).ToList();
            Assume.That(targets.Count, Is.GreaterThanOrEqualTo(2));
            foreach (var s in new[] { targets.First(), targets.Last() })
            {
                Ok(S.Dispatch(new EnterSettlementCommand(s.SettlementId)));
                var plan = W.FindLayout(s.SettlementId);
                var main = SettlementEntrances.Main(plan);
                var pose = W.Player.Pose;
                Assert.That(Math.Abs(pose.X - main.Arrival.X) + Math.Abs(pose.Z - main.Arrival.Z), Is.LessThan(0.01f), $"{s.Name} ({s.SettlementType}): at its front gate.");
                Assert.That(Math.Abs(pose.Y - plan.HeightAt(pose.X, pose.Z)), Is.LessThan(0.01f), "On the ground.");
                Assert.That(pose.Yaw, Is.EqualTo(main.ArrivalYaw).Within(0.01f));
                var layout = ExplorationModule.LayoutFor(W, s);
                Assert.That(layout.ArrivalIsFallback, Is.False);
                Assert.That(Math.Abs(layout.Gate.X - main.Posts[0].X) + Math.Abs(layout.Gate.Z - main.Posts[0].Z), Is.LessThan(0.01f), "The gate guards' post is at the gate.");

                // Saved and loaded: the same place.
                Ok(S.Execute(new Realmkeeper.Application.SaveWorldCommand("p28")));
                Ok(S.Execute(new Realmkeeper.Application.LoadWorldCommand("p28")));
                Assert.That(W.Player.Pose.X, Is.EqualTo(pose.X).Within(0.001f));
                Assert.That(W.Player.SettlementId, Is.EqualTo(s.SettlementId));

                // Out and in again: the same front gate.
                Ok(S.Dispatch(new ExitSettlementCommand()));
                Ok(S.Dispatch(new EnterSettlementCommand(s.SettlementId)));
                Assert.That(Math.Abs(W.Player.Pose.X - pose.X) + Math.Abs(W.Player.Pose.Z - pose.Z), Is.LessThan(0.001f), "Repeated entry: the same place.");
                Ok(S.Dispatch(new ExitSettlementCommand()));
            }
        }

        [Test]
        public void TheCountry_TheWayInLiesOnTheRoadToTheMainGate_AndLeadsBackToIt()
        {
            var home = Realmkeeper.Simulation.Atlas.SimulationTiers.PlayersRegion(W);
            var s = W.Settlements.Where(x => x.RegionId == home && W.FindLayout(x.SettlementId) != null).OrderByDescending(x => x.Population).First();
            if (W.Player.IsInSettlement) Ok(S.Dispatch(new ExitSettlementCommand()));
            Ok(S.Dispatch(new EnterOverlandCommand(s.SettlementId)));
            var t = S.GetOverlandTerrain(RealmIds.Surface, home);
            var gate = OverlandQueries.GateOf(W, t, s);
            Assert.That(Math.Abs(W.Player.Pose.X - gate.x) + Math.Abs(W.Player.Pose.Z - gate.z), Is.LessThan(0.01f), "Out by the settlement's way in.");

            var plan = W.FindLayout(s.SettlementId);
            var (cx, cz) = OverlandScale.CellCentre(s.Position);
            var bearing = Math.Atan2(gate.z - cz, gate.x - cx);
            var main = SettlementEntrances.Main(plan);
            var gap = Math.Abs(bearing - main.Angle) % (Math.PI * 2);
            if (gap > Math.PI) gap = Math.PI * 2 - gap;
            TestContext.WriteLine($"{s.Name}: the way in at bearing {bearing * 180 / Math.PI:0}°, the main gate at {main.Angle * 180 / Math.PI:0}°.");
            Assert.That(gap, Is.LessThan(Math.PI / 4), "The way in from the country lies on the road that leads to the main gate.");

            Ok(S.Dispatch(new EnterSettlementFromOverlandCommand(s.SettlementId)));
            var pose = W.Player.Pose;
            Assert.That(Math.Abs(pose.X - main.Arrival.X) + Math.Abs(pose.Z - main.Arrival.Z), Is.LessThan(0.01f), "From the country, by that road: in at the main gate.");
        }

        [Test]
        public void TheWatch_MansTheMainGateFirst_BesideTheRoad_AndGrowsWithStatus()
        {
            var (s, layout) = W.Settlements.Select(x => (x, ExplorationModule.LayoutFor(W, x))).Where(x => x.Item2.Plan != null && x.Item2.HasWalls).OrderByDescending(x => x.x.Population).First();
            var input = new TownsfolkInput { Population = s.Population, Type = s.SettlementType, Prosperity = s.Prosperity, MinuteOfDay = 11 * 60, Seed = 5UL };
            var specs = TownsfolkPlanner.Plan(layout, input);
            var posts = specs.Where(x => x.Activity == TownsfolkActivity.Guarding).ToList();
            var main = layout.Entrances.First(e => e.IsMain);
            // A post is manned by the unnamed watch, or by a named gate guard standing at it (never both).
            bool Watch(Realmkeeper.Domain.Settlements.PlanPoint p, IEnumerable<TownsfolkSpec> by) => by.Any(g => Math.Abs(g.Route[0].X - p.X) + Math.Abs(g.Route[0].Z - p.Z) < 0.01f);
            bool Named(Realmkeeper.Domain.Settlements.PlanPoint p) => layout.Residents.Any(r => r.SiteKey == SettlementSites.Gate && Math.Abs(r.Position.X - p.X) + Math.Abs(r.Position.Z - p.Z) < 1.0f);
            var named = layout.NamedAtGate;
            TestContext.WriteLine($"{s.Name}: {named} named at the gate; {posts.Count} unnamed at posts; posts at the main gate {main.Posts.Length}.");
            Assert.That(posts.Count + Math.Min(named, 2), Is.GreaterThanOrEqualTo(2), "A walled town keeps its main gate's pair.");
            foreach (var p in main.Posts)
            {
                Assert.That(Watch(p, posts) || Named(p), Is.True, "Both main-gate posts manned.");
                Assert.That(Watch(p, posts) && Named(p), Is.False, "Never two to a post.");
            }

            Assert.That(posts.All(g => g.Facing.HasValue), Is.True, "Each faces out of its gate.");
            Assert.That(specs.Select(x => x.Key).Distinct().Count(), Is.EqualTo(specs.Count), "No one twice.");

            var night = TownsfolkPlanner.Plan(layout, new TownsfolkInput { Population = s.Population, Type = s.SettlementType, Prosperity = s.Prosperity, MinuteOfDay = 23 * 60, Seed = 5UL });
            var nightPosts = night.Where(x => x.Activity == TownsfolkActivity.Guarding).ToList();
            Assert.That(nightPosts.Count + Math.Min(named, 2), Is.EqualTo(2), "By night only the main gate is held.");
            Assert.That(nightPosts.All(g => main.Posts.Any(p => Math.Abs(g.Route[0].X - p.X) + Math.Abs(g.Route[0].Z - p.Z) < 0.01f)), Is.True, "At the main gate.");

            // Status: the same numbers as a city and a fortress keep a larger watch than a town.
            int W2(SettlementType t) => TownsfolkPlanner.Watch(new TownsfolkInput { Population = 1500, Type = t, MinuteOfDay = 720 });
            Assert.That(W2(SettlementType.City), Is.GreaterThan(W2(SettlementType.Town)));
            Assert.That(W2(SettlementType.Fortress), Is.GreaterThan(W2(SettlementType.City)));
            Assert.That(TownsfolkPlanner.Watch(new TownsfolkInput { Population = 60, Type = SettlementType.Hamlet, MinuteOfDay = 720 }), Is.EqualTo(0), "A hamlet keeps no watch.");
        }
    }
}
