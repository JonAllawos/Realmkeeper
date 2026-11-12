using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Places;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 14 (ADR-0071): each kind of place built to look like what it is — its own surfaces (the CC0 kit's textured
    /// materials when the art catalog is present, colours otherwise) and its own furniture of the dead, the faithful, the
    /// outlaws, the engineers or the departed — and the hazards as they stand now: fire burning or burnt out, bridges
    /// sound, sagging or gone, water in the channels or drained. Everything is drawn from the world's view; nothing here
    /// decides anything. A prop that looks interactive is one: interactables are made by the controller from features.
    /// </summary>
    public static partial class PlaceEnvironmentBuilder
    {
        public sealed class Look
        {
            public Material Floor;
            public Material Wall;
            public Material Ceiling;
            public Material Outdoor;
        }

        /// <summary>The surfaces of a kind of place, from the art catalog (null fields: use the palette colours).</summary>
        public static Look Surfaces(PlaceSceneView v, ArtCatalog catalog)
        {
            var look = new Look();
            if (catalog == null) return look;
            Material M(string id) => catalog.Material(id);
            switch (v.Category)
            {
                case PlaceCategory.Mine:
                case PlaceCategory.Cave:
                case PlaceCategory.BanditHideout:
                    look.Floor = M("Dirt"); look.Wall = M("RockTrim"); look.Ceiling = M("RockTrim"); break;
                case PlaceCategory.Crypt:
                case PlaceCategory.Catacomb:
                    look.Floor = M("Gravel"); look.Wall = M("WallStone"); look.Ceiling = M("WallStone"); break;
                case PlaceCategory.Temple:
                case PlaceCategory.Shrine:
                case PlaceCategory.Keep:
                    look.Floor = M("Cobble"); look.Wall = M("WallStone"); look.Ceiling = M("WallStone"); break;
                case PlaceCategory.Underground:
                    look.Floor = M("Brick"); look.Wall = M("UnevenBrick"); look.Ceiling = M("UnevenBrick"); break;
                case PlaceCategory.AbandonedSettlement:
                    look.Floor = M("Planks"); look.Wall = M("Wood"); look.Ceiling = M("Wood"); look.Outdoor = M("Grass"); break;
                case PlaceCategory.Ruin:
                    look.Floor = M("Gravel"); look.Wall = M("WallStone"); look.Ceiling = M("WallStone"); look.Outdoor = M("Grass"); break;
                default:
                    look.Floor = M("Planks"); look.Wall = M("Plaster"); look.Ceiling = M("Wood"); break;
            }

            if (look.Wall == null) return new Look();
            look.Floor = look.Floor ?? look.Wall;
            look.Ceiling = look.Ceiling ?? look.Wall;
            return look;
        }

        private static bool IsBridgeCell(PlaceSceneView v, string owner)
        {
            if (!owner.StartsWith("c:", StringComparison.Ordinal)) return false;
            var p = v.Passages.Find(x => x.Id == owner.Substring(2));
            return p != null && p.Kind == ConnectionKind.Bridge;
        }

        private static GameObject Kit(Transform parent, ArtCatalog catalog, string id, Vector3 at, float yaw, Vector3 scale)
        {
            var e = catalog?.Module(id);
            if (e == null) return null;
            var go = Object.Instantiate(e.Prefab, parent);
            go.name = id;
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.Scale(go.transform.localScale, scale);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            return go;
        }

        /// <summary>The furniture each kind of place has, so a crypt does not look like a mine with another name.</summary>
        private static void Dress14(PlaceSceneView v, Transform root, ExplorationMaterials materials, Palette palette, ArtCatalog catalog)
        {
            var t = new GameObject("Character of the place").transform;
            t.SetParent(root, false);
            var stone = materials.Get(ExplorationMaterials.Stone);
            var dark = materials.Get(new Color(0.07f, 0.06f, 0.06f));
            var bone = materials.Get(new Color(0.85f, 0.82f, 0.72f), 0.1f);
            var wood = materials.Get(ExplorationMaterials.Timber);
            var canvas = materials.Get(ExplorationMaterials.Canvas, 0.05f);
            var iron = materials.Get(new Color(0.22f, 0.22f, 0.24f), 0.4f);
            var brick = materials.Get(new Color(0.46f, 0.30f, 0.24f));
            var ember = materials.Get(new Color(1f, 0.45f, 0.12f), 0.6f);
            var cloth = materials.Get(new Color(0.48f, 0.16f, 0.14f), 0.1f);
            foreach (var r in v.Rooms)
            {
                var y = Elevation(v, r.Floor);
                var cx = (r.Rect.X + r.Rect.W / 2f) * Cell;
                var cz = (r.Rect.Z + r.Rect.D / 2f) * Cell;
                switch (v.Category)
                {
                    case PlaceCategory.Catacomb:
                    case PlaceCategory.Crypt:
                        // Burial niches cut into the long walls, a skull in some, and candles where the room is lit.
                        for (var x = r.Rect.X + 1; x < r.Rect.MaxX - 1; x += 2)
                            foreach (var (z, face) in new[] { (r.Rect.Z * Cell + 0.12f, 1f), (r.Rect.MaxZ * Cell - 0.12f, -1f) })
                                for (var row = 0; row < (r.Ceiling > 2.5f ? 2 : 1); row++)
                                {
                                    var at = new Vector3((x + 0.5f) * Cell, y + 0.6f + row * 0.9f, z);
                                    Box(t, "Niche", at, new Vector3(1.0f, 0.55f, 0.2f), dark);
                                    if (((x + row) & 1) == 0) Shape(t, PrimitiveType.Sphere, "Skull", at + new Vector3(0f, -0.1f, face * 0.12f), new Vector3(0.2f, 0.2f, 0.22f), bone);
                                }
                        if (r.Kind == RoomKind.Corridor) Box(t, "Ossuary course", new Vector3(cx, y + 0.3f, r.Rect.MaxZ * Cell - 0.35f), new Vector3(r.Rect.W * Cell - 0.4f, 0.6f, 0.5f), bone);
                        if (r.Lit) for (var i = 0; i < 3; i++) Box(t, "Candle", new Vector3(cx - 0.4f + i * 0.4f, y + 0.12f, r.Rect.Z * Cell + 0.6f), new Vector3(0.06f, 0.24f, 0.06f), materials.Get(new Color(0.92f, 0.88f, 0.76f)));
                        break;

                    case PlaceCategory.Temple:
                    case PlaceCategory.Shrine:
                        // Two rows of columns down the hall and the shrine, a raised dais, braziers either side of it.
                        if (r.Kind == RoomKind.Hall || r.Kind == RoomKind.Shrine)
                            for (var x = r.Rect.X + 1; x < r.Rect.MaxX - 1; x += 2)
                            {
                                Shape(t, PrimitiveType.Cylinder, "Column", new Vector3((x + 0.5f) * Cell, y + r.Ceiling / 2f, (r.Rect.Z + 1.2f) * Cell), new Vector3(0.55f, r.Ceiling / 2f, 0.55f), stone);
                                Shape(t, PrimitiveType.Cylinder, "Column", new Vector3((x + 0.5f) * Cell, y + r.Ceiling / 2f, (r.Rect.MaxZ - 1.2f) * Cell), new Vector3(0.55f, r.Ceiling / 2f, 0.55f), stone);
                            }

                        if (r.Kind == RoomKind.Shrine)
                        {
                            Box(t, "Dais", new Vector3((r.Rect.MaxX - 1.2f) * Cell, y + 0.15f, cz), new Vector3(2.0f, 0.3f, r.Rect.D * Cell * 0.6f), stone);
                            foreach (var side in new[] { -1f, 1f })
                            {
                                var b = new Vector3((r.Rect.MaxX - 2.2f) * Cell, y, cz + side * r.Rect.D * Cell * 0.3f);
                                Shape(t, PrimitiveType.Cylinder, "Brazier", b + Vector3.up * 0.45f, new Vector3(0.6f, 0.45f, 0.6f), iron);
                                Box(t, "Brazier coals", b + Vector3.up * 0.92f, new Vector3(0.4f, 0.06f, 0.4f), ember);
                            }

                            Box(t, "Hanging", new Vector3((r.Rect.MaxX) * Cell - 0.1f, y + r.Ceiling * 0.6f, cz), new Vector3(0.04f, r.Ceiling * 0.5f, 1.2f), cloth);
                        }

                        break;

                    case PlaceCategory.BanditHideout:
                        // Bedrolls and a tent in the den, stolen goods stacked, a palisade of stakes inside the way in.
                        if (r.Kind == RoomKind.Den || r.Kind == RoomKind.Quarters)
                        {
                            for (var i = 0; i < Math.Min(3, r.Rect.W - 2); i++)
                                Box(t, "Bedroll", new Vector3((r.Rect.X + 1.2f + i * 1.3f) * Cell, y + 0.05f, (r.Rect.MaxZ - 1.2f) * Cell), new Vector3(0.8f, 0.1f, 1.8f), canvas);
                            var tent = new Vector3((r.Rect.MaxX - 1.6f) * Cell, y, (r.Rect.Z + 1.6f) * Cell);
                            Box(t, "Tent", tent + new Vector3(-0.45f, 0.7f, 0f), new Vector3(0.05f, 1.6f, 2.2f), canvas, 0f).transform.rotation = Quaternion.Euler(0f, 0f, -30f);
                            Box(t, "Tent", tent + new Vector3(0.45f, 0.7f, 0f), new Vector3(0.05f, 1.6f, 2.2f), canvas, 0f).transform.rotation = Quaternion.Euler(0f, 0f, 30f);
                        }

                        if (r.Kind == RoomKind.Storage)
                            for (var i = 0; i < 3; i++)
                                if (Kit(t, catalog, "Prop_Crate", new Vector3((r.Rect.MaxX - 1f - i * 0.9f) * Cell, y, (r.Rect.MaxZ - 1f) * Cell), i * 17f, new Vector3(0.7f, 0.7f, 0.7f)) == null)
                                    Box(t, "Crate", new Vector3((r.Rect.MaxX - 1f - i * 0.9f) * Cell, y + 0.35f, (r.Rect.MaxZ - 1f) * Cell), new Vector3(0.7f, 0.7f, 0.7f), wood);
                        if (r.Kind == RoomKind.Entrance)
                            for (var x = r.Rect.X; x < r.Rect.MaxX; x++)
                                if (x != r.Rect.X + r.Rect.W / 2 && x != r.Rect.X + r.Rect.W / 2 - 1)
                                    Shape(t, PrimitiveType.Cylinder, "Stake", new Vector3((x + 0.5f) * Cell, y + 0.9f, (r.Rect.Z + 1.5f) * Cell), new Vector3(0.18f, 0.9f, 0.18f), wood);
                        break;

                    case PlaceCategory.Underground:
                        // Brick arches every few squares, pipes along the walls.
                        for (var x = r.Rect.X + 1; x < r.Rect.MaxX; x += 3)
                        {
                            var ax = (x + 0.5f) * Cell;
                            if (Kit(t, catalog, "Wall_Arch", new Vector3(ax, y, r.Rect.Z * Cell), 0f, new Vector3(0.6f, Mathf.Min(1f, r.Ceiling / 3.5f), 0.6f)) != null) continue;
                            Box(t, "Arch pier", new Vector3(ax, y + r.Ceiling / 2f, r.Rect.Z * Cell + 0.25f), new Vector3(0.5f, r.Ceiling, 0.5f), brick);
                            Box(t, "Arch pier", new Vector3(ax, y + r.Ceiling / 2f, r.Rect.MaxZ * Cell - 0.25f), new Vector3(0.5f, r.Ceiling, 0.5f), brick);
                            Box(t, "Arch", new Vector3(ax, y + r.Ceiling - 0.25f, cz), new Vector3(0.5f, 0.5f, r.Rect.D * Cell), brick);
                        }

                        Shape(t, PrimitiveType.Cylinder, "Pipe", new Vector3(cx, y + r.Ceiling - 0.6f, r.Rect.Z * Cell + 0.3f), new Vector3(0.25f, r.Rect.W * Cell / 2f, 0.25f), iron, new Vector3(0f, 0f, 90f));
                        break;

                    case PlaceCategory.AbandonedSettlement:
                        // Yards under the sky with fences and a cart; houses with their roof beams fallen in.
                        if (r.Tags.Contains("open_sky"))
                        {
                            for (var x = r.Rect.X; x < r.Rect.MaxX; x += 2)
                                if (Kit(t, catalog, "Prop_WoodenFence_Single", new Vector3((x + 0.5f) * Cell, y, r.Rect.MaxZ * Cell - 0.2f), 0f, Vector3.one) == null)
                                    Box(t, "Fence", new Vector3((x + 0.5f) * Cell, y + 0.5f, r.Rect.MaxZ * Cell - 0.2f), new Vector3(Cell * 1.8f, 0.1f, 0.08f), wood);
                            Kit(t, catalog, "Prop_Wagon", new Vector3((r.Rect.MaxX - 2f) * Cell, y, (r.Rect.Z + 2f) * Cell), 35f, new Vector3(0.8f, 0.8f, 0.8f));
                        }
                        else
                        {
                            Box(t, "Fallen beam", new Vector3(cx, y + 0.4f, cz), new Vector3(0.25f, 0.25f, r.Rect.D * Cell * 0.8f), materials.Get(ExplorationMaterials.DarkTimber), 25f).transform.rotation = Quaternion.Euler(18f, 25f, 0f);
                            Box(t, "Fallen beam", new Vector3(cx + 0.8f, y + 0.2f, cz - 0.6f), new Vector3(0.22f, 0.22f, r.Rect.D * Cell * 0.5f), materials.Get(ExplorationMaterials.DarkTimber), -40f);
                        }

                        break;
                }
            }
        }

        /// <summary>What changes as the world does: fire, bridges, water. Drawn into the controller's live layer.</summary>
        public static void Hazards(Transform parent, PlaceSceneView v, ExplorationMaterials materials, ArtCatalog catalog)
        {
            var t = new GameObject("Hazards").transform;
            t.SetParent(parent, false);
            var planks = catalog?.Material("Planks") ?? materials.Get(ExplorationMaterials.Timber);
            var rope = materials.Get(new Color(0.55f, 0.45f, 0.30f));
            var water = materials.Get(new Color(0.10f, 0.20f, 0.30f), 0.9f);
            var deep = materials.Get(new Color(0.03f, 0.05f, 0.07f), 0.6f);
            var char_ = materials.Get(new Color(0.08f, 0.07f, 0.06f), 0.02f);
            var flame = materials.Get(new Color(1f, 0.52f, 0.12f), 0.9f);
            var flameHot = materials.Get(new Color(1f, 0.82f, 0.35f), 0.9f);

            foreach (var r in v.Rooms)
            {
                var y = Elevation(v, r.Floor);
                var centre = new Vector3((r.Rect.X + r.Rect.W / 2f) * Cell, y, (r.Rect.Z + r.Rect.D / 2f) * Cell);
                if (r.Hazard == "deep_water" && !v.Drained)
                    Box(t, "Hazard deep_water", centre + Vector3.up * 0.05f, new Vector3(r.Rect.W * Cell - 0.1f, 0.05f, r.Rect.D * Cell - 0.1f), water).name = "Hazard deep_water";
                else if (r.Hazard == "deep_water")
                    Box(t, "Drained channel", centre + Vector3.up * 0.01f, new Vector3(r.Rect.W * Cell - 0.1f, 0.02f, r.Rect.D * Cell - 0.1f), materials.Get(new Color(0.24f, 0.21f, 0.17f), 0.3f));

                if (!v.Fires.TryGetValue(r.Id, out var fire)) continue;
                if (fire == "burning")
                {
                    // Flames across the floor (they move: Flicker), and their light.
                    for (var x = r.Rect.X; x < r.Rect.MaxX; x += 2)
                        for (var z = r.Rect.Z; z < r.Rect.MaxZ; z += 2)
                        {
                            var at = new Vector3((x + 0.6f) * Cell, y, (z + 0.6f) * Cell);
                            var f1 = Box(t, "Flame", at + Vector3.up * 0.3f, new Vector3(0.35f, 0.6f, 0.35f), flame, (x * 37 + z * 11) % 90);
                            var f2 = Box(t, "Flame", at + Vector3.up * 0.2f + new Vector3(0.2f, 0f, -0.15f), new Vector3(0.22f, 0.4f, 0.22f), flameHot, (x * 13 + z * 29) % 90);
                            f1.AddComponent<Flicker>();
                            f2.AddComponent<Flicker>();
                        }

                    var lightGo = new GameObject("Fire light");
                    lightGo.transform.SetParent(t, false);
                    lightGo.transform.position = centre + Vector3.up * 1.2f;
                    var light = lightGo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.55f, 0.2f);
                    light.range = Mathf.Max(r.Rect.W, r.Rect.D) * Cell * 1.3f;
                    light.intensity = 2.6f;
                    lightGo.AddComponent<Flicker>().Light = light;
                }
                else
                {
                    Box(t, "Charred floor", centre + Vector3.up * 0.01f, new Vector3(r.Rect.W * Cell - 0.2f, 0.02f, r.Rect.D * Cell - 0.2f), char_);
                    Box(t, "Burnt beam", centre + new Vector3(0.4f, 0.15f, 0f), new Vector3(0.25f, 0.25f, r.Rect.D * Cell * 0.6f), char_, 30f);
                }
            }

            foreach (var p in v.Passages)
            {
                if (p.Kind != ConnectionKind.Bridge) continue;
                var a = v.Room(p.A);
                if (a == null) continue;
                var y = Elevation(v, a.Floor);
                var state = v.Bridges.TryGetValue(p.Id, out var s) ? s : BridgeState.Intact;
                var horizontal = p.Rect.W >= p.Rect.D;
                var len = (horizontal ? p.Rect.W : p.Rect.D) * Cell;
                var width = (horizontal ? p.Rect.D : p.Rect.W) * Cell;
                var mid = new Vector3((p.Rect.X + p.Rect.W / 2f) * Cell, y, (p.Rect.Z + p.Rect.D / 2f) * Cell);
                // The channel under it: dark water far below.
                Box(t, "Channel", mid + Vector3.down * 1.6f, horizontal ? new Vector3(len, 0.05f, width) : new Vector3(width, 0.05f, len), deep);
                if (state == BridgeState.Destroyed)
                {
                    // Gone: a few broken ends, and nothing to walk on (a blocker on each side keeps the character from the gap).
                    Box(t, "Broken end", mid + (horizontal ? new Vector3(-len / 2f + 0.2f, -0.1f, 0f) : new Vector3(0f, -0.1f, -len / 2f + 0.2f)), new Vector3(0.4f, 0.1f, 0.4f), planks, 20f);
                    var block = Box(t, "Gap", mid + Vector3.up * 1f, horizontal ? new Vector3(len, 2f, width) : new Vector3(width, 2f, len), planks, 0f, collide: true);
                    block.GetComponent<Renderer>().enabled = false;
                    continue;
                }

                // A deck of planks (a gap in every third when damaged), with a rope rail.
                var planksCount = Mathf.Max(2, Mathf.RoundToInt(len / 0.35f));
                for (var i = 0; i < planksCount; i++)
                {
                    if (state == BridgeState.Damaged && i % 3 == 1) continue;
                    var off = -len / 2f + (i + 0.5f) * len / planksCount;
                    var at = mid + (horizontal ? new Vector3(off, -0.05f, 0f) : new Vector3(0f, -0.05f, off));
                    Box(t, "Plank", at, horizontal ? new Vector3(len / planksCount * 0.85f, 0.08f, width) : new Vector3(width, 0.08f, len / planksCount * 0.85f), planks, state == BridgeState.Damaged && i % 5 == 3 ? 6f : 0f);
                }

                var deck = Box(t, "Deck", mid + Vector3.down * 0.06f, horizontal ? new Vector3(len, 0.1f, width) : new Vector3(width, 0.1f, len), planks, 0f, collide: true);
                deck.GetComponent<Renderer>().enabled = false;
                foreach (var side in new[] { -1f, 1f })
                    Box(t, "Rope", mid + (horizontal ? new Vector3(0f, 0.9f, side * width / 2f) : new Vector3(side * width / 2f, 0.9f, 0f)), horizontal ? new Vector3(len, 0.04f, 0.04f) : new Vector3(0.04f, 0.04f, len), rope, state == BridgeState.Damaged && side > 0 ? 4f : 0f);
            }
        }

        /// <summary>The way a person walked between rooms: from the middle of where they were, through each passage, to where they stand.</summary>
        public static List<Vector3> RouteWaypoints(PlaceSceneView v, string fromRoom, List<string> route, Vector3 end)
        {
            var points = new List<Vector3>();
            var from = v.Room(fromRoom);
            if (from != null) points.Add(new Vector3((from.Rect.X + from.Rect.W / 2f) * Cell, Elevation(v, from.Floor), (from.Rect.Z + from.Rect.D / 2f) * Cell));
            foreach (var id in route)
            {
                var p = v.Passages.Find(x => x.Id == id);
                if (p == null) continue;
                var a = v.Room(p.A);
                if (p.Kind == ConnectionKind.Stairs || p.Kind == ConnectionKind.Ladder) continue; // a floor change: they appear on the landing
                points.Add(new Vector3((p.Rect.X + p.Rect.W / 2f) * Cell, Elevation(v, a?.Floor ?? 0), (p.Rect.Z + p.Rect.D / 2f) * Cell));
            }

            points.Add(end);
            return points;
        }
    }

    /// <summary>A flame or a fire's light that moves: scale and intensity jitter (presentation only; nothing ticks in the world).</summary>
    public sealed class Flicker : MonoBehaviour
    {
        public Light Light;
        private Vector3 _base;
        private float _baseIntensity;
        private float _phase;

        private void Start()
        {
            _base = transform.localScale;
            _baseIntensity = Light != null ? Light.intensity : 0f;
            _phase = (transform.position.x * 3.7f + transform.position.z * 1.3f) % 6.28f;
        }

        private void Update()
        {
            var t = Time.time * 7f + _phase;
            var k = 0.85f + 0.15f * Mathf.Sin(t) + 0.08f * Mathf.Sin(t * 2.3f);
            if (Light != null) Light.intensity = _baseIntensity * k;
            else transform.localScale = new Vector3(_base.x, _base.y * k, _base.z);
        }
    }
}
