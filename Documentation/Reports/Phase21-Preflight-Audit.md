# Phase 21: Preflight audit (environment, living settlements, shops, workshops, interiors)

**Date:** 2026-11-12.
- **Starting HEAD:** `c10a313` on `feature/phase-0-1-foundation`. That is Phase 20A (`77240e7`) plus a documentation commit, both pushed at the owner's instruction before Phase 21 began.
- **Working tree:** clean except Unity's `InitTestScene*` and the Phase 21 brief copied to `Documentation/Briefs/Phase21-Brief.md`.
- **Push rule:** Phase 21 commits locally only.

**Evidence:**
- reading the code;
- the item catalog dumped from the built assemblies (126 items);
- a walkthrough capture in Unity (`Phase21WalkthroughCaptureTests`, prefix `before`, 24 renders of a generated town: overview, street, both shop insides, the tavern, seven entered interiors, dusk and night). The renders are kept with the Phase 21 screenshots as the "before" set.

## 1. World and geography

| Area | Exists | Notes |
|---|---|---|
| Regions | Six regions per world (Phase 16). Each has a kind (Vale, Highlands, Woodland, Lakeland, Lowlands), an atlas position, neighbours, its own seed and the side its mountains lie on | Enough to derive a regional climate without new geography |
| Terrain | Per-region `RegionMap` (height, water, rivers), local `SettlementTerrain`, roads and bridges | Not changed in this phase beyond weather and season presentation |
| Climate, biome, weather, season | **None.** `EconomyRules.SeasonalModifierPercent` is a hook that returns 100 for every date | New domain model needed |
| Calendar | 12 months of 30 days, named (Deepwinter … Longnight); worlds start on 1 Seedtide 412 | Seasons can be mapped onto the months without changing dates |
| Time of day | `ExplorationAtmosphere(.DayNight)`: fixed sunrise 06:15, sunset 19:30; sun arc, golden hour, moon light, lamps on at dusk, post-processing | Reused and extended. **Observed:** the night sky renders flat black; there is a heavy haze from altitude |
| Daily simulation | `SimulationScheduler` with ordered `ISimulationSystem`s (labour 100 … chronicle); distant regions are on the same daily tick with lower-frequency subsystems | A weather system slots in as one more daily system |

## 2. Settlements and buildings

| Area | Exists | Notes |
|---|---|---|
| Layout | `SettlementLayoutData` plans; a town of 71 buildings (21 homes, 6 large homes, 6 cottages, 3 farmhouses, 5 shops, 1 smithy, 3 workshops, 1 tavern, 1 chapel, 1 guard post …) | Plans persist and are never regenerated |
| Shop buildings | A shop is tied to a building through its `LocationData.LayoutKey`; `ShopFit` is only **General** or **Smithy** | In the walkthrough town **three of five shop buildings had no shop** |
| Shop insides | `ShopInteriorView` draws the counter, shelves, rack, forge and anvil as **11 untextured boxes**; wares as code-built shapes (Phase 20A art models for 27 items) | The weakest visual area; owned props unused |
| Tavern | The hall is open to the street, with code-built tables, benches, a bar and a hearth | Reasonable but sparse |
| Entered interiors | `InteriorGenerator` (Phase 13): rooms with doorways, stairs, cellars for the wealthy, a few functional features (bed, hearth, forge, desk, altar, chests, crates). Saved per building as `PlaceData` and never re-rolled | Architecture is right. **Furnishing is almost absent:** a room is a crate and a wall torch; a door is a flat dark slab. Shops and taverns have no entered plan. |
| Doors, locks, containers | Closed and locked doors with lock DCs, night locking, owned containers with theft rules (Phase 14) | Reused |

## 3. Economy, shops and crafting

| Area | Exists | Notes |
|---|---|---|
| Items | 126 definitions; unique stacks with condition, quality, charges, curses and provenance (Phase 11) | Reused; a few new goods are needed for leather, tailoring and herbs |
| Shop stock | Fungible stock lines plus unique goods; buying and selling move real stacks; the till is the keeper's own wealth | Already one inventory system: nothing new to invent |
| Restock | Daily: an item is made from the settlement's stores (iron, timber, stone, food), or, when it lists no materials, **imported** for coin at a cost the keeper pays. Food is never taken during a shortage. | Sound. Gaps: imports ignore trade connections; no record of where stock came from |
| Shop kinds | Four: general/outfitter, smithy, tavern kitchen, granary yard (a farm stall in hamlets) | Too narrow for the brief; the categories are coarse |
| Recipes | 11 (Phase 11): smelt iron, forge dagger, arrows, cure hide, leather jerkin, sling, quarterstaff, cloak, healing potion, stew, smoked fish | All valid |
| Crafting jobs | Start, held materials, ability check with tool proficiency, wait, collect, cancel (materials returned); saved | Reused. **Station** is only a place kind ("a workshop"), not a physical forge or bench |
| NPC production | None. Smithies "restock" finished weapons straight from iron | To become work at stations using real inputs |

## 4. NPCs

| Area | Exists | Notes |
|---|---|---|
| Hourly plan | `NpcActivityPlan.At`: a pure function giving where a person is and why (sleep, meals, work, rest days, evening tavern, market for the curious, shortage and unrest responses) | Ideal place for weather responses and shop visits |
| Presentation | `ResidentView` places people at their location in the layout, with poses (work, pray, chat) and Phase 18 overheard conversations | Reused |
| Simulation LOD | Distant settlements run cheaper subsystems | Unchanged |

## 5. Persistence and architecture
- **Schema:** v22 (Phase 20A), with an append-only migration chain from v1 tested.
- **Identity and transactions:** stable GUID IDs; the deterministic seed mixer; transactional commands with a committed snapshot and an edit copy; the chronicle and event log.
- **Assets:** imported through `ArtImportPostprocessor` and catalogued by `ArtSetup` and `CharacterAnimationBuilder`. Store-licensed and QAL packs are local only (`.gitignore`).

## 6. Decisions for this phase (reuse, extend, new)

| Need | Decision |
|---|---|
| Weather and seasons | **New domain model.** Seasons from the existing months. Regional climate derived from the region kind, atlas latitude and mountains. Weather fronts that move across the atlas, so neighbours share weather with a lag. Eight three-hour periods per region per day, computed by a daily system from the seed, the date and yesterday's state. Saved in v23. |
| Effects | Through existing systems only: the seasonal production hook (farms and fisheries), road travel time (heavy rain, snow, storm), NPC routines (shelter, fewer evening outings). No survival or temperature system is invented. |
| Visual weather and light | Extend `ExplorationAtmosphere` with seasonal daylight, clouds, rain, snow, fog, lightning, wet and snowy ground, and a starry night. Built in Unity (owner decision); no weather asset. |
| Shops | Extend `ShopData` with a **kind** from a data catalog. New kinds go into unused shop buildings, kept by existing residents; nothing existing is replaced. A supply log records initial stock, local making, imports (only along a working trade connection), production and sales. |
| Displays | Rebuild `ShopInteriorView` per kind from owned CC0 props. Display slots are filled from real stock, and an empty slot stays empty. |
| Stations and recipes | Physical stations (forge, anvil, workbench, leather bench, sewing table, oven, hearth, herb bench) as interactables. Recipes name a station kind; the existing job system runs them. About a dozen new recipes, each with real inputs. |
| NPC production | A daily step: a craftsperson at work turns their shop's input stock into goods through the same recipes, limited by hours and inputs |
| Interiors | A data-driven furnishing planner (Domain, deterministic, validated: inside the room, clear of doorways, stairs and features, no overlaps). Presentation-only dressing, not saved, because it is not mutable. Rooms lit by their own hearths and candles. |
| Not attempted | Natural points of interest beyond what Phase 16 and 18 places provide; terrain reshaping. Recorded in the completion report with the reason. |
