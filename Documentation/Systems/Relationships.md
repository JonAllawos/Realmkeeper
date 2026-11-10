# Relationships between named people (Phase 4)

One record per related pair (`WorldData.NpcRelations`, IDs stored in order, never duplicated).
Values are mutual in this phase.

| Value | Range | Meaning |
|---|---|---|
| Affinity | −100 … 100 | Liking (negative: dislike) |
| Trust | 0 … 100 | How far they believe what the other says (it raises the confidence of hearsay) |
| Respect | 0 … 100 | Regard for standing and ability |
| Fear | 0 … 100 | Set by the DM in this phase |
| Rivalry | 0 … 100 | Grows with quarrels |

## No all-pairs: every tie has a cause

A relationship exists only because something connected the two people. It records:
- its **origin**;
- its **last cause**;
- when it **began**;
- when it last **changed**;
- how many **conversations** it has had.

| Cause | Created when | Starting values |
|---|---|---|
| Shared household | They live in the same residence | Affinity +35, trust 70 |
| Shared workplace | They work at the same location | Affinity +10, trust 50 |
| Faction hierarchy | A faction leader and a notable member in the same faction | Respect 60 |
| Conversation | They talked (same location, or neighbours in the evening) | Neutral |
| Manual intervention | The DM set values between two unconnected people | As set |
| Quarrel, kindness, shared hardship | Changes to an existing tie | — |

No one has more than 16 ties. When a person is full, conversations with new people leave no tie.
At world creation and on migration, only shared homes, shared workplaces and faction hierarchy
create ties.

## How ties change

- **Conversation:**
  - compatibility is 100 minus the sociability gap, minus half the greed gap;
  - ≥ 60: affinity +2 plus their combined compassion ÷ 40;
  - ≥ 35: affinity +1;
  - below 35: affinity −3 and rivalry +2, recorded as a quarrel.

  Trust moves a point with each conversation.
- **Kind:** follows the values. Affinity ≥ 40 makes friends; ≤ −35 (or rivalry ≥ 60) makes
  rivals; otherwise colleagues or acquaintances. Household and leader ties keep their kind.
  Becoming friends or rivals is chronicled.
- **Weekly drift:** ties not touched for a week drift one point toward neutral, except households.
- **Death:** when someone dies, everyone with affinity ≥ 10 toward them remembers it, and their
  morale drops.

## Where they show

- **NPC inspector, Relationships:** sorted by strength, with each tie's values, kind, origin, last
  cause and number of conversations.
- **Dialogue:** asked about a person, someone adds "A good friend." or "Can't stand them." where
  the tie says so.
- **DM:** `SetNpcRelationshipCommand` (Manage → Set affinity).
