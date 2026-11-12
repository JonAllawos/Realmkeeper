using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Simulation.Campaigns
{
    /// <summary>
    /// Phase 17 (ADR-0085): stories the world tells itself. Once a day the synthesis looks at what has actually happened —
    /// a caravan lost on a road, a settlement going hungry while another has grain to spare — and, where real people are bound up in it, offers it to the player as a quest: a cause, the people involved, a
    /// place, something to do, and a lasting result. Every fact comes from the event and the people's own knowledge; no
    /// fact is invented. One story per cause, ever; at most <see cref="MaxOpen"/> open at once; at most one new a day.
    /// </summary>
    public static class StorySynthesis
    {
        public const string Prefix = "story:";
        public const int MaxOpen = 3;

        /// <summary>Food days a settlement must hold before it is asked to share (and the share it is asked for).</summary>
        public const int SurplusDays = 30;
        public const int ShareFood = 60;

        public static QuestInstanceData Run(WorldData w, QuestReporter report)
        {
            if (w?.Player == null) return null;
            var open = 0;
            foreach (var q in w.Quests)
                if (q.SourceKey != null && q.SourceKey.StartsWith(Prefix, StringComparison.Ordinal) && (q.IsOpen || q.State == QuestState.Available || q.State == QuestState.Discovered || q.State == QuestState.Offered)) open++;
            if (open >= MaxOpen) return null;

            var events = new List<WorldEventData>(w.WorldEvents);
            events.Sort((a, b) => Number(a.EventId).CompareTo(Number(b.EventId)));
            foreach (var e in events)
            {
                if (!e.IsOpen) continue;
                QuestInstanceData made = null;
                switch (e.Type)
                {
                    case WorldEventType.CaravanLost: made = LostCaravan(w, e, report); break;
                }

                if (made != null) return made;
            }

            var hungry = new List<SettlementData>(w.Settlements);
            hungry.Sort((a, b) => a.SettlementId.CompareTo(b.SettlementId));
            foreach (var s in hungry)
            {
                if (!s.IsInFoodShortage) continue;
                var made = GrainRoad(w, s, report);
                if (made != null) return made;
            }

            return null;
        }

        private static int Number(string id)
        {
            var colon = id?.LastIndexOf(':') ?? -1;
            return colon >= 0 && int.TryParse(id.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : int.MaxValue;
        }

        /// <summary>Whether a story for this cause was ever made (open, closed, or pruned: the player's ledger remembers).</summary>
        public static bool Told(WorldData w, string key)
        {
            if (w.Player.Social.Applied("story-made:" + key)) return true;
            foreach (var q in w.Quests) if (q.SourceKey == key) return true;
            return false;
        }

        private static QuestInstanceData Make(WorldData w, string defId, string key, Dictionary<string, string> given, Dictionary<string, string> vars, QuestReporter report)
        {
            var def = QuestCatalog.Find(defId);
            if (def == null) return null;
            var q = QuestLifecycle.Create(w, def, given, key, vars, report, out _);
            if (q == null) return null;
            var social = w.Player.Social;
            social.AppliedKeys.Add("story-made:" + key);
            while (social.AppliedKeys.Count > PlayerSocialData.MaxAppliedKeys) social.AppliedKeys.RemoveAt(0);

            // The giver carries it as news: it reaches others (and the player) through the people's own talk.
            if (QuestEffects.Person(w, q, "giver") is NpcData giver)
                NpcMind.Learn(giver, KnowledgeTopic.RecentEvent, key, QuestText.Fill(w, q, def.Description), KnowledgeStatus.Known, 75, w.CurrentSimulationDate, default, 0, default,
                    () => new KnowledgeId(NpcHash.Guid(NpcHash.Of(w.Seed, "story-knowledge", key + "|" + giver.NpcId))));
            report?.Invoke(HistoricalEventType.StoryOpportunity, EventSeverity.Info, EventImportance.Minor,
                QuestEffects.SettlementOf(w, q, "settlement") is SettlementData s ? new[] { s.SettlementId } : Array.Empty<SettlementId>(),
                new Dictionary<string, string> { ["source"] = "story", ["what"] = QuestText.Fill(w, q, def.Description), ["instance"] = q.InstanceId }, Array.Empty<NpcId>());
            return q;
        }

        /// <summary>
        /// A caravan was lost: someone in the town it set out from wants to know what happened to it, and someone at the
        /// road's end heard it from those who were there (they hold the event as knowledge). The account they give is the event's own.
        /// </summary>
        private static QuestInstanceData LostCaravan(WorldData w, WorldEventData e, QuestReporter report)
        {
            var key = Prefix + "caravan:" + e.EventId;
            if (Told(w, key)) return null;
            var route = w.FindRoute(e.RouteId);
            var group = w.FindGroup(e.GroupId);
            var from = group != null ? w.FindSettlement(group.OriginId) : route != null ? w.FindSettlement(route.A) : null;
            if (from == null) return null;
            var giver = FirstOf(w, from.SettlementId, n => true, "Merchant", "Elder", "Guard");
            if (giver == null) return null;
            NpcData witness = null;
            foreach (var n in w.Npcs)
            {
                if (n.IsDeceased || n.NpcId == giver.NpcId || n.HomeSettlementId.IsEmpty) continue;
                if (route != null && !route.Joins(n.HomeSettlementId)) continue;
                if (NpcMind.Find(n, KnowledgeTopic.RecentEvent, e.EventId) == null) continue;
                if (witness == null || (witness.HomeSettlementId == from.SettlementId && n.HomeSettlementId != from.SettlementId) || (witness.HomeSettlementId == n.HomeSettlementId && n.NpcId.CompareTo(witness.NpcId) < 0)) witness = n;
            }

            if (witness == null) return null; // nobody knows anything: no story to follow
            var leader = group == null ? null : w.FindNpc(group.LeaderNpcId);
            var vars = new Dictionary<string, string>
            {
                ["account"] = WorldEvents.Telling(w, e),
                ["leader_name"] = leader?.Name ?? "the carter",
                ["leader_fate"] = leader == null ? "Nobody can say who led it." : leader.IsDeceased ? leader.Name + " did not come back from it." : leader.Name + " came back alive, and won't speak of it.",
            };
            return Make(w, QuestCatalog.StoryLostCaravan, key,
                new Dictionary<string, string> { ["giver"] = giver.NpcId.ToString(), ["witness"] = witness.NpcId.ToString(), ["settlement"] = from.SettlementId.ToString() }, vars, report);
        }

        /// <summary>
        /// A settlement is short of food while another (reached by road, preferably in another region) has plenty: the
        /// hungry town's elder asks the player to persuade a merchant there to send grain. Done, the grain really moves.
        /// </summary>
        private static QuestInstanceData GrainRoad(WorldData w, SettlementData hungry, QuestReporter report)
        {
            var key = Prefix + "grain:" + hungry.SettlementId + ":" + (w.CurrentSimulationDate.ToDayNumber() / Domain.Calendar.WorldDate.DaysPerWeek).ToString(CultureInfo.InvariantCulture);
            foreach (var q in w.Quests)
                if (q.SourceKey != null && q.SourceKey.StartsWith(Prefix + "grain:" + hungry.SettlementId + ":", StringComparison.Ordinal) && !q.IsClosed && q.State != QuestState.Unavailable) return null;
            if (Told(w, key)) return null;
            var giver = FirstOf(w, hungry.SettlementId, n => true, "Elder", "Farmer", "Merchant");
            if (giver == null) return null;
            SettlementData supplier = null;
            var bestScore = int.MinValue;
            foreach (var r in w.Routes)
            {
                if (!r.Passable || !r.Joins(hungry.SettlementId)) continue;
                var other = w.FindSettlement(r.Other(hungry.SettlementId));
                if (other == null || other.IsInFoodShortage) continue;
                var eats = Math.Max(1, other.Population / 10);
                var days = other.ResourceInventory.Get(ResourceTypes.Food) / eats;
                if (days < SurplusDays) continue;
                var score = days + (other.RegionId != hungry.RegionId ? 1000 : 0);
                if (score > bestScore || (score == bestScore && other.SettlementId.CompareTo(supplier.SettlementId) < 0)) { supplier = other; bestScore = score; }
            }

            if (supplier == null) return null;
            var merchant = FirstOf(w, supplier.SettlementId, n => true, "Merchant", "Farmer", "Elder");
            if (merchant == null) return null;
            var vars = new Dictionary<string, string> { ["share"] = ShareFood.ToString(CultureInfo.InvariantCulture) };
            return Make(w, QuestCatalog.StoryGrainRoad, key,
                new Dictionary<string, string>
                {
                    ["giver"] = giver.NpcId.ToString(), ["settlement"] = hungry.SettlementId.ToString(), ["merchant"] = merchant.NpcId.ToString(), ["supplier"] = supplier.SettlementId.ToString(),
                }, vars, report);
        }

        private static NpcData FirstOf(WorldData w, SettlementId s, Func<NpcData, bool> ok, params string[] roles)
        {
            foreach (var role in roles)
            {
                NpcData best = null;
                foreach (var n in w.Npcs)
                    if (!n.IsDeceased && n.Tier != NpcTier.Unassigned && n.HomeSettlementId == s && n.Role.ToString() == role && !w.Party.Contains(n.NpcId) && ok(n) && (best == null || n.NpcId.CompareTo(best.NpcId) < 0)) best = n;
                if (best != null) return best;
            }

            return null;
        }
    }

    /// <summary>
    /// Phase 17: the day's campaign pass (order 482, after the quest pass and before the party's): delayed consequences
    /// that are due, campaign progress that depends on the world (time limits, people gone, world-state objectives), the
    /// stories the world offers, and what companions want to say about events at home. A world with no campaign
    /// still gets its stories and its companions' words: nothing here needs a campaign.
    /// </summary>
    public sealed class CampaignSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.campaigns";

        public string SystemId => Id;
        public int ExecutionOrder => 482;

        public void Execute(SimulationDayContext context)
        {
            var w = context.World;
            QuestReporter report = (type, severity, importance, settlements, facts, npcs) =>
            {
                var data = new EventData();
                foreach (var pair in facts) data.Set(pair.Key, pair.Value);
                context.ReportEvent(type, severity, importance, settlements, data, npcs);
            };
            CampaignEngine.EvaluateAll(w, report);
            StorySynthesis.Run(w, report);
            Party.CompanionStories.Worried(w);
        }
    }
}
