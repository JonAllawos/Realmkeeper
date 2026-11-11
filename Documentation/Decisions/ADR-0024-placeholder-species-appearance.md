# ADR-0024: Species appearance is a contract; non-human species use labelled placeholders

**Status.** Accepted, 2026-11-10 (Phase 6).

**Context.**
- The project has finished, licensed (CC0) human bodies only.
- Eleven species need distinct models: ears, tusks, horns, tails, scales and proportions.
- No licensed asset for them was available, and none may be bought without approval.

**Decision.**

1. `SpeciesAppearance` declares `HasFinishedModel`, `BodySet`, `HeadSet`, `HeightScale`,
   `AllowsBeard`, `Features` and a `PlaceholderNote`.
2. Non-human species render the human body scaled to their height. The creator, the review and the
   sheet say so plainly.
3. The validator rejects looks a species cannot have, such as a beard on a dragonborn.
4. The `Features` list is the art brief for the visual production phase.

**Consequences.**
- Every species is playable now, and the visuals are honest about what they are.
- Finished models can replace the placeholders one species at a time without changing saves or
  rules, as long as they use the existing humanoid skeleton.

**Alternatives considered.**
- Recolouring humans and calling them species: rejected as misleading.
- Hiding species that have no art: rejected, because the Bible asks for all twelve.
