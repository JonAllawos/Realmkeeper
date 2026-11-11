# Phase 10: performance

Every number here comes from a run that actually took place, on the machine described below. Where a number
was not measured, this document says so.

## Hardware and method

- **Machine:** see the "PERF machine" line in the scene measurements below (written by the capture run itself).
  The .NET numbers come from the same machine.
- **.NET:** .NET 8, Release build, `dotnet test` on the verification solution. Timings use `Stopwatch` inside
  temporary diagnostic tests:
  - a warm-up run first;
  - simulation timed over 60 simulated days;
  - serialisation averaged over 5 runs;
  - routes averaged over 100.
  The diagnostics were deleted before the commit. The committed tests keep the budgets (below).
- **Unity:** 6000.0.47f1, editor play mode (not a player build), 1920×1080, from `Phase10CaptureTests.Capture4_Performance`:
  - **Build time:** from the "Enter settlement" command until the environment exists.
  - **Scene size:** renderers after merging, triangles, lights and colliders.
  - **Frame time:** 180 frames after 30 warm-up frames, at three fixed poses in the town.

## Simulation (per simulated day, reference world, .NET)

| World | ms per day |
|---|---|
| Before the living world (Phase 9 data only) | 0.49–0.58 |
| Phase 10, first measurement | 13.7 |
| Phase 10, after the fix below | **1.88** |
| Phase 10 without NPC sheets (to isolate them) | 0.89 |

- **Bottleneck found and fixed.** The scheduler validates the whole world twice per simulated day (input and
  output) and deep-copies it once. Plans made each day cost about 12 ms: every copy cloned them, and every
  validation re-checked them.
  - **The fix:**
    - plans are immutable in play, so copies share them;
    - each plan's check is cached per plan instance against a content stamp (linear in the plan's size), so a
      changed plan is checked again.
  - **Before the fix:** Unity's `NpcPerformanceTests.DefaultWorld_15People` and
    `CommercePerformanceTests.DefaultWorld` failed their 15 ms/day budget (26.7 and 23.8 ms).
  - **After the fix:** both pass in the Unity EditMode run.
- **What remains.** About 1 ms/day of the remaining cost is the validation of the nine NPC sheets, the same
  check the player's sheet gets.
- **Fauna.** One week of fauna takes 0.13 ms. It runs weekly.

## Generation and persistence (reference world, .NET)

| Measure | Result |
|---|---|
| World generation, living world included | 275 ms |
| One settlement plan (slowest of 120, 40 seeds) | 86 ms |
| All three plans of the reference world | under 2 s (asserted by `Generating_IsQuickEnough`); about 90 ms in practice |
| Save size | 488 KB (148 KB for the same world before Phase 10) |
| Plans in the save | 23 KB (hamlet), 39 KB (village), 44 KB (town) |
| NPC records in the save (19 people, 9 sheets) | 65 KB |
| Creature populations (128) / herds (20) | 34 KB / 4 KB |
| Serialise | 21 ms |
| Deserialise and validate | 36 ms |
| Validate only | 6 ms |

## Navigation (.NET)

| Settlement | Street segments | First route (builds the graph) | Later routes |
|---|---|---|---|
| Town | 337 | 4.0 ms | 0.010–0.032 ms |
| Village | 261 | 0.2 ms | 0.030 ms |
| Hamlet | 94 | 0.1 ms | 0.032 ms |

## Scene (Unity editor play mode)

From `Phase10CaptureTests.Capture4_Performance` (two runs). Machine: Intel Core i9-14900HX (32 threads), 32 GB, NVIDIA
GeForce RTX 4070 Laptop GPU (Direct3D 11), Windows 11 Pro; Unity editor play mode, 1920×888 game view.

| Measure | Bramhaven (town) |
|---|---|
| Entering: command to environment built (plan to scene, residents included) | 351–606 ms |
| Renderers after merging | 1,186 (draw budget 1,500) |
| Triangles | 3.08 M |
| Lights (night lamps capped) | 54 |
| Colliders (terrain chunks, buildings, fences, walls, bridges, props) | 357 |
| Frame time at the arrival (mean / 95th percentile) | 4.6–6.0 / 5.2–6.4 ms |
| Frame time in the square | 3.4–3.7 / 3.8–4.2 ms |
| Frame time from the keep | 4.5–4.7 / 4.6–5.6 ms |

Not measured:
- frame time in a standalone player build;
- the performance probe (`-rkPerfProbe`), whose fixed poses still describe the older two-street town;
- memory in a long play session.

## Known bottlenecks and budgets

- **Validation dominates the simulated day.** It runs twice per day by design (ADR-0008). Sheet validation will
  grow with the number of sheet holders.
- **Lights at night.** In a planned town, night lamps are capped at 32 street lamps and 24 building fronts.
- **Plan generation.** Its cost grows with buildings × roads (plot search checks every road segment). A city
  would need spatial indexing.
- **Budgets enforced by tests:**
  - 15 ms per simulated day for the default world (Unity);
  - 40 ms for about 100 people; 120 ms for about 300 people;
  - under 2 s for three plans;
  - the draw budget of 50–1,500 renderers after merging, in a planned town (`GraphicsPlayModeTests`).
