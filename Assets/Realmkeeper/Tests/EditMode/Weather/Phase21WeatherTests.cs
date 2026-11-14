using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Climate;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Tests.EditMode.Atlas;

namespace Realmkeeper.Tests.EditMode.Weather
{
    /// <summary>Phase 21 (ADR-0105): seasons, regional climate and weather in the six-region world.</summary>
    public sealed class Phase21WeatherTests : Phase16Fixture
    {
        private static string Fingerprint(WorldEnvironmentData e) =>
            string.Join("|", e.Regions.Select(r => r.RegionId + ":" + r.Day + ":" + r.Moisture + ":" + r.GroundWetness + ":" + r.SnowCover + ":" +
                                                   string.Join(",", r.Periods.Select(p => $"{(int)p.Kind}/{p.Intensity}/{p.TemperatureC}/{p.WindKph}/{p.Cloud}"))))
            + "#" + string.Join("|", e.Fronts.Select(f => $"{f.Id}:{f.X:R}:{f.Z:R}:{f.Strength}:{f.Cold}")) + "#" + e.NextFrontId + ":" + e.LastDay;

        /// <summary>A year of weather from a copy of the fixture's world, without the rest of the simulation (the model alone).</summary>
        private static List<(WorldDate date, RegionData region, RegionWeatherData day)> Year(WorldData world)
        {
            var list = new List<(WorldDate, RegionData, RegionWeatherData)>();
            var start = world.CurrentSimulationDate.ToDayNumber();
            for (var d = start + 1; d <= start + WorldDate.DaysPerYear; d++)
            {
                WeatherModel.AdvanceTo(world, d);
                foreach (var r in world.Regions) list.Add((WorldDate.FromDayNumber(d), r, world.Environment.Find(r.RegionId).Clone()));
            }

            return list;
        }

        [Test]
        public void A_new_world_has_weather_today_for_every_region()
        {
            var w = W;
            Assert.That(w.Environment, Is.Not.Null);
            Assert.That(w.Environment.LastDay, Is.EqualTo(w.CurrentSimulationDate.ToDayNumber()));
            Assert.That(w.Environment.Regions.Count, Is.EqualTo(w.Regions.Count));
            foreach (var r in w.Environment.Regions) Assert.That(r.Periods.Count, Is.EqualTo(RegionWeatherData.PeriodsPerDay));
            Assert.That(S.GetWeather(), Is.Not.Null);
            Assert.That(S.GetWeather().Line, Does.Contain("°C"));
            Assert.That(S.GetWeather().Effects, Is.Not.Empty);
        }

        [Test]
        public void The_same_seed_and_days_give_the_same_weather()
        {
            var a = W;
            var b = W;
            var target = a.CurrentSimulationDate.ToDayNumber() + 90;
            WeatherModel.AdvanceTo(a, target);
            WeatherModel.AdvanceTo(b, target);
            Assert.That(Fingerprint(a.Environment), Is.EqualTo(Fingerprint(b.Environment)));
        }

        [Test]
        public void The_daily_system_runs_the_weather_and_it_survives_a_save_exactly()
        {
            var before = W.Environment.LastDay;
            Ok(S.Execute(new AdvanceDaysCommand(5)));
            Assert.That(W.Environment.LastDay, Is.EqualTo(before + 5));
            Assert.That(WorldJson(), Does.Contain("environment"));

            var saved = Fingerprint(W.Environment);
            Edit(_ => { });
            Assert.That(Fingerprint(W.Environment), Is.EqualTo(saved), "a save and a load keep the weather exactly");

            // Continuing after the load gives what continuing without it gives.
            var straight = W;
            WeatherModel.AdvanceTo(straight, straight.Environment.LastDay + 10);
            Ok(S.Execute(new AdvanceDaysCommand(10)));
            Assert.That(Fingerprint(W.Environment), Is.EqualTo(Fingerprint(straight.Environment)));
        }

        [Test]
        public void An_older_save_without_weather_gets_it_on_load()
        {
            Edit(w => w.Environment = null);
            Assert.That(W.Environment, Is.Not.Null);
            Assert.That(W.Environment.Regions.All(r => r.Periods.Count == RegionWeatherData.PeriodsPerDay), Is.True);
        }

        [Test]
        public void A_v22_save_gains_the_weather_system_and_its_weather()
        {
            var doc = Newtonsoft.Json.Linq.JObject.Parse(new Realmkeeper.Infrastructure.Persistence.JsonWorldSerializer().Serialize(W));
            doc["schemaVersion"] = 22;
            var world = (Newtonsoft.Json.Linq.JObject)doc["world"];
            world.Remove("environment");
            var systems = (Newtonsoft.Json.Linq.JArray)world["settings"]["enabledSimulationSystems"];
            systems.First(t => (string)t == WeatherSystem.Id).Remove();
            Assert.That(systems.Any(t => (string)t == WeatherSystem.Id), Is.False);

            var result = new Realmkeeper.Infrastructure.Persistence.JsonWorldSerializer().Deserialize(doc.ToString());
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Errors ?? new string[0]));
            Assert.That(result.World.WorldSettings.EnabledSimulationSystems[0], Is.EqualTo(WeatherSystem.Id), "weather runs first, before labour");
            Assert.That(result.World.Environment, Is.Null, "no weather is invented in the file; the session works it out on load");
        }

        [Test]
        public void A_year_of_weather_follows_the_seasons_and_the_climates()
        {
            var w = W;
            var year = Year(w);
            foreach (var (date, region, day) in year)
            {
                var climate = RegionClimate.For(region, w.Regions);
                foreach (var p in day.Periods)
                {
                    Assert.That(p.Intensity, Is.InRange(0, 100));
                    Assert.That(p.Cloud, Is.InRange(0, 100));
                    if (WeatherWords.IsSnow(p.Kind)) Assert.That(p.TemperatureC, Is.LessThanOrEqualTo(1), $"snow at {p.TemperatureC} °C");
                    if (p.Kind == WeatherKind.Heat)
                    {
                        Assert.That(climate.HasHeat, Is.True, $"heat in a {region.Kind} region");
                        Assert.That(SeasonRules.Of(date), Is.EqualTo(Season.Summer));
                    }

                    if (p.Kind == WeatherKind.Thunderstorm) Assert.That(SeasonRules.Of(date), Is.EqualTo(Season.Summer).Or.EqualTo(Season.Spring));
                }
            }

            foreach (var region in w.Regions)
            {
                var mine = year.Where(x => x.region.RegionId == region.RegionId).ToList();
                double Mean(Season s) => mine.Where(x => SeasonRules.Of(x.date) == s).SelectMany(x => x.day.Periods).Average(p => p.TemperatureC);
                Assert.That(Mean(Season.Summer), Is.GreaterThan(Mean(Season.Winter) + 8), $"{region.Name}: summer warmer than winter");
                var wetDays = mine.Count(x => x.day.Periods.Any(p => WeatherWords.IsRain(p.Kind) || WeatherWords.IsSnow(p.Kind)));
                const int fewest = 30;
                Assert.That(wetDays, Is.InRange(fewest, 300), $"{region.Name}: {wetDays} wet days in a year");
                var dryDays = mine.Count(x => x.day.Periods.All(p => !WeatherWords.IsRain(p.Kind) && !WeatherWords.IsSnow(p.Kind)));
                Assert.That(dryDays, Is.GreaterThan(30), $"{region.Name}: {dryDays} dry days in a year");
            }

            // Every kind a temperate world should show, somewhere, in a year.
            var kinds = new HashSet<WeatherKind>(year.SelectMany(x => x.day.Periods).Select(p => p.Kind));
            foreach (var k in new[] { WeatherKind.Clear, WeatherKind.PartlyCloudy, WeatherKind.Overcast, WeatherKind.LightRain, WeatherKind.HeavyRain, WeatherKind.Fog })
                Assert.That(kinds, Does.Contain(k), $"no {k} in a year");
            TestContext.WriteLine("Kinds seen in a year: " + string.Join(", ", kinds.OrderBy(k => k)));
        }

        [Test]
        public void Weather_changes_over_hours_not_every_period()
        {
            var year = Year(W);
            int changes = 0, periods = 0;
            foreach (var g in year.GroupBy(x => x.region.RegionId))
            {
                WeatherKind? last = null;
                foreach (var p in g.SelectMany(x => x.day.Periods))
                {
                    if (last != null && p.Kind != last) changes++;
                    last = p.Kind;
                    periods++;
                }
            }

            Assert.That(changes / (double)periods, Is.LessThan(0.6), "the weather holds for a while");
        }

        [Test]
        public void Seasonal_production_averages_a_normal_year()
        {
            var jan1 = new WorldDate(412, 1, 1).ToDayNumber();
            foreach (ProductionSourceType source in Enum.GetValues(typeof(ProductionSourceType)))
            {
                var total = 0;
                for (var d = 0; d < WorldDate.DaysPerYear; d++) total += SeasonalProduction.Percent(WorldDate.FromDayNumber(jan1 + d), source);
                Assert.That(total / (double)WorldDate.DaysPerYear, Is.EqualTo(100).Within(0.6), $"{source}");
            }

            Assert.That(SeasonalProduction.Percent(new WorldDate(412, 10, 1), ProductionSourceType.Farming),
                Is.GreaterThan(SeasonalProduction.Percent(new WorldDate(412, 1, 1), ProductionSourceType.Farming)));
        }

        [Test]
        public void Seasons_and_daylight_follow_the_calendar()
        {
            Assert.That(SeasonRules.Of(new WorldDate(412, 3, 1)), Is.EqualTo(Season.Spring));
            Assert.That(SeasonRules.Of(new WorldDate(412, 7, 1)), Is.EqualTo(Season.Summer));
            Assert.That(SeasonRules.Of(new WorldDate(412, 10, 1)), Is.EqualTo(Season.Autumn));
            Assert.That(SeasonRules.Of(new WorldDate(412, 12, 30)), Is.EqualTo(Season.Winter));
            Assert.That(SeasonRules.Of(new WorldDate(412, 2, 1)), Is.EqualTo(Season.Winter));
            var summer = new WorldDate(412, 6, 21);
            var winter = new WorldDate(412, 12, 21);
            Assert.That(SeasonRules.Sunset(summer) - SeasonRules.Sunrise(summer), Is.EqualTo(16 * 60).Within(5));
            Assert.That(SeasonRules.Sunset(winter) - SeasonRules.Sunrise(winter), Is.EqualTo(8 * 60).Within(5));
        }

        [Test]
        public void Climate_reads_the_regions_kind_latitude_and_mountains()
        {
            var w = W;
            foreach (var r in w.Regions)
            {
                var c = RegionClimate.For(r, w.Regions);
                Assert.That(c.Wetness, Is.InRange(15, 85));
                Assert.That(c.Describe(), Is.Not.Empty);
            }

            var high = new RegionData { Kind = RegionKind.Highlands, AtlasX = 0, AtlasZ = 0, MountainDirection = 0 };
            var low = new RegionData { Kind = RegionKind.Lowlands, AtlasX = 1, AtlasZ = 0, MountainDirection = 0 };
            var all = new[] { high, low };
            Assert.That(RegionClimate.For(high, all).MeanC, Is.LessThan(RegionClimate.For(low, all).MeanC));
            Assert.That(RegionClimate.For(low, all).RainShadow, Is.True, "a highland region to the west casts a rain shadow");
            var north = new RegionData { Kind = RegionKind.Vale, AtlasZ = 1 };
            var south = new RegionData { Kind = RegionKind.Vale, AtlasZ = 0 };
            Assert.That(RegionClimate.For(north, null).MeanC, Is.LessThan(RegionClimate.For(south, null).MeanC));
            var windward = new RegionData { Kind = RegionKind.Vale, MountainDirection = 4 };
            Assert.That(RegionClimate.For(windward, null).Windward, Is.True);
        }

        [Test]
        public void Bad_weather_slows_travel_and_says_why()
        {
            var pass = new RouteSegmentData { Kind = RouteKind.Pass };
            var road = new RouteSegmentData { Kind = RouteKind.Road };
            Assert.That(TravelRules.WeatherFactor(road, new WeatherPeriodData { Kind = WeatherKind.Clear }, 0, out var why), Is.EqualTo(1.0));
            Assert.That(why, Is.Null);
            Assert.That(TravelRules.WeatherFactor(road, new WeatherPeriodData { Kind = WeatherKind.HeavyRain }, 0, out why), Is.LessThan(1.0));
            Assert.That(why, Is.EqualTo("heavy rain"));
            var snowRoad = TravelRules.WeatherFactor(road, new WeatherPeriodData { Kind = WeatherKind.HeavySnow }, 60, out _);
            var snowPass = TravelRules.WeatherFactor(pass, new WeatherPeriodData { Kind = WeatherKind.HeavySnow }, 60, out why);
            Assert.That(snowPass, Is.LessThan(snowRoad));
            Assert.That(why, Does.Contain("pass"));
        }

        [Test]
        public void People_working_in_the_open_shelter_in_a_storm()
        {
            var w = W;
            // Generated people work in shops and workshops; for this check one of them works a market stall (the plan is a pure
            // function of the world, so the copy can say so).
            var npc = w.Npcs.First(n => !n.WorkplaceId.IsEmpty && !n.ResidenceId.IsEmpty && n.Role != NpcRole.Guard && n.Role != NpcRole.Innkeeper && !n.IsDeceased &&
                                        !w.Shops.Exists(s => s.ShopkeeperNpcId == n.NpcId));
            w.FindLocation(npc.WorkplaceId).Kind = LocationKind.Market;
            Assert.That(NpcActivityPlan.IsOpenAir(w, npc.WorkplaceId), Is.True);
            npc.CurrentLocationId = npc.WorkplaceId; // today is a work day
            npc.Goals.Clear();
            var (start, end) = NpcActivityPlan.WorkHours(w, npc);
            var minute = start + 60 < 12 * 60 ? start + 60 : end - 45;
            w.MinuteOfDay = minute;
            var town = w.FindSettlement(npc.HomeSettlementId);
            var state = w.Environment.Find(town.RegionId);
            foreach (var p in state.Periods) { p.Kind = WeatherKind.Clear; p.Intensity = 0; }
            var calm = NpcActivityPlan.At(w, npc, w.CurrentSimulationDate, minute);
            Assert.That(calm.Location, Is.EqualTo(npc.WorkplaceId), calm.Reason);

            foreach (var p in state.Periods) { p.Kind = WeatherKind.Thunderstorm; p.Intensity = 80; }
            var storm = NpcActivityPlan.At(w, npc, w.CurrentSimulationDate, minute);
            Assert.That(storm.Location, Is.EqualTo(npc.ResidenceId));
            Assert.That(storm.Reason, Does.Contain("Sheltering"));
            Assert.That(storm.Until, Is.LessThanOrEqualTo(end));
        }
    }
}
