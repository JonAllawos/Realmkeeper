# ADR-0011: Buildings assembled from engine-free kit plans

**Status.** Accepted, 2026-11-10 (graphics pass).

**Context.** Local environments are engine-free layouts (`LocalEnvironmentLayout`). They are
derived from world data and never saved (ADR-0009). Buildings in a layout are boxes with a
kind, footprint, height, yaw and style. They must now look like timber-framed and stone
medieval buildings, stay deterministic and testable outside Unity, and fit a budget of
1,500 draw calls in a town.

**Decision.**

1. **The plan is domain data.** `BuildingKitPlanner` (in `Exploration.Domain`, engine-free) turns
   a `LayoutBuilding` plus the settlement type and a stable variation key (settlement ID and
   building key) into a `BuildingPlan`. The plan holds an archetype, a footprint snapped down to
   the kit's 2 m grid, storeys, and an ordered list of `ModulePlacement`s (kit module ID or
   procedural `@` piece, position, yaw and scale). Randomness comes from `SeedMixer` hashes,
   never from `GetHashCode` or engine state.
2. **Eight archetypes, three profiles.** The archetypes are Cottage, Townhouse, MerchantHouse,
   Workshop, Tavern, Granary, Tower and Gatehouse. A `SettlementVisualProfile` per settlement
   type (hamlet, village, town) picks the wall families, the maximum storeys and the house
   archetypes. The layout itself is unchanged except for one derived field (`Type`).
3. **Presentation instantiates the plan.** `ModularEnvironmentBuilder` (Unity) looks modules up in
   the `ArtCatalog` (a ScriptableObject in Resources, built by `Realmkeeper ▸ Set Up Art`). It
   keeps each model's own root rotation and scale, and builds the procedural pieces (awnings,
   flower boxes, barrels, signs, lanterns, counters, the gate gallery) from metre-UV meshes.
4. **Merge for the budget.** After assembly, static geometry is merged into one mesh per
   (24 m cell, material, shadow mode) by `MeshMerger`. A town drops from about 2,100 renderers to
   about 310, and 280–950 draw calls per frame including shadow, depth and SSAO passes.
5. **Simple collision.** Each building gets one box collider, except the tavern, whose walls are
   boxes that leave its doorway open. Props and walls get boxes. There are no mesh colliders.
6. **Fallback.** Without a catalog (or in the .NET harness), the primitive builder from the
   vertical slice is used unchanged.

**Consequences.** Plans can be tested headless: the tests cover determinism, footprints, coverage
of the kit, duplicates, doors and archetypes. Visual variety comes from the archetype and from
per-building choices (door slot, shutters, flower boxes, vines, chimneys). The catalog must
contain every ID in `BuildingKitPlanner.KitModuleIds`, which a PlayMode test checks. Merged
meshes trade fine-grained culling for far fewer draws.

**Alternatives considered.**
- Hand-made prefabs per building: not data-driven, and no variety.
- Unity static batching: under the SRP Batcher it does not reduce draw calls, and the measured
  frame stayed at about 4,400 batches.
- GPU instancing: the pieces vary too much per instance to help.
