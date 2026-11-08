# ADR-0003: Engine-independent core plus an out-of-Unity .NET harness

**Context.** Simulation and domain logic must not depend on MonoBehaviours, GameObjects or
`Update()`. Development may happen where no Unity Editor is available.

**Decision.** The four core assemblies set `noEngineReferences: true`. `Tools/DotNet` holds
SDK-style projects that compile the same source files (by glob, no copies) as C# 9 /
netstandard2.1, warnings treated as errors, with references that mirror the `.asmdef` files.
The EditMode tests are plain NUnit and run under both Unity and `dotnet test`.

**Consequences.** The core can be verified in seconds in CI or a cloud session, without a
Unity licence. Two build descriptions exist, so `.asmdef` and `.csproj` references must change
together. Passing the .NET build does not prove Unity compiles the code; Unity remains the
final check.

**Alternatives.** Unity-only testing: slow, and needs a licence and an editor install.
Separate DLLs built outside and imported as plugins: worse iteration and debugging in Unity.
