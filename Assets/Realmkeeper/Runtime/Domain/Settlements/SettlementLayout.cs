using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.Settlements
{
    /// <summary>The kind of plan a settlement grew into (Phase 10, ADR-0047). Chosen from its identity, never at random.</summary>
    public enum LayoutArchetype
    {
        OrganicVillage = 0,
        Roadside = 1,
        RiverCrossing = 2,
        Hillside = 3,
        PlannedTown = 4,
        Fortified = 5,
        Farmstead = 6,
    }

    public enum WaterAccess
    {
        Well = 0,
        Pond = 1,
        Stream = 2,
        River = 3,
    }

    public enum DistrictType
    {
        Market = 0,
        Residential = 1,
        WealthyResidential = 2,
        WorkingResidential = 3,
        Craft = 4,
        Agricultural = 5,
        Civic = 6,
        Religious = 7,
        Military = 8,
        Waterfront = 9,
        Outskirts = 10,
        Green = 11,
    }

    public enum RoadKind
    {
        Main = 0,
        Secondary = 1,
        Lane = 2,
        MarketStreet = 3,
        FarmTrack = 4,
        Path = 5,
    }

    public enum RoadSurface
    {
        Dirt = 0,
        PackedEarth = 1,
        Gravel = 2,
        Paved = 3,
        Stone = 4,
    }

    /// <summary>What a building is for. Function decides its look and whether it is a site the world uses.</summary>
    public enum BuildingFunction
    {
        Home = 0,
        LargeHome = 1,
        Cottage = 2,
        Farmhouse = 3,
        Tavern = 4,
        Shop = 5,
        Smithy = 6,
        Workshop = 7,
        Warehouse = 8,
        Barn = 9,
        Stable = 10,
        Granary = 11,
        Mill = 12,
        GuardPost = 13,
        Watchtower = 14,
        Civic = 15,
        Religious = 16,
        MarketHall = 17,
        Shed = 18,
        Keep = 19,
        Gatehouse = 20,
    }

    public enum RoofShape
    {
        Gable = 0,
        Hip = 1,
        Flat = 2,
    }

    public enum WallMaterial
    {
        Plaster = 0,
        Timber = 1,
        Stone = 2,
        Brick = 3,
    }

    public enum FieldKind
    {
        Crop = 0,
        Fallow = 1,
        Orchard = 2,
        Pasture = 3,
        Garden = 4,
        Yard = 5,
    }

    /// <summary>A point in a settlement's local plan: metres from the market square's centre, +Z north.</summary>
    public readonly struct PlanPoint : IEquatable<PlanPoint>
    {
        public PlanPoint(float x, float z) { X = x; Z = z; }
        public float X { get; }
        public float Z { get; }
        public float DistanceTo(PlanPoint o) => (float)Math.Sqrt((X - o.X) * (X - o.X) + (Z - o.Z) * (Z - o.Z));
        public bool Equals(PlanPoint o) => X == o.X && Z == o.Z;
        public override bool Equals(object obj) => obj is PlanPoint p && Equals(p);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Z.GetHashCode();
        public override string ToString() => $"({X:0.#}, {Z:0.#})";
    }

    /// <summary>
    /// A settlement's persistent identity (ADR-0047): read from its economy, people, ground and water when the layout
    /// is made, and kept. It explains the plan; the plan is built from it.
    /// </summary>
    public sealed class SettlementIdentityData
    {
        public LayoutArchetype Archetype { get; set; }
        public string PrimaryEconomy { get; set; }
        public string SecondaryEconomy { get; set; }
        public WaterAccess Water { get; set; }

        /// <summary>"flat", "rolling", "hilly".</summary>
        public string Terrain { get; set; }

        /// <summary>0–100 from prosperity and the treasury; decides building size, materials and road quality.</summary>
        public int Wealth { get; set; }

        /// <summary>0–100 from guards, walls and stability.</summary>
        public int Security { get; set; }

        /// <summary>Food workers' share of the work, 0–100: how much of the land is farmed.</summary>
        public int Agriculture { get; set; }

        /// <summary>The character of its buildings ("stone and slate", "timber and thatch" …).</summary>
        public string Architecture { get; set; }

        /// <summary>"founding", "growing", "established", "old".</summary>
        public string Growth { get; set; }

        public string Summary { get; set; }

        public SettlementIdentityData Clone() => (SettlementIdentityData)MemberwiseClone();
    }

    /// <summary>
    /// The ground: a deterministic height function from a few persisted numbers (so the same settlement always has the
    /// same hills, valley and banks). Heights are metres above the square.
    /// </summary>
    public sealed class TerrainProfileData
    {
        public ulong NoiseSeed { get; set; }

        /// <summary>Rolling-ground amplitude in metres.</summary>
        public float NoiseAmplitude { get; set; }

        /// <summary>The regional fall of the land: metres per 100 m, toward this direction (radians).</summary>
        public float SlopePer100 { get; set; }
        public float SlopeDirection { get; set; }

        public List<HillData> Hills { get; set; } = new List<HillData>();

        /// <summary>The square and its approaches are levelled within this radius.</summary>
        public float LevelRadius { get; set; }

        public TerrainProfileData Clone()
        {
            var c = (TerrainProfileData)MemberwiseClone();
            c.Hills = new List<HillData>();
            foreach (var h in Hills) c.Hills.Add(h.Clone());
            return c;
        }
    }

    public sealed class HillData
    {
        public PlanPoint Center { get; set; }
        public float Radius { get; set; }
        public float Height { get; set; }

        /// <summary>A flattened top of this radius (a keep's yard, a green).</summary>
        public float Plateau { get; set; }

        public HillData Clone() => (HillData)MemberwiseClone();
    }

    /// <summary>A river or stream: its centre line, width and depth (its bed is cut into the ground).</summary>
    public sealed class WaterwayData
    {
        public string Key { get; set; }
        public bool IsRiver { get; set; }
        public List<PlanPoint> Line { get; set; } = new List<PlanPoint>();
        public float Width { get; set; }
        public float Depth { get; set; }

        public WaterwayData Clone()
        {
            var c = (WaterwayData)MemberwiseClone();
            c.Line = new List<PlanPoint>(Line);
            return c;
        }
    }

    public sealed class DistrictData
    {
        public string Key { get; set; }
        public DistrictType Type { get; set; }
        public string Name { get; set; }
        public PlanPoint Center { get; set; }
        public float Radius { get; set; }

        public DistrictData Clone() => (DistrictData)MemberwiseClone();
    }

    public sealed class RoadData
    {
        public string Key { get; set; }
        public RoadKind Kind { get; set; }
        public RoadSurface Surface { get; set; }
        public float Width { get; set; }
        public List<PlanPoint> Line { get; set; } = new List<PlanPoint>();

        /// <summary>Where it leaves the settlement toward (a neighbouring settlement), or null.</summary>
        public SettlementId Toward { get; set; }

        public RoadData Clone()
        {
            var c = (RoadData)MemberwiseClone();
            c.Line = new List<PlanPoint>(Line);
            return c;
        }
    }

    /// <summary>A crossing of a waterway: a bridge (a deck at <see cref="DeckHeight"/>) or a ford.</summary>
    public sealed class BridgeData
    {
        public string Key { get; set; }
        public string RoadKey { get; set; }
        public string WaterwayKey { get; set; }
        public PlanPoint From { get; set; }
        public PlanPoint To { get; set; }
        public float Width { get; set; }
        public float DeckHeight { get; set; }
        public bool IsFord { get; set; }
        public bool Stone { get; set; }

        public BridgeData Clone() => (BridgeData)MemberwiseClone();
    }

    public sealed class BuildingData
    {
        /// <summary>Stable key. World sites keep their keys ("hall", "granary", "landmark", "house-3"); others are "b-{n}".</summary>
        public string Key { get; set; }
        public BuildingFunction Function { get; set; }
        public string DistrictKey { get; set; }
        public PlanPoint Center { get; set; }

        /// <summary>Facing in degrees; the door faces (sin yaw, −cos yaw).</summary>
        public float Yaw { get; set; }
        public float Width { get; set; }
        public float Depth { get; set; }

        /// <summary>Eaves height above the floor.</summary>
        public float Height { get; set; }
        public int Storeys { get; set; } = 1;

        /// <summary>The floor's height (the highest ground under it); <see cref="Foundation"/> is the plinth below the floor.</summary>
        public float FloorHeight { get; set; }
        public float Foundation { get; set; }
        public RoofShape Roof { get; set; }
        public WallMaterial Walls { get; set; }

        /// <summary>A palette index for colours (presentation decides them).</summary>
        public int Palette { get; set; }

        public bool Chimney { get; set; }
        public bool Porch { get; set; }

        /// <summary>True for the buildings the world's places live in (they keep their site keys).</summary>
        public bool IsSite { get; set; }

        /// <summary>The world location housed here, by its layout key (same as <see cref="Key"/> for sites), or null.</summary>
        public string LocationKey { get; set; }

        public BuildingData Clone() => (BuildingData)MemberwiseClone();
    }

    /// <summary>Land put to use: a field, fallow, an orchard, a pasture, a garden or a yard, as an oriented rectangle.</summary>
    public sealed class FieldData
    {
        public string Key { get; set; }
        public FieldKind Kind { get; set; }
        public PlanPoint Center { get; set; }
        public float Width { get; set; }
        public float Depth { get; set; }
        public float Yaw { get; set; }

        /// <summary>What grows or grazes there ("wheat", "barley", "beans", "apples", "cattle" …).</summary>
        public string Crop { get; set; }

        /// <summary>The farmhouse or home it belongs to.</summary>
        public string OwnerKey { get; set; }

        public bool Fenced { get; set; }

        public FieldData Clone() => (FieldData)MemberwiseClone();
    }

    public sealed class LandmarkData
    {
        public string Key { get; set; }

        /// <summary>"keep", "watermill", "bridge", "market cross", "great oak", "chapel", "watchtower", "gatehouse" …</summary>
        public string Kind { get; set; }

        public string Name { get; set; }

        /// <summary>The building, bridge or feature that is the landmark (its key), or null for a free-standing one.</summary>
        public string FeatureKey { get; set; }

        public PlanPoint Position { get; set; }
        public string Why { get; set; }

        public LandmarkData Clone() => (LandmarkData)MemberwiseClone();
    }

    /// <summary>Small things that belong to the plan: wells, trees, stalls, troughs (presentation dresses the rest).</summary>
    public sealed class FeatureData
    {
        public string Kind { get; set; }
        public PlanPoint Position { get; set; }
        public float Yaw { get; set; }

        public FeatureData Clone() => (FeatureData)MemberwiseClone();
    }

    /// <summary>
    /// A settlement's spatial plan (Phase 10, ADR-0047 and ADR-0048): generated once from the settlement's identity and
    /// ground, then persisted — it never rearranges itself. Presentation, navigation and the strategic map read this;
    /// none of them invents its own.
    /// </summary>
    public sealed class SettlementLayoutData
    {
        public const int CurrentGeneratorVersion = 2;

        public SettlementId SettlementId { get; set; }
        public int GeneratorVersion { get; set; } = CurrentGeneratorVersion;
        public ulong Seed { get; set; }
        public SettlementIdentityData Identity { get; set; } = new SettlementIdentityData();

        /// <summary>Half the side of the square of land the plan covers, in metres.</summary>
        public float HalfExtent { get; set; }

        /// <summary>The market square (or green) at the origin.</summary>
        public float PlazaRadius { get; set; }

        /// <summary>The built-up core's radius (walls, when there are any, stand on it).</summary>
        public float CoreRadius { get; set; }
        public bool HasWalls { get; set; }

        public TerrainProfileData Terrain { get; set; } = new TerrainProfileData();
        public List<WaterwayData> Waterways { get; set; } = new List<WaterwayData>();
        public List<DistrictData> Districts { get; set; } = new List<DistrictData>();
        public List<RoadData> Roads { get; set; } = new List<RoadData>();
        public List<BridgeData> Bridges { get; set; } = new List<BridgeData>();
        public List<BuildingData> Buildings { get; set; } = new List<BuildingData>();
        public List<FieldData> Fields { get; set; } = new List<FieldData>();
        public List<LandmarkData> Landmarks { get; set; } = new List<LandmarkData>();
        public List<FeatureData> Features { get; set; } = new List<FeatureData>();

        /// <summary>The wall's circuit (closed), when there is a wall; gates are where main roads cross it.</summary>
        public List<PlanPoint> Wall { get; set; } = new List<PlanPoint>();
        public List<PlanPoint> Gates { get; set; } = new List<PlanPoint>();

        /// <summary>Where a traveller arrives (on the first main road at the edge of the plan) and which way they face (degrees).</summary>
        public PlanPoint Arrival { get; set; }
        public float ArrivalYaw { get; set; }

        public SettlementLayoutData Clone()
        {
            var c = (SettlementLayoutData)MemberwiseClone();
            c.Identity = Identity?.Clone();
            c.Terrain = Terrain?.Clone();
            c.Waterways = new List<WaterwayData>();
            foreach (var x in Waterways) c.Waterways.Add(x.Clone());
            c.Districts = new List<DistrictData>();
            foreach (var x in Districts) c.Districts.Add(x.Clone());
            c.Roads = new List<RoadData>();
            foreach (var x in Roads) c.Roads.Add(x.Clone());
            c.Bridges = new List<BridgeData>();
            foreach (var x in Bridges) c.Bridges.Add(x.Clone());
            c.Buildings = new List<BuildingData>();
            foreach (var x in Buildings) c.Buildings.Add(x.Clone());
            c.Fields = new List<FieldData>();
            foreach (var x in Fields) c.Fields.Add(x.Clone());
            c.Landmarks = new List<LandmarkData>();
            foreach (var x in Landmarks) c.Landmarks.Add(x.Clone());
            c.Features = new List<FeatureData>();
            foreach (var x in Features) c.Features.Add(x.Clone());
            c.Wall = new List<PlanPoint>(Wall);
            c.Gates = new List<PlanPoint>(Gates);
            return c;
        }

        public BuildingData FindBuilding(string key)
        {
            foreach (var b in Buildings) if (b.Key == key) return b;
            return null;
        }

        public DistrictData DistrictAt(PlanPoint p)
        {
            DistrictData best = null;
            var bestScore = float.MaxValue;
            foreach (var d in Districts)
            {
                var score = p.DistanceTo(d.Center) / Math.Max(1f, d.Radius);
                if (score < bestScore) { bestScore = score; best = d; }
            }

            return best;
        }

        /// <summary>The ground's height at a point (metres above the square), from the persisted profile and waterways.</summary>
        public float HeightAt(float x, float z) => SettlementTerrain.Height(this, x, z);
    }
}
