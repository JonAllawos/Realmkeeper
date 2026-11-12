using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Simulation.Party
{
    /// <summary>
    /// Phase 15 (ADR-0074): how companions' loyalty moves. Only real events move it: a deed the companion saw, their
    /// own quest settled, being left behind or saved in a fight. Each cause counts once (a stable key), is recorded with
    /// its reason, and large changes are announced. Below <see cref="PartyRules.LeaveBelow"/> a companion leaves.
    /// </summary>
    public static class CompanionLoyalty
    {
        /// <summary>Applies one cause to one companion; false when they have no record or the cause already counted.</summary>
        public static bool Apply(WorldData w, NpcId id, string key, int delta, string reason, QuestReporter report)
        {
            var record = w.FindCompanion(id);
            var npc = w.FindNpc(id);
            if (record == null || npc == null || npc.IsDeceased || string.IsNullOrEmpty(key) || delta == 0 || record.Counted(key)) return false;
            var now = QuestClock.Now(w);
            record.Approvals.Add(new ApprovalData { Key = key, Delta = delta, Reason = reason, At = now });
            if (record.Approvals.Count > CompanionRecordData.MaxApprovals) record.Approvals.RemoveAt(0);
            var before = record.Loyalty;
            record.Loyalty += delta;

            // Liking follows loyalty at half the rate (the same ledger key keeps it once).
            if (delta / 2 != 0) Reputation.Change(w, "npc:" + id, "Affinity", delta / 2, "companion:" + key, reason, report);
            if (Math.Abs(delta) >= PartyRules.AnnounceAt && report != null)
                report(HistoricalEventType.CompanionLoyalty, delta > 0 ? EventSeverity.Positive : EventSeverity.Warning, EventImportance.Minor, Home(npc),
                    Facts(w, npc, ("why", reason + (delta > 0 ? " (approves)" : " (disapproves)")), ("delta", delta.ToString(CultureInfo.InvariantCulture)),
                        ("loyalty", record.Loyalty.ToString(CultureInfo.InvariantCulture)), ("before", before.ToString(CultureInfo.InvariantCulture))), new[] { id });

            if (record.Standing == CompanionStanding.InParty && record.Loyalty < PartyRules.LeaveBelow && w.ActiveEncounter == null)
                Leave(w, npc, CompanionStanding.Departed, "lost faith in you (" + reason + ")", report);
            return true;
        }

        /// <summary>Takes a member out of the party (dismissed, departed or their term ended) and records why.</summary>
        public static void Leave(WorldData w, NpcData npc, CompanionStanding standing, string why, QuestReporter report)
        {
            w.Party.Members.RemoveAll(m => m.NpcId == npc.NpcId);
            if (w.Party.Leader == npc.NpcId) w.Party.Leader = default;
            Renumber(w.Party);
            var record = w.FindCompanion(npc.NpcId);
            if (record != null)
            {
                record.Standing = standing;
                record.LeftAt = QuestClock.Now(w);
                record.LeftReason = why;
            }

            report?.Invoke(HistoricalEventType.CompanionLeft, standing == CompanionStanding.Departed ? EventSeverity.Warning : EventSeverity.Info, EventImportance.Minor,
                Home(npc), Facts(w, npc, ("why", why), ("standing", standing.ToString())), new[] { npc.NpcId });
        }

        /// <summary>Renumbers the formation so orders run 0, 1, 2 … without gaps.</summary>
        public static void Renumber(PartyData party)
        {
            var ordered = new List<PartyMemberData>(party.Members);
            ordered.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.JoinedAt.CompareTo(b.JoinedAt));
            for (var i = 0; i < ordered.Count; i++) ordered[i].Order = i;
        }

        /// <summary>A waiting member is where the player is now (same settlement outdoors, or the same place and room).</summary>
        public static bool SameSpot(WorldData w, PartyMemberData m)
        {
            var p = w.Player;
            if (p == null) return false;
            if (!string.IsNullOrEmpty(m.WaitPlaceId)) return p.PlaceId == m.WaitPlaceId && p.PlaceRoom == m.WaitRoom;
            return string.IsNullOrEmpty(p.PlaceId) && p.SettlementId == m.WaitSettlement;
        }

        /// <summary>Active members with the player right now: following, or waiting where the player stands.</summary>
        public static List<NpcData> Present(WorldData w)
        {
            var list = new List<NpcData>();
            foreach (var m in w.Party.Members)
            {
                if (m.Role != PartyRole.Active) continue;
                if (m.Mode == FollowMode.Wait && !SameSpot(w, m)) continue;
                var npc = w.FindNpc(m.NpcId);
                if (npc != null && !npc.IsDeceased) list.Add(npc);
            }

            list.Sort((a, b) => w.Party.Find(a.NpcId).Order.CompareTo(w.Party.Find(b.NpcId).Order));
            return list;
        }

        /// <summary>
        /// How a companion takes a crime they saw the player commit: the deed's weight, made heavier by discipline (and,
        /// for violence, compassion), lighter by greed for thefts. A greedy, undisciplined companion may even approve of a theft.
        /// </summary>
        public static int CrimeReaction(NpcData npc, CrimeKind kind, out string why)
        {
            int baseline;
            switch (kind)
            {
                case CrimeKind.Murder: baseline = -20; break;
                case CrimeKind.Assault: baseline = -8; break;
                case CrimeKind.Theft:
                case CrimeKind.ShopTheft: baseline = -6; break;
                case CrimeKind.Trespass: baseline = -2; break;
                default: baseline = -3; break;
            }

            var discipline = npc.Personality[NpcTrait.Discipline];
            var compassion = npc.Personality[NpcTrait.Compassion];
            var greed = npc.Personality[NpcTrait.Greed];
            var theft = kind == CrimeKind.Theft || kind == CrimeKind.ShopTheft;
            var violence = kind == CrimeKind.Murder || kind == CrimeKind.Assault;
            if (theft && greed >= 75 && discipline < 40) { why = "likes a quick profit"; return 3; }
            var v = (double)baseline;
            var reasons = new List<string>();
            if (discipline >= 60) { v *= 1.5; reasons.Add("respects the law"); }
            if (violence && compassion >= 60) { v *= 1.5; reasons.Add("hates cruelty"); }
            if (theft && greed >= 60) { v *= 0.5; reasons.Add("understands wanting more"); }
            if (discipline < 30 && !violence) { v *= 0.5; reasons.Add("cares little for rules"); }
            why = reasons.Count > 0 ? string.Join(", ", reasons) : "thinks it wrong";
            return (int)Math.Round(v, MidpointRounding.AwayFromZero);
        }

        /// <summary>Each companion present sees the player's crime and weighs it, once per crime.</summary>
        public static void OnCrime(WorldData w, CrimeRecordData c, string deed, QuestReporter report)
        {
            foreach (var npc in Present(w))
            {
                if (c.VictimNpcId == npc.NpcId) continue;
                var delta = CrimeReaction(npc, c.Kind, out var why);
                Apply(w, npc.NpcId, c.CrimeId, delta, "saw you " + deed + " (" + why + ")", report);
            }
        }

        /// <summary>A personal quest settled: done earns real loyalty; failed or abandoned costs it.</summary>
        public static void OnQuestClosed(WorldData w, QuestInstanceData q, QuestState to, QuestReporter report)
        {
            foreach (var r in w.Companions)
            {
                if (r.PersonalQuestId != q.InstanceId) continue;
                var name = QuestCatalog.Find(q.DefinitionId)?.Name ?? "their request";
                if (to == QuestState.Completed) Apply(w, r.NpcId, q.InstanceId + ":done", 25, "you saw " + name + " through", report);
                else if (to == QuestState.Failed) Apply(w, r.NpcId, q.InstanceId + ":failed", -10, name + " failed", report);
                else if (to == QuestState.Abandoned) Apply(w, r.NpcId, q.InstanceId + ":abandoned", -15, "you gave up on " + name, report);
            }
        }

        private static IReadOnlyList<SettlementId> Home(NpcData npc) => npc.HomeSettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { npc.HomeSettlementId };

        private static Dictionary<string, string> Facts(WorldData w, NpcData npc, params (string, string)[] more)
        {
            var d = new Dictionary<string, string> { ["npc"] = npc.NpcId.ToString(), ["name"] = npc.Name, ["player"] = w.Player?.Name ?? "the player" };
            foreach (var (k, v) in more) d[k] = v;
            return d;
        }
    }

    /// <summary>
    /// Phase 15: the party's daily rule. Temporary arrangements end when their time is up; a companion who died or whose
    /// loyalty fell too low during a fight leaves now. Reserve members live their ordinary lives (nothing to do).
    /// </summary>
    public sealed class PartySystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.party";

        public string SystemId => Id;
        public int ExecutionOrder => 485;

        public void Execute(SimulationDayContext context)
        {
            var w = context.World;
            if (w.Party == null || w.Party.Members.Count == 0) return;
            QuestReporter report = (type, severity, importance, settlements, facts, npcs) =>
            {
                var data = new EventData();
                foreach (var pair in facts) data.Set(pair.Key, pair.Value);
                context.ReportEvent(type, severity, importance, settlements, data, npcs);
            };
            // The day is simulated at its midnight: terms and offers are judged then (the clock's minute is not yet set).
            var now = (long)context.Date.ToDayNumber() * 1440L;
            foreach (var m in new List<PartyMemberData>(w.Party.Members))
            {
                var npc = w.FindNpc(m.NpcId);
                if (npc == null) { w.Party.Members.Remove(m); continue; }
                if (npc.IsDeceased) { CompanionLoyalty.Leave(w, npc, CompanionStanding.Departed, "died", report); continue; }
                var record = w.FindCompanion(m.NpcId);
                if (record != null && record.Loyalty < PartyRules.LeaveBelow) { CompanionLoyalty.Leave(w, npc, CompanionStanding.Departed, "lost faith in you", report); continue; }
                if (m.Temporary && now >= m.Until) { CompanionLoyalty.Leave(w, npc, CompanionStanding.TermEnded, "the agreed days are over", report); continue; }
                if (record != null) OfferPersonalQuest(w, npc, record, m, now, report);
            }
        }

        /// <summary>Loyalty at which a companion asks their own quest of the player (once in their life).</summary>
        public const int PersonalQuestAt = 60;

        /// <summary>
        /// A companion who has travelled with the player for a day and trusts them (<see cref="PersonalQuestAt"/>) asks their
        /// personal quest, bound to them and to a place near their home (the lowest place ID there, for determinism). Once:
        /// the record remembers the quest; a person with no place near home has none to ask.
        /// </summary>
        public static Domain.Quests.QuestInstanceData OfferPersonalQuest(WorldData w, NpcData npc, CompanionRecordData record, PartyMemberData m, long now, QuestReporter report)
        {
            if (record.PersonalQuestId != null || record.Loyalty < PersonalQuestAt || m.Temporary || now - m.JoinedAt < 1440) return null;
            var def = Domain.Quests.QuestCatalog.Find(Domain.Quests.QuestCatalog.CompanionRoad);
            if (def == null || npc.HomeSettlementId.IsEmpty) return null;
            Domain.Places.PlaceData place = null;
            foreach (var p in w.Places)
                if (p.SettlementId == npc.HomeSettlementId && (place == null || string.CompareOrdinal(p.PlaceId, place.PlaceId) < 0)) place = p;
            if (place == null) return null;
            var given = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["giver"] = npc.NpcId.ToString(), ["place"] = place.PlaceId, ["settlement"] = npc.HomeSettlementId.ToString(),
            };
            var q = QuestLifecycle.Create(w, def, given, "companion:" + npc.NpcId, null, report, out _);
            if (q != null) record.PersonalQuestId = q.InstanceId;
            return q;
        }
    }
}
