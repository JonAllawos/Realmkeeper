using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Domain.Places
{
    /// <summary>
    /// Phase 13 (ADR-0063): checks a layout before it is used or saved. Rooms do not overlap; every passage touches
    /// the rooms it joins and no other; stairs land inside rooms one floor apart; every room that is not secret can be
    /// reached from the entrance without secret passages (locked doors count as passable, because every one can be
    /// picked, forced or opened with a key reachable before it); required things are never behind a secret; people and
    /// objects stand inside their rooms, clear of walls, stairs and each other; ceilings clear a person's head.
    /// </summary>
    public static class PlaceValidator
    {
        public const float MinCeiling = 2.4f;

        public static List<string> Validate(PlaceLayout layout)
        {
            var errors = new List<string>();
            if (layout == null)
            {
                errors.Add("No layout.");
                return errors;
            }

            var floors = new HashSet<int>();
            foreach (var f in layout.Floors) floors.Add(f.Index);
            var ids = new HashSet<string>(StringComparer.Ordinal);

            // Rooms.
            foreach (var r in layout.Rooms)
            {
                if (string.IsNullOrEmpty(r.Id) || !ids.Add(r.Id)) errors.Add($"Room id '{r.Id}' is missing or repeated.");
                if (!floors.Contains(r.Floor)) errors.Add($"{r.Id} is on floor {r.Floor}, which does not exist.");
                if (r.Rect.W < 2 || r.Rect.D < 2) errors.Add($"{r.Id} is too small to stand in ({r.Rect}).");
                if (r.Rect.X < 0 || r.Rect.Z < 0) errors.Add($"{r.Id} lies outside the place's grid.");
                if (r.Ceiling < MinCeiling) errors.Add($"{r.Id}'s ceiling ({r.Ceiling} m) is below head height.");
                if (r.Hazard != null && HazardCatalog.Find(r.Hazard) == null) errors.Add($"{r.Id} names an unknown hazard '{r.Hazard}'.");
            }

            for (var i = 0; i < layout.Rooms.Count; i++)
                for (var j = i + 1; j < layout.Rooms.Count; j++)
                {
                    var a = layout.Rooms[i];
                    var b = layout.Rooms[j];
                    if (a.Floor == b.Floor && a.Rect.Overlaps(b.Rect)) errors.Add($"Rooms {a.Id} and {b.Id} overlap.");
                }

            // Connections.
            var entrances = 0;
            foreach (var c in layout.Connections)
            {
                if (string.IsNullOrEmpty(c.Id) || !ids.Add(c.Id)) errors.Add($"Connection id '{c.Id}' is missing or repeated.");
                var a = layout.Room(c.A);
                if (a == null)
                {
                    errors.Add($"{c.Id} starts in an unknown room '{c.A}'.");
                    continue;
                }

                if (c.Kind == ConnectionKind.Entrance)
                {
                    entrances++;
                    if (!a.Rect.Overlaps(c.Rect) && !a.Rect.Touches(c.Rect)) errors.Add($"Entrance {c.Id} is not at its room {a.Id}.");
                    continue;
                }

                var b = layout.Room(c.B);
                if (b == null)
                {
                    errors.Add($"{c.Id} leads to an unknown room '{c.B}'.");
                    continue;
                }

                if (c.IsVertical)
                {
                    if (Math.Abs(Rank(layout, a.Floor) - Rank(layout, b.Floor)) != 1) errors.Add($"{c.Id}: {c.Kind} must join neighbouring floors ({a.Floor} and {b.Floor}).");
                    if (!a.Rect.Contains(c.Rect.X, c.Rect.Z)) errors.Add($"{c.Id}: the foot of the {c.Kind.ToString().ToLowerInvariant()} is outside {a.Id}.");
                    if (!b.Rect.Contains(c.ToX, c.ToZ)) errors.Add($"{c.Id}: it lands outside {b.Id}.");
                    continue;
                }

                if (a.Floor != b.Floor) errors.Add($"{c.Id} joins rooms on different floors without stairs.");
                if (c.Rect.W < 1 || c.Rect.D < 1) errors.Add($"{c.Id} has no width.");
                var ta = a.Rect.Overlaps(c.Rect) || a.Rect.Touches(c.Rect);
                var tb = b.Rect.Overlaps(c.Rect) || b.Rect.Touches(c.Rect);
                if (!ta || !tb) errors.Add($"{c.Id} does not reach both {a.Id} and {b.Id}.");
                foreach (var r in layout.Rooms)
                    if (r != a && r != b && r.Floor == a.Floor && r.Rect.Overlaps(c.Rect)) errors.Add($"{c.Id} runs through {r.Id}.");
                foreach (var o in layout.Connections)
                    if (o != c && !o.IsVertical && o.Kind != ConnectionKind.Entrance && layout.Room(o.A)?.Floor == a.Floor && o.Rect.Overlaps(c.Rect) &&
                        !a.Rect.Overlaps(c.Rect) && !b.Rect.Overlaps(c.Rect))
                        errors.Add($"Passages {c.Id} and {o.Id} cross.");
                if (c.Door == DoorState.Locked && c.LockDc <= 0 && c.KeyItem == null) errors.Add($"{c.Id} is locked with no key and no lock to pick.");
                if (c.Kind == ConnectionKind.Secret && c.FindDc <= 0) errors.Add($"Secret {c.Id} can never be found.");
            }

            if (entrances == 0) errors.Add("There is no way in.");

            // Reachability.
            var entrance = layout.Entrance;
            if (entrance != null)
            {
                var open = PlaceGenerator.Depths(layout, entrance.A, includeSecret: false);
                var all = PlaceGenerator.Depths(layout, entrance.A, includeSecret: true);
                foreach (var r in layout.Rooms)
                {
                    if (!all.ContainsKey(r.Id)) errors.Add($"{r.Id} cannot be reached at all.");
                    else if (!r.Secret && !open.ContainsKey(r.Id)) errors.Add($"{r.Id} can only be reached through a secret passage but is not marked secret.");
                }

                foreach (var f in layout.Features)
                    if (f.Required && !open.ContainsKey(f.Room)) errors.Add($"Required {f.Id} is not reachable without a secret.");
                foreach (var c in layout.Connections)
                    if (c.KeyItem != null && c.LockDc <= 0 && !KeyReachable(layout, c, entrance.A)) errors.Add($"The key to {c.Id} lies behind it.");
            }

            // Contents.
            var spots = new Dictionary<(int, int, int), string>();
            void Claim(PlaceRoom r, int x, int z, string who)
            {
                if (r == null) return;
                if (!r.Rect.Contains(x, z)) errors.Add($"{who} stands outside {r.Id} at ({x},{z}).");
                var key = (r.Floor, x, z);
                if (spots.TryGetValue(key, out var other)) errors.Add($"{who} and {other} share a cell in {r.Id}.");
                else spots[key] = who;
            }

            foreach (var c in layout.Connections)
                if (c.IsVertical)
                {
                    Claim(layout.Room(c.A), c.Rect.X, c.Rect.Z, c.Id);
                    Claim(layout.Room(c.B), c.ToX, c.ToZ, c.Id + " (landing)");
                }

            foreach (var f in layout.Features)
            {
                var r = layout.Room(f.Room);
                if (r == null) errors.Add($"{f.Id} is in an unknown room '{f.Room}'.");
                if (f.Kind == FeatureKind.Trap && TrapCatalog.Find(f.Get("trap")) == null) errors.Add($"{f.Id} names an unknown trap.");
                if (f.Kind != FeatureKind.Trap && FeatureCatalog.Find(f.DefinitionId) == null) errors.Add($"{f.Id} names an unknown feature '{f.DefinitionId}'.");
                if (f.Hidden && f.FindDc <= 0) errors.Add($"Hidden {f.Id} can never be found.");
                Claim(r, f.X, f.Z, f.Id);
            }

            foreach (var g in layout.Groups)
            {
                var r = layout.Room(g.Room);
                if (r == null)
                {
                    errors.Add($"{g.Id} is in an unknown room '{g.Room}'.");
                    continue;
                }

                if (g.Members.Count == 0) errors.Add($"{g.Id} has nobody in it.");
                foreach (var m in g.Members)
                {
                    var size = m.CreatureId != null && CreatureCatalog.Find(m.CreatureId)?.Size >= CreatureSize.Large ? 2 : 1;
                    if (m.CreatureId != null && CreatureCatalog.Find(m.CreatureId) == null) errors.Add($"{g.Id} names an unknown creature '{m.CreatureId}'.");
                    for (var i = 0; i < size; i++)
                        for (var j = 0; j < size; j++)
                            Claim(r, m.X + i, m.Z + j, g.Id + ":" + (m.CreatureId ?? m.NpcId.ToString()));
                    if (size > 1 && !Fits(layout, r, size)) errors.Add($"{g.Id}: a large creature cannot leave {r.Id}: its passages are too narrow.");
                }
            }

            return errors;
        }

        /// <summary>A floor's position from the bottom (floors are numbered in the order they were made; a cellar may come last).</summary>
        public static int Rank(PlaceLayout layout, int floor)
        {
            var me = layout.Floors.Find(f => f.Index == floor);
            if (me == null) return int.MinValue / 2;
            var rank = 0;
            foreach (var f in layout.Floors) if (f.Elevation < me.Elevation) rank++;
            return rank;
        }

        /// <summary>True when some passage out of the room is wide enough for a creature of size×size cells.</summary>
        private static bool Fits(PlaceLayout layout, PlaceRoom r, int size)
        {
            foreach (var c in layout.Connections)
                if (c.Joins(r.Id) && (c.IsVertical || Math.Min(c.Rect.W, c.Rect.D) >= size || c.Kind == ConnectionKind.Entrance)) return true;
            return false;
        }

        private static bool KeyReachable(PlaceLayout layout, PlaceConnection door, string from)
        {
            // Rooms reachable without passing this door: the key must be in a container in one of them.
            var seen = new HashSet<string>(StringComparer.Ordinal) { from };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                foreach (var c in layout.Connections)
                {
                    if (c == door || c.Kind == ConnectionKind.Entrance || c.Kind == ConnectionKind.Secret || !c.Joins(r)) continue;
                    var o = c.Other(r);
                    if (o != null && seen.Add(o)) queue.Enqueue(o);
                }
            }

            foreach (var f in layout.Features)
                if (f.Get("key") == door.KeyItem && seen.Contains(f.Room)) return true;
            return false;
        }
    }
}
