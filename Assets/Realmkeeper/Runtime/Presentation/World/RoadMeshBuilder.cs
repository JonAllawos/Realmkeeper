using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.World;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Flattened packed-earth ribbons along each <see cref="RoadPath"/>, with placeholder
    /// timber bridges where a road crosses a river cell.
    /// </summary>
    public static class RoadMeshBuilder
    {
        public const float RoadWidth = 1.7f;
        public const float SurfaceOffset = 0.22f;

        /// <summary>Keep every Nth grid cell before smoothing, so 4-connected staircases become straight runs.</summary>
        public const int SimplifyStep = 3;

        public readonly struct Bridge
        {
            public Bridge(Vector3 position, Vector3 direction)
            {
                Position = position;
                Direction = direction;
            }

            public Vector3 Position { get; }
            public Vector3 Direction { get; }
        }

        public static Mesh Build(RegionMap map, out int bridgeCount)
        {
            var builder = new MeshBuilder();
            bridgeCount = 0;
            foreach (var road in map.Roads)
            {
                if (road.Cells.Count < 2) continue;
                var bridges = FindBridges(map, road.Cells);
                bridgeCount += bridges.Count;
                AddRibbon(map, MapSampling.SmoothPath(map, Simplify(road.Cells, SimplifyStep), 3), bridges, builder);
                foreach (var bridge in bridges) AddBridge(map, bridge, builder);
            }

            return builder.VertexCount > 0 ? builder.ToMesh("Roads") : null;
        }

        public static List<Bridge> FindBridges(RegionMap map, IReadOnlyList<MapCoord> cells)
        {
            var bridges = new List<Bridge>();
            for (var i = 0; i < cells.Count; i++)
            {
                if (map.GetTerrain(cells[i].X, cells[i].Z) != TerrainType.River) continue;

                // Deck sits just above the higher river bank along the road.
                var deck = map.WaterWorldHeight;
                for (var k = Mathf.Max(0, i - 2); k <= Mathf.Min(cells.Count - 1, i + 2); k++)
                {
                    if (!map.IsWater(cells[k].X, cells[k].Z)) deck = Mathf.Max(deck, map.GetWorldHeight(cells[k].X, cells[k].Z));
                }

                var position = MapSampling.CellToWorld(map, cells[i]);
                position.y = deck + 0.15f;
                var a = MapSampling.CellToWorld(map, cells[Mathf.Max(0, i - 1)]);
                var b = MapSampling.CellToWorld(map, cells[Mathf.Min(cells.Count - 1, i + 1)]);
                var dir = b - a;
                dir.y = 0f;
                bridges.Add(new Bridge(position, dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward));
            }

            return bridges;
        }

        public static List<MapCoord> Simplify(IReadOnlyList<MapCoord> cells, int step)
        {
            var result = new List<MapCoord>();
            for (var i = 0; i < cells.Count; i += step) result.Add(cells[i]);
            if (result[result.Count - 1] != cells[cells.Count - 1]) result.Add(cells[cells.Count - 1]);
            return result;
        }

        private static void AddRibbon(RegionMap map, List<Vector3> points, List<Bridge> bridges, MeshBuilder builder)
        {
            var half = RoadWidth * 0.5f;
            var left = new Vector3[points.Count];
            var right = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var dir = MapSampling.PathDirection(points, i);
                var side = new Vector3(dir.z, 0f, -dir.x) * half;
                // Use the highest of centre and edges so the coarse terrain never pokes through.
                var centerHeight = MapSampling.HeightAt(map, points[i].x, points[i].z);
                left[i] = Surface(map, points[i] - side, bridges, centerHeight);
                right[i] = Surface(map, points[i] + side, bridges, centerHeight);
            }

            for (var i = 0; i < points.Count - 1; i++)
            {
                builder.AddQuad(left[i], left[i + 1], right[i + 1], right[i], Vector3.up, Palette.PackedEarth);
            }
        }

        private static Vector3 Surface(RegionMap map, Vector3 p, List<Bridge> bridges, float minHeight)
        {
            var h = Mathf.Max(minHeight, MapSampling.HeightAt(map, p.x, p.z)) + SurfaceOffset;
            var reach = map.CellSize * 1.6f;
            foreach (var bridge in bridges)
            {
                var dx = p.x - bridge.Position.x;
                var dz = p.z - bridge.Position.z;
                if (dx * dx + dz * dz < reach * reach) h = Mathf.Max(h, bridge.Position.y + 0.02f);
            }

            return new Vector3(p.x, h, p.z);
        }

        private static void AddBridge(RegionMap map, Bridge bridge, MeshBuilder builder)
        {
            var yaw = Mathf.Atan2(bridge.Direction.x, bridge.Direction.z) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var length = map.CellSize * 2.6f;
            var deckBase = bridge.Position - Vector3.up * 0.25f;
            builder.AddBox(deckBase, new Vector3(RoadWidth + 0.4f, 0.25f, length), yaw, Palette.BridgeTimber, Palette.BridgeTimber);
            foreach (var side in new[] { -1f, 1f })
            {
                var railBase = bridge.Position + rotation * new Vector3(side * (RoadWidth * 0.5f + 0.12f), 0f, 0f);
                builder.AddBox(railBase, new Vector3(0.12f, 0.45f, length), yaw, Palette.BridgeTimber, Palette.BridgeTimber);
            }

            // Piers down to the river bed.
            foreach (var end in new[] { -0.3f, 0.3f })
            {
                var pierTop = bridge.Position + rotation * new Vector3(0f, 0f, end * length);
                var bed = MapSampling.HeightAt(map, pierTop.x, pierTop.z) - 0.3f;
                var height = pierTop.y - 0.25f - bed;
                if (height > 0.1f) builder.AddBox(new Vector3(pierTop.x, bed, pierTop.z), new Vector3(RoadWidth, height, 0.35f), yaw, Palette.Stone, Palette.Stone);
            }
        }
    }
}
