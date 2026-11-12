# ADR-0078: A journey is a world action on the one clock, hour by hour, and ends in the settlement

**Context.** The player must travel between settlements and regions. The game already has one world clock (`AdvanceTimeCommand`) and one encounter engine. The brief excludes a separate travel minigame and a second clock.

**Decision.**
1. **The journey** (`JourneyData`, saved) holds:
   - the origin and the destination;
   - the chosen plan's nodes and segments, and the progress along them (segment index, metres into it);
   - its status: Travelling, Halted, Interrupted, Arrived or Abandoned;
   - hours travelled and the reason for any stop;
   - any encounter in progress, and the bands already met.
2. **Commands.**
   - `StartJourneyCommand` takes a destination and an option.
   - `TravelHourCommand` moves along the segments by one hour's pace, then advances the world clock by 60 minutes.
   - `Halt`, `Resume`, `Abandon` and `ResolveRoadThreat` act on the journey.
   - `WorldSession.Travel(maxHours, throughTheNight)` repeats hours until the journey arrives, is interrupted, or night falls (unless the player presses on).
3. **Outlaws on the road.** Each hour on a segment with a road band rolls a threat, keyed by journey and hour, so it is deterministic. The chance is `4 + 4 × strength (+6 at night) − 2 × party size`, capped at 40%. The player chooses one of:
   - **Fight:** the encounter engine, with combatants drawn from the band's own population (`EncounterRequest.Population`);
   - **Slip past:** Stealth against DC 10 + strength;
   - **Pay a toll:** 5 coin × strength.

   A band already met does not bar the same journey again.
4. **Arrival.** The session raises `Arrived`, and the exploration module dispatches `EnterSettlementCommand`. The town is entered as any other time, with the party.
5. **Entering is gated.** The player cannot enter a settlement in another region, or enter anywhere while on the road, until they arrive.
6. **The party** travels with the player.
   - Party size lowers the threat.
   - Members join road fights through ADR-0075.
   - Reserves stay at home.
7. **Waiting members.** A member told to wait keeps the pose their body stood on (`PartyMemberData.WaitPose`) and is drawn there on return. This fixes the Phase 15 issue where a waiting member reappeared beside the player.

**Consequences.**
- One clock, one encounter engine, no minigame.
- Saving mid-journey restores the same position and status (tested).
- An hour of travel, including the clock, costs about 6 ms (measured).
- **Limitation:** "Fight it out" on the atlas plays the fight through the engine with the AI choosing the player's actions, because the map has no fight scene. Fights started in a town or a place are played as before.

**Alternatives considered.**
- **Instant travel with a time skip.** Rejected: nothing could interrupt the trip, and there is nothing to see on the way.
- **A free-roaming overworld.** Out of scope: the brief excludes a separate travel game.
