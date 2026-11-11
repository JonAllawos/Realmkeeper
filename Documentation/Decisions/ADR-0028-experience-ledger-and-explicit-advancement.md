# ADR-0028: An experience ledger with idempotent awards, explicit advancement, and outstanding choices

**Status.** Accepted, 2026-11-11 (Phase 7). Extends ADR-0023.

**Context.**
- Experience will come from quests, encounters, discoveries and story beats. The same source must
  never pay twice, even if a command is repeated.
- A level-up involves choices, so it cannot happen silently when experience is awarded.
- Characters created under Phase 6 rules lack two level-1 choices that SRD 5.2.1 has.

**Decision.**
1. **Awards are ledger entries**: key, source, source ID, amount, date and reason.
   - The key is `{source}:{sourceId}` (or explicit) and unique within the character, so repeats are
     refused.
   - The ledger sum must equal `Experience`.
   - Amounts are bounded.
2. **Experience only makes a character eligible.** `AdvanceCharacterCommand` applies one or more
   levels in one transaction, each with its required choices, and is refused as a whole on any
   problem.
   - Choices are never overwritten.
   - Each level adds one advancement record. The whole command adds one chronicle entry.
3. **Rules versions.** `RulesVersion` records the rules a sheet's choices were made under. Choices
   introduced later are outstanding: allowed to be missing until the next advancement, which
   requires them and raises the version.
4. **Schema v9 migration.**
   - Rules version 6 for created characters and 7 for others.
   - An empty ledger (any pre-existing experience becomes one "migrated" entry) and no advancement
     records.
   - Full hit dice.
   - The warlock's `spell_slots_1` is renamed to `pact_slots`.
   - Nothing is invented.

**Consequences.**
- Duplicate awards, level skipping, double grants and lost choices are each refused by the command
  and caught by the validator on load. Mutation tests cover them.
- Phase 5 and Phase 6 saves load, and their characters enter progression at the next level-up.

**Alternatives considered.**
- Automatic level-up on award: rejected, because choices need the player.
- Forcing the open level-1 choices at load: rejected, because loading must never modify a character
  beyond the migration.
