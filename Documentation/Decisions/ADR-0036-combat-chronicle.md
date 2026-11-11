# ADR-0036: Combat reaches the chronicle as a few moments, never as a log of swings

**Status.** Accepted, 2026-11-11 (Phase 8).

**Context.**
- The world chronicle is the shared history that every system reads.
- A fight produces dozens of rolls. Recording each one would flood the chronicle and the NPCs'
  memories.

**Decision.**
1. **The encounter keeps its own bounded log** (500 entries), with every roll explained.
2. **The chronicle gets only these events, each tagged `source: combat`:**
   - `EncounterStarted` and `EncounterEnded` (Minor);
   - `CombatantDowned` for people (Notable);
   - `CombatantKilled` for the player;
   - `NpcDied` (cause "combat") for residents.

**Consequences.**
- A practice fight adds two to five chronicle entries. Tested: always fewer than the log's entries,
  and at most 2 + 2 per combatant.

**Alternatives considered.**
- A chronicle entry per attack: rejected, because it floods the chronicle.
- No chronicle entries at all: rejected, because deaths and fights must be part of history.
