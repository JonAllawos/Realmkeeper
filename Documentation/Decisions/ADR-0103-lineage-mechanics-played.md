# ADR-0103: Lineage benefits played through the existing systems

**Status.** Accepted, 2026-11-12 (Phase 20A).

**Context.** Phase 20 (ADR-0100) made the SRD 5.2.1 lineages choices, but only their resistances and tool proficiencies took effect. The goliath's six giant-ancestry boons and every innate spell were recorded and flagged "not yet played". The engines already had the paths these need: features used after a hit (Stunning Strike), damage reactions (Uncanny Dodge, Deflect Attacks), reactions to being hurt (Hellish Rebuke), teleporting (Misty Step) and the spell pipeline.

**Decision.**
1. **Goliath (all six, SRD 5.2.1).** Each use spends one of the ancestry's uses (`stones_endurance`, proficiency bonus per long rest).
   - **Fire's Burn and Frost's Chill:** `FeatureActivation.AfterHit` on the last creature hit, once per hit (`TurnResources.LastHitBoonUsed`). They deal 1d10 fire or 1d6 cold, doubled on a critical, and Frost's Chill slows by 10 ft until the goliath's next turn.
   - **Hill's Tumble:** the same after-hit path. The target falls prone; Huge creatures and targets already prone are refused.
   - **Cloud's Jaunt:** a bonus action to an unoccupied square within 30 ft in clear line (`FeatureTargeting.Point`, a new `UseFeatureAction.Point`; the combat window asks for a square on the grid).
   - **Stone's Endurance:** a reaction in `ReactToDamage`, reducing the damage by 1d12 + Constitution.
   - **Storm's Thunder:** a reaction in `ReactToBeingHurt`, dealing 1d8 thunder to an attacker within 60 ft.
   - `ReactionKind` gains `StonesEndurance` (10) and `StormsThunder` (11). The computer braces when the blow is 6 or more, or it is below half its hit points.
   - Spell attack hits also count as hits.
2. **Tiefling legacy cantrips.** `CharacterRules.InnateSpells` lists the catalog spells a species knows. `CombatEngine.SpellsOf` adds them after the class cantrips. `InnateAbility` gives the ability when the class does not already ready the same spell; the attack bonus and save DC are then proficiency + that ability. A tiefling fighter casts Fire Bolt, Poison Spray or Chill Touch; a tiefling wizard's own Fire Bolt stays an Intelligence spell.
3. **Charisma.** SRD 5.2.1 lets the player choose Intelligence, Wisdom or Charisma for legacy spells. The catalog has no such choice yet. Adding one would put a new required choice on every saved tiefling, so Charisma is used: the tiefling's own ability bonus here. The choice is deferred, not silently decided.
4. **Chill Touch** (SRD 5.2.1) is added: a touch melee spell attack for 1d10 necrotic, with a new `CombatEffectKind.NoHealing` (22) until the caster's next turn ends. `CombatEngine.Heal` honours it.
   - It is `SpeciesOnly`, on no class list yet. Adding it to the sorcerer, warlock and wizard lists would widen their cantrip menus and change which cantrips newly generated casters pick. That should be a deliberate rules version.
   - The catalog validator accepts a species-only spell only if some species knows it.
5. **What stays "not yet played", with the missing system named.**
   - **Gnome:** Minor Illusion and Speak with Animals (forest), and Mending (rock). There is no illusion, animal-speech or object-repair system, and nothing is faked.
   - **Tiefling:** the level 3 and level 5 legacy spells. They are not in the catalog, and they need a once-per-long-rest innate casting path.
   - **Dragonborn:** the breath weapon.
6. **One classification** (`BenefitStatus`): every trait and option is **active** (always in effect), **conditional** (played in its situation, which is named) or **not yet played** (with the missing system named). The creator, the sheet and the tests all use it.
7. **Played keys are listed explicitly** (`CharacterRules.IsPlayedCombatRule` and `IsPlayedInnateMagic`), each covered by a test. A key not on the list stays flagged.

**Consequences.**
- Goliath and tiefling characters, the player's and generated people's alike, now use these in fights.
- Generated goliaths, who received the stone ancestry from the Phase 20 migration, brace against blows. Every existing test still passes.
- `HasFeature` now also matches a chosen option (`"species:giant_ancestry:fire"`). No earlier feature ID contains a colon, so nothing else matches differently.

**Alternatives.**
- A separate innate-magic engine, rejected: the spell pipeline already resolves attacks, saves and effects.
- Treating Cloud's Jaunt as a pseudo-spell, rejected: it is not a spell. It cannot be counterspelled and costs no slot.
