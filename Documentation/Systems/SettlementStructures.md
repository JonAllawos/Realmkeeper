# Settlement structures (Phase 8)

A settlement's buildings, as world data. Definitions are code data (`Domain/World/Structures.cs`:
`StructureCatalog`). Instances are saved (`WorldData.Structures`) and created by `StructureGenesis`
(`Simulation/Commerce`). The decision behind them is ADR-0035.

## Model

- **`StructureDefinition`:**
  - id, name and description;
  - category (Trade, Food, Hospitality, Civic, Military, Worship, Storage, Agriculture, Transport,
    Utility, Housing);
  - role (Functional, Informational, Decorative);
  - **what it takes part in** (shown to the player);
  - the settlement sizes it appears in;
  - the location kind it is built on (functional only);
  - a sign word.
- **`StructureData`:**
  - id, definition, name and settlement;
  - the location it is, and the shop it houses (if any);
  - owner (NPC) and faction (civic and military buildings);
  - its site in the local layout;
  - condition and establishment date.
  It is owned by its settlement and validated: unknown definitions and settlements, foreign
  locations, two structures on one place, mismatched shops and unknown owners are all rejected.

## The catalog (19 kinds)

| Role | Kinds | What they do |
|---|---|---|
| Functional | Home, Shopfront, Smithy, Granary, Tavern, Market, Square, Keep (hall of governance), Shrine, Guard post | Each *is* an existing place: residents sleep, work and gather there by their routines, and the shopfront, smithy, granary and tavern house the Phase 5 shops |
| Informational | Bakery, Butcher, Carpenter's workshop, Mason's yard, Stable, Warehouse, Barn | Named, described and listed, with "nothing yet": no production, storage or trade is simulated for them |
| Decorative | Well, Market stalls | Scenery |

## Distribution

- **Functional:** one per existing location, of every kind. Locations come from `NpcGenesis` by
  settlement size, so the functional set follows size too.
- **Informational and decorative:** once each, where the size fits.

| Size | Informational and decorative structures |
|---|---|
| Hamlet | barn, well |
| Village | bakery, stable, barn, well, market stalls |
| Town, city, fortress | bakery, butcher, carpenter, mason, stable, warehouse, well, market stalls (no barn) |

## Preservation

- **Existing shops** are housed by the structure on their location. The structure takes the shop's
  name, and its owner is the shopkeeper. Nothing about the shop changes.
- **Homes** are owned by their first living resident, and workplaces by the first living person
  working there (by ID).
- **Generation is deterministic.** IDs are hashed from the world seed, the settlement, and the
  location or definition.
- **It runs once per settlement:**
  - when a world is generated (`WorldPopulationInitializer.EnsureCommerce`);
  - when a save older than v10 is loaded (`JsonWorldSerializer`). A v9 save gains exactly the
    structures a new world of the same seed would have.

## Presentation

- **Strategic map.** The settlement inspector has a Structures section:
  - a summary line, then one line per structure;
  - each line gives the name, kind, role and owner, with the tooltip giving the description, what
    it takes part in, the shop and the condition;
  - informational and decorative lines are dimmed.
- **In the street.** A placeholder sign above the door of each building that is a non-home
  structure, naming it. Informational and decorative structures without a site of their own appear
  in the inspector only; no building is invented for them.

## Deferred

- **Economy and building:** production for informational structures, construction, upkeep, damage
  and repair.
- **Ownership:** changes of ownership over time.
- **Presentation:** structures as distinct models, and procedural layouts.

## Phase 10: structures on a planned settlement

Structures keep their layout keys (`hall`, `granary`, `landmark`, `house-n`). Since Phase 10 those keys name
buildings in each settlement's persisted plan (ADR-0047), so every structure, sign and shop stands on its plan
building, on its floor and plinth. A structure's sign hangs above its building's door at the building's floor
height. The plan's other buildings (homes, barns, mills, warehouses, sheds) are part of the settlement's
look. They are not structures with economic effects.
