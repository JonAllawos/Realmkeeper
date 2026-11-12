# ADR-0091: Four quest chains, and The Waystone Count deepened without changing what it is

**Context.** The audit found 22 quests, of which 18 were linear and only 4 had a decision. The brief asks for four substantial chains (investigation, faction, wilderness/dungeon, settlement/economy), each with decisions, two or more outcomes, persistent consequences, journal updates and rewards. It also asks for The Waystone Count to be deepened while staying provisional.

**Decision.**
1. **Chains of two quests.** The second follows the first through `FollowUps`, with the same people and place.

   | Type | Chain | Ways to end |
   |---|---|---|
   | Investigation | Ashes in the Record-House → The Reeve's Accounts (Hollin Tower) | the reeve exposed, made to restore what was taken, or bought off |
   | Faction | The Cairn Question → Who Keeps the Cairn (Cairn Watch) | back the claim, uphold the holder, mediate under the old charter, or stay aside |
   | Wilderness/dungeon | The Drowned Drift → Greywater's Future (Greywater Drift) | the drift reopened (only once drained) or sealed as a grave |
   | Settlement/economy | The Miller's Measure → A Mill for Two | the old measure restored by persuasion, the miller judged right, or the shortfall supplied |

   Outcomes change stability, prosperity, food, faction influence, faction opinion, regard, memory and notoriety through the existing consequence paths.
2. **New vocabulary, kept small.**
   - `ConditionKind.KnowsDiscovery` lets dialogue reward what the player found.
   - `ConsequenceKind.FactionRelation` (ADR-0090) and `ResolveWorldEvent` (ADR-0093).
3. **The Waystone Count.** No new arc or chapter; no new canon. Two additions to existing beats:
   - **Precedent for the shared count.** The watch log of Cairn Watch, or the ringers' verse, can make the case for sharing the count. This joins exploration to the campaign, across regions.
   - **"Witness".** A fourth way to settle the toll-keeper's count: they own it before both towns, with the evidence. It has its own delayed consequence and companion leanings.

**Consequences.** There are now 37 quest definitions (the audit found 22). Every Phase 18 decision has at least two outcomes, tested through conversation with save and load in the middle.
