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
  - Transitions take 230–760 ms (after the follow-up, which builds the nearest town from the kit during loading).
  - Walking: median frame 1.3 ms, 95th percentile 1.9 ms.
  - A detailed chunk builds in 3–4 ms.
  - Across repeated transitions, objects stay flat and memory does not grow.

  Full figures are in the Phase 23 completion report.
- **Follow-up (owner request, 2026-11-13).**
  - **Trunks and rocks are solid.** A pooled set of capsules is moved onto the trunks and rocks within 45 m on each streaming tick (`OverlandWorld.PlaceSolidColliders`). Ground cover is not solid.
  - **Entry from the map.** "Walk the country" on the map's settlement inspector dispatches `EnterOverlandCommand(settlement)`, under the same rules as entering it (the player's region, no journey under way). The exploration view builds the country behind the loading screen (`ArriveFromMap`).
  - **Ground textures.** `Realmkeeper/VertexColorLit` gains optional world-space ground textures: the owned ambientCG grass and ground, and the village kit's rock. Each is divided by its own average, so the biome colour keeps its hue and the texture adds grain, blended per vertex (dirt, rock and slope). Other meshes are unchanged (strength 0).
  - **Ground cover.** Grass, ferns and flowers from the owned nature kit, made per chunk within 200 m, drawn within 95 m, casting no shadow.
  - **Kit silhouettes.** A settlement seen from the country is built from its own plan with the village kit (`ModularEnvironmentBuilder.BuildSilhouette`): merged, with no colliders, and its emptied pieces destroyed. To keep transitions fast, the nearest town is upgraded during loading and the others one at a time as the player comes within 1.4 km (33–157 ms each). Further towns stay block shapes under the haze.
  - **Vegetation drawn per camera.** Draws are submitted from `RenderPipelineManager.beginCameraRendering`, so every camera, captures included, sees them.
  - **A bug fixed.** The kit models were drawn without their root rotation and scale (the Blender export frame), so trees and grass lay flat, half underground. They are now upright, as the town's scenery places them.

**Alternatives considered.**
- **Additive, seamless region streaming.** Rejected: it needs a floating origin and seam stitching, and would run two environments at once, against the streamer's rule.
- **Unity Terrain.** Rejected for now: a runtime mesh from the engine-free height function keeps one source of truth with the domain's passability and heights. Terrain stays an option if detail texturing needs it.
