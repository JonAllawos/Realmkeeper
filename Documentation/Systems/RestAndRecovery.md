# Rest and recovery

Phase 9 (SRD 5.2.1; ADR-0040). The code is in `Domain/Rpg/CharacterRest.cs`,
`Domain/Rpg/CharacterVitals.cs` (`Recover`), `Application/SpellCommands.cs` (`WorldSession.ShortRest`,
`LongRest`, `RestProblem`, `FinishRestCommand`) and the Hub's **Spells & rest** section.

## Recovery types are explicit

Every limited resource declares how it comes back (`ResourceDefinition`):

| Field | Meaning |
|---|---|
| `Recovery` | `LongRest` or `ShortRest` (a short-rest resource also returns on a long rest) |
| `ShortRestFromLevel` | A long-rest resource that becomes short-rest from a class level (for example Bardic Inspiration from Font of Inspiration) |
| `ShortRestRegain` | Phase 9: a long-rest resource that regains this many uses on a short rest (Rage, Channel Divinity and Wild Shape regain 1) |
| `RecoveryAmount` | `All` or `Half` on a long rest. Hit dice are `All` (SRD 5.2.1); the Phase 7 rest stub's "half" was a 2014 rule, corrected in Phase 9. |

Examples:
- **Spell slots** (`spell_slots_1…9`) are long rest.
- **Pact slots** are short rest.
- **Sorcery points** and Arcane Recovery are long rest.
- **Favoured Enemy's free casts** are long rest.

## Short rest (1 hour)

The player chooses how many hit dice to spend, from 0 up to those left. The Hub has − and + buttons
for this.

1. **Refusals.** The rest is refused:
   - during a fight;
   - before the character exists;
   - for the dead;
   - for more dice than are left.

   The reason is shown next to the button.
2. **Time.** The world clock advances 60 minutes through `AdvanceTimeCommand`, so the world lives
   that hour: NPC schedules, shops and the simulation tick as for any wait. If the hour cannot pass,
   nothing is recovered.
3. **Recovery** (`FinishRestCommand`):
   - **Hit dice:** each die rolls the class hit die + Constitution (at least 0) and heals that much,
     up to the maximum. The rolls come from the world's deterministic dice, keyed to the rest.
   - **Resources:** short-rest resources return in full; the partial regains add their uses.
   - **Arcane Recovery** (wizard, once per long rest). It restores spent slots of combined level up
     to half the wizard level, rounded up, none above level 5; the highest levels come back first.
4. **Record.** The rest goes in the character's rest log (the last few), with what it gave back.
   It is also shown in the Hub and in the HUD message.

## Long rest (8 hours)

1. **Refusals.** The rest is refused:
   - during a fight;
   - below 1 hit point (heal or spend hit dice on a short rest first);
   - within 16 hours of the end of the last long rest. The SRD allows one long rest per 24 hours,
     and the rest itself takes 8. The wait left is shown: "16 h 0 min until the next can begin".
2. **Time.** The clock advances 480 minutes; the world lives through them.
3. **Recovery:**
   - hit points to the maximum;
   - the dying state is cleared;
   - every resource and every hit die is restored.
4. **Preparation reopens:**
   - **Clerics, druids and wizards:** may change their whole prepared list.
   - **Paladins and rangers:** may swap one prepared spell.
   - **Known casters:** nothing changes; they pick at level-up.

   Starting a fight closes the window: preparation happens between rests, not mid-fight.
5. **Record.** As for a short rest; `LastLongRestEnded` is stored for the 24-hour rule.

## Integrity

- **Idempotent.** Each rest has a key (its kind and end time). A repeated `FinishRestCommand` with
  the same key is refused, so a replay or double click cannot double the recovery.
- **One path.** Recovery goes through copy, validate and commit, like every command.
- **Validation.** `CharacterValidation` (run by `WorldValidator`) checks that every resource exists
  and stays within its maximum.
- **Saved** in schema v11: the prepared list, the preparation window, the last long rest end and
  the rest log.

## Outside a fight

- **Healing spells** can be cast from the Hub with a slot (`CastHealingOutsideCombatCommand`), for
  example Cure Wounds after a fight.
- **Durations.** Other spells and the durations of buffs are not tracked outside encounters yet
  (ADR-0039).
