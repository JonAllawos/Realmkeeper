using System.Collections.Generic;

namespace Realmkeeper.Infrastructure.Persistence
{
    // Schema v12 (Phase 10, ADR-0047): a settlement's persisted plan. Lines and point lists are flat [x, z, x, z, …]
    // pairs in plan metres; enums are written by name; the seed is a string (JSON numbers lose 64-bit precision).

    public sealed class SettlementLayoutDto
    {
        public string SettlementId { get; set; }
        public int GeneratorVersion { get; set; }
        public string Seed { get; set; }
        public SettlementIdentityDto Identity { get; set; }
        public float HalfExtent { get; set; }
        public float PlazaRadius { get; set; }
        public float CoreRadius { get; set; }
        public bool HasWalls { get; set; }
        public TerrainProfileDto Terrain { get; set; }
        public List<WaterwayDto> Waterways { get; set; }
        public List<DistrictDto> Districts { get; set; }
        public List<RoadDto> Roads { get; set; }
        public List<BridgeDto> Bridges { get; set; }
        public List<BuildingDto> Buildings { get; set; }
        public List<FieldDto> Fields { get; set; }
        public List<LandmarkDto> Landmarks { get; set; }
        public List<FeatureDto> Features { get; set; }
        public List<float> Wall { get; set; }
        public List<float> Gates { get; set; }
        public float ArrivalX { get; set; }
        public float ArrivalZ { get; set; }
        public float ArrivalYaw { get; set; }
    }

    public sealed class SettlementIdentityDto
    {
        public string Archetype { get; set; }
        public string PrimaryEconomy { get; set; }
        public string SecondaryEconomy { get; set; }
        public string Water { get; set; }
        public string Terrain { get; set; }
        public int Wealth { get; set; }
        public int Security { get; set; }
        public int Agriculture { get; set; }
        public string Architecture { get; set; }
        public string Growth { get; set; }
        public string Summary { get; set; }
    }

    public sealed class TerrainProfileDto
    {
        public string NoiseSeed { get; set; }
        public float NoiseAmplitude { get; set; }
        public float SlopePer100 { get; set; }
        public float SlopeDirection { get; set; }
        public float LevelRadius { get; set; }
        public List<HillDto> Hills { get; set; }
    }

    public sealed class HillDto
    {
        public float X { get; set; }
        public float Z { get; set; }
        public float Radius { get; set; }
        public float Height { get; set; }
        public float Plateau { get; set; }
    }

    public sealed class WaterwayDto
    {
        public string Key { get; set; }
        public bool IsRiver { get; set; }
        public float Width { get; set; }
        public float Depth { get; set; }
        public List<float> Line { get; set; }
    }

    public sealed class DistrictDto
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public float Radius { get; set; }
    }

    public sealed class RoadDto
    {
        public string Key { get; set; }
        public string Kind { get; set; }
        public string Surface { get; set; }
        public float Width { get; set; }
        public string Toward { get; set; }
        public List<float> Line { get; set; }
    }

    public sealed class BridgeDto
    {
        public string Key { get; set; }
        public string RoadKey { get; set; }
        public string WaterwayKey { get; set; }
        public float FromX { get; set; }
        public float FromZ { get; set; }
        public float ToX { get; set; }
        public float ToZ { get; set; }
        public float Width { get; set; }
        public float DeckHeight { get; set; }
        public bool IsFord { get; set; }
        public bool Stone { get; set; }
    }

    public sealed class BuildingDto
    {
        public string Key { get; set; }
        public string Function { get; set; }
        public string DistrictKey { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public float Yaw { get; set; }
        public float Width { get; set; }
        public float Depth { get; set; }
        public float Height { get; set; }
        public int Storeys { get; set; }
        public float FloorHeight { get; set; }
        public float Foundation { get; set; }
        public string Roof { get; set; }
        public string Walls { get; set; }
        public int Palette { get; set; }
        public bool Chimney { get; set; }
        public bool Porch { get; set; }
        public bool IsSite { get; set; }
        public string LocationKey { get; set; }
    }

    public sealed class FieldDto
    {
        public string Key { get; set; }
        public string Kind { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public float Width { get; set; }
        public float Depth { get; set; }
        public float Yaw { get; set; }
        public string Crop { get; set; }
        public string OwnerKey { get; set; }
        public bool Fenced { get; set; }
    }

    public sealed class LandmarkDto
    {
        public string Key { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string FeatureKey { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public string Why { get; set; }
    }

    public sealed class FeatureDto
    {
        public string Kind { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public float Yaw { get; set; }
    }
}
