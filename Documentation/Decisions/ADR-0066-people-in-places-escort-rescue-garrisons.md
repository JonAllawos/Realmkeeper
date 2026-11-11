# ADR-0066: People in places are the same persistent identities — held, following, posted or at home

**Status.** Accepted, 2026-11-11 (Phase 13).

**Context.**
- Escort, rescue and protect were partial in Phase 12: the objectives matched, but nothing emitted their events.
- Places need captives, garrisons and household occupants without duplicating anyone.

**Decision.**
1. **Escorts.** `EscortData` (in `WorldData.Escorts`) records a person the player is responsible for:
   - Captive (in a place and room);
   - Following;
   - Waiting;
   - Arrived;
   - Lost.

   While held, following or waiting, `PlacePresence.IsAway` makes the person absent from their schedule and their home settlement (`NpcActivityPlan.At` reports them unknown), so they are never in two places.
2. **Rescue.** The Free action (an Athletics check) turns a captive into a follower and emits `Rescued`.
3. **Escort.** A follower comes through every transition: into and out of places, onto the map, into a settlement. Entering the follower's destination settlement (or leaving an interior there) brings them home, emits `Escorted`, and returns them to their own life. At most three people follow at once.
4. **Protect.** A follower, or a captive in the room, fights on the player's side. Surviving a won fight emits `Protected`. Dying makes the escort Lost, and quests with an NpcDied failure fail.
5. **Garrisons.** A place's guards are persistent people: new `NpcData` of the holding faction, made once with the place. While their group stands, `PlacePresence.PostedAt` keeps them at their post and out of their home settlement.
   - Guard groups hold doors (`GuardedBy`).
   - Leave to pass comes from standing (Respected and up), Persuasion or a bribe (the `dlg_gate` dialogue, through the Phase 12 engine), or the guards' defeat.
   - Being seen in a restricted room, or inside without leave, is trespass: −10 standing with the faction (once per room and visit), a Chronicle entry and a fight. Attacking the garrison costs −15 standing and +5 notoriety.
6. **Household occupants.** An interior shows the people whose current whereabouts is its building, placed by activity: asleep in a bedroom, at work in the workroom, otherwise the main room. They can be talked to when awake. Their chests are not the player's: theft is not part of Phase 13.

**Consequences.**
- The vertical slice "Lost Below" is complete through these rules:
  - a real resident is held in the mine from the day the world began;
  - a neighbour asks for help;
  - rescue, protect (optional) and escort home complete the quest.
- Unrestricted companions and parties are not part of this. Followers are bounded escorts.

**Alternatives considered.**
- *Spawning a temporary "captive" actor:* a duplicate of a real person.
- *Garrisons as nameless creatures:* the brief asks for intelligent inhabitants to be persistent people.
