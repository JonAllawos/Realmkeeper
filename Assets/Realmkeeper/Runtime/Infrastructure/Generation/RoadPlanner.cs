using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Generation
{
    /// <summary>
    /// A* road routing over the region grid (4-connected). Costs favour flat, open land,
    /// reuse existing roads, allow bridging rivers at a premium, and forbid open water.
    /// Ties are broken by insertion order, so routes are deterministic.
    /// </summary>
    internal sealed class RoadPlanner
    {
        private const float ExistingRoadMultiplier = 0.35f;
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dz = { 0, 0, 1, -1 };

        private readonly RegionMap _map;

        public RoadPlanner(RegionMap map)
        {
            _map = map;
        }

        public List<MapCoord> FindPath(MapCoord start, MapCoord goal)
        {
            var w = _map.Width;
            var count = w * _map.Depth;
            var gScore = new float[count];
            var cameFrom = new int[count];
            var closed = new bool[count];
            for (var i = 0; i < count; i++)
            {
                gScore[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
            }

            var open = new MinHeap();
            var startIndex = start.Z * w + start.X;
            var goalIndex = goal.Z * w + goal.X;
            gScore[startIndex] = 0f;
            open.Push(startIndex, Heuristic(start, goal));

            while (open.Count > 0)
            {
                var current = open.Pop();
                if (current == goalIndex) return Reconstruct(cameFrom, current, w);
                if (closed[current]) continue;
                closed[current] = true;

                var cx = current % w;
                var cz = current / w;
                for (var d = 0; d < 4; d++)
                {
                    var nx = cx + Dx[d];
                    var nz = cz + Dz[d];
                    if (!_map.InBounds(nx, nz)) continue;
                    var ni = nz * w + nx;
                    if (closed[ni]) continue;

                    var step = StepCost(cx, cz, nx, nz);
                    if (float.IsPositiveInfinity(step)) continue;

                    var tentative = gScore[current] + step;
                    if (tentative < gScore[ni])
                    {
                        gScore[ni] = tentative;
                        cameFrom[ni] = current;
                        open.Push(ni, tentative + Heuristic(new MapCoord(nx, nz), goal));
                    }
                }
            }

            return null;
        }

        private float StepCost(int fromX, int fromZ, int x, int z)
        {
            var terrain = _map.GetTerrain(x, z);
            float cost;
            switch (terrain)
            {
                case TerrainType.DeepWater:
                case TerrainType.ShallowWater:
                    return float.PositiveInfinity;
                case TerrainType.River: cost = 10f; break; // bridge
                case TerrainType.Mountain: cost = 14f; break;
                case TerrainType.Hills: cost = 2.2f; break;
                case TerrainType.Forest: cost = 2.6f; break;
                case TerrainType.Shore: cost = 1.4f; break;
                default: cost = 1f; break;
            }

            var slope = Math.Abs(_map.GetElevation(x, z) - _map.GetElevation(fromX, fromZ));
            cost += slope * 220f;
            if (_map.IsRoad(x, z)) cost *= ExistingRoadMultiplier;
            return cost;
        }

        private static float Heuristic(MapCoord a, MapCoord b) =>
            (Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z)) * ExistingRoadMultiplier;

        private static List<MapCoord> Reconstruct(int[] cameFrom, int current, int width)
        {
            var path = new List<MapCoord>();
            while (current >= 0)
            {
                path.Add(new MapCoord(current % width, current / width));
                current = cameFrom[current];
            }

            path.Reverse();
            return path;
        }

        /// <summary>Binary min-heap keyed by (priority, insertion sequence) for deterministic ordering.</summary>
        private sealed class MinHeap
        {
            private readonly List<(float priority, long sequence, int value)> _items = new List<(float, long, int)>();
            private long _sequence;

            public int Count => _items.Count;

            public void Push(int value, float priority)
            {
                _items.Add((priority, _sequence++, value));
                var i = _items.Count - 1;
                while (i > 0)
                {
                    var parent = (i - 1) / 2;
                    if (!Less(i, parent)) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public int Pop()
            {
                var top = _items[0].value;
                var last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                var i = 0;
                while (true)
                {
                    var l = 2 * i + 1;
                    var r = l + 1;
                    var smallest = i;
                    if (l < _items.Count && Less(l, smallest)) smallest = l;
                    if (r < _items.Count && Less(r, smallest)) smallest = r;
                    if (smallest == i) break;
                    Swap(i, smallest);
                    i = smallest;
                }

                return top;
            }

            private bool Less(int a, int b)
            {
                var x = _items[a];
                var y = _items[b];
                return x.priority < y.priority || (x.priority == y.priority && x.sequence < y.sequence);
            }

            private void Swap(int a, int b)
            {
                var t = _items[a];
                _items[a] = _items[b];
                _items[b] = t;
            }
        }
    }
}
