# ADR-0073: Companions are the world's own people; the party is a record of who travels together

**Context.** Phase 15 adds companions and a party. The world already has persistent named people (`NpcData`), with homes, schedules, factions, memories, regard for the player and, from Phase 10, full character sheets. Places already move escorts along real passages. A separate companion entity would duplicate identity, relationships and progression, and two records of the same person would drift apart.

**Decision.**
1. **A companion is an existing `NpcData`.** Recruiting adds no new person. `PartyData` lists members (`PartyMemberData`: role, follow or wait, formation order, follow distance, joined, temporary end, where they wait). `CompanionRecordData` keeps a person's history as a companion (standing, loyalty, approvals, times recruited, why they left, the personal quest). It persists after they leave, so asking again remembers the past.
2. **Recruitment is a rule with stated reasons** (`RecruitRules.Problem`). A person refuses because:
   - they are dead, a child, being escorted, posted elsewhere, or not where the player is;
   - they left over a grievance that is not yet mended;
   - they barely know, dislike or distrust the player;
   - they know of the player's unsettled crime, or their faction counts the player an enemy;
   - they hold a post (elder, merchant, innkeeper, smith, guard), lack the nerve, or wait on news of their own request (these three refuse only a permanent arrangement: a few days is possible);
   - the party is full.

   Each refusal is said in words, in conversation, and changes nothing.
3. **Their sheet is made once by the rules engine.** A recruit without a sheet is promoted through `NpcCharacterGenesis.Make` with their own archetype. That is the same path as `PromoteNpcCommand`, so it is deterministic.
4. **Their gear becomes their own item instances once.** Gear named by string becomes real `ItemStackData` instances, equipped where worn. The strings are cleared, so nothing exists twice. `ItemsGranted` stops a second recruitment from granting again. From then on a companion owns items exactly as the player does. One holder per stack is validated across the player, every companion and every container.
5. **The party takes them out of their schedule, not out of the world.** `PlacePresence.IsAway` is true for active members, so the daily schedule does not move them and the resident layer does not draw them. Reserve members live their ordinary lives. Nightly self-rest skips active members: they rest when the party rests.
6. **Capacity and thresholds are data** (`PartyRules`): 3 active, 3 in reserve; leave below loyalty 15, return at 35; announce changes of 5 or more; temporary terms of 3 days.
7. **Commands only.** Recruit, dismiss, follow or wait, regroup, role, order, leader, item transfer, equip and unequip are validated commands. A refused command changes nothing. Dialogue reaches recruitment through two consequences (`Recruit`, `Dismiss`) and one condition (`InParty`), which call the same routine as the command.

**Consequences.**
- Save schema v17: `party`, `companions`, and a person's `ownsItems`, `inventory` and `equipment`. A v16 save gains an empty party and the party system (`MigrationV16ToV17`).
- Escorts stay a separate concept. A person cannot be both escorted and in the party (validated).
- In settlements a companion's resident body gives way to one follower body, so nobody is drawn twice. In places they are drawn as followers. In fights the fight draws them.

**Alternatives considered.**
- **A `CompanionData` entity copied from the NPC.** Rejected: two identities for one person.
- **Treating companions as escorts.** Rejected: escorts are captives being taken home, with their own states and quest objectives.
- **Gear by name for companions.** Rejected: items given to a companion must be the same instances the player gave, with their state and provenance.
