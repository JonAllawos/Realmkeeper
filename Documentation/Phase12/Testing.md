# Phase 12: testing

All counts below are from runs on 2026-11-11, on the Phase 12 working tree before the commit.

## Suites

| Suite | Command | Result |
|---|---|---|
| .NET harness | `dotnet test Tools/DotNet/Realmkeeper.Verification.sln -c Release` | **603 passed, 0 failed**, 0 warnings, 0 errors (Phase 11: 574) |
| Unity EditMode | `Unity.exe -batchmode -runTests -testPlatform EditMode` | **603 / 603 passed**. One compiler warning, pre-existing since Phase 11: CS0067, `PlayerHubWindow.CombineRequested` is never raised. |
| Unity PlayMode | `Unity.exe -batchmode -runTests -testPlatform PlayMode` | **100: 83 passed, 17 skipped, 0 failed** (Phase 11: 80 passed, 16 skipped). The 17 skipped are the capture sets, which run only with `RK_CAPTURE_DIR`. The previous full run had 1 failure (the tracked-line assertion, fixed in the test; see the report, section N). |
| Captures | `Unity.exe -runTests -testPlatform PlayMode -testFilter Phase12` with `RK_CAPTURE_DIR` | 1 test, passed; 13 screenshots in `Documentation/Screenshots/Phase12/` |
| Mutation checks | `mutants/run.sh` (each mutant breaks one rule; the .NET suite must fail) | **6 of 7 caught** by named tests; one equivalent survivor (see below) |

## New .NET tests (`Tests/EditMode/Quests/`)

`QuestSystemTests` (28):

| Test | What it proves |
|---|---|
| TheCatalogs_AreConsistent_AndStatesMoveOnlyAsAllowed | Quest and dialogue catalogs validate. Every allowed and forbidden state move. A closed quest never reopens unless repeatable. |
| TheAuthoredQuests_AreBoundToRealPeople_AndTheGranaryProblemIsReal | Roles bind to living residents, each a different entity. The missing grain really leaves the stores, and the Chronicle records it. Authored quests are made once. |
| ShortMeasures_MadeGood_RestoresTheStores_PaysOnce_AndIsRemembered | Ledger, keeper, suspect, four sacks delivered. Rewards paid once. The stores rise. The giver remembers. |
| ShortMeasures_TheSilverPath_PaysTheBribe_LeavesTheStoresShort_AndSpreadsNoGoodWill | The bribe is real coin. The stores stay short. No gratitude. The decision is recorded. |
| ShortMeasures_Confronting_UsesTheRealCheck_AndEitherWayTheStoryHolds | Persuasion uses the rules engine's skill check. Success and failure both leave a valid world. |
| Declining_RecordsIt_AndTheQuestCanStillBeTakenLater_AbandoningIsFinal | Declining returns the quest to known. Abandoning is final and costs the giver's trust. |
| UnderSeal_DeliveredSealed_PaysAndEarnsTrust_TheItemIsGone | The letter is a real item. Delivering it removes it, pays and earns trust. |
| UnderSeal_ReadFirst_RevealsItsContents_AndTheRecipientKnows | Reading breaks the seal. A discovery is recorded. The recipient reacts to the opened letter. |
| TheHunt_CountsOnlyTheRightCreatures_NearTheRightPlace_AndSurvivingIsOptional | Only wolves near the bound settlement count. Survival is optional. |
| AlternativePaths_SatisfyOneGroup_AndOptionalNeverBlocks | Any one ending completes the resolution stage. Optional objectives never block. |
| AnEventForALaterStage_DoesNotCount_UntilThatStageOpens | A delivery made while stage 0 is open does not count. The same delivery counts once the stage opens. |
| AConsequence_IsAppliedOnce_EvenWhenItsTimingComesAgain | Running a timing's consequences again takes no more grain and adds no Chronicle entry. |
| AFailureCondition_FailsTheQuest_OnceAndWithItsReason | The giver's death fails the quest, with its reason, once. |
| AFoodShortage_RaisesARequest_OnlyOnce_AndItLapsesWhenTheShortageEnds | A shortage raises one relief request. Its cause is read from the world: an accepted request fails when the shortage ends, and stands while it lasts. |
| AMerchantOutOfStock_AsksForIt_AndTheDeliveryStocksTheShelf | An empty shop asks for supplies. The delivery stocks the shelf. |
| ADeed_Spreads_ByThePhase4Channels_AndEachListenerReactsOnce | Deeds travel through the existing knowledge channels. Each listener reacts once. |
| AReputationChange_WithTheSameKey_IsAppliedOnce | A person's trust and a town's reputation change once per key; a repeat is refused and the ledger holds one entry. |
| ReputationTiers_ComeFromSeparateDimensions | Trust, respect, fear, gratitude and hostility give distinct tiers. |
| FactionStanding_ChangesPrices_AndHostilityClosesTheShop | Standing changes prices. Hostility closes the shop. |
| Conditions_ReadThePerson_TheirKnowledge_AndTheContentRules | Conditions read regard, knowledge and content rules. Hidden content is not shown. |
| TheJournal_ShowsOnlyWhatThePlayerKnows | Hidden objectives, unknown people and places stay hidden. |
| Tracking_IsLimited_AndOnlyForQuestsInProgress | At most three tracked quests, and only active ones. |
| EverythingSurvivesSaveAndLoad_EvenMidConversation | Quests, the conversation, the social record and regard round-trip identically. |
| AVersion13Save_GainsTheQuestWorldOnce | Migration v13 → v14. The quest system is enabled. Genesis runs once. |
| AFailedChoice_ChangesNothing_AndTooHeavyARewardFailsTheWholeCompletion | Fault injection. A failed command changes nothing. A reward too heavy to carry fails the whole completion. |
| TheValidator_CatchesImpossibleQuestStates | Fault injection: unknown definitions, closed-but-unclaimed, duplicate instances, bad bindings and others. |
| CorruptQuestStateInASave_IsRejected | A hand-corrupted save is refused on load. |
| TheSameActions_GiveTheSameWorld | Determinism across two sessions. |

`QuestPerformanceTests` (1): `Quests_AreCheap` (see the report, section O).

Changed: `Phase2CompletionTests` (daily order includes the quest system), `Phase2Worlds.StripToV13`.

## PlayMode tests (`Tests/PlayMode/Phase12PlayModeTests.cs`)

All three drive the real UI: the "Talk…" button, dialogue responses by element name, the journal by element name.

| Test | Flow |
|---|---|
| ShortMeasures_ThroughTheRealUi_FromApproachToReward_AndAfterAReload | Approach, talk, accept, inspect the ledger, question the keeper and suspect, resolve, collect the reward, save, reload, check the journal. |
| UnderSeal_IsDeclined_ThenTaken_AndTheLetterArrivesAsAnItem | Decline, return, accept; the letter appears in the inventory. |
| AHunt_IsProgressed_ByAFightTheQuestStarts | The quest's dialogue starts a real encounter. Wolves defeated in combat progress the hunt. |

Changed: `ExplorationPlayModeTests` (the ledger test now checks the ledger records only what the player finds), `NpcPlayModeTests` (one more topic button: "Talk…").

## Mutation checks

Each mutant changes one rule in the source, the .NET suite is rebuilt and run, and the file is restored. The first versions of three mutants used a constant `if (false)`, which the compiler refuses (unreachable code, warnings are errors). They were rewritten as runtime conditions so they compile and actually test the suite.

| Mutant | Rule broken | Result |
|---|---|---|
| m1_rewards | `Complete` no longer refuses a quest whose rewards were already claimed | **Survived.** Equivalent through the public API: completion is reached only for open quests (`Process` skips closed ones), and the command is atomic, so `RewardsClaimed` cannot be true for an open quest. XP rewards are also keyed per quest. The guard is kept as defence in depth. |
| m2_ledger | `Reputation.Change` ignores a repeated key | Caught by AReputationChange_WithTheSameKey_IsAppliedOnce (added after the first run let it survive) |
| m3_states | A closed quest may move to any state | Caught by TheCatalogs_AreConsistent_AndStatesMoveOnlyAsAllowed |
| m4_stageopen | Events count for objectives in stages not yet open | Caught by AnEventForALaterStage_DoesNotCount_UntilThatStageOpens (added after the first run let it survive) |
| m5_consequence | Consequences no longer check their applied key | Caught by AConsequence_IsAppliedOnce_EvenWhenItsTimingComesAgain (added after the first run let it survive) |
| m6_cause | A request no longer lapses when its cause ends | Caught by AFoodShortage_RaisesARequest_OnlyOnce_AndItLapsesWhenTheShortageEnds (extended after the first run let it survive) |
| m7_groups | An alternative group needs every path, not one | Caught by 10 tests, including all three Short Measures endings and AlternativePaths_SatisfyOneGroup_AndOptionalNeverBlocks |

**6 of 7 caught**; the survivor is an equivalent mutant, explained above.
