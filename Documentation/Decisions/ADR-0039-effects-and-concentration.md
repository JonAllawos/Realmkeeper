# ADR-0039: Lasting effects live in the encounter; concentration is tracked per caster; all end with the fight

**Status.** Accepted, 2026-11-11 (Phase 9).

**Context.**
- Bless, Hold Person, Rage and Wild Shape all outlast the action that creates them.
- They need durations, repeat saves, concentration and cleanup that survive save and load.

**Decision.**
1. **Effects are encounter data.** Every lasting effect is an `ActiveEffectData` in
   `EncounterData.Effects`. It records an instance ID, the source and the target, the spell or
   feature, the values, and its expiry. Expiry is one of: the start or end of a named combatant's
   turn, after N rounds, with concentration, or with the fight.
2. **Concentration.**
   - A caster's concentration is `CombatantData.Concentration`, and its effects carry its instance
     ID.
   - Ending it (a failed Constitution save, 0 hit points, incapacitation, a new concentration
     spell, or its own duration) removes them all, logged.
   - The save DC is 10 or half the damage, whichever is higher, capped at 30.
3. **Repeat saves.** A condition from a spell that allows them is a `ConditionInstance` with
   `RepeatSave`, `SaveDc` and `SaveAbility`. The save is rolled at the end of the target's turn.
4. **Everything ends with the fight** (`EndAllEffects`). Effects are not tracked between fights.
   - Spells whose duration is longer than a fight say so (Partial).
   - `WorldValidator` refuses a finished encounter that still holds effects.
5. **Saved with schema v11.** `ActiveEffectDto`, `ConcentrationDto`, condition repeat-save fields,
   and combatant resources and form.

**Consequences.**
- A fight saved mid-cast reloads with every effect and concentration intact.
- No ghost buffs survive a fight.
- The cost is that Mage Armour or Aid do not persist through the day. That waits for out-of-combat
  effect timing.

**Alternatives considered.**
- *Effects on the character sheet:* they would survive fights, but need world-time expiry that does
  not exist yet.
- *Effects as conditions only:* too narrow for modifiers such as Bless dice or Rage resistance.
