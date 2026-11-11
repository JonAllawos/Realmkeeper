# Known untested areas

| Area | Risk | How to close |
|---|---|---|
| Newtonsoft under Unity (Mono / IL2CPP) | Low with Mono. With IL2CPP, code stripping can remove DTO constructors and property setters | Add `link.xml` preserving `Realmkeeper.Infrastructure.Persistence`, or `[Preserve]`, before the first IL2CPP build |
| Cross-runtime float reproducibility of generation | Medium. The same seed may produce slightly different terrain on Mono, IL2CPP or ARM | Commit a golden map fingerprint for 3 seeds and assert it in every runtime |
| `File.Replace` on all platforms | Low on desktop. Unsupported on WebGL (no real filesystem) | Platform-specific store if WebGL is ever a target |
| Device input (mouse, keyboard, edge pan) | Low. PlayMode tests bypass `CameraInputReader` and the Escape and click handlers | Input System test fixtures (`InputTestFixture`) with virtual devices |
| Load button round trip in play mode | Low. Load is tested in EditMode via `WorldSession`; the button path was not exercised | A PlayMode test using a temporary save directory |
| Rendering look and performance | Visual quality is checked by eye; no frame-time budget exists | Profile on target hardware; add a frame-time smoke test |
| Shaders in player builds | Low. Shaders are referenced by the scene's bootstrap; `Shader.Find` is only a fallback | Make a standalone build and check the console |
| Very long histories (10,000+ events) | Performance only: each day deep-copies the settlement lists, and event lists are copied by reference | Profile once history pruning or chronicle paging exists |
| Concurrent access to `WorldSession` | Not supported by design | Keep all calls on Unity's main thread |
| Generator relaxation fallback (spacing below 34 cells) | Exercised only if some seed needs it; no test forces it | Add a test with a tiny map setting |

## Phase 8 (combat and structures)

| Area | Risk | How to cover it |
|---|---|---|
| Ground markers and labels in a player build | Low. Primitives and the built-in runtime font, checked only in captures | Make a standalone build and look |
| AI with a resident on the hostile side | Only creatures and allied guards were exercised in play; the engine path is shared and unit-tested | Add a scripted encounter with a hostile resident |
| Very long fights (log over 500 entries) | Bounded by design and validated; no test plays one that long | Add a test that trims the log |
| Frame time with the encounter panel open | Not profiled | Run the performance probe in a development build |
| Free mouse-driven play (clicks on markers in the 3D scene) | Moves are made on the panel's grid only; the markers are not clickable | Decide in a later phase whether to add picking |

## Phase 10 (NPC sheets, bestiary, reactions, encounters, settlement plans)

| Area | Risk | How to cover it |
|---|---|---|
| Frame time in a player build of a planned town | Measured in the editor's play mode only (Phase10/Performance.md) | Run the performance probe in a development build (its poses need updating for planned towns) |
| Very large plans (a city) | Cities are not generated yet; the generator's cost grows with buildings × roads | Add a city type and profile it |
| Reactions with several questions in one action | Covered by design (each answer replays); no test asks three questions in one action | Add an engine test with Shield, then Uncanny Dodge, in one attack sequence |
| Walking into a riverbed | Water has no collider; harmless, but not tested in play | Decide whether rivers block, and test it |
| Plans from very old saves (v1–v4) | Tested from v11 (and older documents migrate through v11) | Add a v5 document to the upgrade test |
