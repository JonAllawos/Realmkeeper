# Assembly dependencies

| Assembly | References | Unity engine | Notes |
|---|---|---|---|
| `Realmkeeper.Domain` | none | **No** (`noEngineReferences`) | Models, IDs, calendar, inventory, geography, validators, `IRandomSource` |
| `Realmkeeper.Simulation` | Domain | **No** | Scheduler, `ISimulationSystem`, food and wellbeing systems |
| `Realmkeeper.Application` | Domain, Simulation | **No** | `WorldSession`, commands, queries, playback, ports |
| `Realmkeeper.Infrastructure` | Domain, Simulation, Application, `Newtonsoft.Json.dll` | **No** | xoshiro RNG, `RegionGenerator`, JSON persistence, composition root |
| `Realmkeeper.Presentation` | Domain, Simulation, Application, Infrastructure | Yes | Empty in this phase |
| `Realmkeeper.Tests.EditMode` | Test runner, Domain, Simulation, Application, Infrastructure, `nunit.framework.dll`, `Newtonsoft.Json.dll` | Editor only | Pure NUnit tests; no Unity APIs used |
| `Realmkeeper.Tests.PlayMode` | Test runner, all runtime assemblies, `nunit.framework.dll` | Yes | Empty in this phase |

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

## Package dependencies

| Package | Used by | Why |
|---|---|---|
| `com.unity.render-pipelines.universal` | Presentation | Required renderer |
| `com.unity.test-framework` | Tests | Unity Test Framework (NUnit 3) |
| `com.unity.nuget.newtonsoft-json` | Infrastructure, EditMode tests | JSON persistence (ADR-0007) |

UI Toolkit is built into Unity 6, so it needs no package.
