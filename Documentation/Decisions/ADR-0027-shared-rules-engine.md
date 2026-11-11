# ADR-0027: One shared rules engine with conditions, stacking, roll modes and prerequisites

**Status.** Accepted, 2026-11-11 (Phase 7). Extends ADR-0021.

**Context.**
- Level features add situational rules: speed without armour, AC with armour, advantage on
  initiative, ability modifiers added to skills and saves.
- They also add prerequisites (feats, invocations) and replacements (Extra Attack → Two Extra
  Attacks).
- Calculations repeated in UI code would drift apart.

**Decision.**
1. **`CharacterRules` stays the only evaluator.** It takes any sheet and an optional `Loadout`.
2. **Effects can carry an `EffectCondition`**, evaluated against the loadout.
3. **Stacking follows the SRD "combining effects" rule, encoded explicitly:**
   - different features add;
   - the same source never adds twice;
   - one unarmoured-defence formula applies (the best);
   - best-of for senses and carrying capacity;
   - advantage and disadvantage cancel;
   - conditional advantages are situational.
4. **Typed prerequisites**, with readable descriptions, shared by validation and the UI.
5. **`Replaces`** removes a superseded feature from what applies and what is shown.
6. **`IntegrationNeed`** marks what each feature waits for (combat, magic, rest, dialogue,
   exploration). Nothing is faked to look functional.
7. **Every value has a breakdown.**

**Consequences.**
- The Hub, the level-up window, exploration speed, shop prices and validation all agree.
- Combat, magic and rest phases extend effect handling rather than re-implementing calculations.

**Alternatives considered.**
- Per-feature code hooks: rejected, because they cannot be saved, validated or explained.
