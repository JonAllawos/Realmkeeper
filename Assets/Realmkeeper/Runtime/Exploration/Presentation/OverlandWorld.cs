using System;
using System.Collections.Generic;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 23 (ADR-0110): the walkable country of one region, drawn from its <see cref="OverlandTerrain"/>.
    /// - <b>Ground</b> in chunks of 16 × 16 cells (640 m): every chunk has a coarse mesh (a vertex per cell, no collider) for
    ///   the horizon; the chunks around the player get a detailed mesh (a vertex every 10 m, the terrain's own heights and
    ///   detail) with a collider, built a chunk per frame, and lose it again when far. Vertex colours by biome.
    /// - <b>Water</b>: one surface at the region's water level over the carved beds of lakes and rivers.
    /// - <b>Roads</b>: a ribbon along each road, conforming to the ground (the map's road cells, smoothed).
    /// - <b>Vegetation</b>: the nature kit's trees and rocks by biome and region kind, drawn with GPU instancing for the
    ///   detailed chunks only (no GameObject per tree). Their materials sit on a hidden carrier renderer, so the atmosphere's
    ///   seasonal leaf colour reaches them like any other renderer.
    /// - <b>Barriers</b>: invisible colliders on impassable cells (mountains, deep water, unfordable river) bordering
    ///   walkable ground, for the detailed chunks, and the region's edge.
    /// Nothing here is state: it is rebuilt from the terrain, which is rebuilt from the seed.
    /// </summary>
    public sealed class OverlandWorld : MonoBehaviour
    {
        public const int ChunkCells = 16;
        private const int DetailPerCell = 4; // a vertex every 10 m
        private const float DetailRadiusMetres = 1000f;
        private const float TreeRadiusMetres = 650f;

        private OverlandTerrain _terrain;
        private Material _ground;
        private Material _water;
        private int _chunksX, _chunksZ;
        private Chunk[] _chunks;
        private readonly List<TreeKind> _trees = new List<TreeKind>();
        private MeshRenderer _carrier;
        private readonly List<Material> _own = new List<Material>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private float _streamTimer;

        public Transform Follow { get; set; }
        public OverlandTerrain Terrain => _terrain;
        public int DetailChunks { get; private set; }
        public int CoarseChunks => _chunks?.Length ?? 0;
        public int TreesDrawn { get; private set; }
        public int BarrierColliders { get; private set; }
        public float LastChunkBuildMs { get; private set; }
        public float RoadKm { get; private set; }

        private sealed class Chunk
        {
            public int X, Z;
            public MeshRenderer Coarse;
            public GameObject Detail;
            public readonly List<(int kind, Matrix4x4 m)> Trees = new List<(int, Matrix4x4)>();
        }

        private sealed class TreeKind
        {
            public string Id;
            public readonly List<(Mesh mesh, int sub, int material, Matrix4x4 local)> Parts = new List<(Mesh, int, int, Matrix4x4)>();
        }

        // ----- Building ---------------------------------------------------------------------------------------

        public static OverlandWorld Build(Transform parent, OverlandView view, ArtCatalog catalog)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var go = new GameObject("Overland — " + view.RegionName);
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<OverlandWorld>();
            w._terrain = view.Terrain;
            var vc = Shader.Find("Realmkeeper/VertexColorLit") ?? Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            w._ground = new Material(vc) { name = "Overland ground", enableInstancing = true };
            var water = Shader.Find("Realmkeeper/Water");
            w._water = water != null ? new Material(water) { name = "Overland water" } : new Material(vc) { name = "Overland water" };
            if (view.Profile == RealmProfiles.AncientPrototype && w._water.HasProperty("_ShallowColor")) w._water.SetColor("_ShallowColor", new Color(0.32f, 0.22f, 0.42f, 0.8f));

            w._chunksX = Mathf.CeilToInt(view.Terrain.Map.Width / (float)ChunkCells);
            w._chunksZ = Mathf.CeilToInt(view.Terrain.Map.Depth / (float)ChunkCells);
            w._chunks = new Chunk[w._chunksX * w._chunksZ];
            var coarseRoot = new GameObject("Ground (coarse)").transform;
            coarseRoot.SetParent(go.transform, false);
            for (var cz = 0; cz < w._chunksZ; cz++)
                for (var cx = 0; cx < w._chunksX; cx++)
                {
                    var c = new Chunk { X = cx, Z = cz };
                    c.Coarse = w.MeshObject(coarseRoot, $"Chunk {cx},{cz} (coarse)", w.GroundMesh(cx, cz, 1), w._ground, false).GetComponent<MeshRenderer>();
                    c.Coarse.shadowCastingMode = ShadowCastingMode.Off;
                    w._chunks[cz * w._chunksX + cx] = c;
                }

            w.BuildWater(go.transform);
            w.BuildRoads(go.transform, view.Profile == RealmProfiles.AncientPrototype);
            w.BuildEdges(go.transform);
            if (catalog != null) w.LoadTrees(go.transform, catalog, view.RegionKind);
            Debug.Log($"Realmkeeper: overland {view.RegionName} prepared in {clock.ElapsedMilliseconds} ms ({w._chunks.Length} coarse chunks, {w.RoadKm:0.0} km of road).");
            return w;
        }

        private GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            return go;
        }

        /// <summary>A chunk's ground mesh at <paramref name="perCell"/> vertices a cell, with biome colours.</summary>
        private Mesh GroundMesh(int cx, int cz, int perCell)
        {
            var map = _terrain.Map;
            var cells = ChunkCells;
            var n = cells * perCell + 1;
            var step = OverlandScale.MetresPerCell / perCell;
            var ox = cx * cells * OverlandScale.MetresPerCell;
            var oz = cz * cells * OverlandScale.MetresPerCell;
            var verts = new Vector3[n * n];
            var colours = new Color[n * n];
            for (var z = 0; z < n; z++)
                for (var x = 0; x < n; x++)
                {
                    var px = Mathf.Min(ox + x * step, map.Width * OverlandScale.MetresPerCell);
                    var pz = Mathf.Min(oz + z * step, map.Depth * OverlandScale.MetresPerCell);
                    var h = perCell == 1 ? _terrain.HeightAt(px, pz) - 0.6f : _terrain.HeightAt(px, pz); // the coarse ground sits just below the detail
                    verts[z * n + x] = new Vector3(px, h, pz);
                    colours[z * n + x] = ColourAt(px, pz, h);
                }

            var tris = new int[(n - 1) * (n - 1) * 6];
            var t = 0;
            for (var z = 0; z < n - 1; z++)
                for (var x = 0; x < n - 1; x++)
                {
                    var i = z * n + x;
                    tris[t++] = i; tris[t++] = i + n; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + n; tris[t++] = i + n + 1;
                }

            var mesh = new Mesh { name = $"Overland {cx},{cz} ×{perCell}", indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = verts;
            mesh.colors = colours;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            return mesh;
        }

        private static Color BiomeColour(Biome b)
        {
            switch (b)
            {
                case Biome.Grassland: return new Color(0.38f, 0.53f, 0.23f);
                case Biome.Meadow: return new Color(0.47f, 0.58f, 0.26f);
                case Biome.Forest: return new Color(0.25f, 0.40f, 0.17f);
                case Biome.DenseForest: return new Color(0.18f, 0.32f, 0.13f);
                case Biome.Hills: return new Color(0.46f, 0.50f, 0.29f);
                case Biome.Rocky: return new Color(0.50f, 0.48f, 0.42f);
                case Biome.Mountain: return new Color(0.52f, 0.50f, 0.48f);
                case Biome.Marsh: return new Color(0.31f, 0.37f, 0.22f);
                case Biome.Valley: return new Color(0.42f, 0.57f, 0.25f);
                case Biome.Heath: return new Color(0.50f, 0.43f, 0.31f);
                case Biome.Shore: return new Color(0.71f, 0.65f, 0.47f);
                case Biome.Desert: return new Color(0.80f, 0.70f, 0.45f);
                default: return new Color(0.43f, 0.39f, 0.31f); // lake, river and ford beds
            }
        }

        /// <summary>Ground colour: the four nearest cells' biome colours blended, high peaks snowy, a little noise.</summary>
        private Color ColourAt(float x, float z, float h)
        {
            var fx = x / OverlandScale.MetresPerCell - 0.5f;
            var fz = z / OverlandScale.MetresPerCell - 0.5f;
            var ix = Mathf.FloorToInt(fx);
            var iz = Mathf.FloorToInt(fz);
            var tx = fx - ix;
            var tz = fz - iz;
            var c = Color.Lerp(Color.Lerp(BiomeColour(_terrain.Cell(ix, iz).Biome), BiomeColour(_terrain.Cell(ix + 1, iz).Biome), tx),
                Color.Lerp(BiomeColour(_terrain.Cell(ix, iz + 1).Biome), BiomeColour(_terrain.Cell(ix + 1, iz + 1).Biome), tx), tz);
            if (h > OverlandScale.VerticalMetres * 0.95f) c = Color.Lerp(c, new Color(0.92f, 0.93f, 0.95f), Mathf.Clamp01((h - OverlandScale.VerticalMetres * 0.95f) / 40f));
            var shade = 1.32f * (0.92f + 0.16f * _terrain.Noise(x * 0.08f, z * 0.08f)); // brighter than the map's palette: this ground is seen up close, under the day's light
            c = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
            return c.linear; // vertex colours are not converted by Unity in linear space
        }

        private void BuildWater(Transform parent)
        {
            var sx = _terrain.SizeX;
            var sz = _terrain.SizeZ;
            var y = _terrain.WaterHeight;
            var mesh = new Mesh { name = "Overland water" };
            mesh.vertices = new[] { new Vector3(0, y, 0), new Vector3(0, y, sz), new Vector3(sx, y, 0), new Vector3(sx, y, sz) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            var go = MeshObject(parent, "Water", mesh, _water, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>A ribbon 8 m wide along each road, its cells' centres smoothed, laid on the ground every ~5 m.</summary>
        private void BuildRoads(Transform parent, bool ancient)
        {
            var verts = new List<Vector3>();
            var colours = new List<Color>();
            var tris = new List<int>();
            var dirt = (ancient ? new Color(0.42f, 0.36f, 0.42f) : new Color(0.68f, 0.56f, 0.38f)).linear;
            var planks = new Color(0.42f, 0.31f, 0.20f).linear;
            float metres = 0;
            foreach (var road in _terrain.Map.Roads)
            {
                if (road.Cells.Count < 2) continue;
                var pts = new List<Vector2>();
                foreach (var c in road.Cells) { var (x, z) = OverlandScale.CellCentre(c); pts.Add(new Vector2(x, z)); }
                pts = Unstair(pts);
                for (var pass = 0; pass < 2; pass++) pts = Chaikin(pts);
                var line = new List<Vector2> { pts[0] };
                for (var i = 1; i < pts.Count; i++)
                {
                    var a = line[line.Count - 1];
                    var d = Vector2.Distance(a, pts[i]);
                    var steps = Mathf.Max(1, Mathf.CeilToInt(d / 5f));
                    for (var s = 1; s <= steps; s++) line.Add(Vector2.Lerp(a, pts[i], s / (float)steps));
                }

                var start = verts.Count;
                for (var i = 0; i < line.Count; i++)
                {
                    var p = line[i];
                    var dir = (i + 1 < line.Count ? line[i + 1] - p : p - line[i - 1]).normalized;
                    var side = new Vector2(-dir.y, dir.x) * 4f;
                    var cell = _terrain.CellAt(p.x, p.y);
                    var over = cell.Biome == Biome.River || cell.Biome == Biome.Shallows || cell.Biome == Biome.Lake;
                    foreach (var q in new[] { p - side, p + side })
                    {
                        var h = Mathf.Max(_terrain.HeightAt(q.x, q.y), over ? _terrain.WaterHeight + 0.6f : float.MinValue) + 0.18f;
                        verts.Add(new Vector3(q.x, h, q.y));
                        colours.Add(over ? planks : dirt);
                    }

                    if (i > 0)
                    {
                        metres += Vector2.Distance(line[i], line[i - 1]);
                        var b = start + (i - 1) * 2;
                        tris.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
                    }
                }
            }

            RoadKm = metres / 1000f;
            if (verts.Count == 0) return;
            var mesh = new Mesh { name = "Overland roads", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(colours);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            MeshObject(parent, "Roads", mesh, _ground, false).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Drops the corners of a staircase of cells (a road running diagonally across a 4-connected grid), so the smoothed
        /// road runs along the diagonal instead of waving from cell to cell.
        /// </summary>
        private static List<Vector2> Unstair(List<Vector2> p)
        {
            if (p.Count < 3) return p;
            var cell = OverlandScale.MetresPerCell;
            var r = new List<Vector2> { p[0] };
            for (var i = 1; i < p.Count - 1; i++)
            {
                var a = r[r.Count - 1];
                var b = p[i + 1];
                var diagonalStep = Mathf.Abs(a.x - b.x) > 1f && Mathf.Abs(a.y - b.y) > 1f && (b - a).magnitude < 1.5f * cell;
                if (!diagonalStep) r.Add(p[i]);
            }

            r.Add(p[p.Count - 1]);
            return r;
        }

        private static List<Vector2> Chaikin(List<Vector2> p)
        {
            if (p.Count < 3) return p;
            var r = new List<Vector2> { p[0] };
            for (var i = 0; i < p.Count - 1; i++)
            {
                r.Add(Vector2.Lerp(p[i], p[i + 1], 0.25f));
                r.Add(Vector2.Lerp(p[i], p[i + 1], 0.75f));
            }

            r.Add(p[p.Count - 1]);
            return r;
        }

        /// <summary>The region's edge: walls a little inside it (the ways on are taken by their signposts, not by walking off).</summary>
        private void BuildEdges(Transform parent)
        {
            var root = new GameObject("Region edge").transform;
            root.SetParent(parent, false);
            var sx = _terrain.SizeX;
            var sz = _terrain.SizeZ;
            void Wall(Vector3 c, Vector3 size)
            {
                var go = new GameObject("Edge");
                go.transform.SetParent(root, false);
                go.transform.position = c;
                go.AddComponent<BoxCollider>().size = size;
            }

            Wall(new Vector3(sx / 2, 200, 4), new Vector3(sx, 800, 8));
            Wall(new Vector3(sx / 2, 200, sz - 4), new Vector3(sx, 800, 8));
            Wall(new Vector3(4, 200, sz / 2), new Vector3(8, 800, sz));
            Wall(new Vector3(sx - 4, 200, sz / 2), new Vector3(8, 800, sz));
        }

        // ----- Vegetation -------------------------------------------------------------------------------------

        private void LoadTrees(Transform parent, ArtCatalog catalog, RegionKind kind)
        {
            var carrierGo = new GameObject("Vegetation materials (carrier)");
            carrierGo.transform.SetParent(parent, false);
            carrierGo.AddComponent<MeshFilter>();
            _carrier = carrierGo.AddComponent<MeshRenderer>();
            _carrier.enabled = false;
            var materials = new List<Material>();
            var index = new Dictionary<Material, int>();
            var ids = new List<string>();
            for (var n = 1; n <= 5; n++) ids.Add(ModularEnvironmentBuilder.TreeFor(kind, n * 5 + 0)); // the region's own species
            for (var n = 1; n <= 5; n++) ids.Add(ModularEnvironmentBuilder.TreeFor(kind, n * 5 + 35));
            ids.AddRange(new[] { "DeadTree_1", "DeadTree_3", "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3", "Bush_Common", "Bush_Common_Flowers" });
            foreach (var id in ids)
            {
                if (_trees.Exists(t => t.Id == id)) continue;
                var prefab = catalog.SceneryModel(id);
                if (prefab == null) continue;
                var kindDef = new TreeKind { Id = id };
                var root = prefab.transform.worldToLocalMatrix;
                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r = mf.GetComponent<MeshRenderer>();
                    if (mf.sharedMesh == null || r == null) continue;
                    var mats = r.sharedMaterials;
                    for (var s = 0; s < mf.sharedMesh.subMeshCount && s < mats.Length; s++)
                    {
                        if (mats[s] == null) continue;
                        if (!index.TryGetValue(mats[s], out var mi))
                        {
                            var copy = new Material(mats[s]) { name = mats[s].name, enableInstancing = true };
                            _own.Add(copy);
                            mi = materials.Count;
                            materials.Add(copy);
                            index[mats[s]] = mi;
                        }

                        kindDef.Parts.Add((mf.sharedMesh, s, mi, root * mf.transform.localToWorldMatrix));
                    }
                }

                if (kindDef.Parts.Count > 0) _trees.Add(kindDef);
            }

            _carrier.sharedMaterials = materials.ToArray();
        }

        private int KindIndex(string id) => _trees.FindIndex(t => t.Id == id);

        /// <summary>The trees, bushes and rocks of a chunk, deterministic from the cells.</summary>
        private void PlantChunk(Chunk c)
        {
            c.Trees.Clear();
            if (_trees.Count == 0) return;
            var dead = KindIndex("DeadTree_1");
            var rocks = new[] { KindIndex("Rock_Medium_1"), KindIndex("Rock_Medium_2"), KindIndex("Rock_Medium_3") };
            var bush = KindIndex("Bush_Common");
            var flowers = KindIndex("Bush_Common_Flowers");
            for (var z = c.Z * ChunkCells; z < Mathf.Min(_terrain.Map.Depth, (c.Z + 1) * ChunkCells); z++)
                for (var x = c.X * ChunkCells; x < Mathf.Min(_terrain.Map.Width, (c.X + 1) * ChunkCells); x++)
                {
                    var cell = _terrain.Cell(x, z);
                    if (!cell.Passable || cell.Road) continue;
                    int count;
                    switch (cell.Biome)
                    {
                        case Biome.DenseForest: count = 7; break;
                        case Biome.Forest: count = 4; break;
                        case Biome.Hills: case Biome.Valley: count = _terrain.Random01(x, z, 41) < 0.35f ? 1 : 0; break;
                        case Biome.Marsh: count = _terrain.Random01(x, z, 41) < 0.3f ? 1 : 0; break;
                        case Biome.Grassland: case Biome.Meadow: count = _terrain.Random01(x, z, 41) < 0.12f ? 1 : 0; break;
                        default: count = 0; break;
                    }

                    for (var i = 0; i < count; i++)
                    {
                        var px = (x + 0.1f + 0.8f * _terrain.Random01(x, z, 50 + i)) * OverlandScale.MetresPerCell;
                        var pz = (z + 0.1f + 0.8f * _terrain.Random01(x, z, 70 + i)) * OverlandScale.MetresPerCell;
                        int k;
                        if (cell.Biome == Biome.Marsh && dead >= 0) k = dead;
                        else k = (int)(_terrain.Random01(x, z, 90 + i) * 10) % Math.Min(10, _trees.Count);
                        var scale = 0.85f + 0.5f * _terrain.Random01(x, z, 110 + i);
                        var yaw = 360f * _terrain.Random01(x, z, 130 + i);
                        c.Trees.Add((k, Matrix4x4.TRS(new Vector3(px, _terrain.HeightAt(px, pz) - 0.2f, pz), Quaternion.Euler(0, yaw, 0), Vector3.one * scale)));
                    }

                    // Rocks on high and rough ground; bushes and flowers in the open.
                    var r = _terrain.Random01(x, z, 160);
                    var rocky = cell.Biome == Biome.Rocky || cell.Biome == Biome.Heath || cell.Biome == Biome.Hills;
                    var pick = rocky && r < 0.3f ? rocks[(int)(_terrain.Random01(x, z, 161) * 3) % 3]
                        : (cell.Biome == Biome.Grassland || cell.Biome == Biome.Meadow || cell.Biome == Biome.Valley) && r < 0.35f ? (cell.Biome == Biome.Meadow ? flowers : bush) : -1;
                    if (pick >= 0)
                    {
                        var px = (x + 0.5f) * OverlandScale.MetresPerCell + 12f * (_terrain.Random01(x, z, 162) - 0.5f);
                        var pz = (z + 0.5f) * OverlandScale.MetresPerCell + 12f * (_terrain.Random01(x, z, 163) - 0.5f);
                        c.Trees.Add((pick, Matrix4x4.TRS(new Vector3(px, _terrain.HeightAt(px, pz) - 0.1f, pz), Quaternion.Euler(0, 360f * r, 0), Vector3.one * (1f + r))));
                    }
                }
        }

        // ----- Streaming ----------------------------------------------------------------------------------------

        /// <summary>Builds every detailed chunk within the radius now (on arrival: the ground under the player exists before they stand on it).</summary>
        public void Prewarm(Vector3 at)
        {
            foreach (var c in _chunks)
                if (Distance(c, at) < DetailRadiusMetres && c.Detail == null) BuildDetail(c);
        }

        private float Distance(Chunk c, Vector3 at)
        {
            var size = ChunkCells * OverlandScale.MetresPerCell;
            var cx = (c.X + 0.5f) * size;
            var cz = (c.Z + 0.5f) * size;
            var dx = Mathf.Max(0f, Mathf.Abs(at.x - cx) - size / 2);
            var dz = Mathf.Max(0f, Mathf.Abs(at.z - cz) - size / 2);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void BuildDetail(Chunk c)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            c.Detail = MeshObject(transform, $"Chunk {c.X},{c.Z} (detail)", GroundMesh(c.X, c.Z, DetailPerCell), _ground, true);
            c.Coarse.enabled = false;
            BuildBarriers(c);
            PlantChunk(c);
            DetailChunks++;
            LastChunkBuildMs = (float)clock.Elapsed.TotalMilliseconds;
        }

        private void DropDetail(Chunk c)
        {
            var mesh = c.Detail.GetComponent<MeshFilter>().sharedMesh;
            _meshes.Remove(mesh);
            Destroy(mesh);
            BarrierColliders -= c.Detail.GetComponentsInChildren<BoxCollider>().Length;
            Destroy(c.Detail);
            c.Detail = null;
            c.Coarse.enabled = true;
            c.Trees.Clear();
            DetailChunks--;
        }

        /// <summary>Impassable cells touching walkable ones get a tall invisible box (the mountain face, the lake shore).</summary>
        private void BuildBarriers(Chunk c)
        {
            var root = new GameObject("Barriers").transform;
            root.SetParent(c.Detail.transform, false);
            for (var z = c.Z * ChunkCells; z < Mathf.Min(_terrain.Map.Depth, (c.Z + 1) * ChunkCells); z++)
                for (var x = c.X * ChunkCells; x < Mathf.Min(_terrain.Map.Width, (c.X + 1) * ChunkCells); x++)
                {
                    if (_terrain.Cell(x, z).Passable) continue;
                    if (!(_terrain.Cell(x + 1, z).Passable || _terrain.Cell(x - 1, z).Passable || _terrain.Cell(x, z + 1).Passable || _terrain.Cell(x, z - 1).Passable)) continue;
                    var (px, pz) = OverlandScale.CellCentre(new MapCoord(x, z));
                    var go = new GameObject("Barrier");
                    go.transform.SetParent(root, false);
                    go.transform.position = new Vector3(px, _terrain.HeightAt(px, pz) + 20f, pz);
                    go.AddComponent<BoxCollider>().size = new Vector3(OverlandScale.MetresPerCell, 120f, OverlandScale.MetresPerCell);
                    BarrierColliders++;
                }
        }

        private void Update()
        {
            if (Follow == null || _chunks == null) return;
            _streamTimer -= Time.unscaledDeltaTime;
            if (_streamTimer <= 0f)
            {
                _streamTimer = 0.2f;
                Chunk nearestMissing = null;
                var best = float.MaxValue;
                foreach (var c in _chunks)
                {
                    var d = Distance(c, Follow.position);
                    if (c.Detail != null && d > DetailRadiusMetres + 250f) DropDetail(c); // hysteresis, so a chunk at the edge does not flicker
                    else if (c.Detail == null && d < DetailRadiusMetres && d < best) { best = d; nearestMissing = c; }
                }

                if (nearestMissing != null) BuildDetail(nearestMissing); // one a tick: no hitch
            }

            DrawTrees();
        }

        private readonly Dictionary<int, List<Matrix4x4>> _byKind = new Dictionary<int, List<Matrix4x4>>();

        private void DrawTrees()
        {
            if (_trees.Count == 0 || _carrier == null) { TreesDrawn = 0; return; }
            foreach (var l in _byKind.Values) l.Clear();
            var drawn = 0;
            var at = Follow.position;
            foreach (var c in _chunks)
            {
                if (c.Detail == null || Distance(c, at) > TreeRadiusMetres) continue;
                foreach (var (kind, m) in c.Trees)
                {
                    if (!_byKind.TryGetValue(kind, out var list)) _byKind[kind] = list = new List<Matrix4x4>();
                    list.Add(m);
                    drawn++;
                }
            }

            var materials = _carrier.sharedMaterials; // the atmosphere's seasonal copies, once it has adopted them
            foreach (var pair in _byKind)
            {
                if (pair.Value.Count == 0) continue;
                var kind = _trees[pair.Key];
                foreach (var (mesh, sub, mi, local) in kind.Parts)
                {
                    if (mi >= materials.Length || materials[mi] == null) continue;
                    var rp = new RenderParams(materials[mi]) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, layer = gameObject.layer, worldBounds = new Bounds(at, Vector3.one * TreeRadiusMetres * 2.2f) };
                    var buffer = Scratch(pair.Value.Count);
                    for (var i = 0; i < pair.Value.Count; i++) buffer[i] = pair.Value[i] * local;
                    for (var start = 0; start < pair.Value.Count; start += 1023)
                        Graphics.RenderMeshInstanced(rp, mesh, sub, buffer, Mathf.Min(1023, pair.Value.Count - start), start);
                }
            }

            TreesDrawn = drawn;
        }

        private Matrix4x4[] _scratch = new Matrix4x4[1024];

        private Matrix4x4[] Scratch(int n)
        {
            if (_scratch.Length < n) _scratch = new Matrix4x4[Mathf.NextPowerOfTwo(n)];
            return _scratch;
        }

        private void OnDestroy()
        {
            foreach (var m in _meshes) if (m != null) Destroy(m);
            _meshes.Clear();
            if (_ground != null) Destroy(_ground);
            if (_water != null) Destroy(_water);
            foreach (var m in _own) if (m != null) Destroy(m); // the atmosphere destroys its own seasonal copies
        }
    }
}
