# Realmkeeper

Realmkeeper is a persistent fantasy-world simulation: a seeded medieval region that
lives on through deterministic daily simulation and is presented as a stylized-realistic
isometric 3D world.

This repository is an independent project. It shares no code, assets, packages or
assemblies with any other Unity project.

> **Status: Phase 0/1 foundation complete.**
> The engine-independent core (domain, simulation, generation, persistence, application
> services) is tested inside and outside Unity (75 EditMode cases). The Unity project has
> the 3D world, the isometric camera, settlement selection and the UI Toolkit HUD, covered
> by 11 PlayMode tests. See [Documentation/Testing/TestResults.md](Documentation/Testing/TestResults.md).

![Realmkeeper overview](Documentation/Screenshots/realmkeeper-overview.png)

## Unity version

**Unity 6000.0.47f1** (Unity 6.0 LTS line) with the Universal Render Pipeline.
`ProjectSettings/` and `Packages/` are copied from a Unity-generated *Universal 3D*
template project created with that version (see the handoff document); they are not
hand-written.

## What works today

| Area | State | Evidence |
|---|---|---|
| Seven assembly definitions with enforced dependency rules | Implemented | Mirrored by the .NET harness; compiled by Unity 6000.0.47f1 |
| Domain models (world, settlements, resources, calendar, IDs, geography) | Implemented | Compiled and tested (.NET 8, C# 9, netstandard2.1) |
| Deterministic daily simulation (scheduler + food rule + wellbeing rule) | Implemented | Tested |
| Seed-based region generation (terrain, forest, mountains, lake, river, 3 settlements, roads) | Implemented | Tested across 12 seeds |
| Versioned local JSON save/load | Implemented | Tested |
| Application commands, queries and real-time playback pacing | Implemented | Tested |
| Unity scene, 3D terrain rendering, isometric camera (Stage E) | Implemented | PlayMode tests (world, terrain, pan, zoom, rotate) |
| Settlement selection, inspector, UI Toolkit interface (Stage F) | Implemented | PlayMode tests (select, inspector, clear, +1 week, play/pause) |
| PlayMode tests (Stage H) | Implemented | 11 passing in Unity 6000.0.47f1 |

## Repository layout

```
Assets/Realmkeeper/
  Runtime/
    Domain/          Realmkeeper.Domain          data models, IDs, calendar, geography, validation
    Simulation/      Realmkeeper.Simulation      scheduler and daily systems
    Application/     Realmkeeper.Application     commands, queries, WorldSession, ports
    Infrastructure/  Realmkeeper.Infrastructure  RNG, generation, JSON persistence, composition root
    Presentation/    Realmkeeper.Presentation    bootstrap, world visuals, shaders, lighting, isometric camera,
                                                 selection, UI Toolkit HUD (UI/Resources: UXML, USS, theme)
  Scenes/            Realmkeeper.unity           the single scene (everything else is built at runtime)
  Tests/
    EditMode/        Realmkeeper.Tests.EditMode  61 test methods / 75 cases
    PlayMode/        Realmkeeper.Tests.PlayMode  11 tests: world, camera, selection, HUD
Tools/DotNet/        Out-of-Unity build and test harness (mirrors the .asmdef graph)
Documentation/       Architecture, systems, testing, decisions, handoff
```

## Setup

### Verify the core without Unity (any OS with the .NET 8 SDK)

```bash
Tools/DotNet/verify.sh
```

This compiles Domain, Simulation, Application and Infrastructure at Unity's language level
(C# 9, .NET Standard 2.1), with warnings treated as errors, and runs every EditMode test
under NUnit 3. Results are written to `Tools/DotNet/TestResults/`.

### Open in Unity

The Unity project still has to be created. Follow
[Documentation/Handoff/DesktopSession.md](Documentation/Handoff/DesktopSession.md).

## Known limitations

- Nothing has been compiled or run inside the Unity Editor yet. Expect a few small fixes the
  first time Unity imports the scripts (see the handoff document for the likely ones).
- Generation uses single-precision floats. Results are reproducible on one platform, and
  very likely on all IEEE-754 x64/ARM64 runtimes, but this has not been verified across
  Mono, IL2CPP and CoreCLR.
- Terrain is not saved: it is rebuilt from seed and generator version on load. A save made
  with a different generator version is refused, not migrated.
- One region of 128 × 128 cells, three settlements, two factions and no NPCs.
- The simulation rules are intentionally minimal: food production and consumption, shortage
  and starvation, and slow stability, prosperity and population drift.

## Documentation

- [Architecture overview](Documentation/Architecture/Overview.md)
- [Assembly dependencies](Documentation/Architecture/AssemblyDependencies.md)
- [Simulation lifecycle](Documentation/Architecture/SimulationLifecycle.md)
- [Data ownership](Documentation/Architecture/DataOwnership.md)
- Systems: [World generation](Documentation/Systems/WorldGeneration.md) ·
  [Simulation scheduling](Documentation/Systems/SimulationScheduling.md) ·
  [Persistence](Documentation/Systems/Persistence.md)
- Testing: [How to run](Documentation/Testing/RunningTests.md) ·
  [Results](Documentation/Testing/TestResults.md) ·
  [Untested areas](Documentation/Testing/UntestedAreas.md)
- [Architectural decision records](Documentation/Decisions/README.md)
