# ADR-0085: The world offers its own stories, built only from what happened and who knows it

**Context.** Emergent storytelling must come from the existing systems: events, people, knowledge, settlements and roads. It must never invent facts or flood the player with generic quests, and must not use AI or online services.

**Decision.**
1. **The daily story pass.** `StorySynthesis.Run`, called by the daily `CampaignSystem` at order 482, looks at what actually happened, in a deterministic order (event numbers, then settlement IDs). There are two kinds:
   - **A lost caravan.** The giver is someone of the town it set out from. The witness is someone at an end of its road who holds the event as knowledge (NpcMind). Their account is `WorldEvents.Telling` of the event, plus the caravan leader's real fate. With no witness, there is no story.
   - **A hungry town beside a full one.** The giver is the hungry town's elder. The merchant is in a settlement reached by an open road, preferably in another region, that holds at least 30 days of food. Done, 60 measures of food really move between the two towns' stores.
2. **Bounds.**
   - One story per cause, ever: the key is `story:{kind}:{cause}`, remembered in the player's applied keys even after the quest is pruned.
   - At most three open, and at most one new each day.
   - A story whose cause ends lapses or fails through the existing quest-cause rules (`QuestCauses` `story:` case).
3. **Through the ordinary systems.**
   - Stories are ordinary quests (category Emergent) through `QuestLifecycle`, with their own dialogue.
   - The giver carries the story as knowledge (`RecentEvent`), so it spreads by the people's own talk, and the existing quest mentions put it in rumours.
   - A Chronicle `StoryOpportunity` event records it.
4. **No outlaw story.** Phase 16's event quests already cover outlaw bounties, so the synthesis does not duplicate them.

**Consequences.**
- Every story is checkable against the world. The tests compare a story's account with the event's own telling, confirm that a witness's knowledge is real, and check that food really moved.
- The pass costs about 0.5 ms a day on the connected world.

**Alternatives considered.** Generic "fetch" quests on a timer (rejected: endless and context-free). Writing an account from templates without reading the event (rejected: it could contradict the world).
