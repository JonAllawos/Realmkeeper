using System.Collections.Generic;

namespace Realmkeeper.Domain.Places
{
    /// <summary>Who is moving, which decides the doors they can pass (Phase 14, ADR-0070).</summary>
    public enum Mover
    {
        /// <summary>Someone who lives or serves here: opens closed doors, has the keys to locked ones, knows no secret ways.</summary>
        Resident = 0,

        /// <summary>Someone following the player: opens closed doors, never a locked or barred one, takes only found secret ways and sound bridges.</summary>
        Follower = 1,

        /// <summary>The map: every passage that exists, whatever its lock (for explaining what is in the way).</summary>
        Map = 2,
    }

    /// <summary>One step of a route: the passage taken and the room it leads into.</summary>
    public readonly struct RouteStep
    {
        public RouteStep(string connectionId, string roomId)
        {
            ConnectionId = connectionId;
            RoomId = roomId;
        }

        public string ConnectionId { get; }
        public string RoomId { get; }
    }

    /// <summary>
    /// Routes through a place's rooms along its real passages (Phase 14, ADR-0070): breadth-first over the room graph,
    /// so a route is the fewest passages, ties broken by connection order (deterministic). A passage counts only when this
    /// mover can use it in its current state. A route through a wall cannot exist: rooms are joined only by connections.
    /// The graph is a few dozen rooms at most; nothing here runs per frame or per clock tick.
    /// </summary>
    public static class PlaceRoutes
    {
        /// <summary>Whether this mover can use this passage now (both ways for doors; stairs and ladders as they stand).</summary>
        public static bool Passable(PlaceData place, PlaceConnection c, string fromRoom, Mover who)
        {
            if (c == null || c.Kind == ConnectionKind.Entrance) return false;
            if (who == Mover.Map) return !(c.Kind == ConnectionKind.Secret && !place.Found(c)) && !(c.Kind == ConnectionKind.Bridge && place.BridgeOf(c) == BridgeState.Destroyed);
            if (c.Kind == ConnectionKind.Secret && (who == Mover.Resident || !place.Found(c))) return false;
            if (c.Kind == ConnectionKind.Bridge)
            {
                var b = place.BridgeOf(c);
                if (b == BridgeState.Destroyed) return false;
                if (b == BridgeState.Damaged && who == Mover.Follower) return false; // they will not risk it
                return true;
            }

            if (c.Kind == ConnectionKind.Opening || c.IsVertical) return true;
            switch (place.DoorOf(c))
            {
                case DoorState.Open:
                case DoorState.Broken:
                case DoorState.Closed:
                    return true;
                case DoorState.Locked:
                    return who == Mover.Resident && place.Category == PlaceCategory.Interior;
                case DoorState.Barred:
                    return who == Mover.Resident && fromRoom == c.A; // lifted from its own side
                default:
                    return false;
            }
        }

        /// <summary>The route from one room to another (excluding the start), or null when there is none for this mover.</summary>
        public static List<RouteStep> Find(PlaceData place, string from, string to, Mover who, int maxSteps = 32)
        {
            if (place?.Layout == null || from == null || to == null) return null;
            if (from == to) return new List<RouteStep>();
            var back = new Dictionary<string, (string Prev, string Via)> { [from] = (null, null) };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                foreach (var c in place.Layout.Connections)
                {
                    if (!c.Joins(room)) continue;
                    var next = c.Other(room);
                    if (next == null || back.ContainsKey(next) || !Passable(place, c, room, who)) continue;
                    back[next] = (room, c.Id);
                    if (next == to)
                    {
                        var path = new List<RouteStep>();
                        for (var at = to; at != from; at = back[at].Prev) path.Add(new RouteStep(back[at].Via, at));
                        path.Reverse();
                        return path.Count <= maxSteps ? path : null;
                    }

                    queue.Enqueue(next);
                }
            }

            return null;
        }

        /// <summary>Every room this mover can reach from a room (the room itself included).</summary>
        public static HashSet<string> Reachable(PlaceData place, string from, Mover who)
        {
            var seen = new HashSet<string>();
            if (place?.Layout == null || from == null) return seen;
            var queue = new Queue<string>();
            seen.Add(from);
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                foreach (var c in place.Layout.Connections)
                {
                    if (!c.Joins(room)) continue;
                    var next = c.Other(room);
                    if (next == null || seen.Contains(next) || !Passable(place, c, room, who)) continue;
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }

            return seen;
        }

        /// <summary>Why a follower cannot reach a room, in words (the first impassable passage on the ignoring-doors route).</summary>
        public static string Blocker(PlaceData place, string from, string to)
        {
            var any = Find(place, from, to, Mover.Map);
            if (any == null) return "there is no way through";
            var at = from;
            foreach (var step in any)
            {
                var c = place.Layout.Connection(step.ConnectionId);
                if (!Passable(place, c, at, Mover.Follower))
                {
                    if (c.Kind == ConnectionKind.Bridge) return place.BridgeOf(c) == BridgeState.Destroyed ? "the bridge is down" : "they will not cross the broken bridge";
                    var state = place.DoorOf(c);
                    return state == DoorState.Locked ? "a locked door is in the way" : state == DoorState.Barred ? "a barred door is in the way" : "the way is shut";
                }

                at = step.RoomId;
            }

            return "the way is shut";
        }
    }
}
