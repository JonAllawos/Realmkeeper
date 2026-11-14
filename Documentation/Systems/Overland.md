# The overland: walkable country, realms and transitions (Phase 23)

**Decisions.** ADR-0110 (scale and terrain), ADR-0111 (realms, gateways, location, save v25), ADR-0112 (transitions and streaming).

## Scale

| Measure | Value |
|---|---|
| Walking scale | 40 m a map cell (`OverlandScale.MetresPerCell`) |
| A region | 128 × 128 cells: 5.12 km a side, 26.2 km² |
| A new game | 9 surface regions (3 × 3 atlas): 236 km² |
| The ancient realm | one prototype region, geography only: 26.2 km² |
| Travel scale | unchanged at 1.2 km a cell; one walked metre stands for 30 m of road |
| A road cell walked | 15 game minutes, as on a journey |

## Terrain

`Domain/Geography/OverlandTerrain` (engine-free, deterministic from the region's map, kind and seed) gives each cell:
- a biome;
- passability and a walking cost;
- road and crossing flags;
- concealment and a sight bonus, recorded for later stealth and scouting (nothing reads them yet).

**Biomes:** lake, river, shallows, shore, grassland, meadow, forest, dense forest, hills, rocky, mountain, marsh, valley and heath. Desert is reserved in the enum; no generator uses it yet.

**What blocks the way:**
- Mountains and deep water bar the way, unless a road crosses them (a pass).
- Rivers bar the way except at bridges (a road over the river) and fords. Fords are placed every 12 cells along each river.

**Walking costs:**

| Ground | Cost |
|---|---|
| Road | 1.0 |
| Grassland | 1.25 |
| Heath | 1.35 |
| Hills | 1.5 |
| Forest | 1.6 |
| Rocky | 1.9 |
| Dense forest | 2.0 |
| Marsh | 2.1–2.2 |
| Shallows | 2.4 |
| A ford | 2.6 |

`HeightAt` serves the ground mesh, the colliders and the player's placement alike.

## Time

Walking advances the one clock through `AdvanceTimeCommand`.
- `WalkMinutes` is distance × 30 × the ground's cost at foot pace, doubled at night.
- The living world already runs 2 game minutes a real second, so the controller charges only the surplus.
- Running on a road matches the ambient pace; rough ground costs more.

## Sites and discovery

In the country (`Application/Overland/OverlandQueries`):

| Site | Where it stands | Known when |
|---|---|---|
| Settlement | At its gate: a road cell 6–8 cells from its centre | Its region is known |
| Place | At its approach | Discovered |
| Gateway | At its approach | Discovered |
| Way on to a neighbouring region | On the region's edge, toward the neighbour, nearest the crossing's settlement | Always |

- Walking within 140 m of a place or gateway discovers it.
- Being in a region marks it visited.
- A site is used within 30 m.

## Commands

All are validated, and a refusal leaves the world unchanged.

| Command | Does |
|---|---|
| `EnterOverlandCommand` | Goes out into the country |
| `LeaveOverlandCommand` | Leaves the country |
| `WalkOverlandCommand` | Records the player's position; refuses impassable ground |
| `EnterSettlementFromOverlandCommand` | Enters a settlement from its gate |
| `CrossRegionCommand` | Crosses to a neighbouring region, only along a passable crossing route |
| `UseGatewayCommand` | Passes through a gateway, only when it is open and within reach |
| `SetGatewayStateCommand` | Sets a gateway's state; the campaign's hook |

A place entered from the country returns the player there when they leave it.

## Presentation

- **`OverlandWorld`:**
  - a coarse ground mesh for the horizon;
  - detailed chunks of 16 × 16 cells, with colliders, within 1000 m;
  - instanced trees and rocks within 650 m;
  - barrier colliders along impassable edges;
  - water, and roads 8 m wide.
- **`OverlandSiteView`:**
  - a settlement's silhouette from its real layout, with a banner at the way in;
  - a place entrance;
  - a gateway ring of standing stones, lit when open;
  - a signpost for a way on.
- **HUD:**
  - a compass of known sites;
  - a location line (region · ground · nearest known site);
  - the region map (N).
- **Entry points:**
  - "Out to the country (O)" from a settlement;
  - Return to World Map (M) leaves.
- **`LoadingScreen`:** covers every transition. A refusal shows its reason and leaves the player where they were.

## Follow-up (owner request, 2026-11-13)

- **Look.**
  - Textured ground: the owned ambientCG grass and ground, and the village kit's rock, blended by biome and slope.
  - Grass, ferns and flowers from the owned nature kit near the player.
  - Settlements from their own kit buildings.
- **Collisions.** Trunks and rocks are solid near the player (pooled colliders within 45 m).
- **From the map.** "Walk the country" on the map's settlement inspector.
- **HUD.** The location panel shows region · in the country · ground. The settlement-edge practice fights and outings are hidden in the country.
- **More to find.** Each further region gains a hollow, a fallen steading, a barrow and old workings, once, named after the settlement they lie near.
- **Weather.** Fronts scale with the atlas's rows, so nine-region worlds are not drier than six-region ones.

## Known limits (Phase 23)

- Roadside landmarks (waystones, cairns, camps) are later work. The further regions now have four ordinary wild places each besides their settlements and quest places.
- Settlements beyond 1.4 km are block shapes under the haze; nearer ones are built from the village kit (33–157 ms each, one at a time).
- Entering from the map is by a settlement of the player's region ("Walk the country"); not from mid-journey.
- No mounts, fast travel, overland encounters or hazards.
- Weather does not yet change the ground (mud, snow depth, flooded fords).
- The ancient realm's content is undecided (Campaign Bible); its region is geography only.
