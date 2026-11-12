using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Campaigns;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 17 invariants (ADR-0082): one instance per campaign; a version no newer than this build's definition; its
    /// people, places and factions real (a person may be dead — that is the story — but must exist); its quests real
    /// instances (or empty, for one that could never be offered); progress recorded only for chapters, arcs and
    /// decisions the definition has; each decision made once. A campaign whose definition this build does not know is
    /// kept as it was (it cannot advance) rather than rejected.
    /// </summary>
    public static partial class WorldValidator
    {
        private static void ValidateCampaigns(WorldData world, ValidationResult result)
        {
            if (world.Campaigns == null) { result.AddError("The campaign list is missing."); return; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in world.Campaigns)
            {
                if (c == null) { result.AddError("Null campaign."); continue; }
                var label = $"Campaign '{c.CampaignId}'";
                if (string.IsNullOrEmpty(c.InstanceId) || !ids.Add(c.InstanceId)) result.AddError($"{label} is missing an ID or appears twice.");
                foreach (var id in c.Quests.Values)
                    if (!string.IsNullOrEmpty(id) && world.FindQuest(id) == null) result.AddError($"{label} refers to quest '{id}', which does not exist.");
                foreach (var pair in c.Bindings)
                {
                    if (string.IsNullOrEmpty(pair.Value)) { result.AddError($"{label} binds '{pair.Key}' to nothing."); continue; }
                    if (!Exists(world, pair.Value)) result.AddError($"{label} binds '{pair.Key}' to '{pair.Value}', which is not in this world.");
                }

                var decided = new HashSet<string>(StringComparer.Ordinal);
                foreach (var d in c.Decisions) if (!decided.Add(d.DecisionId)) result.AddError($"{label} records decision '{d.DecisionId}' twice.");
                var pending = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in c.Pending)
                {
                    if (!pending.Add(p.Key)) result.AddError($"{label} schedules '{p.Key}' twice.");
                    if (!decided.Contains(p.DecisionId)) result.AddError($"{label} schedules a consequence of '{p.DecisionId}', which was never decided.");
                }

                if (c.IsClosed && c.ClosedAt < 0) result.AddError($"{label} is {c.State.ToString().ToLowerInvariant()} but has no end time.");
                var def = CampaignCatalog.Find(c.CampaignId);
                if (def == null) continue;
                if (c.Version > def.Version) result.AddError($"{label} was saved by a newer version ({c.Version}) than this build knows ({def.Version}).");
                foreach (var a in c.Arcs) if (def.Arc(a.Id) == null) result.AddError($"{label} records arc '{a.Id}', which its definition does not have.");
                foreach (var ch in c.Chapters) if (def.Chapter(ch.Id) == null) result.AddError($"{label} records chapter '{ch.Id}', which its definition does not have.");
                foreach (var d in c.Decisions)
                    if (def.Decision(d.DecisionId)?.Choice(d.ChoiceId) == null) result.AddError($"{label} records choice '{d.DecisionId}/{d.ChoiceId}', which its definition does not have.");
            }
        }

        private static bool Exists(WorldData w, string id)
        {
            if (w.FindRoute(id) != null) return true;
            if (!Guid.TryParse(id, out var g)) return false;
            return w.FindNpc(new Identifiers.NpcId(g)) != null || w.FindSettlement(new Identifiers.SettlementId(g)) != null || w.FindFaction(new Identifiers.FactionId(g)) != null
                   || w.FindRegion(new Identifiers.RegionId(g)) != null;
        }
    }
}
