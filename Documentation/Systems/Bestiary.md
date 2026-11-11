# Bestiary and wildlife

Creature documentation, kept in one place since Phase 10:

- **The catalog:** creatures, categories, behaviours, traits, sources and licence →
  `Documentation/Phase10/CreatureAndNpcSystems.md` §2.
- **Traits and behaviour in combat** → the same document, and `Systems/Combat.md` ("Phase 10 additions").
- **Wildlife populations, herds and the weekly fauna system** → `Documentation/Phase10/CreatureAndNpcSystems.md`
  §3, and `Systems/Economy.md` (daily order).
- **Encounters drawn from populations** → ADR-0046 and `Documentation/Phase10/CreatureAndNpcSystems.md` §5.
- **Decisions** → ADR-0042 (casters), ADR-0044 (bestiary and wildlife).
- **Credits:** SRD 5.1 creatures, and five labelled Realmkeeper originals → `CREDITS.md`.

`CreatureCatalog.Validate()` runs in the tests. Every creature carries its `ContentSource`, and the combat
inspector shows it.
