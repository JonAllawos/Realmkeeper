# Third-party asset inventory

Every external asset in the repository, with its licence and what was done to it (ADR-0010).
All of them are **CC0 1.0 Universal** (public domain dedication): free to use, modify and ship,
commercially or not, and no attribution is required. Credit is given here anyway.
Downloaded 2026-11-10. Nothing was purchased.

| Pack | Author | Source | Licence (file in repo) | Used for |
|---|---|---|---|---|
| Medieval Village MegaKit (Standard) | Quaternius | quaternius.com, downloaded from quaternius.itch.io (free tier) | CC0 1.0, `Art/ThirdParty/Quaternius/MedievalVillageMegaKit/License.txt` | Building modules, kerbs, fences, crates, wagon, chimneys, vines |
| Universal Base Characters (Standard) | Quaternius | quaternius.com/packs/universalbasecharacters.html (itch.io free tier) | CC0 1.0, `…/UniversalBaseCharacters/License.txt` | Heads (cut from the full bodies), eyes, brows, 8 hair and beard pieces |
| Modular Character Outfits – Fantasy (Standard) | Quaternius | quaternius.com (itch.io free tier) | CC0 1.0, `…/ModularCharacterOutfitsFantasy/License.txt` | Clothed bodies: Male/Female × Peasant/Ranger |
| Universal Animation Library (Standard) | Quaternius | quaternius.com (itch.io free tier) | CC0 1.0, `…/UniversalAnimationLibrary/License.txt` | Idle, walk, jog, sprint, talk (in-place version) |
| Grass004, Ground037, Ground054, PavingStones070, Planks021, Wood049 | ambientCG (Lennart Demes) | ambientcg.com (1K-JPG) | CC0 1.0 (site-wide statement at docs.ambientcg.com/license), `Art/ThirdParty/AmbientCG/License.txt` | Grass, dirt road, gravel, cobbles, planks, furniture wood |

## What is in the repo and how it was modified

- **MegaKit:** 73 of the 176 FBX models (the ones the planner, kerbs, fences and dressing use).
  - Colour and normal maps for Plaster, WoodTrim, UnevenBrick, Brick, RockTrim and RoundTiles,
    plus RedBrick colour, VineLeaf and WindowGradient. All downscaled from 2048 to 1024 px.
  - Normal maps are from the pack's "Godot-Unity" (OpenGL) set. Roughness, ORM, wear and noise
    maps are not used.
- **Base characters:** both full-body FBX files, kept so the setup can cut head meshes from them.
  - All 8 "Rigged to Head Bone" hair FBX files.
  - Skin colour maps (renamed `T_Head_*`), both hair colour maps, and the eye map, at 1024 px.
- **Outfits:** the 4 complete outfit FBX files.
  - Peasant, Peasant 2, Ranger and Ranger 3 colour maps (4096 → 2048 px).
  - Peasant and Ranger normal maps (4096 → 1024 px).
  - The male and female body skin maps (renamed `T_Body_*`, 2048 → 1024 px).
- **Animation library:** `UAL1_Standard.fbx` (the non-root-motion file), imported as humanoid.
  Clips ending in `_Loop` loop.
- **ambientCG:** Color and NormalGL maps of each material, re-encoded as JPEG at 1024 px.

Total: about 80 MB in `Assets/Realmkeeper/Art/ThirdParty`.

## Generated from these assets (in `Assets/Realmkeeper/Art`)

The setup (`Realmkeeper ▸ Set Up Art`) produces:
- `Materials/M_*.mat`: 38 URP Lit materials and a procedural sky (Unity's built-in `Skybox/Procedural`).
- `Characters/Head_Male.prefab` and `Characters/Head_Female.prefab`, with `Head_*_Mesh.asset`:
  head and neck triangles cut from the base characters by bone weight.
- `Characters/Locomotion.controller`: the animator controller.
- `Resources/RealmkeeperArtCatalog.asset`: the lookup the game uses.

## Not used, but worth knowing

The Standard tiers are the free subsets. Each pack has a paid "Source" tier with more outfits,
hairstyles, animations (including turn-in-place) and kit pieces. They are recommend-only
(ADR-0010): nothing here depends on them, and buying one needs the owner's decision.

## Phase 19: interface assets (downloaded 2026-11-12)

| Pack | Author | Source | Licence (file in repo) | Used for | Taken |
|---|---|---|---|---|---|
| game-icons.net | Lorc, Delapouite and 9 others | game-icons.net (PNG export, white on transparent); SVGs from github.com/game-icons/icons | **CC BY 3.0**, `Art/ThirdParty/GameIcons/License.txt` | The one icon family | 142 icons (`Resources/RkIcons`, 512 px PNG imported at ≤256) and their SVGs (`Source/`); list in `Tools/UI/icon-manifest.tsv` |
| Fantasy UI Borders 1.0 | Kenney | kenney.nl/assets/fantasy-ui-borders | CC0 1.0, `Art/ThirdParty/Kenney/FantasyUIBorders/License.txt` | Window corner frame (9-slice), dividers | "Double" (96 px): 32 hollow borders, 6 dividers, 6 fading dividers |
| Cartography Pack | Kenney | kenney.nl/assets/cartography-pack | CC0 1.0, `Art/ThirdParty/Kenney/Cartography/License.txt` | Atlas symbols, compass, parchment | 29 "Retina" (128 px) symbols, parchmentBasic and parchmentAncient (1024 px) |
| EB Garamond | The EB Garamond Project Authors | github.com/octaviopardo/EBGaramond12 | **SIL OFL 1.1** (owner-approved), `Art/ThirdParty/Fonts/EBGaramond/OFL.txt` | Headings, dialogue, the atlas | Regular, SemiBold, Italic (TTF) |
| Alegreya Sans | The Alegreya Sans Project Authors | github.com/google/fonts/tree/main/ofl/alegreyasans | **SIL OFL 1.1** (owner-approved), `Art/ThirdParty/Fonts/AlegreyaSans/OFL.txt` | Body text, controls, numbers | Regular, Medium, Bold, Italic (TTF) |

Import rules (`ArtImportPostprocessor`, version 8):
- interface textures under `Resources/RkIcons`, `RkFrames` and `RkMap` have no mipmaps and clamped edges;
- they use high-quality compression;
- icons are capped at 256 px, frames and symbols at 128, and parchment at 1024.

About 4 MB in all. The full research and the rejected candidates are in `Documentation/Research/Phase19-Asset-License-Register.md`.
