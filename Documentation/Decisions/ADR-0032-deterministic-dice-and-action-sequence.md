# ADR-0032: Counter-based dice per encounter, and an action sequence against stale or repeated actions

**Status.** Accepted, 2026-11-11 (Phase 8).

**Context.**
- Fights must be reproducible: the same save must play on identically, and tests must not depend
  on luck.
- A double click or a stale UI must never apply an action twice.

**Decision.**
1. **Each encounter has a seed and a roll counter.**
   - The seed comes from the world seed and the encounter ID.
   - Die `n` is `Mix(Combine(seed, n)) mod sides + 1`.
   - Only the counter is saved, so the stream continues exactly after loading.
2. **Each encounter has an `ActionSequence`**, incremented by every action. Commands may carry the
   sequence they expect; they are refused when it has moved on.
3. **Initiative ties are broken in this order:**
   - higher initiative;
   - then the higher Dexterity modifier;
   - then a seeded tie-break value;
   - then the combatant ID.

**Consequences.**
- A saved fight and the live one produce the same log (tested). The AI is deterministic for a
  given state.
- The dice are not cryptographically random; that does not matter for a single-player game.

**Alternatives considered.**
- A saved RNG state object: rejected, because the counter is simpler and cannot drift.
- `System.Random`: rejected, because it is not stable across runtimes.
