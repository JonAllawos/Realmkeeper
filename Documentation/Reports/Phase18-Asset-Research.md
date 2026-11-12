# Phase 18 — Free Asset Research

Researched 2026-11-12 at the owner's request, as input to Phase 18 Part 9 (assets) and later phases.
**Nothing has been downloaded.** Per the Phase 18 brief, no pack is added without the owner's explicit
go-ahead, and each pack's bundled `License.txt` is checked on download before it is committed.

## What the project has now (from the repository)

| Area | Current state | Evidence |
|---|---|---|
| Town buildings and props | Quaternius Medieval Village MegaKit (CC0) | `Art/ThirdParty/Quaternius/MedievalVillageMegaKit` |
| People | Quaternius Universal Base Characters, Modular Outfits Fantasy, Universal Animation Library (CC0) | `Art/ThirdParty/Quaternius/*` |
| Undead | KayKit Skeletons 1.1 and Character Animations 1.1 (CC0) | `Art/ThirdParty/KayKit/*` |
| Monsters | Quaternius Bestiary (Imp and Puglin only): QAL licence, **local only, never committed** | `Art/ThirdParty/QuaterniusBestiary` (git-ignored) |
| Ground and wood materials | ambientCG, 6 materials (CC0) | `Art/ThirdParty/AmbientCG` |
| Place interiors (mines, crypts and others) | Built at runtime from Unity primitives; levers are a cube and a cylinder | `PlaceEnvironmentBuilder.cs:540` |
| **Audio** | **None.** No `AudioSource` or `AudioClip` anywhere in the code, and no sound files | grep of `Assets/Realmkeeper` |
| Quaternius Ultimate Monsters | **Not installed** | — |
| UI | Unity UI with no art kit | — |

## Recommended candidates

All of these are free downloads. "CC0" means the listing says CC0; the bundled licence file is the
record that counts.

### Tier 1: fills a visible gap and fits the existing low-poly style

| Pack | Licence | What it would fix | Phase |
|---|---|---|---|
| [KayKit Dungeon Remastered](https://kaylousberg.itch.io/kaykit-dungeon-remastered) (200+ pieces, v1.1) | CC0, name-your-price ($0 allowed); please don't resell unmodified copies | Real walls, floors, stairs, doors, chests, levers and barrels for crypts, catacombs, underground works and ruins, replacing primitive geometry. It matches KayKit Skeletons already in the game. | 18 (exploration depth) |
| [Quaternius Ultimate Monsters](https://sketchfab.com/3d-models/ultimate-monsters-pack-fd72e114d119488da71fe3a16f216c4f) (50 animated monsters) | CC0 | Committable monster figures with attack, walk and death animations. It removes the dependence on the local-only Bestiary. | 18 or 19 |
| [Quaternius Fantasy Props MegaKit](https://quaternius.itch.io/fantasy-props-megakit) (Standard tier) | Free Standard tier, listed as CC0 elsewhere: **confirm the licence file** | Documents, books, chests, altars, tools and loot props for discoveries, mechanisms and rewards | 18 |
| [Kenney RPG Audio](https://kenney.nl/assets/rpg-audio) (50 sound effects) | CC0 | Footsteps, doors, metal, coins and book sounds: a first audio layer. Code would need an audio system first. | 18 (small) or a later UX phase |

### Tier 2: strong fits for later phases

| Pack | Licence | Use |
|---|---|---|
| [KayKit Halloween Bits](https://kaylousberg.itch.io/halloween-bits) | CC0, free | Crypt and graveyard dressing: gravestones, coffins, candles |
| [KayKit Forest Nature Pack](https://kaylousberg.itch.io/kaykit-forest) (200+ assets) | CC0 | Wilderness and travel-stage foliage, rocks and terrain |
| [Quaternius Stylized Nature MegaKit](https://quaternius.itch.io/stylized-nature-megakit) (Standard, 99 MB) | CC0 (Pro and Source tiers are paid) | Alternative to the KayKit forest pack: closer to the Quaternius village style |
| [KayKit Adventurers](https://kaylousberg.itch.io/kaykit-adventurers) | CC0, free (5 characters) | Companion or hero figures. Its style differs from the Quaternius modular people, so it suits Workstream B (character creation) rather than a swap now. |
| [KayKit Furniture Bits](https://kaylousberg.itch.io/furniture-bits) | CC0 | Interior furniture. It may be sold only in the paid Bits Bundle: **check that a free tier exists.** |
| [Kenney Fantasy UI Borders](https://kenney-assets.itch.io/fantasy-ui-borders) (130+ nine-slice sprites) | CC0 | Window and dialog frames: Workstream A (UI/UX overhaul) |
| [Kenney UI Pack: RPG Extension](https://opengameart.org/content/ui-pack-rpg-extension) | CC0 | Bars, buttons and panels: Workstream A |
| [Poly Haven](https://polyhaven.com/license) | CC0 | Sky HDRIs and PBR textures (stone, plaster, mud): lighting and material quality |
| ambientCG (already used) | CC0 | More stone, brick and rock materials for the archetypes' `Material` looks ("brick", "dressed_stone", "shored") |

### Music (CC0)

RandomMind's medieval tracks on OpenGameArt are listed as CC0 and include loop versions:
- [The Old Tower Inn](https://opengameart.org/content/medieval-the-old-tower-inn) (tavern)
- [Medieval Kings Feast](https://opengameart.org/content/medieval-kings-feast)
- [The Bard's Tale](https://opengameart.org/content/medieval-the-bards-tale)

These suit settlements and inns once an audio layer exists.

## Not CC0: usable with conditions

| Source | Licence | Condition |
|---|---|---|
| [game-icons.net](https://game-icons.net/about.html) (4,000+ icons) | CC BY 3.0 | Credit each icon's author in CREDITS and in the in-game Credits. A strong fit for item, condition and journal-category icons. |
| Quaternius Bestiary (current) | QAL 1.0 | Use is allowed, but the files can't be redistributed, so they stay git-ignored. Recommend replacing it with Ultimate Monsters. |
| Mixamo animations | Adobe terms | Requires an Adobe sign-in, and the raw files can't be redistributed. **Not recommended**: KayKit and Quaternius animations already cover the need. |

Avoid share-alike licences (CC BY-SA, GPL art), which would add obligations to the project, and
anything that imitates an existing game's look.

## Outcome of the owner's choice: "Dungeon kit + Monsters" (2026-11-12)

**KayKit Dungeon Remastered: installed.**
- Source: the author's official GitHub mirror (`KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0`), version 1.0.
- Licence: the bundled `License.txt` reads "Creative Commons Zero, CC0"; credit to Kay Lousberg is optional.
- Installed at `Assets/Realmkeeper/Art/ThirdParty/KayKit/DungeonRemastered/`: 203 OBJ models with their MTL files, one 16 KB texture atlas and `License.txt`, about 8.4 MB in total.
- Not git-ignored, so it is committed with Phase 18.
- The mirror carries v1.0, not the itch.io v1.1, which adds 8 stair and ceiling pieces. That gap is acceptable.

**Quaternius Ultimate Monsters: not installed. Two findings:**
1. **Download blocked.** The pack's only download is a Google Drive folder (linked from quaternius.com),
   and Drive currently answers "Quota exceeded: too many users have downloaded this file".
2. **Poor fit.** The FBX listings in that folder show 50 cute, chibi-style creatures: Alien, Birb, Bunny,
   Cactoro, Mushnub, the blobs, Alpaking, Armabee, Glub, Squidle, Hywirl, and so on. The only grounded
   ones are Orc, Orc_Skull, Ghost, Ghost_Skull, Demon, Yeti, Dragon, Wizard and Ninja.
   - None of them maps onto the creatures the game draws as primitives today: wolf, dire wolf, black and
     brown bear, boar, deer, giant rat, giant spider, goblin, ogre and troll.
   - The style also clashes with the Quaternius village people and the KayKit skeletons.
   - This fails the condition the recommendation set ("only if it maps onto creatures already in the
     catalog").

Better-fitting alternatives, all listed as CC0 and not yet verified:
- Quaternius [Animated Animals](https://opengameart.org/content/animated-animales-low-poly) (wolf, dog,
  cat, eagle);
- Quaternius [Lowpoly Animated Farm Animals](https://opengameart.org/content/lowpoly-animated-farm-animal-pack)
  (horse, cow, sheep, pig).

These would cover wolves and livestock. No free CC0 pack found so far covers the spider, rat, bear or deer.

## Outcome of the owner's choice: "Try the animal packs" (2026-11-12)

Both packs were downloaded from OpenGameArt; both listings say CC0, and both were uploaded by the author's
account. Only the animals that map onto creatures already in the game were installed. Each file is
unmodified FBX with no textures (material colours only).

| Model | Animation takes in the file | Game creature(s) | Installed at |
|---|---|---|---|
| Wolf | Idle, Walking | `wolf`, `dire_wolf` (scaled up) | `Quaternius/AnimalPackVol2/` |
| Dog | Idle, Walking | `mastiff` | `Quaternius/AnimalPackVol2/` |
| Horse | Idle, Walk, WalkSlow, Run, Jump, Death | `riding_horse`, `draft_horse` | `Quaternius/FarmAnimals/` |
| Cow | Idle, Walk, WalkSlow, Run, Jump, Death | `cattle` | `Quaternius/FarmAnimals/` |
| Sheep | Idle, Jump | `sheep` | `Quaternius/FarmAnimals/` |
| Pig | Idle, Jump | `boar`: possible stand-in, tinted; judged on screenshot | `Quaternius/FarmAnimals/` |

Licence records:
- **Farm Animals** ships its own `License.txt` ("CC0 1.0 Universal"), kept as-is.
- **Animal Pack Vol.2** ships with no licence file, so `AnimalPackVol2/License.txt` records the CC0
  OpenGameArt listing and its URL.

Not installed, because nothing in the game uses them: Cat, Eagle, Piranha, Llama, Pug, Zebra.

Limitations to handle in integration:
- Wolf and Dog have no attack or death takes. Combat falls back to the existing hit flash and fall.
- Sheep and Pig have no walk take.

Still uncovered, and still drawn from primitives: giant rat, giant spider, black and brown bear, deer,
goat, rabbit, fox, chicken, goblin (the Puglin is local-only), ogre and troll.

## Round 3: attribution-required licences allowed (owner, 2026-11-12)

The owner allowed licences that require credit (CC BY). Two remaining gaps were researched:
- creatures still drawn from primitives;
- settlement structures still built from primitives in `SettlementEnvironmentBuilder.cs`:
  - trees (a cylinder trunk and a sphere canopy, line 236);
  - wells (a cylinder, line 211);
  - barrels (line 225);
  - market stalls, lamp posts and signs;
  - the plaza disc (line 58).

Source: [Poly Pizza](https://poly.pizza) hosts individual models with each creator and licence listed.
Many are Quaternius CC0 models, and its downloads are not subject to the Google Drive quota. Every model
it serves is **GLB only**, and the project has no glTF importer. Using these models therefore needs
Unity's official glTFast package (`com.unity.cloud.gltfast`, Apache-2.0), which also opens the KayKit
`.glb` files. Blender is not installed, so offline conversion to FBX is not an option.

### Creatures: Quaternius, CC0, rigged and animated (per the Poly Pizza listing)

| Game creature | Model | Tris | Poly Pizza ID |
|---|---|---|---|
| `giant_spider` | Spider | 2,712 | `yRYJiAJyiM` |
| `giant_rat` | Rat | 4,004 | `iltq5bVNaV` |
| `deer` | Deer; Stag for a variant | 1,892 / 1,933 | `T6Cs7tmMHJ`, `tQdzbZ1Cmw` |
| `fox` | Fox | 1,826 | `Bc97C66HKi` |
| `goblin` (replaces the local-only Puglin) | Goblin | 2,912 | `OdCOFSmEhl` |
| `ogre`, `troll` | Giant; Orc as an alternative | 3,812 / 4,312 | `BldaiPtyJa`, `5vO2YJsPEf` |
| `cattle` (bull variant) | Bull | 2,064 | `a8PIIYwF7r` |
| `chicken` | Chicken | 2,648 | `ineV9pU5VL` |
| `rabbit` | Bunny: chibi style, **probably reject** | 5,948 | `irZjWFARyl` |

Animation clip names can't be read without downloading. Check them on download, as was done for the
animal packs.

### Creatures with only static CC BY 3.0 models

| Game creature | Model | Credit line |
|---|---|---|
| `black_bear`, `brown_bear` | Black bear (666 tris), not animated | "Black bear" by Poly by Google, CC BY 3.0 |
| `boar` | Boar (782 tris), not animated | "Boar" by Poly by Google, CC BY 3.0 |
| `goat` | Goat (958 tris), not animated | "Goat" by Poly by Google, CC BY 3.0 |

A static figure that slides and tips over on death is still better than a capsule. The style is Google
Poly's flat-shaded low poly, slightly plainer than Quaternius.

### Settlements and shops

| Gap | Candidate | Licence | Note |
|---|---|---|---|
| Market stalls, shop fronts, chests, furniture, tools, potions, sacks | [Quaternius Fantasy Props MegaKit](https://quaternius.itch.io/fantasy-props-megakit), Standard tier | CC0 (itch, name your price) | **Best single pick for shops.** Over 200 props on 4 texture sets, made by the same author as the village kit already in the game. The itch download needs one browser click ("No thanks, just take me to the downloads"). |
| Market stalls (as individual models) | Quaternius "Market Stalls" / "Market Stand" | CC0 | Poly Pizza `PUZZ5F91OE` and `DGIM5HGISb`; a fallback if the MegaKit is not used |
| Well | Quaternius "Well" | CC0 | Poly Pizza `QlqncKYxXb` (1,870 tris) |
| Lamp posts | KayKit "Post Lantern"; Quaternius "Street Light" | CC0 | Poly Pizza `ZSQ65S4lEu`, `nFwrlcLvM5` |
| Trees, bushes and rocks (settlements and travel) | [Quaternius Stylized Nature MegaKit](https://quaternius.itch.io/stylized-nature-megakit), Standard tier | CC0 (itch) | Replaces the sphere-canopy trees |
| Town walls and gates | Quaternius Ultimate Fantasy RTS (Wall Towers, Castle Gate) | CC0 | **Not recommended**: these are RTS-scale miniatures, not walk-around scale. The current walls use the village kit and are fine. |
| Whole buildings | KayKit Medieval Hexagon | CC0 | **Not recommended**: hex-tile miniature scale |

### UI: CC BY 3.0, a Phase 18 fit

[game-icons.net](https://game-icons.net/about.html) has 4,000+ icons. They would give:
- the ten Part 2 discovery categories (Historical, Geographic, Cultural, Faction, Religious, Natural,
  Economic, Personal, Dangerous, Arcane) a distinct journal icon each;
- quest-state and reward markers.

Each icon's author is credited in CREDITS.md and `Credits.cs`. The icons download as PNG in any colour.

## Round 3 outcome: installed set (2026-11-12, owner approved; converted with the owner's Blender)

**Pipeline.** Poly Pizza serves GLB only. Rather than add a glTF package to Unity, each GLB was
converted with the owner's local **Blender 5.2.1**, run headless:
- the script is committed as `Tools/Blender/glb2fbx.py`;
- each model is uniformly scaled to a set real-world height (the `TARGET` table in the script);
- animations are baked with Blender's default curve simplification.

Spider, Deer and Well were re-imported to confirm the output: they come back at 1.10 m, 1.51 m and
2.40 m, with all animations present.

The two itch.io kits ship ready-made FBX and needed no conversion. They were fetched through itch's
"No thanks, just take me to the downloads" free path. Each kit's own `License_Standard.txt` says
CC0 1.0, and each is kept as the folder's `License.txt`.

### Final creature mapping (supersedes the round 2 table)

| Game creature | Model (folder) | Height | Animations | Licence |
|---|---|---|---|---|
| `wolf`, `dire_wolf` (scaled) | Wolf (`Quaternius/Creatures`) | 0.85 m | Attack, Death, Gallop, Walk, Idle, hit reacts (12) | CC0 |
| `mastiff` | Husky (`Quaternius/Creatures`) | 0.75 m | same set as the wolf (12) | CC0 |
| `riding_horse`, `draft_horse` | Horse (`Quaternius/Creatures`) | 2.1 m | Attack, Death, Gallop, Walk, hit reacts (13) | CC0 |
| `cattle` | Cow; Bull as a variant (`Quaternius/Creatures`) | 1.5 / 1.6 m | the same 13 | CC0 |
| `deer` | Deer; Stag as a variant (`Quaternius/Creatures`) | 1.5 / 1.8 m | the same 13 | CC0 |
| `fox` | Fox (`Quaternius/Creatures`) | 0.55 m | 12 | CC0 |
| `giant_spider` | Spider (`Quaternius/Creatures`) | 1.1 m | Attack, Death, Idle, Jump, Walk | CC0 |
| `giant_rat` | Rat (`Quaternius/Creatures`) | 0.45 m | Attack, Death, Idle, Jump, Run, Walk | CC0 |
| `goblin` (replaces the local-only Puglin) | Goblin (`Quaternius/Creatures`) | 1.0 m | Attack, Death, HitRecieve, Idle, Jump, Run, Walk | CC0 |
| `ogre`, `troll` | Giant (`Quaternius/Creatures`); Orc kept as an option | 3.0 / 1.9 m | 7 / 14 | CC0 |
| `chicken` | Chicken (`Quaternius/Creatures`) | 0.45 m | 9, including Death | CC0 |
| `sheep` | Sheep (`Quaternius/FarmAnimals`) | — | Idle, Jump | CC0 |
| `black_bear`, `brown_bear` (tinted) | BlackBear (`PolyByGoogle`) | 1.2 m | none (static) | **CC BY 3.0** |
| `boar` | Boar (`PolyByGoogle`) | 0.9 m | none (static) | **CC BY 3.0** |
| `goat` | Goat (`PolyByGoogle`) | 0.9 m | none (static) | **CC BY 3.0** |

Removed again before any commit, as superseded:
- Animal Pack Vol.2 (the Wolf and Dog with only idle and walk);
- the Farm Animals Horse, Cow and Pig. The real Boar model replaces the pig stand-in.

Still without a model: rabbit (the only Quaternius rabbit is chibi-style), plus the humanoid
bandit, guard and cultist, which already use the Quaternius people.

### Settlement, shop and place props

| Folder | Contents | Licence |
|---|---|---|
| `Quaternius/SettlementProps` | Well (2.4 m), Market Stalls (2.6 m), Market Stand (1.6 m), Street Light (3.2 m) | CC0 |
| `KayKit/PostLantern` | Post Lantern (2.6 m), from KayKit Halloween Bits | CC0 |
| `Quaternius/FantasyPropsMegaKit` | 94 FBX: `Stall_Empty`, `Stall_Cart_Empty`, Anvil, Workbench(es), Barrel(s), crates, Chest_Wood, Cauldron, potions, scrolls, books and bookcases, keys, coins, candles, chandelier, torches, `Lantern_Wall`, tables, chairs, beds, `WeaponStand`, Banner_1/2 and more, with 12 trim textures. The Unreal-only normal maps were skipped. | CC0 |
| `Quaternius/StylizedNatureMegaKit` | 68 FBX from the kit's "FBX (Unity)" export: common, dead, twisted and pine trees (5 each), bushes, ferns, flowers, grass, mushrooms, pebbles, rocks and rock paths, with 20 textures | CC0 |

**Repository size.** `Art/ThirdParty` grew from about 120 MB to about 284 MB. The biggest additions:
- the two MegaKits' textures, about 90 MB;
- the eight animated Ultimate-pack animals, about 6 MB each, because of their 12 or 13 baked clips.

**Trimmed (owner request, 2026-11-12).** `glb2fbx.py` now keeps only the clips the game drives (the `KEEP`
set): idle, walk, run or gallop, attack (headbutt and kick for the hoofed animals; punch and weapon for
the Orc), one hit reaction and death. It drops Eating, Gallop_Jump, Idle_2, the head-low idles,
Idle_HitReact_Right, the jumps, and the Orc's and Chicken's gesture clips. As a result:
- `Quaternius/Creatures` went from 57 MB to 33 MB;
- `Art/ThirdParty` went from about 284 MB to about **228 MB**.

The Wolf and Orc were re-verified after trimming, at 0.86 m with 6 clips and 2.0 m with 7 clips.
Nothing is git-ignored, and the repository does not use LFS.

**Not yet done.** Integration is Phase 18 work, after the preflight audit:
- register the models in `ArtSetup` and `ArtCatalog`;
- map them in `CombatantBodies.cs` and the settlement and place builders;
- import checks in Unity, such as facing direction, clip names (the takes are named like
  `AnimalArmature|…|Walk`) and material conversion to the project's pipeline;
- CREDITS.md and `Credits.cs` rows, with the **three CC BY lines mandatory**;
- screenshots.

## Suggested Phase 18 scope (original recommendation)

1. **KayKit Dungeon Remastered** for crypt, catacomb, underground and ruin interiors and mechanisms. This
   has the biggest visible effect on the Part 2 exploration work.
2. **Quaternius Ultimate Monsters**, only if it maps onto creatures already in the catalog (spider, rat,
   bear, wolf, goblin). Otherwise defer it.
3. Defer audio (Kenney RPG Audio and the music) to a phase that adds an audio system, because the game
   has no audio code today.
4. Defer the UI kits to Workstream A.

Each pack needs:
- a `License.txt` copied into its folder;
- a CREDITS.md row and a `Credits.cs` entry;
- a check that the import doesn't change `ProjectSettings/GraphicsSettings.asset`;
- size kept in check, by importing only the pieces used.
