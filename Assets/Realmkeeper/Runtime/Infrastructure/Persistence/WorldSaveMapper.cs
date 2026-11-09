using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Raised when save data cannot be mapped to domain objects.</summary>
    public sealed class SaveDataException : Exception
    {
        public SaveDataException(string message) : base(message) { }
    }

    /// <summary>Explicit, field-by-field mapping between domain models and save DTOs.</summary>
    public static class WorldSaveMapper
    {
        // ----- Domain → DTO -----------------------------------------------------------------

        public static WorldDto ToDto(WorldData w)
        {
            var dto = new WorldDto
            {
                WorldId = w.WorldId.ToString(),
                WorldName = w.WorldName,
                Seed = w.Seed,
                GeneratorVersion = w.GeneratorVersion,
                CurrentSimulationDate = w.CurrentSimulationDate.ToIsoString(),
                Settings = new WorldSettingsDto
                {
                    SimulationSpeed = w.WorldSettings.SimulationSpeed.ToString(),
                    StartingDate = w.WorldSettings.StartingDate.ToIsoString(),
                    EnabledSimulationSystems = new List<string>(w.WorldSettings.EnabledSimulationSystems),
                },
                Regions = new List<RegionDto>(),
                Settlements = new List<SettlementDto>(),
                Factions = new List<FactionDto>(),
                Npcs = new List<NpcDto>(),
                HistoricalEvents = new List<HistoricalEventDto>(),
                TradeRoutes = new List<TradeRouteDto>(),
                Shipments = new List<ShipmentDto>(),
                Player = w.Player == null ? null : new PlayerDto
                {
                    CharacterId = w.Player.CharacterId.ToString(),
                    Name = w.Player.Name,
                    LocationKind = w.Player.LocationKind.ToString(),
                    SettlementId = w.Player.SettlementId.IsEmpty ? null : w.Player.SettlementId.ToString(),
                    RegionId = w.Player.RegionId.IsEmpty ? null : w.Player.RegionId.ToString(),
                    X = w.Player.Pose.X,
                    Y = w.Player.Pose.Y,
                    Z = w.Player.Pose.Z,
                    Yaw = w.Player.Pose.Yaw,
                    Perspective = w.Player.Perspective.ToString(),
                    LastSettlementId = w.Player.LastSettlementId.IsEmpty ? null : w.Player.LastSettlementId.ToString(),
                },
            };

            foreach (var r in w.Regions)
            {
                dto.Regions.Add(new RegionDto
                {
                    RegionId = r.RegionId.ToString(), Name = r.Name, OriginX = r.Origin.X, OriginZ = r.Origin.Z, Width = r.Width, Depth = r.Depth,
                });
            }

            foreach (var s in w.Settlements)
            {
                dto.Settlements.Add(new SettlementDto
                {
                    SettlementId = s.SettlementId.ToString(),
                    Name = s.Name,
                    SettlementType = s.SettlementType.ToString(),
                    RegionId = s.RegionId.IsEmpty ? null : s.RegionId.ToString(),
                    PositionX = s.Position.X,
                    PositionZ = s.Position.Z,
                    Population = s.Population,
                    Prosperity = s.Prosperity,
                    Stability = s.Stability,
                    FactionId = s.FactionId.IsEmpty ? null : s.FactionId.ToString(),
                    IsInFoodShortage = s.IsInFoodShortage,
                    Resources = Stacks(s.ResourceInventory),
                    Economy = s.Economy.IsInitialized ? EconomyToDto(s.Economy) : null,
                });
            }

            foreach (var f in w.Factions) dto.Factions.Add(new FactionDto { FactionId = f.FactionId.ToString(), Name = f.Name });

            foreach (var n in w.Npcs)
            {
                dto.Npcs.Add(new NpcDto
                {
                    NpcId = n.NpcId.ToString(),
                    Name = n.Name,
                    HomeSettlementId = n.HomeSettlementId.IsEmpty ? null : n.HomeSettlementId.ToString(),
                    FactionId = n.FactionId.IsEmpty ? null : n.FactionId.ToString(),
                    Role = n.Role.ToString(),
                    MetPlayerOn = n.HasMetPlayer ? n.MetPlayerOn.ToIsoString() : null,
                    ConversationCount = n.ConversationCount,
                });
            }

            foreach (var e in w.HistoricalEvents)
            {
                var ids = new List<string>();
                foreach (var id in e.SettlementIds) ids.Add(id.ToString());
                dto.HistoricalEvents.Add(new HistoricalEventDto
                {
                    EventId = e.EventId.ToString(),
                    Date = e.Date.ToIsoString(),
                    EventType = e.EventType.ToString(),
                    Severity = e.Severity.ToString(),
                    Importance = e.Importance.ToString(),
                    SettlementId = e.SettlementId.IsEmpty ? null : e.SettlementId.ToString(),
                    SettlementIds = ids,
                    Data = ToDictionary(e.Data),
                    Description = e.Description,
                });
            }

            foreach (var r in w.TradeRoutes)
            {
                dto.TradeRoutes.Add(new TradeRouteDto
                {
                    TradeRouteId = r.TradeRouteId.ToString(),
                    OriginId = r.OriginId.ToString(),
                    DestinationId = r.DestinationId.ToString(),
                    CapacityPerShipment = r.CapacityPerShipment,
                    TravelDays = r.TravelDays,
                    FrequencyDays = r.FrequencyDays,
                    EfficiencyPercent = r.EfficiencyPercent,
                    Status = r.Status.ToString(),
                    IsSuspended = r.IsSuspended,
                    NextDepartureDay = r.NextDepartureDay,
                    ShipmentsDelivered = r.ShipmentsDelivered,
                    Delivered = Stacks(r.Delivered),
                    LastReportedDay = r.LastReportedDay,
                    LastDeliveryDay = r.LastDeliveryDay,
                    LastDeliveryResource = r.LastDeliveryResource.IsEmpty ? null : r.LastDeliveryResource.Value,
                    LastDeliveryQuantity = r.LastDeliveryQuantity,
                    LastDeliveryPayment = r.LastDeliveryPayment,
                });
            }

            foreach (var s in w.Shipments)
            {
                dto.Shipments.Add(new ShipmentDto
                {
                    ShipmentId = s.ShipmentId.ToString(),
                    TradeRouteId = s.TradeRouteId.ToString(),
                    OriginId = s.OriginId.ToString(),
                    DestinationId = s.DestinationId.ToString(),
                    ResourceTypeId = s.ResourceTypeId.Value,
                    Quantity = s.Quantity,
                    DeliveredQuantity = s.DeliveredQuantity,
                    LostQuantity = s.LostQuantity,
                    Payment = s.Payment,
                    DepartureDay = s.DepartureDay,
                    ArrivalDay = s.ArrivalDay,
                    DaysWaiting = s.DaysWaiting,
                    LossApplied = s.LossApplied,
                    IsReturning = s.IsReturning,
                });
            }

            return dto;
        }

        private static EconomyDto EconomyToDto(SettlementEconomy e)
        {
            var dto = new EconomyDto
            {
                Specialization = e.Specialization.ToString(),
                SecondarySpecialization = e.SecondarySpecialization.ToString(),
                LabourFocus = e.LabourFocus?.ToString(),
                Production = new List<ProductionDto>(),
                StorageCapacity = Stacks(e.StorageCapacity),
                LedgerDay = e.LedgerDay,
                LastDay = new List<ResourceFlowDto>(),
                LastDaySources = new List<SourceOutputDto>(),
                ResourceStatuses = new List<ResourceStatusDto>(),
                LifetimeProduced = Stacks(e.LifetimeProduced),
                DaysWithoutFood = e.DaysWithoutFood,
                PopulationProgressMilli = e.PopulationProgressMilli,
                ProsperityTarget = e.ProsperityTarget,
                StabilityTarget = e.StabilityTarget,
                TradeActivity = e.TradeActivity,
                Markers = new MarkersDto
                {
                    ReportedProsperity = e.Markers.ReportedProsperity,
                    ReportedStability = e.Markers.ReportedStability,
                    ReportedPopulation = e.Markers.ReportedPopulation,
                    CoinMilestone = e.Markers.CoinMilestone,
                    LastFoodExhaustedReportDay = e.Markers.LastFoodExhaustedReportDay,
                    LastStarvationReportDay = e.Markers.LastStarvationReportDay,
                    ProductionMilestoneIndex = new List<ResourceStackDto>(),
                },
            };

            foreach (var p in e.Production)
            {
                dto.Production.Add(new ProductionDto
                {
                    SourceType = p.SourceType.ToString(),
                    AllocationWeight = p.AllocationWeight,
                    WorkerCapacity = p.WorkerCapacity,
                    EfficiencyPercent = p.EfficiencyPercent,
                    Workers = p.Workers,
                });
            }

            foreach (var f in e.LastDay)
            {
                dto.LastDay.Add(new ResourceFlowDto
                {
                    ResourceTypeId = f.ResourceTypeId.Value,
                    Produced = f.Produced,
                    Wasted = f.Wasted,
                    Demand = f.Demand,
                    Consumed = f.Consumed,
                    Shortfall = f.Shortfall,
                    Imported = f.Imported,
                    Exported = f.Exported,
                });
            }

            foreach (var s in e.LastDaySources) dto.LastDaySources.Add(new SourceOutputDto { SourceType = s.SourceType.ToString(), Workers = s.Workers, Amount = s.Amount });
            foreach (var s in e.ResourceStatuses)
                dto.ResourceStatuses.Add(new ResourceStatusDto { ResourceTypeId = s.ResourceTypeId.Value, InShortage = s.InShortage, DaysBelowThreshold = s.DaysBelowThreshold });
            foreach (var pair in e.Markers.ProductionMilestoneIndex)
                dto.Markers.ProductionMilestoneIndex.Add(new ResourceStackDto { ResourceTypeId = pair.Key.Value, Quantity = pair.Value });
            return dto;
        }

        private static List<ResourceStackDto> Stacks(ResourceInventory inventory)
        {
            var list = new List<ResourceStackDto>();
            foreach (var r in inventory.Stacks) list.Add(new ResourceStackDto { ResourceTypeId = r.ResourceTypeId.Value, Quantity = r.Quantity });
            return list;
        }

        private static Dictionary<string, string> ToDictionary(IReadOnlyDictionary<string, string> data)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in data) d[pair.Key] = pair.Value;
            return d;
        }

        // ----- DTO → Domain -----------------------------------------------------------------

        /// <summary>Maps a DTO to domain data. Throws <see cref="SaveDataException"/> on malformed fields.</summary>
        public static WorldData FromDto(WorldDto dto)
        {
            if (dto == null) throw new SaveDataException("World section is missing.");
            if (dto.Settings == null) throw new SaveDataException("Settings section is missing.");

            var world = new WorldData
            {
                WorldId = new WorldId(RequiredGuid(dto.WorldId, "worldId")),
                WorldName = dto.WorldName,
                Seed = dto.Seed,
                GeneratorVersion = dto.GeneratorVersion,
                SchemaVersion = WorldData.CurrentSchemaVersion,
                CurrentSimulationDate = ParseDate(dto.CurrentSimulationDate, "currentSimulationDate"),
                WorldSettings = new WorldSettings
                {
                    SimulationSpeed = ParseEnum<SimulationSpeed>(dto.Settings.SimulationSpeed, "simulationSpeed"),
                    StartingDate = ParseDate(dto.Settings.StartingDate, "startingDate"),
                    EnabledSimulationSystems = new List<string>(dto.Settings.EnabledSimulationSystems ?? new List<string>()),
                },
            };

            foreach (var rd in dto.Regions ?? new List<RegionDto>())
            {
                if (rd == null) throw new SaveDataException("Null region entry.");
                world.Regions.Add(new RegionData
                {
                    RegionId = new RegionId(RequiredGuid(rd.RegionId, "regionId")),
                    Name = rd.Name,
                    Origin = new MapCoord(rd.OriginX, rd.OriginZ),
                    Width = rd.Width,
                    Depth = rd.Depth,
                });
            }

            if (dto.Player != null)
            {
                var pd = dto.Player;
                world.Player = new PlayerCharacterData
                {
                    CharacterId = new CharacterId(RequiredGuid(pd.CharacterId, "characterId")),
                    Name = pd.Name,
                    LocationKind = ParseEnum<CharacterLocationKind>(pd.LocationKind, "player locationKind"),
                    SettlementId = new SettlementId(OptionalGuid(pd.SettlementId, "player settlementId")),
                    RegionId = new RegionId(OptionalGuid(pd.RegionId, "player regionId")),
                    Pose = new LocalPose(pd.X, pd.Y, pd.Z, pd.Yaw),
                    Perspective = ParseEnum<CameraPerspective>(pd.Perspective, "player perspective"),
                    LastSettlementId = new SettlementId(OptionalGuid(pd.LastSettlementId, "player lastSettlementId")),
                };
            }

            foreach (var sd in dto.Settlements ?? new List<SettlementDto>())
            {
                if (sd == null) throw new SaveDataException("Null settlement entry.");
                var s = new SettlementData
                {
                    SettlementId = new SettlementId(RequiredGuid(sd.SettlementId, "settlementId")),
                    Name = sd.Name,
                    SettlementType = ParseEnum<SettlementType>(sd.SettlementType, "settlementType"),
                    RegionId = new RegionId(OptionalGuid(sd.RegionId, "regionId")),
                    Position = new MapCoord(sd.PositionX, sd.PositionZ),
                    Population = sd.Population,
                    Prosperity = sd.Prosperity,
                    Stability = sd.Stability,
                    FactionId = new FactionId(OptionalGuid(sd.FactionId, "factionId")),
                    IsInFoodShortage = sd.IsInFoodShortage,
                    ResourceInventory = Inventory(sd.Resources, $"Settlement '{sd.Name}' resources"),
                    Economy = sd.Economy == null ? new SettlementEconomy() : EconomyFromDto(sd.Economy, sd.Name),
                };
                world.Settlements.Add(s);
            }

            foreach (var fd in dto.Factions ?? new List<FactionDto>())
            {
                if (fd == null) throw new SaveDataException("Null faction entry.");
                world.Factions.Add(new FactionData { FactionId = new FactionId(RequiredGuid(fd.FactionId, "factionId")), Name = fd.Name });
            }

            foreach (var nd in dto.Npcs ?? new List<NpcDto>())
            {
                if (nd == null) throw new SaveDataException("Null NPC entry.");
                world.Npcs.Add(new NpcData
                {
                    NpcId = new NpcId(RequiredGuid(nd.NpcId, "npcId")),
                    Name = nd.Name,
                    HomeSettlementId = new SettlementId(OptionalGuid(nd.HomeSettlementId, "homeSettlementId")),
                    FactionId = new FactionId(OptionalGuid(nd.FactionId, "factionId")),
                    Role = string.IsNullOrEmpty(nd.Role) ? NpcRole.Resident : ParseEnum<NpcRole>(nd.Role, "npc role"),
                    MetPlayerOn = string.IsNullOrEmpty(nd.MetPlayerOn) ? default(WorldDate) : ParseDate(nd.MetPlayerOn, "npc metPlayerOn"),
                    ConversationCount = nd.ConversationCount,
                });
            }

            foreach (var ed in dto.HistoricalEvents ?? new List<HistoricalEventDto>())
            {
                if (ed == null) throw new SaveDataException("Null event entry.");
                var ids = new List<SettlementId>();
                if (ed.SettlementIds != null)
                {
                    foreach (var id in ed.SettlementIds) ids.Add(new SettlementId(RequiredGuid(id, "event settlementIds")));
                }
                else if (!string.IsNullOrEmpty(ed.SettlementId))
                {
                    ids.Add(new SettlementId(RequiredGuid(ed.SettlementId, "settlementId"))); // v1 events
                }

                world.HistoricalEvents.Add(new HistoricalEvent(
                    new EventId(RequiredGuid(ed.EventId, "eventId")),
                    ParseDate(ed.Date, "event date"),
                    ParseEnum<HistoricalEventType>(ed.EventType, "eventType"),
                    ParseEnum<EventSeverity>(ed.Severity, "severity"),
                    string.IsNullOrEmpty(ed.Importance) ? EventImportance.Notable : ParseEnum<EventImportance>(ed.Importance, "importance"),
                    ids,
                    ed.Data,
                    ed.Description));
            }

            foreach (var rd in dto.TradeRoutes ?? new List<TradeRouteDto>())
            {
                if (rd == null) throw new SaveDataException("Null trade route entry.");
                world.TradeRoutes.Add(new TradeRouteData
                {
                    TradeRouteId = new TradeRouteId(RequiredGuid(rd.TradeRouteId, "tradeRouteId")),
                    OriginId = new SettlementId(RequiredGuid(rd.OriginId, "originId")),
                    DestinationId = new SettlementId(RequiredGuid(rd.DestinationId, "destinationId")),
                    CapacityPerShipment = rd.CapacityPerShipment,
                    TravelDays = rd.TravelDays,
                    FrequencyDays = rd.FrequencyDays,
                    EfficiencyPercent = rd.EfficiencyPercent,
                    Status = ParseEnum<TradeRouteStatus>(rd.Status, "route status"),
                    IsSuspended = rd.IsSuspended,
                    NextDepartureDay = rd.NextDepartureDay,
                    ShipmentsDelivered = rd.ShipmentsDelivered,
                    Delivered = Inventory(rd.Delivered, "Trade route deliveries"),
                    LastReportedDay = rd.LastReportedDay,
                    LastDeliveryDay = rd.LastDeliveryDay,
                    LastDeliveryResource = string.IsNullOrEmpty(rd.LastDeliveryResource) ? default : Resource(rd.LastDeliveryResource, "lastDeliveryResource"),
                    LastDeliveryQuantity = rd.LastDeliveryQuantity,
                    LastDeliveryPayment = rd.LastDeliveryPayment,
                });
            }

            foreach (var sd in dto.Shipments ?? new List<ShipmentDto>())
            {
                if (sd == null) throw new SaveDataException("Null shipment entry.");
                world.Shipments.Add(new ShipmentData
                {
                    ShipmentId = new ShipmentId(RequiredGuid(sd.ShipmentId, "shipmentId")),
                    TradeRouteId = new TradeRouteId(RequiredGuid(sd.TradeRouteId, "shipment tradeRouteId")),
                    OriginId = new SettlementId(RequiredGuid(sd.OriginId, "shipment originId")),
                    DestinationId = new SettlementId(RequiredGuid(sd.DestinationId, "shipment destinationId")),
                    ResourceTypeId = Resource(sd.ResourceTypeId, "shipment resource"),
                    Quantity = sd.Quantity,
                    DeliveredQuantity = sd.DeliveredQuantity,
                    LostQuantity = sd.LostQuantity,
                    Payment = sd.Payment,
                    DepartureDay = sd.DepartureDay,
                    ArrivalDay = sd.ArrivalDay,
                    DaysWaiting = sd.DaysWaiting,
                    LossApplied = sd.LossApplied,
                    IsReturning = sd.IsReturning,
                });
            }

            return world;
        }

        private static SettlementEconomy EconomyFromDto(EconomyDto dto, string name)
        {
            var label = $"Settlement '{name}' economy";
            var e = new SettlementEconomy
            {
                Specialization = ParseEnum<SettlementSpecialization>(dto.Specialization, "specialization"),
                SecondarySpecialization = ParseEnum<SettlementSpecialization>(dto.SecondarySpecialization, "secondarySpecialization"),
                LabourFocus = string.IsNullOrEmpty(dto.LabourFocus) ? (ProductionSourceType?)null : ParseEnum<ProductionSourceType>(dto.LabourFocus, "labourFocus"),
                StorageCapacity = Inventory(dto.StorageCapacity, label + " storage"),
                LedgerDay = dto.LedgerDay,
                LifetimeProduced = Inventory(dto.LifetimeProduced, label + " lifetime production"),
                DaysWithoutFood = dto.DaysWithoutFood,
                PopulationProgressMilli = dto.PopulationProgressMilli,
                ProsperityTarget = dto.ProsperityTarget,
                StabilityTarget = dto.StabilityTarget,
                TradeActivity = dto.TradeActivity,
            };

            foreach (var p in dto.Production ?? new List<ProductionDto>())
            {
                if (p == null) throw new SaveDataException($"{label} has a null production entry.");
                e.Production.Add(new ProductionAssignment
                {
                    SourceType = ParseEnum<ProductionSourceType>(p.SourceType, "production source"),
                    AllocationWeight = p.AllocationWeight,
                    WorkerCapacity = p.WorkerCapacity,
                    EfficiencyPercent = p.EfficiencyPercent,
                    Workers = p.Workers,
                });
            }

            foreach (var f in dto.LastDay ?? new List<ResourceFlowDto>())
            {
                if (f == null) throw new SaveDataException($"{label} has a null ledger entry.");
                e.LastDay.Add(new ResourceFlow
                {
                    ResourceTypeId = Resource(f.ResourceTypeId, "ledger resource"),
                    Produced = f.Produced,
                    Wasted = f.Wasted,
                    Demand = f.Demand,
                    Consumed = f.Consumed,
                    Shortfall = f.Shortfall,
                    Imported = f.Imported,
                    Exported = f.Exported,
                });
            }

            foreach (var s in dto.LastDaySources ?? new List<SourceOutputDto>())
            {
                if (s == null) throw new SaveDataException($"{label} has a null source entry.");
                e.LastDaySources.Add(new SourceOutput { SourceType = ParseEnum<ProductionSourceType>(s.SourceType, "source"), Workers = s.Workers, Amount = s.Amount });
            }

            foreach (var s in dto.ResourceStatuses ?? new List<ResourceStatusDto>())
            {
                if (s == null) throw new SaveDataException($"{label} has a null resource status.");
                e.ResourceStatuses.Add(new ResourceStatus { ResourceTypeId = Resource(s.ResourceTypeId, "status resource"), InShortage = s.InShortage, DaysBelowThreshold = s.DaysBelowThreshold });
            }

            var m = dto.Markers ?? new MarkersDto();
            e.Markers = new ChronicleMarkers
            {
                ReportedProsperity = m.ReportedProsperity,
                ReportedStability = m.ReportedStability,
                ReportedPopulation = m.ReportedPopulation,
                CoinMilestone = m.CoinMilestone,
                LastFoodExhaustedReportDay = m.LastFoodExhaustedReportDay,
                LastStarvationReportDay = m.LastStarvationReportDay,
            };
            foreach (var pair in m.ProductionMilestoneIndex ?? new List<ResourceStackDto>())
            {
                if (pair == null) throw new SaveDataException($"{label} has a null milestone entry.");
                e.Markers.ProductionMilestoneIndex[Resource(pair.ResourceTypeId, "milestone resource")] = pair.Quantity;
            }

            return e;
        }

        private static ResourceInventory Inventory(List<ResourceStackDto> stacks, string label)
        {
            var list = new List<ResourceStack>();
            foreach (var r in stacks ?? new List<ResourceStackDto>())
            {
                if (r == null) throw new SaveDataException($"{label} has a null entry.");
                list.Add(new ResourceStack(Resource(r.ResourceTypeId, label), r.Quantity));
            }

            try
            {
                return ResourceInventory.FromStacks(list);
            }
            catch (ArgumentException ex)
            {
                throw new SaveDataException($"{label} are invalid: {ex.Message}");
            }
        }

        private static ResourceTypeId Resource(string key, string field)
        {
            if (!ResourceTypeId.IsValidKey(key)) throw new SaveDataException($"Invalid resource type in {field}.");
            return new ResourceTypeId(key);
        }

        private static Guid RequiredGuid(string text, string field)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new SaveDataException($"Missing {field}.");
            if (!Guid.TryParseExact(text, "D", out var guid)) throw new SaveDataException($"Invalid {field} '{text}'.");
            return guid;
        }

        private static Guid OptionalGuid(string text, string field) =>
            string.IsNullOrEmpty(text) ? Guid.Empty : RequiredGuid(text, field);

        private static WorldDate ParseDate(string text, string field)
        {
            // Strict "YYYY-MM-DD" (year may exceed 4 digits).
            var parts = text?.Split('-');
            if (parts == null || parts.Length != 3 ||
                !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var y) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var m) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var d) ||
                !WorldDate.IsValid(y, m, d))
            {
                throw new SaveDataException($"Invalid {field} '{text}'.");
            }

            return new WorldDate(y, m, d);
        }

        private static T ParseEnum<T>(string text, string field) where T : struct
        {
            if (string.IsNullOrEmpty(text) || char.IsDigit(text[0]) || text[0] == '-' ||
                !Enum.TryParse<T>(text, false, out var value) || !Enum.IsDefined(typeof(T), value))
            {
                throw new SaveDataException($"Invalid {field} '{text}'.");
            }

            return value;
        }
    }
}
