# The rules engine

`CharacterRules` (Domain, engine-free) is the one place where character numbers are worked out. The
creator, the level-up window, the Hub sheet, shops (prices), exploration (speed, carrying capacity)
and validation all ask it. No UI component calculates a rule itself (ADR-0027).

## Inputs

A `CharacterSheetData` (any character) and an optional `Loadout`: body armour, shield, heavy armour.

- For the player, the loadout comes from their equipment slots.
- A sheet without equipment (a future NPC) is unarmoured.

## Collection

The engine gathers sourced, level-tagged effects from:

1. **The species:** traits and languages.
2. **The background:** skills, tools, languages and feature.
3. **The class:** saves, armour, weapons and tools; level-1 features; and features at every level
   up to the character's.
4. **The subclass:** its features at its levels.
5. **Every choice** made up to the character's level: options, skills, languages, feats (with the
   feat's effects) and ability increases. Expertise is read as a rank.

**Feature replacement.** A feature may declare `Replaces`. Once the replacing feature is active,
the replaced one no longer applies and is not shown. Examples: Extra Attack → Two Extra Attacks →
Three Extra Attacks; Improved Critical → Superior Critical; Divine Intervention → Greater Divine
Intervention.

## Effect kinds

Each effect kind has one meaning. The Phase 7 additions:

| Kind | Meaning | Example |
|---|---|---|
| `AbilityIncrease` | +Value to an ability, up to Limit | ASI feat, Epic Boon, Primal Champion |
| `Advantage` / `Disadvantage` | On a roll target (`initiative`, `skill:X`, `save:X`, `check:X`); Text = condition | Feral Instinct, Remarkable Athlete, Danger Sense |
| `InitiativeBonus` | `proficiency` or Value | Alert |
| `HalfProficiencyUntrained` | Half the proficiency bonus on unproficient checks | Jack of All Trades |
| `SkillAbilityBonus` | An ability modifier (at least Limit) added to a skill | Thaumaturge, Magician |
| `SaveAbilityBonus` | An ability modifier (at least Limit) added to saves | Aura of Protection |
| `Sense` | A named sense and range | Blindsight, Truesight, Devil's Sight |
| `MovementMode` | Climb or swim (0 = walking speed) | Roving, Second-Story Work, Gift of the Depths |
| `ArmorClassBonus` | +AC, usually conditional | Defense |
| `MinimumRoll` | The lowest a d20 counts as on proficient checks | Reliable Talent |
| `FightingStyle` | A style ID; styles never repeat | Fighting Style options and feats |

**Conditions** (`EffectCondition`): always, no armour, no armour and no shield, no heavy armour,
wearing armour. They are evaluated against the loadout. Examples: Unarmoured Movement (no armour
and no shield), Fast Movement and Roving (no heavy armour), Defense (wearing armour).

## Stacking (the encoded rule)

The SRD 5.2.1 "Combining Game Effects" rule, as encoded:

- **Different features add up.** A skill can take its ability, proficiency or expertise, Jack of
  All Trades and an ability bonus from a feature, each listed separately.
- **The same feature never counts twice.** Effects are de-duplicated by source and key.
- **One base AC formula applies.** Without armour, the best unarmoured defence is used (barbarian
  Constitution, monk Wisdom without a shield, draconic Charisma). They never add together.
- **Best value wins** for carrying capacity, darkvision and each sense.
- **Advantage and disadvantage cancel.** Any source of each on the same roll gives a normal roll,
  however many there are. A conditional advantage ("unless incapacitated", "against poison") is
  shown as situational and does not change passive scores.
- **Passive scores** are 10 + the bonus, +5 with unconditional advantage, −5 with disadvantage.

## Explanations

Every number comes with its breakdown.

| Number | What its breakdown lists |
|---|---|
| Ability score | Base, species, each increase (with "capped at N" when it hit a cap) |
| Skill | Ability, proficiency or expertise, Jack of All Trades, feature bonuses; the sources of proficiency; advantage and disadvantage |
| Save | Ability, proficiency, aura bonuses; roll mode |
| Initiative | Dexterity and bonuses; advantage |
| Maximum hit points | Hit die maximum, average levels, Constitution × level, each per-level bonus by source |
| Speed | Species and each applicable bonus |
| Armour class | Phase 8: the worn armour (SRD category, base, Dex cap) or the better of 10 + Dex and the unarmoured defence, the shield if trained, conditional bonuses, and notes for untrained armour or shields and missing Strength (ADR-0033) |
| Spell save DC and attack | Base, proficiency, ability |

**Equipment bonuses.** The armour-class breakdown already reads equipment. No item grants a skill
or save modifier yet; if one does, it becomes an effect with a source and shows in the same
breakdown.

## Prerequisites

`Prerequisite` kinds:
- minimum level;
- any of some abilities at a score or more;
- a feature;
- a pick in the same pool (Pact of the Blade before Thirsting Blade);
- spellcasting;
- a Fighting Style feature;
- not already knowing a style.

`CharacterRules.Unmet` describes the ones that fail ("level 5+", "Strength or Dexterity 13+", "the
pact of the blade invocation"). The level-up window shows that text beside each unavailable option.

## What the engine does not do yet

Phase 8 added dice, attacks, damage, the conditions' combat mechanics and SRD armour (`Systems/Combat.md`). The engine's roll modes now include untrained-armour disadvantage on Strength and Dexterity tests and armour Stealth disadvantage. Still deferred:

- spells;
- concentration;
- rests that pass time;
- most class combat features (see `Systems/Combat.md`, Deferred).

Features whose effects need these systems are acquired, validated and shown. Each carries an
`IntegrationNeed` (combat, magic, rest, dialogue, exploration), so the sheet says what each waits
for instead of pretending.
