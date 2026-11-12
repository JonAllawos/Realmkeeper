# ADR-0092: Rewards are tools with uses, and every reward is given once

**Context.** The brief asks for better rewards: a modest set of distinctive items and tools, and a review of progression integrity (no duplicate rewards, idempotent awards).

**Decision.**
1. **Distinctive tools, each with a use in the place mechanics.**
   - The **pump-wright's wrench** mends a cracked pump. It is the reward for reopening Greywater Drift.
   - The **shuttered lantern** gives raking light: it reads the scoring on the Hollin lintel, and it counts as a light anywhere. It is the reward for the first investigation quest.
2. **Evidence items, used in decisions.** The old watch charter and the reeve's true ledger. A third candidate, a ringer's hand-bell, was dropped because it had no use.
3. **Integrity.** Every new award is keyed and given once:

   | Award | Key |
   |---|---|
   | Major-discovery experience | `discovery:{key}` |
   | Sharing a discovery | the discovery's `SharedIn`, plus ledger keys |
   | Companion reaction to an outcome | `outcome:{instance}` |
   | Recovered caravan goods | the event's `recovered` tag |
   | Weekly faction strain, relief and garrisons | the event's `Once` keys |
   | Quest rewards | as before, `{instance}:reward:{i}` |

   Tests assert the second attempt gives nothing.
