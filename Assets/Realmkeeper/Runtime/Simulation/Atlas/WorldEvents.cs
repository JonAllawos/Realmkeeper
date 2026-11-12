using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Simulation.Atlas
{
    /// <summary>
    /// Phase 16 (ADR-0080): the one way world events are made and moved. An event is created once per cause key while
    /// it is open; state changes are validated (open → escalating → resolved, failed or expired; closed states are final);
    /// what happened is learned by the people who could know it (the settlements it touches), from whom it spreads by the
    /// Phase 4 sharing of people who meet. The player's own knowledge is kept on the event and never changes the event.
    /// </summary>
    public static class WorldEvents
    {
        public const int MaxClosedKept = 60;

        public static WorldEventData Open(WorldData w, string key) => w.WorldEvents.Find(e => e.Key == key && e.IsOpen);

        /// <summary>The open event for a cause key, or a new one (the same cause never opens two).</summary>
        public static WorldEventData Raise(WorldData w, WorldEventType type, string key, long now, int severity, string cause, Action<WorldEventData> scope, out bool created)
        {
            created = false;
            var open = Open(w, key);
            if (open != null) return open;
            var e = new WorldEventData
            {
                EventId = "event:" + w.NextWorldEventNumber.ToString(CultureInfo.InvariantCulture), Type = type, State = WorldEventState.Active, Severity = Math.Max(1, Math.Min(5, severity)),
                Cause = cause, Key = key, CreatedAt = now, UpdatedAt = now,
            };
            scope?.Invoke(e);
            w.NextWorldEventNumber++;
            e.Note(now, "Began: " + cause + ".");
            w.WorldEvents.Add(e);
            created = true;
            Trim(w);
            return e;
        }

        /// <summary>Moves an event to a new state if allowed; false (and nothing changes) otherwise.</summary>
        public static bool Move(WorldEventData e, WorldEventState to, long now, string why)
        {
            if (e == null || !e.IsOpen || e.State == to) return false;
            if (to == WorldEventState.Active && e.State != WorldEventState.Escalating) return false;
            e.State = to;
            e.UpdatedAt = now;
            if (!e.IsOpen) e.ClosedAt = now;
            e.Note(now, Word(to) + (string.IsNullOrEmpty(why) ? "." : ": " + why + "."));
            return true;
        }

        public static string Word(WorldEventState s) =>
            s == WorldEventState.Escalating ? "Worsened" : s == WorldEventState.Resolved ? "Resolved" : s == WorldEventState.Failed ? "Failed" : s == WorldEventState.Expired ? "Passed" : "Eased";

        /// <summary>Keeps the record bounded: the oldest closed events are forgotten first (open ones never).</summary>
        private static void Trim(WorldData w)
        {
            var closed = 0;
            foreach (var e in w.WorldEvents) if (!e.IsOpen) closed++;
            for (var i = 0; i < w.WorldEvents.Count && closed > MaxClosedKept;)
            {
                if (!w.WorldEvents[i].IsOpen) { w.WorldEvents.RemoveAt(i); closed--; }
                else i++;
            }
        }

        /// <summary>A short account of the event, the way people tell it.</summary>
        public static string Telling(WorldData w, WorldEventData e)
        {
            var where = w.FindSettlement(e.SettlementId)?.Name;
            var route = w.FindRoute(e.RouteId);
            var road = route == null ? null : $"the road between {w.FindSettlement(route.A)?.Name} and {w.FindSettlement(route.B)?.Name}";
            switch (e.Type)
            {
                case WorldEventType.FoodShortage: return $"food is short in {where}";
                case WorldEventType.TradeDisruption: return $"trade is cut on {road ?? "a road"}";
                case WorldEventType.CaravanLost: return $"a caravan was lost on {road ?? "a road"}";
                case WorldEventType.BanditActivity: return $"{Outlaws(w, e)} work {road ?? "a road"}";
                case WorldEventType.PatrolResponse:
                    var against = w.FindGroup(e.GroupId)?.EventId is string t && w.FindWorldEvent(t) is WorldEventData band ? Outlaws(w, band) : "outlaws";
                    return $"{(w.FindFaction(e.FactionA)?.Name is string f ? f + "'s patrol" : "a patrol")} rides against the {against} on {road ?? "a road"}";
                case WorldEventType.BridgeDamaged: return $"the bridge on {road ?? "a road"} is {route?.Bridge.ToString().ToLowerInvariant() ?? "broken"}";
                case WorldEventType.RoadBlocked: return $"{road ?? "a road"} is barred";
                case WorldEventType.FactionConflict: return $"{w.FindFaction(e.FactionA)?.Name} and {w.FindFaction(e.FactionB)?.Name} are at odds";
                case WorldEventType.RefugeeMovement: return $"people are leaving {where}";
                case WorldEventType.LocalUnrest: return $"{where} is restless";
                default: return e.Type.ToString();
            }
        }

        /// <summary>Who the outlaws are, by the band's own creatures (two bands on one road read apart).</summary>
        private static string Outlaws(WorldData w, WorldEventData e)
        {
            var band = w.FindGroup(e.GroupId);
            var creature = band == null ? null : w.CreaturePopulations.Find(p => p.PopulationId == band.PopulationId)?.CreatureId;
            return creature == "highwayman" ? "highwaymen" : creature == "bandit" ? "bandits" : "outlaws";
        }

        /// <summary>
        /// The people of these settlements learn of the event (as of today). Spreading further is the people's own sharing.
        /// Deterministic knowledge IDs from the world seed, the event and the person.
        /// </summary>
        public static void TellSettlements(WorldData w, WorldEventData e, IEnumerable<SettlementId> settlements, long day)
        {
            var set = new HashSet<SettlementId>(settlements);
            if (set.Count == 0) return;
            var text = Telling(w, e);
            foreach (var n in w.Npcs)
            {
                if (n.IsDeceased || !set.Contains(n.HomeSettlementId)) continue;
                var npc = n;
                NpcMind.Learn(npc, KnowledgeTopic.RecentEvent, e.EventId, text, KnowledgeStatus.Known, 80, w.CurrentSimulationDate, default, 0, default,
                    () => new KnowledgeId(NpcHash.Guid(NpcHash.Of(w.Seed, "event-knowledge", e.EventId + "|" + npc.NpcId, day))));
            }
        }

        /// <summary>The player learns of an event (rumoured, or confirmed by seeing it), with a snapshot of how it stood.</summary>
        public static void PlayerLearns(WorldEventData e, EventKnowledge how, long now, string from)
        {
            if (e == null || how == EventKnowledge.Unknown) return;
            if (e.Known == EventKnowledge.Confirmed && how == EventKnowledge.Rumored && !e.KnowledgeOutdated) return;
            e.Known = how > e.Known ? how : e.Known;
            if (how == EventKnowledge.Confirmed) e.Known = EventKnowledge.Confirmed;
            e.KnownAt = now;
            e.KnownState = e.State;
            e.KnownSeverity = e.Severity;
            e.KnownFrom = from;
        }
    }
}
