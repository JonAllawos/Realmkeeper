# ADR-0113: One travel state for the road and the country; the map position; waypoints

**Status.** Accepted, 2026-11-13 (Phase 24). It extends ADR-0078 (journeys) and ADR-0111 (the overland location).

**Context.**
- **Two places to be.** Phase 23 gave the player a place in the walkable country, while journeys (Phase 16) keep a place on the road graph: a segment, the settlement set out from, and metres along it.
- **Three gaps:**
  - the country could not be entered during a journey;
  - turning to the map lost the country position;
  - the atlas marker knew only settlements and the road.
- **The brief:** one travel state, the player's real position on the map, stepping off and back onto a journey, and waypoints that never reveal what is unknown.

**Decision.**
1. **One state, two views of it.**
   - A journey position is a point of the country: `OverlandRoads.PointOf` maps it onto the region's own road path (`RoadPath`, by the fraction travelled).
   - A crossing's first half lies in the starting region, between its way in and the edge toward the other region; the second half likewise in the other region.
   - The country's nearest road point is a journey position (`OverlandRoads.NearestRoad`).
2. **Stepping off.**
   - `EnterOverlandCommand` with no settlement, while on the road, puts the player at that point.
   - A journey under way is halted there ("you went on foot"), with its time and place kept. Outlaws barring the road refuse it.
   - The compass marks the place ("journey").
3. **Rejoining.** `RejoinJourneyCommand` resumes the halted journey from within 80 m of where the road was left (`OverlandRoads.RejoinMetres`), under the same checks as resuming. The metres along the segment are unchanged, so nothing moves twice.
4. **Ending a journey on foot.**
   - Entering the destination from the country ends the journey (arrived).
   - Entering another settlement, or crossing to another region, gives it up.
5. **Turning to the map** (`LeaveOverlandCommand`) puts the player where a journey can start from:
   - **the halted journey's place,** when within 80 m of it;
   - **a settlement,** when its way in is nearer than any road or within 400 m;
   - **otherwise the nearest road,** as a road position (an abandoned journey at that segment and metres), which the existing planner already routes from.

   The presentation charges the walk to that point with the same walking rule.
6. **The atlas marker** (`AtlasQueries.PlayerAt`) is the persistent position itself:
   - in the country, the pose on the region's cells (the atlas and the country share one grid);
   - in a place, the place's cell;
   - on the road, the journey position;
   - in a settlement, the settlement.
7. **Waypoints.** `PlayerCharacterData.Waypoint` holds one of:
   - `settlement:{id}`, `place:{id}`, `landmark:{id}` or `gateway:{id}`;
   - `journey` (where the road was left).

   **Only what is known can be marked:** an uncharted region's settlement, an undiscovered place or gateway, or an unfound landmark is refused, so trying reveals nothing.

   **Display:**
   - In the same region, the compass points to it and the HUD gives the distance in walked metres.
   - In another region, the HUD names it and the compass points to the way on toward it: the first step of a breadth-first search over passable crossings (`Waypoints.RegionPath`).
   - No road distance is invented.

**Consequences.**
- **The map position is never an arbitrary off-road point.** Arbitrary positions cannot be routed from by the existing planner. The walk to the road is charged instead of pretending.
- **Save schema v26:** the waypoint is optional; absent means no mark.

**Alternatives considered.**
- **A second "overland journey" state.** Rejected: two competing travel states (the brief).
- **Arbitrary map positions with a new planner.** Rejected for now: it needs off-road routing on the travel graph. It can come later without changing saves.
