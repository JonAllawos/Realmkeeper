# ADR-0034: Death and dying: characters make death saves, creatures die, and nothing comes back

**Status.** Accepted, 2026-11-11 (Phase 8).

**Context.**
- In the SRD, characters make death saving throws, and monsters usually die at 0 hit points.
- Non-lethal fights knock out instead of killing.
- There is no resurrection or medical system yet, and the prompt keeps them out of scope.

**Decision.**
1. **The player and named residents make death saves; creatures die at 0.**
   - DC 10. A natural 20 regains 1 hit point; a natural 1 counts as two failures.
   - Damage taken at 0 hit points is one failure (two on a critical).
   - Massive damage kills outright.
2. **Lethality is chosen per encounter.** A non-lethal fight knocks everyone out (unconscious and
   stable) and never kills.
3. **A dying player is stabilized when the fight ends** (Realmkeeper rule). The dead stay dead.
4. **The death state is saved on the sheet:**
   - fields `DeathSaveSuccesses`, `DeathSaveFailures`, `IsStable` and `IsDead`;
   - validated on save and load;
   - healing wakes the stable and the dying.
5. **The dead cannot be healed or harmed.** A dead player cannot start fights; the game says so and
   offers loading an earlier save.

**Consequences.**
- A lethal practice fight can end the character for good; the HUD labels it lethal.
- Resurrection and recovery are future work.

**Alternatives considered.**
- Treating the player's death as a game over that deletes the save: rejected, because it is
  destructive.
- Making every death non-lethal: rejected, because that contradicts the SRD and the prompt.
