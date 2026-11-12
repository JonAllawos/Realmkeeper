# ADR-0093: Every kind of world event has follow-up gameplay, and the story lifecycle is audited

**Context.** The world raised ten kinds of event, but only two became stories.
- **Lifecycle bug:** a caravan's loss is raised and resolved in the same moment, while the synthesis only read open events. So the Phase 17 lost-caravan story could never appear in play; its test raised an open event by hand.
- **Lost goods:** goods lost on the road never came back.

**Decision.**
1. **Lifecycle fix.** A caravan loss resolved within 14 days is still a cause.
2. **More story types**, each bound to the real people involved:

   | Story | Cause | Who asks |
   |---|---|---|
   | Room at the Hearth | refugees arriving | the town they reach |
   | Hard Words in {town} | local unrest | the unhappiest worker speaks for the lane |
   | The Long Way Round | trade cut off on a road | a merchant at each end |
   | Word for a Friend | a recent death | a neighbour of someone with a real friend elsewhere |
   | Bad Blood at the Crossing | a faction conflict | ADR-0090 |

   The unrest and refugee stories end the event they are about (`ConsequenceKind.ResolveWorldEvent`).
3. **Lifecycle rules.**
   - One story per cause, ever (stable key).
   - At most one new story a day, and at most 5 open (was 3).
   - Causes are taken by priority (caravan, conflict, refugees, trade, unrest), then by event number.
   - Offers lapse after 10 days (`ExpiresAfterDays`).
   - `StorySynthesis.For(event)` makes the story for one event (used by tests).
4. **Goods come back.** `GroupSystem.Lose` records what was taken, by which band, and where it was bound. When that band is broken — by a patrol, the player, or its losses — half the goods are carried to the town they were bound for: real stock, once, never above its storage, noted in the event's history.

**Consequences.**
- A lifecycle audit test asserts every `WorldEventType` has follow-up gameplay.
- Phase 17's lost-caravan test now waits for its turn, since real losses compete for the one-a-day slot.
