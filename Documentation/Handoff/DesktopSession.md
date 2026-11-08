# Handoff: completing Phase 1 on a machine with Unity 6

The engine-independent core is finished and tested (see `Documentation/Testing/TestResults.md`).
This document covers the rest: the Unity project, Stage E (environment and camera),
Stage F (selection, inspector, UI) and Stage H (Unity tests and screenshots).

## 0. Prerequisites

- Unity Hub with a **Unity 6 LTS** editor (6000.0.x or later 6000.x). Record the exact
  version you pick in README.md and ADR-0001.
- Git. Optionally the .NET 8 SDK, to keep using `Tools/DotNet/verify.sh`.
- Claude Code running locally in the repository folder, so every stage can be compiled and
  tested.

## 1. Create the Unity project inside this repository

Unity Hub can't create a template project inside a folder that isn't empty. Do this instead:

1. `git clone https://github.com/JonAllawos/realmkeeper && cd realmkeeper && git checkout feature/phase-0-1-foundation`
2. In Unity Hub: **New project → Universal 3D** template, in a **temporary** folder outside
   the repository (for example `~/tmp/rk-template`).
3. Copy from the temporary project into the repository root:
   - `Packages/` (manifest.json, packages-lock.json)
   - `ProjectSettings/`
   - `Assets/Settings/` (the URP pipeline and renderer assets the template configured)
   Do **not** copy the sample scene, `Readme` or `TutorialInfo`.
4. In Unity Hub, choose **Add → Add project from disk** and select the repository root.
5. Install packages through Package Manager (or edit `Packages/manifest.json`):
   - `com.unity.nuget.newtonsoft-json`, required by `Realmkeeper.Infrastructure`
   - `com.unity.test-framework`, usually already present
   - `com.unity.inputsystem`, already present in the Unity 6 URP template
6. Set **Edit → Project Settings → Player → Active Input Handling** to **Input System Package**.
7. Commit `Packages/`, `ProjectSettings/`, `Assets/Settings/` and every generated `.meta` file.

### Likely first-import issues

| Symptom | Fix |
|---|---|
| `Newtonsoft.Json` not found in Infrastructure | The package isn't installed (step 5), or its DLL name differs. Check the package's `Runtime` folder and update `precompiledReferences` in `Realmkeeper.Infrastructure.asmdef` and `Realmkeeper.Tests.EditMode.asmdef` |
| `'Application' is a namespace but is used like a type` | Presentation code inside `Realmkeeper.*` must write `UnityEngine.Application` in full |
| `nunit.framework` not found | The test-framework package is missing |
| Test assemblies missing from Test Runner | `UNITY_INCLUDE_TESTS` is defined automatically by the test framework; check that the package is installed |

**Gate 1:** the console has no errors, and Test Runner → EditMode → Run All shows the same
61 methods / 75 cases passing. Update `TestResults.md` with that run.

## 2. Stage E: environment and camera

Presentation code goes in `Assets/Realmkeeper/Runtime/Presentation/` and stays thin. It reads
`WorldSession.Map` and query snapshots, and sends commands.

1. **Add the Unity references the Presentation `.asmdef` needs:** `Unity.RenderPipelines.Universal.Runtime`,
   `Unity.RenderPipelines.Core.Runtime`, `Unity.InputSystem`.
2. **`RealmkeeperBootstrap` (MonoBehaviour):** creates the session with
   `RealmkeeperComposition.CreateSession(Path.Combine(UnityEngine.Application.persistentDataPath, "Saves"))`,
   executes `CreateWorldCommand`, owns a `SimulationPlayback`, and calls
   `playback.Tick(Time.deltaTime)` in `Update`. This is the only connection between the
   frame loop and the simulation.
3. **`TerrainMeshBuilder`:** builds one mesh from `RegionMap`, split into 4×4 chunks of 32×32
   cells for culling. Use vertex colours blended by terrain type and height (grass, dry
   grass, rock, snow above a threshold, shore sand) and a single shared URP Lit material.
   Compute normals from the height field. The mesh has no colliders except a single
   `MeshCollider` for picking.
4. **Water:** one lake plane at `map.WaterWorldHeight` with a transparent URP material. The
   river is a ribbon mesh along `map.Rivers[0]` at bed height plus a small offset, about
   3–4 units wide.
5. **Forests:** GPU instancing (`Graphics.RenderMeshInstanced`) of 2–3 placeholder conifer
   and broadleaf meshes, positioned from forest cells with deterministic jitter (seeded from
   the cell coordinates). Skip road cells and settlement radii. Aim for 8–15k instances.
6. **Mountains:** come from the height field. Add rock tint and snow in the vertex colours.
7. **Roads:** a flattened ribbon mesh along each `RoadPath`, with packed-earth colour and
   small placeholder bridge meshes on `TerrainType.River` cells.
8. **Settlements:** a `SettlementView` per settlement keyed by `SettlementId`. Compose
   placeholder buildings (box body plus gable-roof prism) from a few prefabs:
   - Town: 25–40 buildings, a wall ring and a keep;
   - Village: 10–15 buildings and a small chapel;
   - Hamlet: 4–6 cottages and fields.
   Use a muted palette: limestone, timber brown, slate and thatch roofs.
9. **Lighting:** a warm low-angle directional light with soft shadows, a gradient
   environment, linear fog for depth, and a URP Volume with mild bloom, tonemapping (ACES or
   Neutral) and a slight colour grade. Avoid saturated, toy-like colours (specification §8).
10. **`IsometricCameraController`:** an orthographic camera on a pivot, pitched at about 35°
    and rotated in 90° steps (Q/E, with easing). Pan with WASD, arrow keys, middle-mouse drag
    and screen-edge (optional). Smooth zoom with the mouse wheel, by orthographic size within
    limits. Clamp the pivot to the region bounds. Keep the input-reading part separate from
    the motion maths, so the maths can be PlayMode-tested with synthetic input.

## 3. Stage F: selection, inspector, UI

1. **`SettlementSelection`:** raycast from the cursor, then `SettlementView` →
   `SettlementId`. Keep the selected ID (never a name) and show a highlight (an outline ring
   decal or emissive tint). Escape or clicking empty ground clears the selection.
2. **UI Toolkit:** one `UIDocument` with `RealmkeeperHud.uxml` and `.uss`:
   - Top bar: Realmkeeper wordmark · world name · date (`WorldDate.ToString()`) ·
     pause/play · +1 day · +1 week (7) · +1 month (30) · speed selector.
   - Left navigation: World (active) · Settlements (active: a list that selects a settlement
     by ID) · Factions, Characters, Adventures, Chronicle, World Editor, each disabled with a
     "Coming later" tag. No fake screens.
   - Right inspector, bound to `session.FindSettlement(selectedId)`: name, type, population,
     prosperity, stability (bars), food reserves with days remaining (`DaysOfFoodRemaining`),
     net food per day, shortage badge, resource list, faction.
   - Bottom activity area: `GetRecentEvents(8)` with severity colours, plus the simulation
     state (Playing/Paused, speed, days advanced).
   - Save and Load buttons (slot `"autosave"` for now) that show `CommandResult.Message`.
3. Refresh the UI from `WorldChanged`, never by polling every frame.

## 4. Stage H: PlayMode tests (8 or more) and screenshots

Suggested PlayMode tests (`Assets/Realmkeeper/Tests/PlayMode/`):

1. The bootstrap creates a world and spawns three `SettlementView`s with matching IDs.
2. The terrain mesh vertex count and bounds match the `RegionMap` size.
3. Camera pan moves the pivot and stays clamped at the region edge.
4. Camera zoom changes orthographic size smoothly and respects its limits.
5. Camera rotation snaps to 90° steps after easing completes.
6. Clicking a settlement's screen position selects that `SettlementId` and shows the highlight.
7. After selection, the inspector labels show that settlement's population and food.
8. Clearing the selection hides the inspector and removes the highlight.
9. The "+1 week" button advances the date by 7 days and refreshes the top-bar label.
10. Play/pause toggles `SimulationPlayback.IsPlaying`, and while paused the date does not change.

Screenshots: capture the running Game view (`ScreenCapture.CaptureScreenshot`) at
1920×1080. Include at least one overview and one shot with a selected settlement and the
inspector open. Store them in `Documentation/Screenshots/`.

## 5. Suggested opening prompt for the desktop session

> Continue Realmkeeper Phase 1 on branch `feature/phase-0-1-foundation`. Read README.md and
> Documentation/Handoff/DesktopSession.md. Unity 6 version is <fill in>. Do step 1 and stop
> at Gate 1 with the console and test results. Then do Stages E, F and H in that order,
> compiling and running tests between stages, and commit each stage separately.
