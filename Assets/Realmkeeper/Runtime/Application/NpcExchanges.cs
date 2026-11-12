using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Application
{
    /// <summary>Two people of a settlement talking where the player can overhear them.</summary>
    public sealed class ExchangeView
    {
        public NpcId A { get; set; }
        public NpcId B { get; set; }
        public string Line { get; set; }

        /// <summary>A quarrel (rivals, or people who dislike each other), not a chat.</summary>
        public bool Quarrel { get; set; }
    }

    /// <summary>
    /// Phase 18 (ADR-0094): people of a settlement talking to each other — only people who really know each other
    /// (household, friends, workmates, rivals), only where both really are at the same place now and free to talk, and only
    /// about what they really know: a world event they saw themselves ("I saw it myself"), believe ("I'm told") or heard as
    /// talk ("there's talk…"); how the town's bread is; the traveller they have met. Rivals argue. Read-only: nothing in
    /// the world changes because the player overheard it. Deterministic within the hour.
    /// </summary>
    public static class NpcExchanges
    {
        public const int Max = 4;

        public static List<ExchangeView> Now(WorldData w, SettlementId settlement)
        {
            var list = new List<ExchangeView>();
            var town = w?.FindSettlement(settlement);
            if (town == null) return list;
            var hour = w.CurrentSimulationDate.ToDayNumber() * 24 + w.MinuteOfDay / 60;
            var pairs = new List<NpcRelationData>(w.NpcRelations);
            pairs.Sort((x, y) => x.NpcA != y.NpcA ? x.NpcA.CompareTo(y.NpcA) : x.NpcB.CompareTo(y.NpcB));
            var busy = new HashSet<NpcId>();
            foreach (var r in pairs)
            {
                if (r.Kind != NpcRelationKind.Friend && r.Kind != NpcRelationKind.Household && r.Kind != NpcRelationKind.Colleague && r.Kind != NpcRelationKind.Rival) continue;
                var a = w.FindNpc(r.NpcA);
                var b = w.FindNpc(r.NpcB);
                if (a == null || b == null || a.IsDeceased || b.IsDeceased || a.HomeSettlementId != settlement || b.HomeSettlementId != settlement) continue;
                if (busy.Contains(a.NpcId) || busy.Contains(b.NpcId) || w.Party.Contains(a.NpcId) || w.Party.Contains(b.NpcId)) continue;
                var wa = NpcActivityPlan.Now(w, a);
                var wb = NpcActivityPlan.Now(w, b);
                if (wa.Location.IsEmpty || wa.Location != wb.Location || !Talkative(wa.Activity) || !Talkative(wb.Activity)) continue;
                var quarrel = r.Kind == NpcRelationKind.Rival || r.Affinity <= -20;
                var topic = Topic(w, town, a, hour);
                busy.Add(a.NpcId);
                busy.Add(b.NpcId);
                list.Add(new ExchangeView
                {
                    A = a.NpcId, B = b.NpcId, Quarrel = quarrel,
                    Line = quarrel ? $"{a.Name} and {b.Name}, arguing. {a.Name}: {topic}" : $"{a.Name}, to {b.Name}: {topic}",
                });
                if (list.Count >= Max) break;
            }

            return list;
        }

        public static bool Talkative(ActivityKind k) =>
            k == ActivityKind.Socialising || k == ActivityKind.Eating || k == ActivityKind.Standing || k == ActivityKind.Sitting || k == ActivityKind.Resting;

        /// <summary>What this person talks about this hour, from what they know (and how sure they are of it).</summary>
        public static string Topic(WorldData w, SettlementData town, NpcData a, long hour)
        {
            var topics = new List<string>();
            foreach (var e in w.WorldEvents)
            {
                if (!e.IsOpen) continue;
                var k = NpcMind.Find(a, KnowledgeTopic.RecentEvent, e.EventId);
                if (k == null) continue;
                var told = WorldEvents.Telling(w, e);
                topics.Add(k.Status == KnowledgeStatus.Known && k.SourceNpcId.IsEmpty ? $"\"I saw it myself — {told}.\""
                    : k.Status == KnowledgeStatus.Rumor ? $"\"There's talk that {told}… if you believe it.\"" : $"\"I'm told {told}.\"");
            }

            var food = NpcMind.FoodState(town);
            topics.Add(food == "plenty" ? "\"Bread's cheap this week, for once.\"" : food == "short" ? "\"Have you seen what they want for bread now?\"" : "\"There's nothing in the stores. Nothing.\"");
            if (a.PlayerRegard.Familiarity > 0 && w.Player != null)
                topics.Add(a.PlayerRegard.Affinity >= 20 ? $"\"That {w.Player.Name} was asking after you.\"" : a.PlayerRegard.Hostility >= 30 ? $"\"Watch that {w.Player.Name}. I mean it.\"" : $"\"That traveller, {w.Player.Name}, was through again.\"");
            var pick = (int)(NpcHash.Of(w.Seed, "exchange", a.NpcId.ToString(), hour) % (ulong)topics.Count);
            return topics[pick];
        }
    }

    public sealed partial class WorldSession
    {
        /// <summary>Phase 18: who is talking to whom in the player's settlement, now.</summary>
        public List<ExchangeView> GetExchanges() =>
            HasWorld && _world.Player != null && _world.Player.IsInSettlement ? NpcExchanges.Now(_world, _world.Player.SettlementId) : new List<ExchangeView>();
    }
}
