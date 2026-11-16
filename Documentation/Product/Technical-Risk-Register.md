# Technical risk and dependency register (Phase 30)

**Date:** 2026-11-16. Ranked by severity × likelihood. "Evidence" is what was read or measured. Review at each milestone.

## Risks

| # | Risk | Severity | Likelihood | Evidence | Mitigation | Milestone |
|---|---|---|---|---|---|---|
| T1 | **Regenerated geography drifts under saves.** A generator change moves rivers, heights or passability beneath saved settlements, places and the player | High | High (generators change often) | ADR-0005: geography not saved; nothing detects an unversioned output change | Baked, versioned world definition; tests that pin generator output | M1.3 |
| T2 | **Whole-world deep copy per command** grows with the world | High at scale | Certain as the world grows | 7.8 ms per copy at 9 regions (measured) | Copy-on-write per aggregate; measure frame cost | M1.2 |
| T3 | **Main-thread save stalls the game**; autosave makes it visible | Medium | Certain once autosave exists | Serialize 608 ms at 9 regions (measured) | Snapshot, serialise on a worker, swap on the main thread | M1.2 |
| T4 | **No backups**: a bad write or bad build can cost a 40-hour save | High | Low to medium | One file per slot, no `.bak` | Rotating backups, checksum, recovery path | M1.1 |
| T5 | **Load time and file size grow** with the world | Medium | Certain | 8.86 MB indented, 1.6 s load at 9 regions; 32 MB limit (~32 regions) | Compact JSON; size the world from content; partition only if needed | M1.2, M2.3 |
| T6 | **Daily simulation tick at target size unmeasured** | Unknown | Medium | No measurement against region count | Measure at 9, 16, 25 regions; dormant tier if needed | M2.3 |
| T7 | **Campaign canon undecided** blocks campaign content | High | Certain until decided | Campaign Bible §5, §8, §10, §16 | Build mechanisms with provisional anchors; owner canon sessions | M5 |
| T8 | **Content authoring as C# literals** does not scale to 40 hours | High | High | 37 quests and 33 dialogues written as code | Data-file pipeline using the existing validators | M5.2 |
| T9 | **Verification debt:** no full suite since Phase 26.1; owner checklists of Phases 18–29 recorded as open; no development build measured in Phases 23–29 | Medium | Certain | `Roadmap.md`, Phase 27–29 reports | Full checkpoint at the start of M1; owner hand-play per milestone | M1 |
| T10 | **Flaky test:** `Phase28PlayModeTests` object count once 308 against 268 | Low | Medium | Phase 29 run | Investigate at the next checkpoint | M1 |
| T11 | **Event history unbounded** in saves | Low to medium | Medium | Quests pruned; no cap on history found | Measure growth over a long session; cap or archive | M1.1 |
| T12 | **Opening needs mechanics that do not exist** (stealth, scripted sequences, set pieces) | High | Certain | Concealment recorded but unread (ADR-0110) | M3.1 before M5 | M3 |
| T13 | **Placeholder character art** | Medium | Certain | Design Bible status | Art direction and assets in M8; licences per Workstream L | M8 |
| T14 | **Region borders feel like separate instances** | Medium | Certain today | Single crossing point per border (ADR-0076, ADR-0112) | Continuous borders | M2.2 |
| T15 | **Documentation drift** | Low | Certain | Stale rows found in preflight §6 | Update status tables at each milestone | Every milestone |
| T16 | **Unity Terrain default unconfirmed** by the owner | Low | — | `UseUnityTerrain = true`; earlier reports wrong | Owner decision | Now |

## Dependencies

```
Owner approval of Phase 30 direction
  └─ M1.1 save system & menu ── M1.2 command & save performance
        └─ M1.3 world definition ── M1.4 anchors ──┐
              └─ M2 world at campaign scale ───────┤
Owner decisions D2, D4 ── M3 player experience ────┤
Owner canon decisions ─────────────────────────────┴─ M5 main campaign ── M6 side content
                                                                      └─ M7 integration ── M8 polish ── M9 release
```

| Decision owed by the owner | Blocks |
|---|---|
| Approve or amend the Phase 30 direction (canonical world, milestones) | Everything after |
| Save slots and autosave policy (`SaveLoad-Design.md`) | M1.1 |
| D4 revisited: creation before play for the opening | M3.1 |
| D2: what fast travel is | M3.3 |
| The campaign's act and region plan | M2.1 world size |
| Canon (Bible §5, §8, §10, ending) | M5 |
| Unity Terrain or mesh ground for new players | — (now) |
