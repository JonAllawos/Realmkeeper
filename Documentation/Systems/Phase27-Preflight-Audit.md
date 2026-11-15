# Phase 27: preflight audit

**Date:** 2026-11-14.

**Scope:** settlement life, architecture and street-level immersion.

## 1. Git state

| | |
|---|---|
| **Branch** | `feature/phase-0-1-foundation` |
| **HEAD at start** | `b90f102` (Phase 26.1 docs) |
| **Remote** | `origin/feature/phase-0-1-foundation` at `3f8ded4` |
| **Ahead / behind** | 5 ahead (the post-play fixes and Phase 26.1), 0 behind |
| **Working tree** | Clean apart from the two untracked `Assets/InitTestScene*` scenes (preserved) |

## 2. Why a 1,600-person town showed five or six people (measured)

- **Named NPCs are a tracked subset of the population** (ADR-0009). They are the only people exploration ever drew.
- **Measured in the test world** (`Phase27SettlementLifeTests`):
  - Bramhaven (town): population 1,644, **12 named**.
  - Villages: 514–689 inhabitants, 5 named.
  - Hamlets: 121–186 inhabitants, 5–10 named.
- **Of the named, most are indoors** at any hour (home, work, the tavern), shown only inside.
- **So a town of 1,644 showed at most a dozen**, usually five or six. The unnamed majority had no presentation at all.

## 3. What existed, by area

| Area | What exists | State |
|---|---|---|
| Population | `SettlementData.Population` (authoritative); named NPCs with persistent IDs, schedules (`NpcActivityPlan`), presence by hour (`GetPresenceIn`) | Works; only the named are shown |
| Residents in the street | `ResidentView`: humanoid bodies, routes along the road graph (`LocalPaths.Route`), activities, talk | Works |
| Professions and labour | `NpcRole`; `SettlementEconomy.Production` (workers by trade); `ShopKind` | Exists; not used for street life |
| Guards | Named Guard NPCs only | **No patrols**, no presence by settlement size |
| Animals | Herds in pastures from `world.Livestock`, as box figures, static, merged into static geometry | **Incomplete**: no movement; the kit's animal models unused |
| Children | No child population, bodies or rules | **Missing**; needs design (§6) |
| Shops | Open-front shop interiors with stations; named keepers | Works. **No signs** (the kit's `@Sign` was a blank plank). **No callouts** |
| Faction identity | The strategic map's faction colours by world list order | **Nothing in towns** |
| Dressing | Plan features (wells, stalls, lamps), the granary's barrels, kit pieces (awnings, flower boxes, barrels), verge stones | Thin; no rule-based dressing by building purpose |
| Doors | `BuildingKitPlanner` puts the door at a **random** module slot; `LocalPaths.DoorwayX` gave **0 (centre)** for non-shops | **Defect:** paths and people went to the middle of the front, beside the door |
| Door leaf | `Door_1_*` hung 0.53 m along, at its model width | **Defect**, measured in the running game: opening 1.29 m, leaf 1.12 m, 7–10 cm open either side |
| Interiors | `InteriorGenerator`: function-based plans from the footprint, cells of 1.5 m clamped to 5–14 | **Defect:** small buildings were given rooms wider than their walls (7.5 m minimum); large ones all capped at 21 m. One or two furnishings per room regardless of size; few furniture definitions |
| Interior persistence | An interior is generated on first entry and **saved** (`PlaceData`); never regenerated | Good: changes affect only buildings not yet entered |
| Roads | Draped on the walked mesh (`ba3d333`); straight polyline corners; square ends | Corners and ends abrupt (owner report) |
| Bridges | Level decks | Owner reported them good; not changed |

## 4. Performance constraints and tests

- **Exploration** keeps everything in one scene per settlement. There are no per-frame full scans except the existing presence and exchange refreshes (2 s).
- **Static geometry is merged** (`MeshMerger`). Anything that moves must live outside `ctx.Static`.
- **The last full Unity checkpoint** was Phase 26.1 (EditMode 954/954; PlayMode 139 passed, 0 failed, 36 capture-only skips).
- **Under the revised policy**, this phase runs focused tests. The full suites are deferred to the next checkpoint (§ completion report).

## 5. Gap analysis (prioritised)

| Priority | Item | Kind |
|---|---|---|
| 1 | Street population from the authoritative population | Missing system, built on existing data |
| 2 | Doors where paths lead; leaf fills frame | Defect, fix in the existing kit |
| 3 | Interiors sized to footprints, furnished by area | Incomplete behaviour, existing generator |
| 4 | Watch patrols and posts | Missing behaviour, on the existing road graph |
| 5 | Shopkeeper callouts | Missing; data-driven on existing shops and keepers |
| 6 | Signs, banners, dressing by building purpose | Visual, existing kits and catalog |
| 7 | Animals moving, kit models | Incomplete; existing herds and catalog |
| 8 | Road corners and ends | Visual geometry, existing road strip |
| — | Children | Deferred (§6) |

**Risks:**
- Spawn churn and figure-building spikes: the townsfolk are budgeted.
- Determinism of generated data: the door-slot draw is kept so other random choices do not move.
- Saved interiors: never regenerated.

## 6. Children: deferred, with the reason

The world has no children:
- no child NPCs or ages under adulthood in the population model;
- no child body in the humanoid kit;
- no rules for what children may do.

Adding them would mean inventing a population category and a body. That is a design decision for the owner, not a presentation change, so this phase does not add children.
