using System.Collections.Generic;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>Phase 8 invariants: encounters (one active at most, consistent order and vitals) and settlement structures.</summary>
    public static partial class WorldValidator
    {
        private static void ValidateCombat(WorldData world, HashSet<SettlementId> settlementIds, HashSet<NpcId> npcIds, ValidationResult result)
        {
            var encounterIds = new HashSet<EncounterId>();
            var active = 0;
            var ended = 0;
            foreach (var e in world.Encounters)
            {
                if (e == null) { result.AddError("Null encounter."); continue; }
                var label = $"Encounter '{e.Name}'";
                if (e.EncounterId.IsEmpty || !encounterIds.Add(e.EncounterId)) result.AddError($"{label} has a missing or duplicate id.");
                if (e.IsActive) active++;
                else if (e.Status == EncounterStatus.Ended) ended++;
                if (!e.SettlementId.IsEmpty && !settlementIds.Contains(e.SettlementId)) result.AddError($"{label} references unknown settlement.");
                if (e.Round < 0 || e.RollCount < 0 || e.ActionSequence < 0) result.AddError($"{label} has a negative round, roll count or action sequence.");
                if (e.Log.Count > EncounterData.MaxLogEntries) result.AddError($"{label} keeps more than {EncounterData.MaxLogEntries} log entries.");

                var ids = new HashSet<CombatantId>();
                var sources = new HashSet<string>();
                var players = 0;
                foreach (var c in e.Combatants)
                {
                    if (c == null) { result.AddError($"{label} has a null combatant."); continue; }
                    var who = $"{label}: combatant '{c.Name}'";
                    if (c.CombatantId.IsEmpty || !ids.Add(c.CombatantId)) result.AddError($"{who} has a missing or duplicate id.");
                    if (string.IsNullOrEmpty(c.Side)) result.AddError($"{who} is on no side.");
                    if (c.Kind == CombatantKind.Player)
                    {
                        players++;
                        if (world.Player == null) result.AddError($"{who} is the player, but there is no player.");
                    }
                    else
                    {
                        if (CreatureCatalog.Find(c.CreatureId) == null) result.AddError($"{who} has unknown creature profile '{c.CreatureId}'.");
                        if (c.MaxHp < 1 || c.CurrentHp < 0 || c.CurrentHp > c.MaxHp || c.TemporaryHp < 0) result.AddError($"{who} has hit points out of range.");
                        if (c.DeathSaveSuccesses < 0 || c.DeathSaveSuccesses > 3 || c.DeathSaveFailures < 0 || c.DeathSaveFailures > 3)
                            result.AddError($"{who} has death saves out of range.");
                        if (c.State == VitalState.Conscious && c.CurrentHp == 0) result.AddError($"{who} is conscious at 0 hit points.");
                        if ((c.State == VitalState.Dying || c.State == VitalState.Stable || c.State == VitalState.Dead) && c.CurrentHp > 0)
                            result.AddError($"{who} is {c.State.ToString().ToLowerInvariant()} with hit points left.");
                    }

                    if (c.Kind == CombatantKind.Npc)
                    {
                        if (!System.Guid.TryParse(c.SourceId, out var g) || !npcIds.Contains(new NpcId(g))) result.AddError($"{who} is not an existing resident.");
                        if (!sources.Add(c.SourceId ?? string.Empty)) result.AddError($"{who}: the same resident is in the encounter twice.");
                    }

                    var exhaustion = 0;
                    foreach (var k in c.Conditions)
                    {
                        if (k == null) { result.AddError($"{who} has a null condition."); continue; }
                        if (k.Type == ConditionType.Exhaustion)
                        {
                            exhaustion++;
                            if (k.Level < 1 || k.Level > 6) result.AddError($"{who} has exhaustion level {k.Level}.");
                        }
                    }

                    if (exhaustion > 1) result.AddError($"{who} has exhaustion recorded twice.");
                    if (c.Turn == null || c.Turn.MovementUsed < 0 || c.Turn.AttacksLeft < 0) result.AddError($"{who} has invalid turn resources.");
                }

                if (players > 1) result.AddError($"{label} has the player twice.");
                var ordered = new HashSet<CombatantId>();
                foreach (var id in e.Order)
                {
                    if (!ids.Contains(id)) result.AddError($"{label}: the initiative order names an unknown combatant.");
                    if (!ordered.Add(id)) result.AddError($"{label}: a combatant is in the initiative order twice.");
                }

                if (ordered.Count != ids.Count) result.AddError($"{label}: the initiative order does not list every combatant.");
                if (e.IsActive && (e.TurnIndex < 0 || e.TurnIndex >= e.Order.Count)) result.AddError($"{label}: the turn points outside the initiative order.");
                if (e.IsActive && e.Outcome != EncounterOutcome.Undecided) result.AddError($"{label} is active but already decided.");
            }

            if (active > 1) result.AddError($"{active} encounters are active; at most one may be.");
            if (ended > WorldData.MaxEndedEncounters) result.AddError($"More than {WorldData.MaxEndedEncounters} finished encounters are kept.");

            var structureIds = new HashSet<StructureId>();
            var placed = new HashSet<LocationId>();
            foreach (var s in world.Structures)
            {
                if (s == null) { result.AddError("Null structure."); continue; }
                var label = $"Structure '{s.Name}'";
                if (s.StructureId.IsEmpty || !structureIds.Add(s.StructureId)) result.AddError($"{label} has a missing or duplicate id.");
                var def = StructureCatalog.Find(s.DefinitionId);
                if (def == null) result.AddError($"{label} has unknown definition '{s.DefinitionId}'.");
                if (!settlementIds.Contains(s.SettlementId)) result.AddError($"{label} references unknown settlement.");
                if (!s.LocationId.IsEmpty)
                {
                    var place = world.FindLocation(s.LocationId);
                    if (place == null) result.AddError($"{label} references unknown location.");
                    else if (place.SettlementId != s.SettlementId) result.AddError($"{label} stands on a location of another settlement.");
                    if (!placed.Add(s.LocationId)) result.AddError($"{label}: another structure already stands on its location.");
                }
                else if (def != null && def.Role == StructureRole.Functional) result.AddError($"{label} is functional but stands on no location.");

                if (!s.ShopId.IsEmpty)
                {
                    var shop = world.FindShop(s.ShopId);
                    if (shop == null) result.AddError($"{label} references unknown shop.");
                    else if (shop.LocationId != s.LocationId) result.AddError($"{label} houses a shop that is elsewhere.");
                }

                if (!s.OwnerNpcId.IsEmpty && !npcIds.Contains(s.OwnerNpcId)) result.AddError($"{label} has an unknown owner.");
            }
        }
    }
}
