using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.World;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Composes a settlement's placeholder architecture into one mesh: cottages and houses
    /// (box body plus gable-roof prism), a wall ring and keep for towns, a chapel for
    /// villages and fields for villages and hamlets. Layout is deterministic per settlement.
    /// Geometry is built in world space; the owning view sits at the origin.
    /// </summary>
    public static class SettlementBuilder
    {
        public sealed class Result
        {
            public Result(Mesh mesh, int buildingCount, Bounds footprint)
            {
                Mesh = mesh;
                BuildingCount = buildingCount;
                Footprint = footprint;
            }

            public Mesh Mesh { get; }
            public int BuildingCount { get; }

            /// <summary>World-space bounds of the settlement (used for the picking collider).</summary>
            public Bounds Footprint { get; }
        }

        private struct Placed
        {
            public Vector2 Position;
            public float Radius;
        }

        public static Result Build(RegionMap map, SettlementSummary settlement)
        {
            var rng = new LocalRandom(Seed(settlement.Position));
            var center = MapSampling.CellToWorld(map, settlement.Position);
            var radius = MapSampling.SettlementRadiusCells(settlement.SettlementType) * map.CellSize;
            var exits = RoadExitAngles(map, settlement);
            var builder = new MeshBuilder();
            var placed = new List<Placed>();
            var buildings = 0;

            switch (settlement.SettlementType)
            {
                case SettlementType.Town:
                case SettlementType.City:
                case SettlementType.Fortress:
                {
                    var keepOffset = AwayFromExits(exits) * (radius * 0.35f);
                    var keepPos = new Vector2(center.x, center.z) + keepOffset;
                    AddKeep(map, builder, keepPos, rng);
                    placed.Add(new Placed { Position = keepPos, Radius = 3.0f });
                    buildings++;

                    var target = settlement.SettlementType == SettlementType.City ? rng.Range(40, 56) :
                        settlement.SettlementType == SettlementType.Fortress ? rng.Range(10, 15) : rng.Range(25, 41);
                    buildings += AddHouses(map, builder, placed, center, radius - 0.8f, 1.4f, target, HouseStyle.Town, rng);
                    AddWall(map, builder, center, radius + 1.1f, exits);
                    break;
                }

                case SettlementType.Village:
                {
                    var chapelPos = new Vector2(center.x, center.z) + AwayFromExits(exits) * (radius * 0.3f);
                    AddChapel(map, builder, chapelPos, rng);
                    placed.Add(new Placed { Position = chapelPos, Radius = 2.0f });
                    buildings++;
                    buildings += AddHouses(map, builder, placed, center, radius, 1.2f, rng.Range(10, 16), HouseStyle.Village, rng);
                    AddFields(map, builder, center, radius, rng.Range(2, 4), exits, rng);
                    break;
                }

                default:
                    buildings += AddHouses(map, builder, placed, center, radius * 0.75f, 0.6f, rng.Range(4, 7), HouseStyle.Hamlet, rng);
                    AddFields(map, builder, center, radius, rng.Range(3, 6), exits, rng);
                    break;
            }

            var size = (radius + 2f) * 2f;
            var footprint = new Bounds(center + Vector3.up * 1.5f, new Vector3(size, 4f, size));
            return new Result(builder.ToMesh($"Settlement {settlement.Name}"), buildings, footprint);
        }

        // ----- Houses ---------------------------------------------------------------------

        private enum HouseStyle { Hamlet, Village, Town }

        private static int AddHouses(RegionMap map, MeshBuilder builder, List<Placed> placed, Vector3 center, float radius,
            float plazaRadius, int target, HouseStyle style, LocalRandom rng)
        {
            var added = 0;
            for (var attempt = 0; attempt < 900 && added < target; attempt++)
            {
                var angle = rng.Range(0f, Mathf.PI * 2f);
                var dist = Mathf.Lerp(plazaRadius + 0.8f, radius, Mathf.Sqrt(rng.Value()));
                var pos = new Vector2(center.x + Mathf.Cos(angle) * dist, center.z + Mathf.Sin(angle) * dist);

                float width, length, wallHeight;
                switch (style)
                {
                    case HouseStyle.Town:
                        width = rng.Range(1.1f, 1.6f); length = rng.Range(1.5f, 2.4f); wallHeight = rng.Range(1.4f, 2.4f);
                        break;
                    case HouseStyle.Village:
                        width = rng.Range(1.1f, 1.5f); length = rng.Range(1.5f, 2.2f); wallHeight = rng.Range(1.0f, 1.4f);
                        break;
                    default:
                        width = rng.Range(1.1f, 1.4f); length = rng.Range(1.6f, 2.1f); wallHeight = rng.Range(0.9f, 1.15f);
                        break;
                }

                var footprintRadius = 0.5f * Mathf.Max(width, length) + (style == HouseStyle.Town ? 0.15f : 0.45f);
                if (!IsFree(placed, pos, footprintRadius)) continue;
                var cell = MapSampling.WorldToCell(map, pos.x, pos.y);
                if (map.IsRoad(cell.X, cell.Z) || map.IsWater(cell.X, cell.Z)) continue;

                // Gable end faces the settlement centre, with a little irregularity.
                var toCenter = new Vector2(center.x, center.z) - pos;
                var yaw = Mathf.Atan2(toCenter.x, toCenter.y) * Mathf.Rad2Deg + rng.Range(-12f, 12f);
                var ground = MinHeight(map, pos, footprintRadius) - 0.25f;
                var baseCenter = new Vector3(pos.x, ground, pos.y);
                var bodyHeight = wallHeight + 0.25f;

                Color walls;
                Color roof;
                var pick = rng.Value();
                switch (style)
                {
                    case HouseStyle.Town:
                        walls = pick < 0.45f ? Palette.Plaster : pick < 0.8f ? Palette.Limestone : Palette.Timber;
                        roof = rng.Value() < 0.6f ? Palette.RoofTile : Palette.RoofTileWarm;
                        break;
                    case HouseStyle.Village:
                        walls = pick < 0.55f ? Palette.Plaster : Palette.Timber;
                        roof = rng.Value() < 0.7f ? Palette.RoofTileWarm : Palette.RoofTile;
                        break;
                    default:
                        walls = pick < 0.5f ? Palette.Timber : Palette.Plaster;
                        roof = Palette.RoofTileWarm;
                        break;
                }

                // Roofs match the exploration kit (terracotta round tiles in every settlement type); the
                // random draws are unchanged, so placement and every other colour stay as they were.

                walls = Palette.Scale(walls, rng.Range(0.93f, 1.05f));
                roof = Palette.Scale(roof, rng.Range(0.92f, 1.06f));
                builder.AddBox(baseCenter, new Vector3(width, bodyHeight, length), yaw, walls, walls);
                var overhang = 0.18f;
                builder.AddGableRoof(baseCenter + Vector3.up * bodyHeight, width + overhang * 2f, length + overhang * 2f,
                    width * rng.Range(0.55f, 0.75f), yaw, roof, walls);

                placed.Add(new Placed { Position = pos, Radius = footprintRadius });
                added++;
            }

            return added;
        }

        // ----- Landmarks ------------------------------------------------------------------

        private static void AddKeep(RegionMap map, MeshBuilder builder, Vector2 pos, LocalRandom rng)
        {
            var yaw = rng.Range(0f, 90f);
            var ground = MinHeight(map, pos, 2.2f) - 0.3f;
            var baseCenter = new Vector3(pos.x, ground, pos.y);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            builder.AddBox(baseCenter, new Vector3(3.2f, 4.6f, 3.2f), yaw, Palette.Limestone, Palette.Stone);
            builder.AddPyramid(baseCenter + Vector3.up * 4.6f, 1.75f, 1.6f, yaw, Palette.Slate);
            for (var i = 0; i < 4; i++)
            {
                var corner = rotation * new Vector3(i % 2 == 0 ? -1.6f : 1.6f, 0f, i < 2 ? -1.6f : 1.6f);
                var turretBase = baseCenter + corner;
                builder.AddBox(turretBase, new Vector3(0.9f, 5.4f, 0.9f), yaw, Palette.Stone, Palette.Stone);
                builder.AddPyramid(turretBase + Vector3.up * 5.4f, 0.55f, 1.1f, yaw, Palette.Slate);
            }
        }

        private static void AddChapel(RegionMap map, MeshBuilder builder, Vector2 pos, LocalRandom rng)
        {
            var yaw = rng.Range(0f, 360f);
            var ground = MinHeight(map, pos, 1.8f) - 0.25f;
            var baseCenter = new Vector3(pos.x, ground, pos.y);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            builder.AddBox(baseCenter, new Vector3(1.6f, 2.1f, 3.2f), yaw, Palette.Limestone, Palette.Limestone);
            builder.AddGableRoof(baseCenter + Vector3.up * 2.1f, 1.95f, 3.5f, 1.2f, yaw, Palette.Slate, Palette.Limestone);
            var towerBase = baseCenter + rotation * new Vector3(0f, 0f, -1.9f);
            builder.AddBox(towerBase, new Vector3(1.0f, 3.8f, 1.0f), yaw, Palette.Stone, Palette.Stone);
            builder.AddPyramid(towerBase + Vector3.up * 3.8f, 0.6f, 1.6f, yaw, Palette.Slate);
        }

        private static void AddWall(RegionMap map, MeshBuilder builder, Vector3 center, float radius, List<float> exits)
        {
            const int segments = 40;
            const float gateHalfAngle = 11f * Mathf.Deg2Rad;
            var step = Mathf.PI * 2f / segments;
            var segmentLength = radius * step * 1.04f;

            for (var i = 0; i < segments; i++)
            {
                var angle = i * step;
                if (NearExit(exits, angle, gateHalfAngle)) continue;
                var pos = new Vector2(center.x + Mathf.Cos(angle) * radius, center.z + Mathf.Sin(angle) * radius);
                var yaw = -angle * Mathf.Rad2Deg; // local Z runs along the tangent
                var ground = MinHeight(map, pos, 0.6f) - 0.4f;
                var baseCenter = new Vector3(pos.x, ground, pos.y);
                builder.AddBox(baseCenter, new Vector3(0.45f, 2.0f, segmentLength), yaw, Palette.Limestone, Palette.Stone);

                var tower = i % 5 == 0 || NearExit(exits, angle + step, gateHalfAngle) || NearExit(exits, angle - step, gateHalfAngle);
                if (tower)
                {
                    builder.AddBox(baseCenter, new Vector3(1.0f, 2.8f, 1.0f), yaw, Palette.Stone, Palette.Stone);
                    builder.AddPyramid(baseCenter + Vector3.up * 2.8f, 0.6f, 0.9f, yaw, Palette.Slate);
                }
            }
        }

        private static void AddFields(RegionMap map, MeshBuilder builder, Vector3 center, float radius, int count, List<float> exits, LocalRandom rng)
        {
            var crops = new[] { Palette.Wheat, Palette.Crop, Palette.Ploughed };
            var added = 0;
            for (var attempt = 0; attempt < 60 && added < count; attempt++)
            {
                var angle = rng.Range(0f, Mathf.PI * 2f);
                if (NearExit(exits, angle, 20f * Mathf.Deg2Rad)) continue;
                var dist = radius + rng.Range(0.5f, 3.5f);
                var pos = new Vector2(center.x + Mathf.Cos(angle) * dist, center.z + Mathf.Sin(angle) * dist);
                var cell = MapSampling.WorldToCell(map, pos.x, pos.y);
                if (map.IsRoad(cell.X, cell.Z) || map.IsWater(cell.X, cell.Z)) continue;
                var t = map.GetTerrain(cell.X, cell.Z);
                if (t != TerrainType.Plains && t != TerrainType.Hills && t != TerrainType.Forest) continue;

                var yaw = -angle * Mathf.Rad2Deg + rng.Range(-10f, 10f);
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                var halfW = rng.Range(1.6f, 2.4f);
                var halfL = rng.Range(2.2f, 3.4f);
                var color = Palette.Scale(crops[rng.Range(0, crops.Length)], rng.Range(0.94f, 1.04f));
                Vector3 Corner(float x, float z)
                {
                    var p = new Vector3(pos.x, 0f, pos.y) + rotation * new Vector3(x, 0f, z);
                    p.y = MapSampling.HeightAt(map, p.x, p.z) + 0.07f;
                    return p;
                }

                // Two strips per field give a hint of furrows.
                builder.AddQuad(Corner(-halfW, -halfL), Corner(-halfW, halfL), Corner(0f, halfL), Corner(0f, -halfL), Vector3.up, color);
                builder.AddQuad(Corner(0f, -halfL), Corner(0f, halfL), Corner(halfW, halfL), Corner(halfW, -halfL), Vector3.up, Palette.Scale(color, 0.9f));
                added++;
            }
        }

        // ----- Helpers --------------------------------------------------------------------

        /// <summary>Angles (radians, atan2(z, x)) at which roads leave the settlement.</summary>
        public static List<float> RoadExitAngles(RegionMap map, SettlementSummary settlement)
        {
            var exits = new List<float>();
            var c = settlement.Position;
            var reach = MapSampling.SettlementRadiusCells(settlement.SettlementType) + 1;
            foreach (var road in map.Roads)
            {
                if (road.From != settlement.SettlementId && road.To != settlement.SettlementId) continue;
                var cells = road.Cells;
                var forward = road.From == settlement.SettlementId;
                for (var k = 0; k < cells.Count; k++)
                {
                    var cell = cells[forward ? k : cells.Count - 1 - k];
                    if (cell.DistanceSquared(c) >= reach * reach)
                    {
                        exits.Add(Mathf.Atan2(cell.Z - c.Z, cell.X - c.X));
                        break;
                    }
                }
            }

            return exits;
        }

        private static Vector2 AwayFromExits(List<float> exits)
        {
            if (exits.Count == 0) return Vector2.up;
            var sum = Vector2.zero;
            foreach (var a in exits) sum += new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            if (sum.sqrMagnitude < 1e-4f)
            {
                // Roads leave in opposite directions: go perpendicular to them.
                return new Vector2(-Mathf.Sin(exits[0]), Mathf.Cos(exits[0]));
            }

            return -sum.normalized;
        }

        private static bool NearExit(List<float> exits, float angle, float halfWidth)
        {
            foreach (var e in exits)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, e * Mathf.Rad2Deg)) * Mathf.Deg2Rad < halfWidth) return true;
            }

            return false;
        }

        private static bool IsFree(List<Placed> placed, Vector2 pos, float radius)
        {
            foreach (var p in placed)
            {
                var min = p.Radius + radius;
                if ((p.Position - pos).sqrMagnitude < min * min) return false;
            }

            return true;
        }

        private static float MinHeight(RegionMap map, Vector2 pos, float radius)
        {
            var h = MapSampling.HeightAt(map, pos.x, pos.y);
            h = Mathf.Min(h, MapSampling.HeightAt(map, pos.x - radius, pos.y));
            h = Mathf.Min(h, MapSampling.HeightAt(map, pos.x + radius, pos.y));
            h = Mathf.Min(h, MapSampling.HeightAt(map, pos.x, pos.y - radius));
            h = Mathf.Min(h, MapSampling.HeightAt(map, pos.x, pos.y + radius));
            return h;
        }

        private static uint Seed(MapCoord position) =>
            unchecked((uint)(position.X * 73856093) ^ (uint)(position.Z * 19349663) ^ 0x9E3779B9u) | 1u;

        /// <summary>Small xorshift generator: visual layout only, independent of simulation RNG.</summary>
        private sealed class LocalRandom
        {
            private uint _state;

            public LocalRandom(uint seed) { _state = seed == 0 ? 1u : seed; }

            public float Value()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / 16777216f;
            }

            public float Range(float min, float max) => min + (max - min) * Value();

            /// <summary>Integer in [min, max).</summary>
            public int Range(int min, int max) => min + Mathf.Min(max - min - 1, (int)(Value() * (max - min)));
        }
    }
}
