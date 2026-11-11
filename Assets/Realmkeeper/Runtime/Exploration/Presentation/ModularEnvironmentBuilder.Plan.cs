using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Exploration.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 10 (ADR-0047, ADR-0049): a planned settlement drawn from its persisted plan — the ground as a terrain mesh
    /// (with its collider) from the plan's height function; roads draped over it and carried over water on bridge decks;
    /// the water; buildings on their floors with plinths where the ground falls away; fields, gardens and yards with
    /// fences; the herds in their pastures; the town wall with its gates; trees and verges. Everything comes from the
    /// plan, so the same plan always gives the same scene.
    /// </summary>
    public static partial class ModularEnvironmentBuilder
    {
        private const float TerrainStep = 2f;
        private const int ChunkCells = 48;

        private static Material Tinted(Context ctx, string baseId, Color colour, string name)
        {
            var key = "tint:" + name;
            if (ctx.Tints.TryGetValue(key, out var m)) return m;
            var source = ctx.Mat(baseId);
            m = ctx.Assets.Track(new Material(source) { name = name });
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
            if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
            ctx.Tints[key] = m;
            return m;
        }

        // ----- Ground -------------------------------------------------------------------------------

        private static void BuildPlanGround(Context ctx)
        {
            var layout = ctx.Layout;
            var plan = layout.Plan;
            var terrain = new GameObject("Terrain").transform;
            terrain.SetParent(ctx.Root, false);
            var extent = plan.HalfExtent + 60f;
            var cells = Mathf.CeilToInt(extent * 2f / TerrainStep);
            var grass = ctx.Mat("Grass");
            var lowest = float.MaxValue;
            for (var cz = 0; cz < cells; cz += ChunkCells)
            for (var cx = 0; cx < cells; cx += ChunkCells)
            {
                var nx = Math.Min(ChunkCells, cells - cx);
                var nz = Math.Min(ChunkCells, cells - cz);
                var verts = new Vector3[(nx + 1) * (nz + 1)];
                var uvs = new Vector2[verts.Length];
                for (var j = 0; j <= nz; j++)
                for (var i = 0; i <= nx; i++)
                {
                    var x = -extent + (cx + i) * TerrainStep;
                    var z = -extent + (cz + j) * TerrainStep;
                    var y = plan.HeightAt(x, z);
                    lowest = Math.Min(lowest, y);
                    verts[j * (nx + 1) + i] = new Vector3(x, y, z);
                    uvs[j * (nx + 1) + i] = new Vector2(x, z);
                }

                var tris = new int[nx * nz * 6];
                var t = 0;
                for (var j = 0; j < nz; j++)
                for (var i = 0; i < nx; i++)
                {
                    var a = j * (nx + 1) + i;
                    var b = a + 1;
                    var c = a + nx + 1;
                    var d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }

                var mesh = ctx.Assets.Track(new Mesh { name = $"Terrain {cx}:{cz}" });
                mesh.vertices = verts;
                mesh.uv = uvs;
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject($"Terrain {cx}:{cz}");
                go.transform.SetParent(terrain, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = grass;
                r.shadowCastingMode = ShadowCastingMode.Off;
                var col = new GameObject($"Terrain collider {cx}:{cz}");
                col.transform.SetParent(ctx.Colliders, false);
                col.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            // The land beyond, to the haze, a little below the plan's lowest ground.
            var far = Mesh(ctx, terrain, "Far ground", MetreMeshes.Plane(extent * 2f + 1400f, extent * 2f + 1400f, ctx.Assets), grass, new Vector3(0f, lowest - 0.6f, 0f));
            far.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            Mesh(ctx, ctx.Static, "Plaza", MetreMeshes.Disc(layout.PlazaRadius, 48, ctx.Assets), ctx.Mat("Cobble"), new Vector3(0f, 0.03f, 0f));
            Mesh(ctx, ctx.Static, "Plaza edge", MetreMeshes.Disc(layout.PlazaRadius + 1.2f, 48, ctx.Assets), ctx.Mat("Gravel"), new Vector3(0f, 0.012f, 0f));

            foreach (var w in plan.Waterways) BuildWater(ctx, w);
            foreach (var road in plan.Roads) BuildRoad(ctx, road);
            foreach (var br in plan.Bridges) BuildBridge(ctx, br);
            foreach (var f in plan.Fields) BuildField(ctx, f);
            BuildHerds(ctx);
        }

        private static Material RoadMaterial(Context ctx, RoadSurface s)
        {
            switch (s)
            {
                case RoadSurface.Stone:
                case RoadSurface.Paved: return ctx.Mat("Cobble");
                case RoadSurface.Gravel: return ctx.Mat("Gravel");
                default: return ctx.Mat("Dirt");
            }
        }

        /// <summary>The height a road's surface lies at: on a bridge's deck over water, else just above the ground.</summary>
        private static float RoadHeight(SettlementLayoutData plan, string roadKey, float x, float z)
        {
            foreach (var br in plan.Bridges)
            {
                if (br.RoadKey != roadKey) continue;
                var d = SettlementTerrain.SegmentDistance(br.From.X, br.From.Z, br.To.X, br.To.Z, x, z, out _, out _);
                if (d <= br.Width) return br.DeckHeight + 0.06f;
            }

            return plan.HeightAt(x, z) + 0.05f;
        }

        /// <summary>A road as a strip that follows the ground, a metre and a half at a time.</summary>
        private static void BuildRoad(Context ctx, RoadData road)
        {
            var plan = ctx.Layout.Plan;
            if (road.Line.Count < 2) return;
            var samples = new List<Vector2>();
            for (var i = 0; i + 1 < road.Line.Count; i++)
            {
                var a = road.Line[i];
                var b = road.Line[i + 1];
                var n = Math.Max(1, Mathf.CeilToInt(a.DistanceTo(b) / 1.5f));
                for (var k = 0; k < n; k++) samples.Add(new Vector2(a.X + (b.X - a.X) * k / n, a.Z + (b.Z - a.Z) * k / n));
            }

            samples.Add(new Vector2(road.Line[road.Line.Count - 1].X, road.Line[road.Line.Count - 1].Z));
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var along = 0f;
            var half = road.Width / 2f;
            for (var i = 0; i < samples.Count; i++)
            {
                var prev = samples[Math.Max(0, i - 1)];
                var next = samples[Math.Min(samples.Count - 1, i + 1)];
                var dir = (next - prev).normalized;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
                var side = new Vector2(-dir.y, dir.x) * half;
                if (i > 0) along += (samples[i] - samples[i - 1]).magnitude;
                var l = samples[i] + side;
                var r = samples[i] - side;
                var y = RoadHeight(plan, road.Key, samples[i].x, samples[i].y);
                var yl = Math.Max(y, plan.HeightAt(l.x, l.y) + 0.04f);
                var yr = Math.Max(y, plan.HeightAt(r.x, r.y) + 0.04f);
                if (y > plan.HeightAt(samples[i].x, samples[i].y) + 0.3f) yl = yr = y; // on a deck: level across
                verts.Add(new Vector3(l.x, yl, l.y));
                verts.Add(new Vector3(r.x, yr, r.y));
                uvs.Add(new Vector2(0f, along));
                uvs.Add(new Vector2(road.Width, along));
                if (i == 0) continue;
                var v = verts.Count - 4;
                tris.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }

            var mesh = ctx.Assets.Track(new Mesh { name = "Road " + road.Key, indexFormat = verts.Count > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16 });
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = Mesh(ctx, ctx.Static, "Road " + road.Key, mesh, RoadMaterial(ctx, road.Surface), Vector3.zero);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>The water: a ribbon at its surface along its course, a little wider than its channel.</summary>
        private static void BuildWater(Context ctx, WaterwayData w)
        {
            var plan = ctx.Layout.Plan;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();
            var along = 0f;
            Vector2? last = null;
            for (var i = 0; i + 1 < w.Line.Count; i++)
            {
                var a = w.Line[i];
                var b = w.Line[i + 1];
                var n = Math.Max(1, Mathf.CeilToInt(a.DistanceTo(b) / 2f));
                var dir = new Vector2(b.X - a.X, b.Z - a.Z).normalized;
                var side = new Vector2(-dir.y, dir.x) * (w.Width / 2f + 0.8f);
                for (var k = (i == 0 ? 0 : 1); k <= n; k++)
                {
                    var p = new Vector2(a.X + (b.X - a.X) * k / n, a.Z + (b.Z - a.Z) * k / n);
                    if (last.HasValue) along += (p - last.Value).magnitude;
                    last = p;
                    var y = SettlementTerrain.WaterSurface(plan, w, p.x, p.y);
                    verts.Add(new Vector3(p.x + side.x, y, p.y + side.y));
                    verts.Add(new Vector3(p.x - side.x, y, p.y - side.y));
                    uvs.Add(new Vector2(0f, along * 0.25f));
                    uvs.Add(new Vector2(1f, along * 0.25f));
                    if (verts.Count < 4) continue;
                    var v = verts.Count - 4;
                    tris.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
                }
            }

            var mesh = ctx.Assets.Track(new Mesh { name = "Water " + w.Key });
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var water = Tinted(ctx, "WindowGlass", w.IsRiver ? new Color(0.20f, 0.33f, 0.40f) : new Color(0.24f, 0.38f, 0.42f), "Water");
            var go = new GameObject("Water " + w.Key);
            go.transform.SetParent(ctx.Root, false); // not merged: one long ribbon
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = water;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>A bridge: a deck from bank to bank with railings and piers (stone or timber), or stepping stones at a ford.</summary>
        private static void BuildBridge(Context ctx, BridgeData br)
        {
            var plan = ctx.Layout.Plan;
            var from = new Vector3(br.From.X, 0f, br.From.Z);
            var to = new Vector3(br.To.X, 0f, br.To.Z);
            var length = Vector3.Distance(from, to) + 1.5f;
            var mid = (from + to) / 2f;
            var dir = (to - from).normalized;
            if (dir.sqrMagnitude < 1e-6f) return;
            var yaw = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
            var root = new GameObject("Bridge " + br.Key).transform;
            root.SetParent(ctx.Static, false);
            root.localPosition = new Vector3(mid.x, br.DeckHeight, mid.z);
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var colliders = new GameObject("Bridge collider " + br.Key).transform;
            colliders.SetParent(ctx.Colliders, false);
            colliders.SetPositionAndRotation(root.position, root.rotation);
            var width = br.Width + 0.6f;
            var mat = ctx.Mat(br.Stone ? "WallStone" : "Planks");
            if (br.IsFord)
            {
                for (var d = -length / 2f + 0.6f; d < length / 2f; d += 0.9f)
                    Mesh(ctx, root, "Stepping stone", Shared(ctx, "Sphere"), ctx.Mat("RockTrim"), new Vector3(((int)(d * 7) % 3 - 1) * 0.25f, -0.25f, d)).transform.localScale = new Vector3(0.7f, 0.35f, 0.6f);
                ColliderBox(colliders, "Ford", new Vector3(0f, -0.2f, 0f), new Vector3(width, 0.3f, length));
                return;
            }

            Mesh(ctx, root, "Deck", MetreMeshes.Box(new Vector3(width, 0.35f, length), ctx.Assets), mat, new Vector3(0f, -0.18f, 0f));
            ColliderBox(colliders, "Deck", new Vector3(0f, -0.18f, 0f), new Vector3(width, 0.35f, length));
            foreach (var s in new[] { -1f, 1f })
            {
                Mesh(ctx, root, "Parapet", MetreMeshes.Box(new Vector3(br.Stone ? 0.4f : 0.15f, br.Stone ? 0.9f : 0.12f, length), ctx.Assets), mat, new Vector3(s * width / 2f, br.Stone ? 0.45f : 0.95f, 0f));
                ColliderBox(colliders, "Parapet", new Vector3(s * width / 2f, 0.5f, 0f), new Vector3(0.3f, 1f, length));
                if (!br.Stone)
                    for (var d = -length / 2f; d <= length / 2f; d += 1.8f)
                        Mesh(ctx, root, "Post", Shared(ctx, "BridgePost", () => MetreMeshes.Box(new Vector3(0.14f, 1.05f, 0.14f), ctx.Assets)), ctx.Mat("Wood"), new Vector3(s * width / 2f, 0.5f, d));
            }

            // Piers down to the bed at each bank and, for a long span, in the middle.
            var spans = Math.Max(1, Mathf.RoundToInt(length / 8f));
            for (var i = 0; i <= spans; i++)
            {
                var d = -length / 2f + length * i / spans;
                var p = root.TransformPoint(new Vector3(0f, 0f, d));
                var ground = plan.HeightAt(p.x, p.z) - 0.6f;
                var h = br.DeckHeight - 0.35f - ground;
                if (h < 0.2f) continue;
                Mesh(ctx, root, "Pier", MetreMeshes.Box(new Vector3(width * 0.8f, h, 0.8f), ctx.Assets), ctx.Mat(br.Stone ? "WallStone" : "Wood"), new Vector3(0f, -0.35f - h / 2f, d));
            }
        }

        // ----- Fields, gardens, yards ---------------------------------------------------------------------

        private static Material FieldMaterial(Context ctx, FieldData f)
        {
            switch (f.Kind)
            {
                case FieldKind.Crop:
                    return f.Crop == "beans" || f.Crop == "turnips" ? Tinted(ctx, "Grass", new Color(0.42f, 0.55f, 0.24f), "Green crop") : Tinted(ctx, "Grass", new Color(0.78f, 0.67f, 0.34f), "Grain");
                case FieldKind.Fallow: return ctx.Mat("Dirt");
                case FieldKind.Pasture: return Tinted(ctx, "Grass", new Color(0.52f, 0.66f, 0.33f), "Pasture");
                case FieldKind.Orchard: return Tinted(ctx, "Grass", new Color(0.40f, 0.55f, 0.28f), "Orchard");
                case FieldKind.Garden: return Tinted(ctx, "Dirt", new Color(0.42f, 0.32f, 0.22f), "Garden soil");
                default: return ctx.Mat("Gravel");
            }
        }

        private static Vector3 FieldPoint(SettlementLayoutData plan, FieldData f, float lx, float lz, float lift)
        {
            var p = SettlementLayoutGenerator.Local(f.Center, f.Yaw, lx, lz);
            return new Vector3(p.X, plan.HeightAt(p.X, p.Z) + lift, p.Z);
        }

        /// <summary>A field as a grid draped over the ground, with rows for crops, trees for an orchard, a fence when it has one.</summary>
        private static void BuildField(Context ctx, FieldData f)
        {
            var plan = ctx.Layout.Plan;
            var nx = Math.Max(1, Mathf.CeilToInt(f.Width / 4f));
            var nz = Math.Max(1, Mathf.CeilToInt(f.Depth / 4f));
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var j = 0; j <= nz; j++)
            for (var i = 0; i <= nx; i++)
            {
                var lx = -f.Width / 2f + f.Width * i / nx;
                var lz = -f.Depth / 2f + f.Depth * j / nz;
                verts.Add(FieldPoint(plan, f, lx, lz, 0.06f));
                uvs.Add(new Vector2(lx, lz));
            }

            for (var j = 0; j < nz; j++)
            for (var i = 0; i < nx; i++)
            {
                var a = j * (nx + 1) + i;
                tris.AddRange(new[] { a, a + nx + 1, a + 1, a + 1, a + nx + 1, a + nx + 2 });
            }

            var mesh = ctx.Assets.Track(new Mesh { name = "Field " + f.Key });
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Mesh(ctx, ctx.Static, "Field " + f.Key, mesh, FieldMaterial(ctx, f), Vector3.zero).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var hash = (int)(SeedMixer.Combine(ctx.Seed, f.Key) % 1000);
            if (f.Kind == FieldKind.Crop || f.Kind == FieldKind.Garden)
            {
                // Rows: low ridges across the field.
                var rowMat = f.Kind == FieldKind.Garden ? ctx.Mat("Foliage") : FieldMaterial(ctx, f);
                var spacing = f.Kind == FieldKind.Garden ? 1.2f : 2.2f;
                for (var lz = -f.Depth / 2f + spacing / 2f; lz < f.Depth / 2f; lz += spacing)
                for (var lx = -f.Width / 2f + 2f; lx < f.Width / 2f - 1f; lx += 4f)
                {
                    var p = FieldPoint(plan, f, lx, lz, 0.12f);
                    var row = Mesh(ctx, ctx.Static, "Row", Shared(ctx, "Row", () => MetreMeshes.Box(new Vector3(4f, 0.25f, 0.5f), ctx.Assets)), rowMat, p);
                    row.transform.localRotation = Quaternion.Euler(0f, -f.Yaw, 0f);
                    row.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            else if (f.Kind == FieldKind.Orchard)
            {
                var trees = 0;
                for (var lz = -f.Depth / 2f + 4f; lz < f.Depth / 2f - 2f && trees < 16; lz += 7f)
                for (var lx = -f.Width / 2f + 4f; lx < f.Width / 2f - 2f && trees < 16; lx += 7f, trees++)
                {
                    var p = FieldPoint(plan, f, lx, lz, 0f);
                    var t = new GameObject("Orchard tree").transform;
                    t.SetParent(ctx.Static, false);
                    t.localPosition = p;
                    t.localScale = Vector3.one * 0.7f;
                    Tree(ctx, t, hash + trees * 31);
                }
            }

            if (f.Fenced) BuildFence(ctx, f);
        }

        /// <summary>Posts and two rails round a field, with a gap for a gate on its front; one collider per side (gate left open).</summary>
        private static void BuildFence(Context ctx, FieldData f)
        {
            var plan = ctx.Layout.Plan;
            var corners = new[] { (-0.5f, -0.5f), (0.5f, -0.5f), (0.5f, 0.5f), (-0.5f, 0.5f) };
            var colliders = new GameObject("Fence colliders " + f.Key).transform;
            colliders.SetParent(ctx.Colliders, false);
            for (var s = 0; s < 4; s++)
            {
                var (ax, az) = corners[s];
                var (bx, bz) = corners[(s + 1) % 4];
                var a = new Vector2(ax * f.Width, az * f.Depth);
                var b = new Vector2(bx * f.Width, bz * f.Depth);
                var length = (b - a).magnitude;
                var n = Math.Max(1, Mathf.CeilToInt(length / 2.5f));
                for (var k = 0; k < n; k++)
                {
                    var t0 = k / (float)n;
                    var t1 = (k + 1) / (float)n;
                    if (s == 0 && Math.Abs((t0 + t1) / 2f - 0.5f) < 1.6f / length) continue; // the gate
                    var p0 = Vector2.Lerp(a, b, t0);
                    var p1 = Vector2.Lerp(a, b, t1);
                    var w0 = FieldPoint(plan, f, p0.x, p0.y, 0f);
                    var w1 = FieldPoint(plan, f, p1.x, p1.y, 0f);
                    Mesh(ctx, ctx.Static, "Fence post", Shared(ctx, "FencePost", () => MetreMeshes.Box(new Vector3(0.14f, 1.2f, 0.14f), ctx.Assets)), ctx.Mat("Bark"), w0 + Vector3.up * 0.6f);
                    var mid = (w0 + w1) / 2f;
                    var span = w1 - w0;
                    var rot = Quaternion.LookRotation(span.normalized, Vector3.up);
                    foreach (var h in new[] { 0.45f, 0.95f })
                    {
                        var rail = Mesh(ctx, ctx.Static, "Fence rail", MetreMeshes.Box(new Vector3(0.06f, 0.1f, span.magnitude), ctx.Assets), ctx.Mat("Wood"), mid + Vector3.up * h);
                        rail.transform.localRotation = rot;
                    }
                }

                // One collider along the side (the gate side split round its gap).
                var wa = FieldPoint(plan, f, a.x, a.y, 0f);
                var wb = FieldPoint(plan, f, b.x, b.y, 0f);
                if (s == 0)
                {
                    var g0 = FieldPoint(plan, f, -1.6f, a.y, 0f);
                    var g1 = FieldPoint(plan, f, 1.6f, a.y, 0f);
                    FenceCollider(colliders, wa, g0);
                    FenceCollider(colliders, g1, wb);
                }
                else FenceCollider(colliders, wa, wb);
            }
        }

        private static void FenceCollider(Transform parent, Vector3 a, Vector3 b)
        {
            var span = b - a;
            if (span.magnitude < 0.3f) return;
            var go = new GameObject("Fence");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) / 2f + Vector3.up * 0.6f;
            go.transform.localRotation = Quaternion.LookRotation(new Vector3(span.x, 0f, span.z).normalized, Vector3.up);
            go.AddComponent<BoxCollider>().size = new Vector3(0.2f, 1.2f + Mathf.Abs(span.y), new Vector2(span.x, span.z).magnitude);
        }

        // ----- Herds ----------------------------------------------------------------------------------------

        /// <summary>
        /// Each herd in its pasture: a few animals (one for every few head, at most a dozen), placed by the herd's own
        /// hash so they stand in the same places each visit. Placeholder forms in proportion: cattle, horses, sheep,
        /// goats, chickens.
        /// </summary>
        private static void BuildHerds(Context ctx)
        {
            var plan = ctx.Layout.Plan;
            foreach (var herd in ctx.Layout.Herds)
            {
                FieldData field = null;
                foreach (var f in plan.Fields) if (f.Key == herd.PastureKey) field = f;
                if (field == null || herd.Count <= 0) continue;
                var shown = Math.Min(12, Math.Max(1, herd.CreatureId == "chicken" ? herd.Count / 2 : herd.Count / 3));
                var rng = new System.Random((int)(SeedMixer.Combine(ctx.Seed, herd.CreatureId + ":" + field.Key) & 0x7FFFFFFF));
                for (var i = 0; i < shown; i++)
                {
                    var lx = (float)(rng.NextDouble() - 0.5) * (field.Width - 3f);
                    var lz = (float)(rng.NextDouble() - 0.5) * (field.Depth - 3f);
                    var p = FieldPoint(plan, field, lx, lz, 0f);
                    var t = new GameObject(herd.CreatureId).transform;
                    t.SetParent(ctx.Static, false);
                    t.localPosition = p;
                    t.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    Animal(ctx, t, herd.CreatureId, rng.Next(3));
                }
            }
        }

        private static void Animal(Context ctx, Transform t, string kind, int variant)
        {
            Vector3 body;
            Color coat;
            switch (kind)
            {
                case "cattle": body = new Vector3(0.75f, 0.8f, 1.7f); coat = variant == 0 ? new Color(0.36f, 0.24f, 0.16f) : variant == 1 ? new Color(0.85f, 0.82f, 0.76f) : new Color(0.2f, 0.17f, 0.15f); break;
                case "draft_horse": case "riding_horse": body = new Vector3(0.6f, 0.85f, 1.8f); coat = variant == 0 ? new Color(0.42f, 0.28f, 0.17f) : new Color(0.25f, 0.2f, 0.16f); break;
                case "sheep": body = new Vector3(0.55f, 0.55f, 0.9f); coat = new Color(0.88f, 0.86f, 0.8f); break;
                case "goat": body = new Vector3(0.4f, 0.5f, 0.8f); coat = variant == 0 ? new Color(0.6f, 0.55f, 0.48f) : new Color(0.9f, 0.88f, 0.84f); break;
                case "chicken": body = new Vector3(0.22f, 0.22f, 0.3f); coat = variant == 0 ? new Color(0.92f, 0.9f, 0.85f) : new Color(0.55f, 0.32f, 0.18f); break;
                default: body = new Vector3(0.5f, 0.5f, 0.9f); coat = new Color(0.45f, 0.38f, 0.3f); break;
            }

            var mat = Tinted(ctx, "Plaster", coat, "Coat " + coat);
            var legs = kind == "chicken" ? 0.12f : body.y * 0.7f;
            Mesh(ctx, t, "Body", Shared(ctx, "AnimalBody:" + kind, () => MetreMeshes.Box(body, ctx.Assets)), mat, new Vector3(0f, legs + body.y / 2f, 0f));
            var head = new Vector3(body.x * 0.7f, body.y * 0.6f, body.z * 0.35f);
            var neck = kind == "draft_horse" || kind == "riding_horse" ? body.y * 0.7f : body.y * 0.25f;
            Mesh(ctx, t, "Head", Shared(ctx, "AnimalHead:" + kind, () => MetreMeshes.Box(head, ctx.Assets)), mat, new Vector3(0f, legs + body.y * 0.75f + neck, body.z / 2f + head.z / 2f - 0.05f));
            if (kind == "chicken") return;
            foreach (var x in new[] { -body.x / 2f + 0.08f, body.x / 2f - 0.08f })
            foreach (var z in new[] { -body.z / 2f + 0.12f, body.z / 2f - 0.12f })
                Mesh(ctx, t, "Leg", Shared(ctx, "AnimalLeg:" + kind, () => MetreMeshes.Box(new Vector3(0.12f, legs, 0.12f), ctx.Assets)), mat, new Vector3(x, legs / 2f, z));
        }

        // ----- Walls, gates and the edge ----------------------------------------------------------------------

        private static bool RoadCrosses(SettlementLayoutData plan, Vector2 a, Vector2 b)
        {
            float Side(Vector2 p, Vector2 q, Vector2 r) => (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
            foreach (var road in plan.Roads)
                for (var i = 0; i + 1 < road.Line.Count; i++)
                {
                    var c = new Vector2(road.Line[i].X, road.Line[i].Z);
                    var d = new Vector2(road.Line[i + 1].X, road.Line[i + 1].Z);
                    if (Side(c, d, a) * Side(c, d, b) < 0 && Side(a, b, c) * Side(a, b, d) < 0) return true;
                }

            return false;
        }

        private static void BuildPlanBoundary(Context ctx)
        {
            var plan = ctx.Layout.Plan;
            var boundary = new GameObject("Boundary").transform;
            boundary.SetParent(ctx.Static, false);
            var colliders = new GameObject("Boundary colliders").transform;
            colliders.SetParent(ctx.Colliders, false);

            // The edge of the walkable land: travellers leave by "Return to World Map".
            var e = plan.HalfExtent - 1f;
            foreach (var (c, s) in new[] { (new Vector3(0f, 10f, e), new Vector3(e * 2f, 40f, 1f)), (new Vector3(0f, 10f, -e), new Vector3(e * 2f, 40f, 1f)),
                         (new Vector3(e, 10f, 0f), new Vector3(1f, 40f, e * 2f)), (new Vector3(-e, 10f, 0f), new Vector3(1f, 40f, e * 2f)) })
                ColliderBox(colliders, "Edge", c, s);

            if (!plan.HasWalls || plan.Wall.Count < 3) return;
            for (var i = 0; i < plan.Wall.Count; i++)
            {
                var a = plan.Wall[i];
                var b = plan.Wall[(i + 1) % plan.Wall.Count];
                var va = new Vector2(a.X, a.Z);
                var vb = new Vector2(b.X, b.Z);
                var mid = (va + vb) / 2f;
                var nearGate = false;
                foreach (var g in plan.Gates) if (Vector2.Distance(new Vector2(g.X, g.Z), mid) < 12f) nearGate = true;
                if (nearGate || RoadCrosses(plan, va, vb)) continue; // a gate, or a postern where a lane or track passes
                if (SettlementTerrain.InWater(plan, mid.x, mid.y, 1f)) continue; // the water is the wall there
                var ga = plan.HeightAt(a.X, a.Z);
                var gb = plan.HeightAt(b.X, b.Z);
                var bottom = Math.Min(ga, gb) - 0.6f;
                var top = Math.Max(ga, gb) + 4.6f;
                var length = Vector2.Distance(va, vb) + 0.8f;
                var centre = new Vector3(mid.x, (bottom + top) / 2f, mid.y);
                var size = new Vector3(1.4f, top - bottom, length);
                var yaw = Quaternion.LookRotation(new Vector3(vb.x - va.x, 0f, vb.y - va.y).normalized, Vector3.up);
                var wall = Mesh(ctx, boundary, "Wall", MetreMeshes.Box(size, ctx.Assets), ctx.Mat("WallStone"), centre);
                wall.transform.localRotation = yaw;
                var col = new GameObject("Wall");
                col.transform.SetParent(colliders, false);
                col.transform.SetPositionAndRotation(centre, yaw);
                col.AddComponent<BoxCollider>().size = size;
                for (var d = -length / 2f + 0.6f; d < length / 2f - 0.4f; d += 1.6f)
                {
                    var merlon = Mesh(ctx, boundary, "Merlon", Shared(ctx, "Merlon"), ctx.Mat("WallStone"), centre + yaw * new Vector3(0f, size.y / 2f + 0.4f, d));
                    merlon.transform.localRotation = yaw;
                }
            }
        }

        /// <summary>A gatehouse over a main road: two towers either side of the way and an arch over it (the way stays open).</summary>
        private static void BuildGatehouse(Context ctx, LayoutBuilding b)
        {
            var plan = ctx.Layout.Plan;
            var root = new GameObject(b.Key + " (gatehouse)").transform;
            root.SetParent(ctx.Static, false);
            root.localPosition = new Vector3(b.Center.X, plan.HeightAt(b.Center.X, b.Center.Z), b.Center.Z);
            root.localRotation = Quaternion.Euler(0f, -b.Yaw, 0f);
            var colliders = new GameObject(b.Key + " colliders").transform;
            colliders.SetParent(ctx.Colliders, false);
            colliders.SetPositionAndRotation(root.position, root.rotation);
            const float opening = 7.5f;
            const float tower = 4f;
            var height = b.Height + 2f;
            foreach (var s in new[] { -1f, 1f })
            {
                var c = new Vector3(s * (opening / 2f + tower / 2f), height / 2f - 1f, 0f);
                Mesh(ctx, root, "Tower", MetreMeshes.Box(new Vector3(tower, height + 2f, tower), ctx.Assets), ctx.Mat("WallStone"), c);
                ColliderBox(colliders, "Tower", c, new Vector3(tower, height + 2f, tower));
                for (var k = -1; k <= 1; k++)
                    Mesh(ctx, root, "Merlon", Shared(ctx, "Merlon"), ctx.Mat("WallStone"), new Vector3(c.x + k * 1.3f, height + 0.4f, -tower / 2f + 0.3f));
            }

            Mesh(ctx, root, "Arch", MetreMeshes.Box(new Vector3(opening + 0.4f, 2f, tower), ctx.Assets), ctx.Mat("WallStone"), new Vector3(0f, height - 1.5f, 0f));
            ColliderBox(colliders, "Arch", new Vector3(0f, height - 1.5f, 0f), new Vector3(opening + 0.4f, 2f, tower));
        }

        /// <summary>A plinth under a building where the ground falls away beneath its floor.</summary>
        private static void BuildPlinth(Context ctx, LayoutBuilding b, Transform building)
        {
            if (b.Foundation < 0.25f) return;
            var h = b.Foundation + 0.4f;
            Mesh(ctx, building, "Plinth", MetreMeshes.Box(new Vector3(b.Width + 0.3f, h, b.Depth + 0.3f), ctx.Assets), ctx.Mat("RockTrim"), new Vector3(0f, -h / 2f + 0.02f, 0f));
            if (b.Foundation > 0.6f)
                Mesh(ctx, building, "Steps", MetreMeshes.Box(new Vector3(1.6f, b.Foundation * 0.6f, 1.2f), ctx.Assets), ctx.Mat("RockTrim"), new Vector3(0f, -b.Foundation * 0.7f, -b.Depth / 2f - 0.75f));
        }

        // ----- Verges --------------------------------------------------------------------------------------

        /// <summary>Stones and grass tufts on the verges, on the ground, clear of the roads, buildings and water.</summary>
        private static void BuildPlanDressing(Context ctx)
        {
            var layout = ctx.Layout;
            var plan = layout.Plan;
            var dress = new GameObject("Street dressing").transform;
            dress.SetParent(ctx.Static, false);
            var rng = new System.Random((int)(ctx.Seed & 0x7FFFFFFF));
            var tuft = Shared(ctx, "Sphere");
            var half = plan.CoreRadius + 30f;
            for (var i = 0; i < 260; i++)
            {
                var x = (float)(rng.NextDouble() * 2 - 1) * half;
                var z = (float)(rng.NextDouble() * 2 - 1) * half;
                if (new Vector2(x, z).magnitude < layout.PlazaRadius + 2f || InsideAnyBuilding(ctx, x, z) || SettlementTerrain.InWater(plan, x, z, 1f)) continue;
                var onRoad = false;
                foreach (var s in layout.Streets)
                    if (SettlementTerrain.SegmentDistance(s.From.X, s.From.Z, s.To.X, s.To.Z, x, z, out _, out _) < s.Width / 2f + 0.4f) { onRoad = true; break; }
                if (onRoad) continue;
                var stone = i % 3 == 0;
                var size = stone ? 0.15f + (float)rng.NextDouble() * 0.2f : 0.35f + (float)rng.NextDouble() * 0.3f;
                var y = plan.HeightAt(x, z);
                var piece = Mesh(ctx, dress, stone ? "Stone" : "Tuft", tuft, ctx.Mat(stone ? "RockTrim" : "Foliage"), new Vector3(x, y + (stone ? size * 0.25f : 0.05f), z));
                piece.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                piece.transform.localScale = stone ? new Vector3(size, size * 0.6f, size * 0.8f) : new Vector3(size, size * 0.45f, size);
            }
        }
    }
}
