using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Randomness
{
    public static class RandomExtensions
    {
        /// <summary>True with the given probability (clamped to [0,1]).</summary>
        public static bool Chance(this IRandomSource random, double probability)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (probability <= 0d) return false;
            if (probability >= 1d) return true;
            return random.NextDouble() < probability;
        }

        /// <summary>Uniform float in [min, max).</summary>
        public static float Range(this IRandomSource random, float min, float max)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            return (float)(min + (max - min) * random.NextDouble());
        }

        public static T Pick<T>(this IRandomSource random, IReadOnlyList<T> items)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (items == null || items.Count == 0) throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
            return items[random.NextInt(0, items.Count)];
        }

        /// <summary>Deterministic Fisher-Yates shuffle in place.</summary>
        public static void Shuffle<T>(this IRandomSource random, IList<T> items)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (items == null) throw new ArgumentNullException(nameof(items));
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = random.NextInt(0, i + 1);
                var tmp = items[i];
                items[i] = items[j];
                items[j] = tmp;
            }
        }
    }

    /// <summary>Builds RFC 4122 version-4 shaped GUIDs from a deterministic random source.</summary>
    public static class DeterministicGuid
    {
        public static Guid Next(IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var bytes = new byte[16];
            var a = random.NextULong();
            var b = random.NextULong();
            for (var i = 0; i < 8; i++)
            {
                bytes[i] = (byte)(a >> (8 * i));
                bytes[8 + i] = (byte)(b >> (8 * i));
            }

            // Version 4 (byte 7 in .NET's little-endian layout of the time_hi field), RFC 4122 variant.
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
            return new Guid(bytes);
        }
    }
}
