# Phase 8 report: Combat Foundation, Encounter Engine & Settlement Structures

Date: 2026-11-11. Branch: `feature/phase-0-1-foundation`. Pre-flight: `Documentation/Phase8/PreFlight.md`.

## A. Summary

- **Combat.** Realmkeeper has turn-based combat on a 5-ft grid, following the SRD 5.2.1 rules.
  - **Engine.** A Unity-free domain engine: encounters with any number of sides and reinforcements;
    deterministic initiative and dice; the action economy; movement with difficult terrain and
    opportunity attacks; SRD attacks, damage and criticals; grapple and shove; the 15 conditions;
    death saves, stabilizing and death; a deterministic AI that only proposes.
  - **Driven by commands.** Every action is an application command, persisted in save schema v10
    and validated.
  - **SRD armour and weapons.** These replace the provisional armour class.
  - **Residents fight as themselves.** A resident who dies goes through the shared NPC death
    routine.
  - **The practice fight.** It starts from the settlement HUD and plays through a new encounter
    panel: initiative, a tactical grid, every action with the reason it is unavailable, results
    explained roll by roll, an inspector and a log. Placeholder ground markers stand in the street.
- **Settlement structures.** Every existing place is a functional structure that houses its shop.
  Informational and decorative structures are added by settlement size. All are shown in the
  settlement inspector and as signs in the street.
- **Shipped credits.** The shipped credits now exist: `CREDITS.md`, the Hub's Info section and the
  README.
- **Tests.**
  - .NET: 470/470.
  - Unity EditMode: 470/470.
  - Unity PlayMode: 66 passed, 0 failed, 6 capture sets skipped by design. This includes 4 new
    tests that click through the real panel.
  - 13 mutants, all caught (one only after a test was strengthened).
  - 10 genuine captures.

## B. Pre-flight findings (detail in `PreFlight.md`)

- **Armour class.** The armour class was provisional: worn protection, with no armour categories.
- **No combat.** No combat, encounter, grid, dice or condition mechanics existed. The Phase 7 rules
  engine had roll modes and condition definitions only.
- **Missing credits.** The SRD attribution was required to appear in the shipped credits, but it
  appeared only in documentation. No credits file or in-game credits existed.
- **No structure model.** Settlements had locations (by size) and Phase 5 shops on them, but no
  building or structure model.
- **Old saves.** Saves at v7, v8 and v9 had to keep loading.
- **Unrelated local change.** An unrelated `ProjectSettings/GraphicsSettings.asset` modification
  was preserved. It is not part of this commit.
- **No help panel.** The strategic map has no help panel. The pre-flight plan to put the credits
  there was dropped; they are in the Hub, `CREDITS.md` and the README.

## C. Architecture and decisions

ADR-0030 to ADR-0036 were added and indexed in `Decisions/README.md`:

| ADR | Decision |
|---|---|
| 0030 | Combat is a domain engine driven by commands; time stands still during a fight (time and leaving the settlement are refused) |
| 0031 | Combatants reference persistent identities; the player's vitals stay on the character sheet; residents are never duplicated; their deaths go through `NpcSystem.RecordDeath` |
| 0032 | Counter-based dice per encounter (reproducible after a save); `ActionSequence` refuses stale or duplicate actions; deterministic initiative tie-breaks |
| 0033 | Combat rules configuration from SRD 5.2.1, with the recorded Realmkeeper choices (Chebyshev grid, automatic reactions, no ammunition count, no off-hand attack, resistance then vulnerability, grapple needs a free hand) |
| 0034 | Death saves for the player and residents; creatures die; non-lethal knock-outs; a dying player is stabilized when the fight ends; no resurrection |
| 0035 | Settlement structures built on existing places and shops, each classified Functional, Informational or Decorative |
| 0036 | The chronicle records only the start, the end, people downed and deaths |

Layers: Domain (`Domain/Combat`, `ItemCombat`, `Structures`, validation) → Application
(`CombatCommands`, `CombatQueries`, `StructureQueries`, `Credits`) → Infrastructure (schema v10)
→ Presentation (`CombatWindow`, `ExplorationController.Combat`/`.Structures`,
`RealmkeeperHud.Structures`). Rules detail: `Systems/Combat.md`.

## D. Combat as implemented

- **Encounters.** One active encounter at a time; the last 20 finished ones are kept.
  - **Outcomes:** victory, stalemate, withdrawn, or ended.
  - **Sides:** any number, plus `neutral`.
  - **Joining:** reinforcements can join a fight already running.
- **Combatants.**
  - **Player:** statistics from `CharacterRules` and the equipped loadout.
  - **Residents:** the SRD Guard or Commoner profile, by role.
  - **Creatures:** commoner, bandit, guard and wolf (SRD 5.2.1 statistics; the descriptions are
    Realmkeeper's own).
- **Turns.**
  - **Initiative:** d20 + Dex, with bonuses and roll modes.
  - **Turn resources:** action, bonus action, reaction, movement and free interaction.
  - **Actions:** Attack (Extra Attack counts), Dash, Disengage, Dodge, Stand up, Escape, Stabilize
    and End turn.
  - **Bonus actions:** Second Wind and Cunning Action.
- **Movement.** Steps cost 5 ft, plus 5 in difficult terrain and plus 5 while prone. Enemies block
  their squares. Leaving an enemy's reach provokes an opportunity attack, unless the mover has
  disengaged.
- **Attacks and damage.**
  - **The roll:** advantage or disadvantage from conditions, Dodge, range, adjacent enemies and Pack
    Tactics. A natural 1 misses and a natural 20 is a critical hit; any hit within 5 ft on a
    paralyzed or unconscious target is also a critical.
  - **Damage:** versatile weapons, finesse, and the Archery, Duelling and Great Weapon Fighting
    fighting styles.
  - **Grapple and shove:** a save against 8 + Strength + proficiency.
  - **Defences:** resistance, vulnerability, immunity and temporary hit points.
- **Death.** Death saves, damage taken while down, massive damage, non-lethal knock-outs,
  stabilizing, healing, and a stabilized end for the player.
- **Conditions.** The 15 conditions with their mechanics. Exhaustion stacks to death at 6.
- **AI.** It stands up, retreats when hurt, attacks the weakest enemy in reach, shoots when no enemy
  is adjacent, closes in, or dashes. It is deterministic, and the engine validates its proposals.
- **The panel.** One AI turn runs every 0.6 s, or straight away with Continue. Withdraw and Call
  off (practice only) are available; Close appears once the fight is over. The clock is paused
  during the fight. Movement is locked during the fight and for a dead player.

## E. Settlement structures

- **Catalog.** 19 definitions:
  - 10 functional, built on locations: home, shopfront, smithy, granary, tavern, market, square,
    keep, shrine and guard post;
  - 7 informational: bakery, butcher, carpenter, mason, stable, warehouse and barn;
  - 2 decorative: well and market stalls.
- **Generation.** Deterministic, once per settlement, on world generation and when loading a save
  older than v10. Shops keep their names and keepers.
- **Example.** The test town (Bramhaven) has 24 structures.
- **Shown in:** the strategic inspector's Structures section (role, owner, and what each takes part
  in) and as signs above doors in the street. Informational structures honestly simulate "nothing
  yet". Detail: `Systems/SettlementStructures.md`.

## F. Persistence and migration

- **Schema v10** adds encounters (whole), structures and the sheet's dying state.
- **`MigrationV9ToV10`** adds empty encounters and the default dying state. A v9 save gains
  exactly the structures a new world of its seed would have. Nothing else changes.
- **Tested with:**
  - v9 (Phase 7), v8 (Phase 6) and v7 (Phase 5) documents;
  - an active fight saved, reloaded with an identical world section, and played on identically
    (.NET);
  - a fight saved mid-way in the shipped scene, with its panel returning (PlayMode).
- **Tampered saves are refused:** two active encounters, out-of-range hit points, an unknown
  creature, a broken order, an unknown status, four death-save failures, dead with hit points, an
  unknown structure, two structures on one place, the same resident twice.

## G. Tests (exact)

| Suite | Passed | Failed | Skipped | Notes |
|---|---|---|---|---|
| .NET (`Realmkeeper.Verification.sln`, Release) | 470 | 0 | 0 | 424 before + 46 new (`CombatTests` 28, `CombatIntegrationTests` 12, `StructureTests` 5, `CombatPerformanceTests` 1) |
| Unity EditMode (batch) | 470 | 0 | 0 | 171 s; 0 compiler errors |
| Unity PlayMode (batch) | 66 | 0 | 6 | the six skipped are the capture sets (they run only with `RK_CAPTURE_DIR`); the 4 new `Phase8PlayModeTests` passed |
| Phase 8 capture | 1 | 0 | 0 | 10 screenshots |

- **Mutation testing:** 13 mutants, all caught. The natural-1 mutant survived at first; the test
  was strengthened to check the stated reason, and the mutant is now caught. Full table in
  `Testing/TestResults.md`.
- **Bug found and fixed by the new tests:** in a non-lethal fight, a large hit on someone already
  knocked out could kill them.
- **Earlier tests updated to the intended Phase 8 behaviour** (none weakened or removed):
  - the Phase 7 armour-class assertion now uses the SRD chain shirt;
  - the schema-version assertion uses the current constant;
  - the old-save fixtures strip the v10 fields;
  - the world fingerprint covers structures and encounters.
- **After the PlayMode run:** the ground-marker labels were made smaller and the player's label was
  removed (presentation only). The capture set was re-run and passed; the full PlayMode suite was
  not re-run.

## H. Performance (measured)

| Operation | .NET | Unity EditMode |
|---|---|---|
| Encounter snapshot | 0.08 ms | 0.19 ms |
| Turn command (validated commit) | 0.3 ms | 0.6 ms |
| 40-combatant AI-only encounter to the end | 7 ms, 495 actions (13 µs/action) | 11 ms (22 µs/action) |
| Save / load with an active encounter | 2.7 / 10.4 ms | 2.4 / 49.6 ms |
| Structure generation (with world generation) | 18.8 ms | 57.7 ms |

Not measured: frame time in a development build with the panel open.

## I. Screenshots (`Documentation/Screenshots/Phase8/`, genuine captures by `Phase8CaptureTests`)

| File | Shows |
|---|---|
| `p8_01_inspector_structures.jpg` | Bramhaven's inspector: the Structures section (functional, informational, decorative, with owners) |
| `p8_02_structure_sign.jpg` | A sign above the door of Fairweather's Smithy in the street |
| `p8_03_fight_start.jpg` | The practice fight begins: initiative, grid, markers, paused time |
| `p8_04_ai_turn.jpg` | A computer-controlled turn resolved and explained |
| `p8_05_player_turn_reasons.jpg` | The player's turn: the action economy, the options, the reasons under the unavailable ones |
| `p8_06_inspect_bandit.jpg` | Inspecting a bandit (AC 12, attacks, scores) |
| `p8_07_attack_result.jpg` | An attack explained: d20, bonus, total against AC, damage dice and modifiers, hit points |
| `p8_08_fight_over.jpg` | The fight over (victory after 5 rounds) |
| `p8_09_hub_sheet_after_fight.jpg` | The sheet after the fight (AC from the SRD armour rules) |
| `p8_10_hub_credits.jpg` | The Hub's Info: the SRD attribution and the CC0 art credits |

The capture log (`p8_capture_log.txt`) records each shot and the fight's initiative and outcome.

## J. Limitations, deferred work and licensing

- **Out of scope (as briefed):**
  - spells and concentration;
  - companions;
  - real-time combat;
  - a bestiary;
  - quests;
  - experience from combat;
  - resurrection and medical systems;
  - construction;
  - procedural cities;
  - asset production.
- **Encounters.** The labelled practice fight is the only way into an encounter in play.
- **Class features.** Only Second Wind and Cunning Action are active in combat. Sneak Attack, Rage,
  Action Surge, Smite and the rest wait, as do the effects of weapon mastery.
- **Not modelled:** cover, line of sight, lighting, elevation, ammunition counts and off-hand
  attacks.
- **Residents.** They use generic SRD profiles until NPCs hold character sheets.
- **Presentation.**
  - Placeholder discs, capsules and boxes, with no creature models or animations.
  - The grid lives in the panel; the 3D markers are not clickable.
  - The guard's street body stays where its routine puts it; its marker shows its place in the
    fight.
- **Structures.** Informational ones simulate nothing yet, and there is no construction or upkeep.
- **Licensing.**
  - The SRD 5.2.1 and 5.1 material is used under CC BY 4.0, with the required attribution now
    shipped in three places: `CREDITS.md`, the Hub's Info section and the README.
  - The art is CC0 (Quaternius; ambientCG), credited anyway; the licence files are in
    `Art/ThirdParty`.
  - No new third-party assets were added.
  - The creature statistics are SRD material; their descriptions are Realmkeeper's own.
  - No protected setting lore or non-SRD material was used.

## K. Git

- **Branch and commit.** On `feature/phase-0-1-foundation`, one commit on top of Phase 7
  (`a3cd9f5`): "Phase 8: combat foundation, encounter engine, settlement structures". It carries
  the trailer.
- **Not committed.** `ProjectSettings/GraphicsSettings.asset` stays modified in the working tree.
  It was not staged.
- **Push.** The owner asked in chat for this commit to be pushed. That authorization is for this
  push only.
- **Scope.** Phase 8 stops here. Phase 9 has not been started.
