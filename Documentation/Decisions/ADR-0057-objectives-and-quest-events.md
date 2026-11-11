# ADR-0057: Objectives advance from typed quest events inside the command's transaction

**Status.** Accepted, 2026-11-11 (Phase 12).

**Context.**
- Objectives must follow authoritative world state without scanning the world every frame.
- Updates must be idempotent and deterministic.

**Decision.**
1. **Typed events.** A command reports what it did through `CommandContext.Quest(QuestEvent)`. The events are:
   - talked, reached settlement, visited location, investigated, delivered, crafted;
   - defeated, defeated named, protected, escorted, rescued;
   - discovered, decision, quest completed, survived or lost an encounter;
   - objective completed.
   The sources are:
   - entering a settlement;
   - an encounter's resolution (each fallen foe, and whether the player was still on their feet);
   - collecting crafted work;
   - the inspect and read commands;
   - dialogue consequences (gives, decisions, explicit completions).
2. **Processed in the transaction.** After a handler succeeds, `QuestRuntime.Process` applies the events to the open quests, before validation and commit, in this order:
   1. match objectives;
   2. re-read state objectives (collect, acquire, faction approval, a settlement problem resolved);
   3. check timers and failures;
   4. complete what is done.
   A failure (for example a reward too heavy to carry) fails the whole command.
3. **Objective logic.** An objective may be:
   - in a **stage** (sequential): a later stage opens when the earlier ones' required groups are done; one stage holds parallel objectives;
   - in a **group** (alternatives): any member satisfies the group, and a member can name the outcome it leads to;
   - **optional**, **hidden** (revealed when its stage opens, by a consequence, or by being done), **timed**, **conditional**, or **unavailable when** conditions hold;
   - one that **fails the quest**.
   The objective kinds cover everything listed in the brief. Escort and rescue are typed and matched, but no world system emits those events yet (there is no escort AI).
4. **No frame scans.** The daily quest system (order 480) handles lapses, failures, timers, emergent generation and deed reactions. Everything else happens when a command emits an event.

**Consequences.**
- Progress, completion and rewards are atomic with the action that caused them.
- A replayed command is a new action and is validated afresh. A reload changes nothing, because nothing is re-derived.

**Alternatives considered.**
- *Polling objectives each frame:* costly, and it cannot be ordered deterministically.
- *An event bus outside the transaction:* partial updates when a later step fails.
