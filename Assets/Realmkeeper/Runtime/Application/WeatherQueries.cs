using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Climate;
using Realmkeeper.Domain.Economy;

namespace Realmkeeper.Application
{
    /// <summary>Phase 21: what the weather and season are where someone stands, worded for the interface.</summary>
    public sealed class WeatherSummary
    {
        public RegionId RegionId { get; set; }
        public string RegionName { get; set; }
        public Season Season { get; set; }
        public string SeasonName { get; set; }
        public WeatherKind Kind { get; set; }
        public string Name { get; set; }
        public int Intensity { get; set; }
        public int TemperatureC { get; set; }
        public int WindKph { get; set; }
        public int Cloud { get; set; }
        public int GroundWetness { get; set; }
        public int SnowCover { get; set; }
        public bool IsSevere { get; set; }

        /// <summary>The minute of the day this period's weather lasts until.</summary>
        public int Until { get; set; }

        /// <summary>The rest of today, period by period (from the current one), for a short outlook.</summary>
        public List<string> Outlook { get; set; } = new List<string>();

        /// <summary>Sunrise and sunset today (minutes of the day).</summary>
        public int Sunrise { get; set; }
        public int Sunset { get; set; }

        /// <summary>The sun's height at noon today, degrees.</summary>
        public float NoonElevation { get; set; }

        public string Climate { get; set; }

        /// <summary>What it changes now, in words (travel, work in the open, farming this season).</summary>
        public List<string> Effects { get; set; } = new List<string>();

        public string Line => $"{Name}, {TemperatureC} °C · {SeasonName}";
    }

    public sealed partial class WorldSession
    {
        /// <summary>The weather in the player's region now, or in <paramref name="region"/>; null when no weather exists yet.</summary>
        public WeatherSummary GetWeather(RegionId region = default)
        {
            if (!HasWorld) return null;
            var w = _world;
            if (region.IsEmpty) region = w.Player?.RegionId ?? RegionId.Empty;
            if (region.IsEmpty && w.Regions.Count > 0) region = w.Regions[0].RegionId;
            var data = w.FindRegion(region);
            var state = w.Environment?.Find(region);
            var now = state?.At(w.MinuteOfDay);
            if (data == null || now == null) return null;
            var date = w.CurrentSimulationDate;
            var season = SeasonRules.Of(date);
            var s = new WeatherSummary
            {
                RegionId = region, RegionName = data.Name, Season = season, SeasonName = SeasonRules.Name(season),
                Kind = now.Kind, Name = WeatherWords.Name(now.Kind), Intensity = now.Intensity, TemperatureC = now.TemperatureC, WindKph = now.WindKph, Cloud = now.Cloud,
                GroundWetness = state.GroundWetness, SnowCover = state.SnowCover, IsSevere = WeatherWords.IsSevere(now.Kind),
                Until = (WorldTime.Normalize(w.MinuteOfDay) / RegionWeatherData.MinutesPerPeriod + 1) * RegionWeatherData.MinutesPerPeriod,
                Sunrise = SeasonRules.Sunrise(date), Sunset = SeasonRules.Sunset(date), NoonElevation = SeasonRules.NoonElevation(date),
                Climate = RegionClimate.For(data, w.Regions).Describe(),
            };
            for (var i = WorldTime.Normalize(w.MinuteOfDay) / RegionWeatherData.MinutesPerPeriod; i < state.Periods.Count; i++)
            {
                var p = state.Periods[i];
                s.Outlook.Add($"{i * 3:00}:00  {WeatherWords.Name(p.Kind)}, {p.TemperatureC} °C");
            }

            var f = TravelRules.WeatherFactor(null, now, state.SnowCover, out var why);
            if (why != null) s.Effects.Add($"Travel is slower ({why}): about {100 - (int)System.Math.Round(f * 100)} % less ground an hour.");
            if (s.IsSevere) s.Effects.Add("Work in the open has stopped; people keep indoors.");
            var farm = SeasonalProduction.Percent(date, ProductionSourceType.Farming);
            s.Effects.Add(farm == 100 ? "Farms yield as usual this season." : $"Farms yield {farm} % of their usual this season ({(farm > 100 ? "harvest" : season == Season.Winter ? "winter" : "growing")}).");
            return s;
        }
    }
}
