# Phase 20: Completion report (advanced character creation, character systems, appearance, progression and character UI)

**Date:** 2026-11-12. **Branch:** `feature/phase-0-1-foundation`, on top of `79099a6` (Phase 19, pushed). **Phase 20 is committed and pushed to origin at the owner's instruction** (section 14).

## 1. Summary
Phase 20 extends the existing character systems instead of replacing them:
- the Phase 6 creator and records;
- the Phase 7 rules engine, ledger and advancement;
- the Phase 10 NPC sheets;
- the Phase 15 companions.

What it adds:
- **Lineages.** SRD 5.2.1 lineages for the tiefling, goliath and gnome, as data choices (rules 9).
- **Identity:** pronouns and a one-line concept.
- **Appearance:** the person's own height and build on the real model, and skin tones for non-human species.
- **Save schema v21**, with a migration that keeps every earlier character exactly as it was.
- **A redesigned creator:** a large preview, cards, a live build panel listing what is still to choose, and the subclass and the road ahead on the class step.
- **A stronger sheet:** ability tiles that explain every number, and a new Origins tab.
- **People as the player knows them.** "About them" in conversation, layered by acquaintance; a stranger's sheet stays hidden.
- **Companion detail:** the same ability tiles.

**Not hand-played** (section 11).

## 2. Preflight findings
See `Reports/Phase20-Preflight-Audit.md`.
- **The systems were sound.** The character data, rules engine, validation, transactional creation and level-up, keyed XP awards and NPC genesis were functional and tested.
- **Missing or partial:**
  - the SRD 5.2.1 lineages;
  - a player-facing person view;
  - a creator layout that gave the character room;
  - height and build within a species;
  - non-human skin tones.
- **Subclasses.** The SRDs contain one subclass per class, and all 12 are implemented, so the catalog was not padded with inert or invented ones.

## 3. Implemented

| Area | What | Where |
|---|---|---|
| Lineages (rules 9) | Tiefling Fiendish Legacy (Abyssal, Chthonic, Infernal: resistance plus innate spell); Goliath Giant Ancestry (Cloud, Fire, Frost, Hill, Stone, Storm); Gnome Lineage (Forest, Rock). Required by the creator, validated, given to generated people deterministically. What combat and magic do not play yet is flagged. | `RpgCatalog.Species.cs`, ADR-0100 |
| Identity | Pronouns (≤ 24) and concept (≤ 80), optional and validated; writing prompts that do not decide the story | `CharacterSheetData`, `CharacterDraft`, creator |
| Appearance | Height 92–108 % of the species' typical height and build (slight, average, heavy), applied to the body's scale; 15 species skin tones appended to the palette; species-appropriate tone checks; species changes that alter the look are announced (`CharacterDraft.Adjustment`) | `PlayerAppearanceData`, `PlayerAppearanceOptions`, `PlayerLook`, `HumanoidCharacter`, ADR-0101 |
| Save v21 | DTO fields; `MigrationV20ToV21` gives every saved tiefling, goliath and gnome sheet (player, people, companions) the lineage matching its old traits, never overwriting a choice; pending characters move to rules 9 | `SaveFileDtos`, `WorldSaveMapper.*`, `SaveMigrations.cs` |
| Creator | Three regions: preview (about 31 %), step, live build. Compact mode below 1380 px. Step pills with icons. Species, background and class cards. Class detail with subclass features and levels 2–6. Live build: abilities (tooltip sources), combat numbers with breakdowns, saves, skills with sources, spellcasting, features, and everything still to choose by step. | `CharacterCreatorWindow.cs`, `ExplorationHud.uss` |
| Sheet | Header with lineage, pronouns and concept; ability tiles (score, modifier, save; the tooltip gives the full explanation); new Origins tab (species, lineage, traits flagged if not yet played, background, own words); tabs in the shared style with icons | `PlayerHubWindow.cs` |
| People | `KnownPersonQueries`: seen → spoken with → familiar (50+) → companion; each line says how it is known; withheld information is named; never memories, private knowledge or a stranger's numbers. "About them" in conversation. | `KnownPersonQueries.cs`, `DialogueWindow.cs`, ADR-0102 |
| Companions | Ability tiles in the party detail; the existing full summary kept (saves, skills, features, spells, resources, loyalty, stories, kit) | `PlayerHubWindow.Party.cs` |

## 4. Existing systems preserved
- The rules engine and its breakdowns.
- Validation.
- Transactional creation, with the kit given once.
- The level-up flow; closing it applies nothing (tested).
- The append-only, keyed XP ledger.
- Rules versioning and outstanding choices.
- NPC genesis.
- Companions.
- Equipment shown on the body: unchanged; height and build join the look's signature, so the body rebuilds only when the look changes.
- The DM's people inspector on the map.

## 5. New content
- **3 lineage choices** with 11 options.
- **15 skin tones**, with names.
- **Character-screen text.** All descriptions are Realmkeeper's own; the mechanics follow SRD 5.2.1 (credited in `CREDITS.md`).
- **No new species, classes or subclasses**, and no setting canon.

## 6. Asset sources and licences
- **No new third-party asset.** Reused:
  - the CC0 Quaternius bodies, heads, hair and outfits;
  - the CC BY game-icons.net icons and OFL fonts from Phase 19.
- **Research:** `Research/Phase20-Character-Assets-and-References.md`. Candidates reviewed:
  - KayKit Adventurers (CC0): rejected on style;
  - MakeHuman exports (CC0): a later art phase;
  - Littlelulu's horns and tail: custom licence, rejected;
  - Dreamloft: unverified;
  - Mixamo: not CC.

  No licensed rigged non-human anatomy fits the Quaternius skeleton.

## 7. Save schema and migration
- **v20 → v21:** see ADR-0101 and `Systems/Persistence.md`.
- **Tested in .NET:**
  - a v20 tiefling keeps fire resistance;
  - people's sheets are migrated;
  - a made choice is kept;
  - a pending character loads at rules 9;
  - absent height and build read as 100 % and average;
  - identity, height, build and a species tone round-trip exactly, and the world fingerprint is unchanged.
- **Older chains:** v5, v6 and v7 to current all pass.

## 8. Tests and exact results

| Suite | When | Passed | Failed | Skipped | Inconclusive | Timed out |
|---|---|---|---|---|---|---|
| .NET (Release), all EditMode test sources | Final code | **841** | 0 | 0 | — | 0 |
| Unity EditMode, full | After all code changes except one description sentence | **841** | 0 | 0 | 0 | 0 |
| Unity PlayMode, full | Same | 121 | **1** | 31 | 2 | 0 |
| Unity PlayMode, `Phase6PlayModeTests` after updating its expected text | Final tests | 2 | 0 | 0 | 0 | 0 |
| Unity PlayMode, `Phase20PlayModeTests` with captures | Same | 1 | 0 | 0 | 0 | 0 |

**The full PlayMode failure.** `Phase6PlayModeTests` read the sheet's old ability row ("● +5"). The Phase 20 tile writes "● save +5": the same value, a changed label. The expected text was updated, not relaxed (the numbers 16, +3 and +5 are still asserted), and the test re-run passed.

**The rest of the full PlayMode run:**
- skipped: the capture sets;
- inconclusive: the known dice-dependent guards (Phase 9, Phase 11).

**Tests changed for real rule changes:**
- the goliath commerce test now chooses an ancestry;
- the schema-18 test asserts v21.

**After the full runs**, the tiefling's description sentence was corrected; it no longer promises fire resistance to every legacy. That is Domain data. The .NET suite was re-run (841/841); the full Unity suites were not.

**New tests:**
- `.NET/EditMode`: `Phase20CharacterTests` (6) and `Phase20PerformanceTests` (1).
- PlayMode: `Phase20PlayModeTests` (1), through the real controls. It walks:
  - cancelling the creator (nothing created);
  - identity with pronouns and a concept;
  - tiefling and legacy, with the problem named until chosen;
  - a species tone, height and build;
  - background, class, skills and review;
  - creation, then the resulting lineage, resistances and appearance;
  - the sheet tiles' explanation and the Origins lineage;
  - "About them" for a new acquaintance (no numbers);
  - recruiting a companion and their tiles, with "About them" at the companion level;
  - a level-up opened and closed (nothing applied);
  - save and load (everything back).

## 9. Performance

| Measure | Result |
|---|---|
| Creator redraw, rules part (draft rules, every breakdown, every step's problems) | 0.19 ms (.NET) |
| Creator redraw in Unity, whole UI, with or without a new look | 4.4 ms / 4.8 ms (no measurable preview cost: the body rebuilds only on a changed signature) |
| Opening a sheet tab in Unity | 1.2 ms each |
| A person's known view | 0.06 ms |
| A sheet's rules from a saved record | 0.02 ms |
| A level-up (suggest, validate, apply) | 0.13 ms |
| Save and load | Unchanged beyond 4 optional fields per sheet |

- **Unity frame time** was not profiled: the batch runs have no meaningful timing.
- **Preview models** are built on demand only, never all species at once.

## 10. Screenshots
`Documentation/Screenshots/Phase20/` holds 25 captures and their log. They are genuine Unity renders driven by the PlayMode test, at 1920×1080 (the creator steps and the sheet overview also at 1366×768). Each was looked at:
1. creator overview;
2. species selection;
3. species detail;
4. appearance;
5. class;
6. background;
7. skills;
8. review;
9. sheet overview;
10. sheet skills;
11. Origins (plus Progression as 11b);
12. level-up;
13. "About them";
14. companion sheet;
15. equipment beside the preview.

**Fixed after earlier captures:**
- the narrow species, class and background picker cards;
- the stretched description spacing on species detail;
- the age wording in "About them".

**Captured before the tiefling description fix**, so p20_02 and p20_03 show the old sentence.

**Not proven by screenshots:** that equipping changes the model. That is covered by the earlier phases' equipment tests and the preview's kit check.

## 11. Human testing
**None.** Claude drives Unity only through tests and captures. The four kinds of evidence:
- automated tests;
- automated UI interaction through the real controls;
- inspected captures;
- human feedback: **none yet**. See `Reports/Phase20-Owner-Playtest-Checklist.md` (sections A–G).

## 12. Known limitations
- **Non-human species use the human body** at their height, with their tones, height and build. Ears, tusks, horns, tails, scales and the draconic head are not modelled; no licensed asset fits. It is labelled "placeholder body" everywhere.
- **Not yet played by the engines.** The goliath ancestry boons and every innate spell (the tiefling legacies' and the gnome lineages') are flagged and shown as such.
- **Eye colour** was not added (the eye texture would tint the whites).
- **Only 8 of the base pack's hairstyles** are in the repository.
- **Subclasses:** one per class (the SRD's).
- **Familiarity 50** in natural play is unmeasured.
- **The DM's people inspector** still shows everything, by design.
- **Not hand-played.**

## 13. Deferred work
- Non-human anatomy (art pass or commission).
- More hairstyles from the CC0 base pack.
- Eye colour with a separate iris mask.
- Drow lineage.
- Innate spells in the magic system.
- Ancestry boons in combat.
- Familiarity pacing review.
- Phase 19's UI backlog (map, HUD, journal, crafting and storage screens) stays separate.

## 14. Git
- **Branch:** `feature/phase-0-1-foundation`.
- **Commits:**
  - `6dce1a2`: Phase 20.
  - The commit after it: Unity's reserialisation of `RealmkeeperPanelSettings.asset` (formatting and one default field, no value changed) and this git section.
- **Pushed to `origin`** at the owner's explicit instruction ("Commit, push it and send report").
- **Not committed and left untouched:**
  - `ProjectSettings/GraphicsSettings.asset` (unmodified);
  - Unity's `InitTestScene*`;
  - the ignored Quaternius Bestiary;
  - the ignored development screenshots.
- **Phase 21 has not started.**
