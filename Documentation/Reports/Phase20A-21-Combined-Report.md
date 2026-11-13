# Realmkeeper: Phases 20A and 21, combined report (with all assets)

**Date:** 2026-11-13. **Branch:** `feature/phase-0-1-foundation`.
**Detailed reports:** `Phase20A-Completion-Report.md`, `Phase21-Completion-Report.md`.
**Not hand-played:** all evidence comes from automated tests, automated play through the real controls, and inspected captures.

## Commits

| Work | Commit | Pushed |
|---|---|---|
| Phase 20A and the free-asset integration | `77240e7` | Yes, at the owner's instruction |
| Phase 21, plus the Bible and roadmap docs | `68b3063`, then `72c5916` (report hash) | No, local only |
| Purchased kits integration | not yet committed: the tests are running | No |

---

## Phase 20A: character systems completion

- **Goliath ancestries are now played:**
  - **Fire's Burn, Frost's Chill and Hill's Tumble** trigger after a hit.
  - **Cloud's Jaunt** is a bonus-action teleport to a clicked square.
  - **Stone's Endurance and Storm's Thunder** are reactions.
  - Uses equal the proficiency bonus per long rest. Companions and computer-run goliaths use them too.
- **Tiefling legacy cantrips:** Fire Bolt, Poison Spray and Chill Touch are cast with Charisma, even by non-casters. Chill Touch (SRD 5.2.1) was added with its no-healing effect.
- **Honest labelling:** each trait shows as active, conditional or not yet played, in the creator and on the sheet.
- **Eye colour:** ten iris colours for the player. Save schema **v22**.
- **Bug fixed:** the body in the world ignored the chosen height and build.
- **Audits with new tests:** familiarity, the level 1–20 progression, and the creator and sheet at 1920×1080, 1366×768 and 1280×720.
- **Still not played:**
  - gnome illusion and animal-speech spells;
  - Mending and Prestidigitation;
  - the tiefling level 3 and 5 spells, and the choice of casting ability;
  - the ancestry reactions against spell and hazard damage;
  - the dragonborn breath changes.
- **Tests (final):**

  | Suite | Result |
  |---|---|
  | .NET | 868/868 |
  | Unity EditMode | 868/868 |
  | Unity PlayMode | 127 passed, 0 failed, 31 skipped, 2 inconclusive |

## Phase 21: environment, living settlements, shops, workshops, interiors

- **Weather and seasons (ADR-0105).**
  - **Seasons** come from the existing calendar. Daylight runs from 16 h at midsummer to 8 h at midwinter.
  - **Regional climate** depends on latitude, the region's kind, and mountains and rain shadows.
  - **Daily weather:** fronts drift west to east, so neighbouring regions share weather. Moisture carries over from day to day, and each region tracks ground wetness and lying snow.
  - **Weather kinds:** clear, partly cloudy, overcast, light rain, heavy rain, thunderstorm, light snow, heavy snow, fog, windy, and hot and dry.
  - **Effects work only through existing systems:**
    - farm yield by season: 85 %, 110 %, 140 % and 65 %, averaging 100 % over a year;
    - travel slowed by rain, storms, snow and fog, with the reason shown;
    - NPCs shelter from storms and skip the market in rain.
  - **On screen, all built in Unity (owner's decision):**
    - the light dims and greys under cloud, and haze thickens;
    - wet ground goes dark and glossy; snow lies on ground and roofs;
    - rain and snow particles fall, under a moving cloud dome;
    - stars and the moon show on clear nights;
    - lightning with thunder, plus generated rain and wind sound;
    - sunrise and sunset follow the season.
  - **HUD:** the clock shows the weather and temperature.
- **Shops (ADR-0106).**
  - **Twelve kinds** from one catalog: the original six, plus bakery & provisions, leatherworks, tailor, bowyer & fletcher, apothecary and carpentry.
  - **Still one inventory and one till.** Existing shops keep everything they had.
  - **Empty shop buildings** now hold specialists, kept by named townspeople, so each town has three to six.
  - **Supply records** show where stock came from: made, imported (only along an open trade route), crafted at the bench, or bought from the player.
  - **The trade window** shows shortfalls with their reasons.
  - **Keeper float:** each keeper keeps 10 coin back so they can still buy from the player.
- **Crafting stations (ADR-0107).**
  - **Seven stations:** forge, woodworker's bench, leatherworker's bench, sewing table, bread oven, kitchen hearth and still.
  - The player uses them in the shop while it is open and the keeper is there.
  - **15 new recipes** bring the total to 26.
  - Each day, keepers make what their shop is short of, at their own station, from their own stock.
- **Interiors (ADR-0108).**
  - **A deterministic furnishing planner** furnishes bedrooms, halls, kitchens, workshops, cellars, offices, shrines, armouries and cells.
  - **Its plans are checked:** nothing on doorways, nothing overlapping, and at least half of each room clear. It was tested on 540 interiors.
  - **Each shop is fitted out for its trade:** the smithy has a forge, anvil and quench barrel; the apothecary a still and bottle shelf; the bakery an oven.
- **NPC errands:** on a free afternoon, half the townspeople visit one of their town's shops.
- **Save schema v23:** adds the world's weather state and each shop's kind and supply records. Older saves migrate.
- **Performance:** weather costs 0.07–0.10 ms per simulated day; shops cost 14 ms per simulated day for 67 shops.
- **Tests (final):**

  | Suite | Result |
  |---|---|
  | .NET | 898/898 |
  | Unity EditMode | 898/898 |
  | Unity PlayMode | 128 passed, 0 failed, 32 skipped, 2 inconclusive |

## After Phase 21: the purchased kits integrated (uncommitted)

- **Medieval Village MegaKit Pro:** 304 models.
  - **Doors:** every working door now uses the kit's door model, scaled to the doorway.
  - **Signs:** smithy, provisions, apothecary and bowyer have shop signs outside.
- **Fantasy Props MegaKit Pro:** 211 models.
  - **Homes:** real beds (double, twin and bunk), nightstands, dressers, desks and rugs.
  - **Shops:** bellows and a hammer rack in the smithy, farm crates in provisions, a vice bench in carpentry, a rug in the general store.
- **Stylized Nature MegaKit Source:** 116 models.
  - **Trees by region:** pines in the highlands, birches by the lakes, tall broadleaves in woodland, cherry trees in the vales and lowlands.
  - **Seasonal leaves:**
    - spring: fresh green;
    - summer: as made;
    - autumn: orange and gold;
    - winter: dull and dusted with snow.
  - Pines and cherry trees keep their colour. The kit's white leaf textures are used so the season can colour the leaves.
- **Bug found and fixed while doing this:** the weather capture test never set the season on its test weather. The game's own weather always did.
- **Tests:**

  | Suite | Result |
  |---|---|
  | .NET | 898/898 passed |
  | Unity EditMode | **running** |
  | Unity PlayMode | **pending** |
  | Weather capture test | passes |

---

## All assets used (all CC0 1.0 unless noted)

| Asset | Author | How obtained | Used for |
|---|---|---|---|
| Medieval Village MegaKit (**Pro**, 304 models, 20 textures) | Quaternius | Purchased by the owner, 2026-11-13 (replaced the free version) | Building modules, doors, shop signs, scenery (`village_*`) |
| Fantasy Props MegaKit (**Pro**, 211 models, 23 textures) | Quaternius | Purchased by the owner (replaced the free version) | Furniture, shop fittings, items in hand and on shelves |
| Stylized Nature MegaKit (**Source**, 116 models, 33 textures) | Quaternius | Purchased by the owner (replaced the free version); models and textures only | Trees, plants, rocks; seasonal leaves |
| Universal Base Characters | Quaternius | Free | Heads, eyes, brows, hair, beards |
| Modular Character Outfits – Fantasy | Quaternius | Free | Clothed bodies |
| Universal Animation Library 1 and 2 | Quaternius | Free | Idle, walk, run and talk; 42 more clips |
| Characters – Fantasy Races | Quaternius | Free, **QAL licence, local only (git-ignored)** | Elf and orc bodies (not yet wired to species) |
| Quaternius Bestiary | Quaternius | **Local only (git-ignored)** | Creatures |
| KayKit Fantasy Weapons Bits | Kay Lousberg | Free | 31 weapons and shields |
| KayKit Dungeon Remastered | Kay Lousberg | Free | Barrels, crates, pallets, dungeon pieces |
| KayKit Post Lantern | Kay Lousberg | Free | Street lanterns |
| Fantasy Town Kit 2.0 | Kenney | Free | 167 town pieces (catalogued as `town_*`) |
| RPG Audio | Kenney | Free | Footsteps, doors, coins, pages |
| ambientCG textures (grass, ground, paving, planks, wood) | ambientCG | Free | Ground, roads, floors |
| Interface fonts | — | Free, **SIL Open Font License 1.1** | UI text |
| Generated in code | Realmkeeper | — | Weather particles, clouds, stars, moon, snow surface, rain, wind and thunder sound |

Full licence details: `CREDITS.md` and `Documentation/Art/AssetInventory.md`.

## Known limitations

- **No customer economy:** keepers' purses run low, because NPC errands are visits, not purchases.
- **Crafting:** keepers' production rolls no checks, and stations have no animated work.
- **Not done:** terrain, open-world scenery and natural points of interest are planned for Phases 23–25.
- **Missing captures:** the smaller screen-size sets and the gameplay-moment captures were not taken.
- **Not hand-played:** see `Phase20A-Owner-Playtest-Checklist.md` and `Phase21-Owner-Playtest-Checklist.md`.
