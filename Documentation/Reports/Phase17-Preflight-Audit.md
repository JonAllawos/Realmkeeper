# Phase 17 preflight audit: Phase 16 closeout and the campaign engine

Date: 2026-11-12. This audit was written before any Phase 17 change. It records what the code actually does, not what earlier reports say.

## 1. Git state

| Item | Found |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| HEAD | `7ab5163266fa7c1b7057b162223c50b2c3f8036a` (Phase 16) |
| Origin | `origin/feature/phase-0-1-foundation` is at the same commit: nothing ahead, nothing behind. The Phase 16 push is confirmed. |
| Working tree | Clean. No user-owned uncommitted files, and `ProjectSettings/GraphicsSettings.asset` is unmodified (already committed in `48b0098`). |
| Stashes | None. |
| Local-only content | The Quaternius Bestiary, `Assets/InitTestScene*` and development screenshots stay ignored or unstaged, as before. |
| Unity | The editor was not running (only Unity Hub). The full EditMode run was started on this unchanged commit before any edit. |

## 2. The reported Phase 16 baseline, checked against the code

| Reported | Checked |
|---|---|
| Save schema v18 | `WorldData.CurrentSchemaVersion = 18`; migrations v1→v18 are chained in `JsonWorldSerializer.BuiltInMigrations`. |
| Six regions on a 3×2 grid | `WorldAtlasGenesis` plus `RegionData.AtlasX/AtlasZ`. Older worlds keep one region until "Discover the lands beyond". |
| Atlas opens with A | `RealmkeeperHud.Atlas.cs`, with eight layers in `AtlasLayers`. |
| Ten world event types | `WorldEventType` has 10 values. |
| Two event quests | `QuestCatalog.Phase16`. |
| Road groups drawn only in the player's region | `AtlasQueries.View`: `if (region != v.PlayerRegion) continue;`. |
| "Fight it out" lets the AI play | `WorldSession.FightItOut` calls `CombatAi.Next` for the player's turns. The fight itself is already a real encounter, started by `TravelCommandHandlers.Fight` through `WorldEncounters.StartPlan`. Only the *presentation* is missing: `ExplorationController.Sync` loads a scene only when the player is in a settlement or a place. |

## 3. The systems the campaign engine must build on

### Quests (Phase 12, extended in 13–16)
- **Definitions and instances.** `QuestDefinition` is a template with roles; `QuestInstanceData` binds those roles to real world IDs. Instance IDs are stable: they come from the definition and its source key.
- **Objectives.**
  - Stages give sequential progress, and one stage can hold parallel objectives.
  - Groups give alternatives (any one of the group is enough).
  - Objectives can be hidden, optional, time-limited, conditional, `UnavailableWhen`, or `FailsQuest`, and can lead to a named outcome.
  - Completion is event-driven: commands emit `QuestEvent`s and `QuestRuntime.Apply` matches them inside the command's transaction. State objectives are re-read only when an event arrives.
- **State machine.** `QuestStates.CanMove` is checked. A closed quest never reopens unless it is repeatable.
- **Rewards and consequences.** These are keyed (`RewardsClaimed`, `AppliedConsequences`, the reputation ledger keys), so a reload or a repeated command never pays twice.
- **Failure.** `FailureKind` covers `NpcDied`, `TimeLimit`, `Decision` and `CauseEnded`.
- **Decisions.** Decisions recorded on a quest are strings in `QuestInstanceData.Decisions`; the player's decision log is in `PlayerSocialData`.
- **Pruning.** `QuestLifecycle.Prune` keeps at most 160 instances. Authored and open quests are never pruned, but emergent closed ones are. Campaign-linked quests must be protected from pruning in the same way.

### Dialogue (Phase 12)
- **Data-driven graphs.** `DialogueDefinition` contains nodes, each with lines and responses. A line or response is shown only when its typed `QuestCondition`s hold.
- **Choosing.** Choosing a response is one command, and its consequences are applied in that command's transaction (`DialogueEngine.Apply`). There are 32 typed consequence kinds, and checks are rolled with the world's dice.
- **Gaps:**
  - A response whose conditions fail is *hidden*; there is no "disabled, because…".
  - Conditions can read only one quest's decisions, not decisions that persist across quests.
  - There is no condition kind for world events, character status, or campaign state.

### People (Phases 4, 5, 10, 14, 15)
- **Each person is one record.** `NpcData` holds personality (8 traits), goals, memories, knowledge, regard for the player (`PlayerRegardData`), and condition. `NpcRelations` hold person-to-person bonds (affinity, trust, respect, fear, rivalry, with a cause).
- **Status already exists in the authoritative systems:**
  - dead (`IsDeceased`);
  - held or escorted (`Escorts`);
  - away from home (`PlacePresence.IsAway`);
  - hostile (`PlayerRegard.Hostility`);
  - departed or dismissed companion (`CompanionRecordData.Standing`);
  - injured (`Condition`).
- **No campaign copies are needed.** A campaign can derive a person's status from these rather than storing its own copy.

### Factions (Phase 3)
- `FactionData` has a leader (`LeaderNpcId`), influence, goals and presence.
- `FactionRelationData` has opinion, trust, rivalry and agreements.
- A leader can change through the political simulation, so a campaign must resolve "the faction's leader" when it needs them, not freeze the leader at the start.

### Companions (Phase 15)
- `CompanionRecordData` holds loyalty plus keyed `Approvals`, each with a reason, along with standing and `PersonalQuestId`.
- `CompanionLoyalty.Apply(w, id, key, delta, reason)` is the one keyed path for loyalty changes.
- The personal quest "Old Ground" (`companion_old_ground`) is offered at loyalty 60 and advances only from real events.
- `CompanionLoyalty.Present` lists active members only, so reserve members are left out.
- There is no second personal story and no companion-to-companion relationship use, although `NpcRelations` can hold one.

### World events, travel and groups (Phase 16)
- **World events.** `WorldEvents.Raise` makes one open event per cause key, `Move` validates state changes, `TellSettlements` seeds knowledge, and `PlayerLearns` records what the player knows.
- **Groups.** These are caravans, patrols, bandits, refugees and pilgrims (`TravelingGroupData`), positioned by `AtlasQueries.GroupAt` from authoritative travel state.

### Chronicle, knowledge and rumours
- Commands report `HistoricalEvent`s.
- People learn and share knowledge (`NpcMind.Learn`, the Phase 4 sharing).
- The player's rumours, discoveries, decisions and known places live in `PlayerSocialData`.

### Persistence
- **Migrations** are JSON-level (`ISaveMigration` on a `JObject`). Each new list gets an empty default, and each new simulation system is inserted into `enabledSimulationSystems` after an anchor.
- **Mapping.** The mapper is split by area (`WorldSaveMapper.*.cs`).
- **Validation.** `WorldValidator` validates the loaded world.

## 4. Critical questions

| Question | Answer from the code |
|---|---|
| Can quests extend into multi-region arcs? | Partly. A quest binds to people anywhere, and `ReachedSettlement` works in any region. An *arc* that strings quests together does not exist yet. |
| Can quests branch on persistent player decisions? | Only on decisions recorded on the *same* quest (`ConditionKind.Decision` reads `c.Quest.Decisions`). Decisions that persist across quests need a new home. |
| Can the quest system hold long-running campaign objectives? | One quest can be long, but there is no container for many quests, their order and their shared outcome. |
| Can NPCs remember specific campaign interactions? | Yes: `NpcMind.Remember` and the `NpcRemembers` condition. |
| Can story-critical NPCs die, disappear, change allegiance or become unavailable? | Yes, through the existing systems (death in combat or simulation, escorts, hostility, faction leadership change, companion departure). Nothing protects them, which is correct. |
| Can a campaign respond to a settlement changing hands, a faction change or a regional event? | Not yet: there is no campaign. The data is all there (`SettlementData.FactionId`, faction leaders, world events). |
| Can several quest lines progress independently? | Yes: quests are independent instances. |
| Can the player ignore the main story indefinitely? | Yes. No system waits on the player; offers lapse, and the world keeps running. |
| Can a campaign be completed without a faction allegiance? | Must be designed in; nothing in the quest system forces membership. |
| Can campaign progress be saved and restored without replaying events? | Quests already can (keyed rewards and consequences). The campaign layer must follow the same keyed pattern. |
| Can a campaign run in an old world? | Yes, if its roles bind to the world as it stands, and if activation checks its prerequisites rather than rewriting history. |
| Can sandbox worlds run without a campaign? | Yes: today every world is a sandbox. The campaign layer must stay optional. |

## 5. Decisions taken from this audit

1. **Build on what exists.**
   - Campaign *definitions* are data.
   - Campaign *instances* live in the world and reference real people, settlements, factions, roads and quest instances by ID.
   - Arc objectives are linked quest instances, recorded decisions, or world-state conditions read through the existing evaluator. There is no second objective engine.
2. **Decisions are campaign records.**
   - They have stable IDs, the choice made, the time and the source.
   - Their effects are typed and keyed, so each applies once.
   - A delayed effect is a persisted pending record with a due time and an optional trigger condition, fired by a daily system.
3. **Character status is derived from the existing systems**, never stored by the campaign. A dead person stays dead.
4. **Dialogue gains condition and consequence kinds** for campaigns, world events and character status, and shows unavailable choices with their reason.
5. **The atlas fight gets a scene.** It is staged on the open ground around the near end of the road, without buildings or residents. The player stays on the road in the world state, and the existing tactical combat HUD runs the fight.
6. **Atlas groups outside the player's region** show only where the player has a report of them (a known world event), marked as reported, with when and from whom.
7. **Save schema v19** adds campaign instances and companion remarks. A world without a campaign is a sandbox.
