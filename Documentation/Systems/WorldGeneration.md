# World generation

Implementation: `Assets/Realmkeeper/Runtime/Infrastructure/Generation/RegionGenerator.cs` (version **1**).
Port: `Realmkeeper.Application.Ports.IWorldGenerator`.

## Output

`Generate(request)` returns `GeneratedWorld { WorldData World; RegionMap Map; }`:

- `RegionMap`: 128 × 128 cells, 2 world units per cell (a 256 × 256 unit region).
  Normalized elevation (0–1, scaled by `MaxElevation` = 36 units), a `TerrainType` per cell,
  one river centre line and three roads. Water surface at normalized height 0.32.
- `WorldData`: three settlements (Town, Village, Hamlet), two factions, a founding event,
  and the generator version and seed.

## Pipeline

Each stage draws from its own random stream, `SeedMixer.Combine(Mix(seed), "<stage>")`, so
changing one stage does not disturb the others.

| # | Stage | What it does |
|---|---|---|
| 1 | Layout | Picks one of 8 compass directions for the mountain side, plus a lake centre and radius on the opposite side and a river source among the mountains. Uses a fixed direction table instead of `sin`/`cos` for cross-platform reproducibility. |
| 2 | Elevation | `0.37 + 0.2·(fbm − 0.5) + 0.07·(hills − 0.5) + mask·(0.16 + 0.44·ridged)`. The mask is a smoothstep across the region toward the mountain side. |
| 3 | Lake | Blends elevation toward a basin below water level, with a noise-wobbled shoreline. |
| 4 | River | A source-to-lake line bent by recursive midpoint displacement, rasterized to 4-connected cells with loops removed, and cut off where it enters the lake. The bed descends steadily; a valley is carved within 3 cells. |
| 5 | Classify | River > Deep (< wl − 0.06) > Shallow (< wl) > Mountain (e > 0.68, or e > 0.58 and steep) > Hills (e > 0.5, or slope > 0.022) > Plains. Second pass: Shore next to open water; Forest where forest noise (biased toward the mountains) exceeds 0.575. |
| 6 | Settlements | Candidates are Plains cells 12+ from the edge, 3+ from water, and flat within a 5 × 5 area. Score = fertile land within 7 cells + nearness to water + small jitter. Sites are taken greedily with minimum spacing 34, relaxed to 26, 18 or 12 if needed. First pick is the Town, then the Village, then the Hamlet. |
| 7 | Flatten | Blends terrain to each site's height over radius + 4 (water excluded). |
| 8 | Reclassify | Repeats stage 5 on the flattened elevation, then clears each site to Plains. |
| 9 | World data | Deterministic IDs and names, starting stats, starting stock, and factions. |
| 10 | Roads | A* over the grid for all three settlement pairs. Costs: plains 1, shore 1.4, hills 2.2, forest 2.6, river (bridge) 10, mountain 14, plus 220 × slope; open water is impassable; existing road × 0.35, so routes merge. |
| 11 | Economy | `EconomyInitializer`: production capacity, specialization, storage, starting labour and a founding-day ledger from the terrain; two trade routes per road (stream `trade-routes`). The same code upgrades Phase 1 saves on load. |
| 12 | Validate | `WorldValidator` and `RegionMapValidator`. Failure throws `WorldGenerationException`. |

Terrain (stages 1–8, 10) is unchanged since Phase 1, so the generator version is still 1 and
existing seeds keep their geography.

## The connected world (Phase 16, ADR-0076)

A new game asks for six regions (`WorldSession.NewGameSettings()`; `WorldSettings.Regions`). The home region is
generated exactly as above. `WorldAtlasGenesis` then adds the others on a 3 × 2 atlas grid, each by the **same
generator**:

- **Seed:** `SeedMixer.Combine(FromWorldSeed(seed), "region:" + i)`. A retry suffix is added only if validation
  refuses a layout.
- **`RegionOnly`:** the authored quests and places stay in the home region.
- **`MountainDirection` (stage 1 forced):** the side is chosen from the region it borders. If the neighbour's range
  runs to the shared edge, the new region's range continues from that edge. If the edge is lowland there, it stays
  lowland. Otherwise the range runs parallel to the border.
- **Merged into the one world:**
  - road-band populations are re-keyed by region (`@road:<regionId>:`);
  - settlement names are made unique;
  - factions of bordering regions start wary of each other (trust 35, opinion −10, rivalry 25);
  - the sub-world's player, chronicle and authored content are dropped.
- **Description:** each region gets a kind (Vale, Highlands, Woodland, Lakeland, Lowlands) and landmarks, from its
  own terrain (lake, river, wide forest, high ground) and name stem. No new canon is written.
- **Route graph:** each region's map roads become route segments (terrain cost, and a bridge where a road crosses
  water). Neighbouring regions are joined by a crossing between their nearest settlements: a **pass** (cost 200)
  where mountains meet the edge, a **bridge** (110) over water, a **trail** (130) through forest, or a **road** (100).
  Each crossing also carries cross-region trade routes.

A region's map is never saved. It is rebuilt from that region's own seed, and the session keeps one per region. An
old world keeps its single region. `DiscoverLandsBeyond` adds the rest once, the same for the same seed.

Measured: a six-region world (18 settlements, about 120 people, 25 road segments) generates in about 0.6 s.

**Two scales.** The 3D overview draws a region at 2 units per cell. Travel measures the same cell as 1.2 km of
country, so a region is about 150 km across. Settlements are explored at their own local scale.

## Settlement economics

- Starting food reserves: 45–75 days of consumption. Timber, stone and iron start from nearby
  forest, hill and mountain cells, with at least 20 days of demand; all clamped to storage.
- Production capacity and specialization come from the land: see [Economy](Economy.md).
- Measured over 6 seeds for one year (Phase 2 balance runs): the default seed 123456789 stays
  stable (prosperity 67–70, stability 90+, about 21 recorded deliveries). One seed (42)
  produces a struggling town short of timber, stone and iron. No seed starves.

## Measured output (this build)

| Seed | Plains | Forest | Hills | Mountain | Water (all) | Time |
|---|---|---|---|---|---|---|
| 1 | 45.3% | 29.4% | 5.2% | 10.8% | 7.3% | 128 ms (first run, JIT) |
| 42 | 49.1% | 28.1% | 2.9% | 11.5% | 6.4% | 48 ms |
| 3 | 52.6% | 19.5% | 5.8% | 10.2% | 9.6% | 35 ms |
| 77 | 50.3% | 19.2% | 6.8% | 14.5% | 7.8% | 33 ms |

Measured with .NET 8 on a 4-core Linux container. Unity Mono timings will differ.

## Reproducibility

- Same seed + same generator version → the same `RegionMap`, bit for bit (elevation
  compared as raw float bits), and the same `WorldData`, except `WorldId` unless one is
  supplied. Covered by tests.
- Integer RNG; value noise from integer hashing; no `sin`, `cos`, `exp` or `pow`. Floats use
  only `+ − × ÷`, `floor`, `sqrt` and `round`, which IEEE-754 defines exactly. **Not
  verified across Mono, IL2CPP and CoreCLR.** See UntestedAreas.md.
- **Any change that alters output for an existing seed must bump `RegionGenerator.Version`.**
  The golden-value RNG test protects the random sequence; the generator itself has no golden
  snapshot yet.

## Manual editing

`IRegionMapEditor` (Domain) changes a `RegionMap` directly, records `TerrainEdit` entries and
raises `RegionChanged(RegionBounds)` so a renderer can rebuild only the affected area. The
generator knows nothing about edits. Persisting edits as changes on top of the regenerated
map is future work.
