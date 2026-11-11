# ADR-0035: Settlement structures are data built on existing places, each honestly classified

**Status.** Accepted, 2026-11-11 (Phase 8).

**Context.**
- Phase 8's secondary workstream asks for building definitions and instances, a modest catalog,
  distribution by settlement size, and preservation of the existing shops.
- It must not invent systems the simulation does not have.

**Decision.**
1. **Definitions are code data and instances are world data.** `StructureCatalog` holds 19 kinds;
   `WorldData.Structures` holds the instances, each owned by its settlement.
2. **Every existing location becomes one functional structure.**
   - It is linked to its `LocationId`.
   - Where there is one, it is also linked to its shop, keeper and governing faction.
   - Nothing is duplicated, and existing shops keep their names.
3. **Informational and decorative structures are added once each, by settlement size.**
   - *Hamlets:* a barn and a well.
   - *Villages:* add a bakery, a stable and market stalls.
   - *Towns and up:* add a butcher, carpenter, mason, warehouse and stable, and have no barn.
4. **Every definition says what it takes part in.** For an informational structure the text is
   "nothing yet". The inspector shows this text.
5. **Generation is deterministic and runs once:** for new worlds, and when loading saves older than
   v10. IDs are hashed from the world seed and the source.

**Consequences.**
- Structures appear in the settlement inspector and as signs in the street.
- Production, construction and upkeep are future work.
- Informational structures have no site of their own in the local layout, so no building is
  invented for them.

**Alternatives considered.**
- Replacing locations with structures: rejected, because it is a breaking change to NPC routines
  and shops.
