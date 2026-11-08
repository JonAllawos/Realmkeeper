# ADR-0008: Copy → simulate → validate → commit per day

**Context.** The specification requires validation before state is committed. A broken day
must never leave the world half-updated.

**Decision.** Every day runs on `WorldData.DeepClone()`. After all systems have run, the
result is checked with `WorldValidator`. Only a valid result replaces the current world; any
exception or validation error discards the copy.

**Consequences.** Strong atomicity, with simple systems that just change the copy in place.
Cost: a full copy per day, O(entities). Trivial now. Once entity counts reach the thousands,
options are copy-on-write per entity, per-system change sets, or validating only the entities
that changed. The scheduler's public API would not need to change for any of them.

**Alternatives.** Mutating in place with an undo log: faster, but every system would have to
record undo steps correctly. Immutable records: allocation-heavy and awkward in C# 9 for
nested collections.
