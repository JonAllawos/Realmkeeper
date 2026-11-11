# Phase 6 report: Character Creation & Core RPG Foundation

Date: 2026-11-10. Branch `feature/phase-0-1-foundation`. Base: `1f37bb6` (Phase 5).

This phase builds the **foundation** of the RPG character:
- species, backgrounds and level-1 classes;
- abilities, skills, saves, hit points and resources;
- a real character creator;
- a character sheet.

It does **not** implement the D&D ruleset as a whole. There is no levelling beyond 1, no subclasses,
no spells, no combat, and no finished art for eleven of the twelve species. These are listed in §12.

## 1. Pre-flight findings

Full record: `Documentation/Phase6/PreFlight.md`.

- **Baseline.** The repository matched the expected baseline: HEAD `1f37bb6`, one commit ahead of
  origin `277bd48`, not pushed.
- **The one uncommitted file.** `ProjectSettings/GraphicsSettings.asset` contains formatting-only
  Unity re-serialisation: the same GUIDs and values. It was inspected, left untouched, and kept out
  of the commit.
- **Test baseline.** .NET 346, EditMode 346, PlayMode 57.
- **The Bible.** The Game Design Bible was not in the repository. It is now in
  `Documentation/Product/GameDesignBible.md`, verbatim, with an implementation-status section.
- **A conflict for the owner, not resolved here.** The binding `ProductFoundation.md` describes "a
  persistent fantasy world simulation and exploration platform". The Bible describes a full RPG.
  Reconciling them is an owner decision.
- **Licensing (verified).**
  - SRD 5.2.1 and SRD 5.1 are both CC BY 4.0.
  - Aasimar is in neither SRD.
  - Eight of the twelve requested backgrounds are not in SRD 5.2.1.
- **One design change during implementation.** An auto-opening creator would have blocked every
  existing exploration flow and PlayMode test. Creation is therefore **offered** (HUD button, C key,
  Hub banner), not forced. The pre-flight record notes the change.

## 2. Architecture decisions

| ADR | Decision |
|---|---|
| [0020](../Decisions/ADR-0020-rpg-record-on-player.md) | The RPG character is one record (`Rpg`) on the existing `PlayerCharacterData`. Derived values are never stored. |
| [0021](../Decisions/ADR-0021-typed-rule-effects.md) | All species, background and class features are typed `RuleEffect`s, with no per-species or per-class conditionals. |
| [0022](../Decisions/ADR-0022-rules-configuration-and-licensing.md) | Fifth-edition-compatible rules from SRD 5.2.1 (5.1 where needed) under CC BY 4.0. Ability increases come from species. Provenance is recorded per definition. |
| [0023](../Decisions/ADR-0023-legacy-migration-and-kit-once.md) | v7 players become **Legacy**, with nothing lost and nothing invented. The starting kit is granted exactly once, in one transaction. |
| [0024](../Decisions/ADR-0024-placeholder-species-appearance.md) | Species appearance is a data contract. Non-human species use **labelled placeholders**. |

**Layering is unchanged and no new assemblies were added:**

| Layer | What Phase 6 added |
|---|---|
| Domain (`Domain/Rpg`) | Rules, catalogs and validation. Engine-free. |
| Application | The creation command, draft and queries. Engine-free. |
| Infrastructure | Schema v8 and its migration. |
| Presentation | The creator window and the Hub sheet. |

## 3. Systems implemented

- **Abilities.**
  - Modifiers are `floor((score−10)/2)`, derived and never stored.
  - Scores come from the standard array or point buy (27 points, 8–15). There is no free entry and
    no rolling; the command and the validator both enforce this.
- **Skills and saves.**
  - 18 skills, with proficiency and expertise.
  - Grants from species, background, class choices and options, checked for duplicates.
  - Six saves.
  - Every bonus has a breakdown with its sources.
- **Proficiency bonus.** `2 + (level−1)/4`.
- **Hit points.**
  - Level 1 gets the hit die's maximum + Constitution; later levels the average; plus per-level
    bonuses.
  - Current and temporary HP are stored; the maximum is derived.
- **Armour class.** Provisional: 10 + Dexterity, unarmoured defence, worn protection.
- **Character model.** `CharacterSheetData` holds:
  - the state, title and history;
  - the species, background, class and subclass IDs;
  - level and XP;
  - the ability method, base scores and choices;
  - HP, temporary HP and resources;
  - whether the kit was granted, and when the character was created.
- **Rules evaluator** (`CharacterRules`). It derives the whole sheet from the typed effects.
- **Validator** (`CharacterValidation`, inside `WorldValidator`). It runs on every command and load,
  covering every rejection listed in the brief.
- **Creation.**
  - `CharacterDraft` holds the step logic and limits.
  - `CreateCharacterCommand` is one transaction. It sets identity and look, the sheet, the kit
    through the inventory and equip rules, the coin, and a chronicle entry.
  - The command refuses to run twice.
- **Creator UI.**
  - Nine steps: Identity, Appearance, Species, Background, Class, Abilities, Skills, Equipment,
    Review.
  - A live 3D preview wearing the chosen kit at the species' height, with turn and zoom.
  - Back and Next keep choices; Esc means "later".
- **Player Hub.** The Character section is a full sheet with five panels: Sheet, Skills, Features,
  Proficiencies, Identity. The other seven sections are unchanged.
- **Live gameplay effects.**
  - Species walking speed in exploration.
  - Goliath carrying capacity in encumbrance.
  - Merchant price factor in shops.
  - Species height on the body.
- **Items.** 19 new catalog items: martial weapons, chain shirt, spellcasting focuses, tools.

## 4. Species, classes and backgrounds delivered

Full tables: `Systems/Species.md`, `Systems/Classes.md`, `Systems/Backgrounds.md`.

- **Species (12).**
  - All are mechanically complete as level-1 rules, with typed traits: Human, High Elf, Wood Elf,
    Dwarf, Halfling, Gnome, Orc, Half-Orc, Tiefling, Dragonborn, Aasimar, Goliath.
  - **Visually, only Human is finished.** The other eleven are placeholders: the human body at the
    species' height, labelled as such.
  - Aasimar is a Realmkeeper original. Its name is flagged for IP review.
- **Backgrounds (12).** Soldier, Noble, Criminal, Acolyte, Artisan, Merchant, Outlander, Scholar,
  Sailor, Entertainer, Hermit, Folk Hero.
  - Four follow SRD 5.2.1; eight are Realmkeeper originals.
  - Merchant's feature changes prices.
  - The other features are typed social effects shown on the sheet, but nothing reads them yet.
- **Classes (12), level 1 only.** Barbarian, Bard, Cleric, Druid, Fighter, Monk, Paladin, Ranger,
  Rogue, Sorcerer, Warlock, Wizard.
  - Each has its hit die, saves, armour, weapon and tool proficiencies, skill choices, equipment
    options, level-1 features, resources and spellcasting stats.
  - A test proves that no two classes are the same.
  - Subclass level and title, and an empty later-feature schema, are in place for Phase 7.

## 5. Character creation screenshots

All are from the shipped exploration scene, captured by the PlayMode fixture `Phase6CaptureTests`.
They are in `Documentation/Screenshots/Phase6/`. The fixture creates a female dwarf Acolyte
Cleric (Protector) by point buy.

| File | Shows |
|---|---|
| `p6_01_hud_create_prompt.jpg` | A new world's player; the HUD offers "Create character (C)" |
| `p6_02_creator_identity.jpg` | Step 1: name, title, age, description, history |
| `p6_03_creator_appearance.jpg` | Step 2: frame, hair, skin, colours, with the live preview |
| `p6_04_creator_species_dwarf.jpg` | Step 3: Dwarf traits and the **placeholder notice** |
| `p6_05_creator_background.jpg` | Step 4: Acolyte |
| `p6_06_creator_class.jpg` | Step 5: Cleric, Divine Order; the preview wears the cleric's kit |
| `p6_07_creator_abilities_pointbuy.jpg` | Step 6: point buy, 0 points left; species bonuses and modifiers |
| `p6_08_creator_skills.jpg` | Step 7: granted skills marked by source; two chosen |
| `p6_09_creator_equipment.jpg` | Step 8: one option per group; "(worn)" pieces; the background kit |
| `p6_10_creator_review.jpg` | Step 9: everything, ready to create |
| `p6_11_created_character_in_world.jpg` | The created character on the street: placeholder at dwarf height, mace and shield |

**Fixed after reviewing the captures:**
- The "worn" marker used a glyph (⚔) missing from the UI font, so it showed as a box. It is now
  text.
- On the Skills step, rows with tags pushed the bonus column out of line. The columns are now
  fixed-width.
- The Features panel did not list chosen options such as Divine Order: Protector. They are now
  listed, and a test covers it.

## 6. Player Hub screenshots

| File | Shows |
|---|---|
| `p6_12_hub_sheet.jpg` | Sheet: HP 12/12, AC 17 (provisional), speed 25 ft, proficiency, initiative, hit die; abilities with base + species; saves |
| `p6_13_hub_skills.jpg` | Skills: each bonus with its breakdown and sources |
| `p6_14_hub_features.jpg` | Features and resources (spell slots, spell DC and attack) |
| `p6_15_hub_proficiencies.jpg` | Proficiencies, languages, senses, resistances |
| `p6_16_hub_equipment.jpg` | Equipment: the starting kit worn |

## 7. Save migration results

**Schema v8.** `MigrationV7ToV8` adds `player.rpg = { state: "Legacy", level: 1, experience: 0 }`
and nothing else.

| Check | Where | Result |
|---|---|---|
| v7 save loads as Legacy; ID, name, look, inventory, equipment, purse, location, pose unchanged | .NET (`CharacterTests`) | Pass |
| Older saves (v1–v6 paths) migrate through v7 to v8 and equal a fresh world apart from Legacy vs Pending | .NET (existing migration tests, updated) | Pass |
| A Legacy player completes creation later, keeps everything, gets the kit once | .NET | Pass |
| **A real Phase 5 save** (made in the shipped scene, rewritten to schema v7 without `rpg`) loads in the scene as Legacy with nothing lost, the dagger still equipped, still in town | **Unity PlayMode** (`Phase5Save_LoadsAsALegacyCharacter_WithNothingLost`) | Pass |
| Created character saves and reloads: class, scores and choices kept; no item duplicated; creation not offered again; dwarf body restored | **Unity PlayMode** | Pass |
| v8 save without `rpg` is refused as corrupt | .NET | Pass |

## 8. .NET test count

**372/372** (Release, 0 warnings). That is 346 + 26 new:
- `CharacterTests`: 15 methods + 10 modifier test cases;
- `CharacterPerformanceTests`: 1.

## 9. Unity EditMode test count

**372/372** (batch, Unity 6000.0.47f1). These are the same test sources as .NET, run inside Unity.

The assertion that a v8 save without `rpg` is refused was added to an existing test after that
run. It was then confirmed by a separate Unity run of the Character fixture: **26/26**.

## 10. Unity PlayMode test count

**60/60**, 0 failed, 0 skipped, with `RK_CAPTURE_DIR` set so every capture fixture ran.

That is 57 before Phase 6, plus 3 new:
- `Phase6PlayModeTests` (2): the full creation flow in the shipped scene, and the Phase 5 save
  loading as Legacy;
- `Phase6CaptureTests` (1).

**What was tested in Unity, and what only in .NET.**

| Tested | Where |
|---|---|
| Creator flow through every step, in the real scene with real UI elements: name, appearance, species, background, class, Divine Order, point buy, skills, equipment, review, create | **Unity PlayMode** |
| The body showing the kit at dwarf height | **Unity PlayMode** (look signature and rendered captures) |
| The Hub sheet's values: HP, AC, Wisdom 16 / +3 / save +5, features, darkvision | **Unity PlayMode** |
| Save and reload, legacy load | **Unity PlayMode** |
| Every species × class combination being creatable | .NET and EditMode only (data and commands, not UI) |
| Validator rejections | .NET and EditMode only |
| Draft logic | .NET and EditMode only |
| Speed, carry and price effects | .NET and EditMode only |
| Other species' and classes' UI rendering | Not tested in PlayMode; only the dwarf cleric path was driven |

**Mutation checks.** Each mutant was introduced, run, and reverted; the source was verified clean.

| Mutant | Caught by |
|---|---|
| Starting kit granted twice | `NewWorld_…_GrantsTheKitOnce…` |
| Invalid skill accepted | `InvalidCreations_…` |
| Wrong ability modifier | 3 modifier test cases |
| Wrong hit points | 2 tests |
| Missing species validation | Validator test |
| Choices lost on save | 2 tests |
| Identity replaced in migration | 4 migration tests |

## 11. Performance measurements

**.NET and EditMode** (`CharacterPerformanceTests`):

| Operation | .NET | Unity EditMode |
|---|---|---|
| Catalog validation, first use (includes building all 36 definitions) | 10.2 ms | 16.7 ms |
| Creation check (command validation) | 21 µs | 43 µs |
| Full sheet (18 skills, 6 saves, HP, AC, speed) | 20 µs | 82 µs |
| World save / load | 1.8 / 51 ms | 1.9 / 29 ms |

**Development build.** Windows, RTX 4070 Laptop GPU, i9-14900HX, 1920×1080, probe
`-rkPerfProbe`. Report: `Screenshots/Phase6/devbuild-perf-phase6.txt`. The probe now also measures
the creator and the Hub sheet.

| Pose | Mean frame | p95 | Draw calls |
|---|---|---|---|
| Map | 16.67 ms | 16.99 ms | 153 |
| Street | 16.67 ms | 17.20 ms | 688 |
| Shop interior | 16.67 ms | 17.17 ms | 652 |
| **Creator open (live 3D preview)** | 16.66 ms | 17.33 ms | 792 |
| **Hub, character sheet** | 16.66 ms | 17.28 ms | 779 |
| Street, clock ×30 | 16.69 ms | 17.41 ms | 763 |
| Plaza, night | 16.67 ms | 16.88 ms | 442 |

Memory with the creator open: 600 MB system, 305 MB graphics (the preview's render texture
included).

**Caution about comparing builds.**
- **Frame time.** This run was **vsync-capped at 60 fps**, so every pose reads 16.67 ms. That shows
  each pose holds 60 fps with a p95 under 17.5 ms. It does **not** show the uncapped frame time, so
  these figures cannot be compared with Phase 5's uncapped 2–4 ms.
- **The town.** The probe's town this time was Bramhaven (6 residents, 2 shops). Draw counts differ
  from Phase 5 for that reason.

## 12. Known limitations

| Area | Limitation |
|---|---|
| **Species art** | Only Human has a finished model. The other eleven species are **placeholders**: the human body scaled to the species' height, labelled in the creator, the review and the sheet. No ears, tusks, horns, tails or scales. |
| Levels | Level 1 only. No XP awards, levelling, subclasses or later features. |
| Spells | Spellcasting ability, slots, cantrip counts, DC and attack exist. There are no spells, and no spell or cantrip choice. |
| Combat | Sneak attack, rage, fighting styles, weapon mastery, resistances and advantages are typed data, not used. |
| Resources | Shown and saved. Nothing spends or recovers them yet (no rests). |
| Armour class | Provisional. Armour has no light, medium or heavy categories, so Dexterity caps and armour proficiency penalties are not applied. |
| Social features | Shown, but not yet read by dialogue, quests or reputation. |
| Rules | Species-based ability increases (SRD 5.1 style). The 5.2.1 background increases and origin feats are not implemented. |
| Tools and languages | Proficiencies are recorded; nothing uses them yet. |
| Creator coverage in Unity | Driven end to end for one combination (dwarf cleric). Other combinations are tested at the data and command level. |
| Duplicate focus | An Acolyte cleric receives two holy symbols (background plus class focus). This is a content overlap, not a duplication bug. |
| IP review | The name "Aasimar"; the use of SRD content needs the CC BY 4.0 attribution in the shipped credits. |
| Product scope | The Bible and `ProductFoundation.md` need owner reconciliation. |

## 13. Files changed

**New, runtime:**
- `Runtime/Domain/Rpg/`: `RpgCore`, `RpgDefinitions`, `RpgCatalog` (+ `.Species`, `.Backgrounds`,
  `.Classes`), `CharacterSheet`, `CharacterRules`, `CharacterValidation`.
- `Runtime/Application/`: `CharacterCommands`, `CharacterDraft`, `CharacterQueries`.
- `Runtime/Infrastructure/Persistence/WorldSaveMapper.Character.cs`.
- `Runtime/Exploration/Presentation/CharacterCreatorWindow.cs`.

**New, tests:**
- `Tests/EditMode/Character/CharacterTests.cs` and `CharacterPerformanceTests.cs`.
- `Tests/PlayMode/Phase6PlayModeTests.cs` and `Phase6CaptureTests.cs`.

**Modified, runtime:**
- Domain:
  - `PlayerCharacterData` (the `Rpg` record);
  - `Items` (Tool and Focus categories, crossbow);
  - `ItemCatalog` (19 items);
  - `WorldData` (schema 8);
  - `WorldEntities` (`PlayerCharacterCreated` event);
  - `WorldValidator`.
- Application:
  - `EventFormatter`;
  - `PlayerCommands` (an `EquipFromPack` helper; background locked once created);
  - `PlayerQueries` (species pace, carry capacity);
  - `WorldSession`.
- Simulation: `CommerceMath` (trade connections).
- Infrastructure: `SaveFileDtos`, `SaveMigrations` (`MigrationV7ToV8`), `JsonWorldSerializer`,
  `WorldSaveMapper.Commerce`.
- Presentation:
  - `ExplorationController.Living` (creator, HUD button, C key);
  - `PlayerHubWindow` (character sheet);
  - `PlayerBodyBuilder` (species height);
  - `PerformanceProbe` (creator and Hub poses);
  - `ExplorationHud.uss`.

**Modified, tests (updated to intended behaviour, none weakened):**
- `Phase2Worlds` (`StripToV7`, old saves expected Legacy);
- `CommerceTests`, `NpcCommandTests` (current schema version; Legacy normalisation);
- the PlayMode test assembly (Newtonsoft reference, to rewrite a real save to v7).

**Documentation:**
- New:
  - `Systems/Characters.md`, `Species.md`, `Backgrounds.md`, `Classes.md`, `CharacterCreation.md`,
    `SkillsAndAbilities.md`;
  - `Product/GameDesignBible.md`;
  - `Phase6/PreFlight.md`;
  - ADR-0020…0024;
  - this report;
  - `Screenshots/Phase6/`.
- Updated: `Persistence.md`, `Roadmap.md`, `Testing/TestResults.md`, `Decisions/README.md`.

## 14. ADRs created

ADR-0020, ADR-0021, ADR-0022, ADR-0023, ADR-0024 (see §2).

## 15. Git branch and commit

Branch `feature/phase-0-1-foundation`. This report is part of the commit "Phase 6: Character Creation &
Core RPG Foundation", directly on top of `1f37bb6`. `git log -1` gives its hash.

## 16. Working tree status

After the commit, only `ProjectSettings/GraphicsSettings.asset` remains modified. It is the
formatting-only change found in pre-flight, deliberately not committed and not discarded.

## 17. Whether anything was pushed

**Nothing was pushed.** The branch is two commits ahead of origin (Phase 5 and Phase 6).

---

Phase 6 stops here. Phase 7 has not been started and waits for review.
