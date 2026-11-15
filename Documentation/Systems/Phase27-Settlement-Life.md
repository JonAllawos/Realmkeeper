# Phase 27: settlement life

**Decision record:** ADR-0119.

**Code:**
- `Exploration/Domain/Townsfolk.cs`: the plan.
- `Exploration/Domain/StreetDressing.cs`: the dressing.
- `Exploration/Presentation/ExplorationController.Townsfolk.cs` and `.Callouts.cs`.
- `ModularEnvironmentBuilder.Dressing.cs`.
- `AmbientWander.cs`.
- `Application/Interface/StreetLifeQueries.cs` and `ShopCallouts.cs`.

## Population

| | |
|---|---|
| **Source** | `WorldSession.GetStreetLife`: population, kind, prosperity, workers by trade, the owner's place in the faction list |
| **Out now** | Population × the hour's share minus the named already out, at most 160 (`TownsfolkPlanner.OnStreets`) |
| **The hour's share (per thousand)** | 00–05: 2–3 · 06: 20 · 07: 38 · 08: 50 · 09–11: 58–64 · 12: 68 · 13–16: 54–64 · 17: 48 · 18: 38 · 19: 28 · 20: 18 · 21: 10 · 22–23: 4–6 |
| **The watch** | Population ÷ 250 (1–10), half at night; posts at the gates by day, the rest on patrol |

**Measured in the test world:**

| Settlement | Population | Named | Unnamed out at noon | At 03:00 | Watch |
|---|---|---|---|---|---|
| Bramhaven (town) | 1,644 | 12 | 112 | 3 | 6 |
| Thorngate (town) | 2,117 | 13 | 144 | 4 | 8 |
| Mossholm (village) | 536 | 5 | 36 | 1 | 2 |
| Westerthorpe (hamlet) | 181 | 10 | 12 | 0 | 1 |

In the running game at noon, Bramhaven had **106 people out**: 100 unnamed, 6 of the watch, and its named residents. At 22:00, **13**.

## What people do

| Activity | Share (about) | Way | Looks |
|---|---|---|---|
| Shopping, errands | 34 % | Home doors ↔ the market (the square and shop doors) | Residents |
| Gathering | 12 % | Standing in twos and threes about the square | Residents, elders |
| Carrying goods | ~22 %, more with trade labour (working hours) | Warehouses and granaries ↔ the market | Labourers |
| Working | (working hours) | Standing at workshop and smithy doors | Smiths, labourers |
| Farming | More with farming labour (working hours) | The gates ↔ the market | Farmers |
| Travelling | (villages and up) | The gates ↔ the market | Residents |
| Errands about the lanes | The rest | Home ↔ home | Residents, elders |

**Busy and quiet.** People are about twice as dense near the square as in a ring of equal area beyond it (measured: 1,396 against 736 position samples).

**Movement.**
- A way is there and back along the roads, pausing 4–14 s at each end.
- Each person walks a little to their own side of the road's line.
- A position is a pure function of time.
- A route the road search could not find (a straight line across town) is rejected and the person is planned again.

## Two tiers

- **Detailed:** the nearest 32 within 70 m have figures: the humanoid bodies and appearance rules, looks seeded from the person.
- **Figure handling:** figures are cached (48) and reused; at most two are built each 0.3 s, the slowest about 1.4 ms.
- **Not obstacles:** townsfolk have no collider, prompt or conversation.
- **The plan is renewed** each game hour, and on arrival.

## Shopkeepers

- A keeper out in the street or at the counter calls to the player 3–12 m away.
- **Never** in a window, a fight or a conversation.
- One call in 14 s anywhere, each shop once a minute, its lines in turn.
- **The line** shows above the keeper for 4.5 s; they turn to the player and talk.
- **Lines** are in `ShopCallouts`, by shop kind (plain trade talk).

## Animals

- **Herds** come from `world.Livestock`, in their pastures.
- **Models:** chickens, cows and horses use the kit's animated models (`ArtCatalog.Animal`); species the kit lacks (sheep, goats) keep the box figures.
- **Wandering:** each animal walks to points inside its pasture (in the field's own frame, so never through the fence) and stands a while. It is inactive beyond 90 m of the camera.
- **Herds are no longer merged** into the static geometry, so they can move.

## Dressing, signs and banners

| Building or place | Dressing |
|---|---|
| Smithy | Anvil, workbench, wood pile |
| Shops | Barrel and crate; sacks at provisioners |
| Workshops, warehouses, mills | Crates and sacks |
| Barns, stables | Hay |
| Farmhouses | Wood pile |
| Homes | About 4 in 10 have a bench, a barrel or a wood pile |
| The square | Up to six benches facing in, where no road enters |
| The granary | A cart |
| The gates | The owner's banners on either side of the way in, back to back |
| Hall, keep, guard posts | Banners either side of the door |

**Clearances** (tested over 27 settlements): every item on the ground keeps clear of every road (half its width + 0.4 m), every door's approach (1.3 m either side, 3.5 m out), other buildings, water and fenced plots. Where the front is blocked, an item goes alongside a side wall.

**Signs.** The kit's sign by trade, where it has one:
- smithy: `Sign_Blacksmith`;
- provisions: `Sign_Food`;
- apothecary: `Sign_Potions`;
- bowyer: `Sign_Armory`;
- tavern: `Sign_Pub`.

Otherwise the plain board as before.

**Banner colours.** By the owner's place in the world's faction list, the strategic map's order:
- crimson, blue, gold, green, violet, teal, orange, silver → the kit's red, blue, yellow, green banners and their patterned variants;
- independent: plain brown.

## Children

**Not added** (preflight audit §6): the world has no child population or body, and adding them is the owner's design decision.
