# ADR-0063: Layouts are generated on a slot grid from archetypes, validated, and saved — never regenerated

**Status.** Accepted, 2026-11-11 (Phase 13).

**Context.**
- Places need believable, navigable layouts with meaningful variation, deterministic from the seed.
- Every mandatory thing must be reachable. Rooms must not overlap. Stairs must be possible. Large creatures must fit.
- A generator update must not change places that already exist.
- Combat happens on a grid of 5-foot squares.

**Decision.**
1. **Cells are combat squares.** 1 cell = 1.5 m. A fight in a room uses the room's own cells.
2. **Slot grid.** Each floor is a 4 × 4 grid of slots (10–14 cells, by archetype).
   - A room sits inside one slot, with a margin, so rooms can never overlap.
   - Every room in a slot covers the slot's centre band. A corridor between neighbouring slots therefore runs straight through that band and always meets both rooms.
   - A spanning tree joins every room; extra loops are added by chance.
   - Upper (or lower) floors grow from the slot above (or below) a stair or ladder.
   - Secret rooms take a free neighbouring slot, behind a secret connection.
3. **Archetypes.** Ten of them: cave, mine, ruin, keep, crypt, catacomb, temple, bandit hideout, underground works and abandoned hamlet. Each decides:
   - floors, room counts and sizes, corridor width, ceiling height and light;
   - loops, doors and locks, secret rooms;
   - entrance and deepest-room kinds, and the room mix;
   - material, inhabitants, traps, hazards, container and loot table, and the clue pool.
4. **Story features.** A definition's own features (the foreman's ledger, the captive) go into rooms of the right kind, deepest first. A required one is never placed behind a secret.
5. **Validation (`PlaceValidator`).** A generated layout is used only if it passes. A seed that cannot pass moves deterministically to its next attempt. The validator checks:
   - room ids and floors;
   - no overlaps;
   - passages touching exactly the rooms they join;
   - stairs joining neighbouring floors (ordered by height), with their foot and landing inside rooms;
   - every room that is not secret reachable from the entrance without secrets;
   - required features reachable;
   - locked doors that can be picked or forced, or whose key lies before them;
   - secret ways that can be found;
   - features and people inside their rooms, not sharing cells or standing on stairs;
   - a passage wide enough for any Large creature;
   - ceilings above head height.
6. **Saved, not regenerated.** The layout is saved with the place. `GeneratorVersion` is recorded. A later generator can make new places differently but never touches an existing one.
7. **Interiors.** `InteriorGenerator` plans rooms that share walls, from the building's function (home, workshop, store, guardhouse, chapel, civic hall, tower), its footprint, its storeys and the settlement's wealth (a cellar for the well-off). It is checked by the same validator.
8. **Authored places.** They use the same model (`AuthoredPlaces.WatchFort`) and are validated the same way.

**Consequences.**
- Measured: 2,500 generated layouts (10 archetypes × 250 seeds) and 720 interiors, with 0 validation failures (`PlaceGenerationTests`).
- Layouts are rectangular rooms and straight corridors. Natural caves look like dug rooms; irregular shapes are a later refinement.

**Alternatives considered.**
- *Free room placement with corridor carving:* overlaps and dead corridors need repair passes that are hard to prove correct.
- *Wave-function collapse or tile sets:* heavier, and harder to validate for reachability.
- *Regenerating from the seed on load:* a generator change would silently move rooms under saved state.
