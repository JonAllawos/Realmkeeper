# ADR-0042: Creatures and residents cast through the same engine; caster stat blocks from SRD 5.1

**Status.** Accepted, 2026-11-11 (Phase 9).

**Context.**
- The brief asks for NPC spellcasting on the same engine.
- The Phase 8 creatures (commoner, bandit, guard, wolf) cast nothing.
- No SRD 5.2.1 caster stat block could be confirmed, but the SRD 5.1 Acolyte and Cult Fanatic are
  available under CC BY 4.0.

**Decision.**
1. **Creature spellcasting.** A creature definition may carry `CreatureSpellcasting`: ability,
   save DC, attack bonus, cantrips, spells and slots per level. Its slots become combatant
   resources when the encounter is created, and are saved with it.
2. **Two SRD 5.1 casters.** The Acolyte and the Cult Fanatic are added from SRD 5.1. Their
   statistics are from SRD 5.1; their descriptions are Realmkeeper's own. Their spells are catalog
   spells. The SRD 5.1 notice already ships in the credits.
3. **Residents keep their identity.** A resident can fight with a caster profile through a
   `CreatureId` override; nothing is duplicated (ADR-0031).
4. **AI.** The deterministic caster AI scores ready spells (heal, bless, damage or control, areas
   weighed against allies caught) and proposes the best. The engine validates the proposal.
5. **Practice.** A practice variant with a Cult Fanatic exercises it in play.

**Consequences.**
- NPC casters use the same legality, slots, concentration and logging as the player.
- Their slots do not recover between fights yet; each encounter starts them full.

**Alternatives considered.**
- *Invented caster stat blocks:* rejected (no non-SRD content).
- *Scripted NPC spells:* they would bypass the rules.
