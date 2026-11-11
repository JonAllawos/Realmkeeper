# Phase 13: testing

All counts are from runs on 2026-11-11, on the Phase 13 working tree before the commit. Hardware: Intel Core i9-14900HX, 32 GB RAM, Windows 11 Pro; .NET SDK 8.0.427; Unity 6000.0.47f1 (Editor).

## Suites

| Suite | Result |
|---|---|
| .NET harness (Release) | 629 passed, 0 failed; build 0 warnings, 0 errors |
| Unity EditMode | 629 / 629 passed; 0 compile warnings |
| Unity PlayMode | 105: 87 passed, 18 skipped (capture tests, run only with `RK_CAPTURE_DIR`), 0 failed |
| Captures | `Phase13CaptureTests` with `RK_CAPTURE_DIR`: 1 test, passed; 16 screenshots in `Documentation/Screenshots/Phase13/` |
| Generation | 2,500 layouts (10 archetypes × 250 seeds) and 720 interiors: 0 validation failures |
| Mutation checks | 12 of 12 caught (below) |
| Reward-claim guard (Phase 12) | Direct test added; mutant m1 rerun: caught |

## New .NET tests (`Tests/EditMode/Places/`)

`PlaceGenerationTests` (8):

| Test | What it proves |
|---|---|
| TheCatalogs_AreConsistent | Archetypes, features, traps, loot tables and items validate; 10 archetypes; every definition names a real archetype |
| EveryArchetype_GeneratesValidConnectedLayouts_AcrossManySeeds | 2,500 layouts, 0 failures; floor and room counts in range; statistics printed |
| TheSameSeed_GivesTheSameLayout_AndADifferentSeed_ADifferentOne | Determinism for every archetype |
| Archetypes_AreDistinct_InRoomsMaterialsAndInhabitants | Mine: two levels joined by a ladder, spiders and rats. Crypt: the dead, tombs. Cave: no doors. Materials differ. |
| StoryFeatures_GoIntoTheirRooms_AndRequiredOnesAreNeverBehindASecret | 60 mine seeds: the captive is required and never secret, and the ledger is always there |
| Interiors_AreValid_ForEveryFunctionSizeAndWealth | 720 combinations; a wealthy two-storey home has three floors; the guardhouse's restricted rooms |
| TheAuthoredFort_IsValid_AndBuiltAsDesigned | Two ways in, the guarded gate, the restricted rooms, the key in the armoury |
| TheValidator_CatchesBrokenLayouts | Fault injection: overlap, no way in, unreachable room, low ceiling, unpickable lock without a key, a feature outside its room, a required feature behind a secret, stairs on one floor, a creature outside its room |

`PlaceSystemTests` (3):

| Test | What it proves |
|---|---|
| TheWorld_HasItsPlaces_OnLand_OffTheRoads_WithTheirPeople_Once | Five places, valid, unknown, apart, with real containers; the captive is away from home; the quest is bound; the garrison of five are persistent people of the fort's faction, posted; genesis runs once |
| ARumour_GivesANameAndADirection_ThenTheWay_AndOnlyAKnownPlaceCanBeEntered | Unknown: refused and unlisted. Rumoured: no map cell. Asked again: Discovered, with a cell. Entered. |
| LostBelow_FromTheAsking_ToTheReturn_ThroughTheRealCommands | The full slice through commands: accept, discovered, enter, read the ledger (discovery), down the ladder, the den fight won, free, follow, out, home (escort), report (rewards once, Chronicle once each), back (still defeated, still freed), and an exact save round trip |

`PlaceRulesTests` (13):

| Test | What it proves |
|---|---|
| TheFortGate_IsHeld_UntilTheWatchLetsThePlayerPass | Refused moves leave the world unchanged; a bribe through the gate dialogue grants passage; the courtyard is open with leave |
| TheSideDoor_IsLearned_ThenForced_TheArmouryHoldsTheKey_AndTheCourtyardIsTrespass | Hidden entrance learned from the roster, forced; the key taken from a real container; trespass starts a fight and costs standing once |
| Locks_KeysAndForce_AreReal_AndAFailedAttemptChangesOnlyItsCount | Unlock needs the key and keeps it; lock again; pick needs tools; failures change only the count |
| Attempts_AreLimitedPerVisit_AndANewVisitAllowsMore | Three listens, then refused; allowed again on a new visit |
| ATrap_HurtsButNeverKillsOutright_IsSpentOrResets_AndCanBeDisarmed | At 1 HP a sprung trap leaves 1 HP; spent within the visit; disarming makes it safe for good |
| AHazard_IsMetOnceAVisit_ThroughTheRulesSaves | Foul air: a Constitution save on entering, not again that visit; the scene status says so |
| ABuilding_IsEnteredThroughItsDoor_HoldsItsPeople_AndIsTheSameOnReturn | Interior created on entry; residents at home; the household chest refused; leaving puts the player at the door; the same interior on return |
| Walls_BlockMovement_AndTheAiGoesAroundThem | Movement into a wall refused; the AI's route avoids walls; walls saved with the fight |
| ACorneredCreature_AmongWalls_FightsInsteadOfDashingInPlaceForEver | Regression for the stalemate found in the scene |
| APlacesContainer_IsReachedOnlyFromInsideThatPlace | A place's chest cannot be taken from while elsewhere |
| AVersion14Save_GainsItsPlacesOnce_AndKeepsEverythingElse | Migration: places, the quest and the escort appear once |
| RefusedActions_LeaveTheWorldUnchanged | Fault injection: unknown place, unknown id, not inside, already inside, non-adjacent room, the wrong ladder, no such target, freeing among enemies, no such trap, leaving from a room with no way out |
| TheValidator_CatchesImpossiblePlaceStates | Fault injection: unknown place, unknown explored room, a door state on an opening, lost-but-alive, escorted twice, posted twice, a container in an unknown place, overlapping rooms |

`PlacePerformanceTests` (1): `Places_AreCheap` (see the report, section R).

Changed: `QuestSystemTests` gains `ARewardAlreadyClaimed_IsNeverPaidAgain_EvenIfCompletionRunsTwice` (preflight). Four person-count tests now include the fort's garrison of five. `Phase2Worlds.StripToV14` is added and chained from `StripToV13`.

## PlayMode tests (`Tests/PlayMode/Phase13PlayModeTests.cs`)

All drive the shipped scene.

| Test | Flows |
|---|---|
| ABuilding_IsEnteredThroughItsDoor_AndLeftTheSameWay | 1 enter a building (the door in the street, through the interact path), 8 inspect an object, 2 leave by the door |
| LostBelow_InTheScene_FromTheAskingToTheReturn | The asking in the conversation window; 3 the map marker and entering from the map's Places list; 4 rooms; 5 the ladder; 15 the fights; 16 the rescue; 18 place objectives; 19 save and reload inside; 17 the escort home; 20 the return |
| TrapsHazardsContainers_AndHiddenThings_InACrypt | 12 a hazard; 10 a trap detected; 11 disarmed; 13 a container opened in Storage; 14 its contents taken; 9 searching for hidden things |
| TheFort_DoorsLocksAndKeys_InTheScene | 6 open a door (through the choice list); 7 unlock with the key; the place map shows only rooms seen |

Changed: `CombatDriver.Step` (the Phase 8 fight driver) now clicks the reachable square nearest the enemy when a wall blocks the straight step. `NpcPlayModeTests.PeopleList_…` now counts 24 named people (the garrison of five); the final run caught the stale 19, and the suite was rerun.

## Mutation checks (`mutants/p13.sh`)

| Mutant | Rule broken | Caught by |
|---|---|---|
| p13_overlap | Overlapping rooms accepted | TheValidator_CatchesBrokenLayouts, TheValidator_CatchesImpossiblePlaceStates |
| p13_required | A required feature behind a secret accepted | TheValidator_CatchesBrokenLayouts |
| p13_guard | Guarded doors pass without leave | TheFortGate_IsHeld… |
| p13_lasthp | Traps can take the last hit point | ATrap_HurtsButNeverKillsOutright… |
| p13_attempts | Unlimited attempts | Attempts_AreLimitedPerVisit… (added after the first run let it survive) |
| p13_defeated | Groups never stay defeated | LostBelow_FromTheAsking… (first version did not compile; rewritten) |
| p13_away | Held people still at home | TheWorld_HasItsPlaces… |
| p13_cornered | Cornered creatures dash in place | ACorneredCreature… |
| p13_adjacent | Moving to any room | RefusedActions…, Locks…, TheFortGate… |
| p13_discovery | Unknown places can be entered | RefusedActions…, ARumour… |
| p13_reach | Place containers reachable from anywhere | APlacesContainer_IsReachedOnlyFromInsideThatPlace (added after the first run let it survive) |
| p13_trespass | Trespass costs nothing | TheSideDoor… |

**12 of 12 caught.**
