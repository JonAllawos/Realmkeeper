using System;

namespace Realmkeeper.Domain.Calendar
{
    /// <summary>
    /// A date on the Realmkeeper calendar: 12 months of 30 days (360-day year).
    /// The fixed-length calendar keeps date arithmetic exact and deterministic.
    /// See Documentation/Decisions/ADR-0006-fixed-calendar.md.
    /// </summary>
    public readonly struct WorldDate : IEquatable<WorldDate>, IComparable<WorldDate>
    {
        public const int MonthsPerYear = 12;
        public const int DaysPerMonth = 30;
        public const int DaysPerYear = MonthsPerYear * DaysPerMonth;
        public const int DaysPerWeek = 7;
        public const int MinYear = 1;
        public const int MaxYear = 99999;

        private static readonly string[] MonthNames =
        {
            "Deepwinter", "Thawmoon", "Seedtide", "Blossomtide", "Greenmonth", "Highsun",
            "Emberdays", "Goldfall", "Reaping", "Leaffall", "Frostcall", "Longnight",
        };

        public int Year { get; }
        public int Month { get; }
        public int Day { get; }

        public WorldDate(int year, int month, int day)
        {
            if (!IsValid(year, month, day))
            {
                throw new ArgumentOutOfRangeException(nameof(day), $"Invalid world date {year}-{month}-{day}.");
            }

            Year = year;
            Month = month;
            Day = day;
        }

        /// <summary>False for <c>default(WorldDate)</c> and for dates built from out-of-range parts.</summary>
        public bool IsValidDate => IsValid(Year, Month, Day);

        public string MonthName => Month >= 1 && Month <= MonthsPerYear ? MonthNames[Month - 1] : "?";

        public static bool IsValid(int year, int month, int day) =>
            year >= MinYear && year <= MaxYear &&
            month >= 1 && month <= MonthsPerYear &&
            day >= 1 && day <= DaysPerMonth;

        /// <summary>Zero-based absolute day count since 1-1-1.</summary>
        public long ToDayNumber()
        {
            if (!IsValidDate) throw new InvalidOperationException("Cannot convert an invalid date to a day number.");
            return (long)(Year - 1) * DaysPerYear + (Month - 1) * DaysPerMonth + (Day - 1);
        }

        public static WorldDate FromDayNumber(long dayNumber)
        {
            if (dayNumber < 0) throw new ArgumentOutOfRangeException(nameof(dayNumber));
            var year = (int)(dayNumber / DaysPerYear) + 1;
            var dayOfYear = (int)(dayNumber % DaysPerYear);
            return new WorldDate(year, dayOfYear / DaysPerMonth + 1, dayOfYear % DaysPerMonth + 1);
        }

        public WorldDate AddDays(int days) => FromDayNumber(checked(ToDayNumber() + days));

        public int DaysUntil(WorldDate other) => checked((int)(other.ToDayNumber() - ToDayNumber()));

        public bool Equals(WorldDate other) => Year == other.Year && Month == other.Month && Day == other.Day;

        public override bool Equals(object obj) => obj is WorldDate other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Year * 397 ^ Month) * 397 ^ Day;
            }
        }

        public int CompareTo(WorldDate other)
        {
            var c = Year.CompareTo(other.Year);
            if (c != 0) return c;
            c = Month.CompareTo(other.Month);
            return c != 0 ? c : Day.CompareTo(other.Day);
        }

        /// <summary>Sortable invariant form, e.g. "0412-03-15".</summary>
        public string ToIsoString() => $"{Year:D4}-{Month:D2}-{Day:D2}";

        /// <summary>Human-readable form, e.g. "15 Seedtide, Year 412".</summary>
        public override string ToString() => IsValidDate ? $"{Day} {MonthName}, Year {Year}" : "(invalid date)";

        public static bool operator ==(WorldDate a, WorldDate b) => a.Equals(b);
        public static bool operator !=(WorldDate a, WorldDate b) => !a.Equals(b);
        public static bool operator <(WorldDate a, WorldDate b) => a.CompareTo(b) < 0;
        public static bool operator >(WorldDate a, WorldDate b) => a.CompareTo(b) > 0;
        public static bool operator <=(WorldDate a, WorldDate b) => a.CompareTo(b) <= 0;
        public static bool operator >=(WorldDate a, WorldDate b) => a.CompareTo(b) >= 0;
    }
}
