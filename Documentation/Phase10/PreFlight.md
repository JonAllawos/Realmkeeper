# Phase 10 pre-flight: Bestiary, NPC Character Systems, Encounter Intelligence & Living Settlement Generation

Date: 2026-11-11. Written before any Phase 10 code change.

## 1. Repository baseline

| Item | Value |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| HEAD | `27a5261` (Phase 9). Its parent is `2fe83eb` (Phase 8). |
| origin/feature/phase-0-1-foundation | `2fe83eb` (fetched). Local is **1 ahead, 0 behind**; Phase 9 is local only. |
| Working tree | Only `ProjectSettings/GraphicsSettings.asset` is modified (3 insertions, 6 deletions). This is the owner's Unity change: preserved, never staged, reverted or committed. Nothing untracked. |
| Unity Editor | Not running, so batch test runs are safe. |
| Save schema | v11 (`WorldData.CurrentSchemaVersion`). Phase 10 moves to **v12**. |
| Rules version | 8 (`ProgressionRules.CurrentRulesVersion`; spell catalog `RulesVersion = 8`) |
| Layout generator | `LocalEnvironmentGenerator.Version = 1` (derived, never saved) |
| Test baseline (the Phase 9 final runs on this exact code) | .NET 520/0/0 · Unity EditMode 520/0/0 · Unity PlayMode 70 passed, 0 failed, 10 skipped (the capture sets, which run only with `RK_CAPTURE_DIR`) |
| Compiler | 0 errors. One pre-existing warning: CS0105, a duplicate `using` in `ExplorationController.Living.cs`. Phase 10 will remove it (the brief asks for no unexplained warnings). |

## 2. Documentation read

- **Product:** the Bible's status section, ProductFoundation and the Roadmap.
- **Phase reports and pre-flights:** Phases 5–9.
- **ADRs:** 0001–0042.
- **Systems docs:** Characters, CharacterProgression, RulesEngine, Combat, Spellcasting,
  ClassFeatures, RestAndRecovery, NPCs, SettlementStructures, WorldGeneration, Exploration and
  Persistence.

The brief names `Documentation/Product/Roadmap.md`. The Roadmap is at `Documentation/Roadmap.md`.

## 3. Existing architecture (verified in code)

- **Assemblies.** Domain, Simulation, Application and Infrastructure are engine-free. Exploration has
  Domain, Application, Interaction, WorldStreaming and Presentation. There is also a Presentation
  assembly for the strategic map and HUD, and a Hosting assembly. The .NET harness compiles every
  engine-free assembly, **including Exploration.Domain**.
- **One world.** `WorldData` is the single authority. Commands copy, validate and commit through
  `CommandContext.Edit()` and `WorldValidator`.
- **The region map is not saved.** `RegionMap` holds elevation, terrain types, roads and one
  carved river. It is regenerated from the seed on every load (`IWorldGenerator.RegenerateRegion`)
  and validated against the saved settlements.
- **Characters.** `CharacterSheetData` with `CharacterRules` (derived values),
  `CharacterValidation`, `CharacterAdvancement`, the XP ledger, `CharacterVitals`, `CharacterRest`
  and `SpellAccess`. ADR-0029 says these services take any sheet. **In practice only the player
  owns one**: `world.Player.Rpg`.
- **Combat.** `CombatEngine` (partial classes) and `CombatStats.For(world, combatant)`.
  - **The player:** stats come from the player's sheet, with the loadout from the player's
    equipment slots.
  - **Everyone else:** a `CreatureDefinition` is used. Residents take the profile named by
    `CreatureCatalog.ProfileFor(role)` (guard or commoner, or an override such as "acolyte").
  - **Player-only paths:** about 40 code paths assume the sheet is the player's:
    `Kind == Player`, `_world.Player.Rpg`, and `Access(c)`, which returns null for non-players.
  - **Reactions are automatic and inline:** opportunity attacks in `DoMove`, Uncanny Dodge in
    `ResolveAttack`, Indomitable in `SavingThrow`, and Bardic Inspiration in `SpendInspiration`.
    Shield, Counterspell, Hellish Rebuke, Cutting Words and Deflect Attacks are Deferred.
- **Encounters.** One active encounter at a time. The practice fight
  (`StartPracticeEncounterCommand`) is the only way in during play. Encounters persist whole: dice
  counter, action sequence, effects. Combatants reference persistent identities (ADR-0031).

## 4. Creature and NPC capabilities today

- **Creatures:** six definitions.
  - **SRD 5.2.1:** commoner, bandit, guard, wolf.
  - **SRD 5.1:** acolyte, cult fanatic.
  - **Model:** AC, speed, scores, attacks (with an on-hit condition), multiattack, pack tactics,
    condition immunities and a spellcasting block.
  - **Missing:** creature types, sizes, senses, damage defences beyond conditions, traits,
    reactions, behaviour profiles, habitats, loot and XP. There are also no persistent creature
    instances: every creature combatant is anonymous and exists only in its encounter.
- **NPCs** (`NpcData`):
  - **Record:** role (Resident, Elder, Merchant, Farmer, Guard, Smith, Innkeeper, Labourer), tier
    (Notable, daily; Resident, weekly), personality, skills, condition, goals, memories,
    knowledge, relations, schedule, residence, workplace, deceased state and player regard.
  - **Not held:** a character sheet, equipment or class.
  - **Tier 3:** the population minus the named residents, simulated in aggregate.
- **Wildlife:** none. No populations, livestock, habitats or hunting.

## 5. Settlement generation today, and its limits

- **Local environment** (`Exploration/Domain/LocalEnvironmentGenerator`, version 1). One
  plaza-centred plan, scaled by settlement type:
  - a north–south street from the south gate and one east–west street;
  - a hall, granary and landmark tower around the plaza;
  - 2 (hamlet), 7 (village) or 10 (town) houses on lots along the two streets;
  - four stalls, a well, lamps, ten trees, and a square wall or fence.

  The layout is derived on every entry and never saved.
- **Flat world.** The ground is a flat plane at height 0, and nothing responds to elevation.
- **No settlement features:** no water, bridges, districts, fields, pastures, gardens or
  livestock, and no road network beyond the two streets.
- **Routes** (`LocalPaths.Route`) project onto the nearest of the two streets and round the plaza
  corner. A real network needs graph routing.
- **The strategic map** builds each settlement's mesh with its own unrelated layout
  (`Presentation/World/SettlementBuilder`). The two views of one settlement do not match, which is
  exactly what the brief forbids.
- **Site keys are the contract between world data and the layout:** `hall`, `granary`,
  `landmark`, `plaza`, `gate` and `house-N`.
  - World `LocationData.LayoutKey`, the shops and the Phase 8 structures refer to them.
  - The house count per type is part of that contract (`SettlementSites.HouseCount`).
  - **Any new layout must keep these keys** so that locations, shops, schedules and structures stay
    valid.
- **The three settlements of the default world (seed 123456789).** Bramhaven is a town, Mossholm
  a village; the third is a hamlet. Each is placed by the region generator on the region map,
  which also carves one river.

## 6. Reusable assets and systems

- **Art:**
  - **Kit:** the Quaternius CC0 modular kit (walls, roofs, doors, windows, kerbs, props),
    assembled by `BuildingKitPlanner` and `ModularEnvironmentBuilder`.
  - **Materials:** ambientCG CC0 (grass, ground, paving, planks, wood).
  - **Meshes:** `MetreMeshes` (planes, discs) and `MeshMerger` (batches the static pieces per
    cell and material).
  - **Characters:** humanoid CC0 characters.
- **The player controller:** a CharacterController with first- and third-person cameras and camera
  collision. It needs ground colliders only, so it can walk on a terrain MeshCollider.
- **Resident views** follow routes from `LocalPaths`, so they need terrain heights for slopes.
- **Shared services:**
  - deterministic streams (`SeedMixer`, xoshiro);
  - typed commands and queries;
  - the XP ledger with idempotent keys;
  - `CharacterAdvancement`;
  - the item catalog with SRD weapon and armour profiles.

## 7. Known technical debt relevant here

- `CombatStats` and `CombatEngine` are player-centric (section 3).
- The layout is regenerated on every entry. It is deterministic, but not persisted or versioned in
  the save.
- The strategic and local settlement representations are independent.
- There is no navigation graph; routes are street projections.
- The pre-existing warning CS0105.

## 8. Files that must remain untouched

- `ProjectSettings/GraphicsSettings.asset` (owner-owned; preserved, never staged).
- The third-party art and its licence files under `Assets/Realmkeeper/Art/ThirdParty/`.
- Earlier tests: none will be removed or weakened. Where Phase 10 intentionally changes behaviour,
  for example the layout, the affected assertions are updated with the reason recorded.

## 9. Plan (stages)

1. **NPC character sheets** (Workstream A).
   - `NpcData.Sheet` (an ordinary `CharacterSheetData`) and `NpcData.Gear` (equipped item IDs).
   - An NPC archetype catalog maps role, settlement and tier to class, level, ability priorities
     and kit.
   - Generation goes through the same validation as the player.
   - Promotion command; keyed NPC XP awards; deterministic NPC advancement through
     `CharacterAdvancement`.
   - `CombatEngine.SheetOf(combatant)` replaces the player-only paths, so a resident with a sheet
     fights with its own class, equipment, spells and resources. Its hit points persist on the
     sheet.
2. **Bestiary** (Workstream B).
   - Extend `CreatureDefinition`: type, size, CR and XP, hit dice, senses, languages, damage
     defences, typed traits (Charge, Regeneration, Undead Fortitude, Relentless, Nimble Escape),
     reactions (Parry), recharge actions, a behaviour profile, habitat, diet, temperament, a loot
     profile and provenance.
   - Add SRD creatures (5.2.1 where already used; 5.1 for those whose 5.1 blocks are known
     exactly), plus clearly labelled Realmkeeper-original livestock and small animals.
   - Persistent `CreatureInstanceData`.
   - Aggregate regional wildlife populations with habitat suitability, predation and hunting.
   - Behaviour-driven AI: flee, territorial, pack, charge, mindless undead, intelligent retreat and
     protect.
3. **Reactions** (Workstream C).
   - A reaction-opportunity architecture inside the engine. Trigger points call one `Ask`. AI
     reactors answer at once, through a deterministic policy.
   - A player reactor suspends the action. The pending opportunity, with the action that
     triggered it, is saved in the encounter. When the player answers, the action is re-run from
     the unchanged state with the recorded answers, so the outcome is atomic and reproducible.
   - Supported: opportunity attacks, Shield, Counterspell, Cutting Words, Deflect Attacks, Uncanny
     Dodge, Parry; and the Indomitable and Bardic Inspiration decisions.
   - A prompt in the encounter panel that cannot be hidden.
4. **Encounter generation** (Workstream D).
   - A deterministic generator from the world context: place (road, wilderness, farmland,
     settlement edge), terrain, habitat, time of day, populations, faction security, road danger,
     and the player's level.
   - A composition budget with objectives (defeat, protect, escape).
   - Persistent creature instances.
   - One idempotent resolution: deaths, population, injuries, XP, loot and road danger.
   - Started from the settlement HUD ("Scout the outskirts").
5. **Settlement identity and layout** (Workstream E). A new engine-free generator (version 2):
   - identity from the economy, population, prosperity, security, water and terrain;
   - a terrain-first heightfield;
   - a river or stream, with bridges or fords;
   - a road network found by slope-aware search;
   - districts; plots and buildings that keep every existing site key;
   - gardens, farmsteads, fields, orchards, pastures and livestock tied to food production;
   - landmarks; walls and gates.

   The layout is **persisted in the save** and never regenerated once made. Validators check
   connectivity, slope, water and overlap. A graph navigation replaces street projection.
6. **Presentation** (Workstream F).
   - Terrain mesh and collider; draped roads by surface; water and bridges; buildings on
     foundations.
   - Fields, fences, orchards and livestock, all batched.
   - Residents on the terrain and road graph.
   - The strategic map's settlement mesh is built from the same persisted layout.
   - The inspector shows identity, districts, landmarks and agriculture; NPC sheet and creature
     inspection.
7. **Persistence.** v11 → v12 migration. Layouts, sheets, instances and populations are generated
   once for older saves, deterministically.
8. **Tests, captures, performance and documentation;** then a local commit, with no push.

## 10. Decisions that need ADRs

- **0043:** NPC character sheets: ownership, tiers and promotion.
- **0044:** creature definitions versus persistent creature instances, and aggregate wildlife.
- **0045:** the reaction decision and resolution architecture (suspend and replay).
- **0046:** encounter generation, objectives and idempotent consequences.
- **0047:** settlement identity and terrain-first layout generation.
- **0048:** persisted spatial data, versioned generation and the migration strategy.
- **0049** (if separate): the navigation graph and its consumers.

## 11. Open owner decisions recorded, not resolved

- **Bible versus Product Foundation scope.** Both documents still differ (noted since Phase 6).
  Phase 10 keeps to the brief and records the discrepancy in its report. It does not change either
  document's vision.
- **Statistics with no reusable source.** Rabbit, fox, cattle, sheep and chicken have no SRD
  statistics. They will be Realmkeeper originals, clearly labelled, and the owner may replace them.
