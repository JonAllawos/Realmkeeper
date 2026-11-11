# ADR-0045: Reactions and decisions are asked, not taken: suspend, ask, replay

**Status.** Accepted, 2026-11-11 (Phase 10).

**Context.**
- Reactions (Shield, opportunity attacks, Parry, Uncanny Dodge, Counterspell) and on-the-spot decisions (Indomitable, Bardic Inspiration) happen in the middle of someone else's action.
- Before Phase 10 the engine decided them itself or skipped them. The player was never asked.
- Pausing an engine half-way through an action and resuming it would need continuations throughout the rules code.

**Decision.**
1. **Ask at the rule point.** Each reaction point calls `Ask(opportunity)`.
   - A computer-controlled reactor decides at once by a fixed policy.
   - A player-controlled reactor uses the answer recorded for this replay. With no answer, the engine throws `ReactionPending`.
2. **Suspend.** `Resolve` snapshots the encounter (when a player-controlled combatant is present) and runs the action. On `ReactionPending` it restores the snapshot **in place**, so combatant identities are kept and nothing of the action is applied. It then stores `PendingReactionData`: the action, the opportunity and the answers so far.
3. **Answer and replay.** `RespondToReactionCommand(encounter, id, accept)` appends the answer and replays the action from the snapshot. Dice are counter-based (ADR-0032), so the replay rolls the same dice and reaches the same point, where the answer is now known. Several questions in one action are asked one after another.
4. **Guards.**
   - While a question waits, other actions are refused.
   - A stale or repeated answer is refused.
   - Answering is atomic: the whole action resolves or nothing does.
   - A waiting question is saved and loaded (schema v12) and validated.
5. **Cost first.** Nothing is offered without its cost: a reaction available, a slot, a use. The prompt says what triggered it, what it costs and what it does.
6. **Presentation.** The encounter panel shows the question as a banner above every column, with Accept and Decline. While it waits, the automatic computer turns stop.

**Consequences.**
- The rules code stays straight-line. The cost is one snapshot and one replay per question.
- Declining leaves the action exactly as if no question had been asked; accepting matches an immediate answer.
- The UI only shows the prompt and dispatches the answer.

**Alternatives considered.**
- *Continuations or a coroutine engine:* a rewrite of every rule path.
- *Asking before the action:* the trigger is unknown until the dice fall.
- *Auto-deciding for the player:* the brief rejects this.
