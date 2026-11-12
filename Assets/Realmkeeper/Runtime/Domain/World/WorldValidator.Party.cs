using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 15 invariants (ADR-0073): each person in the party once, alive and known, never also an escort; capacities
    /// kept; a companion's own items each held once (checked with every other holder's) and worn in a slot they fit;
    /// companion records unique, with loyalty in range and a standing that matches party membership.
    /// </summary>
    public static partial class WorldValidator
    {
        private static void ValidateParty(WorldData world, HashSet<NpcId> npcIds, HashSet<ItemStackId> stackIds, ValidationResult result)
        {
            var party = world.Party;
            if (party == null) { result.AddError("The party is missing."); return; }
            var members = new HashSet<NpcId>();
            var active = 0;
            var reserve = 0;
            foreach (var m in party.Members)
            {
                if (m == null) { result.AddError("Null party member."); continue; }
                var npc = world.FindNpc(m.NpcId);
                var label = $"Party member {npc?.Name ?? m.NpcId.ToString()}";
                if (m.NpcId.IsEmpty || !members.Add(m.NpcId)) result.AddError($"{label} is missing an id or is in the party twice.");
                if (!npcIds.Contains(m.NpcId)) result.AddError($"{label} is not a known person.");
                else if (npc.IsDeceased && world.ActiveEncounter == null) result.AddError($"{label} is dead.");
                if (world.FindEscort(m.NpcId) != null && world.FindEscort(m.NpcId).OverridesSchedule) result.AddError($"{label} is also being escorted.");
                if (m.Role == PartyRole.Active) active++; else reserve++;
                if (m.Order < 0) result.AddError($"{label} has a negative formation order.");
                if (m.FollowDistance < 0.5f || m.FollowDistance > 30f) result.AddError($"{label} keeps an unreasonable follow distance ({m.FollowDistance}).");
                if (m.Mode == FollowMode.Wait && m.WaitSettlement.IsEmpty && string.IsNullOrEmpty(m.WaitPlaceId)) result.AddError($"{label} waits nowhere.");
                if (m.Temporary && m.Until < m.JoinedAt) result.AddError($"{label}'s temporary term ends before it began.");
                if (world.FindCompanion(m.NpcId)?.Standing != CompanionStanding.InParty) result.AddError($"{label} has no companion record that says so.");
                if (npc?.Character?.Sheet == null) result.AddError($"{label} has no character sheet.");
            }

            if (active > PartyRules.ActiveCapacity) result.AddError($"The party has {active} active members (at most {PartyRules.ActiveCapacity}).");
            if (reserve > PartyRules.ReserveCapacity) result.AddError($"The party has {reserve} in reserve (at most {PartyRules.ReserveCapacity}).");
            if (!party.Leader.IsEmpty && !members.Contains(party.Leader)) result.AddError("The party's leader is not a member.");

            var records = new HashSet<NpcId>();
            foreach (var c in world.Companions)
            {
                if (c == null) { result.AddError("Null companion record."); continue; }
                if (c.NpcId.IsEmpty || !records.Add(c.NpcId)) result.AddError($"Companion record {c.NpcId} is missing an id or duplicated.");
                if (!npcIds.Contains(c.NpcId)) result.AddError($"Companion record {c.NpcId} is not a known person.");
                if (c.Standing == CompanionStanding.InParty && !members.Contains(c.NpcId)) result.AddError($"Companion record {c.NpcId} says they are in the party, but they are not.");
                if (c.Approvals.Count > CompanionRecordData.MaxApprovals) result.AddError($"Companion record {c.NpcId} keeps more than {CompanionRecordData.MaxApprovals} approvals.");
                var keys = new HashSet<string>();
                foreach (var a in c.Approvals)
                    if (a == null || string.IsNullOrEmpty(a.Key) || !keys.Add(a.Key)) result.AddError($"Companion record {c.NpcId} counts a cause twice or without a key.");
            }

            // Every person's own items (companions, and anyone who once was): one holder for each instance.
            foreach (var n in world.Npcs)
            {
                var ch = n?.Character;
                if (ch == null) continue;
                if (!ch.OwnsItems)
                {
                    if (ch.Inventory.Count > 0 || ch.Equipment.Count > 0) result.AddError($"{n.Name} holds items without owning items.");
                    continue;
                }

                foreach (var s in ch.Inventory) CheckInstance(s, n.Name + "'s pack", stackIds, result);
                foreach (var pair in ch.Equipment)
                {
                    CheckInstance(pair.Value, $"{n.Name}'s {pair.Key}", stackIds, result);
                    var def = pair.Value == null ? null : ItemCatalog.Find(pair.Value.ItemId);
                    if (def != null && !SlotFits(def, pair.Key)) result.AddError($"{n.Name} wears {def.Name} in a slot it does not fit ({pair.Key}).");
                }
            }
        }
    }
}
