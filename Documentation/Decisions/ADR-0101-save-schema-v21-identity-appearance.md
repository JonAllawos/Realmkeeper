# ADR-0101: Save schema v21: lineages, identity, height and build

**Status.** Accepted, 2026-11-12 (Phase 20).

**Decision.**
1. **Sheet:** `CharacterSheetData` gains `Pronouns` (≤ 24 characters) and `Concept` (≤ 80), both optional and validated like the title and history.
2. **Appearance:** `PlayerAppearanceData` gains:
   - `HeightPercent`: 92–108 % of the species' typical height;
   - `Build`: slight, average or heavy, which scales the body's width.

   Both are drawn on the real model (`PlayerLook`, `HumanoidCharacter`) and are part of the look's signature, so the body rebuilds only when they change.
3. **Skin tones** become one palette, `PlayerAppearanceOptions.SkinPalette`. The five human tones keep indices 0–4; species tones are appended (tiefling, orc, half-orc, goliath, dragonborn, aasimar), so every saved index means the same.
   - `SkinTonesFor(species)` lists what a species can have.
   - `Check(appearance, species)` refuses the rest, in words.
   - Generated residents still draw from the human tones only, so world generation is unchanged.
   - Changing species to one that cannot have the current tone (dragonborn) sets its first tone, and the creator says so (`CharacterDraft.Adjustment`).
4. **`MigrationV20ToV21`** gives every saved tiefling, goliath and gnome sheet (player, people, companions) the lineage matching its old fixed traits, never overwriting a made choice. A character not yet created moves to rules 9. Absent height and build read as 100 % and average.

**Eye colour was not added:** the shared eye texture cannot be tinted without tinting the whites, and a stored value with no visible effect would be data-only.
