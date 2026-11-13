# Phase 20A: Completion report (character systems completion, mechanical integration and regression closeout)

**Date:** 2026-11-12. **Branch:** `feature/phase-0-1-foundation`, on top of `8531143` (Phase 20, pushed). **Committed as `77240e7` and pushed to origin at the owner's instruction** (section 13).

During the phase the owner also asked for a 3D asset research pass and then for the free assets to be downloaded and integrated. That work is recorded in section 12 and is in the same local commit (section 13).

## 1. Summary
- **Lineage mechanics.** The goliath's six giant ancestries and the tiefling's legacy cantrips are now played by the combat and spell systems that already existed. Chill Touch (SRD 5.2.1) was added with its "cannot regain hit points" effect.
- **Honest labelling.** Every trait and lineage option is shown as active, conditional or not yet played, in the creator and on the sheet.
- **Eye colour.** Ten colours for the player, recolouring only the iris through a mask derived from the CC0 eye texture. Save schema v22.
- **A Phase 20 bug fixed.** The body in the world ignored the chosen height and build.
- **Audits.** Familiarity, progression and the three screen sizes, each with new tests.
- **Not hand-played** (section 10).

## 2. Preflight findings
See `Reports/Phase20A-Preflight-Audit.md`. In short:
- Resistances and tool proficiencies were active.
- Every ancestry boon and innate spell was recorded but consumed by nothing.
- The engines already had the paths needed: after-hit features, damage and hurt reactions, teleport, the spell pipeline.

## 3. Baseline (before any change, at `8531143`)

| Suite | Passed | Failed | Skipped | Inconclusive |
|---|---|---|---|---|
| .NET (Release) | 841 | 0 | 0 | — |
| Unity EditMode, full | 840 | 1 | 0 | 0 |
| Unity PlayMode, full | 122 | 0 | 31 | 2 |

- **The EditMode failure** was `Phase14SaveTests.APhase13Save_WithProgress_GainsTheNewPlacesOnce_AndKeepsWhatItHad`: a 180-second timeout.
  - **Cause:** I ran .NET test suites at the same time as the Unity run, and they competed for the CPU. The whole EditMode run took 67 minutes; the later uncontended run took 36.
  - **Check:** re-run alone on the unchanged project, the fixture passed 3 of 3 in 6.5 s. No threshold was changed.
- **Skipped:** the capture sets, which need `RK_CAPTURE_DIR`.
- **Inconclusive:** the two known dice-dependent guards (Phase 9, Phase 11).
- **Compile warnings in Realmkeeper code:** none.

## 4. Implemented

| Area | What | Where |
|---|---|---|
| Goliath ancestries | Fire's Burn, Frost's Chill and Hill's Tumble after a hit (one per hit; dice doubled on a critical; spell-attack hits count). Cloud's Jaunt as a bonus-action teleport to a clicked square. Stone's Endurance and Storm's Thunder as reactions. Each spends an ancestry use (proficiency bonus per long rest). Companions use the after-hit boons; computer-run goliaths decide their reactions. | `CombatFeatures.cs`, `CombatEngine.Features.cs`, `.Reactions.cs`, `.Attacks.cs`, `CombatAi.Companions.cs`, `CombatWindow.Spells.cs` |
| Tiefling legacy cantrips | Fire Bolt, Poison Spray and Chill Touch known innately and cast with Charisma, including by non-casters. A class that readies the same spell keeps its own ability. | `CharacterRules.InnateSpells`, `CombatEngine.Spells.cs`, `CombatQueries*.cs` |
| Chill Touch | SRD 5.2.1 cantrip; `CombatEffectKind.NoHealing`; species-only (on no class list) | `SpellCatalog.cs`, `SpellModel.cs`, `CombatEngine.Heal` |
| Benefit status | `BenefitStatus`: active, conditional (with when), not yet played (with the missing system). Badges with words in the creator and the sheet's Origins. | `BenefitStatus.cs`, `PlayerHubWindow.cs`, `CharacterCreatorWindow.cs` |
| Eye colour | `PlayerAppearanceData.EyeColour`; the iris mask; `EyeColours`; creator swatches | `Items.cs`, `EyeColours.cs`, `HumanoidCharacter.cs`, `Tools/Art/make_iris_mask.py` |
| Save v22 | Optional `eyeColour` and turn flag `lastHitBoonUsed`; structural migration | `SaveMigrations.cs`, `WorldSaveMapper.*` |
| Fix | The hub's appearance copy dropped height and build, so the world body was always average | `PlayerQueries.cs` |

## 5. What is still not played, and the exact dependency

| Benefit | Missing system |
|---|---|
| Gnome (forest): Minor Illusion, Speak with Animals | No illusion or animal-speech system |
| Gnome (rock): Mending, Prestidigitation | No object-repair or minor-effect system. Tinker's tools do work. |
| Tiefling level 3 and 5 legacy spells | The spells are not in the catalog for this use; a once-per-long-rest innate casting path is needed |
| Tiefling casting ability choice (Int, Wis or Cha) | A new required choice; Charisma is used and the choice deferred (ADR-0103) |
| Stone's Endurance and Storm's Thunder against spell, breath and hazard damage | Reactions are offered on attack damage only, as Uncanny Dodge and Hellish Rebuke already were |
| Dragonborn breath weapon | Unchanged; an area attack on a short-rest resource |

## 6. Audits
- **Appearance and anatomy.** Non-human bodies remain the human model, labelled placeholder. Eye colour was added for the player; generated residents keep brown, so nobody's look changed. The asset research found Quaternius's new Fantasy Races pack; see section 12.
- **Creator and sheet at three sizes.** Captured at 1920×1080, 1366×768 and 1280×720. Nothing clips; the compact layout holds at 1280×720.
- **Familiarity.**
  - Talking gives +2 once per day per person; trading +3 once per day per keeper.
  - 50 is reached in 25 days of daily talk, or 10 days of talk and trade.
  - Repeats the same day add nothing, and loading a save does not reset the day.
  - Hostility, death and a departed companion never reveal more than the acquaintance allows.
- **Progression.** The existing 1–20 walk was sound. New checks cover every class as a tiefling, goliath and gnome: nothing outstanding at any level, the subclass arriving at level 3 and not before, the lineage never dropped, ancestry uses equal to the proficiency bonus.

## 7. Save schema and migration
v21 → v22 is structural: both new fields are optional, and absent means exactly what a v21 save meant (brown eyes; no boon used). Tested:
- a v21 save loads at v22 with brown eyes;
- eye colour round-trips with an unchanged world fingerprint;
- out-of-range colours are refused.

## 8. Tests and exact results (final code)

| Suite | Passed | Failed | Skipped | Inconclusive |
|---|---|---|---|---|
| .NET (Release) | **868** | 0 | 0 | — |
| Unity EditMode, full | **868** | 0 | 0 | 0 |
| Unity PlayMode, full | **127** | 0 | 31 | 2 |
| Unity PlayMode, Phase 20A and asset tests with captures | 5 | 0 | 0 | 0 |

- **All four were run on the final code**, one at a time (EditMode 35 min, PlayMode 8 min).
- **Skipped and inconclusive** are the same as the baseline: the capture sets, and the two dice-dependent guards.
- **Compile warnings in Realmkeeper code:** none.
- **An earlier complete run** (before the shield, header and scale fixes) gave the same numbers.
- **Unity runs blocked once.** The first attempt at the Unity stages aborted because the Unity editor had been opened on the project; they were re-run after it was closed.
- **One odd .NET count, not reproduced.** The chain's own .NET stage on the final code reported 535 passed, 0 failed (333 tests not counted). Re-run immediately after the Unity stages, it reported 868 of 868, as did Unity EditMode on the same sources. I do not know the cause of the short count.

**New tests (27 in .NET and EditMode, 5 in PlayMode):**
- `Phase20ALineageTests` (16):
  - each ancestry through the real engine;
  - the three cantrips cast by a fighter with Charisma;
  - a wizard keeping its own Fire Bolt;
  - Chill Touch stopping healing;
  - a computer-run goliath's reaction and a companion's boon;
  - the benefit classification;
  - what stays flagged.
- `Phase20AFamiliarityTests` (6).
- `Phase20AProgressionTests` (2).
- `Phase20AAppearanceTests` (3).
- A boon assertion in `Phase20CharacterTests` updated to the new rule.
- `Phase20APlayModeTests` (2), through the real controls: creator badges, eye colour reaching the preview and the world body with the chosen height and build, sheet Origins, the innate cantrip in a fight, Cloud's Jaunt by clicking the grid.
- `AssetIntegrationPlayModeTests` (3): section 12.

**Tests changed for real rule changes:**
- the Phase 20 "no boon is played yet" assertion, now the opposite;
- the pinned schema version, 21 → 22.

**One intermittent failure, not reproduced.** During drafting, `Phase20AProgressionTests` failed once in a filtered run, after 80 ms and with no message captured, while another process was building the same test binaries. It passed in every later run, including all full runs.

## 9. Screenshots
`Documentation/Screenshots/Phase20A/`: genuine Unity renders driven by the PlayMode tests, each looked at.
- **Creator:** goliath badges, gnome "not yet played", tiefling legacy, eye colour. Three sizes each.
- **Eyes:** a close-up of the world body's blue eyes.
- **Sheet:** Origins with badges, and the overview. Three sizes each.
- **Combat:** the innate cantrip (three sizes), Cloud's Jaunt aiming (three sizes), after the jaunt.
- **Items:** the item gallery (three pages).

**Found in the captures and fixed before the final runs:**
- the shield was held back to front;
- the spells header read "save DC 0, attack +0" for a tiefling fighter: it now shows the innate figures under "Innate spells";
- keys too small, backpack and great axe too large.

## 10. Human testing
**None.** Evidence is automated tests, automated interaction through the real controls, and inspected captures. `Reports/Phase20A-Owner-Playtest-Checklist.md` lists what only the owner can judge.

## 11. Known limitations and deferred work
- The items in section 5.
- **Balance.** Companions and generated goliaths now use their ancestry in fights; the effect on encounter balance is unmeasured.
- **Eye colour** is the player's only; no species-specific eyes (a tiefling's solid colour).
- **Sound** is a first layer (section 12): no ambience, no spatial sound, and batch test runs cannot confirm audible output.
- **Not hand-played.**

## 12. Asset research and free asset integration (owner's instructions during the phase)
- **Research.** `Research/Realmkeeper-Asset-*.md` and `Realmkeeper-Existing-Asset-Inventory.md`: six files. No purchase was made.
- **Owner decisions:** integrate the free assets; **weather stays inside Unity**; the repository is **public**, so store-licensed assets stay out of it.
- **Integrated (all CC0 unless noted):**
  - **Medieval Village MegaKit:** the remaining 103 models. The catalog now has 176 building modules.
  - **KayKit Fantasy Weapons Bits:** 31 models. With owned Fantasy Props, **27 items now use art models** in shops and in hands.
  - **Kenney RPG Audio:** the game's first sounds (footsteps, door, coins, pages), with a volume setting.
  - **Quaternius Universal Animation Library 2:** 42 clips imported on the humanoid rig. Not yet driven; that is Phase 21's NPC work activity.
  - **Kenney Fantasy Town Kit:** 167 models catalogued as `town_*`. Not yet placed.
  - **Quaternius Characters – Fantasy Races** (QAL, no redistribution): Elf and Orc bodies in a git-ignored local folder. Not wired to species: the bodies are unclothed and matching outfits are not released.
- **Not downloaded:**
  - the Sonniss bundles (9–48 GB; no ambience system yet);
  - two free Unity Asset Store packs (they need the owner's Unity account).
- **Regenerated by the art setup:** the art catalog, the two animator controllers and the head prefabs. Same content with new internal IDs.
- **Tests:** `AssetIntegrationPlayModeTests` checks:
  - catalog counts and unique IDs;
  - every item mapping resolving at a believable size;
  - every sound loading and following the volume setting;
  - the animation library on the humanoid rig.

## 13. Git
- **One local commit** on `feature/phase-0-1-foundation` holds Phase 20A and the asset integration together. The two share files (the body builder, the exploration controller, the credits), so splitting them would have left a commit that does not compile. It is `77240e7`.
- **Pushed to `origin`** at the owner's explicit instruction ("can you commit and push all the latest implementations before I purchase unity assets"), as `77240e7` plus the commit recording this. No later push is implied.
- **Left untouched and uncommitted:**
  - `ProjectSettings/GraphicsSettings.asset`;
  - Unity's `InitTestScene*`;
  - the git-ignored Bestiary and Fantasy Races folders;
  - development screenshots.
- **Phase 21 has not started.**
