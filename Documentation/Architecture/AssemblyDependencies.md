# Assembly dependencies

| Assembly | References | Unity engine | Notes |
|---|---|---|---|
| `Realmkeeper.Domain` | none | **No** (`noEngineReferences`) | Models, IDs, calendar, inventory, geography, validators, `IRandomSource` |
| `Realmkeeper.Simulation` | Domain | **No** | Scheduler, `ISimulationSystem`, economy rules and the Phase 2 systems (labour, production, trade, consumption, wellbeing, population, chronicle) |
| `Realmkeeper.Application` | Domain, Simulation | **No** | `WorldSession`, commands, queries, playback, ports |
| `Realmkeeper.Infrastructure` | Domain, Simulation, Application, `Newtonsoft.Json.dll` | **No** | xoshiro RNG, `RegionGenerator`, JSON persistence, composition root |
| `Realmkeeper.Presentation` | Domain, Simulation, Application, Infrastructure, URP Runtime, Core RP Runtime, Input System | Yes | Strategic view: bootstrap, world visuals, camera, selection, UI Toolkit HUD |
| `Realmkeeper.Tests.EditMode` | Test runner, Domain, Simulation, Application, Infrastructure, `nunit.framework.dll`, `Newtonsoft.Json.dll` | Editor only | Pure NUnit tests; no Unity APIs used |
| `Realmkeeper.Tests.PlayMode` | Test runner, all runtime assemblies, `nunit.framework.dll` | Yes | World, camera, selection and HUD tests |

```
Domain ◄── Simulation ◄── Application ◄── Infrastructure
   ▲            ▲              ▲                ▲
   └────────────┴──────────────┴──── Presentation
```

There are no cycles. Infrastructure depends on Application (not the other way round)
because it implements Application's ports.

## How the rules are enforced

1. **Unity:** `.asmdef` references are explicit, `autoReferenced` is off for the test
   assemblies, and `noEngineReferences` is on for the four core assemblies.
2. **Outside Unity:** `Tools/DotNet/*/*.csproj` mirror the same references. A forbidden
   `using` fails `Tools/DotNet/verify.sh`, and the harness has no UnityEngine assembly to
   resolve against.

When an `.asmdef` reference changes, change the matching `.csproj` in the same commit.

## Planned exploration assemblies (not created)

Binding direction from [ADR-0009](../Decisions/ADR-0009-dual-perspective-world.md). Each
assembly is created only when the vertical slice needs it. The intended rules:

| Assembly | May reference | Unity engine | Must never be referenced by |
|---|---|---|---|
| `Realmkeeper.Exploration.Domain` | Domain | **No** (`noEngineReferences`) | Domain |
| `Realmkeeper.Exploration.Application` | Domain, Simulation, Application, Exploration.Domain | **No** | Domain, Simulation, Application |
| `Realmkeeper.Exploration.Interaction` | Application, Exploration.Application, Exploration.Domain, Input System | Yes | Any core or exploration engine-free assembly |
| `Realmkeeper.Exploration.WorldStreaming` | Application, Exploration.Application, Exploration.Domain, Domain | Yes | Any core or exploration engine-free assembly |
| `Realmkeeper.Exploration.Presentation` | Exploration.Interaction, Exploration.WorldStreaming, Exploration.Application, Exploration.Domain, Application, Domain, URP | Yes | Strategic `Realmkeeper.Presentation` (the two views stay siblings) |

- The existing core (Domain, Simulation, Application, Infrastructure) never references an
  `Exploration.*` assembly. Exploration plugs in through the composition root and application
  ports, as Infrastructure does today.
- The engine-free exploration assemblies join the `Tools/DotNet` harness when they are
  created, with mirrored `.csproj` files.
- Shared runtime hosting (one `WorldSession` across additive scenes) needs a small host that
  both presentations use. Where it lives is decided with the first exploration code.

## Package dependencies

| Package | Used by | Why |
|---|---|---|
| `com.unity.render-pipelines.universal` | Presentation | Required renderer |
| `com.unity.test-framework` | Tests | Unity Test Framework (NUnit 3) |
| `com.unity.nuget.newtonsoft-json` | Infrastructure, EditMode tests | JSON persistence (ADR-0007) |
| `com.unity.inputsystem` | Presentation | Camera and selection input |

UI Toolkit is built into Unity 6, so it needs no package. No package has been added for
exploration. Candidates such as Addressables or Cinemachine each need their own decision
record when first required (ADR-0009, point 7).
