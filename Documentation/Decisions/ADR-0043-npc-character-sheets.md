# ADR-0043: Named people carry character records; sheets by archetype, promotion on demand

**Status.** Accepted, 2026-11-11 (Phase 10).

**Context.**
- ADR-0029 says the rules services take any sheet. In practice, until Phase 10 only the player had one.
- Residents fought as generic stat blocks (`commoner`, `guard`). A guard who survived ten fights stayed the same.
- A full sheet for every named person costs save size and validation time. Most people never fight.

**Decision.**
1. **A record for every living named person.** `NpcData.Character` holds:
   - an archetype (`town_guard`, `guard_captain`, `watch_warden`, `woodsman`, `priest`, `priest_elder`, `smith`, plus civilian archetypes);
   - an occupation, gear (body, shield, hands, carried, loadout) and a purse;
   - optionally, a full `CharacterSheetData`.
2. **Sheets by archetype.** People whose work is fighting, healing or keeping order get a sheet at generation.
   - The sheet is built through the player's own pipeline (`CharacterDraft`, `BuildSheet`, `CheckSheet`).
   - Choices come from archetype preferences, at the archetype's level.
   - Everyone else gets a lightweight record and fights with their role's profile.
3. **Promotion.** `PromoteNpcCommand` gives a sheet to someone who has none.
   - Identity, home, schedule, relationships, memories and gear are kept.
   - The tier becomes Notable and the chronicle records it.
   - Promotion happens once; it is refused while the person is in a fight.
4. **Progression.**
   - `AwardNpcExperienceCommand` is keyed, so each award counts once (the player's ledger rules).
   - `AdvanceNpcCommand` takes every earned level through the shared advancement rules.
5. **One identity in combat.** An NPC with a sheet joins an encounter as itself.
   - Hit points, death saves, effects and spells read and write that sheet through `CombatStats.SheetOf`.
   - There is no copy.
6. **Upkeep.** People with sheets take a long rest each night (`NpcSystem.RestOvernight`).

**Consequences.**
- The same validation covers the player's sheet and every NPC sheet. A damaged sheet refuses the load.
- Save size grows by about one player sheet per armed resident (19 named people, 9 sheets in the reference world).
- Civilians stay cheap until something makes them matter.

**Alternatives considered.**
- *Sheets for everyone:* the cost would be large for little benefit.
- *A separate NPC stat model:* it would fork the rules (rejected by ADR-0029).
- *Promotion by simulation:* deferred. Promotion is a command, which the DM tools use.
