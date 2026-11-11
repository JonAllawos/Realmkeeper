# Phase 12 pre-flight: audit, baseline and plan

## Git state at start

- Branch `feature/phase-0-1-foundation`, HEAD `7b2831b` (Phase 11), level with `origin` (Phases 9–11 were pushed on the owner's instruction after Phase 11).
- Working tree: only the owner's `ProjectSettings/GraphicsSettings.asset` is modified. It is preserved and never staged.
- An untracked `Assets/InitTestScene….unity`, left by the Unity test runner, is ignored and never staged.

## Baseline (end of Phase 11)

| Suite | Result |
|---|---|
| .NET harness (Release) | 574 passed, 0 failed, 0 build warnings |
| Unity EditMode | 574 / 574 |
| Unity PlayMode | 96: 80 passed, 16 skipped (capture sets), 0 failed |
| Save schema | v13 |

## What exists: classification from code and tests

| System | Status | Evidence | How Phase 12 uses it |
|---|---|---|---|
| NPC identity, personality, condition, goals, schedules | **Complete** (Phase 4) | `Domain/World/Npcs.cs`, `Simulation/Npcs/NpcSystem.cs`, `Tests/EditMode/Npcs` | Dialogue conditions (traits, morale, activity), quest givers by role and occupation |
| NPC memories | **Complete**, bounded (Phase 4) | `NpcMemoryData`, `NpcMind.Remember`, weekly decay | Quest and dialogue outcomes become memories; dialogue reads them |
| NPC knowledge (Known / Believed / Rumor, source, hops, confidence) | **Complete** (Phase 4) | `NpcKnowledgeData`, `NpcSystem.Share` (one item per meeting, hop and lifetime limits) | Extended with a `PlayerDeed` topic so the player's deeds spread through the same channels |
| NPC ↔ NPC relationships | **Complete** (Phase 4) | `NpcRelationData` (affinity, trust, cause) | Witnesses and who-tells-whom |
| Player ↔ NPC regard | **Partial** | `PlayerRegardData`: familiarity, affinity, trades, conversations | Gains trust, respect, fear, gratitude and hostility |
| Player ↔ faction standing | **Missing** | No player standing anywhere; factions relate only to each other (`FactionRelationData`) | Built: player faction standing, kept separate from faction-to-faction relations |
| Settlement reputation, notoriety | **Missing** | — | Built |
| Factions, influence, governance, goals | **Complete** (Phase 3) | `Domain/World/Politics.cs`, `PoliticalSystem` | Faction quest givers and consequences (influence) |
| Settlement economy (food, resources, stability, prosperity) | **Complete** (Phase 2) | `SettlementData`, `ResourceInventory`, shortage flags | Emergent problems, quest consequences on real stocks |
| Chronicle | **Complete** (structured events, Phase 2+) | `HistoricalEvent`, `CommandContext.ReportEvent` | New quest, decision, reputation and discovery events; a player-known filter |
| Conversation | **Partial** (Phase 4) | `AskNpcCommand`: 11 topics answered from the person's own knowledge; no branching, choices or consequences | Kept as "Ask about…" responses inside the dialogue engine |
| Notice board, granary ledger | **Partial** (Phase 2) | `EnvironmentInteractable`: the ledger reads stores; the board posts a labour notice | Become world objects a quest can require; the board lists notices |
| Inventory, ownership, containers, merchants | **Complete** (Phase 11) | `ItemCommands`, `ItemStacks`, one-holder validator | Delivery, collection and item rewards through the same commands and checks |
| Combat and world encounters | **Complete** (Phases 8–10) | `WorldEncounters.Resolve` (once per encounter, keyed) | Defeat, survive and protect objectives from the resolved encounter |
| XP awards | **Complete**, keyed ledger (Phase 7) | `AwardExperienceCommand`, `ExperienceAward.Key` | Quest XP keyed `quest:{instance}` |
| Quests, objectives, journal | **Missing** | `QuestRef` on encounters is an unused string; `ItemCategory.Quest` is a category with one letter | Built |
| Dialogue graphs, social checks | **Missing** | — | Built |
| Discovery (known vs visited locations) | **Missing** | The player's settlement entry is recorded only as their position | Built |
| Content settings | **Missing** | — | Built: tags and presentation rules |

## Plan (ADR-0056 … ADR-0061)

1. **ADR-0056 Quests.** A definition is a template with roles. An instance binds roles to real world entities, has validated states, and keeps progress, decisions, rewards and history. Authored and emergent quests use one engine.
2. **ADR-0057 Objectives and quest events.** Commands emit typed quest events. The engine updates matching objectives in the same transaction (atomic, idempotent). State objectives are re-checked only after relevant events and by the daily quest system, never per frame.
3. **ADR-0058 Dialogue.** Data-driven graphs: nodes, conditional lines and responses, skill checks rolled with the world's dice, and typed consequences applied by the choose-response command.
4. **ADR-0059 Reputation.** Per person: familiarity, affinity, trust, respect, fear, gratitude, hostility. Faction standing and settlement reputation are separate. Notoriety is separate. Tiers are presentation only. Changes are keyed. Deeds spread as knowledge, so only those who heard react.
5. **ADR-0060 Emergent activities and discovery.** Quests arise from real conditions (food shortage, predators and bandits near a settlement, a merchant short of stock). Keys are deterministic, there are no duplicates, and a quest expires when its cause ends. Discovery state separates "exists", "known" and "visited". The notice board and other objects use one interaction command.
6. **ADR-0061 Content tags.** Quest and dialogue text carry content tags with configurable presentation. There is no main-campaign canon: the main-campaign category exists, and no setting-defining content is authored.

The vertical slice uses three quests bound to the generated world:

- a granary shortfall (a real settlement problem, with investigation and alternative endings);
- wolves at the folds (creatures drawn from real populations, through the encounter system);
- a sealed letter (item ownership and delivery, with a choice).
