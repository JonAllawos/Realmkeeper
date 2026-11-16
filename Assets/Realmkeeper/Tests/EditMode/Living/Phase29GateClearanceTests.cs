using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Living
{
    /// <summary>
    /// Phase 29 (ADR-0121): the owner found an open-looking gate with stone across the way out. Every gate of every walled
    /// plan in seven worlds is walked along its own road, 14 m either side of the wall, by a player-sized circle at every
    /// lateral offset the road allows: no tower and no wall run may touch it. The pre-Phase-29 geometry (gatehouse facing the
    /// centre, a fixed cut along the wall) is measured the same way and reported, as the root-cause record.
    /// </summary>
    public sealed class Phase29GateClearanceTests
    {
        private const float Walker = 0.3f;
        private const float Reach = 14f;

        private static IEnumerable<(SettlementData s, SettlementLayoutData plan)> WalledPlans() => WalledPlansWithV2().Select(x => (x.s, x.plan));

        /// <summary>Each walled plan as generator v3 makes it, with the same plan as v2 made it (what older saves hold).</summary>
        private static IEnumerable<(SettlementData s, SettlementLayoutData plan, SettlementLayoutData v2)> WalledPlansWithV2()
        {
            // Generated worlds hold towns, villages and hamlets only (RegionGenerator.SiteTypes); each town is also planned as
            // a city and as a fortress, so their larger gatehouses are checked on real ground and roads.
            foreach (var seed in new long[] { 123456789, 42, 777, 2026, 90210, 31337, 5150, 8675309, 1999, 4242 })
            {
                var g = Phase2Worlds.Generate(seed);
                foreach (var s in g.World.Settlements)
                {
                    var plan = SettlementLayoutGenerator.Generate(g.World, g.Map, s);
                    if (plan.HasWalls && plan.Wall.Count >= 3) yield return (s, plan, SettlementLayoutGenerator.Generate(g.World, g.Map, s, false));
                    if (s.SettlementType != SettlementType.Town) continue;
                    var was = s.SettlementType;
                    foreach (var standing in new[] { SettlementType.City, SettlementType.Fortress })
                    {
                        s.SettlementType = standing;
                        var bigger = SettlementLayoutGenerator.Generate(g.World, g.Map, s);
                        var biggerV2 = SettlementLayoutGenerator.Generate(g.World, g.Map, s, false);
                        var copy = new SettlementData { SettlementId = s.SettlementId, Name = s.Name + " as " + standing, SettlementType = standing };
                        s.SettlementType = was;
                        if (bigger.HasWalls && bigger.Wall.Count >= 3) yield return (copy, bigger, biggerV2);
                    }
                }
            }
        }

        /// <summary>Points along a road line at arc distance <paramref name="s"/> from the point nearest (x, z), with the direction there.</summary>
        private static IEnumerable<(float x, float z, float dx, float dz)> Along(List<PlanPoint> line, float x, float z, float reach, float step)
        {
            // Arc length of the nearest point.
            var best = float.MaxValue;
            var at = 0f;
            var acc = 0f;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                var len = (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                var d = SettlementTerrain.SegmentDistance(a.X, a.Z, b.X, b.Z, x, z, out var px, out var pz);
                if (d < best) { best = d; at = acc + (float)Math.Sqrt((px - a.X) * (px - a.X) + (pz - a.Z) * (pz - a.Z)); }
                acc += len;
            }

            for (var s = Math.Max(0f, at - reach); s <= Math.Min(acc, at + reach); s += step)
            {
                var run = 0f;
                for (var i = 0; i + 1 < line.Count; i++)
                {
                    var a = line[i];
                    var b = line[i + 1];
                    var len = (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                    if (len < 1e-4f) continue;
                    if (s <= run + len || i + 2 == line.Count)
                    {
                        var t = Math.Min(1f, (s - run) / len);
                        yield return (a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t, (b.X - a.X) / len, (b.Z - a.Z) / len);
                        break;
                    }

                    run += len;
                }
            }
        }

        private static RoadData RoadAt(SettlementLayoutData plan, float x, float z)
        {
            RoadData best = null;
            var d = 4f;
            foreach (var r in plan.Roads)
                for (var i = 0; i + 1 < r.Line.Count; i++)
                {
                    var e = SettlementTerrain.SegmentDistance(r.Line[i].X, r.Line[i].Z, r.Line[i + 1].X, r.Line[i + 1].Z, x, z, out _, out _);
                    if (e < d) { d = e; best = r; }
                }

            return best;
        }

        /// <summary>What blocks a walker on the road through (x, z), or null when the way is clear.</summary>
        private static string Blocked(SettlementLayoutData plan, float x, float z, float halfWay, List<(PlanBox box, string kind)> solids)
        {
            var road = RoadAt(plan, x, z);
            if (road == null) return null;
            var lateral = Math.Max(0f, Math.Min(road.Width / 2f, halfWay) - Walker - 0.2f);
            foreach (var (px, pz, dx, dz) in Along(road.Line, x, z, Reach, 0.5f))
                for (var o = -lateral; o <= lateral + 1e-3f; o += 0.5f)
                {
                    var wx = px + dz * o;
                    var wz = pz - dx * o;
                    foreach (var (b, kind) in solids)
                        if (b.Touches(wx, wz, Walker)) return $"{kind} at ({wx:0.0},{wz:0.0}), {Math.Sqrt((wx - x) * (wx - x) + (wz - z) * (wz - z)):0.0} m from the gate, {o:0.0} m off the road's middle, the road heading {Math.Atan2(dx, dz) * 180.0 / Math.PI:0}° there";
                }

            return null;
        }

        [Test]
        public void EveryGate_IsClearAlongItsRoad_ThroughTheWholeWall_AtEveryStanding()
        {
            int gates = 0, onRoads = 0, before = 0, cities = 0, fortresses = 0;
            var failures = new List<string>();
            var beforeExamples = new List<string>();
            var skipped = new List<string>();
            foreach (var (s, plan, v2) in WalledPlansWithV2())
            {
                var why2 = new List<string>();
                SettlementWalls.StraightenGateRoads(v2.Clone(), why2);
                skipped.AddRange(why2.Select(w => s.Name + ": " + w));
                if (s.SettlementType == SettlementType.City) cities++;
                if (s.SettlementType == SettlementType.Fortress) fortresses++;
                var (runs, _) = SettlementWalls.Runs(plan, s.SettlementType);
                var built = SettlementWalls.Gates(plan, s.SettlementType);
                var solids = runs.Select(r => (SettlementWalls.Footprint(r), "the wall")).Concat(built.SelectMany(g => g.Towers()).Select(t => (t, "a gatehouse tower"))).ToList();
                foreach (var g in built)
                {
                    gates++;
                    if (RoadAt(plan, g.X, g.Z) != null) onRoads++;
                    var why = Blocked(plan, g.X, g.Z, g.Opening / 2f, solids);
                    if (why != null) failures.Add($"{s.Name} ({s.SettlementType}) gate {g.Key} at ({g.X:0.0},{g.Z:0.0}), passage {g.Yaw:0}°, opening {g.Opening:0.0} m: {why}");

                    // The same gate as built before Phase 29 (root-cause record).
                    var old = Before(v2, s.SettlementType);
                    var (opening, _, _) = SettlementWalls.GateSpec(s.SettlementType);
                    var oldWhy = Blocked(v2, g.X, g.Z, opening / 2f, old);
                    if (oldWhy != null)
                    {
                        before++;
                        if (beforeExamples.Count < 6) beforeExamples.Add($"{s.Name} ({s.SettlementType}): {oldWhy}");
                    }
                }
            }

            TestContext.WriteLine($"Walled plans' gates: {gates} ({onRoads} on a road); cities {cities}, fortresses {fortresses}.");
            TestContext.WriteLine($"Before Phase 29: {before} of {gates} gates had stone across the road. " + string.Join(" | ", beforeExamples));
            TestContext.WriteLine("Gates left as they were by the straightening: " + (skipped.Count == 0 ? "none" : string.Join(" | ", skipped)));
            Assert.That(gates, Is.GreaterThan(20), "Enough gates to mean something.");
            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(12)));
        }

        [Test]
        public void EveryGatehouse_LiesAlongItsRoad_AndTheWallMeetsItsTowers()
        {
            foreach (var (s, plan) in WalledPlans())
            {
                var (runs, _) = SettlementWalls.Runs(plan, s.SettlementType);
                foreach (var g in SettlementWalls.Gates(plan, s.SettlementType))
                {
                    var (dx, dz, width) = SettlementWalls.RoadThrough(plan, g.X, g.Z);
                    if (width <= 0f) continue;
                    Assert.That(Math.Abs(dx * g.DirX + dz * g.DirZ), Is.GreaterThan(0.999f), $"{s.Name}: the passage runs along the road.");
                    Assert.That(g.Opening, Is.GreaterThanOrEqualTo(width + 1.2f - 1e-3f), $"{s.Name}: the opening takes the road's width.");
                    Assert.That(g.DirX * -g.X + g.DirZ * -g.Z, Is.GreaterThan(0f), $"{s.Name}: the passage points inward.");

                    // No gap a person could slip through beside a tower: the nearest stone run on each side of the gate,
                    // along the wall, reaches into or within a walker's width of a tower.
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var towers = g.Towers().ToList();
                        var near = runs.Select(SettlementWalls.Footprint).Where(f => Math.Sqrt((f.CX - g.X) * (f.CX - g.X) + (f.CZ - g.Z) * (f.CZ - g.Z)) < 60f).ToList();
                        if (near.Count == 0) continue; // water stands in for the wall there
                        var sx = g.X + g.WallX * side * (g.Opening / 2f + g.Tower + 1.5f);
                        var sz = g.Z + g.WallZ * side * (g.Opening / 2f + g.Tower + 1.5f);
                        var stone = near.Any(f => f.Touches(sx, sz, 1.5f)) || towers.Any(t => t.Touches(sx, sz, 1.5f));
                        var water = SettlementTerrain.InWater(plan, sx, sz, 2f);
                        Assert.That(stone || water, Is.True, $"{s.Name} ({s.SettlementType}) gate {g.Key}: wall or tower beside the gate on side {side}.");
                    }
                }
            }
        }

        [Test]
        public void APlanSavedBeforePhase29_IsStraightenedOnLoad_ExactlyAsANewOneIsGenerated()
        {
            foreach (var seed in new long[] { 123456789, 42 })
            {
                var g = Phase2Worlds.Generate(seed);
                var w = g.World;
                w.SettlementLayouts.Clear();
                foreach (var s in w.Settlements) w.SettlementLayouts.Add(SettlementLayoutGenerator.Generate(w, g.Map, s, false)); // as v2 saved them
                Assert.That(w.SettlementLayouts.All(p => p.GeneratorVersion == 2), Is.True);

                var loaded = Realmkeeper.Infrastructure.Persistence.WorldSaveMapper.FromDto(Realmkeeper.Infrastructure.Persistence.WorldSaveMapper.ToDto(w));
                foreach (var s in w.Settlements)
                {
                    var after = loaded.FindLayout(s.SettlementId);
                    var fresh = SettlementLayoutGenerator.Generate(w, g.Map, s);
                    Assert.That(after.GeneratorVersion, Is.EqualTo(3), s.Name);
                    Assert.That(after.Roads.Count, Is.EqualTo(fresh.Roads.Count), s.Name);
                    for (var i = 0; i < fresh.Roads.Count; i++)
                    {
                        Assert.That(after.Roads[i].Line.Count, Is.EqualTo(fresh.Roads[i].Line.Count), $"{s.Name} {fresh.Roads[i].Key}");
                        for (var k = 0; k < fresh.Roads[i].Line.Count; k++)
                        {
                            Assert.That(after.Roads[i].Line[k].X, Is.EqualTo(fresh.Roads[i].Line[k].X).Within(1e-3f));
                            Assert.That(after.Roads[i].Line[k].Z, Is.EqualTo(fresh.Roads[i].Line[k].Z).Within(1e-3f));
                        }
                    }

                    // Loading again changes nothing (v3 is not straightened twice).
                    var again = Realmkeeper.Infrastructure.Persistence.WorldSaveMapper.FromDto(Realmkeeper.Infrastructure.Persistence.WorldSaveMapper.ToDto(loaded)).FindLayout(s.SettlementId);
                    Assert.That(again.Roads.SelectMany(r => r.Line).Count(), Is.EqualTo(after.Roads.SelectMany(r => r.Line).Count()), s.Name);
                }
            }
        }

        [Test]
        public void StraighteningTwice_ChangesNothing_AndLeavesUnwalledPlansAlone()
        {
            foreach (var (s, plan) in WalledPlans().Take(8))
            {
                var copy = plan.Clone();
                Assert.That(SettlementWalls.StraightenGateRoads(copy), Is.EqualTo(0), s.Name + ": already straight");
            }

            var g = Phase2Worlds.Generate(42);
            foreach (var s in g.World.Settlements.Where(x => x.SettlementType != SettlementType.Town))
            {
                var v2 = SettlementLayoutGenerator.Generate(g.World, g.Map, s, false);
                Assert.That(SettlementWalls.StraightenGateRoads(v2), Is.EqualTo(0), s.Name + ": no wall, no change");
            }
        }

        [Test]
        public void TheWall_IsTheSame_ForTheSamePlan()
        {
            var (s, plan) = WalledPlans().First();
            var a = SettlementWalls.Runs(plan, s.SettlementType).runs;
            var b = SettlementWalls.Runs(plan, s.SettlementType).runs;
            Assert.That(a.Count, Is.EqualTo(b.Count));
            for (var i = 0; i < a.Count; i++) Assert.That((a[i].AX, a[i].AZ, a[i].BX, a[i].BZ), Is.EqualTo((b[i].AX, b[i].AZ, b[i].BX, b[i].BZ)));
        }

        /// <summary>The pre-Phase-29 solids: gatehouses facing the centre with the standing's opening; a fixed cut along the wall.</summary>
        private static List<(PlanBox, string)> Before(SettlementLayoutData plan, SettlementType type)
        {
            var (opening, tower, _) = SettlementWalls.GateSpec(type);
            var gateHalf = opening / 2f + tower - 0.4f;
            var solids = new List<(PlanBox, string)>();
            foreach (var b in plan.Buildings.Where(b => b.Function == BuildingFunction.Gatehouse))
            {
                var len = (float)Math.Max(1e-3, Math.Sqrt(b.Center.X * b.Center.X + b.Center.Z * b.Center.Z));
                var ax = -b.Center.Z / len; // across the radial passage
                var az = b.Center.X / len;
                foreach (var side in new[] { -1f, 1f })
                    solids.Add((new PlanBox(b.Center.X + ax * side * (opening / 2f + tower / 2f), b.Center.Z + az * side * (opening / 2f + tower / 2f), tower / 2f, tower / 2f, ax, az), "a gatehouse tower"));
            }

            for (var i = 0; i < plan.Wall.Count; i++)
            {
                var a = plan.Wall[i];
                var c = plan.Wall[(i + 1) % plan.Wall.Count];
                var len = (float)Math.Sqrt((c.X - a.X) * (c.X - a.X) + (c.Z - a.Z) * (c.Z - a.Z));
                if (len < 0.5f) continue;
                var dx = (c.X - a.X) / len;
                var dz = (c.Z - a.Z) / len;
                var cuts = new List<(float from, float to)>();
                foreach (var g in plan.Gates)
                {
                    var rx = g.X - a.X;
                    var rz = g.Z - a.Z;
                    var along = rx * dx + rz * dz;
                    var off = Math.Abs(rx * dz - rz * dx);
                    if (off < 6f && along > -gateHalf && along < len + gateHalf) cuts.Add((along - gateHalf, along + gateHalf));
                }

                foreach (var (at, width, cos) in SettlementWalls.RoadCrossings(plan, a, c))
                {
                    var along = at * len;
                    if (cuts.Any(k => along > k.from - 1f && along < k.to + 1f)) continue;
                    var half = (width / 2f + 0.9f) / Math.Max(0.35f, (float)Math.Sqrt(Math.Max(0f, 1f - cos * cos)));
                    cuts.Add((along - half, along + half));
                }

                cuts.Sort((x, y) => x.from.CompareTo(y.from));
                var start = 0f;
                cuts.Add((len, len));
                foreach (var k in cuts)
                {
                    var to = Math.Min(len, k.from);
                    if (to - start >= 0.6f)
                    {
                        var r = new WallRun(a.X + dx * start, a.Z + dz * start, a.X + dx * to, a.Z + dz * to);
                        if (!SettlementTerrain.InWater(plan, (r.AX + r.BX) / 2f, (r.AZ + r.BZ) / 2f, 1f)) solids.Add((SettlementWalls.Footprint(r), "the wall"));
                    }

                    start = Math.Max(start, k.to);
                }
            }

            return solids;
        }
    }
}
