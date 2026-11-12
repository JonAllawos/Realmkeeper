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

## Phase 16 (connected world, travel, groups, world events)

| Area | Risk | How to cover it |
|---|---|---|
| "Discover the lands beyond" from the atlas button | The scene starts a six-region world, so the button (shown only in a one-region world) is not clicked in PlayMode; the command is tested in .NET from a v17 save | A PlayMode test that loads a one-region save and clicks it |
| Long play (months) across six regions | Days are measured (about 17 ms each) and events bounded (60 closed kept), but no test runs a simulated year in PlayMode | A soak test of a simulated year with save/load every month |
| A road fight's ground (Phase 17) | Fights met on the road are now played in the combat scene (tested through the panel), but the ground shown is the near settlement's land, not the stretch of road | A generated road scene for the encounter's own map cells |
| Groups outside the player's region | Simulated and validated, but not drawn; a drawing bug there would not show | Draw a neighbour's groups when that layer is on, and capture it |
| Frame time of the atlas window in a player build | Measured as query time (0.13 ms) in .NET; painting not profiled | Profile the atlas in a development build |
| Recordings of travel, caravans, disruptions and events | Only still captures were made | Record with the Unity Recorder in an editor session |
| Campaign decisions made from the journal (no speaker) (Phase 17) | Supported and refused correctly for spoken decisions (tested), but the demonstration has none, so the journal's choice buttons are tested only with spoken decisions (disabled with who will ask) | Author a journal decision in the next campaign content and test it through the journal |
| A hand-played Phase 17 session; gameplay video (Phase 17) | Every flow is checked by automated tests driving the real UI, not by a person; no video was recorded (the batch test runner cannot) | A recorded, hand-played session of the Waystone campaign and a road fight |
| Story content variety (Phase 17) | Two kinds of world story and one provisional campaign; the writing was reviewed, but by the author | Owner review of the text; more story kinds once canon is decided |
| Companion second story in generated worlds (Phase 17) | It needs a companion at loyalty 70 with a bond to someone living elsewhere; generated people's bonds are mostly within their own town, so it may be rare in play (tested with a bond made for the test) | Let the simulation create bonds across towns (travel, trade, marriage) |

## Phase 18 (places, discoveries, chains, stories, natures, companions, creature models)

| Area | Risk | How to close |
|---|---|---|
| Hand play of the four places, the chains and the stories | Medium. Every rule is covered by code tests, and the bells and journal by a UI-driven PlayMode test, but nobody has played them. Pacing and clarity are unjudged. | `Testing/Phase18-Owner-Playtest-Checklist.md` |
| Companion formation, waiting, regrouping and transitions | Low to medium. Code tests only; the water rule was found by inspection | Owner checklist, scenario D |
| Overheard exchanges on screen over a long session | Low. Exchanges are shown only where a real pair stands within earshot; how often that happens in play is unmeasured | Watch a town through a day |
| Creature models in every fight setting (narrow rooms, night, many foes) | Low. Size, facing and materials were checked in one line-up capture and one place fight | Fight each creature kind once |
| Frame time with many animated creatures | Not profiled | Run the performance probe in a development build |
| Very long games with lasting faction conflicts | Low. Weekly keys are now bounded per period (tested); event history notes are bounded as before | A 5-year soak test |
