using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;

namespace Realmkeeper.Application.Overland
{
    /// <summary>
    /// Phase 24 (ADR-0113): one travel state for the road and the country. A journey's position (segment, from, metres) is a
    /// point on the region's own road; the country's nearest road point is a journey position. These convert between them.
    /// </summary>
    public static class OverlandRoads
    {
        /// <summary>Within this distance of the point where the player left the road, the journey can be taken up again.</summary>
        public const float RejoinMetres = 80f;

        /// <summary>
        /// The point of the country a journey position stands at: on the region's road path between the segment's two
        /// settlements (by the fraction travelled). A crossing's first half lies in the start's region, between its way in and
        /// the edge toward the other region; its second half likewise in the other region. False when there is no map.
        /// </summary>
        public static bool PointOf(WorldSession session, WorldData w, RouteSegmentData seg, SettlementId from, int metres, out RegionId region, out float x, out float z)
        {
            region = default;
            x = z = 0f;
            if (seg == null) return false;
            var a = w.FindSettlement(from);
            var b = w.FindSettlement(seg.Other(from));
            if (a == null || b == null) return false;
            var km = Math.Max(0.001, TravelRules.Km(seg));
            var f = (float)Math.Max(0, Math.Min(1, metres / (km * 1000)));
            if (!seg.IsCrossing)
            {
                region = a.RegionId;
                var terrain = session.GetOverlandTerrain(RealmIds.Surface, region);
                if (terrain == null) return false;
                var path = PathBetween(terrain.Map, a.SettlementId, b.SettlementId);
                if (path != null && path.Count > 1)
                {
                    var t = f * (path.Count - 1);
                    var i = Math.Min(path.Count - 2, (int)Math.Floor(t));
                    var (x0, z0) = OverlandScale.CellCentre(path[i]);
                    var (x1, z1) = OverlandScale.CellCentre(path[i + 1]);
                    x = x0 + (x1 - x0) * (t - i);
                    z = z0 + (z1 - z0) * (t - i);
                }
                else
                {
                    var (ax, az) = OverlandQueries.Gate(terrain, a.Position);
                    var (bx, bz) = OverlandQueries.Gate(terrain, b.Position);
                    x = ax + (bx - ax) * f;
                    z = az + (bz - az) * f;
                }

                Settle(terrain, ref x, ref z);
                return true;
            }

            // A crossing: the half nearer each end lies in that end's region, from its way in to the edge toward the other.
            var near = f < 0.5f ? a : b;
            var far = f < 0.5f ? b : a;
            var share = f < 0.5f ? f / 0.5f : (1f - f) / 0.5f; // 0 at the settlement, 1 at the border
            region = near.RegionId;
            var t2 = session.GetOverlandTerrain(RealmIds.Surface, region);
            var rn = w.FindRegion(near.RegionId);
            var rf = w.FindRegion(far.RegionId);
            if (t2 == null || rn == null || rf == null) return false;
            var (gx, gz) = OverlandQueries.Gate(t2, near.Position);
            var edge = OverlandQueries.EdgeCell(t2, near.Position, rf.AtlasX - rn.AtlasX, rf.AtlasZ - rn.AtlasZ);
            var (ex, ez) = OverlandScale.CellCentre(edge);
            x = gx + (ex - gx) * share;
            z = gz + (ez - gz) * share;
            Settle(t2, ref x, ref z);
            return true;
        }

        /// <summary>The road between two settlements on a region's map, ordered from <paramref name="from"/>; null when there is none.</summary>
        public static IReadOnlyList<MapCoord> PathBetween(RegionMap map, SettlementId from, SettlementId to)
        {
            foreach (var road in map.Roads)
            {
                if (road.From == from && road.To == to) return road.Cells;
                if (road.From == to && road.To == from)
                {
                    var r = new List<MapCoord>(road.Cells);
                    r.Reverse();
                    return r;
                }
            }

            return null;
        }

        /// <summary>A point moved onto walkable ground nearby when the road crosses something (a bridge's water, a pass).</summary>
        private static void Settle(OverlandTerrain terrain, ref float x, ref float z)
        {
            if (terrain.Walkable(x, z)) return;
            var (ax, az) = OverlandQueries.Approach(terrain, terrain.Map.InBounds(OverlandScale.CellAt(terrain.Map, x, z)) ? OverlandScale.CellAt(terrain.Map, x, z) : new MapCoord(0, 0), 3);
            x = ax;
            z = az;
        }

        /// <summary>
        /// The nearest point of a road (a route between two of the region's settlements) to a point of the country, with the
        /// journey position it is. False when the region has no such road.
        /// </summary>
        public static bool NearestRoad(WorldData w, OverlandTerrain terrain, float x, float z, out RouteSegmentData seg, out SettlementId from, out int metres, out float rx, out float rz)
        {
            seg = null;
            from = default;
            metres = 0;
            rx = rz = 0f;
            var best = float.MaxValue;
            foreach (var road in terrain.Map.Roads)
            {
                if (road.Cells.Count < 2) continue;
                var route = w.FindRoute(RouteSegmentData.IdFor(road.From, road.To));
                if (route == null) continue;
                for (var i = 0; i < road.Cells.Count; i++)
                {
                    var (cx, cz) = OverlandScale.CellCentre(road.Cells[i]);
                    var d = (cx - x) * (cx - x) + (cz - z) * (cz - z);
                    if (d >= best) continue;
                    best = d;
                    seg = route;
                    from = road.From;
                    metres = (int)(i / (double)(road.Cells.Count - 1) * TravelRules.Km(route) * 1000);
                    rx = cx;
                    rz = cz;
                }
            }

            return seg != null;
        }

        /// <summary>The player's halted journey's point in the country, when it lies in their region; else false.</summary>
        public static bool HaltedPoint(WorldSession session, WorldData w, out float x, out float z)
        {
            x = z = 0f;
            var j = w.Journey;
            var p = w.Player;
            if (j == null || p == null || (j.Status != JourneyStatus.Halted && j.Status != JourneyStatus.Interrupted)) return false;
            var (seg, from, metres) = Application.Atlas.TravelCommandHandlers.Position(w);
            if (seg == null || !PointOf(session, w, seg, from, metres, out var region, out x, out z)) return false;
            return region == p.RegionId && RealmIds.IsSurface(p.RealmId);
        }
    }

    /// <summary>Phase 24 (ADR-0113): what a waypoint points at, resolved against what the player knows.</summary>
    public sealed class WaypointView
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public RegionId RegionId { get; set; }
        public string RegionName { get; set; }

        /// <summary>In the player's own country now: its position (region metres) and distance.</summary>
        public bool InThisRegion { get; set; }

        public float X { get; set; }
        public float Z { get; set; }
        public float Metres { get; set; }

        /// <summary>In another region: the way on to take from here (a neighbouring region's id), or null when none is open.</summary>
        public string NextExitId { get; set; }

        public string NextRegionName { get; set; }
        public int RegionsAway { get; set; }

        /// <summary>A short line for the HUD.</summary>
        public string Line { get; set; }
    }

    public static class Waypoints
    {
        /// <summary>The point and region a waypoint key names, if the player knows it; else null (with why).</summary>
        public static (string name, string realm, RegionId region, float x, float z)? Resolve(WorldSession session, WorldData w, string key, out string why)
        {
            why = null;
            if (string.IsNullOrEmpty(key)) { why = "Nothing is marked."; return null; }
            var colon = key.IndexOf(':');
            var kind = colon < 0 ? key : key.Substring(0, colon);
            var id = colon < 0 ? string.Empty : key.Substring(colon + 1);
            switch (kind)
            {
                case "settlement":
                {
                    if (!Guid.TryParse(id, out var g)) break;
                    var s = w.FindSettlement(new SettlementId(g));
                    if (s == null) break;
                    var r = w.FindRegion(s.RegionId);
                    if (r == null || r.Discovery == RegionDiscovery.Unknown) { why = "That land is uncharted."; return null; }
                    // Its way in when it is in the player's country; elsewhere its cell (no other region's country is built for this).
                    var p0 = w.Player;
                    var t = p0 != null && p0.IsOverland && p0.RegionId == s.RegionId ? session.GetOverlandTerrain(RealmIds.Surface, s.RegionId) : null;
                    var (x, z) = t != null ? OverlandQueries.Gate(t, s.Position) : OverlandScale.CellCentre(s.Position);
                    return (s.Name, RealmIds.Surface, s.RegionId, x, z);
                }

                case "place":
                {
                    var pl = w.FindPlace(id);
                    if (pl == null || pl.ParentKind != PlaceParentKind.Region) break;
                    if (pl.Discovery < PlaceDiscovery.Discovered) { why = "You have not found that place."; return null; }
                    var (x, z) = OverlandScale.CellCentre(pl.Cell);
                    return (pl.Name, RealmIds.Surface, pl.RegionId, x, z);
                }

                case "gateway":
                {
                    var g = w.FindGateway(id);
                    if (g == null) break;
                    if (!g.Discovered) { why = "You have not found it."; return null; }
                    var p = w.Player;
                    var here = p != null && !RealmIds.IsSurface(p.RealmId) ? p.RealmId : RealmIds.Surface;
                    var end = g.A.RealmId == here ? g.A : g.B;
                    var (x, z) = OverlandScale.CellCentre(end.Cell);
                    return (g.Name, end.RealmId, end.RegionId, x, z);
                }

                case "landmark":
                {
                    var l = Landmarks.Find(session, w, id);
                    if (l == null) break;
                    if (!Landmarks.IsFound(w, id)) { why = "You have not found it."; return null; }
                    return (l.Name, l.RealmId, l.RegionId, l.X, l.Z);
                }

                case "journey":
                {
                    var p = w.Player;
                    if (p == null || !OverlandRoads.HaltedPoint(session, w, out var x, out var z)) { why = "There is no journey waiting on the road here."; return null; }
                    return ("Where you left the road", RealmIds.Surface, p.RegionId, x, z);
                }
            }

            why ??= "There is no such destination.";
            return null;
        }

        /// <summary>The player's waypoint as the HUD shows it, or null when there is none (or it is no longer known).</summary>
        public static WaypointView View(WorldSession session, WorldData w)
        {
            var p = w?.Player;
            if (p == null || string.IsNullOrEmpty(p.Waypoint)) return null;
            var target = Resolve(session, w, p.Waypoint, out _);
            if (target == null) return null;
            var (name, realm, region, x, z) = target.Value;
            var here = RealmIds.IsSurface(p.RealmId) ? RealmIds.Surface : p.RealmId;
            var v = new WaypointView { Key = p.Waypoint, Name = name, RegionId = region, X = x, Z = z };
            v.RegionName = RealmIds.IsSurface(realm) ? w.FindRegion(region)?.Name : w.FindRealm(realm)?.FindRegion(region)?.Name;
            if (p.IsOverland && realm == here && region == p.RegionId)
            {
                v.InThisRegion = true;
                v.Metres = (float)Math.Sqrt((x - p.Pose.X) * (x - p.Pose.X) + (z - p.Pose.Z) * (z - p.Pose.Z));
                v.Line = $"{name}: {Distance(v.Metres)}";
                return v;
            }

            if (realm != here)
            {
                v.Line = $"{name}: beyond a gateway";
                return v;
            }

            var from = p.IsOverland ? p.RegionId : SimulationTiers.PlayersRegion(w);
            var path = RegionPath(w, from, region);
            if (path == null) { v.Line = $"{name}, in {v.RegionName}: no open way on from here"; return v; }
            v.RegionsAway = path.Count;
            if (path.Count > 0)
            {
                v.NextExitId = path[0].ToString();
                v.NextRegionName = w.FindRegion(path[0])?.Name;
                v.Line = $"{name}, in {v.RegionName}: by the way on to {v.NextRegionName}" + (path.Count > 1 ? $" ({path.Count} regions away)" : string.Empty);
            }
            else v.Line = name;

            return v;
        }

        /// <summary>The regions to pass through (excluding <paramref name="from"/>, including <paramref name="to"/>) by open crossings; null when there is no way.</summary>
        public static List<RegionId> RegionPath(WorldData w, RegionId from, RegionId to)
        {
            if (from == to) return new List<RegionId>();
            var prev = new Dictionary<RegionId, RegionId> { [from] = from };
            var queue = new Queue<RegionId>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                var next = new List<RegionId>();
                foreach (var route in w.Routes)
                {
                    if (!route.IsCrossing || !route.Passable) continue;
                    if (route.RegionA == r) next.Add(route.RegionB);
                    else if (route.RegionB == r) next.Add(route.RegionA);
                }

                next.Sort((a, b) => a.CompareTo(b)); // deterministic
                foreach (var n in next)
                {
                    if (prev.ContainsKey(n)) continue;
                    prev[n] = r;
                    if (n == to)
                    {
                        var path = new List<RegionId>();
                        for (var c = to; c != from; c = prev[c]) path.Insert(0, c);
                        return path;
                    }

                    queue.Enqueue(n);
                }
            }

            return null;
        }

        public static string Distance(float m) => m < 1000f ? $"{m:0} m" : $"{m / 1000f:0.0} km";
    }
}
