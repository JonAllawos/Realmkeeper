using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Places;
using UnityEngine;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;
using Object = UnityEngine.Object;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 13 (ADR-0063): builds a place's geometry from its scene view — what the world says it is, as far as the
    /// player may see it. One cell is one combat square (1.5 m). Floors, walls and ceilings are generated meshes (one per
    /// floor and surface, with colliders, so the character and the camera meet real walls); a wall stands wherever a floor
    /// cell meets rock or a room it does not open onto. Doors, stairs, ladders, the way out, lights and the objects of the
    /// place are simple shapes in the place's own material. Nothing here holds state: the scene is rebuilt from the world.
    /// </summary>
    public static partial class PlaceEnvironmentBuilder
    {
        public const float Cell = 1.5f;
        public const float WallThickness = 0.18f;

        public sealed class Palette
        {
            public Color Floor;
            public Color Wall;
            public Color Ceiling;
            public Color Trim;
            public Color Light;
        }

        public static Palette For(string material)
        {
            switch (material)
            {
                case "shored": return new Palette { Floor = new Color(0.30f, 0.26f, 0.22f), Wall = new Color(0.36f, 0.32f, 0.28f), Ceiling = new Color(0.22f, 0.19f, 0.16f), Trim = ExplorationMaterials.DarkTimber, Light = new Color(1f, 0.72f, 0.42f) };
                case "dressed_stone": return new Palette { Floor = new Color(0.46f, 0.45f, 0.42f), Wall = new Color(0.58f, 0.56f, 0.52f), Ceiling = new Color(0.36f, 0.35f, 0.33f), Trim = new Color(0.42f, 0.40f, 0.37f), Light = new Color(1f, 0.78f, 0.52f) };
                case "brick": return new Palette { Floor = new Color(0.36f, 0.30f, 0.27f), Wall = new Color(0.46f, 0.30f, 0.24f), Ceiling = new Color(0.30f, 0.22f, 0.19f), Trim = new Color(0.30f, 0.24f, 0.20f), Light = new Color(1f, 0.70f, 0.45f) };
                case "timber": return new Palette { Floor = new Color(0.40f, 0.35f, 0.26f), Wall = ExplorationMaterials.Timber, Ceiling = ExplorationMaterials.DarkTimber, Trim = ExplorationMaterials.DarkTimber, Light = new Color(1f, 0.85f, 0.65f) };
                case "interior": return new Palette { Floor = new Color(0.45f, 0.34f, 0.24f), Wall = ExplorationMaterials.Plaster, Ceiling = ExplorationMaterials.DarkTimber, Trim = ExplorationMaterials.Timber, Light = new Color(1f, 0.80f, 0.56f) };
                default: return new Palette { Floor = new Color(0.33f, 0.31f, 0.29f), Wall = new Color(0.40f, 0.37f, 0.34f), Ceiling = new Color(0.27f, 0.25f, 0.23f), Trim = new Color(0.30f, 0.28f, 0.26f), Light = new Color(1f, 0.74f, 0.46f) };
            }
        }

        public static Vector3 CellCentre(PlaceSceneView v, int floor, int x, int z) => new Vector3((x + 0.5f) * Cell, Elevation(v, floor), (z + 0.5f) * Cell);

        public static float Elevation(PlaceSceneView v, int floor)
        {
            foreach (var f in v.Floors) if (f.Index == floor) return f.Elevation;
            return 0f;
        }

        /// <summary>Which room (or passage) owns each floor cell of a floor.</summary>
        public static Dictionary<(int, int), string> Owners(PlaceSceneView v, int floor)
        {
            var owner = new Dictionary<(int, int), string>();
            foreach (var r in v.Rooms)
            {
                if (r.Floor != floor) continue;
                for (var x = r.Rect.X; x < r.Rect.MaxX; x++)
                    for (var z = r.Rect.Z; z < r.Rect.MaxZ; z++)
                        owner[(x, z)] = r.Id;
            }

            foreach (var p in v.Passages)
            {
                if (p.Kind == ConnectionKind.Entrance || p.Kind == ConnectionKind.Stairs || p.Kind == ConnectionKind.Ladder) continue;
                var a = v.Room(p.A);
                if (a == null || a.Floor != floor) continue;
                for (var x = p.Rect.X; x < p.Rect.MaxX; x++)
                    for (var z = p.Rect.Z; z < p.Rect.MaxZ; z++)
                        if (!owner.ContainsKey((x, z))) owner[(x, z)] = "c:" + p.Id;
            }

            return owner;
        }

        /// <summary>True when the edge between two owned cells is open (same room; a passage and a room it joins; two rooms through a doorway).</summary>
        public static bool Open(PlaceSceneView v, string a, string b, (int, int) ca, (int, int) cb)
        {
            if (a == b) return true;
            var pa = a.StartsWith("c:", StringComparison.Ordinal) ? a.Substring(2) : null;
            var pb = b.StartsWith("c:", StringComparison.Ordinal) ? b.Substring(2) : null;
            if (pa != null && pb != null) return false;
            if (pa != null || pb != null)
            {
                var pid = pa ?? pb;
                var room = pa != null ? b : a;
                var p = v.Passages.Find(x => x.Id == pid);
                return p != null && (p.A == room || p.B == room);
            }

            foreach (var p in v.Passages)
            {
                if (p.Kind == ConnectionKind.Entrance || p.Kind == ConnectionKind.Stairs || p.Kind == ConnectionKind.Ladder) continue;
                if (!((p.A == a && p.B == b) || (p.A == b && p.B == a))) continue;
                if (p.Rect.Contains(ca.Item1, ca.Item2) || p.Rect.Contains(cb.Item1, cb.Item2)) return true;
            }

            return false;
        }

        private sealed class MeshParts
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float uScale, float vScale)
            {
                var i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                for (var k = 0; k < 4; k++) N.Add(normal);
                U.Add(new Vector2(0, 0)); U.Add(new Vector2(uScale, 0)); U.Add(new Vector2(uScale, vScale)); U.Add(new Vector2(0, vScale));
                T.Add(i); T.Add(i + 2); T.Add(i + 1);
                T.Add(i); T.Add(i + 3); T.Add(i + 2);
            }

            public Mesh Build(string name, GeneratedAssets assets)
            {
                var m = assets.Track(new Mesh { name = name });
                if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetUVs(0, U);
                m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Builds the whole place: every floor the player may see, under one root.</summary>
        public static GameObject Build(PlaceSceneView v, ExplorationMaterials materials, out GeneratedAssets assets, ArtCatalog catalog = null)
        {
            var root = new GameObject("Place — " + v.Name);
            assets = root.AddComponent<GeneratedAssets>();
            var palette = For(v.Material);
            // Phase 14: each kind of place in its own surfaces — the kit's textured materials when the art catalog is present.
            var look = Surfaces(v, catalog);
            var floorMat = look.Wall != null ? look.Floor : materials.Get(palette.Floor, 0.05f);
            var wallMat = look.Wall ?? materials.Get(palette.Wall, 0.05f);
            var ceilMat = look.Ceiling ?? materials.Get(palette.Ceiling, 0.02f);
            foreach (var f in v.Floors)
            {
                var owner = Owners(v, f.Index);
                if (owner.Count == 0) continue;
                var floorParts = new MeshParts();
                var outdoorParts = new MeshParts();
                var wallParts = new MeshParts();
                var ceilParts = new MeshParts();
                var y = f.Elevation;
                foreach (var pair in owner)
                {
                    var (x, z) = pair.Key;
                    var height = Height(v, pair.Value);
                    var x0 = x * Cell;
                    var z0 = z * Cell;
                    var x1 = x0 + Cell;
                    var z1 = z0 + Cell;
                    var bridged = IsBridgeCell(v, pair.Value);
                    var fp = OpenSky(v, pair.Value) ? outdoorParts : floorParts;
                    if (!bridged) // a bridge's deck (or its gap) is drawn with the bridge, as it stands (Phase 14)
                    {
                        fp.Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.up, 1f, 1f);
                        fp.Quad(new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1), Vector3.up, 1f, 1f); // both windings: seen from any side
                    }
                    if (!OpenSky(v, pair.Value))
                        ceilParts.Quad(new Vector3(x0, y + height, z0), new Vector3(x1, y + height, z0), new Vector3(x1, y + height, z1), new Vector3(x0, y + height, z1), Vector3.down, 1f, 1f);
                    if (!OpenSky(v, pair.Value))
                        ceilParts.Quad(new Vector3(x0, y + height, z0), new Vector3(x0, y + height, z1), new Vector3(x1, y + height, z1), new Vector3(x1, y + height, z0), Vector3.down, 1f, 1f);

                    // Walls on the four edges, facing into this cell.
                    Edge(v, owner, pair, (x, z - 1), wallParts, new Vector3(x1, y, z0), new Vector3(x0, y, z0), Vector3.forward, height); // south
                    Edge(v, owner, pair, (x, z + 1), wallParts, new Vector3(x0, y, z1), new Vector3(x1, y, z1), Vector3.back, height); // north
                    Edge(v, owner, pair, (x - 1, z), wallParts, new Vector3(x0, y, z0), new Vector3(x0, y, z1), Vector3.right, height); // west
                    Edge(v, owner, pair, (x + 1, z), wallParts, new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.left, height); // east
                }

                Surface(root.transform, "Floor " + f.Index, floorParts.Build("floor-" + f.Index, assets), floorMat, true);
                Surface(root.transform, "Ground " + f.Index, outdoorParts.Build("ground-" + f.Index, assets), look.Outdoor ?? materials.Get(ExplorationMaterials.Grass, 0.02f), true);
                Surface(root.transform, "Walls " + f.Index, wallParts.Build("walls-" + f.Index, assets), wallMat, true);
                Surface(root.transform, "Ceiling " + f.Index, ceilParts.Build("ceiling-" + f.Index, assets), ceilMat, true);
            }

            Lights(v, root.transform, palette);
            Dress(v, root.transform, materials, palette);
            Dress14(v, root.transform, materials, palette, catalog);
            if (v.Category == PlaceCategory.Interior) Furnish(v, root.transform, catalog); // Phase 21 (ADR-0108)
            return root;
        }

        /// <summary>
        /// Phase 21 (ADR-0108): a building's rooms furnished by their kind (beds, tables and chairs, workbenches, barrels),
        /// from the deterministic plan, with the kit's models; a warm light in each furnished room. Scenery only: it is
        /// rebuilt the same each visit from the place's own ID and never saved.
        /// </summary>
        public static int Furnish(PlaceSceneView v, Transform root, ArtCatalog catalog)
        {
            if (catalog == null) return 0;
            var rooms = new List<FurnishRoom>();
            var floorOf = new Dictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var r in v.Rooms)
            {
                floorOf[r.Id] = r.Floor;
                if (!r.Secret) rooms.Add(new FurnishRoom(r.Id, r.Floor, r.Rect, r.Kind));
            }

            var doors = new List<(int, CellRect)>();
            foreach (var p in v.Passages)
            {
                if (p.A != null && floorOf.TryGetValue(p.A, out var fa)) doors.Add((fa, p.Rect));
                if (p.B != null && floorOf.TryGetValue(p.B, out var fb))
                {
                    doors.Add((fb, p.Rect));
                    if (p.Kind == ConnectionKind.Stairs || p.Kind == ConnectionKind.Ladder) doors.Add((fb, new CellRect(p.ToX, p.ToZ, 1, 1)));
                }
            }

            var features = new List<(int, int, int)>();
            foreach (var f in v.Features) features.Add((f.Room != null && floorOf.TryGetValue(f.Room, out var ff) ? ff : 0, f.X, f.Z));
            var blocked = InteriorFurnishing.Blocked(doors, features);
            var plan = InteriorFurnishing.Plan(rooms, blocked, Realmkeeper.Domain.Randomness.SeedMixer.HashString(v.PlaceId ?? v.Name ?? "place"));

            var parent = new GameObject("Furnishings").transform;
            parent.SetParent(root, false);
            var lit = new HashSet<string>(System.StringComparer.Ordinal);
            var placed = 0;
            foreach (var piece in plan)
            {
                var at = CellCentre(v, piece.Floor, piece.X, piece.Z);
                // Against a wall: back from the cell's centre toward the wall it faces away from.
                if (piece.AgainstWall) at -= Quaternion.Euler(0f, piece.Yaw, 0f) * Vector3.forward * (Cell / 2f - 0.42f);
                if (piece.Kind == "lantern") at.y += 1.8f;
                if (KitProps.Place(parent, catalog, piece.Prop, at, piece.Yaw, piece.Size, piece.Solid) == null) continue;
                placed++;
                if (lit.Add(piece.Room) && lit.Count <= 10)
                {
                    var room = v.Room(piece.Room);
                    var light = new GameObject("Room light").AddComponent<Light>();
                    light.transform.SetParent(parent, false);
                    var r = room.Rect;
                    light.transform.localPosition = new Vector3((r.X + r.W / 2f) * Cell, Elevation(v, room.Floor) + Mathf.Min(room.Ceiling, 3.2f) - 0.5f, (r.Z + r.D / 2f) * Cell);
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.78f, 0.52f); // candle and lamp light
                    light.range = Mathf.Max(r.W, r.D) * Cell + 1.5f;
                    light.intensity = 1.1f;
                    light.shadows = LightShadows.None;
                }
            }

            return placed;
        }

        private static float Height(PlaceSceneView v, string owner)
        {
            if (owner.StartsWith("c:", StringComparison.Ordinal))
            {
                var p = v.Passages.Find(x => x.Id == owner.Substring(2));
                var a = p == null ? null : v.Room(p.A);
                var b = p == null ? null : v.Room(p.B);
                return Mathf.Min(3.0f, Mathf.Min(a?.Ceiling ?? 3f, b?.Ceiling ?? 3f));
            }

            return v.Room(owner)?.Ceiling ?? 3f;
        }

        private static bool OpenSky(PlaceSceneView v, string owner) => !owner.StartsWith("c:", StringComparison.Ordinal) && (v.Room(owner)?.Tags.Contains("open_sky") ?? false);

        private static void Edge(PlaceSceneView v, Dictionary<(int, int), string> owner, KeyValuePair<(int, int), string> me, (int, int) other, MeshParts walls, Vector3 a, Vector3 b, Vector3 inward, float height)
        {
            if (owner.TryGetValue(other, out var them) && Open(v, me.Value, them, me.Key, other)) return;
            if (IsEntranceEdge(v, me, other)) return; // the way out is an opening, closed by its own blocker
            walls.Quad(a, b, b + Vector3.up * height, a + Vector3.up * height, inward, Vector3.Distance(a, b) / Cell, height / Cell);
        }

        private static bool IsEntranceEdge(PlaceSceneView v, KeyValuePair<(int, int), string> me, (int, int) other)
        {
            foreach (var p in v.Passages)
            {
                if (p.Kind != ConnectionKind.Entrance || p.A != me.Value) continue;
                var room = v.Room(p.A);
                if (room == null || !p.Rect.Contains(me.Key.Item1, me.Key.Item2) || room.Rect.Contains(other.Item1, other.Item2)) continue;
                if (other.Item2 < room.Rect.Z) return true; // the south edge, where the way in is
            }

            return false;
        }

        private static void Surface(Transform parent, string name, Mesh mesh, Material material, bool collide)
        {
            if (mesh.vertexCount == 0) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void Lights(PlaceSceneView v, Transform root, Palette palette)
        {
            foreach (var r in v.Rooms)
            {
                if (!r.Lit) continue;
                var centre = CellCentre(v, r.Floor, r.Rect.CenterX, r.Rect.CenterZ);
                var go = new GameObject("Light " + r.Name);
                go.transform.SetParent(root, false);
                go.transform.position = centre + Vector3.up * Mathf.Max(1.8f, r.Ceiling - 0.6f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = palette.Light;
                light.range = Mathf.Max(r.Rect.W, r.Rect.D) * Cell * 1.1f;
                light.intensity = r.Tags.Contains("open_sky") ? 2.2f : 1.6f;
                light.shadows = LightShadows.None;
            }
        }

        // ----- Doors, stairs and the things of the place -------------------------------------------

        private static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material, float yaw = 0f, bool collide = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collide) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static GameObject Shape(Transform parent, PrimitiveType type, string name, Vector3 centre, Vector3 size, Material material, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>The door panel of a passage: where it meets room A (or, for a doorway inside a room, the shared wall).</summary>
        public static (Vector3 Centre, float Yaw, float Width) DoorFrame(PlaceSceneView v, PassageView p)
        {
            var a = v.Room(p.A);
            var y = a == null ? 0f : Elevation(v, a.Floor);
            var r = p.Rect;
            var horizontal = r.W >= r.D; // runs east-west → the door spans x
            if (a != null && a.Rect.Overlaps(p.Rect))
            {
                // A doorway inside A: the door sits on A's edge facing B.
                var b = v.Room(p.B);
                if (b != null && (b.Rect.Z == a.Rect.MaxZ || b.Rect.MaxZ == a.Rect.Z))
                {
                    var z = b.Rect.Z == a.Rect.MaxZ ? a.Rect.MaxZ * Cell : a.Rect.Z * Cell;
                    return (new Vector3((r.X + r.W / 2f) * Cell, y, z), 0f, r.W * Cell);
                }

                var x = b != null && b.Rect.X == a.Rect.MaxX ? a.Rect.MaxX * Cell : a.Rect.X * Cell;
                return (new Vector3(x, y, (r.Z + r.D / 2f) * Cell), 90f, r.D * Cell);
            }

            // A corridor: the door sits at its mouth on A's side.
            if (a == null) return (new Vector3((r.X + r.W / 2f) * Cell, y, (r.Z + r.D / 2f) * Cell), 0f, Cell);
            if (r.D > r.W || (r.W == r.D && (a.Rect.MaxZ == r.Z || a.Rect.Z == r.MaxZ)))
            {
                var z = a.Rect.MaxZ == r.Z ? r.Z * Cell : r.MaxZ * Cell;
                return (new Vector3((r.X + r.W / 2f) * Cell, y, z), 0f, r.W * Cell);
            }

            var xx = a.Rect.MaxX == r.X ? r.X * Cell : r.MaxX * Cell;
            return (new Vector3(xx, y, (r.Z + r.D / 2f) * Cell), 90f, r.D * Cell);
        }

        private static void Dress(PlaceSceneView v, Transform root, ExplorationMaterials materials, Palette palette)
        {
            var props = new GameObject("Things").transform;
            props.SetParent(root, false);
            var wood = materials.Get(ExplorationMaterials.Timber);
            var darkWood = materials.Get(ExplorationMaterials.DarkTimber);
            var stone = materials.Get(ExplorationMaterials.Stone);
            var iron = materials.Get(new Color(0.22f, 0.22f, 0.24f), 0.4f);
            var paper = materials.Get(new Color(0.88f, 0.84f, 0.72f));
            var cloth = materials.Get(new Color(0.55f, 0.18f, 0.16f));
            var web = materials.Get(new Color(0.86f, 0.86f, 0.84f), 0.1f);
            var ember = materials.Get(new Color(1f, 0.45f, 0.12f), 0.6f);
            var trim = materials.Get(palette.Trim);

            // Timber shoring in shored workings, tagged furniture by room kind.
            foreach (var r in v.Rooms)
            {
                var y = Elevation(v, r.Floor);
                if (v.Material == "shored")
                    for (var x = r.Rect.X + 1; x < r.Rect.MaxX - 1; x += 3)
                    {
                        Box(props, "Prop", new Vector3((x + 0.5f) * Cell, y + r.Ceiling / 2f, r.Rect.Z * Cell + 0.2f), new Vector3(0.22f, r.Ceiling, 0.22f), trim);
                        Box(props, "Prop", new Vector3((x + 0.5f) * Cell, y + r.Ceiling / 2f, r.Rect.MaxZ * Cell - 0.2f), new Vector3(0.22f, r.Ceiling, 0.22f), trim);
                        Box(props, "Beam", new Vector3((x + 0.5f) * Cell, y + r.Ceiling - 0.15f, (r.Rect.Z + r.Rect.D / 2f) * Cell), new Vector3(0.22f, 0.22f, r.Rect.D * Cell - 0.2f), trim);
                    }

                if (r.Kind == RoomKind.Gallery && v.Material == "shored")
                    Box(props, "Rail", new Vector3((r.Rect.X + r.Rect.W / 2f) * Cell, y + 0.04f, (r.Rect.CenterZ + 0.5f) * Cell), new Vector3(r.Rect.W * Cell - 0.4f, 0.06f, 0.9f), iron);
                if (r.Kind == RoomKind.Barracks)
                    for (var i = 0; i < Math.Min(3, r.Rect.D - 2); i++)
                        Box(props, "Bunk", new Vector3((r.Rect.MaxX - 0.6f) * Cell, y + 0.35f, (r.Rect.Z + 1.5f + i * 1.6f) * Cell), new Vector3(1.0f, 0.4f, 2.0f), wood);
                if (r.Kind == RoomKind.Armoury)
                    Box(props, "Rack", new Vector3((r.Rect.MaxX - 0.4f) * Cell, y + 0.9f, (r.Rect.Z + r.Rect.D / 2f) * Cell), new Vector3(0.3f, 1.8f, 2.4f), darkWood);
                if (r.Kind == RoomKind.Yard)
                    Box(props, "Well", new Vector3((r.Rect.X + 2) * Cell, y + 0.4f, (r.Rect.Z + 2) * Cell), new Vector3(1.2f, 0.8f, 1.2f), stone);
                if (r.Kind == RoomKind.Tomb)
                    Box(props, "Plinth", new Vector3((r.Rect.X + 0.5f) * Cell, y + 0.5f, (r.Rect.MaxZ - 0.6f) * Cell), new Vector3(0.9f, 1.0f, 0.9f), stone);
                if (r.Kind == RoomKind.Collapse || (r.Kind == RoomKind.Den && v.Material == "shored"))
                {
                    Shape(props, PrimitiveType.Sphere, "Rubble", CellCentre(v, r.Floor, r.Rect.MaxX - 2, r.Rect.MaxZ - 2) + Vector3.up * 0.2f, new Vector3(2.4f, 1.0f, 1.8f), stone);
                    Shape(props, PrimitiveType.Sphere, "Rubble", CellCentre(v, r.Floor, r.Rect.MaxX - 1, r.Rect.MaxZ - 1) + Vector3.up * 0.3f, new Vector3(1.6f, 1.2f, 1.6f), stone);
                }

                if (r.Lit)
                {
                    // A torch on the north wall.
                    var t = new Vector3((r.Rect.X + r.Rect.W / 2f) * Cell, y + 1.8f, r.Rect.MaxZ * Cell - 0.12f);
                    Box(props, "Torch", t, new Vector3(0.08f, 0.45f, 0.08f), darkWood);
                    Box(props, "Flame", t + Vector3.up * 0.28f, new Vector3(0.12f, 0.16f, 0.12f), ember);
                }

                var hazard = r.Hazard;
                if (hazard == "toxic_air") // Phase 14: deep water is drawn as it stands now (drained or not), with the bridges
                {
                    var tint = hazard == "toxic_air" ? new Color(0.45f, 0.62f, 0.30f) : new Color(0.10f, 0.20f, 0.30f);
                    var plane = Box(props, hazard, new Vector3((r.Rect.X + r.Rect.W / 2f) * Cell, y + (hazard == "toxic_air" ? 0.6f : 0.05f), (r.Rect.Z + r.Rect.D / 2f) * Cell),
                        new Vector3(r.Rect.W * Cell - 0.1f, 0.05f, r.Rect.D * Cell - 0.1f), materials.Get(tint, hazard == "deep_water" ? 0.9f : 0.1f));
                    plane.name = "Hazard " + hazard;
                }
            }

            // Stairs and ladders.
            foreach (var p in v.Passages)
            {
                if (p.Kind != ConnectionKind.Stairs && p.Kind != ConnectionKind.Ladder) continue;
                var a = v.Room(p.A);
                var b = v.Room(p.B);
                if (a == null) continue;
                var foot = CellCentre(v, a.Floor, p.Rect.X, p.Rect.Z);
                var up = b != null && Elevation(v, b.Floor) > Elevation(v, a.Floor);
                if (p.Kind == ConnectionKind.Ladder)
                {
                    Box(props, "Ladder rail", foot + new Vector3(-0.3f, 1.5f, 0.5f), new Vector3(0.08f, 3.0f, 0.08f), wood);
                    Box(props, "Ladder rail", foot + new Vector3(0.3f, 1.5f, 0.5f), new Vector3(0.08f, 3.0f, 0.08f), wood);
                    for (var i = 0; i < 8; i++) Box(props, "Rung", foot + new Vector3(0f, 0.3f + i * 0.35f, 0.5f), new Vector3(0.6f, 0.05f, 0.05f), wood);
                    if (!up) Box(props, "Shaft", foot + new Vector3(0f, 0.01f, 0f), new Vector3(1.2f, 0.02f, 1.2f), materials.Get(new Color(0.05f, 0.05f, 0.05f)));
                }
                else
                {
                    for (var i = 0; i < 5; i++)
                        Box(props, "Step", foot + new Vector3(0f, up ? 0.1f + i * 0.2f : -0.1f - i * 0.05f, -0.5f + i * 0.25f), new Vector3(1.3f, 0.2f, 0.3f), up ? stone : materials.Get(new Color(0.12f, 0.11f, 0.10f)));
                }

                if (b != null)
                {
                    var landing = CellCentre(v, b.Floor, p.ToX, p.ToZ);
                    Box(props, "Landing", landing + Vector3.up * 0.02f, new Vector3(1.2f, 0.04f, 1.2f), up ? materials.Get(new Color(0.12f, 0.11f, 0.10f)) : stone);
                }
            }

            // The way out: daylight beyond the threshold.
            foreach (var p in v.Passages)
            {
                if (p.Kind != ConnectionKind.Entrance) continue;
                var a = v.Room(p.A);
                if (a == null) continue;
                var y = Elevation(v, a.Floor);
                var centre = new Vector3((p.Rect.X + p.Rect.W / 2f) * Cell, y, a.Rect.Z * Cell);
                var glow = Box(props, "Daylight", centre + new Vector3(0f, 1.3f, -0.25f), new Vector3(p.Rect.W * Cell, 2.6f, 0.05f), materials.Get(new Color(0.95f, 0.92f, 0.80f), 0.0f));
                glow.name = "Way out " + p.Id;
                Box(root, "Threshold", centre + new Vector3(0f, 1.3f, -0.4f), new Vector3(p.Rect.W * Cell, 2.6f, 0.2f), materials.Get(Color.black), 0f, collide: true).GetComponent<Renderer>().enabled = false;
                var lightGo = new GameObject("Daylight light");
                lightGo.transform.SetParent(root, false);
                lightGo.transform.position = centre + new Vector3(0f, 1.6f, 0.6f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.95f, 0.93f, 0.85f);
                l.range = 6f;
                l.intensity = 1.4f;
            }
        }

        /// <summary>A door panel (closed: solid and blocking; open: swung aside; broken: in pieces on the floor).</summary>
        public static GameObject Door(Transform parent, PlaceSceneView v, PassageView p, ExplorationMaterials materials)
        {
            var (centre, yaw, width) = DoorFrame(v, p);
            var go = new GameObject("Door " + p.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var a = v.Room(p.A);
            var height = Mathf.Min(2.4f, (a?.Ceiling ?? 3f) - 0.2f);
            var mat = materials.Get(p.Kind == ConnectionKind.Secret ? PlaceEnvironmentBuilder.For(v.Material).Wall : ExplorationMaterials.DarkTimber);
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(go.transform, false);
            panel.GetComponent<Renderer>().sharedMaterial = mat;
            switch (p.Door)
            {
                case DoorState.Open:
                    panel.transform.localPosition = new Vector3(-width / 2f, height / 2f, width / 2f);
                    panel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    panel.transform.localScale = new Vector3(width, height, 0.08f);
                    Object.Destroy(panel.GetComponent<Collider>());
                    break;
                case DoorState.Broken:
                    panel.transform.localPosition = new Vector3(0f, 0.05f, 0.6f);
                    panel.transform.localRotation = Quaternion.Euler(90f, 10f, 0f);
                    panel.transform.localScale = new Vector3(width * 0.8f, height * 0.6f, 0.06f);
                    Object.Destroy(panel.GetComponent<Collider>());
                    break;
                default:
                    panel.transform.localPosition = new Vector3(0f, height / 2f, 0f);
                    panel.transform.localScale = new Vector3(width, height, 0.1f);
                    if (p.Door == DoorState.Locked || p.Door == DoorState.Barred)
                    {
                        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        bar.name = p.Door == DoorState.Barred ? "Bar" : "Lock";
                        Object.Destroy(bar.GetComponent<Collider>());
                        bar.transform.SetParent(go.transform, false);
                        bar.transform.localPosition = new Vector3(p.Door == DoorState.Barred ? 0f : width * 0.3f, height * 0.5f, -0.08f);
                        bar.transform.localScale = p.Door == DoorState.Barred ? new Vector3(width, 0.12f, 0.06f) : new Vector3(0.12f, 0.16f, 0.06f);
                        bar.GetComponent<Renderer>().sharedMaterial = materials.Get(new Color(0.22f, 0.22f, 0.24f), 0.4f);
                    }

                    break;
            }

            return go;
        }

        /// <summary>The shape of a feature (the object the player interacts with).</summary>
        /// <summary>
        /// Phase 18: the CC0 kit model that draws a feature, where one fits what it is (a wooden chest, a crate, a lectern, an
        /// anvil, a bed), or null to keep the built shape.
        /// </summary>
        public static string KitFor(FeatureView f)
        {
            switch (f.DefinitionId)
            {
                case "chest": case "reliquary": return "Chest_Wood";
                case "crate": case "ore_crate": return "Crate_Wooden";
                case "strongbox": case "drowned_strongbox": case "reeves_strongbox": return "Crate_Metal";
                case "hunters_cache": return "Bag";
                case "grain_bin": return "Barrel_Apples";
                case "burial_register": case "foremans_ledger": case "toll_ledger": case "watch_log": case "granary_ledger_in": return "BookStand";
                case "forge": return "Anvil";
                case "workbench": return "Workbench";
                case "bed": return "Bed_Twin1";
                default: return null;
            }
        }

        public static GameObject Feature(Transform parent, PlaceSceneView v, FeatureView f, ExplorationMaterials materials, ArtCatalog catalog = null)
        {
            var room = v.Room(f.Room);
            var at = CellCentre(v, room?.Floor ?? 0, f.X, f.Z);
            var go = new GameObject("Feature " + f.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var t = go.transform;
            Material M(Color c, float s = 0.15f) => materials.Get(c, s);
            var wood = M(ExplorationMaterials.Timber);
            var stone = M(ExplorationMaterials.Stone);
            var iron = M(new Color(0.22f, 0.22f, 0.24f), 0.4f);
            var paper = M(new Color(0.88f, 0.84f, 0.72f));
            var opened = (f.Flags & FeatureFlags.Opened) != 0;
            void Part(PrimitiveType type, Vector3 local, Vector3 size, Material m, Vector3 euler = default)
            {
                var p = GameObject.CreatePrimitive(type);
                Object.Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(t, false);
                p.transform.localPosition = local;
                p.transform.localRotation = Quaternion.Euler(euler);
                p.transform.localScale = size;
                p.GetComponent<Renderer>().sharedMaterial = m;
            }

            // Phase 18: kit models where they fit (the built shapes remain the fallback when the kits are not installed).
            GameObject Kit(string id, Vector3 offset = default, float scale = 1f)
            {
                var prefab = catalog?.SceneryModel(id);
                if (prefab == null) return null;
                var k = Object.Instantiate(prefab, t, false);
                k.name = id;
                k.transform.localPosition = offset;
                k.transform.localScale = k.transform.localScale * scale;
                foreach (var c in k.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                return k;
            }

            var kit = KitFor(f);
            if (kit != null && Kit(kit) != null) return go;
            if (f.Kind == FeatureKind.Altar && catalog?.SceneryModel("CandleStick_Triple") != null)
            {
                Part(PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1.0f, 0.8f), stone);
                Kit("CandleStick_Triple", new Vector3(-0.45f, 1.0f, 0f));
                Kit("CandleStick_Triple", new Vector3(0.45f, 1.0f, 0f));
                return go;
            }

            if (f.DefinitionId != null && f.DefinitionId.StartsWith("bell_", System.StringComparison.Ordinal))
            {
                // The ringers' bells: a bronze bell on an iron bracket, each a little different (ferry, mill, forge).
                var bronze = M(f.DefinitionId == "bell_ferry" ? new Color(0.55f, 0.42f, 0.22f) : f.DefinitionId == "bell_mill" ? new Color(0.62f, 0.48f, 0.26f) : new Color(0.48f, 0.36f, 0.2f), 0.6f);
                Part(PrimitiveType.Cube, new Vector3(0f, 1.0f, 0f), new Vector3(0.08f, 2.0f, 0.08f), iron);
                Part(PrimitiveType.Cube, new Vector3(0f, 1.95f, -0.2f), new Vector3(0.06f, 0.06f, 0.45f), iron);
                Part(PrimitiveType.Cylinder, new Vector3(0f, 1.62f, -0.4f), new Vector3(0.38f, 0.22f, 0.38f), bronze);
                Part(PrimitiveType.Sphere, new Vector3(0f, 1.85f, -0.4f), new Vector3(0.28f, 0.2f, 0.28f), bronze);
                return go;
            }

            switch (f.Kind)
            {
                case FeatureKind.Container:
                    switch (f.DefinitionId)
                    {
                        case "sarcophagus":
                            Part(PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 2.0f), stone);
                            Part(PrimitiveType.Cube, opened ? new Vector3(0.45f, 0.75f, 0.3f) : new Vector3(0f, 0.85f, 0f), new Vector3(1.0f, 0.12f, 2.1f), stone, opened ? new Vector3(0f, 0f, 25f) : default);
                            break;
                        case "bones_pile":
                            for (var i = 0; i < 5; i++) Part(PrimitiveType.Capsule, new Vector3(-0.3f + i * 0.15f, 0.08f, (i % 2) * 0.2f), new Vector3(0.08f, 0.25f, 0.08f), M(new Color(0.85f, 0.82f, 0.72f)), new Vector3(90f, i * 40f, 0f));
                            break;
                        case "niche":
                            Part(PrimitiveType.Cube, new Vector3(0f, 1.0f, 0f), new Vector3(1.2f, 0.7f, 0.5f), M(new Color(0.20f, 0.18f, 0.16f)));
                            break;
                        default:
                            var crate = f.DefinitionId != "chest";
                            Part(PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), crate ? new Vector3(0.8f, 0.6f, 0.8f) : new Vector3(1.0f, 0.55f, 0.6f), crate ? wood : M(ExplorationMaterials.DarkTimber));
                            if (!crate) Part(PrimitiveType.Cube, opened ? new Vector3(0f, 0.75f, -0.3f) : new Vector3(0f, 0.6f, 0f), new Vector3(1.02f, 0.1f, 0.62f), iron, opened ? new Vector3(-70f, 0f, 0f) : default);
                            break;
                    }

                    break;
                case FeatureKind.Document:
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.6f), wood);
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.91f, 0f), new Vector3(0.45f, 0.02f, 0.32f), paper);
                    break;
                case FeatureKind.Trap:
                    if (f.Hidden) break; // not seen yet: nothing to draw
                    var safe = (f.Flags & FeatureFlags.Disarmed) != 0;
                    var col = safe ? M(new Color(0.3f, 0.3f, 0.3f)) : M(new Color(0.72f, 0.18f, 0.12f), 0.3f);
                    if (f.TrapId == "spike_pit") Part(PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(1.2f, 0.02f, 1.2f), M(new Color(0.05f, 0.04f, 0.03f)));
                    else if (f.TrapId == "tripwire_alarm") Part(PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(1.4f, 0.02f, 0.02f), col);
                    else if (f.TrapId == "glyph_ward") Part(PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), new Vector3(1.1f, 0.005f, 1.1f), safe ? col : M(new Color(0.95f, 0.55f, 0.15f), 0.8f));
                    else Part(PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.04f, 0.9f), col);
                    break;
                case FeatureKind.Mechanism:
                    Part(PrimitiveType.Cube, new Vector3(0f, 1.0f, 0f), new Vector3(0.3f, 0.4f, 0.15f), iron);
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 1.25f, -0.15f), new Vector3(0.05f, 0.3f, 0.05f), iron, (f.Flags & FeatureFlags.Used) != 0 ? new Vector3(-40f, 0f, 0f) : new Vector3(40f, 0f, 0f));
                    break;
                case FeatureKind.Captive:
                    if ((f.Flags & FeatureFlags.Freed) != 0)
                    {
                        Part(PrimitiveType.Cube, new Vector3(0.6f, 0.15f, 0f), new Vector3(0.25f, 0.25f, 2.2f), wood, new Vector3(0f, 30f, 0f));
                        break;
                    }

                    Part(PrimitiveType.Capsule, new Vector3(0f, 0.35f, 0f), new Vector3(0.45f, 0.5f, 0.45f), M(new Color(0.45f, 0.38f, 0.30f)), new Vector3(0f, 0f, 70f));
                    Part(PrimitiveType.Sphere, new Vector3(-0.55f, 0.45f, 0f), new Vector3(0.3f, 0.3f, 0.3f), M(new Color(0.80f, 0.64f, 0.52f)));
                    Part(PrimitiveType.Cube, new Vector3(0.1f, 0.65f, 0f), new Vector3(0.25f, 0.25f, 2.2f), wood, new Vector3(0f, 30f, 10f));
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 0.9f, 1.0f), M(new Color(0.88f, 0.88f, 0.86f), 0.05f));
                    break;
                case FeatureKind.Workstation:
                    if (f.DefinitionId == "forge")
                    {
                        Part(PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 1.0f), stone);
                        Part(PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f), new Vector3(0.8f, 0.06f, 0.6f), M(new Color(1f, 0.42f, 0.12f), 0.7f));
                        Part(PrimitiveType.Cube, new Vector3(0.9f, 0.4f, 0f), new Vector3(0.4f, 0.8f, 0.3f), iron);
                    }
                    else Part(PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.9f, 0.7f), wood);
                    break;
                case FeatureKind.Altar:
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1.0f, 0.8f), stone);
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 1.05f, 0f), new Vector3(0.35f, 0.04f, 0.35f), iron);
                    break;
                case FeatureKind.Furniture:
                    if (f.DefinitionId == "bed")
                    {
                        Part(PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(1.0f, 0.5f, 2.0f), wood);
                        Part(PrimitiveType.Cube, new Vector3(0f, 0.52f, 0.1f), new Vector3(0.9f, 0.08f, 1.7f), M(new Color(0.70f, 0.64f, 0.50f)));
                    }
                    else
                    {
                        Part(PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1.0f, 0.6f), stone);
                        Part(PrimitiveType.Cube, new Vector3(0f, 0.25f, -0.1f), new Vector3(0.8f, 0.3f, 0.4f), M(new Color(1f, 0.45f, 0.12f), 0.7f));
                    }

                    break;
                case FeatureKind.Hazard:
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 0.9f, 1.4f), stone);
                    Part(PrimitiveType.Cube, new Vector3(0.2f, 1.4f, 0f), new Vector3(0.2f, 0.2f, 1.6f), wood, new Vector3(20f, 0f, 15f));
                    break;
                default:
                    Clue(f, Part, M, wood, stone, paper);
                    break;
            }

            return go;
        }

        private static void Clue(FeatureView f, Action<PrimitiveType, Vector3, Vector3, Material, Vector3> part, Func<Color, float, Material> m, Material wood, Material stone, Material paper)
        {
            switch (f.DefinitionId)
            {
                case "webbing":
                    part(PrimitiveType.Cube, new Vector3(0f, 1.4f, 0f), new Vector3(1.4f, 1.2f, 0.02f), m(new Color(0.86f, 0.86f, 0.84f), 0.05f), default);
                    break;
                case "old_campfire":
                    for (var i = 0; i < 6; i++) part(PrimitiveType.Sphere, new Vector3(Mathf.Cos(i) * 0.4f, 0.06f, Mathf.Sin(i) * 0.4f), new Vector3(0.18f, 0.12f, 0.18f), stone, default);
                    part(PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.02f, 0.5f), m(new Color(0.12f, 0.10f, 0.09f), 0f), default);
                    break;
                case "abandoned_tools":
                    part(PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.08f, 0.08f, 1.1f), wood, new Vector3(0f, 30f, 0f));
                    part(PrimitiveType.Cube, new Vector3(0.3f, 0.05f, 0.2f), new Vector3(0.08f, 0.08f, 0.9f), wood, new Vector3(0f, -20f, 0f));
                    part(PrimitiveType.Cube, new Vector3(0.1f, 0.1f, 0.5f), new Vector3(0.4f, 0.1f, 0.12f), m(new Color(0.22f, 0.22f, 0.24f), 0.4f), new Vector3(0f, 30f, 0f));
                    break;
                case "shoring_split":
                    part(PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.25f, 2.4f, 0.25f), wood, new Vector3(0f, 0f, 6f));
                    break;
                case "tally_marks":
                case "duty_roster":
                case "epitaph":
                case "carved_symbol":
                    part(PrimitiveType.Cube, new Vector3(0f, 1.3f, 0f), new Vector3(0.9f, 0.7f, 0.06f), f.DefinitionId == "epitaph" || f.DefinitionId == "carved_symbol" ? stone : wood, default);
                    part(PrimitiveType.Cube, new Vector3(0f, 1.3f, -0.04f), new Vector3(0.7f, 0.5f, 0.01f), paper, default);
                    break;
                case "faction_banner":
                    part(PrimitiveType.Cube, new Vector3(0f, 2.0f, 0f), new Vector3(0.9f, 1.4f, 0.02f), m(new Color(0.55f, 0.18f, 0.16f), 0.1f), default);
                    break;
                case "broken_statue":
                    part(PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.8f, 0.5f), stone, new Vector3(0f, 0f, 85f));
                    break;
                case "stolen_goods":
                    part(PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.6f, 0.8f), wood, default);
                    part(PrimitiveType.Capsule, new Vector3(0.7f, 0.3f, 0f), new Vector3(0.5f, 0.35f, 0.5f), m(new Color(0.60f, 0.51f, 0.33f), 0.1f), default);
                    break;
                case "bones":
                case "disturbed_dust":
                case "claw_marks":
                default:
                    part(PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), new Vector3(0.9f, 0.01f, 0.9f), m(new Color(0.24f, 0.21f, 0.18f), 0f), default);
                    break;
            }
        }

        /// <summary>A figure: a person (by role colour) or a creature (by kind), simple and readable.</summary>
        public static GameObject Figure(Transform parent, PlaceSceneView v, FigureView f, ExplorationMaterials materials, Vector3 at)
        {
            var go = new GameObject("Figure " + f.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            Material M(Color c) => materials.Get(c, 0.2f);
            void Part(PrimitiveType type, Vector3 local, Vector3 size, Material m, Vector3 euler = default)
            {
                var p = GameObject.CreatePrimitive(type);
                Object.Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(go.transform, false);
                p.transform.localPosition = local;
                p.transform.localRotation = Quaternion.Euler(euler);
                p.transform.localScale = size;
                p.GetComponent<Renderer>().sharedMaterial = m;
            }

            switch (f.CreatureId)
            {
                case "giant_spider":
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.6f, 0f), new Vector3(1.0f, 0.7f, 1.3f), M(new Color(0.08f, 0.07f, 0.07f)));
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0.75f), new Vector3(0.5f, 0.45f, 0.5f), M(new Color(0.12f, 0.10f, 0.09f)));
                    for (var i = 0; i < 4; i++)
                        foreach (var side in new[] { -1f, 1f })
                            Part(PrimitiveType.Cube, new Vector3(side * 0.75f, 0.4f, -0.4f + i * 0.3f), new Vector3(0.9f, 0.06f, 0.06f), M(new Color(0.10f, 0.09f, 0.08f)), new Vector3(0f, 0f, side * -25f));
                    return go;
                case "giant_rat":
                    Part(PrimitiveType.Capsule, new Vector3(0f, 0.25f, 0f), new Vector3(0.4f, 0.4f, 0.4f), M(new Color(0.32f, 0.28f, 0.25f)), new Vector3(90f, 0f, 0f));
                    return go;
                case "wolf":
                case "dire_wolf":
                case "black_bear":
                case "brown_bear":
                    var big = f.CreatureId.Contains("bear") ? 1.4f : 1f;
                    Part(PrimitiveType.Capsule, new Vector3(0f, 0.55f * big, 0f), new Vector3(0.55f * big, 0.6f * big, 0.55f * big), M(new Color(0.35f, 0.27f, 0.20f)), new Vector3(90f, 0f, 0f));
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.8f * big, 0.7f * big), new Vector3(0.4f * big, 0.4f * big, 0.45f * big), M(new Color(0.30f, 0.23f, 0.17f)));
                    return go;
            }

            Color body;
            switch (f.Role)
            {
                case "foe": body = f.CreatureId == "skeleton" ? new Color(0.86f, 0.84f, 0.76f) : f.CreatureId == "zombie" ? new Color(0.42f, 0.48f, 0.38f) : new Color(0.52f, 0.20f, 0.18f); break;
                case "guard": body = new Color(0.42f, 0.44f, 0.50f); break;
                case "follower": body = new Color(0.45f, 0.38f, 0.30f); break;
                default: body = new Color(0.50f, 0.42f, 0.32f); break;
            }

            var asleep = f.Activity == "Sleeping";
            Part(PrimitiveType.Capsule, asleep ? new Vector3(0f, 0.35f, 0f) : new Vector3(0f, 0.9f, 0f), new Vector3(0.5f, 0.75f, 0.4f), M(body), asleep ? new Vector3(90f, 0f, 0f) : default);
            Part(PrimitiveType.Sphere, asleep ? new Vector3(0f, 0.4f, 0.85f) : new Vector3(0f, 1.75f, 0f), new Vector3(0.3f, 0.32f, 0.3f), M(f.CreatureId == "skeleton" ? new Color(0.9f, 0.88f, 0.8f) : new Color(0.80f, 0.64f, 0.52f)));
            if (f.Role == "guard" || f.CreatureId == "guard" || f.CreatureId == "veteran")
                Part(PrimitiveType.Cylinder, new Vector3(0.35f, 1.0f, 0.2f), new Vector3(0.6f, 0.04f, 0.6f), M(new Color(0.45f, 0.30f, 0.18f)), new Vector3(0f, 0f, 90f));
            return go;
        }
    }
}
