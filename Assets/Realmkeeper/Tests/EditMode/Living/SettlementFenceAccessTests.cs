using System;
using System.Collections.Generic;
using NUnit.Framework;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Living
{
    /// <summary>
    /// Owner report (2026-11-14): "the fence blocks me in when starting this new world and there's no gates". Every
    /// fence a plan raises must leave the traveller free: the arrival is never inside a fenced field, a fence never
    /// stands across a road, and from the arrival the square and every field's gate can be walked to.
    /// </summary>
    public sealed class SettlementFenceAccessTests
    {
        private const float Cell = 0.5f;

        private static IEnumerable<SettlementLayoutData> Plans()
        {
            foreach (var seed in new long[] { 123456789, 42, 777, 2026, 90210, 31337 })
            {
                var g = Phase2Worlds.Generate(seed);
                foreach (var s in g.World.Settlements) yield return SettlementLayoutGenerator.Generate(g.World, g.Map, s);
            }
        }

        private static (float x, float z) ToLocal(PlanPoint c, float yaw, float x, float z)
        {
            var r = yaw * Math.PI / 180.0;
            var cos = (float)Math.Cos(r);
            var sin = (float)Math.Sin(r);
            var dx = x - c.X;
            var dz = z - c.Z;
            return (dx * cos + dz * sin, -dx * sin + dz * cos);
        }

        private static bool InsideField(FieldData f, float x, float z, float margin)
        {
            var (lx, lz) = ToLocal(f.Center, f.Yaw, x, z);
            return Math.Abs(lx) < f.Width / 2f + margin && Math.Abs(lz) < f.Depth / 2f + margin;
        }

        [Test]
        public void Arrival_IsNeverInsideAFencedField()
        {
            foreach (var p in Plans())
                foreach (var f in p.Fields)
                    if (f.Fenced)
                        Assert.That(InsideField(f, p.Arrival.X, p.Arrival.Z, 1.5f), Is.False, p.SettlementId + ": the arrival is inside " + f.Key);
        }

        [Test]
        public void Fences_NeverStandAcrossARoad()
        {
            foreach (var p in Plans())
                foreach (var f in p.Fields)
                {
                    if (!f.Fenced) continue;
                    foreach (var r in p.Roads)
                    {
                        if (r.Kind == RoadKind.FarmTrack && r.Key.StartsWith(f.OwnerKey ?? "\u0000", StringComparison.Ordinal)) continue;
                        for (var i = 0; i + 1 < r.Line.Count; i++)
                        {
                            var a = r.Line[i];
                            var b = r.Line[i + 1];
                            var len = a.DistanceTo(b);
                            for (var t = 0f; t <= len; t += 0.5f)
                            {
                                var x = a.X + (b.X - a.X) * (len > 0 ? t / len : 0);
                                var z = a.Z + (b.Z - a.Z) * (len > 0 ? t / len : 0);
                                Assert.That(InsideField(f, x, z, r.Width / 2f), Is.False, p.SettlementId + ": " + r.Key + " runs through the fence of " + f.Key);
                            }
                        }
                    }
                }
        }

        [Test]
        public void FromTheArrival_TheSquareAndEveryGate_CanBeWalkedTo()
        {
            foreach (var p in Plans())
            {
                var n = (int)Math.Ceiling(p.HalfExtent * 2f / Cell);
                var blocked = new bool[n, n];
                int Ix(float v) => Math.Max(0, Math.Min(n - 1, (int)((v + p.HalfExtent) / Cell)));
                void Line(PlanPoint a, PlanPoint b)
                {
                    var len = a.DistanceTo(b);
                    for (var t = 0f; t <= len; t += Cell / 3f)
                    {
                        var k = len > 0 ? t / len : 0;
                        blocked[Ix(a.X + (b.X - a.X) * k), Ix(a.Z + (b.Z - a.Z) * k)] = true;
                    }
                }

                foreach (var b in p.Buildings)
                {
                    if (b.Function == BuildingFunction.Gatehouse) continue;
                    for (var i = 0; i < n; i++)
                        for (var j = 0; j < n; j++)
                        {
                            var x = -p.HalfExtent + (i + 0.5f) * Cell;
                            var z = -p.HalfExtent + (j + 0.5f) * Cell;
                            var (lx, lz) = ToLocal(b.Center, b.Yaw, x, z);
                            if (Math.Abs(lx) < b.Width / 2f && Math.Abs(lz) < b.Depth / 2f) blocked[i, j] = true;
                        }
                }

                foreach (var f in p.Fields)
                {
                    if (!f.Fenced) continue;
                    PlanPoint L(float lx, float lz) => SettlementLayoutGenerator.Local(f.Center, f.Yaw, lx, lz);
                    float hw = f.Width / 2f, hd = f.Depth / 2f;
                    Line(L(-hw, -hd), L(-1.6f, -hd));
                    Line(L(1.6f, -hd), L(hw, -hd));
                    Line(L(hw, -hd), L(hw, hd));
                    Line(L(hw, hd), L(-hw, hd));
                    Line(L(-hw, hd), L(-hw, -hd));
                }

                var seen = new bool[n, n];
                var queue = new Queue<(int, int)>();
                var start = (Ix(p.Arrival.X), Ix(p.Arrival.Z));
                Assert.That(blocked[start.Item1, start.Item2], Is.False, p.SettlementId + ": the arrival stands on a fence or a building");
                seen[start.Item1, start.Item2] = true;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var (i, j) = queue.Dequeue();
                    foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= n || b >= n || seen[a, b] || blocked[a, b]) continue;
                        seen[a, b] = true;
                        queue.Enqueue((a, b));
                    }
                }

                Assert.That(seen[Ix(0f), Ix(0f)] || blocked[Ix(0f), Ix(0f)], Is.True, p.SettlementId + ": the square cannot be reached from the arrival");
                foreach (var f in p.Fields)
                {
                    if (!f.Fenced) continue;
                    var inside = SettlementLayoutGenerator.Local(f.Center, f.Yaw, 0f, -f.Depth / 2f + 1f);
                    var outside = SettlementLayoutGenerator.Local(f.Center, f.Yaw, 0f, -f.Depth / 2f - 1f);
                    Assert.That(seen[Ix(outside.X), Ix(outside.Z)], Is.True, p.SettlementId + ": the gate of " + f.Key + " cannot be walked to from the arrival");
                    Assert.That(seen[Ix(inside.X), Ix(inside.Z)], Is.True, p.SettlementId + ": the gate of " + f.Key + " does not open into it");
                }
            }
        }
    }
}
