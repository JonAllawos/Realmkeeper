using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Randomness;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 21 (ADR-0105): the weather, worked out one day at a time for every region. Deterministic: the same seed, date
    /// and saved state give the same weather.
    ///
    /// <para><b>How it works, so it can be explained.</b></para>
    /// <list type="number">
    /// <item><b>Fronts.</b> Bands of moist air are born at the atlas's western edge (more often in autumn and winter) and
    /// drift east about a region a day, weakening as they go. Regions under the same front share its weather a day apart,
    /// so rain does not stop at a border.</item>
    /// <item><b>Moisture.</b> A region's moisture follows its climate and the fronts over it. It carries from day to day
    /// (wet spells last) and is blended a little with its neighbours'. Mountains on the west side add rain; a rain shadow
    /// takes some away.</item>
    /// <item><b>Temperature.</b> The day's mean follows the season and the region's climate; a cold front lowers it. Each
    /// period adds the daily rhythm (cool nights, warm afternoons).</item>
    /// <item><b>Each period.</b> A slowly varying value per region, continuous across midnight, decides rain or snow
    /// against the moisture, so weather changes over hours, not every hour. Below about 1 °C it snows. Heavy summer rain
    /// under a strong front can become a thunderstorm. Still, damp, cool mornings are foggy. A hot, dry summer afternoon
    /// in a warm region is "hot and dry".</item>
    /// <item><b>The ground.</b> Rain wets it and sun dries it; snow lies below freezing and melts above.</item>
    /// </list>
    /// </summary>
    public static class WeatherModel
    {
        /// <summary>At most this many days are simulated to catch up (a long absence): earlier days could not be seen anyway.</summary>
        public const int MaxCatchUpDays = 30;

        /// <summary>Works the weather out up to and including <paramref name="day"/> (a <see cref="WorldDate.ToDayNumber"/>).</summary>
        public static void AdvanceTo(WorldData world, long day)
        {
            if (world == null || world.Regions.Count == 0) return;
            var env = world.Environment ?? (world.Environment = new WorldEnvironmentData());
            EnsureRegions(world, env);
            // A region added after the day was worked out (the lands beyond discovered mid-day) gets that day now, under
            // the fronts as they stand, without moving them again.
            if (env.LastDay >= 0)
                foreach (var r in world.Regions)
                {
                    var state = env.Find(r.RegionId);
                    if (state.Day != env.LastDay) FillDay(world, state, r, env.LastDay, ColdOver(env, r));
                }

            if (env.LastDay >= day) return;
            var from = env.LastDay < 0 ? day - 3 : Math.Max(env.LastDay + 1, day - MaxCatchUpDays + 1); // a new world: three days of history
            for (var d = from; d <= day; d++) StepDay(world, env, d);
        }

        /// <summary>The weather now in a region (null before any weather exists).</summary>
        public static WeatherPeriodData Now(WorldData world, Identifiers.RegionId region)
        {
            var r = world?.Environment?.Find(region);
            return r?.At(world.MinuteOfDay);
        }

        private static void EnsureRegions(WorldData world, WorldEnvironmentData env)
        {
            foreach (var r in world.Regions)
                if (env.Find(r.RegionId) == null)
                {
                    var climate = RegionClimate.For(r, world.Regions);
                    env.Regions.Add(new RegionWeatherData { RegionId = r.RegionId, Day = -1, Moisture = climate.Wetness / 2 });
                }
        }

        private static ulong Seed(WorldData world, string purpose, long day, string subject = "") =>
            SeedMixer.Combine(SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "weather:" + purpose + ":" + subject), unchecked((ulong)day));

        private static double Unit(ulong seed) => (SeedMixer.Mix(seed) >> 11) * (1.0 / (1UL << 53));

        /// <summary>
        /// A smooth value in [0,1] for a region and an absolute period index: linear between random knots every three
        /// periods (nine hours), so it never jumps, and continuous across days.
        /// </summary>
        private static double Smooth(WorldData world, string region, long period)
        {
            const int step = 3;
            var k0 = (long)Math.Floor(period / (double)step);
            var t = (period - k0 * step) / (double)step;
            var a = Unit(Seed(world, "knot", k0, region));
            var b = Unit(Seed(world, "knot", k0 + 1, region));
            t = t * t * (3 - 2 * t);
            return a + (b - a) * t;
        }

        private static void StepDay(WorldData world, WorldEnvironmentData env, long day)
        {
            var date = WorldDate.FromDayNumber(day);
            var season = SeasonRules.Of(date);
            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            foreach (var r in world.Regions)
            {
                minX = Math.Min(minX, r.AtlasX); maxX = Math.Max(maxX, r.AtlasX);
                minZ = Math.Min(minZ, r.AtlasZ); maxZ = Math.Max(maxZ, r.AtlasZ);
            }

            // 1. Fronts drift east and weaken; those gone past the atlas or spent are dropped.
            foreach (var f in env.Fronts)
            {
                f.X += f.VX;
                f.Z += f.VZ;
                f.Strength = Math.Max(0, f.Strength - 6);
            }

            env.Fronts.RemoveAll(f => f.Strength < 8 || f.X > maxX + 2.5f || f.Z < minZ - 3f || f.Z > maxZ + 3f);

            // 2. New fronts at the western edge; autumn and winter bring more.
            var chance = season == Season.Autumn ? 0.55 : season == Season.Winter ? 0.5 : season == Season.Spring ? 0.42 : 0.3;
            var roll = Seed(world, "front", day);
            if (Unit(roll) < chance && env.Fronts.Count < 4)
            {
                var cold = Unit(SeedMixer.Combine(roll, 1)) < (season == Season.Winter ? 0.6 : season == Season.Autumn ? 0.4 : 0.2);
                env.Fronts.Add(new WeatherFrontData
                {
                    Id = env.NextFrontId++,
                    X = minX - 1.5f,
                    Z = (float)(minZ - 0.5 + Unit(SeedMixer.Combine(roll, 2)) * (maxZ - minZ + 1)),
                    VX = (float)(0.7 + Unit(SeedMixer.Combine(roll, 3)) * 0.6),
                    VZ = (float)((Unit(SeedMixer.Combine(roll, 4)) - 0.5) * 0.4),
                    Strength = 45 + (int)(Unit(SeedMixer.Combine(roll, 5)) * 50),
                    Radius = (float)(1.1 + Unit(SeedMixer.Combine(roll, 6)) * 1.0),
                    Cold = cold,
                    BornDay = day,
                });
            }

            // 3. Each region's moisture and the day's mean temperature.
            var raw = new Dictionary<Identifiers.RegionId, double>();
            var coldBy = new Dictionary<Identifiers.RegionId, double>();
            foreach (var r in world.Regions)
            {
                var state = env.Find(r.RegionId);
                var climate = RegionClimate.For(r, world.Regions);
                double influence = 0, cold = 0;
                foreach (var f in env.Fronts)
                {
                    var dx = r.AtlasX - f.X;
                    var dz = r.AtlasZ - f.Z;
                    var dist = Math.Sqrt(dx * dx + dz * dz);
                    var falloff = Math.Max(0, 1 - dist / f.Radius);
                    influence += f.Strength * falloff;
                    if (f.Cold) cold = Math.Max(cold, falloff);
                }

                influence = Math.Min(100, influence);
                if (climate.Windward && influence > 0) influence = Math.Min(100, influence + 10);
                var target = climate.Wetness * 0.55 + influence * 0.75 - 18;
                var noise = (Unit(Seed(world, "moist", day, r.RegionId.ToString())) - 0.5) * 16;
                raw[r.RegionId] = state.Moisture * 0.45 + target * 0.55 + noise;
                coldBy[r.RegionId] = cold;
            }

            foreach (var r in world.Regions)
            {
                var state = env.Find(r.RegionId);
                double sum = raw[r.RegionId] * 3, weight = 3;
                foreach (var n in r.Neighbours)
                    if (raw.TryGetValue(n, out var v)) { sum += v; weight += 1; }
                state.Moisture = Clamp((int)Math.Round(sum / weight), 0, 100);
            }

            // 4. The eight periods of the day, and the ground.
            foreach (var r in world.Regions) FillDay(world, env.Find(r.RegionId), r, day, coldBy[r.RegionId]);
            env.LastDay = day;
        }

        /// <summary>How much of a cold front lies over a region (0–1).</summary>
        private static double ColdOver(WorldEnvironmentData env, RegionData r)
        {
            double cold = 0;
            foreach (var f in env.Fronts)
            {
                if (!f.Cold) continue;
                var dx = r.AtlasX - f.X;
                var dz = r.AtlasZ - f.Z;
                cold = Math.Max(cold, Math.Max(0, 1 - Math.Sqrt(dx * dx + dz * dz) / f.Radius));
            }

            return cold;
        }

        /// <summary>A region's eight periods for a day from its moisture, and the ground's response.</summary>
        private static void FillDay(WorldData world, RegionWeatherData state, RegionData r, long day, double coldFront)
        {
            var date = WorldDate.FromDayNumber(day);
            var season = SeasonRules.Of(date);
            {
                var climate = RegionClimate.For(r, world.Regions);
                var key = r.RegionId.ToString();
                var coldBy = coldFront;
                var mean = climate.SeasonalMeanC(date) - 4.5 * coldBy + (Unit(Seed(world, "temp", day, key)) - 0.5) * 4;
                state.Day = day;
                state.Periods = new List<WeatherPeriodData>(RegionWeatherData.PeriodsPerDay);
                var threshold = 52 - climate.Wetness * 0.12; // the moisture at which it starts to rain
                for (var i = 0; i < RegionWeatherData.PeriodsPerDay; i++)
                {
                    var period = day * RegionWeatherData.PeriodsPerDay + i;
                    var hour = i * 3 + 1.5;
                    var temp = mean + 4.5 * Math.Cos(2 * Math.PI * (hour - 15) / 24);                           // warmest at 15:00
                    var s = Smooth(world, key, period);
                    var p = state.Moisture * (0.55 + 0.75 * s);                                                  // the period's wetness
                    var wind = climate.Windiness + 30 * coldBy + 20 * Smooth(world, key + ":wind", period) + Math.Max(0, p - threshold) * 0.3;
                    var w = new WeatherPeriodData { TemperatureC = (int)Math.Round(temp), WindKph = Clamp((int)Math.Round(wind), 0, 90) };
                    if (p > threshold)
                    {
                        var intensity = Clamp((int)Math.Round((p - threshold) * 2.4), 10, 100);
                        var snow = temp <= 1.0;
                        if (snow) w.Kind = intensity >= 60 ? WeatherKind.HeavySnow : WeatherKind.LightSnow;
                        else if (intensity >= 65 && temp >= 14 && (season == Season.Summer || season == Season.Spring) && Smooth(world, key + ":storm", period) > 0.55) w.Kind = WeatherKind.Thunderstorm;
                        else w.Kind = intensity >= 60 ? WeatherKind.HeavyRain : WeatherKind.LightRain;
                        w.Intensity = intensity;
                        w.Cloud = Clamp(70 + intensity / 3, 0, 100);
                    }
                    else
                    {
                        var cloud = Clamp((int)Math.Round(p / threshold * 85), 0, 100);
                        w.Cloud = cloud;
                        var morning = i <= 2; // 00:00–09:00
                        var fogChance = climate.FogProneness / 100.0 * (state.GroundWetness >= 30 || climate.Kind == RegionKind.Lakeland ? 1.0 : 0.55);
                        if (morning && w.WindKph < 18 && temp < 14 && Smooth(world, key + ":fog", period) < fogChance * 0.8)
                        {
                            w.Kind = WeatherKind.Fog;
                            w.Intensity = Clamp((int)(40 + fogChance * 50), 20, 95);
                        }
                        else if (climate.HasHeat && season == Season.Summer && temp >= 27 && cloud < 30)
                        {
                            w.Kind = WeatherKind.Heat;
                            w.Intensity = Clamp((int)((temp - 24) * 10), 20, 100);
                        }
                        else if (w.WindKph >= 40)
                        {
                            w.Kind = WeatherKind.Windy;
                            w.Intensity = Clamp(w.WindKph, 0, 100);
                        }
                        else w.Kind = cloud >= 70 ? WeatherKind.Overcast : cloud >= 35 ? WeatherKind.PartlyCloudy : WeatherKind.Clear;
                    }

                    state.Periods.Add(w);
                }

                // 5. The ground: rain wets it, sun and warmth dry it; snow lies when it is cold and melts when it is not.
                foreach (var w in state.Periods)
                {
                    if (WeatherWords.IsRain(w.Kind)) state.GroundWetness += w.Intensity / 6;
                    else state.GroundWetness -= w.Kind == WeatherKind.Heat ? 8 : w.Cloud < 50 ? 4 : 2;
                    if (WeatherWords.IsSnow(w.Kind)) state.SnowCover += w.Intensity / 5;
                    else if (w.TemperatureC > 2) state.SnowCover -= Math.Max(1, (w.TemperatureC - 2) / 2) * (WeatherWords.IsRain(w.Kind) ? 2 : 1);
                    state.GroundWetness = Clamp(state.GroundWetness, 0, 100);
                    state.SnowCover = Clamp(state.SnowCover, 0, 100);
                }
            }
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
