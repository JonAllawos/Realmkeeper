# ADR-0009 implementation: audit and plan

Written before any code for this milestone changed (branch `feature/phase-0-1-foundation` at `8611659`).

## Audit findings

| Area | Current state |
|---|---|
| Worlds | `WorldData` (schema v2): settlements, factions, NPCs (empty), events, trade routes, shipments. One `RegionMap` per world, regenerated from the seed on load (ADR-0005). |
| Regions | **Missing.** No `RegionId`, no region records; settlements have only a `MapCoord`. |
| Settlements | `SettlementData` keyed by `SettlementId`, with Phase 2 economy. Strategic visuals are keyed by `SettlementId` (`SettlementView`). |
| NPCs | `NpcData` (`NpcId`, name, home settlement, faction) is **modelled but never generated**; nothing simulates it. |
| Character | **Missing.** |
| Session | `WorldSession` (Application, engine-free) owns the world. **Partial host:** `RealmkeeperBootstrap` (a MonoBehaviour in the single scene) creates the session in `Awake`, so a scene change would destroy it. |
| Commands | `WorldSession.Execute` has **one overload per command**; `CommandResult` has success/failure only, with no categories and no registry. Copy → validate → commit exists only inside the scheduler. |
| UI | Strategic HUD (UI Toolkit) with selection, inspector, time controls, save/load. There is no mode switching. |
| Persistence | JSON schema v2 with a migration chain (v1→v2). Economy is derived from the terrain on load for v1 saves. |
| Tests | 110 EditMode (also run by the .NET harness), 17 PlayMode. All pass in Unity 6000.0.47f1. |
| Assemblies | Domain, Simulation, Application, Infrastructure (engine-free); Presentation; two test assemblies. No exploration assemblies exist. |

## ADR-0009 requirements → plan

| ADR-0009 item | Existing | Change | Files / systems | Tests |
|---|---|---|---|---|
| D1 One world, one session | `WorldSession` exists; owned by a scene object | Engine-free `WorldSessionHost` with an explicit lifecycle (Idle → Active → Shut down); a persistent Unity bridge (`RealmkeeperRuntime`, `DontDestroyOnLoad`) that owns the host, playback and mode transitions; the strategic bootstrap attaches to it | Application, new `Realmkeeper.Hosting`, Presentation bootstrap | Host lifecycle (EditMode); same session across transitions (PlayMode) |
| D2 One way to change state | Typed `Execute` overloads | `ICommand` / `ICommandHandler<T>` / `CommandRegistry` in Application; `WorldSession.Dispatch<T>`; copy → validate → commit per command; structured `CommandStatus`; persistent consequences recorded in the chronicle | Application, `Queries`/`Commands` | Registry resolution, missing handler, invalid refs, no partial mutation |
| D3 Keyed, not copied | IDs exist for settlements and NPCs | `RegionId`, `CharacterId`; settlements reference their region; the local environment is keyed by (settlement ID, region ID); NPC views keyed by `NpcId` | Domain, Infrastructure (generation, migration), Exploration.Domain | Region uniqueness/serialization/validation; NPC no-duplication |
| D4 Clock independent of camera | Playback ticked by the bootstrap | Playback moves to the persistent runtime, ticking whatever the mode | Hosting | Date unchanged by perspective/mode switches |
| D5 LOD as a simulation concept | Population is an integer | **Partial (documented):** named NPCs generated as a tracked subset of the population (they are counted in it, never added to it). Full tiers are deferred. | Infrastructure generation, Domain `NpcData` | NPC identity stable across load/enter/exit |
| D6 New modules when needed | None | `Exploration.Domain`, `Exploration.Application` (engine-free); `Exploration.Interaction`, `Exploration.WorldStreaming`, `Exploration.Presentation` (Unity) | New asmdefs, harness projects | EditMode and PlayMode suites |
| D7 No new dependencies | — | Built-in `SceneManager.CreateScene`/`UnloadSceneAsync`, `CharacterController`, Input System (already present). No packages added. | — | — |
| Consequence: one exploration action visible on the map, saved, in the chronicle | — | Notice-board command changes the settlement's labour focus (shown in the inspector, saved, chronicled); talking to an NPC records an acquaintance (saved, chronicled) | Exploration.Application, Simulation (labour focus) | Integration test across session, save/load and chronicle |

## Clarifications and discrepancies (to record in ADR-0009)

1. **Where character state lives.** ADR-0009's expected changes put character state "inside
   `WorldData`", but its module rules forbid core Domain from referencing `Exploration.Domain`.
   Resolution: persistent world facts (regions, the player character's location and
   perspective, NPC acquaintance) are core Domain types, because they are world state.
   `Exploration.Domain` holds engine-free local-environment layouts and interaction
   definitions, which are derived from world data and never saved.
2. **Where the shared runtime lives.** The architecture docs left this open. Decision: a small
   Unity assembly `Realmkeeper.Hosting`, referenced by both the strategic and exploration presentations.
3. **"Recorded in the chronicle."** Only actions with persistent consequences are chronicled
   (meeting a person, posting a labour notice). Entering and leaving a settlement update the
   character state but are not chronicled, to avoid low-value events (Phase 2 rule).
4. **No item inventory in this slice.** The prompt forbids inventory mechanics. The
   ADR-0009 "inventory" item stays future work.

## Vertical-slice content

- **Playable settlement:** whichever settlement the user selects. The environment is a
  deterministic layout generated from (world seed, settlement ID, settlement type), so every
  settlement can be entered, but only one layout generator exists and it is tuned for this slice.
- **Interactions:**
  - talk to a resident NPC (persistent: acquaintance plus a chronicle entry);
  - read the granary ledger (informational: an authoritative snapshot, no change);
  - post a labour notice (persistent: labour focus, shown in the inspector, affects production from the next day).
