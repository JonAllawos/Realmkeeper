using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Simulation.Party
{
    /// <summary>
    /// Phase 18 (ADR-0095): companions react to more of what happens — how a quest ended, what was discovered, the hardship
    /// of a town they pass through — and to each other. Every reaction comes from the companion's own personality, is
    /// given once (by key), and the chattier kinds wait out a cooldown so a companion does not comment on everything. Two
    /// more personal stories may be offered when the first kind (a rival elsewhere) does not fit the companion's life.
    /// </summary>
    public static partial class CompanionStories
    {
        /// <summary>Minutes a companion keeps quiet after a passing remark (discoveries, banter, hardship) before making another.</summary>
        public const int RemarkCooldown = 6 * 60;

        /// <summary>A trait leaning on a quest's outcome: how strongly a companion of that nature approves (sign) of it.</summary>
        private static readonly Dictionary<string, (NpcTrait Trait, int Sign, string Approve, string Disapprove)[]> OutcomeLeanings =
            new Dictionary<string, (NpcTrait, int, string, string)[]>(StringComparer.Ordinal)
            {
                ["reeves_accounts:exposed"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Discipline, 1, "\"A thief's a thief, whatever their title. Good.\"", "\"You shamed them before the whole town. Was that needed?\"") },
                ["reeves_accounts:restored"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Compassion, 1, "\"They'll make it right, and nobody had to be ruined. Well done.\"", "\"So they pay back what they stole and keep their post. Lucky them.\"") },
                ["reeves_accounts:bought"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Greed, 1, "\"Sixty coin's sixty coin.\"", "\"You let a thief buy you. I won't forget that.\""), (NpcTrait.Discipline, -1, null, "\"That ledger was the town's. You sold it back to the man who robbed them.\"") },
                ["cairn_keeping:shared"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Compassion, 1, "\"By turns, as it was built. That was the right answer.\"", null), (NpcTrait.Trustfulness, 1, "\"Both sides keeping it — I'd like to believe it'll hold.\"", "\"They'll be at each other's throats inside a year.\"") },
                ["cairn_keeping:claimed"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Ambition, 1, "\"A tower's a tower. Better to have it.\"", "\"You've set two towns against each other over a pile of stones.\"") },
                ["cairn_keeping:upheld"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Discipline, 1, "\"They kept the watch when the others didn't. Fair's fair.\"", null) },
                ["drift_future:sealed"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Compassion, 1, "\"Let them rest. Some things are worth more than silver.\"", "\"All that silver, under a stone. Seems a waste.\"") },
                ["drift_future:reopened"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Ambition, 1, "\"The town will be the richer for it.\"", null), (NpcTrait.Greed, 1, "\"And a share for us. I like it.\"", null) },
                ["story_conflict:mediated"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Compassion, 1, "\"You talked them down. Nobody bleeds today.\"", null), (NpcTrait.Courage, -1, "\"Better talking than fighting. Much better.\"", null) },
                ["story_conflict:supported"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Courage, 1, "\"You picked a side and stood on it. I respect that.\"", "\"Now we've enemies across the crossing.\"") },
                ["story_conflict:neutral"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Courage, -1, "\"Not our quarrel. Good.\"", "\"You walked away while they fought. Someone will pay for that.\"") },
                ["story_unrest:heard"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Compassion, 1, "\"You listened to them. That's all most people want.\"", null) },
                ["story_unrest:crushed"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Discipline, 1, "\"Order first. Grievances after.\"", null), (NpcTrait.Compassion, -1, null, "\"They were hungry, and you sent the guard.\"") },
                ["story_refugees:welcomed"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Compassion, 1, "\"Nobody should be turned away with winter coming.\"", null) },
                ["story_refugees:sent_on"] = new (NpcTrait, int, string, string)[] { (NpcTrait.Compassion, -1, null, "\"You sent families back onto the road. In this weather.\""), (NpcTrait.Discipline, 1, "\"Hard, but the town can't feed everyone.\"", null) },
            };

        /// <summary>A quest has ended with an outcome: each companion present reacts by their nature — loyalty once, a remark once.</summary>
        public static void QuestOutcome(WorldData w, QuestInstanceData q, QuestReporter report)
        {
            if (q?.Outcome == null || !OutcomeLeanings.TryGetValue(q.DefinitionId + ":" + q.Outcome, out var leanings)) return;
            var now = QuestClock.Now(w);
            foreach (var npc in CompanionLoyalty.Present(w))
            {
                var score = 0;
                string best = null;
                var bestStrength = 0;
                foreach (var (trait, sign, approve, disapprove) in leanings)
                {
                    var s = sign * (npc.Personality[trait] - 50) / 10;
                    score += s;
                    var said = s > 0 ? approve : disapprove;
                    if (said != null && Math.Abs(s) > bestStrength) { best = said; bestStrength = Math.Abs(s); }
                }

                score = Math.Max(-MaxSwing, Math.Min(MaxSwing, score));
                if (Math.Abs(score) < Indifferent) continue;
                var key = "outcome:" + q.InstanceId;
                CompanionLoyalty.Apply(w, npc.NpcId, key, score, (score > 0 ? "approved of how " : "disliked how ") + QuestCatalog.Find(q.DefinitionId)?.Name + " ended", report);
                if (best != null) Remark(w, npc.NpcId, key + ":" + npc.NpcId, best, now);
            }
        }

        /// <summary>What kind of discovery catches this companion's interest (by their nature), if any.</summary>
        public static bool Interested(NpcData npc, DiscoveryCategory c)
        {
            switch (c)
            {
                case DiscoveryCategory.Historical:
                case DiscoveryCategory.Cultural:
                case DiscoveryCategory.Arcane:
                    return npc.Personality[NpcTrait.Curiosity] >= 60;
                case DiscoveryCategory.Economic:
                    return npc.Personality[NpcTrait.Greed] >= 60 || npc.Personality[NpcTrait.Ambition] >= 65;
                case DiscoveryCategory.Faction:
                    return npc.Personality[NpcTrait.Ambition] >= 60;
                case DiscoveryCategory.Personal:
                    return npc.Personality[NpcTrait.Compassion] >= 60;
                case DiscoveryCategory.Dangerous:
                    return npc.Personality[NpcTrait.Courage] <= 35 || npc.Personality[NpcTrait.Courage] >= 70;
                case DiscoveryCategory.Religious:
                case DiscoveryCategory.Natural:
                case DiscoveryCategory.Geographic:
                    return npc.Personality[NpcTrait.Curiosity] >= 70;
                default:
                    return false;
            }
        }

        /// <summary>A major discovery: one companion present who cares about that kind of thing says so (once; not if they spoke lately).</summary>
        public static bool Discovered(WorldData w, DiscoveryData d)
        {
            if (d == null || !d.Major || d.Category == DiscoveryCategory.None) return false;
            var now = QuestClock.Now(w);
            foreach (var npc in CompanionLoyalty.Present(w))
            {
                if (!Interested(npc, d.Category) || Quiet(w, npc.NpcId, now)) continue;
                var text = d.Category == DiscoveryCategory.Dangerous
                    ? npc.Personality[NpcTrait.Courage] <= 35 ? "\"I don't like this. We should be careful — more careful than we have been.\"" : "\"Good to know what we're walking into. Let them come.\""
                    : d.Category == DiscoveryCategory.Economic ? "\"That's worth money to someone. Remember who'd pay for it.\""
                    : d.Category == DiscoveryCategory.Faction ? "\"Now that's worth knowing. The right people would want to hear it.\""
                    : d.Category == DiscoveryCategory.Personal ? "\"Somebody's whole life, in a few lines. Somebody should be told.\""
                    : "\"Look at that. How long has it been waiting for someone to read it?\"";
                if (Remark(w, npc.NpcId, "found:" + d.Key, text, now)) return true; // one voice is enough
            }

            return false;
        }

        /// <summary>Arriving in a town in hardship: a companion who would care says so — once a week per town.</summary>
        public static void Hardship(WorldData w, SettlementId where)
        {
            var place = w.FindSettlement(where);
            if (place == null) return;
            var unrest = w.WorldEvents.Exists(e => e.IsOpen && e.Type == WorldEventType.LocalUnrest && e.SettlementId == where);
            if (!place.IsInFoodShortage && !unrest) return;
            var now = QuestClock.Now(w);
            var week = (w.CurrentSimulationDate.ToDayNumber() / 7).ToString(CultureInfo.InvariantCulture);
            foreach (var npc in CompanionLoyalty.Present(w))
            {
                if (Quiet(w, npc.NpcId, now)) continue;
                string text = null;
                if (place.IsInFoodShortage && npc.Personality[NpcTrait.Compassion] >= 60) text = $"\"Look at the faces here. {place.Name} is going hungry.\"";
                else if (unrest && npc.Personality[NpcTrait.Courage] <= 40) text = $"\"There's an edge to {place.Name} today. Let's not linger.\"";
                else if (unrest && npc.Personality[NpcTrait.Discipline] >= 65) text = $"\"Somebody needs to take {place.Name} in hand before it boils over.\"";
                if (text != null && Remark(w, npc.NpcId, "hardship:" + where + ":" + week, text, now)) return;
            }
        }

        /// <summary>Two companions who like each other share a word now and then (once a week a pair; within the cooldown).</summary>
        public static void Banter(WorldData w)
        {
            var present = CompanionLoyalty.Present(w);
            if (present.Count < 2) return;
            var now = QuestClock.Now(w);
            var week = (w.CurrentSimulationDate.ToDayNumber() / 7).ToString(CultureInfo.InvariantCulture);
            for (var i = 0; i < present.Count; i++)
                for (var j = i + 1; j < present.Count; j++)
                {
                    var a = present[i];
                    var b = present[j];
                    var r = w.FindNpcRelation(a.NpcId, b.NpcId);
                    if (r == null || r.Affinity < 30 || r.Rivalry >= QuarrelAt || Quiet(w, a.NpcId, now)) continue;
                    var text = a.Personality[NpcTrait.Sociability] >= 60 ? $"\"{b.Name} snores like a mill-wheel, did you know? Don't tell {Them(b)} I said so.\""
                        : a.Personality[NpcTrait.Compassion] >= 60 ? $"\"{b.Name} gave me the last of the bread last night. Didn't say a word about it.\""
                        : $"\"{b.Name} and I have been talking. We'd both follow you a long way, for what it's worth.\"";
                    Remark(w, a.NpcId, "banter:" + a.NpcId + ":" + b.NpcId + ":" + week, text, now);
                }
        }

        /// <summary>True when the companion made a passing remark within the cooldown.</summary>
        public static bool Quiet(WorldData w, NpcId who, long now)
        {
            var rec = w.FindCompanion(who);
            if (rec == null) return true;
            foreach (var r in rec.Remarks)
                if (now - r.At < RemarkCooldown && (r.Key.StartsWith("found:", StringComparison.Ordinal) || r.Key.StartsWith("banter:", StringComparison.Ordinal) || r.Key.StartsWith("hardship:", StringComparison.Ordinal))) return true;
            return false;
        }

        /// <summary>
        /// The other personal stories, when no rival waits elsewhere: family living in another town (Going Home), or a friend
        /// who died lately (Words at the Grave). Bound to the real person; once.
        /// </summary>
        private static QuestInstanceData OfferOtherStory(WorldData w, NpcData npc, CompanionRecordData record, QuestReporter report)
        {
            NpcData kin = null;
            NpcData lost = null;
            var today = w.CurrentSimulationDate.ToDayNumber();
            foreach (var r in w.NpcRelations)
            {
                if (r.NpcA != npc.NpcId && r.NpcB != npc.NpcId) continue;
                var other = w.FindNpc(r.NpcA == npc.NpcId ? r.NpcB : r.NpcA);
                if (other == null || w.Party.Contains(other.NpcId)) continue;
                if (other.IsDeceased)
                {
                    if ((r.Kind == NpcRelationKind.Friend || r.Kind == NpcRelationKind.Household) && other.DiedOn.IsValidDate && today - other.DiedOn.ToDayNumber() <= 60 &&
                        (lost == null || other.NpcId.CompareTo(lost.NpcId) < 0)) lost = other;
                    continue;
                }

                if (r.Kind == NpcRelationKind.Household && other.HomeSettlementId != npc.HomeSettlementId && !other.HomeSettlementId.IsEmpty && (kin == null || other.NpcId.CompareTo(kin.NpcId) < 0)) kin = other;
            }

            QuestInstanceData q = null;
            if (kin != null)
                q = QuestLifecycle.Create(w, QuestCatalog.Find(QuestCatalog.CompanionGoingHome),
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["giver"] = npc.NpcId.ToString(), ["kin"] = kin.NpcId.ToString(), ["kin_home"] = kin.HomeSettlementId.ToString() },
                    "companion-home:" + npc.NpcId, null, report, out _);
            else if (lost != null && !lost.HomeSettlementId.IsEmpty)
                q = QuestLifecycle.Create(w, QuestCatalog.Find(QuestCatalog.CompanionGrave),
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["giver"] = npc.NpcId.ToString(), ["grave_town"] = lost.HomeSettlementId.ToString() },
                    "companion-grave:" + npc.NpcId, new Dictionary<string, string> { ["lost_name"] = lost.Name }, report, out _);
            if (q != null) record.StoryQuestId = q.InstanceId;
            return q;
        }
    }
}
