# Dual-perspective architecture (ADR-0009 prerequisites and vertical slice)

How the strategic map and settlement exploration share one world. Decision:
[ADR-0009](../Decisions/ADR-0009-dual-perspective-world.md). Plan and audit:
[Phase 3 plan](../Phase3/ImplementationPlan.md).

## 1. Region identity

- `RegionId` (Domain, `Identifiers/RegionId.cs`) is a GUID, like every other ID. It never
  depends on a name, position or list order.
- `RegionData` holds the ID, a display name and the region's rectangle on the map
  (`Origin`, `Width`, `Depth`). Today each world has one region covering the whole map.
- Every `SettlementData` has a `RegionId`. `WorldValidator` rejects:
  - a settlement with no region;
  - a settlement whose region does not exist;
  - a settlement outside its region's rectangle;
  - duplicate or empty region IDs, and regions with no name or no size.

  Error messages name the settlement and the region.
- **New worlds:** `WorldPopulationInitializer.EnsureRegions` creates the regions from the
  deterministic stream `"regions"` (IDs) and `"region-names"` (names). It assigns each
  settlement to the region containing it.
- **Saves:** schema v3 stores `regions[]` and `settlement.regionId`. The
  `MigrationV2ToV3` migration is structural: it adds an empty `regions` array. After mapping, a
  save older than v3 is upgraded by `WorldPopulationInitializer.EnsureAll` using the same streams
  as generation. A v2 world therefore gets exactly the region, player and resident IDs a fresh
  world with that seed would get. Nothing that was already in the save is discarded or
  regenerated. If the upgrade cannot place a settlement in a region, loading fails with
  `LoadStatus.Invalid` and an explanation, rather than inventing data.

## 2. The session lifecycle

```
WorldSessionHost (Application, engine-free)
  Idle --StartNew / StartFromSave--> Active --Shutdown--> Idle --Dispose--> Disposed
```

- `WorldSessionHost` owns the one `WorldSession`.
  - Starting while a session is active is refused with `InvalidSessionState`; the world is
    never silently replaced.
  - A failed start closes the half-built session and stays `Idle`.
  - `Shutdown` closes the session, so stale references get `InvalidSessionState`.
- `RealmkeeperRuntime` (Unity, `Realmkeeper.Hosting`) is the only Unity object that owns the host.
  - It is marked `DontDestroyOnLoad`, so it survives scene changes.
  - It owns `SimulationPlayback` and ticks it every frame in every view mode (ADR-0009 point 4).
  - It derives `ViewMode` (Strategic or Exploration) from the player character's location in world data.
  - A second runtime destroys itself with an error. There is no static instance field: scene
    objects call `RealmkeeperRuntime.FindOrCreate()`.
- The full lifecycle:

  | Step | Mechanism |
  |---|---|
  | Start | `RealmkeeperRuntime.Awake` creates the host. |
  | Load or create | `EnsureWorld` → `Host.StartNew`; or `LoadWorldCommand`. |
  | Map | `RealmkeeperBootstrap` builds the strategic view on `Runtime.Session`. |
  | Enter settlement | `EnterSettlementCommand`. |
  | Explore | The exploration controller. |
  | Return | `ExitSettlementCommand`. |
  | Continue | The same session, same date. |
  | Save | `Runtime.Save` raises `PreparingSave`, then dispatches `SaveWorldCommand`. |
  | Close | `Host.Shutdown` or `Dispose`. |

### Rebinding after a restart

Presentation never assumes the first session is the only one. Every component that listens
to a session holds a `WorldSessionBinding` (Application, engine-free):
- `Bind(newSession)` first removes the old subscription, then subscribes exactly once.
- Binding the same session again does nothing.
- A closed session is never kept: the binding drops it when the session closes.

`RealmkeeperBootstrap` listens to `Host.SessionStarted` and `SessionEnded`, and rebinds the
world view, selection, HUD (and its playback clock) and map markers. `ExplorationController`
does the same.

Tests check this:
- `WorldSession.WorldChangedSubscriberCount` stays the same over repeated shutdown and restart
  cycles (EditMode and PlayMode);
- the HUD refuses commands safely while no session is active.

### Accessing the session from a scene

1. Call `RealmkeeperRuntime.FindOrCreate()`.
2. Check `runtime.Host.IsActive`, then use `runtime.Session`.
3. Subscribe to `runtime.Host.SessionStarted` and `SessionEnded` if the object can outlive a session.
4. Subscribe to `runtime.ModeChanged` to react to the view switching.

Never create a `WorldSession` in a scene.

## 3. The command registry

- Commands are typed classes implementing `ICommand`. Handlers implement
  `ICommandHandler<TCommand>` and are registered per command type on a session's
  `CommandRegistry`.
- There are no strings, no reflection and no dynamic dispatch on names.
- Registering a second handler for the same type throws.

**Executing a command:** `session.Dispatch(new SomeCommand(...))` returns a `CommandResult`
with a `CommandStatus`:

| Status | Meaning |
|---|---|
| `Success` | The change was committed. |
| `ValidationFailed` | The command or its result is invalid. Nothing changed. |
| `NotFound` | A referenced ID does not exist. |
| `InvalidSessionState` | Wrong state: no world, session closed, already inside, or a re-entrant dispatch. |
| `Unsupported` | No handler is registered for this command type. |
| `Failed` | The handler threw. Nothing changed. |

**Atomicity (ADR-0008 per command):**
- A handler reads `context.Committed` and writes only to `context.Edit()`, a lazy deep copy.
- After the handler succeeds, the copy is validated with `WorldValidator`, and only then
  committed.
- A failure at any point leaves the committed world untouched. The EditMode test
  `FailingCommands_NeverPartiallyMutateTheWorld` checks this.
- Events reported with `context.ReportEvent` get deterministic IDs and are committed with the world.

**Registering commands in a module:**
1. Write the command and handler classes in the module's Application assembly.
2. Write a static `Register(WorldSession)` (see `ExplorationModule.Register`).
3. Call it from an `IRuntimeModule.ConfigureSession` (Unity) or directly (tests).
4. `RealmkeeperRuntime.RegisterModule` applies it to the active session and every later one.

The core never references the module.

**Commands in this slice:**

| Command | Persistent effect | Chronicled |
|---|---|---|
| `EnterSettlementCommand(SettlementId)` | Character location, region, arrival pose, last settlement | No |
| `ExitSettlementCommand` | Character back on the world map | No |
| `SetPlayerPoseCommand(SettlementId, LocalPose)` | Position and yaw; must lie inside that settlement | No |
| `SetCameraPerspectiveCommand` | Third or first person | No |
| `TalkToResidentCommand(NpcId)` | First-meeting date and conversation count | Yes, on first meeting |
| `PostLabourNoticeCommand(SettlementId, ProductionSourceType)` | Settlement labour focus, which doubles that source's labour weight from the next day | Yes |
| `SaveWorldCommand(slot)` | Writes the save | No |

## 4. Settlement ↔ local environment

- `LocalEnvironmentGenerator.Generate(seed, settlementId, regionId, type, residents)` returns
  an engine-free `LocalEnvironmentLayout`. It is deterministic: same inputs, same layout.
  It contains the gate and arrival point, streets, the plaza and market, houses, the
  enterable hall (with a small interior), the granary, a landmark tower, walls or fences,
  props, interaction points and one spot per named resident.
- The layout is keyed by `EnvironmentKey` = `settlement:<SettlementId>` and carries the
  `RegionId`. It is never saved.
- Nothing in the environment copies settlement state. Every number shown is read from the
  session (`FindSettlement`, `FindNpc`), and every change is a command.
- `LocalEnvironmentStreamer` (`Exploration.WorldStreaming`) creates one runtime scene per
  visit with `SceneManager.CreateScene`, and unloads it on exit. Loading a second environment
  while one is loaded is refused, so environments and residents cannot be duplicated.
- `ExplorationController` (`Exploration.Presentation`) is a pure observer:
  - when world data says the character is in a settlement, the environment is loaded;
  - otherwise it is unloaded;
  - after a save is loaded, the environment is rebuilt from the loaded world.
- The strategic presentation hides its camera, world view, sun, selection highlight and HUD
  in exploration mode. It keeps their state, and on return selects and focuses the
  settlement the character just left.

## 5. Exploration and saves

- Saves store, in `WorldData.Player` (schema v3):
  - character ID and name;
  - location kind, settlement and region;
  - `LocalPose` (x, y, z, yaw);
  - perspective;
  - last settlement.
- Named residents store their role, first-meeting date and conversation count.
- Settlements store their labour focus.
- The character's live position is presentation state until it is recorded. It is recorded by
  `SetPlayerPoseCommand` on save (`PreparingSave`), on perspective switch and on leaving. The
  recorded position is clamped to the settlement's bounds.
- Loading a save made inside a settlement puts the character back inside it, at the saved
  position and perspective.

## 6. One character, two perspectives

- There is one `PlayerCharacter`, a `CharacterController` (1.8 m tall, 0.35 m radius) with
  ground detection, gravity and jumping.
- `ExplorationCameraRig` has separate `PerspectiveSettings` for third person (orbit distance
  4 m, shoulder offset, sphere-cast obstruction) and first person (eye at 1.65 m, wider pitch
  range, body hidden).
- The strategic map keeps its own `IsometricCameraController`.
- Switching perspective changes only the camera, never the character's position. The
  PlayMode test `SwitchingPerspective_KeepsTheCharacterWhereItIs` checks this.
- Input goes through `IExplorationInput`:
  - `InputSystemExplorationInput` is one Input System action map ("Exploration"), rebindable
    without code changes;
  - `ScriptedExplorationInput` is for tests.

**Controls:**

| Key | Action |
|---|---|
| WASD or arrow keys | Move |
| Shift | Sprint |
| Space | Jump |
| Mouse | Look |
| V | Switch perspective |
| E | Interact |
| F5 | Save |
| M | Return to World Map |
| Esc | Free the cursor; click the view to capture it again |

## 7. Temporary parts (vertical slice only)

- **Art:** replaced by the graphics pass (2026-11-10). Buildings come from the CC0 modular kit, planned by
  `BuildingKitPlanner`; residents and the player are animated humanoids (ADR-0010 to ADR-0012,
  [Exploration](../Systems/Exploration.md)). The primitive builder remains as the fallback when the art
  catalog is missing.
- **Layout generator:** one plaza-centred plan for every settlement type, scaled by type.
  Version 1, with no authored content.
- **Region model:** one region per world.
- **Residents:** 4 to 6 per settlement, idle wandering only, no schedules or simulation. Dialogue
  is one generated line per role, built from real settlement data.
- **Gate:** closed (the environment is bounded). Interiors exist for the hall only.
- **Saving:** one fixed save slot (`autosave`) from both views.

## 8. Before scaling up

- Region-scale geography: several regions per world, per-region regeneration and saved terrain deltas.
- Entity-level change notifications instead of rebuilding on `Loaded`.
- Copy-on-write or per-system change sets for commands and days once NPC counts grow
  (ADR-0008 cost).
- Simulated NPC tiers, with promotion and demotion that conserve identity and population.
- Character inventory and conserving item transfers.
- Navigation for residents (kit art and animation are done; see ADR-0011 and ADR-0012).
- Time of day in the domain.
- Several save slots and a load menu.
