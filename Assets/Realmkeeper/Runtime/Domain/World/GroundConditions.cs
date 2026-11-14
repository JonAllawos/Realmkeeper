using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>Phase 24: the state of the ground underfoot, from the region's weather.</summary>
    public enum GroundState
    {
        Dry = 0,
        Wet = 1,
        Muddy = 2,
        Snow = 3,
        DeepSnow = 4,
        Frozen = 5,
    }

    /// <summary>
    /// Phase 24 (ADR-0115): how the region's weather has left the ground, and what that does to walking, crossings and
    /// sight. A pure rule over the existing, saved weather (<see cref="RegionWeatherData"/>: ground wetness, snow cover, the
    /// current period): nothing new is simulated or saved, and the same weather always gives the same ground.
    /// <list type="bullet">
    /// <item>Wet ground (wetness 40+) and mud (70+) slow walking off the road; roads barely.</item>
    /// <item>Snow (cover 20+) slows all ground, roads less; deep snow (60+) more.</item>
    /// <item>Hard frost on wet ground freezes it: fords become slippery, not closed.</item>
    /// <item>Fords flood when the ground is sodden (85+), or wet (70+) while it rains hard (60+): they cannot be waded until
    ///   the water drops; bridges still cross.</item>
    /// <item>Fog, heavy rain and snow shorten the distance at which things are noticed.</item>
    /// </list>
    /// </summary>
    public sealed class GroundConditions
    {
        public GroundState State { get; private set; }
        public int Wetness { get; private set; }
        public int Snow { get; private set; }
        public bool FordsFlooded { get; private set; }

        /// <summary>Walking-time multiplier on roads, and off them.</summary>
        public float RoadFactor { get; private set; } = 1f;

        public float OffRoadFactor { get; private set; } = 1f;

        /// <summary>Extra multiplier at a ford (ice, high water short of flooding).</summary>
        public float FordFactor { get; private set; } = 1f;

        /// <summary>Multiplier on the distance at which things are noticed (1 = clear day).</summary>
        public float SightFactor { get; private set; } = 1f;

        /// <summary>A short line for the HUD (empty on good dry ground in clear air).</summary>
        public string Line { get; private set; } = string.Empty;

        /// <summary>Calm, dry ground (a realm or region with no weather record).</summary>
        public static GroundConditions Calm => new GroundConditions();

        /// <summary>The ground of a region at a minute of the day; calm when the region has no weather.</summary>
        public static GroundConditions Of(WorldData world, RegionId region, int minuteOfDay)
        {
            var r = world?.Environment?.Find(region);
            return r == null ? Calm : Of(r, minuteOfDay);
        }

        public static GroundConditions Of(RegionWeatherData r, int minuteOfDay)
        {
            var g = new GroundConditions();
            if (r == null) return g;
            var period = r.Periods != null && r.Periods.Count > 0 ? r.At(minuteOfDay) : null;
            g.Wetness = r.GroundWetness;
            g.Snow = r.SnowCover;
            var raining = period != null && WeatherWords.IsRain(period.Kind);
            var hardRain = raining && period.Intensity >= 60;
            if (g.Snow >= 60) { g.State = GroundState.DeepSnow; g.RoadFactor = 1.3f; g.OffRoadFactor = 2.0f; g.FordFactor = 1.3f; }
            else if (g.Snow >= 20) { g.State = GroundState.Snow; g.RoadFactor = 1.1f; g.OffRoadFactor = 1.5f; g.FordFactor = 1.2f; }
            else if (period != null && period.TemperatureC <= -2 && g.Wetness >= 40) { g.State = GroundState.Frozen; g.RoadFactor = 1.05f; g.OffRoadFactor = 1.1f; g.FordFactor = 1.5f; }
            else if (g.Wetness >= 70) { g.State = GroundState.Muddy; g.RoadFactor = 1.1f; g.OffRoadFactor = 1.4f; g.FordFactor = 1.3f; }
            else if (g.Wetness >= 40) { g.State = GroundState.Wet; g.OffRoadFactor = 1.15f; g.FordFactor = 1.1f; }

            g.FordsFlooded = g.State != GroundState.Frozen && (g.Wetness >= 85 || (g.Wetness >= 70 && hardRain));
            if (period != null)
            {
                if (period.Kind == WeatherKind.Fog) g.SightFactor = period.Intensity >= 60 ? 0.4f : 0.6f;
                else if (WeatherWords.IsSevere(period.Kind)) g.SightFactor = 0.6f;
                else if (raining || WeatherWords.IsSnow(period.Kind)) g.SightFactor = 0.8f;
            }

            g.Line = Describe(g);
            return g;
        }

        /// <summary>The walking-time multiplier for a cell under these conditions.</summary>
        public float FactorFor(OverlandCell cell)
        {
            if (cell.Road) return RoadFactor;
            if (cell.Crossing) return OffRoadFactor * FordFactor; // a ford (a road crossing is a bridge: Road)
            return OffRoadFactor;
        }

        /// <summary>Whether a cell is a ford (waded, off the road).</summary>
        public static bool IsFord(OverlandCell cell) => cell.Crossing && !cell.Road;

        private static string Describe(GroundConditions g)
        {
            string ground;
            switch (g.State)
            {
                case GroundState.Wet: ground = "Wet ground: off the road is slower"; break;
                case GroundState.Muddy: ground = "Mud: off the road is much slower"; break;
                case GroundState.Snow: ground = "Snow: slower going, roads less so"; break;
                case GroundState.DeepSnow: ground = "Deep snow: hard going off the road"; break;
                case GroundState.Frozen: ground = "Frozen ground: fords are icy"; break;
                default: ground = string.Empty; break;
            }

            var parts = new System.Collections.Generic.List<string>();
            if (ground.Length > 0) parts.Add(ground);
            if (g.FordsFlooded) parts.Add("fords in flood: cross by a bridge");
            if (g.SightFactor < 0.7f) parts.Add("poor visibility");
            return string.Join(" · ", parts);
        }
    }
}
