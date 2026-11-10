# ADR-0015: The player is one persistent domain entity, extended rather than replaced

**Status.** Accepted, 2026-11-10 (Phase 5).

**Context.**
- Phase 3 introduced `PlayerCharacterData`: an ID, a name, a location and a pose.
- Phase 5 needs a full character: identity, looks, money, belongings and condition, editable by
  the player and shown in a hub.
- A separate "player profile" object, or presentation-side state, would duplicate identity and
  could drift from world data.

**Decision.**

1. **One entity.** `PlayerCharacterData` remains the single source. It gains:
   - species, age, background and description;
   - an appearance;
   - a purse and a fed level;
   - an inventory and equipment;
   - an `IsOutfitted` flag.

   All of it is saved in schema v7.
2. **Commands only.** Every change goes through a validated, atomic command:
   - profile and appearance;
   - equip, unequip, split, combine, discard, use;
   - buy and sell.

   The Hub and the 3D preview only read snapshots (`GetPlayerHub`).
3. **Appearance is limited to what the art can render** (`PlayerAppearanceOptions`). The validator
   enforces the same limits, so a save cannot hold a look the game cannot draw.
4. **Justified defaults, given once.**
   - New worlds and pre-v7 saves get the Phase 4 body's clothes, a knife, bread, 150 coin and fed 80.
   - Species is "Human" (the only bodies the art has), age 28, background "Wanderer".
   - `IsOutfitted` prevents a second grant.
5. **Condition has effects.** Hunger (−2 per hour) and carried weight slow the player through
   `PlayerMovement.SpeedFactor`. There is no new stat system.

**Consequences.**
- The player's state is saved, loaded, migrated and validated like everything else.
- Tests can drive the Hub's buttons and assert on world data.
- Adding fields later is a schema change with a migration. This is deliberate.

**Alternatives considered.**
- *A separate profile file.* Rejected: two sources of identity, and it would not travel with saves.
- *Free-form appearance values.* Rejected: they would allow looks the art cannot show.
