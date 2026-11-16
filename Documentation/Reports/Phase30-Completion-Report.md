# Phase 30 completion report: canonical world architecture, save/load design and release roadmap

**Date:** 2026-11-16. **Branch:** `feature/phase-0-1-foundation`. **Kind:** audit and documentation only. **Nothing in the game was changed.**

## Executive summary

- **Realmkeeper can become one canonical, connected world without a rewrite.**
  - The game already keeps one world: every region, person and faction in one saved `WorldData`, with stable IDs and one simulation.
  - The change is mainly in what the player sees, plus a few targeted architectural changes:
    - one fixed New Game instead of a named, seeded world each time;
    - a baked, versioned world definition, so code changes cannot move the ground under saves;
    - fixed story anchors for the campaign;
    - an atlas beyond its current 9-region cap;
    - no whole-world copy per command, and saves written off the main thread.
- **The save system can carry three manual slots plus autosaves.** It already writes atomically and migrates every version since v1. It needs:
  - slot metadata (character, level, location, playtime, thumbnail);
  - autosave;
  - backups and corrupted-save recovery;
  - a release menu.
- **The campaign engine is ready; the 40-hour campaign is not.**
  - One provisional demonstration campaign exists.
  - Missing technically: story anchors, a scripted opening with stealth, and set-piece fights.
  - Missing narratively: most of the canon, which the Campaign Bible marks undecided.
- **No major system is content complete, playtested or release ready.** Most are integrated (level 4 of 7).
- **Proposed roadmap:** nine milestones (M1–M9).
- **Recommended next phase: M1.1, the release save system and main menu.**
- **Awaiting owner review.**

## 1. What architecture exists

Detail: `Architecture/CanonicalWorld-Audit.md` §1.

| Area | Implementation |
|---|---|
| Layers | Domain, Application, Infrastructure, Presentation and Simulation |
| World state | One `WorldData` |
| Commands | Validated, applied to a deep copy, then committed atomically |
| Simulation | Daily systems with their own random streams, over every region, in tiers |
| World creation | 9 regions on a 3 × 3 atlas, each 128 cells (5.12 km walked); 3 settlements a region |
| Geography | Regenerated from the seed on every load |
| Saves | One JSON document per slot, schema v27 |
| Presentation | Streams one environment at a time (a region's country, a settlement, a place) behind loading transitions |

## 2. Can it support one canonical connected world?

**Yes.** What must change (`Architecture/SingleWorld-Transition.md`):

| Change | Why |
|---|---|
| Canonical New Game | No seed or world name for players |
| Baked world definition | Stops geography drifting under saves (risk T1) |
| Story anchors | The authored campaign needs fixed people and places |
| Atlas beyond 9 regions; cities, fortresses and ports placed | The world the content needs |
| Continuous borders, crossable wherever passable | Removes the "separate instance" feel |
| Per-aggregate copy-on-write | Frame cost no longer grows with the world |
| Compact saves off the main thread | No save hitch |

**World size** should follow from the campaign's act and region plan. Measured costs per region bound it: about 9–12 regions as the code stands, and plausibly 25–50 after the changes. The daily simulation tick against region count has not been measured.

## 3. Saves: three slots and autosaves?

**Yes, with the additions in `Systems/SaveLoad-Design.md`:**
- the release menu;
- slots `slot-1`…`slot-3` and `auto-1`…`auto-3`;
- metadata (save v28, to be designed);
- autosave at arrivals, milestones, a timer and on quit;
- backups and checksums;
- Continue by last played among valid saves.

No system was found that depends on transient scene state. The exposure is regenerated geography.

## 4. What a 40-hour campaign needs

`Product/Campaign-Architecture-Assessment.md`:
- anchors;
- scripted sequences;
- stealth;
- set-piece fights;
- creation-first New Game;
- a data-file authoring pipeline;
- the ancient realm's content;
- a decision on romance;
- the content itself: an estimated 30–70 main quests, an assumption, not a plan;
- **owner canon decisions** (its §6).

**First step:** a vertical slice of the opening and chapter 1.

## 5. System status

`Product/Feature-Completion-Matrix.md`:
- **Level 4 (integrated):** most systems.
- **Level 3:** streaming, overland, magic, travel, survival, saves, menu, audio, graphics, accessibility, settings.
- **Level 2:** main campaign, ancient world, performance.
- **Level 1:** onboarding.
- **Unverified in play:** the overland, audio and Unity Terrain, pending owner hand-play.

## 6. First release and deferrals

`Product/First-Release-Scope.md`.
- **Release critical:**
  - the canonical world;
  - the save system;
  - the campaign and its opening mechanics;
  - companions;
  - meaningful side content;
  - onboarding;
  - performance budgets;
  - settings.
- **Post-release:**
  - property and estates (R3);
  - R2 at full scale;
  - an underground network;
  - seamless border streaming.

## 7. Milestones

`Product/FutureRoadmap.md` §0, proposed:

| Milestone | Focus |
|---|---|
| M1 | Foundation and canonical world |
| M2 | Living world at the campaign's scale |
| M3 | Complete player experience |
| M4 | Exploration depth |
| M5 | Main campaign |
| M6 | Side content |
| M7 | Integration |
| M8 | Polish |
| M9 | Release readiness |

**Recommended next:** M1.1, the release save system and main menu. It has no canon dependency, and everything later needs it. It needs a save-schema brief.

## 8. Technical risks and dependencies

`Product/Technical-Risk-Register.md`. The highest risks:

| Risk | What |
|---|---|
| T1 | Geography drift under saves |
| T2 | Whole-world copy per command (7.8 ms at 9 regions) |
| T4 | No backups |
| T7 | Undecided canon |
| T8 | Authoring as code |
| T12 | The opening's missing mechanics |

## 9. Measurements (editor, read only)

A 9-region save, `p24.rkworld.json`:

| Measure | Value |
|---|---|
| File size | 8.86 MB |
| Deserialize | 1,587 ms |
| Serialize | 608 ms |
| Deep copy | 7.8 ms |
| Contents | 206 named people, population 23,634, 27 settlements |

No development build was measured.

## 10. Phase 29 status

- **Phase 29 documentation:** closed in this round (`7ce9749`).
- **Still open for Phase 29**, separate from Phase 30's findings:
  - owner hand-play;
  - a development-build measurement;
  - a full Unity checkpoint;
  - the owner's choice of overland ground. `UseUnityTerrain` is `true`; Claude's earlier reports said otherwise, wrongly.

## 11. Tests

- **Performed:** none in Phase 30. It is an audit. The measurements in §9 used the editor to read an existing save; nothing was written to it.
- **Not performed:** full EditMode and PlayMode suites (due; last Phase 26.1), development-build measurement, any hand-play (Claude has not played the game).

## 12. Documentation

| Deliverable | File |
|---|---|
| Preflight | `Systems/Phase30-Preflight-Audit.md` |
| 1. Canonical World Architecture Audit | `Architecture/CanonicalWorld-Audit.md` |
| 2. Single-World Transition Architecture | `Architecture/SingleWorld-Transition.md` |
| 3. Save/Load System Design | `Systems/SaveLoad-Design.md` (linked from `Systems/Persistence.md`) |
| 4. Campaign Architecture Assessment | `Product/Campaign-Architecture-Assessment.md` |
| 5. Feature-Completion Matrix | `Product/Feature-Completion-Matrix.md` |
| 6. First Release Scope and Deferred Features | `Product/First-Release-Scope.md` |
| 7. Revised Milestone Roadmap | `Product/FutureRoadmap.md` §0 (§5 marked superseded if approved) |
| 8. Technical Risk and Dependency Register | `Product/Technical-Risk-Register.md` |
| Release acceptance (brief §10) | `Testing/Release-Acceptance.md` |
| 9. Owner Review Checklist | `Systems/Phase30-Owner-Review-Checklist.md` |
| 10. Completion Report | this file |

Also updated:
- `Roadmap.md`: the stale "Phases 27–34 planned" row replaced by Phase 30.
- `GameDesignBible.md`: a note pointing its status section to the matrix and naming its stale rows.

The Campaign Bible is unchanged.

## 13. Git

| Commit | Content | Pushed |
|---|---|---|
| `7ce9749` | Phase 29 close-out docs | No |
| (this commit) | Phase 30 documentation only | No |

**Working tree after the commit:** only the preserved files remain uncommitted.
- `M_LampGlow.mat` (Unity's dropped emission keyword).
- `Packages/manifest.json` and `Packages/packages-lock.json` (Unity MCP).
- The untracked `Assets/InitTestScene*` scenes.

## Stop

Phase 30 stops here. Phase 31 is not started. Nothing was implemented, no save schema was changed, no save was migrated, world generation was not rewritten, and no canon was invented. Waiting for the owner's review (`Systems/Phase30-Owner-Review-Checklist.md`).
