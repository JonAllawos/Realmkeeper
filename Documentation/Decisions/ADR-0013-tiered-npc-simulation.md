# ADR-0013: Tiered NPC simulation inside the day, named people counted in the population

**Status.** Accepted, 2026-11-10 (Phase 4).

**Context.** ADR-0009 point 5 asks for level of detail as a simulation concept: individuals where
they matter, aggregates elsewhere, with identity and counts conserved. Phase 4 adds full inner
lives to named people. The cost must stay bounded as worlds grow (measured at about 300 people),
the result must be deterministic and transactional (ADR-0004, ADR-0008), and no person may be
counted twice.

**Decision.**

1. **Extend, don't duplicate.** All NPC state lives on `NpcData`, plus two world lists:
   `Locations` and `NpcRelations`. IDs never change.
2. **Three tiers.**
   - **Tier 1:** notable people (leaders, elders, innkeepers), updated daily.
   - **Tier 2:** other named residents, updated weekly on a weekday fixed by a hash of their ID,
     which spreads the load.
   - **Tier 3:** the rest of the population, not entities, computed as population minus living
     named residents.
3. **Named people are members of the population.** The validator enforces living named ≤
   population. When the economy takes the population below that, the least notable die, which
   is reported and remembered. A DM relocation moves one person of the population with them.
4. **One system in the day.** `NpcSystem` runs at order 425, after population and before politics,
   inside the copy → validate → commit transaction.
5. **Hashed choices, no stream.**
   - Every decision hashes (seed, date, person, purpose).
   - Engine-made IDs come from the same hashes.
   - Commands use `CommandContext.NextGuid`.
6. **Bounded growth.**
   - Ties: at most 16 per person, and only with a cause.
   - Memories: at most 24, fading weekly.
   - Knowledge: at most 40, going stale or expiring.
   - Goals: at most 4 ambitions.
   - Conversations: a few per location, and one per six residents per settlement per day.

**Consequences.**
- **Cost:** about 10 ms per simulated day at 300 people in 10 settlements (.NET 8), against
  1.7 ms without the engine. Save and load stay under half a second.
- **Saves** grow about 20 KB per person.
- **Tier 2 people:** their condition, goals and observations are up to six days out of date
  between updates, but they move to their routine location every day, so exploration is always right.
- **Changes in tier:** promotion happens on succession.

**Alternatives considered.**
- Everyone daily: 7× the tier-2 cost for little visible difference.
- Separate agent entities for the population: double counting, and a second store.
- A shared RNG stream: order-dependent and fragile under refactors.
