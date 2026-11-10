# Realmkeeper Graphics Pass: visual foundation and environment pipeline

Date: 2026-11-10. Branch `feature/phase-0-1-foundation`, on top of `b9ac7ea`.
One commit, not pushed.

## Summary

Exploration used to be built from Unity primitives and capsules. It now uses:
- a modular medieval kit, assembled deterministically from the existing engine-free layouts;
- textured streets, kerbs, a paved plaza, stone walls with a gatehouse, and dressed props;
- animated humanoid residents and an animated player, whose looks come from their stable IDs;
- street-level lighting and post-processing;
- a furnished tavern interior.

All art is CC0, with each licence checked and stored next to the files. Nothing was bought,
and no packages were added.

Measured in a town, the scene has 279–951 draw calls per frame including shadow, depth and SSAO
passes. The budget is 1,500. Development-build frame times are below. All test suites pass.

## Before and after

The images are in `Documentation/Screenshots/GraphicsPass/`. Each pair was rendered from the same
camera pose in the shipped scene by `GraphicsCaptureTests`:
- `before_*.jpg` is the primitive vertical slice;
- `after_*.jpg` is this pass.

| View | What changed |
|---|---|
| `street` | Timber-framed brick and plaster houses with tiled roofs, cobbled street with kerbs, lamps, benches, crates and barrels; warm sun, sky and haze |
| `building` | Close-up of a town house: brick ground floor, plaster upper floor with timber framing, arched windows with shutters, lantern by the door |
| `character` | A resident as an animated humanoid (outfit, head, hair, tint from its NPC ID) instead of a capsule |
| `plaza_residents` | Plaza with stalls (awnings, goods), the well, the tavern, residents |
| `interior` | Tavern: plank floor, plaster walls, bar with barrels and shelf, tables and benches, rug, hearth and hanging lamps |
| `gate` | Gatehouse with two towers and a gallery; the player as a humanoid in third person |
| `overview` | The whole town from above |
| `map` | Strategic map: roofs now terracotta to match the towns (one palette change) |

## What was built

### Assets and licences (ADR-0010)

Five sources, all CC0 1.0. The licence of each was read before use and stored next to its files:
- Quaternius Medieval Village MegaKit;
- Quaternius Universal Base Characters;
- Quaternius Modular Character Outfits – Fantasy;
- Quaternius Universal Animation Library;
- ambientCG (six ground and wood materials).

Only the used subset is committed: about 80 MB, with textures re-encoded at 1024 px (outfit
colour maps at 2048). Details are in `Documentation/Art/AssetInventory.md`. Paid Source tiers
exist; they are listed there as recommendations only.

### Material, lighting and atmosphere foundation

- **Materials:** 38 shared URP Lit materials (colour, normal and smoothness) built by the setup
  tool. Ground and furniture use metre-UV meshes, so tiling stays even.
- **Atmosphere:** street level gets a warm sun, a gradient ambient that lifts walls in shade
  (the old "dark walls"), a procedural sky and exponential haze.
- **Shadows:** tuned for walking distance (90 m, 2 cascades).
- **Post-processing:** a street-level volume (ACES, warm grade, bloom, vignette), plus SMAA.
- **Restore on exit:** everything is restored on exit. A test checks this; it found a real
  defect, which is fixed (below).

### Modular building kit (ADR-0011)

- **Planner:** `BuildingKitPlanner`, engine-free. It turns each layout building into a plan:
  archetype, footprint snapped to the 2 m grid, storeys, pieces.
- **Archetypes:** eight. Cottage, townhouse, merchant house, workshop, tavern, granary, tower and
  gatehouse.
- **Profiles:** three, for hamlet, village and town.
- **Variation:** door slot, shutters, flower boxes, vines, chimney side and roof orientation,
  from a stable hash of the settlement ID and building key.
- **Builder:** `ModularEnvironmentBuilder` assembles the plans, the ground, the boundary, the
  props, the tavern interior and the set dressing. `MeshMerger` then merges static geometry per
  24 m cell, material and shadow mode.
- **Collision:** one box per building, the tavern's doorway left open, boxes for props. No mesh
  colliders.

### Representative street (Bramhaven)

Bramhaven's main street runs from the gatehouse to the plaza. It has:
- brick and plaster town houses, a merchant house with an awning and counter, and workshops;
- a cobbled street with stone kerbs, lamp posts, benches, barrels and crates by the doors, and
  flower boxes;
- the plaza with stalls, the well, the notice board and a handcart.

### Characters and animation (ADR-0012)

- **Appearance:** `NpcAppearanceResolver` (engine-free) maps an NPC's ID, role and given name
  to its body frame, outfit and texture variant, hair, beard, skin tone, hair colour, tint and
  height. Nothing is stored.
- **Assembly:** the CC0 outfits are headless bodies, so the setup cuts head meshes out of the
  base characters by bone weight. The builder binds head, hair and beard to the body skeleton by
  bone name.
- **Animation:** idle, walk, jog and sprint blend on measured speed; turning in place shows as
  slow steps; talking plays after a successful conversation. One body per NPC ID.

### Tavern interior

Plank floor and two-storey plaster walls with windows. A bar with barrels and a shelf of jugs,
two table groups with benches, a rug, the stone hearth with firelight, and two hanging lamps.

### Strategic map

One contained change: town, village and hamlet roofs use two terracotta shades to match the
exploration kit. The random draws are unchanged, so placement is identical.

## Defects found and fixed during the pass

| Defect | Cause | Fix |
|---|---|---|
| Kit pieces imported at 1/100 scale and lying flat | Measured the models after resetting their root transform; the Blender export puts 90° X and ×100 on the root | Keep the model's root rotation and scale when placing pieces |
| About 4,400 batches per frame | Thousands of small pieces; static batching does not reduce draws under the SRP Batcher | `MeshMerger`: about 2,100 renderers become about 310; 279–951 draws per frame |
| Some residents stood in a T-pose | Unity's auto-mapping rejects Female_Peasant's rig; its avatar imports as non-human | Use the Female_Ranger avatar (same skeleton); a test checks every body animates as a humanoid |
| Strategic fog and ambient wrong after leaving a town | The snapshot was taken after the exploration scene became active, so it saved that scene's defaults | Capture before the scene switch; a test compares fog, ambient, shadow distance and cascades |
| Animation read a teleport as running | Speed was measured from the position before placement | Jumps above 25 m/s are ignored |

## Performance

The budget is 60 fps at 1080p on a GTX 1660-class GPU, and at most 1,500 draw calls in a town.

### Editor (batch mode, RTX 4070 Laptop GPU, 1920 × 1080 render target)

The counters come from the engine's render profiler, per rendered frame, including shadow,
depth and SSAO passes.

| View | Draw calls | Set-pass | Triangles | Before: renderers in view | After: renderers in view |
|---|---|---|---|---|---|
| gate | 951 | 148 | 2.76 M | 149 | 260 |
| street | 859 | 128 | 2.54 M | 121 | 225 |
| building | 317 | 49 | 1.27 M | 41 | 70 |
| character | 684 | 137 | 1.75 M | 56 | 176 |
| plaza | 599 | 108 | 1.59 M | 87 | 142 |
| interior | 279 | 63 | 0.91 M | 20 | 62 |
| overview | 901 | 122 | 2.50 M | 182 | 311 |

Whole town scene: 314 renderers (184 before), 0.89 M triangles (0.02 M before), 40 materials
(25 before), 81 colliders (158 before).

### Development build

This is a Windows x64 development build (192 MB), run windowed at 1920 × 1080 with vsync off on
an RTX 4070 Laptop GPU and an i9-14900HX. Run it with
`Realmkeeper.exe -rkPerfProbe report.txt [-rkPrimitives]`. Each row averages 240 frames after a
60-frame warm-up. The primitives column comes from the same build with the art switched off.

| View | Kit: frame mean / p95 | Kit: fps | Kit: draw calls | Kit: triangles | Primitives: frame mean | Primitives: draw calls |
|---|---|---|---|---|---|---|
| map (strategic) | 1.39 / 2.21 ms | 719 | 153 | 0.20 M | 2.33 ms | 153 |
| gate | 1.69 / 2.08 ms | 593 | 955 | 2.76 M | 2.01 ms | 544 |
| street | 1.42 / 1.75 ms | 703 | 863 | 2.54 M | 2.09 ms | 458 |
| building | 1.23 / 1.45 ms | 813 | 321 | 1.27 M | 2.09 ms | 185 |
| plaza | 1.33 / 1.59 ms | 753 | 603 | 1.59 M | 2.07 ms | 333 |
| interior | 1.23 / 1.46 ms | 812 | 283 | 0.91 M | 2.06 ms | 121 |
| overview | 1.49 / 1.81 ms | 671 | 905 | 2.50 M | 2.06 ms | 637 |

**Memory with the kit:** system 565–570 MB, graphics 183–185 MB in town (with primitives: 437 MB
and 136 MB).

**Against the budget:**
- **Draw calls:** at most 955, within the 1,500 budget.
- **Frame time:** the worst p95 is 2.2 ms on this GPU, against the 16.7 ms that 60 fps allows.
- **Primitives faster?** The primitive run was not: the second run of the pair was slower
  overall, including the unchanged map. That points to thermal or clock state rather than the
  scene, so the two columns should not be compared closely.

**Not measured:** a GTX 1660-class GPU. That GPU has roughly a quarter to a third of this laptop
GPU's throughput, so frames would be expected around 5–8 ms, still under 16.7 ms. This is an
estimate from relative GPU performance, not a measurement.

## Tests

| Suite | Result |
|---|---|
| .NET harness (`dotnet test`) | **297 passed**, 0 failed (279 before the pass, plus 18 new) |
| Unity EditMode (batch) | **297 passed**, 0 failed |
| Unity PlayMode (batch) | **48 passed**, 0 failed (43 before, plus 4 graphics tests and the capture fixture, which ran with `RK_CAPTURE_DIR` set) |

**New tests**
- **EditMode / .NET, 18:**
  - `BuildingKitTests`: determinism, order independence, variation, kit coverage, footprints,
    no duplicate pieces, doors and doorway, profiles, all 8 archetypes reachable, grid snapping,
    layout type.
  - `NpcAppearanceTests`: same identity gives the same look, pinned mapping, role outfits,
    body frame from name, palette bounds, distinct looks for the default world's residents,
    empty ID rejected.
- **PlayMode, 4 plus the capture fixture:**
  - the catalog covers every planned module, and every body animates as a humanoid;
  - entering a town builds the kit environment within budget, with colliders, no mesh colliders,
    one humanoid per NPC ID that matches its resolved appearance, and render settings restored
    on exit;
  - the same layout builds the same scene;
  - bodies animate from real movement and talking.
- **`GraphicsCaptureTests`:** evidence capture, not pass or fail. It runs only when
  `RK_CAPTURE_DIR` is set.

**Mutation checks** (each change was reverted):
- Making the front door slot a window fails `EveryHouse_HasAFrontDoor…`.
- Rounding footprints up fails `Plans_StayInsideTheirLayoutFootprint` and
  `Snap_RoundsToTheTwoMetreGrid`.

**Regression:** `RealScenePlayModeTests` (click a city, enter, walk) and all earlier suites pass.

## Manual checklist for the owner

- [ ] Open the project, press Play, click Bramhaven, Enter.
- [ ] Walk the main street in third and first person (V): no gaps between wall modules, roofs sit
      on walls, kerbs line the street.
- [ ] Enter the tavern through the archway; check the lighting and the furniture.
- [ ] Talk to a resident (E): they turn, and play the talking animation.
- [ ] Sprint (Shift) and stop: run, walk and idle blend smoothly.
- [ ] Visit the village and the hamlet: plaster cottages, a wooden fence, no town walls.
- [ ] Return to the map (M): the map looks as before, apart from the terracotta roofs.
- [ ] Check the Game view at 1080p for frame rate (Stats).

## Known limitations

- **Characters:**
  - Two outfits per body frame in the free tier.
  - No turn-in-place clip; turning shows as slow steps.
  - No navigation; residents wander as before.
- **Buildings:** only the tavern has an interior; other buildings are closed.
- **Ground:** flat. No plank or thatch wall modules, so hamlets use plaster.
- **Sky:** the overview from high above shows a horizon band where the haze meets the sky.
- **Body frame:** chosen from a list of feminine given names (a hint until Phase 4 identity data
  exists).

## Handoff to Phase 4

- **Bodies:** residents are keyed by `NpcId`; their bodies hold no state. Phase 4 can add NPCs,
  move them between locations and change roles; the next visit shows them correctly.
- **Appearance:** if Phase 4 adds identity fields (for example sex or age), extend
  `NpcAppearanceResolver.Resolve` to read them, and update the pinned test deliberately.
- **Locations:** the layout's building keys (`hall`, `granary`, `house-N`, `landmark`) are stable.
  Phase 4 locations (residence, tavern, market) can refer to them. The tavern interior is the
  only walkable interior.
- **Animation:** `CharacterAnimationDriver.Talk(seconds)` is the hook for conversation and
  reaction animations; movement animation follows automatically.
- **Art setup:** after adding art, run **Realmkeeper ▸ Set Up Art**.
