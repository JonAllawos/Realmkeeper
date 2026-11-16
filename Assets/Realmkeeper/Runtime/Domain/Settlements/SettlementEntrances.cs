using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.Settlements
{
    /// <summary>One way into a settlement: where a main road passes its walls (a gate) or enters its built core (a way in).</summary>
    public sealed class SettlementEntrance
    {
        public string RoadKey { get; set; }

        /// <summary>The neighbouring settlement the road leads to, or empty for a track.</summary>
        public SettlementId Toward { get; set; }

        /// <summary>True for a gate in the town wall; false for an unwalled way in.</summary>
        public bool Walled { get; set; }

        /// <summary>The settlement's front gate: the plan's own first main road (its <c>Gates[0]</c>, the "town gate" landmark).</summary>
        public bool IsMain { get; set; }

        /// <summary>0 for the main entrance, then in plan order: the deterministic order of fallbacks.</summary>
        public int Rank { get; set; }

        /// <summary>Where the road crosses the wall line (or the core's edge).</summary>
        public PlanPoint Gate { get; set; }

        /// <summary>The unit direction along the road into the settlement, at the gate.</summary>
        public float InX { get; set; }
        public float InZ { get; set; }

        public float RoadWidth { get; set; }

        /// <summary>The bearing of the gate from the centre (radians, atan2(z, x)): the frame the plan's exits were laid out in.</summary>
        public float Angle { get; set; }

        /// <summary>Where an arriving traveller stands (on the road, just inside) and faces (degrees, Unity yaw).</summary>
        public PlanPoint Arrival { get; set; }
        public float ArrivalYaw { get; set; }

        /// <summary>False when no clear standing point was found inside this entrance (another entrance is used instead).</summary>
        public bool ArrivalValid { get; set; }

        /// <summary>Two watch posts, one either side of the road just inside, clear of the roadway; and the way they face (out).</summary>
        public PlanPoint[] Posts { get; set; } = new PlanPoint[0];
        public float PostYaw { get; set; }
    }

    /// <summary>
    /// Phase 28 (ADR-0120): a settlement's entrances, derived from its persisted plan and nothing else, so the arrival, the
    /// watch's posts, the gate guards, the gatehouses and the way in from the country all agree, for old saves and new.
    /// The main entrance is the plan's own front gate (its first main road); nothing is invented beside the plan.
    /// </summary>
    public static class SettlementEntrances
    {
        /// <summary>How far inside a wall gate the traveller stands (clear of the gatehouse and its open leaves).</summary>
        public const float GateInset = 11f;

        /// <summary>How far inside an unwalled way in.</summary>
        public const float WayInset = 4f;

        public static List<SettlementEntrance> Of(SettlementLayoutData plan)
        {
            var list = new List<SettlementEntrance>();
            if (plan == null) return list;
            var radius = plan.CoreRadius > 1f ? plan.CoreRadius : plan.HalfExtent * 0.5f;
            foreach (var road in plan.Roads)
            {
                if (road.Kind != RoadKind.Main || road.Line.Count < 2) continue;
                if (!Crossing(road.Line, radius, out var seg, out var gate)) continue;
                // An unwalled way in is not fixed by a wall: off a bend, onto the nearest straight stretch of its road.
                if (!plan.HasWalls) Straighten(plan, road.Line, ref seg, ref gate);
                var e = new SettlementEntrance
                {
                    RoadKey = road.Key, Toward = road.Toward, Walled = plan.HasWalls, IsMain = list.Count == 0, Rank = list.Count,
                    Gate = gate, RoadWidth = road.Width, Angle = (float)Math.Atan2(gate.Z, gate.X),
                };
                var (_, ix, iz) = Back(road.Line, seg, gate, 0.5f);
                e.InX = ix;
                e.InZ = iz;
                StandingPoint(plan, road, seg, e);
                Posts(plan, road, seg, e);
                list.Add(e);
            }

            return list;
        }

        /// <summary>
        /// Where a traveller arrives: just inside the main entrance; or, coming from <paramref name="fromAngle"/> (radians, the
        /// bearing they came from), the entrance facing that way. An entrance with no clear standing point gives way to the next
        /// in rank. <c>fallback</c> is true only when no entrance had one and the plan's own edge arrival point was used.
        /// </summary>
        public static (PlanPoint at, float yaw, SettlementEntrance by, bool fallback) Arrival(SettlementLayoutData plan, float? fromAngle = null)
        {
            var all = Of(plan);
            SettlementEntrance best = null;
            if (fromAngle.HasValue)
            {
                var gap = float.MaxValue;
                foreach (var e in all)
                {
                    if (!e.ArrivalValid) continue;
                    var g = AngleGap(e.Angle, fromAngle.Value);
                    if (g < gap) { gap = g; best = e; }
                }
            }

            if (best == null)
                foreach (var e in all)
                    if (e.ArrivalValid) { best = e; break; } // rank order: the main entrance first

            if (best != null) return (best.Arrival, best.ArrivalYaw, best, false);
            return (plan.Arrival, plan.ArrivalYaw, all.Count > 0 ? all[0] : null, true);
        }

        /// <summary>The bearing (radians, atan2(z, x)) of the main entrance from the centre, cheaply (no standing checks); null without one.</summary>
        public static float? MainBearing(SettlementLayoutData plan)
        {
            if (plan == null) return null;
            var radius = plan.CoreRadius > 1f ? plan.CoreRadius : plan.HalfExtent * 0.5f;
            foreach (var road in plan.Roads)
            {
                if (road.Kind != RoadKind.Main || road.Line.Count < 2) continue;
                if (Crossing(road.Line, radius, out _, out var gate)) return (float)Math.Atan2(gate.Z, gate.X);
            }

            return null;
        }

        public static SettlementEntrance Main(SettlementLayoutData plan)
        {
            foreach (var e in Of(plan)) if (e.IsMain) return e;
            return null;
        }

        /// <summary>True when a standing point is clear: on the plan, dry, outside every building and fenced field.</summary>
        public static bool Clear(SettlementLayoutData plan, PlanPoint p, float clearance)
        {
            if (Math.Abs(p.X) > plan.HalfExtent - 3f || Math.Abs(p.Z) > plan.HalfExtent - 3f) return false;
            if (SettlementTerrain.InWater(plan, p.X, p.Z, 1.5f)) return false;
            foreach (var b in plan.Buildings)
                if (InBox(p, b.Center, b.Width, b.Depth, b.Yaw, clearance)) return false;
            foreach (var f in plan.Fields)
                if (InBox(p, f.Center, f.Width, f.Depth, f.Yaw, 0.5f)) return false;
            return true;
        }

        // ----- Geometry ------------------------------------------------------------------------------------

        /// <summary>The sharpest turn the road makes within 6 m either side of the point <paramref name="along"/> metres down it (degrees).</summary>
        public static float TurnNear(List<PlanPoint> line, float along, float reach = 6f)
        {
            var worst = 0f;
            for (var d = along - reach + 1f; d <= along + reach - 1f; d += 1f)
            {
                var a = PointAt(line, d - 1f);
                var b = PointAt(line, d);
                var c = PointAt(line, d + 1f);
                var h1 = Math.Atan2(b.Z - a.Z, b.X - a.X);
                var h2 = Math.Atan2(c.Z - b.Z, c.X - b.X);
                var t = Math.Abs(h1 - h2) % (Math.PI * 2);
                if (t > Math.PI) t = Math.PI * 2 - t;
                worst = Math.Max(worst, (float)(t * 180 / Math.PI));
            }

            return worst;
        }

        /// <summary>The sharpest turn within <paramref name="reach"/> metres of the point of the road nearest <paramref name="p"/>.</summary>
        public static float TurnAt(List<PlanPoint> line, PlanPoint p, float reach = 4f)
        {
            var best = float.MaxValue;
            var at = 0f;
            var run = 0f;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                var len = a.DistanceTo(b);
                var dx = b.X - a.X;
                var dz = b.Z - a.Z;
                var t = len < 1e-4f ? 0f : Math.Max(0f, Math.Min(1f, ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / (len * len)));
                var q = new PlanPoint(a.X + dx * t, a.Z + dz * t);
                var d = q.DistanceTo(p);
                if (d < best) { best = d; at = run + len * t; }
                run += len;
            }

            return TurnNear(line, at, reach);
        }

        /// <summary>The distance down the road (from its start) of a point on segment <paramref name="seg"/>.</summary>
        private static float Along(List<PlanPoint> line, int seg, PlanPoint p)
        {
            var d = 0f;
            for (var i = 0; i < seg; i++) d += line[i].DistanceTo(line[i + 1]);
            return d + line[seg].DistanceTo(p);
        }

        /// <summary>The point <paramref name="along"/> metres down the road (clamped to its ends), and its segment.</summary>
        private static PlanPoint PointAt(List<PlanPoint> line, float along) => PointAt(line, along, out _);

        private static PlanPoint PointAt(List<PlanPoint> line, float along, out int seg)
        {
            if (along <= 0f) { seg = 0; return line[0]; }
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var len = line[i].DistanceTo(line[i + 1]);
                if (along <= len || i + 2 == line.Count)
                {
                    seg = i;
                    var t = len < 1e-4f ? 0f : Math.Min(1f, along / len);
                    return new PlanPoint(line[i].X + (line[i + 1].X - line[i].X) * t, line[i].Z + (line[i + 1].Z - line[i].Z) * t);
                }

                along -= len;
            }

            seg = line.Count - 2;
            return line[line.Count - 1];
        }

        /// <summary>Moves an unwalled way in along its road (up to 18 m in or 9 m out, nearest first) to where the road runs straight.</summary>
        private static void Straighten(SettlementLayoutData plan, List<PlanPoint> line, ref int seg, ref PlanPoint gate)
        {
            var at = Along(line, seg, gate);
            if (TurnNear(line, at, 4f) < 25f) return;
            foreach (var shift in new[] { -3f, -6f, -9f, -12f, -15f, -18f, 3f, 6f, 9f }) // inward (toward the road's start, the centre) first
            {
                var d = at + shift;
                var p = PointAt(line, d, out var s);
                var r = (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);
                if (r < plan.PlazaRadius + 12f || r > plan.HalfExtent - 20f) continue;
                if (TurnNear(line, d, 4f) >= 25f) continue;
                seg = s;
                gate = p;
                return;
            }
        }

        /// <summary>The first segment (from the centre outward) where the road crosses the circle of <paramref name="radius"/>.</summary>
        private static bool Crossing(List<PlanPoint> line, float radius, out int seg, out PlanPoint at)
        {
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                var ra = (float)Math.Sqrt(a.X * a.X + a.Z * a.Z);
                var rb = (float)Math.Sqrt(b.X * b.X + b.Z * b.Z);
                if ((ra - radius) * (rb - radius) > 0) continue;
                var t = (radius - ra) / Math.Max(0.001f, rb - ra); // the generator's own formula (SettlementLayoutGenerator.Walls)
                seg = i;
                at = new PlanPoint(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
                return true;
            }

            seg = -1;
            at = default;
            return false;
        }

        /// <summary>The point <paramref name="distance"/> metres back along the road from <paramref name="from"/> (on segment <paramref name="seg"/>) toward its start, and the inward direction there.</summary>
        private static (PlanPoint p, float dx, float dz) Back(List<PlanPoint> line, int seg, PlanPoint from, float distance)
        {
            var cur = from;
            var i = seg;
            var left = distance;
            var dx = line[seg].X - line[seg + 1].X;
            var dz = line[seg].Z - line[seg + 1].Z;
            while (true)
            {
                var target = line[i];
                var sx = target.X - cur.X;
                var sz = target.Z - cur.Z;
                var len = (float)Math.Sqrt(sx * sx + sz * sz);
                if (len > 1e-4f) { dx = sx / len; dz = sz / len; }
                if (len >= left || i == 0)
                {
                    var k = Math.Min(left, len);
                    return (new PlanPoint(cur.X + dx * k, cur.Z + dz * k), dx, dz);
                }

                left -= len;
                cur = target;
                i--;
            }
        }

        private static void StandingPoint(SettlementLayoutData plan, RoadData road, int seg, SettlementEntrance e)
        {
            var insets = e.Walled ? new[] { GateInset, GateInset + 3f, GateInset - 2f, GateInset + 7f } : new[] { WayInset, WayInset + 3f, WayInset + 6f, WayInset - 2f };
            foreach (var d in insets)
            {
                var (p, dx, dz) = Back(road.Line, seg, e.Gate, d);
                if (!Clear(plan, p, 1.0f)) continue;
                e.Arrival = p;
                e.ArrivalYaw = Yaw(dx, dz);
                e.ArrivalValid = true;
                return;
            }

            var (q, qx, qz) = Back(road.Line, seg, e.Gate, insets[0]);
            e.Arrival = q;
            e.ArrivalYaw = Yaw(qx, qz);
            e.ArrivalValid = false;
        }

        private static void Posts(SettlementLayoutData plan, RoadData road, int seg, SettlementEntrance e)
        {
            var side = road.Width / 2f + 1.5f;
            var posts = new List<PlanPoint>();
            foreach (var d in e.Walled ? new[] { 6.5f, 8.5f, 10.5f } : new[] { 2.5f, 4.5f, 6.5f })
            {
                var (p, dx, dz) = Back(road.Line, seg, e.Gate, d);
                // Right of the inward direction is (dz, −dx).
                var a = new PlanPoint(p.X + dz * side, p.Z - dx * side);
                var b = new PlanPoint(p.X - dz * side, p.Z + dx * side);
                if (!Clear(plan, a, 0.6f) || !Clear(plan, b, 0.6f)) continue;
                posts.Add(a);
                posts.Add(b);
                e.PostYaw = Yaw(-dx, -dz); // facing out, toward who comes
                break;
            }

            e.Posts = posts.ToArray();
        }

        private static bool InBox(PlanPoint p, PlanPoint c, float w, float d, float yawDegrees, float margin)
        {
            var r = yawDegrees * Math.PI / 180.0;
            var cos = (float)Math.Cos(r);
            var sin = (float)Math.Sin(r);
            var dx = p.X - c.X;
            var dz = p.Z - c.Z;
            var lx = dx * cos + dz * sin;
            var lz = -dx * sin + dz * cos;
            return Math.Abs(lx) <= w / 2f + margin && Math.Abs(lz) <= d / 2f + margin;
        }

        /// <summary>Unity yaw (degrees about +Y) of the direction (dx, dz).</summary>
        public static float Yaw(float dx, float dz) => (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);

        private static float AngleGap(float a, float b)
        {
            var d = Math.Abs(a - b) % (float)(Math.PI * 2);
            return d > Math.PI ? (float)(Math.PI * 2) - d : d;
        }
    }
}
