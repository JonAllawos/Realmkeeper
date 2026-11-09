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
