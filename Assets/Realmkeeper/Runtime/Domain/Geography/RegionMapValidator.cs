using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Geography
{
    /// <summary>Checks that geography is well-formed and consistent with the settlements placed on it.</summary>
    public static class RegionMapValidator
    {
        public static ValidationResult Validate(RegionMap map, IReadOnlyList<SettlementData> settlements)
        {
            var result = new ValidationResult();
            if (map == null)
            {
                result.AddError("Region map is null.");
                return result;
            }

            for (var z = 0; z < map.Depth; z++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var e = map.GetElevation(x, z);
                    if (float.IsNaN(e) || e < 0f || e > 1f) result.AddError($"Cell ({x}, {z}) has invalid elevation {e}.");
                    if (!Enum.IsDefined(typeof(TerrainType), map.GetTerrain(x, z))) result.AddError($"Cell ({x}, {z}) has undefined terrain.");
                }
            }

            if (settlements != null)
            {
                foreach (var s in settlements)
                {
                    if (!map.InBounds(s.Position)) result.AddError($"Settlement '{s.Name}' is outside the region.");
                    else if (map.IsWater(s.Position.X, s.Position.Z)) result.AddError($"Settlement '{s.Name}' is placed on water.");
                }
            }

            foreach (var road in map.Roads)
            {
                for (var i = 0; i < road.Cells.Count; i++)
                {
                    var c = road.Cells[i];
                    var t = map.GetTerrain(c.X, c.Z);
                    if (t == TerrainType.DeepWater || t == TerrainType.ShallowWater) result.AddError($"Road crosses open water at {c}.");
                    if (i > 0 && Math.Abs(c.X - road.Cells[i - 1].X) + Math.Abs(c.Z - road.Cells[i - 1].Z) != 1)
                        result.AddError($"Road is not contiguous at {c}.");
                }
            }

            return result;
        }
    }
}
