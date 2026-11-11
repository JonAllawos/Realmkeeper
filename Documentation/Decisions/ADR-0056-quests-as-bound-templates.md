# ADR-0056: Quests are templates bound to real entities; one engine for authored and emergent quests

**Status.** Accepted, 2026-11-11 (Phase 12).

**Context.**
- Realmkeeper's people, places and factions are generated. A quest cannot name its people in advance.
- The brief asks for many quest categories without separate engines, for persistent, deterministic instances, and for no completion twice and no silent reactivation.

**Decision.**
1. **Definitions are templates** (`QuestDefinition`). A definition has:
   - text with `{role}` tokens;
   - role specifications (`RoleSpec`): a person by their role in a settlement, the giver's settlement, someone's faction, an object, an item or a creature;
   - prerequisites, availability, objectives, failures, rewards, consequences, follow-ups and expiry;
   - a dialogue, content tags, a source and a version.
   The 12 categories label definitions. They are not engines.
2. **Instances bind roles** (`QuestInstanceData`).
   - The generic binder (`QuestBinder`) fills each role deterministically: candidates sorted by ID, picked by a seed.
   - An instance keeps:
     - its bindings, state and objective progress;
     - its decisions, variables and outcome;
     - which rewards and consequences were applied;
     - its failure reason and a bounded history.
   - Its ID is stable: `quest:{definition}:{giver or cause}`, with a generation suffix if repeated.
3. **States** are Unavailable, Available, Discovered, Offered, Accepted, Active, Completed, Failed and Abandoned. `QuestStates.CanMove` checks every transition. A closed quest never reopens unless it is repeatable. Each move is recorded in the history and reported to the Chronicle once.
4. **The problem is real.** A consequence timed `OnAvailable` happens when the instance comes into being. "Short Measures" removes the missing grain from the settlement's stores then. Completion consequences restore it, or do not, by outcome.
5. **Completion is transactional.** All required groups done completes the quest inside the command that did the last step. Rewards, consequences, follow-ups and the state commit together, or the command fails and nothing changes.

**Consequences.**
- The same quest works in any generated world. Its people are whoever the world has.
- No main-campaign content is authored. The category exists, and canon stays open.

**Alternatives considered.**
- *Scripts per quest:* not data, not testable, duplicates the engine.
- *Fixed NPC IDs in data:* generated worlds have none.
