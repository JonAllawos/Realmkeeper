# Phase 11: testing

All counts below are from runs on 2026-11-11, on the Phase 11 working tree before the commit.

## Suites

| Suite | Command | Result |
|---|---|---|
| .NET harness | `dotnet test Tools/DotNet/Realmkeeper.Verification.sln -c Release` | **574 passed, 0 failed** (Phase 10: 552) |
| Unity EditMode | `Unity.exe -batchmode -runTests -testPlatform EditMode` | **574 / 574 passed** |
| Unity PlayMode | `Unity.exe -batchmode -runTests -testPlatform PlayMode` | **96: 80 passed, 16 skipped, 0 failed** (Phase 10: 74 passed, 14 skipped). The 16 skipped are the capture sets, which run only with `RK_CAPTURE_DIR`. |
| Captures | `Unity.exe -runTests -testPlatform PlayMode -testFilter Phase11` with `RK_CAPTURE_DIR` | 8 tests, all passed after one harness fix (the camera was held across a save reload); 13 screenshots in `Documentation/Screenshots/Phase11/` |
| Mutation checks | `mutants/p11.sh` (each mutant breaks one rule; the .NET suite must fail) | **12 of 12 caught** by named tests (two first rewritten so they compile without the unreachable-code error) |

## New .NET tests (`Tests/EditMode/Items/`)

`ItemSystemTests` (21):

| Test | What it proves |
|---|---|
| TheCatalog_TablesAndRecipes_AreConsistent | Catalog, loot tables and recipes validate. The counts are printed. A new wand starts full. |
| Genesis_GivesEachSettlementAChestACacheAndSites_Deterministically_AndOnce | Every settlement has a chest, a cache and sites. The same seed gives the same caches; a second run adds nothing. |
| Chest_TakesAndGivesBack_KeepingEachItemsIdentity_AndOneHolder | Partial put makes a new instance and the rest keeps its ID. Taking back is lossless; coin moves both ways. |
| Containers_RefuseWhatTheyCannotDo_AndAFailedTransferChangesNothing | A cache takes no deposits; another town's chest is out of reach; take-all that is too heavy moves nothing. |
| ThePack_IsLimitedByWeight_AndOverCapacityTheSpeedIs5Feet | The limit is twice the SRD capacity; over capacity the speed is 5 ft and the hub says why; past the limit, refused. |
| Rings_GoOnEitherHand_AndALightWeapon_InTheOffHand_GivesABonusAttack | Two rings; a dagger in the off hand gives the off-hand attack; a battleaxe is refused. |
| MagicWeaponsAndArmour_AddToTheRules_AndConditionChangesThem | The +1 is a named part of the attack; shield +1 adds to AC; damaged −1; broken none. |
| Attunement_TakesAShortRest_IsLimitedToThree_AndCursesBind | An hour of work; +1 AC once attuned; refused at three; a curse binds, blocks unequip, attunement's end and equipping over it; a shrine lifts it. |
| Potions_HealOutsideFights_AndWands_RechargeOnALongRest | Refused when unhurt; 2d4+2 healing; the wand regains 1d6+1. |
| ASmith_MendsForAPrice_AndThePlayer_CanMendWithTools | The quote equals the rule; a changed price is refused; self-repair holds the item and settles by its check. |
| Crafting_HoldsMaterials_IsDeterministic_AndCancelling_ReturnsEverything | Materials held; one job at a time; cancel returns all; the outcome follows the roll; Fine on DC+5; failure returns the right share. |
| Crafting_IsRefused_WithoutStationToolOrMaterials | Missing materials and tools are named. |
| Gathering_IsFinite_AndRecoversWeekly | Stock falls when work starts; a worked-out site is refused; the weekly system restores it. |
| Loot_IsLeftInAContainer_FilledOnce_FromWhatTheFoesCarried | A real fight: nothing straight into the pack or purse; one loot container; a second fill is identical; take-all empties it. |
| LootTables_AreDeterministic_AndRespectRarity | 199 seeds: the same seed gives the same roll; a Common cap is respected. |
| ADamagedSword_SellsForLess_AndBecomesThatShopsParticularGoods | Half price; kept as goods with its state; bought back as that instance. |
| TheInventory_Filters_Searches_AndSorts_Stably | Groups, search, weight order, stable order, inspection lines and source. |
| EveryItemState_SurvivesSaveAndLoad | Charges, condition, make, provenance, acquisition, held work, containers and sites; the world section is identical after a round trip. |
| AVersion12Save_LoadsWithItsItemsIntact_AndGainsTheItemWorldOnce | Migration: items intact, catalog version 2, the gathering system enabled, genesis once, valid. |
| TheValidator_CatchesImpossibleItemStates | Fault injection: two holders, too many charges, bound without attunement, attunement where none is needed, a duplicated container, four attunements, an over-full site, a battleaxe in the off hand. |
| CorruptItemStateInASave_IsRejected_NotGuessed | An unknown condition or container kind refuses the load. |

`ItemPerformanceTests` (1): see `Performance.md`.

Changed tests:

- `CommerceTests`: the two-handed example is now the greatsword, because the spear is Versatile in SRD 5.2.1.
- `CharacterTests`: the Goliath's capacity is asserted against the SRD rule.
- `Phase2CompletionTests`: the daily order includes the gathering system.
- `Phase2Worlds`: adds `StripToV12`.

## PlayMode (`Tests/PlayMode/Phase11PlayModeTests.cs`, shipped scene, real UI)

Items are given by editing the saved file and loading it, the same path a player's own save takes through the loader's validation. There is no backdoor into the world.

| # | Flow | Test |
|---|---|---|
| 1 | The inventory shows its load and the rules' AC | TheInventory_… |
| 2 | Search narrows the list | TheInventory_… |
| 3 | A group filter shows only that group | TheInventory_… |
| 4 | Sort by weight puts the heaviest first | TheInventory_… |
| 5 | Inspect shows a magic item's rules and source | TheInventory_… |
| 6 | The comparison table, from the rules engine | TheInventory_… |
| 7 | Equip from the row | TheInventory_… |
| 8 | Rings on both hands; a dagger in the off hand; the Equipment section | TheInventory_… |
| 9 | Drink a potion when hurt | Potions_Attunement_… |
| 10 | Attune from Equipment; wait out the hour from Crafting & work | Potions_Attunement_… |
| 11 | A cursed axe binds; Unequip is refused with the reason | Potions_Attunement_… |
| 12 | Walk up to the chest and press E: Storage opens on it (the cache is also in the street) | TheChestInTheStreet_… |
| 13 | Store from the inventory, take back from Storage; coin left survives save and load | TheChestInTheStreet_… |
| 14 | Start a recipe, its materials held, wait it out, the result arrives, time passed | Crafting_AndGathering_… |
| 15 | Gather at a site; its stock falls; the herbs arrive | Crafting_AndGathering_… |
| 16 | At an armourer: sell a damaged blade (shown as damaged), see it in particular goods, buy it back, mend a mace | AnArmourer_… |
| 17 | After a won outing, the combat panel's loot button opens Storage; Take all | AFight_LeavesLoot_… |
