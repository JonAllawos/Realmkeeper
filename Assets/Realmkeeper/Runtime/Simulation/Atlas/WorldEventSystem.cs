using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Simulation.Atlas
{
    /// <summary>
    /// Phase 16 (ADR-0080): world events, evaluated daily (order 477, after the economy, people, politics, wildlife and
    /// gathering have run, and before quests react). Each kind reads its cause from an existing system and acts back on
    /// existing state; every consequence is keyed once on its event:
    /// <list type="bullet">
    /// <item>Food shortage: from the settlement's shortage; after a week it worsens and some people leave (a refugee group).</item>
    /// <item>Local unrest: from stability.</item>
    /// <item>Bandit activity: from a road band's strength; broken bands resolve it (by a patrol, caravan guards or the player).</item>
    /// <item>Bridges: old spans can crack (a seeded, explainable chance a month); a cracked span left unmended falls; the nearer
    /// settlement mends it with its own timber and stone; a fallen bridge closes the way to travellers and trade.</item>
    /// <item>Faction conflict: from the relationship's state (rivals: tension; hostile: confrontation, the crossing between
    /// their lands barred and held); it ends when the relationship mends.</item>
    /// <item>Trade disruption: resolved when the way is open again. Events with an expiry pass.</item>
    /// </list>
    /// The people of the settlements an event touches learn of it; the player confirms what they see where they are.
    /// </summary>
    public sealed class WorldEventSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.world-events";
        public const int BridgeCheckDays = 30;
        public const int BridgeCrackPercent = 12;
        public const int CrackedFallsAfterDays = 6;
        public const int RepairTimberPerDay = 10;
        public const int RepairStonePerDay = 5;

        public string SystemId => Id;
        public int ExecutionOrder => 477;

        public void Execute(SimulationDayContext context)
        {
            var w = context.World;
            if (w.Routes.Count == 0) return;
            var day = context.Date.ToDayNumber();
            var now = day * 1440L;
            var before = new Dictionary<string, WorldEventState>();
            foreach (var e in w.WorldEvents) before[e.EventId] = e.State;

            foreach (var s in w.Settlements)
            {
                Shortage(w, s, day, now);
                Unrest(w, s, day, now);
            }

            foreach (var g in w.Groups) if (g.Kind == TravelGroupKind.Bandits) Bandits(w, g, day, now);
            foreach (var r in w.Routes) if (r.HasBridge) Bridge(w, r, day, now);
            Conflicts(w, day, now);
            Disruptions(w, now);
            foreach (var e in w.WorldEvents)
                if (e.IsOpen && e.ExpiresAt >= 0 && now >= e.ExpiresAt) WorldEvents.Move(e, WorldEventState.Expired, now, "it has run its course");

            Quests(context, now);
            Observe(w, now);
            Chronicle(context, before);
        }

        // ----- Quests the events call for -----------------------------------------------------------

        /// <summary>
        /// Outlaws on a road call for someone to break them; a bridge its town cannot mend calls for timber. Each event asks
        /// once (keyed), through the one quest engine, bound to real people and places; the quest holds while the event is
        /// open (source "event:{id}").
        /// </summary>
        private static void Quests(SimulationDayContext context, long now)
        {
            var w = context.World;
            Simulation.Quests.QuestReporter report = (type, severity, importance, settlements, facts, npcs) =>
            {
                var data = new EventData();
                foreach (var pair in facts) data.Set(pair.Key, pair.Value);
                context.ReportEvent(type, severity, importance, settlements, data, npcs);
            };
            foreach (var e in new List<WorldEventData>(w.WorldEvents))
            {
                if (!e.IsOpen) continue;
                var route = w.FindRoute(e.RouteId);
                if (e.Type == WorldEventType.BanditActivity && route != null && e.Severity >= 2 && e.Once("quest"))
                {
                    var band = w.FindGroup(e.GroupId);
                    var creature = band != null ? w.CreaturePopulations.Find(p => p.PopulationId == band.PopulationId)?.CreatureId : null;
                    Ask(w, e, Domain.Quests.QuestCatalog.RoadOutlaws, route.A, route.B, creature == "highwayman" ? "highwayman" : "bandit", new[] { NpcRole.Guard, NpcRole.Elder, NpcRole.Resident }, report);
                }
                else if (e.Type == WorldEventType.BridgeDamaged && route != null && route.RepairDays == 0 && now - e.CreatedAt >= 2 * 1440L && e.Once("quest"))
                {
                    var mender = Mender(w, route);
                    if (mender != null) Ask(w, e, Domain.Quests.QuestCatalog.BridgeTimber, mender.SettlementId, route.Other(mender.SettlementId), null,
                        new[] { NpcRole.Smith, NpcRole.Elder, NpcRole.Labourer, NpcRole.Resident }, report);
                }
                else if (e.Type == WorldEventType.FoodShortage && e.Once("quest-link"))
                {
                    // The Phase 12 emergent shortage quest is this event's quest: they are linked, not duplicated.
                    var q = w.Quests.Find(x => x.IsOpen || x.State == Domain.Quests.QuestState.Available ? x.SourceKey != null && x.SourceKey.StartsWith("shortage:", StringComparison.Ordinal) && x.SourceKey.EndsWith(e.SettlementId.ToString(), StringComparison.Ordinal) : false);
                    if (q != null) e.Note(now, "a call for food went out (" + Domain.Quests.QuestCatalog.Find(q.DefinitionId)?.Name + ")");
                }
            }
        }

        private static void Ask(WorldData w, WorldEventData e, string definition, SettlementId home, SettlementId other, string creature, NpcRole[] roles, Simulation.Quests.QuestReporter report)
        {
            var def = Domain.Quests.QuestCatalog.Find(definition);
            if (def == null) return;
            NpcData giver = null;
            foreach (var role in roles)
            {
                foreach (var n in w.Npcs)
                {
                    if (n.IsDeceased || n.HomeSettlementId != home || n.Role != role || n.Tier == NpcTier.Unassigned) continue;
                    if (w.Party.Contains(n.NpcId) || w.Groups.Exists(g => g.IsActive && g.LeaderNpcId == n.NpcId)) continue;
                    if (giver == null || n.NpcId.CompareTo(giver.NpcId) < 0) giver = n;
                }

                if (giver != null) break;
            }

            if (giver == null) return;
            var given = new Dictionary<string, string>(StringComparer.Ordinal) { ["giver"] = giver.NpcId.ToString(), ["other"] = other.ToString() };
            if (creature != null) given["creature"] = creature;
            var q = Simulation.Quests.QuestLifecycle.Create(w, def, given, "event:" + e.EventId, null, report, out _);
            if (q != null) e.Note(e.UpdatedAt, $"{giver.Name} of {w.FindSettlement(home)?.Name} asks for help ({q.InstanceId})");
        }

        // ----- Economy and people -------------------------------------------------------------------

        private static void Shortage(WorldData w, SettlementData s, long day, long now)
        {
            var key = "shortage:" + s.SettlementId;
            var open = WorldEvents.Open(w, key);
            if (!s.IsInFoodShortage)
            {
                if (open != null) WorldEvents.Move(open, WorldEventState.Resolved, now, "the stores are filling again");
                return;
            }

            var e = WorldEvents.Raise(w, WorldEventType.FoodShortage, key, now, s.Population >= 1000 ? 3 : 2, $"{s.Name}'s stores ran low",
                x => { x.SettlementId = s.SettlementId; x.RegionId = s.RegionId; }, out var made);
            if (made) WorldEvents.TellSettlements(w, e, new[] { s.SettlementId }, day);
            if (now - e.CreatedAt < 7 * 1440L || !e.Once("worsened")) return;
            e.Severity = Math.Min(5, e.Severity + 1);
            WorldEvents.Move(e, WorldEventState.Escalating, now, "a week without enough");
            Refugees(w, s, e, day, now);
        }

        /// <summary>Some people leave a hungry settlement for the nearest one with food: counted out now, counted in on arrival.</summary>
        private static void Refugees(WorldData w, SettlementData s, WorldEventData cause, long day, long now)
        {
            RoutePlan best = null;
            SettlementData to = null;
            foreach (var o in w.Settlements)
            {
                if (o == s || o.IsInFoodShortage) continue;
                var plan = RoutePlanner.Plan(w, s.SettlementId, o.SettlementId);
                if (plan == null) continue;
                if (best == null || plan.Hours < best.Hours || plan.Hours == best.Hours && o.SettlementId.CompareTo(to.SettlementId) < 0) { best = plan; to = o; }
            }

            if (best == null) return;
            var leaving = Math.Max(3, Math.Min(s.Population / 10, s.Population * 3 / 100));
            if (s.Population - leaving < 20) return;
            var ev = WorldEvents.Raise(w, WorldEventType.RefugeeMovement, "refugees:" + cause.EventId, now, 2, $"{leaving} people left {s.Name} for {to.Name}",
                x => { x.SettlementId = s.SettlementId; x.RegionId = s.RegionId; }, out var made);
            if (!made) return;
            s.Population -= leaving;
            var g = new TravelingGroupData
            {
                GroupId = "refugees:" + ev.EventId, Kind = TravelGroupKind.Refugees, Name = "Families from " + s.Name, Members = leaving, Strength = 0, OriginId = s.SettlementId,
                DestinationId = to.SettlementId, State = TravelGroupState.Travelling, DepartedAt = now, DueAt = GroupSystem.DueAfter(now, best.Hours), EventId = ev.EventId,
            };
            g.Nodes.AddRange(best.Nodes);
            g.Segments.AddRange(best.Segments);
            g.Note($"{TravelRules.Clock(now)} {leaving} people set out for {to.Name}.");
            w.Groups.Add(g);
            ev.GroupId = g.GroupId;
            WorldEvents.TellSettlements(w, ev, new[] { s.SettlementId, to.SettlementId }, day);
        }

        private static void Unrest(WorldData w, SettlementData s, long day, long now)
        {
            var key = "unrest:" + s.SettlementId;
            var open = WorldEvents.Open(w, key);
            if (s.Stability < 30)
            {
                var e = WorldEvents.Raise(w, WorldEventType.LocalUnrest, key, now, s.Stability < 15 ? 4 : 2, $"stability in {s.Name} fell to {s.Stability}",
                    x => { x.SettlementId = s.SettlementId; x.RegionId = s.RegionId; x.FactionA = s.FactionId; }, out var made);
                if (made) WorldEvents.TellSettlements(w, e, new[] { s.SettlementId }, day);
            }
            else if (open != null && s.Stability >= 40) WorldEvents.Move(open, WorldEventState.Resolved, now, $"stability is back to {s.Stability}");
        }

        // ----- Security -----------------------------------------------------------------------------

        public const int BanditActivityAt = 4;

        private static void Bandits(WorldData w, TravelingGroupData band, long day, long now)
        {
            var key = "bandits:" + band.PopulationId;
            var open = WorldEvents.Open(w, key);
            if (!band.IsActive || band.Strength <= 0)
            {
                if (open != null) WorldEvents.Move(open, WorldEventState.Resolved, now, "the band is broken");
                return;
            }

            if (band.Strength < BanditActivityAt && open == null) return;
            var route = w.FindRoute(band.StationRouteId);
            if (route == null) return;
            var e = WorldEvents.Raise(w, WorldEventType.BanditActivity, key, now, Math.Min(5, 1 + band.Strength / 3), $"{band.Strength} outlaws hold the road",
                x => { x.RouteId = route.RouteId; x.RegionId = route.RegionA; x.GroupId = band.GroupId; }, out var made);
            band.EventId = e.EventId;
            if (made) WorldEvents.TellSettlements(w, e, new[] { route.A, route.B }, day);
            e.Severity = Math.Min(5, 1 + band.Strength / 3);
            if (e.State == WorldEventState.Escalating && band.Strength < BanditActivityAt) WorldEvents.Move(e, WorldEventState.Active, now, "the band is thinning");
        }

        // ----- Infrastructure -----------------------------------------------------------------------

        private static void Bridge(WorldData w, RouteSegmentData r, long day, long now)
        {
            var key = "bridge:" + r.RouteId;
            var open = WorldEvents.Open(w, key);
            if (r.Bridge == BridgeState.Intact)
            {
                var offset = (long)(NpcHash.Of(w.Seed, "bridge-check", r.RouteId) % BridgeCheckDays);
                if (open != null || day % BridgeCheckDays != offset) return;
                if (NpcHash.Roll(NpcHash.Of(w.Seed, "bridge-crack", r.RouteId, day), 100) >= BridgeCrackPercent) return;
                r.Bridge = BridgeState.Damaged;
                r.RepairDays = 0;
                var e = WorldEvents.Raise(w, WorldEventType.BridgeDamaged, key, now, 2, $"old timbers cracked on the bridge between {Name(w, r.A)} and {Name(w, r.B)}",
                    x => { x.RouteId = r.RouteId; x.RegionId = r.RegionA; }, out _);
                r.BlockedBy = e.EventId;
                WorldEvents.TellSettlements(w, e, new[] { r.A, r.B }, day);
                return;
            }

            var ev = open ?? WorldEvents.Raise(w, WorldEventType.BridgeDamaged, key, now, r.Bridge == BridgeState.Destroyed ? 4 : 2,
                $"the bridge between {Name(w, r.A)} and {Name(w, r.B)} is {r.Bridge.ToString().ToLowerInvariant()}", x => { x.RouteId = r.RouteId; x.RegionId = r.RegionA; }, out _);

            // The nearer settlement (the larger of the two) mends it with its own timber and stone, a day of work at a time.
            var mender = Mender(w, r);
            if (mender != null && mender.ResourceInventory.Get(ResourceTypes.Timber) >= RepairTimberPerDay && mender.ResourceInventory.Get(ResourceTypes.Stone) >= RepairStonePerDay)
            {
                mender.ResourceInventory.TryRemove(ResourceTypes.Timber, RepairTimberPerDay);
                mender.ResourceInventory.TryRemove(ResourceTypes.Stone, RepairStonePerDay);
                EconomyMath.Flow(mender.Economy, ResourceTypes.Timber, day).Consumed += RepairTimberPerDay;
                EconomyMath.Flow(mender.Economy, ResourceTypes.Stone, day).Consumed += RepairStonePerDay;
                r.RepairDays++;
                if (r.RepairDays >= (r.Bridge == BridgeState.Destroyed ? 6 : 3))
                {
                    r.Bridge = BridgeState.Intact;
                    r.RepairDays = 0;
                    r.BlockedBy = null;
                    Unblock(w, r, ev.EventId);
                    WorldEvents.Move(ev, WorldEventState.Resolved, now, $"{mender.Name} mended the bridge");
                    return;
                }
            }
            else if (r.Bridge == BridgeState.Damaged && now - ev.CreatedAt >= CrackedFallsAfterDays * 1440L && ev.Once("fell"))
            {
                r.Bridge = BridgeState.Destroyed;
                r.RepairDays = 0;
                ev.Severity = 4;
                WorldEvents.Move(ev, WorldEventState.Escalating, now, "the cracked span fell: nobody could mend it in time");
            }

            if (r.Bridge == BridgeState.Destroyed) Block(w, r, ev.EventId);
        }

        private static SettlementData Mender(WorldData w, RouteSegmentData r)
        {
            var a = w.FindSettlement(r.A);
            var b = w.FindSettlement(r.B);
            if (a == null) return b;
            if (b == null) return a;
            return a.Population > b.Population || a.Population == b.Population && a.SettlementId.CompareTo(b.SettlementId) < 0 ? a : b;
        }

        /// <summary>Trade between the two ends stops while the way is closed (the trade system holds what is on the road).</summary>
        public static void Block(WorldData w, RouteSegmentData r, string eventId)
        {
            r.BlockedBy = eventId;
            foreach (var t in w.TradeRoutes)
                if (r.Joins(t.OriginId) && r.Joins(t.DestinationId) && string.IsNullOrEmpty(t.BlockedBy)) t.BlockedBy = eventId;
        }

        public static void Unblock(WorldData w, RouteSegmentData r, string eventId)
        {
            if (r.BlockedBy == eventId) r.BlockedBy = null;
            foreach (var t in w.TradeRoutes)
                if (t.BlockedBy == eventId) t.BlockedBy = null;
        }

        private static void Disruptions(WorldData w, long now)
        {
            foreach (var e in w.WorldEvents)
            {
                if (!e.IsOpen || e.Type != WorldEventType.TradeDisruption) continue;
                var r = w.FindRoute(e.RouteId);
                if (r == null || r.Passable) WorldEvents.Move(e, WorldEventState.Resolved, now, "the way is open again");
            }
        }

        // ----- Factions -----------------------------------------------------------------------------

        private static void Conflicts(WorldData w, long day, long now)
        {
            foreach (var rel in w.Relations)
            {
                var a = w.FindFaction(rel.FactionA);
                var b = w.FindFaction(rel.FactionB);
                if (a == null || b == null || a.IsDissolved || b.IsDissolved) continue;
                var crossing = CrossingBetween(w, a.FactionId, b.FactionId);
                if (crossing == null) continue; // conflict needs a shared border to touch the roads
                var key = "conflict:" + rel.FactionA + "|" + rel.FactionB;
                var open = WorldEvents.Open(w, key);
                var state = PoliticalMath.StateOf(rel);
                if (state != RelationState.Rival && state != RelationState.Hostile)
                {
                    if (open == null) continue;
                    Unblock(w, crossing, open.EventId);
                    if (crossing.Condition == RouteCondition.Blocked && crossing.BlockedBy == null) crossing.Condition = RouteCondition.Good;
                    foreach (var g in w.Groups)
                        if (g.Kind == TravelGroupKind.FactionPatrol && g.EventId == open.EventId && g.IsActive) { g.State = TravelGroupState.Arrived; g.DueAt = now; g.Note($"{TravelRules.Clock(now)} stood down."); }
                    WorldEvents.Move(open, WorldEventState.Resolved, now, $"{a.Name} and {b.Name} are no longer at odds");
                    continue;
                }

                var e = WorldEvents.Raise(w, WorldEventType.FactionConflict, key, now, state == RelationState.Hostile ? 4 : 2, $"{a.Name} and {b.Name} are {(state == RelationState.Hostile ? "hostile" : "rivals")}",
                    x => { x.FactionA = a.FactionId; x.FactionB = b.FactionId; x.RouteId = crossing.RouteId; x.RegionId = crossing.RegionA; }, out var made);
                if (made) WorldEvents.TellSettlements(w, e, new[] { crossing.A, crossing.B }, day);
                if (state != RelationState.Hostile) continue;
                if (e.State == WorldEventState.Active) WorldEvents.Move(e, WorldEventState.Escalating, now, "open hostility: the crossing is barred");
                if (!e.Once("blockade")) continue;

                // Confrontation: the crossing is barred and held by the stronger side's soldiers; it gains standing for it.
                crossing.Condition = RouteCondition.Blocked;
                Block(w, crossing, e.EventId);
                var holder = Strength(w, a.FactionId) >= Strength(w, b.FactionId) ? a : b;
                holder.Influence = Math.Min(holder.InfluenceTarget > 0 ? Math.Max(holder.InfluenceTarget, holder.Influence + 5) : holder.Influence + 5, 1000);
                var patrol = new TravelingGroupData
                {
                    GroupId = "blockade:" + e.EventId, Kind = TravelGroupKind.FactionPatrol, Name = holder.Name + " soldiers at the crossing", FactionId = holder.FactionId,
                    Members = 6, Strength = 6 + Strength(w, holder.FactionId) / 50, OriginId = crossing.A, DestinationId = crossing.B, StationRouteId = crossing.RouteId,
                    State = TravelGroupState.Stationed, DepartedAt = now, DueAt = -1, EventId = e.EventId,
                };
                patrol.Segments.Add(crossing.RouteId);
                patrol.Nodes.Add(crossing.A);
                patrol.Nodes.Add(crossing.B);
                patrol.Note($"{TravelRules.Clock(now)} barred the crossing.");
                w.Groups.Add(patrol);
                var barred = WorldEvents.Raise(w, WorldEventType.RoadBlocked, "blocked:" + crossing.RouteId, now, 3, $"{holder.Name} barred the crossing",
                    x => { x.RouteId = crossing.RouteId; x.RegionId = crossing.RegionA; x.FactionA = holder.FactionId; }, out _);
                WorldEvents.TellSettlements(w, barred, new[] { crossing.A, crossing.B }, day);
            }

            // A barred crossing opens when its conflict ends.
            foreach (var e in w.WorldEvents)
            {
                if (!e.IsOpen || e.Type != WorldEventType.RoadBlocked) continue;
                var r = w.FindRoute(e.RouteId);
                var cause = w.WorldEvents.Find(x => x.Type == WorldEventType.FactionConflict && x.RouteId == e.RouteId && x.IsOpen);
                if (cause != null || r == null) continue;
                if (r.Condition == RouteCondition.Blocked) r.Condition = RouteCondition.Good;
                WorldEvents.Move(e, WorldEventState.Resolved, now, "the soldiers stood down");
            }
        }

        private static int Strength(WorldData w, FactionId f)
        {
            var total = 0;
            var faction = w.FindFaction(f);
            if (faction != null) foreach (var p in faction.Presence) total += p.Strength;
            return total;
        }

        /// <summary>The crossing between two factions' lands: a crossing whose ends they govern, one each.</summary>
        public static RouteSegmentData CrossingBetween(WorldData w, FactionId a, FactionId b)
        {
            foreach (var r in w.Routes)
            {
                if (!r.IsCrossing) continue;
                var sa = w.FindSettlement(r.A);
                var sb = w.FindSettlement(r.B);
                if (sa == null || sb == null) continue;
                if (sa.FactionId == a && sb.FactionId == b || sa.FactionId == b && sb.FactionId == a) return r;
            }

            return null;
        }

        private static string Name(WorldData w, SettlementId id) => w.FindSettlement(id)?.Name ?? "?";

        // ----- Knowledge and the chronicle ----------------------------------------------------------

        /// <summary>The player sees for themselves what touches the place they are in or the road they are on.</summary>
        public static void Observe(WorldData w, long now)
        {
            var p = w.Player;
            if (p == null) return;
            var here = p.IsInSettlement ? p.SettlementId : default;
            string road = null;
            var j = w.Journey;
            if (j != null && (j.Status == JourneyStatus.Travelling || j.Status == JourneyStatus.Halted || j.Status == JourneyStatus.Interrupted) && j.SegmentIndex < j.Segments.Count)
                road = j.Segments[j.SegmentIndex];
            foreach (var e in w.WorldEvents)
            {
                if (!e.IsOpen) continue;
                var seen = !here.IsEmpty && e.SettlementId == here || road != null && e.RouteId == road;
                if (!seen && !here.IsEmpty && e.RouteId != null && w.FindRoute(e.RouteId) is RouteSegmentData r && r.Joins(here) && e.Type != WorldEventType.BanditActivity) seen = true;
                if (seen) WorldEvents.PlayerLearns(e, EventKnowledge.Confirmed, now, "seen for yourself");
            }
        }

        private static void Chronicle(SimulationDayContext context, Dictionary<string, WorldEventState> before)
        {
            var w = context.World;
            foreach (var e in w.WorldEvents)
            {
                var existed = before.TryGetValue(e.EventId, out var was);
                if (existed && was == e.State) continue;
                if (e.Type == WorldEventType.CaravanLost) continue; // its own chronicle line
                var settlements = new List<SettlementId>();
                if (!e.SettlementId.IsEmpty) settlements.Add(e.SettlementId);
                if (w.FindRoute(e.RouteId) is RouteSegmentData r) { settlements.Add(r.A); settlements.Add(r.B); }
                context.ReportEvent(HistoricalEventType.WorldEventChanged, e.State == WorldEventState.Escalating ? EventSeverity.Warning : e.IsOpen ? EventSeverity.Info : EventSeverity.Positive,
                    e.Severity >= 3 ? EventImportance.Notable : EventImportance.Minor, settlements,
                    new EventData().Set("event", e.EventId).Set("what", WorldEvents.Telling(w, e)).Set("state", existed ? WorldEvents.Word(e.State).ToLowerInvariant() : "began").Set("why", e.Cause),
                    Array.Empty<NpcId>());
            }
        }
    }
}
