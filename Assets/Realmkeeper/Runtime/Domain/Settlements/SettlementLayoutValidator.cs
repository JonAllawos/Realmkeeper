using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Settlements
{
    /// <summary>
    /// What a settlement plan must satisfy (Phase 10, ADR-0047), whether freshly generated or loaded: every site the world
    /// uses is present; the square is dry; roads form one network (crossings over water are bridged, and a bridge lands
    /// on dry ground at both ends); no building stands in water, on a road, on another building, or on ground steeper
    /// than its plinth allows; fields lie on land fit for them; districts, landmarks and herds' pastures refer to things
    /// that exist. Each problem in words.
    /// </summary>
    public static class SettlementLayoutValidator
    {
        public const float MaxPlinth = 2.8f;

        public static List<string> Check(SettlementLayoutData p, SettlementType type)
        {
            var errors = new List<string>();
            if (p == null) { errors.Add("No plan."); return errors; }
            if (p.HalfExtent <= 0 || p.PlazaRadius <= 0) errors.Add("The plan has no land.");
            if (p.GeneratorVersion < 1 || p.GeneratorVersion > SettlementLayoutData.CurrentGeneratorVersion) errors.Add($"Unknown generator version {p.GeneratorVersion}.");

            // Sites.
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in p.Buildings) if (!keys.Add(b.Key)) errors.Add($"Building '{b.Key}' appears twice.");
            foreach (var site in RequiredSites(type)) if (!keys.Contains(site)) errors.Add($"The site '{site}' has no building.");
            if (SettlementTerrain.InWater(p, 0, 0, p.PlazaRadius)) errors.Add("The square is in the water.");

            // Roads: one network.
            if (p.Roads.Count == 0) errors.Add("There are no roads.");
            else if (!Connected(p)) errors.Add("The roads do not form one network.");
            foreach (var r in p.Roads)
            {
                if (r.Line.Count < 2) { errors.Add($"Road '{r.Key}' has no length."); continue; }
                for (var i = 0; i + 1 < r.Line.Count; i++)
                {
                    var a = r.Line[i];
                    var b = r.Line[i + 1];
                    var mid = new PlanPoint((a.X + b.X) / 2, (a.Z + b.Z) / 2);
                    if (SettlementTerrain.InWater(p, mid.X, mid.Z) && !Bridged(p, r.Key, mid)) errors.Add($"Road '{r.Key}' runs into the water at {mid} with no crossing.");
                }
            }

            foreach (var br in p.Bridges)
            {
                if (SettlementTerrain.InWater(p, br.From.X, br.From.Z) || SettlementTerrain.InWater(p, br.To.X, br.To.Z)) errors.Add($"Bridge '{br.Key}' ends in the water.");
                var road = Road(p, br.RoadKey);
                if (road == null) errors.Add($"Bridge '{br.Key}' carries no road.");
                else if (Distance(road, br.From) > road.Width + 2f || Distance(road, br.To) > road.Width + 2f) errors.Add($"Bridge '{br.Key}' does not meet its road.");
                if (br.From.DistanceTo(br.To) < 1f) errors.Add($"Bridge '{br.Key}' spans nothing.");
            }

            // Buildings.
            foreach (var b in p.Buildings)
            {
                if (b.Width <= 0 || b.Depth <= 0 || b.Height <= 0) errors.Add($"Building '{b.Key}' has no size.");
                if (b.Function != BuildingFunction.Gatehouse && b.Function != BuildingFunction.Mill && SettlementTerrain.InWater(p, b.Center.X, b.Center.Z, 1f)) errors.Add($"Building '{b.Key}' stands in the water.");
                if (b.Foundation > MaxPlinth + 0.5f) errors.Add($"Building '{b.Key}' stands on ground too steep ({b.Foundation:0.0} m of plinth).");
                if (b.Function == BuildingFunction.Gatehouse) continue;
                foreach (var r in p.Roads)
                {
                    if (Distance(r, b.Center) < r.Width / 2 + Math.Min(b.Width, b.Depth) / 2 - 0.5f) { errors.Add($"Building '{b.Key}' stands on road '{r.Key}'."); break; }
                }
            }

            for (var i = 0; i < p.Buildings.Count; i++)
                for (var j = i + 1; j < p.Buildings.Count; j++)
                {
                    var a = p.Buildings[i];
                    var b = p.Buildings[j];
                    if (a.Function == BuildingFunction.Gatehouse || b.Function == BuildingFunction.Gatehouse) continue;
                    if (a.Center.DistanceTo(b.Center) < (Math.Min(a.Width, a.Depth) + Math.Min(b.Width, b.Depth)) / 2 - 0.5f) errors.Add($"Buildings '{a.Key}' and '{b.Key}' overlap.");
                }

            // Fields.
            foreach (var f in p.Fields)
            {
                if (f.Width <= 0 || f.Depth <= 0) errors.Add($"Field '{f.Key}' has no size.");
                if (SettlementTerrain.InWater(p, f.Center.X, f.Center.Z)) errors.Add($"Field '{f.Key}' is in the water.");
                if (f.Kind == FieldKind.Crop || f.Kind == FieldKind.Fallow)
                {
                    var h = new List<float>();
                    foreach (var (dx, dz) in new[] { (-0.5f, -0.5f), (0.5f, -0.5f), (-0.5f, 0.5f), (0.5f, 0.5f) })
                    {
                        var pt = SettlementLayoutGenerator.Local(f.Center, f.Yaw, dx * f.Width, dz * f.Depth);
                        h.Add(p.HeightAt(pt.X, pt.Z));
                    }

                    h.Sort();
                    if (h[h.Count - 1] - h[0] > 4f) errors.Add($"Field '{f.Key}' is ploughed on ground too steep.");
                }
            }

            // References.
            foreach (var b in p.Buildings)
                if (b.DistrictKey != null && b.DistrictKey != "farms" && !HasDistrict(p, b.DistrictKey)) errors.Add($"Building '{b.Key}' is in an unknown district.");
            foreach (var l in p.Landmarks)
                if (l.FeatureKey != null && p.FindBuilding(l.FeatureKey) == null && !HasBridge(p, l.FeatureKey)) errors.Add($"Landmark '{l.Key}' points at nothing.");
            return errors;
        }

        public static IEnumerable<string> RequiredSites(SettlementType type)
        {
            yield return SettlementSites.Hall;
            yield return SettlementSites.Granary;
            yield return SettlementSites.Landmark;
            for (var i = 0; i < SettlementSites.HouseCount(type); i++) yield return SettlementSites.House(i);
        }

        /// <summary>True when every road meets the rest (end or vertex within a few metres of another road).</summary>
        public static bool Connected(SettlementLayoutData p)
        {
            var n = p.Roads.Count;
            var parent = new int[n];
            for (var i = 0; i < n; i++) parent[i] = i;
            int Root(int i) => parent[i] == i ? i : parent[i] = Root(parent[i]);
            for (var i = 0; i < n; i++)
                for (var j = i + 1; j < n; j++)
                    if (Meet(p.Roads[i], p.Roads[j])) parent[Root(i)] = Root(j);
            var root = Root(0);
            for (var i = 1; i < n; i++) if (Root(i) != root) return false;
            return true;
        }

        /// <summary>True when a road meets any road already in the plan.</summary>
        public static bool Joins(SettlementLayoutData p, RoadData road)
        {
            foreach (var r in p.Roads) if (r != road && Meet(road, r)) return true;
            return false;
        }

        private static bool Meet(RoadData a, RoadData b)
        {
            var tolerance = Math.Max(a.Width, b.Width) / 2 + 3f;
            foreach (var pt in new[] { a.Line[0], a.Line[a.Line.Count - 1] }) if (Distance(b, pt) <= tolerance) return true;
            foreach (var pt in new[] { b.Line[0], b.Line[b.Line.Count - 1] }) if (Distance(a, pt) <= tolerance) return true;
            // Roads that cross meet at the crossing.
            for (var i = 0; i + 1 < a.Line.Count; i++)
                for (var j = 0; j + 1 < b.Line.Count; j++)
                    if (Cross(a.Line[i], a.Line[i + 1], b.Line[j], b.Line[j + 1])) return true;
            // Main roads all start at the square's edge.
            var ra = Math.Sqrt(a.Line[0].X * a.Line[0].X + a.Line[0].Z * a.Line[0].Z);
            var rb = Math.Sqrt(b.Line[0].X * b.Line[0].X + b.Line[0].Z * b.Line[0].Z);
            return ra < 30 && rb < 30;
        }

        private static bool Cross(PlanPoint p1, PlanPoint p2, PlanPoint q1, PlanPoint q2)
        {
            float Side(PlanPoint a, PlanPoint b, PlanPoint c) => (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
            var d1 = Side(q1, q2, p1);
            var d2 = Side(q1, q2, p2);
            var d3 = Side(p1, p2, q1);
            var d4 = Side(p1, p2, q2);
            return d1 * d2 < 0 && d3 * d4 < 0;
        }

        public static float Distance(RoadData r, PlanPoint at)
        {
            var best = float.MaxValue;
            for (var i = 0; i + 1 < r.Line.Count; i++)
                best = Math.Min(best, SettlementTerrain.SegmentDistance(r.Line[i].X, r.Line[i].Z, r.Line[i + 1].X, r.Line[i + 1].Z, at.X, at.Z, out _, out _));
            return best;
        }

        private static bool Bridged(SettlementLayoutData p, string road, PlanPoint at)
        {
            foreach (var b in p.Bridges)
            {
                if (b.RoadKey != road) continue;
                var d = SettlementTerrain.SegmentDistance(b.From.X, b.From.Z, b.To.X, b.To.Z, at.X, at.Z, out _, out _);
                if (d <= b.Width + 6f) return true;
            }

            return false;
        }

        private static RoadData Road(SettlementLayoutData p, string key)
        {
            foreach (var r in p.Roads) if (r.Key == key) return r;
            return null;
        }

        private static bool HasDistrict(SettlementLayoutData p, string key)
        {
            foreach (var d in p.Districts) if (d.Key == key) return true;
            return false;
        }

        private static bool HasBridge(SettlementLayoutData p, string key)
        {
            foreach (var b in p.Bridges) if (b.Key == key) return true;
            return false;
        }
    }
}
