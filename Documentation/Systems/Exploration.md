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

## People in exploration (Phase 4)

- **Who is shown:** the people present today, meaning living people whose current location is in this
  settlement (`WorldSession.GetPeoplePresent`). Each stands at the layout site of their location
  (`ResidentInput.SiteKey`): inside the tavern, among the stalls, by the gate, or at a building's door.
- **Body:** the body frame follows the person's recorded sex, with the name as a fallback.
- **Talking:** press E to greet; the greeting chronicles the first meeting. Then six topics are
  offered (click, or press 1–6), each an `AskNpcCommand` answered from the person's own knowledge
  (see [NPCs](NPCs.md)). The resident plays the talking animation, and the conversation ends
  when you walk away.
- **Presence:** talking to someone who is not in the settlement today is refused.

## Known limits

- **Characters:** two outfits per body frame. There is no turn clip, so turning in place shows
  as slow steps. Residents wander without navigation, as before.
- **Interiors:** only the tavern has one. Other buildings are closed; shop fronts show a counter.
- **Ground:** the town ground is flat. The kit has no plank or thatch walls, so hamlets use plaster.
- **Female_Peasant:** Unity's auto-mapping rejects its rig, so it uses the Female_Ranger avatar
  (same skeleton).

## The living settlement (Phase 5)

- **Time runs while the player walks.**
  - Minutes advance at Normal (2 game minutes a second); the HUD shows the clock with pause (P),
    speed, wait an hour (T) and the Hub (H).
  - On leaving, the realm map's whole-day pacing returns, and its play state is restored.
- **Light follows the clock** (`ExplorationAtmosphere.SetTime`).
  - The sun climbs from 06:15 to 19:30 on an east–west arc, warm and low at golden hour.
  - After sunset the sun sinks below the horizon and the procedural sky darkens with it; a faint
    cool moon takes over.
  - Ambient light, haze and exposure follow.
  - From dusk, real point lights come on at every street lamp and door lantern, and windows glow.
  - Window, lantern and sky materials are copied per visit, so the art assets are never edited.
- **People move.**
  - Each resident's place and activity come from `GetPresenceIn` this hour.
  - When their place changes, they walk a street route (`LocalPaths.Route`): out of the door, along
    the nearest street, round the crossing (never through the well), in at the destination.
  - At home or asleep, they go indoors: not drawn and not approachable.
  - After a jump in time (waiting, a fast clock) they are placed directly.
  - Poses come from the activity: kneeling at work, kneeling at the shrine, chatting at the tavern,
    standing at a counter, walking. The prompt names the activity.
- **Shops are entered.**
  - The town's Outfitters and Smithy (house-0 and house-1) are built with an open archway, a plank
    floor, a ceiling and doorway-aware collision.
  - Inside are a counter, shelves, a rack and, in the smithy, a forge and an anvil.
  - Every unit of stock stands on the shelves as its own model, rebuilt when stock changes.
  - The keeper stands behind the counter when the world says they are working there.
  - Trade at the counter (E), or with "Trade" in conversation (`Commerce.md`).
- **The player's body is built from what they wear** (`Equipment.md`). Speed follows encumbrance and
  hunger.
- **The Player Hub** (H) has eight sections and a live preview (`Player.md`). While it or the trade
  window is open, the character does not move and the cursor is free.

Tested in PlayMode:
- the full purchase flow, including save and load;
- the hub and equipment on the body;
- day and night lighting with lamps;
- residents hidden indoors at night;
- a keeper walking from home to the counter;
- a shut shop whose keeper is asleep, with nobody in their place.

## Planned settlements (Phase 10)

A settlement with a persisted plan (ADR-0047, every world generated or loaded since Phase 10) is explored
as that plan. `LocalEnvironmentGenerator.FromPlan` turns it into the environment layout:
- the plan's road segments as streets;
- its buildings, keeping their keys, floors, plinths, materials and roofs;
- its features as props;
- its herds in their pastures.

`ModularEnvironmentBuilder` (the `.Plan` partial) draws the scene:
- terrain mesh chunks with colliders;
- draped roads, carried level over bridges;
- the water;
- bridges or fords;
- fenced fields with rows, orchards, gardens and yards;
- herd placeholders;
- the wall circuit with gatehouses;
- plinths and steps;
- verges.

People and props stand at `LocalPaths.StandingHeight`: a floor inside a building, a deck on a bridge, the ground
elsewhere. People walk the plan's road graph (ADR-0049).

Settlements without a plan keep the older two-street environment described above. Plan generation:
`Documentation/Phase10/SettlementGeneration.md`.

## The country as a third environment (Phase 23)

Beside settlements and places, a region's walkable country is now an environment of its own, built by `OverlandWorld`. As before, only one environment is active at a time. Moving between environments (country, settlement, place, another region, another realm) is a loading transition behind `LoadingScreen`. See `Overland.md` and ADR-0112.
