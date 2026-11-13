# Phase 22: Completion report

**Phase 22 — Character Creation Integrity, Level 1 Starting Rules, Player-Driven Advancement & Targeted Phase 20A/21 Backfill.** Date: 2026-11-13. **Decision:** ADR-0109. **Preflight:** `Phase22-Preflight-Audit.md`.

**Owner instructions during the phase:**
- **D4 (2026-11-13):** "No XP before creation".
- **Testing (2026-11-13):** "I don't think we need to do as many edit mode or play mode tests as we do. So even though my instructions state to do it, let's ignore it for this round." The full Unity EditMode and PlayMode suites were therefore not run (§10).

**No one hand-played this phase.**

## 1. Implementation summary

- **Level 1 baseline** (`LevelOneBaseline`, Domain). Every new character is checked against the level 1 class table before creation commits. A matrix test covers every class with every species and lineage option, and every background with every class.
- **No experience before creation** (D4), for new awards only. Old saves keep what they hold.
- **Species traits by level** (`FeatureDefinition.MinLevel`), and `LockedFeatures` for everything still to come.
- **Tiefling (rules 10).** The legacy's spellcasting ability is chosen. Otherworldly Presence and the level 3 and 5 Legacy Spells are in the data, locked until their levels and marked "not yet played".
- **The player-driven level-up**, on Phase 7's explicit advancement:
  - a one-time, recorded notice;
  - a bronze HUD button with the open choices;
  - the overview separates automatic benefits from choices, and shows what stays locked;
  - picks are kept on closing and saving, then resumed; confirming applies each level once;
  - the sheet's Progression shows the class and subclass, latest level, waiting level and locked features.
- **Save schema v24:** `pendingAdvancement` and `levelUpNoticed`.
- **The creator's review** shows level 1 and everything the character starts with.
- **Phase 20A backfill.** Stone's Endurance and Storm's Thunder answer spell damage; Stone's Endurance also answers saving-throw damage from a creature's hit.
- **Phase 21 closeout.** The purchased-kit integration was verified and committed on its own. The missing captures were taken. The shop economy is recorded as a future requirement.
- **Ruleset baseline:** `../Product/Ruleset-Baseline.md`.

## 2. Character creation, before and after

| | Before Phase 22 | After |
|---|---|---|
| Level and experience | Level 1. Experience earned while pending was kept, so a new character could be eligible for level 2+ at once | Level 1 with 0 experience. Awards are refused until creation (D4). An old save's pre-creation experience is kept |
| Checks | The validator caught premature level choices, an early subclass and level above experience; no level 1 table check | `LevelOneBaseline` recomputes level 1 from the class table and refuses creation on any difference |
| Species traits | All applied from level 1 | `MinLevel` gates them. The tiefling's level 3 and 5 legacy spells are locked |
| Tiefling ability | Charisma, fixed | Chosen with the legacy (rules 10). Older tieflings keep Charisma until their next level-up asks |
| Review page | Scores, HP, saves, proficiencies, skills, tools, languages, kit | Adds level 1 in words, AC from the kit, initiative and speed, level 1 features (with "not yet played" marks), cantrips, spells, slots, save DC and attack, uses, and later species traits |

**Creation paths inspected:**
- **The creator and `CreateCharacterCommand`:** the only player path.
- **Legacy and pending players:** created through the same command and baseline.
- **New worlds:** start pending, then use the same command.
- **Tests:** the same command.
- **The creator's live figures (`CharacterDraft.Rules`):** never saved.
- **NPC sheets (`NpcCharacterGenesis`):** go through the same draft and `BuildSheet`, then advance with the shared rules.

No template or developer shortcut creates a higher-level player. A template carrying later-level choices or a subclass is refused (tested).

## 3. Level 1 validation results

- **Matrix:** every class × every species-and-lineage option (backgrounds in turn), plus every background × every class. Every build passes the creator's checks, the validator and the baseline.
- **Per class:**
  - level 1, 0 experience, proficiency +2;
  - HP = hit die + Constitution;
  - HP full; no subclass; only level 1 features; no `lv` keys;
  - no slot above 1st; one hit die;
  - two 1st-level slots for full and half casters, one pact slot for the warlock;
  - spells at most 1st level; cantrips are cantrips.
- **Mutation:** the baseline catches 15 kinds of leak:
  - a higher level, experience, an advancement record, a subclass, a later choice key, or a level-up in progress;
  - a 2nd-level slot or an extra 1st-level slot;
  - a resource not full;
  - a 2nd-level spell in the book or prepared, or a levelled spell as a cantrip;
  - hurt, old rules, or no kit.

## 4. The advancement workflow

1. **Experience** makes the character eligible; nothing levels by itself.
2. **The notice.**
   - It appears once per new level, when nothing else is on screen, and is not repeated after loading.
   - It says the levels reached, what the next brings, how many choices it asks, and that nothing is taken until the player confirms.
   - It is recorded in `levelUpNoticed` and in the chronicle (`PlayerLevelReady`).
3. **The HUD button** reads "Level N ready (L) · k choices" in bronze while a level waits. Its tooltip says when a level-up is in progress.
4. **The window.**
   - **Overview:** "Gained automatically when you confirm" (hit points, proficiency, features, resources, class-table columns); "Your choices", each ✓ chosen, ◌ optional and empty, or ○ to choose; and "Still to come".
   - **Pages:** one per choice, then the review.
5. **Closing keeps the picks** (`SaveAdvancementDraftCommand`, validated), and reopening resumes them. Confirming applies the whole level once, and several levels are taken in turn.
6. **Nothing is chosen for the player.** `SuggestChoices` serves only NPCs and tests. Where a class has only its SRD subclass, the window says further subclasses are deferred; no choice is invented.
7. **The sheet** (Progression):
   - class and subclass, or the level the subclass comes at;
   - experience bar;
   - the waiting level, with its automatic gains and open choices;
   - latest level and levels gained;
   - experience log;
   - "Ahead: still locked" for four levels, with species traits and "not yet played" marks.
8. **Duplicates and prerequisites.** A level cannot be taken twice; picks are never overwritten (ADR-0028). Kept picks are refused when:
   - they are for a later level;
   - they name a choice not unlocked;
   - they exceed a count or name a non-existent option;
   - they name another class's subclass.

## 5. Phase 20A backfill completed

| Item | Rule level | Result |
|---|---|---|
| Tiefling spellcasting ability (Int, Wis or Cha) | 1 | **Implemented** (rules 10, creation choice; older tieflings asked at their next level-up) |
| Stone's Endurance against spell and saving-throw damage | 1 | **Implemented** (one offer per blow; the cut comes off the largest damage share). Tested with an acolyte's Sacred Flame |
| Storm's Thunder against spell damage | 1 | **Implemented**, now listed as functional. Tested |
| Tiefling level 3 and 5 legacy spells | 3 and 5 | **Represented and locked** (not granted at creation; shown as new in the level-up that brings them). Mechanics deferred (§6) |
| Tiefling Otherworldly Presence (Thaumaturgy) | 1 | **Represented, not played**: added to the data with its "not yet played" mark |

## 6. Phase 20A items deferred

| Item | Why | Dependency |
|---|---|---|
| Gnome (forest): Minor Illusion, Speak with Animals | Level 1, but no system exists | Illusion and animal-speech systems |
| Gnome (rock): Mending, Prestidigitation | Level 1, but no system exists | Object-repair and minor-effect systems (tinker's tools work) |
| Tiefling Thaumaturgy | Level 1, no system | Minor-effect magic |
| Tiefling level 3 and 5 spells cast | Levels 3 and 5 | Ray of Sickness, False Life, Ray of Enfeeblement and Darkness in the spell catalog, and a once-per-long-rest innate casting path |
| Dragonborn Breath Weapon (SRD 5.2.1 form) | Level 1 | A non-spell area action in combat: cone or line aiming, AI use, grid UI. Routing it through the spell engine would let Counterspell stop it and rage or Wild Shape block it. It stays labelled "not yet played" |
| Ancestry reactions against hazards outside a fight | Level 1 | No reaction is taken outside a fight; would need an out-of-combat reaction path |
| Hellish Rebuke against spell damage | Warlock | Unchanged this phase (attacks only) |

## 7. Phase 21 asset integration closeout

- **Kit commit:** `30b5681`, made separately before any Phase 22 change.
- **Checks:**
  - every art catalog reference resolves (1,158 GUIDs);
  - the local-only packs (Bestiary, Fantasy Races) are git-ignored and absent from the tree;
  - licences read: all three purchased kits are CC0 1.0 (`CREDITS.md`, `Art/AssetInventory.md`).
- **Seasonal leaves (fixed during closeout).** Two bugs:
  - the kit paints its leaves green, so a tint could not turn them;
  - the weather capture test never set the season.

  Now autumn turns broadleaves orange and gold, and winter dulls and snow-dusts them.
- **Tests:**

  | Suite | Result |
  |---|---|
  | .NET | 898 of 898 passed |
  | Weather capture test | passed, with its captures inspected |
  | Full Unity EditMode | stopped part-way |
  | Full Unity PlayMode | not run |

  The full Unity suites were left at the owner's instruction (§10).
- **Visual checks of the kit work:** doors fill the doorway; signs outside four shops; furniture in homes and shops; trees by region; seasonal leaves. All are in the captures in `../Screenshots/Phase21/kits/`.

## 8. Phase 21 backfill completed

- **Missing captures:** a working shop's trade window and a work station in use (crafting opened at the forge), each at 1920×1080, 1366×768 and 1280×720 (`Phase22QaCaptureTests`). The creator and sheet were captured at all three sizes in the Phase 22 set.
- **The shop economy** is recorded as requirement R1 in `../Product/FutureRoadmap.md` §8, and not built.

## 9. Save schema and migration

- **Schema v24.**
  - **Sheet fields:** optional `pendingAdvancement` (`level`, `choices`, `subclassId`) and `levelUpNoticed`.
  - **Rules 10** adds `species:legacy_ability`.
  - **New event type:** `PlayerLevelReady`.
- **`MigrationV23ToV24`.**
  - **Created characters:** structural. Absent fields read as nothing pending and nothing announced, so a character already eligible is told once after loading.
  - **A player not yet created** moves to rules 10.
  - **Untouched:** levels, experience, choices and history.
- **Tested:**
  - a v23 save loads with nothing pending, keeps its level and experience, and announces once;
  - drafts, the notice and levels survive saving and loading through the real save path;
  - tampered drafts and an out-of-range notice level are refused on load;
  - older migrations still load (the existing migration tests pass).

## 10. Exact test results

| Suite | Result |
|---|---|
| .NET (Release, `Tools/DotNet`, all tests) | **917 passed, 0 failed, 0 skipped** (3 min 45 s; build 0 warnings, 0 errors) |
| Phase 22 .NET tests (`Phase22LevelOneTests`, `Phase22AdvancementTests`) | 19 passed, 0 failed (included above) |
| Unity PlayMode, targeted: `Phase22PlayModeTests`, `Phase22QaCaptureTests` | 2 passed, 0 failed (with captures) |
| Unity PlayMode, targeted: `Phase20APlayModeTests` (changed for rules 10) | 2 passed, 0 failed |
| Unity PlayMode, targeted: `Phase21WeatherCaptureTests` (closeout) | 1 passed, 0 failed |
| Unity compile (Presentation and tests) | No errors |
| **Full Unity EditMode** | **Not run** (owner's instruction) |
| **Full Unity PlayMode** | **Not run** (owner's instruction) |

- **No timeouts.** The .NET suite and every targeted Unity run finished; nothing was skipped or inconclusive in them.
- **What is unverified without the full Unity suites:**
  - **EditMode.** It compiles the same test sources as .NET, so its result is expected to match, but it was not run on this code.
  - **PlayMode.** About 130 existing tests were not re-run.
- **Main risks among those PlayMode tests:**
  - tests that grant experience and then compare world state, since the one-time notice now writes `levelUpNoticed` and a chronicle event when a window closes;
  - tests that create a tiefling through `LevelUpDriver.Choices`, which now also picks the legacy ability (its first option, Intelligence).

**Tests changed for real rule changes:**
- the schema pins 23 → 24;
- `Experience_BeforeCreation_IsKept_AndCountsOnceCreated` became `Experience_BeforeCreation_IsRefused_SoANewCharacterStartsAtZero` (D4);
- the tiefling helpers pick the legacy ability (Charisma, as those tests assume);
- the infernal option's status text says "spellcasting ability", not "Charisma";
- the Phase 20A PlayMode test clicks the ability.

No assertion was weakened.

## 11. Screenshots and findings

`../Screenshots/Phase22/` (27 images and two logs). The 1920×1080, 1366×768 and 1280×720 sets are listed below; the other shots are at 1920×1080.

| Shot | Sizes | Shows |
|---|---|---|
| `p22_01_creator_tiefling_ability_and_later_traits` | three | Legacy ability chosen; level 3 and 5 legacy spells marked "from level N, not at creation" |
| `p22_02_creator_review_level_one` | three | Level 1, AC, initiative and speed, level 1 features, magic, uses, later species traits |
| `p22_03_sheet_progression_level_one` | one | Class, experience, locked features by level |
| `p22_04_level_up_notice` | three | The notice; the HUD button bronze |
| `p22_05_level_up_overview` | three | Automatic apart from choices; still to come |
| `p22_06_hud_level_up_in_progress` | one | The waiting level after closing |
| `p22_07_level_up_subclass_choice` | three | The Arcane Tradition, nothing picked for the player |
| `p22_08_level_up_review` | one | Level 3 review |
| `p22_09_sheet_after_levelling` | three | Wizard 3 · Evoker; latest level; levels gained; locked |
| `p22qa_01_trade_window` | three | A working shop: stock, prices, shortfalls, supply |
| `p22qa_02_station_crafting` | three | Crafting at the forge and anvil |

**Found in the captures and fixed:**
- The review listed Fire Bolt twice for a tiefling wizard who also took it as a class cantrip. It is now listed once, as the class's, under the Phase 20A rule that the class's own copy is the one cast.
- The overview marked an empty "up to" choice (Spellbook) as "✓ Chosen". It now shows "◌ Optional, nothing picked yet".

**Checked and fine:**
- no clipping at 1280×720 in the creator review, notice, sheet, crafting or trade window;
- the notice does not hide the HUD button.

**Not captured:** the narrow-window behaviour below 1280×720, which the UI does not support.

## 12. Remaining limitations

- **Phase 20A deferrals:** see §6.
- **Only the SRD subclass** is offered for each class (Phase 30).
- **The level 3 legacy spell** shows in the level-up summary and history without its "not yet played" mark. The sheet and the windows do mark it.
- **The notice is checked** on HUD refreshes and when windows close. Experience paid while a fight is on is announced after the fight.
- **The shop economy** is recorded, not built (R1).
- **The full Unity suites were not run** (§10).
- **Not hand-played.**

## 13. Owner playtest checklist

`Phase22-Owner-Playtest-Checklist.md`.

## 14. Git

- **Branch:** `feature/phase-0-1-foundation`.
- **Commits this phase:**
  - `30b5681`: the Phase 21 kit closeout;
  - the Phase 22 commit (hash below).
- **Working tree after committing:** clean, apart from `ProjectSettings/GraphicsSettings.asset` (if Unity touched it) and Unity's `Assets/InitTestScene*`, which are never committed.

## 15. Push

Nothing was pushed. Phase 23 has not been started.
