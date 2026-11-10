# Realmkeeper Phase 5: Living World, Player Hub & Commerce

**Branch** `feature/phase-0-1-foundation` · **Base** `277bd48` (Phase 4) · **Save schema** v6 → v7 · **Date** 2026-11-10

Phase 5 adds three things, all running on the existing world and NPC simulation:

- **A clock and a living settlement.**
  - The light follows the time.
  - People walk the streets to where the world says they are each hour and do what it says
    they are doing.
- **A persistent player character** with an inventory, equipment that shows on the body, and an
  eight-section Player Hub.
- **Shops kept by existing residents.** They are stocked from the settlement's own stores and sell
  through atomic, explained, chronicled trades.

Bramhaven's Outfitters and Smithy are walk-in shops. Every unit of stock stands on their shelves.
Everything below was run, and the figures are from those runs.

---

## 1. Pre-flight findings

The full record is in `Documentation/Phase5/PreFlight.md`, written before any code.

**Repository.**
- HEAD `277bd48`, equal to origin.
- The working tree was clean and Unity was closed.
- Save schema v6.

**What existed.**
- **The clock:** a day-only clock; playback ran at 1 day per second.
- **The player:** a persistent `PlayerCharacterData` holding identity, location and pose. It had no
  items, money or appearance.
- **The economy:** aggregate resources per settlement, plus personal NPC savings.
- **Locations:** shop and workshop locations existed (`house-0`, `house-1`), with candidate keepers
  (merchant, smith, innkeeper, labourer, farmer). There were no shops.
- **NPC routines:** one place per day. Residents idled on the spot.
- **Buildings:** only the tavern could be entered.

**Art.**
- Outfit models whose parts are separate meshes.
- No weapon, shield or hat models in any CC0 pack.

**Conflicts, and how they were resolved:**

1. **Time of day.** The day remains the simulation's unit. Minutes sit on top of it, and hourly
   behaviour is derived, not stored (ADR-0019).
2. **Item ownership.** Items are definitions plus stacks with exactly one owner. Settlement resources
   stay aggregate, and shop restocking is the only bridge between the two (ADR-0016, ADR-0018).
3. **Shopkeepers.** They are existing NPCs, and a keeper's till is their own savings.
4. **Equipment.** Only renderable equipment exists. Clothing uses the outfit parts; weapons, shields
   and hats are procedural.

**One plan was revised during the work.**
- **Plan:** "1 game hour per 2 s" at Normal.
- **Change:** Normal is now 2 game minutes per second.
- **Reason:** at the planned pace, a resident walking 40 m would have arrived many game-hours late,
  so walking could never match the schedule.
- **What did not change:** the realm map keeps 1 day per second.

## 2. World clock

**Model.**
- `WorldData.MinuteOfDay` (0–1439, saved) sits alongside the date.
- `WorldTime` provides minute arithmetic, and the clock is formatted as "HH:MM".
- `DayPart` has data-driven ranges:

  | Part | Starts |
  |---|---|
  | Morning | 06:00 |
  | Midday | 11:00 |
  | Afternoon | 13:00 |
  | Evening | 18:00 |
  | Night | 22:00 |

**`AdvanceTimeCommand(minutes)`.**
- Each midnight crossed runs one full transactional simulated day, so the daily systems are
  unchanged.
- The minute is then set.
- The player loses 2 fed per hour mark passed, counted on absolute time.
- A test proves 200 × 10-minute steps give byte-identical worlds to one 2000-minute step.

**Pace.**
- On the realm map: whole days, unchanged; the time of day is left alone.
- In a settlement: minutes.

  | Speed | Game minutes per second |
  |---|---|
  | Slow | 0.5 |
  | Normal | 2 |
  | Fast | 10 |
  | Very fast | 60 |

- A hitch is clamped to 120 minutes.
- The HUD shows the clock with Pause (P), speed, "Wait 1 h" (T) and "Hub" (H).

**Lighting** (`ExplorationAtmosphere.SetTime`) is a pure function of the minute:
- The sun rises at 06:15 and sets at 19:30 on an east–west arc, with a golden tint near the horizon.
- After sunset the sun drops below the horizon, so the procedural sky darkens, and a faint blue moon
  takes over.
- Ambient light, fog colour, density and exposure all follow the time.
- From dusk, **real point lights** come on at the 16 street lamps and door lanterns, and window glass
  glows.
- Materials are copied per visit, so assets are never edited.

## 3. NPC movement and schedules

**`NpcActivityPlan.At(world, npc, date, minute)`** is a pure, deterministic function over the Phase 4
day. It decides where each person is and what they are doing:
- Each person has their own wake time (06:00–07:00) and sleep time (21:30–23:00), hashed from their
  ID.
- People sleep at home and eat breakfast there.
- They work in role hours, and **keepers work their shop's hours**. They take a midday meal.
- On rest days they go to their rest-day place.
- In the evening they go to the tavern if sociable, otherwise home.
- People who are unwell stay home.
- Today's main place is the day's committed location: the routine, a need, or the DM's word.

The plan yields an activity (sleeping, eating, working, trading, guarding, sitting, worshipping,
socialising, resting, standing), a reason, and the time the activity lasts until.

**Visible movement.**
- When someone's place changes, their `ResidentView` walks a street route (`LocalPaths.Route`) at
  1.8 m/s: out of the door, along the nearest street, round the corner of the crossing, and in at
  the destination.
- At home or asleep, they go indoors: hidden and not approachable.
- After a jump in time they are placed directly.

**Poses** come from new animator states built from CC0 clips, driven by the real activity:
- kneeling at work (`Fixing_Kneeling`);
- kneeling in prayer (`Crouch_Idle_Loop`);
- chatting at the tavern (`Idle_Talking_Loop`).

The interaction prompt names the activity, e.g. "Talk to Elric Brooke (Merchant · trading)".

**Proof.** A PlayMode test watches a keeper:
1. hidden at home at 22:30;
2. setting off in the morning;
3. walking, and arriving behind their own counter.

The screenshots show a resident walking to the keep at 09:00 and a labourer kneeling at work at the
granary.

**A bug found and fixed.** The strengthened "absent keeper" test caught it: the hourly plan
recomputed a pressing need's place and could put a DM-moved keeper back at their counter. The plan
now treats today's committed place as authoritative.

## 4. Player character model

`PlayerCharacterData` (ADR-0015) adds:

| Field | Range or note |
|---|---|
| Species | Human (the only bodies the art has) |
| Age | 16–90 |
| Background | 1–40 characters |
| Description | Up to 400 characters |
| Appearance | Frame, hair, beard, skin tone, hair colour, cloth tint |
| Purse | Never negative |
| Fed | 0–100 |
| Inventory | At most 40 stacks |
| Equipment | 8 slots |
| `IsOutfitted` | The starting kit has been given |

**New worlds, and pre-v7 saves (once), get:**
- the clothes the Phase 4 body already showed (linen tunic, sleeves, wool trousers, simple shoes);
- a knife, 3 loaves, 150 coin, and fed 80.

**Effects.**
- Carrying more than 25 kg slows the player, down to 55% at 50 kg.
- Hunger below 25 slows them ×0.85.
- The Hub states the reason.

## 5. Player Hub screenshots

The Hub opens with H or the HUD button.

- **Sections:** Character, Appearance, Inventory, Equipment, Weapons, Clothing & armour,
  Currency & possessions, Info.
- **Live 3D preview:** the same body builder as the street, on a lit stage on its own layer, filmed
  into a render texture. Drag to turn it; use the wheel to zoom.
- **Captured** at 1920×1080 in the shipped scene:
  - `p5_09_hub_inventory`: the Inventory section;
  - `p5_10_hub_equipment`: the Equipment section, with the arming sword in the preview's right hand;
  - `p5_11_hub_weapons_zoomed`: the Weapons section, with the preview zoomed.

All screenshots are in `Documentation/Screenshots/Phase5/`.

## 6. Appearance and equipment

**Appearance** is limited to what the art draws (`PlayerAppearanceOptions`):
- two frames;
- per frame, three hair parts or none;
- a beard (male frame only);
- 5 skin tones, 6 hair colours and 4 cloth tints.

Changes apply at once. The command and the validator both enforce the limits.

**Equipment.**
- **Slots:** main hand, off hand, head, chest, hands, legs, feet, back.
- **Rules:**
  - an item goes only in its own slot;
  - a two-handed weapon clears the off hand;
  - a shield puts a two-hander away;
  - one unit is taken from a stack;
  - the displaced item returns to the pack.
- **Effects:** protection, weapon class and damage, weight and worth are shown. There is no combat in
  this phase.
- **Visuals (ADR-0017):**
  - Clothing uses the outfit models' own parts (body, arms, legs, feet, hood, pauldrons), swapped
    per slot, with cloth variants. Empty slots show undyed underclothes. A hood hides the hair.
  - Procedural props:

    | Item | Where it goes |
    |---|---|
    | Hats | Head |
    | One-handed weapons | Right hand |
    | Shields | Left forearm |
    | Spear, staff and bow | Slung on the back |

  - The body rebuilds whenever the look changes.
  - Tested in PlayMode: the knife appears in `hand_r`; underclothes show after taking off the
    tunic; the female frame rebuilds.
  - Screenshot `p5_12_player_equipped`: the sword in hand, in the street.

## 7. Inventory and item architecture

**Definitions and instances** (ADR-0016).
- `ItemCatalog` holds 30 definitions: 7 weapons, 2 shields, 3 headwear, 9 clothing and armour,
  3 food, 4 trade goods. Each has value, weight, stacking, slot, stats, **materials** and the shop
  categories that sell it.
- Instances are `ItemStackData` (stack ID, item ID, quantity). Each lives in exactly one place:
  inventory or one slot.
- Shop stock is a count.

**Commands.** Split, combine, discard and use (eat) are validated and atomic.

**The validator rejects:**
- a stack ID seen twice (duplication);
- unknown items;
- bad quantities, or more than the stack limit;
- an item in the wrong slot;
- a two-hander with an off-hand item;
- a negative purse;
- a look the art cannot render.

## 8. Shop and shopkeeper architecture

**`ShopData`** (saved) holds:
- ID, settlement, location and name;
- categories and hours;
- **the keeper's NPC ID**;
- stock;
- last restock date and sales totals.

**Genesis** runs once, per settlement without shops.

| Settlement | Shops |
|---|---|
| Town | Outfitters, Smithy, Tavern Kitchen, Granary Yard |
| Village | General Store, Tavern Kitchen |
| Hamlet | Farm Stall |

- Each shop is kept by an **existing resident** with the matching role, whose workplace and routine
  then follow the shop.
- No vendor is ever invented.

**Availability** explains itself:
- no keeper, or the keeper has died;
- unrest;
- outside hours;
- "X is not here (at the tavern: …)";
- at the midday meal.

Nobody stands in for an absent keeper. Tested in .NET and in PlayMode.

**In the world** (Outfitters, Smithy):
- an open archway, plank floor, ceiling and doorway-aware collision;
- a counter, shelves, a rack and, in the smithy, a forge and anvil;
- **every unit of stock as its own model**, rebuilt on every stock change;
- the keeper behind the counter when working.

Trade happens at the counter (E) or via "Trade" in conversation.

## 9. Buying and selling

**`BuyItemCommand`** follows the brief's nine steps on one working copy:
1. check availability;
2. check stock;
3. check the price is **the one shown** (a changed price is refused, with the new price stated);
4. check funds;
5. take the coin;
6. give the goods;
7. reduce the stock;
8. credit the keeper;
9. record the trade in the ledger and the chronicle.

The working copy is validated whole, then committed or discarded. `SellItemCommand` mirrors it. The
shop must want the item, and the keeper must be able to pay.

**Tests.**
- Every refusal leaves the saved world **byte-identical**: price moved, too few in stock, zero
  quantity, unknown item or shop, insufficient funds, closed at night, wrong settlement, keeper
  away.
- The full sequence is checked:
  - coin out equals keeper coin in (none created);
  - goods in;
  - stock down by one;
  - ledger and chronicle ("Maud the Wayfarer bought …");
  - an exact save and load round trip.
- **Real scene:** at Bramhaven's Outfitters counter, the Buy button bought linen sleeves:
  - purse 150 → 146;
  - stock 2 → 1;
  - the shelf showed one fewer.

  After saving and loading, all of that held, and the rebuilt shelves still matched the stock
  (`p5_06`–`p5_08`).

**Prices are explained.** The trade window lists each factor, e.g. "Base 3: Shop markup +20%,
Keeper's haggling +2%, Last ones +15% → 4 coin".
- Selling price factors: shop markup, the keeper's haggling skill, how well they know you, scarcity,
  being well stocked, the town's fortunes, unrest, food shortage.
- Bounds: selling prices 85–170% of base value; buying prices 30–80%.
- **Buying back always costs more than selling earned** (tested).

**Reputation.**
- Trading (+3 familiarity, +1 affinity) and talking (+2 familiarity) count **once per day per
  person**. Repeats add nothing (tested).
- Familiarity lowers prices.
- **Keepers remember** their first trade with the player and any trade of 25 coin or more, as a
  private memory.

## 10. Economic integration

ADR-0018 has the full rationale.

**The till is the keeper's own savings.** There is no second money pool.

**Restocking** (`ShopSystem`, order 430, after the NPC engine and before politics) works per unit:
- It consumes the item's materials from the settlement's stores, booked as **Consumed** in the
  economy flow. No food is taken in a shortage.
- It pays their value into the **settlement treasury**, booked as imported coin.
- It is paid for by the keeper.

**Targets** scale with prosperity: ×0.5 at 0, ×1.5 at 100; hamlets hold two-thirds.

**Consequences in the world:**
- Prosperity sets stock levels and prices.
- Low stores (poor production or trade) limit restocking.
- Shortages raise food prices and stop food restocking.
- Unrest shuts shops and stops restocking.
- Keepers' wealth rises and falls with sales.
- Shop hours follow keepers' routines.

**The 200-day conservation test still balances** with shops running. Writing it exposed two real
bugs, both now fixed:
- restocking took materials without booking them;
- generation stocked shops before the economy set the starting stores.

**Performance of commerce at scale (.NET, Release, this machine):**

| World | People | Shops | Simulated day | One hour of everyone's whereabouts + shop availability | All prices |
|---|---|---|---|---|---|
| Default | 15 | 7 | 0.44 ms | 0.021 ms | 0.04 ms |
| ~100 people | 99 | 7 | 2.54 ms | 0.138 ms | 0.68 ms |
| ~300 people, 10 settlements (synthetic) | 300 | 25 | 8.24 ms | 0.508 ms | 0.43 ms |

## 11. Persistence and migration

**Schema v7 adds:**
- `minuteOfDay`;
- the player's fields (§4);
- `shops` (keyed stock maps);
- `transactions` (the last 500);
- each NPC's `playerRegard`.

**`MigrationV6ToV7`** is structural:
- 08:00, empty shops and ledger;
- `realmkeeper.shops` enabled after the NPC system.

After mapping, `CommerceGenesis` runs **once**. It adds shops for settlements without them, kept by
existing residents and paid for from their own stores, and gives the starting kit if not yet
given. It invents no trades, events or history.

**Tests:**
- A v6 save, built from the same seed with generation's new pre-commerce mode, loads
  **byte-identical to a fresh v7 world**.
- Reloading a v7 save never re-initialises.
- v4 and v5 migrations still produce fresh-world-identical results.
- Exact v7 round trips.

**Validator rules (`WorldValidator.Commerce`):**
- the clock's range;
- belongings (above);
- shops: settlement and location match, the keeper exists, one shop per keeper, hours, categories,
  known stock items, positive and unique stock lines;
- the ledger: unique IDs, total = quantity × price, and size.

## 12. Test results and regression

All runs were on this machine on 2026-11-10, at the final code.

| Suite | Result |
|---|---|
| .NET harness (`dotnet test`, Release) | **346 passed, 0 failed** (323 before Phase 5) |
| Unity EditMode (batch) | **346/346** |
| Unity PlayMode (batch, shipped scene) | **57/57**, 0 failed, 0 skipped (screenshot captures run with `RK_CAPTURE_DIR`) |

**New tests.**
- **.NET / EditMode** (`Commerce/CommerceTests.cs`, 20 tests):
  - genesis;
  - the starting kit;
  - the full buy sequence with save and load;
  - every refusal;
  - another settlement;
  - an absent keeper;
  - a closed shop;
  - selling and no arbitrage;
  - regard and memories;
  - explained prices;
  - equip, unequip and two-handed rules;
  - split, combine, discard, eat;
  - appearance limits;
  - clock, midnight and step equivalence;
  - both playback paces;
  - hourly routines;
  - knowledge-bound topics;
  - v6→v7 migration once only;
  - validator mutations;
  - restocking from stores and in unrest.
- **Performance:** `CommercePerformanceTests`, 3 tests.
- **PlayMode** (`Phase5PlayModeTests`, 4 tests):
  - the purchase at the counter, with save and load;
  - the Hub's eight sections and equipment on the body;
  - the light and residents following the clock, and a keeper walking to work;
  - a shut shop with nobody in for the keeper.
- **Capture:** `Phase5CaptureTests` (evidence, gated).

**Earlier tests updated to the new, intended behaviour (none weakened):**

| Test | Was | Now |
|---|---|---|
| Schema version expectations | 6 | 7 |
| Daily system order | — | Includes shops |
| Old-save helpers | — | Strip v7 fields; source saves are built pre-commerce |
| "People where they are today" | Today | This hour |
| "One enterable building" | 1 | The hall plus each house shop (asserted ≥ 2 and exact) |
| Three PlayMode tests that took "the first resident" | First resident | The first resident who is out of doors (a resident indoors this hour cannot be approached, by design); body parts counted including those indoors |

**Mutation checks** (each bug was introduced deliberately, the suite run, then the bug reverted):

| Mutant | Caught by |
|---|---|
| Currency duplication (coin not taken) | `Buy_FullSequence_IsAtomicAndRecorded` |
| Item duplication (equipped and left in the pack) | `Equip_Unequip_TwoHanded_AndInvalidEquipment` (the validator rejects the shared stack ID) |
| Invalid equipment (command check removed) | Survived alone. The validator still rejected the world, so the behaviour stayed correct. With the validator rule also removed, two tests failed. |
| Stock not decreasing | `Buy_FullSequence_IsAtomicAndRecorded` |
| Partial transaction (coin taken, goods not given) | `Buy_FullSequence_IsAtomicAndRecorded` |
| Detached shopkeeper (trades without the keeper present) | `AbsentShopkeeper_ShutsTheShop_InOpeningHours…` |
| Time and lighting desynchronised (light not updated) | PlayMode `TheLight_AndTheResidents_FollowTheClock…` (expected 480) |
| Inventory not persisting (not written to the save) | `Buy_FullSequence…` (strengthened during this work) and `Equip_…` |

## 13. Actual Unity scene verification

The screenshots were taken by `Phase5CaptureTests` in the shipped scene at 1920×1080. It drove the
same commands and windows as the player, with the HUD rendered into the image. The capture log beside
the images records the world state behind each shot.

| Shot | Shows |
|---|---|
| `p5_01_town_morning` | Bramhaven at 08:00: sun elevation 22°, lamps off |
| `p5_02_resident_walking` | Edda Thorne walking to the keep at 09:00 ("Working hours") |
| `p5_03_resident_working` | Ulric Dunmore, labourer, kneeling at work at the granary until 17:00 |
| `p5_04_shop_interior` | Brooke's Outfitters, open: 18 wares on the shelves = stock 18; the keeper behind the counter |
| `p5_05_shopkeeper_conversation` | Elric Brooke answering "What's for sale" from his own stock; the 12 topics plus Trade |
| `p5_06_trade_window` | Before the purchase: linen sleeves ×2 at 4 coin, with the price explanation |
| `p5_07_purchase_complete` | "Bought Linen sleeves for 4 coin": purse 150 → 146, stock 2 → 1, ledger line |
| `p5_08_reduced_stock` | The same shop afterwards: 17 wares, sleeves 1 |
| `p5_09`–`p5_11` | Player Hub: inventory, equipment (sword in the preview), weapons zoomed |
| `p5_12_player_equipped` | The character in the street with the sword in hand |
| `p5_13_town_golden_hour` | 19:15: sun 3°, lamps coming on |
| `p5_14_town_night` | 22:40: moonlight, 16 lamps lit, windows glowing, everyone indoors |
| `p5_15_plaza_night` | The plaza at night |

## 14. Performance measurements

**Simulation:** the .NET table in §10. The NPC engine's own Phase 4 performance tests still pass.

**Development build** (Windows, Development, this machine). The probe (`-rkPerfProbe`) walks the
Phase 4 poses, then the Phase 5 ones:
- inside a shop;
- the street with the clock at ×30 (residents re-planned and walking);
- the street and plaza at night, with every lamp lit.

Measured on Windows: RTX 4070 Laptop GPU, i9-14900HX, 1920×1080, kit art.

| Pose | Mean frame | p95 | Draw calls | Triangles |
|---|---|---|---|---|
| Map | 2.13 ms (470 fps) | 4.35 ms | 153 | 0.20 M |
| Gate | 2.82 ms | 4.46 ms | 874 | 2.20 M |
| Street | 2.77 ms | 4.78 ms | 693 | 1.97 M |
| Building | 2.70 ms | 4.63 ms | 821 | 1.76 M |
| Plaza | 2.65 ms | 4.90 ms | 477 | 1.15 M |
| Tavern interior | 2.57 ms | 4.87 ms | 236 | 0.72 M |
| Overview | 2.77 ms | 4.26 ms | 1150 | 2.43 M |
| **Shop interior** | 2.77 ms | 4.87 ms | 871 | 1.68 M |
| **Street, clock ×30** (08:11 → 09:23; residents re-planning and walking) | 3.90 ms (257 fps) | 7.46 ms | 703 | 1.99 M |
| **Street, night** (16 lamps lit) | 2.74 ms | 4.78 ms | 649 | 2.03 M |
| **Plaza, night** | 2.45 ms | 4.71 ms | 443 | 1.14 M |

Memory was 545 MB system and 253 MB graphics.

- The night lights cost nothing measurable (Forward+, no shadows on lamps).
- The fast clock adds about 1.1 ms a frame: a minute advance, presence and route planning, shop
  refreshes, and body checks on each clock change.
- The Player Hub preview renders only while the Hub is open (on demand and on changes).
- Inventory operations and transactions are single commands, measured in microseconds (the .NET
  suite runs 346 tests in 15 s, including 200-day runs).

The probe report is in `Documentation/Screenshots/Phase5/devbuild-perf-phase5.txt`.

Not measured in a build: the 100- and 300-person worlds as rendered scenes. Only one settlement is
rendered at a time, and their simulation cost is measured above.

## 15. Known limitations

**Equipment and combat.**
- There is no combat, so weapons and armour have stats but no combat use.
- Long weapons are slung on the back rather than held.
- Weapons, shields and hats are simple procedural shapes: the CC0 packs have none.
- Hand placement is tuned for the idle and walk poses.

**Clothing.**
- Only two outfit families (peasant, ranger) with two cloth variants each.
- Empty slots show undyed underclothes, because there is no bare body fitted to the outfits.

**Interiors.**
- Only the Outfitters and Smithy are walk-in.
- The Tavern Kitchen trades at the tavern, which was already walk-in, and has no display.
- The Granary Yard and village and hamlet shops trade via conversation ("Trade") and the counter
  only where a fitted building exists. In villages the general store is house-0, which is fitted.

**Residents.**
- Walking uses street routes, not a navmesh. People do not avoid each other or the player while
  walking.
- Routes through the plaza go round the crossing, not round individual props.

**The hourly plan.**
- It is derived from the daily engine. Needs are still evaluated once a day, not hourly.

**Commerce.**
- Shop stock is counts, so a sold item does not keep its identity if it is bought back.
- Loose items in the world are not modelled: discarding destroys the item.
- The player has no income beyond selling.

**The clock and saves.**
- On the realm map, whole days leave the time of day unchanged.
- The minute moves only while the player walks a settlement or waits.
- Hunger falls only with minutes, not with whole-day map advances.

**Not measured.**
- Hardware other than this machine was not tested.
- Rendered-scene performance at 100 or 300 people was not measured (see §14).

## 16. Files and systems changed

**New, domain** (`Runtime/Domain`):
- `Calendar/WorldTime.cs`
- `World/Items.cs`, `World/ItemCatalog.cs`, `World/Commerce.cs`, `World/WorldValidator.Commerce.cs`
- `Identifiers/ItemStackId.cs`, `ShopId.cs`, `TransactionId.cs`

**New, simulation:**
- `Npcs/NpcActivity.cs` (the hourly plan)
- `Commerce/CommerceMath.cs`, `Commerce/ShopSystem.cs` (restock, genesis)

**New, application:**
- `PlayerCommands.cs`, `PlayerQueries.cs`, `TradeTalk.cs`, `WorldSession.Time.cs`

**New, infrastructure:**
- `Persistence/WorldSaveMapper.Commerce.cs`

**New, exploration:**
- Domain: `LocalPaths.cs`
- Presentation: `ExplorationController.Living.cs`, `ExplorationAtmosphere.DayNight.cs`,
  `ShopInteriorView.cs`, `TradeWindow.cs`, `PlayerHubWindow.cs`, `CharacterPreview.cs`,
  `PlayerBodyBuilder.cs`, `ItemModels.cs`

**Changed:**
- **World data and persistence:** `WorldData`, `PlayerCharacterData`, `WorldEntities` (event types,
  regard), `WorldValidator`, DTOs, `NpcCommands` (presence, regard, topics), `NpcQueries` (topics),
  `EventFormatter`, `WorldSession`, `SimulationPlayback`, the migration, the serializer.
- **Generation:** the generator (commerce after the economy; pre-commerce mode),
  `WorldPopulationInitializer`.
- **Systems:** the standard systems list.
- **Exploration:** `ExplorationHandlers` (layout from hourly presence and shop fits),
  `BuildingKit` (shop plans), `LocalEnvironmentGenerator`, `LocalEnvironmentLayout`, `Interactions`
  (availability), `ModularEnvironmentBuilder` (open doorways, ceiling), `ResidentView`,
  `PlayerCharacter`, `HumanoidCharacter`, the HUD (styles and dismissing responses),
  `PerformanceProbe`, `ArtSetup` (activity poses).
- **Art:** the regenerated `Locomotion.controller`, the `UAL1_Standard.fbx.meta` loop flag, and the
  `_EMISSION` keyword on `M_LampGlow.mat`.

**Tests.**
- New: `EditMode/Commerce/CommerceTests.cs`, `CommercePerformanceTests.cs`,
  `PlayMode/Phase5PlayModeTests.cs`, `Phase5CaptureTests.cs`.
- Updated (§12): `NpcCommandTests`, `NpcEngineTests`, `NpcPerformanceTests` (helpers made internal),
  `Phase2CompletionTests`, `Phase2Worlds`, `ExplorationTests`, `PoliticalWorldTests`,
  `ExplorationPlayModeTests`, `GraphicsPlayModeTests`.

**Documentation.**
- New: `Systems/WorldTime.md`, `Player.md`, `Inventory.md`, `Equipment.md`, `Commerce.md`.
- Updated: `NPCs.md`, `Exploration.md`, `Persistence.md`, `Roadmap.md`, `Testing/TestResults.md`.
- `Phase5/PreFlight.md`, ADRs 0015–0019, this report, and the screenshots.

## 17. Architecture decisions

| ADR | Decision |
|---|---|
| 0015 | The player is one persistent domain entity, extended rather than replaced; appearance is limited to what the art renders |
| 0016 | Item definitions are data; instances are stacks with exactly one owner; trades are single atomic commands carrying the expected price |
| 0017 | Equipment is shown from world data with the outfit kit's own parts and procedural props; one builder serves the street and the preview |
| 0018 | Shops are kept by existing residents, with the keeper's savings as the till; restocking converts settlement materials through the existing ledger, without double counting |
| 0019 | Minutes on top of daily simulation; hourly behaviour is a pure derived function; two playback paces |

## 18. Git commits, branch and origin

| | |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| Before Phase 5 | HEAD `277bd48` (Phase 4), equal to `origin/feature/phase-0-1-foundation` |
| Phase 5 | One commit on top of `277bd48`, "Phase 5: Living World, Player Hub & Commerce" (its hash is in the hand-off message, since a file cannot contain its own commit's hash) |
| Origin | **Not pushed.** Origin stays at `277bd48` until the owner reviews and pushes. |
| Working tree | Clean after the commit. Unrelated files untouched. Two head prefabs that the art setup regenerated with only new internal IDs were restored to their committed versions; the animator controller, the clip-loop setting and the lamp material's emission keyword are real changes and are included. |

Phase 5 stops here. Phase 6 has not been started.
