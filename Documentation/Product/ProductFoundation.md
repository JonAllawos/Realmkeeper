# Realmkeeper product foundation

This document defines what Realmkeeper is and the product requirements that every phase
must respect. Requirements marked **binding** can only be changed by a new decision record
that explicitly supersedes them (see `Documentation/Decisions`).

Before this document existed, the product definition was spread across the README, ADR-0001
and the original Phase 0/1 specification (cited in code as "specification §8"). This document
consolidates those statements; they are unchanged in substance.

## 1. What Realmkeeper is

Realmkeeper is a **persistent fantasy world simulation and exploration platform**.

- A seeded medieval world lives on through deterministic daily simulation: settlements,
  economies, trade, factions, people and history.
- The user observes, manages, edits and simulates that world from an **overhead strategic
  perspective**, and (per Addendum A) can enter its settlements and explore the same places
  as a **playable character**.
- The world's history is recorded in a chronicle built from the simulation's own facts.

## 2. Established product principles

These come from Phases 0–2 and remain in force.

| # | Principle | Where it is enforced |
|---|---|---|
| P1 | **Independent project.** Realmkeeper shares no code, assets, packages or assemblies with any other project, including Living Realms and The Tavern Table. | README; repository contents |
| P2 | **The simulation is the source of truth.** Everything the user sees is derived from authoritative world data; nothing is faked for display. | ADR-0002, ADR-0003; Data ownership |
| P3 | **Deterministic and reproducible.** Same seed, configuration and commands → same world. | ADR-0004, ADR-0008 |
| P4 | **Stable identity.** Every entity has a persistent GUID-based ID; names are display data only. | Data ownership |
| P5 | **Gradual, explainable consequences.** No unexplained random swings; rules are documented and configurable. | Phase 2 economy (`EconomyRules`) |
| P6 | **Stylized-realistic, not toy-like.** Muted, believable colours and lighting. | ADR-0001; `Palette` |
| P7 | **Local and private.** Local saves; no multiplayer, cloud services, AI generation or monetization unless a future decision adds them. | ADR-0001 |

## 3. Visual identity

### Strategic world view (exists today)

- Stylized realistic presentation at large geographic scale.
- Readable terrain, settlements, roads and rivers; borders when factions gain territory.
- Attractive geographic composition (the diorama overview).
- Clear strategic information overlays (HUD, inspector, chronicle).

### Exploration view (future; Addendum A)

- More realistic and detailed 3D environments.
- Modular medieval architecture, detailed interiors and environmental props.
- Believable scale and lighting.
- Animated NPCs and creatures.
- First-person and third-person character perspectives.

---

## Addendum A: dual-perspective persistent world (binding)

Adopted 2026-11-09. Architectural record: [ADR-0009](../Decisions/ADR-0009-dual-perspective-world.md).
**Status: direction adopted, not implemented.** No exploration code, assemblies or
dependencies exist yet.

### A1. Product direction

The user can observe, manage, edit and simulate an expansive fantasy world from an overhead
strategic perspective, then enter settlements and explore those same locations as a playable
character. **Both perspectives represent the same underlying persistent world.**

### A2. Architectural requirements

| ID | Requirement |
|---|---|
| A2.1 | World simulation remains independent of Unity presentation. |
| A2.2 | Strategic and exploration modes consume the same authoritative world data. |
| A2.3 | All settlements, factions, NPCs, resources, events and locations use persistent stable IDs. |
| A2.4 | Local environments are associated with their corresponding settlement and region IDs. |
| A2.5 | Character movement and interactions update the shared world state through application-level commands. |
| A2.6 | The world simulation continues to function regardless of which camera mode is active. |
| A2.7 | Region-based world streaming and local environment loading. |
| A2.8 | Different levels of simulation detail for distant and nearby NPCs. |
| A2.9 | First-person and third-person exploration are both supported. |
| A2.10 | Character position, inventory, interactions and relevant world state are preserved when switching perspectives. |
| A2.11 | The simulation is never duplicated, and exploration mode never keeps its own copy of settlement data. |
| A2.12 | Persistence supports world state, character state and historical events. |

### A3. Exploration modules

Introduced as separate modules **only when their functionality is required**, integrating
with (not replacing) the existing architecture:

| Module | Purpose (intended) |
|---|---|
| `Exploration.Domain` | Engine-free models: character, character inventory and items, local environment descriptors, NPC detail tiers. |
| `Exploration.Application` | Engine-free commands and queries for exploration (move, enter, exit, interact, transfer), executed against the shared world session. |
| `Exploration.Interaction` | Unity-side translation of player input and in-world interactions into exploration commands. |
| `Exploration.WorldStreaming` | Unity-side region and local-environment loading and unloading. |
| `Exploration.Presentation` | Unity-side character, cameras (first and third person), environments and NPC visuals. |

Dependency rules for these modules are in [Assembly dependencies](../Architecture/AssemblyDependencies.md#planned-exploration-assemblies-not-created).

### A4. Development strategy: one vertical slice first

Before any large explorable world, deliver one complete vertical slice:

1. The existing overhead map.
2. One selected settlement.
3. One detailed, playable settlement environment.
4. One controllable character.
5. First-person and third-person camera switching.
6. A small group of persistent NPCs.
7. Basic NPC interaction.
8. Enter and exit transitions.
9. Shared simulation state verified across both perspectives.

**Success criterion:** actions taken in exploration mode produce persistent, verifiable
changes to the same world the strategic map shows. For example, an exploration action that
changes a settlement's stock is visible in the strategic inspector, survives save and load,
and appears in the chronicle. This must be verified by automated tests, not only by eye.

### A5. Scope protection

- Do not replace the existing simulation architecture to accommodate exploration.
- Do not prematurely build massive continents, hundreds of fully detailed settlements,
  multiplayer or cloud infrastructure.
- Preserve the existing Unity 6 project and all functioning systems.
- Realmkeeper remains completely independent of Living Realms and The Tavern Table (P1).
