namespace Realmkeeper.Domain.Randomness
{
    /// <summary>
    /// Stable seed derivation. Used to split one world seed into independent streams
    /// (per generation stage, per simulated day, per simulation system) without storing
    /// RNG state in save files.
    /// </summary>
    /// <remarks>
    /// Never use <c>string.GetHashCode()</c> for seeds: it is randomized per process in .NET.
    /// </remarks>
    public static class SeedMixer
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>SplitMix64 finalizer: a strong, bijective 64-bit mix.</summary>
        public static ulong Mix(ulong value)
        {
            unchecked
            {
                value += 0x9E3779B97F4A7C15UL;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        /// <summary>Combines a seed with one additional component.</summary>
        public static ulong Combine(ulong seed, ulong component)
        {
            unchecked
            {
                return Mix(seed ^ Mix(component + 0x632BE59BD9B4E019UL));
            }
        }

        public static ulong Combine(ulong seed, ulong a, ulong b) => Combine(Combine(seed, a), b);

        /// <summary>Stable 64-bit FNV-1a hash of a string (ordinal, UTF-16 code units).</summary>
        public static ulong HashString(string text)
        {
            unchecked
            {
                var hash = FnvOffset;
                if (text == null)
                {
                    return hash;
                }

                for (var i = 0; i < text.Length; i++)
                {
                    var c = text[i];
                    hash ^= (byte)(c & 0xFF);
                    hash *= FnvPrime;
                    hash ^= (byte)(c >> 8);
                    hash *= FnvPrime;
                }

                return hash;
            }
        }

        public static ulong Combine(ulong seed, string component) => Combine(seed, HashString(component));

        public static ulong FromWorldSeed(long worldSeed) => Mix(unchecked((ulong)worldSeed));
    }
}
