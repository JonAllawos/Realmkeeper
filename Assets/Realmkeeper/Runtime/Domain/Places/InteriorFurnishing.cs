using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Randomness;

namespace Realmkeeper.Domain.Places
{
    /// <summary>A room to furnish, as the planner sees it (cells of 1.5 m).</summary>
    public readonly struct FurnishRoom
    {
        public FurnishRoom(string id, int floor, CellRect rect, RoomKind kind)
        {
            Id = id;
            Floor = floor;
            Rect = rect;
            Kind = kind;
        }

        public string Id { get; }
        public int Floor { get; }
        public CellRect Rect { get; }
        public RoomKind Kind { get; }
    }

    /// <summary>One piece of furniture: a kit model, the cell it stands in, which way it faces, and how big it is.</summary>
    public sealed class FurnishingPiece
    {
        public string Room { get; set; }
        public int Floor { get; set; }
        public int X { get; set; }
        public int Z { get; set; }

        /// <summary>Degrees about the vertical; a piece against a wall faces into the room.</summary>
        public float Yaw { get; set; }

        /// <summary>The kit model's id (the art catalog's scenery).</summary>
        public string Prop { get; set; }

        /// <summary>The model's largest side, metres.</summary>
        public float Size { get; set; }

        /// <summary>What it is, in a word ("bed", "table" …).</summary>
        public string Kind { get; set; }

        /// <summary>Blocks walking (a collider is made for it).</summary>
        public bool Solid { get; set; }

        /// <summary>It stands against a wall.</summary>
        public bool AgainstWall { get; set; }
    }

    /// <summary>
    /// Phase 21 (ADR-0108): what a building's rooms hold — beds in bedrooms, a table and chairs in the hall, barrels and
    /// crates in the cellar, benches in the workshop — planned from each room's kind and size. Deterministic (the same
    /// seed gives the same rooms), validated (every piece inside its room, never on a doorway, stair, feature or the cell
    /// in front of a door, no two in one cell, and at least half of each room's floor kept clear). Presentation only: the
    /// pieces are scenery, not world state, so nothing is saved and nothing pretends to be usable — beds, chests and
    /// hearths that matter to the rules remain the room's own features.
    /// </summary>
    public static class InteriorFurnishing
    {
        public const float Cell = 1.5f;

        private struct Item
        {
            public string Prop;
            public float Size;
            public string Kind;
            public bool Wall;
            public bool Solid;
            public int Count;

            public Item(string prop, float size, string kind, bool wall, bool solid = true, int count = 1)
            {
                Prop = prop;
                Size = size;
                Kind = kind;
                Wall = wall;
                Solid = solid;
                Count = count;
            }
        }

        private static Item[] For(RoomKind kind, int cells)
        {
            var big = cells >= 9;
            switch (kind)
            {
                case RoomKind.Bedroom:
                    return new[] { new Item("Bed_Twin1", 2.0f, "bed", true), new Item("Nightstand_Shelf", 0.6f, "nightstand", true), new Item("Chest_Wood", 0.85f, "chest", true),
                                   new Item("Chair_1", 0.95f, "chair", true, false), new Item("Lantern_Wall", 0.4f, "lantern", true, false) };
                case RoomKind.Quarters:
                case RoomKind.Barracks:
                    return new[] { new Item("Bed_Twin2", 2.0f, "bed", true, true, big ? 3 : 2), new Item("Chest_Wood", 0.85f, "chest", true), new Item("Stool", 0.5f, "stool", false, false),
                                   new Item("Lantern_Wall", 0.4f, "lantern", true, false) };
                case RoomKind.Hall:
                case RoomKind.TapRoom:
                case RoomKind.Chamber:
                case RoomKind.Entrance:
                    return new[] { new Item("Table_Large", 1.9f, "table", false), new Item("Chair_1", 0.95f, "chair", false, false, 2), new Item("Cabinet", 1.6f, "cabinet", true),
                                   new Item("Barrel", 0.9f, "barrel", true), new Item("Shelf_Simple", 1.2f, "shelf", true, false), new Item("Lantern_Wall", 0.4f, "lantern", true, false) };
                case RoomKind.Kitchen:
                    return new[] { new Item("Table_Large", 1.7f, "table", true), new Item("Barrel", 0.9f, "barrel", true), new Item("Shelf_Simple", 1.2f, "shelf", true, false),
                                   new Item("Pot_1", 0.4f, "pot", true, false), new Item("Bucket_Wooden_1", 0.4f, "bucket", false, false), new Item("Crate_Wooden", 0.6f, "crate", true) };
                case RoomKind.Workshop:
                    return new[] { new Item("Workbench", 1.7f, "workbench", true), new Item("Workbench_Drawers", 1.6f, "workbench", true), new Item("Crate_Wooden", 0.6f, "crate", true),
                                   new Item("Stool", 0.5f, "stool", false, false), new Item("Barrel", 0.9f, "barrel", true) };
                case RoomKind.Storage:
                case RoomKind.Cellar:
                case RoomKind.Vault:
                    return new[] { new Item("crates_stacked", 1.3f, "crates", true), new Item("Barrel", 0.9f, "barrel", true, true, 2), new Item("barrel_small_stack", 1.0f, "barrels", true),
                                   new Item("Crate_Wooden", 0.6f, "crate", true), new Item("Bag", 0.5f, "sack", true, false) };
                case RoomKind.Office:
                    return new[] { new Item("Table_Large", 1.6f, "desk", false), new Item("Chair_1", 0.95f, "chair", false, false), new Item("Bookcase_2", 1.9f, "bookcase", true),
                                   new Item("BookStand", 1.1f, "lectern", true, false), new Item("CandleStick_Stand", 1.3f, "candles", true, false) };
                case RoomKind.Shrine:
                    return new[] { new Item("Bench", 1.6f, "bench", false, true, big ? 3 : 2), new Item("CandleStick_Stand", 1.3f, "candles", true, false, 2) };
                case RoomKind.Armoury:
                case RoomKind.Gatehouse:
                    return new[] { new Item("WeaponStand", 1.5f, "weapon stand", true), new Item("Dummy", 1.7f, "dummy", false), new Item("Chest_Wood", 0.85f, "chest", true),
                                   new Item("Bench", 1.5f, "bench", true) };
                case RoomKind.Cell:
                    return new[] { new Item("bed_floor", 1.8f, "bed", true), new Item("Bucket_Wooden_1", 0.4f, "bucket", true, false) };
                default:
                    return Array.Empty<Item>();
            }
        }

        /// <summary>
        /// Plans the rooms' furniture. <paramref name="blocked"/>: cells that must stay clear (doorways and the cells either
        /// side of them, stairs, the room's own features), as (floor, x, z).
        /// </summary>
        public static List<FurnishingPiece> Plan(IReadOnlyList<FurnishRoom> rooms, ISet<(int, int, int)> blocked, ulong seed)
        {
            var plan = new List<FurnishingPiece>();
            foreach (var room in rooms)
            {
                var r = room.Rect;
                var cells = r.W * r.D;
                if (cells < 2) continue;
                var items = For(room.Kind, cells);
                if (items.Length == 0) continue;
                var taken = new HashSet<(int, int)>();
                var budget = cells / 2; // at least half the floor stays clear
                var rng = SeedMixer.Combine(seed, "furnish:" + room.Id);
                var n = 0;
                foreach (var item in items)
                for (var copy = 0; copy < item.Count && budget > 0; copy++)
                {
                    var spots = new List<(int x, int z, float yaw)>();
                    for (var x = r.X; x < r.X + r.W; x++)
                    for (var z = r.Z; z < r.Z + r.D; z++)
                    {
                        if (taken.Contains((x, z)) || blocked.Contains((room.Floor, x, z))) continue;
                        var edge = x == r.X || x == r.X + r.W - 1 || z == r.Z || z == r.Z + r.D - 1;
                        if (item.Wall != edge) continue;
                        if (!item.Wall && (r.W < 3 || r.D < 3)) continue; // a free-standing piece needs room around it
                        // Facing into the room from the wall it stands against (+Z is 0°).
                        var yaw = z == r.Z ? 0f : z == r.Z + r.D - 1 ? 180f : x == r.X ? 90f : x == r.X + r.W - 1 ? 270f : 0f;
                        spots.Add((x, z, yaw));
                    }

                    if (spots.Count == 0) continue;
                    var pick = spots[(int)(SeedMixer.Mix(SeedMixer.Combine(rng, unchecked((ulong)n++))) % (ulong)spots.Count)];
                    taken.Add((pick.x, pick.z));
                    budget--;
                    plan.Add(new FurnishingPiece
                    {
                        Room = room.Id, Floor = room.Floor, X = pick.x, Z = pick.z, Yaw = pick.yaw, Prop = item.Prop, Size = item.Size, Kind = item.Kind,
                        Solid = item.Solid, AgainstWall = item.Wall,
                    });
                }
            }

            return plan;
        }

        /// <summary>
        /// The cells furniture must leave clear: every doorway or stair cell and the cells around it (the way in and out),
        /// and every feature's cell (a bed, hearth or chest the rules know about).
        /// </summary>
        public static HashSet<(int, int, int)> Blocked(IEnumerable<(int Floor, CellRect Cells)> doorways, IEnumerable<(int Floor, int X, int Z)> features)
        {
            var blocked = new HashSet<(int, int, int)>();
            foreach (var (floor, rect) in doorways)
                for (var x = rect.X - 1; x <= rect.X + rect.W; x++)
                for (var z = rect.Z - 1; z <= rect.Z + rect.D; z++)
                    blocked.Add((floor, x, z));
            foreach (var f in features) blocked.Add((f.Floor, f.X, f.Z));
            return blocked;
        }

        /// <summary>The rooms and clear cells of a generated layout (for the planner and its tests).</summary>
        public static (List<FurnishRoom> Rooms, HashSet<(int, int, int)> Blocked) Inputs(PlaceLayout layout)
        {
            var rooms = new List<FurnishRoom>();
            var floorOf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in layout.Rooms)
            {
                floorOf[r.Id] = r.Floor;
                if (!r.Secret) rooms.Add(new FurnishRoom(r.Id, r.Floor, r.Rect, r.Kind));
            }

            var doors = new List<(int, CellRect)>();
            foreach (var c in layout.Connections)
            {
                if (c.A != null && floorOf.TryGetValue(c.A, out var fa)) doors.Add((fa, c.Rect));
                if (c.B != null && floorOf.TryGetValue(c.B, out var fb))
                {
                    doors.Add((fb, c.Rect));
                    if (c.IsVertical) doors.Add((fb, new CellRect(c.ToX, c.ToZ, 1, 1)));
                }
            }

            var features = new List<(int, int, int)>();
            foreach (var f in layout.Features) features.Add((f.Room != null && floorOf.TryGetValue(f.Room, out var ff) ? ff : 0, f.X, f.Z));
            return (rooms, Blocked(doors, features));
        }

        /// <summary>The problems with a plan (empty when it is sound): every check <see cref="Plan"/> promises.</summary>
        public static List<string> Validate(IReadOnlyList<FurnishRoom> rooms, ISet<(int, int, int)> blocked, IReadOnlyList<FurnishingPiece> plan)
        {
            var errors = new List<string>();
            var seen = new HashSet<(int, int, int)>();
            var perRoom = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var p in plan)
            {
                FurnishRoom? room = null;
                foreach (var r in rooms) if (r.Id == p.Room) room = r;
                if (room == null) { errors.Add($"{p.Kind} in unknown room {p.Room}."); continue; }
                var rect = room.Value.Rect;
                if (p.X < rect.X || p.Z < rect.Z || p.X >= rect.X + rect.W || p.Z >= rect.Z + rect.D) errors.Add($"{p.Kind} outside {p.Room}.");
                if (blocked.Contains((p.Floor, p.X, p.Z))) errors.Add($"{p.Kind} in {p.Room} blocks a doorway, stair or feature.");
                if (!seen.Add((p.Floor, p.X, p.Z))) errors.Add($"Two pieces share a cell in {p.Room}.");
                perRoom[p.Room] = (perRoom.TryGetValue(p.Room, out var c) ? c : 0) + 1;
            }

            foreach (var r in rooms)
                if (perRoom.TryGetValue(r.Id, out var count) && count > r.Rect.W * r.Rect.D / 2) errors.Add($"{r.Id} is more than half filled.");
            return errors;
        }
    }
}
