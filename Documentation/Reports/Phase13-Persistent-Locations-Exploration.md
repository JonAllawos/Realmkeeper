# Phase 13 report: persistent dungeons, ruins, interior locations and exploration

Date: 2026-11-11. Branch `feature/phase-0-1-foundation`, on top of Phase 12 (`1e593ae`). Committed locally only; not pushed.

## A. Executive summary

Places are now part of the one world.

- Five wilderness places (a mine, a crypt, a watch fort, a cave and a ruin) are placed when a world is made, or when an older save is loaded. Buildings in every settlement can be entered through their doors.
- Each place has a saved layout, validated before it is used and never regenerated. It also keeps every change made to it: rooms explored, doors opened or broken, secret ways found, traps sprung or disarmed, containers emptied, inhabitants defeated, and people held, freed and brought home.
- The player goes in from the strategic map (once the place is known) or through a door in the street, and walks from room to room. Each move is a validated command that explores the room, meets its hazard, notices what passive Perception finds, catches trespass and starts fights.
- Fights use the one combat engine on the place's own cells, with walls as blocked squares.
- The vertical slice "Lost Below" is complete in .NET and in the running scene: rumour or request, map marker, mine, ledger, ladder, den fight, rescue, escort home, report, return, save and reload.

Results:

- .NET: **629 passed, 0 failed, 0 build warnings**.
- Unity EditMode: **629 / 629 passed, 0 compile warnings**.
- Unity PlayMode: **105 tests: 87 passed, 18 skipped (capture tests), 0 failed**.
- Generation: **2,500 layouts over 10 archetypes and 720 interiors, 0 validation failures**.
- Mutation checks: **12 of 12 caught (after 2 tests were added and 1 mutant was rewritten so it compiled)**.
- Screenshots: **16**, genuine captures from the running game.

## B. Initial Git state and baseline

From `Documentation/Reports/Phase13-Preflight-Integration-Audit.md`:

- **Git:** HEAD `1e593ae`, level with `origin`. Phases 10 to 12 were present. The one owner change is `ProjectSettings/GraphicsSettings.asset`, which was preserved and never staged.
- **Baseline:**
  - .NET: 603 passed, 0 warnings.
  - Unity EditMode: 603 / 603 passed.
  - Unity PlayMode: 100 tests, 83 passed, 17 skipped (captures), 0 failed.
  - Unity warning: one, CS0067 (pre-existing).

## C. Phase 10 integration audit

See the audit, section 3. Summary:

- **Complete:** NPC identities, sheets, schedules, death, memory, the bestiary and populations, encounter consequences.
- **Gap fixed here:** the combat grid had no walls.
- **Deferred:** settlement layout variety (two archetypes are never chosen).

## D. Phase 11 integration audit

See the audit, section 4. Containers had no place in the world; loot from fights was reachable only through Storage. Phase 13 gives place containers and fight loot a physical presence in rooms. Recipes and inventory UI gaps are deferred.

## E. Phase 12 integration audit

See the audit, section 5.

- **Escort, rescue and protect** had matching objectives but no emitters. Phase 13 adds the emitters and a quest that uses all three.
- **Object examination** is generalised.
- **Map markers** now exist.
- **The reward-claim guard** now has a direct test, and its mutant is caught.

## F. Outstanding defects resolved and deferred

| Defect | Resolution |
|---|---|
| Reward-claim guard untested (Phase 12 mutant survived) | Direct test; mutant caught |
| CS0067 (`CombineRequested` never raised) | Dead event removed; the Unity compile now has 0 warnings |
| Docs saying Phase 12 was not pushed | Corrected (Roadmap, Phase 12 report) |
| **Found in Phase 13:** a cornered, frightened creature among walls dashed in place for ever (a 749-round stalemate in the scene) | The AI now turns and fights when it cannot get further away; .NET regression test |
| **Found in Phase 13:** the scene rebuilt every interactable on each clock tick inside a place | Rebuilt only when doors, features, people or loot change |
| **Found in Phase 13:** place events had no Chronicle wording | Eight new event types worded |

Deferred: see `Phase13/Limitations.md`.

## G. Location architecture and data model

See `Systems/Locations.md` and ADR-0062. **Complete.**

- **Model:** `PlaceData` (identity, parent, cell, seed, generator version, saved layout, discovery, faction, threat, state) and `PlaceLayout` (floors, rooms, connections, features, groups).
- **Hierarchy:** world → region → wilderness place, and world → region → settlement → building → interior, each with floors, rooms and features (containers).
- **The player:** `CharacterLocationKind.Place` with place, floor and room.
- **Commands:** enter, enter building, move, traverse, exit, act, spring trap.
- **Integrity:** no duplicate player or NPC. One environment is loaded at a time, and people held, following or posted are absent from their settlements.

## H. Dungeon generation and validation

See `Systems/DungeonGeneration.md` and ADR-0063. **Complete** for the scope described; layouts are rectangular rooms and straight corridors.

- 10 archetypes, generated on a slot grid with a spanning tree, loops, stairs or ladders, secret rooms and contents.
- Validated for overlaps, passages, stairs, reachability, required features, locks, find DCs, clearance and ceilings.
- 2,500 seeds and 720 interiors tested, with 0 failures.
- Deterministic (the same seed gives the same layout; different seeds differ).
- Saved, never regenerated.
- The authored fort uses the same model.

## I. Interior architecture

See `Systems/Interiors.md`. **Complete** for the representative set:

- home, workshop or smithy, store or granary, guardhouse, chapel, civic hall, tower;
- doors in the street;
- planned on first entry and saved;
- people at home shown by activity and talkable.

**Partial:** occupants do not move between rooms, and households do not react to intrusion.

## J. Exploration interactions

See `Systems/Interactions.md` and ADR-0064. **Complete:** fourteen actions with rules checks and reasons for refusal.

- Retries are bounded per visit.
- Darkness and light matter.
- Searches and passive Perception find hidden things.
- Documents and clues record discoveries: the foreman's ledger explains why the mine was abandoned, and the duty roster reveals the fort's side door.

## K. Traps and hazards

- **Complete:** six traps (detect, disarm, save, damage, alarm, reset, salvage) and four hazards in use (foul air, darkness, unstable ground, deep water), with fairness enforced.
- **Partial:** conditions are not kept.
- **Data-only:** fire.

## L. Encounters and creature integration

**Complete:**

- archetype-appropriate inhabitants (a giant spider in the mine's den, rats in its workings, the crypt's dead, goblins in the ruin, a garrison in the fort);
- fights on the place's grid with walls;
- creatures as lasting individuals;
- defeated stays defeated; survivors keep their wounds;
- experience and loot keyed once;
- the loot pile in the room.

**Partial:** no line of sight through walls for ranged attacks.

## M. Quest, escort and rescue integration

**Complete.**

- **New objective kinds:** explore a room, reach a floor, disarm a trap, use a feature.
- **New events:** place discovered and entered, room explored, floor reached, trap disarmed, feature used. Rescued, Escorted and Protected now emitted.
- **The quest "Lost Below":** a resident is held in the mine from the day the world begins, and a neighbour asks for help. The objectives are: learn where the mine lies, enter it, reach the lower level (optional), free the captive, keep them alive (optional), bring them home, tell the neighbour. Failure: the captive dies.
- **The fort's gate:** the `dlg_gate` dialogue (standing, Persuasion or a bribe) gives leave to pass.

## N. Discovery and map implementation

See ADR-0065. **Complete:**

- discovery states Unknown to Cleared;
- rumours give a name and a direction; a second ask gives the way;
- map markers and the strategic map's Places list with ways in;
- the journal's Places tab;
- the in-place map (N), showing only rooms seen.

## O. Persistence and migrations

Schema **v15** (`Systems/Persistence.md`):

- places with their layouts and state, and escorts;
- the player's place, floor and room;
- containers' place and feature;
- fights' blocked squares and their place origin.

`MigrationV14ToV15` adds the lists; the places come with the living world on load. Tested with a v14 document gaining its places once, an exact round trip inside a place after the rescue, and a save and reload inside the mine in the scene.

## P. UI and presentation changes

- A place environment built from the world's view of the place: floors, walls and ceilings with colliders; doors (open, closed, locked, barred, broken); stairs and ladders; the way out with daylight; torches; props for every feature kind; simple figures for people and creatures.
- Interaction prompts with choices and reasons. Keys: E act, R search the room, N the place map, 1–9 choose.
- A place panel (name, floor, room, status: restricted, foul air, no light).
- The combat grid draws walls.
- The strategic map's Places list and markers.
- The journal's Places tab.
- Building doors in the street.

## Q. Testing and fault-injection results

Full results and the list of new tests are in `Phase13/Testing.md`.

| Suite | Baseline (preflight) | Phase 13 |
|---|---|---|
| .NET harness (Release) | 603 passed, 0 warnings | **629 passed, 0 failed, 0 warnings** |
| Unity EditMode | 603 / 603 | **629 / 629 passed** |
| Unity PlayMode | 100: 83 passed, 17 skipped, 0 failed | **105: 87 passed, 18 skipped (capture tests, run only with `RK_CAPTURE_DIR`), 0 failed** |
| Unity compile warnings | 1 (CS0067) | **0** |
| Phase 13 captures | — | 1 test, passed; 16 screenshots |
| Generation | — | 2,500 layouts and 720 interiors, 0 validation failures |
| Mutation checks | Phase 12's reward-claim mutant survived | **12 of 12 Phase 13 mutants caught**; the reward-claim mutant is now caught |

New: 26 .NET and EditMode tests (`PlaceGenerationTests` 8, `PlaceSystemTests` 3, `PlaceRulesTests` 13, `PlacePerformanceTests` 1, and the reward-claim test), 4 PlayMode tests driving the shipped scene (covering all 20 flows in the brief), and 1 capture test.

Fault injection: the layout validator is tested against nine kinds of broken layout, the world validator against eight impossible place states, and ten refused commands are each shown to leave the world unchanged.

What the final run caught: a Phase 10 PlayMode test still counted 19 named people. The fort's garrison makes 24. The count was corrected; the town filter is unchanged because the garrison belongs to no settlement. The full suite was then rerun, and the counts above are from that rerun.

## R. Performance measurements

From `PlacePerformanceTests` (.NET 8, Release, i9-14900HX), and the scene's own log. These are Editor and test-harness figures, not production measurements.

| Measure | Result |
|---|---|
| Generating a layout (500 over 10 archetypes) | 0.15 ms each |
| Validating a 15-room, multi-floor catacomb | 0.06 ms |
| Entering an interior for the first time (planned, filled, entered, left), 40 buildings | 3.5 ms each |
| Scene view of the mine / its map / known places | 0.036 / 0.019 / 0.016 ms |
| A move into a room (command, validation, arrival) | 2.4 ms |
| AI route round a 21-square wall | 1.0 ms |
| Save / load with 45 places (5 wilderness, 40 interiors) | 864 KB in 71 ms / 149 ms; places 220 KB |
| Building a place scene in the Editor | logged on entering ("built in … ms"): 9–12 ms for the mine, 11 ms for the fort, 25 ms for a two-floor interior (the PlayMode log) |

## S. Screenshots and documentation

All 16 are genuine captures of the shipped scene, taken by `Phase13CaptureTests` with the Editor in play mode (not batch mode). Each caption is built from the world's own state at the moment of capture, as written to `Screenshots/Phase13/p13_capture_log.txt`. Unlit rooms are dim by design.

| File | What it shows |
|---|---|
| `p13_01_dungeon_entrance.jpg` | The entrance room of The Hollow Seam: the HUD names the place, the floor and the room; the passages lead on. |
| `p13_02_mine_entrance.jpg` | The Hollow Seam: the mouth of the mine from inside, daylight beyond the way out; timber shoring, the first room. |
| `p13_03_mine_interior.jpg` | Inside the workings: the office, timber shoring and beams (in the room: foreman's ledger). |
| `p13_04_multi_room.jpg` | The place map (N): only the rooms already seen, on each floor, with the passages between them; gold marks where the player stands. |
| `p13_05_vertical_connection.jpg` | The ladder down the shaft to the lower level (taken with the interact key; the floor changes in the HUD). |
| `p13_06_ruin_or_crypt.jpg` | The Fordkeepers' Crypt: the tomb, dressed-stone walls (nothing else in the room). |
| `p13_07_discovery.jpg` | A discovery: the foreman's ledger, read — why the mine was abandoned. It is kept in the journal's Discoveries. |
| `p13_08_hazard.jpg` | A hazard: foul air in the tomb (green haze), met through a Constitution save — the HUD's status line says so. Tomb. The air is close and sour; lamps burn low and blue. Constitution save 6 against 11: failure: 2 poison damage. You are left poisoned for a while. |
| `p13_08b_trap.jpg` | A trap the player noticed: the poisoned needle, drawn once found; Disarm needs the tools and a check. |
| `p13_09_creature_encounter.jpg` | A fight with the mine's inhabitants at the foot of the ladder, on the place's own grid: walls are dark squares nobody can enter; the combat window lists who acts. |
| `p13_10_loot_interaction.jpg` | Loot in the place: the container opened in the Storage section, with Take and Take all — real items with one holder. |
| `p13_11_settlement_interior.jpg` | Inside House 4: the main room, built from the building's footprint and function (3 rooms on 2 floor(s)). |
| `p13_12_npc_inside_building.jpg` | Edda Thorne, at home (Sitting), spoken to through the conversation window: ""Well met. I'm Edda Thorne."" |
| `p13_13_faction_location.jpg` | Watch Fort: the gatehouse and its watch (Neville Oakes); the gate is held until they give leave — by standing, Persuasion or coin. |
| `p13_14_map_discovered_location.jpg` | The strategic map after the giver told the way: a marker where The Hollow Seam lies, and the Places list with its way in. Places the player has not heard of are not shown. |
| `p13_15_after_return.jpg` | Back in The Hollow Seam's den after the rescue: no fight, nobody held, the timber thrown aside — visits 2, state Cleared. |

Documents:

- ADR-0062 … ADR-0066;
- `Systems/Locations.md`, `Systems/DungeonGeneration.md`, `Systems/Interiors.md`, `Systems/Interactions.md`;
- updates to `Systems/Persistence.md`;
- `Phase13/Testing.md`, `Phase13/Limitations.md`;
- `Reports/Phase13-Preflight-Integration-Audit.md`;
- the Bible, ProductFoundation, the Roadmap, the README and CREDITS.

## T. Licensing and asset provenance

- No new art assets and no third-party models. All place geometry, props and figures are generated at runtime from Unity primitives and generated meshes.
- All place, clue, document, trap, hazard and quest text is Realmkeeper's own. Archetypes are generic kinds.
- Checks, saves and damage use the SRD 5.2.1 rules already credited.
- No campaign canon (kingdoms, the protagonist, an antagonist) is established.
- The project remains separate from The Tavern Table.

## U. Known limitations

See `Phase13/Limitations.md`.

- **Partial:**
  - conditions after traps;
  - line of sight;
  - household trespass;
  - occupants' movement;
  - followers' pathing;
  - stairs as interactions;
  - primitive visuals;
  - walking physics not automatically tested;
  - the Hub's character preview empty in places;
  - no travel time.
- **Data-only:** five archetypes not placed in the world, wake-on-feature content, bridges, fire, keys for generated places.
- **Deferred:** settlement variety, recipes, inventory UI, crime, repeatable quests, content-settings screen.

## V. Final Git status and commit hash

- **Branch:** `feature/phase-0-1-foundation`, one local commit on top of `1e593ae` (Phase 12), with the message "Phase 13: persistent places, dungeons, interiors and exploration". A commit cannot contain its own hash; `git log -1` shows it, and it is given in the review message.
- **Not pushed.** `origin` remains at `1e593ae`.
- **Not staged:** `ProjectSettings/GraphicsSettings.asset`, the owner's change, preserved as found, and the Unity-generated `Assets/InitTestScene*` files.
- No history was rewritten, and no reset, clean or discard was used.
- Phase 14 has not been started.

## W. Recommended Phase 14 priorities

1. **Lasting conditions and rest** outside fights, so traps, hazards and poisons carry over.
2. **Line of sight and cover** on the place grid (walls, doorways, pillars).
3. **Crime and witnesses:** household trespass, theft from owned containers, and their reputation consequences, using the existing restricted rooms and ownership.
4. **Place the five unused archetypes** in the world, with their own stories and quests, including wake-on-feature encounters.
5. **Visual pass for places:** modular wall and floor art, irregular caves, light and fog for hazards.
6. **NPC movement inside interiors and places,** and followers who path.
