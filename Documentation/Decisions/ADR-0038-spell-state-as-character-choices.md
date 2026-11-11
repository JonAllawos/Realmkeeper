# ADR-0038: Known, spellbook and cantrip picks are character choices; preparation is separate state

**Status.** Accepted, 2026-11-11 (Phase 9).

**Context.**
- Phase 7 records every character decision as a `ChoiceSlot`. The slots are validated, saved and
  re-offered at level-up (ADR-0025, ADR-0028).
- Phase 9 needs known spells, cantrips, spellbooks and prepared lists for four keeping styles.

**Decision.**
1. **Picks are ordinary choices.** Cantrips, known spells and spellbook entries are ordinary choice
   slots:
   - `class:cantrips` and `lv{L}:cantrips`;
   - `class:spells` and `lv{L}:spells` (bard, sorcerer, warlock);
   - `class:spellbook` (6) and `lv{L}:spellbook` (2) for the wizard.

   They are added by `RpgCatalog.AddSpellChoices`.
2. **Picks are "up to".** Spell slots are marked `UpTo` and introduced in rules version 8. An open
   pick never blocks creation or advancement; it can be filled later from the Hub
   (`ChooseSpellCommand`).
3. **Preparation is state, not a choice.** It changes with rests, not levels.
   `CharacterSheetData.Spellcasting.Prepared` holds the prepared list. `PreparationChanges` records
   the window: any change, one swap, or none. A long rest opens the window; a fight closes it.
4. **Always-prepared spells are derived, never stored.** These are subclass lists, the Paladin's
   Smite and Favoured Enemy's Hunter's Mark.
5. **Rules version 8.**
   - `MigrationV10ToV11` sets rules version 8 on characters still being created, so they meet the
     new picks.
   - Finished characters keep their version. Their new picks appear as open picks when they next
     level, or from the Hub.

**Consequences.**
- One validation path (`CharacterValidation.CheckSpellcasting`) covers creation, advancement,
  saves and tampering.
- Companions and NPCs with character sheets get spell state for free.

**Alternatives considered.**
- *A separate spell inventory:* it would duplicate choice validation and the level-up workflow.
- *Mandatory picks:* they would block old characters on load.
