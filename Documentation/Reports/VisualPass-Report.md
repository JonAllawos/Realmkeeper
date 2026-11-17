# Visual-quality pass: report

**Date:** 2026-11-16. **Branch:** `feature/phase-0-1-foundation`. **Baseline:** `17a45f9`. **Brief:** `Briefs/Visual-Quality-Pass-Brief.md`. **Preflight:** `Systems/VisualPass-Preflight-Audit.md`.

## Summary

The baseline showed that the pass's best wins were bugs, not polish, and that the game was already over its performance budget. So the pass fixed what made the country look broken and made it cheaper, rather than adding effects.

| Change | Where | Effect |
|---|---|---|
| **Distant trees keep their leaves.** "Preserve coverage" mipmaps (reference 0.45, the leaves' cutoff) on the 19 alpha-tested leaf, grass, flower and vine textures. | Texture import settings | Beyond a few tens of metres the leaves faded out, leaving bare trunks; the birch trunks read as white sticks across the country. Now the canopies hold to the horizon. No run-time cost. |
| **The country's trees are trees.** Planting now picks from the region's species and the kit's five variants, weighted as `TreeFor` weights them. | `OverlandWorld.LoadTrees`, `PlantChunk` | The old code asked for five "variants" whose hashes all gave variant 1, so two or three kinds loaded; planting then picked among "the first ten kinds", which included the dead trees, rocks and bushes. About 70% of "trees" were dead trees, rocks or bushes. |
| **Exponential fog on the ground and water.** The project's own shaders (`TerrainGround`, `VertexColorLit`, `Water`) handled only linear fog; towns and the country use exponential-squared haze. Fog depth is now computed per pixel. | Shaders | The ground and lakes stayed full colour to the horizon while the trees and buildings on them faded, which flattened the view and made the far ground lime. The strategic map's linear fog is unchanged. |
| **The sky in the water** at grazing angles, from the ambient probe (no extra sampling). | `Water.shader` (`_SkyReflection`, default 0.75) | Lakes read as water across the land rather than flat navy. |
| **Country ground less lime:** the up-close brightening went from ×1.32 to ×1.18, with a little wider noise. | `OverlandWorld.ColourAt` | Deeper, less acid greens. |
| **Vegetation drawn only where seen.** Each camera now culls the instanced trees and ground cover to its view, and only trees within the shadow distance (plus 25 m) cast shadows. Trees thin out in the haze beyond 250 m (about a third remain at 600 m; each tree's own hash decides, so none flicker). | `OverlandWorld.OnBeginCamera` | Before, every tree within 650 m was drawn in every direction, and all of them again into the shadow map. |
| **Town lawns:** a large-scale tone variation (a generated, tileable texture as the lawn's detail map, every 32 m), a lighter base, and wet grass darkening and shining less than wet soil. | `M_Grass.mat`, `Art/Textures/T_GroundVariation.png` (generated in the project; no licence), `ExplorationAtmosphere.Weather` | Less of the flat, even green; lawns no longer go near-black in rain as quickly. |
| **Town draw calls:** the mesh merger's cell is 48 m instead of 24 m; townsfolk further than 30 m from the camera cast no shadow. | `MeshMerger`, `ExplorationController.Townsfolk` | About 25–30% fewer batches in town. |
| **A capture test for this pass.** | `Tests/PlayMode/VisualPassCaptureTests.cs` | The same town and country views at a fixed time and weather, with the engine's counters. Capture-only (needs `RK_CAPTURE_DIR`). |

## Evidence

- **Before:** `Screenshots/VisualPass/before/` (the pass's views, the Phase 21 weather set, the Phase 29 terrain set, and the Graphics Pass poses as PNG).
- **After:** `Screenshots/VisualPass/after/` (the pass's views).
- **Step-by-step numbers:** `Screenshots/VisualPass/steps/s1_stats.txt` … `s5_stats.txt` (the images of the intermediate steps were not kept).
- The two baseline stats files were overwritten by later captures under the same names and were restored by hand from the session's printed output; each says so.

## Measurements

Editor, RTX 4070 Laptop GPU, Direct3D 11, 1920 × 1080, the exploration camera. "Batches" and "triangles" are the engine's per-frame counters and are reliable. **The frame times are not comparable between runs:** in the step 4 run, views whose content had not changed got 30–100% slower, so they reflect the laptop's thermal state and the editor, not the scene.

| View | Batches before → after | Triangles before → after |
|---|---|---|
| Town street, clear noon | 3,900 → 2,775 | 8.5 M → 9.0 M |
| Town plaza, clear noon | 2,417 → 1,769 | 5.4 M → 7.0 M |
| Town overview | 4,030 → 3,022 | 7.8 M → 7.9 M |
| Town street, dusk | 4,712 → 3,397 | 10.2 M → 10.9 M |
| Town street, rain | 3,087 → 2,253 | 6.5 M → 6.6 M |
| Country eye level (three views), clear | 322–1,664 → 305–1,006 | **72.6–76.8 M → 1.4–14.2 M** |
| Country from above, clear | 320 → 308 | **72.6 M → 2.3 M** |
| Country eye level, rain | 255 → 275 | **36.5 M → 1.4 M** |

- **The country** now draws 2–20% of the triangles it did, with proper trees in place of dead ones. In between, planting the trees properly had raised it to about 122 M; culling and the shadow split brought it down.
- **Towns** are 25–30% lower in batches. Coarser merge cells cost up to 1.6 M more triangles in some views (culling by 48 m cells instead of 24 m).
- **Still over budget in town.** The budget is 1,500 draw calls; the busiest views are at 1,769–3,397. Most of what is left is the merged scenery: 106 materials, each drawn per 48 m cell. Reducing that further means consolidating materials (an atlas), which is a task of its own.
- **Not measured:** a development build, and any GTX 1660-class GPU. With the SRP Batcher on, a batch is cheap on the CPU, so whether 1,500 is the right limit for this pipeline is itself worth measuring on the target hardware.

## Tests

| Run | Result |
|---|---|
| Capture runs (`VisualPassCaptureTests`, `GraphicsCaptureTests`, `Phase21WeatherCaptureTests`, `Phase29TerrainCaptureTests.TheUnityTerrain_Captured`) | All passed (they also assert, for example, that rain and snow fall) |
| Focused PlayMode: Phase 5, 23, 24, 27, 28, the three Phase 29 terrain and ground-weather suites, townsfolk poses, audio pass | **26 / 26 passed** (277 s), on the final code |
| Focused EditMode: Phase 24 overland life, Phase 27 settlement life, Phase 28 settlement arrival | **24 / 24 passed** |
| **Full EditMode and full PlayMode** | **Not run.** Owed; the owner asked to be asked before suites are run. |

No existing test was changed.

## Not done, and why

- **Decals, wind sway, chimney smoke** (brief §3–4): not started. There was no headroom; each adds draw calls or shader cost in a game already over its town budget.
- **A sky HDRI and a reflection probe** (brief §1): not started, for the same reason and because the sky already reads well.
- **The town wall from outside** is still a plain brown slab when seen from the country (preflight finding 2).
- **Quality levels:** the project has two Unity levels ("Mobile" and "PC"), not the Low/Medium/High the brief assumed. Nothing in this pass needed a per-level switch; a three-level ladder is an owner decision.
- **Town draw calls** remain over budget (above).
- **Owner hand-play:** not done. Checklist below.
- **`GraphicsCaptureTests`'** poses no longer suit the walled towns; left unchanged.

## Owner checklist

1. Walk out of a town into the country at noon: the trees across the fields have leaves, and no field is full of dead trees and rocks.
2. Look across a lake from its shore: it reflects the sky toward the far bank, and darkens toward your feet.
3. Turn slowly in a forest: no trees pop in or out near you; far trees thin into the haze rather than vanish in a line.
4. Stand in a town lawn at noon, then in rain: the green varies over tens of metres; in rain it darkens but stays green.
5. On the strategic map: the water and ground look as before (linear fog is unchanged).
6. Watch the frame rate in a big town and in a forest compared with before; report anything that feels slower.

## Git

One local commit (`a6c0d51`), at the owner's word after the focused runs ("Commit locally, do not run edit mode or play mode"). **Pushed 2026-11-17** at the owner's instruction ("Push it"), with the seven earlier local commits beneath it (audio pass to M1.1). The full suites were not run, as the owner directed. The preserved uncommitted files are untouched: `M_LampGlow.mat`, the Unity MCP lines in `Packages/`, the untracked `Assets/InitTestScene*` scenes. `ProjectSettings/EditorSettings.asset` (changed by test runs) was restored before the commit.
