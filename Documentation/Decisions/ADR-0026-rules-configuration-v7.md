# ADR-0026: Rules configuration v7 — SRD 5.2.1 progression on the Phase 6 base

**Status.** Accepted, 2026-11-11 (Phase 7). Extends ADR-0022.

**Context.**
- Phase 6 uses SRD 5.2.1 level-1 class mechanics with species-based ability increases (the SRD 5.1
  style).
- SRD 5.2.1 (CC BY 4.0) contains, verified for this phase:
  - full class tables for levels 1–20;
  - one subclass per class;
  - the feats: Origin, General, Fighting Style and Epic Boon;
  - eldritch invocations and Metamagic;
  - the experience table.
- SRD 5.2.1 also moves ability increases to backgrounds and grants an origin feat at creation.
  Adopting that would change every existing character.

**Decision.** The rules configuration is explicit:

| Area | Choice |
|---|---|
| Experience | SRD 5.2.1 thresholds, as configuration (the Bible keeps the final curve open) |
| Class features, resources, cantrips, prepared spells, slots | SRD 5.2.1 class tables |
| Subclasses | The one SRD 5.2.1 subclass per class; others are deferred, none invented |
| Ability increases | Species at creation (kept from Phase 6); level feats (Ability Score Improvement, capped at 20); Epic Boon (capped at 30); capstones (capped at 25). **No** background increases and **no** origin feat at creation. |
| Feats at feat levels | General and Origin feats, and Fighting Style feats for classes with that feature; Epic Boons at 19 |
| Hit points after level 1 | Fixed average, `die / 2 + 1` (no rolling, as in Phase 6's creation) |
| Current values on level-up | Current hit points and resource uses rise by the increase in the maximum; new resources arrive full; nothing is healed or refilled |
| Phase 6 level-1 gaps | Primal Order (druid) and the level-1 invocation (warlock) are added as rules-v7 choices: outstanding for older characters until their next advancement |

**Deferred, with reasons:**
- invocations that need damage cantrips, and Lessons of the First Ones (spells and nested feats);
- Skilled's tool proficiencies;
- multiclassing;
- invocation and spell swapping on level-up.

**Consequences.**
- Every number is traceable to SRD 5.2.1 or to a documented Realmkeeper rule.
- The mix of species increases and 5.2.1 progression is a deliberate, documented hybrid. Changing it
  later is a configuration change with a migration.

**Alternatives considered.**
- Fully adopting SRD 5.2.1 origins: rejected for now, because it changes existing characters.
- Rolled hit points: rejected, because they are non-deterministic.
