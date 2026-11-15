# Phase 27: buildings, doors, interiors and roads

## Doors (owner report: paths ended beside the door; doors smaller than their frames)

**Where the door is.**
- **The cause.** `BuildingKitPlanner` chose the front door's module slot at random (the centre slot or the next). `LocalPaths.DoorwayX` returned 0 (the front's centre) for every building but shops. With an even number of modules, the centre slot itself is half a module off centre.
- **The fix.**
  - The door is always the centre-most slot. The random draw is still made, so windows, shutters and roofs do not change.
  - `BuildingKitPlanner.DoorX` gives the door's position for every kind: houses and stores at their slot, the tavern centred, towers at their first slot.
  - `LocalPaths.DoorwayX` and `DoorOutside` use it. Residents' routes, place entrances and exits, the local map's door marks and the dressing's door clearances all use the same point.
- **Tested:** 1,242 doors across 27 settlements. The kit's door piece is within 5 cm of where paths lead, and the point outside is square in front of it.

**The leaf.**
- **Measured in the running game:** the door frame's opening is 1.29 m (−0.65 to 0.64 from the wall module's origin). The leaf model is 1.12 m wide (−0.05 to 1.07 from its hinge), hung at −0.53, so it covered −0.58 to 0.54.
- **The fix:** it is drawn 1.17× wide from −0.60 (`DoorLeafAlong`, `DoorLeafWidth`) and now spans −0.65 to 0.65.
- **Tested** in PlayMode against the measured opening, within 3 cm.
- **Height** could not be measured reliably by rays and is unchanged (the flat leaf is 2.10 m, the round 2.32 m). **Unverified** against the frame.

## Interiors (owner report: very different buildings had nearly identical interiors)

**Cause.**
- Cells of 1.5 m were clamped to 5–14 a side: every building at least 7.5 m, even a 6 m cottage, and at most 21 m.
- Each room got one or two furnishings whatever its size, from few definitions.

**Changes** (`InteriorGenerator`):

| | Before | After |
|---|---|---|
| Size | 5–14 cells, rounded | The building's own cells (floor of size ÷ 1.5 m), 4–20: never larger than the building from 6 m up, up to 30 m |
| Back rooms | Could be 1 cell deep | At least 2 cells deep (a 1-cell room failed validation: found by the new test) |
| Large houses | Main room, bedroom, larder | From 15 m wide: a second bedroom |
| Furnishing | One or two pieces per room | About one piece per 10 cells (22 m²) per room, by purpose (below), on top of the plan's own pieces |
| Furniture definitions | Bed, chest, crate, desk, forge, workbench, hearth | Also table, long table, chair, bench, stool, shelf, shelves, cabinet, bookcase, rug, barrel, sacks (the owned kits' models) |

**Furniture by room:**

| Room | Pieces |
|---|---|
| Main room | Table, chair, bench, shelf, rug, barrel |
| Bedroom | Cabinet, rug, stool |
| Store | Barrels, sacks, shelves |
| Workshop | Shelf, barrel, workbench, sacks |
| Office | Bookcase, chair, cabinet |
| Hall | Long table, benches, chair, bookcase |
| Guard room | Table, benches, barrel |
| Shrine | Benches |

**Measured** (`Phase27SettlementLifeTests`):

| Building | Footprint | Ground floor | Rooms | Furnishings |
|---|---|---|---|---|
| Cottage | 6×6 m | 6.0×6.0 m | 2 | 2 |
| Home | 9×8 m | 9.0×7.5 m | 2 | 4 |
| Large home | 14×11 m | 13.5×10.5 m | 3 | 8 |
| Smithy | 10×9 m | 9.0×9.0 m | 3 | 4 |
| Warehouse | 18×12 m | 18.0×12.0 m | 2 | 9 |
| Civic | 24×16 m | 24.0×15.0 m | 2 | 17 |

**Unchanged:**
- **Persistence.** An interior is generated once on first entry and saved; it is never regenerated, so interiors already visited in a save keep their old layout, and only buildings not yet entered use the new generator. Deterministic by the building's seed.
- **The tavern and shops** stay open to the street (their own interiors, as before). The tavern sign is now the pub sign.

## Roads (owner report: sharp angles at ends and on hills)

- **Corners rounded:**
  - each corner cut back by at most 2.5 m (a quarter of the shorter leg), three passes;
  - rows every metre along the rounded line, still draped on the walked ground (3 cm);
  - the road stays within its planned corridor.
- **Ends rounded:** a half-disc cap draped on the ground; none on bridge decks.
- **Hills.** The road follows the walked surface exactly (Phase 26.1: every vertex 3 cm above the collider, verified again this phase). On a slope, the surface is the terrain's own 2 m triangles. Any crease the owner sees on a hill is the ground's own; the road no longer adds square ends or sharp corners to it. Smoothing the terrain itself was not done (it would change the ground everyone walks on).
- **Bridges:** unchanged (reported good). Where a deck meets the bank the deck stands up to 92 cm above the ground beneath it (Phase 26.1); the transition was not inspected by eye.
