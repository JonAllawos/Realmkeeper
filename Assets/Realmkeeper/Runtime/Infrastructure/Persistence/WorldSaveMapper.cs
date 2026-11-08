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
                Settlements = new List<SettlementDto>(),
                Factions = new List<FactionDto>(),
                Npcs = new List<NpcDto>(),
                HistoricalEvents = new List<HistoricalEventDto>(),
            };

            foreach (var s in w.Settlements)
            {
                var sd = new SettlementDto
                {
                    SettlementId = s.SettlementId.ToString(),
                    Name = s.Name,
                    SettlementType = s.SettlementType.ToString(),
                    PositionX = s.Position.X,
                    PositionZ = s.Position.Z,
                    Population = s.Population,
                    Prosperity = s.Prosperity,
                    Stability = s.Stability,
                    FactionId = s.FactionId.IsEmpty ? null : s.FactionId.ToString(),
                    FoodProductionPerDay = s.FoodProductionPerDay,
                    IsInFoodShortage = s.IsInFoodShortage,
                    Resources = new List<ResourceStackDto>(),
                };
                foreach (var r in s.ResourceInventory.Stacks)
                    sd.Resources.Add(new ResourceStackDto { ResourceTypeId = r.ResourceTypeId.Value, Quantity = r.Quantity });
                dto.Settlements.Add(sd);
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
                });
            }

            foreach (var e in w.HistoricalEvents)
            {
                dto.HistoricalEvents.Add(new HistoricalEventDto
                {
                    EventId = e.EventId.ToString(),
                    Date = e.Date.ToIsoString(),
                    EventType = e.EventType.ToString(),
                    Severity = e.Severity.ToString(),
                    SettlementId = e.SettlementId.IsEmpty ? null : e.SettlementId.ToString(),
                    Description = e.Description,
                });
            }

            return dto;
        }

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

            foreach (var sd in dto.Settlements ?? new List<SettlementDto>())
            {
                if (sd == null) throw new SaveDataException("Null settlement entry.");
                var s = new SettlementData
                {
                    SettlementId = new SettlementId(RequiredGuid(sd.SettlementId, "settlementId")),
                    Name = sd.Name,
                    SettlementType = ParseEnum<SettlementType>(sd.SettlementType, "settlementType"),
                    Position = new MapCoord(sd.PositionX, sd.PositionZ),
                    Population = sd.Population,
                    Prosperity = sd.Prosperity,
                    Stability = sd.Stability,
                    FactionId = new FactionId(OptionalGuid(sd.FactionId, "factionId")),
                    FoodProductionPerDay = sd.FoodProductionPerDay,
                    IsInFoodShortage = sd.IsInFoodShortage,
                };

                var stacks = new List<ResourceStack>();
                foreach (var r in sd.Resources ?? new List<ResourceStackDto>())
                {
                    if (r == null || !ResourceTypeId.IsValidKey(r.ResourceTypeId))
                        throw new SaveDataException($"Settlement '{sd.Name}' has an invalid resource type.");
                    stacks.Add(new ResourceStack(new ResourceTypeId(r.ResourceTypeId), r.Quantity));
                }

                try
                {
                    s.ResourceInventory = ResourceInventory.FromStacks(stacks);
                }
                catch (ArgumentException ex)
                {
                    throw new SaveDataException($"Settlement '{sd.Name}' has an invalid inventory: {ex.Message}");
                }

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
                });
            }

            foreach (var ed in dto.HistoricalEvents ?? new List<HistoricalEventDto>())
            {
                if (ed == null) throw new SaveDataException("Null event entry.");
                world.HistoricalEvents.Add(new HistoricalEvent(
                    new EventId(RequiredGuid(ed.EventId, "eventId")),
                    ParseDate(ed.Date, "event date"),
                    ParseEnum<HistoricalEventType>(ed.EventType, "eventType"),
                    ParseEnum<EventSeverity>(ed.Severity, "severity"),
                    new SettlementId(OptionalGuid(ed.SettlementId, "settlementId")),
                    ed.Description));
            }

            return world;
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
