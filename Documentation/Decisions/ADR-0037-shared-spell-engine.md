# ADR-0037: One spell engine, driven by data, for every caster

**Status.** Accepted, 2026-11-11 (Phase 9).

**Context.**
- Phase 9 brings spells to twelve classes, to creatures and to residents.
- Phase 8 already resolves attacks, saves, damage and conditions in one domain engine (ADR-0030).
- A second resolution path for spells would drift from it.

**Decision.**
1. **Spells are data.** Each `SpellDefinition` has an ordered list of typed `SpellEffect`s (damage,
   healing, condition, modifier, teleport …) and a targeting and area description. Nothing about a
   spell is code except the effect kinds.
2. **Casting is an engine action.** `CastSpellAction` goes through `CombatEngine.Act`, like an
   attack. It uses the same action sequence, deterministic dice, log and validation.
3. **Shared mechanics.** Spell attacks use the shared `AttackRoll`; saves use the shared
   `SavingThrow`; damage uses the shared `ApplyDamage`. A rule fixed once is fixed for weapons and
   spells alike.
4. **One path for every caster.** The player, residents and creatures use the same path. Only where
   the spell list and slots come from differs:
   - the character's choices and resources (`SpellAccess`);
   - or a creature's `CreatureSpellcasting` block.
5. **Honest status.** Each spell carries an `ImplementationStatus` and a note. DataOnly and Deferred
   spells are listed but cannot be cast; the reason is shown.

**Consequences.**
- New spells are mostly data.
- The audit (`Systems/SpellCatalogAudit.md`) can be produced from the catalog.
- The AI proposes spells through the same legality checks as the player.
- Areas are computed on the 5-ft grid with its Chebyshev distance, so spheres are squares of
  squares. This matches the movement rules.

**Alternatives considered.**
- *A scripting hook per spell:* more freedom, but every spell becomes untested code.
- *A separate magic subsystem:* rejected because it duplicates the roll rules.
