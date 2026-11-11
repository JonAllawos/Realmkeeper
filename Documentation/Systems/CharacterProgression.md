# Character progression

Phase 7 turns the level-1 character of Phase 6 into one that grows from level 1 to 20. Progression
is part of the same record, `PlayerCharacterData.Rpg` (`CharacterSheetData`, ADR-0025). There is no
second character model and no separate progression record.

## Experience

- **Thresholds.** The SRD 5.2.1 Character Advancement table (CC BY 4.0), used as configuration:

  | Level | XP | Level | XP | Level | XP | Level | XP |
  |---|---|---|---|---|---|---|---|
  | 1 | 0 | 6 | 14,000 | 11 | 85,000 | 16 | 195,000 |
  | 2 | 300 | 7 | 23,000 | 12 | 100,000 | 17 | 225,000 |
  | 3 | 900 | 8 | 34,000 | 13 | 120,000 | 18 | 265,000 |
  | 4 | 2,700 | 9 | 48,000 | 14 | 140,000 | 19 | 305,000 |
  | 5 | 6,500 | 10 | 64,000 | 15 | 165,000 | 20 | 355,000 |

  The Game Design Bible keeps the final XP curve open, so this is replaceable configuration
  (`ProgressionRules.Thresholds`), not canon.
- **Awards.** `AwardExperienceCommand(amount, source, sourceId, reason, awardKey)`. The source is
  Quest, Encounter, Discovery, Milestone, Story or Other.
- **The ledger.** Every award is a ledger entry: key, source, source ID, amount, date and reason.
  - `Experience` must always equal the ledger's sum, and the validator checks this on every load.
  - **Keys are unique.** An award for a source ID is keyed `{source}:{sourceId}`, or by an explicit
    key, so the same quest, encounter or discovery can never pay twice.
  - Awards without a source ID get the next `award-{n}` key.
- **Limits.** Each award is 1–1,000,000; the total is at most 10,000,000. Anything else is refused,
  and a refused award changes nothing.
- **Eligibility only.** Experience never levels a character up; it makes them eligible.
- **Before creation.** Legacy and pending players can already earn experience. It is kept when
  they create their character.

## Advancement

`AdvanceCharacterCommand(LevelChoices…)` applies one or more levels **in one transaction**:

1. **Checks.** The character must be created, the level must be the next one, and the experience
   must allow it.
2. **Choices.** The level's choices are added under level-scoped keys (`lv4:feat`,
   `lv4:feat:abilities`, `lv2:invocations` …). The subclass goes into the existing `SubclassId`.
   - A key that already exists is refused, so choices are never repeated or overwritten.
   - A key that does not belong to the level is refused.
3. **Hit points.** The maximum hit points are derived again. **Current hit points rise by the same
   amount**: damage is not healed, and nothing is reset.
4. **Resources.** Maximums are derived again. Existing uses rise by the increase in the maximum
   (spent uses stay spent), and a resource that is new at this level arrives full.
5. **History.** An advancement record (level, date, hit points gained, a readable summary) is
   appended, and one chronicle entry ("… rose to level 5 as a fighter of the Champion") is written.
6. **Validation.** The whole sheet, and the whole world, are validated before commit. Any problem
   refuses the entire transaction.

Several levels can be taken at once, each validated against what the previous one left. In the
game, the window takes them one at a time and reopens for the next.

### Outstanding choices (rules versions)

SRD 5.2.1 gives druids **Primal Order** and warlocks an **Eldritch Invocation** at level 1. Phase 6
characters were made without them. Each sheet records the `RulesVersion` its choices were made
under: 6 for Phase 6 characters, 7 from Phase 7.

- **Tolerated until the next level-up.** A choice introduced after the sheet's version may be
  missing ("outstanding").
- **Required at the next level-up.** The level-up window asks for it first, and the advancement is
  refused without it. Afterwards the sheet is at version 7.
- **Nothing is invented.** The migration never guesses these choices.

**Rules version 8 (Phase 9)** adds the spell picks: cantrips, known spells and wizard spellbook
entries (ADR-0038).
- **"Up to" picks.** Unlike earlier choices, they are marked `UpTo`, so they are never outstanding:
  an open spell pick does not block a level-up. It is offered in the level-up window and can be
  filled later from the Hub (`ChooseSpellCommand`).
- **Characters still in creation** are moved to version 8 by `MigrationV10ToV11`. Finished
  characters keep their version.

Detail: `Spellcasting.md`.

## Class progression (levels 1–20)

Each class keeps its Phase 6 level-1 definition and adds:

- **Features by level.** Each has a stable ID, kind (passive, active, limited use, scaling,
  choice), typed effects, a scaling column it reads, the feature it replaces (if any), and the
  systems it waits for.
- **Choices by level.** Expertise, Primal Knowledge, Deft Explorer, Scholar, Fighting Style,
  Blessed Strikes, Elemental Fury, invocations, Metamagic.
- **Feat levels.**
  - 4, 8, 12 and 16 for every class;
  - plus 6 and 14 for the fighter, and 10 for the rogue;
  - the Epic Boon at 19.
- **The subclass level and subclass feature levels.**
- **Resource tables.** Rage, Channel Divinity, Wild Shape, Second Wind, Action Surge, Indomitable,
  Focus Points, Favoured Enemy, Sorcery Points, pact slots, spell slots 1–9 and others.
- **Class-table columns.** Rage damage, weapon masteries, Bardic Inspiration die, Martial Arts die,
  Unarmoured Movement, Sneak Attack dice, pact slot level, Arcane Recovery.
- **Spellcasting tables.** Cantrips and prepared spells per level, and the caster type: full, half
  (the full table at half level, rounded up), or pact.

Per-class summaries are in `Classes.md`. Every number follows the SRD 5.2.1 class tables, which I
verified for Phase 7. A test proves the twelve progressions are distinct.

### Subclasses

The SRD 5.2.1 includes **one subclass per class**, and that is what Realmkeeper offers.

| Class | Subclass |
|---|---|
| Barbarian | Path of the Berserker |
| Bard | College of Lore |
| Cleric | Life Domain |
| Druid | Circle of the Land |
| Fighter | Champion |
| Monk | Warrior of the Open Hand |
| Paladin | Oath of Devotion |
| Ranger | Hunter |
| Rogue | Thief |
| Sorcerer | Draconic Sorcery |
| Warlock | Fiend Patron |
| Wizard | Evoker |

- Each subclass has its features at the class's subclass levels, its own choices (Lore's three
  skills, the Land type, Hunter's Prey, Defensive Tactics, Elemental Affinity, the Champion's extra
  Fighting Style) and its own resources.
- These are SRD rules content, not world canon. **Further subclasses are deferred**: none are
  invented.

### Feats (SRD 5.2.1)

| Category | Feats |
|---|---|
| General | Ability Score Improvement (+2 to one ability, or +1 to two; maximum 20; repeatable); Grappler (Strength or Dexterity 13+) |
| Origin | Alert, Magic Initiate (repeatable with a different list), Savage Attacker, Skilled (repeatable; skills only, tools deferred) |
| Fighting Style | Archery, Defense, Great Weapon Fighting, Two-Weapon Fighting (needs a Fighting Style feature; never the same style twice) |
| Epic Boon | Combat Prowess, Dimensional Travel, Fate, Irresistible Offense, Spell Recall (needs spellcasting), the Night Spirit, Truesight (each +1 to an ability, maximum 30) |

- **When feats are offered.** Feat levels offer General, Origin and (for classes with a Fighting
  Style feature) Fighting Style feats. Level 19 also offers Epic Boons.
- **Checks.** Prerequisites are checked against the character as they were before the level.
  Ability increases are checked against their caps.
- **History.** Every increase is recorded with its source, and the sheet shows each score's
  breakdown.

### Ability increases, and how they combine

The rules configuration is explicit (ADR-0026):

| Source | Rule |
|---|---|
| Species | Phase 6: increases at creation (SRD 5.1 style), with the creation score at most 20 |
| Background | **None.** SRD 5.2.1 background increases and origin feats at creation are not used, because they would change every existing character |
| Level feats | SRD 5.2.1: Ability Score Improvement or another feat at the class's feat levels; capped at 20 |
| Epic Boon | SRD 5.2.1: +1, capped at 30 |
| Capstones | Primal Champion (Strength and Constitution +4) and Body and Mind (Dexterity and Wisdom +4), capped at 25 |

Increases apply in the order they were gained, each with its own cap. Improvements cannot be edited
after confirming.

## Hit points and hit dice

- **Maximum hit points** are derived: the hit die's maximum at level 1; the die's average rounded up
  (`die / 2 + 1`) for each later level (fixed values, no rolling); plus Constitution × level, plus
  per-level bonuses (dwarven toughness, Draconic Resilience); at least 1 per level.
- **Constitution increases count retroactively**, as the SRD intends: the maximum is derived, never
  stored.
- **Current and temporary hit points** are stored. `ChangeHitPointsCommand` applies the rules:
  - damage takes temporary hit points first;
  - hit points stop at 0;
  - healing stops at the maximum;
  - temporary hit points do not stack (the higher amount stays).
- **Hit dice** are a resource: one per level.
  - All return on a long rest (SRD 5.2.1; Phase 9 corrected the earlier "half").
  - On a short rest the player spends any number to heal (each die + Constitution). See
    `RestAndRecovery.md`.

## Resources

Each resource is defined by an ID, a formula (fixed, ability modifier, per level, proficiency
bonus, or a 20-level table), the first level it exists at, and its recovery (short or long rest;
Font of Inspiration switches Bardic Inspiration to short rests from level 5). Each has an amount
(all, or half). Phase 9 adds `ShortRestRegain`: Rage, Channel Divinity and Wild Shape regain one use
on a short rest.

| Command | What it does |
|---|---|
| `SpendResourceCommand(id, n)` | Refused if the resource is unknown, the amount is below 1, or not enough uses are left. A refused spend changes nothing. |
| `RestoreResourceCommand(id, n?)` | Restores up to the maximum (all, when no amount is given). |
| `RecoverResourcesCommand(rest)` | What a short or long rest restores, without passing time (kept for tools and tests). Play uses the Phase 9 rests, which pass world time first (`RestAndRecovery.md`). |

The same operations exist for any sheet in `CharacterVitals`.

## Persistence

Schema v9 (see `Persistence.md`) adds `rulesVersion`, `experienceLog` and `advancements` to the
character record. Level choices live in the existing `choices` map.

## The level-up window

See `CharacterCreation.md` § Level-up for the window, and `RulesEngine.md` for how the sheet
explains every number.

## NPCs and companions

Every service above works on a `CharacterSheetData`, not on "the player":

| Service | Does |
|---|---|
| `CharacterRules` | Derives the sheet, given an optional `Loadout` (what the character wears) |
| `CharacterValidation.CheckSheet` | Validates any character; only `Check(player)` adds player-only checks such as names and appearance |
| `CharacterProgression` | Lists choices |
| `CharacterAdvancement.Preview`, `Apply` | Plan and apply level-ups |
| `CharacterAdvancement.SuggestChoices` | Deterministic choices for characters nobody steers |
| `CharacterVitals` | Resources and hit points |

A test advances a sheet that belongs to no player (a dwarf paladin "companion") to level 7 and
spends and recovers its resources through these services.

**Still to build:** an NPC or companion holding a sheet (a field on `NpcData`), commands that
target it, and the decision of when such characters gain experience. Phase 7 does not implement NPC
levelling or companions.
