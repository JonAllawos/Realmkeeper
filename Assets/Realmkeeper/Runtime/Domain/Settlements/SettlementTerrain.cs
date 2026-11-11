using System;

namespace Realmkeeper.Domain.Settlements
{
    /// <summary>
    /// The ground of a settlement plan (Phase 10, ADR-0047): the regional fall of the land, its hills (with flattened
    /// tops), a rolling noise, the levelled square, and the beds and banks of its waterways — all from the persisted
    /// profile, so the same plan always has the same ground. Heights are metres relative to the square.
    /// </summary>
    public static class SettlementTerrain
    {
        public const float BankWidth = 6f;

        /// <summary>The ground's height at a point, waterways cut in.</summary>
        public static float Height(SettlementLayoutData plan, float x, float z)
        {
            var h = Natural(plan, x, z);
            foreach (var w in plan.Waterways)
            {
                var d = DistanceToLine(w, x, z, out var nx, out var nz);
                var half = w.Width / 2f;
                if (d > half + BankWidth) continue;
                var surface = WaterSurface(plan, w, nx, nz);
                var bank = surface + 0.5f;
                if (d <= half)
                {
                    var t = d / half;
                    h = Math.Min(h, surface - w.Depth * (1f - t * t));
                }
                else
                {
                    var t = (d - half) / BankWidth;
                    t = t * t * (3f - 2f * t);
                    h = bank + (h - bank) * t;
                }
            }

            return h;
        }

        /// <summary>The water's surface for a waterway at its nearest centre-line point: a little below the land it runs through.</summary>
        public static float WaterSurface(SettlementLayoutData plan, WaterwayData w, float x, float z) => Natural(plan, x, z) - (w.IsRiver ? 1.4f : 0.8f);

        /// <summary>True when a point is in a waterway's channel (with an extra margin).</summary>
        public static bool InWater(SettlementLayoutData plan, float x, float z, float margin = 0f)
        {
            foreach (var w in plan.Waterways)
                if (DistanceToLine(w, x, z, out _, out _) <= w.Width / 2f + margin) return true;
            return false;
        }

        /// <summary>The land before any waterway is cut: fall, hills, noise, the levelled square.</summary>
        public static float Natural(SettlementLayoutData plan, float x, float z)
        {
            var t = plan.Terrain;
            var raw = Raw(t, x, z);
            var level = t.LevelRadius;
            if (level <= 0) return raw - Raw(t, 0, 0);
            var r = (float)Math.Sqrt(x * x + z * z);
            var origin = Raw(t, 0, 0);
            if (r <= level) return 0f;
            var edge = level * 1.8f;
            if (r >= edge) return raw - origin;
            var k = (r - level) / (edge - level);
            k = k * k * (3f - 2f * k);
            return (raw - origin) * k;
        }

        private static float Raw(TerrainProfileData t, float x, float z)
        {
            var h = t.SlopePer100 / 100f * (x * (float)Math.Cos(t.SlopeDirection) + z * (float)Math.Sin(t.SlopeDirection));
            var calm = 1f; // a hill's levelled top carries no roughness (its building stands there)
            foreach (var hill in t.Hills)
            {
                var d = (float)Math.Sqrt((x - hill.Center.X) * (x - hill.Center.X) + (z - hill.Center.Z) * (z - hill.Center.Z));
                if (d >= hill.Radius) continue;
                if (hill.Plateau > 0 && d <= hill.Plateau * 1.6f) calm = Math.Min(calm, d <= hill.Plateau ? 0f : (d - hill.Plateau) / (hill.Plateau * 0.6f));
                if (d <= hill.Plateau) { h += hill.Height; continue; }
                var k = 1f - (d - hill.Plateau) / Math.Max(1f, hill.Radius - hill.Plateau);
                h += hill.Height * k * k * (3f - 2f * k);
            }

            h += calm * t.NoiseAmplitude * (Noise(t.NoiseSeed, x / 45f, z / 45f) + 0.45f * Noise(t.NoiseSeed + 17, x / 17f, z / 17f));
            return h;
        }

        /// <summary>The distance from a point to a waterway's centre line, and the nearest point on it.</summary>
        public static float DistanceToLine(WaterwayData w, float x, float z, out float nx, out float nz)
        {
            var best = float.MaxValue;
            nx = x;
            nz = z;
            for (var i = 0; i + 1 < w.Line.Count; i++)
            {
                var a = w.Line[i];
                var b = w.Line[i + 1];
                var d = SegmentDistance(a.X, a.Z, b.X, b.Z, x, z, out var px, out var pz);
                if (d < best) { best = d; nx = px; nz = pz; }
            }

            return best;
        }

        public static float SegmentDistance(float ax, float az, float bx, float bz, float x, float z, out float px, out float pz)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var len = dx * dx + dz * dz;
            var t = len <= 0 ? 0f : Math.Max(0f, Math.Min(1f, ((x - ax) * dx + (z - az) * dz) / len));
            px = ax + dx * t;
            pz = az + dz * t;
            return (float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
        }

        /// <summary>Smooth value noise in about −1…1 (deterministic per seed).</summary>
        private static float Noise(ulong seed, float x, float z)
        {
            var x0 = (int)Math.Floor(x);
            var z0 = (int)Math.Floor(z);
            var fx = x - x0;
            var fz = z - z0;
            fx = fx * fx * (3f - 2f * fx);
            fz = fz * fz * (3f - 2f * fz);
            var a = Lattice(seed, x0, z0);
            var b = Lattice(seed, x0 + 1, z0);
            var c = Lattice(seed, x0, z0 + 1);
            var d = Lattice(seed, x0 + 1, z0 + 1);
            return a + (b - a) * fx + (c - a) * fz + (a - b - c + d) * fx * fz;
        }

        private static float Lattice(ulong seed, int x, int z)
        {
            var h = seed ^ ((ulong)(uint)x * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)z * 0xC2B2AE3D27D4EB4FUL);
            h ^= h >> 33;
            h *= 0xFF51AFD7ED558CCDUL;
            h ^= h >> 33;
            return (h >> 40) / (float)(1UL << 24) * 2f - 1f;
        }
    }
}
