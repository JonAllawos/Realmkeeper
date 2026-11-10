# ADR-0019: Minutes on top of daily simulation; hourly behaviour derived, not stored

**Status.** Accepted, 2026-11-10 (Phase 5).

**Context.**
- The simulation advances whole days in a validated transaction (ADR-0008), and every system is
  tested at that cadence.
- The living world needs a time of day: light, routines, shop hours, hunger.
- Simulating every minute would multiply cost and invalidate the tested daily behaviour.
- Storing hourly NPC positions would bloat saves and could disagree with the routine.

**Decision.**

1. **The clock is a minute of the day on top of the date** (`WorldData.MinuteOfDay`, saved in v7).
2. **Daily systems still run once per day**, at each midnight the clock crosses
   (`AdvanceTimeCommand`). Advancing minutes inside a day changes only:
   - the minute itself (in place: it cannot invalidate the world);
   - the player's hunger, counted on absolute hour marks so it is step-size independent.
3. **Hourly behaviour is a pure function**, `NpcActivityPlan.At(world, npc, date, minute)`. It
   derives where someone is and what they do from:
   - the day's committed location (routine, need, or the DM's word);
   - role, working hours (a keeper's are the shop's), personality and pressing needs;
   - a per-person wake and sleep time hashed from their ID.

   It stores nothing and is deterministic. The same function serves shops (is the keeper at the
   counter?), dialogue ("I'm at it now"), queries and exploration.
4. **Two paces.**
   - The realm map advances whole days, as before.
   - Exploration advances minutes at a pace where walking matches the schedule: Normal is 2 game
     minutes per real second.
   - The HUD offers pause, speed and "wait an hour".
5. **Lighting is a pure function of the minute**: sun path, golden hour, moon, sky, ambient, haze,
   lamps and windows.

**Consequences.**
- All earlier daily tests are unchanged. New tests check:
  - many small steps equal one large one exactly;
  - midnight runs a day;
  - saves keep the minute.
- Presence can change within a day without any state change, so presentation must poll it on each
  clock change. That is cheap: 0.5 ms for 300 people.
- The day's simulation does not know the time of day: needs are evaluated daily, not hourly. This
  is acceptable now; finer needs would be a later change.

**Alternatives considered.**
- *Simulate every hour.* Rejected: 24× cost and re-validation of every system.
- *Store hourly schedules per NPC.* Rejected: larger saves, and a second truth that could disagree
  with the routine.
