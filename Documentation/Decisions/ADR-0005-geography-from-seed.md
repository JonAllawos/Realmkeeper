# ADR-0005: Geography rebuilt from seed, not saved

**Context.** The region map holds 16,384 cells of elevation and terrain plus rivers and roads.
Saving it would multiply save size and add a second source of truth next to the seed.

**Decision.** Saves store `seed` and `generatorVersion`. On load, `IWorldGenerator.RegenerateRegion`
rebuilds the map, and the session checks that every saved settlement still sits on valid land.
A save whose generator version this build doesn't support is refused with a clear message.

**Consequences.** Small saves; one source of truth. Any change to generator output requires a
version bump. Supporting old saves after a bump means keeping the old generator version
available. Manual terrain edits (World Editor) will have to be saved as a list of changes
replayed on the regenerated map; `TerrainEdit` records already exist for this. Once
settlements can be founded during play, roads must become saved data.

**Alternatives.** Save the full map: simple, robust to generator changes, about 100–300 KB per
save as JSON. That is the fallback if keeping old generator versions becomes a burden.
