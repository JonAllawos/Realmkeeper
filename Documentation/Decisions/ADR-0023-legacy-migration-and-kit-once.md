# ADR-0023: Older players become Legacy; the starting kit is granted exactly once

**Status.** Accepted, 2026-11-10 (Phase 6).

**Context.**
- Schema v7 saves have a player with a name, look, belongings and location, but no species, class or
  scores.
- Inventing a class or backstory would change someone's character without consent.
- Forcing a blocking creator would interrupt play and break existing tests.
- Starting equipment must never be duplicated, whether by reloading, migrating or re-running
  creation.

**Decision.**

1. Schema v8. `MigrationV7ToV8` adds `player.rpg = { state: "Legacy", level: 1, experience: 0 }` and
   nothing else. New worlds start `Pending`.
2. Legacy and Pending characters play normally. Creation is offered (HUD button, C key, Hub banner)
   and never forced.
3. `CreateCharacterCommand` is one copy-validate-commit transaction. Either everything is applied or
   nothing is:
   - identity and appearance;
   - the sheet;
   - the kit, through the inventory rules;
   - coin;
   - a chronicle entry.
4. The command is refused when the state is already `Complete`. `StartingEquipmentGranted` is
   validated against the state: a complete character must have it set, and any other character must
   not.
5. Loading never grants anything. The Phase 5 "clothes on your back" kit is kept, and the class and
   background kit is added on top.

**Consequences.**
- A v7 save keeps its ID, name, look, inventory, equipment, purse, location and pose. This is tested
  in .NET and in PlayMode.
- The kit cannot be duplicated. This is mutation-tested.

**Alternatives considered.**
- Giving old saves a default class automatically: rejected, because it invents a character.
- Removing the Phase 5 kit at creation: rejected, because it destroys owned items.
