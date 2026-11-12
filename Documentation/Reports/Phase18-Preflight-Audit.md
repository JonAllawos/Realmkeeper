# Phase 18 — Preflight Audit

Date: 2026-11-12. Made before any Phase 18 code change. Paths are relative to `Assets/Realmkeeper/Runtime`
unless stated. A system counts as complete here only if the code shows it working, not because a class or
panel exists.

## 1. Repository state (verified against Git)

| Fact | Reported | Verified |
|---|---|---|
| Branch | `feature/phase-0-1-foundation` | ✅ same |
| HEAD | `29d43f2` | ✅ `29d43f2` (Phase 17 report), parent `8f36025` (Phase 17 implementation) |
| Origin | pushed | ✅ `origin/feature/phase-0-1-foundation` = `29d43f2`; nothing ahead or behind |
| Working tree | — | Clean at the start of the phase. The only changes since are the owner-approved asset additions (see §4, Q11) and this phase's reports. |
| `ProjectSettings/GraphicsSettings.asset` | owner-preserved | Not modified. It stays excluded from staging. |
| Ignored files | preserve | `Art/ThirdParty/QuaterniusBestiary/` (QAL, local only) and `Documentation/Screenshots/dev-*.png` are present and ignored. They will not be touched or staged. |
| Save schema | v19 | ✅ `Domain/World/WorldData.cs:49`: `CurrentSchemaVersion = 19` |
| Test baselines (Phase 17) | — | .NET 791 passed; Unity EditMode 791 passed, 0 failed; PlayMode 115 passed, 0 failed, 29 skipped (capture sets) |
| Unity | — | Not running. Editor 6000.0.47f1 is installed. Blender 5.2.1 is installed (owner) and used only for asset conversion. |

## 2. What each area actually contains

| Area | Main code | Reality |
|---|---|---|
| Places (exploration) | `Domain/Places/*`, `Application/Places/*`, `Simulation/Places/PlaceWorld.cs`, `Infrastructure/Generation/PlaceGenesis.cs` | 10 archetypes and **10 place definitions in total**: five from Phase 13 and five from Phase 14 (`PlaceCatalog.cs:603–646`, `PlaceContent.Phase14.cs:90–145`). `PlaceGenesis` places **one instance of each definition per world** (`"place:" + definitionId`, `PlaceGenesis.cs:191`), whatever the number of regions. |
| Features | `FeatureCatalog` (`PlaceCatalog.cs:260`) | About 40 features: clues, documents, containers, one lever (`vault_lever`), one sluice wheel, altar, captive, workstations. Study checks are skill against DC; insight text records a discovery key. |
| Mechanisms | `PlaceActions.cs:357–380` | "Pull it": sets `Used` and opens one connection named in `opens`. The sluice is a special case that sets `drained`. No sequence, no clue tie-in, no failure consequence, no reset rules. A second pull does nothing new. |
| Discoveries | `QuestRuntime.Discover` (`Application/Quests/QuestRuntime*.cs:239`), `DiscoveryData` (`Domain/Quests/SocialModel.cs:75`) | A key, text, time and source. **No category and no certainty.** Capped at 200, with the oldest dropped. The journal shows them as flat "when — text" lines (`PlayerHubWindow.Journal.cs:73`). Rumours are a separate unverified list (ADR-0060). |
| Place states | `PlaceDiscovery` (`PlaceModel.cs:33`) | Unknown, Rumored, Discovered, Visited, Explored, Cleared already exist and are shown in the Places tab. |
| Quests | `Domain/Quests/QuestCatalog*.cs` | **22 definitions**: base 5, Phase 13 1, Phase 14 4, Phase 15 1, Phase 16 2, Phase 17 9. **Only 4 have a `Decision` objective** (Phase 14 1, Phase 17 2, base 1). Distinct outcomes are used by a handful of quests ("returned", "replaced", "covered", "sealed", "opened", "proof"). The Phase 14 exploration quests are linear: find → enter → use or read → report. |
| Campaign | `Domain/Campaigns/CampaignCatalog.Waystone.cs`, `Simulation/Campaigns/*` | The Waystone Count: 6 quests, decisions, characters bound by role, endings. Complete as a provisional campaign. |
| World events | `Simulation/Atlas/WorldEventSystem.cs` (441 lines), `Domain/World/Atlas.cs:221` | 10 event types, all raised by the simulation: food shortage, trade disruption, caravan lost, bandits, patrol, bridge, road blocked, faction conflict, refugees, unrest. Each has a lifecycle (Active, Escalating, Resolved, Failed, Expired) and settlements learn of it. |
| Emergent stories | `Simulation/Campaigns/StorySynthesis.cs` | **2 story types**: a lost caravan and the grain road. One per cause ever, a capped number open, at most one new a day. |
| Factions | `Simulation/Systems/PoliticalSystem.cs`, `PoliticalMath.cs`, `Simulation/Atlas/FactionActivitySystem.cs` (91 lines) | Relationships and influence move daily. **The only faction activity is a paid patrol answering bandits** (`FactionActivitySystem.cs`). `FactionConflict` events are raised from relationships, but nothing stages them. |
| NPCs | `Simulation/Npcs/*` (NpcSystem 656 lines, NpcMind 223, NpcActivity 179) | Schedules, needs, relationships (with a cause and capped), memories (`MemoryCategory`, 8 kinds), knowledge kept apart from truth, rumour spread. |
| Companions | `Simulation/Party/CompanionLoyalty.cs`, `CompanionStories.cs` | Loyalty, crime reactions, campaign-decision reactions by personality, remarks on home settlements, a rivalry between companions. **Personal stories: 2** (Old Ground, Phase 15; Unfinished Business, Phase 17). |
| Loot and rewards | `Domain/World/Loot.cs`, archetype `LootTable` | Tables per archetype ("old_cache", "miners_stores", "tomb_offerings", "garrison_stores", "outlaw_leader"). The Phase 11 items include SRD magic items. |
| Presentation | `Exploration/Presentation/*` | Places and creatures are built at runtime. Creature models existed only for skeletons and zombies (KayKit) and the local-only goblin (`CombatantBodies.cs:33–54`); the rest used primitives. Settlement trees, wells, barrels, stalls and lamps were primitives (`SettlementEnvironmentBuilder.cs:58–237`). |

## 3. The twelve audit questions

### Q1. Which content systems are genuinely complete?
- **Settlement simulation:** economy, consumption, production, trade and wellbeing run daily with tests and determinism checks.
- **Travel and atlas:** regions, routes, groups, road fights and event knowledge (Phase 16).
- **Combat and spells:** the SRD rules engine, reactions, conditions and creatures (Phases 9–11, 15).
- **Crime:** witnesses, bounties and companion crime reactions (Phase 14).
- **Campaign engine:** The Waystone Count, end to end, with save/load (Phase 17).
- **Place model:** generation, persistence of every door, feature, group and trap state, fire, bridges, keys and parley (Phases 13–14).

### Q2. Which are technically implemented but have little content?
- **Mechanisms:** the machinery for persistent feature state exists, but only two mechanisms (`vault_lever`, `sluice_wheel`) do anything, and each does one thing.
- **Emergent stories:** the pipeline is sound (stable keys, deduplication, cap), but there are only 2 types against 10 event types.
- **Companion personal stories:** 2.
- **Faction activity:** a single behaviour, the patrol.
- **Discoveries:** stored, but with no structure, so they can't drive anything beyond quest objectives keyed by name.
- **Places:** 10 per world, one per definition, regardless of world size.

### Q3. Which features exist but are hard to discover or reach?
- **Clue-to-consequence links are invisible.** A studied clue records a discovery key, but nothing in the journal says what it relates to or where it leads.
- **Places beyond the starting region.** All 10 places anchor on settlements chosen by terrain (`PlaceGenesis.cs:EnsurePhase13`). In a multi-region world, other regions may have none.
- **Mechanisms.** A lever offers "Pull it" and an Examine check, but the examine text is the only hint of what it does.
- **The journal has 13 tabs** (`PlayerHubWindow.Journal.cs:24`). Discoveries, Rumours, Events and Places are separate flat lists with no grouping, so the useful ones are buried.

### Q4. Which content systems lack variety?
- **Mechanisms:** one pattern, "pull to open".
- **Emergent stories:** 2 types.
- **Faction actions:** 1 type.
- **Puzzles:** none.
- **Places:** each archetype has exactly one authored place. Generated rooms vary, but the story features are per definition.
- **Quest structure:** 18 of 22 quests are linear with no decision.

### Q5. Which world systems rarely interact?
- **Places ↔ world events.** No event touches a place, and no place state affects a settlement. The culverts' sluice is local only.
- **Discoveries ↔ factions and trade.** Discovering a faction's orders (`fort_orders`) or bandit tolls changes nothing about that faction's standing or the road.
- **World events ↔ follow-up gameplay.** Most events resolve themselves (`WorldEventSystem.cs`). Only caravan-lost and food-shortage spawn stories.
- **Factions ↔ each other.** `FactionConflict` is raised but has no stages, no actions and no player choice.

### Q6. Which NPC behaviours feel repetitive or artificial?
- **Sitting, Eating and Resting render as standing.** `ActivityKind` has them (`Simulation/Npcs/NpcActivity.cs:9`), but `ResidentView.PoseFor` returns pose 0 (standing) for all three (`Exploration/Presentation/ResidentView.cs:179`). That holds even though the Universal Animation Library has `Sitting_Idle_Loop` and `Sitting_Talking_Loop`.
- **No visible exchanges between NPCs.** Relationships and rumours move in data, but nobody visibly talks to anyone except as "Socialising" in place.
- **Personality doesn't change what an NPC does in the street.** It affects dialogue and decisions only.

### Q7. Which exploration locations are underused?
- **All ten:** each exists once per world and carries one quest.
- **Watch fort:** `garrison_orders` produces a discovery that changes nothing.
- **Bramble Cave and The Broken Hall:** no story features beyond generic clues (`PlaceCatalog.cs:643–644`) and no quest.
- **Crypt:** a `hidden_panel` and an altar, with no puzzle.

### Q8. Which quests lack alternative outcomes?
- **Single outcome:**
  - Lost Below;
  - The Founders' Register;
  - The Lantern of the Fane;
  - The Old Culverts;
  - Old Ground;
  - Timber for the Bridge;
  - The Road to {other};
  - Teeth at the Folds;
  - Empty Shelves;
  - most Waystone sub-quests outside their decision points.
- **With real alternatives:**
  - Tolls on the Road (drive off or talk off);
  - Short Measures;
  - Under Seal (returned or opened);
  - Bread Before Winter;
  - the Waystone decisions.

### Q9. Which world events fail to create follow-up gameplay?
Of the 10 types:
- **Trade disruption, refugee movement, local unrest, bridge damaged, road blocked and faction conflict** create no quest, no opportunity and no NPC request.
- **Bandit activity** spawns a patrol, which the player cannot join or support.
- **Caravan lost and food shortage** have stories. However, the lost-caravan story ends in news, not goods: the caravan's cargo never reappears.

### Q10. Which companion features remain underdeveloped?
- **Personal stories:** 2, both short.
- **Reactions are limited to:**
  - campaign decisions (`CompanionStories.React`);
  - crimes (`CompanionLoyalty.CrimeReaction`);
  - remarks about home settlements.
- **No reactions to:** discoveries, places, combat outcomes, faction choices outside the campaign, or settlements in hardship.
- **No once-only or cooldown framework** beyond per-decision guards.
- **Companion AI:** follows and regroups (Phase 14 `PlaceFollowers`). Hazard avoidance relies on the player's path. To be checked hands-on in Part 7.

### Q11. Which assets and animations are unused?

**Unused clips that are already imported** (take names read from the FBX files):
- Universal Animation Library:
  - `Sitting_Enter`, `Sitting_Idle_Loop`, `Sitting_Talking_Loop`, `Sitting_Exit`;
  - `Fixing_Kneeling`, `PickUp_Table`, `Push_Loop`, `Idle_Torch_Loop`;
  - `Dance_Loop`, `Roll`, `Swim_*`, `Sprint_Loop`, `Crouch_Fwd_Loop`.
- KayKit General: `PickUp`, `Use_Item`, `Throw`, `Spawn_Ground`.
- KayKit Special:
  - `Skeletons_Awaken_Floor`, `Skeletons_Awaken_Standing`;
  - `Skeletons_Inactive_Floor_Pose`, `Skeletons_Inactive_Standing_Pose`;
  - `Skeletons_Taunt`, `Skeletons_Death_Resurrect`.
- KayKit Ranged: `Ranged_Bow_Draw`, `Ranged_Bow_Release`, `Ranged_Magic_Summon` and others. Only a subset of the ranged set is wired.

**Wired clips** (`Editor/ArtSetup.cs`, `CharacterAnimationBuilder.cs`): idle and walk loops, run, attack variants, block, cast, hit, death, interact, jump, talk.

**New assets added this phase** (owner-approved; see `Phase18-Asset-Research.md`):
- the KayKit Dungeon Remastered kit;
- Quaternius creatures, scaled and trimmed by Blender script: spider, rat, deer, stag, fox, goblin, giant, orc, bull, cow, horse, wolf, husky, chicken; plus the farm sheep;
- the Poly by Google bear, boar and goat (CC BY 3.0);
- Quaternius settlement props: well, stalls, street light;
- the KayKit post lantern;
- the Fantasy Props MegaKit;
- the Stylized Nature MegaKit.

None of these are wired in yet.

**Bestiary:** Imp and Puglin are local only and stay git-ignored. **Ultimate Monsters is not installed**: it was rejected as a stylistic mismatch, and its download was blocked.

### Q12. Which interfaces make existing functionality hard to use?
- **Journal Discoveries tab:** one flat chronological list (`PlayerHubWindow.Journal.cs:73, 97`), with no grouping, certainty or link to the place.
- **Mechanism feedback:** the result text is the only signal, and there is no "what this did" in the journal.
- **Places tab:** shows state, but not what remains: secrets found, captives, the objective.
- **Thirteen journal tabs:** discovering where something went is slow. A wholesale redesign is out of scope (Workstream A); Phase 18 will improve only the tabs its new content uses.

## 4. Current limitations to respect
- **Schema v19.** Any new persisted state (discovery category and certainty, mechanism sequences, new place instances, faction conflict stages) needs a v20 migration and old-save handling, using the strip chain in tests.
- **Determinism.** Every new daily behaviour must be ordered by record order and seeded from the world seed, as the existing systems are.
- **`PlaceGenesis` makes each generation of definitions once.** New places must be a new generation (`EnsurePhase18`), so older saves gain them exactly once.
- **Architecture.** Domain and Simulation must not reference UnityEngine.
- **Scope.** No second quest or story system: new content goes through `QuestCatalog`/`DialogueCatalog` partials, `StorySynthesis`, `CampaignCatalog`, the feature catalog and the existing commands.

## 5. Recommended Phase 18 priorities (the plan this phase follows)

| # | Work | Answers | Persists? |
|---|---|---|---|
| 1 | **Discoveries with meaning:** a category on features and discoveries (Historical, Geographic, Cultural, Faction, Religious, Natural, Economic, Personal, Dangerous, Arcane), each used for something: journal grouping, who will pay or care for the knowledge (reporting to a faction or temple), companion interest. Certainty is Rumoured, Unverified or Confirmed, linked to the place. | Q2, Q3, Q5, Q12 | yes (v20) |
| 2 | **Three complete mechanism patterns:** (a) an ordered sequence with a written clue and a consequence for a wrong order; (b) a counterweight or water control with a staged state; (c) a light-revealed sign that opens a hidden way. All persistent, clue-based and solvable without guessing, with no reset on load. | Q2, Q4, Q7 | yes |
| 3 | **Location depth:** room purposes and story features for the cave, ruin, crypt and fort; per-archetype story sets; more places per world in regions that lack them (a Phase 18 generation that reuses archetypes with new story sets, made once). | Q4, Q7 | yes |
| 4 | **Four quest chains** (investigation, faction/political, wilderness/dungeon, settlement/economic), each with a decision and at least 2 outcomes; plus the **Waystone expansion** (optional objectives, an alternative solution, companion involvement), still provisional. | Q8 | yes |
| 5 | **Emergent stories and event follow-ups:** new story types from bandit activity, bridge damage or road blocks, refugees, unrest and faction conflict, each with real consequences. A recovered caravan returns its real goods. Plus a lifecycle audit test. | Q9, Q5 | yes |
| 6 | **Faction conflict stages:** tension → dispute → skirmishes → open conflict → settlement, with player options to support, oppose, mediate or ignore, and the world moving on without the player. | Q5, Q4 | yes |
| 7 | **NPC life:** sitting, eating and resting poses (unused clips); visible NPC-to-NPC exchanges grounded in existing relationships and rumours; personality-driven differences in daily choices; memory certainty (witnessed, heard, believed). | Q6, Q11 | partly |
| 8 | **Companions:** 2 new personal story templates; contextual reactions (discoveries, places, combat, settlements in hardship, faction choices) with once-only and cooldown rules; fixes from hands-on checks. | Q10 | yes |
| 9 | **Rewards:** distinctive tools tied to the mechanics (for example a lantern that reveals signs, a surveyor's rod, a counterweight), and a progression integrity check (no double rewards). | Part 8 | yes |
| 10 | **Assets:** wire in the creature models, place and settlement props and unused clips (dormant skeletons rising, sitting, pick-up), only where visible; CREDITS, with the CC BY lines mandatory. | Q11 | no |
| 11 | **UI:** grouped and certainty-marked Discoveries; a "what remains" line for places; mechanism outcomes in the journal. No redesign. | Q12 | no |
| 12 | **Tests, captures, performance, hands-on validation, docs** (Parts 11–17). | — | — |

## 6. Hands-on validation: an honest constraint
Claude has no interactive control of the Unity editor in this environment. Gameplay validation will therefore be:
1. automated EditMode and PlayMode scenario tests;
2. automated UI-driven PlayMode sequences, with captured frame sequences assembled into short recordings where possible;
3. manual inspection of the captured screenshots.

None of this is human play. An owner playtest checklist will be provided for real hands-on play, and the
Completion Report will keep these four kinds of evidence separate.
