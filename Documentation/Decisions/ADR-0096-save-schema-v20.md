# ADR-0096: Save schema v20

**Context.** Phase 18 adds optional discovery fields, and the Phase 18 places, quests and stories.

**Decision.**
- `WorldData.CurrentSchemaVersion` is 20.
- `MigrationV19ToV20` is structural and writes nothing. A discovery's new fields (category, certainty, place, subject, major, confirmed-by, shared-in) are optional and default to "none, confirmed, not major, told nowhere", which is exactly as true as a v19 save recorded it.
- The Phase 18 places and their quest chains are added by the session on load, once per region, as each earlier generation of places was (ADR-0071).
- Mechanism state lives in each place's existing `Variables`. Recovered-goods and strain bookkeeping lives in each world event's existing `Applied` list. Conflict stages are each event's existing severity. So no other structure changes.
- Tests: the strip chain gains `StripToV19`. A v19 save loads, gains its places and chains once, keeps every older place, and loading again adds nothing.
