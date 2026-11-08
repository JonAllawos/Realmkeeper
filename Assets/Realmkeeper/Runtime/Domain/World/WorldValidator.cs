using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    public sealed class ValidationResult
    {
        private readonly List<string> _errors = new List<string>();

        public bool IsValid => _errors.Count == 0;
        public IReadOnlyList<string> Errors => _errors;

        public void AddError(string error) => _errors.Add(error);

        public override string ToString() => IsValid ? "Valid" : string.Join("; ", _errors);
    }

    /// <summary>
    /// Structural and invariant checks for world state. Used before committing a simulated
    /// day, after loading a save, and after generation. Collects all errors instead of
    /// stopping at the first one.
    /// </summary>
    public static class WorldValidator
    {
        public static ValidationResult Validate(WorldData world)
        {
            var result = new ValidationResult();
            if (world == null)
            {
                result.AddError("World is null.");
                return result;
            }

            if (world.WorldId.IsEmpty) result.AddError("WorldId is empty.");
            if (string.IsNullOrWhiteSpace(world.WorldName)) result.AddError("WorldName is empty.");
            else if (world.WorldName.Length > WorldData.MaxNameLength) result.AddError("WorldName is too long.");
            if (world.GeneratorVersion <= 0) result.AddError("GeneratorVersion must be positive.");
            if (world.SchemaVersion != WorldData.CurrentSchemaVersion)
                result.AddError($"SchemaVersion {world.SchemaVersion} is not the current schema ({WorldData.CurrentSchemaVersion}).");
            if (!world.CurrentSimulationDate.IsValidDate) result.AddError("CurrentSimulationDate is invalid.");

            if (world.WorldSettings == null)
            {
                result.AddError("WorldSettings is missing.");
            }
            else
            {
                if (!world.WorldSettings.StartingDate.IsValidDate) result.AddError("StartingDate is invalid.");
                else if (world.CurrentSimulationDate.IsValidDate && world.CurrentSimulationDate < world.WorldSettings.StartingDate)
                    result.AddError("CurrentSimulationDate precedes StartingDate.");
                if (world.WorldSettings.EnabledSimulationSystems == null) result.AddError("EnabledSimulationSystems is null.");
            }

            if (world.Settlements == null || world.Factions == null || world.Npcs == null || world.HistoricalEvents == null)
            {
                result.AddError("World collections must not be null.");
                return result;
            }

            var factionIds = new HashSet<FactionId>();
            foreach (var faction in world.Factions)
            {
                if (faction == null) { result.AddError("Null faction entry."); continue; }
                if (faction.FactionId.IsEmpty) result.AddError("Faction has empty ID.");
                else if (!factionIds.Add(faction.FactionId)) result.AddError($"Duplicate FactionId {faction.FactionId}.");
                if (string.IsNullOrWhiteSpace(faction.Name)) result.AddError($"Faction {faction.FactionId} has no name.");
            }

            var settlementIds = new HashSet<SettlementId>();
            foreach (var settlement in world.Settlements)
            {
                if (settlement == null) { result.AddError("Null settlement entry."); continue; }
                ValidateSettlement(settlement, result);
                if (!settlement.SettlementId.IsEmpty && !settlementIds.Add(settlement.SettlementId))
                    result.AddError($"Duplicate SettlementId {settlement.SettlementId}.");
                if (!settlement.FactionId.IsEmpty && !factionIds.Contains(settlement.FactionId))
                    result.AddError($"Settlement '{settlement.Name}' references unknown faction {settlement.FactionId}.");
            }

            var npcIds = new HashSet<NpcId>();
            foreach (var npc in world.Npcs)
            {
                if (npc == null) { result.AddError("Null NPC entry."); continue; }
                if (npc.NpcId.IsEmpty) result.AddError("NPC has empty ID.");
                else if (!npcIds.Add(npc.NpcId)) result.AddError($"Duplicate NpcId {npc.NpcId}.");
                if (!npc.HomeSettlementId.IsEmpty && !settlementIds.Contains(npc.HomeSettlementId))
                    result.AddError($"NPC {npc.NpcId} references unknown settlement.");
            }

            var eventIds = new HashSet<EventId>();
            foreach (var e in world.HistoricalEvents)
            {
                if (e == null) { result.AddError("Null event entry."); continue; }
                if (e.EventId.IsEmpty) result.AddError("Event has empty ID.");
                else if (!eventIds.Add(e.EventId)) result.AddError($"Duplicate EventId {e.EventId}.");
                if (!e.Date.IsValidDate) result.AddError($"Event {e.EventId} has an invalid date.");
                else if (world.CurrentSimulationDate.IsValidDate && e.Date > world.CurrentSimulationDate)
                    result.AddError($"Event {e.EventId} is dated in the future.");
                if (!e.SettlementId.IsEmpty && !settlementIds.Contains(e.SettlementId))
                    result.AddError($"Event {e.EventId} references unknown settlement.");
            }

            return result;
        }

        public static void ValidateSettlement(SettlementData s, ValidationResult result)
        {
            var label = string.IsNullOrWhiteSpace(s.Name) ? s.SettlementId.ToString() : s.Name;
            if (s.SettlementId.IsEmpty) result.AddError($"Settlement '{label}' has empty ID.");
            if (string.IsNullOrWhiteSpace(s.Name)) result.AddError($"Settlement {s.SettlementId} has no name.");
            else if (s.Name.Length > SettlementData.MaxNameLength) result.AddError($"Settlement '{label}' name is too long.");
            if (s.Population < 0) result.AddError($"Settlement '{label}' has negative population.");
            if (s.Prosperity < SettlementData.MinRating || s.Prosperity > SettlementData.MaxRating)
                result.AddError($"Settlement '{label}' prosperity {s.Prosperity} is outside 0..100.");
            if (s.Stability < SettlementData.MinRating || s.Stability > SettlementData.MaxRating)
                result.AddError($"Settlement '{label}' stability {s.Stability} is outside 0..100.");
            if (s.FoodProductionPerDay < 0) result.AddError($"Settlement '{label}' has negative food production.");
            if (s.Position.X < 0 || s.Position.Z < 0) result.AddError($"Settlement '{label}' has a negative map position.");
            if (s.ResourceInventory == null)
            {
                result.AddError($"Settlement '{label}' has no resource inventory.");
            }
            else
            {
                foreach (var stack in s.ResourceInventory.Stacks)
                {
                    if (stack.Quantity < 0) result.AddError($"Settlement '{label}' has negative {stack.ResourceTypeId}.");
                }
            }
        }

        public static ValidationResult ValidateSettlement(SettlementData settlement)
        {
            var result = new ValidationResult();
            if (settlement == null) result.AddError("Settlement is null.");
            else ValidateSettlement(settlement, result);
            return result;
        }
    }
}
