# ADR-0025: Progression lives in the one character record

**Status.** Accepted, 2026-11-11 (Phase 7).

**Context.**
- Phase 6 made the RPG character one record on the player (ADR-0020). It already held `Level`,
  `Experience`, `SubclassId` and a `Choices` map.
- Progression needs:
  - experience history;
  - choices for every level;
  - the subclass;
  - hit dice;
  - an account of each level-up.
- A separate progression record could disagree with the sheet about level, hit points or
  resources.

**Decision.**
1. **Level choices** go in the existing `Choices` map, under level-scoped keys: `lv{level}:{name}`,
   and `lv{level}:{name}:{local}` for a feat's own choices. The subclass goes in `SubclassId`.
2. **Two append-only histories** are added:
   - `ExperienceLog`, the awards, whose sum must equal `Experience` (ADR-0028);
   - `Advancements`, one record per level after the first.
3. **Everything level-dependent is derived:**
   - maximum hit points, proficiency and resource maximums;
   - features (including replaced ones) and class-table columns;
   - spell slots and the ability scores after increases.
4. **Stored mutable state** is only current and temporary hit points, resource uses (including hit
   dice) and `RulesVersion`.

**Consequences.**
- There is one source of truth, and a rule change re-derives every character on load.
- The validator can check every level's choices against the state before that level.
- Schema v9 adds three small fields; old saves migrate without guessing (ADR-0028).

**Alternatives considered.**
- A `ProgressionRecord` entity beside the sheet: rejected, because the two could disagree.
- Storing maximums per level: rejected, because they are derived.
