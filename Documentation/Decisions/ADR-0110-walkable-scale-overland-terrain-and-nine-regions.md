# ADR-0110: A walkable scale of 40 m a cell, overland terrain rules, and nine regions in a new game

**Status.** Accepted, 2026-11-13 (Phase 23).
- It settles owner decision D1 (`Product/FutureRoadmap.md` §4), taken under the Phase 23 brief.
- It adds to ADR-0076 and ADR-0077 and leaves their travel scale unchanged. **Owner review is invited.**

**Context.**
- **No walkable country.** Between settlements there was only the strategic map and the abstract journey, at 1.2 km a cell. A region (128 × 128 cells) was 154 km a side, which is far too large to walk.
- **The travel scale is load-bearing.** Caravans, trade, journeys, groups, weather and quests all read the 1.2 km cell (`TravelRules.KmPerCell`).
- **The brief asks for:**
  - substantially larger walkable worlds, in measured terms;
  - real terrain (biomes, passability, crossings);
  - physical travel that advances the one authoritative clock;
  - no compromise of performance or stability.

**Decision.**
1. **Two scales over one geography.** The ground the player walks is the region's own `RegionMap`, projected at **40 m a cell** (`Domain/Geography/OverlandScale.MetresPerCell`).
   - A region is 5.12 km a side, 26.2 km².
   - Heights span 240 m (`VerticalMetres`), with mountain crags up to about 125 m more.
   - The travel scale is unchanged. **One walked metre stands for 30 m of road** (`OverlandScale.TravelKmPerMetre` = 1.2 km ÷ 40 m), so a walked road cell takes the same 15 game minutes as a journey's.
   - The map, the journey and the walker describe one geography.
2. **Why 40 m.**
   - At the existing run speed, crossing a region takes about 14 real minutes, and the nearest destination to a settlement is 1–3 minutes on foot.
   - At 100 m, the same content would sit 1–3 km apart in 164 km² regions: the empty map the brief rules out.
   - At 20 m, mountains and rivers lose their scale.
3. **Overland terrain (`OverlandTerrain`, engine-free and deterministic per region and seed).** It classifies each cell into one of 14 biomes: lake, river, shallows, shore, grassland, meadow, forest, dense forest, hills, rocky, mountain, marsh, valley and heath. `Desert` is in the enum but no generator uses it yet.
   - Each cell has passability, a walking cost, road and crossing flags, concealment and a sight bonus.
   - **Impassable:** mountains and deep water, unless a road crosses them (a pass); a river, except at a bridge (a road over it) or a ford.
   - **Fords:** placed every 12 cells along each river path (`FordSpacing`), so every river can be crossed off-road.
   - **Walking costs:**

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

   - Height comes from the cell corners, plus noise scaled by the biome's roughness, with water beds lowered. The same function serves the ground mesh, the colliders and the player's placement.
4. **Time.** Walking advances the clock through `AdvanceTimeCommand`, never on its own.
   - `WalkMinutes` gives the game minutes for a stretch: distance × 30 × the ground's cost at foot pace, doubled at night, as on the roads.
   - The living world already runs at 2 game minutes a real second. The controller charges only the surplus above that ambient time. Whole minutes are advanced; the fraction carries over.
   - Running on a road therefore matches the ambient pace, and rough ground costs more time.
5. **Nine regions.** A new game generates the whole 3 × 3 atlas (`WorldSession.NewGameSettings`), up from 6.
   - Walkable surface: 9 × 26.2 = **236 km²**.
   - With the ancient realm's prototype region (ADR-0111): **262 km²**.
   - Older saves keep their regions.

**Consequences.**
- **Geography is still regenerated from the seed, never saved.** `RegionGenerator.GenerateGeography` rebuilds a region's map without touching the world. The overland adds no save data beyond the player's position (schema v25, ADR-0111).
- **Density.** Outside the home region, a region first had about 7 destinations in 26 km². The Phase 23 follow-up (owner request, 2026-11-13) gives each further region the country's ordinary wild places once (`PlaceGenesis.EnsureCountryPlaces`): a hollow, a fallen steading, a barrow and old workings on the existing cave, ruin, crypt and mine archetypes. Each is named after the settlement it lies near (no lore invented), sited like every place, and found by walking. Older saves gain them once on load, the Phase 18 pattern. Roadside landmarks are still later work.
- **Weather on a taller atlas.** Phase 21's fronts are born on the west edge at a fixed rate and spread over the atlas's rows. With three rows instead of two, each region saw fewer of them (one region had 26 wet days a year). The follow-up scales the birth rate and the cap on fronts by the number of rows (`WeatherModel.StepDay`), which leaves two-row (six-region) worlds exactly as before. The Phase 21 year test keeps its floor of 30 wet days.
- **Concealment and the sight bonus are recorded, but nothing reads them yet.** They are for later stealth and scouting.

**Alternatives considered.**
- **A compressed travel scale (100–200 m a cell for everything).** Rejected: it re-derives every journey, caravan and trade time and changes saves, for no gain to the walker.
- **One seamless continent.** Rejected: it needs a floating origin and terrain streaming across region seams, against the brief's preference for reliability (ADR-0112).
- **A coarse outer zone only.** Rejected: the walkable area would stay small.
