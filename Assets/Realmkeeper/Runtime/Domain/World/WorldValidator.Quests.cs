using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 12 invariants (ADR-0056 … ADR-0059): each quest instance is unique, of a known definition and a valid state,
    /// with its giver bound and its progress matching its definition; a completed quest has claimed its rewards exactly
    /// once; the conversation names a living person and a real node; standings are in range and unique.
    /// </summary>
    public static partial class WorldValidator
    {
        private static void ValidateQuests(WorldData world, ValidationResult result)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in world.Quests)
            {
                var label = $"Quest '{q?.InstanceId}'";
                if (q == null || string.IsNullOrEmpty(q.InstanceId) || !ids.Add(q.InstanceId)) { result.AddError($"{label} is missing or duplicated."); continue; }
                var def = QuestCatalog.Find(q.DefinitionId);
                if (def == null) { result.AddError($"{label} names unknown quest '{q.DefinitionId}'."); continue; }
                if (!Enum.IsDefined(typeof(QuestState), q.State)) result.AddError($"{label} has an unknown state.");
                if (q.Bound("giver") == null) result.AddError($"{label} has no giver.");
                foreach (var p in q.Objectives)
                {
                    ObjectiveDefinition o = null;
                    foreach (var x in def.Objectives) if (x.Id == p.Id) o = x;
                    if (o == null) { result.AddError($"{label} tracks unknown objective '{p.Id}'."); continue; }
                    if (p.Count < 0 || p.Count > o.Quantity) result.AddError($"{label} objective '{p.Id}' counts {p.Count} of {o.Quantity}.");
                    if (p.Done && p.Failed) result.AddError($"{label} objective '{p.Id}' is both done and failed.");
                }

                if (q.State == QuestState.Completed && !q.RewardsClaimed) result.AddError($"{label} is completed but its rewards were never given.");
                if (q.RewardsClaimed && q.State != QuestState.Completed) result.AddError($"{label} gave rewards without being completed.");
                if (q.IsClosed && q.ClosedAt < 0) result.AddError($"{label} is closed with no closing time.");
                if (q.IsOpen && q.AcceptedAt < 0) result.AddError($"{label} is in progress but was never accepted.");
                if (q.Tracked && !q.IsOpen) result.AddError($"{label} is tracked but not in progress.");
            }

            var c = world.Conversation;
            if (c != null)
            {
                var npc = world.FindNpc(c.NpcId);
                if (npc == null || npc.IsDeceased) result.AddError("The conversation is with nobody living.");
                var dlg = DialogueCatalog.Find(c.DialogueId);
                if (dlg == null || dlg.Find(c.NodeId) == null) result.AddError($"The conversation is at an unknown place ({c.DialogueId}.{c.NodeId}).");
                if (c.QuestInstanceId != null && world.FindQuest(c.QuestInstanceId) == null) result.AddError("The conversation concerns an unknown quest.");
            }

            var s = world.Player?.Social;
            if (s == null) return;
            if (s.Notoriety < 0 || s.Notoriety > 100) result.AddError($"Notoriety {s.Notoriety} is outside 0–100.");
            var factions = new HashSet<FactionId>();
            foreach (var f in s.FactionStandings)
            {
                if (!factions.Add(f.FactionId)) result.AddError($"Standing with faction {f.FactionId} is listed twice.");
                if (world.FindFaction(f.FactionId) == null) result.AddError($"Standing with unknown faction {f.FactionId}.");
                if (f.Standing < -100 || f.Standing > 100) result.AddError($"Standing {f.Standing} with {f.FactionId} is outside −100..100.");
            }

            var places = new HashSet<SettlementId>();
            foreach (var r in s.SettlementStandings)
            {
                if (!places.Add(r.SettlementId)) result.AddError($"Reputation in settlement {r.SettlementId} is listed twice.");
                if (world.FindSettlement(r.SettlementId) == null) result.AddError($"Reputation in unknown settlement {r.SettlementId}.");
                if (r.Reputation < -100 || r.Reputation > 100) result.AddError($"Reputation {r.Reputation} is outside −100..100.");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in s.Ledger) if (e.Key != null && !keys.Add(e.Key)) result.AddError($"Reputation change '{e.Key}' was applied twice.");
            if (s.Ledger.Count > PlayerSocialData.MaxLedger) result.AddError("The reputation ledger is over its bound.");
        }
    }
}
