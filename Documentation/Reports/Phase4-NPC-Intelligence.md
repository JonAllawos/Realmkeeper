# Realmkeeper Phase 4: NPC Intelligence Engine

Date: 2026-11-10. Branch `feature/phase-0-1-foundation`, on top of the graphics pass (`b0545b5`).
One commit, not pushed. Work stops here: Phase 5 is not started.

## Summary

The fifteen named people of the default world are now simulated individuals. Each has:
- an identity, a bounded personality, skills and needs;
- goals that come from their needs and their character;
- relationships that exist only for a reason;
- bounded, fading memories linked to the chronicle;
- knowledge kept apart from the truth, which spreads as hearsay.

They also have a home, a workplace and a weekly routine. They move between persistent locations,
and exploration shows them where the world says they are.

The player can ask them about six topics. Every answer comes from what that person knows, never
from the world's truth. The DM can edit everything through atomic, chronicled commands.

The engine runs inside the daily transaction, deterministically. It costs about 10 ms per
simulated day at 300 people in 10 settlements, and saves are schema v6 with a once-only migration
that invents no history.

| Suite | Result |
|---|---|
| .NET harness | 323 passed, 0 failed |
| Unity EditMode (batch) | 323 passed, 0 failed |
| Unity PlayMode (batch) | 50 passed, 0 failed; 2 evidence-capture fixtures skipped without `RK_CAPTURE_DIR` |

## Pre-flight

`Documentation/Phase4/PreFlight.md` records three things:
- **What existed:** `NpcData` with 7 fields, 15 residents counted in the population, faction
  leaders as NPC IDs, the daily transaction and the command pattern.
- **What would be extended:** no parallel system.
- **The risks:** double counting, cost, determinism, omniscience, migration, time of day and
  scope, each with its mitigation.

## What was built

### Data (extends `NpcData`; two world lists)

| Area | Contents |
|---|---|
| Identity | Sex, birth date (age), occupation (the existing role), tier, home, faction, deceased |
| Personality | 8 traits, 0–100 |
| Skills | 8 skills, 0–100 |
| Condition | Health, fed, energy, safety, morale; savings |
| Goals | 9 types, priority, progress, status, source; at most 4 ambitions |
| Memories | ID, event link, date, category, subject, people, salience, confidence, source (experienced, witnessed or learned); at most 24; fading |
| Knowledge | Topic, subject, value, status (known, believed or rumour), confidence, source person, tellings; at most 40 |
| Whereabouts | Residence, workplace, current location, 7-day routine |
| `WorldData.Locations` | Residence, shop, workshop, tavern, market, administration, worship, guard post, square. Each is on a layout site (`SettlementSites`), shared with the exploration layout. |
| `WorldData.NpcRelations` | One per related pair: affinity, trust, respect, fear, rivalry, kind, origin, last cause, since, conversations; at most 16 per person |

### Simulation: `realmkeeper.npcs` at order 425

Tiers:
- **Tier 1** (leaders, elders, innkeepers): updated daily.
- **Tier 2:** weekly, on a weekday fixed by the person's ID.
- **Tier 3:** the rest of the population, in aggregate.

**No double counting.** Named people are members of the population. When the population falls
below them, the least notable die: the death is chronicled and remembered, and a faction leader
who dies is succeeded.

Each day, the system:
1. Updates condition, goals (needs bypass the ambition cap; 30-day cooldown) and location, and
   lets people observe their surroundings.
2. Runs conversations at shared locations and among neighbours in the evening. Ties form with a
   cause, affinity follows personality compatibility, and one new piece of news passes on as
   hearsay.
3. Records reactions to the day's notable events: memory, knowledge and morale.

Every week, memories fade, first-hand knowledge goes stale, rumours expire and ties drift.

Every choice hashes (seed, date, person, purpose). Tests verify that the same seed gives the same
month.

### Commands, dialogue and queries

- **`AskNpcCommand`** (player): asks about themselves, this place, who governs, the faction's
  leader, news, rumours, or a named person. It is allowed only where the person is today.
  - **Source of answers:** the person's own knowledge, through `NpcDialogue`; hedges reflect
    belief and rumour.
  - **Effects:** the conversation is remembered, and the first meeting is chronicled.
- **DM commands:**
  - edit identity and occupation;
  - set the schedule;
  - set or abandon a goal;
  - set a relationship;
  - add a memory or knowledge;
  - move a person (a move to another settlement conserves people);
  - change affiliation (a leader must be replaced first).

  All are atomic and chronicled with source `dm`. A rejected command changes nothing, which is
  tested for eleven invalid inputs.
- **Queries:**
  - `GetNpcs(filter)`;
  - `GetNpcDetail` (ten sections, each line with an explanation);
  - `GetPeoplePresent`, `GetLocations`.

  The inspector marks knowledge that no longer matches the world.

### UI

- **People** (left navigation): a list filtered by place, role, tier and name. A star marks
  tier 1.
- **NPC inspector:** identity, personality, skills, condition, goals, whereabouts (with the
  7-day routine), relationships, memories, knowledge and history. Each section opens with a
  sentence explaining where its values come from, and each line can carry its own reason.
  **Manage** opens the DM's tools: rename, set work, move, set routine, set goal, affiliate,
  set affinity, add memory, tell (food).
- **Settlement inspector → Residents:** "6 named of 1,650 people; the other 1,644 are simulated
  together", with links to each person.
- **Exploration:**
  - **Placement:** people stand at the site of their current location: inside the tavern,
    among the stalls, by the gate, or at a building's door.
  - **Conversation:** E greets and offers six topics (click, or keys 1–6). The resident plays
    the talking animation, and walking away ends the conversation.
  - **Appearance:** the body frame follows the recorded sex.

### Persistence: schema v6

- **`MigrationV5ToV6`** adds the empty lists and enables the NPC system.
- **`NpcGenesis.EnsurePeople`** runs once for people not yet initialised. It derives everything
  from the seed and IDs, adds present-day knowledge of the person's own settlement, and adds
  ties only for a shared home, a shared workplace, or a faction leader.
- **No invented history:** no memories, no events, no conversations.
- **Tests confirm:**
  - a v5 copy of a save loads identical to a fresh v6 world of that seed;
  - a v6 save is never re-initialised;
  - a schedule of the wrong length is rejected.

### Documentation

`Systems/NPCs.md`, `Systems/Relationships.md`, `Systems/Persistence.md` (v6),
`Systems/Exploration.md` (people in exploration), `Systems/Economy.md` (daily order),
ADR-0013 (tiers and population), ADR-0014 (knowledge and dialogue), `Roadmap.md`,
`Testing/TestResults.md`.

## Screenshots

These are fresh, taken after the final UI fixes, from the shipped scene with its UI, by
`Phase4CaptureTests` after 45 simulated days. They are in `Documentation/Screenshots/Phase4/`.

| File | Shows |
|---|---|
| `p4_people_and_inspector.jpg` | The People list with filters, and the NPC inspector (identity, personality, skills) |
| `p4_inspector_relationships_memories.jpg` | Whereabouts with the weekly routine, and relationships with causes |
| `p4_inspector_knowledge.jpg` | Knowledge with status, confidence and source, and history |
| `p4_dm_tools.jpg` | The DM's tools |
| `p4_settlement_residents.jpg` | The settlement inspector's Residents section |
| `p4_exploration_conversation.jpg` | A conversation in Bramhaven: answer and topics |

## Tests

New tests:
- **`NpcEngineTests`** (14):
  - generation from present facts only;
  - locations on layout sites;
  - determinism;
  - tier cadence;
  - population reconciliation (no double counting);
  - routine and need overrides;
  - no omniscience, and rumour propagation with hops and source;
  - knowledge rules and bounds;
  - staleness and rumour expiry;
  - memory bounds and decay;
  - relationships capped and cause-based;
  - event-linked memories;
  - no chronicle spam;
  - exploration placement.
- **`NpcCommandTests`** (9):
  - queries and the ten sections;
  - every DM command sourced and chronicled;
  - eleven rejected commands leave the world unchanged;
  - relocation conserves people;
  - answers come from knowledge, not truth;
  - presence is required to talk;
  - an exact v6 round trip;
  - the once-only v5 → v6 migration with no invented history;
  - corrupt-schedule rejection.
- **`NpcPerformanceTests`** (3): 15, about 100 and about 300 people.
- **`NpcPlayModeTests`** (3, real scene):
  - the People list, filters, inspector sections and a DM tool that dispatches through the
    button;
  - residents in the settlement inspector open their inspector;
  - a real conversation in exploration, with the resident placed in the tavern by a DM move.

Changed tests:
- **`DailyOrder`:** includes the NPC system.
- **v4 save test:** strips v6 data first (`Phase2Worlds.StripToV5`).
- **Graphics test:** uses the identity mapping.
- **Capture fixtures:** now skip from the test body. A skip inside `UnitySetUp` stalled the full
  PlayMode run, and a skipped teardown no longer touches the runner's scene.

**Mutation checks** (each change was reverted):
- Making dialogue read the granary instead of knowledge fails `Answers_ComeFromWhatThePersonKnows…`.
- Disabling population reconciliation fails `Tier3_IsTheRestOfThePopulation…`.

**Defects the new tests found and fixed during the phase:**
- **Repeated news:** speakers repeated news the listener already held, so new facts never
  spread.
- **Crowded-out needs:** urgent needs could not open a goal when four ambitions were held.
- **Goal-event spam:** "achieved" events repeated daily (717 in a year).
- **Order clash:** the system's order collided with a test system.

## Performance

`NpcPerformanceTests` ran 60 simulated days with all systems. Columns: milliseconds per day with
the NPC engine, then without it; save time; load time; save size.

| World | .NET 8 (Release) | Unity editor (Mono) | Save | Load | Size |
|---|---|---|---|---|---|
| 3 settlements, 15 people | 0.58 / 0.23 ms | 0.39 / 0.34 ms | 49–76 ms | 54–70 ms | 206 KB |
| 3 settlements, ~100 people | 0.82 / 0.95 ms | 2.48 / 1.18 ms | 40–98 ms | 126–133 ms | 1.9 MB |
| 10 settlements, ~300 people | 10.4 / 1.7 ms | 14.6 / 3.0 ms | 131–133 ms | 299–460 ms | 6.3 MB |

At normal speed (one day per second), even 300 people use about 1.5% of a second per day.

## Known limitations

- **Dialogue:** templated, with no free text and no LLM (excluded by the brief). There are 6
  conversation topics in exploration; asking about a named person is available as a command but
  has no exploration button yet.
- **Time:** there is no time of day, so a person is at one location for the whole day.
- **Movement:** people do not walk between locations in exploration; they appear at today's
  location.
- **Interiors:** only the tavern has an interior; other locations are building doors or plaza
  areas.
- **Fear:** set only by the DM. No simulated cause raises it yet.
- **Tier 2 detail:** tier 2 people's condition, goals and observations are up to six days out of
  date between their weekly updates; they still follow their routine every day.
- **Body frame:** the sex of the default residents is taken from their given names (no
  pre-existing data). The DM can change it.
- **Save size:** saves grow about 20 KB per named person.

## Handoff

The NPC engine is complete for this phase. Natural next steps belong to a Phase 5 brief:
- time of day, so routines change by hour;
- walking between locations;
- more conversation topics, and asking about people in exploration;
- reputation with the player;
- simulated causes of fear and violence;
- jobs moving with the economy.
