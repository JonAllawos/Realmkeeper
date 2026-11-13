# Phase 21: Completion report

**Phase 21 — Environmental World, Living Settlements, Functional Shops, Crafting Workshops & Realistic Interiors.** Dates: 2026-11-12 to 2026-11-13.

**Brief:** `../Briefs/Phase21-Brief.md`. **Decisions:** ADR-0105 to ADR-0108.

This report follows the brief's 22 items. Each claim is backed by a test, a capture or a reading of the code. **No one hand-played this phase:** see the owner checklist.

## 1. Starting and ending commit

| | Commit |
|---|---|
| Starting | `c10a313`: Phase 20A plus a documentation commit, both pushed at the owner's instruction before Phase 21 began |
| Ending | the local Phase 21 commit; its hash is in §21 |

The owner also asked for a documentation-only update midway (the Game Design Bible's amendments and `../Product/FutureRoadmap.md`). It is included in this commit, and Phase 21 was then resumed at the owner's word.

## 2. Preflight findings

See `Phase21-Preflight-Audit.md`. In short:
- there was no weather or season system;
- shop interiors were 11 boxes;
- three of a town's five shop buildings stood empty;
- entered interiors were nearly bare;
- smithies "restocked" bows and leather from iron;
- imports had no road and no record;
- the night sky rendered black.

## 3. Weather and season systems (ADR-0105)

- **Seasons come from the existing calendar.** Spring is Seedtide–Greenmonth, and so on; no dates change. Daylight runs from 16 h at midsummer to 8 h at midwinter (`SeasonRules`).
- **Regional climate** comes from what the world already knows: the region's kind, its latitude (the north is colder), mountains to the west (wetter), and a highland region to the west (a rain shadow, drier). `RegionClimate.Describe()` says why.
- **Weather.** A daily system (`realmkeeper.weather`, first in the day) works out eight three-hour periods per region:
  - fronts are born in the west and drift east about a region a day, so neighbours share weather;
  - moisture carries from day to day and is blended with neighbours';
  - temperature follows the season, plus a daily rhythm and cold fronts.

  The kinds are clear, partly cloudy, overcast, light and heavy rain, thunderstorm, light and heavy snow, fog, windy, and hot and dry. Each region also tracks ground wetness and lying snow. It is deterministic and saved (v23).
- **Effects, through existing systems only.**
  - **Production.** The economy's seasonal hook (`EconomyRules.WithSeasons`, used by the game) sets farming to 85 % in spring, 110 % in summer, 140 % in autumn and 65 % in winter; fishing, hunting and logging vary less. Each averages 100 % over a year (tested).
  - **Travel.** Each road hour is slowed by heavy rain (× 0.8), a storm (× 0.7), snow (× 0.85 or × 0.6), lying snow, a snowy pass and fog, and the hour's message says why.
  - **People.** Open-air workers shelter at home in a storm or heavy snow; the curious skip the market in rain; fewer people go to the tavern on a stormy night.

  No survival or temperature system was invented.
- **Shown in the street** (owner decision: built inside Unity, with no weather asset):
  - cloud dims the sun and greys the light;
  - haze thickens in rain, snow and fog;
  - wet ground darkens and glosses, and lying snow whitens ground and roofs (a snow surface replaces the texture under real cover);
  - rain and snow particles fall around the camera;
  - a world-pinned, wind-driven cloud dome reaches to the horizon;
  - stars and the moon show on clear nights, against a deep blue sky instead of the old black;
  - lightning with thunder;
  - generated rain and wind sound, at the player's sound volume;
  - sunrise, sunset and the sun's height follow the season.

  The HUD clock shows the weather and temperature, with the outlook, effects and climate on hover.

## 4. Environmental and terrain improvements

Within the settlement scene: weather, light, sky, wet and snowy ground, haze and the cloud dome (all above).

**Not attempted:** terrain reshaping and natural points of interest beyond the Phase 16 and 18 places. The preflight decided against them for this phase. Open-world terrain is planned as Phases 23–25 (`../Product/FutureRoadmap.md`).

## 5. Vegetation and scenery

- No new vegetation.
- Seasonal foliage (autumn colours, bare winter trees) was planned and **not done**. It is deferred, with the terrain work.
- Snow does whiten grass and ground.

## 6. Shop types and real inventory integration (ADR-0106)

- **Twelve kinds** from one data catalog (`ShopKinds`): the six Phase 5 kinds, plus bakery & provisions, leatherworks, tailor, bowyer & fletcher, apothecary and carpentry.
- **One inventory.** Shops are still the Phase 5 shops: one stock, one till (the keeper's purse), a resident keeper. A kind only says what a shop deals in, and `CommerceMath.Sells` asks it, so restocking, quests and dialogue agree.
- **Existing shops keep everything.** On load, each is given the kind it was made as.
- **The smithy deals in metalwork only.** Stock it already held stays on its shelf.
- **Specialists fill the empty shop buildings.** Towns gain them in planned shop buildings that no location or explored interior uses (villages gain a leatherworks); each building is marked as a site.
  - **Keepers.** Each is kept by a townsperson. Towns had only nine named people, so the keepers are named from the town's own population, the way Phase 10 named its priests and guards: deterministic IDs and names, never more named people than the population, once only. They get homes, natures and character records like everyone else.
  - **Result.** In the six-region test world every town now has three to six specialists.
- **Tests:** `Phase21ShopTests`. Kinds are sound, specialists only in unused shop buildings, keepers are residents, stock holds only each kind's goods, placing again changes nothing, and kinds survive a save. An older save's shops get the kinds they were made as.

## 7. Merchant supply and restocking

- **Supply records.** Each shop keeps its 40 latest: made here from the stores, brought in by road (the town named), made at the bench by the keeper, or bought from the player.
- **Imports need an open road.** An item with no modelled material is bought in only along a trade route that is not suspended. Tested: with every road shut nothing comes in, and the shortfall says so; with one open, it is imported and recorded.
- **Shortfalls.** The trade window lists what the shop deals in but has none of, with the reason.
- **Workshops make rather than buy.** A workshop does not buy in what it makes, keeps of each input just what its smallest batch takes, and restocks inputs first.
- **The keeper's float.** Keepers keep 10 coin back from restocking to buy from customers; a new shop's opening stock may use the whole purse. Without the float, restocking emptied purses, and a keeper could not buy a dagger from the player (an existing test caught this).

## 8. Physical shop displays

- **Wares stay real.** They are exactly the shop's stock, one model per unit as in Phase 5.
- **Furniture per trade.** Each fit-out now has its trade's furniture from the CC0 kits, measured to size:
  - **smithy:** forge with fire, anvil, quench barrel, weapon stand, bucket;
  - **leatherworks:** bench, peg rack, tanning tub, stool;
  - **tailor:** cutting table, dress form;
  - **bowyer:** bench, weapon stand;
  - **apothecary:** still over a fire, bottle shelf, bookcase, candles on the counter;
  - **bakery:** a brick oven with its fire, apples and carrots;
  - **general store:** barrel, crate, chest.
- **A pre-existing bug fixed:** shop interiors ignored the building's floor height, so on raised foundations the counter and fittings sank into the floor.
- **Empty displays stay empty.** Where stock is thin, the shelves are thin.

## 9. Functional crafting stations (ADR-0107)

- **The stations:** forge, woodworker's bench, leatherworker's bench, sewing table, bread oven, kitchen hearth and apothecary's still. Each is kept by the shops of a kind.
- **Using one.** The player interacts with the station in the shop. The Hub opens on crafting with a note; work started there names the shop, and the command checks that it is open with its keeper there. Refusals name the shop and the reason.
- **Old rules still hold.** The Phase 11 place rules (the shrine's still, the tavern's kitchen, a village workshop) remain, so nothing that worked before stops working.

## 10. Recipe and production additions

- **15 new recipes, 26 in all.** By station:
  - **forge:** shortsword, hand axe, iron cap, chain shirt;
  - **leather bench:** bracers, leggings, riding boots, backpack;
  - **sewing table:** linen tunic, padded coat;
  - **woodworker's bench:** hunting bow, arrows, round shield;
  - **oven:** bread, travel rations.

  All use real catalog items, and `RecipeCatalog.Validate` passes.
- **Keepers' production.** Each day after restocking, a keeper makes what the shop is short of, at their own station, from their own stock, within an 8-hour working day. It is recorded as made at the bench, by whom and from what.
- **Seen in ten simulated days:** the smithy forges shortswords from bars, the leatherworks cures hides and stitches bracers and slings, the tailor sews tunics, the apothecary brews potions, and the bakery bakes bread and packs rations.

## 11. Building and interior improvements (ADR-0108)

- **A furnishing planner in the domain** (`InteriorFurnishing`) picks pieces by room kind:
  - bedroom: bed, nightstand, chest, chair;
  - hall: table, chairs, cabinet, barrel, shelf;
  - kitchen; workshop; cellar and store; office; shrine; armoury; cell.
- **It is deterministic from the place's ID and validated:**
  - every piece inside its room;
  - never on a doorway, stair, the cells around them, or a feature's cell;
  - no two pieces in one cell;
  - at least half of each room clear.

  Tested over 540 generated interiors of nine building kinds; 160 of 160 bedrooms got a bed.
- **Scenery only.** The plan is instantiated with the CC0 kits, with colliders for solid pieces and a warm light in each furnished room. Nothing is saved, and the beds, chests and hearths the rules use remain the room's features.
- **Not done:** the dark slab doors were not changed.

## 12. NPC profession and activity behaviour

- **Errands.** On a free day at home, one in two people spends an hour of the afternoon at one of their town's shops (not their own), within its hours, unless it rains, snows or storms. It is pure and deterministic, and tested.
- **Shelter.** People shelter from storms (§3).
- **Keepers at work.** They are at their shops in working hours, as before. The stations are not animated with working poses: the existing work pose is used.

## 13. UI changes

- **HUD clock:** weather and temperature, with a tooltip for the outlook, effects, sunrise, sunset and climate.
- **Trade window:** the shop's trade, its shortfalls and reasons, and its latest supply (all on hover).
- **Crafting:** recipes show their physical station and whether the town has one; a station note appears when opened from a station.
- **Travel:** the hour's message says what slowed it.

## 14. Asset sources and exact licences

- **No new third-party assets.** Furniture and shop fittings use models already integrated and credited in Phase 18 and 20A (Quaternius Fantasy Props MegaKit, CC0 1.0, in Phase 18; KayKit Dungeon Remastered, CC0 1.0: the barrel stacks, crates and floor pallet).
- **Generated in code:** weather particles, the cloud texture, stars, the moon disc, the snow surface, and the rain, wind and thunder sounds.
- `CREDITS.md` is unchanged.
- **Local only:** store-licensed and QAL packs stay git-ignored.

## 15. Save schema and migrations

Schema **v23**.

- **New data:**
  - the world's `environment` (each region's day, moisture, ground and snow; the fronts);
  - a shop's `kind` and `supply`;
  - specialist shops as new locations on unused planned shop buildings (marked as sites, each with its structure).
- **`MigrationV22ToV23`** puts the weather system first in an old save's enabled systems, and writes nothing else.
- **On load:** weather is worked out from the seed; older shops get the kind they were made as; the living-world step adds specialists once.
- **Tested:** exact weather round trip, with continuation equal to an unsaved run; a v22 save; kinds and records round-tripping.
- **A latent fix.** Emergent quests are now offered on load only when the daily system would offer them (a new quest log, the week's turn, a food shortage). Before, every load offered them. Production empties input stock, which exposed this in the save-and-continue tests.

## 16. Exact test results

Run strictly one suite at a time, on the final code:

| Suite | Result |
|---|---|
| .NET (Tools/DotNet) | **898 passed, 0 failed** (Release, 3 min 56 s) |
| Unity EditMode | **898 passed, 0 failed**, 0 skipped (6000.0.47f1, batch mode, 41 min) |
| Unity PlayMode | **128 passed, 0 failed**, 2 inconclusive, 32 skipped (as Phase 20A: the two inconclusive and the skipped are captures and optional runs without their environment variables) |
| Unity compile | No errors or warnings |

There are 30 Phase 21 .NET tests, in `Tests/EditMode/Weather/`: weather (12), shops (7), stations (5), interiors (3), life (2) and performance (1). `Phase21Trades` is a test helper, not a test. The PlayMode capture tests for weather and the walkthrough pass.

Older tests changed, each because the design changed:
- schema pins 22 → 23;
- the daily order now starts with weather;
- the count of systems in a faked v4 save;
- named-people counts now exclude the new keepers, recognised by their own ID stream;
- the layout test counts specialist shop buildings;
- the sites test accepts plan buildings marked as sites;
- in PlayMode, two people-list tests count the town's keepers apart (the town shows 12 people, not 9), and the clock-light test shows fair weather before measuring the morning sun (the simulated morning was cloudy, which rightly dims it). On the first full PlayMode run these three failed; after the change they pass.

## 17. Performance measurements

Six-region world, .NET, Release:

| What | Cost |
|---|---|
| Weather | 0.07–0.10 ms a simulated day for six regions |
| Shops' restock and production | 14 ms a simulated day for 67 shops, after caching each kind's work profile (28 ms before) |

The shop figure is the larger part of a day's tick. The exploration frame cost of weather (particles at most 4,000 rain and 3,500 snow; one cloud dome) was not profiled on a frame budget.

## 18. Screenshot inventory

`../Screenshots/Phase21/`, all at 1920×1080:

- **`before/`** (24): the preflight walkthrough.
- **`after/`** (33 and a log): the same walkthrough after the phase, adding each shop toward its door so the trade furniture shows.
- **`weather/`** (14 and a log), each kind of weather in the same street:
  - the simulated weather now;
  - clear noon, partly cloudy, overcast;
  - light rain, heavy rain, thunderstorm;
  - morning fog;
  - light and heavy snow;
  - midwinter dusk and a midsummer evening;
  - a clear night and a rainy night.

**Not captured** (gaps against the brief's §13 list):
- the 1366×768 and compact-ratio sets;
- forest, water and mountain scenery, and seasonal foliage;
- the gameplay moments: buying, selling, a shortage shown in the window, using a station, starting and collecting a job, a keeper at a station, a customer, and save and load of stock.

These are covered by tests where they are rules. The owner checklist asks for them by hand.

## 19. Owner playtest checklist

`Phase21-Owner-Playtest-Checklist.md`.

## 20. Remaining limitations and deferred work

- **The shop economy.** Keepers' purses run low after some days of restocking, because NPC customers do not buy with money in the simulation (the errands are presence, not purchases). The float keeps them able to buy from the player. A real customer economy is future work.
- **Not attempted:** seasonal foliage, terrain and vegetation, and natural points of interest (deferred to the open-world phases).
- **Keepers' production** rolls no checks, and some recipes are never used by keepers (smelting, chain shirts) because their inputs are not kept.
- **No animated work** at stations; doors are unchanged; furniture is scenery only.
- **Lightning** is a light flash, with no bolt model.
- **Missing captures** (§18).
- **Not hand-played.**

## 21. Git status and local commit hash

`68b3063` on `feature/phase-0-1-foundation` (local only). After it, the working tree is clean apart from Unity's test scene `Assets/InitTestScene*`, which is never committed.

## 22. Push

No push was made. Phase 21 is committed locally only, as the brief requires. Phase 22 has not been started.

## 23. Closeout: the purchased kits (2026-11-13, after this report)

The owner bought the full versions of three Quaternius kits, and they were integrated as Phase 21's closeout, in their own local commit.
- **Medieval Village MegaKit Pro.**
  - **Doors:** every working door now has the kit's door model, scaled to the doorway, so there is no gap.
  - **Signs:** shop signs hang outside the smithy, provisions, apothecary and bowyer.
- **Fantasy Props MegaKit Pro.** Homes get real beds (double, twin and bunk), nightstands, dressers, desks and rugs; the planner's validation is unchanged. Shops get bellows and a hammer rack in the smithy, farm crates in provisions, and a vice bench in carpentry.
- **Stylized Nature MegaKit Source.**
  - **Trees by region:** pines in the highlands, birches by the lakes, tall broadleaves in woodland, cherry trees in the vales and lowlands.
  - **Seasonal leaves:** summer as made, spring fresher, autumn orange and gold, winter dull and dusted with snow. Pines and cherry trees keep their colour.
  - **How the leaves are coloured:** the kit paints its leaves green into the texture, so a tint alone could not turn them. Outside summer, the kit's white leaf textures are used and coloured outright.
- **The weather capture test** now sets the season on its made-up weather. Before, every shot was drawn as spring. The game's own weather always set it.
- **§5 and §20 are partly superseded:** seasonal foliage is now done.
- **Checks made:**
  - every one of the art catalog's 1,158 references resolves;
  - the git-ignored local packs (Bestiary, Fantasy Races) are not in the tree;
  - the licences were read: all three kits are CC0 1.0 (`CREDITS.md`, `Art/AssetInventory.md`).
- **Tests:**

  | Suite | Result |
  |---|---|
  | .NET | 898 of 898 passed |
  | The weather capture test, in Unity PlayMode | passed, with its captures inspected |
  | Full Unity EditMode | stopped part-way at the owner's instruction (2026-11-13); not run to completion |
  | Full Unity PlayMode | not run, at the owner's instruction (2026-11-13) |

- **Screenshots:** `../Screenshots/Phase21/kits/` (11).
