# ADR-0047: Settlements have a persisted identity and a terrain-first plan

**Status.** Accepted, 2026-11-11 (Phase 10).

**Context.**
- The walkable settlement was one plaza-centred cross of two streets, scaled by type. It was regenerated on every visit and knew nothing of the ground, water, economy or the strategic map.

**Decision.**
1. **Identity first.** `SettlementLayoutGenerator.Identify` reads the settlement's economy, people, treasury, ground and water. It records:
   - an archetype (market town, village green, river crossing, hillside, fortified, roadside, farmstead);
   - primary and secondary economy, water access and terrain;
   - wealth, security and agriculture (0–100);
   - architecture, growth, and a one-line summary.
   The plan is built from this identity, never at random.
2. **Terrain first.** The plan is generated in this order:
   1. ground: regional fall, a hill for the landmark with a flattened top, rolling noise, a levelled square;
   2. water: a river keeps the region's course, offset clear of the square and, in a town, outside the ring street;
   3. main roads to the real neighbouring settlements;
   4. districts by function;
   5. the hall (tavern), the landmark and the mill, before any street, so none runs through them;
   6. streets and district lanes that follow the land and go round what stands there;
   7. walls and gates for a town;
   8. the world's sites (granary, homes, shops, smithy, shrine) in their districts;
   9. infill to each district's density;
   10. farmsteads with tracks, fields, pastures and orchards;
   11. gardens, landmarks, trees, lamps and the arrival point.
3. **Sound by construction, then checked.** `SettlementLayoutValidator.Check` requires:
   - every required site present and a dry square;
   - one connected road network (crossing roads meet), with water crossed only by bridges and bridges landing on dry ground;
   - no building in water, on a road, overlapping another or on ground steeper than its plinth allows;
   - fields fit for their use, and references that resolve.
4. **Site keys kept.** The world's places keep their layout keys (`hall`, `granary`, `landmark`, `house-n`), so schedules, shops and residents find them unchanged.
5. **One plan, three readers.** The walkable environment (`LocalEnvironmentGenerator.FromPlan`), the strategic map's settlement mesh and the inspector all read the plan. None invents its own.

**Consequences.**
- 40 seeds × 3 settlements validate with no errors. Average size: town 70 buildings, village 53, hamlet 23.
- Generating a plan takes under 90 ms. It happens once per settlement.
- Older worlds without a plan keep the older generated environment.

**Alternatives considered.**
- *Hand-authored towns:* they do not scale to generated worlds.
- *Generate on every visit:* plans would drift as the world changes (ADR-0048).
- *Unity terrain and NavMesh as the source:* the plan must be engine-free and testable.
