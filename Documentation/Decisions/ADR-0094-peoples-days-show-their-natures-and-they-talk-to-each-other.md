# ADR-0094: People's days show their natures and their town's condition, and they talk to each other about what they know

**Context.** The audit found:
- personality changed dialogue and decisions but not what people did, apart from sociability (the tavern);
- people never visibly talked to one another;
- Sitting, Eating and Resting rendered as standing.

**Decision.**
1. **Natures in the day** (`NpcActivityPlan.At`). All deterministic, with the reason shown in words:
   - Discipline 65 or more moves the working day 30 minutes earlier and later; 30 or less, a late start. Shopkeepers and guards are excepted.
   - Curiosity 65 or more spends a free day at the market.
   - Compassion 65 or more in a hungry town hands out bread in the square of an evening.
   - Courage 35 or less stays in while the town is restless.
   - A prosperous town (70 or more) goes out more (sociability 45 is enough); a poor one (30 or less), less (70 needed).
2. **Overheard exchanges** (`NpcExchanges.Now`, `WorldSession.GetExchanges`). Only people who know each other (household, friends, workmates, rivals), only at the same place now, and only when free to talk. They talk about what they really know, worded by certainty:
   - "I saw it myself" (known first-hand);
   - "I'm told" (believed);
   - "there's talk…" (rumour);
   - plus the price of bread and the traveller they have met.
   Rivals argue. The presentation shows lines above pairs within 14 m, and the two turn to each other. Nothing changes in the world.
3. **Sitting poses: deferred.** The library has sitting clips, but the generated streets and interiors have no seats, so a seated pose would float. This is left for the UI/visual workstream, with that reason.

**Consequences.** Tests cover the condition-driven choices and that exchanges only pair people who know each other and stand together.
