# ADR-0070: People move through rooms along real passages; followers keep a room of their own

**Status.** Accepted, 2026-11-11 (Phase 14).

**Context.**
- Phase 13 placed residents in a room by activity and rebuilt them when the activity changed, so they appeared in a new room without walking there.
- Followers had no position inside a place: their figures were carried behind the player through every transition, including through doors the player had locked behind them.

**Decision.**
1. **Routes.** `PlaceRoutes.Find` does a breadth-first search over the room graph (rooms joined only by connections), so a route through a wall cannot exist. The fewest passages win, and ties go by connection order (deterministic). What each mover can pass:

   | Mover | Can pass | Cannot pass |
   |---|---|---|
   | Resident | closed and open doors; locked doors of an interior (their own keys); barred doors from the bar's side | secret ways |
   | Follower | open and closed doors; found secret ways; sound bridges | locked or barred doors; damaged or destroyed bridges |

   Stairs and ladders are passable for both.
2. **Indoor schedule.** `IndoorSchedule.RoomAt` is a pure function of the world and the minute. The Phase 5 activity plan says whether someone is in the building and what they are doing; this picks the room:
   - sleep in their own bedroom (households share the bedrooms out by person);
   - meals in the kitchen or hall;
   - other activities move among suitable rooms on a personal rhythm (a dwell of 30–75 minutes with a personal offset), so no two people keep the same timetable.

   Only rooms reachable from the way in are used. `IndoorSchedule.At` also gives the room a few minutes earlier and the route between, so the scene walks the figure through the doorways rather than moving it. People whose next whereabouts is elsewhere walk out by the way in. Cells are handed out in person order, so two people never share a cell.
3. **Followers in places.** A following escort records the place and room it is in (`EscortData.PlaceId` and `Room` while Following).
   - When the player moves, each follower takes the follower route from their room to the player's. If there is one, they arrive. If not (a door locked behind the player, a bridge down), they stay, with the reason in the HUD ("waiting: a locked door is in the way").
   - On every later move a waiting follower re-checks and catches up when a way opens: recovery from blocked routes.
   - Leaving a place takes only the followers who are with the player. Anyone left behind stays in that room, and the place keeps them.
   - Fights include only the followers who are in the fight's room.
   - Nothing in this foundation is a party system. It gives no orders, inventory or tactics (Phase 15).
4. **Cost.** No pathfinding runs per tick. Routes are computed when the scene asks or a command moves someone, over at most a few dozen rooms.

**Consequences.**
- A resident never appears behind a locked door they could not have passed, and never passes through a wall.
- A follower can be left behind and found again where they were.

**Alternatives considered.**
- *Unity NavMesh for indoor movement:* would make movement a presentation fact the simulation cannot test or save. The room graph is the authority; the scene only animates along it.
- *Followers teleporting to the player on every move (the Phase 13 behaviour):* passes locked doors.
