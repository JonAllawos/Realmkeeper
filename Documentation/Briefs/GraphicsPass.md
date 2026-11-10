# REALMKEEPER — GRAPHICS PASS (brief for a future milestone)

Status: **proposed, not started.** Written 2026-11-10, after Phase 3 (`d82a01b`). It's
intended to be used as a milestone prompt. The owner answered the open decisions on
2026-11-10 (see "Decisions"); two of those answers still have a detail to confirm.

## Mission

Bring Realmkeeper's visuals up to the identity the Product Foundation (§3) already commits to:
- **Strategic view:** stylized realistic, at large geographic scale.
- **Exploration view:** more realistic and detailed, with modular medieval architecture,
  interiors, props, believable lighting and animated people.

This is a presentation milestone. It changes how the world looks, not what the world is.
Simulation, saves, IDs and the command architecture stay as they are.

## Where the visuals are today

| Area | How it is built now | What it looks like |
|---|---|---|
| Strategic terrain, water, roads | Procedural meshes (`TerrainMeshBuilder`, `WaterMeshBuilder`, `RoadMeshBuilder`) with the custom `VertexColorLit` and `Water` shaders | The strongest part of the game; flat-shaded, colours per vertex |
| Strategic settlements and trees | Procedural meshes (`SettlementBuilder`, `TreeMeshes`, instanced `ForestRenderer`) | Readable, but simple boxes and cones |
| Map overlays | Selection ring, economic diamonds (`MapIndicatorLayer`), political rings (`PoliticalOverlayLayer`) | Functional; the diamonds are flat coloured quads |
| Exploration environment | Unity primitives (cubes, cylinders, spheres) from `SettlementEnvironmentBuilder`, one flat colour each (`ExplorationMaterials`, URP Lit) | Placeholder: no textures, no detail, dark walls in shade |
| Characters and residents | A capsule and a sphere (`PlayerCharacter`, `ResidentView`) | Placeholder; residents slide, nothing animates |
| Lighting | One directional sun with soft shadows; a point light in the hall and at the hearth; linear fog; ambient from a hand-built probe | Flat in shade; no bounce light, no sky |
| Post-processing | One global Volume: bloom, neutral tonemapping, colour adjustments, white balance, vignette (`SceneEnvironment`) | Fine for the map; untuned for street level |
| HUD | UI Toolkit with the default font, dark translucent panels, gold accents | Clean but generic |

**Constraints already in force:**
- **URP only.** ADR-0001 and ADR-0009 point 7 rule out a pipeline change.
- **Packages:** ADR-0009 point 7 asks for an ADR per package. There is no ProBuilder, Cinemachine,
  Animation Rigging, Terrain Tools or Addressables today. The owner has since allowed any package
  this milestone needs (see Decisions).
- **No purchased assets** (an earlier milestone instruction).
- **Layouts are engine-free.** `LocalEnvironmentLayout` is engine-free data. Visuals are
  derived from it and never stored, so a nicer town must still come from the same layout.

## Decisions (owner, 2026-11-10)

| # | Question | Answer | Still to confirm |
|---|---|---|---|
| 1 | Where the art comes from | **Mix:** procedural layout and assembly, from a small kit of hand-made or free, licence-clear (CC0 or similar) modular pieces. Every licence is recorded in the repo. | — |
| 2 | Which packages | **Any package the work needs is allowed.** Likely candidates: ProBuilder, Cinemachine, Animation Rigging, Shader Graph, Terrain Tools, Addressables. The project rule stays: each one gets a short ADR, so the reason is on file. The ADR records the decision; it no longer needs approval. | — |
| 3 | Target hardware and budget | **Both.** Read as: the targets apply in the editor *and* in a development build. | The numbers. Proposed default: 60 fps at 1080p on a mid-range GPU (GTX 1660 / RX 5600 class), at most 1,500 draw calls in a town scene. |
| 4 | Art-direction reference | **Unclear.** | Proposed default until references exist: *warm stylized realism*, continuing the current strategic map: muted earth palette, soft warm sunlight, slightly exaggerated proportions, visible material texture without photorealism. The owner confirms or redirects after the first before/after screenshots of one street. |

## Part 1 — Pre-flight

- Verify the branch, HEAD, the working tree and test baselines, as previous milestones did.
- Capture a **before** screenshot set with the F10 tour, plus an exploration set (gate, street,
  plaza, hall interior, first person). This set is the comparison for acceptance.
- Measure the baseline in the editor and in a development build: frame time, draw calls,
  triangles, memory, for the map and for one town.

## Part 2 — Exploration environment

- Replace primitives with **modular building pieces**: walls with timber framing, plaster or
  stone; pitched roofs with eaves and thickness; doors; window frames; chimneys. Assemble them
  from the existing `LayoutBuilding` data (width, depth, height, yaw, style), so the layout code
  does not change.
- **Materials:** URP Lit with albedo, normal and roughness. Use a small, shared texture set
  (plaster, timber, stone, thatch, roof tile, cobble, dirt, grass), with world-space or
  triplanar mapping so pieces of any size tile cleanly.
- **Ground:**
  - cobbled streets with kerbs;
  - worn paths;
  - grass with colour variation;
  - the plaza as a proper paved area;
  - simple ground clutter (stones, grass tufts), placed deterministically from the layout seed.
- **Props:** market stalls with cloth, barrels, crates, carts, the well, lamp posts, fences,
  the notice board and the ledger. Each prop kind maps to one model.
- **The hall interior:** plank floor, beams, furniture, hearth glow, and windows that let light in.
- **Walls, gate and towers:** with height, crenellations and an actual gate.
- **Keep** collision simple: box colliders per module, not mesh colliders on detail.

## Part 3 — Lighting and atmosphere

- **Sky:** a sky that matches the strategic palette (procedural skybox or gradient).
- **Lighting:** better ambient (a sky-driven probe or baked probes per generated scene if
  feasible); tuned sun angle and colour; contact-hardening or softer shadows; light from
  windows and lamps at dusk if time of day is later added (not required now).
- **Post-processing:** a separate Volume profile for street level, with exposure, colour
  grading, mild bloom and vignette. Keep the strategic profile unchanged.
- **Fog:** distance and height fog tuned for the settlement scale.
- **Shade:** fix the very dark walls in shade (ambient and albedo balance).

## Part 4 — Characters

- **Player and residents:** replace capsules with simple humanoid models, one shared rig.
  Each resident role gets a distinct outfit or palette (elder, merchant, guard, smith,
  innkeeper, farmer, labourer).
- **Animation:** idle, walk, run and turn-in-place at minimum. Driven by the existing
  movement (speed and grounded state), not by new gameplay logic.
- **Facing:** residents face and turn toward the player smoothly, as now, but animated.
- **First person:** no body visible, as now. A head-bob option, off by default.

## Part 5 — Strategic map

- Keep the art style and improve what reads as crude:
  - settlement models closer to the exploration buildings' silhouettes;
  - tree variety;
  - shoreline foam or a softer edge;
  - road edges.
- **Economic markers:** replace the flat diamonds with small, readable icons (billboards) that
  have an outline, sized for 1080p and above. Keep their meaning and data source.
- **Political rings:** keep them, tune colours and thickness, and add an optional soft
  territory fill.
- **Clicking:** settlement selection and the camera must keep working. The overlays have no
  colliders and must stay that way.

## Part 6 — HUD

- **Font:** a licence-clear serif or humanist font for headings and a readable sans for data,
  bundled with its licence.
- **Panels and icons:** consistent spacing scale, icons for resources and faction types, and
  better contrast.
- **Layout:** the inspector and faction inspector must stay readable at 1280×720 and at
  2560×1440. Test both.
- **No structural change** to the HUD's data flow (query snapshots in, typed commands out).

## Part 7 — Performance and robustness

- **Budgets:** stay within the budget agreed in Decisions, row 3. Measure before and after, in the
  editor and in a development build.
- **Batching:** use GPU instancing or static batching for repeated modules and props; keep the
  material count small.
- **Shaders by name:** every shader found by name (`Shader.Find`) must be guaranteed in builds
  (Always Included Shaders, or materials referenced from Resources). Today the URP Lit and
  Unlit lookups in exploration and the map layers are unverified in a player build.
- **Frame rate:** keep the fix from `d82a01b`, where movement works at any frame rate. Add an
  optional frame cap setting.

## Part 8 — Tests and acceptance

**Existing tests**
- All existing EditMode, PlayMode and .NET suites keep passing.
- `RealScenePlayModeTests` (click a city, enter, walk) must pass.

**New tests**
- Every layout building and prop kind produces geometry, with colliders where it should and
  none where it should not.
- The same layout produces the same scene (deterministic placement of clutter).
- No shader lookup returns null in a development build. This needs a small build-time check.
- Characters play their walk animation when moving and idle when not.

**Visual acceptance:** the after screenshot set, taken at the same camera positions as the
before set, reviewed by the project owner. Gameplay screenshots are evidence, not tests; don't
claim visual quality without them.

**Performance acceptance:** within the agreed budget in both the editor and a development build, with numbers reported.

## Strict scope limits

Don't change:
- simulation, economy, politics, saves or the command registry;
- the meaning of any map marker.

Don't add:
- gameplay (combat, quests, building);
- a time-of-day system;
- a weather system.

These belong to later phases. No pipeline change, no HDRP and no paid assets. Packages are allowed (Decisions, row 2),
each recorded in a short ADR.

## Delivery

Report:
- the before and after screenshot sets;
- the asset sources and licences;
- any packages added, with their ADRs;
- performance numbers before and after;
- test results by suite;
- known visual limitations.

Commit only after the tests pass and the owner has reviewed the screenshots. Stop after this
milestone.
