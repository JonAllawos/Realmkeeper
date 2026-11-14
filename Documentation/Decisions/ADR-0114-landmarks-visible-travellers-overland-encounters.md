# ADR-0114: Landmarks, visible travellers, and overland encounters

**Status.** Accepted, 2026-11-13 (Phase 24). It adds save schema v26 (`WorldData.Overland`).

**Context.** The brief asks for:
- an inhabited country with landmarks;
- the simulation's own travellers seen on the roads;
- encounters that make travel consequential without a combat corridor.

All of it must be deterministic, persistent and built on the existing systems, with nothing spawned to fill the view.

**Decision.**
1. **Landmarks are generated, and only their changes are saved.**
   - `LandmarkGenesis.Generate` builds a region's landmarks from its seed and geography (engine-free; cached by the session), with ids `landmark:{region}:{kind}:{n}`.

   | Landmark | Where | What it offers |
   |---|---|---|
   | Waystones (at most 6) | Road junctions and long roads | Inscriptions naming each road's settlements, with directions and hours on foot |
   | Cairns (at most 4) | Hilltops | A look-out that finds what lies within view |
   | Travellers' camps (at most 3) | Midway along long roads | A safe night's rest |
   | Abandoned camps and ruined steadings (2 each) | Off the road | A search, once |
   | Watch posts | Where a faction's settlement sends a road into another region | Lower encounter odds nearby |

   - **Wrecks** are not generated: one lies where a real `CaravanLost` event is on one of the region's roads. Searching it confirms the event and records a discovery.
   - **Saved:** `LandmarkStateData` (found, searched, rested). A search makes a one-time cache container from the existing `old_cache` loot table.
   - **The ancient realm gets none,** so no lore is invented.
2. **Travellers are the simulation's groups.**
   - `Travellers.InRegion` places each active `TravelingGroupData` at its position on its route now (departure, due time, the clock), mapped onto the region's road.
   - **Nobody is created.** The leader is the group's real person; companions are drawn with stable looks; a caravan has its wagon.
   - **Bodies exist only within 600 m,** pooled by the view and dropped beyond 800 m.
   - **Talking to a group:**
     - the player learns, as rumours (`WorldEvents.PlayerLearns`), the unresolved events that touch its origin, its destination or its roads;
     - a patrol answers according to the player's standing with its faction, and their notoriety.
   - **Refugees** can be given a ration: their home settlement's reputation and their leader's gratitude rise, through `Reputation.Change`.
3. **Overland encounters have a real source, a clock, and a record.**
   - **When:** the walk command checks once per half-hour bucket of the clock (`encounter:{region}:{bucket}`), never twice for a bucket, with three quiet hours after any encounter.
   - **Sources:**
     - a creature population whose range covers the place (challenge above 0);
     - a bandit band stationed on a road within 160 m.
   - **Odds:**
     - Raised by: night, dense wood, marsh and rocks.
     - Lowered by: roads, nearby towns, watch posts, patrols and poor visibility.
   - **Rolls:** derived from the world seed and the bucket, so the same world and walk meet the same thing.
   - **Choices:**
     - fight, through the one combat engine (`WorldEncounters.StartPlan`, drawn from the real population, near the nearest town), on the ground where the player stands;
     - slip away, with Stealth against 12 (10 at night) or 10 plus the band's strength, plus 2 for cover;
     - back off;
     - pay a band its existing toll.
   - **Walking away** more than 200 m counts as backing off.
   - **A long rest in the open at night** can be broken by such a population, deterministically by night and place. A travellers' camp is safe.
   - **The records** (the 40 most recent) are saved, so loading never rerolls one.

**Consequences.**
- **Save schema v26:** `overland` (landmark states, encounters, the clock) is optional; absent means none found, none met. The migration is structural.
- **The world validator** checks unique ids, known kinds and states, at most one pending encounter, and the bound.
- **Not in this phase:** caravans asking for escort, stranded travellers, rescue quests, roadside trade, mounts.

**Alternatives considered.**
- **Spawning travellers and creatures around the player for atmosphere.** Rejected by the brief: it duplicates people and breaks the simulation.
- **Saving every landmark.** Rejected: geography is regenerated (ADR-0110); only changes are saved.
