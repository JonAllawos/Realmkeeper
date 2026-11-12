# ADR-0080: World events are typed records with a lifecycle; consequences apply once; the player knows them through people

**Context.** The world needs events that start from causes, change over time, act on the existing systems and reach the player as news. The game already has a chronicle of facts, NPC knowledge with sharing (`NpcMind.Learn`), and quests built from bound templates. A scripted event deck is excluded, and so are new world canon and omniscient UI.

**Decision.**
1. **The record** (`WorldEventData`, saved) holds:
   - its type: `FoodShortage`, `TradeDisruption`, `CaravanLost`, `BanditActivity`, `PatrolResponse`, `BridgeDamaged`, `RoadBlocked`, `FactionConflict`, `RefugeeMovement` or `LocalUnrest`;
   - its state: Active, Escalating, Resolved, Failed or Expired;
   - severity 1–5, its cause in words, and its scope (settlement, road, region, factions, group);
   - its times (created, updated, expires, closed), and a stable key (one open event per cause);
   - the consequences applied, by key, and its history;
   - the player's knowledge (below).
2. **One daily system** (`WorldEventSystem`, order 477) reads the systems' own state:
   - food reserves;
   - stability;
   - road-band strength;
   - bridge states;
   - faction relations;
   - blocked routes.

   It opens, escalates, resolves or expires events, and applies their consequences once each (`Once(key)`):
   - **Bridges:** crack, fall, and are repaired after their repair days. A fallen bridge closes the route.
   - **Conflicts:** blockades close the border crossings between factions at odds; faction patrols ride against them.
   - **Shortages and unrest:** they lower stability, and in time send refugees.

   Closed events are kept, up to 60 of them.
3. **Faction activity** (`FactionActivitySystem`, order 456) answers open bandit events with patrols, paid from the faction's treasury (40 coin). A patrol's strength comes from the faction's presence. When patrols wear a band down to nothing, the event resolves.
4. **Knowledge through people.**
   - `TellSettlements` writes each event as NPC knowledge (topic `RecentEvent`) in the settlements it touches. From there the people's own sharing spreads it.
   - The player learns in three ways: seeing it (Confirmed), being told on arrival in a settlement whose people know, or asking about News or Rumours in conversation (Rumored).
   - The player's knowledge is a snapshot: `Known`, `KnownAt`, `KnownState`, `KnownSeverity`, `KnownFrom`.
   - The atlas shows only what the player knows, as of when they learned it. `KnowledgeOutdated` marks news the player can see has changed.
5. **Quests from events.** Two compact templates are created from events through the existing quest lifecycle, with source key `event:{id}`:
   - `RoadOutlaws`: break the band, then report;
   - `BridgeTimber`: deliver timber planks for the repair.

   `QuestCauses` holds them while the event is open, or once the player has done the work. They fail if the cause ends otherwise.

**Consequences.**
- Every event has a cause in the world's state, and the same state gives the same events (tested).
- Consequences are applied once, and saving and loading does not apply them again (tested).
- What the player is told comes from people who know. The atlas never shows events the player has not heard of.
- **Limitation:** events are local and regional. There is no grand-strategy war, which is out of scope.

**Alternatives considered.**
- **A random event deck.** Rejected: events must come from causes.
- **Events visible to the player as soon as they happen.** Rejected: knowledge must travel through people.
