# Canonical world architecture audit (Phase 30)

**Date:** 2026-11-16. **Status:** audit and recommendation for owner review; nothing implemented.
**Companion:** `SingleWorld-Transition.md` (what would change and in what order).

## Answer first

**The existing architecture can support one canonical, connected, persistent world without a destructive rewrite.**
- The world is already one `WorldData`: every region, settlement, person, faction, quest and event, with stable GUIDs, simulated by one scheduler and saved as one document (ADR-0009).
- What is missing is mostly **player-facing and data-level**:
  - New Game asks for a world name and seed and makes a new world each time.
  - There is no authored, fixed "world definition".
  - Geography is regenerated from code on every load, so any change to the generator moves the ground under existing saves.
- Four technical limits need changing before the world grows past its present 9 regions:
  - the atlas is capped at 9 regions;
  - every command deep-copies the whole world;
  - saves are indented JSON written on the main thread;
  - region borders are hard edges crossed only at fixed points.

## 1. What exists

| Concern | How it works today | Evidence |
|---|---|---|
| World identity | `WorldData` with `WorldId`, name, seed, generator version | `Domain/World/WorldData.cs` |
| Creation | `CreateWorldCommand(name, seed, settings)` → `RegionGenerator.Generate` → `WorldAtlasGenesis.EnsureAtlas` → 9 regions | `WorldSession.Execute(CreateWorldCommand)`, `NewGameSettings` |
| Region layout | Atlas grid of 3 columns, at most 9 regions; each further region from a seed derived from the world's | `WorldAtlasGenesis.Columns`, `MaxRegions` |
| Region size | 128 × 128 cells; 40 m a cell walked (5.12 km), 1.2 km a cell for travel | ADR-0110 |
| Settlements | One town, one village, one hamlet per region; cities and fortresses never placed | `RegionGenerator.SiteTypes` |
| Connectivity | Adjacent regions joined by one crossing (pass, bridge, trail or road) and trade routes; a persistent route graph | ADR-0076, ADR-0077 |
| Region transitions | Each region's country is its own environment; crossing is a loading transition at a crossing point | ADR-0112, `CrossRegionCommand` |
| Streaming | Within a region: coarse mesh or Unity Terrain, detail chunks within 1 km, trees within 650 m | ADR-0112, ADR-0122 |
| Geography persistence | Not saved; rebuilt from seed and generator version on load | ADR-0005 |
| Entity persistence | Settlements, people, factions, plans, places, containers, quests, crimes, events and weather are saved | `Systems/Persistence.md` (v27) |
| Offscreen simulation | Every system runs over every region daily; distant regions' people update weekly | ADR-0081 |
| Determinism | Per-day and per-system random streams; idempotent genesis steps (`Ensure…`) | `Architecture/SimulationLifecycle.md` |
| Time | One clock; simulation at 2 game minutes a real second; walking charges the surplus | ADR-0110 |
| Realms | Surface plus one ancient prototype region (geography only), joined by a dormant gateway | ADR-0111 |

## 2. Internal technology and player experience

| Internal technology (keep) | Player-facing experience (change) |
|---|---|
| Seeds, derived region seeds, per-system random streams | **One** world: the same geography, names, people and history every New Game |
| `RegionGenerator`, `WorldAtlasGenesis`, place and layout generators | No seed or world name to choose; "New world" becomes "New Game" |
| Idempotent `Ensure…` genesis on load | Additions in later builds appear in the same world without regenerating what exists |
| Chunk and terrain streaming, region cache | Travel across the whole world without the sense of entering separate instances |

Deterministic generation stays the tool used to **produce** the canonical world. What changes is that its output is fixed, versioned and shipped as the world, instead of being a fresh world at each New Game.

## 3. The required questions

### How large can the canonical world reasonably become?

Measured on a 9-region save (Phase 30 preflight §5), divided by region:

| Cost | Per region | Limit that binds | Regions at that limit (current code) |
|---|---|---|---|
| Save file (indented JSON) | ~0.98 MB | 32 MB load limit (`JsonWorldStore`) | ~32 |
| Load (deserialize) | ~176 ms | A 10 s load target (assumed) | ~55 |
| Save (serialize, main thread) | ~68 ms | A 250 ms autosave hitch (assumed) | ~3–4 (already exceeded at 9: 608 ms) |
| Deep copy per command | ~0.87 ms | 8 ms, half a 60 fps frame (assumed) | ~9 |

**Findings:**
- At 9 regions the world already meets the deep-copy budget and exceeds a comfortable autosave hitch.
- **With the current persistence and command model, 9–12 regions is the practical ceiling.**
- With the changes in `SingleWorld-Transition.md` (no whole-world copy per command, compact JSON written off the main thread), size is bound by load time and the daily simulation tick. Roughly 25–50 regions becomes plausible, about 650–1,300 km² of walkable country.
- **Not measured:** the daily simulation tick against region count. It must be measured before a size is chosen.
- **A size should not be picked from the engine's ceiling.** It should follow from the content plan: the regions the 40-hour campaign and its side content actually need (`Product/Campaign-Architecture-Assessment.md` §5). Empty regions cost save size, load time and simulation time.

### How should geography and regions be organised?

- Keep the **128-cell region as the unit** of generation, streaming, simulation tier and save partition. It already works and is tested.
- Lift the atlas from a fixed 3-column grid to an **authored macro-map**: which regions exist, their kinds (coast, highland, marsh…), the ranges and rivers that cross borders, and where the cities, fortresses, ports and campaign sites are.
- Generate the detail inside each region from that macro-map, as today.

### How do regions connect, and how are boundaries handled?

- **Today:** a hard edge, crossed only at the region's crossing point, behind a loading screen. Terrain is made coherent at borders only in which side of a range faces the neighbour.
- **Recommended:**
  1. Make borders continuous in data: shared edge heights, rivers and roads that leave one region at the cell where they enter the next.
  2. Allow crossing **anywhere passable** along the border, behind a short loading transition, as the brief for Phase 23 approved. This needs no seamless streaming.
  3. Seamless cross-border streaming (neighbouring regions loaded at once) is **post-release**. It costs memory and needs a floating origin, and the loading transition already meets ADR-0112.

### How does continuity survive unloading and reloading?

- It already does. Regions unload **presentation only**; all state lives in `WorldData`.
- The overland's own state (landmarks found, encounters, the player's pose) is saved (v25–v26).
- Phase 29's cache never serves old ground for a new or loaded world (ADR-0122).

### How are NPCs, settlements, factions, weather and events simulated outside loaded regions?

- **All of them are always simulated**, abstractly, in `WorldData`.
- Distant regions' people get a full update weekly instead of daily (ADR-0081).
- Weather fronts cross the whole atlas.
- This scales linearly with region count. A third tier (dormant regions: daily aggregates only) is the likely need beyond about 25 regions, to be decided by measurement.

### How are new regions added without inconsistency, and how are duplicates prevented?

- Every genesis step is idempotent and keyed (`EnsureAtlas`, `RealmGenesis.Ensure`, `PlaceGenesis.EnsureCountryPlaces`). IDs come from the world seed, so a migrated save and a new game agree. `DiscoverLandsBeyond` proved this by adding regions to old single-region saves.
- Entities are saved once generated and are never regenerated over saved state. Settlement plans carry their generator version, and Phase 29 showed a versioned in-place migration (ADR-0121).
- **The real risk is geography:** it is regenerated from code. A generator change can move rivers or heights under saved settlements, places and the player's overland pose. ADR-0005 refuses a save with an unsupported generator version, but nothing stops an unversioned code change from altering output.

### How are world-generation decisions persisted?

- **Entities:** saved.
- **Geography:** not saved (ADR-0005).
- For a canonical shipped world the recommendation is a **baked world definition**: geography and macro-layout generated once, reviewed, edited where authored changes are needed (`TerrainEdit` records exist for this), and shipped as versioned data. Saves record that version. Detailed in `SingleWorld-Transition.md` §2.

### How does the player cross the whole world without it feeling like separate instances?

1. One New Game, one world, always the same.
2. Borders continuous in terrain, rivers and roads, crossable wherever passable.
3. The strategic map and atlas show the whole known world as one map (they already show regions together, ADR-0081).
4. Loading transitions short and in-world (the loading screen shows the destination; ADR-0112).
5. Fast travel or transport between known places (owner decision D2), so distance stays a choice.

## 4. Recommended architecture (summary)

```
Canonical World Definition (versioned data, shipped)
  ├─ macro-map: regions, kinds, borders, ranges, rivers, roads between regions
  ├─ authored anchors: named settlements, sites, people and factions the story needs (fixed IDs)
  └─ baked geography per region (generated once from seeds, reviewed, edited)
        │  New Game
        ▼
WorldData (one save): every entity, simulated in tiers; generated detail filled in by idempotent Ensure… steps
        │  presentation streams one environment at a time (country, settlement, place)
        ▼
Unity: region country (Unity Terrain + chunks), settlements, places, loading transitions
```

## 5. Changes that are genuinely necessary

| # | Change | Why | Size |
|---|---|---|---|
| 1 | One canonical New Game (no seed or world name for players) | The core of the brief | Small (menu and command) |
| 2 | Baked, versioned world definition | Generator changes must not move ground under saves | Medium |
| 3 | Authored anchors with fixed IDs, and a campaign binding to them | A 40-hour authored story needs fixed people and places | Medium |
| 4 | Atlas beyond 9 regions, with cities and fortresses placed | Scale the content needs | Medium |
| 5 | No whole-world deep copy per command | Frame cost grows with the world (7.8 ms at 9 regions) | Medium to large |
| 6 | Compact save written off the main thread, with backups | Save hitch (608 ms) and reliability | Medium |
| 7 | Continuous borders, crossable wherever passable | Removes the "separate instance" feel | Medium |
| 8 | Measure, then perhaps add, a dormant simulation tier | Daily tick at target size (unmeasured) | Unknown until measured |

**Not necessary:** removing deterministic generation; replacing the region model; a new save format; a new simulation; seamless streaming across regions.
