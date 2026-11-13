# ADR-0109: A level 1 baseline at creation, and a player-driven level-up on the explicit advancement

**Status.** Accepted, 2026-11-13 (Phase 22). It extends ADR-0025 and ADR-0028, and changes nothing in either.

**Context.**
- **What already held.** `CreateCharacterCommand` built level 1 sheets, and `CharacterValidation` refused:
  - a level above the experience;
  - an early subclass;
  - a choice key from a level not yet reached.
- **What was not proved.** There was no check against the level 1 table, and no test over every class, species, lineage and background.
- **Experience before creation.** A pending player could earn experience, and creation kept it, so a new character could be eligible for level 2 or higher at once.
- **The level-up was easy to miss.**
  - Experience only showed a small "Level up" button.
  - Closing the level-up window threw its picks away.
  - Nothing recorded that the player had been told.
- **The tiefling.**
  - The legacy spells at levels 3 and 5 were not in the data at all.
  - Its spellcasting ability was fixed at Charisma (ADR-0103), where SRD 5.2.1 offers Intelligence, Wisdom or Charisma.

**Decision.**
1. **A level 1 baseline (`Domain/Rpg/LevelOneBaseline`, engine-free).** It recomputes level 1 from the class table and the sheet's own picks. Every new player character must pass it before creation commits.
   - **Level and history:** level 1, no advancement records, no subclass, no level-up in progress, current rules.
   - **Experience:** exactly what was carried in; 0 in every new game.
   - **Proficiency and hit points:** proficiency +2; maximum HP = the hit die's maximum + Constitution + per-level bonuses; HP full.
   - **Features and choices:** only level 1 features; no later-level choice key.
   - **Resources and slots:** every resource full at its level 1 maximum, with spell slots exactly the level 1 row (pact slots 1, no 2nd-level slot).
   - **Spells:** cantrip picks are cantrips; every spell known, in the book or prepared is level 0 or 1, within the level 1 counts.
   - **Kit:** given once.
2. **No experience before creation (owner decision D4, 2026-11-13).**
   - `AwardExperienceCommand` and encounter awards pay only a created character. Quest and discovery awards already did.
   - An older save that already holds pre-creation experience keeps it. Nothing is deleted, and that character is still created at level 1 and may advance through the level-up.
3. **Species traits can arrive later.** `FeatureDefinition.MinLevel` (default 1) gates species traits the way the class table gates class features:
   - a later trait is shown locked, with its level, and never applies early;
   - `CharacterRules.LockedFeatures` lists everything still to come, by level: class, subclass and species.
4. **The tiefling (rules 10).**
   - **New traits:** Otherworldly Presence (Thaumaturgy) and the Legacy Spells at levels 3 and 5. They are marked "not yet played", because the engine has no Thaumaturgy effects and no once-per-long-rest innate casting path yet.
   - **New choice:** `species:legacy_ability`, introduced in rules 10, chooses the legacy's spellcasting ability.
   - **Characters made before rules 10** keep Charisma until they make the choice at their next level-up, through the existing outstanding-choice path (ADR-0028 §3).
5. **The level-up stays atomic (ADR-0028).** A level is applied whole, with all its choices, in one transaction, so the validator can still check every level. Two things are added on the sheet (schema v24):
   - **`PendingAdvancement`:** the next level's picks so far.
     - It is kept by `SaveAdvancementDraftCommand`, whenever the window closes without confirming.
     - It is validated: only the next level, which the experience allows; only that level's choices (or earlier open ones); no more picks than allowed; real options only; a subclass only at its level and of the class.
     - It applies nothing. The window resumes from it, and confirming the level consumes it.
   - **`LevelUpNoticed`:** the highest level the player has been told about.
     - `AcknowledgeLevelUpCommand` sets it when the notice is shown, and writes `PlayerLevelReady` to the chronicle.
     - So the notice appears once per level and is not repeated after loading.
6. **Automatic and chosen benefits are separate in the UI, not in the data.**
   - The overview lists what the level gives by itself: hit points, proficiency, features, resources and class-table columns. Separately, it lists the choices, each as chosen, optional or still to choose, and what stays locked.
   - Automatic benefits apply when the level is confirmed, together with the choices.
   - Nothing is ever chosen for the player. `CharacterAdvancement.SuggestChoices` remains only for people nobody steers (NPCs) and for tests.
7. **The HUD.** The level-up button turns bronze while a level waits, with its open choices, and is never hidden while a level is available. The one-time notice shows the levels reached, what the next level brings, how many choices it asks, and that nothing is taken until the player confirms.

**Consequences.**
- **Creation:** no new character can start above level 1 or with anything from a later level. The matrix test holds this over every class with every species and lineage option, and every background with every class.
- **Level-ups:** picks survive closing and saving; duplicates are refused; a corrupted draft is refused on load.
- **Saves:** schema v24 is structural. The v23 → v24 migration also moves a not-yet-created player to rules 10, as a new world's is.
- **Not changed:** the Phase 7 ledger, the advancement records and NPC advancement.

**Alternatives considered.**
- **Apply automatic benefits at once and leave choices pending.** Rejected: a level 3 character without its required subclass is a state the validator cannot check, and later systems would see half a level.
- **Discard pre-creation experience on load.** Rejected: loading must never take away what a save holds (ADR-0028), so D4 is applied to new awards only.
- **Default the tiefling's ability silently.** Rejected: the owner's brief forbids substituting a default for a real choice. Older characters keep their existing Charisma until they choose.
