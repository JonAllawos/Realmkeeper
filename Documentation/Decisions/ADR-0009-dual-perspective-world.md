# ADR-0009: Dual-perspective persistent world (strategic map + exploration)

**Status.** Accepted as binding direction, 2026-11-09. Not implemented. Product requirements:
[Product foundation, Addendum A](../Product/ProductFoundation.md#addendum-a-dual-perspective-persistent-world-binding).

**Context.** Realmkeeper will let the user switch between the overhead strategic map and
first- or third-person exploration of the same settlements. The risk is that exploration
grows a second world: its own NPC copies, its own settlement stock, its own clock. Then the
two views drift apart and saves can no longer describe one world.

**Decision.**

1. **One world, one session.** `WorldData`, owned by `WorldSession`, stays the only
   authoritative state. Exploration is a second *client* of the same session, beside the
   strategic presentation, not a second simulation.
2. **One way to change state.** Exploration actions (move, enter or exit a settlement, talk,
   take or give goods) are application-level commands executed by the shared session. They
   go through the same copy → validate → commit path (ADR-0008) and the same conservation
   rules as trade, and are recorded in the chronicle.
3. **Exploration data is keyed, not copied.** Local environments, NPC instances and
   character state refer to `SettlementId`, a future `RegionId`, `NpcId` and new IDs. Views
   read snapshots and render them, exactly as `SettlementView` is keyed by `SettlementId` today.
4. **The simulation clock is independent of camera mode.** `SimulationPlayback` keeps
   advancing whole days whichever view is active. Exploration may add finer-grained time
   (time of day) in the domain, never in Unity frame time.
5. **Level of detail is a simulation concept.** Distant NPCs are simulated in aggregate
   (as today's settlement population is); nearby NPCs as individuals. Promotion and demotion
   between tiers are explicit, deterministic and conserve identity and counts.
6. **New code arrives as new modules.** They are `Exploration.Domain` and `Exploration.Application`
   (engine-free), plus `Exploration.Interaction`, `Exploration.WorldStreaming` and
   `Exploration.Presentation` (Unity). Each is added only when the vertical slice needs it,
   and none is depended on by the existing core.
7. **No new dependencies until required.** Streaming starts with Unity's built-in scene
   loading. Packages such as Addressables, Cinemachine or animation tooling need their own
   decision record when first needed.

**Consequences.** The existing core needs no rewrite. It does need extending: regions,
character state, item inventories, finer-grained change notifications, and an extensible
command path (see "Expected changes"). The vertical slice is the proof: one action taken in
exploration must be visible on the strategic map, survive save and load, and appear in the
chronicle.

## What already supports this direction

| Existing decision | How it supports the dual-perspective world |
|---|---|
| ADR-0002 layered assemblies, ports in Application | Exploration modules can sit beside Presentation and reuse Application ports; the core never needs to know they exist. |
| ADR-0003 engine-independent core | Requirement A2.1 is already enforced by `noEngineReferences` and the .NET harness. |
| ADR-0004 per-system random streams | New exploration systems get their own streams without changing existing outcomes. |
| ADR-0007 versioned saves with migrations | Character state and exploration data can be added as schema v3+ with a migration, as v2 did for the economy. |
| ADR-0008 copy → validate → commit | Exploration commands get the same atomicity: a failed action leaves no partial change. |
| Stable GUID IDs (Data ownership) | `SettlementId`, `FactionId`, `NpcId`, `EventId`, `TradeRouteId` and `ShipmentId` already exist; local environments can key on them (A2.3, A2.4). |
| Single command path; immutable snapshots | A2.5 and A2.11: the strategic UI already never holds mutable domain objects. |
| `SimulationPlayback` driven by the bootstrap, not the camera | A2.6: the clock already ignores camera state. |
| Phase 2 economy and trade in the domain, with conservation checks | Character trade with settlements can reuse the same inventories, validation and conservation rules. |
| Structured events plus `EventFormatter` | Exploration events (met, traded, entered) are just new event types. |
| `NpcData` with `HomeSettlementId` and `FactionId` | Persistent NPC identity is already modelled, though not yet simulated. |
| `SettlementView` keyed by `SettlementId` | The pattern for exploration environments keyed by settlement. |

## Expected changes (when implementation begins)

| Area | Today | Change needed | Reason |
|---|---|---|---|
| Regions | One region per world; no `RegionId`; `WorldData` has no region list | Add `RegionId` and region records; key settlements, roads and local environments by region | A2.4, A2.7 |
| Geography persistence (ADR-0005) | The whole map is regenerated from the seed on load | Per-region regeneration, plus saved deltas for changes made in play (terrain edits, built or destroyed structures) | A2.7; exploration changes must persist |
| Local environments | None | Deterministic generation from (seed, settlement ID, generator version) or authored prefabs, keyed by ID; any in-environment changes saved as data | A2.4, A2.11 |
| Time | Whole days only (`WorldDate`) | Optional time of day in the domain; a rule for how exploration time relates to day ticks | A2.6, A2.10 |
| NPCs | `NpcData` exists but none are generated or simulated; population is an integer | Named NPCs as a tracked subset of population, with detail tiers and a conservation rule between aggregate and individuals | A2.8 |
| Character | None | Character state (position, region or environment, inventory, perspective) inside `WorldData` and saves | A2.10, A2.12 |
| Inventory | Settlement-level `ResourceInventory` only | An item model for characters; transfers to and from settlements as validated, conserving commands | A2.5, A2.10 |
| Commands | `WorldSession.Execute` has one overload per command type | A registry of command handlers, so `Exploration.Application` adds commands without editing `WorldSession` | Module independence (A3) |
| Change notification | `WorldChanged` with a coarse reason | Entity-level change sets (which settlements, NPCs or regions changed), so streamed environments update only what changed | A2.7 performance |
| Per-day cost (ADR-0008) | Full deep clone and full validation each day | Copy-on-write or per-system change sets once NPC counts reach the thousands | A2.8 scale |
| Unity scene structure | One scene; the bootstrap owns the session and builds everything | A persistent runtime object owning the session across additive scene loads; strategic and exploration presentations attach to it | A2.2, A2.10 |
| Input | The Input System is read directly in readers | Input action maps per mode (strategic, exploration first/third person) | A2.9 |
| Rendering | Vertex-colour stylized shaders | Standard URP Lit/PBR materials for exploration art (still URP; no pipeline change) | Visual identity: exploration |
| Save size | One JSON file per world | Possibly region-chunked saves once worlds are large | A2.7, A2.12 |
| IDs for runtime-created entities | Deterministic streams per day and system | Deterministic ID streams for command-created entities, so command replay reproduces the same IDs | P3 determinism |

**Alternatives considered.**
- *A separate exploration world synchronized with the strategic world:* rejected. It breaks
  A2.11, and synchronization bugs would corrupt saves.
- *Running exploration as the "real" simulation and deriving the strategic view from it:*
  rejected. It replaces the existing architecture (A5) and ties simulation to frame time.
