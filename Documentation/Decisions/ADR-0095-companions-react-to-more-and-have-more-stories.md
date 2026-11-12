# ADR-0095: Companions react to more of what happens, keep quiet in between, and have more stories

**Context.** Phase 17 companions reacted to campaign decisions, crimes and their home towns, and had one second story (Unfinished Business). The brief asks for:
- at least two more personal story templates;
- contextual reactions with cooldowns and once-only rules;
- party interactions;
- AI fixes for problems found, without a rewrite.

**Decision.**
1. **Reactions** (`CompanionStories.Phase18.cs`):
   - **Quest outcomes.** A table of trait leanings covers the Phase 18 decisions (refugees, unrest, conflict, cairn, drift, reeve). Loyalty and a remark are given once per quest.
   - **Major discoveries,** to companions whose nature cares about that kind (ADR-0088).
   - **Hardship** in a hungry or restless town: once a week per town.
   - **Banter** between companions who like each other: once a week per pair.
   - The passing kinds (discoveries, hardship, banter) wait out a 6-hour cooldown after any passing remark.
   - This amends ADR-0087's "no random banter": the banter comes from a real relationship, not chance.
2. **Stories** (`QuestCatalog.Phase18Companions.cs`). Offered under the Phase 17 rules (loyalty 70, first story behind them) when no rival waits elsewhere:
   - **Going Home:** family living in another town. Two endings: vouched, or left alone together.
   - **Words at the Grave:** a friend or family member dead within 60 days. Two endings: spoke, or silent.
   - Unfinished Business now needs a real quarrel (a rival, or rivalry 30 or more), so family and friends lead to the new stories.
3. **AI fix found by inspection, not play.** Followers would pass through rooms the player must swim. They now refuse undrained deep water, as they already refused damaged bridges, and the reason is shown ("the water is too deep for them").

**Consequences.** Hands-on checks of formation, waiting and transitions remain on the owner playtest checklist. They could not be done without interactive play.
