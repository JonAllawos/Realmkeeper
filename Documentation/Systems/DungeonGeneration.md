# Layout generation and validation

Phase 13. Decision: ADR-0063. Code: `PlaceGenerator`, `PlaceValidator`, `ArchetypeCatalog`, `AuthoredPlaces`.

## Inputs

- The world seed and the place's own seed (`Combine(world, "place:" + definition)`).
- The archetype and the definition (its story features).
- `ArchetypeCatalog.GeneratorVersion`, which is saved with the place.

The same inputs always give the same layout. A seed that cannot produce a valid layout moves to its next attempt (up to 8), also deterministically.

## Algorithm

1. **Floors.** Between the archetype's minimum and maximum. They go down (mine, crypt, catacomb, underground) or up (keep).
2. **Slots.** Each floor is a 4 × 4 grid of slots, each `Slot` cells wide (10–14).
3. **Rooms.** One per slot, inside it with a one-cell margin, so rooms never overlap. Each room covers its slot's centre band.
4. **Tree.** Growth starts at the entrance slot (floor 0, south edge). A frontier of free neighbouring slots is grown at random until the floor's room count is reached. Every growth step is a corridor, so the floor is a spanning tree and fully connected.
5. **Loops.** Neighbouring rooms not yet joined are joined with probability `LoopChance`.
6. **Corridors.** They run straight between neighbouring slots through the shared centre band, `CorridorWidth` cells wide. A corridor may get a door (`DoorChance`), and a door may be locked (`LockedChance`, with a lock DC of 12–15 that can be picked or forced).
7. **Upper and lower floors.** Each grows from the slot above or below a room of the previous floor. That room's corner holds the stairs (ladders in mines and catacombs); the landing is the matching corner one floor away.
8. **Kinds.** The entrance room gets the archetype's entrance kind and the deepest room (most steps from the entrance) its deep kind. Other rooms are drawn from the room pool. Ladder rooms in mines become shafts.
9. **Secret rooms.** A free neighbouring slot of an ordinary room, joined by a secret connection with a find DC of 12–15.
10. **Contents.** These go into an occupancy grid that keeps stair cells and the cells just inside each passage's mouth clear:
    - story features: definition-specific, in rooms of their kind, deepest first; required ones never in secret rooms;
    - inhabitant groups: in rooms of their preferred kinds, deepest first, members on free cells (2 × 2 for Large creatures);
    - containers: every secret room has one, then storage-like rooms;
    - traps: hidden, with find DC equal to the trap's detection DC;
    - clues: drawn from the archetype's pool.
11. **Light and hazards.** Rooms are lit by `LitChance`. Hazards are drawn by `HazardChance`; darkness only in unlit rooms.

## Validation (`PlaceValidator`)

A layout is rejected unless:

- **Rooms:** ids are unique; every room is on an existing floor, at least 2 × 2 and inside the grid; no two rooms on a floor overlap.
- **Passages:**
  - every horizontal passage touches both rooms it joins and runs through no other room;
  - passages do not cross;
  - stairs and ladders join floors that are neighbours by height, with the foot and landing inside their rooms;
  - there is at least one entrance.
- **Reachability:**
  - every room that is not secret is reachable from the entrance without secret passages, and secret rooms are reachable with them;
  - required features are reachable without secrets;
  - every locked door can be picked or forced, or its key lies in a container before it;
  - every secret way and hidden feature has a find DC.
- **Contents:**
  - features and members stand inside their rooms and share no cell, including stair cells;
  - a room with a Large creature has a passage at least two cells wide;
  - ceilings are at least 2.4 m;
  - hazards, traps, features and creatures are known to their catalogs.

## Results

`PlaceGenerationTests` generates 250 seeds for each of the 10 archetypes: 2,500 layouts with 0 validation failures (2026-11-11). Per archetype:

| Archetype | Avg rooms | Avg floors | Secret rooms | Groups | Traps | Containers | Locked doors |
|---|---|---|---|---|---|---|---|
| cave | 6.5 | 1.0 | 250 | 500 | 0 | 369 | 0 |
| mine | 8.0 | 2.0 | 0 | 500 | 248 | 362 | 115 |
| ruin | 6.5 | 1.0 | 250 | 250 | 250 | 613 | 50 |
| keep | 8.0 | 2.0 | 0 | 250 | 0 | 366 | 189 |
| crypt | 7.9 | 1.5 | 250 | 250 | 392 | 616 | 197 |
| catacomb | 15.5 | 2.4 | 250 | 250 | 369 | 581 | 171 |
| temple | 5.9 | 1.0 | 250 | 250 | 250 | 623 | 136 |
| bandit_hideout | 5.9 | 1.0 | 250 | 500 | 249 | 628 | 139 |
| underground | 12.9 | 2.4 | 250 | 250 | 369 | 639 | 295 |
| abandoned_settlement | 4.9 | 1.0 | 0 | 250 | 0 | 362 | 64 |

Interiors (`InteriorGenerator`) were also validated: 720 combinations of building function, footprint, storeys and wealth, with 0 failures. The authored watch fort is validated by the same validator.

## Saved, never regenerated

The layout is saved with the place (schema v15). A loaded place is never regenerated, so a generator update cannot move rooms under saved state. A new generator version applies only to places made afterwards.

## Limits

- Rooms are rectangles and corridors are straight. Caves are not irregular.
- There are no bridges or chasms (bridges are data-only).
- One staircase or ladder joins each pair of floors.
