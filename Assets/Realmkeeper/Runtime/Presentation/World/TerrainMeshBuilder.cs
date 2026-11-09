using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Builds terrain meshes from a <see cref="RegionMap"/>: one vertex per cell centre,
    /// split into chunks of <see cref="ChunkCells"/> × <see cref="ChunkCells"/> quads for
    /// culling. Colours blend terrain type, height and slope; normals come from the height
    /// field so chunk seams shade continuously. Pure function of the map: no randomness.
    /// </summary>
    public static class TerrainMeshBuilder
    {
        public const int ChunkCells = 32;
        public const float SnowElevation = 0.84f;

        public sealed class Chunk
        {
            public Chunk(int chunkX, int chunkZ, Mesh mesh)
            {
                ChunkX = chunkX;
                ChunkZ = chunkZ;
                Mesh = mesh;
            }

            public int ChunkX { get; }
            public int ChunkZ { get; }
            public Mesh Mesh { get; }
        }

        public static List<Chunk> BuildChunks(RegionMap map)
        {
            var normals = ComputeNormals(map);
            var colors = ComputeColors(map, normals);
            var chunks = new List<Chunk>();
            var quadsX = map.Width - 1;
            var quadsZ = map.Depth - 1;

            for (var cz = 0; cz * ChunkCells < quadsZ; cz++)
            {
                for (var cx = 0; cx * ChunkCells < quadsX; cx++)
                {
                    var x0 = cx * ChunkCells;
                    var z0 = cz * ChunkCells;
                    var x1 = Mathf.Min(x0 + ChunkCells, quadsX);
                    var z1 = Mathf.Min(z0 + ChunkCells, quadsZ);
                    chunks.Add(new Chunk(cx, cz, BuildGrid(map, normals, colors, x0, z0, x1, z1, $"Terrain {cx},{cz}")));
                }
            }

            return chunks;
        }

        /// <summary>Full-resolution mesh for the single MeshCollider used by picking.</summary>
        public static Mesh BuildColliderMesh(RegionMap map)
        {
            var mesh = BuildGrid(map, null, null, 0, 0, map.Width - 1, map.Depth - 1, "Terrain Collider");
            return mesh;
        }

        /// <summary>Earth-coloured walls around the region edge, so the map reads as a diorama.</summary>
        public static Mesh BuildSkirt(RegionMap map, float bottomY)
        {
            var perimeter = new List<Vector2Int>();
            for (var x = 0; x < map.Width; x++) perimeter.Add(new Vector2Int(x, 0));
            for (var z = 1; z < map.Depth; z++) perimeter.Add(new Vector2Int(map.Width - 1, z));
            for (var x = map.Width - 2; x >= 0; x--) perimeter.Add(new Vector2Int(x, map.Depth - 1));
            for (var z = map.Depth - 2; z >= 0; z--) perimeter.Add(new Vector2Int(0, z));

            var center = new Vector3(MapSampling.WorldWidth(map) * 0.5f, 0f, MapSampling.WorldDepth(map) * 0.5f);
            var builder = new MeshBuilder();
            for (var i = 0; i < perimeter.Count - 1; i++)
            {
                var a = perimeter[i];
                var b = perimeter[i + 1];
                var topA = MapSampling.CellToWorld(map, a.x, a.y);
                var topB = MapSampling.CellToWorld(map, b.x, b.y);
                var botA = new Vector3(topA.x, bottomY, topA.z);
                var botB = new Vector3(topB.x, bottomY, topB.z);
                var outward = (topA + topB) * 0.5f - center;
                outward.y = 0f;
                builder.AddQuad(topA, topB, botB, botA, outward, Palette.Earth);
            }

            return builder.ToMesh("Terrain Skirt");
        }

        public static Vector3[] ComputeNormals(RegionMap map)
        {
            var normals = new Vector3[map.Width * map.Depth];
            var inv = 1f / (2f * map.CellSize);
            for (var z = 0; z < map.Depth; z++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var hl = map.GetWorldHeight(Mathf.Max(0, x - 1), z);
                    var hr = map.GetWorldHeight(Mathf.Min(map.Width - 1, x + 1), z);
                    var hd = map.GetWorldHeight(x, Mathf.Max(0, z - 1));
                    var hu = map.GetWorldHeight(x, Mathf.Min(map.Depth - 1, z + 1));
                    normals[z * map.Width + x] = new Vector3(-(hr - hl) * inv, 1f, -(hu - hd) * inv).normalized;
                }
            }

            return normals;
        }

        public static Color[] ComputeColors(RegionMap map, Vector3[] normals)
        {
            var colors = new Color[map.Width * map.Depth];
            var waterLevel = map.WaterLevel;
            for (var z = 0; z < map.Depth; z++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var i = z * map.Width + x;
                    var e = map.GetElevation(x, z);
                    var slope = Mathf.Sqrt(Mathf.Max(0f, 1f / (normals[i].y * normals[i].y) - 1f)); // rise over run
                    var terrain = map.GetTerrain(x, z);
                    var dryness = MapSampling.ValueNoise(x / 11f, z / 11f, 17);

                    Color c;
                    switch (terrain)
                    {
                        case TerrainType.DeepWater:
                        case TerrainType.ShallowWater:
                        case TerrainType.River:
                            c = Color.Lerp(Palette.DeepBed, Palette.ShallowBed, Mathf.Clamp01((e - (waterLevel - 0.1f)) / 0.1f));
                            break;
                        case TerrainType.Shore:
                            c = Palette.Sand;
                            break;
                        case TerrainType.Forest:
                            c = Color.Lerp(Palette.ForestFloor, Palette.Grass, dryness * 0.35f);
                            break;
                        case TerrainType.Hills:
                            c = Color.Lerp(Palette.HillGrass, Palette.DryGrass, dryness * 0.6f);
                            break;
                        case TerrainType.Mountain:
                            c = Color.Lerp(Palette.Rock, Palette.DarkRock, dryness);
                            break;
                        default:
                            c = Color.Lerp(Palette.Grass, Palette.DryGrass, Mathf.SmoothStep(0.25f, 0.85f, dryness));
                            break;
                    }

                    var isWater = map.IsWater(x, z);
                    if (!isWater)
                    {
                        c = Color.Lerp(c, Palette.Rock, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.95f, slope)));
                        var snow = Mathf.InverseLerp(SnowElevation, SnowElevation + 0.06f, e + (dryness - 0.5f) * 0.04f);
                        snow *= 1f - 0.6f * Mathf.InverseLerp(0.9f, 1.4f, slope);
                        c = Color.Lerp(c, Palette.Snow, Mathf.Clamp01(snow));
                        if (map.IsRoad(x, z)) c = Color.Lerp(c, Palette.PackedEarth, 0.45f);
                    }

                    c = Palette.Scale(c, 0.96f + 0.08f * MapSampling.Hash01(x, z, 5));
                    c.a = 1f;
                    colors[i] = c;
                }
            }

            return colors;
        }

        private static Mesh BuildGrid(RegionMap map, Vector3[] normals, Color[] colors, int x0, int z0, int x1, int z1, string name)
        {
            var w = x1 - x0 + 1;
            var d = z1 - z0 + 1;
            var vertices = new Vector3[w * d];
            var meshNormals = normals != null ? new Vector3[w * d] : null;
            var meshColors = colors != null ? new Color[w * d] : null;

            for (var z = 0; z < d; z++)
            {
                for (var x = 0; x < w; x++)
                {
                    var mx = x0 + x;
                    var mz = z0 + z;
                    var v = z * w + x;
                    vertices[v] = MapSampling.CellToWorld(map, mx, mz);
                    if (meshNormals != null) meshNormals[v] = normals[mz * map.Width + mx];
                    if (meshColors != null) meshColors[v] = colors[mz * map.Width + mx];
                }
            }

            var triangles = new int[(w - 1) * (d - 1) * 6];
            var t = 0;
            for (var z = 0; z < d - 1; z++)
            {
                for (var x = 0; x < w - 1; x++)
                {
                    var v00 = z * w + x;
                    var v10 = v00 + 1;
                    var v01 = v00 + w;
                    var v11 = v01 + 1;

                    // Split each quad along the diagonal that better follows the surface.
                    var hA = Mathf.Abs(vertices[v00].y - vertices[v11].y);
                    var hB = Mathf.Abs(vertices[v10].y - vertices[v01].y);
                    if (hA <= hB)
                    {
                        triangles[t++] = v00; triangles[t++] = v01; triangles[t++] = v11;
                        triangles[t++] = v00; triangles[t++] = v11; triangles[t++] = v10;
                    }
                    else
                    {
                        triangles[t++] = v00; triangles[t++] = v01; triangles[t++] = v10;
                        triangles[t++] = v10; triangles[t++] = v01; triangles[t++] = v11;
                    }
                }
            }

            var mesh = new Mesh { name = name };
            if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            if (meshNormals != null) mesh.normals = meshNormals;
            if (meshColors != null) mesh.colors = meshColors;
            mesh.triangles = triangles;
            if (meshNormals == null) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
