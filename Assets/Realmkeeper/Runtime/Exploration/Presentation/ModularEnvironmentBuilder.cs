using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Exploration.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Builds a settlement's local environment from its engine-free layout with the modular art kit:
    /// textured ground, cobbled streets with stone kerbs, a paved plaza, kit-assembled buildings
    /// (<see cref="BuildingKitPlanner"/>), the tavern interior, town walls with a gatehouse or a
    /// village fence, dressed props, and set dressing placed deterministically from the layout.
    /// Collision stays simple: one box per building (the tavern's walls leave its doorway open),
    /// boxes for props and walls, none on detail. Static geometry is batched after building.
    /// Same layout in, same scene out.
    /// </summary>
    public static class ModularEnvironmentBuilder
    {
        public static GameObject Build(LocalEnvironmentLayout layout, ArtCatalog catalog)
        {
            var root = new GameObject("Environment " + layout.EnvironmentKey);
            var assets = root.AddComponent<GeneratedAssets>();
            var ctx = new Context(root.transform, layout, catalog, assets);

            BuildGround(ctx);
            foreach (var b in layout.Buildings) BuildBuilding(ctx, b);
            BuildBoundary(ctx);
            foreach (var p in layout.Props) BuildProp(ctx, p);
            BuildTavernInterior(ctx);
            BuildStreetDressing(ctx);

            // Merge the thousands of kit pieces into a few hundred draws (one per cell, material and shadow mode).
            MeshMerger.Merge(ctx.Static, assets);
            return root;
        }

        /// <summary>Build-time state shared by the steps.</summary>
        private sealed class Context
        {
            public Context(Transform root, LocalEnvironmentLayout layout, ArtCatalog catalog, GeneratedAssets assets)
            {
                Root = root;
                Layout = layout;
                Catalog = catalog;
                Assets = assets;
                Static = new GameObject("Static").transform;
                Static.SetParent(root, false);
                Colliders = new GameObject("Colliders").transform;
                Colliders.SetParent(root, false);
                Seed = SeedMixer.Combine(SeedMixer.HashString("dressing"), layout.SettlementId.ToString());
            }

            public Transform Root { get; }
            public Transform Static { get; }
            public Transform Colliders { get; }
            public LocalEnvironmentLayout Layout { get; }
            public ArtCatalog Catalog { get; }
            public GeneratedAssets Assets { get; }
            public ulong Seed { get; }
            public readonly Dictionary<string, BuildingPlan> Plans = new Dictionary<string, BuildingPlan>(StringComparer.Ordinal);

            public Material Mat(string id) => Catalog.Material(id) ?? Catalog.Material("Plaster");
        }

        // ----- Ground ------------------------------------------------------------------------------

        private static void BuildGround(Context ctx)
        {
            var layout = ctx.Layout;
            var size = layout.HalfExtent * 2f + 1400f; // to the haze: no visible edge
            var ground = Mesh(ctx, ctx.Static, "Ground", MetreMeshes.Plane(size, size, ctx.Assets), ctx.Mat("Grass"), new Vector3(0f, 0f, 0f));
            var groundCollider = new GameObject("Ground collider");
            groundCollider.transform.SetParent(ctx.Colliders, false);
            groundCollider.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            groundCollider.AddComponent<BoxCollider>().size = new Vector3(size, 1f, size);
            ground.layer = 0;

            foreach (var s in layout.Streets)
            {
                var from = new Vector3(s.From.X, 0f, s.From.Z);
                var to = new Vector3(s.To.X, 0f, s.To.Z);
                var length = Vector3.Distance(from, to);
                var mid = (from + to) * 0.5f;
                var rot = Quaternion.LookRotation((to - from).normalized, Vector3.up);
                var street = Mesh(ctx, ctx.Static, "Street", MetreMeshes.Plane(s.Width, length, ctx.Assets, mid.x, mid.z), ctx.Mat("Cobble"), mid + Vector3.up * 0.02f);
                street.transform.localRotation = rot;
                Kerbs(ctx, from, to, s.Width, layout.PlazaRadius);
            }

            var road = Mesh(ctx, ctx.Static, "Approach road", MetreMeshes.Plane(4.5f, 60f, ctx.Assets), ctx.Mat("Dirt"), new Vector3(0f, 0.015f, -layout.HalfExtent - 30f));
            road.name = "Approach road";
            Mesh(ctx, ctx.Static, "Plaza", MetreMeshes.Disc(layout.PlazaRadius, 48, ctx.Assets), ctx.Mat("Cobble"), new Vector3(0f, 0.03f, 0f));
            Mesh(ctx, ctx.Static, "Plaza edge", MetreMeshes.Disc(layout.PlazaRadius + 1.2f, 48, ctx.Assets), ctx.Mat("Gravel"), new Vector3(0f, 0.012f, 0f));
        }

        /// <summary>Stone kerbs along both edges of a street, outside the plaza.</summary>
        private static void Kerbs(Context ctx, Vector3 from, Vector3 to, float width, float plaza)
        {
            var dir = (to - from).normalized;
            var right = Vector3.Cross(Vector3.up, dir);
            var length = Vector3.Distance(from, to);
            var yaw = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
            for (var d = 1f; d < length; d += 2f)
            {
                var centre = from + dir * d;
                if (centre.magnitude < plaza + 1.5f) continue;
                foreach (var side in new[] { -1f, 1f })
                {
                    // The kerb module lies along its X axis and extends 0.7 m toward −Z (away from the street).
                    var p = centre + right * side * (width / 2f);
                    Module(ctx, ctx.Static, "Prop_ExteriorBorder_Straight1", p + Vector3.up * 0.01f, yaw + (side > 0 ? -90f : 90f), Vector3.one);
                }
            }
        }

        // ----- Buildings -----------------------------------------------------------------------------

        private static void BuildBuilding(Context ctx, LayoutBuilding b)
        {
            var plan = BuildingKitPlanner.Plan(b, ctx.Layout.Type, ctx.Layout.SettlementId.ToString());
            ctx.Plans[b.Key] = plan;
            var go = new GameObject(b.Key + " (" + plan.Archetype + ")");
            go.transform.SetParent(ctx.Static, false);
            go.transform.localPosition = new Vector3(b.Center.X, 0f, b.Center.Z);
            go.transform.localRotation = Quaternion.Euler(0f, -b.Yaw, 0f); // door on local −Z faces (sin yaw, −cos yaw)
            foreach (var piece in plan.Pieces) Piece(ctx, go.transform, piece);

            // Collision: the walls' volume. The tavern keeps its doorway open and its interior walkable.
            var colliders = new GameObject(b.Key + " colliders").transform;
            colliders.SetParent(ctx.Colliders, false);
            colliders.SetPositionAndRotation(go.transform.position, go.transform.rotation);
            if (plan.DoorwayWidth > 0f) OpenColliders(colliders, plan); // the tavern and the shops
            else ColliderBox(colliders, "Walls", new Vector3(0f, plan.WallHeight / 2f, 0f), new Vector3(plan.Width + 0.1f, plan.WallHeight, plan.Depth + 0.1f));
        }

        /// <summary>Walls with the doorway left open, and a ceiling: an enterable building's collision.</summary>
        private static void OpenColliders(Transform t, BuildingPlan plan)
        {
            const float thick = 0.4f;
            var w = plan.Width;
            var d = plan.Depth;
            var h = plan.WallHeight;
            var dx = plan.DoorwayX;
            var ceiling = plan.InteriorHeight > 0f ? plan.InteriorHeight : h;
            ColliderBox(t, "Back wall", new Vector3(0f, h / 2f, d / 2f), new Vector3(w, h, thick));
            ColliderBox(t, "West wall", new Vector3(-w / 2f, h / 2f, 0f), new Vector3(thick, h, d));
            ColliderBox(t, "East wall", new Vector3(w / 2f, h / 2f, 0f), new Vector3(thick, h, d));
            var gap = plan.DoorwayWidth;
            var west = (w / 2f + dx) - gap / 2f;  // wall from the west corner to the doorway
            var east = (w / 2f - dx) - gap / 2f;
            if (west > 0.01f) ColliderBox(t, "Front wall W", new Vector3(-w / 2f + west / 2f, h / 2f, -d / 2f), new Vector3(west, h, thick));
            if (east > 0.01f) ColliderBox(t, "Front wall E", new Vector3(w / 2f - east / 2f, h / 2f, -d / 2f), new Vector3(east, h, thick));
            ColliderBox(t, "Lintel", new Vector3(dx, (2.7f + h) / 2f, -d / 2f), new Vector3(gap, h - 2.7f, thick));
            ColliderBox(t, "Ceiling", new Vector3(0f, ceiling + 0.1f, 0f), new Vector3(w, 0.2f, d));
        }

        // ----- Pieces --------------------------------------------------------------------------------

        private static void Piece(Context ctx, Transform parent, ModulePlacement p)
        {
            var pos = new Vector3(p.X, p.Y, p.Z);
            var scale = new Vector3(p.ScaleX, p.ScaleY, p.ScaleZ);
            if (!p.IsProcedural)
            {
                Module(ctx, parent, p.ModuleId, pos, p.Yaw, scale);
                return;
            }

            var go = new GameObject(p.ModuleId.Substring(1));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
            var t = go.transform;
            var hash = (int)(SeedMixer.Combine(ctx.Seed, parent.name + p) % 1000);
            switch (p.ModuleId)
            {
                case "@Awning":
                    var cloth = ctx.Mat(new[] { "CanvasRed", "CanvasBlue", "Canvas" }[hash % 3]);
                    var awning = Mesh(ctx, t, "Cloth", MetreMeshes.Box(new Vector3(2.2f, 0.05f, 1.5f), ctx.Assets), cloth, new Vector3(0f, 0f, -0.7f));
                    awning.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
                    Mesh(ctx, t, "Post L", MetreMeshes.Box(new Vector3(0.08f, 2.3f, 0.08f), ctx.Assets), ctx.Mat("Wood"), new Vector3(-1.05f, -1.35f, -1.35f));
                    Mesh(ctx, t, "Post R", MetreMeshes.Box(new Vector3(0.08f, 2.3f, 0.08f), ctx.Assets), ctx.Mat("Wood"), new Vector3(1.05f, -1.35f, -1.35f));
                    break;
                case "@Ceiling":
                    var ceiling = Mesh(ctx, t, "Ceiling", MetreMeshes.Box(new Vector3(p.ScaleX, p.ScaleY, p.ScaleZ), ctx.Assets), ctx.Mat("Planks"), Vector3.zero);
                    ceiling.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                    break;
                case "@Counter":
                    Mesh(ctx, t, "Counter", MetreMeshes.Box(new Vector3(1.7f, 1f, 0.5f), ctx.Assets), ctx.Mat("Planks"), new Vector3(0f, 0.5f, 0f));
                    Mesh(ctx, t, "Goods", MetreMeshes.Box(new Vector3(0.5f, 0.22f, 0.35f), ctx.Assets), ctx.Mat("Canvas"), new Vector3(-0.4f, 1.11f, 0f));
                    Mesh(ctx, t, "Goods 2", MetreMeshes.Box(new Vector3(0.35f, 0.3f, 0.3f), ctx.Assets), ctx.Mat("CanvasRed"), new Vector3(0.35f, 1.15f, 0.02f));
                    break;
                case "@FlowerBox":
                    Mesh(ctx, t, "Box", MetreMeshes.Box(new Vector3(1.1f, 0.22f, 0.28f), ctx.Assets), ctx.Mat("Wood"), new Vector3(0f, 0f, 0f));
                    for (var i = 0; i < 5; i++)
                    {
                        var bloom = Mesh(ctx, t, "Flower", Shared(ctx, "Sphere"), ctx.Mat(i % 2 == 0 ? "Flowers" : "Foliage"),
                            new Vector3(-0.44f + i * 0.22f, 0.17f, (i % 2) * 0.04f));
                        bloom.transform.localScale = new Vector3(0.24f, 0.2f, 0.2f);
                    }

                    break;
                case "@Barrel":
                    Barrel(ctx, t, Vector3.zero, 1f);
                    break;
                case "@Sign":
                    Mesh(ctx, t, "Bracket", MetreMeshes.Box(new Vector3(0.06f, 0.06f, 0.7f), ctx.Assets), ctx.Mat("Metal"), new Vector3(0f, 0.35f, -0.05f));
                    Mesh(ctx, t, "Board", MetreMeshes.Box(new Vector3(0.06f, 0.5f, 0.62f), ctx.Assets), ctx.Mat("Planks"), new Vector3(0f, 0f, -0.1f))
                        .transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
                case "@Lantern":
                    Lantern(ctx, t, Vector3.zero);
                    break;
                case "@Gallery":
                    Mesh(ctx, t, "Gallery", MetreMeshes.Box(new Vector3(p.ScaleX, 1.4f, 2.2f), ctx.Assets), ctx.Mat("WallStone"), new Vector3(0f, 0.7f, 0f));
                    Mesh(ctx, t, "Gallery roof", MetreMeshes.Box(new Vector3(p.ScaleX + 0.6f, 0.2f, 2.8f), ctx.Assets), ctx.Mat("Planks"), new Vector3(0f, 2.6f, 0f));
                    for (var x = -p.ScaleX / 2f + 0.3f; x <= p.ScaleX / 2f; x += 1.2f)
                        Mesh(ctx, t, "Merlon", MetreMeshes.Box(new Vector3(0.6f, 0.8f, 0.5f), ctx.Assets), ctx.Mat("WallStone"), new Vector3(x, 1.8f, -0.9f));
                    break;
            }
        }

        /// <summary>Instantiates a kit module, keeping the model's own root rotation and scale (the Blender export frame).</summary>
        private static GameObject Module(Context ctx, Transform parent, string id, Vector3 localPosition, float yaw, Vector3 scale)
        {
            var entry = ctx.Catalog.Module(id);
            if (entry == null)
            {
                Debug.LogWarning("Realmkeeper: kit module " + id + " is not in the art catalog.");
                return null;
            }

            var prefabRoot = entry.Prefab.transform;
            Transform holder = parent;
            if (scale != Vector3.one)
            {
                var pivot = new GameObject(id + " pivot").transform;
                pivot.SetParent(parent, false);
                pivot.localPosition = localPosition;
                pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
                pivot.localScale = scale;
                holder = pivot;
                localPosition = Vector3.zero;
                yaw = 0f;
            }

            var go = Object.Instantiate(entry.Prefab, holder, false);
            go.name = id;
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prefabRoot.localRotation;
            go.transform.localScale = prefabRoot.localScale;
            return go;
        }

        // ----- Boundary ------------------------------------------------------------------------------

        private static void BuildBoundary(Context ctx)
        {
            var layout = ctx.Layout;
            var half = layout.HalfExtent;
            const float gate = 6f;
            var boundary = new GameObject("Boundary").transform;
            boundary.SetParent(ctx.Static, false);
            var colliders = new GameObject("Boundary colliders").transform;
            colliders.SetParent(ctx.Colliders, false);

            if (layout.HasWalls)
            {
                const float height = 4.5f;
                const float thick = 1.2f;
                var c = half + thick / 2f;
                var span = half * 2f + thick * 2f;
                var sideLength = (span - gate) / 2f;
                void WallRun(string name, Vector3 centre, Vector3 size)
                {
                    Mesh(ctx, boundary, name, MetreMeshes.Box(size, ctx.Assets), ctx.Mat("WallStone"), centre);
                    ColliderBox(colliders, name, centre, size);
                    var alongX = size.x > size.z;
                    var length = alongX ? size.x : size.z;
                    for (var d = -length / 2f + 0.5f; d <= length / 2f - 0.4f; d += 1.6f)
                    {
                        var offset = alongX ? new Vector3(d, 0f, 0f) : new Vector3(0f, 0f, d);
                        Mesh(ctx, boundary, "Merlon", Shared(ctx, "Merlon"), ctx.Mat("WallStone"), centre + offset + Vector3.up * (size.y / 2f + 0.4f));
                    }
                }

                WallRun("North wall", new Vector3(0f, height / 2f, c), new Vector3(span, height, thick));
                WallRun("West wall", new Vector3(-c, height / 2f, 0f), new Vector3(thick, height, span));
                WallRun("East wall", new Vector3(c, height / 2f, 0f), new Vector3(thick, height, span));
                WallRun("South wall W", new Vector3(-(gate / 2f + sideLength / 2f), height / 2f, -c), new Vector3(sideLength, height, thick));
                WallRun("South wall E", new Vector3(gate / 2f + sideLength / 2f, height / 2f, -c), new Vector3(sideLength, height, thick));

                var gatehouse = BuildingKitPlanner.Gatehouse(gate, -c);
                var gh = new GameObject("Gatehouse").transform;
                gh.SetParent(boundary, false);
                foreach (var piece in gatehouse.Pieces) Piece(ctx, gh, piece);
                foreach (var sign in new[] { -1f, 1f })
                    ColliderBox(colliders, "Gate tower", new Vector3(sign * (gate / 2f + 2f), gatehouse.WallHeight / 2f, -c), new Vector3(4.2f, gatehouse.WallHeight, 4.2f));
                BarrierAt(colliders, gate, thick, -c);
            }
            else
            {
                var c = half + 0.2f;
                void FenceRun(Vector3 from, Vector3 to)
                {
                    var dir = (to - from);
                    var length = dir.magnitude;
                    dir /= length;
                    var yaw = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up).eulerAngles.y;
                    for (var d = 1.03f; d < length; d += 2.06f)
                        Module(ctx, boundary, "Prop_WoodenFence_Single", from + dir * d, yaw, Vector3.one);
                    var mid = (from + to) / 2f;
                    ColliderBox(colliders, "Fence", mid + Vector3.up * 0.55f, new Vector3(Mathf.Abs(dir.x) * length + 0.25f, 1.1f, Mathf.Abs(dir.z) * length + 0.25f));
                }

                FenceRun(new Vector3(-c, 0f, c), new Vector3(c, 0f, c));
                FenceRun(new Vector3(-c, 0f, -c), new Vector3(-c, 0f, c));
                FenceRun(new Vector3(c, 0f, -c), new Vector3(c, 0f, c));
                FenceRun(new Vector3(-c, 0f, -c), new Vector3(-gate / 2f, 0f, -c));
                FenceRun(new Vector3(gate / 2f, 0f, -c), new Vector3(c, 0f, -c));
                foreach (var sign in new[] { -1f, 1f })
                {
                    var post = Mesh(ctx, boundary, "Gate post", MetreMeshes.Box(new Vector3(0.35f, 2.8f, 0.35f), ctx.Assets), ctx.Mat("Bark"), new Vector3(sign * gate / 2f, 1.4f, -c));
                    ColliderBox(colliders, "Gate post", post.transform.localPosition, new Vector3(0.35f, 2.8f, 0.35f));
                }

                Mesh(ctx, boundary, "Gate beam", MetreMeshes.Box(new Vector3(gate + 0.6f, 0.3f, 0.3f), ctx.Assets), ctx.Mat("Bark"), new Vector3(0f, 2.75f, -c));
                BarrierAt(colliders, gate, 0.3f, -c);
            }
        }

        /// <summary>
        /// The gate is open to look at but the walkable area ends here: leaving is done through
        /// "Return to World Map", which keeps the character's position valid in world data.
        /// </summary>
        private static void BarrierAt(Transform parent, float gate, float thick, float z)
        {
            ColliderBox(parent, "Gate barrier", new Vector3(0f, 2f, z), new Vector3(gate, 4f, thick));
        }

        // ----- Props -----------------------------------------------------------------------------------

        private static void BuildProp(Context ctx, LayoutProp p)
        {
            var go = new GameObject(p.Kind.ToString());
            go.transform.SetParent(ctx.Static, false);
            go.transform.localPosition = new Vector3(p.Position.X, 0f, p.Position.Z);
            go.transform.localRotation = Quaternion.Euler(0f, -p.Yaw, 0f);
            var t = go.transform;
            var colliders = new GameObject(p.Kind + " collider").transform;
            colliders.SetParent(ctx.Colliders, false);
            colliders.SetPositionAndRotation(t.position, t.rotation);
            var hash = (int)(SeedMixer.Combine(ctx.Seed, p.Kind + ":" + p.Position.X.ToString("F2") + ":" + p.Position.Z.ToString("F2")) % 1000);

            switch (p.Kind)
            {
                case PropKind.Well:
                    Mesh(ctx, t, "Ring", MetreMeshes.Lathe(new[] { new Vector2(0.95f, 0f), new Vector2(0.95f, 0.85f), new Vector2(0.75f, 0.85f) }, 20, ctx.Assets), ctx.Mat("RockTrim"), Vector3.zero);
                    Mesh(ctx, t, "Water", MetreMeshes.Disc(0.75f, 20, ctx.Assets), ctx.Mat("WindowGlass"), new Vector3(0f, 0.55f, 0f));
                    Mesh(ctx, t, "Post W", MetreMeshes.Box(new Vector3(0.16f, 2.4f, 0.16f), ctx.Assets), ctx.Mat("Bark"), new Vector3(-0.85f, 1.2f, 0f));
                    Mesh(ctx, t, "Post E", MetreMeshes.Box(new Vector3(0.16f, 2.4f, 0.16f), ctx.Assets), ctx.Mat("Bark"), new Vector3(0.85f, 1.2f, 0f));
                    Mesh(ctx, t, "Axle", MetreMeshes.Box(new Vector3(1.9f, 0.1f, 0.1f), ctx.Assets), ctx.Mat("Wood"), new Vector3(0f, 1.7f, 0f));
                    Module(ctx, t, "Roof_RoundTiles_4x4", new Vector3(0f, 2.3f, 0f), 0f, new Vector3(0.5f, 0.32f, 0.5f));
                    ColliderBox(colliders, "Well", new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 1.2f, 1.9f));
                    break;
                case PropKind.MarketStall:
                    Mesh(ctx, t, "Counter", MetreMeshes.Box(new Vector3(2.6f, 0.95f, 1f), ctx.Assets), ctx.Mat("Planks"), new Vector3(0f, 0.475f, 0f));
                    Mesh(ctx, t, "Post L", MetreMeshes.Box(new Vector3(0.1f, 2.5f, 0.1f), ctx.Assets), ctx.Mat("Bark"), new Vector3(-1.25f, 1.25f, 0.45f));
                    Mesh(ctx, t, "Post R", MetreMeshes.Box(new Vector3(0.1f, 2.5f, 0.1f), ctx.Assets), ctx.Mat("Bark"), new Vector3(1.25f, 1.25f, 0.45f));
                    Mesh(ctx, t, "Post FL", MetreMeshes.Box(new Vector3(0.08f, 2.1f, 0.08f), ctx.Assets), ctx.Mat("Bark"), new Vector3(-1.25f, 1.05f, -0.55f));
                    Mesh(ctx, t, "Post FR", MetreMeshes.Box(new Vector3(0.08f, 2.1f, 0.08f), ctx.Assets), ctx.Mat("Bark"), new Vector3(1.25f, 1.05f, -0.55f));
                    var awning = Mesh(ctx, t, "Awning", MetreMeshes.Box(new Vector3(2.9f, 0.05f, 1.5f), ctx.Assets),
                        ctx.Mat(new[] { "CanvasRed", "CanvasBlue", "Canvas" }[hash % 3]), new Vector3(0f, 2.3f, -0.05f));
                    awning.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
                    for (var i = 0; i < 3; i++)
                    {
                        var goods = Mesh(ctx, t, "Goods", MetreMeshes.Box(new Vector3(0.55f, 0.22f, 0.4f), ctx.Assets),
                            ctx.Mat(new[] { "Flowers", "Foliage", "Canvas", "CanvasRed" }[(hash + i) % 4]), new Vector3(-0.8f + i * 0.8f, 1.06f, -0.1f));
                        goods.transform.localRotation = Quaternion.Euler(0f, (hash + i * 37) % 20 - 10f, 0f);
                    }

                    Module(ctx, t, "Prop_Crate", new Vector3(1.6f, 0f, 0.3f), hash % 30, new Vector3(0.6f, 0.6f, 0.6f));
                    Barrel(ctx, t, new Vector3(-1.65f, 0f, 0.35f), 0.85f);
                    ColliderBox(colliders, "Stall", new Vector3(0f, 0.6f, 0f), new Vector3(2.8f, 1.2f, 1.1f));
                    break;
                case PropKind.Barrel:
                    Barrel(ctx, t, Vector3.zero, 1f);
                    ColliderBox(colliders, "Barrel", new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 0.7f));
                    break;
                case PropKind.Crate:
                    Module(ctx, t, "Prop_Crate", Vector3.zero, 0f, new Vector3(0.75f, 0.75f, 0.75f));
                    ColliderBox(colliders, "Crate", new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f));
                    break;
                case PropKind.LampPost:
                    Mesh(ctx, t, "Post", MetreMeshes.Lathe(new[] { new Vector2(0.14f, 0f), new Vector2(0.11f, 0.3f), new Vector2(0.07f, 0.35f), new Vector2(0.06f, 3.1f) }, 8, ctx.Assets),
                        ctx.Mat("Metal"), Vector3.zero);
                    Mesh(ctx, t, "Arm", MetreMeshes.Box(new Vector3(0.05f, 0.05f, 0.6f), ctx.Assets), ctx.Mat("Metal"), new Vector3(0f, 3.05f, -0.28f));
                    Lantern(ctx, t, new Vector3(0f, 2.75f, -0.55f));
                    ColliderBox(colliders, "Post", new Vector3(0f, 1.6f, 0f), new Vector3(0.2f, 3.2f, 0.2f));
                    break;
                case PropKind.Tree:
                    Tree(ctx, t, hash);
                    ColliderBox(colliders, "Trunk", new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 3f, 0.5f));
                    break;
                case PropKind.Table:
                    Table(ctx, t, Vector3.zero, colliders);
                    break;
                case PropKind.Bench:
                    Bench(ctx, t, Vector3.zero, 2.2f, colliders);
                    break;
                case PropKind.Hearth:
                    Mesh(ctx, t, "Hearth", MetreMeshes.Box(new Vector3(2.2f, 1.25f, 1f), ctx.Assets), ctx.Mat("RockTrim"), new Vector3(0f, 0.625f, 0f));
                    Mesh(ctx, t, "Mantel", MetreMeshes.Box(new Vector3(2.5f, 0.15f, 1.15f), ctx.Assets), ctx.Mat("Wood"), new Vector3(0f, 1.3f, -0.05f));
                    Mesh(ctx, t, "Firebox", MetreMeshes.Box(new Vector3(1.2f, 0.75f, 0.1f), ctx.Assets), ctx.Mat("Metal"), new Vector3(0f, 0.45f, -0.46f));
                    Mesh(ctx, t, "Embers", MetreMeshes.Box(new Vector3(1.0f, 0.18f, 0.35f), ctx.Assets), ctx.Mat("Ember"), new Vector3(0f, 0.2f, -0.35f));
                    Mesh(ctx, t, "Chimney breast", MetreMeshes.Box(new Vector3(1.4f, 4.2f, 0.7f), ctx.Assets), ctx.Mat("RockTrim"), new Vector3(0f, 3.4f, 0.15f));
                    Light(t, "Fire light", new Vector3(0f, 0.9f, -1f), new Color(1f, 0.56f, 0.26f), 6.5f, 3f);
                    ColliderBox(colliders, "Hearth", new Vector3(0f, 0.65f, 0f), new Vector3(2.2f, 1.3f, 1f));
                    break;
            }
        }

        private static void Barrel(Context ctx, Transform t, Vector3 at, float scale)
        {
            var profile = new[]
            {
                new Vector2(0.28f, 0f), new Vector2(0.34f, 0.2f), new Vector2(0.36f, 0.45f), new Vector2(0.34f, 0.7f), new Vector2(0.28f, 0.9f),
            };
            var barrel = Mesh(ctx, t, "Barrel", Shared(ctx, "Barrel", () => MetreMeshes.Lathe(profile, 14, ctx.Assets)), ctx.Mat("WoodTrimWear"), at);
            barrel.transform.localScale = Vector3.one * scale;
            foreach (var y in new[] { 0.18f, 0.72f })
            {
                var band = Mesh(ctx, t, "Band", Shared(ctx, "Band", () => MetreMeshes.Lathe(new[] { new Vector2(0.355f, 0f), new Vector2(0.355f, 0.05f) }, 14, ctx.Assets, false)),
                    ctx.Mat("Metal"), at + Vector3.up * y * scale);
                band.transform.localScale = Vector3.one * scale;
            }
        }

        private static void Lantern(Context ctx, Transform t, Vector3 at)
        {
            Mesh(ctx, t, "Lantern frame", Shared(ctx, "LanternFrame", () => MetreMeshes.Box(new Vector3(0.24f, 0.05f, 0.24f), ctx.Assets)), ctx.Mat("Metal"), at + Vector3.up * 0.2f);
            Mesh(ctx, t, "Lantern glass", Shared(ctx, "LanternGlass", () => MetreMeshes.Box(new Vector3(0.18f, 0.3f, 0.18f), ctx.Assets)), ctx.Mat("LampGlow"), at + Vector3.up * 0.03f).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Tree(Context ctx, Transform t, int hash)
        {
            Mesh(ctx, t, "Trunk", MetreMeshes.Lathe(new[] { new Vector2(0.32f, 0f), new Vector2(0.22f, 0.5f), new Vector2(0.16f, 3.2f), new Vector2(0.08f, 4.2f) }, 9, ctx.Assets),
                ctx.Mat("Bark"), Vector3.zero);
            var leaves = ctx.Mat("Foliage");
            for (var i = 0; i < 6; i++)
            {
                var a = (hash * 13 + i * 61) % 360 * Mathf.Deg2Rad;
                var r = i == 0 ? 0f : 0.9f + (hash + i * 7) % 5 * 0.12f;
                var s = i == 0 ? 3f : 1.9f + (hash + i * 3) % 4 * 0.25f;
                var c = Mesh(ctx, t, "Canopy", Shared(ctx, "Sphere"), leaves, new Vector3(Mathf.Cos(a) * r, (i == 0 ? 4.4f : 3.6f + i % 3 * 0.55f), Mathf.Sin(a) * r));
                c.transform.localScale = new Vector3(s, s * 0.85f, s);
            }
        }

        private static void Table(Context ctx, Transform t, Vector3 at, Transform colliders)
        {
            Mesh(ctx, t, "Top", MetreMeshes.Box(new Vector3(2.2f, 0.08f, 0.95f), ctx.Assets), ctx.Mat("Planks"), at + new Vector3(0f, 0.78f, 0f));
            foreach (var x in new[] { -0.95f, 0.95f })
            foreach (var z in new[] { -0.35f, 0.35f })
                Mesh(ctx, t, "Leg", Shared(ctx, "TableLeg", () => MetreMeshes.Box(new Vector3(0.08f, 0.74f, 0.08f), ctx.Assets)), ctx.Mat("Wood"), at + new Vector3(x, 0.37f, z));
            ColliderBox(colliders, "Table", at + new Vector3(0f, 0.4f, 0f), new Vector3(2.2f, 0.8f, 0.95f));
        }

        private static void Bench(Context ctx, Transform t, Vector3 at, float length, Transform colliders)
        {
            Mesh(ctx, t, "Seat", MetreMeshes.Box(new Vector3(length, 0.07f, 0.35f), ctx.Assets), ctx.Mat("Planks"), at + new Vector3(0f, 0.45f, 0f));
            foreach (var x in new[] { -length / 2f + 0.15f, length / 2f - 0.15f })
                Mesh(ctx, t, "Leg", Shared(ctx, "BenchLeg", () => MetreMeshes.Box(new Vector3(0.07f, 0.42f, 0.3f), ctx.Assets)), ctx.Mat("Wood"), at + new Vector3(x, 0.21f, 0f));
            ColliderBox(colliders, "Bench", at + new Vector3(0f, 0.25f, 0f), new Vector3(length, 0.5f, 0.4f));
        }

        // ----- The tavern interior -------------------------------------------------------------------------

        /// <summary>
        /// Furnishes the tavern beyond the layout's table, bench and hearth: a bar with barrels and
        /// shelves, more tables and benches, a rug, and warm hanging lights. Positions are in the
        /// tavern's frame and avoid the doorway and the layout's own furniture.
        /// </summary>
        private static void BuildTavernInterior(Context ctx)
        {
            LayoutBuilding hall = null;
            foreach (var b in ctx.Layout.Buildings) if (b.Kind == BuildingKind.Hall) hall = b;
            if (hall == null || !ctx.Plans.TryGetValue(hall.Key, out var plan)) return;
            var room = new GameObject("Tavern interior").transform;
            room.SetParent(ctx.Static, false);
            room.localPosition = new Vector3(hall.Center.X, 0f, hall.Center.Z);
            room.localRotation = Quaternion.Euler(0f, -hall.Yaw, 0f);
            var colliders = new GameObject("Tavern interior colliders").transform;
            colliders.SetParent(ctx.Colliders, false);
            colliders.SetPositionAndRotation(room.position, room.rotation);
            var w = plan.Width / 2f;
            var d = plan.Depth / 2f;

            // Bar along the west half of the back wall, with barrels and a shelf behind it.
            Mesh(ctx, room, "Bar", MetreMeshes.Box(new Vector3(3.6f, 1.05f, 0.6f), ctx.Assets), ctx.Mat("Planks"), new Vector3(-w + 2.4f, 0.525f, d - 1.9f));
            Mesh(ctx, room, "Bar top", MetreMeshes.Box(new Vector3(3.8f, 0.07f, 0.75f), ctx.Assets), ctx.Mat("Wood"), new Vector3(-w + 2.4f, 1.08f, d - 1.9f));
            ColliderBox(colliders, "Bar", new Vector3(-w + 2.4f, 0.55f, d - 1.9f), new Vector3(3.8f, 1.1f, 0.75f));
            for (var i = 0; i < 3; i++) Barrel(ctx, room, new Vector3(-w + 0.9f + i * 0.85f, 0f, d - 0.65f), 0.95f);
            Mesh(ctx, room, "Shelf", MetreMeshes.Box(new Vector3(3.4f, 0.06f, 0.35f), ctx.Assets), ctx.Mat("Wood"), new Vector3(-w + 2.4f, 1.9f, d - 0.45f));
            for (var i = 0; i < 6; i++)
                Mesh(ctx, room, "Jug", Shared(ctx, "Jug", () => MetreMeshes.Lathe(new[] { new Vector2(0.07f, 0f), new Vector2(0.09f, 0.12f), new Vector2(0.05f, 0.24f) }, 8, ctx.Assets)),
                    ctx.Mat(i % 2 == 0 ? "RedBrick" : "RockTrim"), new Vector3(-w + 1f + i * 0.55f, 1.93f, d - 0.45f));

            // A second table group on the east side, clear of the hearth and the layout table.
            Table(ctx, room, new Vector3(w - 3.2f, 0f, -0.9f), colliders);
            Bench(ctx, room, new Vector3(w - 3.2f, 0f, -1.75f), 2f, colliders);
            Bench(ctx, room, new Vector3(w - 3.2f, 0f, -0.05f), 2f, colliders);
            Bench(ctx, room, new Vector3(-2f, 0f, 1.25f), 2.2f, colliders); // the layout table's north side

            Mesh(ctx, room, "Rug", MetreMeshes.Plane(3f, 2f, ctx.Assets), ctx.Mat("CanvasRed"), new Vector3(0f, 0.035f, -d + 2.4f));

            // Light: warm lamps under the roof (the room is closed to the sun).
            Light(room, "Lamp W", new Vector3(-w + 3f, plan.WallHeight - 1.6f, 0f), new Color(1f, 0.78f, 0.52f), 9f, 2.2f);
            Light(room, "Lamp E", new Vector3(w - 3f, plan.WallHeight - 1.6f, 0f), new Color(1f, 0.78f, 0.52f), 9f, 2.2f);
            Lantern(ctx, room, new Vector3(-w + 3f, plan.WallHeight - 1.75f, 0f));
            Lantern(ctx, room, new Vector3(w - 3f, plan.WallHeight - 1.75f, 0f));
            foreach (var x in new[] { -w + 3f, w - 3f })
                Mesh(ctx, room, "Chain", MetreMeshes.Box(new Vector3(0.03f, 1.4f, 0.03f), ctx.Assets), ctx.Mat("Metal"), new Vector3(x, plan.WallHeight - 0.85f, 0f));
        }

        // ----- Street dressing ----------------------------------------------------------------------------

        /// <summary>
        /// Deterministic clutter that makes the streets read as lived in: barrels and crates by doors,
        /// benches along the main street, a handcart near the plaza, stones and grass tufts on the verges.
        /// Placed from the layout seed, clear of streets' centre lines, doors and the plaza.
        /// </summary>
        private static void BuildStreetDressing(Context ctx)
        {
            var layout = ctx.Layout;
            var dress = new GameObject("Street dressing").transform;
            dress.SetParent(ctx.Static, false);
            var colliders = new GameObject("Street dressing colliders").transform;
            colliders.SetParent(ctx.Colliders, false);
            var half = layout.HalfExtent;
            var edge = LocalEnvironmentGenerator.StreetWidth / 2f + 1.1f;

            // Benches facing the main street (in the gaps between houses).
            var k = 0;
            for (var z = -half + 18f; z < -layout.PlazaRadius - 4f; z += 12f, k++)
            {
                var side = k % 2 == 0 ? -1f : 1f;
                var bench = new GameObject("Bench").transform;
                bench.SetParent(dress, false);
                bench.localPosition = new Vector3(side * (edge + 0.6f), 0f, z);
                bench.localRotation = Quaternion.Euler(0f, 90f, 0f);
                var bc = new GameObject("Bench collider").transform;
                bc.SetParent(colliders, false);
                bc.SetPositionAndRotation(bench.position, bench.rotation);
                Bench(ctx, bench, Vector3.zero, 1.8f, bc);
            }

            // A handcart parked by the plaza.
            Module(ctx, dress, "Prop_Wagon", new Vector3(layout.PlazaRadius + 1.5f, 0f, -layout.PlazaRadius + 1f), 200f, new Vector3(0.7f, 0.7f, 0.7f));
            ColliderBox(colliders, "Wagon", new Vector3(layout.PlazaRadius + 1.5f, 0.6f, -layout.PlazaRadius + 1f), new Vector3(1.6f, 1.2f, 2.6f));

            // Verge clutter: small stones and grass tufts (no collision).
            var rng = new System.Random((int)(ctx.Seed & 0x7FFFFFFF));
            var tuftMesh = Shared(ctx, "Sphere");
            for (var i = 0; i < 90; i++)
            {
                var x = (float)(rng.NextDouble() * 2 - 1) * (half - 2f);
                var z = (float)(rng.NextDouble() * 2 - 1) * (half - 2f);
                if (Mathf.Abs(x) < edge || Mathf.Abs(z) < edge || new Vector2(x, z).magnitude < layout.PlazaRadius + 2f) continue;
                if (InsideAnyBuilding(ctx, x, z)) continue;
                var stone = i % 3 == 0;
                var s = stone ? 0.15f + (float)rng.NextDouble() * 0.2f : 0.35f + (float)rng.NextDouble() * 0.3f;
                var piece = Mesh(ctx, dress, stone ? "Stone" : "Tuft", tuftMesh, ctx.Mat(stone ? "RockTrim" : "Foliage"), new Vector3(x, stone ? s * 0.25f : 0.05f, z));
                piece.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                piece.transform.localScale = stone ? new Vector3(s, s * 0.6f, s * 0.8f) : new Vector3(s, s * 0.45f, s);
            }
        }

        private static bool InsideAnyBuilding(Context ctx, float x, float z)
        {
            foreach (var b in ctx.Layout.Buildings)
            {
                var r = Mathf.Max(b.Width, b.Depth) / 2f + 1.2f;
                if (Mathf.Abs(x - b.Center.X) < r && Mathf.Abs(z - b.Center.Z) < r) return true;
            }

            return false;
        }

        // ----- Helpers ------------------------------------------------------------------------------------

        private static readonly Dictionary<GeneratedAssets, Dictionary<string, Mesh>> SharedMeshes = new Dictionary<GeneratedAssets, Dictionary<string, Mesh>>();

        /// <summary>One mesh per environment for repeated small pieces (spheres, legs, barrels).</summary>
        private static Mesh Shared(Context ctx, string key, Func<Mesh> make = null)
        {
            if (!SharedMeshes.TryGetValue(ctx.Assets, out var cache))
            {
                cache = new Dictionary<string, Mesh>(StringComparer.Ordinal);
                SharedMeshes.Clear(); // only the environment being built needs its cache
                SharedMeshes[ctx.Assets] = cache;
            }

            if (cache.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            if (make != null) mesh = make();
            else if (key == "Merlon") mesh = MetreMeshes.Box(new Vector3(0.8f, 0.8f, 1.2f), ctx.Assets);
            else
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
                ctx.Assets.Track(mesh);
                if (UnityEngine.Application.isPlaying) Object.Destroy(temp);
                else Object.DestroyImmediate(temp);
            }

            cache[key] = mesh;
            return mesh;
        }

        private static GameObject Mesh(Context ctx, Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        private static void ColliderBox(Transform parent, string name, Vector3 localCentre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.AddComponent<BoxCollider>().size = size;
        }

        private static void Light(Transform parent, string name, Vector3 localPosition, Color colour, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }
    }
}
