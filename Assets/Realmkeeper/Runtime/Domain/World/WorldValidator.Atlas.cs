using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 16 invariants (ADR-0076–0080): regions on distinct atlas squares with symmetric neighbours; route segments
    /// unique, joining real settlements of the regions they name (a crossing joins neighbours); a journey whose nodes and
    /// segments agree; groups unique and on real roads (an active caravan with its shipment); world events unique, at most
    /// one open per cause, with real references.
    /// </summary>
    public static partial class WorldValidator
    {
        private static void ValidateAtlas(WorldData world, HashSet<SettlementId> settlementIds, ValidationResult result)
        {
            var regions = new HashSet<RegionId>();
            var squares = new HashSet<(int, int)>();
            foreach (var r in world.Regions)
            {
                regions.Add(r.RegionId);
                if (world.Regions.Count > 1 && !squares.Add((r.AtlasX, r.AtlasZ))) result.AddError($"Region '{r.Name}' shares its atlas square with another.");
            }

            foreach (var r in world.Regions)
                foreach (var n in r.Neighbours)
                {
                    var other = world.FindRegion(n);
                    if (other == null) result.AddError($"Region '{r.Name}' has an unknown neighbour.");
                    else if (!other.Neighbours.Contains(r.RegionId)) result.AddError($"Regions '{r.Name}' and '{other.Name}' do not agree that they are neighbours.");
                }

            var routes = new HashSet<string>();
            foreach (var r in world.Routes)
            {
                if (r == null || string.IsNullOrEmpty(r.RouteId) || !routes.Add(r.RouteId)) { result.AddError("A route segment is missing an ID or duplicated."); continue; }
                var a = world.FindSettlement(r.A);
                var b = world.FindSettlement(r.B);
                if (a == null || b == null || r.A == r.B) { result.AddError($"Route {r.RouteId} does not join two real settlements."); continue; }
                if (r.RouteId != RouteSegmentData.IdFor(r.A, r.B)) result.AddError($"Route {r.RouteId} is not named for its ends.");
                if (!regions.Contains(r.RegionA) || !regions.Contains(r.RegionB)) result.AddError($"Route {r.RouteId} names an unknown region.");
                if (!(a.RegionId == r.RegionA && b.RegionId == r.RegionB || a.RegionId == r.RegionB && b.RegionId == r.RegionA)) result.AddError($"Route {r.RouteId} names regions its ends are not in.");
                if (r.IsCrossing && world.FindRegion(r.RegionA)?.Neighbours.Contains(r.RegionB) != true) result.AddError($"Crossing {r.RouteId} joins regions that are not neighbours.");
                if (r.Cells <= 0 || r.TerrainPercent < 50) result.AddError($"Route {r.RouteId} has an impossible length or cost.");
            }

            var j = world.Journey;
            if (j != null)
            {
                if (j.Nodes.Count != j.Segments.Count + 1 || j.Segments.Count == 0) result.AddError("The journey's settlements and roads do not match.");
                else
                {
                    for (var i = 0; i < j.Segments.Count; i++)
                    {
                        var seg = world.FindRoute(j.Segments[i]);
                        if (seg == null || !seg.Joins(j.Nodes[i]) || !seg.Joins(j.Nodes[i + 1])) { result.AddError("The journey follows a road that does not join its settlements."); break; }
                    }

                    if (j.SegmentIndex < 0 || j.SegmentIndex > j.Segments.Count) result.AddError("The journey is past its own end.");
                    if (j.Nodes[j.Nodes.Count - 1] != j.DestinationId) result.AddError("The journey does not end where it is going.");
                    if (j.MetresIntoSegment < 0) result.AddError("The journey has negative progress.");
                }
            }

            var groups = new HashSet<string>();
            foreach (var g in world.Groups)
            {
                if (g == null || string.IsNullOrEmpty(g.GroupId) || !groups.Add(g.GroupId)) { result.AddError("A group on the roads is missing an ID or duplicated."); continue; }
                if (g.Members < 0 || g.Strength < 0) result.AddError($"Group {g.GroupId} has negative numbers.");
                foreach (var s in g.Segments) if (!routes.Contains(s)) result.AddError($"Group {g.GroupId} is on an unknown road.");
                foreach (var n in g.Nodes) if (!settlementIds.Contains(n)) result.AddError($"Group {g.GroupId} passes an unknown settlement.");
                if (g.Kind == TravelGroupKind.Caravan && g.IsActive && !world.Shipments.Exists(s => s.ShipmentId == g.ShipmentId)) result.AddError($"Caravan {g.GroupId} carries no shipment.");
                if (!g.LeaderNpcId.IsEmpty && world.FindNpc(g.LeaderNpcId) == null) result.AddError($"Group {g.GroupId} has an unknown leader.");
            }

            var events = new HashSet<string>();
            var openKeys = new HashSet<string>();
            foreach (var e in world.WorldEvents)
            {
                if (e == null || string.IsNullOrEmpty(e.EventId) || !events.Add(e.EventId)) { result.AddError("A world event is missing an ID or duplicated."); continue; }
                if (e.IsOpen && !string.IsNullOrEmpty(e.Key) && !openKeys.Add(e.Key)) result.AddError($"Two open world events share the cause '{e.Key}'.");
                if (e.Severity < 1 || e.Severity > 5) result.AddError($"World event {e.EventId} has a severity outside 1–5.");
                if (!e.SettlementId.IsEmpty && !settlementIds.Contains(e.SettlementId)) result.AddError($"World event {e.EventId} names an unknown settlement.");
                if (!string.IsNullOrEmpty(e.RouteId) && !routes.Contains(e.RouteId)) result.AddError($"World event {e.EventId} names an unknown road.");
                if (!e.IsOpen && e.ClosedAt < e.CreatedAt) result.AddError($"World event {e.EventId} closed before it began.");
                var seen = new HashSet<string>();
                foreach (var k in e.Applied) if (!seen.Add(k)) result.AddError($"World event {e.EventId} applied '{k}' twice.");
            }

            if (world.NextWorldEventNumber < 1) result.AddError("The next world event number is below 1.");
            foreach (var t in world.TradeRoutes)
                if (!string.IsNullOrEmpty(t.BlockedBy) && world.FindWorldEvent(t.BlockedBy) == null) result.AddError("A trade route is held by an unknown world event.");
        }
    }
}
