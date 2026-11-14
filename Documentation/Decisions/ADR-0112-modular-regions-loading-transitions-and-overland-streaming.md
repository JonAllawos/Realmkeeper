# ADR-0112: Modular regions, loading transitions, and streaming within a region

**Status.** Accepted, 2026-11-13 (Phase 23).

**Context.**
- **The brief.** It approves loading screens and prefers reliability and performance to seamlessness.
- **What already exists.**
  - `LocalEnvironmentStreamer` keeps one active local environment: a settlement layout or a place.
  - Environment changes happened in a single frame, with no feedback.
- **What the overland adds.** A third kind of environment, about 5 km across. It needs distance-based detail within a region and a clear transition between environments.

**Decision.**
1. **Modular, one environment at a time.** Each of these is its own environment, and moving between them is a loading transition:
   - a region's country;
   - a settlement;
   - a place;
   - another realm's region.

   A transition unloads the current environment before it loads the next one.
2. **The transition (`ExplorationController.Travel`).** It runs these steps in order:
   1. Show the loading screen: the destination and what is happening.
   2. Dispatch the command (enter, cross, pass through).
   3. If the command is refused, show its reason with "You stay where you were", and leave nothing changed.
   4. Otherwise, tear down the current environment, then build the next one with progress reported.
   5. Place the player at the entry: a settlement's gate (a road cell 6–8 cells from its centre), a place's approach, a region's edge facing the neighbour, or a gateway's far end.
   6. Hide the screen.

   It records `LastTransitionMs` and `LastTransitionMessage`.
3. **The loading screen (`LoadingScreen`).** It sits on its own UI document at sorting order 100, outside the environment it hides. It is deliberately plain: a title, the destination, the status, a progress bar and a failure state. Artwork comes later.
4. **Streaming within a region (`OverlandWorld`).**
   - **Ground:**
     - one coarse mesh of the whole region, at one vertex per cell, for the horizon;
     - detailed chunks of 16 × 16 cells, at four vertices per cell (10 m), each with a `MeshCollider`, built within 1000 m of the player at one chunk per 0.2 s tick and dropped beyond.
   - **Trees and rocks:** taken from the purchased Stylized Nature MegaKit and drawn with `Graphics.RenderMeshInstanced` within 650 m, by chunk. Their materials sit on a hidden carrier renderer so the seasonal tint (Phase 21) reaches them.
   - **Barriers:** box colliders only on impassable cells next to walkable ones, near the player.
   - **Water and roads:** a water plane, and road ribbons 8 m wide.
     - Road paths are un-staired, then Chaikin-smoothed.
     - Bridges show as planks over water.
   - **Fog and atmosphere:** fog runs from 250 to 1700 m and the camera's far plane is 2600 m. `ExplorationAtmosphere.FogScale` and `RealmTint` adapt the existing day and night cycle.
   - **Before arrival:** `Prewarm` builds the chunks around the arrival point before the screen hides.
5. **Navigation that respects discovery.**
   - A compass strip shows known sites, the cardinal points and the ways on.
   - A location line shows the region, the ground and the nearest known site.
   - A north-up region map (N) shows the ground by biome, roads, water, known sites and the player.
   - Places and gateways not yet found stay off the compass and the map until the player comes within sight of them.
6. **Entry points.** From a settlement, "Out to the country (O)" leads into the country. Returning to the world map leaves it. A place entered from the country returns there.

**Consequences.**
- **No floating origin is needed.** A region is 5.12 km, within float precision for the player's movement.
- **Measured (PlayMode, editor, batch).**
  - Transitions take 230–670 ms.
  - Walking: median frame 1.3 ms, 95th percentile 1.9 ms.
  - A detailed chunk builds in 3–4 ms.
  - Across repeated transitions, objects stay flat and memory does not grow.

  Full figures are in the Phase 23 completion report.
- **Trees have no colliders yet.** Only barriers, the ground and the site models collide.
- **The strategic map does not drop the player into the country directly.** The way in is through a settlement.

**Alternatives considered.**
- **Additive, seamless region streaming.** Rejected: it needs a floating origin and seam stitching, and would run two environments at once, against the streamer's rule.
- **Unity Terrain.** Rejected for now: a runtime mesh from the engine-free height function keeps one source of truth with the domain's passability and heights. Terrain stays an option if detail texturing needs it.
