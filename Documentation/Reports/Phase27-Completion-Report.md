# Phase 27 completion report

**Phase:** Settlement Life, Architecture & Street-Level World Immersion.

**Date:** 2026-11-14.

**Baseline:** `b90f102`.

**Commits:** listed in §16. Local only; **nothing was pushed.**

**Status words used throughout:**
- **Complete:** built and shown working by tests in the running game.
- **Partial:** part of what was asked is done.
- **Deferred:** not done this phase.
- **Unverified:** not checked by a person.

## 1. Executive summary

| Requirement | Status | Evidence |
|---|---|---|
| Believable population from the authoritative figure | **Complete** | Bramhaven (1,644): 12 named → 100 unnamed and a watch of 6 out at noon, 13 at 22:00; the nearest 32 shown. Market about twice as busy as an equal area beyond (tests) |
| Roles and daily activity | **Complete** | Errands, gathering, carrying, working, farming, travelling, from the town's labour and the hour; varied looks. Tested: labour shifts the mix |
| Shopkeeper callouts | **Complete** | Data-driven lines by shop kind; distance, gap, per-shop cooldown, no calls in windows (live test) |
| Guards and patrols | **Complete** | Posts at gates by day; patrols on the streets; count by population |
| Animals | **Partial** | Chickens, cows, horses and sheep: animated kit models grazing inside their pastures (live test; sheep added 2026-11-15). Goats: still box figures (the owned goat model has no animations) |
| Children | **Complete** (addendum §17) | Added after owner feedback; body is a scaled stand-in |
| Shop signs | **Partial** | Kit signs for smithy, provisions, apothecary, bowyer, tavern; others the plain board. Readability from the street **unverified** |
| Faction identity | **Partial** | The owner's banners (map colour) at gates and at hall, keep and guard posts. Guard uniforms and notices **not** changed |
| Everyday objects | **Complete** | By building purpose; clear of roads, doors, buildings, water and fences (27 settlements, tested; live: no doorway blocked) |
| Distinctive settlements | **Partial** | Population, labour and owner now differ visibly. Architecture by wealth and type is the existing kit's (unchanged) |
| Doors and paths | **Complete** | Door slot made deterministic; one shared door position (1,242 doors tested); leaf fills its frame (measured, tested). Leaf height **unverified** |
| Exterior architectural variety | **Deferred** | Beyond signs and dressing, no new archetypes (the kit's existing variety) |
| Interiors reflect exteriors | **Complete** | Sized to the footprint (never larger from 6 m up, up to 30 m), second bedroom for large houses, furnished by area with 12 new furniture kinds; small homes enterable (a defect found and fixed) |
| Road refinement | **Partial** | Corners rounded and ends capped, draped. Hill creases are the terrain's own 2 m triangles (not smoothed: it would change the walked ground) |
| Performance | **Measured (editor)** | +0.6 ms mean frame; townsfolk update 0.12 ms; worst-frame spike accepted by the owner. No player build |

**Not hand-played.**

## 2. Preflight findings

Details are in `Systems/Phase27-Preflight-Audit.md`.

- **Why the towns looked empty:** only named NPCs (12 of 1,644) were ever drawn, most indoors.
- **Doors** were placed at a random slot while paths went to the front's centre.
- **Interiors** clamped to 7.5–21 m whatever the building.
- **Herds** were static boxes merged into static geometry.
- **The kit's signs, banners, furniture and animal models were unused.**

## 3. Population system changes

- `TownsfolkPlanner`: the unnamed share by hour (per thousand: 2–68), minus the named out, at most 160.
- Routes along the roads, validated to stay on the streets.
- Deterministic by settlement, day and hour.
- **Two tiers:**
  - **abstract:** positions as a pure function of time, for everyone;
  - **detailed:** 32 nearest within 70 m as full figures, cached and reused, built two each 0.3 s.
- No records, colliders or conversation for townsfolk (ADR-0119). No save change.

## 4. NPC and ambient activity

Activities and shares are in `Systems/Phase27-Settlement-Life.md`:
- poses for working and chatting;
- people walk to their own side of the road and pause at each end;
- the plan is renewed each game hour.

## 5. Shopkeepers, guards, animals and children

- **Callouts:** `ShopCallouts` (lines by `ShopKind`) and `ExplorationController.Callouts`.
  - **Live:** the smith called "Blades sharpened, buckles mended!" 4.8 s after the player stood 7 m away.
  - No second call within the gap.
  - No call with the hub open past the gap.
- **The watch:** population ÷ 250, half at night; gate posts by day; patrols between the gates and civic buildings.
- **Animals:** the kit's animated chicken, cow and horse. `AmbientWander` keeps each inside its pasture and sleeps beyond 90 m.
  - **Live:** 39 of 39 near the player moved, all inside their fields.
- **Children:** deferred (§1).

## 6. Exterior and environmental improvements

- `StreetDressingPlanner` (rules in `Systems/Phase27-Settlement-Life.md`).
- **Trade signs** from the kit.
- **The owner's banners**, back to back, the gate ones on poles: colour by the strategic map's faction order (Bramhaven, The Crown of Bramhaven: red).
- **Live:** 58 items planned, 84 objects placed, 42 solid; no doorway blocked.

## 7. Building entrances and doors

- **Door slot:** deterministic (the centre-most); the random draw kept, so nothing else changes.
- **`BuildingKitPlanner.DoorX`:** used by paths, people, place entrances, the map and the dressing.
- **The leaf:**
  - measured opening 1.29 m against a leaf of 1.12 m;
  - now drawn 1.17× from −0.60, spanning −0.65 to 0.65 against the opening's −0.65 to 0.64 (tested within 3 cm);
  - height unverified.
- **Existing saves:** door placement is drawn from the layout every visit, so the fix applies to existing settlements too.

## 8. Interior generation

Details are in `Systems/Phase27-Building-Interiors.md`.

**Measured:**

| Building | Ground floor | Rooms | Furnishings |
|---|---|---|---|
| Cottage 6×6 m | 6.0×6.0 m | 2 | 2 |
| Large home 14×11 m | 13.5×10.5 m | 3 | 8 |
| Warehouse 18×12 m | 18×12 m | 2 | 9 |
| Civic 24×16 m | 24×15 m | 2 | 17 |

**Defect found and fixed:** the new sizing left 6 m cottages with a bedroom one cell deep, which the place validator rejected ("too small to stand in"), so they could not be entered. Back rooms now keep two cells, and a regression test enters the four smallest homes.

**Persistence:** interiors already entered stay as saved; only new ones use the new generator.

## 9. Road geometry

- Corners rounded (at most 2.5 m cut, within the corridor).
- Ends capped with a draped half-disc; bridges unchanged.
- **Verified live:** every road vertex is still 3 cm above the walked collider (the Phase 26.1 test, rerun).

## 10. Architecture and persistence

**New:**
- Exploration.Domain: `Townsfolk`, `StreetDressing`.
- Application: `StreetLifeQueries`, `ShopCallouts`.
- Presentation: `ExplorationController.Townsfolk` and `.Callouts`, `ModularEnvironmentBuilder.Dressing`, `AmbientWander`.

**Changed:**
- `BuildingKitPlanner`: the door, the leaf, `SignFor`.
- `LocalPaths`: the door position.
- `InteriorGenerator` and `FeatureCatalog`: sizing and 12 furniture kinds.
- `LocalEnvironmentLayout`: `Dressing`, `OwnerIndex`.
- `NpcAppearanceResolver.ResolveUnnamed`.
- `ModularEnvironmentBuilder`: `RoadStrip` corners and caps; signs; herds outside the merged geometry.

**No save schema change.** Everything new is derived. Decision record: ADR-0119.

## 11. Performance

Details are in `Systems/Phase27-Performance.md`. All figures are from the Unity editor in batch mode.

| Measure | Result |
|---|---|
| Frame by the square at noon | 1.82 ms mean with townsfolk, 1.22 ms without |
| Worst frame | 14.2–18.8 ms with townsfolk, 1.8–2.4 ms without (accepted by the owner) |
| Townsfolk update | 0.12 ms mean, 0.25 ms worst |
| Hourly plan | 23–38 ms |
| Figure build | ≤ 1.8 ms each |
| Town entry | 0.76–1.7 s |

**Not measured:** a development build, draw calls, GPU time.

## 12. Focused test results

| Group | Result |
|---|---|
| .NET full (Release; run because the domain changes reach many systems) | **966/966** (960 + 6 new in `Phase27SettlementLifeTests`), 5 min 44 s |
| `Phase27SettlementLifeTests` (.NET) | 6/6: population share by hour; determinism, labour, busy versus quiet, staying on the streets, continuous walking; dressing clearances; 1,242 doors; interior sizes; smallest homes enterable |
| Unity PlayMode, focused regression | **47/47** (382 s): `Phase27PlayModeTests`, `Phase26_1PlayModeTests`, `Phase10`, `Phase13`, `Phase14`, `Phase5`, `Phase11`, `NpcPlayModeTests`, `ExplorationPlayModeTests` |
| `Phase27PlayModeTests` + `Phase27CaptureTests` (final, with captures) | 2/2 |
| Related .NET focus during the work | 193/193 (places, interiors, settlements, building kit, NPCs) |

**Corrections made to new tests (not weakened):**
- Townsfolk planned along a straight-line fallback route were a real planner flaw. They are now rejected and re-planned.
- The callout test waits out the 14 s gap that an earlier call had started.
- Door measurement in world space, with each model's own rotation.

## 13. Full-suite status and next checkpoint

- **Not run this phase**, by the revised policy (changes are covered by the focused runs above).
- **Last full checkpoint:** Phase 26.1 (EditMode 954/954; PlayMode 139/0/36).
- **Next checkpoint:** recommended at the end of the next phase, or before then if shared systems change. Phases 27 and 28 together will justify it.

## 14. Screenshots and documentation

**`Documentation/Screenshots/Phase27/`:** 21 captures at 1920×1080 from the running game (free camera, no interface), with `p27_capture_log.txt` and `p27_log.txt`.

| # | Capture |
|---|---|
| 01–03 | Small, medium and large streets |
| 04 | Market |
| 05 | Residential |
| 06 | Gate banners |
| 07 | Patrol |
| 08 | Storefront |
| 09 | Animals |
| 11 | Street details |
| 12 | A sign |
| 13–17 | Interiors |
| 18 | Door |
| 19–22 | Roads |

**Missing:** 10 (children, not implemented). Other resolutions were not captured this phase: the interface did not change.

**Documentation:**
- **New:** `Systems/Phase27-Preflight-Audit.md`, `Phase27-Settlement-Life.md`, `Phase27-Building-Interiors.md`, `Phase27-Performance.md`, `Phase27-Owner-Playtest-Checklist.md`, ADR-0119, this report.
- **Updated:** Roadmap, Game Design Bible, FutureRoadmap (renumbering note), Decisions index.
- **No new assets adopted:** everything used was already in the art catalog (Quaternius and KayKit-style CC0 kits recorded earlier), so the asset register is unchanged.

## 15. Known limitations and deferred work

- **Deferred:**
  - children (design decision);
  - new building archetypes;
  - guard uniforms by faction;
  - notices;
  - goat models (the owned one has no animations; sheep done 2026-11-15);
  - terrain smoothing on hills.
- **Townsfolk:**
  - cannot be talked to (by design);
  - their positions restart from the plan after a reload (same people, not the same spots);
  - figures appear as one walks near (two each 0.3 s);
  - not on the local map.
- **Unverified:** sign readability; door leaf height; bridge-to-road transitions by eye.
- **Interiors** already entered in a save keep their old layout.
- **The worst-frame spike** with townsfolk (accepted).
- **Not hand-played:** Phases 24–27.

## 16. Git commits and push status

- **Commits:** code and tests, then documentation and screenshots (hashes in the closing message).
- **Not committed:** the two untracked `Assets/InitTestScene*` scenes (preserved). `M_LampGlow.mat` was restored after every batch run.
- **Push:** pushed 2026-11-15 at the owner's instruction: `3f8ded4..03b3ded` to `origin/feature/phase-0-1-foundation` (Phases 26.1 and 27 with the feedback round). This note is recorded in a follow-up commit.

## 17. Addendum: owner feedback round (2026-11-14)

**Asked:** children; different factions; different building types; a smithy partly indoors and partly outdoors; some shops as outdoor stalls; no floating white lettered names; research CC0 and CC BY assets.

| Request | Status | What was done |
|---|---|---|
| Children | **Complete** (scaled owned bodies, kept by owner decision 2026-11-15) | About 16 % of the unnamed people out by day; they play or run errands; none at night (tested). Owned bodies at 0.62 scale, head ×1.25 |
| Factions | **Partial** | The watch wears the owner's colour; one shared colour table for the map, banners and guards. Buildings are not faction-styled |
| Building types | **Partial** | Form follows purpose: barns, warehouses and stables in the barn form; workshops, townhouses, cottages, merchant house. No new models |
| Smithy partly outdoors | **Complete** | A forge yard (`Stall_Blacksmith`) beside 7 of 9 smithies in Thorngate |
| Outdoor stall shops | **Complete** | 6 in 10 food, potion and general shops near the market; the keeper stands behind the stall by day (tested). Thorngate: 27 |
| No floating names | **Complete** | Replaced by kit signs and painted emblem boards. Readability **unverified** by a person |
| Asset research | **Complete** | `Research/Phase27-Asset-Research.md`. Nothing bought or downloaded |

**Tests this round:**
- `Phase27SettlementLifeTests` (.NET): 8/8, including two new tests (children by day only; forge yards and stall keepers).
- `Phase8PlayModeTests` now asserts the owner's request: no floating name text, a painted sign present, the kit sign for a smithy.
- **.NET full suite: 968/968** (Release, 5 min 47 s).
- **Unity focused regression: 51/51** (421 s): Phase27, Phase26_1, Phase10, Phase13, Phase14, Phase5, Phase11, Npc, Exploration, Phase8.
- `Phase27CaptureTests`: passed, 27 captures.
- **Test fix (not weakened):** the first regression run had 1 failure, Phase 11 `TheChestInTheStreet_OpensInStorage_AndKeepsWhatIsStored`. It teleported the player onto the chest lid; the character sometimes slid off to 0.7 m, facing away, so nothing was in front. Before the fix it failed in 3 of 5 runs of its class (nothing new stands near that chest). The test now stands the player a metre in front, facing the chest, as Phase 13 does. It then passed 4 runs in 4, and the whole regression passed.

**Screenshots added:** p27_10 (children), p27_23 (forge yard), p27_24 (stall shop), p27_25 (painted sign), p27_26 (the watch's colours), p27_27 (barn form).

**Owner decisions:** child bodies: keep the scaled owned bodies (decided 2026-11-15). Still open: KayKit Medieval Hexagon for faction buildings (style risk). Sheep: done (2026-11-15; see below).

## 18. Owner playtest checklist

`Systems/Phase27-Owner-Playtest-Checklist.md`: 23 checks. Automated tests, editor measurements and screenshots are kept separate from your playtesting. **Claude has not played the game by hand.**

## 19. Addendum: sheep (2026-11-15)

**Asked:** add the owned sheep model.

**Found:** the sheep (with idle and walk animations) was already in the art catalog. The pasture code never asked for it, and its FBX materials imported at **zero opacity**, so the sheep was invisible wherever it was drawn, including as a beast.

**Done:**
- Pastures draw sheep with the kit's animated model (`ModularEnvironmentBuilder.Dressing.AnimalModel`).
- The art importer makes every animal's materials opaque (`ArtImportPostprocessor`, version 9, which reimports the art).
- Goats stay box figures: the owned goat model has no animations.

**Tests:**
- `Phase27PlayModeTests` now asserts every sheep is the kit model and opaque. Live: 11 of 11 sheep drawn with the kit's model.
- Focused Unity regression plus the creature tests (Phase18, Phase15Art): **58/58** (466 s).
- Captures passed; new `p27_28_sheep.jpg`.
- .NET not rerun: no domain code changed.
