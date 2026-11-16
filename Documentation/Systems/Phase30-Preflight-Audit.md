# Phase 30: preflight audit

**Date:** 2026-11-16. **Method:** reading the repository, the documentation and the code paths named below, and measuring a real 9-region save inside the Unity editor (read only). Nothing in the game was changed.

## 1. Repository and branch

- **Branch:** `feature/phase-0-1-foundation`.
- **Latest commits** (all local, none pushed since Phase 28's `80093e2`):

| Commit | Content |
|---|---|
| `7ce9749` | Phase 29 docs: preflight, ADR-0121 (gates), owner checklist, completion report, indexes |
| `b288ce5` | Townsfolk standing sway fix |
| `8c3f094` | Phase 29: open gates, Unity Terrain overland ground with a region cache, unified ground weather |
| `da4fb07` | Audio pass: music, footsteps by ground, combat hits and jingles |
| `80093e2` | Docs: record the Phase 28 push (last pushed commit) |

- **Uncommitted, preserved and not touched by Phase 30:**
  - `Assets/Realmkeeper/Art/Materials/M_LampGlow.mat`: Unity dropped its `_EMISSION` keyword.
  - `Packages/manifest.json` and `Packages/packages-lock.json`: the Unity MCP package (`com.coplaydev.unity-mcp` v10.0.0).
  - Untracked: `Assets/InitTestScene37e5c1f4-….unity` and `Assets/InitTestScene8aeb0c53-….unity`, with their `.meta` files.

## 2. Phase 29 status

**Phase 29 documentation is closed** (`7ce9749`): preflight, ADR-0121, ADR-0122, performance, owner checklist and completion report.

**Phase 29 is not fully verified.** These remain open and belong to Phase 29, not Phase 30:
- Owner hand-play (`Systems/Phase29-Owner-Playtest-Checklist.md`), and the open checklists of Phases 23–28.
- A development-build measurement of the overland.
- A full Unity EditMode and PlayMode checkpoint. The last full run was Phase 26.1 (EditMode 954/954, PlayMode 139 passed); Phases 27–29 ran focused tests only.
- An owner question: which overland ground a new player sees. `OverlandWorld.UseUnityTerrain` has been `true` since the renderer went in; Claude wrongly reported it as "not the default".

## 3. Documents reviewed

| Document | Role | Notes |
|---|---|---|
| `Roadmap.md` | Phase history | Rows to Phase 29 are current. The last row, "Phases 27–34 (proposed): planned, not started", is stale |
| `Product/GameDesignBible.md` | Vision and status | Status section "updated at the end of Phase 28", with stale rows (see §5) |
| `Product/FutureRoadmap.md` | Proposed Phases 22–34, owner decisions D1–D5, requirements R1–R3 | §5's phase plan no longer matches what was built (Phases 25–29 went elsewhere) |
| `Product/CampaignBible.md` | Story canon, proposed and undecided | Preserved unchanged. Structure §11 is placeholders only |
| `Product/ProductFoundation.md` | Original product scope | |
| `Architecture/*.md` | Layers, data ownership, simulation lifecycle, dual perspective | Accurate for the code read |
| `Systems/Persistence.md` | Save format, schema v1–v27 | Accurate |
| ADR-0005, 0009, 0076–0087, 0110–0112, 0120–0122 | World, campaign, overland decisions | Accurate for the code read |

## 4. Implementations inspected

| Area | Where | Finding |
|---|---|---|
| New game | `TitleScreen.CreateWorld`, `WorldSession.Execute(CreateWorldCommand)`, `NewGameSettings` | The player names a world and picks (or rerolls) a seed; 9 regions are generated at once; each world gets its own new slot |
| Atlas | `WorldAtlasGenesis` | `Columns = 3`, `MaxRegions = 9`; each further region from a seed derived from the world's; borders joined by crossings |
| Settlements | `RegionGenerator.SiteTypes` | Exactly one town, one village and one hamlet per region (27 in a 9-region world); cities and fortresses are never placed |
| Geography | ADR-0005, `RegionGenerator.RegenerateRegion` | Not saved: rebuilt from seed and generator version on every load |
| Commands | `CommandRegistry`, `CommandContext.Working` | Each edit deep-copies the whole `WorldData`, then commits |
| Simulation | `SimulationScheduler`, `SimulationTiers` | Daily systems over every region; distant regions' people update weekly (ADR-0081) |
| Saves | `JsonWorldStore`, `JsonWorldSerializer` | One indented JSON file per slot, atomic write (`.tmp` then replace), 32 MB load limit, no backup, no autosave |
| Main menu | `TitleScreen` | Continue, New world, Load world, Campaigns, Settings, Credits, Exit |
| Campaign | `Application/Campaigns`, ADR-0082–0087 | One provisional demonstration campaign; 37 quest and 33 dialogue definitions in code |

## 5. Measured (editor, read only, 2026-11-16)

A 9-region save (`p24.rkworld.json`, schema 27, game date 10 Seedtide 412):

| Measure | Value |
|---|---|
| File size | 8.86 MB (indented JSON) |
| Deserialize | 1,587 ms |
| Serialize | 608 ms, on the calling (main) thread |
| `WorldData.DeepClone()` | 7.8 ms (mean of 10), paid by every command and every simulated day |
| Contents | 9 regions, 27 settlements, population 23,634, 206 named people, 114 quest records, 162 events |
| Largest sections (compact) | people 1,590 KB, settlement layouts 953 KB, places 400 KB, creature populations 308 KB, relations 221 KB, containers 212 KB |

The 12 save files present are 3.7–8.7 MB; 9-region saves are 6.5–8.7 MB.

## 6. Discrepancies found

| Where | Says | Code says |
|---|---|---|
| `TitleScreen` New world text | "six connected regions" | 9 regions since Phase 23 |
| `GameDesignBible.md` status, "A connected world of regions" | 6 regions | 9 |
| `GameDesignBible.md` status, "Open-world overland travel" and "Navigation HUD" | Not started | Foundation since Phases 23–24 (rows further down say so) |
| `GameDesignBible.md` status, Phases 22–34 row | Planned, not started | 22–29 done |
| `Roadmap.md` last row | Phases 27–34 planned, not started | 27–29 done |
| `FutureRoadmap.md` §5 | Phase 29 fast travel, Phase 30 advancement depth | Phase 29 was gates and terrain; Phase 30 is this audit |
| Phase 29 reports (Claude) | Unity Terrain "not the default" | `UseUnityTerrain = true` |

Phase 30 corrects the roadmap rows and marks FutureRoadmap §5 superseded. The title-screen text is code; it is recorded, not changed (no implementation in this phase).
