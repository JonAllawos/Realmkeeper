# ADR-0020: The RPG character is a record on the existing player

**Status.** Accepted, 2026-11-10 (Phase 6).

**Context.**
- Phase 5 established one persistent player entity (ADR-0015). It owns identity, appearance,
  inventory, equipment, purse, hunger and location.
- Phase 6 adds species, background, class, ability scores, proficiencies, hit points and resources.
- A separate "character" type would duplicate identity, need synchronising, and could disagree with
  the player.

**Decision.**

1. `PlayerCharacterData` gains one field, `Rpg` (`CharacterSheetData`). It holds only what cannot be
   derived:
   - state, IDs, level and experience;
   - the ability method and base scores;
   - choices;
   - current and temporary hit points, and resource uses;
   - `StartingEquipmentGranted` and the creation date.
2. Everything derivable is computed by `CharacterRules` and never saved: modifiers, bonuses, maximum
   hit points, armour class, speed, proficiencies, languages, features, resource maximums and the
   spell DC.
3. Starting equipment goes through the existing inventory and equip rules. There is no second
   inventory.
4. `CharacterValidation` runs inside `WorldValidator`, so every command and every load checks it.

**Consequences.**
- One identity, one inventory and one save record. The Hub, shops and exploration all see the same
  player.
- Changing a rule changes every character on load, because nothing derived is stored.
- The Domain stays engine-free; the sheet is a plain, cloneable class.

**Alternatives considered.**
- A parallel `RpgCharacter` entity linked by ID: rejected. It duplicates data, causes sync bugs, and
  the Phase 6 brief forbids it.
- Storing derived values for speed: rejected. The full sheet derives in about 20 µs.
