# Phase 18 completion report: World Depth, Exploration, Living Content & Gameplay Expansion

Date: 2026-11-12. Branch `feature/phase-0-1-foundation`. Committed locally, **not pushed** (the brief: "Do not push to origin"). The commit hashes are in section 26 and in `git log`.

Status words used throughout:

| Word | Meaning |
|---|---|
| **Implemented** | built, covered by passing automated tests, and reachable in the shipped scene |
| **Partial** | some of it is built; the rest is named |
| **Deferred** | not built, with the reason |
| **Unverified** | built and tested in code, but not seen working in hands-on play |

Nothing in this phase was hand-played (section 22).

## 1. Starting commit and branch

| Item | Found |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| HEAD | `29d43f2` (Phase 17 docs), on top of `8f36025` (Phase 17); both local, not pushed |
| Working tree | Clean of tracked changes. `ProjectSettings/GraphicsSettings.asset` is unmodified (committed earlier, in `48b0098`). |
| Ignored, local-only and preserved | The Quaternius Bestiary (QAL), the development screenshots in `Documentation/Screenshots/dev-*.png`, Unity's `Assets/InitTestScene*` files |
| Save schema | v19 |

## 2. Preflight findings

The full audit is `Reports/Phase18-Preflight-Audit.md`. In short:

- **Places.**
  - Each world held one instance of each of ten place definitions, whatever its size.
  - Further regions held no places at all.
  - The only mechanism was "pull to open".
- **Discoveries** were a flat list of key and text. Many clues recorded nothing, and nothing in the world cared what the player found.
- **Quests.** Of 22 quests, 18 were linear; only 4 had a decision.
- **World events.** Ten kinds were raised, but only two became stories.
  - **Bug:** a caravan's loss is raised and resolved in the same moment, while the story pass read only open events. So the Phase 17 lost-caravan story could never appear in play; its test raised an open event by hand.
- **Factions.** Conflicts had no stages between "rivals" and the blockade, and the player had no part in them.
- **People.** Personality changed dialogue but not what people did, apart from going to the tavern. People never visibly talked to each other.
- **Creatures** were capsules, and settlement trees were spheres on sticks.

## 3. Systems expanded (no new foundational system)

Every addition uses an existing system. There is no second world model, quest system, NPC system or combat system.

| Area | Expanded through | ADR |
|---|---|---|
| Places | existing archetypes, generator and place actions, plus a finishing step on the layout | 0089 |
| Discoveries | the existing social knowledge record, plus seven optional fields | 0088 |
| Quests and stories | the existing quest engine and catalogues, plus two consequence kinds and one condition kind | 0091, 0093 |
| Factions | existing relations and world events (stage stored as severity) and the existing activity system | 0090 |
| NPCs | the existing activity plan and knowledge | 0094 |
| Companions | the existing companion stories and remarks | 0095 |
| Rewards | the existing reward and ledger keys | 0092 |
| Art | the existing art catalog, combat controller and import postprocessor | 0097 |

## 4. New content delivered

| Content | Count |
|---|---|
| Places, on existing archetypes, in every region | 4: Greywater Drift, the Ringers' Vault, Hollin Tower, Cairn Watch |
| Puzzle patterns | 3: a sequence, staged water control, raking light |
| Authored quest chains | 4, of 2 quests each (8 quests) |
| New world-story kinds | 5, making 7 in all |
| New companion stories | 2, making 4 in all |
| New items | 4: two evidence items and two tools with uses |
| Quest definitions | 37 in all (the preflight audit found 22) |

All text was written for Realmkeeper. The places' histories are local and fix no canon.

## 5. New quest chains (ADR-0091)

| Type | Chain | Ways to end |
|---|---|---|
| Investigation | Ashes in the Record-House → The Reeve's Accounts (Hollin Tower) | the reeve exposed, made to restore what was taken, or bought off |
| Faction | The Cairn Question → Who Keeps the Cairn (Cairn Watch) | back the claim, uphold the holder, mediate under the old charter, or stay aside |
| Wilderness/dungeon | The Drowned Drift → Greywater's Future (Greywater Drift) | reopened (only once drained) or sealed as a grave |
| Settlement/economy | The Miller's Measure → A Mill for Two | measure restored by persuasion, the miller judged right, or the shortfall supplied |

- **Consequences.** Outcomes change stability, prosperity, food, faction influence and opinion, regard, memory and notoriety, through the existing consequence paths.
- **Tested.** Every decision with at least two outcomes is tested through conversation, with a save and load in the middle.
- **Status: Implemented.** None was played through by hand. Cairn Watch exists only where two factions exist.
- ***The Waystone Count* stays provisional and non-canonical.** No arc or chapter was added. Its additions:
  - What the player found at Cairn Watch, or the ringers' verse, can make the case for a shared count.
  - "Witness" is a fourth way to settle the toll-keeper's count.

## 6. New emergent story types (ADR-0093, ADR-0090)

| Story | Cause | Who asks |
|---|---|---|
| Room at the Hearth | refugees arriving | the town they reach |
| Hard Words in {town} | local unrest | the unhappiest worker |
| The Long Way Round | trade cut off on a road | a merchant at each end |
| Word for a Friend | a recent death | a neighbour of someone with a real friend elsewhere |
| Bad Blood at the Crossing | a faction conflict at dispute or worse | the faction's town |

- **Lifecycle:**
  - one story per cause, ever;
  - one new a day, five open at most;
  - causes taken by priority, then by event number;
  - offers lapse after 10 days.
- **Lifecycle fix:** a caravan loss resolved within 14 days still counts.
- **Goods come back** when the band that took them is broken: half reaches the town they were bound for, once.
- **Audit:** a test asserts that every kind of world event has follow-up gameplay.

**Status: Implemented.** None was triggered by hand play.

## 7. Exploration and location additions (ADR-0088, ADR-0089)

- **Four places per region**, sited deterministically, once per region; old saves gain them on load.
- **A finishing step** wires each mechanism to what it works, and rejects layouts whose puzzle cannot be solved. Tested on 60 seeds per place.
- **The patterns:**
  - **Bells** in a verse's order.
    - A wrong bell costs a Dexterity save and resets the sequence.
    - A second wrong peal wakes the dead.
    - The door can still be picked or forced at DC 22.
  - **A pump drained in stages.**
    - Clear the intake (Athletics), then FULL → HALF → DRY.
    - A choked intake cracks the barrel until it is mended.
    - The gallery can still be swum.
  - **Raking light** on a scored lintel, which reveals the hidden way. A thorough search also finds it.
- **Discoveries:**
  - ten kinds, used three ways (journal grouping, who cares, which companions remark);
  - confirmed or unverified, with proof;
  - every clue records;
  - 25 XP once for major findings;
  - told to those who care, rewarded once per settlement.
- **Older places.** Bramble Cave and the Broken Hall gain story features in new worlds.

**Status: Implemented.** The bells and the journal were also driven through the real UI by an automated PlayMode test. **Partial:** the KayKit dungeon kit dresses features (chests, crates, lecterns, anvils, workbenches, beds, candles), but walls and floors are still the built ones.

## 8. NPC improvements (ADR-0094)

- **Natures in the day:**
  - discipline moves working hours;
  - curiosity goes to market;
  - compassion hands out bread in a hungry town;
  - the timid stay in during unrest;
  - prosperity sets tavern-going.
- **Overheard exchanges** between people who know each other and stand together. They say what they really know, worded by certainty; rivals argue. Lines appear above pairs within 14 m, and the two turn to each other.

**Status: Implemented in code and tests. Unverified on screen:** the capture found no pair together at the moment it was taken (section 20). **Deferred:** sitting poses, because the streets and interiors have no seats.

## 9. Faction improvements (ADR-0090)

- **Conflict stages** from the relationship: peace, tension, dispute, skirmishes, open conflict.
  - Changes are noted, and the crossing's towns are told.
  - From dispute up, border towns lose stability weekly.
  - Open conflict is the Phase 16 blockade.
- **Factions act without the player:**
  - relief for a hungry town (40 food for 50 coin, weekly);
  - garrisons from skirmishes up (30 coin a week, for 2 stability).
- **The player's part:** back a side, back the other, mediate (Persuasion, or freely at standing 20+ with both), stay neutral, or ignore it (it lapses).

**Status: Implemented.** Not observed in hand play.

## 10. Companion improvements (ADR-0095)

- **Reactions:**
  - quest outcomes (trait leanings, once per quest);
  - major discoveries of kinds they care about;
  - hardship at home (weekly per town);
  - banter between companions who like each other (weekly per pair).

  The passing kinds wait out a 6-hour cooldown.
- **Stories:** Going Home and Words at the Grave. Unfinished Business now needs a real quarrel.
- **AI fix found by inspection:** followers refuse undrained deep water and say why.

**Status: Implemented in code and tests. Unverified in play:** formation, waiting, regrouping and transitions (owner checklist).

## 11. Rewards and progression (ADR-0092)

- **Tools with uses:**
  - the pump-wright's wrench mends the pump;
  - the shuttered lantern reads the lintel and counts as a light.
- **Evidence items** used in decisions: the old watch charter and the reeve's true ledger. A third candidate, a ringer's hand-bell, was dropped because it had no use.
- **Every new award is keyed and given once:**
  - experience for major discoveries;
  - sharing a finding;
  - companion outcome reactions;
  - recovered goods;
  - weekly strain, relief and garrisons;
  - quest rewards.

  Tests assert that a second attempt gives nothing.
- **Balance.** A major finding gives 25 experience, against 100–300 for an authored quest. No new weapons or armour were added, so equipment balance is unchanged.

## 12. Assets and animations (ADR-0097, `Reports/Phase18-Asset-Research.md`)

The owner approved CC0, and later CC BY with credit. Licences are recorded in each pack's folder, and credits in `CREDITS.md` and the in-game Credits.

| Pack | Licence | Used for |
|---|---|---|
| KayKit Dungeon Remastered, KayKit post lantern | CC0 | place features |
| Quaternius animals and monsters | CC0 | 14 animated creature models, with idle, walk, run, attack, hit and death |
| Quaternius farm animals (sheep) | CC0 | 1 animated model |
| Quaternius Fantasy Props MegaKit, Stylized Nature MegaKit, settlement props | CC0 | trees, wells, place props |
| Poly by Google black bear, boar, goat | CC BY 3.0 | 3 static models that tip over when they fall |

- **Mapping.** 19 creatures are drawn with 15 models, some scaled or tinted.
- **Conversion.** GLB to FBX with the owner's Blender (`Tools/Blender/`). Clips are trimmed to the ones driven, and `Art/ThirdParty` is about 228 MB (was 284).
- **Fixed after the captures exposed them:**
  - every creature faced backwards;
  - a stray helper sphere showed as a grey rock under each Quaternius model;
  - the sheep was 4.4 m tall;
  - the giant, goblin and Poly animals were untextured;
  - the Poly bear and boar stood on end, because placing a body replaced the model's own root turn. The game code had the same fault; it is fixed in `CombatantBodies` and `CombatantAnimator`.
  - The nature kit's leaves showed black edges; they are now alpha-clipped.

**Status: Implemented** and checked in the captures (`p18_12`, with logged heights). **Deferred:**
- full dungeon walls and floors from the kit;
- dormant creature poses;
- more varied combat animations (workstream D).

## 13. UI/UX improvements (no redesign)

- **The journal's Discoveries tab:** grouped by kind; ◆ for major; [unverified]; where it was found and told. Long lines now wrap; the capture showed them clipped.
- **In a fight inside a place,** the place card and the last room text step aside. The capture showed them overlapping the combat bar and the combatant card.
- **The condition and rest panel** in places now sits below the top HUD, instead of tucked under it.

Workstream A (the full UI/UX overhaul) is not started.

## 14. Save schema changes (ADR-0096)

**Schema v20.** A discovery gains optional fields: category, certainty, place, subject, major, confirmed-by and shared-in. The discovery list keeps 400 (was 200), dropping the oldest minor finding first.

No other structure changes:
- mechanism state is in each place's existing `Variables`;
- recovered goods and strain are in each event's applied keys;
- conflict stages are each event's severity.

## 15. Migrations

`MigrationV19ToV20` is structural and writes nothing. A v19 discovery reads as uncategorised, confirmed, not major and told nowhere, which is exactly what it recorded.

Tested:
- a v19 save loads, gains its places and chains once, and keeps every older place;
- loading again adds nothing;
- the strip chain from every older schema gains `StripToV19`.

## 16. ADRs

| ADR | Decision |
|---|---|
| 0088 | Discoveries have a kind, a certainty, and people who care about them |
| 0089 | Places in every region, and three complete mechanism patterns |
| 0090 | Faction conflicts move through stages, factions act without the player, and the player can take part |
| 0091 | Four quest chains, and *The Waystone Count* deepened without changing what it is |
| 0092 | Rewards are tools with uses, and every reward is given once |
| 0093 | Every kind of world event has follow-up gameplay, and the story lifecycle is audited |
| 0094 | People's days show their natures and their town's condition, and they talk to each other about what they know |
| 0095 | Companions react to more of what happens, keep quiet in between, and have more stories |
| 0096 | Save schema v20 |
| 0097 | Third-party creatures and scenery come in through one pipeline, licence first |

## 17. Tests and exact results

| Suite | When | Passed | Failed | Skipped | Inconclusive | Timed out |
|---|---|---|---|---|---|---|
| .NET (Release), all EditMode tests | final, after every change | **830** | 0 | 0 | — | 0 |
| Unity EditMode, full | before the last round of fixes | **827** | 0 | 0 | 0 | 0 |
| Unity PlayMode, full | before the last round of fixes | 113 | **1** | 30 | 2 | 0 |
| Unity PlayMode, targeted | final: capture, Phase 18 UI test, Phase 15 art tests | **8** | 0 | 0 | 0 | 0 |

- **The EditMode run** took 39.6 min.
- **The one PlayMode failure** was a Phase 15 expectation that goblins and wolves have no model without the local Bestiary. They now have repository models (ADR-0097), so the test was updated to the new truth:
  - goblins and wolves use the repository models;
  - an unknown creature still falls back to the placeholder;
  - with the local install, the repository goblin comes first.

  It passed in the targeted run.
- **The 2 inconclusive** are existing dice-dependent `Assume` guards: the Phase 11 loot fight was not won with a foe slain, and the Phase 9 spell test's fight ended early. Neither failed.
- **The 30 skipped** are the capture sets, which run only with `RK_CAPTURE_DIR`.
- **Not rerun in full in Unity.** At the owner's direction, after the last fixes the full Unity suites were not run again (about an hour). Those fixes are:
  - the bounded per-period event keys;
  - the three UI fixes;
  - the capture changes;
  - three new tests.

  They are covered by the final .NET run (830, including the 3 new tests) and the targeted Unity run.

**Phase 18 tests (39, all passing in .NET):**
- Phase18ExplorationTests: 9;
- Phase18QuestTests: 9;
- Phase18WaystoneTests: 3;
- Phase18StoryTests: 7;
- Phase18WorldLifeTests: 8;
- Phase18PerformanceTests: 3;
- PlayMode: Phase18PlayModeTests (the bells rung through the UI, the door kept across a save, the journal) and Phase18CaptureTests.

**Brief scenarios 1–67:**

| Scenarios | Coverage |
|---|---|
| Exploration 1–10 | Covered by the exploration tests: 60 seeds per place, persistent and failed puzzle states, save/load, journal |
| Quests 11–22 | Covered by the quest and Waystone tests: chains, sequences, alternatives, failure on the giver's death, rewards once, save/load mid-quest |
| Emergent stories 23–34 | Covered by the story and world-life tests. The surplus case (26) is the Phase 17 "grain down the road" test. |
| NPCs 35–41 | Covered by the world-life tests |
| NPCs 42–43 (movement, simulation tiers) | Earlier phases' tests; unchanged code |
| Factions 44–51 | Covered by the world-life tests |
| Companions 52–54 | Covered by the world-life tests |
| Companions 55–60 | Partly. Hazard avoidance (57) has a new test. Party movement, combat, wait and regroup, and transitions rest on earlier phases' tests and are on the owner checklist. |
| Rewards 61–67 | Covered by the exploration and quest tests (once-only awards, ownership, tools used), except balance (65), which was reviewed, not tested |
| Regression | The full suites above |

## 18. Performance comparisons

Measured back to back on the same machine: the Phase 17 code (`git archive HEAD`, built separately) and the Phase 18 code, two runs each, on the same six-region world. Figures are ranges over the two runs.

| Measure | Phase 17 | Phase 18 |
|---|---|---|
| Simulated day (connected world) | 16.0–18.2 ms | 19.4–21.7 ms |
| Simulated day (campaign fixture, with a campaign) | 7.4–7.9 ms | 6.4–7.8 ms |
| Story pass | 0.61–0.75 ms | 0.70–0.72 ms |
| Dialogue view | 0.51–0.59 ms | 0.56–0.59 ms |
| A decision with consequences | 5.3–5.4 ms | 6.1–6.4 ms |
| Save | 68–131 ms | 63–132 ms |
| Load | 297–395 ms | 286–457 ms |
| Save size | 5033 / 5080 KB | 5562 / 5609 KB (+10%) |

**New measures:**

| Measure | Result |
|---|---|
| Layout generation, finishing step included | 0.05–0.07 ms a place |
| A town's overheard exchanges | 0.48 ms |
| Which findings a person cares about | 0.42 ms per person (once per conversation) |

- **Day cost.** On the connected world, a day costs about 2–4 ms more (+15–20%), within run-to-run noise of about 25%. On the campaign fixture, it is unchanged.
- **Load** varies widely between runs.
- **The save** grows 10%, with 24 more places and their quests on six regions.
- **Bounds:**
  - discoveries 400;
  - stories 5 open, 1 a day;
  - companion remarks 12;
  - per-period event keys, one per prefix (a fix in this phase: long conflicts had grown one key a week, and Phase 16 patrols one a day).
- **Nothing is evaluated per frame**, apart from the exchange lines, which refresh every 2 s.
- **Unity frame time** was not profiled.

## 19. Determinism

- `TheSameWorld_GivesTheSamePhase18Places_Chains_AndTenDays` builds the same world twice. It checks the same Phase 18 places, seeds, rooms and features; the same quest bindings; and the same world fingerprint after ten days.
- The place tests regenerate 60 seeds per place deterministically.
- Phase 17's determinism test still passes.
- Every award is keyed and given once, and tests assert that a second attempt gives nothing.
- Domain and Simulation still do not reference UnityEngine.

## 20. Screenshots

There are 11 genuine Unity captures in `Screenshots/Phase18/`, with the capture log and inspection notes. They were taken automatically in the editor by `Phase18CaptureTests`, and each was looked at:
- settlement scenery;
- the overheard-exchange attempt (no pair was in earshot);
- the cracked pump;
- a place fight (the panels step aside; the spider is out of frame);
- the bells and verse, a wrong bell, and the door opened;
- the raking light;
- the Discoveries journal;
- the drift's quest-giver in conversation;
- the creature line-up.

The drained-gallery shot was dropped because its camera showed only fog.

Earlier captures in this phase found and led to fixes for:
- backwards creatures;
- grey rocks;
- an oversized sheep;
- untextured models;
- tilted static animals;
- black leaf edges;
- overlapping combat panels;
- clipped journal lines.

**Not captured:** emergent opportunities, faction activity, companion interactions, rewards and regional travel. They are covered by tests only.

## 21. Gameplay recordings

**None.** No screen recorder is installed (no ffmpeg, no Unity Recorder), and the test runner cannot record. Downloading a recorder would need the owner's permission.

## 22. Manual gameplay verification

**No hand-played session took place.** Claude drives Unity only through test runs. The four kinds of evidence:

| Kind | What exists |
|---|---|
| Automated tests | Section 17 |
| Automated UI interaction | PlayMode tests pressing the real controls: the bells, the journal, and the capture's walking, acting and fighting |
| Manually inspected gameplay | Claude inspected each captured screenshot. These are images of automated runs, not play. |
| Human playtest feedback | None yet. `Testing/Phase18-Owner-Playtest-Checklist.md` gives scenarios A–E with exact steps. |

## 23. Known limitations

- Not hand-played, and no video.
- Dungeon walls and floors are still the built shapes.
- No sitting poses.
- Companion movement and regrouping are not checked in play.
- Cairn Watch exists only where two factions exist.
- The drained gallery could not be framed by the capture camera.
- The overheard exchange was not caught on screen.
- *The Waystone Count* is the only campaign, and it is provisional.
- Road fights are still staged on the near settlement's land.
- Travel is on foot only.

## 24. Deferred work

- Full KayKit dungeon walls and floors.
- Dormant creature poses.
- Sitting poses.
- Richer combat animation.
- Workstreams A–D: the UI/UX overhaul, advanced character creation, mature content, and further work. These are kept on the roadmap, not started and not reordered.

## 25. Roadmap changes

- Phase 18 is marked done (local), and Phase 19 is not started.
- New tables: a Phase 17 carry-forward table, and the Phase 18 known limitations.
- Workstreams A–D are reaffirmed as required future milestones.

## 26. Git status and commit details

- **Committed locally on `feature/phase-0-1-foundation`**, on top of `29d43f2`, in two commits: implementation `713b9a0` (code, tests, assets and tools), then documentation, the commit that holds this report (its hash is in `git log`; a commit cannot contain its own hash).
- **Not pushed.**
- **Not staged and left as they were:**
  - `ProjectSettings/GraphicsSettings.asset` (unmodified);
  - Unity's `Assets/InitTestScene*`;
  - the ignored local-only Quaternius Bestiary;
  - the ignored development screenshots.

  Nothing was discarded.
- **All committed third-party assets are CC0 or CC BY 3.0 with credit,** as the owner approved.
- **Phase 19 has not started.**
