# ADR-0041: Class features act through one activation model matched by feature ID

**Status.** Accepted, 2026-11-11 (Phase 9).

**Context.**
- Phase 7 records class features as typed rules with an integration need (ADR-0027). Few of them
  acted in Phase 8's combat.
- Phase 9 must make the combat-relevant ones usable for every class, without bespoke UI per class.

**Decision.**
1. **One catalog.** `CombatFeatureCatalog` describes each combat feature: activation (action, bonus
   action, free, after a hit, automatic), targeting, resource and cost, range, options or amount,
   and status with a note.
2. **Matched by feature ID, not class.** The engine asks whether the combatant's rules have the
   feature. A companion, or a resident with a sheet, gets the feature without code changes.
3. **One action.** Active features are one action, `UseFeatureAction`, with `FeatureProblem`
   reasons. Automatic features are applied inside the shared attack, damage and save code.
4. **The panel lists features generically:** cost, targets and options from the catalog, and an
   unavailable feature with its reason.
5. **Honest status.** Features that are not simulated stay "sheet only" and are listed in
   `Systems/ClassFeatures.md`.

**Consequences.**
- Every class has its combat features in one list, with one test style.
- Reaction features wait for a reaction prompt, since reactions are automatic (ADR-0033).

**Alternatives considered.**
- *Per-class controllers:* faster for one class, but twelve times the UI and tests.
