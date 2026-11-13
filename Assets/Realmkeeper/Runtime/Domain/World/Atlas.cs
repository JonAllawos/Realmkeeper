using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;

namespace Realmkeeper.Domain.World
{
    /// <summary>Phase 16: what kind of way a route segment is.</summary>
    public enum RouteKind
    {
        Road = 0,
        Trail = 1,
        Pass = 2,
        Bridge = 3,
        Ford = 4,
    }

    /// <summary>Phase 16: the state of a route segment's surface.</summary>
    public enum RouteCondition
    {
        Good = 0,
        Worn = 1,
        Blocked = 2,
    }

    /// <summary>
    /// Phase 16 (ADR-0077): one persistent segment of the route network, between two settlements. Intra-region
    /// segments follow the region's roads (their geometry is regenerated with the region; this record keeps what
    /// changes: condition, bridge, blockade). A crossing joins two regions over their shared border. The travel planner,
    /// caravans, events and the map all read this one record.
    /// </summary>
    public sealed class RouteSegmentData
    {
        public string RouteId { get; set; }
        public SettlementId A { get; set; }
        public SettlementId B { get; set; }
        public RegionId RegionA { get; set; }
        public RegionId RegionB { get; set; }
        public RouteKind Kind { get; set; }

        /// <summary>Length in map cells (<see cref="TravelRules.KmPerCell"/> each).</summary>
        public int Cells { get; set; }

        /// <summary>Terrain cost in percent of a road on open ground (100); hills, forest and passes cost more.</summary>
        public int TerrainPercent { get; set; } = 100;

        public RouteCondition Condition { get; set; }

        /// <summary>Whether the segment crosses water on a bridge, and its state.</summary>
        public bool HasBridge { get; set; }
        public BridgeState Bridge { get; set; }

        /// <summary>The world event holding it blocked or broken (empty when none).</summary>
        public string BlockedBy { get; set; }

        /// <summary>Days of repair work done on a destroyed bridge or a blocked way.</summary>
        public int RepairDays { get; set; }

        public bool IsCrossing => RegionA != RegionB;

        public bool Passable => Condition != RouteCondition.Blocked && (!HasBridge || Bridge != BridgeState.Destroyed);

        public SettlementId Other(SettlementId s) => s == A ? B : A;
        public bool Joins(SettlementId s) => s == A || s == B;

        public static string IdFor(SettlementId a, SettlementId b) => a.CompareTo(b) <= 0 ? "route:" + a + "|" + b : "route:" + b + "|" + a;

        public RouteSegmentData Clone() => (RouteSegmentData)MemberwiseClone();
    }

    /// <summary>Phase 16: how the player travels. On foot only for now (the extension point for mounts and boats).</summary>
    public enum TravelMode
    {
        OnFoot = 0,
    }

    public enum JourneyStatus
    {
        Travelling = 0,

        /// <summary>Stopped by the player (or night); resumes on request.</summary>
        Halted = 1,

        /// <summary>Stopped by something on the road (a fight, a broken bridge); resumes when it is dealt with.</summary>
        Interrupted = 2,
        Arrived = 3,
        Abandoned = 4,
    }

    /// <summary>
    /// Phase 16 (ADR-0078): the player's journey. The party travels with the player (no second party model): active
    /// members who follow are carried by the player's position. Progress is in metres along the current segment;
    /// the clock is the world's one clock.
    /// </summary>
    public sealed class JourneyData
    {
        public string JourneyId { get; set; }
        public SettlementId OriginId { get; set; }
        public SettlementId DestinationId { get; set; }

        /// <summary>Settlements along the way, origin first, destination last.</summary>
        public List<SettlementId> Nodes { get; set; } = new List<SettlementId>();

        /// <summary>Route segment IDs, one fewer than nodes.</summary>
        public List<string> Segments { get; set; } = new List<string>();

        public int SegmentIndex { get; set; }
        public int MetresIntoSegment { get; set; }
        public TravelMode Mode { get; set; }
        public JourneyStatus Status { get; set; }
        public long StartedAt { get; set; }

        /// <summary>Whole hours travelled (the stable index of each hour's road check).</summary>
        public int HoursTravelled { get; set; }

        /// <summary>Why it stopped (words), and the fight that stopped it, if any.</summary>
        public string Reason { get; set; }
        public string EncounterId { get; set; }

        /// <summary>Bands that have let this journey pass (paid, slipped past or fought): they do not bar it again.</summary>
        public List<string> Cleared { get; set; } = new List<string>();

        /// <summary>Turned back on a road that closed under them: the first segment is walked back the way they came (the closure lies ahead of where they stood).</summary>
        public bool Back { get; set; }

        public JourneyData Clone()
        {
            var j = (JourneyData)MemberwiseClone();
            j.Nodes = new List<SettlementId>(Nodes);
            j.Segments = new List<string>(Segments);
            j.Cleared = new List<string>(Cleared);
            return j;
        }
    }

    public enum TravelGroupKind
    {
        Caravan = 0,
        MerchantParty = 1,
        Patrol = 2,
        FactionPatrol = 3,
        Pilgrims = 4,
        Refugees = 5,
        Bandits = 6,
    }

    public enum TravelGroupState
    {
        Travelling = 0,

        /// <summary>Held on the road (a blocked way, a broken bridge).</summary>
        Halted = 1,

        /// <summary>Holding a stretch of road (patrols, bandits).</summary>
        Stationed = 2,
        Arrived = 3,
        Defeated = 4,
        Dispersed = 5,
    }

    /// <summary>
    /// Phase 16 (ADR-0079): a group on the roads. Its leader is one of the world's own people; its members are a
    /// number (low-detail). A caravan carries a shipment of the existing economy (its goods are the shipment's);
    /// bandits are an existing road band (their numbers are the band's). Position comes from the route and the time.
    /// </summary>
    public sealed class TravelingGroupData
    {
        public const int MaxHistory = 12;

        public string GroupId { get; set; }
        public TravelGroupKind Kind { get; set; }
        public string Name { get; set; }
        public FactionId FactionId { get; set; }
        public NpcId LeaderNpcId { get; set; }
        public int Members { get; set; }

        /// <summary>Fighting strength: guards for a caravan, soldiers for a patrol, outlaws for bandits.</summary>
        public int Strength { get; set; }

        public SettlementId OriginId { get; set; }
        public SettlementId DestinationId { get; set; }
        public List<SettlementId> Nodes { get; set; } = new List<SettlementId>();
        public List<string> Segments { get; set; } = new List<string>();

        /// <summary>Absolute minutes it set out and is due.</summary>
        public long DepartedAt { get; set; }
        public long DueAt { get; set; }

        public TravelGroupState State { get; set; }

        /// <summary>A caravan's shipment (the goods it carries).</summary>
        public ShipmentId ShipmentId { get; set; }

        /// <summary>Bandits: the road band they are. Patrols and bandits: the segment they hold.</summary>
        public string PopulationId { get; set; }
        public string StationRouteId { get; set; }

        /// <summary>The world event it belongs to, if any.</summary>
        public string EventId { get; set; }

        public List<string> History { get; set; } = new List<string>();

        public void Note(string line)
        {
            History.Add(line);
            while (History.Count > MaxHistory) History.RemoveAt(0);
        }

        public bool IsActive => State == TravelGroupState.Travelling || State == TravelGroupState.Halted || State == TravelGroupState.Stationed;

        public TravelingGroupData Clone()
        {
            var g = (TravelingGroupData)MemberwiseClone();
            g.Nodes = new List<SettlementId>(Nodes);
            g.Segments = new List<string>(Segments);
            g.History = new List<string>(History);
            return g;
        }
    }

    public enum WorldEventType
    {
        FoodShortage = 0,
        TradeDisruption = 1,
        CaravanLost = 2,
        BanditActivity = 3,
        PatrolResponse = 4,
        BridgeDamaged = 5,
        RoadBlocked = 6,
        FactionConflict = 7,
        RefugeeMovement = 8,
        LocalUnrest = 9,
    }

    public enum WorldEventState
    {
        Active = 0,
        Escalating = 1,
        Resolved = 2,
        Failed = 3,
        Expired = 4,
    }

    /// <summary>What the player knows of a world event (separate from the event itself: rumours never change the world).</summary>
    public enum EventKnowledge
    {
        Unknown = 0,
        Rumored = 1,
        Confirmed = 2,
    }

    /// <summary>
    /// Phase 16 (ADR-0080): a persistent world event. Typed, with a cause, a scope (settlement, route, region, factions,
    /// a group), validated state changes, consequences applied once each (keys in <see cref="Applied"/>), a history,
    /// and the player's own knowledge of it (what they heard, from whom, and when: it can be out of date).
    /// </summary>
    public sealed class WorldEventData
    {
        public const int MaxHistory = 16;

        public string EventId { get; set; }
        public WorldEventType Type { get; set; }
        public WorldEventState State { get; set; }

        /// <summary>1 (minor) to 5 (grave).</summary>
        public int Severity { get; set; }

        /// <summary>Why it happened, in words.</summary>
        public string Cause { get; set; }

        public SettlementId SettlementId { get; set; }
        public string RouteId { get; set; }
        public RegionId RegionId { get; set; }
        public FactionId FactionA { get; set; }
        public FactionId FactionB { get; set; }
        public string GroupId { get; set; }

        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
        public long ExpiresAt { get; set; } = -1;
        public long ClosedAt { get; set; } = -1;

        /// <summary>The cause key that created it: the same cause never creates a second event while this one is open.</summary>
        public string Key { get; set; }

        public List<string> Applied { get; set; } = new List<string>();
        public List<string> History { get; set; } = new List<string>();

        public EventKnowledge Known { get; set; }
        public long KnownAt { get; set; } = -1;
        public WorldEventState KnownState { get; set; }
        public int KnownSeverity { get; set; }
        public string KnownFrom { get; set; }

        public bool IsOpen => State == WorldEventState.Active || State == WorldEventState.Escalating;

        /// <summary>The player's knowledge no longer matches the event (it changed since they heard).</summary>
        public bool KnowledgeOutdated => Known != EventKnowledge.Unknown && (KnownState != State || KnownSeverity != Severity);

        public bool Once(string key)
        {
            if (Applied.Contains(key)) return false;
            Applied.Add(key);
            return true;
        }

        /// <summary>
        /// Phase 18: <see cref="Once"/> for something done once per period (a day, a week). Keys of earlier periods with the same
        /// prefix are dropped, so an event that lasts for years keeps one key per prefix, not one per period.
        /// </summary>
        public bool OncePer(string prefix, long period)
        {
            var key = prefix + period.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Applied.RemoveAll(k => k != key && k.StartsWith(prefix, StringComparison.Ordinal) && k.Length > prefix.Length && char.IsDigit(k[prefix.Length]));
            return Once(key);
        }

        public void Note(long at, string line)
        {
            History.Add(TravelRules.Clock(at) + " " + line);
            while (History.Count > MaxHistory) History.RemoveAt(0);
        }

        public WorldEventData Clone()
        {
            var e = (WorldEventData)MemberwiseClone();
            e.Applied = new List<string>(Applied);
            e.History = new List<string>(History);
            return e;
        }
    }

    /// <summary>Phase 16: the numbers of travel, one place for the planner, caravans and the map.</summary>
    public static class TravelRules
    {
        /// <summary>The economy's scale (25 cells a travel day of ~30 km on foot).</summary>
        public const double KmPerCell = 1.2;

        /// <summary>Walking pace on a good road, km per hour (SRD normal pace: about 3 miles an hour).</summary>
        public const double FootKmPerHour = 4.8;

        /// <summary>Hours walked in a travel day (the rest is camp, meals and sleep).</summary>
        public const int HoursPerTravelDay = 8;

        public const int DayStartsAt = 6 * 60;
        public const int NightFallsAt = 20 * 60;

        public static double Km(RouteSegmentData r) => r.Cells * KmPerCell;

        /// <summary>Cost multiplier of a segment's state (worn roads are slower).</summary>
        public static double ConditionFactor(RouteSegmentData r) => r.Condition == RouteCondition.Worn ? 1.25 : 1.0;

        /// <summary>Walking hours for a segment (for planning and progress), or +∞ when it cannot be crossed.</summary>
        public static double Hours(RouteSegmentData r, TravelMode mode = TravelMode.OnFoot) => r.Passable ? WalkingHours(r) : double.PositiveInfinity;

        /// <summary>Walking hours for the length of the road itself, open or not (for walking back over a road closed ahead).</summary>
        public static double WalkingHours(RouteSegmentData r)
        {
            var bridge = r.HasBridge && r.Bridge == BridgeState.Damaged ? 1.15 : 1.0;
            return Km(r) / FootKmPerHour * (r.TerrainPercent / 100.0) * ConditionFactor(r) * bridge;
        }

        /// <summary>Metres walked in one hour on this segment (night halves it); <paramref name="back"/>: walking back over a road closed ahead.</summary>
        public static int MetresPerHour(RouteSegmentData r, bool night, bool back = false)
        {
            var h = back ? WalkingHours(r) : Hours(r);
            if (double.IsInfinity(h) || h <= 0) return 0;
            var m = Km(r) * 1000.0 / h;
            return (int)Math.Max(1, Math.Round(night ? m / 2 : m));
        }

        public static bool IsNight(int minuteOfDay) => minuteOfDay < DayStartsAt || minuteOfDay >= NightFallsAt;

        /// <summary>
        /// Phase 21: how the weather slows walking on a segment (1 = not at all), and why. Heavy rain and storms make the
        /// going slow and muddy; snow slower, and lying snow slower still; a pass in snow is hardest. Fog slows a little.
        /// </summary>
        public static double WeatherFactor(RouteSegmentData r, WeatherPeriodData w, int snowCover, out string why)
        {
            why = null;
            if (w == null) return 1.0;
            var f = 1.0;
            switch (w.Kind)
            {
                case WeatherKind.HeavyRain: f = 0.8; why = "heavy rain"; break;
                case WeatherKind.Thunderstorm: f = 0.7; why = "a thunderstorm"; break;
                case WeatherKind.LightSnow: f = 0.85; why = "falling snow"; break;
                case WeatherKind.HeavySnow: f = 0.6; why = "heavy snow"; break;
                case WeatherKind.Fog: f = 0.9; why = "fog"; break;
            }

            if (snowCover >= 40)
            {
                f *= 0.85;
                why = why == null ? "lying snow" : why + " on lying snow";
            }

            if (r != null && r.Kind == RouteKind.Pass && (WeatherWords.IsSnow(w.Kind) || snowCover >= 40))
            {
                f *= 0.75;
                why += " in the pass";
            }

            return f;
        }

        public static string Clock(long absoluteMinute)
        {
            if (absoluteMinute < 0) return "-";
            var day = absoluteMinute / 1440;
            var m = (int)(absoluteMinute % 1440);
            var date = Calendar.WorldDate.FromDayNumber(day);
            return date.IsValidDate ? $"{date}, {m / 60:00}:{m % 60:00}" : $"day {day} {m / 60:00}:{m % 60:00}";
        }

        /// <summary>A duration in hours in words ("1 day 3 h", "5 h").</summary>
        public static string Duration(double hours)
        {
            if (double.IsInfinity(hours)) return "impassable";
            var days = (int)(hours / HoursPerTravelDay);
            var rest = hours - days * HoursPerTravelDay;
            return days > 0 ? $"{days} travel day{(days == 1 ? string.Empty : "s")} {Math.Round(rest):0} h" : $"{Math.Max(1, Math.Round(hours)):0} h";
        }
    }
}
