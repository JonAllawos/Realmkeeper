using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Atlas
{
    /// <summary>A planned way between two settlements over the route network.</summary>
    public sealed class RoutePlan
    {
        public List<SettlementId> Nodes { get; } = new List<SettlementId>();
        public List<string> Segments { get; } = new List<string>();
        public double Hours { get; set; }
        public double Km { get; set; }

        /// <summary>Begins by walking back over the road they stand on, which closed ahead of them.</summary>
        public bool Back { get; set; }

        /// <summary>Crossings, passes, bridges and trouble along the way, in words.</summary>
        public List<string> Notes { get; } = new List<string>();

        public string Key => string.Join(">", Segments);
    }

    /// <summary>
    /// Phase 16 (ADR-0078): deterministic planning over the persistent route network. Dijkstra on walking hours
    /// (<see cref="TravelRules.Hours"/>: length, terrain, condition, bridge state); impassable segments are skipped; ties go
    /// to the lower settlement ID. Alternatives are the next-best loopless routes (Yen's method), so the player can
    /// compare. The same planner moves caravans and patrols. A destination the network does not reach has no plan.
    /// </summary>
    public static class RoutePlanner
    {
        public static RoutePlan Plan(WorldData world, SettlementId from, SettlementId to, ICollection<string> avoid = null, ICollection<SettlementId> avoidNodes = null)
        {
            if (from == to || world.FindSettlement(from) == null || world.FindSettlement(to) == null) return null;
            var dist = new Dictionary<SettlementId, double> { [from] = 0 };
            var prev = new Dictionary<SettlementId, (SettlementId at, string via)>();
            var done = new HashSet<SettlementId>();
            var adjacency = Adjacency(world);
            while (true)
            {
                SettlementId best = default;
                var bestD = double.PositiveInfinity;
                foreach (var pair in dist)
                    if (!done.Contains(pair.Key) && (pair.Value < bestD || pair.Value == bestD && pair.Key.CompareTo(best) < 0)) { best = pair.Key; bestD = pair.Value; }
                if (double.IsInfinity(bestD)) return null;
                if (best == to) break;
                done.Add(best);
                if (!adjacency.TryGetValue(best, out var edges)) continue;
                foreach (var r in edges)
                {
                    if (avoid != null && avoid.Contains(r.RouteId)) continue;
                    var next = r.Other(best);
                    if (avoidNodes != null && avoidNodes.Contains(next) && next != to) continue;
                    var h = TravelRules.Hours(r);
                    if (double.IsInfinity(h)) continue;
                    var d = bestD + h;
                    if (!dist.TryGetValue(next, out var old) || d < old - 1e-9 || Math.Abs(d - old) < 1e-9 && prev.TryGetValue(next, out var p) && best.CompareTo(p.at) < 0)
                    {
                        dist[next] = d;
                        prev[next] = (best, r.RouteId);
                    }
                }
            }

            var plan = new RoutePlan();
            var at2 = to;
            var stack = new List<(SettlementId, string)>();
            while (at2 != from)
            {
                var p = prev[at2];
                stack.Add((at2, p.via));
                at2 = p.at;
            }

            plan.Nodes.Add(from);
            for (var i = stack.Count - 1; i >= 0; i--)
            {
                plan.Nodes.Add(stack[i].Item1);
                plan.Segments.Add(stack[i].Item2);
            }

            Measure(world, plan);
            return plan;
        }

        /// <summary>The best route and up to <paramref name="k"/> − 1 alternatives, cheapest first (Yen's k-shortest loopless paths).</summary>
        public static List<RoutePlan> Alternatives(WorldData world, SettlementId from, SettlementId to, int k = 3)
        {
            var found = new List<RoutePlan>();
            var first = Plan(world, from, to);
            if (first == null) return found;
            found.Add(first);
            var candidates = new List<RoutePlan>();
            for (var n = 1; n < k; n++)
            {
                var last = found[found.Count - 1];
                for (var i = 0; i < last.Nodes.Count - 1; i++)
                {
                    var spur = last.Nodes[i];
                    var rootNodes = last.Nodes.GetRange(0, i + 1);
                    var rootSegs = last.Segments.GetRange(0, i);
                    var avoid = new HashSet<string>();
                    foreach (var p in found)
                        if (p.Nodes.Count > i && Same(p.Nodes, rootNodes, i + 1)) avoid.Add(p.Segments[i]);
                    var avoidNodes = new HashSet<SettlementId>(rootNodes);
                    avoidNodes.Remove(spur);
                    var tail = Plan(world, spur, to, avoid, avoidNodes);
                    if (tail == null) continue;
                    var whole = new RoutePlan();
                    whole.Nodes.AddRange(rootNodes);
                    whole.Nodes.AddRange(tail.Nodes.GetRange(1, tail.Nodes.Count - 1));
                    whole.Segments.AddRange(rootSegs);
                    whole.Segments.AddRange(tail.Segments);
                    if (new HashSet<SettlementId>(whole.Nodes).Count != whole.Nodes.Count) continue; // loopless only
                    Measure(world, whole);
                    if (!found.Exists(p => p.Key == whole.Key) && !candidates.Exists(p => p.Key == whole.Key)) candidates.Add(whole);
                }

                if (candidates.Count == 0) break;
                candidates.Sort((a, b) => a.Hours != b.Hours ? a.Hours.CompareTo(b.Hours) : string.CompareOrdinal(a.Key, b.Key));
                found.Add(candidates[0]);
                candidates.RemoveAt(0);
            }

            return found;
        }

        private static bool Same(List<SettlementId> a, List<SettlementId> b, int count)
        {
            for (var i = 0; i < count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>Hours, kilometres and what lies on the way (crossings, passes, bridges, trouble the world knows of).</summary>
        public static void Measure(WorldData world, RoutePlan plan)
        {
            plan.Hours = 0;
            plan.Km = 0;
            plan.Notes.Clear();
            foreach (var id in plan.Segments)
            {
                var r = world.FindRoute(id);
                if (r == null) { plan.Hours = double.PositiveInfinity; continue; }
                plan.Hours += TravelRules.Hours(r);
                plan.Km += TravelRules.Km(r);
                var a = world.FindSettlement(r.A)?.Name;
                var b = world.FindSettlement(r.B)?.Name;
                if (r.IsCrossing) plan.Notes.Add($"crosses into {world.FindRegion(r.RegionB)?.Name} ({Word(r.Kind)} between {a} and {b})");
                else if (r.Kind == RouteKind.Pass) plan.Notes.Add($"a pass between {a} and {b}");
                if (r.HasBridge && r.Bridge != Domain.Places.BridgeState.Intact) plan.Notes.Add($"the bridge between {a} and {b} is {r.Bridge.ToString().ToLowerInvariant()}");
                if (r.Condition == RouteCondition.Worn) plan.Notes.Add($"the road between {a} and {b} is worn (slower)");
                // Only outlaws the player has heard of (an event about them they know of): no knowledge from nowhere.
                foreach (var g in world.Groups)
                    if (g.IsActive && g.Kind == TravelGroupKind.Bandits && g.StationRouteId == r.RouteId && g.Strength > 0 &&
                        world.WorldEvents.Exists(e => e.GroupId == g.GroupId && e.Known != EventKnowledge.Unknown))
                        plan.Notes.Add($"outlaws are said to work the road between {a} and {b}");
            }

            // One note per thing, in the order met.
            var seen = new HashSet<string>();
            plan.Notes.RemoveAll(n => !seen.Add(n));
        }

        public static string Word(RouteKind k) => k == RouteKind.Pass ? "a mountain pass" : k == RouteKind.Bridge ? "a bridge" : k == RouteKind.Trail ? "a forest trail" : k == RouteKind.Ford ? "a ford" : "a road";

        private static Dictionary<SettlementId, List<RouteSegmentData>> Adjacency(WorldData world)
        {
            var a = new Dictionary<SettlementId, List<RouteSegmentData>>();
            foreach (var r in world.Routes)
            {
                if (!a.TryGetValue(r.A, out var la)) a[r.A] = la = new List<RouteSegmentData>();
                if (!a.TryGetValue(r.B, out var lb)) a[r.B] = lb = new List<RouteSegmentData>();
                la.Add(r);
                lb.Add(r);
            }

            foreach (var l in a.Values) l.Sort((x, y) => string.CompareOrdinal(x.RouteId, y.RouteId));
            return a;
        }

        /// <summary>The region a traveller is in at a point of a route: the segment's own region, or across the border past halfway.</summary>
        public static RegionId RegionAt(WorldData world, RouteSegmentData r, SettlementId from, int metres)
        {
            var start = world.FindSettlement(from);
            var end = world.FindSettlement(r.Other(from));
            if (!r.IsCrossing || start == null || end == null) return start?.RegionId ?? r.RegionA;
            return metres * 2 >= TravelRules.Km(r) * 1000 ? end.RegionId : start.RegionId;
        }
    }

    /// <summary>
    /// Phase 16 (ADR-0081): simulation tiers. The player's region is local (full detail); its neighbours are regional
    /// (aggregates every day, people on their staggered updates); the rest are distant (aggregates every day, people's full
    /// updates weekly). Identities are never dropped: a distant person is still there, updated less often.
    /// </summary>
    public enum SimulationTier
    {
        Local = 0,
        Regional = 1,
        Distant = 2,
    }

    public static class SimulationTiers
    {
        public static RegionId PlayersRegion(WorldData world)
        {
            var p = world.Player;
            if (p == null) return world.Regions.Count > 0 ? world.Regions[0].RegionId : default;
            if (!p.RegionId.IsEmpty) return p.RegionId;
            var s = world.FindSettlement(p.SettlementId.IsEmpty ? p.LastSettlementId : p.SettlementId);
            return s?.RegionId ?? (world.Regions.Count > 0 ? world.Regions[0].RegionId : default);
        }

        public static SimulationTier TierOf(WorldData world, RegionId region)
        {
            if (world.Regions.Count <= 1) return SimulationTier.Local;
            var here = PlayersRegion(world);
            if (region == here) return SimulationTier.Local;
            var r = world.FindRegion(here);
            return r != null && r.Neighbours.Contains(region) ? SimulationTier.Regional : SimulationTier.Distant;
        }
    }
}
