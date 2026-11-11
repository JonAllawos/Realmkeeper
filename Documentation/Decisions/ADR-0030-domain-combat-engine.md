# ADR-0030: Combat is a domain engine driven by commands; time stands still during a fight

**Status.** Accepted, 2026-11-11 (Phase 8).

**Context.**
- Phase 8 adds turn-based combat. The rules must be testable without Unity, persist with the world,
  and never be decided by the UI or the AI.
- The living world advances minute by minute while the player explores (Phase 5).

**Decision.**
1. **`Domain/Combat` holds the whole rules engine:**
   - `CombatEngine`, `CombatStats` and `CombatAi`;
   - the model and the creature catalog.
   It has no Unity dependency.
2. **Encounters are world data** (`WorldData.Encounters`): at most one active, plus the 20 most
   recent finished ones. Everything else (armour class, attacks, speed) is derived on each command.
3. **Every change is an application command** (`CombatCommands.cs`), run through the existing copy,
   validate and commit cycle. The UI reads `EncounterSnapshot` and dispatches commands.
4. **The AI only proposes.** `CombatAi.Next` returns an action; the engine validates and resolves
   it exactly as it would the player's.
5. **Time stands still while a fight is active.**
   - `Advance` and `AdvanceTimeCommand` are refused (`EncounterBlocksTime`).
   - Leaving the settlement is refused.
   - The exploration clock pauses, and resumes afterwards.

**Consequences.**
- Combat rules are covered by .NET tests and checked with mutations. The UI has PlayMode tests that
  click through it.
- Only one fight can run at a time. Simultaneous fights elsewhere in the world would need a
  different time rule.

**Alternatives considered.**
- A MonoBehaviour combat manager: rejected, because it is untestable outside Unity and not
  persistent.
- Real-time combat with pause: out of scope for Phase 8.
