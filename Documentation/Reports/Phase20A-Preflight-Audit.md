# Phase 20A: Preflight audit (character systems completion)

**Date:** 2026-11-12. **Starting point:** `8531143` on `feature/phase-0-1-foundation` (Phase 20, pushed). Phase 20A commits locally only; nothing is pushed.

## 1. Regression baseline (before any Phase 20A change)

| Suite | Result |
|---|---|
| .NET build (Release) | 0 warnings, 0 errors |
| .NET tests (Release), every EditMode test source | **841 passed**, 0 failed, 0 skipped (4 min 13 s) |
| Unity EditMode, full | 840 passed, 1 failed (a timeout from CPU contention; passes alone: see the completion report §3) |
| Unity PlayMode, full | 122 passed, 0 failed, 31 skipped, 2 inconclusive |


## 2. Lineage mechanics: what the engines actually consume

Traced from `RpgCatalog.Species.cs` through `CharacterRules` to `CombatEngine` (`Features`, `Reactions`, `Spells`, `Attacks`) and `CombatStats`.

| Lineage benefit | Effect in data | Consumed by | Status at baseline |
|---|---|---|---|
| Tiefling legacy resistance (poison, necrotic, fire) | `Resistance` | `CharacterRules.Resistances` → `DamageRules` | **Active** (tested in Phase 20) |
| Tiefling legacy cantrip (Poison Spray, Chill Touch, Fire Bolt) | `InnateMagic` "poison-spray" / "chill-touch" / "fire-bolt" | nothing | **Not played.** `SpellsOf` lists class spellcasting and stat blocks only; `CharacterRules.SpellSaveDc()`/`SpellAttack()` return null for a non-caster. Chill Touch is not in `SpellCatalog`. |
| Tiefling level 3 and 5 legacy spells (SRD 5.2.1) | not in the catalog | — | **Absent from data** (Phase 20 recorded only the cantrip) |
| Goliath Giant Ancestry uses | `Resource` stones_endurance (PB per long rest) | rest recovery | Pool exists and recovers; nothing spends it |
| Goliath Cloud's Jaunt / Fire's Burn / Frost's Chill / Hill's Tumble / Stone's Endurance / Storm's Thunder | `CombatRule` keys | nothing | **Not played.** `HasFeature` reads class/species features only, not option choices. |
| Gnome forest: Minor Illusion, Speak with Animals | `InnateMagic` | nothing | **Not played**; no illusion or animal-speech system exists |
| Gnome rock: tinker's tools | `ToolProficiency` | crafting checks | **Active** |
| Gnome rock: Mending (and Prestidigitation) | `InnateMagic` | nothing | **Not played**; no object-repair system |
| Dragonborn breath weapon | `CombatRule` "breath-weapon" + resource | nothing | Not played (outside this phase's three lineages; recorded) |

**Routes that exist and fit (no new system needed):**
- **AfterHit features** (the Stunning Strike pattern: `FeatureActivation.AfterHit`, `FeatureTargeting.LastHit`, `TurnData.LastHitTargetId`): Fire's Burn, Frost's Chill (the slow uses `CombatEffectKind.SpeedChange` as Ray of Frost does), Hill's Tumble (`ConditionType.Prone`, size from the creature or species).
- **Damage reactions** (the Uncanny Dodge / Deflect Attacks pattern in `ReactToDamage`): Stone's Endurance.
- **Hurt reactions** (the Hellish Rebuke pattern in `ReactToBeingHurt`): Storm's Thunder.
- **Bonus-action teleport**: `CombatEffectKind.Teleport` already validated for Misty Step (occupied, range).
- **Innate cantrips**: `SpellsOf` + a per-spell casting ability; the attack and save resolution is shared.

**Missing and needed:** Chill Touch's rider ("cannot regain hit points") needs one new effect kind consumed by healing. The SRD lets a tiefling pick Intelligence, Wisdom or Charisma for legacy spells; the catalog has no such choice.

## 3. Appearance and anatomy

- Non-human species use the human body (CC0 Quaternius), labelled "placeholder body". Phase 20's research found no licensed rigged anatomy (horns, tails, ears, tusks, draconic heads) for this skeleton; nothing has changed since.
- **Eye colour.** The head's eye texture (`T_Eye_Brown.png`, 256², CC0) paints iris, white and surrounding skin in one map, so a material tint colours the whites. An iris mask derived from that texture lets the iris alone be recoloured. Feasible without new third-party art.
- Preview and exploration use the same `HumanoidCharacter.Build`; the look's signature decides rebuilds.

## 4. Creator and sheet at three sizes
Phase 20 captured 1920×1080 and 1366×768. A compact size (1280×720) was never captured. To audit: clipping, overflow, the compact mode switch (below 1380 px), long names and concepts.

## 5. NPC familiarity
- Talking gives +2 familiarity once per day per person; trading +3 once per day per keeper (`PlayerCommands.Acquaint`). Quests can grant more (`QuestWorld`, "Familiarity" rewards). No decay. Clamped 0–100.
- **50 is reachable naturally:** about 25 days of daily talk, or about 10 days of talking and trading, sooner with quests. It cannot be farmed: repeats on the same day count conversations but add nothing.
- **Privacy:** `KnownPersonQueries` shows a stranger only what is seen; never memories or private knowledge; the DM inspector is separate.
- **Untested at baseline:** repeated same-day talk, hostility, death, a dismissed companion, save and load of regard.

## 6. Progression and subclasses
- `ProgressionTests.EveryClass_AdvancesFromOneToTwenty_ValidAtEveryLevel` already walks all 12 classes from 1 to 20 through suggest, validate and apply: HP rises each level, PB 6 at 20, feat count, resources, every subclass feature present.
- **Not covered:** no outstanding choices left after each level; the subclass absent before its level; the species lineage choice surviving advancement; a goliath's ancestry uses scaling with PB.

## 7. Plan
1. Make the six goliath boons and the tiefling cantrips work through the existing systems; add Chill Touch (SRD 5.2.1) with its healing rider. Use Charisma as the legacy spells' ability (documented; the SRD's choice of three is deferred).
2. Record what still cannot be played (gnome utility magic, tiefling level 3 and 5 spells, breath weapon) with its exact missing system.
3. The sheet and the creator show each benefit as **active**, **conditional** (works only in a situation, such as a reaction or a hit) or **not yet played**.
4. Eye colour through a derived iris mask: player only, saved (schema v22), in the creator preview and the world.
5. Tests for every item, the familiarity cases and the progression gaps; captures at three sizes; documents; local commit.
