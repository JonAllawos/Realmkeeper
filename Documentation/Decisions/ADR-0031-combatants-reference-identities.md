# ADR-0031: Combatants reference persistent identities; the sheet stays the one source of the player's vitals

**Status.** Accepted, 2026-11-11 (Phase 8).

**Context.**
- Persistent NPCs must never be duplicated.
- The player, residents, creatures and temporary entities must all be able to fight.

**Decision.**
1. **A combatant is a participation record** (`CombatantData`): kind, side, position, conditions,
   turn resources and initiative. It names its source by ID: the player's character, an NPC's ID,
   or a creature catalog ID.
2. **The player's vitals stay on the character sheet:** hit points, temporary hit points and death
   saves change through `CharacterVitals`. For residents and creatures, the combatant holds them.
3. **One resident at most once per encounter.** This is enforced at creation, on joining and by
   the validator.
4. **Residents fight with an SRD profile chosen by role:** a guard uses Guard, everyone else uses
   Commoner. Full NPC character sheets are deferred (ADR-0029).
5. **A resident who dies in a fight dies through the shared routine** (`NpcSystem.RecordDeath`).
   - The routine handles the deceased record, goals, faction succession and memories.
   - The command adds an `NpcDied` event with the cause "combat".

**Consequences.**
- No second copy of a person exists anywhere, and a dead resident stays dead in the simulation.
- Residents' combat statistics are generic until NPCs have character sheets.

**Alternatives considered.**
- Copying NPCs into the encounter: rejected, because it duplicates identity and loses changes.
