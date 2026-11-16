# Phase 29: preflight audit

**Date:** 2026-11-15 to 2026-11-16. **Written at close-out (2026-11-16)** from the findings recorded while the work was done; no separate document was written before the first change.

**Baseline:** `80093e2` (Phase 28 pushed), branch `feature/phase-0-1-foundation`. The working tree also held the uncommitted audio pass (committed later as `da4fb07`), the Unity MCP package line in `Packages/`, and the two untracked `Assets/InitTestScene*` scenes.

**Scope decided with the owner:**
- The Phase 29 brief asked for the gate defect fix, then overland exploration content (sections 3–8: discoveries, encounters, escorts, the ancient realm).
- The owner chose to **focus on the terrain**. When asked whether MapMagic, Gaia or MicroSplat should replace it, the owner chose a **Unity Terrain renderer**.
- Sections 3–8 are **deferred, not started**.

## 1. The gate defect (owner report)

| Question | Finding |
|---|---|
| How are gatehouses placed? | In `ModularEnvironmentBuilder.Plan.cs` (renderer), at each `plan.Gates` point, **turned to face the settlement's centre** |
| How is the wall opened? | By a fixed cut along the wall line at each gate, sized for a road crossing at a right angle |
| How do roads meet the wall? | As generated: some jog at the wall. The owner's gate had two turns of about 70° within 6 m |
| How many gates are blocked? | 3 of 60 across 10 seeds (a tower or the wall within the road's width) |
| Can a test see it? | No: the geometry existed only in the renderer |

**Root cause:** a gatehouse facing the centre stands across a road that meets the wall at a slant; the fixed cut leaves wall in the road. Decision and fix: ADR-0121.

## 2. The terrain (owner question: MapMagic, Gaia or MicroSplat?)

| Question | Finding |
|---|---|
| Where do the overland's heights come from? | `OverlandTerrain.HeightAt` (Domain), from the region map and a seeded hash |
| Who reads them? | Travel, walkability, roads, settlement layouts and gates, places, and the .NET tests |
| How is the ground drawn? | Phase 23 chunk meshes (`Realmkeeper/VertexColorLit`): a coarse mesh everywhere, a 10 m detail mesh with a `MeshCollider` near the player |
| What would MapMagic or Gaia do? | Generate their **own** heights: a second source of terrain truth the game logic cannot read |
| What would MicroSplat do? | Texture Unity Terrains only; it needs a Unity Terrain first |
| Licences | MapMagic 2 and Gaia are Asset Store per-seat licences; Gaia is paid. Not bought |

**Recommendation accepted by the owner:** a Unity Terrain filled from `HeightAt`, keeping one source of truth, with the old mesh ground kept as a fallback (ADR-0122).

## 3. What the Unity Terrain had to keep

- The same heights, within 5 mm, for the drawn ground and the collider.
- The biome colours and ground grain of `VertexColorLit`, and Phase 24's ground weather (`_Wet`, `_Snow`, ADR-0115).
- The game's fog, the trees, grass and barriers streamed by the 640 m chunks.
- Region switches, save and load, and the ancient realm.

## 4. Found later in the phase, by the matched screenshots

These were not known at the start. Each is recorded with its correction in ADR-0122:

- The terrain was darker: colours already linear were stored in an sRGB texture.
- The terrain got no sunlight: URP's per-renderer light flag is 0 for a Terrain drawn with a custom shader.
- Roads and bridge decks drew with the terrain material and lost their colour.
- Under real weather the two grounds differed: the atmosphere tinted the mesh ground's per-visit material copies and hid ADR-0115's wet and snow.

## 5. Constraints carried through the phase

- No commits or pushes until the owner asked (the local commits came on 2026-11-16, not pushed).
- The audio pass, the Unity MCP package line and the `InitTestScene*` scenes preserved.
- Focused tests only; no full Unity suites after the first round.
- No claim of hand-play or of development-build measurements.
- `OverlandWorld.UseUnityTerrain` has defaulted to `true` since the renderer was added, so the game draws the Unity Terrain; the mesh ground is a switch-only fallback. Claude later asked "Shall I make the Unity Terrain the default?" and the owner said "not yet". That question was wrongly framed: it was already the default. What the owner meant (keep it as is, or switch back to the mesh ground until playtested) is an **open owner question** (completion report §11).
