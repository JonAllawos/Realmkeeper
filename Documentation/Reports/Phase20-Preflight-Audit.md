# Phase 20: Preflight audit (character systems)

**Date:** 2026-11-12. **Branch:** `feature/phase-0-1-foundation`. **HEAD:** `79099a6` (Phase 19 test results), matching `origin` (Phase 19 was pushed at the owner's instruction). The working tree was clean.

**Preserved and untouched:**
- `ProjectSettings/GraphicsSettings.asset` (unmodified at the start);
- the ignored development screenshots;
- Unity's `InitTestScene*` files;
- the local-only Quaternius Bestiary.

**Tests and save format at the start:**
- .NET 834/834.
- Full Unity EditMode 833/834 at `6f225d8` (the timing bound, since raised by the owner).
- Full PlayMode 121 passed, 0 failed.
- Save schema v20; rules configuration version 8.

## 1. What exists, where, and in what state

| System | Source | State found |
|---|---|---|
| Character record | `Domain/World/PlayerCharacterData.cs` (identity, appearance, inventory), `Domain/Rpg/CharacterSheet.cs` (`CharacterSheetData`: species, background, class, subclass, level, scores, choices, HP, resources, spellcasting, the XP ledger, advancements, conditions) | **Functional**, one record, persisted. Title and history already existed. No pronouns or concept. |
| Rules engine | `Domain/Rpg/CharacterRules.cs` (890 lines): every derived value as a `Breakdown` with sources; roll modes (advantage and its causes); senses, resistances, resources, spellcasting | **Functional** and explainable. Phase 20 builds on it and does not replace it. |
| Catalogs | `RpgCatalog.Species` (12), `.Backgrounds` (12), `.Classes` (12, levels 1–20 in `.Progression`), `.Feats` (12 subclasses, 9 feats), `.Spells` | **Functional.** Each definition is labelled with its source (SRD 5.1, SRD 5.2.1, Realmkeeper original). |
| Species variants | Tiefling: SRD 5.1 fixed fire resistance (= the 5.2.1 *infernal* legacy). Goliath: fixed Stone's Endurance (= the *stone* ancestry). Gnome: Tinker fixed (= the *rock* gnome). Dragonborn ancestry is already a choice. High and Wood Elf are separate species. Dwarf is the SRD 5.1 Hill Dwarf; Halfling the Lightfoot. | **Partial:** the SRD 5.2.1 lineages were missing |
| Subclasses | One per class; all 12 SRD 5.2.1, with features through level 20 | **Functional.** The SRDs contain no other subclasses, so adding any would mean Realmkeeper-original designs. **Not expanded** (brief: no inert subclasses). |
| Validation | `Domain/Rpg/CharacterValidation.cs` (sheet, ledger, choices, prerequisites, scores, rules version), `Application/CharacterDraft.cs` (problems per creation step, in words) | **Functional** |
| Creation | `Application/CharacterCommands.cs` (`CreateCharacterCommand`: one transaction, kit given once), `CharacterDraft` | **Functional** |
| Progression | `Domain/Rpg/CharacterAdvancement.cs`, `CharacterProgression.cs` (outstanding choices from later rules versions), `Application/AdvancementDraft.cs`, `ProgressionCommands.cs` (keyed, idempotent XP awards) | **Functional and transactional** (`Apply` returns a validated copy, or null with reasons) |
| Creator UI | `Exploration/Presentation/CharacterCreatorWindow.cs`: 9 steps, live 3D preview, summary text | **Functional.** The layout was cramped: a 380 px preview, a plain-text summary, name-only pickers, no subclass or road-ahead information. |
| Sheet UI | `PlayerHubWindow.cs` (`Character`, 6 panels: Sheet, Skills, Features, Proficiencies, Progression, Identity) | **Functional, rich in data.** Abilities were a plain table, with no Origins view. |
| Level-up UI | `CharacterAdvancementWindow.cs` (guided, a summary before confirming) | **Functional**. Restyled by the Phase 19 theme. |
| Appearance | `PlayerAppearanceData`: frame, hair, beard, 5 human skin tones, hair colour, cloth tint. `SpeciesAppearance` (`HeightScale`, `PlaceholderNote`, `Features`). `PlayerBodyBuilder` and `HumanoidCharacter`: equipment shown on the body. | **Partial.** Only humans have a finished model. Other species are the human model at their height, honestly labelled "placeholder body". No height or build within a species; no species skin tones. |
| NPC sheets | `Application/NpcCharacterGenesis.cs` (sheets built through the same draft, validation and advancement; deterministic), `NpcCharacterCommandHandlers.View` | **Functional** |
| NPC inspection | Strategic map ▸ People: the DM's full view, with sheet, memories and knowledge | **Missing for the player character.** Exploration had no knowledge-gated view of a person. |
| Companions | `PlayerHubWindow.Party.cs`: the full sheet summary, loyalty, stories, kit | **Functional.** Phase 20 adds ability tiles. |
| Equipment and appearance | `PlayerLook.From(hub)`: worn and wielded items are drawn; rebuilt only when the look's signature changes | **Functional.** Unchanged; height and build join the signature. |

## 2. Dependencies on character data
- **Combat** (`CombatQueries`, combatant building): scores, AC, saves, resistances, resources, features, spells.
- **Rest and recovery**; spellcasting.
- **Items:** equip rules, attunement, proficiency checks.
- **Commerce:** background bargaining.
- **Dialogue:** skill checks.
- **Party:** companion sheets and advancement on rest.
- **NPC simulation:** genesis, promotion.
- **Persistence:** `WorldSaveMapper.Character/Commerce`, migrations v8–v20.

Phase 20 changes none of these interfaces. New lineage effects flow through the existing effect pipeline: resistances, tool proficiency, innate-magic notes, combat-rule notes.

## 3. Problems found

| # | Problem | Phase 20 action |
|---|---|---|
| 1 | SRD 5.2.1 lineages missing (tiefling, goliath, gnome) | Added as species choices (rules 9). A migration gives saved characters the lineage they already had. |
| 2 | The goliath's Stone's Endurance and the other ancestry boons are not played by combat | Flagged as such (`IntegrationNeed.Combat`); the sheet's Origins says so |
| 3 | No player-facing person inspection | `KnownPersonQueries`, plus "About them" in conversation |
| 4 | The creator layout gave the preview too little room; pickers showed names only; no live build | Three-region layout, cards, live build panel, compact mode |
| 5 | No height or build within a species; non-human species had only human skin tones | Height (92–108 %) and build on the real model; species skin tones |
| 6 | Strangers' full sheets are visible in the DM's people inspector | Kept: it is the DM's view by design. The player's view is the new knowledge-gated one. |

## 4. Save compatibility
- Schema v21. `MigrationV20ToV21` walks every character sheet in the save (player, people, companions) and gives tieflings, goliaths and gnomes the lineage matching their old fixed traits. It never overwrites a made choice.
- A character not yet created is set to rules 9, as in the v10 → v11 step.
- New fields (pronouns, concept, height, build) are optional and default when absent.
- No starting equipment, XP or progression is touched.

## 5. Performance concerns
- The creator redraws on every choice: measured at 0.19 ms of rules work.
- The preview rebuilds only when the look's signature changes; this is unchanged.

## 6. Asset limits
No CC0 or CC BY rigged non-human anatomy (ears, horns, tails, scales) fits the Quaternius skeleton. See `Research/Phase20-Character-Assets-and-References.md`.

## 7. Implementation order (as carried out)
1. Lineages and rules 9.
2. Identity and appearance fields.
3. Schema v21 and migration.
4. Knowledge-gated person view.
5. Creator redesign.
6. Sheet (tiles, Origins, tabs).
7. "About them".
8. Companion tiles.
9. Tests, measurements, captures, documents.
