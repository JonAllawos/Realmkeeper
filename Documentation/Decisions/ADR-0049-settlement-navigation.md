# ADR-0049: People walk the plan's own roads: a road graph, ground heights and one leaving point

**Status.** Accepted, 2026-11-11 (Phase 10).

**Context.**
- `LocalPaths.Route` assumed the old two-street cross. It projected onto the nearest street and turned the corner at the plaza.
- Planned settlements have dozens of roads, bridges and real ground heights.

**Decision.**
1. **Road graph.** For a planned settlement, `LocalPaths.GraphOf(layout)` builds a graph, once per layout:
   - each road's points, joined along the road;
   - points of different roads within 4.5 m of each other, joined across;
   - points around the square, joined across it.
   Routes are the shortest path (Dijkstra) between the nodes nearest to each end, with the door legs of enterable buildings as before.
2. **Standing height.** `LocalPaths.StandingHeight(layout, x, z)` is a building's floor inside it, a bridge's deck on it, and the ground elsewhere. Residents, props, signs and lamps use it. The player stands on the terrain collider.
3. **Arrival and leaving.** The arrival pose is the plan's arrival point on the first main road, at ground height. People leaving walk to the same point (`LeavingPoint`). The guard's post and the gate site are a few metres inside it.
4. **Older environments unchanged.** Without a plan the old routing, flat ground and south gate apply.

**Consequences.**
- Routes from the way in to every site door stay on roads and never pass through a building (tested for all sites in the reference world).
- The graph is small: a few hundred nodes, built in milliseconds.

**Alternatives considered.**
- *Unity NavMesh:* engine-bound and untestable in the domain; it would duplicate the plan.
- *Grid A\* over the plan:* more cost for the same routes along roads.
