using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Npcs
{
    /// <summary>
    /// Shared operations on a person's inner life: relationships (created only with a cause and
    /// capped), memories (bounded, least salient forgotten first) and knowledge (observed facts,
    /// beliefs and rumours, kept apart from the world's truth). Used by the daily system, the
    /// initializer and the commands, so the rules are the same everywhere.
    /// </summary>
    public static class NpcMind
    {
        // ----- Relationships -----------------------------------------------------------------------

        public static (NpcId, NpcId) Order(NpcId a, NpcId b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

        public static int RelationCount(WorldData world, NpcId id)
        {
            var count = 0;
            foreach (var r in world.NpcRelations) if (r.Involves(id)) count++;
            return count;
        }

        /// <summary>
        /// The relationship between two people, creating it for a stated cause when both still have
        /// room (<see cref="NpcBounds.MaxRelationsPerNpc"/>). Returns null when it does not exist
        /// and cannot be created.
        /// </summary>
        public static NpcRelationData Relate(WorldData world, NpcId a, NpcId b, NpcRelationCause cause, NpcRelationKind kind, WorldDate today)
        {
            if (a.IsEmpty || b.IsEmpty || a == b) return null;
            var existing = world.FindNpcRelation(a, b);
            if (existing != null) return existing;
            if (RelationCount(world, a) >= NpcBounds.MaxRelationsPerNpc || RelationCount(world, b) >= NpcBounds.MaxRelationsPerNpc) return null;
            var (first, second) = Order(a, b);
            var r = new NpcRelationData
            {
                NpcA = first,
                NpcB = second,
                Kind = kind,
                Origin = cause,
                LastCause = cause,
                Since = today,
                LastChanged = today,
            };
            switch (cause)
            {
                case NpcRelationCause.SharedHousehold: r.Affinity = 35; r.Trust = 70; r.Respect = 50; break;
                case NpcRelationCause.SharedWorkplace: r.Affinity = 10; r.Trust = 50; r.Respect = 45; break;
                case NpcRelationCause.FactionHierarchy: r.Affinity = 5; r.Trust = 45; r.Respect = 60; break;
                default: r.Affinity = 0; r.Trust = 35; r.Respect = 40; break;
            }

            world.NpcRelations.Add(r);
            return r;
        }

        /// <summary>The kind a relationship should have given its values (household and hierarchy ties keep theirs).</summary>
        public static NpcRelationKind KindFor(NpcRelationData r, NpcRules rules)
        {
            if (r.Kind == NpcRelationKind.Household || r.Kind == NpcRelationKind.Superior) return r.Kind;
            if (r.Affinity >= rules.FriendAffinity) return NpcRelationKind.Friend;
            if (r.Affinity <= rules.RivalAffinity || r.Rivalry >= 60) return NpcRelationKind.Rival;
            return r.Origin == NpcRelationCause.SharedWorkplace ? NpcRelationKind.Colleague : NpcRelationKind.Acquaintance;
        }

        // ----- Memories ----------------------------------------------------------------------------

        /// <summary>Adds a memory, forgetting the least salient (then oldest) one when full. Returns the memory kept.</summary>
        public static NpcMemoryData Remember(NpcData npc, NpcMemoryData memory)
        {
            if (!memory.EventId.IsEmpty)
            {
                foreach (var m in npc.Memories)
                {
                    if (m.EventId == memory.EventId)
                    {
                        // Already remembered: a fuller account raises it, never duplicates it.
                        m.Salience = Math.Max(m.Salience, memory.Salience);
                        m.Confidence = Math.Max(m.Confidence, memory.Confidence);
                        if (memory.Source < m.Source) m.Source = memory.Source;
                        return m;
                    }
                }
            }

            npc.Memories.Add(memory);
            while (npc.Memories.Count > NpcBounds.MaxMemories) npc.Memories.RemoveAt(LeastSalient(npc.Memories));
            return npc.Memories.Contains(memory) ? memory : null;
        }

        private static int LeastSalient(List<NpcMemoryData> memories)
        {
            var index = 0;
            for (var i = 1; i < memories.Count; i++)
            {
                var m = memories[i];
                var worst = memories[index];
                if (m.Salience < worst.Salience || m.Salience == worst.Salience && m.Date < worst.Date) index = i;
            }

            return index;
        }

        // ----- Knowledge ---------------------------------------------------------------------------

        public static NpcKnowledgeData Find(NpcData npc, KnowledgeTopic topic, string subject)
        {
            foreach (var k in npc.Knowledge)
            {
                if (k.Topic == topic && string.Equals(k.SubjectId, subject, StringComparison.Ordinal)) return k;
            }

            return null;
        }

        /// <summary>
        /// Records what a person now holds about a subject. An observation replaces whatever they held;
        /// hearsay only replaces weaker or older hearsay, and never overrides something they observed
        /// recently. Bounded: the weakest, oldest piece is dropped when full.
        /// </summary>
        public static NpcKnowledgeData Learn(NpcData npc, KnowledgeTopic topic, string subject, string value, KnowledgeStatus status, int confidence,
            WorldDate today, NpcId source, int hops, EventId eventId, Func<KnowledgeId> newId)
        {
            var k = Find(npc, topic, subject);
            if (k != null)
            {
                if (status != KnowledgeStatus.Known)
                {
                    // Hearsay never overrides what they currently know first-hand (that goes stale weekly);
                    // it replaces a belief or rumour only when it is at least as confident, or newer.
                    if (k.Status == KnowledgeStatus.Known) return k;
                    if (confidence < k.Confidence && k.LearnedOn >= today) return k;
                    if (k.Value == value && confidence <= k.Confidence) return k;
                }
                k.Value = value;
                k.Status = status;
                k.Confidence = confidence;
                k.LearnedOn = today;
                k.SourceNpcId = source;
                k.Hops = hops;
                if (!eventId.IsEmpty) k.EventId = eventId;
                return k;
            }

            k = new NpcKnowledgeData
            {
                KnowledgeId = newId(),
                Topic = topic,
                SubjectId = subject,
                Value = value,
                Status = status,
                Confidence = confidence,
                LearnedOn = today,
                SourceNpcId = source,
                Hops = hops,
                EventId = eventId,
            };
            npc.Knowledge.Add(k);
            while (npc.Knowledge.Count > NpcBounds.MaxKnowledge) npc.Knowledge.RemoveAt(Weakest(npc.Knowledge));
            return npc.Knowledge.Contains(k) ? k : null;
        }

        private static int Weakest(List<NpcKnowledgeData> knowledge)
        {
            var index = 0;
            for (var i = 1; i < knowledge.Count; i++)
            {
                var k = knowledge[i];
                var worst = knowledge[index];
                var score = Score(k);
                var worstScore = Score(worst);
                if (score < worstScore || score == worstScore && k.LearnedOn < worst.LearnedOn) index = i;
            }

            return index;
        }

        private static int Score(NpcKnowledgeData k) => k.Confidence - (int)k.Status * 20;

        // ----- What a person can observe ---------------------------------------------------------------

        public static string FoodState(SettlementData s) =>
            s.Economy.DaysWithoutFood > 0 ? "starving" : s.IsInFoodShortage ? "short" : "plenty";

        public static string ProsperityState(SettlementData s) =>
            s.Prosperity >= 65 ? "thriving" : s.Prosperity >= 35 ? "steady" : "struggling";

        /// <summary>
        /// Refreshes what a person sees for themselves in the settlement they are in today: its food,
        /// how it is doing, who governs it, and (if they belong to a faction) who leads it.
        /// </summary>
        public static void Observe(WorldData world, NpcData npc, SettlementData here, WorldDate today, Func<KnowledgeId> newId)
        {
            if (here == null) return;
            var id = here.SettlementId.ToString();
            Learn(npc, KnowledgeTopic.SettlementFood, id, FoodState(here), KnowledgeStatus.Known, 100, today, default, 0, default, newId);
            Learn(npc, KnowledgeTopic.SettlementProsperity, id, ProsperityState(here), KnowledgeStatus.Known, 100, today, default, 0, default, newId);
            Learn(npc, KnowledgeTopic.SettlementGovernor, id, here.FactionId.IsEmpty ? "" : here.FactionId.ToString(), KnowledgeStatus.Known, 100, today, default, 0, default, newId);
            if (!npc.FactionId.IsEmpty)
            {
                var f = world.FindFaction(npc.FactionId);
                if (f != null)
                    Learn(npc, KnowledgeTopic.FactionLeader, f.FactionId.ToString(), f.LeaderNpcId.IsEmpty ? "" : f.LeaderNpcId.ToString(),
                        KnowledgeStatus.Known, 100, today, default, 0, default, newId);
            }
        }

        /// <summary>Settlement a location belongs to (null when unknown).</summary>
        public static SettlementData SettlementAt(WorldData world, LocationId location)
        {
            var l = location.IsEmpty ? null : world.FindLocation(location);
            return l == null ? null : world.FindSettlement(l.SettlementId);
        }

        public static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
