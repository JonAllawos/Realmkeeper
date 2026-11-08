using System;
using Realmkeeper.Domain.Randomness;

namespace Realmkeeper.Infrastructure.Randomness
{
    /// <summary>
    /// xoshiro256** generator (Blackman &amp; Vigna), seeded through SplitMix64.
    /// Integer-only state and arithmetic, so sequences are identical on every platform
    /// and runtime (Mono, IL2CPP, CoreCLR). Never use System.Random for simulation:
    /// its algorithm is not guaranteed to be stable across runtimes.
    /// </summary>
    public sealed class Xoshiro256Random : IRandomSource
    {
        private ulong _s0, _s1, _s2, _s3;

        public Xoshiro256Random(ulong seed)
        {
            var sm = seed;
            _s0 = SplitMix(ref sm);
            _s1 = SplitMix(ref sm);
            _s2 = SplitMix(ref sm);
            _s3 = SplitMix(ref sm);
        }

        public ulong NextULong()
        {
            unchecked
            {
                var result = RotateLeft(_s1 * 5, 7) * 9;
                var t = _s1 << 17;
                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;
                _s2 ^= t;
                _s3 = RotateLeft(_s3, 45);
                return result;
            }
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
            }

            var range = (ulong)((long)maxExclusive - minInclusive);

            // Rejection sampling removes modulo bias.
            var limit = ulong.MaxValue - ulong.MaxValue % range;
            ulong value;
            do
            {
                value = NextULong();
            }
            while (value >= limit);

            return (int)((long)minInclusive + (long)(value % range));
        }

        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

        private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));

        private static ulong SplitMix(ref ulong state)
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                var z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }

    public sealed class Xoshiro256RandomFactory : IRandomSourceFactory
    {
        public IRandomSource Create(ulong seed) => new Xoshiro256Random(seed);
    }
}
