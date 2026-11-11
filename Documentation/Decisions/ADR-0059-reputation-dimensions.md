# ADR-0059: Reputation has separate dimensions; deeds travel as knowledge, and only those who hear react

**Status.** Accepted, 2026-11-11 (Phase 12).

**Context.**
- Phase 4 gave each person familiarity and affinity toward the player.
- Factions related only to each other (`FactionRelationData`: opinion, trust, rivalry).
- The brief asks for distinct dimensions, contextual changes and witnesses.

**Decision.**
1. **Per person** (`PlayerRegardData`): familiarity and affinity (Phase 4), plus trust, respect, fear, gratitude and hostility, each 0–100.
2. **Per faction and settlement** (`PlayerSocialData`):
   - faction standing and settlement reputation, each −100 to 100;
   - notoriety (0–100).
   **This is the player's standing with a faction. It is not the faction-to-faction relations of Phase 3.** Those stay as they were: the player is not a faction, and no faction calculation is bypassed. A quest may change faction influence through the existing value.
3. **Keyed changes.** `Reputation.Change(target, dimension, amount, key)` writes a bounded ledger entry. A key already applied is refused, so a reload, a replay or a second consequence never doubles a change. Changes of 10 or more, and all faction and settlement changes, are reported to the Chronicle.
4. **Tiers** (Unknown, Recognized, Respected, Trusted, Honored, Feared, Hostile) are read from the dimensions with configurable thresholds (`ReputationThresholds`). They are for presentation only. Hostility and fear outrank warmth.
5. **Witnesses.** A deed (`PlayerDeed` knowledge) is given to the people who witnessed it. It then travels through Phase 4's sharing: one item per meeting, a hop limit, the rumour lifetime, and higher interest for deeds. Each person who learns of it reacts once, by the deed and their own personality (`Reputation.Reaction`). Nothing is broadcast. In the test world a deed reached 9 of 19 people in 30 days.
6. **Effects.**
   - Dialogue lines and responses read the dimensions.
   - Faction standing moves shop prices ("Standing with …" as a named price factor).
   - Hostility of 70 or more, or standing of −50 or less, closes a keeper's shop and the trade response.

**Consequences.**
- Stealing from one merchant would not turn every faction. There is no theft mechanic yet, which is a documented limitation.
- One universal reputation number is never used.

**Alternatives considered.**
- *A single karma value:* the brief rules it out, and it cannot say "trusted but feared".
- *Instant global propagation:* everyone would know everything.
