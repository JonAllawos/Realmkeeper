# ADR-0087: Companions react by their nature, say what they think when asked, and relate to each other through the existing records

**Context.** Phase 17 asks for companions who are people in the story. They should react to decisions, have more than one personal story, talk about what happened, and form relationships with each other. Loyalty must change only from real events, through the existing relationship architecture, without a companion-AI overhaul.

**Decision.**
1. **Reactions to decisions.**
   - A choice declares leanings: a personality trait and a sign, with the words an approving or objecting companion would say.
   - Each companion travelling with the player (active members present, never reserves) reacts by their own traits: the sum of sign × (trait − 50) / 10, bounded at ±8. A reaction below 2 is indifference.
   - Loyalty moves once per decision, through `CompanionLoyalty.Apply`, keyed `campaign:{campaign}:{decision}`, with the reason in words. They remember it (`NpcMind.Remember`).
2. **Remarks.**
   - What a companion has to say is a bounded record (`CompanionRecordData.Remarks`, newest 12, one per cause key). Causes:
     - a decision they felt strongly about;
     - falling out, or making up, with another companion;
     - arriving where their own people live (a household, friend or rival);
     - a world event the player knows of at their home.
   - The player asks ("Something on your mind?" in general talk). The companion says the oldest unheard remark, once.
   - There is no random banter.
3. **Companion to companion.**
   - Two companions who react on opposite sides of the same decision grow apart (Phase 4 relation: rivalry +12, affinity −8, cause Quarrel). On the same side, they grow closer (affinity +6, trust +4).
   - At rivalry 20 or more (two disagreements), the quarrel shows. The player can try to talk them round with a Persuasion check:
     - success eases the rivalry, and both gain a little loyalty;
     - failure hardens it.
4. **A second personal story.**
   - "Unfinished Business" (`companion_unfinished_business`) is asked once, by a companion who:
     - is active and not temporary;
     - has loyalty 70 or more;
     - has their first story behind them;
     - has a bond with someone living elsewhere (a rival, a friend, or household).
   - It is bound to that real person. It advances only from real events (arriving in their town, then the meeting) and ends one of three ways: mended, paid, or defied. Each way changes the real relationship.
   - Closing it moves loyalty through the existing personal-quest rule: +25 for completion, −10 for failure, −15 for abandonment.
   - "Old Ground" is unchanged.

**Consequences.**
- Disagreement always has a stated cause, and loyalty changes are tied to real events. Reserve companions are not part of the travelling party's consequences.
- The Party section shows each companion's second story, what they have to say, and how they get on with the others.

**Alternatives considered.** An approval meter per companion per topic (rejected: it would duplicate loyalty and the relationship records). Random banter (rejected by the brief).
