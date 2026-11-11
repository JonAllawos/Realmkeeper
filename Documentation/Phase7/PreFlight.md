# Phase 7 pre-flight: Character Progression & Core Rules Engine

Written 2026-11-11, before any Phase 7 code.

## Repository

| Check | Result |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| HEAD | `f2682a2` Phase 6 (on `1f37bb6` Phase 5) |
| Origin | `277bd48`; the branch is 2 commits ahead, **not pushed** |
| Uncommitted | `ProjectSettings/GraphicsSettings.asset` only |
| Save schema | v8 |
| Baseline | .NET 372, Unity EditMode 372, PlayMode 60 (Phase 6 report) |

**The uncommitted GraphicsSettings change.** I re-inspected it. Three references that Unity wrapped
over two lines are now written on one line each. The GUIDs, file IDs and types are identical, so
the change is formatting only. It stays untouched and out of the Phase 7 commit.

The repository matches the Phase 6 report.

## Design authority

- **`Product/GameDesignBible.md`.** Read in full. Phase 7 is the Bible's "RPG Progression" phase:
  XP, levels, subclasses and class features. Its principles that apply most directly here:
  - 8: progression has real mechanical effects;
  - 14: data-driven;
  - 15: extend, don't duplicate;
  - 16: tests;
  - 20: controlled phases.
- **`Product/ProductFoundation.md`.** It still describes "a persistent fantasy world simulation and
  exploration platform". The conflict with the Bible's full-RPG scope was recorded in Phase 6 and
  is **still unresolved**. It is an owner decision, so Phase 7 builds only what its brief
  authorises and leaves the conflict open.
- **Open questions stay open.** The Bible's §20 lists questions that are not locked: the XP curve,
  the spell list, and the subclass catalogue. Phase 7 uses the SRD 5.2.1 XP table and the SRD's
  single subclass per class **as rules configuration**, not as world canon. Both are documented as
  replaceable.

## What exists and will be extended

| Piece | Phase 6 state |
|---|---|
| `CharacterSheetData` on `PlayerCharacterData.Rpg` | State, IDs, `Level` (always 1), `Experience` (always 0), method, base scores, choices by ID, current and temporary HP, resource uses, kit flag, creation date. `SubclassId` is reserved. |
| `CharacterRules` | Derives the sheet from typed effects (species, background, class level 1, chosen options). `MaxHitPoints` already uses the average for later levels. AC is provisional. |
| `CharacterValidation` | Validates level-1 choices only. Requires non-created characters to have level 1 and XP 0. |
| `ClassDefinition` | Level-1 data plus `SubclassLevel`, `SubclassTitle` and an empty `LaterFeatures` map, reserved for this phase. |
| Resources | `Fixed`, `AbilityModifierMin1`, `PerLevel` and similar formulas. Every caster, the warlock included, uses `spell_slots_1`. Nothing spends or recovers them. |
| Commands | `CreateCharacterCommand` only. |
| UI | Creator (nine steps); the Hub's Character section (Sheet, Skills, Features, Proficiencies, Identity). |

**Level-1 differences from SRD 5.2.1 found in the Phase 6 data:**

| Class | Difference | Plan |
|---|---|---|
| Druid | Missing the **Primal Order** choice (Magician or Warden) | Add it as a level-1 choice "introduced in rules v7" |
| Warlock | Its **Eldritch Invocation** is a placeholder, not a choice | Make it a choice of one invocation, "introduced in rules v7" |
| Rogue | Missing **Weapon Mastery** (2) | Add; no choice is needed |
| Wizard | Missing **Ritual Adept** | Add; no choice is needed |

Existing Phase 6 druids and warlocks must stay valid. The two new choices will therefore be
**outstanding choices**: allowed to be missing on a sheet whose rules version predates them, and
required at the character's next advancement.

## Licensing and sources

I verified the SRD 5.2.1 content online (CC BY 4.0, Wizards of the Coast LLC), against the SRD
5.2.1 text as published in the Fantasy Grounds library.

| Content | In SRD 5.2.1 | Use in Phase 7 |
|---|---|---|
| Character advancement table (XP 0 … 355,000; proficiency +2 … +6) | Yes | The XP thresholds |
| All twelve class tables, levels 1–20: features, scaling columns, cantrips, prepared spells, slots | Yes | Class progression |
| One subclass per class: Berserker, Lore, Life, Land, Champion, Open Hand, Devotion, Hunter, Thief, Draconic Sorcery, Fiend, Evoker | Yes | The one selectable subclass per class |
| Origin feats: Alert, Magic Initiate, Savage Attacker, Skilled | Yes | Feat choices at ASI levels |
| General feats: Ability Score Improvement (level 4+, +2 or +1/+1, max 20), Grappler | Yes | Feat choices at ASI levels |
| Fighting Style feats: Archery, Defense, Great Weapon Fighting, Two-Weapon Fighting | Yes | Fighting Style choices from level 2 |
| Epic Boons (level 19+, +1, max 30): Combat Prowess, Dimensional Travel, Fate, Irresistible Offense, Spell Recall, Night Spirit, Truesight | Yes | The level-19 choice |
| Eldritch Invocations with prerequisites | Yes | Invocation choices; see below for the deferred ones |
| Metamagic options | Yes | Sorcerer choices |

**Not used:**
- Any non-SRD subclass, feat, spell or invocation.
- The SRD's background ability increases and origin feat at creation. The Phase 6 configuration
  (species increases, SRD 5.1 style) stays. Mixing the two would change existing characters, so
  it is a documented configuration decision (ADR), not a silent combination.
- Invocations that need a damage cantrip (Agonizing Blast, Eldritch Spear, Repelling Blast), and
  Lessons of the First Ones (a nested feat). These are deferred until spells exist.

All descriptions are written in Realmkeeper's own words. Every new definition records its source.

## Architectural decisions to make (ADRs)

1. **Progression is the same record.**
   - Level choices go in the sheet's existing `Choices` map under level-scoped keys (`lv4:feat`,
     `lv4:feat:abilities` …). The subclass goes in the existing `SubclassId`.
   - History is appended: an XP ledger, and one advancement record per level.
   - Everything level-dependent is derived.
2. **One progression catalogue per class.**
   - Features by level, with a typed kind, scaling and integration notes.
   - Choices by level, ASI levels, the epic boon, and subclass levels.
   - Resource tables, spellcasting tables and named scalings.
   - Subclasses and feats are catalogues of their own.
3. **The rules engine.**
   - Conditions on effects (no armour, no shield, no heavy armour, wearing armour).
   - Stacking per SRD "combining effects": different sources add up; the same feature does not
     stack with itself; only one base AC formula applies.
   - Advantage and disadvantage as sourced roll modes.
   - Typed prerequisites.
   - Feature replacement (`Replaces`).
   - Breakdowns everywhere.
4. **Advancement is explicit.**
   - XP only makes a character eligible.
   - `AdvanceCharacterCommand` applies one or more levels in one transaction, each with its
     required choices.
   - It is shared by any `CharacterSheetData`: player, companion or NPC.
5. **The XP ledger.** Each award has a key, and keyed awards are idempotent. The ledger sum must
   equal `Experience`.
6. **Schema v9.**
   - Adds a rules version, the XP ledger and the advancement records.
   - Adds hit dice as a resource.
   - Renames the warlock's level-1 `spell_slots_1` to `pact_slots`.
   - Nothing else changes.

## Implementation sequence

1. Pre-flight (this file).
2. Domain data: the XP table, progression definitions for all twelve classes, subclasses, feats,
   invocations, metamagic, and resource and spellcasting tables. Extend catalogue validation.
3. The rules engine: level-aware collection, conditions, stacking, advantage, initiative, passives,
   ability increases with caps, feature replacement, scalings.
4. Domain advancement service and validation (all levels, outstanding choices, ledger, records).
5. Application commands: award XP, advance, spend, restore and recover resources, adjust hit
   points. Queries.
6. Persistence v9 and migration, with tests from Phase 5, 6 and 7 saves.
7. UI: the level-up window, the Hub's XP and progression panel, and the HUD indicator.
8. Tests: .NET and EditMode, mutations, performance; PlayMode with six classes through the real
   UI; captures.
9. Documentation, ADRs, Bible status, report, and a local commit (no push).
