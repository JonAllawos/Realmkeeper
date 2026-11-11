# Settlement generation (Phase 10)

Each settlement has a persisted **identity** and a **plan**. The plan is made once, saved in full, and is the only
source for:
- the walkable settlement;
- the strategic map's settlement mesh;
- the inspector's description.

Decisions: ADR-0047 (identity and terrain-first plan), ADR-0048 (persistence, versions, migration), ADR-0049
(navigation).

Code: `Assets/Realmkeeper/Runtime/Domain/Settlements/`:
- `SettlementLayout.cs`: the model;
- `SettlementTerrain.cs`: the ground;
- `SettlementLayoutGenerator.cs`;
- `SettlementLayoutValidator.cs`.

## 1. Identity

`SettlementLayoutGenerator.Identify(world, map, settlement)` reads what the world already says. It does not roll dice.

| Field | From |
|---|---|
| Archetype | Size, water, walls, the landmark's ground and the economy. Values: market town, village green, river crossing, hillside, fortified, roadside, farmstead. |
| Primary and secondary economy | The settlement's specialization and its largest production assignments. |
| Water | The region map: a river through or beside the settlement, a stream for larger places, otherwise a well. |
| Terrain | The local relief on the region map (flat, rolling, hilly). |
| Wealth (0–100) | Prosperity, plus treasury in coin (capped at 25). |
| Security (0–100) | Guards × 14, half of stability, +20 for a town. |
| Agriculture (0–100) | Food workers' share of all work. |
| Architecture and growth | Wealth and region (stone and slate, plastered timber, timber and thatch …), and age. |
| Summary | One sentence, e.g. "A walled market town: farming and trade, beside a stream, rolling ground, built in stone and slate, with plastered timber houses." |

## 2. The plan, in order

Each step can only use what earlier steps made. Later steps are checked against earlier ones: a lane does not cross
the hall, a farm track does not cut through a house.

| Step | What it does |
|---|---|
| Exits | The bearings of the real roads to the neighbouring settlements, read off the region map. Roads that share their first miles merge into one way out. A single exit gets a track out the other side. |
| Ground | Regional fall, from the map's gradient (≤ 5 m per 100 m). The high ground lies away from the roads, with a hill whose flat top is clear of the square's levelling. Countryside rises add relief, plus rolling noise. The square is level. |
| Water | A river keeps the region's course, offset square to its direction on the side away from the high ground. A town's water runs outside its ring street. The bed and banks are cut into the ground. |
| Main roads | From the square's edge to the plan's edge, toward each exit. Each step picks the heading with the least climb (within 40°), crosses water straight on, and goes round what is already built. Road ends never lie in water. |
| Districts | Town: market, keep hill, smiths' row, millside, the lanes, high street, shrine close, fields, outskirts. Village: green, chapel rise, roadside, back lane, forge end, brookside, farms. Hamlet: green, watch rise, riverside, farmsteads. |
| Anchors | The hall (tavern) faces the square from its quietest side: away from the roads, the water and the high ground. The landmark stands on the hill's top. The mill stands on either bank, facing the water. These are placed before any street, so no street runs through them. |
| Streets | Town: a ring street, and a street from it into each district not already served. Village and hamlet: lanes from the green, which step aside if the hall is in the way, and the back lane. |
| District lanes | 1–3 lanes across each district at different angles, following the land. A lane that would cut a building is dropped. A lane that meets nothing is linked to the nearest street. |
| Walls | A town's wall circuit at its core radius. Gates are where the main roads cross it. |
| Sites | The world's places keep their keys: the granary by the working side; houses, shops, the smithy and the shrine each in the district its use belongs to. |
| Infill | Each district is filled to its density with what such a district holds. Plots are tried every few metres along each street, on both sides, facing the street, and must pass these checks: no road, water, wall or square; no overlap with another footprint (exact rectangle test); ground level enough for the plinth. |
| Farms | Farmsteads (farmhouse, barn, shed and yard) between the core and the edge. A track runs to the nearest road; if it would cut a building, the farm is not built there. Crop, fallow, pasture and orchard fields sit on land fit for them. |
| Gardens | Behind about half the homes outside the market. |
| Landmarks and features | The landmark, the mill, the largest bridge, the gate, the market cross or the great oak. Also the well, stalls, trees and lamps. |
| Arrival | On the first main road, just inside the plan's edge, facing in. |

Bridges are not decided while routing. Each finished road is walked a metre at a time. Every wet stretch is
bridged from the last dry point before it to the first dry point after it. A farm track over a stream is a ford.

## 3. What the validator requires

`SettlementLayoutValidator.Check(plan, type)` returns each problem in words. A plan with any problem refuses
the load (`WorldValidator`):

- **Sites:**
  - every required site is present: hall, granary, landmark and every `house-n`;
  - no key appears twice;
  - the square is dry.
- **Roads:**
  - roads form one network (ends meeting, or crossing);
  - every stretch over water is bridged;
  - each bridge lands on dry ground at both ends and meets its road.
- **Buildings:**
  - none in water (except the mill and gatehouses), on a road or overlapping another;
  - plinth no deeper than 3.3 m.
- **Fields:** none in water; a ploughed field falls no more than 4 m across its corners.
- **References:** districts, landmarks and herds' pastures name things that exist.

## 4. Results

The sweep covered 40 world seeds × 3 settlements (the shipped generator settings, .NET 8):

| Type | Plans | Buildings min / mean / max | Validator errors |
|---|---|---|---|
| Town | 40 | 54 / 70 / 84 | 0 |
| Village | 40 | 40 / 53 / 61 | 0 |
| Hamlet | 40 | 15 / 23 / 27 | 0 |

- **Slowest plan:** 86 ms (a town). Plans are made once per settlement.
- **Reference world** (seed 123456789):
  - Bramhaven: walled town, 59 buildings.
  - Mossholm: village, 58.
  - Westerthorpe: hamlet on a river, 24.
  - Every site is present, and routes run from the way in to every site door.

## 5. Readers of the plan

- **Walkable settlement:** `LocalEnvironmentGenerator.FromPlan` builds it.
  - Streets are the road segments.
  - Buildings keep their keys, floors, plinths, materials and roofs.
  - Features become props.
  - The tavern's furniture sits in the hall's own frame, and the granary's ledger is at its door.
  - The notice board stands at the square's edge, toward the way in.
  - Herds are placed in their pastures.
- **Scene:** `ModularEnvironmentBuilder.Plan` draws:
  - terrain mesh chunks (2 m grid) with mesh colliders;
  - roads draped on the ground, carried level over bridge decks;
  - water ribbons at the surface;
  - bridges with decks, parapets and piers, or fords of stepping stones;
  - fields with rows, orchards with trees, and fences with a gate gap (one collider per side);
  - herds as placeholder animals in proportion;
  - wall runs with merlons (gaps at gates and where a lane passes), gatehouses as two towers and an arch;
  - plinths and steps under buildings;
  - verges.
- **Strategic map:** `SettlementBuilder.Build(map, settlement, plan)` draws the same buildings, at map scale and
  facing as they face, plus the landmark, the wall and the fields.
- **Inspector:** shows the identity, the districts, the landmarks and the building and field counts.

## 6. Determinism and persistence

- **Seed:** `SeedMixer.Combine(world seed, "settlement-layout-v2:" + settlement ID)`. Its own stream; no
  simulation stream is touched.
- **Inputs:** the plan depends on world state (economy, people, locations). Two worlds in the same state get the
  same plan, so the same seed gives byte-identical plans.
- **Upgraded saves:** a v11 save gains its plans at upgrade, from its state then. They then never change
  (ADR-0048).
- **Edits:** a plan edited in the save loads as edited if it validates. A corrupt plan refuses the load.
