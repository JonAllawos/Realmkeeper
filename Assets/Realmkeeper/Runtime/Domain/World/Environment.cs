using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    // Phase 21 (ADR-0105): the world's environment — seasons, regional climate and weather — as simulation state. The
    // presentation only shows it. Everything here is deterministic from the world seed, the date and the saved state.

    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3,
    }

    /// <summary>The weather of a region in one three-hour period. Not every region has every kind (see <see cref="RegionClimate"/>).</summary>
    public enum WeatherKind
    {
        Clear = 0,
        PartlyCloudy = 1,
        Overcast = 2,
        LightRain = 3,
        HeavyRain = 4,
        Thunderstorm = 5,
        LightSnow = 6,
        HeavySnow = 7,
        Fog = 8,
        Windy = 9,
        Heat = 10,
    }

    /// <summary>One three-hour period of a region's weather.</summary>
    public sealed class WeatherPeriodData
    {
        public WeatherKind Kind { get; set; }

        /// <summary>0–100: how hard it rains or snows, how thick the fog, how strong the wind or heat.</summary>
        public int Intensity { get; set; }

        public int TemperatureC { get; set; }
        public int WindKph { get; set; }

        /// <summary>0–100 cloud cover.</summary>
        public int Cloud { get; set; }

        public WeatherPeriodData Clone() => (WeatherPeriodData)MemberwiseClone();
    }

    /// <summary>A region's weather today (eight periods from midnight) and what it carries into tomorrow.</summary>
    public sealed class RegionWeatherData
    {
        public const int PeriodsPerDay = 8;
        public const int MinutesPerPeriod = 180;

        public RegionId RegionId { get; set; }

        /// <summary>The day (<see cref="WorldDate.ToDayNumber"/>) <see cref="Periods"/> describe.</summary>
        public long Day { get; set; }

        public List<WeatherPeriodData> Periods { get; set; } = new List<WeatherPeriodData>();

        /// <summary>0–100 moisture carried from day to day (it makes wet spells last).</summary>
        public int Moisture { get; set; }

        /// <summary>0–100: how wet the ground is (puddles, mud).</summary>
        public int GroundWetness { get; set; }

        /// <summary>0–100: snow lying on the ground.</summary>
        public int SnowCover { get; set; }

        public WeatherPeriodData At(int minuteOfDay)
        {
            if (Periods.Count == 0) return null;
            var i = Math.Max(0, Math.Min(Periods.Count - 1, WorldTime.Normalize(minuteOfDay) / MinutesPerPeriod));
            return Periods[i];
        }

        public RegionWeatherData Clone()
        {
            var c = (RegionWeatherData)MemberwiseClone();
            c.Periods = Periods.ConvertAll(p => p.Clone());
            return c;
        }
    }

    /// <summary>A weather front: a band of moist air drifting across the atlas (west to east, mostly). Neighbouring regions share it.</summary>
    public sealed class WeatherFrontData
    {
        public int Id { get; set; }

        /// <summary>Atlas position, in region units (a region's atlas cell is 1×1).</summary>
        public float X { get; set; }
        public float Z { get; set; }

        /// <summary>Atlas units per day.</summary>
        public float VX { get; set; }
        public float VZ { get; set; }

        /// <summary>0–100.</summary>
        public int Strength { get; set; }

        /// <summary>Atlas units.</summary>
        public float Radius { get; set; }

        /// <summary>A cold front brings colder air (snow in winter) and stronger wind.</summary>
        public bool Cold { get; set; }

        public long BornDay { get; set; }

        public WeatherFrontData Clone() => (WeatherFrontData)MemberwiseClone();
    }

    /// <summary>The world's environment state (schema v23).</summary>
    public sealed class WorldEnvironmentData
    {
        public List<RegionWeatherData> Regions { get; set; } = new List<RegionWeatherData>();
        public List<WeatherFrontData> Fronts { get; set; } = new List<WeatherFrontData>();
        public int NextFrontId { get; set; } = 1;

        /// <summary>The last day the weather was worked out for (−1: never).</summary>
        public long LastDay { get; set; } = -1;

        public RegionWeatherData Find(RegionId id)
        {
            foreach (var r in Regions) if (r.RegionId == id) return r;
            return null;
        }

        public WorldEnvironmentData Clone()
        {
            var c = (WorldEnvironmentData)MemberwiseClone();
            c.Regions = Regions.ConvertAll(r => r.Clone());
            c.Fronts = Fronts.ConvertAll(f => f.Clone());
            return c;
        }
    }

    /// <summary>Seasons and daylight from the existing calendar (twelve months of thirty days; no date changes).</summary>
    public static class SeasonRules
    {
        /// <summary>
        /// Spring: Seedtide, Blossomtide, Greenmonth. Summer: Highsun, Emberdays, Goldfall. Autumn: Reaping, Leaffall,
        /// Frostcall. Winter: Longnight, Deepwinter, Thawmoon.
        /// </summary>
        public static Season Of(WorldDate date)
        {
            switch (date.Month)
            {
                case 3: case 4: case 5: return Season.Spring;
                case 6: case 7: case 8: return Season.Summer;
                case 9: case 10: case 11: return Season.Autumn;
                default: return Season.Winter;
            }
        }

        public static string Name(Season s) => s.ToString();

        public static int DayOfYear(WorldDate date) => (date.Month - 1) * WorldDate.DaysPerMonth + date.Day - 1;

        /// <summary>The longest day falls on 21 Highsun (day 170 of 360), the shortest half a year later.</summary>
        public const int Midsummer = 170;

        /// <summary>−1 at midwinter … +1 at midsummer.</summary>
        public static double SeasonalCurve(WorldDate date) => Math.Cos(2 * Math.PI * (DayOfYear(date) - Midsummer) / WorldDate.DaysPerYear);

        /// <summary>Hours of daylight: 16 at midsummer, 8 at midwinter, 12 at the equinoxes.</summary>
        public static double DaylightHours(WorldDate date) => 12.0 + 4.0 * SeasonalCurve(date);

        /// <summary>Solar noon, minute of the day.</summary>
        public const int SolarNoon = 12 * 60 + 50;

        public static int Sunrise(WorldDate date) => SolarNoon - (int)Math.Round(DaylightHours(date) * 30.0);
        public static int Sunset(WorldDate date) => SolarNoon + (int)Math.Round(DaylightHours(date) * 30.0);

        /// <summary>Sun elevation at noon in degrees (54 at the equinox; 66 at midsummer, 42 at midwinter).</summary>
        public static float NoonElevation(WorldDate date) => (float)(54.0 + 12.0 * SeasonalCurve(date));
    }

    /// <summary>
    /// A region's climate, read from what the world already knows about it: its kind, its place on the atlas (north is
    /// colder) and its mountains. Explainable on purpose: <see cref="Describe"/> says why.
    /// </summary>
    public sealed class RegionClimate
    {
        public RegionKind Kind { get; private set; }

        /// <summary>Mean temperature over the year, °C.</summary>
        public double MeanC { get; private set; }

        /// <summary>Half the difference between the summer and winter means, °C.</summary>
        public double SwingC { get; private set; }

        /// <summary>0–100: how readily it rains.</summary>
        public int Wetness { get; private set; }

        /// <summary>0–100: how often mornings are foggy.</summary>
        public int FogProneness { get; private set; }

        /// <summary>0–100: base wind.</summary>
        public int Windiness { get; private set; }

        /// <summary>Hot, dry summer spells are possible.</summary>
        public bool HasHeat { get; private set; }

        /// <summary>Mountains lie on its western side (weather from the west is lifted: more rain).</summary>
        public bool Windward { get; private set; }

        /// <summary>A highland region lies to its west (it sits in a rain shadow: less rain).</summary>
        public bool RainShadow { get; private set; }

        public static RegionClimate For(RegionData region, IReadOnlyList<RegionData> all)
        {
            var c = new RegionClimate { Kind = region.Kind };
            switch (region.Kind)
            {
                case RegionKind.Highlands: c.MeanC = 6; c.SwingC = 10; c.Wetness = 60; c.FogProneness = 35; c.Windiness = 40; break;
                case RegionKind.Woodland: c.MeanC = 10; c.SwingC = 8; c.Wetness = 60; c.FogProneness = 50; c.Windiness = 15; break;
                case RegionKind.Lakeland: c.MeanC = 10; c.SwingC = 7; c.Wetness = 68; c.FogProneness = 65; c.Windiness = 25; break;
                case RegionKind.Lowlands: c.MeanC = 13; c.SwingC = 11; c.Wetness = 45; c.FogProneness = 30; c.Windiness = 25; c.HasHeat = true; break;
                default: c.MeanC = 11; c.SwingC = 9; c.Wetness = 55; c.FogProneness = 40; c.Windiness = 20; c.HasHeat = true; break; // vale
            }

            // North (atlas Z above the home region) is colder; south warmer.
            c.MeanC -= 1.5 * region.AtlasZ;
            // Mountains on the west side (the generator's directions 3, 4 and 5: north-west, west, south-west; 0 is east and
            // 2 north) catch the westerlies.
            c.Windward = region.MountainDirection >= 3 && region.MountainDirection <= 5;
            if (c.Windward) c.Wetness += 8;
            if (all != null)
                foreach (var r in all)
                    if (r.Kind == RegionKind.Highlands && r.AtlasX == region.AtlasX - 1 && r.AtlasZ == region.AtlasZ && region.Kind != RegionKind.Highlands)
                        c.RainShadow = true;
            if (c.RainShadow) c.Wetness -= 12;
            c.Wetness = Math.Max(15, Math.Min(85, c.Wetness));
            return c;
        }

        /// <summary>The day's mean temperature from the season alone, °C.</summary>
        public double SeasonalMeanC(WorldDate date) => MeanC + SwingC * SeasonalCurve(date);

        private static double SeasonalCurve(WorldDate date) => SeasonRules.SeasonalCurve(date);

        /// <summary>Whether a weather kind can happen in this climate at all.</summary>
        public bool Allows(WeatherKind k) => k != WeatherKind.Heat || HasHeat;

        public string Describe()
        {
            var parts = new List<string>();
            parts.Add(Kind == RegionKind.Highlands ? "high, cold and windy" : Kind == RegionKind.Lakeland ? "mild, damp and often misty" : Kind == RegionKind.Woodland ? "sheltered and damp" : Kind == RegionKind.Lowlands ? "open, with warm summers" : "temperate");
            parts.Add($"about {Math.Round(MeanC + SwingC)} °C in summer, {Math.Round(MeanC - SwingC)} °C in winter");
            if (Windward) parts.Add("its western mountains catch the rain");
            if (RainShadow) parts.Add("drier, in the shadow of the highlands to the west");
            return string.Join("; ", parts) + ".";
        }
    }

    /// <summary>Words for weather and the season.</summary>
    public static class WeatherWords
    {
        public static string Name(WeatherKind k)
        {
            switch (k)
            {
                case WeatherKind.Clear: return "Clear";
                case WeatherKind.PartlyCloudy: return "Partly cloudy";
                case WeatherKind.Overcast: return "Overcast";
                case WeatherKind.LightRain: return "Light rain";
                case WeatherKind.HeavyRain: return "Heavy rain";
                case WeatherKind.Thunderstorm: return "Thunderstorm";
                case WeatherKind.LightSnow: return "Light snow";
                case WeatherKind.HeavySnow: return "Heavy snow";
                case WeatherKind.Fog: return "Fog";
                case WeatherKind.Windy: return "Windy";
                case WeatherKind.Heat: return "Hot and dry";
                default: return k.ToString();
            }
        }

        public static bool IsRain(WeatherKind k) => k == WeatherKind.LightRain || k == WeatherKind.HeavyRain || k == WeatherKind.Thunderstorm;
        public static bool IsSnow(WeatherKind k) => k == WeatherKind.LightSnow || k == WeatherKind.HeavySnow;

        /// <summary>Severe enough that people leave outdoor work and stay in.</summary>
        public static bool IsSevere(WeatherKind k) => k == WeatherKind.HeavyRain || k == WeatherKind.Thunderstorm || k == WeatherKind.HeavySnow;
    }
}
