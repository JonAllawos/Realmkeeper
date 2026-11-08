# ADR-0004: Deterministic randomness

**Context.** The same seed and inputs must reproduce the same world and history.
`System.Random` is not guaranteed to be the same across runtimes, and `string.GetHashCode()`
is randomized per process.

**Decision.**
- Generator: xoshiro256** seeded through SplitMix64. Integer-only state; checked against an
  independent reference implementation.
- No stored RNG state. Streams are derived on demand:
  - generation: `Combine(Mix(seed), stageName)`
  - simulation: `Combine(Combine(Mix(seed), dayNumber), systemId)`
  - event IDs: a separate per-day stream.
  String components are hashed with FNV-1a 64.
- Entity IDs are GUID-shaped values drawn from these streams.
- Simulation state is integers only. Floats are confined to generation and to `Chance`
  comparisons.

**Consequences.** Saves need no RNG state. Adding a simulation system doesn't change other
systems' outcomes. Changing the RNG or the stream naming changes every world, which is why a
golden-value test guards it. Float reproducibility of generation across runtimes is likely
but not proven.

**Alternatives.** One global RNG serialized into the save: fragile, because the result
depends on the order of every draw across all systems. PCG32: equally good; xoshiro was
picked for its 64-bit output and simple state.
