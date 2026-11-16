# Phase 29: owner playtest checklist

| | |
|---|---|
| **Automated tests (done by Claude)** | .NET and focused Unity tests (completion report §7) |
| **Editor measurements (done by Claude)** | Ground preparation, cache hits, memory (`Phase29-Performance.md`). Editor only, not a player build |
| **Screenshots (done by Claude)** | `Documentation/Screenshots/Phase29/`: 9 matched pairs, Unity Terrain against the old mesh ground |
| **Manual playtesting (yours, not done)** | Everything below. **Claude has not played the game by hand** |

The game draws the overland ground as a Unity Terrain (`OverlandWorld.UseUnityTerrain` is `true`). Existing saves work: settlement plans saved before Phase 29 have their gate roads straightened once, on load.

## Gates (ADR-0121)

1. **Every gate of a walled town.** Walk out through each gate and back in. Nothing stands in the road: no tower, no wall, no lintel.
2. **A road that meets the wall at a slant.** The gatehouse is turned along the road, not towards the town's centre.
3. **The road near the gate.** It runs straight through the gate for about 9 m each side, then rejoins its old line without a sharp kink.
4. **Alderholm** (seed 123456789), or any gate at the end of a bridge. The bridge lands in line with the gate and you walk straight from the deck through it.
5. **A city or a fortress**, if your world has one: the larger gatehouse is just as clear.
6. **An old save.** Load one made before Phase 29 and walk through its gates.

## The overland ground (ADR-0122)

7. **Out to the country.** Leave a town: the ground looks like the old ground in colour and brightness, lit by the sun, with its grain.
8. **Roads and bridges.** Roads keep their colour; bridge decks are visible over water.
9. **Hills and crags.** Steep ground looks right. Rocky knolls are a little smoother than before (5 m heightmap normals): say if that bothers you.
10. **Walking.** You never sink into or float above the ground, on slopes or at lake shores.
11. **Coming back.** Return to the same region (a town, then out again). Entry feels quicker the second time (the region cache).
12. **Three or four regions in turn,** then back to the first. Nothing looks stale or wrong.

## Ground weather (ADR-0115, ADR-0122)

13. **Rain.** The ground and roads darken as they get wet.
14. **Snow.** Level ground whitens; under deep snow the roads disappear (your decision: leave as is).
15. **A flood.** Fords show barriers. There is no rising water (a known limit).
16. **Frost.** Frozen ground looks wet, not frosty (a known limit).
17. **Re-entry and load.** After leaving and coming back, and after a save and load, the ground shows the current weather, not an old look.

## Also since Phase 28 (not Phase 29, but unplayed)

18. **Music** follows what is on screen (title, town, country, night, fight) and crossfades; the Music volume setting works.
19. **Footsteps** change with the ground (grass, wood, stone, snow); hits, blocks, quest and level-up sounds play.
20. **Townsfolk** (`b288ce5`): workers kneel and stay kneeling; people standing about no longer twitch or "dance".

## The open question

21. **Which ground should a new player see?** The Unity Terrain is drawn today. Keep it, or switch back to the mesh ground until you have played it? (See the completion report §11.)

## Still outstanding from earlier phases

The hand-play checklists of Phases 23–28 are still open.
