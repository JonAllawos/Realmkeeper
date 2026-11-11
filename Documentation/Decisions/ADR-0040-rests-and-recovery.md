# ADR-0040: Rests pass world time first, then recover through one idempotent command

**Status.** Accepted, 2026-11-11 (Phase 9).

**Context.**
- Phase 7 had a rest stub with a 2014-style hit-dice rule.
- Phase 9 needs short and long rests with SRD 5.2.1 recovery, hit-die spending, the
  once-per-24-hours rule, and interaction with world time (ADR-0019).

**Decision.**
1. **The time passes first.** A rest is two steps. First `AdvanceTimeCommand` passes the time
   (60 or 480 minutes), so the world lives through it. Then `FinishRestCommand` applies the
   recovery. If the time cannot pass, nothing is recovered.
2. **Recovery is explicit per resource:**
   - `Recovery` (short or long);
   - `ShortRestFromLevel`;
   - `ShortRestRegain` (new: one use of Rage, Channel Divinity and Wild Shape on a short rest);
   - `RecoveryAmount`.

   Hit dice recover in full on a long rest (SRD 5.2.1). The Phase 7 "half" is corrected, and the
   test that pinned it is updated with the reason.
3. **Long rests.** A long rest needs at least 1 hit point and 16 hours since the last one ended. It
   reopens spell preparation.
4. **No double recovery.** Each rest has a key (kind and end time) recorded in a bounded rest log.
   A repeated key is refused.
5. **No rests during a fight.** Time stands still in a fight (ADR-0030).

**Consequences.**
- Rests are visible in the world: shops close and NPCs move.
- Recovery is reproducible: hit-die rolls use the world's deterministic dice keyed to the rest.
- Interrupted rests (a fight breaking out) are not modelled. A rest either passes or is refused.

**Alternatives considered.**
- *Recovery without time:* rejected, because it contradicts the living-world clock.
- *Time inside the rest command:* it would duplicate the time pipeline and its NPC simulation.
