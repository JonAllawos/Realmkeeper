# ADR-0090: Faction conflicts move through stages, factions act without the player, and the player can take part

**Context.** Phase 16 raised a conflict event for rival or hostile factions, and barred their crossing when they were hostile. Between those, nothing happened. The only faction activity was a paid patrol against bandits, and the player had no part in a conflict.

**Decision.**
1. **Stages.** `WorldEventSystem.ConflictStage` reads the stage from the relationship. It is stored as the event's severity, so no new state is saved.

   | Stage | Condition |
   |---|---|
   | 0 Peace | not rivals or hostile |
   | 1 Tension | rivals |
   | 2 Dispute | rivals, opinion −40 or worse |
   | 3 Skirmishes | hostile |
   | 4 Open conflict | hostile, opinion −75 or worse |

   - A change of stage is noted in the event's history ("…became…" or "…eased to…"), and the towns at the crossing are told.
   - **Dispute or worse:** each week the two border towns lose stability, 1 point in a dispute and 2 from skirmishes up.
   - **Open conflict:** the Phase 16 blockade. Easing below it stands the soldiers down and opens the way; peace resolves the event.
2. **Factions act on their own** (`FactionActivitySystem`).
   - **Relief:** a faction governing a hungry settlement buys it 40 food for 50 coin from its treasury, once a week while the shortage lasts.
   - **Garrisons:** from skirmishes up, each side pays 30 coin a week to garrison its town at the crossing, for 2 stability.
   - Both are noted on the events.
3. **The player's part.** "Bad Blood at the Crossing" (`story_conflict`) is offered from a dispute or worse. The player can:
   - back the side that asks;
   - back the other side;
   - mediate: a Persuasion check, or freely when standing 20 or more with both sides;
   - stay neutral;
   - ignore it: the offer lapses after 10 days.

   Outcomes move the two factions' opinion through a new quest consequence, `ConsequenceKind.FactionRelation`, and the player's standing with each. Faction-standing rewards and conditions now resolve a faction role directly, not only a person's faction.

**Consequences.** The world evolves without the player: opinions move daily (Phase 9), and stages and their effects follow. The Phase 16 blockade test still holds at stage 4.
