using System;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Atlas
{
    /// <summary>
    /// Phase 16 (ADR-0081): what factions do on the roads, daily (order 456, after the political system has moved
    /// relationships and influence). A faction governing a road's end answers outlaws on it with a patrol it pays for
    /// from its treasury; its strength comes from the faction's presence there. A faction that cannot pay says so (once).
    /// Conflicts between factions are read from their relationships by the world-event system. Deterministic: factions
    /// and events are taken in record order.
    /// </summary>
    public sealed class FactionActivitySystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.faction-activity";
        public const int PatrolCost = 40;

        public string SystemId => Id;
        public int ExecutionOrder => 456;

        public void Execute(SimulationDayContext context)
        {
            var w = context.World;
            var day = context.Date.ToDayNumber();
            var now = day * 1440L;
            Relief(w, day, now); // Phase 18 (ADR-0090)
            Garrisons(w, day, now);
            if (w.Routes.Count == 0) return;
            foreach (var e in new System.Collections.Generic.List<WorldEventData>(w.WorldEvents))
            {
                if (!e.IsOpen || e.Type != WorldEventType.BanditActivity) continue;
                if (w.Groups.Exists(g => (g.Kind == TravelGroupKind.Patrol) && g.EventId == e.EventId && g.IsActive)) continue;
                var route = w.FindRoute(e.RouteId);
                if (route == null) continue;
                var post = Post(w, route);
                if (post == null) continue;
                var faction = w.FindFaction(post.FactionId);
                if (faction == null || faction.IsDissolved) continue;
                if (faction.Treasury < PatrolCost)
                {
                    if (e.Once("no-patrol:" + faction.FactionId)) e.Note(now, $"{faction.Name} cannot pay for a patrol");
                    continue;
                }

                if (!e.OncePer("patrol:", day)) continue;
                faction.Treasury -= PatrolCost;
                var presence = faction.FindPresence(post.SettlementId);
                var strength = Math.Max(4, Math.Min(12, 4 + (presence?.Strength ?? 0) / 20));
                var plan = RoutePlanner.Plan(w, post.SettlementId, route.Other(post.SettlementId));
                var patrol = new TravelingGroupData
                {
                    GroupId = "patrol:" + e.EventId + ":" + day, Kind = TravelGroupKind.Patrol, Name = $"{faction.Name} patrol from {post.Name}", FactionId = faction.FactionId,
                    LeaderNpcId = Captain(w, post), Members = strength, Strength = strength, OriginId = post.SettlementId, DestinationId = route.Other(post.SettlementId),
                    StationRouteId = route.RouteId, State = TravelGroupState.Travelling, DepartedAt = now, DueAt = now + 12 * 60, EventId = e.EventId,
                };
                patrol.Nodes.Add(post.SettlementId);
                patrol.Nodes.Add(route.Other(post.SettlementId));
                patrol.Segments.Add(route.RouteId);
                patrol.Note($"{TravelRules.Clock(now)} rode out ({strength} riders, {PatrolCost} coin from the treasury).");
                w.Groups.Add(patrol);
                var response = WorldEvents.Raise(w, WorldEventType.PatrolResponse, "patrol:" + e.EventId + ":" + day, now, 1, $"{faction.Name} sent {strength} riders against the outlaws",
                    x => { x.RouteId = route.RouteId; x.RegionId = route.RegionA; x.FactionA = faction.FactionId; x.GroupId = patrol.GroupId; }, out _);
                response.ExpiresAt = now + 12 * 1440L;
                e.Note(now, $"{faction.Name} sent a patrol of {strength}");
                WorldEvents.TellSettlements(w, response, new[] { route.A, route.B }, day);
            }
        }

        public const int ReliefCost = 50;
        public const int ReliefFood = 40;
        public const int GarrisonCost = 30;

        /// <summary>
        /// Phase 18 (ADR-0090): a faction that governs a hungry settlement buys it grain from its own treasury — once a week
        /// while the shortage lasts and the treasury can bear it. The settlement's shortage event says who helped.
        /// </summary>
        private static void Relief(WorldData w, long day, long now)
        {
            var week = day / 7;
            foreach (var s in w.Settlements)
            {
                if (s == null || !s.IsInFoodShortage || s.FactionId.IsEmpty) continue;
                var f = w.FindFaction(s.FactionId);
                if (f == null || f.IsDissolved || f.Treasury < ReliefCost) continue;
                var shortage = WorldEvents.Open(w, "shortage:" + s.SettlementId) ?? w.WorldEvents.Find(e => e.IsOpen && e.Type == WorldEventType.FoodShortage && e.SettlementId == s.SettlementId);
                if (shortage == null || !shortage.OncePer("relief:" + f.FactionId + ":", week)) continue;
                f.Treasury -= ReliefCost;
                var cap = s.Economy?.StorageCapacity.Get(Domain.Economy.ResourceTypes.Food) ?? 0;
                var have = s.ResourceInventory.Get(Domain.Economy.ResourceTypes.Food);
                var add = cap > 0 ? Math.Min(ReliefFood, Math.Max(0, cap - have)) : ReliefFood;
                s.ResourceInventory.Set(Domain.Economy.ResourceTypes.Food, have + add);
                shortage.Note(now, $"{f.Name} sent {add} food from its treasury");
            }
        }

        /// <summary>
        /// Phase 18 (ADR-0090): in skirmishes or open conflict, each side pays to garrison its town at the crossing — once a
        /// week, if its treasury can bear it — which steadies that town against the raids.
        /// </summary>
        private static void Garrisons(WorldData w, long day, long now)
        {
            var week = day / 7;
            foreach (var e in w.WorldEvents)
            {
                if (!e.IsOpen || e.Type != WorldEventType.FactionConflict || e.Severity < 3) continue;
                var route = w.FindRoute(e.RouteId);
                if (route == null) continue;
                foreach (var end in new[] { route.A, route.B })
                {
                    var s = w.FindSettlement(end);
                    var f = s == null ? null : w.FindFaction(s.FactionId);
                    if (f == null || f.IsDissolved || (f.FactionId != e.FactionA && f.FactionId != e.FactionB) || f.Treasury < GarrisonCost) continue;
                    if (!e.OncePer("garrison:" + s.SettlementId + ":", week)) continue;
                    f.Treasury -= GarrisonCost;
                    s.Stability = Math.Min(100, s.Stability + 2);
                    e.Note(now, $"{f.Name} garrisoned {s.Name}");
                }
            }
        }

        /// <summary>Which end answers: the one governed by a faction, the larger first.</summary>
        private static SettlementData Post(WorldData w, RouteSegmentData r)
        {
            var a = w.FindSettlement(r.A);
            var b = w.FindSettlement(r.B);
            if (a == null || a.FactionId.IsEmpty) return b != null && !b.FactionId.IsEmpty ? b : null;
            if (b == null || b.FactionId.IsEmpty) return a;
            return a.Population >= b.Population ? a : b;
        }

        private static NpcId Captain(WorldData w, SettlementData s)
        {
            NpcData best = null;
            foreach (var n in w.Npcs)
            {
                if (n.IsDeceased || n.HomeSettlementId != s.SettlementId || n.Role != NpcRole.Guard) continue;
                if (w.Party.Contains(n.NpcId) || w.Groups.Exists(g => g.IsActive && g.LeaderNpcId == n.NpcId)) continue;
                if (best == null || n.NpcId.CompareTo(best.NpcId) < 0) best = n;
            }

            return best?.NpcId ?? default;
        }
    }
}
