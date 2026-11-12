# ADR-0077: Travel follows a persistent route graph, measured on one stated scale

**Context.** Road geometry is regenerated from the map. Travel, caravans, blockades and bridges need roads that persist with state. Durations must come from data, not invented numbers.

**Decision.**
1. **Route segments** (`RouteSegmentData`) join two settlements. Each segment holds:
   - its kind: Road, Trail, Pass or Bridge;
   - its length in cells, its terrain cost (percent) and its condition;
   - its bridge and the bridge's state (`Places.BridgeState`, the Phase 14 states);
   - what blocks it, its repair days, and whether it is a border crossing.

   The segments are built once and saved, from two sources:
   - each region's map roads (terrain cost; a bridge where a road crosses water);
   - the border crossings. Each crossing also gets cross-region `TradeRoute`s, so goods move between regions through the existing trade system.
2. **Scale.**
   - A cell is 1.2 km and walking pace is 4.8 km/h.
   - Night (20:00–06:00) halves the pace.
   - A travel day for groups is 8 walking hours.

   These live in `TravelRules`, and every duration shown is computed from them.
3. **Planning.**
   - `RoutePlanner.Plan` is Dijkstra over passable segments.
   - `Alternatives` uses Yen's algorithm to give up to three ways.
   - A segment is impassable when it is blocked, its bridge is destroyed, or a blockade holds it.
   - Plans also work from mid-road, from the last settlement passed.
4. **Trade honours closures.**
   - A shipment due over a blocked route arrives a day later.
   - No new shipment departs on a blocked route; its status is `Blocked`.

**Consequences.**
- The same state gives the same plan.
- Three alternatives cost about 0.19 ms per pair in the six-region world (measured).
- Closing a road changes travel, caravans and trade together, because all three read the same segment.

**Alternatives considered.**
- **Pathfinding on map cells.** Rejected: it is costly across six maps, and roads would lose their identity and state.
- **Fixed travel times between towns.** Rejected: the brief requires estimates from data.
