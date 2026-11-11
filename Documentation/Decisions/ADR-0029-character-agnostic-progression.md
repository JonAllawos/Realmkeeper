# ADR-0029: Progression services work on any character sheet

**Status.** Accepted, 2026-11-11 (Phase 7).

**Context.**
- The Bible plans companions, named NPCs, hostile characters and faction leaders who use the same
  rules.
- Phase 7 builds progression for the player only, but it must not lock the rules to the player.

**Decision.**
1. **The domain services take a `CharacterSheetData`, never `PlayerCharacterData`:**
   - `CharacterRules`;
   - `CharacterValidation.CheckSheet`;
   - `CharacterProgression`;
   - `CharacterAdvancement` (Preview, Apply, SuggestChoices);
   - `CharacterVitals`.
2. **Player-only facts are separate.** The displayed species name, the look and the starting kit
   are checked in `CharacterValidation.Check(player)`. Equipment reaches the rules as a `Loadout`.
3. **The player's commands are thin wrappers.** A future NPC or companion command will wrap the
   same services for its own sheet.
4. **`SuggestChoices`** gives deterministic picks for characters nobody steers.

**Consequences.**
- A test advances a sheet owned by nobody to level 7, spends and recovers its resources, and
  validates it, through these services alone.
- **Still to do when companions arrive:**
  - give `NpcData` a sheet;
  - add NPC-targeted commands;
  - decide when such characters gain experience.

**Alternatives considered.**
- Building NPC levelling now: rejected, because it is out of scope for Phase 7.
