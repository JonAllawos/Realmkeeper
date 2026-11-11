# Spellcasting

Phase 9 (rules version 8, save schema v11). The decisions behind this system are in ADR-0037 to
ADR-0039 and ADR-0042. The catalog, with every spell's status, is in `SpellCatalogAudit.md`.
Rests are in `RestAndRecovery.md`.

## One engine for everyone

Spells are data, and one engine resolves them for every caster: the player, residents and
creatures. Companions will use the same path.

| Layer | Where | What |
|---|---|---|
| Domain | `Domain/Magic/SpellModel.cs`, `SpellCatalog.cs`, `SpellAccess.cs` | Definitions, the effect model, the catalog, and what a character can cast |
| Domain | `Domain/Combat/CombatEngine.Spells.cs`, `.Effects.cs` | Casting in an encounter: legality, targets, areas, attacks, saves, effects, concentration, expiry |
| Domain | `Domain/Combat/CombatAi.Spells.cs` | Caster decisions (the AI proposes; the engine validates) |
| Application | `SpellCommands.cs`, `SpellQueries.cs`, `CombatQueries.Spells.cs` | Prepare, choose, rest and heal-outside-combat commands; the spellbook and combat views |
| Infrastructure | `SaveFileDtos.cs`, `WorldSaveMapper.*`, `SaveMigrations.cs` | Schema v11 and `MigrationV10ToV11` |
| Presentation | `PlayerHubWindow.Spells.cs`, `CombatWindow.Spells.cs` | The Hub's **Spells & rest** section; the encounter panel's Spells and Class features sections and the cast composer |

## Spell definitions

A `SpellDefinition` records the following:

- **Identity:** ID, SRD name, level (0 = cantrip) and school.
- **Casting:** casting time (action, bonus action or reaction), range, touch, and the verbal,
  somatic and material components.
- **Targets:**
  - targeting: self, creatures, a point, or an area from the caster;
  - who it affects: hostile, ally or any;
  - the maximum targets, and extra targets per slot level above.
- **Area:** sphere, cube, cone, line or cylinder, with its size in feet.
- **Resolution:** a spell attack, a saving throw (with the ability), or automatic.
- **Effects:** an ordered list of `SpellEffect`s (below).
- **Duration:** instantaneous, timed or concentration, with the duration text.
- **Scaling:** upcasting (extra dice, targets or instances per slot level) and cantrip scaling by
  character level (5, 11, 17).
- **Classes:** the class lists it is on.
- **Status:** its `ImplementationStatus` (Functional, Partial, DataOnly or Deferred) and a status
  note.
- **Description:** in Realmkeeper's own words. No SRD prose is copied.

`SpellCatalog.Validate()` checks the following:
- IDs are unique and every class ID exists;
- levels run 0–9 and areas have sizes;
- save spells name an ability;
- a non-Functional status has a note.

A test runs it.

### The effect model

`CombatEffectKind` covers the following:

- **Damage and healing:**
  - damage (dice, type, half on a save, dice per slot above);
  - healing (dice plus the spellcasting modifier);
  - temporary hit points; a maximum hit point increase (Aid).
- **Conditions:** apply a condition, with a repeat save at the end of each turn where the spell
  allows one; end a condition (Lesser Restoration).
- **Modifiers:**
  - armour class bonus or base;
  - speed change;
  - attack and save dice (Bless +d4, Bane −d4);
  - advantage against the target (Faerie Fire, Guiding Bolt);
  - the next attack at disadvantage (Vicious Mockery).
- **Other effects:**
  - no reactions;
  - teleport (Misty Step);
  - invisibility, ended by a hostile act unless greater;
  - haste;
  - a marked target with extra damage (Hunter's Mark);
  - a smite rider;
  - a dash as a bonus action;
  - dispel.

Feature effects share the store: Rage, Reckless Attack, Inspiration, Wild Shape, Innate Sorcery and
weapon bonuses.

Every effect that lasts is an `ActiveEffectData` in `EncounterData.Effects`. It records:
- an instance ID, the spell or feature, the source and the target;
- whether it belongs to a concentration;
- its expiry: at the start or end of the source's or the target's turn, after N rounds, with
  concentration, or with the fight.

## Casting in a fight

`CastSpellAction(spellId, slotLevel, targets, point, metamagic, freeResource)` goes through
`CombatEngine.Act`, like every other action.

1. **Legality** (`SpellProblem`). The cast is checked for:
   - the caster's turn and the action economy (action, bonus action, and one levelled slot per
     turn);
   - a ready spell;
   - a slot at or above the spell's level (or the free use named);
   - the target count and the targets' side;
   - range, from the caster to each target or to the point;
   - incapacity, Rage (no spells while raging) and Wild Shape (no spells in a beast form);
   - a casting time longer than a turn;
   - metamagic: points, and the option's rules.

   Each refusal gives the reason in words. The panel shows it next to the spell.
2. **Cost.** The slot (or pact slot, sorcery points, or free use) is spent once.
3. **Targets.** The engine works out who is affected.
   - **Chosen creatures:** taken as given.
   - **Areas** (`AreaSquares`): the shape is laid out on the 5-ft grid.
     - **Distance:** the grid's own (Chebyshev), so a sphere covers a square of squares.
     - **Sphere, cylinder:** within the radius of the point.
     - **Cube at a point:** centred on it.
     - **Cube from the caster:** the squares next to the caster, toward the point.
     - **Cone:** within its length and its angle (a half-angle of about 26.6°, so its width equals its
       distance).
     - **Line:** 5 ft wide, straight toward the point.
4. **Resolution, per target.**
   - **Attack:** the shared `AttackRoll`, with advantage, disadvantage and criticals exactly
     as for weapons. A critical doubles the dice.
   - **Save:** the shared `SavingThrow`, against the caster's spell save DC (8 + proficiency +
     ability). It includes the target's proficiencies, the Paladin's Aura of Protection, Bless and
     Bane, Danger Sense and conditions. A success halves the damage or negates the effect.
   - **Automatic:** no roll.
5. **Effects** apply in order, through the shared `ApplyDamage` (resistance, vulnerability, immunity,
   temporary hit points, concentration checks, death and dying) and `ApplyCondition`.
6. **Log.** Every roll is written in words: "d20 12 +1 = 13 vs DC 13: success; 3d6 [3, 5, 6] = 14,
   half on a successful save: 7 fire".

**Upcasting.** A slot above the spell's level adds the spell's own `PerSlotAbove` dice, targets or
instances. For example, Magic Missile gets one more dart per level and Cure Wounds 2d8 more per
level. **Cantrips** scale by character level.

**Metamagic** (sorcerer): Careful, Distant, Empowered, Heightened, Quickened and Twinned are
simulated; the other four are listed as Partial. Quickened turns an action spell into a bonus-action
cast.

## Concentration

- **One at a time.** A caster concentrates on one spell (`CombatantData.Concentration`); casting a
  second concentration spell ends the first.
- **Damage.** Damage forces a Constitution save. The DC is 10 or half the damage, whichever is
  higher, capped at 30.
- **Ending.** Concentration ends at 0 hit points, when incapacitated, or when the fight ends.
- **Cleanup.** When it ends, every effect tied to it is removed from every target, and the log says
  so.
- **Display.** The initiative row reads "concentrating on Bless". The inspector repeats it, with the
  effects on each target.

## Durations and time

- **Expiry in fights.** Effects expire in encounter time: rounds, and the start or end of a named
  creature's turn.
- **End of the fight.** Every effect ends when the encounter ends (`EndAllEffects`). Durations
  longer than a fight (Mage Armour's 8 hours, Aid) are marked Partial and noted. Effects out of
  combat are not tracked yet; this is a recorded limitation (ADR-0039).
- **Outside a fight:**
  - healing spells can be cast from the Hub (`CastHealingOutsideCombatCommand`), spending a slot;
  - other spells are combat-only for now.

## What a character can cast (spell state)

`CharacterSheetData.Spellcasting` (`SpellcastingData`) holds:
- the **prepared list**;
- whether preparation can change now (`PreparationChanges`: −1 any, 0 none, 1 one swap);
- when the last long rest ended;
- a bounded **rest log**.

Known spells, cantrips and spellbook entries are **ordinary character choices** (ADR-0038):

| Keeping | Classes | Picks | Preparation |
|---|---|---|---|
| Known | Bard, Sorcerer, Warlock | `class:cantrips`, `class:spells`, then `lv{L}:cantrips`/`lv{L}:spells` | None: every known spell is ready |
| Prepared | Cleric, Druid | `class:cantrips`, `lv{L}:cantrips` | From the whole class list up to the prepared count; any change after a long rest |
| PreparedOneSwap | Paladin, Ranger | none | Up to the prepared count; one swap after a long rest |
| Spellbook | Wizard | `class:cantrips`, `class:spellbook` (6), `lv{L}:spellbook` (2) | From the book up to the prepared count; any change after a long rest |

- **Picks are "up to".** A player may leave picks open and choose them later from the Hub
  (`ChooseSpellCommand`). Open picks do not block creation or level-up.
- **Level limits.** A pick offers only spells of a level the character can cast.
  - Full casters reach spell level L at class level 2L−1.
  - Half casters reach it at 4L−3.
  - Pact casters follow the full-caster schedule, with their slot level from the class table.
- **Always prepared.** Some spells are always prepared and do not count against the limit:
  - the Life, Devotion and Fiend subclass lists;
  - the Paladin's Smite (Divine Smite);
  - the Ranger's Favoured Enemy (Hunter's Mark, cast free a number of times per long rest).
- **Validation.** `CharacterValidation.CheckSpellcasting` checks every stored pick and prepared
  list: on the list, of a castable level, within limits, and with no duplicates. A tampered save is
  refused.
- **Pact Magic is distinct.**
  - The `pact_slots` resource is spent at the pact slot level only (`SlotsLeft` returns 0 for other
    levels).
  - It recovers on a short rest.
  - It is shown as "Pact slots (level N)".

## NPC and creature casters

- **Creature definitions.** A creature definition can carry `CreatureSpellcasting`: ability, DC,
  attack, cantrips, spells and slots. Its slots become per-combatant resources when the encounter is
  created.
- **Two SRD 5.1 casters** are defined, with Realmkeeper's own descriptions:
  - **Acolyte:** Sacred Flame, Bless, Cure Wounds;
  - **Cult Fanatic:** Sacred Flame, Inflict Wounds, Shield of Faith, Hold Person.
- **Residents.** A resident can fight with a caster profile (`CreatureId` override "acolyte") while
  keeping their identity.
- **The caster AI** (`CombatAi.Spells.cs`) scores the ready spells deterministically and proposes the
  best one when it beats the weapon. In order, it:
  1. heals a dying ally, or one at a quarter of its hit points or less (a bonus-action spell first);
  2. blesses allies when not already concentrating;
  3. uses damage or control on foes. Each area counts the foes it catches and the allies it would
     hurt.

  A slot is spent only when it beats the best cantrip or attack by half again. It is never spent on
  a foe a cantrip would finish, or to replace a concentration already running. The engine validates
  every proposal.
- **Practice.** "Practice vs caster (G)" adds a Cult Fanatic to the practice bandits.

## Presentation

- **Hub, Spells & rest:**
  - ability, spell save DC and attack with their breakdown;
  - slots as pips (pact slots labelled);
  - the open spell picks;
  - the preparation editor with limit and window ("3 of 4 prepared · you can change the list now");
  - the spell list with search and level and type filters;
  - a detail view: casting time, range or area, components, duration, effects in words, upcasting,
    classes, status note and SRD source;
  - rest controls and the rest log; the class features with their combat status.
- **Encounter panel:**
  - **Spells section:** each spell with its cost and, if it cannot be cast, why.
  - **The composer:**
    - slot level choice, and the free use where there is one;
    - targets (out of range or wrong side disabled, with the reason);
    - for points and areas, aiming on the grid with the affected squares shaded;
    - metamagic options with their point cost;
    - Cast and Cancel.
  - **Sections fold.** A section the player folds stays folded until the window closes.
- **Inspector:** concentration, effects, spell DC and attack, and the resources left.

## Limits (recorded, not hidden)

- **Reactions.** Reactions are automatic (ADR-0033), so Shield, Hellish Rebuke and Counterspell are
  Deferred.
- **Durations.** Durations beyond a fight are not tracked outside encounters.
- **Not simulated yet:** summoned entities, moving auras, light, stealth, sight and difficult
  terrain created by spells.
- **Rituals.** Ritual casting is not offered.
- **Third casters.** The SRD subclasses present include no third-caster subclasses (Eldritch Knight,
  Arcane Trickster), so none were invented.
