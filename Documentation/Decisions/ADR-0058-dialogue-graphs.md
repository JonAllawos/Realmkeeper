# ADR-0058: Dialogue is data — conditional lines and responses, rolled checks, typed consequences

**Status.** Accepted, 2026-11-11 (Phase 12).

**Context.**
- Phase 4 had topic questions answered from each person's knowledge, with no branching, choices or consequences.

**Decision.**
1. **Graphs** (`DialogueDefinition`). Each node has lines (the first whose conditions hold, and which the content rules allow, is said) and responses (shown when their conditions hold). A response has:
   - a kind;
   - an optional skill check (skill, DC, and a personality trait that raises or lowers it);
   - consequences, applied on success, on failure, or always;
   - the next node (a node, `end`, or back to the start).
   A response can be offered only once per person. The response kinds are ask, request information, offer help, accept, decline, negotiate, threaten, persuade, deceive, intimidate, bribe, trade, challenge, end, continue and give.
2. **The general talk and the hooks.** `npc_general` greets by regard, mood and activity, and offers the Phase 4 topics. The engine adds a way into every quest the person plays a role in: the giver's trouble, or "About …" when a visible objective concerns them. It also adds their shop, unless they are hostile. Each quest's dialogue has one node per role.
3. **Conditions** (`QuestConditions`) are shared by quests and dialogue. They cover:
   - quest state, decisions and variables;
   - the regard dimensions;
   - faction standing and settlement reputation;
   - items held;
   - time of day;
   - the person's activity, traits, knowledge, memories and morale;
   - the player's skills and level;
   - the settlement's food;
   - objectives done;
   - a role's death.
4. **Checks** are d20 plus the rules engine's skill bonus, against the DC. They are rolled with the world's dice, keyed by the conversation and the turn, so the same choice at the same point rolls the same. The check and its result are shown.
5. **Consequences** (`DialogueConsequence`) apply inside `ChooseResponseCommand`, in order, with keyed reputation changes. They cover:
   - answers and offers;
   - accept, decline and abandon;
   - decisions and variables;
   - regard, faction standing and settlement reputation;
   - facts, rumours and memories;
   - deeds;
   - giving and receiving items, paying and receiving coin;
   - revealing a location;
   - starting an encounter;
   - setting an outcome;
   - completing or revealing an objective;
   - morale and notoriety.
   The choice's quest events are applied before the reply is chosen, so the person reacts to what just happened. A failure (for example missing items) changes nothing.

**Consequences.**
- Every conversation path in the slice is a command, testable without the UI.
- The UI is a view: `DialogueView` is what is said, the responses, and the person's standing.

**Alternatives considered.**
- *Hard-coded conversations in the controller:* untestable, with no data to validate.
- *Random checks per frame:* outcomes could be re-rolled by reloading.
