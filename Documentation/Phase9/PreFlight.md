# Phase 9 pre-flight: Spellcasting, Class Features & Combat Integration

Date: 2026-11-11. Written before any Phase 9 code change.

## 1. Repository baseline

| Item | Value |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| HEAD | `2fe83eb` (Phase 8) |
| origin/feature/phase-0-1-foundation | `2fe83eb` (fetched; 0 ahead, 0 behind) |
| Working tree | `ProjectSettings/GraphicsSettings.asset` modified (the owner's change; preserved, never staged). Nothing untracked. |
| Save schema | v10 (`WorldData.CurrentSchemaVersion`) |
| Rules version | 7 (`ProgressionRules.CurrentRulesVersion`) |
| .NET baseline | 470 passed, 0 failed, 0 skipped (re-run now) |
| Unity baseline (Phase 8 run) | EditMode 470/470; PlayMode 66 passed, 0 failed, 6 skipped (the capture sets) |

## 2. Documentation read

- **Status and roadmap:** the Bible's status section, `ProductFoundation.md` and the Roadmap.
- **Systems docs:** Combat, CharacterProgression, RulesEngine, Characters, Classes, Persistence and
  SettlementStructures.
- **Reports and pre-flights:** Phases 6–8.
- **ADRs:** 0020–0036.

Several paths named in the brief do not exist as given. The documents live under
`Documentation/Systems/` and `Documentation/Product/`, and there is no separate
`Realmkeeper-Game-Design-Bible.md` (the Bible is `Product/GameDesignBible.md`).

## 3. Spellcasting as it exists (verified in code)

The data is in `RpgCatalog.Classes.cs` and `RpgCatalog.Progression.cs`.

| Class | Ability | Style (data) | Progression | Cantrips (L1/4/10) | Prepared table |
|---|---|---|---|---|---|
| Bard | Cha | Known spells | Full | 2/3/4 | 4…22 |
| Cleric | Wis | Prepared | Full | 3/4/5 | 4…22 |
| Druid | Wis | Prepared | Full | 2/3/4 | 4…22 |
| Paladin | Cha | Prepared | Half | — | 2…15 |
| Ranger | Wis | Prepared | Half | — | 2…15 |
| Sorcerer | Cha | Known spells | Full | 4/5/6 | 2…22 |
| Warlock | Cha | Pact magic | Pact (`pact_slots`, `pact_slot_level` scaling) | 2/3/4 | 2…15 |
| Wizard | Int | Prepared from a spellbook | Full | 3/4/5 | 4…25 |
| Barbarian, Fighter, Monk, Rogue | — | — | — | — | — |

- **Slot resources.** Slots are typed resources: `spell_slots_1…9` (full casters), `spell_slots_1…5`
  (half casters), and `pact_slots`. They recover on a long rest; pact slots on a short rest.
- **Derived values.** `CharacterRules` derives the spell save DC, the spell attack, cantrip counts
  and prepared counts.
- **Not present:**
  - spells, a spell catalog, or known, prepared or spellbook state;
  - spell effects and concentration;
  - spell picks at creation or level-up.
- **Subclasses (one each, SRD 5.2.1):** Berserker, Lore, Life, Land, Champion, Open Hand, Devotion,
  Hunter, Thief, Draconic, Fiend, Evoker. There is **no Eldritch Knight or Arcane Trickster**, so no
  third-caster access will be invented.
- **Invocations:**
  - 24 exist;
  - Agonizing Blast and Eldritch Spear were deferred in Phase 7 because they need Eldritch Blast;
  - Armour of Shadows names Mage Armour.
- **Metamagic options:** 10.

## 4. Class features as they exist

- **Typed effects.** Features are typed `RuleEffect`s with a `FeatureKind` (Passive, Active,
  LimitedUse, Choice) and an `IntegrationNeed` (Combat, Magic, Rest, Explore, Talk).
- **Functional in combat today (Phase 8):**
  - Extra Attack (2, 3 or 4 attacks);
  - Second Wind;
  - Cunning Action (Dash, Disengage);
  - the Archery, Duelling and Great Weapon Fighting fighting styles;
  - passive roll modes such as Danger Sense, through the rules engine.
- **Data-only:** everything else that is combat-relevant. This includes Rage, Reckless Attack,
  Sneak Attack, Action Surge, Indomitable, Bardic Inspiration, Channel Divinity, Wild Shape, Martial
  Arts, Flurry of Blows, Stunning Strike, Lay on Hands, Paladin's Smite, Aura of Protection, Favoured
  Enemy, Uncanny Dodge, Evasion, Sorcery Points, Font of Magic, Metamagic, Innate Sorcery, Arcane
  Recovery and the subclass combat features.
- **Resources and recovery.**
  - Resources declare `ResourceRecovery` (LongRest or ShortRest), `ShortRestFromLevel` and
    `RecoveryAmount` (All or Half).
  - **Known gap (from Phase 7):** "one use returns on a short rest" is not modelled. This affects
    Rage, Channel Divinity and Wild Shape, which only return on a long rest.
  - Long rests restore half the hit dice, which is the 2014 rule. SRD 5.2.1 restores all spent hit
    dice.
  - No rest command passes time: `CharacterVitals.Recover` exists but is not wired to a rest.

## 5. Phase 8 combat integration points

- **`CombatEngine.Act` and `Unavailable`:** one validation path shared by the UI and the AI. New
  actions (cast a spell, use a feature) plug in here.
- **Shared rule routines to reuse, never duplicate:**
  - `ResolveAttack`, `AttackModes`, `SavingThrow` and `ApplyDamage`;
  - `Heal`, `ApplyCondition` and `RemoveCondition`;
  - the condition durations (start or end of a named turn) and `Expire`.
  A spell attack will reuse the attack pipeline with a spell `AttackOption`. A spell save will
  reuse `SavingThrow`.
- **Stats are cached per command** (`CombatEngine.Stats`). Temporary modifiers from spells and
  features must be read live from the encounter, not baked into the cache.
- **Persistence.** Each encounter persists whole, along with its seed, counter and sequence.
  New per-combatant state (resources, concentration, active effects) belongs in the encounter.
- **Bonus abilities are hard-coded.** `BonusAbilities` holds Second Wind and Cunning Action. They
  will move to a shared, data-driven feature activation model.
- **The AI.** `CombatAi.Next` proposes and the engine validates. It will be extended with spell
  and feature candidates.
- **Time.** World time is frozen during encounters (ADR-0030). Spell durations will run in
  encounter rounds only.

## 6. Spell data and licensing

- **No spell definitions exist anywhere.** Nothing will be duplicated.
- **Source.** Spell mechanics will come from SRD 5.2.1 (CC BY 4.0). The attribution is already
  shipped (`CREDITS.md`, the Hub's Info section, the README).
  - Phase 9 extends the notice to name spells and NPC spellcasters.
  - The two NPC caster profiles (the SRD 5.1 Acolyte and Cult Fanatic) are recorded as SRD 5.1.
- **Wording.** All descriptions will be Realmkeeper's own.
- **Excluded:** no setting lore, no non-SRD spells, and nothing copied from unofficial sources.
- **Uncertain values.** Where the 2024 value of a spell is uncertain, the spell is left out rather
  than guessed.

## 7. Architecture plan

1. **Domain (`Domain/Magic`):**
   - `SpellDefinition` (versioned, with provenance and an implementation status);
   - a structured `SpellEffect` list (damage, healing, temporary HP, conditions, AC, speed, roll
     modifiers, extra damage on hit, resistance, teleport, end a condition);
   - targeting (self, creature(s), ally, hostile, touch, point, and the sphere, cube, cone, line and
     cylinder area shapes on the existing Chebyshev grid);
   - upcasting by definition;
   - catalog validation.
2. **Character spell state: choices plus one new record.**
   - Cantrips, known spells and the wizard's spellbook are new **choices**, so the creator, the
     level-up window, atomic advancement and validation all work unchanged. They are introduced at
     **rules version 8**, so older characters see them as outstanding and fill them in the
     spellbook.
   - Prepared spells, the preparation window and rest history are a new `SpellcastingData` on the
     sheet.
3. **Combat integration.**
   - `CastSpellAction` and `UseFeatureAction` go through `Act` and `Unavailable`.
   - Resolution is a `CombatEngine` partial that calls the existing attack, save, damage, heal and
     condition routines.
   - Timed modifiers are `ActiveEffect` records in the encounter, read by stats, attack modes, saves
     and damage.
   - Concentration lives per combatant: one at a time, Con save DC max(10, damage/2) (capped at 30),
     and ending it removes its effects.
4. **Features.** A data-driven `CombatFeatureCatalog` (activation, cost, resource, target, range,
   effects, status) replaces the hard-coded bonus abilities. Attack riders (Sneak Attack, rage
   damage, Hunter's Mark, smites, masteries) run inside the shared attack resolution.
5. **Rests.**
   - `ShortRest` (spend hit dice) and `LongRest` are application operations that pass world time
     through the existing time advance (1 h and 8 h, so the simulation runs), then apply recovery
     idempotently with a rest key.
   - They are refused during a fight.
   - One long rest per 24 hours.
   - Per-resource short-rest amounts are added ("regain one use").
6. **NPC casters.** Creature profiles gain an optional spellcasting block, using the same engine
   and resources on the combatant. A resident can fight with a caster profile without being
   duplicated. The AI scores spells deterministically.
7. **Persistence.** Schema v11: the sheet's spellcasting record and rest history, plus encounter
   effects, combatant resources, concentration and form. `MigrationV10ToV11` adds empty defaults.
8. **UI.**
   - A Hub "Spells" section: the spellbook, with search, filters, prepare and inspect.
   - In the combat panel, Spells and Features sections with a cast flow: spell, slot, targets or
     area, confirm.
   - Rest buttons on the HUD.

## 8. Risks

| Risk | Handling |
|---|---|
| Scope: twelve classes and their features in one phase | Every feature gets an explicit status (Functional, Partial, Passive, Deferred); nothing unimplemented is offered as usable |
| SRD 5.2.1 values recalled imprecisely | Include only spells whose 2024 mechanics are certain; mark simplifications as Partial |
| Older characters lack spell choices | Rules version 8 choices are outstanding for v7 characters and can be filled in the spellbook; nothing is reset |
| Rest passing world time | Time passes first through the existing advance, then recovery is applied with a rest key (idempotent); both are refused during fights |
| Effects with durations longer than a fight | Only encounter-time durations are supported; longer ones (Mage Armour) last until the fight ends and are marked Partial |
| Reactions (Shield, Counterspell, Hellish Rebuke) | Reactions stay automatic (ADR-0033); reaction spells are Deferred |

## 9. Stages

1. Pre-flight (this document).
2. Spell foundation.
3. Resolution and concentration.
4. Spellcasting progression.
5. Class features.
6. Rests.
7. NPC casters.
8. Persistence, UI, tests, captures and documentation.

Then a local commit (without `GraphicsSettings.asset`), no push, and stop.
