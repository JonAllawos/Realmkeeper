# ADR-0048: Spatial plans are persisted, versioned and generated once; schema v12

**Status.** Accepted, 2026-11-11 (Phase 10).

**Context.**
- A plan made from world state changes when the world does. Regenerating it would move houses under people's feet.
- Older saves (v5–v11) must keep loading.

**Decision.**
1. **Persist.** `WorldData.SettlementLayouts` is saved in full: identity, terrain profile, waterways, districts, roads, bridges, buildings, fields, landmarks, features, wall, gates and arrival. Lines are flat coordinate lists; seeds are strings.
2. **Version.** Each plan records `GeneratorVersion` (now 2). A plan from an unknown version is refused, not regenerated.
3. **Never regenerate.** Loading reads the plan exactly. A plan edited outside the generator loads as edited, provided it validates. A corrupt plan refuses the load (`WorldValidator`). It is not repaired.
4. **Schema v12.** `MigrationV11ToV12` adds the empty Phase 10 lists (`creaturePopulations`, `creatures`, `livestock`, `settlementLayouts`) and enables the fauna system.
5. **Fill once.** `LivingWorldGenesis.Ensure` runs at generation and on every load (after the region map is regenerated). It only adds what is missing, in dependency order:
   1. the people a place calls for;
   2. NPC records;
   3. wildlife and herds;
   4. plans;
   5. herd pastures.
   An upgraded save gains its additions once; loading again changes nothing.

**Consequences.**
- A v11 save gains plans made from its own state at upgrade time. It is the same deterministic generator, though not necessarily the plan a fresh world of that seed would have, since the world has moved on.
- Plans add about 60–120 KB per settlement to a save.

**Alternatives considered.**
- *Derive the plan from the seed alone:* it would ignore the world's state and could not keep edits.
- *Regenerate when the version changes:* rejected; a new generator version will need an explicit migration.
