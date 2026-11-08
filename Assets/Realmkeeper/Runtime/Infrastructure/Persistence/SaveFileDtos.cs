using System.Collections.Generic;

namespace Realmkeeper.Infrastructure.Persistence
{
    // Save-file data transfer objects (schema version 1).
    // Deliberately separate from domain types: the on-disk layout can evolve through
    // migrations without changing domain classes, and domain classes carry no
    // serialization attributes. Enums, IDs and dates are stored as strings so files stay
    // readable and robust to enum reordering.

    public sealed class WorldSaveFileDto
    {
        public string Format { get; set; }
        public int SchemaVersion { get; set; }
        public string SavedAtUtc { get; set; }
        public WorldDto World { get; set; }
    }

    public sealed class WorldDto
    {
        public string WorldId { get; set; }
        public string WorldName { get; set; }
        public long Seed { get; set; }
        public int GeneratorVersion { get; set; }
        public string CurrentSimulationDate { get; set; }
        public WorldSettingsDto Settings { get; set; }
        public List<SettlementDto> Settlements { get; set; }
        public List<FactionDto> Factions { get; set; }
        public List<NpcDto> Npcs { get; set; }
        public List<HistoricalEventDto> HistoricalEvents { get; set; }
    }

    public sealed class WorldSettingsDto
    {
        public string SimulationSpeed { get; set; }
        public string StartingDate { get; set; }
        public List<string> EnabledSimulationSystems { get; set; }
    }

    public sealed class SettlementDto
    {
        public string SettlementId { get; set; }
        public string Name { get; set; }
        public string SettlementType { get; set; }
        public int PositionX { get; set; }
        public int PositionZ { get; set; }
        public int Population { get; set; }
        public int Prosperity { get; set; }
        public int Stability { get; set; }
        public string FactionId { get; set; }
        public int FoodProductionPerDay { get; set; }
        public bool IsInFoodShortage { get; set; }
        public List<ResourceStackDto> Resources { get; set; }
    }

    public sealed class ResourceStackDto
    {
        public string ResourceTypeId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class FactionDto
    {
        public string FactionId { get; set; }
        public string Name { get; set; }
    }

    public sealed class NpcDto
    {
        public string NpcId { get; set; }
        public string Name { get; set; }
        public string HomeSettlementId { get; set; }
        public string FactionId { get; set; }
    }

    public sealed class HistoricalEventDto
    {
        public string EventId { get; set; }
        public string Date { get; set; }
        public string EventType { get; set; }
        public string Severity { get; set; }
        public string SettlementId { get; set; }
        public string Description { get; set; }
    }
}
