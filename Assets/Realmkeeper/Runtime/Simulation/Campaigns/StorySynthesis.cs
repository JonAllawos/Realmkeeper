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

        /// <summary>Stories open at once (Phase 18: 5, was 3 — more kinds of cause, in up to nine regions).</summary>
        public const int MaxOpen = 5;

        /// <summary>
        /// Phase 18 (ADR-0093): which causes are told first when several wait — a caravan's loss first (its witnesses
        /// forget, and it is recent by nature), then a conflict, refugees on the road, trade cut off, and unrest.
        /// </summary>
        public static int Priority(WorldEventType t) =>
            t == WorldEventType.CaravanLost ? 0 : t == WorldEventType.FactionConflict ? 1 : t == WorldEventType.RefugeeMovement ? 2 : t == WorldEventType.TradeDisruption ? 3 : 4;

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
            events.Sort((a, b) =>
            {
                var p = Priority(a.Type).CompareTo(Priority(b.Type));
                return p != 0 ? p : Number(a.EventId).CompareTo(Number(b.EventId));
            });
            foreach (var e in events)
            {
                var made = For(w, e, report);
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

            return WordHome(w, report); // Phase 18: a personal story
        }

        /// <summary>
        /// Phase 18 (ADR-0090): two factions in dispute or worse at a crossing. A leader or guard of one side, in a town at
        /// the crossing, asks where the player stands. One story per conflict.
        /// </summary>
        private static QuestInstanceData Conflict(WorldData w, WorldEventData e, QuestReporter report)
        {
            if (e.Severity < 2) return null; // tension alone is not yet a story
            var key = Prefix + "conflict:" + e.EventId;
            if (Told(w, key)) return null;
            var route = w.FindRoute(e.RouteId);
            if (route == null) return null;
            foreach (var end in new[] { route.A, route.B })
            {
                var s = w.FindSettlement(end);
                if (s == null || (s.FactionId != e.FactionA && s.FactionId != e.FactionB)) continue;
                var other = s.FactionId == e.FactionA ? e.FactionB : e.FactionA;
                var giver = FirstOf(w, s.SettlementId, n => n.FactionId == s.FactionId, "Elder", "Guard") ?? FirstOf(w, s.SettlementId, n => true, "Elder", "Guard");
                if (giver == null) continue;
                return Make(w, QuestCatalog.StoryConflict, key,
                    new Dictionary<string, string> { ["giver"] = giver.NpcId.ToString(), ["faction"] = s.FactionId.ToString(), ["other"] = other.ToString(), ["settlement"] = s.SettlementId.ToString() },
                    new Dictionary<string, string> { ["account"] = WorldEvents.Telling(w, e), ["event"] = e.Key }, report);
            }

            return null;
        }

        /// <summary>How long a closed cause (a caravan lost, a death) still makes a story.</summary>
        public const int RecentDays = 14;

        /// <summary>
        /// Phase 18: families arrive from a town that could not feed them. The elder of the town they reach must decide —
        /// and the player is asked. Bound to the group's real destination; the story ends the event.
        /// </summary>
        private static QuestInstanceData Refugees(WorldData w, WorldEventData e, QuestReporter report)
        {
            var key = Prefix + "refugees:" + e.EventId;
            if (Told(w, key)) return null;
            var group = w.FindGroup(e.GroupId);
            var to = group == null ? null : w.FindSettlement(group.DestinationId);
            if (to == null) return null;
            var giver = FirstOf(w, to.SettlementId, n => true, "Elder", "Guard", "Resident");
            if (giver == null) return null;
            var vars = new Dictionary<string, string> { ["account"] = WorldEvents.Telling(w, e), ["event"] = e.Key };
            return Make(w, QuestCatalog.StoryRefugees, key, new Dictionary<string, string> { ["giver"] = giver.NpcId.ToString(), ["settlement"] = to.SettlementId.ToString() }, vars, report);
        }

        /// <summary>Phase 18: a town is restless. A guard (or elder) asks for help, and the loudest voice in the lane is a real, unhappy person.</summary>
        private static QuestInstanceData Unrest(WorldData w, WorldEventData e, QuestReporter report)
        {
            var key = Prefix + "unrest:" + e.EventId;
            if (Told(w, key)) return null;
            var s = w.FindSettlement(e.SettlementId);
            if (s == null) return null;
            var giver = FirstOf(w, s.SettlementId, n => true, "Guard", "Elder");
            if (giver == null) return null;
            NpcData agitator = null;
            foreach (var n in w.Npcs)
            {
                if (n.IsDeceased || n.Tier == NpcTier.Unassigned || n.HomeSettlementId != s.SettlementId || w.Party.Contains(n.NpcId) || n.NpcId == giver.NpcId) continue;
                if (n.Role != NpcRole.Labourer && n.Role != NpcRole.Farmer && n.Role != NpcRole.Resident && n.Role != NpcRole.Smith) continue;
                if (agitator == null || n.Condition.Morale < agitator.Condition.Morale || (n.Condition.Morale == agitator.Condition.Morale && n.NpcId.CompareTo(agitator.NpcId) < 0)) agitator = n; // the unhappiest
            }

            if (agitator == null) return null;
            return Make(w, QuestCatalog.StoryUnrest, key,
                new Dictionary<string, string> { ["giver"] = giver.NpcId.ToString(), ["agitator"] = agitator.NpcId.ToString(), ["settlement"] = s.SettlementId.ToString() },
                new Dictionary<string, string> { ["event"] = e.Key }, report);
        }

        /// <summary>Phase 18: trade cut off on a road — a merchant at one end will pay to have goods carried to a merchant at the other.</summary>
        private static QuestInstanceData LongWay(WorldData w, WorldEventData e, QuestReporter report)
        {
            var key = Prefix + "longway:" + e.EventId;
            if (Told(w, key)) return null;
            var route = w.FindRoute(e.RouteId);
            if (route == null) return null;
            var a = w.FindSettlement(route.A);
            var b = w.FindSettlement(route.B);
            if (a == null || b == null) return null;
            var giver = FirstOf(w, a.SettlementId, n => true, "Merchant", "Smith", "Innkeeper");
            var buyer = FirstOf(w, b.SettlementId, n => true, "Merchant", "Innkeeper", "Smith");
            if (giver == null || buyer == null) return null;
            return Make(w, QuestCatalog.StoryLongWay, key,
                new Dictionary<string, string> { ["giver"] = giver.NpcId.ToString(), ["settlement"] = a.SettlementId.ToString(), ["buyer"] = buyer.NpcId.ToString(), ["market"] = b.SettlementId.ToString() },
                new Dictionary<string, string> { ["account"] = WorldEvents.Telling(w, e), ["event"] = e.Key }, report);
        }

        /// <summary>
        /// Phase 18: someone died lately, and a friend of theirs in another settlement has not been told. A neighbour asks
        /// the player to carry the word. The friendship is a real one (a friend the dead person liked); one story per death.
        /// </summary>
        private static QuestInstanceData WordHome(WorldData w, QuestReporter report)
        {
            var today = w.CurrentSimulationDate.ToDayNumber();
            var dead = new List<NpcData>();
            foreach (var n in w.Npcs)
                if (n.IsDeceased && n.Tier != NpcTier.Unassigned && n.DiedOn.IsValidDate && today - n.DiedOn.ToDayNumber() <= RecentDays) dead.Add(n);
            dead.Sort((x, y) => x.NpcId.CompareTo(y.NpcId));
            foreach (var d in dead)
            {
                var key = Prefix + "word:" + d.NpcId;
                if (Told(w, key)) continue;
                NpcData friend = null;
                foreach (var r in w.NpcRelations)
                {
                    if (r.Kind != NpcRelationKind.Friend || r.Affinity < 30 || (r.NpcA != d.NpcId && r.NpcB != d.NpcId)) continue;
                    var other = w.FindNpc(r.NpcA == d.NpcId ? r.NpcB : r.NpcA);
                    if (other == null || other.IsDeceased || other.HomeSettlementId == d.HomeSettlementId || other.HomeSettlementId.IsEmpty || w.Party.Contains(other.NpcId)) continue;
                    if (friend == null || other.NpcId.CompareTo(friend.NpcId) < 0) friend = other;
                }

                if (friend == null) continue;
                var giver = FirstOf(w, d.HomeSettlementId, n => true, "Resident", "Elder", "Innkeeper", "Labourer", "Farmer", "Merchant", "Smith", "Guard");
                if (giver == null) continue;
                var made = Make(w, QuestCatalog.StoryWordHome, key,
                    new Dictionary<string, string>
                    {
                        ["giver"] = giver.NpcId.ToString(), ["settlement"] = d.HomeSettlementId.ToString(), ["friend"] = friend.NpcId.ToString(), ["far"] = friend.HomeSettlementId.ToString(),
                    },
                    new Dictionary<string, string> { ["deceased_name"] = d.Name }, report);
                if (made != null) return made;
            }

            return null;
        }

        /// <summary>The story this event makes, if it makes one now (once per cause; used by the daily pass, and by tests and the DM tools for one event).</summary>
        public static QuestInstanceData For(WorldData w, WorldEventData e, QuestReporter report)
        {
            if (w?.Player == null || e == null) return null;
            // Phase 18 (ADR-0093, lifecycle audit): a caravan's loss is recorded and resolved in the same moment (its
            // survivors are already home), so a recent loss is a cause even though the event is closed.
            var now = QuestClock.Now(w);
            var recentLoss = e.Type == WorldEventType.CaravanLost && e.State == WorldEventState.Resolved && now - e.CreatedAt <= RecentDays * (long)Domain.Calendar.WorldTime.MinutesPerDay;
            if (!e.IsOpen && !recentLoss) return null;
            switch (e.Type)
            {
                case WorldEventType.CaravanLost: return LostCaravan(w, e, report);
                case WorldEventType.RefugeeMovement: return Refugees(w, e, report); // Phase 18
                case WorldEventType.LocalUnrest: return Unrest(w, e, report); // Phase 18
                case WorldEventType.TradeDisruption: return LongWay(w, e, report); // Phase 18
                case WorldEventType.FactionConflict: return Conflict(w, e, report); // Phase 18 (ADR-0090)
                default: return null;
            }
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
            Party.CompanionStories.Banter(w); // Phase 18
        }
    }
}
