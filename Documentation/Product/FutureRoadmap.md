# Realmkeeper future development roadmap (from Phase 21 onward)

**Status:** proposed plan, written 2026-11-12 at the owner's request ("Game Design Bible update: open-world overland travel, level 1 character progression and complete future development roadmap").

- **Nothing below Phase 21 in this document is implemented.**
- The phase numbers after 21 are proposals. The owner sets the final order and authorises each phase with its own brief (Bible §19 principle 20, §21 items 12 and 13).
- No dates are given.

**Related documents:**
- **The Game Design Bible** (`GameDesignBible.md`) remains the central source of product direction. Its "Owner design amendments (2026-11-12)" section states the requirements this roadmap serves; this document holds the phase-by-phase detail.
- **`../Roadmap.md`:** the history of completed milestones.

## 1. Where the project stands (inspected 2026-11-12)

### Repository

| Item | Value |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| HEAD | `c10a313` (Phase 20A, pushed) |
| Phase 21 | In progress and uncommitted. See §2. |

### What the code establishes

These facts shape the plan below.

| Area | What exists | Where |
|---|---|---|
| World scale | Six regions on a 3 × 2 atlas grid. Each region is a 128 × 128 cell `RegionMap`. Travel counts a cell as 1.2 km (`TravelRules.KmPerCell`), so a region is about 154 km a side and the world about 460 × 307 km on the travel scale. | ADR-0076, ADR-0077 |
| Coordinates | **Region-local.** The session keeps one map per region, and a change of region rebuilds the 3D view. ADR-0076 rejected "one large map" for Phase 16. | ADR-0076 §5 |
| Overland travel | **Abstract.** Journeys advance hour by hour along a persistent route graph (`RouteSegmentData`, Dijkstra plus Yen) on the atlas strip. There is no walking between settlements. Road fights are staged on the near settlement's ground. | `Systems/Travel.md`, ADR-0077, ADR-0078, ADR-0086 |
| Settlement scenes | A settlement is a persisted layout with a half-extent of 110, 150 or 200 m (`SettlementLayoutGenerator`), which is less than one map cell. Beyond its edge there is no continuing landscape. Wilderness places use their own local layouts. | `Systems/Exploration.md`, ADR-0047, ADR-0048 |
| Discovery | Places already have Unknown, Rumoured, Discovered, Visited, Explored and Cleared (ADR-0065). Regions have visited, known and uncharted. | ADR-0065, ADR-0076 |
| Character creation | `CreateCharacterCommand` builds the sheet at **level 1** with 0 new experience, full HP and resources, and the class and background kit once. It is refused once the character exists. | `Application/CharacterCommands.cs` |
| Advancement | A keyed experience ledger and explicit `AdvanceCharacterCommand` level-ups. Experience makes a character eligible and never levels them by itself. The level-up window offers choices. All twelve classes go to level 20 with SRD 5.2.1 data. | ADR-0028, `Systems/CharacterProgression.md` |

### Level 1 gaps found while inspecting

These were documented here and not fixed. **Phase 22 addressed them (2026-11-13):** items 2 and 4 are fixed (D4; the level 1 baseline and its matrix test), item 5 is now proven by a test, and items 1 and 3 keep their paths (pending play allowed; legacy players are created through the same command and baseline). See `../Reports/Phase22-Completion-Report.md`.

1. **Creation is offered, never forced.** A new world's player starts *Pending*, with no class or level, and can walk, trade and talk before creating a character (`Systems/CharacterCreation.md`).
2. **Pre-creation play carries over.** Experience earned before creation is kept, with its ledger. A freshly created character can therefore be eligible for level 2 or higher at once; they are never levelled automatically. Coin and belongings carried before creation are also kept, and the background's coin is added on top.
3. **Legacy characters.** Characters from saves older than Phase 6 load as *Legacy*. Their path into a level 1 sheet needs re-checking against the new rule.
4. **No formal level 1 audit.** No validator checks a created character against a level 1 baseline (HP, AC, proficiency, features, spells, slots, resources, kit). Phase 20A audited progression in general, not a level 1 baseline.
5. **Test helpers are not a gap.** They create characters through the same command. For example, `CombatDriver.EnterTownAsFighter` dispatches `CreateCharacterCommand`. Levelling for tests goes through `AdvanceCharacterCommand`. No shortcut path to a higher-level player was found in the shipped code. This is a reading of the code; it has not been proven by a test.

## 2. Phase 21: Environment and settlement depth (workstream A, complete)

**Status:** complete (2026-11-13); pushed 2026-11-13 at the owner's instruction. The detail below is the status written while it was in progress, kept as the record of that update; the outcome is in `../Reports/Phase21-Completion-Report.md`. The two later additions it made to the economy (specialist keepers named from the population; a keeper's float) are described there.

**Brief:** `../Briefs/Phase21-Brief.md`. Its full title is "Environmental World, Living Settlements, Functional Shops, Crafting Workshops & Realistic Interiors".

### Done so far

All of it is uncommitted.

- **Preflight audit:** `../Reports/Phase21-Preflight-Audit.md`.
- **Walkthrough capture test:** `Tests/PlayMode/Phase21WalkthroughCaptureTests.cs`, with "before" captures.
- **Domain model for seasons, regional climate and weather:**
  - `Domain/World/Environment.cs`;
  - `Domain/World/WeatherModel.cs`;
  - `WorldData.Environment`.
- **A daily weather system and seasonal production:** `Simulation/Environment/WeatherSystem.cs`, plus a one-line hook in `EconomyRules` and registration in `StandardSimulationSystems`.
- **Not yet built or tested:** none of the code above has been compiled or tested.

### Still planned within Phase 21 (its own brief)

- Weather effects on travel and NPC routines.
- Save v23.
- Visual weather and seasonal light, and the night sky.
- Shop kinds, a supply log and physical displays.
- Crafting stations, recipes and NPC production.
- Furnished interiors.
- Settlement life.
- Asset licensing.
- Tests, captures and an owner checklist.
- A local commit.

### Not in Phase 21

Phase 21 must not take on:
- the open-world, travel or terrain workstreams below;
- the level 1 work.

### Conflict with the older documents

`Roadmap.md` and the Bible's status table listed Phase 21 as "Mature World Content & Immersion". The owner's Phase 21 brief replaced that scope with environment and settlement depth. Mature content therefore becomes workstream K (proposed Phase 33). The older row is corrected in both documents, and the change is recorded rather than rewritten silently.

## 3. Why the phases are in this order

1. **Level 1 integrity first (Phase 22).** It is small, independent of world work, and fixes the foundation that every later character phase builds on. Broader advancement work (Phase 30) must not start on top of a creation path that can produce an over-equipped or immediately over-eligible character.
2. **The world frame before the world's content (Phases 23 and 24 before Phase 25).**
   - Terrain, roads and landmarks need a single coordinate frame, a chunk model and streaming to live in.
   - Generating detailed terrain into today's region-local, per-scene structure would build exactly the "disconnected levels" the owner has rejected, and would then have to be redone.
3. **The scale decision gates everything in the open world.** See §4, decision D1. It must be settled at the start of Phase 23, before any data format is fixed.
4. **Traversal (Phase 26) needs geography to walk across (Phase 25).** Navigation UI (Phase 27) follows traversal, because a compass and minimap must show real roads and positions, not mock-ups.
5. **Living overland simulation (Phase 28) needs physical roads and streaming.** Caravans, patrols and wildlife then appear where they are rather than on the atlas strip.
6. **Character depth (Phase 30) and visual identity (Phase 31) need level 1 integrity and asset research.** They are independent of the world track and could swap places with Phases 28 and 29 if the owner prefers. The dependency is only on Phase 22.
7. **The interface overhaul (Phase 32) comes after the screens it must cover exist.** That means the navigation HUD, the level-up workflow and the creator. Earlier phases keep using the Phase 19 design system so nothing is built twice.
8. **Mature content (Phase 33) needs a real content-settings screen and character visual identity.** The settings screen comes from Phase 32, the visual identity from Phase 31; the content tags already exist (ADR-0061).
9. **Asset research (workstream L) runs in every phase**, each with a research gate before integration (§6). It is not a single phase.

## 4. Open decisions for the owner

These must be decided before or at the start of the named phase. The roadmap does not decide them.

| ID | Decision | Needed by | Notes |
|---|---|---|---|
| D1 | **World scale for walking.** At the current scale (1.2 km a cell, 4.8 km/h), one region takes about 32 hours to walk across, and the full world is roughly 460 × 307 km, about ten times wider than large open-world RPGs. Options: (a) compress the walkable scale (for example 100–200 m a cell) and re-derive travel times; (b) keep the travel scale and generate the walkable world at reduced detail in a coarse outer zone; (c) shrink the region grid. | Start of Phase 23 | **Decided in Phase 23 under the brief (2026-11-13), owner review invited:** a hybrid of (a) and (b). The walkable country is the same region map at 40 m a cell (5.12 km a region), and the 1.2 km travel scale is kept, with one walked metre standing for 30 m of road. Nothing in `TravelRules`, groups or trade changed. New games have 9 regions (ADR-0110). **Confirmed by the owner in the Phase 26 brief (2026-11-14): 40 m a cell is the approved scale.** |
| D2 | **What "fast travel" is.** For example: carts between connected towns, a known-road auto-walk, or none. | Phase 29 | Physical travel stays the primary experience. |
| D3 | **Ruleset identity.** The name and scope of the Realmkeeper ruleset (for example "Realmkeeper Rules 1.0"); which SRD content is kept as-is; what is adapted. | Phase 22 (baseline), Phase 30 (expansion) | See the Bible's ruleset section |
| D4 | **Pending play before creation.** Should a new game require creation before play, or allow pending play but discard pre-creation experience and coin? | Phase 22 | **Decided 2026-11-13: "No XP before creation".** Experience awards are refused until the character exists, so every new character starts at 0. Pending play (walking, trading, talking) stays allowed, and coin earned before creation is kept. Old saves that already hold pre-creation experience keep it (ADR-0109) |
| D5 | **Store-licensed and paid assets.** Commit them to the private repository, or keep them local only? | Phase 25 | Currently local only and git-ignored |

## 5. The proposed phases

Each phase is: foundational architecture (**Arch**), functional implementation (**Func**), content expansion (**Content**), visual and UX refinement (**UX**), performance and stability (**Perf**), or testing and validation (**Test**). Most are a mix; the main kind is listed first.

---

### Phase 22: Character creation and level 1 integrity (workstream B)

**Status:** complete (2026-11-13); pushed 2026-11-13 at the owner's instruction. Delivered with a player-driven level-up workflow on the existing advancement (ADR-0109). The outcome is in `../Reports/Phase22-Completion-Report.md`.

**Kind:** Func, Test.

**Purpose:** guarantee that every new player character starts as a legitimate level 1 character, and document the advancement baseline.

**Player-facing outcome:**
- Creating a character produces exactly what level 1 allows.
- The sheet shows exactly what the character can do.
- Nothing from a later level appears early.

**Systems affected:**
- `CreateCharacterCommand`, `CharacterDraft` and `CharacterValidation`;
- the creator and sheet UI;
- the new-game flow (pending state);
- the experience ledger at creation;
- NPC sheet construction (`NpcCharacterCommands`);
- saves and migration.

**Dependencies:** Phase 21 committed; owner decisions D3 and D4.

**Major implementation areas:**
1. **Audit.** Every class, species and lineage, and every background at creation: hit points, AC, proficiency bonus, saves, skills, expertise, features, subclass absence, cantrips and spells known or prepared, slots and Pact Magic, resources and charges, kit and coin, innate lineage spells by level (for example the tiefling legacy spells at levels 3 and 5 must stay locked at level 1).
2. **A `LevelOneValidator`** (Domain, engine-free) and a matrix test over every combination.
3. **New-game flow per D4.** Pending play and pre-creation experience and coin are handled as the owner decides.
4. **The legacy path** to a level 1 sheet stated and tested.
5. **The same baseline for generated NPCs at level 1.**
6. **A written advancement baseline** (`Systems/CharacterProgression.md` §"Level 1 baseline").

**Explicit exclusions:**
- new classes, species or subclasses;
- the level-up redesign (Phase 30);
- appearance work (Phase 31).

**Testing:**
- the level 1 validator over all 12 × 12 × 12 class, species and background combinations, plus lineages;
- creator PlayMode for several classes;
- save and load of a new level 1 character;
- migration of pending and legacy saves.

**Completion criteria:**
- No path in the shipped game creates a player character above level 1 or with level 2+ benefits.
- The validator runs on load, and the sheet matches the rules engine.
- Owner checklist written.

**Known risks:**
- Older saves with pre-creation experience: the decision must not silently delete earned data.
- SRD data errors found by the audit may touch combat balance.

---

### Phase 23: Open-world architecture I — one world frame (workstream C)

> **Done differently (2026-11-13).** The Phase 23 brief replaced this plan with "Overland World Foundation, Expanded World Scale, Realm Architecture & Traversal". It built walkable regions as modular environments (ADR-0110 to ADR-0112), not one coordinate frame, and also covered much of the geography and traversal planned below for later phases. See `../Reports/Phase23-Completion-Report.md`. The plans for Phase 24 and later should be re-read against it.

**Kind:** Arch.

**Purpose:** give the world one authoritative coordinate frame, so the strategic map, settlements, wilderness and places are positions in the same world, not separate scenes.

**Player-facing outcome:** little that is visible. The atlas and settlements look the same, but each settlement now sits at its true world position, and its surroundings can be queried.

**Systems affected:**
- `RegionMap` and `RegionData` (atlas cells), `MapCoord`;
- settlement layouts and place positions;
- `TravelRules`;
- the strategic map renderer;
- persistence.

**Dependencies:** owner decision D1 (scale); ADR review of ADR-0009, ADR-0076 and ADR-0077.

**Major implementation areas:**
1. **A `WorldFrame`** (Domain): region-to-world and world-to-region transforms, metres per cell for walking (D1), and a world-space position type with region ID and local offset.
2. **Settlement and place anchoring.** Every settlement layout and wilderness place gets a world anchor that matches its map cell. A validator checks that layouts fit their cell and do not overlap.
3. **The terrain data model.**
   - Chunked world heightfield tiles derived deterministically from the region maps (plus detail noise), with continuous edges across region borders.
   - A tile cache, not saved; only edits are saved.
4. **Route geometry.** Each `RouteSegmentData` gets a polyline in world space, derived from the region's road cells and border crossings, so roads have a physical line.
5. **An ADR** that supersedes the "positions stay region-local" part of ADR-0076 and records the scale.
6. **Save migration.** World anchors are derived on load, and nothing is regenerated.

**Explicit exclusions:**
- streaming and walking (Phase 24);
- terrain art, vegetation and landmarks (Phase 25);
- any HUD.

**Testing:**
- transform round-trips;
- border continuity (heights match within tolerance across every shared edge);
- anchors inside their cells;
- determinism (same seed, same tiles);
- old saves load with the same settlements and people.

**Completion criteria:**
- A query `HeightAt(worldPos)` and `WhatIsAt(worldPos)` answers for any point in the world.
- The strategic map and a debug terrain view agree.
- Performance is measured for tile generation.

**Known risks:**
- The scale decision ripples into travel times, caravan pacing and trade (all read `TravelRules`).
- Six 128² maps may be too coarse for walkable terrain without heavy detail synthesis.

---

### Phase 24: Open-world architecture II — streaming, travel state and persistence (workstreams C and E)

> **Done differently (2026-11-13).** The Phase 24 brief replaced this plan with "Overland World Living Systems, Navigation Integration, Encounters, Hazards & Ground Weather".
> - Streaming within a region was already built in Phase 23.
> - Phase 24 unified the travel state (ADR-0113).
> - It added landmarks, visible travellers and encounters (ADR-0114), and ground weather (ADR-0115).
> - It raised the save schema to v26.
>
> See `../Reports/Phase24-Completion-Report.md`. The plans for Phase 25 and later should be re-read against both reports.

**Kind:** Arch, Perf.

**Purpose:** make the world frame walkable in Unity, with chunks loading around the player and the player's world position as authoritative state.

**Player-facing outcome:**
- The player can walk out of a settlement's edge onto continuous (still plain) terrain, and walk back in.
- Save and load work in the open.

**Systems affected:**
- `ExplorationController` and the streamer;
- `RealmkeeperRuntime` view modes;
- the player's location model (settlement or place versus open world);
- simulation tiers (ADR-0081);
- `SimulationPlayback`;
- saves.

**Dependencies:** Phase 23.

**Major implementation areas:**
1. **A chunk streamer.** Load and unload terrain tiles in rings around the player, with a budget per frame and floating-origin shifting so precision holds far from the origin.
2. **Location state.** `PlayerLocation` gains `InWorld(worldPos)`. A settlement or place is entered by crossing into its footprint, not by a menu.
3. **Simulation tiers by distance.** Nearby settlements tick in detail; distant ones as today.
4. **Save and load mid-wilderness,** and in a region other than the home region.
5. **Settlement scenes become streamed content placed at their anchor.** The existing layout builder is reused.
6. **Memory and frame-time budgets,** measured and stated.

**Explicit exclusions:**
- detailed terrain content;
- NPC groups on the roads in 3D;
- the navigation HUD;
- fast travel.

**Testing:**
- PlayMode: walk out of town, cross a region border, save, load, walk back in;
- streaming stress test (memory flat over a long walk);
- determinism of what loads.

**Completion criteria:**
- No blank void beyond a settlement edge: the terrain continues to the horizon (low detail is acceptable).
- Region borders are crossed without a loading screen, or with a short documented one.
- Frame time and memory stay within stated budgets.

**Known risks:**
- Unity terrain versus custom meshes (decide by measurement).
- Interplay with existing scene-per-settlement code; physics and navigation baking at runtime.

---

### Phase 25: Overworld geography generation (workstream D)

> **Done differently (2026-11-14).** The Phase 25 brief replaced this plan with "Complete UI/UX Overhaul, Interface Architecture, Visual Identity & Third-Party Asset Integration" (the interface work this roadmap had planned later, under visual identity and the interface overhaul). See `../Reports/Phase25-Completion-Report.md`. Overworld geography generation is still to be scheduled.

**Kind:** Content, Func.

**Purpose:** fill the world with coherent geography that the simulation already implies.

**Player-facing outcome:**
- Mountains, hills, valleys, forests, fields, rivers, lakes and the region's landmarks are visible in every direction.
- Roads and trails run between settlements where the route graph says they do.
- Bridges and passes are physical.

**Systems affected:**
- terrain generation;
- `RouteSegmentData` geometry;
- the river and water model;
- vegetation (extending the Phase 21 seasonal foliage);
- landmarks (ADR-0089 places, Phase 16 landmarks);
- farms and settlement outskirts;
- the asset pipeline.

**Dependencies:** Phase 24; asset research gate L-Terrain (§6); D5.

**Major implementation areas:**
1. **Terrain shaping.** Ranges, cliffs, valleys and rock formations from the region map's elevation and the region kind. Settlement sites are flattened and blended into the landscape.
2. **Roads, trails and bridges.** Meshes along the route polylines, with kind-specific surfaces. Bridges and fords where routes cross water; passes cut through ranges.
3. **Water.** Rivers from the region rivers, with continuous beds across borders; lakes, waterfalls where heights demand them, and coastlines where a region has sea.
4. **Vegetation.** Biome rules per region kind and climate (forests, woods, clearings, meadows, farmland). Instanced, with LOD and seasonal variation.
5. **Roadside and outlying structures.** Farms, mills, watchtowers, ruins, forts, camps and shrines. Only where the simulation has a reason for them (Bible principle: no decoration that pretends to function).
6. **Distant landmarks.** Visible from afar, and registered for discovery (Phase 27).

**Explicit exclusions:**
- creatures and NPC groups in the open (Phase 28);
- the HUD (Phase 27);
- new settlements.

**Testing:**
- geography validators: roads reachable and on walkable slope; rivers continuous and downhill; settlements not on cliffs; bridges where roads cross water;
- determinism;
- PlayMode captures along a route between two towns.

**Completion criteria:**
- Walking from one settlement to a neighbour along the road shows coherent, continuous geography.
- The atlas and the walked world agree on roads, rivers and landmarks.

**Known risks:**
- Content volume and performance (vegetation density).
- Asset coherence across packs ("not a collection of unrelated free asset packs", Bible §16).

---

### Phase 26: Physical traversal and destination integration (workstream E)

> **Done differently (2026-11-14).** The Phase 26 brief replaced this plan with "Interface Completion, Gameplay Experience Refinement, Codex & UI Quality Assurance". See `../Reports/Phase26-Completion-Report.md`. Traversal integration is still to be scheduled. The owner confirmed D1 (40 m a cell) in the Phase 26 brief.

**Kind:** Func.

**Purpose:** make walking between places the normal way to travel, with time, companions and arrival handled as they are for atlas journeys today.

**Player-facing outcome:**
- Leave any settlement or place and walk or run to another, through the open world, with the party following.
- Time passes by distance covered.
- Arrival is physical.

**Systems affected:**
- the player controller (walk, run, terrain speed);
- the party and followers (ADR-0070, ADR-0073);
- world time (time from movement);
- journeys (ADR-0078: kept for groups and for an optional "follow the road" auto-travel);
- road fights (ADR-0086 can now stage on the actual road);
- place entry;
- persistence of location state.

**Dependencies:** Phase 25.

**Major implementation areas:**
1. **Movement.**
   - Speeds from the rules engine (species speed, encumbrance, conditions), with terrain modifiers (road, trail, rough, forest, slope, water, snow from Phase 21 weather).
   - Night and weather visibility.
2. **Time from movement.** Game minutes per metre walked, consistent with `TravelRules` under decision D1.
3. **Companions in the open.** They follow, regroup, wait at a spot and keep up, and are never lost at chunk edges.
4. **Entering and leaving** settlements, places and interiors through their real entrances.
5. **Road encounters on the road itself.** This closes the Phase 17 limitation "ground is the near settlement's land".
6. **Atlas journeys.** They stay as a planning and "auto-walk along this road" convenience, moving the real party along the real road.

**Explicit exclusions:**
- the minimap and compass (Phase 27);
- fast travel (Phase 29);
- mounts.

**Testing:**
- PlayMode: walk between two towns;
- party arrival and companion continuity;
- the time-passed formula;
- save and load mid-journey;
- a road fight on the road.

**Completion criteria:**
- The atlas is no longer required to reach any settlement.
- Times and positions agree between atlas and walking.

**Known risks:** long walks may feel empty until Phase 28. Accept this, and keep the phases close together.

---

### Phase 27: Navigation, discovery and exploration HUD (workstream F)

**Kind:** UX, Func.

**Purpose:** let the player find their way with the HUD and the world itself, without constant atlas use and without a permanent GPS line.

**Player-facing outcome:**
- A compass with heading and markers.
- A compact local map or minimap, with discovered roads and trails.
- Destination and quest markers, distances, and optional route guidance.
- Landmarks discovered by sight.

**Systems affected:**
- the HUD (Phase 19 design system);
- place discovery (ADR-0065, extended to overland locations);
- NPC knowledge (directions, rumours, ADR-0014);
- maps as items;
- the quest tracker;
- settings.

**Dependencies:** Phases 25 and 26.

**Major implementation areas:**
1. **Three knowledge states for every location:**
   - **exists** (in world data);
   - **known** (told, read, rumoured, seen from afar), with direction or rough area;
   - **discovered** (physically reached).
   Sources: physical exploration, sightlines to landmarks, NPC directions, rumours, maps and documents, quests, travellers and merchants, faction and settlement knowledge.
2. **Compass, minimap and local map.** These show only what the player knows; zoom and orientation controls.
3. **Guidance modes,** as a player setting:
   - **Guided:** a route line along known roads.
   - **Markers only.**
   - **Explorer:** compass bearings and signposts only.
4. **Signposts and milestones in the world** that read real route data.
5. **The atlas stays for planning.** It shares the same knowledge model.

**Explicit exclusions:**
- the full UI overhaul (Phase 32);
- fast travel.

**Testing:**
- discovery transitions (seen, then discovered), keyed so they happen once;
- HUD shows nothing unknown;
- PlayMode navigation to a quest target in each guidance mode.

**Completion criteria:** a new player can reach the next town and a quest place using only the HUD and signposts, in every guidance mode.

**Known risks:** an over-helpful HUD undermining exploration. Defaults must be decided with the owner.

---

### Phase 28: Living overland simulation (workstream G)

**Kind:** Func, Content.

**Purpose:** make the open world inhabited and eventful without random encounters every few minutes.

**Player-facing outcome:**
- Caravans, patrols, refugees, travellers and outlaws seen physically on the roads where the simulation says they are.
- Wildlife in its habitat.
- Campsites and resting.
- Roadside discoveries.
- Hazards.
- Travel quests.

**Systems affected:**
- groups (ADR-0079), fauna (ADR-0044), world events (ADR-0080);
- discoveries (ADR-0088), places (ADR-0089);
- rest (ADR-0040), weather (Phase 21);
- quests, companions.

**Dependencies:** Phases 26 and 27.

**Major implementation areas:**
1. **Groups as bodies.** Existing groups become physical when near the player: the same IDs, goods and leaders.
2. **Wildlife.** Spawned from regional populations into habitats (forest, hills, water), with named individuals' fates kept (ADR-0044).
3. **Encounter pacing.** Authored and emergent situations placed by geography and world state, with a stated density budget per hour of travel and no unlimited respawns.
4. **Camps and rests in the open.**
   - Campsites as places.
   - Watches; weather and night risks.
   - Companions' camp talk through existing records.
5. **Hazards and consequences.** Fords in flood, snow on passes, falls, exhaustion; they use existing lasting conditions (ADR-0068).
6. **Wilderness locations.** Hidden places reachable only overland. Roadside discoveries tied to the discovery system.

**Explicit exclusions:**
- mounts and transport (Phase 29);
- new creature art beyond research gate L-Creatures.

**Testing:**
- the density budget;
- group continuity between atlas and body;
- determinism of placements;
- fates persist;
- save and load with groups nearby.

**Completion criteria:** a long journey shows the world's real traffic and wildlife, and its encounters have causes the player can learn.

**Known risks:** CPU cost of many active bodies; tuning pacing.

---

### Phase 29: Optional fast travel and transport (workstreams E and G)

**Kind:** Func.

**Purpose:** convenience that respects geography and the world's persistence, never a replacement for walking.

**Player-facing outcome:** per decision D2, for example:
- paid carts between connected towns, taking real time along real roads;
- "travel on along this known road";
- possibly mounts.

**Systems affected:** journeys, groups, the economy (fares), time, the party.

**Dependencies:** Phase 28; D2.

**Major implementation areas:**
1. **Simulated travel.** Fast travel advances the world through the same journey model, so events, encounters and closures can interrupt it.
2. **Availability.** It needs world knowledge and real infrastructure (a known road; a cart service in a town with a trade route).
3. **Mounts and carts,** if approved.

**Explicit exclusions:**
- teleportation without an in-world cause;
- any route that bypasses closures or active events without an explicit design.

**Testing:**
- fast travel equals the equivalent walked journey in time and world state;
- interruptions work.

**Completion criteria:** every fast-travel option is optional, in-world, and consistent with walking.

**Known risks:** players using it to skip content. Acceptable if it is honest about time.

---

### Phase 30: Character advancement and RPG depth (workstream H)

**Kind:** Func, Content.

**Purpose:**
- Complete the level-up workflow and class depth on top of Phase 22's baseline.
- Define the Realmkeeper ruleset as a deliberate adaptation, not a 5e reproduction.

**Player-facing outcome:**
- Every level-up shows: gained automatically, newly available, choices required, choices optional, and what is coming next.
- The sheet separates current, newly gained, unselected, future (locked) and historical choices.
- More subclasses and spells.

**Systems affected:**
- the advancement workflow and `AdvancementDraft`;
- the level-up window;
- the sheet;
- `RpgCatalog` (classes, subclasses, feats, spells);
- the rules configuration (ADR-0026);
- NPC progression (ADR-0043).

**Dependencies:** Phase 22; D3. It may run before Phases 28 and 29 if the owner prefers.

**Major implementation areas:**
1. **Level-up presentation and advancement history.** Notifications when a level is available; never auto-chosen.
2. **Realmkeeper original subclasses.**
   - Labelled as originals.
   - Validated against the progression model.
3. **Spells.**
   - Catalog levels 6–9.
   - Deferred spells and innate lineage spells by level.
   - Out-of-combat durations.
4. **Feats, species and lineage mechanics** not yet played (the Phase 20A list).
5. **NPCs** on the same underlying progression, with simplified presentation.
6. **The ruleset document.** A new `Systems/RulesetOverview.md` (not yet written) labels every rule area as SRD 5.2.1 (adapted or as-is), Realmkeeper-specific, planned, or deliberately unsupported.
7. **Tavern Table study.** A study of *The Tavern Table* (the owner's separate D&D application, found locally as a standalone HTML build) for species, class, item, spell and progression ideas. **It is a reference only:** no code, assets or data are merged.

**Explicit exclusions:**
- multiclassing (unless the owner approves it);
- appearance (Phase 31).

**Testing:**
- every class, levels 1 to 20, through the real level-up window;
- mutation tests on the rules;
- NPC progression parity;
- save and migration of advancement history.

**Completion criteria:** at every level, the player can see what changed and what to choose, and every choice is theirs.

**Known risks:** balance drift from originals; scope creep into a full 5e reproduction.

---

### Phase 31: Advanced character creation and visual identity (workstream J)

**Kind:** Content, UX.

**Purpose:** make species and people visually distinct and identity-rich.

**Player-facing outcome:**
- Species-specific anatomy (ears, horns, tails, scales and proportions).
- Faces, hair, facial hair, skin, eyes, scars and tattoos.
- Clothing and armour that fit.
- A high-quality 3D preview.
- NPCs built and inspected with the same rules.

**Systems affected:** appearance data (schema), the character model pipeline, the creator, the preview, NPC generation, the item presentation (ADR-0017).

**Dependencies:** Phase 22; asset research gate L-Characters; Phase 30 for new species rules, if any.

**Major implementation areas:**
1. **Replace the nonhuman anatomy placeholders.** This is a known gap: Phase 20 and ADR-0024 show non-human species on the human model.
2. **A modular character pipeline** with a shared rig, and armour fitted per body.
3. **New species or subspecies,** only when rules and art both exist.

**Explicit exclusions:**
- mature presentation (Phase 33);
- interface restyling (Phase 32).

**Testing:**
- every species and body combination renders and animates;
- NPC generation produces valid looks;
- save migration of appearance.

**Completion criteria:** no species is shown as a placeholder, or the remaining ones are labelled.

**Known risks:** asset availability and licence (highest risk area; see the Phase 20 research).

---

### Phase 32: Complete UI/UX and gameplay interface overhaul (workstream I)

**Kind:** UX.

**Purpose:** a cohesive, complete interface over everything that now exists. This is not a cosmetic restyle.

**Player-facing outcome:** consistent design language, feedback, transitions and animation, responsive layouts and consistent interaction across:
- main menu, world and campaign selection, world creation, loading screens;
- navigation, tabs, panels, HUD, contextual menus;
- character creation and sheets, inventory and equipment, spellbook;
- journal, quests, map;
- NPC interaction and inspection;
- settlement, kingdom and faction management;
- settings: accessibility, key bindings, graphics, audio, gameplay and content.

**Systems affected:** all presentation; the Phase 19 design system (ADR-0098, ADR-0099); settings persistence.

**Dependencies:** Phases 27, 30 and 31 (so the screens they add are covered). Interim phases use the Phase 19 design system.

**Major implementation areas:**
- the remaining items from the Phase 19 report §3;
- key rebinding;
- accessibility (text size, contrast, colour-blind modes, reduced motion);
- a content settings screen (needed by Phase 33).

**Explicit exclusions:** gameplay rule changes.

**Testing:**
- the interaction audit (as in Phase 19) at three resolutions;
- keyboard-only paths;
- settings persistence.

**Completion criteria:** no screen outside the design system; every screen reachable and readable at 1366×768.

**Known risks:** a long tail of screens. Use an inventory checklist.

---

### Phase 33: Mature world content and immersion (workstream K)

**Kind:** Content.

**Purpose:** a grounded adult world, intentional and contextual, never gratuitous, controlled by player settings.

**Player-facing outcome:**
- profanity in dialogue where in character;
- blood, injuries and gore, and violence with consequences;
- darker themes;
- adult relationships and romance where appropriate;
- contextual non-sexual nudity (bathhouses, medical settings, homes);
- believable taverns, bathhouses, homes and healers;
- NPC behaviour that reflects the setting.

All of it follows content settings applied consistently to dialogue, appearance, environments and generated text.

**Systems affected:**
- content tags (ADR-0061);
- dialogue and quests;
- appearance (Phase 31);
- conditions and injuries (ADR-0068);
- relationships and loyalty (`Systems/Relationships.md`, ADR-0074);
- interiors (Phase 21);
- the settings screen (Phase 32).

**Dependencies:** Phases 31 and 32.

**Explicit exclusions:**
- sexual content beyond what the owner's brief approves;
- anything presented without its content setting.

**Testing:**
- every tag respected at every setting level, in dialogue, visuals and generated text;
- settings persist.

**Completion criteria:** each mature category can be shown, softened or hidden, consistently.

**Known risks:** asset licences for mature content; platform rating implications.

---

### Phase 34: Performance, stability, balance and release readiness

**Kind:** Perf, Test.

**Purpose:** profile and harden the whole game: the open world at full content, long sessions, save sizes, balance passes, a build pipeline, and crash-free long playtests.

**Dependencies:** all of the above.

**Completion criteria:**
- stated budgets met on the target hardware;
- a player build verified (including shaders found by name; see the Phase 2 limitation);
- owner playtest sign-off.

## 6. Workstream L: asset research, integration and visual quality (continuous)

Asset research is a gate inside each phase that needs art, not a separate phase.

**Existing deliverables to build on:**
- `../Research/Realmkeeper-Asset-Research-Master.md`
- `Realmkeeper-Asset-Shortlist.md`
- `Realmkeeper-Asset-License-Registry.md`
- `Realmkeeper-Asset-Rejected-Candidates.md`
- `Realmkeeper-Asset-Integration-Roadmap.md`
- `Realmkeeper-Existing-Asset-Inventory.md`
- the Phase 19 and Phase 20 research files
- `../Art/AssetInventory.md`

| Gate | Before phase | Scope |
|---|---|---|
| L-Environment | 21 (in progress) | Interiors, furniture, taverns, shops, forges, workshops, props |
| L-Terrain | 25 | Terrain materials, vegetation, rocks, water, roads, bridges, roadside structures, skies, weather VFX, ambient audio |
| L-Creatures | 28 | Wildlife and monster models and animation, camp props |
| L-Characters | 31 | Species bodies and heads, hair, clothing, armour, animation compatibility |
| L-Interface | 32 | Fonts (OFL), icon sets, UI sounds |
| L-Mature | 33 | Injury and blood VFX, bathhouse and medical sets, under content settings |

### Rules for every gate

These restate Bible §16.3 and the standing licence rules.

- **Verify the exact licence text** and record it in the licence registry, with the source link.
- **Commercial use and redistribution.** Confirm commercial use. Check redistribution in a source repository: store-licensed packs stay local and git-ignored unless decision D5 says otherwise.
- **Approved and refused licences.**
  - CC0 and CC BY are approved.
  - OFL is approved for fonts.
  - CC BY-SA, NC, custom and unclear licences need an explicit owner decision.
- **"Free" does not mean commercially usable**, and a marketplace listing does not mean the licence suits this use.
- **Technical suitability.** Assess pipeline, rig and scale, poly and texture budgets, and URP compatibility, then measure in-scene performance.
- **Record attribution** in `CREDITS.md` at integration time.
- **No purchases** without the owner's approval.

## 7. Cross-cutting principles for every phase

These are restated from the Bible, not new.

- **One persistent world.**
  - The strategic map, settlements, the wilderness and places are views of one authoritative state.
  - No duplicated world, and no scene that invents its own geography.
- **The player.** They are not a chosen one, and may work alone or with factions.
- **The world goes on without the player**, at simulation tiers by distance.
- **Named people** keep their identities and lives across every view.
- **Determinism.** Generation and simulation are deterministic and seeded. Saves are versioned with append-only migrations.
- **Function first.**
  - Functional gameplay comes before decoration.
  - No prop pretends to function.
  - UI shows real system state.
- **Architecture.**
  - Keep the layers: Domain, Simulation, Application, Infrastructure, Presentation.
  - Domain and Simulation stay free of UnityEngine. An exception needs an ADR.
- **No premature scaling.** No multiplayer, cloud services, ECS/DOTS or speculative infrastructure.
- **Controlled phases.** Each phase is tested, documented and stopped for review.

## 8. Recorded requirements, not yet scheduled

### R1: Shops as economic entities (recorded in Phase 22, 2026-11-13)

**Source:** the Phase 21 report found that keepers' purses run low, because NPC customers do not buy with money. Phase 22's brief asked for this to be recorded as a future requirement and **not** built in Phase 22.

**Requirement.** Shops should eventually work as actual economic entities, not as inventories that replenish themselves:
- **NPC purchases:** people buy goods with their own money, at the same prices and through the same stock and till.
- **Customer demand:** what a town wants, by season, occupation, wealth and events, drives what sells.
- **NPC spending behaviour:** budgets, needs before wants, and saving.
- **Revenue and depletion:** sales fill the keeper's till and empty the shelves.
- **Feedback into production and trade:** what sells is made and imported more; what does not is made less.
- **Believable behaviour:** customers seen buying in the street and in shops; keepers who adjust prices and stock to their trade.

**Constraints:**
- One economy and one inventory (ADR-0018, ADR-0106). No second stock or price system.
- Deterministic and saved.
- Measured against the daily tick budget. Shops already cost about 14 ms per simulated day for 67 shops.

**Candidate phases:** Phase 28 (living overland simulation) or a dedicated economy phase. The owner decides.
