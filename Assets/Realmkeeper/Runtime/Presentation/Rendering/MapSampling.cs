using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.World;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Conversions between region cells and world space. Cell (x, z) has its centre at
    /// world (x * CellSize, height, z * CellSize); the region spans 0..(Width - 1) * CellSize.
    /// </summary>
    public static class MapSampling
    {
        public static Vector3 CellToWorld(RegionMap map, MapCoord cell) =>
            new Vector3(cell.X * map.CellSize, map.GetWorldHeight(cell.X, cell.Z), cell.Z * map.CellSize);

        public static Vector3 CellToWorld(RegionMap map, int x, int z) =>
            new Vector3(x * map.CellSize, map.GetWorldHeight(x, z), z * map.CellSize);

        public static float WorldWidth(RegionMap map) => (map.Width - 1) * map.CellSize;

        public static float WorldDepth(RegionMap map) => (map.Depth - 1) * map.CellSize;

        public static MapCoord WorldToCell(RegionMap map, float wx, float wz)
        {
            var x = Mathf.Clamp(Mathf.RoundToInt(wx / map.CellSize), 0, map.Width - 1);
            var z = Mathf.Clamp(Mathf.RoundToInt(wz / map.CellSize), 0, map.Depth - 1);
            return new MapCoord(x, z);
        }

        /// <summary>Bilinear terrain height in world units at a world-space XZ position.</summary>
        public static float HeightAt(RegionMap map, float wx, float wz)
        {
            var fx = Mathf.Clamp(wx / map.CellSize, 0f, map.Width - 1.001f);
            var fz = Mathf.Clamp(wz / map.CellSize, 0f, map.Depth - 1.001f);
            var x0 = (int)fx;
            var z0 = (int)fz;
            var tx = fx - x0;
            var tz = fz - z0;
            var h00 = map.GetWorldHeight(x0, z0);
            var h10 = map.GetWorldHeight(x0 + 1, z0);
            var h01 = map.GetWorldHeight(x0, z0 + 1);
            var h11 = map.GetWorldHeight(x0 + 1, z0 + 1);
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        }

        /// <summary>Deterministic hash of a cell and salt to [0, 1). Visual variety only, never simulation.</summary>
        public static float Hash01(int x, int z, int salt)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + z * 668265263 + salt * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Smooth value noise built on <see cref="Hash01"/>.</summary>
        public static float ValueNoise(float x, float z, int salt)
        {
            var x0 = Mathf.FloorToInt(x);
            var z0 = Mathf.FloorToInt(z);
            var tx = x - x0;
            var tz = z - z0;
            tx = tx * tx * (3f - 2f * tx);
            tz = tz * tz * (3f - 2f * tz);
            var a = Hash01(x0, z0, salt);
            var b = Hash01(x0 + 1, z0, salt);
            var c = Hash01(x0, z0 + 1, salt);
            var d = Hash01(x0 + 1, z0 + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>Converts a cell path to world XZ points (Y = 0) and smooths it with Chaikin's algorithm.</summary>
        public static List<Vector3> SmoothPath(RegionMap map, IReadOnlyList<MapCoord> cells, int iterations)
        {
            var points = new List<Vector3>(cells.Count);
            foreach (var c in cells)
            {
                var p = new Vector3(c.X * map.CellSize, 0f, c.Z * map.CellSize);
                if (points.Count == 0 || (points[points.Count - 1] - p).sqrMagnitude > 1e-6f) points.Add(p);
            }

            for (var it = 0; it < iterations && points.Count > 2; it++)
            {
                var next = new List<Vector3>(points.Count * 2) { points[0] };
                for (var i = 0; i < points.Count - 1; i++)
                {
                    next.Add(Vector3.Lerp(points[i], points[i + 1], 0.25f));
                    next.Add(Vector3.Lerp(points[i], points[i + 1], 0.75f));
                }

                next.Add(points[points.Count - 1]);
                points = next;
            }

            return points;
        }

        /// <summary>Horizontal direction of a polyline at index i (central difference).</summary>
        public static Vector3 PathDirection(IReadOnlyList<Vector3> points, int i)
        {
            var a = points[Mathf.Max(0, i - 1)];
            var b = points[Mathf.Min(points.Count - 1, i + 1)];
            var d = b - a;
            d.y = 0f;
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
        }

        public static int SettlementRadiusCells(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.City: return 6;
                case SettlementType.Town: return 5;
                case SettlementType.Fortress: return 4;
                case SettlementType.Village: return 4;
                default: return 3;
            }
        }
    }
}
