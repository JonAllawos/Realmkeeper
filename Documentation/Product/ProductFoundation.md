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

## Status note (Phase 8, 2026-11-11)

This note does not change any requirement.
- **Exploration.** Addendum A's exploration has been implemented since this document was written:
  the line above saying "not implemented" describes the time of adoption.
- **RPG systems.** Phases 6–8 have added systems from the Game Design Bible: character creation,
  progression and, in Phase 8, turn-based combat inside the exploration view.
- **Binding requirements.** These systems respect them:
  - one persistent, deterministic world model;
  - every change goes through application commands;
  - no state lives only in the scene.
- **Open owner decision.** Whether the Bible supersedes parts of §1 is still for the owner to
  decide (see `GameDesignBible.md`).
- **Phase 10 (2026-11-11).** Phase 10 adds more Bible systems:
  - NPC character sheets, a bestiary and aggregate wildlife;
  - reaction prompts and world-drawn encounters;
  - persisted settlement plans that the walkable settlement, the strategic map and the inspector all read.
  These respect the binding requirements above: one world model, changes through commands, nothing only in the
  scene, older saves upgraded once. The scope difference between this document's §1 and the Bible is still
  unresolved. Phase 10 records it and does not settle it; the decision remains the owner's.
- **Phase 11 (2026-11-11).** Phase 11 adds the Bible's item systems (§10):
  - item instances with their own state;
  - containers and the player's chest;
  - loot filled once per fight;
  - magic items acting through the rules engine;
  - crafting, gathering and repair as work in world time;
  - merchants that keep particular goods and mend.
  The same binding requirements hold. Every item has one holder, every change is a validated command, the presentation only reads snapshots and asks, and v12 saves upgrade once (schema v13). The scope question above remains the owner's.
- **Phase 12 (2026-11-11).** Phase 12 adds the Bible's quest, dialogue and reputation systems:
  - quests as templates bound to the generated world's own people and places;
  - objectives advanced by events from validated commands, in the same transaction;
  - data-driven dialogue with rolled skill checks;
  - reputation in separate dimensions, where deeds spread only through the people who hear of them;
  - activities that arise from the settlement simulation;
  - a journal that shows only what the player knows.
  The binding requirements still hold: one world model, every change a command, nothing only in the scene, and older saves upgraded once (schema v14). No campaign canon is fixed. The scope question above remains the owner's.
- **Phase 13 (2026-11-11).** Phase 13 adds places beyond the streets:
  - persistent dungeons, ruins, caves, a fort and building interiors, all one architecture in the world's hierarchy;
  - generated layouts that are validated and then saved, never regenerated;
  - one interaction framework with rules checks, fair traps and hazards;
  - fights with a place's own inhabitants among its walls, using the one combat engine;
  - rescue and escort of a real resident;
  - discovery from rumour to map marker.
  The binding requirements still hold: one world model, every change a command, the scene rebuilt from the world, and older saves upgraded once (schema v15). No campaign canon is fixed.
- **Phase 15 (2026-11-12): a recorded conflict, for this feature only.** Phase 15 adds the Bible's companions and
  party (§11.2–11.3), as the roadmap now directs:
  - named people recruited from the world, with refusals that state their cause;
  - an active line-up and a reserve, follow and wait, formation and leader;
  - companions' own sheets and items;
  - fights beside the player through the one combat engine, with a companion layer of the one AI that explains itself;
  - loyalty moved only by real events;
  - one personal quest;
  - resting together.

  This document's §1 describes a simulation and exploration platform, not a party RPG. For companions and the
  party, the Bible's direction is followed and this document's narrower statement is treated as superseded **for
  this feature only**. The rest of §1 is not rewritten, and the general scope question remains the owner's. The
  binding requirements still hold: a companion is the world's own persistent person, not a copy; every change is a
  validated command; the scene is rebuilt from the world; and v16 saves upgrade once (schema v17). Solo play stays
  complete: nothing in the world or its quests requires a party.
- **Phase 16 (2026-11-12).** Phase 16 widens the world, which §1 and Addendum A already describe as a persistent
  simulation, from one region to six connected ones. It adds:
  - regions made by the one generator, with borders that agree with each other;
  - a persistent route graph;
  - journeys on the one clock that end in the town;
  - caravans that carry the economy's own goods;
  - outlaws, patrols and refugees on the roads;
  - world events with causes, a lifecycle and consequences applied once;
  - news that reaches the player through people;
  - simulation tiers;
  - a world atlas that shows only what the player knows.

  It stays inside the established principles. One world model; every change a command; the scene and the atlas
  rebuilt from the world; and v17 saves upgraded once (schema v18). An old world keeps its single region until the
  player chooses to discover the lands beyond. The new regions use the generator's existing name and terrain rules,
  so no campaign canon is written. War between kingdoms, weather, mounts and ships remain out of scope.
