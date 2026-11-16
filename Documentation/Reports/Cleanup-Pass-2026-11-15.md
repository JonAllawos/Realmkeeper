# Cleanup, dead-code audit and performance pass

**Date:** 2026-11-15.

**Approach:** conservative. Remove only what is provably unused; measure before and after any performance change; delete no files the owner has asked to keep; report candidates rather than act on them.

**Baseline:** `3c61b01`. **Commits:** `ff08004` (code and tests), `433532a` (this report); pushed 2026-11-15 at the owner's instruction.

## 1. What changed

| Change | Why | Evidence |
|---|---|---|
| Removed packages `com.unity.ai.navigation`, `com.unity.collab-proxy`, `com.unity.multiplayer.center`, `com.unity.visualscripting` | Unused | No reference in any script, assembly definition, scene, prefab or asset. The scene's `NavMeshSettings` and `m_NavMeshLayer` fields are Unity's built-in AI module (kept), not the navigation package. Unity updated `packages-lock.json` itself |
| The player hub renders once per action | Every hub click rendered the whole hub, including its 3D preview, twice or three times | Measured below |

**The hub fix.**
- **Before:** a hub action ran its command. The world-change event then re-rendered the hub (`RefreshPlayerBody` → `ShowHub`), and the action rendered it again with its message.
- **Now:** the world-change refresh only marks the open hub stale.
  - An action's own render (in the same frame) clears the mark.
  - Otherwise the hub is rendered once at the end of the frame (`ExplorationController.LateUpdate`).
  - A world change with the hub open, such as time passing, still refreshes it.
- **Diagnostics added:** `ExplorationController.HubRenders` and `HubRenderMilliseconds` (cheap counters).

## 2. Measurements (editor, batch mode; `CleanupPerformancePlayModeTests`)

| | Before | After |
|---|---|---|
| Full hub renders for 10 equip / unequip clicks (counted over 2 frames each) | 23 (2–3 per click) | 11 (1 per click, plus one clock refresh) |
| Renders done by the click itself | 2 | **1** (asserted) |
| Hub render time, total for 10 clicks | 148.6 ms | 63.3 ms |
| Time per render | 6.5 ms | 5.8 ms (unchanged in kind) |

**Noted, not changed:** with the hub open and idle, the game clock re-renders it about twice a second (4 renders in 2 s, about 6 ms each). Skipping a render when nothing the hub shows has changed would need a signature of the hub's data. That is a larger change, so it is left as a candidate.

**Not measured:** a player build; GPU time.

## 3. Dead-code audit

- **Method:** every class, struct, enum and interface name in `Runtime` and `Editor` (1,378 types), counted across the project, the tests and the .NET harness.
- **Names found only once:** 4. None was removed:

| Type | Verdict |
|---|---|
| `ArtImportPostprocessor` | Called by Unity (asset import). Keep |
| `RandomExtensions` | Extension methods, used through their method names. Keep |
| `RkToastLabels` | Extension `SetBody`, used twice. Keep |
| `RegardDimension` | A documented enum in the social model (ADR-0059), unused in code. **Candidate:** remove only if the regard dimensions stay unmodelled; owner's call |

## 4. Files: preserved, not touched

- `Assets/InitTestScene37e5…` and `Assets/InitTestScene8aeb…` (untracked) with their meta files: preserved at the owner's instruction.
- `Assets/InitTestScenea160…` (tracked since Phase 4): a leftover test-runner scene. **Candidate for removal; owner's call.**
- 174 ignored paths, including the `dev-*.png` editor screenshots and the git-ignored `QuaterniusBestiary` and `QuaterniusFantasyRaces` folders (paid assets kept out of the repository): untouched.
- `Art/ThirdParty` is 447 MB. A per-file "is it referenced" audit of the art was not done (risky with catalog and Resources loading); a candidate for a later pass.

## 5. Tests

| Suite | Result |
|---|---|
| Unity PlayMode, full | **141 passed, 0 failed, 37 skipped** (capture-only tests; they run only with `RK_CAPTURE_DIR`), 1,025 s |
| Unity EditMode, full | **968/968**, 2,709 s |
| `CleanupPerformancePlayModeTests` | Passed (part of the full PlayMode run) |

This is a full checkpoint (the last was Phase 26.1: EditMode 954/954, PlayMode 139 passed, 36 skipped). The .NET harness was not rerun: no domain code changed, and EditMode covers the same tests in Unity.
- `CleanupPerformancePlayModeTests` (new): one render per click, the action's message kept, a world change still refreshes the open hub (deferred, not inside the command).
- **Full Unity PlayMode and EditMode suites:** run as a checkpoint, because the hub fix touches every hub screen and the package removal touches the whole project.
