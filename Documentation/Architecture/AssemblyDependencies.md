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

## Hosting and exploration assemblies (ADR-0009)

Created for the ADR-0009 prerequisites and vertical slice
(see [Dual-perspective architecture](DualPerspective.md)).

| Assembly | References | Unity engine | Must never be referenced by |
|---|---|---|---|
| `Realmkeeper.Hosting` | Domain, Simulation, Application, Infrastructure | Yes | Any engine-free assembly |
| `Realmkeeper.Exploration.Domain` | Domain | **No** (`noEngineReferences`) | Domain |
| `Realmkeeper.Exploration.Application` | Domain, Simulation, Application, Exploration.Domain | **No** | Domain, Simulation, Application, Infrastructure |
| `Realmkeeper.Exploration.Interaction` | Domain, Application, Exploration.Domain, Exploration.Application, Input System | Yes | Any engine-free assembly |
| `Realmkeeper.Exploration.WorldStreaming` | Domain, Application, Exploration.Domain, Exploration.Application | Yes | Any engine-free assembly |
| `Realmkeeper.Exploration.Presentation` | Domain, Simulation, Application, Hosting, Exploration.Domain, Exploration.Application, Exploration.Interaction, Exploration.WorldStreaming, Input System, URP, Core RP | Yes | Strategic `Realmkeeper.Presentation` (the two views stay siblings) |

- `Realmkeeper.Presentation` (strategic) additionally references `Realmkeeper.Hosting`, to
  use the shared runtime, and `Realmkeeper.Exploration.Application`, for the "Enter settlement"
  command. It does not reference any exploration Unity assembly.
- The core (Domain, Simulation, Application, Infrastructure) references no `Exploration.*`
  assembly. Exploration registers its command handlers on the session through
  `IRuntimeModule` (`ExplorationInstaller`).
- `Exploration.Domain` and `Exploration.Application` are mirrored in the .NET harness
  (`Tools/DotNet/Realmkeeper.Exploration.*`) and covered by the EditMode suite.
## Package dependencies

| Package | Used by | Why |
|---|---|---|
| `com.unity.render-pipelines.universal` | Presentation | Required renderer |
| `com.unity.test-framework` | Tests | Unity Test Framework (NUnit 3) |
| `com.unity.nuget.newtonsoft-json` | Infrastructure, EditMode tests | JSON persistence (ADR-0007) |
| `com.unity.inputsystem` | Presentation, Exploration.Interaction, Exploration.Presentation | Camera and selection input; exploration action map |

UI Toolkit is built into Unity 6, so it needs no package. No package has been added for
exploration. Candidates such as Addressables or Cinemachine each need their own decision
record when first required (ADR-0009, point 7).
