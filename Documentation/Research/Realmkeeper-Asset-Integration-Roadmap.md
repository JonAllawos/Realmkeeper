# Realmkeeper: asset integration roadmap

**Date:** 2026-11-12. **A plan, not work done.** Each step needs the owner's approval for any download or purchase it names; IDs refer to `Realmkeeper-Asset-Research-Master.md`.

## How assets enter the game today
- **Registration.** `Assets/Realmkeeper/Art/Resources/RealmkeeperArtCatalog.asset` (`ArtCatalog`) registers every model by name.
- **Buildings.** `ModularEnvironmentBuilder` assembles Quaternius village pieces into the buildings of a settlement plan.
- **Places.** `PlaceEnvironmentBuilder` maps place features to props (forge → Anvil, workbench → Workbench, bed → Bed_Twin1).
- **Shops.** `ShopInteriorView` builds shop interiors from boxes (`MetreMeshes.Box`) and shows the real stock through `ItemModels.Build`.
- **Items.** `ItemModels` draws items from primitives; `ItemDefinition.Visual` can redirect one item to another's model (`like:`).
- **Bodies.** `HumanoidCharacter` and `PlayerBodyBuilder` attach heads, hair, outfits and held items to the Quaternius universal rig. `Locomotion.controller` and the person override controller carry the animations.
- **Credits.** Every third-party pack has a licence file under `Art/ThirdParty/<Author>/<Pack>/` and a row in `CREDITS.md`. Non-redistributable packs are git-ignored (the Bestiary pattern).

## Phase 21 priorities (in order)

1. **Use what is owned (no download).**
   - Replace `ShopInteriorView`'s boxes with Fantasy Props pieces: Anvil, Workbench, Workbench_Drawers, Shelf_Simple, Shelf_Arch, Shelf_Small_Bottles, WeaponStand, Peg_Rack, Cabinet, Barrel, Crate_Wooden, Chest_Wood, CandleStick, Lantern_Wall.
   - Pick these per shop kind as data (a trade's "dressing set").
   - Use the KayKit Dungeon furniture in houses and taverns.
   - This is Phase 21's "physical displays matching stock" and "realistic interiors", with no new asset.
2. **Import the free remainder of the kits in use** (A1, N1; CC0, commit-safe): register in the art catalog; let the building and scatter rules draw on the new variants.
3. **Interiors.** Phase 21's data-driven interiors (`InteriorGenerator`) take room kinds (common room, kitchen, bedroom, workshop, storeroom) with dressing sets and density targets. The sets name catalog pieces, so later packs only add entries.
4. **Item models.**
   - Give `ItemModels` a prefab path: an item with a catalog prefab uses it, otherwise the primitive.
   - Map the 14 weapons and 2 shields first: from W2 (free, CC0) now, and W1 (Polytope) if bought.
   - Every mapping is data (`ItemDefinition.Visual`); `like:` keeps working.
5. **Icons.** Add an editor step that renders each item's model to a 128² transparent PNG under `Presentation/UI/Resources/ItemIcons/`. Items fall back to their game-icons silhouette.
6. **Weather visuals** (E3): URP particle systems for rain, snow and drizzle; fog and sky tint by the simulation's regional weather; wind as a vertex-shader sway on vegetation materials.
   - The simulation stays the only owner of weather state (deterministic, saved).
   - COZY (E1), if bought, is driven by that state, never the reverse.
7. **Audio** (S1, S2): an `AmbienceView` choosing loops by place, weather and time (rain, wind, forest, market, tavern, forge), plus one-shots for footsteps by surface and doors.
   - Only the edited files chosen from the Sonniss bundles are committed (permitted as part of the game). The bundles themselves stay outside the repository.
8. **Crafting stations.** The anvil, workbench and (later) tanning frame are interactables bound to Phase 21's recipes. With C4 bought, the hammering clip plays on the person working there.

## Phase 22+ priorities
- **Pro editions** (A2, F2, N2): more roofs, buildings, props, worn variants, seasonal vegetation.
- **Taverns** (T1), after the free Armory (W3) confirms the publisher's style.
- **Leatherworking:** L1 if the previews fit; otherwise a small custom hide-and-frame set in the Quaternius style.
- **Work and social animations** (C4, C5): retarget onto the universal rig through Mecanim humanoid avatars.
  - Add the clips to the person override controller.
  - Drive them by NPC work activity (Phase 21).
- **Species anatomy** (C1, Quaternius Fantasy Races):
  1. Confirm the races.
  2. Keep it local-only under QAL (git-ignored, with a licence file, as the Bestiary).
  3. Map the species to it in `SpeciesAppearance`; keep the human body as the fallback when it is absent, as creatures already do.

## Optional future polish
- **The Polytope line** (A3, C2, N3) as a coherent second look, if the owner ever wants to leave the Quaternius style. That would be a whole-world swap, not a mix.
- **Volumetric skies** (E2), if performance allows.
- **A realistic pivot:** see the master report, section 3.3. Not planned.

## Rules every integration follows
1. **Licence first.** Read the licence file before import; add it under `Art/ThirdParty/<Author>/<Pack>/License.txt`; add a `CREDITS.md` row.
2. **Placement in the repository.** Git-ignore anything that may not be redistributed (QAL, Asset Store, Fab, Synty) unless the owner confirms a private repository and the seat terms are met.
3. **Materials.** Convert to URP/Lit, or use the project's shared palette material. No HDRP-only or custom-pipeline shaders.
4. **Scale and orientation.** 1 unit = 1 m; check pivots; the modular grid is 2 m for Quaternius walls.
5. **Determinism.** Generation picks pieces by stable IDs from the catalog, never by load order, so worlds do not change when packs are added: new pieces join only through new data.
6. **Owner checklist.** Each import adds captures and a checklist entry. Nothing is claimed as hand-tested.
