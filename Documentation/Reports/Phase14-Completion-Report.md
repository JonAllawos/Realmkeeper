# Phase 14: Exploration Integrity, Living Locations & World Reactivity — completion report

Date: 2026-11-11. Branch `feature/phase-0-1-foundation`. Base `cda9da0` (Phase 13, pushed). Phase 14 is committed locally in one commit on top of `cda9da0`. **Not pushed.**

## A. Executive summary

Phase 14 completes the systems Phase 13 introduced, without redesigning them:
- **Line of sight and cover** now exist. One deterministic grid function (`GridSight`) is used by attacks, spells, areas, the AI, witnesses and the combat window. Closed doors and low obstacles from the place come into fights.
- **Conditions outlast fights.** They are stored on the character sheet with a source, duration and remedy. They survive save and load, expire with world time, and carry into and out of fights.
- **Harm uses one rule.** All damage, in fights and out, goes through `DamageRules`.
- **Where you rest matters.** Rest in a place can be refused, or broken by inhabitants who find the sleeper.
- **Crime is a record of what happened.** Witnesses are decided by sight, light and noise; consequences are local and applied once. Crimes can be put right through dialogue. The journal has a Crimes tab.
- **The five archetypes Phase 13 left unused are real places.** Each world gets three to five of them by its own context, with quests, story features and keys.
- **Places react.** Sleepers wake when disturbed, never through a wall. Bridges have a state and can fall. Fire burns for its time. A sluice drains the culverts. Outlaws parley before they fight.
- **People move indoors.** Residents walk their houses along real passages. Followers keep a room of their own and wait where they cannot pass.

Results:

| Check | Result |
|---|---|
| .NET | 681 / 681 passed, 0 warnings |
| Unity EditMode | 681 / 681 passed |
| Unity PlayMode | 93 passed, 0 failed, 20 skipped (captures) |
| Unity compile | 0 warnings |
| Mutants | 16 / 16 caught |
| Screenshots | 19 genuine captures |
| Save schema | v16 |

## B. Initial Git and repository state

As recorded in `Reports/Phase14-Preflight-Integration-Audit.md` §1:
- HEAD and `origin/feature/phase-0-1-foundation` were both at `cda9da0`: 0 ahead, 0 behind.
- The only uncommitted change was the owner's `ProjectSettings/GraphicsSettings.asset` (+3 −6). It is preserved and has never been staged.
- The owner's ignored `Documentation/Screenshots/dev-*.png` and the Unity-generated `Assets/InitTestScene*` were left alone.

The baseline before any change matched Phase 13's report exactly: .NET 629/629, EditMode 629/629, PlayMode 87 passed, 18 skipped, 0 failed.

The Phase 13 documents said "committed locally and not pushed"; Phase 13 was in fact pushed at `cda9da0`. This is corrected in `Roadmap.md` as part of the normal documentation update (no separate commit).

## C. Phase 13 code-level audit findings

The audit (`Reports/Phase14-Preflight-Integration-Audit.md`) read the source, not only the report:
- **No Phase 13 claim was false.** Its "Partial" and "Data-only" limitations were accurate.
- **One finding was not listed there:** a wake-on group could start a fight from a room the player could not see. This is now fixed and tested (`NobodyWakes_ThroughAWall_TheyAwaitThePlayer`).
- **Gaps that set the scope of Phase 14:**
  - no line of sight or cover;
  - out-of-fight harm bypassed resistances and temporary hit points;
  - conditions did not outlive fights;
  - rest ignored where the player was;
  - no crime records;
  - five archetypes were defined but never placed;
  - waking, bridges and fire were data only;
  - residents stood still, and followers had no room of their own.

## D. Architecture and ADR decisions

| ADR | Decision |
|---|---|
| 0067 | One grid function for line of sight, line of effect and cover: corner lines on a doubled integer grid, the attacker's best corner, walls and closed doors hard, creatures and low obstacles soft (half at most) |
| 0068 | Lasting conditions live on the sheet with source, expiry and remedies; one damage rule (`DamageRules`) for every source; rest checked by site, refused or broken by inhabitants |
| 0069 | Crime is a record; who knows is decided by perception (sight, light, noise, stealth against passive Perception); consequences only when recognised, keyed by crime so applied once; news travels by the Phase 4 knowledge exchange |
| 0070 | Indoor presence is derived from each person's plan and the minute (`IndoorSchedule`), never stored; routes over real passages (`PlaceRoutes`) for residents, followers and the map; followers keep a room |
| 0071 | New places reach every world once, chosen by its context; waking only in sight; bridges and fire have saved state |

Layering is unchanged:
- **Domain**: rules and data, no engine (`GridSight`, `DamageRules`, `LastingConditions`, `PlaceRoutes`, crime records, place content).
- **Simulation**: `IndoorSchedule`, discovery of crimes, the road toll.
- **Application**: commands and queries (`CrimeRules`, `RestRules`, `PlaceHarm`, `PlaceFollowers`).
- **Infrastructure**: genesis and persistence.
- **Presentation**: reads queries and sends commands only.

There is no second combat engine and no duplicated authoritative state. Fights copy doors, low cover and lasting conditions in at the start and give them back at the end.

## E. Persistent conditions and damage

See `Systems/Conditions.md`.
- **Persisted:** any condition that can last outside a fight (Blinded, Deafened, Frightened, Poisoned, Restrained, Exhaustion with levels). The source key refreshes a condition and never duplicates it; the validator refuses duplicates.
- **Applied today** by a poison needle and foul air (Poisoned, 60 minutes), a snare net (Restrained until escaped), blinding dust (Blinded, 10 minutes) and deep water (a Constitution save against a level of Exhaustion).
- **Not persisted:** grappled, prone, unconscious, incapacitated, invisible, charmed, stunned, paralyzed and petrified. They belong to a fight and are never kept on the sheet.
- **Diseased** has no rules definition in the project, so none was invented.
- **Expiry:** conditions expire when the clock passes (`SetClock`), and the HUD reports what wore off.
- **Remedies:**
  - Lesser Restoration, castable outside fights;
  - a long rest, for one level of exhaustion only;
  - escape checks, with no soft lock (after three failures the holder works free in ten minutes).
- **Effects:** disadvantage and exhaustion penalties apply to checks and saves outside fights. Restrained blocks moving between rooms, resting and leaving; Blinded blocks reading.
- **Damage:** `DamageRules.Adjust` applies immunity, then resistance (halved, rounded down), then vulnerability, then temporary hit points first. `ApplyToSheet` keeps the last hit point outside fights. Traps, hazards, fire and falls all go through `PlaceHarm` and record `HarmTaken` in the Chronicle.

## F. Rest and recovery

`RestRules.Check`:

| Site | Rest |
|---|---|
| Settlement | Lodging |
| Map | Camp |
| A room in a place | Allowed without live hazard, foes in sight or private use |
| An interior | Refused |
| While restrained | Refused |

With inhabitants still in the place, a long rest is broken halfway: the group finds the sleeper and the fight begins in the player's room. A long rest lowers exhaustion by one and lists what is still in force. The HUD's rest line says what resting here would mean.

## G. Line of sight and cover

`GridSight.Cover` draws four lines from each corner of the attacker's square to the target's corners, and uses the attacker's best corner.

| Walls block | Cover | Bonus |
|---|---|---|
| 0 lines | None | — |
| 1–2 lines | Half | +2 AC and Dexterity saves |
| 3 lines | Three-quarters | +5 |
| 4 lines | Total | Cannot be targeted |

- Grazing a wall's edge is clear. Squeezing between diagonal walls or along a seam closed on both sides is not.
- Creatures and low obstacles give half cover at most.
- Different floors are total cover.
- The result is the same both ways and every time: whole numbers only.

The combat window draws walls, doors and low cover, fades figures out of the player's sight, and shows each foe's cover in its row and its square's tooltip.

## H. Ranged combat and spell targeting

- **Attacks:** an attack needs line of effect. Total cover refuses it, with the reason given. Cover is added to AC, named in the breakdown and the log, and returned in `AttackResult.Cover`.
- **Spells:** a spell needs a clear path to its target and to its point. An area spreads from its origin only to squares the centre line reaches; walls and closed doors stop it, creatures do not. Dexterity saves get the cover bonus unless the spell ignores cover (Sacred Flame).
- **The AI:** it uses the same checks. A shooter without a line closes in along a real route (`PathAround`, which now accepts only a goal in reach with line of effect and stops when already placed). It never shoots through walls.
- **Doors in a fight:** opening or shutting a door is an action (`DoorAction`), and the door states go back to the place afterwards.

## I. Crime, witnesses and reputation

See `Systems/Crime.md`.
- **What is recorded:** stealing from household or shop containers, quietly or openly; breaking in at night; trespass after a warning; assault (recorded before the first blow) and murder; property damage.
- **Witnesses:**
  - Each must be alive, on the same floor, within 60 ft, in the room or in sight through open ways, with line of sight through the walls, and with light (or the act must be loud).
  - Sleepers wake only to noise.
  - A quiet theft beats each witness whose passive Perception is below the Sleight of Hand total.
- **Only when recognised:** a bounty, a once-only reputation loss (settlement, victim, faction) and notoriety.
- **Otherwise:** only a description goes round. Unseen thefts and break-ins are found out the next day with no culprit.
- **Consequences:** news travels by meeting. Merchants who know refuse trade. Anyone who knows can settle: goods back, the rest in coin.
- **In the UI:** the journal's Crimes tab, and Chronicle events 83–85.

## J. Five integrated location archetypes

| Place | Archetype | Story and quest |
|---|---|---|
| The Ashgrave Catacombs | `catacomb` | A sealed vault with sleepers, the founders' register — *The Founders' Register* |
| The Lantern Fane | `temple` | A burning room, the fane's lantern — *The Lantern of the Fane* |
| Crowfoot Den | `bandit_hideout` | Outlaws who parley and tax the trade road, a strongbox and its key — *Tolls on the Road* |
| The Old Culverts | `underground` | A damaged bridge, deep channels, a sluice wheel — *The Old Culverts* |
| Thornwick | `abandoned_settlement` | A flooded-out hamlet under the open sky |

Placement:
- Each world gets three to five, chosen by its context (`PlaceGenesis.EnsurePhase14`; the same seed chooses the same). A place that cannot be sited gives way to the next eligible one.
- Old saves gain them once and keep everything else.
- `GeneratorVersion` 2 adds forced loops for bridges (with a side-room fallback), fire rooms, keys (turning an opening into a door where needed), waking groups beside their feature, story loot and items.
- Validated across many seeds per definition.

## K. Interaction-activated creatures

A group with `WakesOn` sleeps beside its feature.
- Opening, taking or using the feature wakes it.
- The fight begins only if the player is in that room or sees it through an open way. Otherwise the group is marked awake and fights on arrival.
- It happens once. The creatures are keyed, so nothing respawns, and re-entering wakes nothing.
- The Chronicle records `CreaturesWoke`.

## L. Bridges and fire hazards

- **Bridges** are Intact, Damaged or Destroyed, and saved per place:
  - Damaged: an Acrobatics check to cross. A failure drops the player into the water (shared harm) and brings the bridge down (`BridgeCollapsed`), and the player is put back on their side.
  - Destroyed: no way for the player, the map, residents or followers.
  - Followers will not risk a damaged bridge.
- **Fire** is a hazard: a Dexterity save against fire damage through the shared rule.
  - It burns 240 minutes from first discovery, then stays burnt out. The state is a recorded minute, with no tick.
  - Rest is refused in a burning room.
  - A torch or lantern burns marked webbing once.
- **The scene** shows firelight (`Flicker`), drained channels, bridge decks and gaps. The place map marks bridges by state and burning rooms.

## M. Indoor NPC movement

- `IndoorSchedule` derives each resident's room and cell from their plan and the minute, and the room they came from. The route goes through real passages (`PlaceRoutes`): residents open shut doors and pass their own house's locks.
- The scene's `PlaceWalker` walks someone who has just moved along that route at 1.4 m/s, never through a wall.
- The query costs 0.1 ms for a building with 24 people.

## N. Follower navigation

- Followers (freed captives, escorts) keep a room. They follow through open ways, wait at locked or barred doors and at bridges they will not cross, and catch up when the way opens. The HUD names them, their room and why they wait.
- Only those in the fight's room join it. Leaving takes only those with the player.
- Save and load keeps where each waits.

## O. Environmental visual improvements

`PlaceEnvironmentBuilder.Phase14`:
- each new archetype gets its own look and dressing (catacomb niches, temple, hideout, culverts, hamlet yards with an outdoor grass mesh);
- places use the CC0 kit's textured materials and props, and the character's own body;
- the live hazard layer: water, a drained channel, fire burning or burnt out, bridge decks, gaps.

No new third-party assets were added.

## P. Quest, journal and map integration

- **Quests:** four new quests are bound to the new places, with dialogues.
- **Journal:**
  - a Crimes tab;
  - the Places tab and the map's list show each place's state (a damaged or fallen bridge, drained, on fire or burnt out, sleepers awake, some have left), next to the Phase 13 discovery state (visited, cleared).
- **Place map:** bridges and fire.
- **Dialogue:** a general line about a known crime, and a settle response.
- **Shops:** refusal over a known crime.
- **HUD:** the conditions panel, with Break free and Lesser Restoration, followers, and the rest line.

## Q. Save schema and migrations

Schema **v16** (`MigrationV15ToV16`):
- world `crimes` and `nextCrimeNumber`;
- the sheet's `conditions`;
- places' `bridges` and groups' `woken`;
- fights' `doors`, `lowCover` and conditions' `lastingKey`;
- trade flows' `lostOnRoad`.

New places are added by genesis on load, once. The validator covers crimes, conditions, bridges and followers (see `Systems/Persistence.md`).

Tests:
- a v15 save with progress keeps the explored rooms, the dead, the player's room and the quest, gains the same new places a fresh world of that seed has, and loading twice changes nothing;
- an exact round trip of every new field;
- doors and low cover mid-fight.

## R. Determinism and transactional safety

- **Determinism:**
  - `GridSight` uses integers only and is symmetric. A test checks the same answer both ways and every time.
  - Place selection and generation are seeded (`SeedMixer`).
  - Rolls go through the existing dice service.
- **Once-only effects:**
  - Waking, bridge collapse, crime consequences and reputation changes are keyed, so they are applied once.
  - Settling, stealing, breaking in and harm run inside the command's working copy, which is discarded unless the command succeeds and validates. A settle the player cannot pay changes nothing.
- **Reward claims:** the Phase 12 reward-claim guard is mutation-tested again (§T).

## S. Testing results

See `Phase14/Testing.md` for every test and the twenty PlayMode flows.

| Suite | Result |
|---|---|
| .NET build | 0 warnings, 0 errors |
| .NET tests | **681 passed, 0 failed** (629 + 52 new) |
| Unity compile | 0 errors, 0 warnings |
| Unity EditMode | 681 / 681 passed, 0 failed, 0 skipped; 0 compile warnings |
| Unity PlayMode | **113 tests: 93 passed, 0 failed, 20 skipped** (the capture tests, which run only with `RK_CAPTURE_DIR`). The 6 new tests cover the 20 flows. |
| Captures | `Phase14CaptureTests` (2 tests, passed): 19 screenshots at 1920×1080 |

Nothing is reported as passing that was not run.

## T. Mutation testing

Each mutant breaks one rule in the source. The .NET suite is then run, and the file is restored. The scripts are kept with the session's scratch files (`mutants/p14.sh`, `mutants/p14b.sh`).

| Mutant | What it breaks | Result | Caught by |
|---|---|---|---|
| p14_cover_half | Half cover gives +0 AC | Caught | `HalfAndThreeQuartersCover…`, `ALowObstacle_OrAnotherCreature…` |
| p14_total_cover | An attack through total cover is allowed | Caught | `ARangedAttack_ThroughAWall…`, `AClosedDoor_BlocksTheShot…` |
| p14_area_walls | An area spell reaches past walls | Caught | `AnAreaSpell_StopsAtAWall…` |
| p14_resistance | Resistance no longer halves | Caught | 4 tests, including `ADwarf_ResistsPoison…` and `OneDamageRule…` |
| p14_last_hp | Harm outside a fight can drop the player to 0 | Caught | `HarmGoesThroughTheOneDamageRule…`, Phase 13's trap test |
| p14_exhaustion | A long rest does not lower exhaustion | Caught | `ALongRest_TakesOneLevelOfExhaustion…` |
| p14_escape | A net can hold for ever (1,000 tries) | Caught | `ANet_Restrains…NobodyIsTrappedForEver` |
| p14_wake_walls | Sleepers wake and fight through a wall | Caught | `NobodyWakes_ThroughAWall…` |
| p14_wake_once | Waking can happen again | Caught (after a new assertion) | `NobodyWakes_ThroughAWall…` |
| p14_witness_wall | Witnesses see through walls | Caught (after a new test) | `AWitness_InARoomInSight_ButBehindItsWall…` |
| p14_bounty | A bounty on an unrecognised witness's word | Caught | `AStranger_WhoDoesNotKnowYou…` |
| p14_settle | Settling leaves the bounty | Caught | `ATheft_SeenBySomeoneWhoKnowsYou…` |
| p14_bridge | A fallen bridge is still a way | Caught | `ABridge_HasAState_AndAFallenOne…` |
| p14_night | Homes never lock at night | Caught | `AtNight_AHomeIsLocked…` |
| p14_rest_hazard | Rest allowed in a hazardous or burning room | Caught | `Rest_IsRefused_InAHazardousRoom…`, `Fire_BurnsForItsTime…` |
| p12_reward_claim | A quest reward can be paid twice (Phase 12 guard) | Caught | `ARewardAlreadyClaimed_IsNeverPaidAgain…` |

**16 of 16 caught**, but not all on the first pass. In the first pass:
- **Two survived** (wake once, witness through a wall). They exposed real gaps in the tests: a new assertion (opening the vault twice wakes nobody twice) and a new test (a guard in a neighbouring room, behind the wall beside the doorway, does not see the act; in the line through the doorway, they do) now catch them.
- **Four did not build.** Written as `if (false)`, they were rejected by the compiler: unreachable code is an error in this build. That proves nothing about the tests, so they were rewritten with conditions that still compile and run again. All four were caught.
- **One was malformed** (wake through walls left a dangling `else`). It was rewritten and caught.

All sources were checked back to their original state after the runs.


## U. Performance results

`Phase14PerformanceTests` (.NET 8 Release; harness figures, not production):

| Measure | Result |
|---|---|
| Cover check (40×40, 15 % walls) | 20–24 µs |
| Area line | 0.5–1 µs |
| New place generation | 0.12 ms each |
| Indoor schedule (24 people) | 0.1 ms per query |
| Follower route | 0.3–0.4 µs |
| Witnesses | 0.09 ms |
| Conditions apply, query and expire | 0.6–0.7 µs |
| Save / load, 22 places | 817 KB in 55–65 ms / 90–100 ms |

## V. Screenshots and manual verification

19 actual captures from the running game (`Phase14CaptureTests`, 1920×1080, rendered from the scene's camera with its UI), in `Documentation/Screenshots/Phase14/`. The captions are the capture log's own text, cut short here:

| Shot | What it shows |
|---|---|
| `p14_08_locked_for_the_night` | 23:00 at the keep's door (locked for the night) — offered: 1  Pick the lock (Sleight of Hand; breaking in) / 2  Force the door (Athletics; loud, and breaking in) / 3  Leave it |
| `p14_01_lasting_conditions_hud` | The conditions panel in town: Poisoned (a poisoned needle): Disadvantage on attack rolls and ability checks. Ends: Lesser Restoration ends it Exhausted 1 (cold water): Level 1: −2 to every d20 test and −5 ft speed. D |
| `p14_02_rest_in_a_place` | Inside The Fordkeepers' Crypt; the HUD's rest line: "You can rest here, but not unseen: something still lives in this place." |
| `p14_03_combat_grid_walls_cover` | A fight in tomb 2: 802 wall squares, 3 door(s), 3 low cover square(s) on the grid. Cover from you — Skeleton 1: no cover; Zombie 2: half cover (+2 AC and Dexterity saves) from the sarcophagus |
| `p14_04_combat_cover_in_the_log` | Round 3; the order list shows each foe's cover from you. |
| `p14_05_household_before_theft` | House 8, the bedroom: Fenna Oakes is here (Resting) beside the chest. |
| `p14_06_theft_seen` | Taken from the chest: "You take linen tunic and 9 coin. Fenna Oakes sees you (in the same room): "Thief! Stop!" They could not tell who you were." |
| `p14_07_journal_crimes` | The journal's Crimes tab: Took what was in Fenna Oakes's chest — Witnessed; Fenna Oakes saw it (in the same room) but could not say who it was |
| `p14_09_map_new_places` | The map, with this world's 5 new places: Crowfoot Den (Bandit hideout), The Ashgrave Catacombs (Catacombs), The Lantern Fane (Forsaken temple), The Old Culverts (Underground works), Thornwick (Abandoned hamlet). |
| `p14_10_catacombs_inside` | The Ashgrave Catacombs: the entrance, dressed as a catacomb. |
| `p14_11_vault_sleepers_wake` | The sealed vault opened: "As the lid shifts, something wakes! The vault's sleepers in the tomb 7. |
| `p14_12_fane_on_fire` | The Lantern Fane: the quarters burning; "Quarters. Flames lick across the floor. Dexterity save 14 against 12: success. 3 fire damage: 25 → 22 HP." |
| `p14_13_place_map_fire` | The place map: the burning room marked in red. |
| `p14_14_culverts_bridge` | The Old Culverts: a damaged bridge over the channel. |
| `p14_15_crossing_and_place_map` | After crossing: "Acrobatics 20 (16+4) against 12: success. The bridge sways and creaks, but holds. Hall 6. No light reaches here." — the bridge is now damaged. |
| `p14_16_outlaws_parley` | The outlaws talk before they fight: Pay their toll (10 coin) / Talk them off the road / Threaten them / Attack a band of outlaws |
| `p14_17_hamlet` | Thornwick: the yard under the open sky. |
| `p14_18_map_place_states` | Places and how they stand: The Lantern Fane: on fire; The Old Culverts: a damaged bridge |
| `p14_19_follower` | Wenna Fairweather follows into the office 2; the HUD: Wenna Fairweather: with you Rest: A quiet corner: you can rest here. |

Every image was opened and checked against its caption. They show, among others:
- the conditions panel;
- the combat grid with walls, doors, low cover and cover text;
- the burning room in the scene and on the place map;
- the map list with place states.

The combat shot also shows the Phase 8 combat panel covering about half the screen, which is the combat overhaul's starting point.


## W. Known limitations and deferred work

See `Phase14/Limitations.md`. In short:
- **Sight:** per floor only, and light is yes or no.
- **Crime:** bounties are owed sums with refusals, with no pursuit or jail. Street crimes are not recorded.
- **Fire** does not spread.
- **Movement:** residents move indoors only; dungeon inhabitants do not patrol.
- **The combat window** is still the large Phase 8 panel. The **combat overhaul** (the next milestone, already requested) replaces it and builds on this phase's sight and cover.
- **PlayMode:** some flows depend on the shipped world's content and would be skipped, not failed, without it.

## X. Files changed

Excluding the owner's `ProjectSettings/GraphicsSettings.asset` (never staged). The tables below list source and documentation files; Unity's `.meta` files for the new ones are committed with them. Paths under `Assets/Realmkeeper/` are shortened.

| Area (files) | Files |
|---|---|
| `CREDITS.md` (1) | `CREDITS.md` |
| `Documentation/Decisions` (6) | `Documentation/Decisions/ADR-0067-line-of-sight-and-cover.md`, `Documentation/Decisions/ADR-0068-lasting-conditions-shared-damage-rest-rules.md`, `Documentation/Decisions/ADR-0069-crime-witnesses-local-consequences.md`, `Documentation/Decisions/ADR-0070-indoor-movement-and-followers.md`, `Documentation/Decisions/ADR-0071-archetype-placement-wake-bridges-fire.md`, `Documentation/Decisions/README.md` |
| `Documentation/Phase14` (2) | `Documentation/Phase14/Testing.md`, `Documentation/Phase14/Limitations.md` |
| `Documentation/Reports` (2) | `Documentation/Reports/Phase14-Completion-Report.md`, `Documentation/Reports/Phase14-Preflight-Integration-Audit.md` |
| `Documentation/Roadmap.md` (1) | `Documentation/Roadmap.md` |
| `Documentation/Screenshots` (20) | `Documentation/Screenshots/Phase14/`: 19 JPEG captures and `p14_capture_log.txt` |
| `Documentation/Systems` (7) | `Documentation/Systems/Combat.md`, `Documentation/Systems/Conditions.md`, `Documentation/Systems/Crime.md`, `Documentation/Systems/Interiors.md`, `Documentation/Systems/Locations.md`, `Documentation/Systems/Persistence.md`, `Documentation/Systems/RestAndRecovery.md` |
| `Runtime/Application` (18) | `Runtime/Application/CombatQueries.cs`, `Runtime/Application/EventFormatter.cs`, `Runtime/Application/Places/CrimeRules.cs`, `Runtime/Application/Places/Phase14Queries.cs`, `Runtime/Application/Places/PlaceActions.cs`, `Runtime/Application/Places/PlaceCommands.cs`, `Runtime/Application/Places/PlaceEncounters.cs`, `Runtime/Application/Places/PlaceFollowers.cs`, `Runtime/Application/Places/PlaceHarm.cs`, `Runtime/Application/Places/PlaceQueries.cs`, `Runtime/Application/Places/PlaceRules.cs`, `Runtime/Application/Places/RestRules.cs`, `Runtime/Application/PlayerCommands.cs`, `Runtime/Application/Quests/DialogueEngine.cs`, `Runtime/Application/Quests/QuestRuntime.cs`, `Runtime/Application/SpellCommands.cs`, `Runtime/Application/WorldSession.Time.cs`, `Runtime/Application/WorldSession.cs` |
| `Runtime/Domain` (32) | `Runtime/Domain/Combat/CombatActions.cs`, `Runtime/Domain/Combat/CombatAi.Behaviors.cs`, `Runtime/Domain/Combat/CombatAi.cs`, `Runtime/Domain/Combat/CombatEngine.Attacks.cs`, `Runtime/Domain/Combat/CombatEngine.Spells.cs`, `Runtime/Domain/Combat/CombatEngine.cs`, `Runtime/Domain/Combat/CombatModel.cs`, `Runtime/Domain/Combat/DamageRules.cs`, `Runtime/Domain/Combat/GridSight.cs`, `Runtime/Domain/Economy/SettlementEconomy.cs`, `Runtime/Domain/Magic/SpellCatalog.cs`, `Runtime/Domain/Magic/SpellModel.cs`, `Runtime/Domain/Places/InteriorGenerator.cs`, `Runtime/Domain/Places/PlaceCatalog.cs`, `Runtime/Domain/Places/PlaceContent.Phase14.cs`, `Runtime/Domain/Places/PlaceGenerator.cs`, `Runtime/Domain/Places/PlaceModel.cs`, `Runtime/Domain/Places/PlaceRoutes.cs`, `Runtime/Domain/Quests/DialogueCatalog.cs`, `Runtime/Domain/Quests/DialogueModel.cs`, `Runtime/Domain/Quests/QuestCatalog.Phase14.cs`, `Runtime/Domain/Quests/QuestCatalog.cs`, `Runtime/Domain/Quests/QuestModel.cs`, `Runtime/Domain/Rpg/CharacterSheet.cs`, `Runtime/Domain/Rpg/LastingConditions.cs`, `Runtime/Domain/World/Crime.cs`, `Runtime/Domain/World/ItemCatalog.Phase14.cs`, `Runtime/Domain/World/ItemCatalog.cs`, `Runtime/Domain/World/Loot.cs`, `Runtime/Domain/World/WorldData.cs`, `Runtime/Domain/World/WorldEntities.cs`, `Runtime/Domain/World/WorldValidator.Places.cs` |
| `Runtime/Exploration` (11) | `Runtime/Exploration/Application/PlaceEntryHandlers.cs`, `Runtime/Exploration/Presentation/CombatWindow.cs`, `Runtime/Exploration/Presentation/ExplorationController.Phase14.cs`, `Runtime/Exploration/Presentation/ExplorationController.Places.cs`, `Runtime/Exploration/Presentation/ExplorationController.Quests.cs`, `Runtime/Exploration/Presentation/ExplorationController.cs`, `Runtime/Exploration/Presentation/PlaceEnvironmentBuilder.Phase14.cs`, `Runtime/Exploration/Presentation/PlaceEnvironmentBuilder.cs`, `Runtime/Exploration/Presentation/PlaceWalker.cs`, `Runtime/Exploration/Presentation/PlayerHubWindow.Journal.cs`, `Runtime/Exploration/Presentation/Resources/ExplorationHud.uss` |
| `Runtime/Infrastructure` (10) | `Runtime/Infrastructure/Generation/Phase14Quests.cs`, `Runtime/Infrastructure/Generation/PlaceGenesis.cs`, `Runtime/Infrastructure/Persistence/JsonWorldSerializer.cs`, `Runtime/Infrastructure/Persistence/SaveFileDtos.cs`, `Runtime/Infrastructure/Persistence/SaveMigrations.cs`, `Runtime/Infrastructure/Persistence/WorldSaveMapper.Character.cs`, `Runtime/Infrastructure/Persistence/WorldSaveMapper.Combat.cs`, `Runtime/Infrastructure/Persistence/WorldSaveMapper.Crimes.cs`, `Runtime/Infrastructure/Persistence/WorldSaveMapper.Places.cs`, `Runtime/Infrastructure/Persistence/WorldSaveMapper.cs` |
| `Runtime/Presentation` (1) | `Runtime/Presentation/UI/RealmkeeperHud.Places.cs` |
| `Runtime/Simulation` (4) | `Runtime/Simulation/Npcs/NpcSystem.cs`, `Runtime/Simulation/Places/IndoorSchedule.cs`, `Runtime/Simulation/Places/PlaceWorld.cs`, `Runtime/Simulation/Systems/TradeSystem.cs` |
| `Tests/EditMode` (13) | `Tests/EditMode/Combat/GridSightTests.cs`, `Tests/EditMode/Combat/Phase14CombatTests.cs`, `Tests/EditMode/Phase2/Phase2Worlds.cs`, `Tests/EditMode/Phase2/TradeTests.cs`, `Tests/EditMode/Places/Phase14ConditionRestTests.cs`, `Tests/EditMode/Places/Phase14CrimeTests.cs`, `Tests/EditMode/Places/Phase14Fixture.cs`, `Tests/EditMode/Places/Phase14MovementTests.cs`, `Tests/EditMode/Places/Phase14PerformanceTests.cs`, `Tests/EditMode/Places/Phase14PlaceTests.cs`, `Tests/EditMode/Places/Phase14SaveTests.cs`, `Tests/EditMode/Places/PlaceRulesTests.cs`, `Tests/EditMode/Places/PlaceSystemTests.cs` |
| `Tests/PlayMode` (3) | `Tests/PlayMode/Phase13PlayModeTests.cs`, `Tests/PlayMode/Phase14CaptureTests.cs`, `Tests/PlayMode/Phase14PlayModeTests.cs` |


## Y. Commit and Git status

- **Commit:** one local commit on `feature/phase-0-1-foundation`, directly on `cda9da0`, titled "Phase 14: exploration integrity, living locations and world reactivity". A report cannot contain its own commit's hash; `git log -1` shows it, and the hand-off message states it.
- **Staged with:** `git add -A -- . ':!ProjectSettings/GraphicsSettings.asset' ':!Assets/InitTestScene*'`.
- **Left out:**
  - the owner's `ProjectSettings/GraphicsSettings.asset` change, which stays uncommitted and unstaged, exactly as found;
  - the owner's ignored `Documentation/Screenshots/dev-*.png` files, untouched.
- **Pushed:** no. After the commit the branch is 1 ahead of `origin/feature/phase-0-1-foundation`.

## Z. Recommended Phase 15 scope

1. **Combat overhaul first** (already briefed): a compact HUD, point-and-click movement with path preview, a compact action bar, visual feedback. It builds directly on `GridSight`, doors and low cover.
2. **Companions, Parties, Relationships & Party Progression.** It builds on:
   - persistent NPC sheets (Phase 10);
   - follower navigation (this phase);
   - progression and XP, inventory and equipment, combat and spellcasting;
   - dialogue and reputation, quest participation, and the world simulation.

   It covers recruiting from relationships, party orders, shared and individual progression, and companion opinions of the player's deeds. Crime records and witness knowledge already provide the material for those opinions.
3. **Carry forward as dedicated milestones, not incidental work:**
   - the full UI/UX and menus overhaul;
   - advanced character creation and character sheets;
   - mature content with player-controlled settings;
   - systematic research into RPG interface patterns;
   - settlement, faction and civilisation depth;
   - visual, animation, audio, atmosphere, balance and production readiness.
4. **Small follow-ups from this phase:** pursuit and justice for bounties, crimes in the street, darkvision and dim light for witnesses, sight between floors, and dungeon patrols.
