# Phase 14: testing

All counts are from runs on 2026-11-11, on the Phase 14 working tree before the commit. Hardware: Intel Core i9-14900HX, 32 GB RAM, Windows 11 Pro; .NET SDK 8.0.427; Unity 6000.0.47f1 (Editor).

## Suites

| Suite | Result |
|---|---|
| .NET harness (Release) | 681 passed, 0 failed; build 0 warnings, 0 errors |
| Unity compile | 0 errors, 0 warnings |
| Unity EditMode | 681 / 681 passed, 0 failed, 0 skipped; 0 compile warnings |
| Unity PlayMode | 113 tests: 93 passed, 20 skipped (capture tests, run only with `RK_CAPTURE_DIR`), 0 failed |
| Captures | `Phase14CaptureTests` with `RK_CAPTURE_DIR`: 2 tests, passed; 19 screenshots at 1920×1080 (saved as JPEG, quality 85) in `Documentation/Screenshots/Phase14/`, with captions in `p14_capture_log.txt` |
| Mutation checks | 16 / 16 caught (15 Phase 14 mutants and the Phase 12 reward-claim mutant), after one new test and one new assertion; details below |

The PlayMode suite was run as a whole after the last code change. It includes the six new Phase 14 tests and all 87 earlier tests that run in batch mode.

## New .NET and EditMode tests (52)

`Combat/GridSightTests` (11): line of sight and cover on hand-drawn grids.
- a clear corridor gives no cover, both ways;
- a solid wall is total cover;
- a closed door is total cover until opened;
- a corner hides what is behind it, but not what is past it;
- grazing a wall's edge is clear, but squeezing between diagonal walls is not;
- a target in a room's corner has no cover from inside the room;
- half and three-quarters cover come from how many corner lines a wall blocks;
- creatures and low obstacles give half cover, never more;
- an area's spread stops at walls but not at creatures;
- different floors are total cover;
- the same squares always give the same answer, both ways.

`Combat/Phase14CombatTests` (9):
- a ranged attack through a wall is refused, with the reason;
- a closed door blocks the shot, and opening it clears the line for every rule;
- cover raises AC, and the log says why;
- a low obstacle or another creature gives half cover;
- an area spell stops at a wall;
- a spell needs a clear path to its target and to its point;
- the AI does not shoot through walls, it goes round;
- a lasting condition comes into the fight and goes back to the sheet;
- one damage rule: immunity, then resistance, then vulnerability, each once.

`Places/Phase14ConditionRestTests` (7):
- a trap's poison lasts, survives save and load, gives disadvantage, and wears off with time;
- harm goes through the one damage rule: temporary hit points first, and the last hit point is kept;
- a dwarf resists a trap's poison damage;
- a net restrains (no moving, no resting) until broken free, and nobody is trapped for ever;
- a long rest takes one level of exhaustion but does not cure poison;
- rest depends on where you are;
- rest is refused in a hazardous room and with foes in sight.

`Places/Phase14PlaceTests` (9):
- the five new places generate with their stories, valid across many seeds;
- each world chooses three to five by its own context, and the same seed always chooses the same;
- the sealed vault wakes its sleepers once, and the dead stay dead;
- nobody wakes through a wall;
- a bridge has a state, and a fallen one is no way for anyone;
- a damaged bridge takes an Acrobatics check, and a fall brings it down for good;
- fire burns for its time through the shared rules, then is out;
- the outlaws parley, and the road is freed by word or by blade;
- the culverts' sluice drains the channels for good.

`Places/Phase14CrimeTests` (8):
- a theft seen by someone who knows you has a bounty, costs reputation once, and is remembered;
- a theft nobody sees is undiscovered, then found out the next day with no culprit, and costs nothing;
- a stranger gives only a description, and there is no bounty;
- the news travels only between people who meet, so other towns do not know;
- a household warns once, then shows you out, and coming back is trespass;
- at night a home is locked, and breaking in is a crime;
- an assault is recorded before the first blow, and a killing is murder;
- a guard in a room in sight, but behind the wall beside the doorway, does not see the act; in the line through the doorway, they do.

`Places/Phase14MovementTests` (4):
- residents move through their house along real passages, each on their own rhythm;
- a locked door is no way for a follower, but a resident's own lock is;
- a follower follows through open ways, waits at a locked door, and catches up when it opens;
- leaving a place takes only those with the player.

`Places/Phase14SaveTests` (3):
- a Phase 13 (v15) save with progress gains the new places once and keeps everything it had; loading twice changes nothing;
- every new field survives save and load exactly;
- a fight's doors and low cover survive save and load mid-fight.

`Places/Phase14PerformanceTests` (1): the figures in the performance section below.

Changed tests:
- `PlaceSystemTests` and `PlaceRulesTests` now count the Phase 13 places plus the three to five new ones.
- The interior test runs at 21:50, because homes now lock at 22:00.
- `Phase2/TradeTests` counts goods lost on the road as consumed.
- `Phase2Worlds.StripToV15` builds a v15 document.
- Three assertions were rewritten to the plain form, because Unity's NUnit 3.5 lacks `Is.AnyOf`.

## PlayMode: twenty flows in the shipped scene (`Phase14PlayModeTests`, 6 tests)

| # | Flow | Test |
|---|---|---|
| 1 | Lasting conditions shown in the HUD, with effect and how each ends | `Conditions_ShowInTheHud…` |
| 2 | Break free from a net with the HUD's button; no rest while restrained | same |
| 3 | Conditions survive save and reload, with their reasons | same |
| 4 | A long rest takes one level of exhaustion and does not cure poison | same |
| 5 | A fight in a crypt room: walls on the grid | `AFightAmongWalls…` |
| 6 | Cover from the player for every foe, in the order list and the square's tooltip | same |
| 7 | Doors drawn on the grid; the fight fought to its end | same |
| 8 | Stealing under a resident's eyes records a witnessed crime | `ATheft_IsSeen…` |
| 9 | The journal's Crimes tab lists it with who saw it | same |
| 10 | Settled through the witness's dialogue, or no bounty on a stranger's word | same |
| 11 | At 23:00 a home's door offers pick, force or leave it | `AtNight_AHomeIsLocked…` |
| 12 | Forcing it gets in, and is recorded as a crime | same |
| 13 | Three to five new places, each with a marker on the map | `TheNewPlaces…` |
| 14 | The catacombs' vault wakes its sleepers; the fight; nothing wakes twice | same |
| 15 | The fane's burning room: firelight in the scene, marked on the place map | same |
| 16 | The culverts' damaged bridge; crossing takes an Acrobatics check; it holds or falls for good | same |
| 17 | The bridge on the place map | same |
| 18 | The map's list says how the place stands now | same |
| 19 | The outlaws talk before they fight, and take a toll | same |
| 20 | Residents walk between rooms (route and walker), and a freed captive follows room to room, named in the HUD | `Residents_WalkTheirHouse…` |

The new-places test runs the steps for each place this world has. On the shipped seed it has all five (Crowfoot Den, the Ashgrave Catacombs, the Lantern Fane, the Old Culverts, Thornwick), so every step ran.

## Mutation checks

Each mutant breaks one rule in the source. The .NET suite is then run, and the file is restored. The scripts are kept with the session's scratch files (`mutants/p14.sh`, `mutants/p14b.sh`).

| Mutant | What it breaks | Result | Caught by |
|---|---|---|---|
| p14_cover_half | Half cover gives +0 AC | Caught | `HalfAndThreeQuartersCover…`, `ALowObstacle_OrAnotherCreature…` |
| p14_total_cover | An attack through total cover is allowed | Caught | `ARangedAttack_ThroughAWall…`, `AClosedDoor_BlocksTheShot…` |
| p14_area_walls | An area spell reaches past walls | Caught | `AnAreaSpell_StopsAtAWall…` |
| p14_resistance | Resistance no longer halves | Caught | 4 tests, including `ADwarf_ResistsPoison…` and `OneDamageRule…` |
| p14_last_hp | Harm outside a fight can drop the player to 0 | Caught | `HarmGoesThroughTheOneDamageRule…`, Phase 13's trap test |
| p14_exhaustion | A long rest does not lower exhaustion | Caught | `ALongRest_TakesOneLevelOfExhaustion…` |
| p14_escape | A net can hold for ever (1,000 tries) | Caught | `ANet_Restrains…NobodyIsTrappedForEver` |
| p14_wake_walls | Sleepers wake and fight through a wall | Caught | `NobodyWakes_ThroughAWall…` |
| p14_wake_once | Waking can happen again | Caught (after a new assertion) | `NobodyWakes_ThroughAWall…` |
| p14_witness_wall | Witnesses see through walls | Caught (after a new test) | `AWitness_InARoomInSight_ButBehindItsWall…` |
| p14_bounty | A bounty on an unrecognised witness's word | Caught | `AStranger_WhoDoesNotKnowYou…` |
| p14_settle | Settling leaves the bounty | Caught | `ATheft_SeenBySomeoneWhoKnowsYou…` |
| p14_bridge | A fallen bridge is still a way | Caught | `ABridge_HasAState_AndAFallenOne…` |
| p14_night | Homes never lock at night | Caught | `AtNight_AHomeIsLocked…` |
| p14_rest_hazard | Rest allowed in a hazardous or burning room | Caught | `Rest_IsRefused_InAHazardousRoom…`, `Fire_BurnsForItsTime…` |
| p12_reward_claim | A quest reward can be paid twice (Phase 12 guard) | Caught | `ARewardAlreadyClaimed_IsNeverPaidAgain…` |

**16 of 16 caught**, but not all on the first pass. In the first pass:
- **Two survived** (wake once, witness through a wall). They exposed real gaps in the tests: a new assertion (opening the vault twice wakes nobody twice) and a new test (a guard in a neighbouring room, behind the wall beside the doorway, does not see the act; in the line through the doorway, they do) now catch them.
- **Four did not build.** Written as `if (false)`, they were rejected by the compiler: unreachable code is an error in this build. That proves nothing about the tests, so they were rewritten with conditions that still compile and run again. All four were caught.
- **One was malformed** (wake through walls left a dangling `else`). It was rewritten and caught.

All sources were checked back to their original state after the runs.


## Performance (`Phase14PerformanceTests`, .NET 8 Release)

| Measure | Result |
|---|---|
| Cover check (corner lines, 40×40 grid, 15 % walls) | 20–24 µs |
| Area line of effect | 0.5–1 µs |
| Generating one of the five new places | 0.12 ms |
| Indoor schedule, busiest building (24 people) | 0.1 ms per query (about 4 µs per person in the world) |
| Follower route | 0.3–0.4 µs |
| Witnesses for one act | 0.09 ms |
| Lasting conditions: apply, query and expire | 0.6–0.7 µs |
| Save / load, 22 places | 817 KB in 55–65 ms / 90–100 ms |
