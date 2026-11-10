# Exploration: presentation of a settlement

How a settlement looks and moves when the player walks it. The rules of exploration (one
world, commands, saves, one character in two perspectives) are in
[Dual-perspective architecture](../Architecture/DualPerspective.md). This page covers what the
graphics pass built on top of them (ADR-0010, ADR-0011, ADR-0012).

## From world data to a scene

```
WorldData ──► LocalEnvironmentGenerator ──► LocalEnvironmentLayout     (engine-free, derived, never saved)
                                              │
                         BuildingKitPlanner ◄─┤ per building: archetype, snapped footprint, pieces
                     NpcAppearanceResolver ◄──┘ per resident: body, outfit, hair, tones (from NpcId, role, name)
                                              │
                   ModularEnvironmentBuilder ─┴► kit modules + procedural pieces → MeshMerger → scene
                    HumanoidCharacterBuilder ───► body + head + hair on one skeleton → Animator
```

The same world, settlement and residents always give the same scene. Nothing in this chain
writes to world data: the scene is rebuilt on each visit.

## Settlement profiles and archetypes

| Profile | Ground floor | Upper floors | Max storeys | House archetypes |
|---|---|---|---|---|
| Hamlet | plaster | (none) | 1 | cottage, workshop |
| Village | plaster | plaster with timber | 2 | cottage, townhouse, workshop, merchant house |
| Town (also city, fortress) | uneven brick | plaster with timber | 3 | townhouse, merchant house, workshop, cottage |

The eight archetypes:
- **Cottage:** one storey.
- **Townhouse:** two or three storeys, balcony door.
- **Merchant house:** open shop front with a counter and an awning.
- **Workshop:** brick, large chimney, barrels and crates.
- **Tavern:** the enterable hall. Two-storey open interior, centred archway, bar, tables and warm lamps.
- **Granary:** brick barn with a double door.
- **Tower:** the landmark, with narrow windows and a conical roof.
- **Gatehouse:** two brick towers and a gallery over the gate, in walled settlements.

Per-building variation (door slot, shutters, flower boxes, vines, chimney side, roof orientation
on square plans) comes from a stable hash of the settlement ID and building key.

## The kit grid

The building kit is the CC0 Medieval Village MegaKit, with these measurements once imported into Unity:
- **Walls:** 2 m wide, 3.12 m tall (3 m storey pitch). The exterior faces −Z of the module.
- **Roofs:** `Roof_RoundTiles_WxL` cover a W × L footprint (W ∈ 4, 6, 8; L from 4 to 14). The ridge
  runs along the module's Z axis and the base sits on the wall top.
- **Gable ends:** `Roof_Front_BrickW`.

Footprints snap down to whole modules: `n = floor((size + 0.5) / 2)`, at least 2. A roof wider
than 8 m (the tavern) is the 8 m roof scaled.

## Ground, streets and dressing

- **Surfaces:** grass, cobbled streets with stone kerbs, a paved plaza with a gravel edge, and a
  dirt approach road. These are metre-UV meshes, so tiling materials keep the same texel density.
- **Boundary:** walled settlements get stone walls with merlons and the gatehouse. The others get
  a wooden fence and a gate frame.
- **Layout props:** the well, stalls, lamps, trees, barrels, crates and tavern furniture are
  modelled from kit pieces and simple procedural shapes.
- **Set dressing:** benches, a handcart, stones and grass tufts are placed from the layout seed,
  clear of streets, the plaza and buildings.

## Lighting and atmosphere (street level only)

`ExplorationAtmosphere` captures the strategic render settings before the exploration scene
becomes active, and restores them on exit. While a settlement is explored it applies:
- a warm sun (42°, soft shadows) and a gradient ambient that lifts walls in shade;
- a procedural sky and exponential-squared haze;
- shadows tuned for walking distance: 90 m, 2 cascades;
- a street-level volume with ACES tonemapping, a gentle warm grade, mild bloom and a vignette;
- SMAA on the exploration camera.

SSAO comes from the PC renderer, as before. Inside the tavern, the hearth and two hanging lamps
provide the light.

## Characters

- **Assembly:** every resident gets a humanoid body built from its appearance. The body is a
  clothed outfit, with the head, hair and beard skinned to the same skeleton. Skin, hair and
  cloth are tinted with property blocks.
- **Animation:** `CharacterAnimationDriver` feeds the locomotion blend (idle, walk, jog, sprint)
  from measured speed. A successful conversation plays the talk animation for a few seconds.
- **Player:** the player is a ranger built the same way. It is hidden in first person, as before.

## Performance

- **Merging:** static geometry is merged per 24 m cell, material and shadow mode (`MeshMerger`).
  A town has about 310 renderers and about 0.9 M triangles before shadow passes.
- **Animation:** animators always run, since there are fewer than 20 bodies.
- **Measurements:** see the graphics pass report for the numbers measured in the editor and in a
  development build.

## Setting up the art

Unity imports the art with fixed settings (`ArtImportPostprocessor`). After adding or changing
art, run **Realmkeeper ▸ Set Up Art**, or in batch mode
`-executeMethod Realmkeeper.Editor.ArtSetup.RunBatch` with `RK_SETUP_OUT` set to a report folder.
It rebuilds the materials, the material remaps, the clip settings, the animator controller, the
head meshes and the catalog. Running it again gives the same result.

Screenshots for comparison: run the PlayMode test `GraphicsCaptureTests` with `RK_CAPTURE_DIR`
(and optionally `RK_CAPTURE_TAG`) set.

Performance in a player: build with `-executeMethod Realmkeeper.Editor.PerfBuild.Build`
(output folder in `RK_BUILD_OUT`), then run
`Realmkeeper.exe -rkPerfProbe report.txt [-rkPrimitives]`.

## Known limits

- **Characters:** two outfits per body frame. There is no turn clip, so turning in place shows
  as slow steps. Residents wander without navigation, as before.
- **Interiors:** only the tavern has one. Other buildings are closed; shop fronts show a counter.
- **Ground:** the town ground is flat. The kit has no plank or thatch walls, so hamlets use plaster.
- **Female_Peasant:** Unity's auto-mapping rejects its rig, so it uses the Female_Ranger avatar
  (same skeleton).
