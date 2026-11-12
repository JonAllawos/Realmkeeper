# ADR-0097: Third-party creatures and scenery come in through one pipeline, licence first

**Context.** Most creatures were drawn as capsules, and settlement trees were spheres on sticks. The owner approved CC0 kits and, later, CC BY works with credit. Several sources serve GLB only, and the project has no glTF importer.

**Decision.**
1. **Licence first.**
   - Each pack's folder holds its licence: the bundled file, or a written record of the listing where the download had none.
   - The CC BY works carry their exact attribution lines in CREDITS.md and in the in-game Credits.
   - Nothing is restricted. The QAL Bestiary stays git-ignored and local, and is no longer needed for goblins.
2. **GLB to FBX with Blender**, run headless with the owner's Blender (`Tools/Blender/glb2fbx.py`). Each model is:
   - converted;
   - uniformly scaled to a stated real-world height;
   - trimmed to the clips the game drives: idle, walk, run or gallop, attack, one hit reaction and death;
   - fixed up by `Tools/Blender/fixfbx.py`: stray helper meshes removed (each Quaternius source carried an "Icosphere" that showed as a grey rock), feet set on the ground, and the model turned half round to face Unity's +Z. Changing the export axis does not do this, because Unity reads the axis system from the file and undoes it.
   - Models that ship at the wrong size are rescaled the same way (`fbxrescale.py`; the farm-pack sheep was 4.4 m tall).
   - A texture embedded in the FBX is extracted beside it as `<Model>_Texture.png` (`extracttexture.py`), because Unity left those materials untextured.
   No new Unity package is added.
3. **Import rules** (`ArtImportPostprocessor`, version 7):
   - creatures import as generic rigs whose idle, walk and run clips loop;
   - static kits import with no rig and readable meshes;
   - both import URP materials through the pipeline's material-description path, which binds an extracted `<Model>_Texture.png` as the base map;
   - the nature kit's leaves, grass and flowers are alpha-clipped and two-sided.
4. **One combat controller.**
   - Each animated creature gets an override of the shared combat controller, with its own clip in every slot by purpose; a slot with no clip stands still.
   - Static models tip over when they fall.
   - A body keeps its model's own root turn (the Poly by Google models stand on one), both when placed and when it falls and rises.
   - `CombatantBodies.AnimalFor` maps 19 creatures onto 15 models, some scaled or tinted where one model draws two creatures.
5. **Scenery.**
   - The art catalog's `Scenery` list feeds settlement trees and wells, and place features: chests, crates, lecterns, anvils, workbenches, beds, and candles on altars.
   - The built shapes remain the fallback where the kits are absent.

**Consequences.**
- `Art/ThirdParty` is about 228 MB.
- Size, facing and materials are checked in the Phase 18 capture `p18_12_creature_lineup`, which logs each model's height. That check found the backwards facing, the rocks, the oversized sheep, the untextured giant, goblin and Poly animals, and the Poly bear and boar stood on end; all were fixed.
- Not done this phase:
  - full dungeon walls and floors from the KayKit kit, which would mean rebuilding the place builder;
  - dormant skeleton poses, which need a presentation path for creatures at rest.
