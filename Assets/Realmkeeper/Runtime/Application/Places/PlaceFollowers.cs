using System.Collections.Generic;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Places
{
    /// <summary>
    /// Phase 14 (ADR-0070): people following the player keep a room of their own inside a place. They come along any
    /// way a follower can pass (never a locked or barred door, never a damaged or fallen bridge, never a secret way the
    /// player has not found); when they cannot, they wait where they are, and they catch up as soon as a way opens. They
    /// leave a place only with the player. The same person is moved, never copied.
    /// </summary>
    public static class PlaceFollowers
    {
        /// <summary>Followers who came in with the player stand in the entrance room; anyone waiting here who can now reach it joins.</summary>
        public static void Enter(WorldData w, PlaceData place, string room)
        {
            foreach (var e in w.Escorts)
            {
                if (Alive(w, e) && e.State == EscortState.Following && e.PlaceId == null)
                {
                    e.PlaceId = place.PlaceId;
                    e.Room = room;
                }
            }

            Follow(w, place, room);
        }

        /// <summary>
        /// The player is now in <paramref name="room"/>: each follower here takes the follower route to it; one who cannot is
        /// left waiting; a waiting one whose way has opened catches up. Returns what changed, in words.
        /// </summary>
        public static List<string> Follow(WorldData w, PlaceData place, string room)
        {
            var said = new List<string>();
            if (w.Player == null || w.Player.PlaceId != place.PlaceId) return said; // the player was shown out
            var now = QuestClock.Now(w);
            foreach (var e in w.Escorts)
            {
                if (!Alive(w, e) || e.PlaceId != place.PlaceId) continue;
                if (e.State != EscortState.Following && e.State != EscortState.Waiting) continue;
                if (e.State == EscortState.Waiting && WasHeldHere(place, e)) continue; // a captive not yet freed waits to be freed
                var name = w.FindNpc(e.NpcId)?.Name ?? "Your companion";
                if (e.Room == room)
                {
                    if (e.State == EscortState.Waiting) { e.State = EscortState.Following; e.ChangedAt = now; said.Add(name + " is with you again."); }
                    continue;
                }

                var route = PlaceRoutes.Find(place, e.Room, room, Mover.Follower);
                if (route != null)
                {
                    if (e.State == EscortState.Waiting) said.Add(name + " catches up with you.");
                    e.Room = room;
                    e.State = EscortState.Following;
                    e.ChangedAt = now;
                    continue;
                }

                if (e.State == EscortState.Following)
                {
                    e.State = EscortState.Waiting;
                    e.ChangedAt = now;
                    said.Add($"{name} cannot follow — {PlaceRoutes.Blocker(place, e.Room, room)} — and waits in the {place.Layout.Room(e.Room)?.Name?.ToLowerInvariant() ?? "room behind"}.");
                }
            }

            return said;
        }

        /// <summary>Leaving the place: those with the player come out; anyone else stays where they wait.</summary>
        public static List<string> Leave(WorldData w, PlaceData place, string room)
        {
            var left = new List<string>();
            foreach (var e in w.Escorts)
            {
                if (!Alive(w, e) || e.PlaceId != place.PlaceId) continue;
                if (e.State == EscortState.Following && e.Room == room)
                {
                    e.PlaceId = null;
                    e.Room = null;
                    continue;
                }

                if (e.State == EscortState.Following || (e.State == EscortState.Waiting && !WasHeldHere(place, e)))
                {
                    e.State = EscortState.Waiting;
                    left.Add(w.FindNpc(e.NpcId)?.Name ?? "someone");
                }
            }

            return left;
        }

        /// <summary>Who is waiting in this place, and where, for the HUD.</summary>
        public static List<(string Name, string Room, string Why)> Waiting(WorldData w, PlaceData place, string playerRoom)
        {
            var list = new List<(string, string, string)>();
            foreach (var e in w.Escorts)
                if (Alive(w, e) && e.State == EscortState.Waiting && e.PlaceId == place.PlaceId && !WasHeldHere(place, e))
                    list.Add((w.FindNpc(e.NpcId)?.Name ?? "someone", place.Layout.Room(e.Room)?.Name ?? e.Room, PlaceRoutes.Find(place, e.Room, playerRoom, Mover.Follower) == null ? PlaceRoutes.Blocker(place, e.Room, playerRoom) : "can reach you"));
            return list;
        }

        private static bool Alive(WorldData w, EscortData e) => w.FindNpc(e.NpcId) is NpcData n && !n.IsDeceased;

        /// <summary>A person still held captive here (their feature not yet freed).</summary>
        private static bool WasHeldHere(PlaceData place, EscortData e)
        {
            foreach (var f in place.Layout.Features)
                if (f.Kind == FeatureKind.Captive && f.Get("npc") == e.NpcId.ToString() && !place.Has(f.Id, FeatureFlags.Freed)) return true;
            return false;
        }
    }
}
