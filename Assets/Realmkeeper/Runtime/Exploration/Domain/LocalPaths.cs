using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Exploration.Domain
{
    /// <summary>
    /// Where people stand and how they walk in a settlement's local environment (Phase 5). Engine-free
    /// and deterministic. Spots come from the place the world says a person is this hour: in front of a
    /// building's door, inside the tavern, behind a shop's counter, among the stalls, by the gate. Routes
    /// follow the streets: out of a door, onto the nearest street, round the corner of the crossing (never
    /// through the well), and in at the other door.
    /// </summary>
    public static class LocalPaths
    {
        /// <summary>Metres a door's outside point stands in front of the wall.</summary>
        public const float DoorStandOff = 1.8f;

        /// <summary>A point in a building's own frame (door on local −Z) in settlement coordinates.</summary>
        public static LocalPoint ToWorld(LayoutBuilding b, float localX, float localZ)
        {
            var r = b.Yaw * Math.PI / 180.0;
            var cos = (float)Math.Cos(r);
            var sin = (float)Math.Sin(r);
            return new LocalPoint(b.Center.X + localX * cos - localZ * sin, b.Center.Z + localX * sin + localZ * cos);
        }

        /// <summary>The facing (degrees, Unity yaw about +Y) of someone looking out of a building's door.</summary>
        public static float FacingOut(LayoutBuilding b) => 180f - b.Yaw;

        public static LayoutBuilding Find(LocalEnvironmentLayout layout, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var b in layout.Buildings) if (b.Key == key) return b;
            return null;
        }

        /// <summary>The point outside a building's door, offset sideways for the n-th person there.</summary>
        public static LocalPoint DoorOutside(LayoutBuilding b, float side = 0f) => ToWorld(b, side, -(b.Depth / 2f + DoorStandOff));

        /// <summary>Just inside an enterable building's doorway.</summary>
        public static LocalPoint DoorInside(LayoutBuilding b) => ToWorld(b, DoorwayX(b), -(b.Depth / 2f - 1.2f));

        /// <summary>Where the doorway of an enterable building is along its front (the tavern's is centred; a shop's at its door slot).</summary>
        public static float DoorwayX(LayoutBuilding b) => b.Fit == ShopFit.None ? 0f : BuildingKitPlanner.ShopDoorX(b);

        /// <summary>Behind a shop's counter (the keeper's place), the n-th person further along.</summary>
        public static LocalPoint BehindCounter(LayoutBuilding b, int n = 0) => ToWorld(b, -0.6f + n * 1.1f, b.Depth / 2f - 1.15f);

        /// <summary>In a shop, on the customers' side of the counter.</summary>
        public static LocalPoint ShopFloor(LayoutBuilding b, int n = 0) => ToWorld(b, (n % 2 == 0 ? 1.2f : -1.2f) * (1 + n / 2), -0.4f);

        /// <summary>Slot spacing for several people at one door.</summary>
        public static float Side(int n) => (n % 2 == 0 ? 1f : -1f) * (0.9f + 1.3f * (n / 2));

        /// <summary>
        /// Where someone at a place stands, and how far they may wander: the same rules the layout uses
        /// for people present when the environment is built, plus shop interiors. Null when the place has
        /// no site in this environment (the caller then uses the role's default spot).
        /// </summary>
        public static (LocalPoint spot, float wander, bool indoors)? SpotAt(LocalEnvironmentLayout layout, string siteKey, LocationKind? kind, int n, bool keeper)
        {
            if (string.IsNullOrEmpty(siteKey)) return null;
            var plaza = layout.PlazaRadius;
            if (siteKey == SettlementSites.Plaza)
            {
                var market = kind == LocationKind.Market;
                var angle = (market ? 0.6f : 3.6f) + n * 0.55f;
                var radius = market ? plaza - 4.5f : plaza - 2.5f;
                return (new LocalPoint((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius), 1.5f, false);
            }

            if (siteKey == SettlementSites.Gate) return (new LocalPoint(layout.Gate.X + Side(n) * 1.4f, layout.Gate.Z), 1.5f, false);

            var b = Find(layout, siteKey);
            if (b == null) return null;
            if (b.Kind == BuildingKind.Hall)
                return (new LocalPoint(b.Center.X + 1.5f + Side(n) * 0.8f, b.Center.Z + 0.6f), 0.5f, true);
            if (b.Fit != ShopFit.None)
                return keeper ? (BehindCounter(b, 0), 0.4f, true) : (ShopFloor(b, n), 0.6f, true);
            return (DoorOutside(b, Side(n)), 1.2f, false);
        }

        /// <summary>
        /// A walking route between two points along the streets. Each end may be inside an enterable
        /// building (then the route passes its doorway). The first point is <paramref name="from"/>,
        /// the last <paramref name="to"/>.
        /// </summary>
        public static List<LocalPoint> Route(LocalEnvironmentLayout layout, LocalPoint from, LayoutBuilding fromInside, LocalPoint to, LayoutBuilding toInside)
        {
            var path = new List<LocalPoint> { from };
            if (fromInside != null)
            {
                path.Add(DoorInside(fromInside));
                path.Add(ToWorld(fromInside, DoorwayX(fromInside), -(fromInside.Depth / 2f + DoorStandOff)));
            }

            var start = path[path.Count - 1];
            var end = toInside != null ? ToWorld(toInside, DoorwayX(toInside), -(toInside.Depth / 2f + DoorStandOff)) : to;
            if (Distance(start, end) > 3f && layout.Plan != null)
            {
                // A real road network: the shortest way along it (Phase 10).
                foreach (var q in GraphRoute(layout, start, end)) path.Add(q);
            }
            else if (Distance(start, end) > 3f)
            {
                var a = Project(layout, start, out var streetA);
                var b = Project(layout, end, out var streetB);
                if (Distance(start, a) > 0.5f) path.Add(a);
                if (streetA != streetB && streetA >= 0 && streetB >= 0)
                {
                    // Round the corner of the crossing on the side the walk turns, clear of the plaza's centre.
                    var corner = 3.2f;
                    var cx = Math.Abs(a.X) > Math.Abs(b.X) ? Math.Sign(a.X) : Math.Sign(b.X);
                    var cz = Math.Abs(a.Z) > Math.Abs(b.Z) ? Math.Sign(a.Z) : Math.Sign(b.Z);
                    path.Add(new LocalPoint((cx == 0 ? 1 : cx) * corner, (cz == 0 ? 1 : cz) * corner));
                }

                if (Distance(b, end) > 0.5f) path.Add(b);
            }

            if (toInside != null)
            {
                path.Add(end);
                path.Add(DoorInside(toInside));
            }

            path.Add(to);
            return path;
        }

        /// <summary>
        /// The road network of a planned settlement as a graph: every road's points, joined along each road, and joined
        /// across where roads meet or cross (points of different roads within a few metres), and across the square.
        /// </summary>
        internal sealed class RoadGraph
        {
            public readonly List<LocalPoint> Nodes = new List<LocalPoint>();
            public readonly List<List<int>> Links = new List<List<int>>();

            public int Add(LocalPoint p)
            {
                Nodes.Add(p);
                Links.Add(new List<int>());
                return Nodes.Count - 1;
            }

            public void Join(int a, int b)
            {
                if (a == b || Links[a].Contains(b)) return;
                Links[a].Add(b);
                Links[b].Add(a);
            }
        }

        internal static RoadGraph GraphOf(LocalEnvironmentLayout layout)
        {
            if (layout.Graph != null) return layout.Graph;
            var g = new RoadGraph();
            var roadOf = new List<int>();
            var plan = layout.Plan;
            for (var r = 0; r < plan.Roads.Count; r++)
            {
                var line = plan.Roads[r].Line;
                var previous = -1;
                for (var i = 0; i < line.Count; i++)
                {
                    var n = g.Add(new LocalPoint(line[i].X, line[i].Z));
                    roadOf.Add(r);
                    if (previous >= 0) g.Join(previous, n);
                    previous = n;
                }
            }

            var plaza = layout.PlazaRadius + 3f;
            for (var a = 0; a < g.Nodes.Count; a++)
                for (var b = a + 1; b < g.Nodes.Count; b++)
                {
                    if (roadOf[a] == roadOf[b]) continue;
                    var d = Distance(g.Nodes[a], g.Nodes[b]);
                    var ra = (float)Math.Sqrt(g.Nodes[a].X * g.Nodes[a].X + g.Nodes[a].Z * g.Nodes[a].Z);
                    var rb = (float)Math.Sqrt(g.Nodes[b].X * g.Nodes[b].X + g.Nodes[b].Z * g.Nodes[b].Z);
                    if (d <= 4.5f || (ra <= plaza && rb <= plaza)) g.Join(a, b);
                }

            layout.Graph = g;
            return g;
        }

        /// <summary>The way between two points along a planned settlement's roads (excluding the two points themselves).</summary>
        internal static List<LocalPoint> GraphRoute(LocalEnvironmentLayout layout, LocalPoint start, LocalPoint end)
        {
            var g = GraphOf(layout);
            var result = new List<LocalPoint>();
            if (g.Nodes.Count == 0) return result;
            var from = Nearest(g, start);
            var to = Nearest(g, end);
            var dist = new float[g.Nodes.Count];
            var prev = new int[g.Nodes.Count];
            var done = new bool[g.Nodes.Count];
            for (var i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[from] = 0f;
            for (var step = 0; step < g.Nodes.Count; step++)
            {
                var u = -1;
                for (var i = 0; i < dist.Length; i++) if (!done[i] && dist[i] < float.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || u == to) break;
                done[u] = true;
                foreach (var v in g.Links[u])
                {
                    var alt = dist[u] + Distance(g.Nodes[u], g.Nodes[v]);
                    if (alt < dist[v]) { dist[v] = alt; prev[v] = u; }
                }
            }

            if (dist[to] == float.MaxValue) { result.Add(g.Nodes[from]); result.Add(g.Nodes[to]); return result; }
            for (var n = to; n >= 0; n = prev[n]) result.Insert(0, g.Nodes[n]);
            return result;
        }

        private static int Nearest(RoadGraph g, LocalPoint p)
        {
            var best = 0;
            var bestD = float.MaxValue;
            for (var i = 0; i < g.Nodes.Count; i++)
            {
                var d = Distance(g.Nodes[i], p);
                if (d < bestD) { bestD = d; best = i; }
            }

            return best;
        }

        /// <summary>The nearest point on any street's centre line, and that street's index (−1 if there are none).</summary>
        public static LocalPoint Project(LocalEnvironmentLayout layout, LocalPoint p, out int street)
        {
            street = -1;
            var best = p;
            var bestD = float.MaxValue;
            for (var i = 0; i < layout.Streets.Count; i++)
            {
                var s = layout.Streets[i];
                var dx = s.To.X - s.From.X;
                var dz = s.To.Z - s.From.Z;
                var len2 = dx * dx + dz * dz;
                var t = len2 <= 0f ? 0f : Math.Max(0f, Math.Min(1f, ((p.X - s.From.X) * dx + (p.Z - s.From.Z) * dz) / len2));
                var q = new LocalPoint(s.From.X + t * dx, s.From.Z + t * dz);
                var d = Distance(p, q);
                if (d < bestD)
                {
                    bestD = d;
                    best = q;
                    street = i;
                }
            }

            return best;
        }

        /// <summary>
        /// The height someone stands at (Phase 10): a building's floor inside it, a bridge's deck on it, the ground
        /// elsewhere; 0 everywhere in an environment without a plan (its ground is flat).
        /// </summary>
        public static float StandingHeight(LocalEnvironmentLayout layout, float x, float z)
        {
            var plan = layout?.Plan;
            if (plan == null) return 0f;
            foreach (var b in layout.Buildings)
            {
                var r = b.Yaw * Math.PI / 180.0;
                var dx = x - b.Center.X;
                var dz = z - b.Center.Z;
                var lx = dx * Math.Cos(r) + dz * Math.Sin(r);
                var lz = -dx * Math.Sin(r) + dz * Math.Cos(r);
                if (Math.Abs(lx) <= b.Width / 2f + 0.2f && Math.Abs(lz) <= b.Depth / 2f + 0.2f) return b.FloorHeight;
            }

            foreach (var br in plan.Bridges)
            {
                var d = Realmkeeper.Domain.Settlements.SettlementTerrain.SegmentDistance(br.From.X, br.From.Z, br.To.X, br.To.Z, x, z, out _, out _);
                if (d <= br.Width / 2f + 0.3f) return br.DeckHeight;
            }

            return plan.HeightAt(x, z);
        }

        /// <summary>Where people leave by: the plan's way in, or the older plan's south gate.</summary>
        public static LocalPoint LeavingPoint(LocalEnvironmentLayout layout) =>
            layout.Plan != null ? new LocalPoint(layout.Plan.Arrival.X, layout.Plan.Arrival.Z) : new LocalPoint(2.5f, -layout.HalfExtent + 2f);

        public static float Distance(LocalPoint a, LocalPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static float Length(IReadOnlyList<LocalPoint> path)
        {
            var total = 0f;
            for (var i = 1; i < path.Count; i++) total += Distance(path[i - 1], path[i]);
            return total;
        }
    }
}
