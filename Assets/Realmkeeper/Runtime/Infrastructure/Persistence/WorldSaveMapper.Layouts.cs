using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v12 (Phase 10, ADR-0047): settlement layouts, written exactly and read back exactly (never regenerated).</summary>
    public static partial class WorldSaveMapper
    {
        static partial void WriteSettlementLayouts(WorldData w, WorldDto dto)
        {
            dto.SettlementLayouts = new List<SettlementLayoutDto>();
            foreach (var p in w.SettlementLayouts) dto.SettlementLayouts.Add(LayoutDto(p));
        }

        static partial void ReadSettlementLayouts(WorldDto dto, WorldData world)
        {
            foreach (var d in dto.SettlementLayouts ?? new List<SettlementLayoutDto>()) world.SettlementLayouts.Add(Layout(d));
        }

        private static List<float> Flat(IEnumerable<PlanPoint> points)
        {
            var list = new List<float>();
            foreach (var p in points)
            {
                list.Add(p.X);
                list.Add(p.Z);
            }

            return list;
        }

        private static List<PlanPoint> Points(List<float> flat, string field)
        {
            var list = new List<PlanPoint>();
            if (flat == null) return list;
            if (flat.Count % 2 != 0) throw new SaveDataException($"The {field} has an odd number of coordinates.");
            for (var i = 0; i < flat.Count; i += 2) list.Add(new PlanPoint(flat[i], flat[i + 1]));
            return list;
        }

        private static ulong ParseSeed(string text, string field)
        {
            if (string.IsNullOrEmpty(text)) return 0UL;
            if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var v)) throw new SaveDataException($"The {field} is not a seed.");
            return v;
        }

        private static SettlementLayoutDto LayoutDto(SettlementLayoutData p)
        {
            var i = p.Identity ?? new SettlementIdentityData();
            var t = p.Terrain ?? new TerrainProfileData();
            var d = new SettlementLayoutDto
            {
                SettlementId = p.SettlementId.ToString(), GeneratorVersion = p.GeneratorVersion, Seed = p.Seed.ToString(CultureInfo.InvariantCulture),
                Identity = new SettlementIdentityDto
                {
                    Archetype = i.Archetype.ToString(), PrimaryEconomy = i.PrimaryEconomy, SecondaryEconomy = i.SecondaryEconomy, Water = i.Water.ToString(), Terrain = i.Terrain,
                    Wealth = i.Wealth, Security = i.Security, Agriculture = i.Agriculture, Architecture = i.Architecture, Growth = i.Growth, Summary = i.Summary,
                },
                HalfExtent = p.HalfExtent, PlazaRadius = p.PlazaRadius, CoreRadius = p.CoreRadius, HasWalls = p.HasWalls,
                Terrain = new TerrainProfileDto
                {
                    NoiseSeed = t.NoiseSeed.ToString(CultureInfo.InvariantCulture), NoiseAmplitude = t.NoiseAmplitude, SlopePer100 = t.SlopePer100, SlopeDirection = t.SlopeDirection,
                    LevelRadius = t.LevelRadius, Hills = new List<HillDto>(),
                },
                Waterways = new List<WaterwayDto>(), Districts = new List<DistrictDto>(), Roads = new List<RoadDto>(), Bridges = new List<BridgeDto>(), Buildings = new List<BuildingDto>(),
                Fields = new List<FieldDto>(), Landmarks = new List<LandmarkDto>(), Features = new List<FeatureDto>(), Wall = Flat(p.Wall), Gates = Flat(p.Gates),
                ArrivalX = p.Arrival.X, ArrivalZ = p.Arrival.Z, ArrivalYaw = p.ArrivalYaw,
            };
            foreach (var h in t.Hills) d.Terrain.Hills.Add(new HillDto { X = h.Center.X, Z = h.Center.Z, Radius = h.Radius, Height = h.Height, Plateau = h.Plateau });
            foreach (var w in p.Waterways) d.Waterways.Add(new WaterwayDto { Key = w.Key, IsRiver = w.IsRiver, Width = w.Width, Depth = w.Depth, Line = Flat(w.Line) });
            foreach (var x in p.Districts) d.Districts.Add(new DistrictDto { Key = x.Key, Type = x.Type.ToString(), Name = x.Name, X = x.Center.X, Z = x.Center.Z, Radius = x.Radius });
            foreach (var r in p.Roads)
                d.Roads.Add(new RoadDto { Key = r.Key, Kind = r.Kind.ToString(), Surface = r.Surface.ToString(), Width = r.Width, Toward = r.Toward.IsEmpty ? null : r.Toward.ToString(), Line = Flat(r.Line) });
            foreach (var b in p.Bridges)
                d.Bridges.Add(new BridgeDto
                {
                    Key = b.Key, RoadKey = b.RoadKey, WaterwayKey = b.WaterwayKey, FromX = b.From.X, FromZ = b.From.Z, ToX = b.To.X, ToZ = b.To.Z, Width = b.Width,
                    DeckHeight = b.DeckHeight, IsFord = b.IsFord, Stone = b.Stone,
                });
            foreach (var b in p.Buildings)
                d.Buildings.Add(new BuildingDto
                {
                    Key = b.Key, Function = b.Function.ToString(), DistrictKey = b.DistrictKey, X = b.Center.X, Z = b.Center.Z, Yaw = b.Yaw, Width = b.Width, Depth = b.Depth, Height = b.Height,
                    Storeys = b.Storeys, FloorHeight = b.FloorHeight, Foundation = b.Foundation, Roof = b.Roof.ToString(), Walls = b.Walls.ToString(), Palette = b.Palette,
                    Chimney = b.Chimney, Porch = b.Porch, IsSite = b.IsSite, LocationKey = b.LocationKey,
                });
            foreach (var f in p.Fields)
                d.Fields.Add(new FieldDto
                {
                    Key = f.Key, Kind = f.Kind.ToString(), X = f.Center.X, Z = f.Center.Z, Width = f.Width, Depth = f.Depth, Yaw = f.Yaw, Crop = f.Crop, OwnerKey = f.OwnerKey, Fenced = f.Fenced,
                });
            foreach (var l in p.Landmarks) d.Landmarks.Add(new LandmarkDto { Key = l.Key, Kind = l.Kind, Name = l.Name, FeatureKey = l.FeatureKey, X = l.Position.X, Z = l.Position.Z, Why = l.Why });
            foreach (var f in p.Features) d.Features.Add(new FeatureDto { Kind = f.Kind, X = f.Position.X, Z = f.Position.Z, Yaw = f.Yaw });
            return d;
        }

        private static SettlementLayoutData Layout(SettlementLayoutDto d)
        {
            if (d == null) throw new SaveDataException("A settlement layout is empty.");
            var i = d.Identity ?? throw new SaveDataException("A settlement layout has no identity.");
            var t = d.Terrain ?? throw new SaveDataException("A settlement layout has no terrain.");
            var p = new SettlementLayoutData
            {
                SettlementId = new SettlementId(RequiredGuid(d.SettlementId, "layout settlementId")), GeneratorVersion = d.GeneratorVersion, Seed = ParseSeed(d.Seed, "layout seed"),
                Identity = new SettlementIdentityData
                {
                    Archetype = ParseEnum<LayoutArchetype>(i.Archetype, "layout archetype"), PrimaryEconomy = i.PrimaryEconomy ?? string.Empty, SecondaryEconomy = i.SecondaryEconomy,
                    Water = ParseEnum<WaterAccess>(i.Water, "layout water"), Terrain = i.Terrain ?? string.Empty, Wealth = i.Wealth, Security = i.Security, Agriculture = i.Agriculture,
                    Architecture = i.Architecture ?? string.Empty, Growth = i.Growth ?? string.Empty, Summary = i.Summary ?? string.Empty,
                },
                HalfExtent = d.HalfExtent, PlazaRadius = d.PlazaRadius, CoreRadius = d.CoreRadius, HasWalls = d.HasWalls,
                Terrain = new TerrainProfileData
                {
                    NoiseSeed = ParseSeed(t.NoiseSeed, "terrain noiseSeed"), NoiseAmplitude = t.NoiseAmplitude, SlopePer100 = t.SlopePer100, SlopeDirection = t.SlopeDirection, LevelRadius = t.LevelRadius,
                },
                Wall = Points(d.Wall, "layout wall"), Gates = Points(d.Gates, "layout gates"), Arrival = new PlanPoint(d.ArrivalX, d.ArrivalZ), ArrivalYaw = d.ArrivalYaw,
            };
            foreach (var h in t.Hills ?? new List<HillDto>())
                p.Terrain.Hills.Add(new HillData { Center = new PlanPoint(h.X, h.Z), Radius = h.Radius, Height = h.Height, Plateau = h.Plateau });
            foreach (var w in d.Waterways ?? new List<WaterwayDto>())
                p.Waterways.Add(new WaterwayData { Key = w.Key, IsRiver = w.IsRiver, Width = w.Width, Depth = w.Depth, Line = Points(w.Line, "waterway line") });
            foreach (var x in d.Districts ?? new List<DistrictDto>())
                p.Districts.Add(new DistrictData { Key = x.Key, Type = ParseEnum<DistrictType>(x.Type, "district type"), Name = x.Name ?? string.Empty, Center = new PlanPoint(x.X, x.Z), Radius = x.Radius });
            foreach (var r in d.Roads ?? new List<RoadDto>())
                p.Roads.Add(new RoadData
                {
                    Key = r.Key, Kind = ParseEnum<RoadKind>(r.Kind, "road kind"), Surface = ParseEnum<RoadSurface>(r.Surface, "road surface"), Width = r.Width,
                    Toward = new SettlementId(OptionalGuid(r.Toward, "road toward")), Line = Points(r.Line, "road line"),
                });
            foreach (var b in d.Bridges ?? new List<BridgeDto>())
                p.Bridges.Add(new BridgeData
                {
                    Key = b.Key, RoadKey = b.RoadKey, WaterwayKey = b.WaterwayKey, From = new PlanPoint(b.FromX, b.FromZ), To = new PlanPoint(b.ToX, b.ToZ), Width = b.Width,
                    DeckHeight = b.DeckHeight, IsFord = b.IsFord, Stone = b.Stone,
                });
            foreach (var b in d.Buildings ?? new List<BuildingDto>())
                p.Buildings.Add(new BuildingData
                {
                    Key = b.Key, Function = ParseEnum<BuildingFunction>(b.Function, "building function"), DistrictKey = b.DistrictKey, Center = new PlanPoint(b.X, b.Z), Yaw = b.Yaw,
                    Width = b.Width, Depth = b.Depth, Height = b.Height, Storeys = b.Storeys, FloorHeight = b.FloorHeight, Foundation = b.Foundation,
                    Roof = ParseEnum<RoofShape>(b.Roof, "building roof"), Walls = ParseEnum<WallMaterial>(b.Walls, "building walls"), Palette = b.Palette,
                    Chimney = b.Chimney, Porch = b.Porch, IsSite = b.IsSite, LocationKey = b.LocationKey,
                });
            foreach (var f in d.Fields ?? new List<FieldDto>())
                p.Fields.Add(new FieldData
                {
                    Key = f.Key, Kind = ParseEnum<FieldKind>(f.Kind, "field kind"), Center = new PlanPoint(f.X, f.Z), Width = f.Width, Depth = f.Depth, Yaw = f.Yaw, Crop = f.Crop,
                    OwnerKey = f.OwnerKey, Fenced = f.Fenced,
                });
            foreach (var l in d.Landmarks ?? new List<LandmarkDto>())
                p.Landmarks.Add(new LandmarkData { Key = l.Key, Kind = l.Kind, Name = l.Name ?? string.Empty, FeatureKey = l.FeatureKey, Position = new PlanPoint(l.X, l.Z), Why = l.Why ?? string.Empty });
            foreach (var f in d.Features ?? new List<FeatureDto>()) p.Features.Add(new FeatureData { Kind = f.Kind, Position = new PlanPoint(f.X, f.Z), Yaw = f.Yaw });
            return p;
        }
    }
}
