using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Presentation;
using UnityEngine;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 29: deterministic ground-weather fixtures for tests and captures. Each is a region's weather record, the input
    /// the authoritative rule reads (<see cref="GroundConditions.Of(RegionWeatherData, int)"/>, ADR-0115), and a matching
    /// atmosphere summary: overcast, nothing falling, the fixture's wetness and snow, the live season and daylight, so the
    /// live simulation cannot change a capture's conditions part way and no rain or snow is drawn over the ground being shown.
    /// </summary>
    internal static class GroundWeatherFixtures
    {
        public const int Minute = 600; // 10:00, the captures' hour

        public sealed class Fixture
        {
            public string Name;
            public GroundState State;
            public RegionWeatherData Weather;
            public bool Flooded;
            public GroundConditions Conditions => GroundConditions.Of(Weather, Minute);

            /// <summary>What the ground's materials must carry (ADR-0115's mapping, written out here independently).</summary>
            public (float wet, float snow) Expected
            {
                get
                {
                    var g = Conditions;
                    return (g.Wetness >= 40 ? Mathf.InverseLerp(40f, 90f, g.Wetness) : 0f, g.Snow >= 10 ? Mathf.InverseLerp(10f, 60f, g.Snow) : 0f);
                }
            }
        }

        private static RegionWeatherData Weather(int wetness, int snow, int temperatureC)
        {
            var r = new RegionWeatherData { GroundWetness = wetness, SnowCover = snow };
            for (var i = 0; i < RegionWeatherData.PeriodsPerDay; i++)
                r.Periods.Add(new WeatherPeriodData { Kind = WeatherKind.Overcast, Intensity = 0, TemperatureC = temperatureC, Cloud = 80 });
            return r;
        }

        public static readonly Fixture Dry = new Fixture { Name = "dry", State = GroundState.Dry, Weather = Weather(10, 0, 12) };
        public static readonly Fixture Wet = new Fixture { Name = "wet", State = GroundState.Wet, Weather = Weather(55, 0, 10) };
        public static readonly Fixture Muddy = new Fixture { Name = "muddy", State = GroundState.Muddy, Weather = Weather(78, 0, 10) };
        public static readonly Fixture Flooded = new Fixture { Name = "flooded", State = GroundState.Muddy, Weather = Weather(88, 0, 10), Flooded = true };
        public static readonly Fixture Frozen = new Fixture { Name = "frozen", State = GroundState.Frozen, Weather = Weather(60, 0, -6) };
        public static readonly Fixture Snow = new Fixture { Name = "snow", State = GroundState.Snow, Weather = Weather(20, 35, -3) };
        public static readonly Fixture DeepSnow = new Fixture { Name = "deep snow", State = GroundState.DeepSnow, Weather = Weather(20, 70, -5) };

        public static IReadOnlyList<Fixture> All => new[] { Dry, Wet, Muddy, Flooded, Frozen, Snow, DeepSnow };

        /// <summary>An atmosphere summary for the fixture: the live season and daylight, the fixture's ground, nothing falling.</summary>
        public static WeatherSummary Summary(Fixture f, WeatherSummary live)
        {
            var p = f.Weather.Periods[0];
            return new WeatherSummary
            {
                RegionId = live?.RegionId ?? default, RegionName = live?.RegionName, Season = live?.Season ?? Season.Summer, SeasonName = live?.SeasonName,
                Kind = WeatherKind.Overcast, Name = "overcast", Intensity = 0, TemperatureC = p.TemperatureC, WindKph = 5, Cloud = p.Cloud,
                GroundWetness = f.Weather.GroundWetness, SnowCover = f.Weather.SnowCover, IsSevere = false, Until = live?.Until ?? 0,
                Sunrise = live?.Sunrise ?? 360, Sunset = live?.Sunset ?? 1200, NoonElevation = live?.NoonElevation ?? 50f, Climate = live?.Climate,
            };
        }

        /// <summary>Shows a fixture the way the game does: the conditions applied to the overland, then the atmosphere's pass.</summary>
        public static void Show(ExplorationController c, Fixture f, WeatherSummary live)
        {
            c.Overland.ApplyGroundConditions(f.Conditions);
            c.Atmosphere.SetWeather(Summary(f, live));
        }
    }
}
