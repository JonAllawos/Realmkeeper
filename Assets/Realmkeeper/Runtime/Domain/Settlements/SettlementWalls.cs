using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Settlements
{
    /// <summary>A straight run of town wall (stone) between two plan points, on the wall line.</summary>
    public readonly struct WallRun
    {
        public WallRun(float ax, float az, float bx, float bz)
        {
            AX = ax; AZ = az; BX = bx; BZ = bz;
        }

        public float AX { get; }
        public float AZ { get; }
        public float BX { get; }
        public float BZ { get; }
        public float Length => (float)Math.Sqrt((BX - AX) * (BX - AX) + (BZ - AZ) * (BZ - AZ));
    }

    /// <summary>A footprint on the plan: an oriented rectangle (centre, half sizes along its own axes, and its x axis).</summary>
    public readonly struct PlanBox
    {
        public PlanBox(float cx, float cz, float halfX, float halfZ, float axisX, float axisZ)
        {
            CX = cx; CZ = cz; HalfX = halfX; HalfZ = halfZ; AxisX = axisX; AxisZ = axisZ;
        }

        public float CX { get; }
        public float CZ { get; }
        public float HalfX { get; }
        public float HalfZ { get; }

        /// <summary>The box's own x axis on the plan (unit); its z axis is (−AxisZ, AxisX).</summary>
        public float AxisX { get; }
        public float AxisZ { get; }

        /// <summary>Whether a circle of <paramref name="radius"/> at (x, z) touches the box.</summary>
        public bool Touches(float x, float z, float radius)
        {
            var dx = x - CX;
            var dz = z - CZ;
            var u = dx * AxisX + dz * AxisZ;
            var v = -dx * AxisZ + dz * AxisX;
            var cu = Math.Max(-HalfX, Math.Min(HalfX, u));
            var cv = Math.Max(-HalfZ, Math.Min(HalfZ, v));
            return (u - cu) * (u - cu) + (v - cv) * (v - cv) < radius * radius;
        }
    }

    /// <summary>
    /// Phase 29 (ADR-0121): the solid parts of a walled settlement's boundary, from the plan alone, so the renderer draws them
    /// and the engine-free tests can check every gate of every world. Before Phase 29 this was computed inside the renderer,
    /// and the gatehouse was turned to face the settlement's centre whatever way its road ran; where the road met the wall
    /// aslant, a tower or the wall stood across the road beside an open-looking arch (the owner's report).
    /// Now each gatehouse is turned to its road's own direction through the wall, its opening is widened to the road, and
    /// the wall is cut back to the towers along the wall line, so the passage is clear through the whole wall thickness.
    /// </summary>
    public static class SettlementWalls
    {
        /// <summary>The wall's thickness in metres (the renderer's stone runs and their colliders).</summary>
        public const float Thickness = 1.4f;

        /// <summary>How far each stone run is drawn past its ends, so runs meet at corners and tuck into the towers.</summary>
        public const float EndOverlap = 0.4f;

        /// <summary>Runs shorter than this are not built.</summary>
        public const float MinRun = 0.6f;

        /// <summary>A gatehouse's proportions by the settlement's standing (Phase 28): opening, tower side, extra height.</summary>
        public static (float opening, float tower, float extra) GateSpec(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.Fortress: return (7f, 5.5f, 4f);
                case SettlementType.City: return (8f, 5f, 2.5f);
                default: return (7.5f, 4f, 0f);
            }
        }

        /// <summary>One gatehouse as built: its centre, the passage direction (unit, along the road), and its opening.</summary>
        public readonly struct Gate
        {
            public Gate(string key, float x, float z, float dirX, float dirZ, float opening, float tower, float wallX, float wallZ)
            {
                Key = key; X = x; Z = z; DirX = dirX; DirZ = dirZ; Opening = opening; Tower = tower; WallX = wallX; WallZ = wallZ;
            }

            public string Key { get; }
            public float X { get; }
            public float Z { get; }

            /// <summary>The passage's direction (unit): the road's own direction where it crosses the wall.</summary>
            public float DirX { get; }
            public float DirZ { get; }

            /// <summary>The clear width between the towers, measured across the passage.</summary>
            public float Opening { get; }
            public float Tower { get; }

            /// <summary>The wall line's direction at the gate (unit).</summary>
            public float WallX { get; }
            public float WallZ { get; }

            /// <summary>The gatehouse's yaw in the plan's convention (degrees; 0 faces +Z), along the passage.</summary>
            public float Yaw => (float)(Math.Atan2(DirX, DirZ) * 180.0 / Math.PI);

            /// <summary>The two towers' footprints, either side of the passage.</summary>
            public IEnumerable<PlanBox> Towers()
            {
                var ax = DirZ; // across the passage
                var az = -DirX;
                var off = Opening / 2f + Tower / 2f;
                foreach (var s in new[] { -1f, 1f })
                    yield return new PlanBox(X + ax * off * s, Z + az * off * s, Tower / 2f, Tower / 2f, ax, az);
            }
        }

        /// <summary>The gatehouses of a walled plan, each on the road through it (none when the plan has no wall).</summary>
        public static List<Gate> Gates(SettlementLayoutData plan, SettlementType type)
        {
            var list = new List<Gate>();
            if (plan == null || !plan.HasWalls || plan.Wall.Count < 3) return list;
            var (opening, tower, _) = GateSpec(type);
            foreach (var b in plan.Buildings)
            {
                if (b.Function != BuildingFunction.Gatehouse) continue;
                var (wx, wz) = WallDirectionAt(plan, b.Center.X, b.Center.Z);
                var (dx, dz, roadWidth) = RoadThrough(plan, b.Center.X, b.Center.Z);
                if (dx == 0f && dz == 0f)
                {
                    // No road through it: square to the wall, facing the centre.
                    dx = -wz;
                    dz = wx;
                    if (dx * -b.Center.X + dz * -b.Center.Z < 0f) { dx = -dx; dz = -dz; }
                }

                // Across the passage the road needs its width (and a margin) clear; never narrower than the standing's opening.
                var open = Math.Max(opening, roadWidth + 1.2f);
                list.Add(new Gate(b.Key, b.Center.X, b.Center.Z, dx, dz, open, tower, wx, wz));
            }

            return list;
        }

        /// <summary>
        /// The stone runs of the wall: the wall line cut where a gatehouse stands (back to its towers, measured along the wall,
        /// allowing for a passage that crosses the wall aslant) and where lanes pass under a postern; runs in water are left
        /// out (the water is the wall there). Posterns are returned separately (their lintels stand above head height).
        /// </summary>
        public static (List<WallRun> runs, List<WallRun> posterns) Runs(SettlementLayoutData plan, SettlementType type)
        {
            var runs = new List<WallRun>();
            var posterns = new List<WallRun>();
            if (plan == null || !plan.HasWalls || plan.Wall.Count < 3) return (runs, posterns);
            var gates = Gates(plan, type);
            for (var i = 0; i < plan.Wall.Count; i++)
            {
                var a = plan.Wall[i];
                var b = plan.Wall[(i + 1) % plan.Wall.Count];
                var len = Dist(a.X, a.Z, b.X, b.Z);
                if (len < 0.5f) continue;
                var dirX = (b.X - a.X) / len;
                var dirZ = (b.Z - a.Z) / len;
                var cuts = new List<(float from, float to, bool postern)>();
                foreach (var g in gates)
                {
                    var rx = g.X - a.X;
                    var rz = g.Z - a.Z;
                    var along = rx * dirX + rz * dirZ;
                    var off = Math.Abs(rx * dirZ - rz * dirX);
                    var half = GateHalf(g, dirX, dirZ);
                    if (off < 6f && along > -half && along < len + half) cuts.Add((along - half, along + half, false));
                }

                foreach (var (at, width, cos) in RoadCrossings(plan, a, b))
                {
                    var along = at * len;
                    var inGate = false;
                    foreach (var c in cuts) if (!c.postern && along > c.from - 1f && along < c.to + 1f) inGate = true;
                    if (inGate) continue;
                    var sin = (float)Math.Sqrt(Math.Max(0f, 1f - cos * cos));
                    var half = (width / 2f + 0.9f) / Math.Max(0.35f, sin) + Thickness / 2f * Math.Abs(cos) / Math.Max(0.35f, sin);
                    cuts.Add((along - half, along + half, true));
                }

                cuts.Sort((x, y) => x.from.CompareTo(y.from));
                var start = 0f;
                foreach (var c in cuts)
                {
                    Add(plan, runs, a, dirX, dirZ, start, Math.Min(len, c.from));
                    if (c.postern)
                    {
                        var f = Math.Max(0f, c.from);
                        var t = Math.Min(len, c.to);
                        if (t - f >= 1f) posterns.Add(new WallRun(a.X + dirX * f, a.Z + dirZ * f, a.X + dirX * t, a.Z + dirZ * t));
                    }

                    start = Math.Max(start, c.to);
                }

                Add(plan, runs, a, dirX, dirZ, start, len);
            }

            return (runs, posterns);
        }

        /// <summary>
        /// How far either side of a gate, along a wall line of direction (wx, wz), the wall must stop: to the far side of the
        /// towers, measured where the towers' outer corners meet the wall's faces, less the run's own end overlap.
        /// </summary>
        private static float GateHalf(Gate g, float wx, float wz)
        {
            // In the gate's frame: u across the passage, v along it. The wall line runs through the gate with direction w.
            var wu = wx * g.DirZ - wz * g.DirX; // component across
            var wv = wx * g.DirX + wz * g.DirZ; // component along the passage
            var reach = g.Opening / 2f + g.Tower; // the towers' outer sides, across the passage
            // A point s along the wall is at (s·wu, s·wv); the wall's faces are ±Thickness/2 either side of the line. The
            // wall must stop where its whole thickness is past the towers' outer sides (|u| ≥ reach) or beyond the towers'
            // depth along the passage (|v| ≥ tower/2); whichever comes first, plus the face offset.
            var su = Math.Abs(wu) > 1e-3f ? (reach + Thickness / 2f * Math.Abs(wv)) / Math.Abs(wu) : float.MaxValue;
            var sv = Math.Abs(wv) > 1e-3f ? (g.Tower / 2f + Thickness / 2f * Math.Abs(wu)) / Math.Abs(wv) : float.MaxValue;
            var s = Math.Min(su, sv);
            // The stone run is drawn EndOverlap past its end and tucks into the tower; never less than the opening's half.
            return Math.Max(g.Opening / 2f + 0.6f + EndOverlap, s - EndOverlap - 0.4f);
        }

        private static void Add(SettlementLayoutData plan, List<WallRun> runs, PlanPoint a, float dx, float dz, float from, float to)
        {
            from = Math.Max(0f, from);
            if (to - from < MinRun) return;
            var p = new WallRun(a.X + dx * from, a.Z + dz * from, a.X + dx * to, a.Z + dz * to);
            if (SettlementTerrain.InWater(plan, (p.AX + p.BX) / 2f, (p.AZ + p.BZ) / 2f, 1f)) return;
            runs.Add(p);
        }

        /// <summary>The wall line's direction (unit) at its point nearest to (x, z).</summary>
        public static (float x, float z) WallDirectionAt(SettlementLayoutData plan, float x, float z)
        {
            var best = float.MaxValue;
            (float, float) dir = (1f, 0f);
            for (var i = 0; i < plan.Wall.Count; i++)
            {
                var a = plan.Wall[i];
                var b = plan.Wall[(i + 1) % plan.Wall.Count];
                var d = SettlementTerrain.SegmentDistance(a.X, a.Z, b.X, b.Z, x, z, out _, out _);
                var len = Dist(a.X, a.Z, b.X, b.Z);
                if (d < best && len > 0.5f) { best = d; dir = ((b.X - a.X) / len, (b.Z - a.Z) / len); }
            }

            return dir;
        }

        /// <summary>The direction (unit, pointing inward) and width of the road passing nearest a gate, within 4 m; zeros if none.</summary>
        public static (float x, float z, float width) RoadThrough(SettlementLayoutData plan, float x, float z)
        {
            var best = 4f;
            (float, float, float) found = (0f, 0f, 0f);
            foreach (var road in plan.Roads)
                for (var i = 0; i + 1 < road.Line.Count; i++)
                {
                    var a = road.Line[i];
                    var b = road.Line[i + 1];
                    var d = SettlementTerrain.SegmentDistance(a.X, a.Z, b.X, b.Z, x, z, out _, out _);
                    var len = Dist(a.X, a.Z, b.X, b.Z);
                    if (d >= best || len < 0.5f) continue;
                    best = d;
                    var dx = (b.X - a.X) / len;
                    var dz = (b.Z - a.Z) / len;
                    if (dx * -x + dz * -z < 0f) { dx = -dx; dz = -dz; } // point inward
                    found = (dx, dz, road.Width);
                }

            return found;
        }

        /// <summary>Where roads cross the wall segment a→b: the fraction along it, the road's width, and the cosine between them.</summary>
        public static List<(float at, float width, float cos)> RoadCrossings(SettlementLayoutData plan, PlanPoint a, PlanPoint b)
        {
            var list = new List<(float, float, float)>();
            var abx = b.X - a.X;
            var abz = b.Z - a.Z;
            var abl = Math.Max(1e-6f, (float)Math.Sqrt(abx * abx + abz * abz));
            foreach (var road in plan.Roads)
                for (var i = 0; i + 1 < road.Line.Count; i++)
                {
                    var c = road.Line[i];
                    var d = road.Line[i + 1];
                    var cdx = d.X - c.X;
                    var cdz = d.Z - c.Z;
                    var den = abx * cdz - abz * cdx;
                    if (Math.Abs(den) < 1e-5f) continue;
                    var t = ((c.X - a.X) * cdz - (c.Z - a.Z) * cdx) / den;
                    var u = ((c.X - a.X) * abz - (c.Z - a.Z) * abx) / den;
                    if (t < 0f || t > 1f || u < 0f || u > 1f) continue;
                    var cdl = Math.Max(1e-6f, (float)Math.Sqrt(cdx * cdx + cdz * cdz));
                    list.Add((t, road.Width, (abx * cdx + abz * cdz) / (abl * cdl)));
                }

            return list;
        }

        /// <summary>How far either side of a gate, along its road, the road is made straight (beyond the largest gatehouse).</summary>
        public const float StraightReach = 9f;

        /// <summary>The straight run's half length through the gate itself (the largest tower's half depth, plus room to turn).</summary>
        public const float StraightCore = 5f;

        /// <summary>
        /// Phase 29 (ADR-0121), generator v3: a road that passes a wall gate runs straight through it. Generated roads can jog
        /// at the wall (the owner's case: two turns of about 70° within 6 m of the gate), and no gatehouse can stand on such a
        /// road without a tower in the way. Within <see cref="StraightReach"/> of the gate the road becomes: the point where it
        /// was at that distance outside, a straight run through the gate point (±<see cref="StraightCore"/>), and the point
        /// where it was at that distance inside. The gate stays where it is on the wall. A gate is left as it was when a bridge
        /// is near it, or when the new run would cross water or a building. Applied at the end of generation and, for plans
        /// saved before v3, once on load. Returns how many gates were straightened.
        /// </summary>
        public static int StraightenGateRoads(SettlementLayoutData plan) => StraightenGateRoads(plan, null);

        /// <summary>As <see cref="StraightenGateRoads(SettlementLayoutData)"/>, noting why any gate was left as it was.</summary>
        public static int StraightenGateRoads(SettlementLayoutData plan, List<string> skipped)
        {
            if (plan == null || !plan.HasWalls || plan.Gates.Count == 0) return 0;
            var changed = 0;
            for (var gi = 0; gi < plan.Gates.Count; gi++)
            {
                var g = plan.Gates[gi];
                RoadData road = null;
                var seg = -1;
                var best = 4f;
                foreach (var r in plan.Roads)
                    for (var i = 0; i + 1 < r.Line.Count; i++)
                    {
                        var d = SettlementTerrain.SegmentDistance(r.Line[i].X, r.Line[i].Z, r.Line[i + 1].X, r.Line[i + 1].Z, g.X, g.Z, out _, out _);
                        if (d < best) { best = d; road = r; seg = i; }
                    }

                if (road == null) continue;
                // A bridge that ends at the gate (the owner's case): the road follows the bridge's deck, straight, and carries
                // on straight through the gate; where it was beyond that it is left as it was.
                BridgeData landing = null;
                foreach (var b in plan.Bridges)
                    if (b.RoadKey == road.Key && (Dist(b.From.X, b.From.Z, g.X, g.Z) < 3f || Dist(b.To.X, b.To.Z, g.X, g.Z) < 3f)) landing = b;
                if (landing != null)
                {
                    var done = AlongBridge(plan, road, g, landing);
                    if (done > 0) { changed++; OnTheCircle(plan, gi, road); }
                    else if (done < 0) skipped?.Add($"gate ({g.X:0.0},{g.Z:0.0}) on {road.Key}: its bridge could not be followed");
                    continue;
                }

                string near = null;
                foreach (var b in plan.Bridges) if (Dist(b.From.X, b.From.Z, g.X, g.Z) < StraightReach + 6f || Dist(b.To.X, b.To.Z, g.X, g.Z) < StraightReach + 6f) near = $"({b.From.X:0.0},{b.From.Z:0.0})→({b.To.X:0.0},{b.To.Z:0.0}) on {b.RoadKey}";
                if (near != null) { skipped?.Add($"gate ({g.X:0.0},{g.Z:0.0}) on {road.Key}: a bridge is near, {near}"); continue; }

                // Arc position of the gate on the road, and the points at ±StraightReach.
                var line = road.Line;
                var arc = new float[line.Count];
                for (var i = 1; i < line.Count; i++) arc[i] = arc[i - 1] + Dist(line[i - 1].X, line[i - 1].Z, line[i].X, line[i].Z);
                SettlementTerrain.SegmentDistance(line[seg].X, line[seg].Z, line[seg + 1].X, line[seg + 1].Z, g.X, g.Z, out var px, out var pz);
                var s0 = arc[seg] + Dist(line[seg].X, line[seg].Z, px, pz);
                var total = arc[line.Count - 1];
                var sa = Math.Max(0f, s0 - StraightReach);
                var sb = Math.Min(total, s0 + StraightReach);
                if (sb - sa < StraightCore * 2f + 1f) { skipped?.Add($"gate ({g.X:0.0},{g.Z:0.0}): the road is too short"); continue; }
                var pa = PointAt(line, arc, sa);
                var pb = PointAt(line, arc, sb);
                var dx = pb.X - pa.X;
                var dz = pb.Z - pa.Z;
                var len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len < 1f) continue;
                dx /= len;
                dz /= len;
                var ca = new PlanPoint(g.X - dx * StraightCore, g.Z - dz * StraightCore);
                var cb = new PlanPoint(g.X + dx * StraightCore, g.Z + dz * StraightCore);

                // Already straight enough through the gate, or already straightened here? Leave it.
                if (MaxOff(line, arc, sa, sb, g, dx, dz) < 0.35f || Straightened(line, g)) continue;

                // The new run must not cross water or stand in a building.
                string blocked = null;
                foreach (var q in new[] { pa, ca, g, cb, pb })
                    if (SettlementTerrain.InWater(plan, q.X, q.Z, 0.5f)) blocked = "water";
                foreach (var b in plan.Buildings)
                {
                    if (b.Function == BuildingFunction.Gatehouse) continue;
                    foreach (var (a, c) in new[] { (pa, ca), (ca, cb), (cb, pb) })
                        if (SettlementTerrain.SegmentDistance(a.X, a.Z, c.X, c.Z, b.Center.X, b.Center.Z, out _, out _) < road.Width / 2f + Math.Min(b.Width, b.Depth) / 2f) blocked = "building " + b.Key;
                }

                if (blocked != null) { skipped?.Add($"gate ({g.X:0.0},{g.Z:0.0}): {blocked}"); continue; }
                var rebuilt = new List<PlanPoint>();
                for (var i = 0; i < line.Count && arc[i] < sa - 1e-3f; i++) rebuilt.Add(line[i]);
                rebuilt.Add(pa);
                rebuilt.Add(ca);
                rebuilt.Add(g);
                rebuilt.Add(cb);
                rebuilt.Add(pb);
                for (var i = 0; i < line.Count; i++) if (arc[i] > sb + 1e-3f) rebuilt.Add(line[i]);
                // Drop points that coincide (a gate at a road's very end).
                var clean = new List<PlanPoint>();
                foreach (var q in rebuilt) if (clean.Count == 0 || Dist(clean[clean.Count - 1].X, clean[clean.Count - 1].Z, q.X, q.Z) > 0.05f) clean.Add(q);
                road.Line = clean;
                changed++;
                OnTheCircle(plan, gi, road);
            }

            return changed;
        }

        /// <summary>Whether the line already runs straight through g for ±<see cref="StraightCore"/> (made so before).</summary>
        private static bool Straightened(List<PlanPoint> line, PlanPoint g)
        {
            for (var i = 1; i + 1 < line.Count; i++)
            {
                if (Dist(line[i].X, line[i].Z, g.X, g.Z) > 0.05f) continue;
                var a = line[i - 1];
                var b = line[i + 1];
                if (Math.Abs(Dist(a.X, a.Z, g.X, g.Z) - StraightCore) > 0.1f || Math.Abs(Dist(b.X, b.Z, g.X, g.Z) - StraightCore) > 0.1f) continue;
                var cross = (g.X - a.X) * (b.Z - g.Z) - (g.Z - a.Z) * (b.X - g.X);
                if (Math.Abs(cross) < 0.05f * StraightCore) return true;
            }

            return false;
        }

        /// <summary>
        /// After straightening, the gate (and its gatehouse and landmark) is put exactly where the road crosses the circle the
        /// entrances are measured on (<see cref="SettlementEntrances"/>): a few millimetres at most, so the entrance, the plan's
        /// gate and the gatehouse agree exactly on a road that now crosses at a new angle.
        /// </summary>
        private static void OnTheCircle(SettlementLayoutData plan, int gi, RoadData road)
        {
            var g = plan.Gates[gi];
            var radius = plan.CoreRadius > 1f ? plan.CoreRadius : plan.HalfExtent * 0.5f;
            var r = (float)Math.Sqrt(g.X * g.X + g.Z * g.Z);
            if (r < 1f || Math.Abs(r - radius) > 0.5f) return; // not a gate on the core circle: leave it
            var snapped = new PlanPoint(g.X * radius / r, g.Z * radius / r);
            for (var i = 0; i < road.Line.Count; i++)
                if (Dist(road.Line[i].X, road.Line[i].Z, g.X, g.Z) < 0.05f) road.Line[i] = snapped;
            foreach (var b in plan.Buildings)
                if (b.Function == BuildingFunction.Gatehouse && Dist(b.Center.X, b.Center.Z, g.X, g.Z) < 0.05f) b.Center = snapped;
            foreach (var l in plan.Landmarks)
                if (Dist(l.Position.X, l.Position.Z, g.X, g.Z) < 0.05f) l.Position = snapped;
            plan.Gates[gi] = snapped;
        }

        /// <summary>
        /// Makes the road follow a bridge that lands at the gate, then run straight through the gate beyond it. Returns 1 when
        /// the road was changed, 0 when it already ran so, −1 when it could not be done.
        /// </summary>
        private static int AlongBridge(SettlementLayoutData plan, RoadData road, PlanPoint g, BridgeData bridge)
        {
            var line = road.Line;
            if (line.Count < 2) return -1;
            var far = Dist(bridge.From.X, bridge.From.Z, g.X, g.Z) < Dist(bridge.To.X, bridge.To.Z, g.X, g.Z) ? bridge.To : bridge.From;
            var len = Dist(far.X, far.Z, g.X, g.Z);
            if (len < 1f) return -1;
            var already = 0;
            foreach (var q in line)
                if (Dist(q.X, q.Z, far.X, far.Z) < 0.05f || Dist(q.X, q.Z, g.X, g.Z) < 0.05f) already++;
            for (var i = 0; i + 2 < line.Count && already >= 2; i++)
                if ((Dist(line[i].X, line[i].Z, far.X, far.Z) < 0.05f && Dist(line[i + 1].X, line[i + 1].Z, g.X, g.Z) < 0.05f) ||
                    (Dist(line[i + 1].X, line[i + 1].Z, g.X, g.Z) < 0.05f && Dist(line[i + 2].X, line[i + 2].Z, far.X, far.Z) < 0.05f)) return 0;
            var dx = (g.X - far.X) / len; // from the bridge, through the gate
            var dz = (g.Z - far.Z) / len;
            var arc = new float[line.Count];
            for (var i = 1; i < line.Count; i++) arc[i] = arc[i - 1] + Dist(line[i - 1].X, line[i - 1].Z, line[i].X, line[i].Z);
            var sGate = ArcOf(line, arc, g);
            var sFar = ArcOf(line, arc, far);
            var beyond = new PlanPoint(g.X + dx * StraightCore, g.Z + dz * StraightCore);
            if (SettlementTerrain.InWater(plan, beyond.X, beyond.Z, 0.5f)) return -1;
            var rebuilt = new List<PlanPoint>();
            if (sFar < sGate)
            {
                var sOut = Math.Min(arc[line.Count - 1], sGate + StraightReach);
                for (var i = 0; i < line.Count && arc[i] < sFar - 1e-3f; i++) rebuilt.Add(line[i]);
                rebuilt.Add(far);
                rebuilt.Add(g);
                rebuilt.Add(beyond);
                rebuilt.Add(PointAt(line, arc, sOut));
                for (var i = 0; i < line.Count; i++) if (arc[i] > sOut + 1e-3f) rebuilt.Add(line[i]);
            }
            else
            {
                var sOut = Math.Max(0f, sGate - StraightReach);
                for (var i = 0; i < line.Count && arc[i] < sOut - 1e-3f; i++) rebuilt.Add(line[i]);
                rebuilt.Add(PointAt(line, arc, sOut));
                rebuilt.Add(beyond);
                rebuilt.Add(g);
                rebuilt.Add(far);
                for (var i = 0; i < line.Count; i++) if (arc[i] > sFar + 1e-3f) rebuilt.Add(line[i]);
            }

            var clean = new List<PlanPoint>();
            foreach (var q in rebuilt) if (clean.Count == 0 || Dist(clean[clean.Count - 1].X, clean[clean.Count - 1].Z, q.X, q.Z) > 0.05f) clean.Add(q);
            if (clean.Count < 2) return -1;
            road.Line = clean;
            return 1;
        }

        /// <summary>The arc length along a line of its point nearest p.</summary>
        private static float ArcOf(List<PlanPoint> line, float[] arc, PlanPoint p)
        {
            var best = float.MaxValue;
            var at = 0f;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var d = SettlementTerrain.SegmentDistance(line[i].X, line[i].Z, line[i + 1].X, line[i + 1].Z, p.X, p.Z, out var px, out var pz);
                if (d < best) { best = d; at = arc[i] + Dist(line[i].X, line[i].Z, px, pz); }
            }

            return at;
        }

        private static PlanPoint PointAt(List<PlanPoint> line, float[] arc, float s)
        {
            for (var i = 0; i + 1 < line.Count; i++)
            {
                if (s > arc[i + 1] && i + 2 < line.Count) continue;
                var seg = Math.Max(1e-4f, arc[i + 1] - arc[i]);
                var t = Math.Max(0f, Math.Min(1f, (s - arc[i]) / seg));
                return new PlanPoint(line[i].X + (line[i + 1].X - line[i].X) * t, line[i].Z + (line[i + 1].Z - line[i].Z) * t);
            }

            return line[line.Count - 1];
        }

        /// <summary>The largest distance of the road (between arcs sa and sb) from the straight line through g along (dx, dz).</summary>
        private static float MaxOff(List<PlanPoint> line, float[] arc, float sa, float sb, PlanPoint g, float dx, float dz)
        {
            var worst = 0f;
            for (var s = sa; s <= sb; s += 0.5f)
            {
                var p = PointAt(line, arc, s);
                worst = Math.Max(worst, Math.Abs((p.X - g.X) * dz - (p.Z - g.Z) * dx));
            }

            return worst;
        }

        /// <summary>The footprint of a stone run as built (its thickness, and drawn <see cref="EndOverlap"/> past each end).</summary>
        public static PlanBox Footprint(WallRun r)
        {
            var len = Math.Max(1e-4f, r.Length);
            var ux = (r.BX - r.AX) / len;
            var uz = (r.BZ - r.AZ) / len;
            return new PlanBox((r.AX + r.BX) / 2f, (r.AZ + r.BZ) / 2f, len / 2f + EndOverlap, Thickness / 2f, ux, uz);
        }

        private static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
    }
}
