# Visual-quality pass: preflight audit

**Date:** 2026-11-16. **Branch:** `feature/phase-0-1-foundation`. **Baseline commit:** `17a45f9` (M1.1). **Brief:** `Briefs/Visual-Quality-Pass-Brief.md` (the owner chose this before the KitBash3D trial).

## 0. Baseline evidence

| Set | Where | How |
|---|---|---|
| The pass's own views | `Screenshots/VisualPass/before/before_*.jpg`, `before_stats.txt` | New `VisualPassCaptureTests`: the exploration camera at views derived from the town's layout (inside the main gate, the street towards the plaza, the plaza, a house, the approach, an overview), in clear noon, overcast, light rain and dusk; then the country at eye level in three directions and from above, in clear noon and rain. Spring (May), fixed weather. |
| Weather set | `before/weather_*.jpg` | `Phase21WeatherCaptureTests`, unchanged. |
| Country ground set | `before/p29_terrain_*.jpg` | `Phase29TerrainCaptureTests.TheUnityTerrain_Captured`, unchanged. |
| Graphics Pass views | `before/before_*.png` | `GraphicsCaptureTests`, unchanged. **Its poses no longer suit the towns:** since Phase 26–28 a town is walled and planned, so "street" lands outside the wall at dusk and "overview" shows only fog. Kept for the record; the new test replaces it for this pass. |

All captures ran in the open editor (RTX 4070 Laptop GPU, Direct3D 11). No development build was made for the baseline.

## 1. What exists (read before changing)

| Area | Finding |
|---|---|
| Strategic map | `SceneEnvironment`: a sun, Trilight ambient, linear fog, one global volume (Neutral tonemapping, mild bloom, warm white balance, vignette). |
| Town and country light | `ExplorationAtmosphere` (three partial files): a sun and a moon on the clock and the season, Trilight ambient plus a hand-built SH probe, a procedural sky material from the art catalog, exponential-squared haze, a street volume (ACES, colour adjustments, white balance, shadows-midtones-highlights, bloom, vignette). Street lamps and door lanterns are real point lights from dusk (32 + 24 budget in planned towns). Shadow distance 90 m, two cascades. |
| Weather | Already drawn (Phase 21): rain and snow particles, clouds, stars, lightning; cloud dims the sun; wet ground darker and glossier; snow whitens ground and roofs; leaves change with the season. **The brief's §3 "wetness in rain" already exists.** |
| Country ground | Unity Terrain with the custom `TerrainGround` shader: a biome colour map, dirt and rock weights, the ambientCG grass, dirt and rock as grain at 0.25 tiles a metre. `ColourAt` brightens the biome colours by 1.32 "because it is seen up close". |
| Country plants | `OverlandWorld`: trees, bushes, rocks and ground cover from the owned nature kit, GPU-instanced per camera, streamed by chunk. Grass draws to 95 m. |
| Town ground | `ModularEnvironmentBuilder`: lawns with the ambientCG grass, cobbled roads, verge clutter (small stones and tufts, no collision). |
| Anti-aliasing | SMAA (medium) on the exploration camera. MSAA off in both quality levels. |
| SSAO | On in the PC renderer (intensity 0.4, radius 0.3). |
| Quality levels | **Only two: "Mobile" and "PC".** The brief assumed Low, Medium and High. `RkSettings.Quality` switches between Unity's levels. |
| Shaders by name | `VertexColorLit`, `TerrainGround` (via a Resources material), `Water`, URP Lit and Unlit. |

## 2. Findings from the baseline

### Bugs

1. **Distant trees lose their leaves.** The leaves are alpha-tested (cutoff 0.45). Their mipmaps average the alpha below the cutoff, so beyond a few tens of metres only trunks remain; the birch trunks then read as white sticks across the country. Unity's fix is the import option "mip maps preserve coverage". (Diagnosed with temporary `Diagnose_CountryTrees` and `Diagnose_TownRenderers` captures, since removed. The same diagnosis found a second cause of bare trees: the planting bug fixed in the report.)
2. **A town's wall seen from the country is a plain brown slab** without texture or crenellation (`before_clear_noon_country_eye_2`).

### Looks

3. **Town lawns** are one flat, even green that fills most of every view; at dusk and in rain they go nearly black.
4. **Country ground** is bright lime from far and near, with dark cloud-like blotches seen from above.
5. **Lakes** are a flat dark navy with no reflection of the sky.
6. **Hard edges** where roads meet grass, in town and country.
7. The sky and the buildings already read well; the post-processing is reasonable.

### Performance (against the Graphics Pass budget: 60 fps at 1080p on a GTX 1660-class GPU, at most 1,500 draw calls in a town)

| View | Batches | Triangles | Frame (editor, RTX 4070 Laptop) |
|---|---|---|---|
| Town street, clear noon | 3,900 | 8.5 M | 6.3 ms |
| Town street, dusk | 4,712 | 10.2 M | 6.5 ms |
| Town overview | 4,030 | 7.8 M | 9.4 ms |
| Country, eye level, clear | 322–1,664 | **72–77 M** | 11.5–12.5 ms |
| Country, eye level, rain (no sun shadows) | 255 | 36.5 M | 6.6 ms |

- **Towns are 2.6–3.1 times over the draw-call budget.** The Graphics Pass measured at most 955, in the smaller towns of that time; Phases 26–28 made towns planned, walled and much larger.
- **The country draws 72–77 million triangles** in clear weather, half of them in the sun's shadow pass. On the target GPU (roughly a quarter to a third of this one) that would be far over 16.7 ms. This is an estimate, not a measurement.
- **The pass therefore has no headroom.** Rule 3 of the brief applies with force: any addition must pay for itself, and the cheapest visual wins are those that cost nothing (import settings, colour) or that reduce cost.

## 3. Order of work (decided in preflight)

1. Leaf and grass coverage at distance (import settings only; no cost).
2. Country ground and water colour (shader constants; no cost).
3. Country triangle count: find what draws 36 M triangles a pass, and cut it without losing what the player sees.
4. Town lawns: colour variation and less darkness in dusk and rain (material and light; no draw calls).
5. Town draw calls: measure what the 3,900 batches are, and reduce what is cheap to reduce.
6. Then, only with headroom: decals, wind, smoke, a second quality level.

Each step is captured and measured with `VisualPassCaptureTests` before the next.

## 4. Decisions taken in preflight

1. **The quality-level ladder.** The project has two Unity quality levels, not three. The pass maps its additions onto "PC" and "Mobile" and records that a Low/Medium/High ladder would be new work (a decision for the owner, not taken here).
2. **The new capture test** replaces `GraphicsCaptureTests` as this pass's evidence. The older test is left as it is.
3. **No new assets so far.** Everything in §3 steps 1–5 uses what the project owns.
