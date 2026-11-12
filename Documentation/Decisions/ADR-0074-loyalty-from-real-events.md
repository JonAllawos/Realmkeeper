# ADR-0074: Loyalty moves only from real events, once per cause, with its reason

**Context.** Companion approval in many games is driven by dialogue choices that exist only to please a companion. The brief forbids trivial approvals and asks that every change be explained. The world already records deeds: crimes with witnesses (Phase 14), quest outcomes (Phase 12), and what happens in fights (Phase 8).

**Decision.**
1. **One entry point.** `CompanionLoyalty.Apply(world, person, key, delta, reason, report)` applies a cause once per stable key and records it (`ApprovalData`: key, delta, reason, minute). It moves liking (Affinity) at half the rate through the existing `Reputation.Change` ledger, and announces changes of 5 or more as chronicle events (`CompanionLoyalty`). It keeps the last 40 causes.
2. **The causes are real events:**
   - **A crime the companion saw** (they were with the player when it was recorded). The weight comes from the deed (murder −20, assault −8, theft −6, trespass −2). Discipline 60+ makes it heavier (×1.5), and so does compassion 60+ for violence. Greed 60+ makes theft lighter (×0.5), and discipline below 30 makes non-violent deeds lighter (×0.5). A greedy (75+), undisciplined (below 40) companion approves of a theft (+3). The reason names the trait.
   - **Their own quest settled:** done +25, failed −10, abandoned −15.
   - **A fight.** Left on the ground when the player withdrew: −20. Stabilised by the player's own hands: +5. Killed: they leave the party.
3. **Departure and return.** Below 15 a companion leaves (`Departed`, with the reason), but never in the middle of a fight. The daily party rule catches it afterwards. They come back only when loyalty is mended to 35.
4. **The personal quest is an ordinary bound quest.** "Old Ground" (original text) is offered once by a companion who has travelled a full day with the player at loyalty 60 or more. It is bound to them and to a place near their home. It advances from entering that place and talking it over, through the existing quest engine and the giver dialogue pattern.

**Consequences.**
- No approval exists that the world did not cause. No dialogue option exists only to raise loyalty.
- The same cause never counts twice (keys: crime ID, `abandoned:`/`saved:` + encounter ID, quest instance + outcome).
- Initial loyalty is 40 plus a third of the person's liking for the player (up to +30), so friends start steadier.

**Alternatives considered.**
- **Approval tags on dialogue choices.** Rejected as trivial approvals.
- **A separate relationship model for companions.** Rejected: `PlayerRegard` and its ledger already exist, and loyalty is the one new number.
