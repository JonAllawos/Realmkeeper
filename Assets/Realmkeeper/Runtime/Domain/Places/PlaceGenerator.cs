using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.Randomness;

namespace Realmkeeper.Domain.Places
{
    /// <summary>A small deterministic generator for layouts (SplitMix over <see cref="SeedMixer"/>): the same seed, the same layout.</summary>
    public sealed class PlaceRng
    {
        private ulong _state;

        public PlaceRng(ulong seed) { _state = seed; }

        public ulong NextULong()
        {
            _state = unchecked(_state + 0x9E3779B97F4A7C15UL);
            return SeedMixer.Mix(_state);
        }

        /// <summary>0 ≤ n &lt; max.</summary>
        public int Next(int max) => max <= 1 ? 0 : (int)(NextULong() % (ulong)max);

        public int Range(int min, int max) => max <= min ? min : min + Next(max - min + 1);

        public bool Percent(int chance) => chance > 0 && Next(100) < chance;

        public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];
    }

    /// <summary>
    /// Phase 13 (ADR-0063): builds a place's layout from its archetype and a seed. Rooms sit one to a slot on a coarse
    /// grid, so they can never overlap; corridors run straight between neighbouring slots through a band every room
    /// in a slot is made to cover; a spanning tree joins every room before extra loops, secret rooms, stairs and
    /// contents are added. The result is checked by <see cref="PlaceValidator"/>; generation never returns a layout
    /// that fails it.
    /// </summary>
    public static class PlaceGenerator
    {
        private const int GridSize = 4;

        public static PlaceLayout Generate(ArchetypeDefinition a, ulong seed, PlaceDefinition story = null)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            // A seed whose layout cannot be completed (a crowded grid with no room left for a stair or a secret) moves to
            // the next attempt, deterministically: the same seed always ends at the same attempt.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var layout = TryGenerate(a, SeedMixer.Combine(seed, (ulong)attempt), story);
                if (layout != null && PlaceValidator.Validate(layout).Count == 0) return layout;
            }

            throw new InvalidOperationException($"No valid {a.Id} layout for seed {seed}.");
        }

        private sealed class Slot
        {
            public int Floor;
            public int Sx;
            public int Sz;
            public PlaceRoom Room;
        }

        private static PlaceLayout TryGenerate(ArchetypeDefinition a, ulong seed, PlaceDefinition story)
        {
            var rng = new PlaceRng(seed);
            var layout = new PlaceLayout();
            var floors = rng.Range(a.MinFloors, a.MaxFloors);
            var slots = new List<Slot>();
            var edges = new List<(Slot A, Slot B, bool Tree)>();
            var cw = a.CorridorWidth;
            var roomIndex = 0;

            for (var f = 0; f < floors; f++)
            {
                layout.Floors.Add(new PlaceFloor
                {
                    Index = f, Elevation = (a.Descends ? -1f : 1f) * f * 6f,
                    Name = f == 0 ? (a.Category == PlaceCategory.Interior ? "Ground floor" : "Entrance level") : a.Descends ? (f == 1 ? "Lower level" : "Deep level " + f) : (f == 1 ? "Upper floor" : "Floor " + (f + 1)),
                });

                var count = rng.Range(a.MinRooms, a.MaxRooms);
                if (f > 0) count = Math.Max(2, count - 1);
                var occupied = new Dictionary<(int, int), Slot>();
                Slot start;
                if (f == 0) start = new Slot { Floor = 0, Sx = GridSize / 2 - rng.Next(2), Sz = 0 };
                else
                {
                    // The floor grows from the slot above (or below) where its stair stands.
                    var candidates = slots.FindAll(s => s.Floor == f - 1 && s.Room.Kind != a.EntranceRoom && !s.Room.Secret);
                    if (candidates.Count == 0) candidates = slots.FindAll(s => s.Floor == f - 1 && !s.Room.Secret);
                    var below = candidates[rng.Next(candidates.Count)];
                    start = new Slot { Floor = f, Sx = below.Sx, Sz = below.Sz };
                }

                start.Room = MakeRoom(a, rng, f, start.Sx, start.Sz, ref roomIndex);
                occupied[(start.Sx, start.Sz)] = start;
                slots.Add(start);
                while (occupied.Count < count)
                {
                    var frontier = new List<(Slot From, int X, int Z)>();
                    foreach (var s in occupied.Values)
                        foreach (var (dx, dz) in Steps)
                        {
                            var nx = s.Sx + dx;
                            var nz = s.Sz + dz;
                            if (nx < 0 || nz < 0 || nx >= GridSize || nz >= GridSize || occupied.ContainsKey((nx, nz))) continue;
                            frontier.Add((s, nx, nz));
                        }

                    if (frontier.Count == 0) break;
                    frontier.Sort((p, q) => p.From.Room.Id != q.From.Room.Id ? string.CompareOrdinal(p.From.Room.Id, q.From.Room.Id) : p.X != q.X ? p.X - q.X : p.Z - q.Z);
                    var (from, x, z) = frontier[rng.Next(frontier.Count)];
                    var slot = new Slot { Floor = f, Sx = x, Sz = z, Room = MakeRoom(a, rng, f, x, z, ref roomIndex) };
                    occupied[(x, z)] = slot;
                    slots.Add(slot);
                    edges.Add((from, slot, true));
                }

                // Loops: neighbouring rooms not yet joined.
                foreach (var s in new List<Slot>(occupied.Values))
                    foreach (var (dx, dz) in new[] { (1, 0), (0, 1) })
                        if (occupied.TryGetValue((s.Sx + dx, s.Sz + dz), out var n) && !edges.Exists(e => (e.A == s && e.B == n) || (e.A == n && e.B == s)) && rng.Percent(a.LoopChance))
                            edges.Add((s, n, false));
            }

            // Phase 14 (ADR-0071): a story that wants a bridge needs a loop to put it on, so a fallen bridge never cuts the only way.
            if (story != null && story.Bridges > 0 && !edges.Exists(e => !e.Tree && e.A.Floor == 0))
                foreach (var s in slots)
                {
                    if (s.Floor != 0) continue;
                    var n = slots.Find(o => o.Floor == 0 && ((o.Sx == s.Sx + 1 && o.Sz == s.Sz) || (o.Sx == s.Sx && o.Sz == s.Sz + 1)));
                    if (n == null || edges.Exists(e => (e.A == s && e.B == n) || (e.A == n && e.B == s))) continue;
                    edges.Add((s, n, false));
                    break;
                }

            // Passages on each floor.
            var connIndex = 0;
            var loops = new List<PlaceConnection>();
            foreach (var (sa, sb, tree) in edges)
            {
                var corridor = Corridor(a, rng, sa, sb, cw, ref connIndex);
                layout.Connections.Add(corridor);
                if (!tree) loops.Add(corridor);
            }

            // Bridges over water or a chasm, on loop passages only.
            if (story != null)
                for (var i = 0; i < story.Bridges && i < loops.Count; i++)
                {
                    var b = loops[i];
                    b.Kind = ConnectionKind.Bridge;
                    b.Door = DoorState.Open;
                    b.LockDc = 0;
                    b.KeyItem = null;
                    b.Bridge = i == 0 && story.DamagedBridge ? BridgeState.Damaged : BridgeState.Intact;
                }

            // Stairs or ladders: each upper floor's first slot stands over a room of the floor before.
            for (var f = 1; f < floors; f++)
            {
                var top = slots.Find(s => s.Floor == f);
                var bottom = slots.Find(s => s.Floor == f - 1 && s.Sx == top.Sx && s.Sz == top.Sz);
                var kind = a.Category == PlaceCategory.Mine || a.Category == PlaceCategory.Catacomb ? ConnectionKind.Ladder : ConnectionKind.Stairs;
                var foot = bottom.Room.Rect;
                var land = top.Room.Rect;
                layout.Connections.Add(new PlaceConnection
                {
                    Id = "c" + (connIndex++), Kind = kind, A = bottom.Room.Id, B = top.Room.Id, Rect = new CellRect(foot.MaxX - 2, foot.MaxZ - 2, 1, 1),
                    ToX = land.MaxX - 2, ToZ = land.MaxZ - 2,
                });
                if (kind == ConnectionKind.Ladder && bottom.Room.Kind != a.EntranceRoom) bottom.Room.Kind = RoomKind.Shaft;
            }

            // Room kinds: entrance, deepest, and the archetype's mix.
            foreach (var s in slots) layout.Rooms.Add(s.Room);
            var entrance = slots[0].Room;
            entrance.Kind = a.EntranceRoom;
            entrance.Lit = entrance.Lit || a.Category != PlaceCategory.Cave && a.LitChance > 0;
            var depth = Depths(layout, entrance.Id, includeSecret: false);
            PlaceRoom deepest = null;
            foreach (var r in layout.Rooms)
                if (r != entrance && depth.TryGetValue(r.Id, out var d) && (deepest == null || d > depth[deepest.Id] || (d == depth[deepest.Id] && r.Floor > deepest.Floor)))
                    deepest = r;
            if (deepest != null) deepest.Kind = a.DeepRoom;

            // Secret rooms: a free neighbouring slot reached by a hidden passage.
            for (var i = 0; i < a.SecretRooms; i++)
            {
                var hosts = new List<(Slot S, int X, int Z)>();
                foreach (var s in slots)
                {
                    if (s.Room == entrance || s.Room.Secret) continue;
                    foreach (var (dx, dz) in Steps)
                    {
                        var nx = s.Sx + dx;
                        var nz = s.Sz + dz;
                        if (nx < 0 || nz < 0 || nx >= GridSize || nz >= GridSize) continue;
                        if (slots.Exists(o => o.Floor == s.Floor && o.Sx == nx && o.Sz == nz)) continue;
                        hosts.Add((s, nx, nz));
                    }
                }

                if (hosts.Count == 0) break;
                var (host, hx, hz) = hosts[rng.Next(hosts.Count)];
                var secret = new Slot { Floor = host.Floor, Sx = hx, Sz = hz, Room = MakeRoom(a, rng, host.Floor, hx, hz, ref roomIndex) };
                secret.Room.Secret = true;
                secret.Room.Kind = a.Category == PlaceCategory.Crypt || a.Category == PlaceCategory.Catacomb ? RoomKind.Tomb : RoomKind.Vault;
                secret.Room.Name = "Hidden " + Label(secret.Room.Kind).ToLowerInvariant();
                slots.Add(secret);
                layout.Rooms.Add(secret.Room);
                var c = Corridor(a, rng, host, secret, cw, ref connIndex);
                c.Kind = ConnectionKind.Secret;
                c.Door = DoorState.Closed;
                c.LockDc = 0;
                c.FindDc = 12 + rng.Next(4);
                layout.Connections.Add(c);
            }

            // The way in.
            var e = entrance.Rect;
            layout.Connections.Insert(0, new PlaceConnection { Id = "entrance", Kind = ConnectionKind.Entrance, A = entrance.Id, Rect = new CellRect(e.CenterX - 1, e.Z, 2, 1) });

            // Names, light and hazards.
            var used = new Dictionary<RoomKind, int>();
            foreach (var r in layout.Rooms)
            {
                if (!r.Secret)
                {
                    used.TryGetValue(r.Kind, out var n);
                    used[r.Kind] = n + 1;
                    r.Name = Label(r.Kind) + (n > 0 ? " " + (n + 1) : string.Empty);
                }

                r.Ceiling = a.Ceiling;
                if (a.Category == PlaceCategory.AbandonedSettlement && r.Kind == RoomKind.Yard && !r.Tags.Contains("open_sky")) r.Tags.Add("open_sky"); // Phase 14: the hamlet's yards lie under the sky
                if (r == entrance || r.Secret) continue;
                if (a.HazardChance > 0 && a.Hazards.Length > 0 && rng.Percent(a.HazardChance))
                {
                    var h = a.Hazards[rng.Next(a.Hazards.Length)];
                    if (h == "darkness" && r.Lit) continue;
                    r.Hazard = h;
                }
            }

            // Phase 14: a story's burning room.
            if (story?.FireRoom != null)
            {
                var fire = layout.Rooms.Find(r => r.Kind == story.FireRoom.Value && r != entrance && !r.Secret) ?? layout.Rooms.Find(r => r != entrance && !r.Secret);
                if (fire != null) fire.Hazard = "fire";
            }

            var cells = new Occupancy(layout);
            Populate(a, rng, layout, cells, depth, story);
            if (story?.KeyItem != null) PlaceKey(layout, entrance, story.KeyItem, rng);
            if (story != null && story.Bridges > 0 && !layout.Connections.Exists(c => c.Kind == ConnectionKind.Bridge)) SideBridge(layout, entrance, story);
            // Phase 18 (ADR-0089): mechanisms wired to what they work. A layout where a puzzle cannot be made solvable (its
            // workings would lie behind the door they open) is not used: the next attempt is tried.
            if (story?.Finish != null && !story.Finish(layout)) return null;
            return layout;
        }

        /// <summary>
        /// Phase 14 (ADR-0071): a place with no loop for its bridge puts it on the way into a side room — a dead end that holds
        /// nothing the place's story needs — so a fallen bridge cuts off only that room.
        /// </summary>
        private static void SideBridge(PlaceLayout layout, PlaceRoom entrance, PlaceDefinition story)
        {
            foreach (var r in layout.Rooms)
            {
                if (r == entrance || r.Secret || layout.Features.Exists(f => f.Room == r.Id && (f.Required || f.Id.StartsWith("story:", StringComparison.Ordinal) || f.Get("key") != null))) continue;
                var ways = layout.Connections.FindAll(c => c.Joins(r.Id) && c.Kind != ConnectionKind.Secret);
                if (ways.Count != 1) continue;
                var c0 = ways[0];
                if (c0.IsVertical || c0.Kind == ConnectionKind.Entrance || c0.KeyItem != null) continue;
                c0.Kind = ConnectionKind.Bridge;
                c0.Door = DoorState.Open;
                c0.LockDc = 0;
                c0.Bridge = story.DamagedBridge ? BridgeState.Damaged : BridgeState.Intact;
                return;
            }
        }

        /// <summary>
        /// Phase 14 (ADR-0071): the deepest locked door takes the story's key (the deepest door is locked for it if none
        /// is); the key goes into a container that can be reached from the way in without passing that door.
        /// </summary>
        private static void PlaceKey(PlaceLayout layout, PlaceRoom entrance, string keyItem, PlaceRng rng)
        {
            var depth = Depths(layout, entrance.Id, includeSecret: false);
            int Deep(PlaceConnection c) => Math.Max(depth.TryGetValue(c.A, out var x) ? x : 0, c.B != null && depth.TryGetValue(c.B, out var y) ? y : 0);
            PlaceConnection door = null;
            foreach (var c in layout.Connections)
                if (c.Kind == ConnectionKind.Door && c.Door == DoorState.Locked && (door == null || Deep(c) > Deep(door))) door = c;
            if (door == null)
            {
                foreach (var c in layout.Connections)
                    if (c.Kind == ConnectionKind.Door && (door == null || Deep(c) > Deep(door))) door = c;
                if (door == null)
                {
                    // No door anywhere: the deepest open passage gets one, so the key has a lock to open.
                    foreach (var c in layout.Connections)
                        if (c.Kind == ConnectionKind.Opening && (door == null || Deep(c) > Deep(door))) door = c;
                    if (door == null) return;
                    door.Kind = ConnectionKind.Door;
                }

                door.Door = DoorState.Locked;
                door.LockDc = Math.Max(door.LockDc, 15);
            }

            door.KeyItem = keyItem;
            var near = new HashSet<string> { entrance.Id };
            var queue = new Queue<string>();
            queue.Enqueue(entrance.Id);
            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                foreach (var c in layout.Connections)
                {
                    if (c == door || c.Kind == ConnectionKind.Secret || c.Kind == ConnectionKind.Entrance || !c.Joins(room)) continue;
                    var next = c.Other(room);
                    if (next != null && near.Add(next)) queue.Enqueue(next);
                }
            }

            var boxes = layout.Features.FindAll(f => f.Kind == FeatureKind.Container && near.Contains(f.Room) && f.Get("key") == null && f.Get("owned") == null && !f.Id.StartsWith("story:", StringComparison.Ordinal));
            if (boxes.Count == 0) return; // the lock can still be picked or forced
            boxes[rng.Next(boxes.Count)].Data["key"] = keyItem;
        }

        private static readonly (int, int)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        private static PlaceRoom MakeRoom(ArchetypeDefinition a, PlaceRng rng, int floor, int sx, int sz, ref int index)
        {
            var size = a.Slot;
            var cw = a.CorridorWidth;
            var cx = sx * size + size / 2 - cw / 2;
            var cz = sz * size + size / 2 - cw / 2;
            int Span(int lo, int c, out int start)
            {
                var minW = Math.Max(a.MinRoom, cw + 2);
                var maxW = Math.Min(a.MaxRoom, size - 2);
                var w = rng.Range(minW, maxW);
                var from = Math.Max(lo + 1, c + cw + 1 - w);
                var to = Math.Min(c - 1, lo + size - 1 - w);
                if (to < from)
                {
                    w = size - 2;
                    from = to = lo + 1;
                }

                start = rng.Range(from, to);
                return w;
            }

            var width = Span(sx * size, cx, out var x);
            var depth = Span(sz * size, cz, out var z);
            var kind = a.RoomPool[rng.Next(a.RoomPool.Length)];
            return new PlaceRoom { Id = "f" + floor + "r" + (index++), Floor = floor, Rect = new CellRect(x, z, width, depth), Kind = kind, Lit = rng.Percent(a.LitChance) };
        }

        private static PlaceConnection Corridor(ArchetypeDefinition a, PlaceRng rng, Slot sa, Slot sb, int cw, ref int index)
        {
            var (s1, s2) = sa.Sx < sb.Sx || sa.Sz < sb.Sz ? (sa, sb) : (sb, sa);
            var r1 = s1.Room.Rect;
            var r2 = s2.Room.Rect;
            CellRect rect;
            if (s1.Sz == s2.Sz)
            {
                var cz = s1.Sz * a.Slot + a.Slot / 2 - cw / 2;
                rect = new CellRect(r1.MaxX, cz, r2.X - r1.MaxX, cw);
            }
            else
            {
                var cx = s1.Sx * a.Slot + a.Slot / 2 - cw / 2;
                rect = new CellRect(cx, r1.MaxZ, cw, r2.Z - r1.MaxZ);
            }

            var c = new PlaceConnection { Id = "c" + (index++), Kind = ConnectionKind.Opening, A = sa.Room.Id, B = sb.Room.Id, Rect = rect };
            if (rng.Percent(a.DoorChance))
            {
                c.Kind = ConnectionKind.Door;
                c.Door = DoorState.Closed;
                if (rng.Percent(a.LockedChance))
                {
                    c.Door = DoorState.Locked;
                    c.LockDc = 12 + rng.Next(4); // picked with thieves' tools or forced: never the only obstacle to a required room
                }
            }

            return c;
        }

        /// <summary>Steps from a room to every other through passages (secret ones only when asked); vertical links count as one step.</summary>
        public static Dictionary<string, int> Depths(PlaceLayout layout, string from, bool includeSecret)
        {
            var depth = new Dictionary<string, int>(StringComparer.Ordinal) { [from] = 0 };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                foreach (var c in layout.Connections)
                {
                    if (c.Kind == ConnectionKind.Entrance || !c.Joins(r)) continue;
                    if (c.Kind == ConnectionKind.Secret && !includeSecret) continue;
                    var o = c.Other(r);
                    if (o == null || depth.ContainsKey(o)) continue;
                    depth[o] = depth[r] + 1;
                    queue.Enqueue(o);
                }
            }

            return depth;
        }

        public static string Label(RoomKind k)
        {
            switch (k)
            {
                case RoomKind.Entrance: return "Entrance";
                case RoomKind.TapRoom: return "Tap room";
                case RoomKind.Den: return "Den";
                case RoomKind.Collapse: return "Collapsed hall";
                case RoomKind.Shaft: return "Shaft";
                default: return k.ToString();
            }
        }

        /// <summary>Which cells of each room are taken (stairs, corridor mouths, people, features), so nothing is placed on top of anything else.</summary>
        public sealed class Occupancy
        {
            private readonly HashSet<(int, int, int)> _taken = new HashSet<(int, int, int)>();
            private readonly PlaceLayout _layout;

            public Occupancy(PlaceLayout layout)
            {
                _layout = layout;
                foreach (var c in layout.Connections)
                {
                    var a = layout.Room(c.A);
                    if (a == null) continue;
                    if (c.IsVertical)
                    {
                        Take(a.Floor, c.Rect.X, c.Rect.Z);
                        var b = layout.Room(c.B);
                        if (b != null) Take(b.Floor, c.ToX, c.ToZ);
                        continue;
                    }

                    // The cells just inside each room at a passage's mouth stay clear.
                    foreach (var id in new[] { c.A, c.B })
                    {
                        var r = layout.Room(id);
                        if (r == null) continue;
                        for (var x = c.Rect.X - 1; x <= c.Rect.MaxX; x++)
                            for (var z = c.Rect.Z - 1; z <= c.Rect.MaxZ; z++)
                                if (r.Rect.Contains(x, z)) Take(r.Floor, x, z);
                    }
                }
            }

            public bool Free(int floor, int x, int z) => !_taken.Contains((floor, x, z));

            public void Take(int floor, int x, int z) => _taken.Add((floor, x, z));

            /// <summary>A free cell inside a room, at least one cell from its walls when the room allows it, for something of size×size cells.</summary>
            public bool Find(PlaceRoom r, PlaceRng rng, int size, out int x, out int z)
            {
                var margin = r.Rect.W >= size + 2 && r.Rect.D >= size + 2 ? 1 : 0;
                var options = new List<(int, int)>();
                for (var cx = r.Rect.X + margin; cx + size <= r.Rect.MaxX - margin; cx++)
                    for (var cz = r.Rect.Z + margin; cz + size <= r.Rect.MaxZ - margin; cz++)
                    {
                        var ok = true;
                        for (var i = 0; i < size && ok; i++)
                            for (var j = 0; j < size && ok; j++)
                                ok = Free(r.Floor, cx + i, cz + j);
                        if (ok) options.Add((cx, cz));
                    }

                if (options.Count == 0)
                {
                    x = z = 0;
                    return false;
                }

                (x, z) = options[rng.Next(options.Count)];
                for (var i = 0; i < size; i++)
                    for (var j = 0; j < size; j++)
                        Take(r.Floor, x + i, z + j);
                return true;
            }
        }

        private static void Populate(ArchetypeDefinition a, PlaceRng rng, PlaceLayout layout, Occupancy cells, Dictionary<string, int> depth, PlaceDefinition story)
        {
            int D(PlaceRoom r) => depth.TryGetValue(r.Id, out var d) ? d : 0;
            var ordinary = layout.Rooms.FindAll(r => !r.Secret && r.Kind != a.EntranceRoom);
            if (ordinary.Count == 0) ordinary = layout.Rooms.FindAll(r => !r.Secret);
            ordinary.Sort((p, q) => D(q) != D(p) ? D(q) - D(p) : string.CompareOrdinal(p.Id, q.Id)); // deepest first
            var featureIndex = 0;
            string NextId(string prefix) => prefix + (featureIndex++);

            // Story features (a definition's own), placed first so they get their rooms.
            if (story != null)
                foreach (var (feature, kind, required) in story.Story)
                {
                    var def = FeatureCatalog.Find(feature);
                    if (def == null) continue;
                    var room = ordinary.Find(r => r.Kind == kind) ?? ordinary[0];
                    if (!cells.Find(room, rng, 1, out var x, out var z)) continue;
                    var sf = new PlaceFeature { Id = "story:" + feature, Room = room.Id, Kind = def.Kind, DefinitionId = feature, X = x, Z = z, Required = required };
                    if (story.StoryLoot.TryGetValue(feature, out var table)) sf.Data["loot"] = table;
                    if (story.StoryItem.TryGetValue(feature, out var relic)) sf.Data["key"] = relic; // one named item, put in by the same path as a key
                    layout.Features.Add(sf);
                }

            // Inhabitants.
            var taken = new HashSet<string>(StringComparer.Ordinal);
            var g = 0;

            // Phase 14: groups that sleep beside a story feature and wake when it is disturbed.
            if (story != null)
                foreach (var (feature, name, creatures, sleepers) in story.Waking)
                {
                    var at = layout.Features.Find(x => x.Id == "story:" + feature);
                    var room = at == null ? null : layout.Room(at.Room);
                    if (room == null) continue;
                    taken.Add(room.Id);
                    var waking = new PlaceGroup { Id = "g" + (g++), Room = room.Id, Name = name, Behaviour = GroupBehaviour.Lair, WakesOn = at.Id };
                    for (var i = 0; i < sleepers; i++)
                    {
                        var creature = creatures[rng.Next(creatures.Length)];
                        if (!cells.Find(room, rng, 1, out var x, out var z)) {
                            // A crowded vault: any free cell of the room will do (they lie where they can).
                            x = -1; z = -1;
                            for (var cx = room.Rect.X; cx < room.Rect.MaxX && x < 0; cx++)
                                for (var cz = room.Rect.Z; cz < room.Rect.MaxZ && x < 0; cz++)
                                    if (cells.Free(room.Floor, cx, cz) && !layout.Features.Exists(ft => ft.Room == room.Id && ft.X == cx && ft.Z == cz)) { x = cx; z = cz; }
                            if (x < 0) continue;
                            cells.Take(room.Floor, x, z);
                        }
                        waking.Members.Add(new GroupMember { CreatureId = creature, X = x, Z = z });
                    }

                    if (waking.Members.Count > 0) layout.Groups.Add(waking);
                }
            foreach (var t in a.Inhabitants)
            {
                var room = ordinary.Find(r => !taken.Contains(r.Id) && Array.IndexOf(t.Rooms, r.Kind) >= 0) ?? ordinary.Find(r => !taken.Contains(r.Id));
                if (room == null) continue;
                taken.Add(room.Id);
                var group = new PlaceGroup { Id = "g" + (g++), Room = room.Id, Name = t.Name, Behaviour = t.Behaviour };
                var n = rng.Range(t.Min, t.Max);
                for (var i = 0; i < n; i++)
                {
                    var creature = t.Creatures[rng.Next(t.Creatures.Length)];
                    var size = CreatureCatalog.Find(creature)?.Size >= CreatureSize.Large ? 2 : 1;
                    if (!cells.Find(room, rng, size, out var x, out var z)) continue;
                    group.Members.Add(new GroupMember { CreatureId = creature, X = x, Z = z });
                }

                if (story != null && story.Parley && !layout.Groups.Exists(x => x.Behaviour == GroupBehaviour.Parley)) group.Behaviour = GroupBehaviour.Parley;
                if (group.Members.Count > 0) layout.Groups.Add(group);
            }

            // Containers: storage-like rooms first; every secret room holds one.
            var storageKinds = new[] { RoomKind.Storage, RoomKind.Vault, RoomKind.Tomb, RoomKind.Quarters, RoomKind.Den, RoomKind.Office, RoomKind.Armoury };
            var containerRooms = new List<PlaceRoom>(layout.Rooms.FindAll(r => r.Secret));
            var count = rng.Range(a.MinContainers, a.MaxContainers);
            foreach (var r in ordinary)
                if (containerRooms.Count < count + a.SecretRooms && Array.IndexOf(storageKinds, r.Kind) >= 0) containerRooms.Add(r);
            foreach (var r in ordinary)
                if (containerRooms.Count < count + a.SecretRooms && !containerRooms.Contains(r)) containerRooms.Add(r);
            foreach (var r in containerRooms)
            {
                if (!cells.Find(r, rng, 1, out var x, out var z)) continue;
                var f = new PlaceFeature { Id = NextId("box"), Room = r.Id, Kind = FeatureKind.Container, DefinitionId = a.ContainerFeature, X = x, Z = z };
                f.Data["loot"] = r.Secret ? "vault_treasure" : a.LootTable;
                layout.Features.Add(f);
            }

            // Traps: just inside a passage's mouth, hidden until noticed.
            var traps = a.MaxTraps > 0 ? rng.Range(Math.Min(1, a.MaxTraps), a.MaxTraps) : 0;
            for (var i = 0; i < traps && i < ordinary.Count; i++)
            {
                var r = ordinary[(i * 2 + 1) % ordinary.Count];
                var trap = TrapCatalog.Find(a.Traps[rng.Next(a.Traps.Length)]);
                if (!cells.Find(r, rng, 1, out var x, out var z)) continue;
                var f = new PlaceFeature { Id = NextId("trap"), Room = r.Id, Kind = FeatureKind.Trap, DefinitionId = "trap", X = x, Z = z, Hidden = true, FindDc = trap.DetectDc };
                f.Data["trap"] = trap.Id;
                layout.Features.Add(f);
            }

            // Clues that tell the place's story.
            var pool = new List<string>(a.Clues);
            for (var i = 0; i < a.ClueCount && pool.Count > 0; i++)
            {
                var clue = pool[rng.Next(pool.Count)];
                pool.Remove(clue);
                var r = ordinary[rng.Next(ordinary.Count)];
                if (!cells.Find(r, rng, 1, out var x, out var z)) continue;
                var def = FeatureCatalog.Find(clue);
                layout.Features.Add(new PlaceFeature { Id = NextId("clue"), Room = r.Id, Kind = def.Kind, DefinitionId = clue, X = x, Z = z });
            }
        }
    }
}
