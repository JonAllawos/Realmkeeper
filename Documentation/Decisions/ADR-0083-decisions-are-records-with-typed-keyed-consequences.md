# ADR-0083: Decisions are records with typed, keyed consequences — some now, some later — applied once

**Context.** Campaign choices must change the persistent world, not just the next line of dialogue. Their consequences must survive saves without ever being applied twice, some must come later, and companions must be able to react. The brief forbids an untyped scripting system.

**Decision.**
1. **The definition (`DecisionDefinition`)** has:
   - a stable ID, its arc, a prompt, and who puts it (`SpeakerRole`; null means the player decides alone, from the journal);
   - availability conditions;
   - choices.

   **A choice (`ChoiceDefinition`)** has:
   - its words and a journal summary;
   - conditions, and an `UnavailableText` shown when they fail;
   - typed effects and delayed consequences;
   - personality leanings (ADR-0087);
   - an outcome for its arc.

   The validator requires every choice to change something, and at least one choice to be unconditional.
2. **The record.** A decision is recorded once per campaign (`DecisionRecordData`: choice, time, and the name of who put it) and is never undone. It also goes into the player's decision log, and emits a quest `Decision` event, so a quest objective waiting on it completes in the same transaction.
3. **Typed effects (`CampaignEffect`).** The kinds are:
   - regard, faction standing and influence, faction opinion;
   - settlement stability, prosperity and resources;
   - NPC memory, NPC relation, NPC morale;
   - companion loyalty;
   - start a campaign quest, discovery, spread (people of a settlement learn it, then it travels), player deed;
   - raise a world event, road condition;
   - flag, arc state, experience, coin, Chronicle note.

   Each goes through the authoritative path of the system it touches:
   - `Reputation.Change`, with its keyed ledger;
   - `CompanionLoyalty.Apply`;
   - `WorldEvents.Raise` and `TellSettlements`;
   - `NpcMind.Remember`, `Learn` and `Relate`;
   - `QuestLifecycle.Create`.
4. **Idempotency.** Every effect is applied once, by key: `{instance}:{decision}:{choice}:{index}`, or `arc:{arc}:{state}:{index}`, or `{pending}:{index}`. The keys are kept in `Applied`, and reputation changes are also keyed in the player's ledger.
5. **Delayed consequences.**
   - A choice may schedule `DelayedConsequence`s, each with a delay in days and optional `When` conditions.
   - They are persisted as `PendingConsequenceData` with a due time, and fired once (`Fired`, `FiredAt`) by the daily pass, or by any re-read, once they are due and their conditions hold.
   - The world does not wait for them, and they fire even after the campaign closes, because they are the world's consequences of a decision already made. Effects that would advance a closed campaign (starting a quest, moving an arc) do nothing then.
   - A consequence can announce itself in the journal (`Hint`) or not. Once it happens, the journal shows its text.
6. **Shared evaluator.** Conditions use the shared `QuestCondition` with new kinds:
   - campaign arc, decision, flag, state, quest and arc-days;
   - character status;
   - world event open (on a route or settlement role);
   - companion loyalty, settlement stability, route open;
   - NPC relation, has-remark, campaign standing;
   - player coin and companion quarrel.

   The application's evaluator hands kinds 22–35 to the simulation's `CampaignConditions`, so dialogue and campaign arcs read the same answers.
7. **Dialogue.** Dialogue makes decisions with a `CampaignDecision` consequence, inside the choose command, so they commit with the conversation or not at all. A response can carry `Requirements`; with `ShowWhenUnavailable` it is shown disabled with its reason, and choosing it is refused.

**Consequences.**
- Decisions change real state: standings, stabilities, roads, people's memories and regard, world events. The tests check these after save, reload and many simulated days.
- A decision can neither be reapplied nor reversed by reloading. A delayed consequence fires exactly once.

**Alternatives considered.** Consequences as dialogue lines only (rejected: they change nothing). Re-evaluating "has this happened?" from the world each time (rejected: it cannot tell a consequence apart from the same change made by something else).
