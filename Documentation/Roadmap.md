# Roadmap and milestone status

| Milestone | Status | Where |
|---|---|---|
| Phase 0–1: foundation, world generation, strategic map | Done | `Documentation/Architecture`, ADR-0001…0007 |
| Phase 2: settlement economies, trade, chronicle | Done (`fa521f2`), completed by the Phase 2 completion pass below | `Documentation/Systems/Economy.md` |
| ADR-0009 prerequisites and exploration vertical slice | Accepted as a vertical slice (`0c7f60d`) | `Documentation/Architecture/DualPerspective.md` |
| Pre-Phase-2 stabilization: session rebinding, save slots | Done in the Phase 2 completion commit | `DualPerspective.md`, `Systems/Persistence.md` |
| Phase 2 completion | Done; see the test results for what was run | `Systems/Economy.md`, `Testing/TestResults.md` |
| Phase 3 | **Not started.** It needs its own brief. | — |

## Phase 2 completion: what it added

- **Trade:** routes can be suspended and resumed through a typed command, which is chronicled
  and saved. Schema v4 adds this, with a migration from v3.
- **Inspector:**
  - a "Why" line under prosperity and stability, listing the terms behind each target;
  - the food reserves trend, and whether a shortage is easing or worsening;
  - "Generalist" for settlements without a specialization;
  - a Suspend/Resume toggle on each route, and a count of active routes.
- **Map:** markers above settlements for food shortage, declining stability, strong production
  and trade on the road.

## Known limitations carried forward

- One region per world; three settlements in the default world.
- There are no births, deaths, migration of individuals, construction, crafting or caravans.
  These were out of scope.
- Performance was measured only at the default world size: a 30-day run takes 7–9 ms. No
  claim is made for larger worlds.
- One save slot is active at a time. There is no save browser.
- The map markers use `Universal Render Pipeline/Unlit` found by name. A player build must
  include that shader (editor runs are unaffected). Not yet verified in a build.