# Reputation, knowledge and memory (Phase 12)

Decision: ADR-0059. This builds on Phase 4's knowledge, memories and regard (`Systems/NPCs.md`, `Systems/Relationships.md`) and Phase 3's faction relations (`Systems/Politics.md`).

## What is kept, and where

| Concept | Where | Range |
|---|---|---|
| Familiarity, affinity (Phase 4) | The person's `PlayerRegard` | 0–100, −100–100 |
| Trust, respect, fear, gratitude, hostility (Phase 12) | The person's `PlayerRegard` | 0–100 each |
| Faction standing | The player's `Social.FactionStandings` | −100–100 |
| Local settlement reputation | `Social.SettlementStandings` | −100–100 |
| Public notoriety | `Social.Notoriety` | 0–100 |
| Every change, keyed | `Social.Ledger` (300 newest) | — |

Faction standing is the player's standing with a faction. It is separate from Phase 3's faction-to-faction relations (opinion, trust, rivalry), which are untouched. A quest may change a faction's influence through the existing value.

## Tiers (presentation only)

Unknown, Recognized, Respected, Trusted, Honored, Feared and Hostile are read from the dimensions, with configurable thresholds in `ReputationThresholds`:

- Hostility of 50 or more, or affinity of −50 or less, means Hostile.
- Fear of 50 or more, and above respect, means Feared.
- Trust of 80 or more with respect of 70 or more, or gratitude of 80 or more, means Honored.
- Trust of 55 or more means Trusted.
- Respect or gratitude of 40 or more means Respected.

## What changes it

| Source | Changes |
|---|---|
| Quest rewards | Regard dimensions, faction standing, settlement reputation (by ending) |
| Quest consequences | Regard (for example, the exposed suspect's hostility), notoriety |
| Dialogue | Small changes for asking, refusing, failed checks; a lie that is caught |
| Abandoning a quest | The giver's trust −10 |
| Deeds heard of | Each listener's reaction by deed and personality, once |

Theft, assault and killing are not implemented as player actions yet. Where the brief lists them, the model is ready (deeds, notoriety, witnesses) but no command produces them. This is documented in `Phase12/Limitations.md`.

## Knowledge, rumour and memory

**Knowledge.** Phase 4's records hold:

- the status: Known, Believed or Rumor, which covers witnessed, told and heard, and incorrect beliefs when a value is out of date;
- the source, hops, confidence and the date learned.

Phase 12 adds the `PlayerDeed` topic. A deed is given to its witnesses with status Known and 0 hops, then shared by the Phase 4 rules: one item per meeting between people with a tie, a hop limit of 3, confidence lost in the retelling, a rumour lifetime, and deeds more interesting than gossip.

**Memories.** Quest and dialogue outcomes become memories with salience, inside Phase 4's bound and weekly decay:

- a person asked the player for help;
- the player found the grain;
- the player lied to them;
- the player brought a letter with its seal broken.

No copy of the Chronicle is kept on people: a memory is one short line, and a deed is one knowledge record.

**The player's side.** Discoveries (facts, with their source), rumours (with who said them and how sure), known places and visited settlements, and decisions are kept, each bounded.
