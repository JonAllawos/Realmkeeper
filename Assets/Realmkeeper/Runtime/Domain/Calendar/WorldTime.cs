using System;
using System.Globalization;

namespace Realmkeeper.Domain.Calendar
{
    /// <summary>The parts of the day used by routines, shops and lighting.</summary>
    public enum DayPart
    {
        Night = 0,
        Morning = 1,
        Midday = 2,
        Afternoon = 3,
        Evening = 4,
    }

    /// <summary>
    /// Phase 5: the hour ranges of the day parts (configurable; defaults below). Each value is the
    /// minute of the day the part starts at; night runs from <see cref="NightStarts"/> to <see cref="MorningStarts"/>.
    /// </summary>
    public sealed class DayPartRules
    {
        public int MorningStarts { get; set; } = 6 * 60;
        public int MiddayStarts { get; set; } = 11 * 60;
        public int AfternoonStarts { get; set; } = 13 * 60;
        public int EveningStarts { get; set; } = 18 * 60;
        public int NightStarts { get; set; } = 22 * 60;

        public DayPart PartOf(int minuteOfDay)
        {
            var m = WorldTime.Normalize(minuteOfDay);
            if (m >= NightStarts || m < MorningStarts) return DayPart.Night;
            if (m >= EveningStarts) return DayPart.Evening;
            if (m >= AfternoonStarts) return DayPart.Afternoon;
            if (m >= MiddayStarts) return DayPart.Midday;
            return DayPart.Morning;
        }

        public void Validate()
        {
            if (!(0 <= MorningStarts && MorningStarts < MiddayStarts && MiddayStarts < AfternoonStarts && AfternoonStarts < EveningStarts &&
                  EveningStarts < NightStarts && NightStarts < WorldTime.MinutesPerDay))
                throw new InvalidOperationException("Day parts must start in order within the day.");
        }
    }

    /// <summary>
    /// A moment in world time: a calendar date plus the minute of the day (0–1439). The date is
    /// authoritative for the daily simulation (which runs once per midnight); the minute drives
    /// routines, shop hours and lighting. Value type; immutable.
    /// </summary>
    public readonly struct WorldTime : IEquatable<WorldTime>, IComparable<WorldTime>
    {
        public const int MinutesPerHour = 60;
        public const int HoursPerDay = 24;
        public const int MinutesPerDay = MinutesPerHour * HoursPerDay;

        /// <summary>When a new world's clock starts (08:00).</summary>
        public const int DefaultStartMinute = 8 * 60;

        public WorldTime(WorldDate date, int minuteOfDay)
        {
            if (minuteOfDay < 0 || minuteOfDay >= MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(minuteOfDay));
            Date = date;
            MinuteOfDay = minuteOfDay;
        }

        public WorldDate Date { get; }
        public int MinuteOfDay { get; }
        public int Hour => MinuteOfDay / MinutesPerHour;
        public int Minute => MinuteOfDay % MinutesPerHour;

        /// <summary>Minutes since the calendar's epoch (for ordering and arithmetic).</summary>
        public long TotalMinutes => Date.ToDayNumber() * MinutesPerDay + MinuteOfDay;

        public static WorldTime FromTotalMinutes(long total)
        {
            var day = Math.DivRem(total, MinutesPerDay, out var minute);
            if (minute < 0)
            {
                minute += MinutesPerDay;
                day--;
            }

            return new WorldTime(WorldDate.FromDayNumber(day), (int)minute);
        }

        public WorldTime AddMinutes(long minutes) => FromTotalMinutes(TotalMinutes + minutes);

        public static int Normalize(int minuteOfDay) => ((minuteOfDay % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;

        public static string Clock(int minuteOfDay)
        {
            var m = Normalize(minuteOfDay);
            return (m / MinutesPerHour).ToString("00", CultureInfo.InvariantCulture) + ":" + (m % MinutesPerHour).ToString("00", CultureInfo.InvariantCulture);
        }

        public bool Equals(WorldTime other) => Date == other.Date && MinuteOfDay == other.MinuteOfDay;
        public override bool Equals(object obj) => obj is WorldTime t && Equals(t);
        public override int GetHashCode() => Date.GetHashCode() * 1441 + MinuteOfDay;
        public int CompareTo(WorldTime other) => TotalMinutes.CompareTo(other.TotalMinutes);
        public override string ToString() => Clock(MinuteOfDay) + ", " + Date;

        public static bool operator ==(WorldTime a, WorldTime b) => a.Equals(b);
        public static bool operator !=(WorldTime a, WorldTime b) => !a.Equals(b);
    }
}
