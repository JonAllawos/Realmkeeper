using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Campaigns;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Campaigns;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Simulation.Party
{
    /// <summary>
    /// Phase 17 (ADR-0087): companions as people in the story. They react to the player's decisions by their own
    /// personality (loyalty moves for a stated reason, once per decision); they say what they think, later, when asked;
    /// two companions who come down on opposite sides of a decision grow apart, and side by side grow closer (the Phase 4
    /// relationship between them, not a new score); they remark on places where their own people live; and a companion
    /// with an old quarrel elsewhere may ask the player's help with it. Only companions travelling with the player take
    /// part: reserve members are not there.
    /// </summary>
    public static class CompanionStories
    {
        /// <summary>A reaction smaller than this is indifference (no loyalty change, no remark).</summary>
        public const int Indifferent = 2;

        /// <summary>The most a single decision moves a companion's loyalty.</summary>
        public const int MaxSwing = 8;

        /// <summary>Rivalry at which two companions' quarrel shows (and can be talked through).</summary>
        public const int QuarrelAt = 20;

        /// <summary>How strongly a companion of this personality feels about this choice (signed, bounded).</summary>
        public static int Reaction(NpcData npc, ChoiceDefinition choice)
        {
            var total = 0;
            foreach (var l in choice.Leanings)
            {
                if (!Enum.TryParse<NpcTrait>(l.Trait, out var trait)) continue;
                total += l.Sign * (npc.Personality[trait] - 50) / 10;
            }

            return Math.Max(-MaxSwing, Math.Min(MaxSwing, total));
        }

        public static void React(WorldData w, CampaignInstanceData c, DecisionDefinition d, ChoiceDefinition choice, QuestReporter report)
        {
            var present = CompanionLoyalty.Present(w);
            if (present.Count == 0 || choice.Leanings.Count == 0) return;
            var now = QuestClock.Now(w);
            var feelings = new List<(NpcData npc, int score)>();
            foreach (var npc in present)
            {
                var score = Reaction(npc, choice);
                feelings.Add((npc, score));
                if (Math.Abs(score) < Indifferent) continue;
                var summary = CampaignEngine.Text(w, c, choice.Summary);
                var key = "campaign:" + c.CampaignId + ":" + d.Id;
                CompanionLoyalty.Apply(w, npc.NpcId, key, score, (score > 0 ? "approved when you " : "objected when you ") + Lower(summary), report);
                var lean = Strongest(npc, choice, score > 0);
                var said = lean == null ? null : score > 0 ? lean.Approve : lean.Disapprove;
                if (said != null) Remark(w, npc.NpcId, key, Fill(w, c, said, npc), now);
                NpcMind.Remember(npc, new NpcMemoryData
                {
                    MemoryId = new MemoryId(NpcHash.Guid(NpcHash.Of(w.Seed, "companion-decision", key + "|" + npc.NpcId))), Date = w.CurrentSimulationDate,
                    Category = MemoryCategory.Personal, Subject = (score > 0 ? "Stood by " : "Disagreed with ") + w.Player?.Name + " when they " + Lower(summary), SettlementId = npc.HomeSettlementId,
                    Salience = 40 + Math.Abs(score) * 4, Source = MemorySource.Experienced,
                });
            }

            // Companions on opposite sides of the same choice grow apart; on the same side, closer.
            for (var i = 0; i < feelings.Count; i++)
                for (var j = i + 1; j < feelings.Count; j++)
                {
                    var (a, sa) = feelings[i];
                    var (b, sb) = feelings[j];
                    if (Math.Abs(sa) < Indifferent || Math.Abs(sb) < Indifferent) continue;
                    var key = "pair:" + c.CampaignId + ":" + d.Id + ":" + a.NpcId + ":" + b.NpcId;
                    if (c.Applied.Contains(key)) continue;
                    c.Applied.Add(key);
                    var apart = Math.Sign(sa) != Math.Sign(sb);
                    var r = NpcMind.Relate(w, a.NpcId, b.NpcId, apart ? NpcRelationCause.Quarrel : NpcRelationCause.Kindness, NpcRelationKind.Acquaintance, w.CurrentSimulationDate);
                    if (r == null) continue;
                    if (apart)
                    {
                        Relations.Shift(r, "Rivalry", 12, NpcRelationCause.Quarrel, w.CurrentSimulationDate);
                        Relations.Shift(r, "Affinity", -8, NpcRelationCause.Quarrel, w.CurrentSimulationDate);
                        var pro = sa > 0 ? a : b;
                        var con = sa > 0 ? b : a;
                        Remark(w, con.NpcId, key, $"\"{pro.Name} thinks you did right back there. I don't, and I've told {Them(pro)} so. We'll see if it passes.\"", now);
                    }
                    else
                    {
                        Relations.Shift(r, "Affinity", 6, NpcRelationCause.Kindness, w.CurrentSimulationDate);
                        Relations.Shift(r, "Trust", 4, NpcRelationCause.Kindness, w.CurrentSimulationDate);
                    }
                }
        }

        private static string Them(NpcData n) => n.Sex == NpcSex.Female ? "her" : n.Sex == NpcSex.Male ? "him" : "them";

        private static ChoiceLeaning Strongest(NpcData npc, ChoiceDefinition choice, bool approve)
        {
            ChoiceLeaning best = null;
            var bestScore = 0;
            foreach (var l in choice.Leanings)
            {
                if (!Enum.TryParse<NpcTrait>(l.Trait, out var trait)) continue;
                var s = l.Sign * (npc.Personality[trait] - 50);
                if (approve ? s > bestScore : s < bestScore) { best = l; bestScore = s; }
            }

            return best;
        }

        private static string Lower(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        private static string Fill(WorldData w, CampaignInstanceData c, string text, NpcData companion) =>
            CampaignEngine.Text(w, c, text).Replace("{companion}", companion.Name);

        /// <summary>Something a companion will say when asked (once per key; the newest twelve are kept).</summary>
        public static bool Remark(WorldData w, NpcId who, string key, string text, long now)
        {
            var rec = w.FindCompanion(who);
            if (rec == null || string.IsNullOrEmpty(text)) return false;
            foreach (var r in rec.Remarks) if (r.Key == key) return false;
            rec.Remarks.Add(new CompanionRemarkData { Key = key, Text = text, At = now });
            while (rec.Remarks.Count > CompanionRecordData.MaxRemarks) rec.Remarks.RemoveAt(0);
            return true;
        }

        /// <summary>The oldest thing a companion has not yet said (it is now said), or null.</summary>
        public static string Hear(WorldData w, NpcId who)
        {
            var rec = w.FindCompanion(who);
            if (rec == null) return null;
            foreach (var r in rec.Remarks)
                if (!r.Heard)
                {
                    r.Heard = true;
                    return r.Text;
                }

            return null;
        }

        /// <summary>
        /// The player arrives somewhere: a companion travelling with them who has their own people there (household,
        /// friend or rival, still living) says so — once per companion and place.
        /// </summary>
        public static void Arrived(WorldData w, SettlementId where)
        {
            var now = QuestClock.Now(w);
            var place = w.FindSettlement(where);
            if (place == null) return;
            foreach (var npc in CompanionLoyalty.Present(w))
            {
                NpcData known = null;
                NpcRelationData bond = null;
                foreach (var r in w.NpcRelations)
                {
                    if (r.NpcA != npc.NpcId && r.NpcB != npc.NpcId) continue;
                    if (r.Kind != NpcRelationKind.Household && r.Kind != NpcRelationKind.Friend && r.Kind != NpcRelationKind.Rival) continue;
                    var other = w.FindNpc(r.NpcA == npc.NpcId ? r.NpcB : r.NpcA);
                    if (other == null || other.IsDeceased || other.HomeSettlementId != where || w.Party.Contains(other.NpcId)) continue;
                    if (known == null || other.NpcId.CompareTo(known.NpcId) < 0) { known = other; bond = r; }
                }

                if (known == null) continue;
                var text = bond.Kind == NpcRelationKind.Household ? $"\"{known.Name} lives here. Family. If we've time, I'd like to look in on them.\""
                    : bond.Kind == NpcRelationKind.Rival ? $"\"{known.Name} lives here. We don't get on, and I'd as soon we didn't cross paths.\""
                    : $"\"{known.Name} is here somewhere — an old friend. It's been too long.\"";
                Remark(w, npc.NpcId, "home:" + npc.NpcId + ":" + where, text, now);
            }
        }

        /// <summary>A world event the player has confirmed touches a companion's home: they will want to talk about it.</summary>
        public static void Worried(WorldData w)
        {
            var now = QuestClock.Now(w);
            foreach (var npc in CompanionLoyalty.Present(w))
                foreach (var e in w.WorldEvents)
                {
                    if (!e.IsOpen || e.Known == EventKnowledge.Unknown) continue;
                    var touches = e.SettlementId == npc.HomeSettlementId || (w.FindRoute(e.RouteId) is RouteSegmentData r && r.Joins(npc.HomeSettlementId));
                    if (!touches) continue;
                    var home = w.FindSettlement(npc.HomeSettlementId)?.Name ?? "home";
                    Remark(w, npc.NpcId, "worry:" + npc.NpcId + ":" + e.EventId, $"\"You've heard — {Atlas.WorldEvents.Telling(w, e)}. That's {home}. My people are there.\"", now);
                }
        }

        /// <summary>Two companions whose quarrel shows (rivalry at <see cref="QuarrelAt"/> or more), or null.</summary>
        public static NpcData QuarrelWith(WorldData w, NpcData npc)
        {
            if (npc == null) return null;
            NpcData best = null;
            foreach (var other in CompanionLoyalty.Present(w))
            {
                if (other.NpcId == npc.NpcId) continue;
                var r = w.FindNpcRelation(npc.NpcId, other.NpcId);
                if (r == null || r.Rivalry < QuarrelAt) continue;
                if (best == null || other.NpcId.CompareTo(best.NpcId) < 0) best = other;
            }

            return best;
        }

        /// <summary>The player talks two companions round (or fails to): rivalry eases and they remember it, or it hardens.</summary>
        public static string Reconcile(WorldData w, NpcData npc, bool success, QuestReporter report)
        {
            var other = QuarrelWith(w, npc);
            if (other == null) return null;
            var r = w.FindNpcRelation(npc.NpcId, other.NpcId);
            var now = QuestClock.Now(w);
            var key = "reconcile:" + npc.NpcId + ":" + other.NpcId + ":" + w.CurrentSimulationDate.ToDayNumber().ToString(CultureInfo.InvariantCulture);
            if (success)
            {
                Relations.Shift(r, "Rivalry", -20, NpcRelationCause.Kindness, w.CurrentSimulationDate);
                Relations.Shift(r, "Affinity", 8, NpcRelationCause.Kindness, w.CurrentSimulationDate);
                CompanionLoyalty.Apply(w, npc.NpcId, key, 3, "you helped settle things with " + other.Name, report);
                CompanionLoyalty.Apply(w, other.NpcId, key, 3, "you helped settle things with " + npc.Name, report);
                Remark(w, other.NpcId, key, $"\"{npc.Name} and I talked it out. Thank you for that.\"", now);
                return $"{npc.Name} and {other.Name} put the matter behind them.";
            }

            Relations.Shift(r, "Rivalry", 5, NpcRelationCause.Quarrel, w.CurrentSimulationDate);
            return $"{npc.Name} isn't ready to let it go.";
        }

        public const int SecondStoryAt = 70;

        /// <summary>
        /// A companion who trusts the player deeply (<see cref="SecondStoryAt"/>), whose first personal quest is behind
        /// them, and who has a rival living elsewhere, asks for help putting it right — once. Bound to the real rival.
        /// </summary>
        public static QuestInstanceData OfferSecondStory(WorldData w, NpcData npc, CompanionRecordData record, PartyMemberData m, QuestReporter report)
        {
            if (record.StoryQuestId != null || record.Loyalty < SecondStoryAt || m.Temporary || m.Role != PartyRole.Active) return null;
            if (record.PersonalQuestId != null && w.FindQuest(record.PersonalQuestId) is QuestInstanceData first && !first.IsClosed) return null;
            var def = QuestCatalog.Find(QuestCatalog.CompanionUnfinished);
            if (def == null) return null;
            NpcData rival = null;
            foreach (var r in w.NpcRelations)
            {
                if (r.NpcA != npc.NpcId && r.NpcB != npc.NpcId) continue;
                if (r.Kind != NpcRelationKind.Rival && r.Kind != NpcRelationKind.Friend && r.Kind != NpcRelationKind.Household && r.Rivalry < 30) continue;
                var other = w.FindNpc(r.NpcA == npc.NpcId ? r.NpcB : r.NpcA);
                if (other == null || other.IsDeceased || other.HomeSettlementId == npc.HomeSettlementId || w.Party.Contains(other.NpcId)) continue;
                if (rival == null || other.NpcId.CompareTo(rival.NpcId) < 0) rival = other;
            }

            if (rival == null) return null;
            var given = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["giver"] = npc.NpcId.ToString(), ["rival"] = rival.NpcId.ToString(), ["rival_home"] = rival.HomeSettlementId.ToString(),
            };
            var q = QuestLifecycle.Create(w, def, given, "companion-debt:" + npc.NpcId, null, report, out _);
            if (q != null) record.StoryQuestId = q.InstanceId;
            return q;
        }
    }
}
