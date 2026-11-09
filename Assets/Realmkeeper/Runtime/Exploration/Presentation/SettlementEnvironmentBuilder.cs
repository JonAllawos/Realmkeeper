using System.Collections.Generic;
using Realmkeeper.Exploration.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Builds a settlement's local environment from its engine-free <see cref="LocalEnvironmentLayout"/>
    /// into the active (exploration) scene: ground, streets, plaza, houses, the enterable hall with
    /// a small interior, the granary, a landmark tower, walls or fences, props and lighting.
    /// Temporary vertical-slice art: Unity primitives with colliders at correct real-world scale
    /// (metres), coloured with URP Lit materials. Residents and interactables are created by the
    /// controller, which needs the session.
    /// </summary>
    public static class SettlementEnvironmentBuilder
    {
        private static readonly Color[] WallStyles = { ExplorationMaterials.Plaster, ExplorationMaterials.Limestone, new Color(0.62f, 0.50f, 0.38f) };
        private static readonly Color[] RoofStyles = { ExplorationMaterials.Thatch, ExplorationMaterials.RoofTile, ExplorationMaterials.Slate };

        public const float HallDoorWidth = 2.4f;
        public const float HallDoorHeight = 2.8f;

        /// <summary>Builds everything under one root object in the active scene and returns the root.</summary>
        public static GameObject Build(LocalEnvironmentLayout layout, ExplorationMaterials materials)
        {
            var root = new GameObject("Environment " + layout.EnvironmentKey);
            BuildGround(root.transform, layout, materials);
            foreach (var b in layout.Buildings) BuildBuilding(root.transform, b, materials);
            BuildBoundary(root.transform, layout, materials);
            foreach (var p in layout.Props) BuildProp(root.transform, p, materials);
            BuildLighting(root.transform, layout);
            return root;
        }

        // ----- Ground, streets, plaza ------------------------------------------------------

        private static void BuildGround(Transform parent, LocalEnvironmentLayout layout, ExplorationMaterials m)
        {
            var size = layout.HalfExtent * 2f + 60f;
            Box(parent, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size), m.Get(ExplorationMaterials.Grass));

            foreach (var s in layout.Streets)
            {
                var from = new Vector3(s.From.X, 0f, s.From.Z);
                var to = new Vector3(s.To.X, 0f, s.To.Z);
                var mid = (from + to) * 0.5f;
                var length = Vector3.Distance(from, to);
                var street = Box(parent, "Street", mid + Vector3.up * 0.01f, new Vector3(s.Width, 0.02f, length), m.Get(ExplorationMaterials.Cobble));
                street.transform.rotation = Quaternion.LookRotation((to - from).normalized, Vector3.up);
                RemoveCollider(street);
            }

            // Road outside the south gate, so the arrival reads as coming in from the region.
            var road = Box(parent, "Approach road", new Vector3(0f, 0.01f, -layout.HalfExtent - 15f), new Vector3(4f, 0.02f, 30f), m.Get(ExplorationMaterials.Dirt));
            RemoveCollider(road);

            var plaza = Primitive(parent, PrimitiveType.Cylinder, "Plaza", new Vector3(0f, 0.015f, 0f),
                new Vector3(layout.PlazaRadius * 2f, 0.015f, layout.PlazaRadius * 2f), m.Get(ExplorationMaterials.Cobble));
            RemoveCollider(plaza);
        }

        // ----- Buildings --------------------------------------------------------------------

        private static void BuildBuilding(Transform parent, LayoutBuilding b, ExplorationMaterials m)
        {
            var go = new GameObject(b.Key);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(b.Center.X, 0f, b.Center.Z);
            go.transform.localRotation = Quaternion.Euler(0f, -b.Yaw, 0f); // door on local −Z faces (sin yaw, −cos yaw)
            var t = go.transform;
            var wall = m.Get(WallStyles[Mathf.Abs(b.Style) % WallStyles.Length]);
            var roof = m.Get(RoofStyles[Mathf.Abs(b.Style) % RoofStyles.Length]);
            var timber = m.Get(ExplorationMaterials.DarkTimber);

            switch (b.Kind)
            {
                case BuildingKind.Hall:
                    BuildHall(t, b, m);
                    return;
                case BuildingKind.Landmark:
                    Box(t, "Tower", new Vector3(0f, b.Height / 2f, 0f), new Vector3(b.Width, b.Height, b.Depth), m.Get(ExplorationMaterials.Stone));
                    Box(t, "Parapet", new Vector3(0f, b.Height + 0.4f, 0f), new Vector3(b.Width + 0.8f, 0.8f, b.Depth + 0.8f), m.Get(ExplorationMaterials.Stone));
                    var spire = Box(t, "Spire", new Vector3(0f, b.Height + 2.4f, 0f), new Vector3(b.Width * 0.55f, b.Width * 0.55f, b.Width * 0.55f), m.Get(ExplorationMaterials.Slate));
                    spire.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                    Box(t, "Door", new Vector3(0f, 1.2f, -b.Depth / 2f - 0.04f), new Vector3(1.4f, 2.4f, 0.1f), timber);
                    return;
                default:
                    Box(t, "Walls", new Vector3(0f, b.Height / 2f, 0f), new Vector3(b.Width, b.Height, b.Depth), wall);
                    PitchedRoof(t, b.Width, b.Depth, b.Height, roof);
                    Box(t, "Door", new Vector3(0f, 1.05f, -b.Depth / 2f - 0.04f), new Vector3(1.2f, 2.1f, 0.1f), timber);
                    if (b.Width > 6f)
                    {
                        Box(t, "Window L", new Vector3(-b.Width / 2f + 1.3f, 2.2f, -b.Depth / 2f - 0.04f), new Vector3(0.9f, 0.9f, 0.08f), timber);
                        Box(t, "Window R", new Vector3(b.Width / 2f - 1.3f, 2.2f, -b.Depth / 2f - 0.04f), new Vector3(0.9f, 0.9f, 0.08f), timber);
                    }

                    return;
            }
        }

        /// <summary>
        /// The enterable hall: four walls with a door gap on the south side, a floor, ceiling
        /// and roof, and a warm interior light. Interior furniture comes from the layout props.
        /// </summary>
        private static void BuildHall(Transform t, LayoutBuilding b, ExplorationMaterials m)
        {
            const float thick = 0.4f;
            var w = b.Width;
            var d = b.Depth;
            var h = b.Height;
            var wall = m.Get(ExplorationMaterials.Limestone);
            var timber = m.Get(ExplorationMaterials.DarkTimber);

            Box(t, "Floor", new Vector3(0f, 0.05f, 0f), new Vector3(w, 0.1f, d), m.Get(ExplorationMaterials.Timber));
            Box(t, "Back wall", new Vector3(0f, h / 2f, d / 2f - thick / 2f), new Vector3(w, h, thick), wall);
            Box(t, "West wall", new Vector3(-w / 2f + thick / 2f, h / 2f, 0f), new Vector3(thick, h, d), wall);
            Box(t, "East wall", new Vector3(w / 2f - thick / 2f, h / 2f, 0f), new Vector3(thick, h, d), wall);

            var side = (w - HallDoorWidth) / 2f;
            Box(t, "Front wall W", new Vector3(-(HallDoorWidth / 2f + side / 2f), h / 2f, -d / 2f + thick / 2f), new Vector3(side, h, thick), wall);
            Box(t, "Front wall E", new Vector3(HallDoorWidth / 2f + side / 2f, h / 2f, -d / 2f + thick / 2f), new Vector3(side, h, thick), wall);
            Box(t, "Lintel", new Vector3(0f, (HallDoorHeight + h) / 2f, -d / 2f + thick / 2f), new Vector3(HallDoorWidth, h - HallDoorHeight, thick), wall);
            Box(t, "Door frame L", new Vector3(-HallDoorWidth / 2f - 0.1f, HallDoorHeight / 2f, -d / 2f - 0.02f), new Vector3(0.2f, HallDoorHeight, thick + 0.1f), timber);
            Box(t, "Door frame R", new Vector3(HallDoorWidth / 2f + 0.1f, HallDoorHeight / 2f, -d / 2f - 0.02f), new Vector3(0.2f, HallDoorHeight, thick + 0.1f), timber);

            Box(t, "Ceiling", new Vector3(0f, h + 0.1f, 0f), new Vector3(w, 0.2f, d), timber);
            PitchedRoof(t, w + 0.4f, d + 0.4f, h + 0.2f, m.Get(ExplorationMaterials.RoofTile));
            Box(t, "Sign", new Vector3(0f, HallDoorHeight + 0.6f, -d / 2f - 0.08f), new Vector3(1.8f, 0.6f, 0.08f), m.Get(ExplorationMaterials.Parchment));

            var lightGo = new GameObject("Interior light");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0f, h - 1.2f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.52f);
            light.range = Mathf.Max(w, d) * 0.9f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
        }

        /// <summary>Two roof slabs meeting at a ridge along local X, pitched 35°.</summary>
        private static void PitchedRoof(Transform t, float width, float depth, float eaves, Material roof)
        {
            const float pitch = 35f;
            var rad = pitch * Mathf.Deg2Rad;
            var slab = depth / 2f / Mathf.Cos(rad) + 0.5f;
            var rise = depth / 4f * Mathf.Tan(rad);
            var north = Box(t, "Roof N", new Vector3(0f, eaves + rise, depth / 4f), new Vector3(width + 0.6f, 0.25f, slab), roof);
            north.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f); // +X rotation lowers the +Z (north) edge: eaves down, ridge up
            var south = Box(t, "Roof S", new Vector3(0f, eaves + rise, -depth / 4f), new Vector3(width + 0.6f, 0.25f, slab), roof);
            south.transform.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
        }

        // ----- Walls or fences -------------------------------------------------------------

        private static void BuildBoundary(Transform parent, LocalEnvironmentLayout layout, ExplorationMaterials m)
        {
            var half = layout.HalfExtent;
            var height = layout.HasWalls ? 4.5f : 1.1f;
            var thick = layout.HasWalls ? 1f : 0.25f;
            var mat = m.Get(layout.HasWalls ? ExplorationMaterials.Stone : ExplorationMaterials.Timber);
            const float gate = 6f;
            var c = half + thick / 2f;
            var span = half * 2f + thick * 2f;

            var boundary = new GameObject("Boundary").transform;
            boundary.SetParent(parent, false);
            Box(boundary, "North", new Vector3(0f, height / 2f, c), new Vector3(span, height, thick), mat);
            Box(boundary, "West", new Vector3(-c, height / 2f, 0f), new Vector3(thick, height, span), mat);
            Box(boundary, "East", new Vector3(c, height / 2f, 0f), new Vector3(thick, height, span), mat);
            var sideLength = (span - gate) / 2f;
            Box(boundary, "South W", new Vector3(-(gate / 2f + sideLength / 2f), height / 2f, -c), new Vector3(sideLength, height, thick), mat);
            Box(boundary, "South E", new Vector3(gate / 2f + sideLength / 2f, height / 2f, -c), new Vector3(sideLength, height, thick), mat);

            if (layout.HasWalls)
            {
                Box(boundary, "Gate tower W", new Vector3(-gate / 2f - 1.5f, 3.5f, -c), new Vector3(3f, 7f, 3f), mat);
                Box(boundary, "Gate tower E", new Vector3(gate / 2f + 1.5f, 3.5f, -c), new Vector3(3f, 7f, 3f), mat);
            }
            else
            {
                Box(boundary, "Gate post W", new Vector3(-gate / 2f, 1.3f, -c), new Vector3(0.4f, 2.6f, 0.4f), mat);
                Box(boundary, "Gate post E", new Vector3(gate / 2f, 1.3f, -c), new Vector3(0.4f, 2.6f, 0.4f), mat);
            }

            // The gate is open to look at but the walkable area ends here: leaving is done through
            // "Return to World Map", which keeps the character's position valid in world data.
            var barrier = new GameObject("Gate barrier");
            barrier.transform.SetParent(boundary, false);
            barrier.transform.localPosition = new Vector3(0f, 2f, -c);
            barrier.AddComponent<BoxCollider>().size = new Vector3(gate, 4f, thick);
        }

        // ----- Props ------------------------------------------------------------------------

        private static void BuildProp(Transform parent, LayoutProp p, ExplorationMaterials m)
        {
            var go = new GameObject(p.Kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(p.Position.X, 0f, p.Position.Z);
            go.transform.localRotation = Quaternion.Euler(0f, -p.Yaw, 0f);
            var t = go.transform;
            var timber = m.Get(ExplorationMaterials.Timber);
            var dark = m.Get(ExplorationMaterials.DarkTimber);
            var stone = m.Get(ExplorationMaterials.Stone);

            switch (p.Kind)
            {
                case PropKind.Well:
                    Primitive(t, PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.45f, 1.8f), stone);
                    Box(t, "Post W", new Vector3(-0.8f, 1.2f, 0f), new Vector3(0.15f, 2.4f, 0.15f), dark);
                    Box(t, "Post E", new Vector3(0.8f, 1.2f, 0f), new Vector3(0.15f, 2.4f, 0.15f), dark);
                    Box(t, "Roof", new Vector3(0f, 2.45f, 0f), new Vector3(2.2f, 0.15f, 1.4f), m.Get(ExplorationMaterials.RoofTile));
                    break;
                case PropKind.MarketStall:
                    Box(t, "Counter", new Vector3(0f, 0.5f, 0f), new Vector3(2.6f, 1f, 1f), timber);
                    Box(t, "Post L", new Vector3(-1.2f, 1.2f, 0.45f), new Vector3(0.1f, 2.4f, 0.1f), dark);
                    Box(t, "Post R", new Vector3(1.2f, 1.2f, 0.45f), new Vector3(0.1f, 2.4f, 0.1f), dark);
                    var awning = Box(t, "Awning", new Vector3(0f, 2.35f, -0.1f), new Vector3(2.9f, 0.08f, 1.6f), m.Get(ExplorationMaterials.Canvas));
                    awning.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
                    Box(t, "Goods", new Vector3(0.5f, 1.12f, 0f), new Vector3(0.7f, 0.25f, 0.5f), m.Get(ExplorationMaterials.Thatch));
                    break;
                case PropKind.Barrel:
                    Primitive(t, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.45f, 0.7f), timber);
                    break;
                case PropKind.Crate:
                    Box(t, "Crate", new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), dark);
                    break;
                case PropKind.LampPost:
                    Box(t, "Post", new Vector3(0f, 1.6f, 0f), new Vector3(0.14f, 3.2f, 0.14f), dark);
                    var lamp = Box(t, "Lamp", new Vector3(0f, 3.25f, 0f), new Vector3(0.35f, 0.35f, 0.35f), m.Get(ExplorationMaterials.Ember, 0.6f));
                    RemoveCollider(lamp);
                    break;
                case PropKind.Tree:
                    Primitive(t, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.5f, 0f), new Vector3(0.45f, 1.5f, 0.45f), dark);
                    var canopy = Primitive(t, PrimitiveType.Sphere, "Canopy", new Vector3(0f, 4f, 0f), new Vector3(3.6f, 3.4f, 3.6f), m.Get(ExplorationMaterials.Leaves));
                    RemoveCollider(canopy);
                    break;
                case PropKind.Table:
                    Box(t, "Top", new Vector3(0f, 0.78f, 0f), new Vector3(2.4f, 0.08f, 1f), timber);
                    Box(t, "Legs", new Vector3(0f, 0.37f, 0f), new Vector3(2f, 0.74f, 0.7f), dark);
                    break;
                case PropKind.Bench:
                    Box(t, "Bench", new Vector3(0f, 0.25f, 0f), new Vector3(2.2f, 0.5f, 0.4f), dark);
                    break;
                case PropKind.Hearth:
                    Box(t, "Hearth", new Vector3(0f, 0.6f, 0f), new Vector3(2f, 1.2f, 1f), stone);
                    var embers = Box(t, "Embers", new Vector3(0f, 0.35f, -0.35f), new Vector3(1.2f, 0.3f, 0.4f), m.Get(ExplorationMaterials.Ember, 0.8f));
                    RemoveCollider(embers);
                    Box(t, "Chimney", new Vector3(0f, 3f, 0.2f), new Vector3(1.2f, 3.6f, 0.6f), stone);
                    var fire = new GameObject("Fire light");
                    fire.transform.SetParent(t, false);
                    fire.transform.localPosition = new Vector3(0f, 0.9f, -0.9f);
                    var light = fire.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.55f, 0.25f);
                    light.range = 6f;
                    light.intensity = 2.5f;
                    light.shadows = LightShadows.None;
                    break;
            }
        }

        // ----- Lighting ---------------------------------------------------------------------

        private static void BuildLighting(Transform parent, LocalEnvironmentLayout layout)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            RenderSettings.sun = sun;
        }

        /// <summary>Fog for walking scale (the strategic view uses map-scale distances); restored on exit.</summary>
        public static void ApplyAtmosphere()
        {
            // Ambient light (and its hand-built probe) is left as the strategic environment set it.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.70f, 0.74f, 0.78f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 220f;
        }

        // ----- Helpers ----------------------------------------------------------------------

        internal static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material) =>
            Primitive(parent, PrimitiveType.Cube, name, localPosition, size, material);

        internal static GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        internal static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;
            if (UnityEngine.Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }

        /// <summary>Snapshot of the render settings exploration changes, so they can be restored.</summary>
        public sealed class RenderSettingsSnapshot
        {
            private Color _fogColor;
            private bool _fog;
            private FogMode _fogMode;
            private float _fogStart, _fogEnd, _fogDensity;
            private Light _sun;

            public static RenderSettingsSnapshot Capture() => new RenderSettingsSnapshot
            {
                _fog = RenderSettings.fog,
                _fogMode = RenderSettings.fogMode,
                _fogColor = RenderSettings.fogColor,
                _fogStart = RenderSettings.fogStartDistance,
                _fogEnd = RenderSettings.fogEndDistance,
                _fogDensity = RenderSettings.fogDensity,
                _sun = RenderSettings.sun,
            };

            public void Restore()
            {
                RenderSettings.fog = _fog;
                RenderSettings.fogMode = _fogMode;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogStartDistance = _fogStart;
                RenderSettings.fogEndDistance = _fogEnd;
                RenderSettings.fogDensity = _fogDensity;
                RenderSettings.sun = _sun;
            }
        }
    }
}
