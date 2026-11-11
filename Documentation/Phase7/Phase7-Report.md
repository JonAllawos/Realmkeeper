# Phase 7 report: Character Progression & Core Rules Engine

Date: 2026-11-11. Branch `feature/phase-0-1-foundation`. Base: `f2682a2` (Phase 6).

## 1. Executive summary

The level-1 character of Phase 6 now progresses through levels 1–20.

- **Experience** goes into a keyed ledger. The same quest, encounter or discovery never pays twice,
  and experience only makes a character eligible.
- **Level-ups** happen in one validated transaction, through a level-up window in the exploration
  scene.
- **All twelve classes** have distinct progressions taken from the SRD 5.2.1 class tables: features,
  scaling columns, resources and spell slots.
- **One SRD subclass per class**, plus the SRD feats (Ability Score Improvement, Grappler, Origin,
  Fighting Style, Epic Boons), invocations and Metamagic.
- **The shared rules engine** gained conditions, stacking, advantage and disadvantage, initiative,
  passive scores, senses and movement, prerequisites, feature replacement and breakdowns.
- **Resources and hit points** have commands: spend, restore, recover, damage, heal, temporary hit
  points, hit dice.
- **Saves:** schema v9. Phase 5 and Phase 6 saves load. Phase 6 druids and warlocks make their new
  level-1 choice at their next level-up.

| Suite | Result |
|---|---|
| .NET | 424/424 |
| Unity EditMode | 424/424 |
| Unity PlayMode | 67/67 |

PlayMode drives six classes through the real window from Phase 6 saves. Nine mutation checks were
run, and all are caught.

**Not built** (as the brief requires): combat, spells, rests that pass time, the full subclass
catalogue, companions and NPC levelling, multiclassing. **No gameplay system awards experience
yet.** The command exists and the tests use it; quests and combat will call it.

## 2. Pre-flight findings

Full record: `Phase7/PreFlight.md`.

- **The repository matched the Phase 6 report.**
  - HEAD `f2682a2`, two commits ahead of origin, not pushed.
  - The only uncommitted file is `ProjectSettings/GraphicsSettings.asset`. I re-inspected it: three
    references re-wrapped onto single lines, identical values, formatting only. It was left
    untouched and is not committed.
  - Schema v8; baselines .NET 372, EditMode 372, PlayMode 60.
- **The Bible and `ProductFoundation.md`.** The scope conflict is still an owner decision and is
  unresolved.
- **Level-1 gaps in the Phase 6 data, compared with SRD 5.2.1.**
  - Two missing choices: druid Primal Order and the warlock's level-1 invocation. These are handled
    as "outstanding" choices (§11).
  - Two missing features: rogue Weapon Mastery and wizard Ritual Adept. These were added.
- **Licensing.** I verified the SRD 5.2.1 contents online (§21).

## 3. Architecture decisions

| ADR | Decision |
|---|---|
| [0025](../Decisions/ADR-0025-progression-in-the-character-record.md) | Progression lives in the one character record: level choices under `lv{level}:` keys, the subclass in `SubclassId`, an append-only ledger and advancement history; level values derived |
| [0026](../Decisions/ADR-0026-rules-configuration-v7.md) | Rules configuration v7: SRD 5.2.1 progression and feats on the Phase 6 base (species increases kept; no background increases or origin feat at creation; fixed-average hit points; current values rise with maximums) |
| [0027](../Decisions/ADR-0027-shared-rules-engine.md) | One rules engine: conditions, encoded stacking, roll modes, typed prerequisites, `Replaces`, integration needs, breakdowns |
| [0028](../Decisions/ADR-0028-experience-ledger-and-explicit-advancement.md) | Experience ledger with idempotent keyed awards; explicit multi-level advancement; rules versions and outstanding choices; the v9 migration |
| [0029](../Decisions/ADR-0029-character-agnostic-progression.md) | Progression services take any character sheet (NPC and companion readiness) |

**Layering is unchanged and no assemblies were added:**

| Layer | What Phase 7 added |
|---|---|
| Domain (`Domain/Rpg`) | Catalog, rules, progression, advancement, vitals, validation |
| Application | Commands, the level-up draft, queries |
| Infrastructure | Schema v9 and its migration |
| Presentation | The window, the Hub and the HUD |

## 4. Experience and level progression

- **Thresholds.** The SRD 5.2.1 table (0 … 355,000), held in `ProgressionRules` as replaceable
  configuration.
- **Awards.** `AwardExperienceCommand(amount, source, sourceId, reason, key)`.
  - Sources: Quest, Encounter, Discovery, Milestone, Story, Other.
  - Each award is 1–1,000,000; the total is at most 10,000,000.
  - The key is `{source}:{sourceId}`, or explicit, or the next `award-{n}`.
  - Duplicate keys are refused. The ledger must add up to `Experience` on every load.
- **Eligibility.**
  - The HUD shows **Level up (L)**.
  - The Hub shows an experience bar and a banner.
  - The snapshot reports levels available, the next threshold and progress.
- **Advancement.** `AdvanceCharacterCommand` takes one or more levels, each with its choices, and
  validates each against the state the previous one left.
  - Choices are never overwritten, and keys that do not belong to the level are refused.
  - Hit points and resources rise by the increase in their maximums.
  - One record is added per level, and one chronicle entry per command.
  - The whole sheet and world are validated before commit.
- **Before creation.** Experience earned by legacy or pending players is kept when they create
  their character.

## 5. The twelve classes

The full table is in `Systems/Classes.md`. In brief:

| Class | Subclass | Highlights with effects now |
|---|---|---|
| Barbarian | Berserker | Rage 2→6, rage damage +2→+4; Fast Movement +10 ft; Feral Instinct (initiative advantage); Primal Champion +4 Str/Con (max 25) |
| Bard | Lore (+3 skills) | Expertise ×4; Jack of All Trades; Font of Inspiration (short-rest recovery); inspiration die d6→d12; full caster |
| Cleric | Life | Channel Divinity 2/3/4; Blessed Strikes choice; full caster |
| Druid | Land (land choice) | **Primal Order at 1** (Magician adds Wisdom to Arcana and Nature); Wild Shape 2/3/4; Elemental Fury choice |
| Fighter | Champion (Remarkable Athlete: initiative and Athletics advantage; extra Fighting Style) | Feats at 4, 6, 8, 12, 14, 16; Action Surge 1→2; Indomitable 1→3; Extra → Two → Three Extra Attacks (replacement) |
| Monk | Open Hand (Wholeness of Body resource) | Focus = level; Unarmoured Movement +10→+30 ft (no armour, no shield); Disciplined Survivor (all saves); Body and Mind |
| Paladin | Devotion | Fighting Style feat; Aura of Protection (Charisma to saves); Lay on Hands 5×level; half caster |
| Ranger | Hunter (Hunter's Prey, Defensive Tactics) | Deft Explorer (expertise and 2 languages); Roving (+10 ft, climb, swim); Feral Senses (blindsight); Favoured Enemy 2→6 |
| Rogue | Thief (climb speed) | Sneak Attack 1d6→10d6; Expertise at 6; Reliable Talent; Slippery Mind (Wis/Cha saves); feat at 10 |
| Sorcerer | Draconic (+1 HP per level, Charisma AC, element resistance) | Sorcery points = level; Metamagic 2/4/6 |
| Warlock | Fiend (Dark One's Own Luck) | **Invocation at 1**; invocations to 10 with prerequisites; pact slots 1→4 of level 1→5 |
| Wizard | Evoker | **Ritual Adept at 1**; Scholar expertise; Arcane Recovery; full caster |

A test proves the twelve progressions are distinct. Another advances every class from 1 to 20,
validating at every level.

## 6. Feature and ability advancement

- **Feature definitions** have:
  - a stable ID, kind (passive, active, limited use, scaling, choice) and level;
  - typed effects, a scaling column and `Replaces`;
  - **integration needs**: what each waits for (combat, spells, rests, conversation checks,
    exploration checks). The sheet and the window show these, so nothing pretends to work.
- **Choices by level.** Skills, expertise (with allowed lists), languages, options with
  prerequisites, pools that never repeat (invocations, Metamagic), feats (with their own choices)
  and the subclass.
- **Feats.** Categories, prerequisites (level, ability, Fighting Style feature, spellcasting),
  repeatable rules (Magic Initiate must use a different list), and fighting styles that never
  repeat.
- **Ability increases.**
  - Ability Score Improvement: +2, or +1/+1, capped at 20.
  - Grappler: +1 Strength or Dexterity.
  - Epic Boons: +1, capped at 30.
  - Capstones: +4, capped at 25.
  - Applied in the order gained. Every score shows its breakdown, and confirmed improvements cannot
    be edited.
- **The rules configuration** (ADR-0026) states which increases exist and which do not.

## 7. Hit points and resources

- **Maximum hit points.** The die's maximum at level 1, the fixed average `die / 2 + 1` after that,
  Constitution × level (retroactive), and per-level bonuses. Derived, never stored.
- **Current hit points** rise by the gain. Damage takes temporary hit points first; healing is
  capped; temporary hit points do not stack. Tested with d6, d8, d10 and d12 classes and
  Constitution from 8 to 15.
- **Hit dice** are a resource: one per level, half back on a long rest.
- **Resources.** IDs; formulas (fixed, ability modifier, per level, proficiency bonus, 20-level
  tables); first level; recovery (with Font of Inspiration's change); amount (all or half).
  Subclass resources are included. Commands spend, restore and recover; recovering passes no time.

## 8. The shared rules engine

See `Systems/RulesEngine.md`. Highlights:
- **Inputs:** any sheet, plus a loadout.
- **Collection:** level-aware, with feature replacement.
- **Conditions:** no armour, no armour or shield, no heavy armour, wearing armour.
- **Stacking:**
  - different sources add, and the same source never adds twice;
  - one unarmoured defence;
  - best-of for senses and carrying;
  - advantage and disadvantage cancel;
  - conditional advantage is situational.
- **Calculations:** initiative (Alert); passive scores (±5); skill and save ability bonuses
  (Thaumaturge, Magician, Aura of Protection); Jack of All Trades; Reliable Talent; senses;
  movement modes; Defense AC.
- **Prerequisites** with readable text.

No UI component calculates a rule.

## 9. The level-up window

`CharacterAdvancementWindow` is built on `AdvancementDraft` (engine-free, tested in .NET).
- **Overview:** level, hit points (+gain), proficiency, hit dice, new features with what they wait
  for, resources and class-table columns that change, open choices, and the choices ahead.
- **One page per choice.** Unavailable options are disabled with the reason ("needs level 5+",
  "needs the pact of the blade invocation", "would pass 20", "already taken"). A picked feat adds
  its own pages.
- **Review.**
- **Navigation:** Back and Next keep picks. Confirm is enabled only when valid. After confirming,
  the window reopens for the next level or closes onto the Hub's Progression panel.
- **The Hub** keeps eight sections, with a new Character › Progression panel (ledger, levels gained,
  levels ahead, open choices). Its other panels gained breakdowns, roll modes, senses, movement,
  class-table columns, recovery, slots and prepared spells.

## 10. NPC and companion readiness

Every domain service takes a `CharacterSheetData`:
- `CharacterRules`, `CharacterValidation.CheckSheet`, `CharacterProgression`;
- `CharacterAdvancement` (Preview, Apply, SuggestChoices), `CharacterVitals`.

Player-only checks and equipment stay outside these services. A test advances a sheet owned by
nobody (a dwarf paladin) to level 7, spends and recovers its resources, and validates it.

**Still to do:** a sheet on `NpcData`, NPC-targeted commands, and the decision of when NPCs gain
experience (ADR-0029).

## 11. Save migration results

`MigrationV8ToV9`:
- **Rules version:** 6 for created characters (their new level-1 choices stay open until the next
  level-up), 7 otherwise.
- **History:** an empty ledger and history. Any held experience becomes one `migrated:v8` entry.
- **Hit dice:** full, for created characters.
- **Warlocks:** `spell_slots_1` is renamed to `pact_slots`, keeping its uses.
- **Nothing else** changes.

| Check | Where | Result |
|---|---|---|
| Phase 6 fighter, druid and warlock saves (v8) keep ID, name, purse, pose, inventory, equipment, scores, skills, current HP and chronicle; get rules v6, hit dice 1; open choices exactly Primal Order / the invocation / none | .NET | Pass |
| Those migrated characters advance, making the open choice; nothing is left open | .NET | Pass |
| Phase 5 save (v7) loads Legacy with the v9 record and nothing granted | .NET | Pass |
| Fresh world; level-1 character load; XP award and save/load; advancement and save/load; multi-level; invalid advancement refused unchanged; duplicate XP and feature refused; resource state kept | .NET and EditMode | Pass |
| **A real save rewritten to Phase 6 v8, loaded in the shipped scene**, levelled through the window, saved and reloaded, for six classes | **Unity PlayMode** | Pass |
| A tampered ledger is refused on load | .NET | Pass |

## 12. .NET test count

**424/424**: 372 + `ProgressionTests` (51, counting test cases) + `ProgressionPerformanceTests` (1).

## 13. Unity EditMode test count

**424/424**, the full suite in batch mode. After a final assertion was added to an existing test,
the Character fixtures were run again in Unity: **78/78**.

## 14. Unity PlayMode test count

**67/67**, 0 skipped, with captures enabled: 60 earlier + 6 class tests + 1 capture fixture.

**Driven through the real UI:**
- Fighter (→5), Rogue (→6), Cleric (→3), Wizard (→4), Barbarian (→5) and Warlock (→5). Every page
  was clicked, every choice made with the window's buttons.
- The Hub banner, the HUD button, the Progression panel.

**Tested in .NET and EditMode only:**
- the other six classes' level-ups, and all twelve classes to level 20;
- feat edge cases, all validator rejections, resources and recovery, hit-point rules;
- non-player sheets.

## 15. Mutation testing results

| Mutant | Caught by |
|---|---|
| Incorrect XP threshold | `ExperienceThresholds_AreTheSrdTable` |
| Incorrect proficiency bonus | 4 tests (incl. every class 1→20) |
| Incorrect hit-point increase | 7 tests (incl. the hit-point cases) |
| Duplicate level and feature grant (same level applied again) | Initially survived at the test level (the validator refused it via the record count); a test now asserts the rule itself and catches it |
| Replaced feature still applying | `ClassMilestones_ScaleAndReplaceFeatures` |
| Invalid ability improvement (cap off) | `AbilityScoreImprovement_RaisesScores_UpToTwenty` |
| Resource maximum miscalculation | 4 tests |
| Advancement choice lost after save/load | `AdvancedCharacter_SurvivesSaveAndLoadExactly` and `Progression_IsCheap` |
| Duplicate XP award | `Experience_IsAwardedThroughTheLedger…` |

All were reverted; I verified the source was clean before committing.

## 16. Performance measurements

On a level-20 warlock, .NET / Unity EditMode:

| Operation | Time |
|---|---|
| Rules evaluation | 57 µs / 52 µs |
| Full sheet | 158 µs / 396 µs |
| Validation of all 20 levels | 0.70 ms / 1.16 ms |
| Suggestion and preview of a choice-heavy level | 0.25 ms / 0.43 ms |
| XP award command | 0.19 ms / 0.41 ms |
| Level-up command | 0.86 ms / 1.10 ms |
| Save / load | 3.0 / 60 ms; 3.8 / 138 ms |

Award and level-up times include whole-world validation. A development-build frame probe of the new
window was **not** run (§18).

## 17. Screenshots

These are from the shipped scene: a Phase 6 tiefling warlock (a save rewritten to v8). They are in
`Documentation/Screenshots/Phase7/`, with captions in `p7_capture_log.txt`.

| File | Shows |
|---|---|
| `p7_01_hud_level_up.jpg` | 2,800 XP from a quest and a discovery; the HUD offers Level up (L); still level 1 |
| `p7_02_hub_eligible.jpg` | The Hub sheet: experience bar and level-up banner |
| `p7_03_level2_overview.jpg` | Level 2 overview: hit points, features, resources, the open level-1 choice |
| `p7_04_level2_open_invocation.jpg` | The level-1 invocation Phase 6 left open |
| `p7_05_level2_invocations.jpg` | Level-2 invocations with reasons for unavailable ones |
| `p7_06_level2_review.jpg` | Review |
| `p7_07_level3_subclass.jpg` | The subclass page (Fiend Patron, features by level) |
| `p7_08_level4_feat.jpg` | Feat choice with categories and prerequisites |
| `p7_09_level4_ability_points.jpg` | Ability points (Charisma 17 → 19, cap 20) |
| `p7_10_level4_review.jpg` | Level 4 review |
| `p7_11_hub_progression.jpg` | Progression panel: levels gained, ledger, levels ahead |
| `p7_12_hub_features.jpg` | Features with what they wait for; class table; resources |
| `p7_13_hub_sheet.jpg` | The sheet at level 4 |

**Fixed after reviewing the first captures:**
- long names overlapped their descriptions in key/value rows (now they wrap);
- a tall choice page squeezed the header and steps bar;
- every option said "full", which hid the real reasons (now prerequisite reasons come first, and
  "full" shows no line);
- level-up summaries repeated themselves;
- subclass levels sorted as text;
- the hit-point card overflowed.

## 18. Known limitations

| Area | Limitation |
|---|---|
| Experience sources | No gameplay system awards experience yet; quests and combat will call the command. PlayMode used the command directly. |
| Subclasses | One per class (the SRD's). Further subclasses are deferred, not invented. |
| Combat, spells, rests | Features are acquired, validated and shown with what they wait for. Attacks, spells, concentration and rests that pass time do not exist. Hit dice cannot yet be spent to heal. |
| Invocations | Agonizing Blast, Eldritch Spear and Repelling Blast (need damage cantrips) and Lessons of the First Ones (nested feat) are deferred. Swapping invocations on level-up is not supported. |
| Feats | Skilled offers skills only (tools deferred); Magic Initiate's spells await the magic phase |
| Rules configuration | A documented hybrid: SRD 5.1-style species increases with SRD 5.2.1 progression; no background increases or origin feat at creation |
| Hit points | Fixed averages only (no rolling option) |
| Multiclassing | Not supported |
| Armour class | Still provisional (no armour categories; "heavy armour" conditions always pass, since the catalog has no heavy armour) |
| Rest-recovery details | One use of rage, Channel Divinity or Wild Shape returning on a short rest is described, not modelled. Those resources recover on long rests only. |
| NPCs and companions | The services are ready; no NPC holds a sheet |
| Performance | No development-build frame probe of the level-up window was run |
| Species art, scope question | Unchanged from Phase 6 |

## 19. Files changed

**New, runtime:**
- `Domain/Rpg/`: `RpgCatalog.Progression.cs`, `RpgCatalog.Feats.cs`, `CharacterProgression.cs`,
  `CharacterAdvancement.cs`, `CharacterVitals.cs`.
- `Application/`: `ProgressionCommands.cs`, `AdvancementDraft.cs`.
- `Exploration/Presentation/CharacterAdvancementWindow.cs`.

**New, tests:**
- `EditMode/Character/ProgressionTests.cs` and `ProgressionPerformanceTests.cs`.
- `PlayMode/Phase7PlayModeTests.cs` and `Phase7CaptureTests.cs`.

**Modified, runtime:**
- Domain:
  - `RpgCore` (effect kinds, conditions, progression rules);
  - `RpgDefinitions` (progression, subclass, feat, prerequisite and scaling definitions);
  - `RpgCatalog.Classes` (level-1 additions; Thaumaturge and Defense typed);
  - `RpgCatalog` (validation);
  - `CharacterSheet` (rules version, ledger, history, `Loadout`);
  - `CharacterRules` (rewritten, level-aware; the Phase 6 API is kept);
  - `CharacterValidation` (any sheet, every level);
  - `WorldData` (schema 9);
  - `WorldEntities` (`PlayerLevelAdvanced`).
- Application:
  - `CharacterCommands` (experience kept at creation);
  - `CharacterQueries` (progression);
  - `PlayerQueries` (loadout-aware speed);
  - `EventFormatter`, `WorldSession`.
- Infrastructure: `SaveFileDtos`, `WorldSaveMapper.Character`, `SaveMigrations` (`MigrationV8ToV9`),
  `JsonWorldSerializer`.
- Presentation: `ExplorationController.Living` (window, HUD button, L key), `PlayerHubWindow`
  (Progression panel, sheet), `ExplorationHud.uss`.

**Modified, tests:**
- `CharacterTests` (the schema expectation now uses the current version).

**Documentation:**
- New:
  - `Phase7/PreFlight.md`, this report;
  - `Systems/CharacterProgression.md`, `Systems/RulesEngine.md`;
  - ADR-0025…0029;
  - `Screenshots/Phase7/`.
- Updated:
  - `Product/GameDesignBible.md` (status), `Roadmap.md`, `Testing/TestResults.md`;
  - `Systems/Persistence.md`, `Systems/Characters.md`, `Systems/Classes.md`,
    `Systems/CharacterCreation.md`;
  - `Decisions/README.md`.

## 20. ADRs created or updated

Created ADR-0025, 0026, 0027, 0028 and 0029. They extend ADR-0021, 0022 and 0023, which remain in
force.

## 21. Licensing and attribution status

- **Verified for this phase.** SRD 5.2.1 (Wizards of the Coast LLC, CC BY 4.0) contains the material
  used here, checked against a published copy of its text:
  - the Character Advancement table;
  - all twelve class tables to level 20;
  - one subclass per class;
  - the Origin, General, Fighting Style and Epic Boon feats;
  - eldritch invocations with prerequisites;
  - Metamagic.
- **Wording.** Realmkeeper's own. Every class, subclass and feat records its source. The Phase 6
  attribution statement (in `Systems/Characters.md`, required in the shipped credits) covers this
  content, and no further attribution is needed.
- **Content by origin:**

  | Origin | What |
  |---|---|
  | SRD-derived | Everything listed above |
  | Realmkeeper original | Earlier decisions only (Aasimar, eight backgrounds); nothing new in Phase 7 |
  | Placeholder | Species art (unchanged) |
  | Deferred | Further subclasses, four invocations, Skilled's tools, spells |

- **No proprietary or unlicensed content or assets** were added.

## 22. Git commit and branch

Branch `feature/phase-0-1-foundation`. This report is part of the commit "Phase 7: Character
Progression & Core Rules Engine", directly on top of `f2682a2`. `git log -1` gives its hash, which
is also reported in the chat summary.

## 23. Working-tree status

After the commit, only `ProjectSettings/GraphicsSettings.asset` remains modified. It is the
formatting-only change, not committed and not discarded.

## 24. Whether anything was pushed

**Nothing was pushed.** The branch is three commits ahead of origin (Phases 5, 6 and 7).

---

Phase 7 stops here. Phase 8 has not been started and waits for review.
