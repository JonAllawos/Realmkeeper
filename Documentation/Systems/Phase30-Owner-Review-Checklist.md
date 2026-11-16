# Phase 30: owner review checklist

Phase 30 is an audit. **Nothing in the game changed.** These are the decisions and reviews it needs from you before Phase 31 is briefed.

## Direction

1. **One canonical world.** Do you accept the recommendation in `Architecture/CanonicalWorld-Audit.md`?
   - Keep the region architecture.
   - Bake a versioned world definition.
   - New Game always starts the same world, with no seed or world name for players.
2. **The transition plan.** Review `Architecture/SingleWorld-Transition.md`, especially §2 (baked geography: a departure from ADR-0005) and §3 (story anchors).
3. **The milestone roadmap.** Approve, amend or reject `Product/FutureRoadmap.md` §0. If approved, the old phase list (§5) is superseded.
4. **The next phase.** The recommendation is M1.1: release save system and main menu.

## Saves

5. **Slots:** three manual slots, three rotating autosaves, an optional quicksave (`Systems/SaveLoad-Design.md` §2).
6. **Autosave moments:** arrivals, milestones, every 10–15 minutes, on quit (§5). Any others, or fewer?
7. **Manual save limits:** not during a fight or a conversation. Is that right?
8. **Old developer saves** stay loadable in development builds only. Is that acceptable?

## Scope

9. Review `Product/First-Release-Scope.md`. In particular:
   - Is property and estates (R3) post-release?
   - Is "dozens of cities" (R2) scaled after the campaign sets the world's size?
   - Is romance in or out of the first release?
10. **World size:** agree that the size follows the campaign's act and region plan, rather than a number now.

## Owner decisions already open

11. **D4:** creation before play, for the opening (Campaign Bible §3, §11 note).
12. **D2:** what fast travel is.
13. **Canon** for the campaign to begin: the crew, the conspiracy, the ancient world, the antagonist (`Product/Campaign-Architecture-Assessment.md` §6).
14. **Overland ground:** the game draws the Unity Terrain today (`UseUnityTerrain = true`). Keep it, or switch new players to the mesh ground until you have played it? (Claude earlier reported, wrongly, that it was not the default.)

## Still open from Phase 29 and earlier

15. Hand-play: the Phase 29 checklist (gates, the Unity Terrain, weather, audio, townsfolk) and those of Phases 23–28.
16. A full Unity checkpoint is due (last: Phase 26.1). The recommendation is to run it at the start of M1.
