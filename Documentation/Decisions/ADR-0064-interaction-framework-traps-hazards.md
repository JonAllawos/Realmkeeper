# ADR-0064: One interaction framework for places; traps and hazards through the rules; fights among real walls

**Status.** Accepted, 2026-11-11 (Phase 13).

**Context.**
- Phase 12 had two special-case interactions (the granary ledger, the notice board).
- Places need doors, locks, keys, searches, documents, clues, containers, mechanisms, traps, hazards and captives.
- Every action must be validated, transactional and checked through the existing rules engine.
- Fights in places must use the one combat engine, but walls must matter.

**Decision.**
1. **Targets and actions.** Everything in a place is a target: a feature, a passage (door), the room itself, or an inhabitant group. `PlaceActions.Options` lists the actions its kind and state allow, each with the reason when it cannot be done now:
   - inspect, search, open, close, unlock, lock, pick, force, disarm, read, listen, activate, free, attack.

   One typed command (`PlaceActionCommand`) applies the chosen one and re-checks everything. Stairs, the way out, entering and moving have their own commands.
2. **Checks.** Checks use `CharacterRules` (skill and save bonuses, darkvision) and the world's dice, keyed by place, target, visit and attempt.
   - A failed check does not lock anything away for ever. Three attempts per visit; then another visit, or another approach (forcing instead of picking, a light in the dark, another skill), allows more.
   - Recording a failed attempt is the only change a failure makes.
3. **Darkness.** An unlit dark room imposes disadvantage on Investigation and −5 on passive Perception, unless the player carries a light (an item tagged `light`) or has darkvision.
4. **Detection.** On entering a room, passive Perception finds hidden features, traps and secret ways whose DC it meets. Searching (Investigation) rolls once against everything hidden in the room.
5. **Traps (`TrapCatalog`).** Six kinds: dart plate, tripwire and bells, loose roof, spiked pit, poisoned needle, warding glyph. Each has:
   - a detection DC;
   - a disarm skill, DC and tool;
   - a spring-on-fail margin;
   - a save, damage and condition;
   - an alarm;
   - reset behaviour;
   - salvage.

   Stepping on an armed trap's cell springs it. Fairness rules: detection DCs 11–14, no damage beyond 16 points of dice, and outside a fight a trap or hazard never takes the last hit point.
6. **Hazards (`HazardCatalog`).** Foul air, darkness, unstable ground, deep water and fire are met once per visit to a room, through a save (or an Athletics check for water).
7. **Conditions.** A trap or hazard's condition (Poisoned) is reported but not kept on the sheet: there are no lasting conditions outside fights yet. This is a known limitation.
8. **Fights.** `PlaceEncounters.Start` builds the encounter:
   - the player;
   - followers and anyone held in the room, on the player's side (protect);
   - the group's members as lasting individuals: creature instances keep their wounds, and people are themselves.

   It passes through `CombatCommandHandlers.StartGenerated`. Every cell around the room that is not floor (and every closed door) is a blocked square.
9. **Walls in the engine.** The combat engine refuses movement into a blocked square. Its AI routes around walls breadth-first. The combat window draws walls.
10. **Aftermath.** `WorldEncounters.Resolve` keeps its own aftermath (experience keyed once, loot filled once) and calls `PlaceEncounters.Resolve`:
    - groups won against are defeated for good, and what they guarded opens;
    - survivors keep their wounds;
    - the loot pile lies in the room;
    - a protected person who lived counts for Protect objectives;
    - one who died is lost.

**Consequences.**
- New kinds of object are data (`FeatureCatalog`) plus, at most, a new action.
- Ranged line of sight through walls is not modelled. Walls stop movement, not missiles. This is a known limitation.

**Alternatives considered.**
- *Interaction logic in MonoBehaviours:* not transactional, not testable.
- *A separate dungeon combat mode:* a second engine.
- *Instant-death traps:* unfair, and against the brief.
