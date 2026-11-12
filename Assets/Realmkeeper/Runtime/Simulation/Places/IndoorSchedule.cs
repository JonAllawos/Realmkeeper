using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Simulation.Places
{
    /// <summary>Where a resident is inside their building at a minute, and the way they took from where they were before.</summary>
    public sealed class IndoorPresence
    {
        public NpcData Npc { get; set; }
        public ActivityKind Activity { get; set; }
        public string Room { get; set; }

        /// <summary>The room a few minutes ago (same as <see cref="Room"/> when they stayed put).</summary>
        public string FromRoom { get; set; }

        /// <summary>The passages from <see cref="FromRoom"/> to <see cref="Room"/> (empty when they stayed).</summary>
        public List<RouteStep> Route { get; set; } = new List<RouteStep>();

        /// <summary>They are on their way out of the building (their next place is elsewhere).</summary>
        public bool Leaving { get; set; }

        public int X { get; set; }
        public int Z { get; set; }
    }

    /// <summary>
    /// Phase 14 (ADR-0070): residents move through their building. The outdoor schedule (Phase 5) says whether they are
    /// home and what they are doing; this picks the room. Sleep is in their own bedroom (households share out the
    /// bedrooms by person), meals in the kitchen or hall, and other activities move among the rooms that suit them, each
    /// person on their own rhythm (a dwell of 30–75 minutes, offset by person), so no two keep the same timetable.
    /// A room is used only if it can be reached from the way in along passages they can use (their own keys open their
    /// own locks; nobody walks through a wall). It is a pure function of the world and the minute, so it is
    /// deterministic, needs nothing saved, and costs a lookup per resident when the scene asks — never per tick.
    /// </summary>
    public static class IndoorSchedule
    {
        /// <summary>Minutes looked back to see whether they just moved (the scene walks them along the way).</summary>
        public const int MoveWindow = 3;

        public static IndoorPresence At(WorldData world, PlaceData place, NpcData npc, long absoluteMinute)
        {
            if (place?.Layout == null || npc == null || npc.IsDeceased) return null;
            var room = RoomAt(world, place, npc, absoluteMinute, out var activity);
            if (room == null) return null;
            var before = RoomAt(world, place, npc, absoluteMinute - MoveWindow, out _);
            var entrance = place.Layout.Entrance?.A;
            var from = before ?? entrance;
            var presence = new IndoorPresence { Npc = npc, Activity = activity, Room = room, FromRoom = from };
            if (from != room) presence.Route = PlaceRoutes.Find(place, from, room, Mover.Resident) ?? new List<RouteStep>();
            var after = RoomAt(world, place, npc, absoluteMinute + MoveWindow, out _);
            presence.Leaving = after == null;
            return presence;
        }

        /// <summary>The room they are in, or null when they are not in this building at that minute.</summary>
        public static string RoomAt(WorldData world, PlaceData place, NpcData npc, long absoluteMinute, out ActivityKind activity)
        {
            activity = ActivityKind.None;
            if (place.LocationId.IsEmpty) return null;
            var day = (int)Math.Floor(absoluteMinute / (double)WorldTime.MinutesPerDay);
            var minute = (int)(absoluteMinute - (long)day * WorldTime.MinutesPerDay);
            var date = WorldDate.FromDayNumber(day);
            var where = NpcActivityPlan.At(world, npc, date, minute);
            if (!where.IsKnown || where.Location != place.LocationId) return null;
            activity = where.Activity;
            var reachable = PlaceRoutes.Reachable(place, place.Layout.Entrance?.A, Mover.Resident);
            var options = new List<PlaceRoom>();
            foreach (var k in Preferred(activity))
                foreach (var r in place.Layout.Rooms)
                    if (r.Kind == k && reachable.Contains(r.Id) && !options.Contains(r)) options.Add(r);
            if (options.Count == 0)
                foreach (var r in place.Layout.Rooms)
                    if (reachable.Contains(r.Id) && !r.Secret) options.Add(r);
            if (options.Count == 0) return place.Layout.Entrance?.A;

            var person = SeedMixer.HashString(npc.NpcId.ToString());
            if (activity == ActivityKind.Sleeping || options.Count == 1)
                return options[(int)(person % (ulong)options.Count)].Id; // their own bed, every night

            var dwell = 30 + (int)(SeedMixer.Mix(person ^ 0x51ED) % 46); // 30–75 minutes
            var offset = (long)(SeedMixer.Mix(person ^ 0xA77E) % (ulong)dwell);
            var slot = (absoluteMinute + offset) / dwell;
            var pick = SeedMixer.Mix(SeedMixer.Combine(person, (ulong)slot));
            return options[(int)(pick % (ulong)options.Count)].Id;
        }

        /// <summary>Rooms that suit an activity, most suitable first; shared spaces join the daytime ones.</summary>
        public static RoomKind[] Preferred(ActivityKind activity)
        {
            switch (activity)
            {
                case ActivityKind.Sleeping: return new[] { RoomKind.Bedroom, RoomKind.Quarters, RoomKind.Barracks };
                case ActivityKind.Eating: return new[] { RoomKind.Kitchen, RoomKind.Hall, RoomKind.TapRoom };
                case ActivityKind.Working:
                case ActivityKind.Trading: return new[] { RoomKind.Workshop, RoomKind.Office, RoomKind.Storage, RoomKind.TapRoom, RoomKind.Barracks };
                case ActivityKind.Worshipping: return new[] { RoomKind.Shrine, RoomKind.Hall };
                case ActivityKind.Guarding: return new[] { RoomKind.Gatehouse, RoomKind.Barracks };
                case ActivityKind.Resting: return new[] { RoomKind.Bedroom, RoomKind.Chamber, RoomKind.Hall };
                default: return new[] { RoomKind.Hall, RoomKind.Chamber, RoomKind.Kitchen, RoomKind.Workshop, RoomKind.TapRoom };
            }
        }

        /// <summary>
        /// Everyone in the building now, each on a cell of their room nobody else stands on (cells handed out in
        /// person order, so the same people always get the same cells).
        /// </summary>
        public static List<IndoorPresence> Everyone(WorldData world, PlaceData place, long absoluteMinute)
        {
            var list = new List<IndoorPresence>();
            if (place?.Layout == null || place.LocationId.IsEmpty) return list;
            var people = new List<NpcData>();
            foreach (var n in world.Npcs)
                if (n != null && !n.IsDeceased && !PlacePresence.IsAway(world, n)) people.Add(n);
            people.Sort((a, b) => a.NpcId.CompareTo(b.NpcId));
            var taken = new HashSet<(int, int)>();
            foreach (var n in people)
            {
                var p = At(world, place, n, absoluteMinute);
                if (p == null) continue;
                var room = place.Layout.Room(p.Room);
                (p.X, p.Z) = FreeCell(place, room, n, taken);
                taken.Add((p.X, p.Z));
                list.Add(p);
            }

            return list;
        }

        /// <summary>A cell of the room, hashed by person, skipping cells taken and cells with something on them.</summary>
        public static (int, int) FreeCell(PlaceData place, PlaceRoom room, NpcData npc, HashSet<(int, int)> taken)
        {
            var h = SeedMixer.Mix(SeedMixer.HashString(npc.NpcId.ToString()));
            var area = room.Rect.Area;
            for (var i = 0; i < area; i++)
            {
                var k = (int)((h + (ulong)i) % (ulong)area);
                var x = room.Rect.X + k % room.Rect.W;
                var z = room.Rect.Z + k / room.Rect.W;
                if (taken.Contains((x, z))) continue;
                if (place.Layout.Features.Exists(f => f.Room == room.Id && f.X == x && f.Z == z)) continue;
                return (x, z);
            }

            return (room.Rect.CenterX, room.Rect.CenterZ);
        }
    }
}
