using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>Lake plane at the water level and ribbon meshes along river centre lines.</summary>
    public static class WaterMeshBuilder
    {
        public const float RiverWidth = 3.6f;
        public const float RiverSurfaceOffset = 0.25f;

        public static Mesh BuildLakePlane(RegionMap map)
        {
            var y = map.WaterWorldHeight;
            var w = MapSampling.WorldWidth(map);
            var d = MapSampling.WorldDepth(map);
            var builder = new MeshBuilder();
            builder.AddQuad(new Vector3(0, y, 0), new Vector3(0, y, d), new Vector3(w, y, d), new Vector3(w, y, 0), Vector3.up, Color.white);
            return builder.ToMesh("Lake");
        }

        /// <summary>Returns null when the map has no rivers.</summary>
        public static Mesh BuildRivers(RegionMap map)
        {
            if (map.Rivers.Count == 0) return null;
            var builder = new MeshBuilder();
            foreach (var river in map.Rivers)
            {
                if (river.Count < 2) continue;
                var points = MapSampling.SmoothPath(map, river, 2);
                AddRiverRibbon(map, points, builder);
            }

            return builder.VertexCount > 0 ? builder.ToMesh("Rivers") : null;
        }

        private static void AddRiverRibbon(RegionMap map, List<Vector3> points, MeshBuilder builder)
        {
            // Surface height follows the bed, never rises downstream, and meets the lake.
            var heights = new float[points.Count];
            var previous = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var h = MapSampling.HeightAt(map, points[i].x, points[i].z) + RiverSurfaceOffset;
                h = Mathf.Max(Mathf.Min(previous, h), map.WaterWorldHeight - 0.05f);
                heights[i] = h;
                previous = h;
            }

            var taper = Mathf.Max(1, points.Count / 6);
            for (var i = 0; i < points.Count - 1; i++)
            {
                var a = Edge(points, heights, i, taper);
                var b = Edge(points, heights, i + 1, taper);
                builder.AddQuad(a.left, b.left, b.right, a.right, Vector3.up, Color.white);
            }
        }

        private static (Vector3 left, Vector3 right) Edge(List<Vector3> points, float[] heights, int i, int taper)
        {
            var dir = MapSampling.PathDirection(points, i);
            var right = new Vector3(dir.z, 0f, -dir.x);
            var width = RiverWidth * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01((float)i / taper));
            var center = new Vector3(points[i].x, heights[i], points[i].z);
            return (center - right * (width * 0.5f), center + right * (width * 0.5f));
        }
    }
}
