# Phase 10 report: Bestiary, NPC Character Systems, Encounter Intelligence & Living Settlement Generation

Date: 2026-11-11. Branch: `feature/phase-0-1-foundation`. Base: `27a5261` (Phase 9, local). Pre-flight:
`Documentation/Phase10/PreFlight.md`. Detail documents: `Documentation/Phase10/` (SettlementGeneration,
CreatureAndNpcSystems, Testing, Performance, Limitations). ADRs 0043–0049.

## A. Summary

**What was implemented (functional):**
- **Named people with character records** (ADR-0043).
  - Every living named person has an archetype, occupation, gear and purse.
  - Those whose work is fighting, healing or keeping order hold full character sheets, built by the player's
    own rules and validated the same way.
  - Promotion, keyed experience and advancement are commands.
  - People with sheets fight as themselves, rest nightly and keep their identity throughout.
- **A data-driven bestiary** (ADR-0044).
  - 25 new creatures: 20 from SRD 5.1 and 5 labelled Realmkeeper originals, across five categories, with
    behaviour profiles, traits, reactions, riders and loot, all validated.
  - Traits act in combat (Charge, Regeneration, Undead Fortitude, Relentless, Nimble Escape, Recharge, Parry).
  - Behaviour drives the AI (flight, packs, predators, the mindless, the territorial).
- **Aggregate wildlife and herds.**
  - Regional populations breed, are preyed upon and hunted, weekly.
  - Settlements keep herds that graze real pastures.
  - Encounters draw named individuals whose fates persist.
- **Reactions asked, not taken** (ADR-0045).
  - Ten reactions and decisions are asked at their rule points.
  - The player answers through a prompt in the encounter panel; computer reactors use the same path.
  - Answers are atomic, answered once, and saved while waiting.
- **Encounters from the world** (ADR-0046).
  - Outings to the outskirts, the road or the farms, planned from the populations in range, the time and the
    place.
  - Checked before they start, and settled exactly once: fates, deaths, XP, loot, memories, chronicle.
- **Settlement identity and terrain-first plans** (ADR-0047–0049).
  - Each settlement has an identity read from its economy, people and ground.
  - It has a persisted plan:
    - terrain with hills and a levelled square;
    - a stream or river with bridges or fords;
    - main roads to its real neighbours, district streets and lanes;
    - districts, buildings on buildable ground with plinths;
    - farmsteads with tracks, fields, pastures, orchards, gardens and yards;
    - herds, landmarks, and a town's walls and gates.
  - The walkable settlement, the strategic map and the inspector all draw from that one plan.
  - People walk its road graph and stand on its ground.
- **Schema v12** with migration. Older saves gain all of this once, on load.

**What materially changed:**
- **Settlements:** the walkable town is no longer a flat two-street cross of 13 buildings. It is a walled town of
  59–84 buildings on rolling ground, with a stream, bridges, districts and farms.
- **Fights:** they can be asked about, come from the world, and leave marks on it.
- **Guards:** a guard is a level-2 fighter with a chain shirt and a spear, not a stat block.

**What remains deferred:**
- plan growth;
- creature models;
- automatic promotion;
- travel encounters on the map;
- function-specific building kits;
- water collision.

Full list: section L and `Phase10/Limitations.md`.

## B. Architecture

**New systems:**
- `FaunaSystem` (`realmkeeper.fauna`, order 460, weekly).
- `LivingWorldGenesis`, which fills what a world lacks at generation and on load.
- `SettlementLayoutGenerator` and `SettlementLayoutValidator`.
- `EncounterGenerator`.
- The reaction core (`CombatEngine.Reactions`).

**Modified systems:**
- **Combat engine:** sheets for NPCs through `CombatStats.SheetOf`, creature traits, behaviour AI, flee, and
  reaction points in attacks, spells, damage and saves.
- **Exploration:** environment from the plan, road-graph routing, standing heights.
- **Presentation:** a terrain-first scene builder, and the strategic settlement mesh from the plan.
- **HUD:** the Character section with DM tools, settlement identity, a reaction prompt, the bestiary in the
  combat inspector, and the outing button.
- **Persistence:** v12.
- **World validator:** NPC records and sheets, populations, instances, herds, plans and waiting reactions.

**New data models:**
- `NpcCharacterData` and `NpcGearData`; `NpcArchetypeDefinition` and its catalog.
- `CreaturePopulationData`, `CreatureInstanceData`, `LivestockHerdData`.
- Bestiary fields on `CreatureDefinition`.
- `PendingReactionData`, `ReactionOpportunity`, `ActionRecord`.
- `EncounterOriginData`.
- `SettlementLayoutData` and its parts: identity, terrain profile, waterways, districts, roads, bridges,
  buildings, fields, landmarks and features.
- `LayoutHerd`; `LocalEnvironmentLayout.Plan` and `Gate`.

**New commands and queries:**
- **Commands:** `PromoteNpcCommand`, `AwardNpcExperienceCommand`, `AdvanceNpcCommand`, `RespondToReactionCommand`,
  `StartWorldEncounterCommand`.
- **Queries:** `GetNpcCharacter`, `GetSettlementLayout`, `GetHerds`, `PreviewWorldEncounter`;
  `EncounterSnapshot.Prompt`; `CombatantView.Bestiary`.

**New ADRs:** 0043 (NPC sheets), 0044 (bestiary and wildlife), 0045 (reactions), 0046 (world encounters),
0047 (settlement identity and plan), 0048 (persisted spatial data and v12), 0049 (navigation).

## C. NPCs

- **Sheet support.**
  - Sheet archetypes:
    - town guard (fighter 2);
    - guard captain (fighter, Champion);
    - watch warden;
    - woodsman;
    - priest and priest elder (cleric, Life);
    - smith.
  - Sheets are made by `CharacterDraft`, `BuildSheet` and `CheckSheet` from archetype preferences. Equipment
    comes from the item catalog; features, resources and spells from the class.
  - 19 named people in the reference world, 9 with sheets.
- **Simulation tiers.** Tiers are unchanged (Notable, Resident, simulated population). Promotion makes a person
  Notable. People with sheets take a long rest each night. Civilians keep lightweight records.
- **Class and progression coverage.**
  - Any of the twelve classes can be given by promotion (`PromoteNpcCommand(npc, class)`).
  - Advancement takes every earned level through `CharacterAdvancement`, with choices from the archetype.
- **Equipment and spellcasting.**
  - The sheet's armour and weapons set armour class and attacks.
  - Clerics prepare spells and cast through the shared spell engine in fights.
- **Continuity.** Combatants reference the person: there is no copy, no duplication on entering or leaving, and
  the fight's results are written to their sheet.

## D. Bestiary

- **Catalog.** The Phase 8–9 creatures, plus 20 from SRD 5.1:
  - deer, boar, black and brown bears, dire wolf, giant rat, giant spider, goat, riding and draft horses, mastiff;
  - goblin, highwayman (the SRD's Thug), bandit captain, cultist, veteran;
  - skeleton, zombie, ogre, troll.
  Plus 5 Realmkeeper originals: rabbit, fox, cattle, sheep, chicken. `CreatureCatalog.Validate()` runs in the
  tests.
- **Categories:** wildlife, livestock, common threat, dangerous, undead.
- **Behaviour:** skittish, passive, pack, predatory, territorial, scavenging, mindless, intelligent, each with a
  flee threshold and a summary.
- **Combat integration:**
  - traits at their rule points;
  - riders on hit (prone, grappled);
  - save-for-damage attacks and recharge;
  - Parry as a reaction;
  - flight that ends the turn and takes the creature off the field.
- **Wildlife:**
  - populations by habitat niche per sector, plus road bands, vermin and herds;
  - weekly logistic breeding, predation and hunting;
  - herds toward the farms' targets, and culled in famine.

## E. Reactions

- **Player:**
  - Asked at the rule point. The action waits with nothing of it applied, behind a banner above every column of
    the encounter panel (trigger, cost, effect, Accept or Decline).
  - Accepting equals answering at once. Declining leaves the action as if nothing was asked.
  - Stale or repeated answers are refused, and the question survives save and load.
- **NPCs:** the same `Ask` path, decided by policy. Opportunity attacks are taken unless the creature is skittish;
  Shield and Parry are used when they matter.
- **Supported:** opportunity attack, Shield, Parry, Cutting Words, Deflect Attacks, Uncanny Dodge, Hellish Rebuke,
  Counterspell, Indomitable, Bardic Inspiration.
- **Deferred:**
  - Shield and Hellish Rebuke against spells;
  - reactions on a missed attack (the hook exists, unused);
  - asking allies who have sheets (they decide by policy).

## F. Encounter generation

- **Types:** wildlife, predators, bandit ambush, patrol (with the road watch), hostile creatures, defend a farm,
  vermin.
- **Composition:**
  - weighted by population count, habitat, time of day, purpose, distance and the watch;
  - groups within the moderate budget (75 XP per level), a leader only within the high budget (100 per level);
  - dangerous creatures rare;
  - escape as the objective when the foe is far beyond the party.
- **World integration:** draws from real populations, takes a protected farmer along to the farms, and brings
  the road watch on the road by day.
- **Persistence and consequences:**
  - The origin is saved with the encounter.
  - Instances record their fates; populations count their deaths.
  - XP is keyed `encounter:{id}`, loot and memories are recorded, and an `EncounterResolved` event is written.
  - Everything is applied once (tested through commands, save and load, and a direct second resolution).

## G. Settlement identity and generation

- **Archetypes:** market town, village green, river crossing, hillside, fortified, roadside, farmstead, chosen from
  the identity (economy, water, ground, walls), never at random.
- **Terrain:**
  - regional fall from the region map;
  - a landmark hill with a flat top;
  - countryside rises, rolling noise, a levelled square;
  - the water's bed and banks cut in.
- **Roads:**
  - main roads to the real neighbours, following the land and going round buildings;
  - a ring street and district streets in a town; lanes across districts;
  - farm tracks.
  - Every road is joined into one network (tested).
- **Waterways and bridges:** a river keeps the region's course; a stream runs on the low side. Every crossing is
  bridged (stone or timber, with piers), or forded on a farm track.
- **Districts:** 9 in a town, 7 in a village, 4 in a hamlet, each with its own density and building mix.
- **Buildings:**
  - plots along streets, facing them, on ground levelled with plinths;
  - sizes, materials, roofs and storeys by function and district;
  - the world's site keys kept.
- **Farms and gardens:** farmsteads with barn, shed, yard and track; crop, fallow, pasture and orchard fields; a
  garden behind about half the homes.
- **Livestock:** herds per settlement (cattle, sheep, goats, horses, chickens) on their pastures, persisted and
  simulated weekly, drawn as placeholder animals.
- **Landmarks:** the keep, chapel or watchtower on the high ground; the mill; the main bridge; the town gate; the
  market cross or great oak.
- **Before and after**, in the shipped world (seed 123456789):

| Settlement | Before (Phase 9) | After (Phase 10) |
|---|---|---|
| Bramhaven (town) | <ul><li>104 × 104 m, flat.</li><li>2 crossing streets.</li><li>13 buildings (10 houses, hall, granary, tower).</li><li>A square wall and gate.</li></ul> | <ul><li>400 × 400 m of rolling ground.</li><li>71 buildings in 9 districts.</li><li>17 roads, 2 bridges over its stream.</li><li>Walls on a 105 m circuit, with gatehouses where the roads pass.</li><li>A keep on its hill.</li><li>Farmsteads, fields and herds outside.</li></ul> |
| Mossholm (village) | 84 m square, flat, 2 streets, 10 buildings | <ul><li>300 m of ground.</li><li>63 buildings round a green.</li><li>A chapel on its rise, a watermill and a bridge over the brook.</li><li>Farms and pastures.</li></ul> |
| Westerthorpe (hamlet) | 68 m square, flat, 2 streets, 5 buildings | <ul><li>220 m of ground.</li><li>24 buildings by its river.</li><li>A watchtower on the rise, a mill and a bridge.</li><li>Farmsteads.</li></ul> |

The three are recognizably different: a walled market town, a village round its green, and a hamlet at a river
crossing. Each is described in one line by its identity (screenshots p10_01–p10_10).

## H. Persistence

- **Schema version:** 12.
- **Migration:** `MigrationV11ToV12` adds the empty populations, creatures, herds and plans, and enables the fauna
  system. On load, `LivingWorldGenesis` fills what is missing, once and deterministically. Plans are never
  regenerated.
- **Legacy results:** a v11 document upgraded through a session gains 19 people, sheets, wildlife, herds, plans and
  pastures. Saved and loaded again, it is unchanged (tested). v2–v10 parity tests still pass through the chain.
- **Validation:** NPC records and sheets, populations and instances, herds and their pastures, every plan (cached
  per instance), and waiting reactions. A corrupt plan or record refuses the load.

## I. Testing

| Suite | Before | After |
|---|---|---|
| .NET | 520 passed | **552 passed**, 0 failed, 0 skipped; build 0 warnings |
| Unity EditMode | 520 passed | **552 passed**, 0 failed, 0 skipped; no compiler warnings |
| Unity PlayMode | 70 passed, 10 skipped | **88 tests: 74 passed**, 0 failed, 0 inconclusive, 14 skipped (the capture sets) |
| Phase 10 captures | — | 4 passed (15 screenshots and a performance log) |

- **Skips:** the capture sets of Phases 4–10 skip when `RK_CAPTURE_DIR` is unset (by design; captures need a
  graphics device). No other test is skipped.
- **UI-driven tests:** `Phase10PlayModeTests` (4) drive the HUD and panels, listed in `Phase10/Testing.md`.
- **Fault injection:** 10 mutants.
  - 6 caught: two after strengthening the tests.
  - 4 equivalent, each explained: a second guard makes the first one redundant.
- **Bugs found and fixed:**
  - the per-day cost (13.7 → 1.88 ms);
  - the NPC armour-class text;
  - floating signs;
  - practice allies;
  - over-budget outings;
  - eight generator faults found by a 40-seed sweep.
  Details: `Phase10/Testing.md`.

## J. Performance

- **Hardware:** Intel Core i9-14900HX (32 threads), 32 GB RAM, NVIDIA GeForce RTX 4070 Laptop GPU (Direct3D 11), Windows 11 Pro.
- **Method:** .NET timings in Release with warm-up; scene timings in the editor's play mode at 1920×1080, 180
  frames after 30 warm-up frames at fixed poses (`Phase10CaptureTests.Capture4_Performance`).
- **Results:**
  - Simulation: 1.88 ms per simulated day for the reference world, against 0.49–0.58 ms before Phase 10. NPC
    sheet validation accounts for about 1 ms of it. Unity's 15 ms/day budgets pass.
  - Generation: world generation 275 ms; slowest plan 86 ms.
  - Save: 488 KB (148 KB before); serialise 21 ms; load and validate 36 ms.
  - Routes: first 4 ms, then 0.01–0.03 ms.
  - Scene: entering the town builds its scene in 351–606 ms (two runs; plan to scene, with residents). The scene has 1,186 renderers after merging (draw budget 1,500), 3.08 M triangles, 54 lights and 357 colliders. Mean frame time is 3.4–6.0 ms (95th percentile 3.8–6.4 ms) at the arrival, the square and the keep. Editor play mode, 1920×888 game view, not a player build.
- **Bottlenecks:**
  - validation twice per day (by design);
  - plan generation's road × plot search (fine at town size; a city would need an index);
  - night lights, capped.

## K. Screenshots

Location: `Documentation/Screenshots/Phase10/` (JPEG, from 1920×1080 PNG captures taken in the shipped scene by
`Phase10CaptureTests`). Every one was inspected.

| File | Demonstrates |
|---|---|
| `p10_01_town_arrival.jpg` | Arrival on the main road, which follows the ground; the walls and a gatehouse ahead; roofs toward the keep's hill. |
| `p10_02_market_square.jpg` | The levelled market square: well, stalls, the tavern facing the square, shops, market streets. |
| `p10_03_district_street.jpg` | A district lane: houses on plots facing the street, stone plinths where the ground falls, the wall beyond. |
| `p10_04_bridge_and_water.jpg` | A timber bridge with railings over the town's stream in its cut channel; the wall behind. |
| `p10_05_keep_on_its_hill.jpg` | From the square toward Keep Hill: the ground rising behind the stalls, large houses on its slope, the wall beyond. The keep itself is hidden behind them in this frame. |
| `p10_06_farms_and_livestock.jpg` | Fenced pastures with their herd (placeholder animals), fields, a farmstead. |
| `p10_07_first_person_square.jpg` | First person in the square, looking at the tavern. |
| `p10_08_village_green.jpg` | Mossholm from its green: lanes up to the landmark on its rise. |
| `p10_09_hamlet.jpg` | Westerthorpe from its green, its watchtower on the rise. |
| `p10_10_strategic_map_and_identity.jpg` | The strategic map's Bramhaven drawn from its plan (the wall circuit, the keep, the buildings), with the inspector's identity of the place. |
| `p10_11_npc_character_sheet.jpg` | A guard's character record in the inspector: archetype, Fighter 2, hit points, armour class, abilities, saves, skills, equipment, features. |
| `p10_12_outing_and_bestiary.jpg` | An outing from the HUD (an ambush on the town's edge), with the creature's bestiary entry in the combat inspector. |
| `p10_13_reaction_prompt.jpg` | The Shield prompt: trigger, cost and effect, with Accept and Decline above every column. |
| `p10_14_reaction_resolved.jpg` | Shield accepted: AC 17, the attack misses, explained in the result and the log. |
| `p10_15_outing_settled.jpg` | The outing over, with its result and log. |

**Visual limitations:**
- Herds and creatures are placeholders.
- The kit draws every landmark function as a tower (the village's chapel looks like a tower).
- Lane surfaces in the village and hamlet read greenish.
- Water is a flat tinted ribbon.
- Strategic-map fields are small at map scale.

## L. Known limitations

`Phase10/Limitations.md` has the full list. By status:

- **Functional:**
  - NPC records and sheets, promotion, experience and advancement;
  - the bestiary and its traits;
  - wildlife and herds (aggregate);
  - the ten reactions listed;
  - world encounters;
  - settlement identity, plans, persistence, routing, scene and strategic map.
- **Partial:**
  - Shield and Hellish Rebuke (not against spells);
  - building looks (by kit, not by function);
  - water (no collision);
  - herds (static placeholders);
  - the performance probe (its poses still describe the old town).
- **Data-only:** fields' crop names and herd keepers (stored and shown, not simulated beyond counts).
- **Deferred:**
  - plan growth;
  - creature models;
  - automatic promotion;
  - travel encounters on the strategic map;
  - reactions on misses;
  - asking allies;
  - city-size plans.
- **Owner decisions open:**
  - the Bible / Product Foundation scope difference (recorded in both, not resolved);
  - whether to replace the five original animals.

## M. Git

- **Branch:** `feature/phase-0-1-foundation`.
- **Base:** `27a5261` (Phase 9, local; `origin` at `2fe83eb`).
- **New commit:** the commit that contains this report: "Phase 10: bestiary, NPC character systems, encounter intelligence and living settlement generation" (`git log -1` on the branch; the hash is in the handoff message). Local only, not pushed.
- **Working tree after the commit:** only `ProjectSettings/GraphicsSettings.asset` is modified: the owner's change,
  intentionally left unstaged and uncommitted.
- **Ignored:** developer screenshots under `Documentation/Screenshots/dev-*.png`.

## N. Phase 11 recommendations

The natural next step is items, the phase the owner has already briefed: **Phase 11, Inventory, Loot, Equipment,
Crafting & Item Progression.** Phase 10 left hooks for it:
- creature loot profiles and encounter loot keyed per encounter;
- NPC gear on sheets;
- the settlement's crafts in its plan (smithy, workshop, mill, granary).

After items, the strongest candidates are:
- settlement growth (versioned plan edits);
- travel and encounters on the strategic map;
- creature and animal models.
