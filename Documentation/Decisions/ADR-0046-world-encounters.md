# ADR-0046: Encounters come from the world, are checked before they start, and settle exactly once

**Status.** Accepted, 2026-11-11 (Phase 10).

**Context.**
- Practice fights were labelled stand-ins.
- The brief asks for encounters made from world context: who lives where, threat, objective. Their consequences must persist without double counting.

**Decision.**
1. **Plan from the world.** `EncounterGenerator.Plan(request)` is deterministic for a world state. It takes:
   - the purpose (outskirts, road, farms);
   - the settlement, the date and time;
   - the populations in range (distance from the range edge);
   - the party's XP budget.
   It weighs creatures by population count and habitat. It picks an encounter type (wildlife, predators, bandit ambush, a patrol with the road watch, hostile creatures, defending a farm, vermin) and an objective (defeat or drive off, protect a named person, escape, hunt), a composition, positions and terrain.
2. **Budget and check.** Groups stay within the moderate XP budget (SRD 5.2.1: 75 per level), and a band's leader joins only while the total stays within the high budget (100 per level). A truly dangerous creature (challenge above level + 1) is rare near people, but possible. When the total exceeds twice the budget, the objective becomes escape, and the plan says so ("deadly"). Before start, `Plan.Check()` refuses a plan that:
   - names an unknown creature;
   - overdraws a population (every draw is counted);
   - places two participants on one square, or anyone off the field;
   - has nobody hostile.
3. **Individuals.** Starting draws named instances from their populations (ADR-0044). The plan's origin (`EncounterOriginData`) is stored on the encounter.
4. **Settle once.** When the encounter ends, `WorldEncounters.Resolve` applies, keyed by the encounter:
   - instance fates and population deaths;
   - whether the objective was met;
   - XP keyed `encounter:{id}` for the player and NPCs with sheets;
   - loot and a farmer's memory;
   - an `EncounterResolved` chronicle event.
   A second call changes nothing.
5. **Entry points.** `StartWorldEncounterCommand(purpose)`, the HUD's "Go out" button (outskirts → road → farms) and `WorldSession.PreviewWorldEncounter`.

**Consequences.**
- The same world state gives the same encounter. A different place or time gives a different one.
- Consequences survive save and load. Repeated resolution, replays and reloads cannot duplicate XP or loot.

**Alternatives considered.**
- *Random tables:* no tie to the world.
- *Consequences at each death:* partial results on withdrawal and double counting.
