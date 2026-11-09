using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
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

            if (world.Regions == null)
            {
                result.AddError("Regions collection must not be null.");
                return result;
            }

            var regions = new Dictionary<RegionId, RegionData>();
            foreach (var region in world.Regions)
            {
                if (region == null) { result.AddError("Null region entry."); continue; }
                if (region.RegionId.IsEmpty) result.AddError($"Region '{region.Name}' has an empty ID.");
                else if (regions.ContainsKey(region.RegionId)) result.AddError($"Duplicate RegionId {region.RegionId}.");
                else regions.Add(region.RegionId, region);
                if (string.IsNullOrWhiteSpace(region.Name)) result.AddError($"Region {region.RegionId} has no name.");
                else if (region.Name.Length > RegionData.MaxNameLength) result.AddError($"Region {region.RegionId} name is too long.");
                if (region.Width < 1 || region.Depth < 1) result.AddError($"Region '{region.Name}' must cover at least one cell.");
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
                if (settlement.RegionId.IsEmpty)
                    result.AddError($"Settlement '{settlement.Name}' has no region.");
                else if (!regions.TryGetValue(settlement.RegionId, out var region))
                    result.AddError($"Settlement '{settlement.Name}' references unknown region {settlement.RegionId}.");
                else if (!region.Contains(settlement.Position))
                    result.AddError($"Settlement '{settlement.Name}' at {settlement.Position} lies outside its region '{region.Name}'.");
            }

            if (world.Player != null) ValidatePlayer(world, settlementIds, regions, result);

            var npcIds = new HashSet<NpcId>();
            var namedPerSettlement = new Dictionary<SettlementId, int>();
            foreach (var npc in world.Npcs)
            {
                if (npc == null) { result.AddError("Null NPC entry."); continue; }
                if (npc.NpcId.IsEmpty) result.AddError("NPC has empty ID.");
                else if (!npcIds.Add(npc.NpcId)) result.AddError($"Duplicate NpcId {npc.NpcId}.");
                if (string.IsNullOrWhiteSpace(npc.Name)) result.AddError($"NPC {npc.NpcId} has no name.");
                if (npc.ConversationCount < 0) result.AddError($"NPC {npc.NpcId} has a negative conversation count.");
                if (!npc.HomeSettlementId.IsEmpty && !settlementIds.Contains(npc.HomeSettlementId))
                    result.AddError($"NPC {npc.NpcId} references unknown settlement.");
                if (!npc.HomeSettlementId.IsEmpty)
                {
                    namedPerSettlement.TryGetValue(npc.HomeSettlementId, out var count);
                    namedPerSettlement[npc.HomeSettlementId] = count + 1;
                }
            }

            // Named people are part of the population, never extra to it.
            foreach (var settlement in world.Settlements)
            {
                if (settlement != null && namedPerSettlement.TryGetValue(settlement.SettlementId, out var named) && named > settlement.Population)
                    result.AddError($"Settlement '{settlement.Name}' has more named residents ({named}) than people ({settlement.Population}).");
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
                foreach (var id in e.SettlementIds)
                {
                    if (id.IsEmpty || !settlementIds.Contains(id)) result.AddError($"Event {e.EventId} references unknown settlement.");
                }
            }

            ValidateTrade(world, settlementIds, result);
            return result;
        }

        private static void ValidatePlayer(WorldData world, HashSet<SettlementId> settlementIds, Dictionary<RegionId, RegionData> regions, ValidationResult result)
        {
            var p = world.Player;
            if (p.CharacterId.IsEmpty) result.AddError("Player character has an empty ID.");
            if (string.IsNullOrWhiteSpace(p.Name)) result.AddError("Player character has no name.");
            else if (p.Name.Length > PlayerCharacterData.MaxNameLength) result.AddError("Player character name is too long.");
            if (!System.Enum.IsDefined(typeof(CharacterLocationKind), p.LocationKind)) result.AddError("Player character location kind is unknown.");
            if (!System.Enum.IsDefined(typeof(CameraPerspective), p.Perspective)) result.AddError("Player character perspective is unknown.");
            if (!p.Pose.IsFinite) result.AddError("Player character position is not a finite number.");
            if (!p.LastSettlementId.IsEmpty && !settlementIds.Contains(p.LastSettlementId))
                result.AddError("Player character's last settlement does not exist.");

            if (p.LocationKind == CharacterLocationKind.Settlement)
            {
                var settlement = world.FindSettlement(p.SettlementId);
                if (settlement == null) result.AddError("Player character is in a settlement that does not exist.");
                else if (p.RegionId != settlement.RegionId) result.AddError("Player character's region does not match their settlement's region.");
                if (!regions.ContainsKey(p.RegionId)) result.AddError("Player character is in a region that does not exist.");
            }
            else if (!p.SettlementId.IsEmpty || !p.RegionId.IsEmpty)
            {
                result.AddError("Player character on the world map must not reference a settlement or region.");
            }
        }

        private static void ValidateTrade(WorldData world, HashSet<SettlementId> settlementIds, ValidationResult result)
        {
            if (world.TradeRoutes == null || world.Shipments == null)
            {
                result.AddError("Trade collections must not be null.");
                return;
            }

            var routes = new Dictionary<TradeRouteId, TradeRouteData>();
            foreach (var r in world.TradeRoutes)
            {
                if (r == null) { result.AddError("Null trade route entry."); continue; }
                var label = $"Trade route {r.TradeRouteId}";
                if (r.TradeRouteId.IsEmpty) result.AddError("Trade route has empty ID.");
                else if (routes.ContainsKey(r.TradeRouteId)) result.AddError($"Duplicate TradeRouteId {r.TradeRouteId}.");
                else routes.Add(r.TradeRouteId, r);
                if (!settlementIds.Contains(r.OriginId)) result.AddError($"{label} has an unknown origin settlement.");
                if (!settlementIds.Contains(r.DestinationId)) result.AddError($"{label} has an unknown destination settlement.");
                if (r.OriginId == r.DestinationId) result.AddError($"{label} starts and ends at the same settlement.");
                if (r.CapacityPerShipment < 1) result.AddError($"{label} capacity must be at least 1.");
                if (r.TravelDays < 1) result.AddError($"{label} travel time must be at least 1 day.");
                if (r.FrequencyDays < 1) result.AddError($"{label} frequency must be at least 1 day.");
                if (r.EfficiencyPercent < 1 || r.EfficiencyPercent > 100) result.AddError($"{label} efficiency must be 1..100.");
                if (r.ShipmentsDelivered < 0) result.AddError($"{label} has a negative delivery count.");
                if (r.Delivered == null) result.AddError($"{label} has no delivery totals.");
            }

            var shipmentIds = new HashSet<ShipmentId>();
            var routesInUse = new HashSet<TradeRouteId>();
            foreach (var s in world.Shipments)
            {
                if (s == null) { result.AddError("Null shipment entry."); continue; }
                var label = $"Shipment {s.ShipmentId}";
                if (s.ShipmentId.IsEmpty) result.AddError("Shipment has empty ID.");
                else if (!shipmentIds.Add(s.ShipmentId)) result.AddError($"Duplicate ShipmentId {s.ShipmentId}.");
                if (!routes.TryGetValue(s.TradeRouteId, out var route)) result.AddError($"{label} references an unknown trade route.");
                else if (!routesInUse.Add(s.TradeRouteId)) result.AddError($"Trade route {s.TradeRouteId} has more than one shipment on the road.");
                else if (s.OriginId != route.OriginId) result.AddError($"{label} origin does not match its route.");
                if (!settlementIds.Contains(s.OriginId) || !settlementIds.Contains(s.DestinationId)) result.AddError($"{label} references an unknown settlement.");
                if (s.ResourceTypeId.IsEmpty || !Economy.ResourceTypes.IsKnown(s.ResourceTypeId) || s.ResourceTypeId == Economy.ResourceTypes.Coin)
                    result.AddError($"{label} carries an unknown or non-tradeable resource.");
                if (s.Quantity < 1) result.AddError($"{label} quantity must be positive.");
                if (s.ArrivalDay < s.DepartureDay) result.AddError($"{label} arrives before it departs.");
                if (s.DaysWaiting < 0) result.AddError($"{label} has negative waiting days.");
            }
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
            if (s.Position.X < 0 || s.Position.Z < 0) result.AddError($"Settlement '{label}' has a negative map position.");
            if (s.Economy == null)
            {
                result.AddError($"Settlement '{label}' has no economy.");
            }

            if (s.ResourceInventory == null)
            {
                result.AddError($"Settlement '{label}' has no resource inventory.");
            }
            else
            {
                foreach (var stack in s.ResourceInventory.Stacks)
                {
                    if (stack.Quantity < 0) result.AddError($"Settlement '{label}' has negative {stack.ResourceTypeId}.");
                    if (!Economy.ResourceTypes.IsKnown(stack.ResourceTypeId)) result.AddError($"Settlement '{label}' stores unknown resource '{stack.ResourceTypeId}'.");
                    if (s.Economy != null && stack.Quantity > s.Economy.CapacityOf(stack.ResourceTypeId))
                        result.AddError($"Settlement '{label}' stores more {stack.ResourceTypeId} than its capacity.");
                }
            }

            if (s.Economy != null) ValidateEconomy(s, label, result);
        }

        private static void ValidateEconomy(SettlementData s, string label, ValidationResult result)
        {
            var e = s.Economy;
            if (e.Production == null || e.StorageCapacity == null || e.LastDay == null || e.LastDaySources == null ||
                e.ResourceStatuses == null || e.LifetimeProduced == null || e.Markers == null)
            {
                result.AddError($"Settlement '{label}' economy has missing sections.");
                return;
            }

            var seen = new HashSet<Economy.ProductionSourceType>();
            var workers = 0L;
            foreach (var p in e.Production)
            {
                if (p == null) { result.AddError($"Settlement '{label}' has a null production entry."); continue; }
                if (!seen.Add(p.SourceType)) result.AddError($"Settlement '{label}' lists {p.SourceType} twice.");
                if (p.AllocationWeight < 0 || p.WorkerCapacity < 0 || p.Workers < 0)
                    result.AddError($"Settlement '{label}' {p.SourceType} has negative labour values.");
                if (p.Workers > p.WorkerCapacity) result.AddError($"Settlement '{label}' {p.SourceType} employs more workers than its capacity.");
                if (p.EfficiencyPercent < 1 || p.EfficiencyPercent > 300) result.AddError($"Settlement '{label}' {p.SourceType} efficiency must be 1..300.");
                workers += p.Workers;
            }

            if (s.Population >= 0 && workers > s.Population) result.AddError($"Settlement '{label}' employs more workers than it has people.");
            if (e.DaysWithoutFood < 0) result.AddError($"Settlement '{label}' has negative days without food.");
            if (e.TradeActivity < 0) result.AddError($"Settlement '{label}' has negative trade activity.");
            if (e.ProsperityTarget < SettlementData.MinRating || e.ProsperityTarget > SettlementData.MaxRating ||
                e.StabilityTarget < SettlementData.MinRating || e.StabilityTarget > SettlementData.MaxRating)
                result.AddError($"Settlement '{label}' rating targets are outside 0..100.");
            foreach (var status in e.ResourceStatuses)
            {
                if (status == null || status.DaysBelowThreshold < 0) result.AddError($"Settlement '{label}' has an invalid resource status.");
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
