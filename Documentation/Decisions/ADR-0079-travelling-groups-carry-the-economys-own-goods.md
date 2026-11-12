# ADR-0079: Travelling groups are the world's own people and carry the economy's own goods

**Context.** The brief asks for caravans, merchants, patrols, pilgrims, refugees and bandits on the roads, moving real goods through the existing economy. The economy already moves goods between settlements as shipments (`TradeSystem`, conserved). Road bandits already exist as creature populations (Phase 10 road bands). A second trade model or a separate bandit model would break conservation and duplicate state.

**Decision.**
1. **One record for every group on the road** (`TravelingGroupData`, saved). It holds:
   - kind and name; faction and leader (a real person);
   - members and strength;
   - origin, destination, nodes and segments; departure and due times;
   - state: Travelling, Halted, Stationed, Arrived, Defeated or Dispersed;
   - links: the shipment it carries, the population it mirrors, the road it holds, the event it answers;
   - a short history (at most 12 lines).
2. **Caravans are shipments.**
   - `GroupSystem` (daily, order 160) gives each shipment in transit a caravan along the planned route.
   - Its position comes from its departure, its due time and the route's length.
   - Guards come from the cargo's size.
   - The leader is a person of the origin. Merchants come before labourers. A leader is never a shopkeeper, someone posted, a party member or an escort, and each settlement sends at most one leader away at a time.
   - A leader away is not at home (`PlacePresence.IsAway`), so towns do not draw them twice.
3. **Losses are accounted, never silent.** Each day a caravan on a road held by a band may be attacked:

   | Term | Value |
   |---|---|
   | Chance | `10 + 10 × band − 6 × caravan strength`, capped at 60% |
   | Seed | the world seed, the caravan and the day |

   - **Repelled:** the history says so.
   - **Lost:** the shipment's goods are recorded as `LostOnRoad`, through the flow accounting that Phase 14 added. A `CaravanLost` event and a chronicle entry follow.
   - **Closed road:** the caravan halts and raises a `TradeDisruption` event.
4. **Bandits are the road bands.** Each road-band population has a bandit group stationed on its road, with strength equal to the population count. Defeating them, in a road fight or by a patrol, lowers that same count.
5. **Patrols, faction patrols and refugees** are groups with their own causes:
   - patrols are paid from a faction's treasury (ADR-0080);
   - refugees leave a settlement in long unrest or shortage.

   Each answers an event and ends when it is resolved.

**Consequences.**
- Goods are conserved. Every unit that leaves a settlement arrives, is delayed, or is recorded as lost on the road (tested).
- What the player sees on the atlas is what the economy did: the cargo, the guards, the attack and the loss.
- The six-region world keeps about 24 active groups, 22 of them caravans. A simulated day costs about 17 ms (measured).

**Alternatives considered.**
- **Caravans as decoration.** Rejected: the brief requires that they carry real goods.
- **A separate convoy economy.** Rejected: it would be a second trade model.
