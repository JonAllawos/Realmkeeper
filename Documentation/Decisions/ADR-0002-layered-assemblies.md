# ADR-0002: Layered assemblies, with ports in Application

**Context.** The specification requires Domain, Simulation, Application, Infrastructure and
Presentation assemblies, places persistence and generation in Infrastructure, and forbids
circular references.

**Decision.** Domain ← Simulation ← Application ← Infrastructure, with Presentation on top.
Application declares the interfaces it needs (`IWorldGenerator`, `IWorldStore`), and
Infrastructure implements them. `RealmkeeperComposition` (Infrastructure) is the only place
that creates concrete types. The `IRandomSource` interface lives in Domain so Simulation can
use randomness without referencing Infrastructure.

**Consequences.** Application can be tested with fake ports. Persistence and generation can
be swapped without touching the simulation. Cost: the namespace `Realmkeeper.Application`
hides `UnityEngine.Application` inside `Realmkeeper.*` namespaces, so presentation code has
to write `UnityEngine.Application` in full.

**Alternatives.** Infrastructure depending only on Domain, with Application calling concrete
classes: that ties Application to Newtonsoft and file IO. Ports in Domain: that drags
application concerns (load results, generation requests) into the model.
