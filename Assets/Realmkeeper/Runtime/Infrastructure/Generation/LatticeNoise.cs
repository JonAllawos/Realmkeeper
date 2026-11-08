using System;
using Realmkeeper.Domain.Randomness;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>
    /// Seeded 2D value noise with fractal Brownian motion. Lattice values come from an
    /// integer hash, so results depend only on (seed, coordinates). Avoids transcendental
    /// functions so output is reproducible across IEEE-754 platforms.
    /// </summary>
    internal sealed class LatticeNoise
    {
        private readonly ulong _seed;

        public LatticeNoise(ulong seed)
        {
            _seed = seed;
        }

        /// <summary>Smooth noise in [0, 1].</summary>
        public float Sample(float x, float z)
        {
            var fx = (float)Math.Floor(x);
            var fz = (float)Math.Floor(z);
            var x0 = (int)fx;
            var z0 = (int)fz;
            var tx = Smooth(x - fx);
            var tz = Smooth(z - fz);

            var a = Lattice(x0, z0);
            var b = Lattice(x0 + 1, z0);
            var c = Lattice(x0, z0 + 1);
            var d = Lattice(x0 + 1, z0 + 1);

            var top = a + (b - a) * tx;
            var bottom = c + (d - c) * tx;
            return top + (bottom - top) * tz;
        }

        /// <summary>Fractal sum normalized to [0, 1].</summary>
        public float Fbm(float x, float z, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                // Offset each octave so lattice points do not align.
                sum += amplitude * Sample(x * frequency + i * 17.31f, z * frequency - i * 11.07f);
                norm += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return sum / norm;
        }

        /// <summary>Ridged fractal: sharp crests at noise mid-values, in [0, 1].</summary>
        public float Ridged(float x, float z, int octaves)
        {
            var n = Fbm(x, z, octaves);
            var r = 1f - Math.Abs(2f * n - 1f);
            return r * r;
        }

        private float Lattice(int x, int z)
        {
            unchecked
            {
                var h = SeedMixer.Mix(_seed ^ ((ulong)(uint)x * 0x9E3779B1UL) ^ ((ulong)(uint)z * 0x85EBCA77C2B2AE63UL));
                return (h >> 40) * (1f / 16777216f);
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
