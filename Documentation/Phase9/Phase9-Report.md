# Phase 9 report: Spellcasting, Class Features & Combat Integration

Date: 2026-11-11. Branch: `feature/phase-0-1-foundation`. Pre-flight: `Documentation/Phase9/PreFlight.md`.

**Document locations.** The brief names `Documentation/Spellcasting.md`, `ClassFeatures.md`,
`RestAndRecovery.md` and `SpellCatalogAudit.md`. Following the repository's convention, they are in
`Documentation/Systems/`, beside `Combat.md` and `CharacterProgression.md`. The ADRs are 0037–0042.

## A. Summary

**What is genuinely functional:**

- **One shared spell engine** (ADR-0037) resolves spells for the player, residents and creatures.
  It uses the Phase 8 attack roll, saving throw, damage, conditions, action economy, deterministic
  dice and log.
  - **Targeting:** creatures, points, self and areas: sphere, cube, cone, line and cylinder.
  - **Rolls:** spell attacks and saves.
  - **Scaling:** upcasting and cantrip scaling.
  - **Concentration:** with damage checks.
  - **Lasting effects:** with turn-based expiry and repeat saves.
  - **Limits:** one levelled slot per turn; metamagic (6 of 10 options).
- **Spell state** (ADR-0038).
  - **Picks:** cantrips, known spells and wizard spellbooks are character choices made at creation
    and level-up, or later from the Hub.
  - **Prepared lists** follow each class's rule and the long-rest window.
  - **Slots and Pact Magic** are kept distinct.
  - **Rules version 8.**
- **Catalog:** 47 SRD 5.2.1 spells, cantrips to level 5.
  - **Castable (37):** 27 Functional and 10 Partial.
  - **Not castable (10):** 4 DataOnly and 6 Deferred, each with the reason shown in game.
- **All twelve classes have their combat features through one activation model** (ADR-0041).
  - The catalog has 38 combat features.
  - Section D audits each class separately; several features are Partial and some are sheet-only.
- **Short and long rests** (ADR-0040) pass world time and spend hit dice. Each resource recovers
  in its own way. Arcane Recovery is included. Long rests are limited to one per 24 hours, and
  rests are idempotent.
- **NPC casters** (ADR-0042): the SRD 5.1 Acolyte and Cult Fanatic, with a deterministic caster
  AI. Residents can fight as casters without being copied.
- **UI.**
  - **Hub, Spells & rest:**
    - the spellbook with search, level and type filters, and spell details;
    - preparation, open picks, slots, rests and the rest log;
    - features with their status.
  - **Encounter panel:**
    - Spells and Class features sections, and a cast composer (slot level, targets, metamagic,
      grid aiming with an area preview);
    - effects and concentration on the initiative rows and in the inspector;
    - sections fold and stay folded.
- **Save schema v11,** with a migration from v10. Phase 5–8 saves load.
- **Tests:** .NET and EditMode 520 each, PlayMode 70 passed (10 capture sets skipped by design); 12
  mutants, all caught; 15 genuine captures. Exact numbers are in section I.

## B. Spell engine

Detail is in `Systems/Spellcasting.md`.

- **Architecture.**
  - **Domain:**
    - `Magic/SpellModel.cs`, `SpellCatalog.cs` and `SpellAccess.cs`;
    - `Combat/CombatEngine.Spells.cs`, `.Effects.cs`, `.Features.cs` and `.Attacks.cs`. The engine
      is now a partial class with the shared rolls in one place.
  - **Application:** `SpellCommands.cs`, `SpellQueries.cs` and `CombatQueries.Spells.cs`.
  - **Infrastructure:** the v11 DTOs, mapper and migration.
  - **Presentation:** `PlayerHubWindow.Spells.cs` and `CombatWindow.Spells.cs`.
- **Definitions.** Spell definitions are independent of character state. They record:
  - level, school, casting time, range and touch, and components;
  - targeting, who is affected, the maximum targets and the area;
  - resolution (attack, save with its ability, or automatic);
  - an ordered list of typed effects;
  - duration and concentration;
  - upcast and cantrip scaling;
  - class lists, status and note, and Realmkeeper's own description.
- **Effect system.** It has 22 spell effect kinds and 7 feature effect kinds. Lasting effects are
  `ActiveEffectData` in the encounter, with source, target, values, concentration link and expiry.
- **Targeting.**
  - **Validation:** chosen creatures are validated for side, range and count.
  - **Areas:** laid out on the 5-ft grid with Chebyshev distance.
    - Spheres and cylinders cover a radius around the point.
    - A cube at a point is centred on it; a cube from the caster sits next to the caster.
    - Cones have a half-angle of about 26.6°.
    - Lines are 5 ft wide.
  - **Allies** caught in an area are affected unless Sculpt Spells or Careful Spell protects them.
- **Resolution.**
  - **Attacks:** the shared `AttackRoll` (advantage and disadvantage sources, Bless and Bane,
    criticals that double the dice).
  - **Saves:** the shared `SavingThrow` (proficiency, roll modes, Aura of Protection, Bless and Bane,
    Evasion), against 8 + proficiency + ability. A success halves or negates.
  - **Damage:** the shared `ApplyDamage`.
  - **Log:** every roll is written out.
- **Concentration.** One spell per caster.
  - **Damage** forces a Constitution save: DC 10 or half the damage, whichever is higher, capped at
    30.
  - **It ends** at 0 HP, when incapacitated, or when a new concentration spell starts.
  - **Ending it** removes its effects from every target.
- **Duration.** Effects expire at the start or end of the named turns, or after rounds. Everything
  ends with the fight (ADR-0039).
- **Upcasting.** A higher slot adds the spell's own dice, targets or instances per level above.
  Cantrips scale at character levels 5, 11 and 17.

## C. Spell catalog

Detail is in `Systems/SpellCatalogAudit.md`.

| Level | Functional | Partial | DataOnly | Deferred | Total |
|---|---:|---:|---:|---:|---:|
| Cantrips | 6 | 1 | 3 | 0 | 10 |
| 1 | 12 | 4 | 1 | 2 | 19 |
| 2 | 5 | 2 | 0 | 1 | 8 |
| 3 | 2 | 2 | 0 | 3 | 7 |
| 4 | 1 | 0 | 0 | 0 | 1 |
| 5 | 1 | 1 | 0 | 0 | 2 |
| **Total** | **27** | **10** | **4** | **6** | **47** |

**By class list** (a spell can be on several lists):

| Class | Cantrips | Levelled | Total |
|---|---:|---:|---:|
| Bard | 3 | 13 | 16 |
| Cleric | 3 | 16 | 19 |
| Druid | 2 | 10 | 12 |
| Paladin | 0 | 9 | 9 |
| Ranger | 0 | 5 | 5 |
| Sorcerer | 6 | 19 | 25 |
| Warlock | 3 | 8 | 11 |
| Wizard | 6 | 19 | 25 |

**Unsupported effect types:**
- reaction casting: Shield, Hellish Rebuke, Counterspell;
- summons: Spiritual Weapon;
- moving auras: Spirit Guardians;
- resurrection: Revivify;
- durations outside fights: Mage Armour and Aid are Partial;
- light, sight, stealth and object interaction;
- spell-made difficult terrain;
- out-of-combat skill bonuses: Guidance;
- the higher-level dispel check;
- Haste's extra limited action.

No catalog spell is above level 5 yet. A level 9 caster's level 5 slots and the level 6–9 slot
resources exist, but there are no spells of those levels to put in them.

## D. Class integration (each class)

The full tables are in `Systems/ClassFeatures.md`. "Deferred" means on the sheet with its
integration need, not acting in a fight.

| Class | Spellcasting | Functional features | Partial features | Deferred | Resources | Subclass (SRD) |
|---|---|---|---|---|---|---|
| Barbarian | None | Keep raging, Reckless Attack, Danger Sense | Rage (lasts the fight; 10-min limit and Strength-check advantage not modelled) | Brutal Strike, Primal Knowledge and other non-combat features | Rage, with 1 use back on a short rest | Berserker: Frenzy Functional |
| Bard | Full, known (Cha); cantrip and known picks | Spellcasting | Bardic Inspiration (used automatically when it turns a failure; not on ability checks) | Lore features (Cutting Words is a reaction) | Slots; Inspiration (short rest from level 5) | Lore: sheet only |
| Cleric | Full, prepared (Wis) | Spellcasting, Divine Spark, Preserve Life, Disciple of Life | — | Non-combat features | Slots; Channel Divinity, with 1 back on a short rest | Life: always-prepared list, Preserve Life and Disciple of Life Functional |
| Druid | Full, prepared (Wis) | Spellcasting, Leave Wild Shape | Wild Shape (one form, the SRD wolf) | Other forms; Land features | Slots; Wild Shape, with 1 back on a short rest | Land: sheet only |
| Fighter | None | Fighting Style, Extra Attack, Second Wind, Action Surge, Improved/Superior Critical | Indomitable (automatic on the first failed save); Weapon Mastery (6 properties; no chosen-weapon limit) | Tactical Mind and other non-combat features | Second Wind, Action Surge, Indomitable | Champion: criticals Functional |
| Monk | None | Martial Arts, Flurry of Blows, Patient Defence, Step of the Wind, Stunning Strike, Wholeness of Body, Evasion | — | Deflect Attacks (a reaction), Slow Fall and others | Focus points; Wholeness of Body | Open Hand: Wholeness of Body Functional; Open Hand Technique deferred |
| Paladin | Half, prepared with one swap (Cha) | Spellcasting, Smite (with a slot), Lay on Hands, Aura of Protection | Sacred Weapon (lasts the fight) | The free Smite once per long rest; Aura of Courage and others | Slots; Lay on Hands pool; Channel Divinity | Devotion: always-prepared list and Sacred Weapon |
| Ranger | Half, prepared with one swap (Wis) | Spellcasting, Favoured Enemy free Hunter's Mark | Hunter's Mark (no re-marking; no tracking advantage) | Hunter features | Slots; Favoured Enemy uses | Hunter: sheet only |
| Rogue | None | Sneak Attack, Cunning Action (Dash, Disengage), Steady Aim, Uncanny Dodge, Evasion | — | Hide (no stealth model), Thief features | — | Thief: sheet only |
| Sorcerer | Full, known (Cha) | Spellcasting | Innate Sorcery (lasts the fight); Font of Magic (bounded by the slot maximums); Metamagic (6 of 10) | Draconic features | Slots; sorcery points; Innate Sorcery | Draconic: sheet only |
| Warlock | Pact Magic, known (Cha) | Pact Magic, Dark One's Blessing | Eldritch Invocations (Agonizing Blast and Armour of Shadows act; others listed) | Other invocations | Pact slots (short rest) | Fiend: always-prepared list and Dark One's Blessing |
| Wizard | Full, spellbook (Int) | Spellcasting, Arcane Recovery, Potent Cantrip, Sculpt Spells, Empowered Evocation | — | Non-combat features | Slots; Arcane Recovery | Evoker: three features Functional |

The SRD set has no third-caster subclass (Eldritch Knight, Arcane Trickster), so none was
invented.

## E. Rest and recovery

Detail is in `Systems/RestAndRecovery.md`.

- **Short rest.**
  - **Time:** the clock advances 60 minutes, and the world lives through them.
  - **Hit dice:** the player spends 0 or more. Each heals the class die + Constitution (at least 0),
    rolled with the deterministic dice.
  - **Resources:** short-rest resources return, and Rage, Channel Divinity and Wild Shape regain one
    use.
  - **Arcane Recovery:** once per long rest.
- **Long rest.**
  - **Time:** the clock advances 480 minutes.
  - **Needs:** at least 1 hit point, and 16 hours since the last one ended.
  - **Recovery:** full hit points, every resource, and every hit die (SRD 5.2.1; the earlier "half"
    was corrected).
  - **Preparation:** the window reopens. Clerics, druids and wizards may change anything; paladins
    and rangers swap one.
- **Spell slots** recover on a long rest. **Pact Magic slots** recover on a short rest, at the pact
  slot level only.
- **Class resources:** each declares Recovery, ShortRestFromLevel, ShortRestRegain and
  RecoveryAmount.
- **World time:**
  - rests are refused during a fight;
  - if the time cannot pass, nothing is recovered;
  - each rest is recorded with a key, and a repeated key is refused;
  - shops, NPC schedules and the simulation advance as for any wait.

## F. NPC integration

- **NPC spellcasting.**
  - **Creature blocks:** `CreatureSpellcasting` on the SRD 5.1 Acolyte (Sacred Flame, Bless, Cure
    Wounds) and Cult Fanatic (Sacred Flame, Inflict Wounds, Shield of Faith, Hold Person).
  - **Slots:** per-combatant resources, through the same engine as the player's.
  - **Residents:** fight as casters through a `CreatureId` override, keeping their identity.
- **NPC class features.**
  - Creatures have Multiattack.
  - Class features act for any combatant whose rules have them, but no NPC holds a character sheet
    yet. So in play only the player uses class features.
- **AI decisions** (deterministic, validated by the engine):
  - heal a dying or badly hurt ally;
  - bless when not concentrating;
  - otherwise damage or control, weighing the foes an area catches against the allies it would hurt.

  A slot is spent only when it beats the best cantrip or attack by half again. It is never spent on
  a foe a cantrip would finish, or to replace a concentration already running.
- **Persistence.** Creature slots, concentration and effects are saved with the encounter (v11).
- **Known limitations:**
  - two caster stat blocks only;
  - creature slots refill for each encounter;
  - no NPC holds a character sheet with class features yet;
  - the AI is a baseline, not tactical.

## G. Persistence

- **Versions.** The previous schema was v10 (Phase 8); the new one is v11. The rules version moves
  from 7 to 8.
- **Migration.** `MigrationV10ToV11` adds an empty `spellcasting` record. It sets rules version 8
  only for characters still being created; finished characters keep their version, and their spell
  picks are offered later, never invented. Encounter fields default when absent.
- **Older saves.**
  - v10, v9, v8 and v7 saves load through the migration chain.
  - The old-save fixtures (`StripToV9`, `StripToV8`, `StripToV7`) now first strip the v11 fields
    (`StripToV10`), so they stay faithful.
- **Tests.**
  - **.NET and EditMode:**
    - a Phase 8 document migrates with hit points, experience, resources, structures and the
      chronicle unchanged;
    - a fight with concentration and effects is saved and loaded with an identical world section;
    - tampered documents are refused: an effect on no one, an unknown effect kind, a wizard spell
      prepared by a cleric, a bad preparation window.
  - **PlayMode:** rest state survives save and reload in the shipped scene.

## H. Licensing

- **Rules source.** SRD 5.2.1 (CC BY 4.0) for spells, spellcasting, class features and rests.
  SRD 5.1 (CC BY 4.0) for the Acolyte and Cult Fanatic.
- **Provenance.**
  - Spell names are the SRD's. Every spell, feature and creature description is Realmkeeper's own.
  - No SRD prose was copied.
  - No protected setting content, no non-SRD spell, subclass or creature, and no D&D trademark is
    used.
- **Attribution.** Both SRD notices are unchanged. The "rules use" text now names spells,
  spellcasting, class features, rests and the SRD 5.1 casters.
- **Shipped credit locations:** `CREDITS.md`, the Hub's Info section (`Application/Credits.cs`) and
  the README. No new art or third-party assets were added.

## I. Tests

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| .NET Release (`Realmkeeper.Verification.sln`) | 520 | 0 | 0 |
| Unity EditMode (batch) | 520 | 0 | 0 |
| Unity PlayMode (batch) | 70 | 0 | 10 |

- **Baseline (pre-flight):**

  | Suite | Passed | Failed | Skipped |
  |---|---:|---:|---:|
  | .NET | 470 | 0 | 0 |
  | EditMode | 470 | 0 | 0 |
  | PlayMode | 66 | 0 | 6 |

  The PlayMode skips are the capture sets, which run only with `RK_CAPTURE_DIR`. Phase 9 adds 4
  more.
- **New tests (50):**
  - **`SpellTests` (16):**
    - the catalog;
    - area shapes, including cylinder and point cube;
    - spell attacks and saves through the shared rolls;
    - areas with separate saves;
    - upcasting and Magic Missile;
    - healing;
    - Bless and concentration replacement;
    - concentration checks;
    - Hold Person repeat saves;
    - failed casts spending nothing;
    - Pact Magic and Eldritch Blast beams;
    - Misty Step;
    - NPC casters.
  - **`FeatureTests` (13):**
    - Rage, Reckless Attack, Sneak Attack, Action Surge;
    - Second Wind and Cunning Action through the shared path;
    - Smite, Lay on Hands, Bardic Inspiration, Wild Shape;
    - Flurry of Blows and Stunning Strike;
    - Font of Magic and Metamagic;
    - Agonizing Blast, Vex, and deferred features not offered.
  - **`SpellProgressionTests` (20: 12 tests, plus 8 cases of the per-class test):**
    - every caster's picks, one case for each of the 8 spellcasting classes;
    - level-up picks and later picks;
    - pick-level limits and preparation rules;
    - short rest, long rest, refusals and Arcane Recovery;
    - healing outside a fight;
    - save and load with effects;
    - v10 migration;
    - the caster AI;
    - a resident as caster.
  - **`MagicPerformanceTests` (1).**
- **Regression tests updated** to the intended Phase 9 behaviour; none was weakened or removed:
  - `ProgressionTests`: hit dice now all return on a long rest (SRD 5.2.1), with the reason in the
    test.
  - `CombatIntegrationTests`: the schema assertion uses `CurrentSchemaVersion`.
  - `Phase2Worlds`: `StripToV10` was added and is called from `StripToV9`.
- **UI-driven tests (PlayMode, shipped scene; all passed):**
  - `Spellbook_FiltersInspectsAndPrepares`.
  - `Combat_CastsASpellAttack_AndAnAreaSpell_ThroughTheComposer`: Fire Bolt by clicking, then
    Thunderwave aimed on the grid with its 9-square preview and a slot spent; a folded section stays
    folded.
  - `Combat_ActivatesAClassFeature_FromThePanel`: Rage, with its effect on the row and in the
    inspector.
  - `Rests_FromTheHub_RecoverAndShowResources_AndStateSurvivesReload`.

  The combat tests start through a driver. A level-1 wizard can be dropped by the bandit archer's
  first shot before acting. When that happens, the driver calls the fight off and recovers the
  in-game way (a hit die, a day passing, a long rest) before trying again. The dice are untouched.
- **Fault injection (mutation testing):** 12 mutants. Each was introduced, run against the full
  .NET suite and reverted.

  | Mutant | Result |
  |---|---|
  | Slot not spent | Caught |
  | Sneak Attack not once per turn | Caught |
  | Rage resistance removed | Caught |
  | Long-rest 16-hour gap removed | Caught |
  | Short-rest regain removed | Caught |
  | AI heal threshold changed | Caught |
  | Half damage on a save | Caught after strengthening: a deterministic `RawDamage` check that loops to a success |
  | Concentration DC floor | Caught after strengthening: asserts "vs DC 10" |
  | Upcast dice | Caught after strengthening: a new `Upcasting_AddsTheSpellsOwnDice` test |
  | One slot per turn | Caught after strengthening: the Misty Step assertion |
  | Preparation limit | Caught after strengthening: asserts the "At most 4" message |
  | Pact slots at other levels | Caught after strengthening: `SlotsLeft(1) == 0` |

- **Bugs found and fixed during verification:**
  - the spell stats row in the Hub let values wrap into the next label;
  - the aim hint was clipped;
  - the encounter panel's long attack list pushed the Spells section out of view (sections now fold
    and stay folded);
  - concentration and effects did not show on the initiative rows or in the inspector.

  Each was fixed, re-tested and re-captured.
- **Compiler output.** Unity compiled with 0 errors. The one warning (CS0105, a duplicate `using` in
  `ExplorationController.Living.cs`) predates Phase 9 and was left alone.
- **Not run:**
  - a development-build frame-time probe;
  - manual play outside the automated captures;
  - Unity PlayMode on platforms other than Windows.

## J. Performance (measured)

**Conditions:**
- **Hardware:** Intel Core i9-14900HX, 31.6 GB RAM, Windows 11 Pro.
- **.NET:** .NET 8.0.427, Release.
- **Unity:** 6000.0.47f1 EditMode in batch mode.
- **Test world:** `MagicPerformanceTests`, with a level 9 wizard and 20 bandits; 200 iterations
  each.
- **Caster AI:** a fight of 3 acolytes, 3 guards, 3 cult fanatics and 3 bandits, played to its end.

| Operation | .NET | Unity EditMode |
|---|---|---|
| Catalog first load (47 spells) | 0.05 ms | 0.05 ms |
| Spell lookup | 1.40 µs | 1.12 µs |
| Catalog validation | 0.13 ms | 0.09 ms |
| Single-target cast (Fire Bolt) | 86 µs | 67 µs |
| Area cast (Fireball, 20 creatures) | 420 µs | 450 µs |
| Magic Missile, 7 darts | 139 µs | 94 µs |
| Concentration check | 48 µs | 57 µs |
| Feature activation (Rage) | 7 µs | 3 µs |
| Caster AI decision (116 decisions) | 52 µs | 27 µs |
| Short rest (with the hour of world time) | 0.7 ms | 0.9 ms |
| Long rest (with 8 hours of world time) | 0.5 ms | 0.8 ms |
| Spellbook query | 0.13 ms | 0.08 ms |
| Save / load with spellcasting data | 5.2 / 31.8 ms | 3.7 / 73.4 ms |

The .NET column is from the run during development, and the Unity column from the final EditMode
run. Single runs vary by tens of percent, so treat them as orders of magnitude.

Not measured: frame time in a development build with the encounter panel open.

## K. Screenshots (`Documentation/Screenshots/Phase9/`, genuine captures by `Phase9CaptureTests`)

| File | Shows |
|---|---|
| `p9_01_spellbook.jpg` | A wizard's spellbook: DC and attack with breakdowns, slots, open picks, preparation, the filtered list |
| `p9_02_spell_details.jpg` | Burning Hands inspected: cast time, cone, components, duration, effects, upcasting, classes, status, source |
| `p9_03_preparation_and_picks.jpg` | Open picks and the preparation editor |
| `p9_04_sheet_spellcasting_section.jpg` | A cleric's spellcasting section |
| `p9_05_slot_display.jpg` | Slots as pips after spending one in a fight |
| `p9_06_combat_spell_selection.jpg` | The encounter panel's Spells section, attacks folded |
| `p9_07_target_selection.jpg` | The cast composer for Fire Bolt with targets |
| `p9_08_spell_attack_result.jpg` | Fire Bolt resolved by the shared attack roll |
| `p9_09_saving_throw_and_area_result.jpg` | Burning Hands resolved: separate Dexterity saves, full or half damage |
| `p9_10_area_aim.jpg` | The cone previewed on the grid before casting |
| `p9_11_concentration.jpg` | Bless under concentration on the rows and in the inspector |
| `p9_12_class_feature.jpg` | Rage activated from the panel |
| `p9_13_npc_spellcasting.jpg` | The Cult Fanatic casting Hold Person on its own turn |
| `p9_14_short_rest.jpg` | After a short rest: a hit die spent, the pact slot back |
| `p9_15_long_rest.jpg` | After a long rest: restored, and the next long rest refused with the wait shown |

The capture log (`p9_capture_log.txt`) records each shot and its roll text.

**One honest caveat about p9_09 and p9_10.** The capture aims Burning Hands at the nearest foe, and
the guard ally (Neville Wade) stood in the cone. He failed his save and dropped. This is the
correct rule (areas hit allies), and it is shown as it happened.

## L. Known limitations

- **Reactions are automatic** (ADR-0033). Shield, Hellish Rebuke, Counterspell, Cutting Words,
  Deflect Attacks and the other reaction features wait for a reaction prompt.
- **Durations.** Effects end with the fight. Out-of-combat durations (Mage Armour, Aid, Rage's 10
  minutes) are not tracked. Outside fights only healing spells can be cast.
- **Catalog.**
  - **Coverage:** 47 spells; levels 6–9 have none yet.
  - **Not simulated:** summons, moving auras, light, stealth, sight, objects and spell-made terrain.
  - **No ritual casting.**
- **Class features.**
  - **Partial:** Wild Shape (one form); Metamagic (6 of 10 options); Font of Magic (bounded by the
    slot maximums).
  - **Automatic, not chosen:** Indomitable and Bardic Inspiration are used automatically rather than
    chosen.
  - **Not offered yet:** Smite's free use.
  - **Sheet only:** several subclasses' features (Lore, Land, Hunter, Thief, Draconic, parts of Open
    Hand).
- **NPCs.**
  - Two caster stat blocks, from SRD 5.1.
  - Creature slots refill per encounter.
  - No NPC holds a character sheet, so no NPC uses class features.
- **Presentation.**
  - Placeholder figures and markers.
  - No spell VFX or animations.
  - The grid is in the panel.
  - The search field is a plain text field.
- **Practice fights.** A level-1 caster can drop before acting. Practice fights are not balanced
  encounters.

## M. Git status

- **Branch:** `feature/phase-0-1-foundation`.
- **Base commit:** `2fe83eb` (Phase 8). It equals `origin/feature/phase-0-1-foundation`.
- **Phase 9 commit:** one local commit on top of `2fe83eb`, "Phase 9: spellcasting, class features
  and combat integration", carrying the trailer. Its hash is reported in the hand-off message.
- **Remote relationship:** local is 1 ahead and 0 behind origin. **Nothing was pushed.**
- **Working tree:**
  - `ProjectSettings/GraphicsSettings.asset` stays modified, as it was before Phase 9. It was not
    staged, discarded or committed.
  - Nothing else is uncommitted.
- **Scope.** Phase 9 stops here. Phase 10 has not been started.

## N. Next-phase recommendations

These are dependencies, not implemented:

1. **A reaction prompt.** It unblocks Shield, Counterspell, Hellish Rebuke and the reaction
   features, and lets the player choose Indomitable, Bardic Inspiration and Uncanny Dodge.
2. **Effect timing outside fights.** World-time expiry for Mage Armour, Aid and Rage, and casting
   utility spells in exploration.
3. **Character sheets for NPCs and companions.** The progression, spell and feature services
   already take any sheet; NPCs then get class features and real spell lists.
4. **Catalog growth** to spell levels 6–9, and a beast list for Wild Shape, each with the same
   status discipline.
5. **Summons and area triggers.** Spiritual Weapon, Spirit Guardians and spell-made terrain.
