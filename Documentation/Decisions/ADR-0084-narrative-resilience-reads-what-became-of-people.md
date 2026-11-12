# ADR-0084: Narrative resilience reads what became of people from the world, and offers other ways — never resurrection

**Context.** Story-critical people must stay actual people in the world: they can die, turn hostile, go away, be captured or be replaced as leaders. Campaigns must adapt instead of protecting them or bringing them back.

**Decision.**
1. **Status is derived, never stored.** `CampaignCharacters.StatusOf` reads, in this order:
   - Dead: `IsDeceased`;
   - Captured: a captive escort;
   - Allied: in the party;
   - Departed: a companion who left;
   - Hostile: regard hostility of 70 or more, the line shops already use;
   - Missing: no home;
   - Unavailable: away (`PlacePresence.IsAway`, which covers leading a caravan, posted elsewhere, or travelling with the player);
   - Injured: health below 40;
   - otherwise Available.

   The campaign never sets any of these.
2. **Roles that follow the world.**
   - `FactionLeader` reads the faction's current leader, so a succession changes who answers.
   - `FirstAvailable` takes the first of several roles whose person can still be met, so the warden or, failing them, the far faction's leader speaks for the far town.
3. **Other ways on.**
   - Alternative objective groups let an arc finish another way, with its own outcome: if the warden is gone, the letter goes unanswered and the story continues.
   - `ArcFallback`s offer a further campaign quest when their conditions hold: if the toll-keeper cannot answer, their household is asked.
   - State objectives close an arc as lost when nobody is left who knows.
4. **Honest failure.** Arcs fail when the world says so: a time limit passes ("the towns settled it without you"), or `FailWhen` holds. Quests still fail by their own rules, for example when a quest's person dies.
5. **No protection, no resurrection.** No campaign person is protected from the world's systems, and nothing in the engine can clear `IsDeceased`. The tests confirm a dead person stays dead across days and reloads.

**Consequences.**
- The demonstration campaign plays to an ending whoever dies or turns hostile. The tests kill the warden, the toll-keeper and their household; make the toll-keeper hostile; send them away; and replace the far faction's leader.
- The journal names each person's fate only once the player has met them.

**Alternatives considered.** Essential (unkillable) NPCs (rejected by the brief). Stored story-state copies of people (rejected: they would contradict the world).
