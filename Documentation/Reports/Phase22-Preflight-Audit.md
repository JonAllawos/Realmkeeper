# Phase 22: Preflight audit

**Date:** 2026-11-13. **Brief:** "Phase 22 — Character Creation Integrity, Level 1 Starting Rules, Player-Driven Advancement & Targeted Phase 20A/21 Backfill" (given in chat).

## 1. Starting Git state

- **Branch:** `feature/phase-0-1-foundation`.
- **Last commits:**
  - `72c5916`: the Phase 21 report hash;
  - `68b3063`: Phase 21;
  - `c10a313`: Phase 20A docs, pushed.
- **Uncommitted at the start:** the purchased-kit integration done after Phase 21.
  - **Kit files:** 369 modified, mostly the three kits' FBX files and their licence files. There are 646 untracked entries: the new kit models, textures and their `.meta` files, the integration screenshots, and the combined report.
  - **Code:** the art catalog builder and the art catalog; the region kind on layouts; trees by region and seasonal leaves; doors, signs and furniture; the weather capture test's season.
  - **Docs:** `CREDITS.md` and `Art/AssetInventory.md`.
  - **Never committed:** Unity's `Assets/InitTestScene*` and `ProjectSettings/GraphicsSettings.asset`.
- **Kept apart.** The kit work closes Phase 21 and is committed on its own (section 6) before any Phase 22 change.

## 2. Save schema

- **Current schema:** v23 (Phase 21).
- **Character history:** v9 added the experience ledger, the advancement records and the rules version (ADR-0025, ADR-0028). v22 added eye colour and the boon-used flag (Phase 20A). v23 added weather and the shops' kinds.

## 3. What exists for character creation and advancement

| Area | Finding | Where |
|---|---|---|
| Creation | One command, `CreateCharacterCommand`, builds the sheet with `Level = 1`, `Experience = 0` and full HP and resources. It grants the kit once and is refused once a character exists. | `Application/CharacterCommands.cs` |
| Validation | `CharacterValidation.CheckSheet` runs on every commit and load. It refuses: a level above what the experience allows; an advancement count that is not level − 1; a subclass below its level; any choice key from a level not yet reached ("premature level choice"); resources unknown at the level; HP outside 0 to max. | `Domain/Rpg/CharacterValidation.cs` |
| Level 1 derivation | Class features, resources, slots and scalings are derived from the level (ADR-0025). Nothing level-dependent is stored. | `Domain/Rpg/CharacterRules.cs` |
| Species features | **Not level-gated.** Every species trait applies from level 1. | `CharacterRules.Collect` |
| Experience | A keyed, append-only ledger. Awards only make the character eligible. | ADR-0028, `ProgressionCommands.cs` |
| Advancement | `AdvanceCharacterCommand` applies one or more levels, each with all its choices, in one transaction. `AdvancementDraft` pages through the overview, each required choice (including a picked feat's own) and the review. | `CharacterAdvancement.cs`, `AdvancementDraft.cs` |
| Level-up UI | A "Level up (L)" button that shows only when eligible, a banner on the hub's character pages, the advancement window, and the history list in the hub. | `ExplorationController.Living.cs`, `PlayerHubWindow.cs`, `CharacterAdvancementWindow.cs` |
| Other paths to a sheet | `CharacterDraft.Rules` builds a throw-away sheet for the creator's live figures, not saved. NPC sheets come from `NpcCharacterGenesis` and the NPC commands, not player characters. Tests create players through the same command. | |

## 4. Gaps against the brief

1. **Experience before creation.**
   - A pending player can earn experience, and creation keeps it. A new character can therefore be eligible for level 2 or more at once.
   - **Owner decision (D4), 2026-11-13: "No XP before creation".** Awards are refused until the character exists. Old saves that already hold pre-creation experience keep it, and nothing is deleted.
2. **No formal level 1 check.** There is no validator for the level 1 baseline, and no test matrix over classes, species, lineages and backgrounds.
3. **The creator's review** does not show:
   - level 1 in words, AC, initiative, speed or proficiency;
   - class and species features;
   - cantrips, spells and slots;
   - resources.
4. **The level-up notice is easy to miss.**
   - The award message says "Ready to advance", and a small button appears in the time row.
   - Nothing announces the moment.
   - Nothing records that the player was told.
   - There is no indicator on the HUD beyond that button.
5. **Partial choices are lost.** Closing the level-up window throws the draft away. Nothing about a pending level-up is saved.
6. **Automatic and chosen benefits are not separated** on the overview.
7. **The sheet** does not show the next-level progress as a bar, pending choices, or the features still locked and when they unlock.
8. **The tiefling legacy spells at levels 3 and 5** are not in the data at all. There is no way to show them as locked.

## 5. Phase 20A outstanding items, classified

| Item | Rule level | State found | Phase 22 action |
|---|---|---|---|
| Tiefling casting ability (Int, Wis or Cha) | Level 1, chosen with the legacy | Missing; Charisma is hard-coded (ADR-0103) | Implement as a level 1 choice |
| Gnome (forest): Minor Illusion; Speak with Animals | Level 1 | Data only, marked "needs magic" | Classify; implement only if the engine can play it honestly |
| Gnome (rock): Mending, Prestidigitation | Level 1 | Data only | As above |
| Tiefling legacy spells | Levels 3 and 5 | Absent | Represent as level-gated features, locked before their level and shown when unlocked; mechanics deferred |
| Goliath's Stone's Endurance and Storm's Thunder against spell and hazard damage | Level 1 trait | Offered on attack damage only | Classify; implement if the shared damage path allows |
| Dragonborn breath (SRD 5.2.1 changes) | Level 1 trait | 2d6, once per short rest (2014 rules) | Classify against the documented Realmkeeper rule |

## 6. Phase 21 closeout gate

- The purchased-kit integration was being verified when Phase 22 was given.
- **.NET:** 898 of 898 passed on the kit code.
- **Unity EditMode and PlayMode:** results are recorded in the Phase 22 report.
- The kit work is committed as its own local commit before Phase 22 begins.
