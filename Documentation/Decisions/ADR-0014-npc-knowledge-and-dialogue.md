# ADR-0014: Knowledge separate from truth; dialogue only from knowledge

**Status.** Accepted, 2026-11-10 (Phase 4).

**Context.** People should know what they plausibly could, be wrong in believable ways, and
spread news imperfectly. The phase rules out an LLM or any external AI service, so answers must
come from the simulation, not be invented.

**Decision.**

1. **Four layers.**
   - The world's truth (`WorldData`).
   - What a person knows first-hand (Known).
   - What they believe (Believed: observed but possibly out of date, or heard from someone
     trusted).
   - What they have heard (Rumour).

   Each piece records its value as of the day learned, confidence, the source person and how many
   tellings away it is.
2. **No omniscience.** People observe only the settlement they are in (food, prosperity,
   governor), their own faction's leader, the people they talk to (whereabouts), and the events
   that touch their settlement or faction. Everything else arrives by conversation.
3. **Propagation is constrained.**
   - One fact per direction per conversation, and only one the listener does not already hold.
     News comes before whereabouts.
   - Each telling costs confidence, adjusted by trust.
   - At most 3 tellings from the observation.
   - Hearsay never overrides current first-hand knowledge.
   - First-hand knowledge goes stale after 14 days; rumours expire after 45.
4. **Dialogue is typed.** `AskNpcCommand(person, topic)` answers from the person's knowledge,
   memories and self only, through fixed templates.
   - Status shows as a hedge: "last I knew", "I've heard".
   - Missing knowledge yields "couldn't say".
   - An out-of-date belief is stated as they believe it.
5. **The truth stays visible to the DM.** The inspector marks knowledge that no longer matches the
   world.

**Consequences.**
- **Testable:** tests can assert that an answer contradicts the truth when the person was told
  otherwise, and that strangers know nothing of each other.
- **Templated speech:** dialogue is short and formulaic. A later phase can add phrasing variety,
  or a model, behind the same typed interface, without changing what people know.

**Alternatives considered.**
- Reading the truth directly: omniscient, and nothing for players to discover.
- Free-text generation: excluded by the brief, and it cannot be kept consistent with the world
  without this layer anyway.
