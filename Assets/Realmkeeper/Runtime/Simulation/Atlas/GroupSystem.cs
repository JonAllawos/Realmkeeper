using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Simulation.Atlas
{
    /// <summary>
    /// Phase 16 (ADR-0079): groups on the roads, daily (order 160, after trade has sent and received the day's shipments).
    /// <list type="bullet">
    /// <item>Every shipment of the existing economy travels with a caravan: a leader from its origin, guards by the
    /// origin's size and the cargo's worth. Its goods are the shipment's: delivered by the trade system, or lost here.</item>
    /// <item>Bandits are the existing road bands (their numbers are the band's), holding the road their band works.</item>
    /// <item>A caravan whose way is impassable halts (its shipment waits); one passing outlaws may be attacked: outlaws
    /// against guards, an explainable comparison, with a seeded roll for whether they strike today.</item>
    /// <item>Patrols ride to a threatened road and break the band; refugees walk to a settlement with food and are
    /// counted into its people on arrival (out of their home's on departure: nobody is counted twice).</item>
    /// </list>
    /// </summary>
    public sealed class GroupSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.groups";
        public const int KeepFinishedDays = 5;

        public string SystemId => Id;
        public int ExecutionOrder => 160;

        public void Execute(SimulationDayContext context)
        {
            var w = context.World;
            if (w.Routes.Count == 0) return; // a world without its route network (test worlds)
            var day = context.Date.ToDayNumber();
            var now = day * 1440L;

            foreach (var p in w.CreaturePopulations)
                if (p.Habitat == "Road") Bandits(w, p, now);
            foreach (var s in w.Shipments) Caravan(w, s, now);

            foreach (var g in new List<TravelingGroupData>(w.Groups))
            {
                switch (g.Kind)
                {
                    case TravelGroupKind.Caravan: UpdateCaravan(context, g, day, now); break;
                    case TravelGroupKind.Patrol:
                    case TravelGroupKind.FactionPatrol: UpdatePatrol(context, g, now); break;
                    case TravelGroupKind.Refugees: UpdateRefugees(context, g, now); break;
                }
            }

            w.Groups.RemoveAll(g => !g.IsActive && g.DueAt >= 0 && now - g.DueAt > KeepFinishedDays * 1440L && g.Kind != TravelGroupKind.Bandits);
        }

        // ----- Bandits: the road bands --------------------------------------------------------------

        private static void Bandits(WorldData w, CreaturePopulationData p, long now)
        {
            var id = "bandits:" + p.PopulationId;
            var g = w.FindGroup(id);
            if (g == null)
            {
                var route = RouteOfBand(w, p);
                if (route == null) return;
                g = new TravelingGroupData
                {
                    GroupId = id, Kind = TravelGroupKind.Bandits, Name = (p.CreatureId == "highwayman" ? "Highwaymen" : "Outlaws") + " on the road", PopulationId = p.PopulationId,
                    StationRouteId = route.RouteId, OriginId = route.A, DestinationId = route.B, State = TravelGroupState.Stationed, DepartedAt = now, DueAt = -1,
                };
                g.Nodes.Add(route.A);
                g.Nodes.Add(route.B);
                g.Segments.Add(route.RouteId);
                w.Groups.Add(g);
            }

            g.Members = p.Count;
            g.Strength = p.Count * (p.CreatureId == "highwayman" ? 2 : 1);
            if (p.Count <= 0 && g.State == TravelGroupState.Stationed) { g.State = TravelGroupState.Dispersed; g.DueAt = now; g.Note(TravelRules.Clock(now) + " the band is broken."); }
            else if (p.Count > 0 && !g.IsActive) { g.State = TravelGroupState.Stationed; g.DueAt = -1; g.Note(TravelRules.Clock(now) + " outlaws are back on the road."); }
        }

        /// <summary>The road a band works: in its region, the road whose two ends' midpoint lies nearest the band's centre.</summary>
        public static RouteSegmentData RouteOfBand(WorldData w, CreaturePopulationData p)
        {
            RouteSegmentData best = null;
            long bestD = long.MaxValue;
            foreach (var r in w.Routes)
            {
                if (r.IsCrossing || r.RegionA != p.RegionId) continue;
                var a = w.FindSettlement(r.A);
                var b = w.FindSettlement(r.B);
                if (a == null || b == null) continue;
                long mx = (a.Position.X + b.Position.X) / 2 - p.Center.X;
                long mz = (a.Position.Z + b.Position.Z) / 2 - p.Center.Z;
                var d = mx * mx + mz * mz;
                if (d < bestD || d == bestD && string.CompareOrdinal(r.RouteId, best.RouteId) < 0) { best = r; bestD = d; }
            }

            return best;
        }

        public static TravelingGroupData BandOn(WorldData w, string routeId)
        {
            TravelingGroupData best = null;
            foreach (var g in w.Groups)
                if (g.Kind == TravelGroupKind.Bandits && g.StationRouteId == routeId && g.IsActive && g.Strength > 0 && (best == null || g.Strength > best.Strength)) best = g;
            return best;
        }

        // ----- Caravans: the shipments' carriers ----------------------------------------------------

        private static void Caravan(WorldData w, ShipmentData s, long now)
        {
            var id = "caravan:" + s.ShipmentId;
            if (w.FindGroup(id) != null) return;
            var origin = w.FindSettlement(s.OriginId);
            var dest = w.FindSettlement(s.DestinationId);
            if (origin == null || dest == null) return;
            var direct = w.FindRoute(s.OriginId, s.DestinationId);
            var plan = direct != null ? null : RoutePlanner.Plan(w, s.OriginId, s.DestinationId);
            var g = new TravelingGroupData
            {
                GroupId = id, Kind = TravelGroupKind.Caravan, Name = $"{origin.Name} caravan to {dest.Name}", FactionId = origin.FactionId, LeaderNpcId = Leader(w, origin),
                OriginId = s.OriginId, DestinationId = s.DestinationId, ShipmentId = s.ShipmentId, State = TravelGroupState.Travelling,
                DepartedAt = s.DepartureDay * 1440L + TravelRules.DayStartsAt, DueAt = s.ArrivalDay * 1440L + 18 * 60,
            };
            var guards = Guards(origin.SettlementType, s.Quantity);
            g.Members = guards + 2;
            g.Strength = guards * 2;
            if (direct != null) { g.Nodes.Add(s.OriginId); g.Nodes.Add(s.DestinationId); g.Segments.Add(direct.RouteId); }
            else if (plan != null) { g.Nodes.AddRange(plan.Nodes); g.Segments.AddRange(plan.Segments); }
            g.Note($"{TravelRules.Clock(now)} set out with {s.Quantity} {s.ResourceTypeId} ({guards} guards).");
            w.Groups.Add(g);
        }

        /// <summary>Guards a caravan hires: by the size of the town it leaves from, and one more for every 120 units of cargo (at most two more).</summary>
        public static int Guards(SettlementType type, int quantity) =>
            (type == SettlementType.City ? 5 : type == SettlementType.Town ? 3 : type == SettlementType.Village ? 2 : 1) + Math.Min(2, quantity / 120);

        /// <summary>The caravan's leader: a merchant of the town if there is one free, else another grown resident (lowest ID first).</summary>
        private static NpcId Leader(WorldData w, SettlementData origin)
        {
            // One person at a time leaves a settlement to lead a caravan (its other caravans go with hired drovers, unnamed):
            // the town's life goes on. Never someone who keeps a shop, holds a post, travels with the player or is escorted.
            foreach (var g in w.Groups)
                if (g.IsActive && !g.LeaderNpcId.IsEmpty && g.Kind == TravelGroupKind.Caravan && w.FindNpc(g.LeaderNpcId)?.HomeSettlementId == origin.SettlementId) return default;
            var keepers = new HashSet<NpcId>();
            foreach (var shop in w.Shops) keepers.Add(shop.ShopkeeperNpcId);
            NpcData best = null;
            foreach (var n in w.Npcs)
            {
                if (n.IsDeceased || n.HomeSettlementId != origin.SettlementId || n.Tier == NpcTier.Unassigned || keepers.Contains(n.NpcId)) continue;
                if (w.Party.Contains(n.NpcId) || w.FindEscort(n.NpcId) != null || Places.PlacePresence.PostedAt(w, n.NpcId) != null) continue;
                var rank = n.Role == NpcRole.Merchant ? 0 : n.Role == NpcRole.Labourer ? 1 : 2;
                if (rank > 1) continue;
                var bestRank = best == null ? 9 : best.Role == NpcRole.Merchant ? 0 : 1;
                if (best == null || rank < bestRank || rank == bestRank && n.NpcId.CompareTo(best.NpcId) < 0) best = n;
            }

            return best?.NpcId ?? default;
        }

        private static void UpdateCaravan(SimulationDayContext context, TravelingGroupData g, long day, long now)
        {
            var w = context.World;
            if (!g.IsActive) return;
            var s = w.Shipments.Find(x => x.ShipmentId == g.ShipmentId);
            if (s == null)
            {
                g.State = TravelGroupState.Arrived;
                g.DueAt = now;
                g.Note($"{TravelRules.Clock(now)} arrived at {w.FindSettlement(g.DestinationId)?.Name}.");
                return;
            }

            if (s.IsReturning && g.DestinationId != s.DestinationId)
            {
                g.DestinationId = s.DestinationId;
                g.Note($"{TravelRules.Clock(now)} turned back: no room for the goods.");
            }

            // A way that cannot be crossed holds the caravan (the trade system holds its shipment).
            var blocked = BlockedSegment(w, g);
            if (blocked != null)
            {
                if (g.State != TravelGroupState.Halted) g.Note($"{TravelRules.Clock(now)} halted: {Blocked(w, blocked)}.");
                g.State = TravelGroupState.Halted;
                var e = WorldEvents.Raise(w, WorldEventType.TradeDisruption, "disruption:" + blocked.RouteId, now, 2, Blocked(w, blocked),
                    x => { x.RouteId = blocked.RouteId; x.RegionId = blocked.RegionA; }, out var made);
                if (made) WorldEvents.TellSettlements(w, e, new[] { blocked.A, blocked.B, g.OriginId }, day);
                return;
            }

            if (g.State == TravelGroupState.Halted) { g.State = TravelGroupState.Travelling; g.Note($"{TravelRules.Clock(now)} on the move again."); }

            // Outlaws on the way may strike (once a day at most).
            foreach (var segment in g.Segments)
            {
                var band = BandOn(w, segment);
                if (band == null) continue;
                var chance = Math.Max(0, Math.Min(60, 10 + 10 * band.Strength - 6 * g.Strength));
                var roll = NpcHash.Roll(NpcHash.Of(w.Seed, "caravan-attack", g.GroupId, day), 100);
                if (roll >= chance) continue;
                var why = $"{band.Strength} outlaws against {g.Strength / 2} guards";
                if (band.Strength > g.Strength)
                {
                    Lose(context, g, s, band, why, day, now);
                    return;
                }

                // Driven off: the band is the poorer for it.
                var p = w.CreaturePopulations.Find(x => x.PopulationId == band.PopulationId);
                if (p != null && p.Count > 0) p.Count--;
                band.Note($"{TravelRules.Clock(now)} beaten off by the {g.Name} ({why}).");
                g.Note($"{TravelRules.Clock(now)} beat off outlaws ({why}).");
                return;
            }
        }

        /// <summary>The caravan is taken: its goods are lost (recorded as lost, never vanished), its people walk home with the news.</summary>
        private static void Lose(SimulationDayContext context, TravelingGroupData g, ShipmentData s, TravelingGroupData band, string why, long day, long now)
        {
            var w = context.World;
            var dest = w.FindSettlement(s.DestinationId);
            if (dest != null) EconomyMath.Flow(dest.Economy, s.ResourceTypeId, day).LostOnRoad += s.Quantity;
            var lost = s.Quantity;
            s.LostQuantity += s.Quantity;
            s.Quantity = 0;
            w.Shipments.Remove(s);
            var route = w.FindTradeRoute(s.TradeRouteId);
            if (route != null) route.Status = TradeRouteStatus.Active;
            g.State = TravelGroupState.Defeated;
            g.DueAt = now;
            g.Note($"{TravelRules.Clock(now)} taken by outlaws ({why}): {lost} {s.ResourceTypeId} lost.");
            band.Note($"{TravelRules.Clock(now)} took the {g.Name} ({why}).");
            var e = WorldEvents.Raise(w, WorldEventType.CaravanLost, "caravan-lost:" + g.GroupId, now, 3, $"outlaws took the {g.Name} ({why}); {lost} {s.ResourceTypeId} lost",
                x => { x.RouteId = band.StationRouteId; x.GroupId = g.GroupId; x.SettlementId = s.DestinationId; x.RegionId = dest?.RegionId ?? default; }, out var made);
            if (made)
            {
                WorldEvents.Move(e, WorldEventState.Resolved, now, "the survivors reached " + w.FindSettlement(g.OriginId)?.Name);
                // Phase 18 (ADR-0093): what was taken, by whom, bound where — so the goods can come back if the band is broken.
                e.Applied.Add("cargo:" + s.ResourceTypeId + ":" + lost.ToString(CultureInfo.InvariantCulture) + ":" + s.DestinationId);
                e.Applied.Add("band:" + band.PopulationId);
                WorldEvents.TellSettlements(w, e, new[] { g.OriginId, s.DestinationId }, day);
                var bandit = WorldEvents.Open(w, "bandits:" + band.PopulationId);
                if (bandit != null) WorldEvents.Move(bandit, WorldEventState.Escalating, now, "they took a caravan");
            }

            context.ReportEvent(HistoricalEventType.CaravanLost, EventSeverity.Warning, EventImportance.Notable, new[] { s.OriginId, s.DestinationId },
                new EventData().Set("caravan", g.Name).Set("lost", lost).Set("resource", s.ResourceTypeId.ToString()).Set("why", why), Array.Empty<NpcId>());
        }

        public static RouteSegmentData BlockedSegment(WorldData w, TravelingGroupData g)
        {
            foreach (var id in g.Segments)
            {
                var r = w.FindRoute(id);
                if (r != null && !r.Passable) return r;
            }

            return null;
        }

        private static string Blocked(WorldData w, RouteSegmentData r)
        {
            var where = $"between {w.FindSettlement(r.A)?.Name} and {w.FindSettlement(r.B)?.Name}";
            if (r.HasBridge && r.Bridge == Domain.Places.BridgeState.Destroyed) return "the bridge " + where + " is down";
            return "the road " + where + " is barred";
        }

        // ----- Patrols and refugees -----------------------------------------------------------------

        private static void UpdatePatrol(SimulationDayContext context, TravelingGroupData g, long now)
        {
            var w = context.World;
            if (!g.IsActive) return;
            if (g.State == TravelGroupState.Travelling && now >= g.DueAt)
            {
                g.State = TravelGroupState.Stationed;
                g.Note($"{TravelRules.Clock(now)} took up the watch on the road.");
            }

            if (g.State != TravelGroupState.Stationed) return;
            var band = BandOn(w, g.StationRouteId);
            var days = (int)((now - g.DueAt) / 1440);
            if (band == null || days >= 10)
            {
                g.State = TravelGroupState.Arrived;
                g.DueAt = now;
                g.Note($"{TravelRules.Clock(now)} went home: {(band == null ? "the road is clear" : "their ten days were up")}.");
                return;
            }

            var p = w.CreaturePopulations.Find(x => x.PopulationId == band.PopulationId);
            if (p == null) return;
            var broken = Math.Min(p.Count, Math.Max(1, g.Strength / 4));
            p.Count -= broken;
            band.Members = p.Count;
            band.Strength = Math.Max(0, band.Strength - broken * (p.CreatureId == "highwayman" ? 2 : 1));
            g.Note($"{TravelRules.Clock(now)} caught {broken} outlaw{(broken == 1 ? string.Empty : "s")}.");
        }

        private static void UpdateRefugees(SimulationDayContext context, TravelingGroupData g, long now)
        {
            var w = context.World;
            if (!g.IsActive || now < g.DueAt) return;
            var dest = w.FindSettlement(g.DestinationId);
            if (dest != null) dest.Population += g.Members; // counted in on arrival (they were counted out at home when they left)
            g.State = TravelGroupState.Arrived;
            g.Note($"{TravelRules.Clock(now)} {g.Members} people reached {dest?.Name}.");
            var e = w.FindWorldEvent(g.EventId);
            WorldEvents.Move(e, WorldEventState.Resolved, now, $"{g.Members} people found shelter in {dest?.Name}");
        }

        /// <summary>When a group set out on a plan, it is due after its walking hours spread over travel days.</summary>
        public static long DueAfter(long now, double hours) => now + (long)Math.Ceiling(hours / TravelRules.HoursPerTravelDay * 1440.0);
    }
}
