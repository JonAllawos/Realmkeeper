# ADR-0076: The connected world is made by the one region generator, region by region, with coherent borders

**Context.** Phase 16 asks for several regions, each with its own identity, its neighbours and believable borders. The world had one region, made by `RegionGenerator`, which is terrain-first, deterministic and validated. A second generator would duplicate it and drift from it. Saves from Phase 15 and earlier hold one region and must keep every settlement and person.

**Decision.**
1. **Six regions on an atlas grid.** Each region has a cell on a 3 × 2 grid (`AtlasX`, `AtlasZ`). The home region is (0, 0).
   - `WorldSettings.Regions` gives the count.
   - `WorldSession.NewGameSettings()` asks for six. The runtime uses it for a new game.
   - `DefaultSettings()` stays at one, so tests and old worlds are unchanged.
2. **One generator, a derived seed per region.** `WorldAtlasGenesis` makes each further region with the same `RegionGenerator`. Its seed is `SeedMixer.Combine(FromWorldSeed(seed), "region:" + i)`, with a retry suffix if validation refuses a layout. The generator runs with `RegionOnly`, which skips the authored quests and places that belong to the home region.
3. **Coherent borders.** The generator takes a `MountainDirection` hint from the regions a new region borders:
   - a range that reaches the shared edge continues across it;
   - a lowland edge stays lowland;
   - otherwise the range runs parallel to the border.

   A crossing's kind comes from the terrain on both sides of the edge:

   | Kind | Cost |
   |---|---|
   | Pass | 200 |
   | Bridge | 110 |
   | Trail | 130 |
   | Road | 100 |
4. **Merging.** The sub-world's settlements, people, factions, economy, layouts and wildlife are merged into the one world.
   - Its player, chronicle and authored content are not merged.
   - Road-band population IDs are re-keyed per region (`@road:<regionId>:`).
   - Settlement names are made unique.
   - Factions of bordering regions start wary of each other: Trust 35, Opinion −10, Rivalry 25.
5. **Positions stay region-local.** `RegionMap` carries its `RegionId` and `MountainDirection`. The session keeps one map per region (`MapFor`), and `Session.Map` is the map of the player's region. A change of region (`RegionChanged`) rebuilds the 3D view.
6. **Old saves.** A world keeps its region count. `WorldSession.DiscoverLandsBeyond(6)` adds the other regions once, deterministically, from the world's own seed. No existing settlement or person is regenerated.
7. **The opening town** (`QuestGenesis.FirstTown`) is chosen in the home region, so the authored opening quests stay at home.

**Consequences.**
- Every region has the generator's guarantees: a river, roads, an economy, people, politics, layouts and wildlife, all validated.
- A six-region world takes about 0.6 s to generate (measured in .NET).
- The same seed gives the same world. This is tested, including through a save and load.
- The validator checks the atlas (`WorldValidator.Atlas`):
  - unique cells;
  - real, mutual neighbours;
  - each settlement in a known region;
  - every region reachable.
- No new world canon: region names, kinds and landmarks come from the generator's existing name and terrain rules.

**Alternatives considered.**
- **One large map.** Rejected: it would change every map-taking routine, the terrain resolution and the save size, and break every existing world.
- **A lightweight "far region" generator.** Rejected: it would be a second generator with weaker guarantees, which the brief forbids.
