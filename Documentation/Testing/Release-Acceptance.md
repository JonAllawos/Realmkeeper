# Release acceptance (Phase 30 draft)

**Date:** 2026-11-16. **Status:** draft criteria for owner review; refined in M7 and applied in M9 (`Product/FutureRoadmap.md` §0).

## Testing policy (unchanged)

- Focused tests during normal implementation.
- Full Unity EditMode and PlayMode checkpoints about every two or three phases, and at each milestone.
- The same full suites are not run twice in one development round.
- Tests are never weakened to pass. Every failure, skip and unverified area is reported.
- **The owner does the gameplay testing.** Claude never claims hand-play that did not happen.

## What must be true before release

| Area | Criterion | How verified |
|---|---|---|
| Functional | Every release-critical system at level 5+ in `Product/Feature-Completion-Matrix.md` | Matrix review |
| Save and load | All of `Systems/SaveLoad-Design.md` §8 pass; no data loss across 1,000 automated save, kill and load cycles | Automated |
| Campaign completion | Every main ending reachable in an automated run with scripted choices; the owner completes the campaign at least once | Automated, owner |
| Quest-state consistency | No quest, arc or decision in an impossible state after save and load at every milestone | Automated (validators on load) |
| World persistence | Entity counts and IDs stable across save and load; no duplicate or regenerated location | Automated |
| Region transitions | Every border and every settlement, place and gateway entered and left both ways | Automated sweep |
| NPC and settlement simulation | One in-game year advanced with no validator errors; populations and economies within designed bounds | Automated |
| Combat and progression balance | Encounter difficulty across the campaign's level range within designed bands | Automated sims, owner |
| Performance | Frame time, load, save and transition budgets met on a development build on target hardware, in the busiest town and the largest region | Measured |
| UI and accessibility | Text size, contrast, reduced motion, remapping; every screen keyboard-operable | Automated captures, owner |
| Resolution | 1280×720 to 3440×1440, including 4:3 and 21:9 | Automated captures |
| Audio | Music moods, footsteps, cues and volumes behave; no clipping or stuck loops | Owner |
| Regression | Full EditMode and PlayMode green, with no unexplained skips | Automated |
| Long-session stability | A 4-hour session without memory growth or slowdown | Measured |
| New Game and multiple saves | New Game never alters existing saves; three slots, autosaves and Continue behave | Automated, owner |
