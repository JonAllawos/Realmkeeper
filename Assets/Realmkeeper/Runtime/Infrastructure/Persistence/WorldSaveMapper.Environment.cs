using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v23 (Phase 21, ADR-0105): the world's weather — each region's day, the fronts crossing the atlas.</summary>
    public sealed class WorldEnvironmentDto
    {
        public long LastDay { get; set; } = -1;
        public int NextFrontId { get; set; } = 1;
        public List<RegionWeatherDto> Regions { get; set; }
        public List<WeatherFrontDto> Fronts { get; set; }
    }

    public sealed class RegionWeatherDto
    {
        public string RegionId { get; set; }
        public long Day { get; set; }
        public int Moisture { get; set; }
        public int GroundWetness { get; set; }
        public int SnowCover { get; set; }
        public List<WeatherPeriodDto> Periods { get; set; }
    }

    public sealed class WeatherPeriodDto
    {
        public string Kind { get; set; }
        public int Intensity { get; set; }
        public int TemperatureC { get; set; }
        public int WindKph { get; set; }
        public int Cloud { get; set; }
    }

    /// <summary>Positions are stored as doubles so the float values round-trip exactly.</summary>
    public sealed class WeatherFrontDto
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Z { get; set; }
        public double VX { get; set; }
        public double VZ { get; set; }
        public int Strength { get; set; }
        public double Radius { get; set; }
        public bool Cold { get; set; }
        public long BornDay { get; set; }
    }

    public static partial class WorldSaveMapper
    {
        private static void WriteEnvironment(WorldData w, WorldDto dto)
        {
            var e = w.Environment;
            if (e == null) { dto.Environment = null; return; }
            var d = new WorldEnvironmentDto { LastDay = e.LastDay, NextFrontId = e.NextFrontId, Regions = new List<RegionWeatherDto>(), Fronts = new List<WeatherFrontDto>() };
            foreach (var r in e.Regions)
            {
                var rd = new RegionWeatherDto { RegionId = r.RegionId.ToString(), Day = r.Day, Moisture = r.Moisture, GroundWetness = r.GroundWetness, SnowCover = r.SnowCover, Periods = new List<WeatherPeriodDto>() };
                foreach (var p in r.Periods) rd.Periods.Add(new WeatherPeriodDto { Kind = p.Kind.ToString(), Intensity = p.Intensity, TemperatureC = p.TemperatureC, WindKph = p.WindKph, Cloud = p.Cloud });
                d.Regions.Add(rd);
            }

            foreach (var f in e.Fronts)
                d.Fronts.Add(new WeatherFrontDto { Id = f.Id, X = f.X, Z = f.Z, VX = f.VX, VZ = f.VZ, Strength = f.Strength, Radius = f.Radius, Cold = f.Cold, BornDay = f.BornDay });
            dto.Environment = d;
        }

        /// <summary>A save without weather (older than v23, or never simulated) leaves it null; the weather system fills it on the next day.</summary>
        private static void ReadEnvironment(WorldDto dto, WorldData world)
        {
            var d = dto.Environment;
            if (d == null) { world.Environment = null; return; }
            var e = new WorldEnvironmentData { LastDay = d.LastDay, NextFrontId = d.NextFrontId };
            var seen = new HashSet<RegionId>();
            foreach (var rd in d.Regions ?? new List<RegionWeatherDto>())
            {
                if (rd == null) throw new SaveDataException("A region's weather record is empty.");
                var id = new RegionId(RequiredGuid(rd.RegionId, "weather region"));
                if (!seen.Add(id)) throw new SaveDataException($"Region {id} has two weather records.");
                var r = new RegionWeatherData { RegionId = id, Day = rd.Day, Moisture = rd.Moisture, GroundWetness = rd.GroundWetness, SnowCover = rd.SnowCover };
                foreach (var p in rd.Periods ?? new List<WeatherPeriodDto>())
                {
                    if (p == null) throw new SaveDataException("A weather period is empty.");
                    r.Periods.Add(new WeatherPeriodData { Kind = ParseEnum<WeatherKind>(p.Kind, "weather kind"), Intensity = p.Intensity, TemperatureC = p.TemperatureC, WindKph = p.WindKph, Cloud = p.Cloud });
                }

                e.Regions.Add(r);
            }

            foreach (var f in d.Fronts ?? new List<WeatherFrontDto>())
            {
                if (f == null) throw new SaveDataException("A weather front record is empty.");
                e.Fronts.Add(new WeatherFrontData { Id = f.Id, X = (float)f.X, Z = (float)f.Z, VX = (float)f.VX, VZ = (float)f.VZ, Strength = f.Strength, Radius = (float)f.Radius, Cold = f.Cold, BornDay = f.BornDay });
            }

            world.Environment = e;
        }
    }
}

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v23 (Phase 21, ADR-0106): one line of a shop's supply record.</summary>
    public sealed class SupplyRecordDto
    {
        public long Day { get; set; }
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public string Origin { get; set; }
        public string Detail { get; set; }
    }
}
