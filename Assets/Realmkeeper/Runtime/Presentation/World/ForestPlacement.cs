using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Geography;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    public enum TreeSpecies
    {
        Conifer = 0,
        Broadleaf = 1,
    }

    public readonly struct TreeInstance
    {
        public TreeInstance(TreeSpecies species, int variant, Vector3 position, float yawDegrees, float scale)
        {
            Species = species;
            Variant = variant;
            Position = position;
            YawDegrees = yawDegrees;
            Scale = scale;
        }

        public TreeSpecies Species { get; }
        public int Variant { get; }
        public Vector3 Position { get; }
        public float YawDegrees { get; }
        public float Scale { get; }

        public Matrix4x4 Matrix => Matrix4x4.TRS(Position, Quaternion.Euler(0f, YawDegrees, 0f), Vector3.one * Scale);
    }

    /// <summary>
    /// Deterministic tree placement: positions depend only on the map and settlement
    /// positions (jitter is hashed from cell coordinates), so a world always looks the same.
    /// </summary>
    public static class ForestPlacement
    {
        public const int SettlementClearanceCells = 2;
        public const float LoneTreeChance = 0.035f;

        public static List<TreeInstance> Place(RegionMap map, IReadOnlyList<SettlementSummary> settlements)
        {
            var exclusion = new List<(float x, float z, float r2)>();
            foreach (var s in settlements)
            {
                var r = (MapSampling.SettlementRadiusCells(s.SettlementType) + SettlementClearanceCells) * map.CellSize;
                if (s.SettlementType == Domain.World.SettlementType.Hamlet || s.SettlementType == Domain.World.SettlementType.Village)
                {
                    r += 2f * map.CellSize; // room for fields
                }

                var p = MapSampling.CellToWorld(map, s.Position);
                exclusion.Add((p.x, p.z, r * r));
            }

            var trees = new List<TreeInstance>();
            for (var z = 1; z < map.Depth - 1; z++)
            {
                for (var x = 1; x < map.Width - 1; x++)
                {
                    var terrain = map.GetTerrain(x, z);
                    int count;
                    if (terrain == TerrainType.Forest)
                    {
                        count = 2;
                        if (MapSampling.Hash01(x, z, 101) < 0.6f) count++;
                        if (MapSampling.Hash01(x, z, 102) < 0.25f) count++;
                    }
                    else if ((terrain == TerrainType.Plains || terrain == TerrainType.Hills) && MapSampling.Hash01(x, z, 103) < LoneTreeChance)
                    {
                        count = 1;
                    }
                    else
                    {
                        continue;
                    }

                    if (map.IsRoad(x, z) || IsNextToRoad(map, x, z)) continue;

                    for (var k = 0; k < count; k++)
                    {
                        var salt = 200 + k * 7;
                        var wx = (x + (MapSampling.Hash01(x, z, salt) - 0.5f) * 0.95f) * map.CellSize;
                        var wz = (z + (MapSampling.Hash01(x, z, salt + 1) - 0.5f) * 0.95f) * map.CellSize;
                        if (IsExcluded(exclusion, wx, wz)) continue;

                        var elevation = map.GetElevation(x, z);
                        var coniferChance = 0.3f + 0.65f * Mathf.InverseLerp(0.42f, 0.6f, elevation);
                        var species = MapSampling.Hash01(x, z, salt + 2) < coniferChance ? TreeSpecies.Conifer : TreeSpecies.Broadleaf;
                        var variant = MapSampling.Hash01(x, z, salt + 3) < 0.5f ? 0 : 1;
                        var scale = Mathf.Lerp(0.75f, 1.25f, MapSampling.Hash01(x, z, salt + 4));
                        var yaw = MapSampling.Hash01(x, z, salt + 5) * 360f;
                        var y = MapSampling.HeightAt(map, wx, wz) - 0.1f;
                        trees.Add(new TreeInstance(species, variant, new Vector3(wx, y, wz), yaw, scale));
                    }
                }
            }

            return trees;
        }

        private static bool IsNextToRoad(RegionMap map, int x, int z) =>
            map.IsRoad(x - 1, z) || map.IsRoad(x + 1, z) || map.IsRoad(x, z - 1) || map.IsRoad(x, z + 1);

        private static bool IsExcluded(List<(float x, float z, float r2)> exclusion, float wx, float wz)
        {
            foreach (var e in exclusion)
            {
                var dx = wx - e.x;
                var dz = wz - e.z;
                if (dx * dx + dz * dz < e.r2) return true;
            }

            return false;
        }
    }
}
