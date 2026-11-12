# ADR-0069: Crime is a record of what happened; who knows is a matter of who perceived it

**Status.** Accepted, 2026-11-11 (Phase 14).

**Context.**
- Phase 12 gave the world reputation dimensions with keyed, once-only changes, and person-to-person hearsay. Deeds already travel as `PlayerDeed` knowledge between people who meet.
- Phase 13 gave garrisons trespass and household rooms restrictions, but nobody reacted to them, and household chests could not be taken from.
- A crime system must not grant universal knowledge, must not double-penalise, and must stay local.

**Decision.**
1. **The record.** `CrimeRecordData` (`WorldData.Crimes`) is the world's truth:
   - kind: theft, shop theft, unauthorised entry, trespass, assault, murder, property damage;
   - status: Undiscovered, Discovered (known to have happened, culprit unknown), Witnessed, Settled;
   - time, settlement, place and room, victim (person or faction), what was taken, bounty;
   - witnesses (person, recognised or not, a description, how they saw).

   It never makes anyone know anything.
2. **Perception** (`Witnesses.Of`). A person witnesses an act only if all of these hold:
   - They are in the building or place now, by their schedule, or posted there.
   - They are awake. Sleepers wake only for loud acts: a forced door.
   - They can see the player's cell from their own cell (`GridSight` over the place's cells, closed doors as walls) within 60 ft.
   - There is light enough: the room is lit, the player carries a light, or the witness has darkvision.
   - They are alive and conscious.
   - For a quiet act ("take quietly"), the player's Sleight of Hand or Stealth does not beat their passive Perception.
   - They **recognise** the player if they have met them, or the player is well known (renown or notoriety 30+). Otherwise they keep a description: species, build, armour.
3. **Knowledge.** Each witness remembers the act:
   - a memory with source Witnessed;
   - `PlayerDeed` knowledge if they recognised the player;
   - `RecentEvent` knowledge naming "a stranger" if they did not.

   From then on the existing daily hearsay carries it, only between people who meet, so it stays local and loses confidence with each hop. A witness who dies takes what they knew with them.
4. **Consequences, once each** (every change keyed by the crime ID), only when someone recognised the player:
   - settlement reputation and the victim's regard fall; faction standing falls for a faction's property;
   - a bounty is set (theft: the goods' value, at least 10; assault 25; murder 100; entry and damage 10; trespass 5).

   Unrecognised: no reputation change, because nobody can name the player. Undiscovered thefts are found out when the owner is next at home (the daily system), as Discovered with no culprit.
5. **Responses** are contextual, not universal:
   - The witness confronts the player at once.
   - Merchants who know (first-hand, or by rumour at 50+ confidence) of an unsettled crime by the player in their settlement refuse trade.
   - In conversation, a victim or a guard who knows can be paid the bounty, or given the goods back, to settle. Settling clears the bounty and stops the refusals, but not the memories.
   - Refusing a guard is a separate notoriety and standing change.
6. **Restricted areas** (`Restrictions.ResponseFor`) are reusable.
   - A room is restricted by its kind and owner: bedrooms and private stores in homes, workrooms and stock in businesses, guard rooms, a faction's interiors, secure rooms in places.
   - Who enforces is whoever is present and sees the player. The response depends on who that is:
     - a household warns once, then shows the player out (trespass is recorded);
     - a business warns, then refuses service;
     - a garrison fights (unchanged from Phase 13, now with a crime record).
   - A locked home at night needs Pick or Force; getting in is unauthorised entry if anyone notices.

**Consequences.**
- A theft in an empty room leaves no trace on reputation until it is discovered, and even then nobody knows who did it.
- A theft in front of a stranger who has never met the player spreads as "a stranger, a dwarf in mail" rather than the player's name.
- Bounties and refusals are local to a settlement and to the people who know.

**Alternatives considered.**
- *A global "wanted" flag per settlement on any crime:* gives universal knowledge. Rejected.
- *A full legal system with jails and trials:* out of scope (the brief says so); deferred.
