# Realmkeeper: 3D asset research, sourcing and visual realism audit (master report)

**Date:** 2026-11-12. **Status:** research only. Nothing was downloaded, purchased, imported, integrated or changed in the project.

**Companion files (all in `Documentation/Research/`):**
- `Realmkeeper-Existing-Asset-Inventory.md` (the audit);
- `Realmkeeper-Asset-Shortlist.md`;
- `Realmkeeper-Asset-License-Registry.md`;
- `Realmkeeper-Asset-Integration-Roadmap.md`;
- `Realmkeeper-Asset-Rejected-Candidates.md`.

**How to read the evidence.** Every price, licence, pipeline and Unity-version fact below was read on the product's own page on 2026-11-12, unless marked:
- **(snapshot):** taken from a search-result snapshot and not confirmed on the page itself;
- **(unverified):** not confirmed at all.

Prices change and sales end, so re-check before buying.

**What could not be read.** Unity Asset Store pages load their description, "Package Content" and preview images by script. For those listings I could verify price, publisher, licence type, pipeline table, version, date and size, but **not the itemised contents or polycounts**. They are marked "contents unverified": look at the previews before approving.

## 1. Summary of the audit (detail in the inventory)

1. **The visual direction is already set.** Everything in the world is **stylized low-poly CC0** (Quaternius, KayKit), lit at runtime with no baked GI, because worlds are generated.
   - The reference games are realistic PBR, and their quality comes from assets *and* baked lighting, dense hand placement and photogrammetry.
   - Realmkeeper cannot reach Kingdom Come or The Witcher by buying realistic props: they would sit in stylized buildings beside stylized people and look pasted in.
   - **Recommendation: stay stylized, and raise density, variety, interiors and atmosphere** (section 3). A realistic pivot is a separate, large decision (section 3.3).
2. **The biggest visible gaps are not models we lack but primitives we draw.**
   - Shop interiors (`ShopInteriorView`) are 11 grey boxes.
   - 72 items have 27 primitive models (`ItemModels`), with no armour models.
   - Owned props (anvil, workbenches, weapon stand, shelves, barrels) are not used in shops.
3. **Owned for free but not imported.**
   - The **free** Medieval Village MegaKit has **170** pieces; the project imported 73.
   - The free Stylized Nature MegaKit has 81; the project has 68.
4. **Missing entirely:**
   - leatherworking and tanning;
   - armour models;
   - textiles (rugs, curtains, tapestries);
   - work animations (hammering, sitting, eating);
   - weather VFX;
   - audio (none at all).

## 2. Method and sources

| Source | Searched | Result |
|---|---|---|
| quaternius.com and quaternius.itch.io | Pack pages, editions, prices, licence page | Verified: CC0 for MegaKits and UAL2; QAL for Bestiary and Fantasy Races |
| kaylousberg.itch.io (KayKit) | Catalogue, Furniture, Restaurant, Fantasy Weapons Bits | Verified CC0, prices |
| kenney.nl | 3D category, Fantasy Town Kit, RPG Audio, licence (support page) | Verified CC0 |
| polyhaven.com | Licence, model categories | Verified CC0; realistic style |
| Unity Asset Store | About 20 product pages fetched; EULA (Appendix 1) | Verified prices, pipelines, versions; contents not readable |
| Fab (fab.com) | Standard Licence; one tannery listing | Licence verified; the listing rejected |
| Synty Store | One-time-purchase EULA; Fantasy Kingdom on the Asset Store | Verified |
| Kevin Iglesias (itch.io, Asset Store) | Villager/Crafting, Basic Motions | Asset Store editions verified; itch editions state no licence |
| Sonniss | GDC Game Audio Bundle page and FAQ | Verified royalty-free terms |
| CGTrader, Blender Market | Via search only (tavern kit, weapons, tanning racks) | Not verified on the page; no recommendation depends on them |
| Sketchfab, BlenderKit, OpenGameArt, ArtStation Marketplace, ambientCG (new materials) | **Not investigated in this pass** | Listed as follow-up. Sketchfab licences differ per model; never assume CC. |

**Counts:**
- **Sources.** 11 investigated: 9 read at a product or licence page; CGTrader and Blender Market only through search.
- **Products.** About 47 products and collections evaluated.
- **By licence** (detail in the registry):
  - **CC0:** 14;
  - **CC BY:** 0 new (the project already uses CC BY 3.0 icons and three animals);
  - **other permissive:** 2 (Quaternius QAL; the Sonniss GDC licence; Fab Standard was read but its one listing was rejected);
  - **free under the Asset Store EULA:** 2;
  - **paid:** 20 (8 of them prices from search snapshots only).
- **Rejected:** 11.

## 3. Art direction and how the reference games translate

### 3.1 What the references do that Realmkeeper can borrow (visual qualities, not their assets)

| Reference | What makes it read well | Realmkeeper requirement it implies |
|---|---|---|
| Baldur's Gate 3 | Interiors that are full: every table set, every shelf stocked; warm pools of point light against cool shadow | Furnish rooms to **density targets** (props per m²), set dressing per room kind, point lights from candles and hearths |
| Kingdom Come: Deliverance 2 | Workshops that show their trade: the smithy's forge glow, quench tub, finished goods hung for sale; clutter on floors | **Trade-specific prop sets** for each shop kind; "work in progress" props (hide on a frame, blade on the anvil) |
| The Witcher 3 | Weather and time of day change everything: wet surfaces, wind in trees, fog in valleys | Weather visuals driven by the simulation (Phase 21): rain and snow particles, wind on vegetation, fog density by region |
| Skyrim | Readable item silhouettes; every item a distinct model; shops display their stock | **One model per item kind**; displays that hold real stock (already the rule in `ShopInteriorView`) |
| Red Dead Redemption 2 | Interior lighting through windows; dust in light; people doing work | Window light and ambient dust (cheap particles); **work animations** at stations |
| Dragon Age: Inquisition | Strong silhouettes of buildings with varied roofs | Use the 97 unimported free village pieces and the Pro kit's roof variety |
| Assassin's Creed | Market streets busy with awnings, carts, hanging goods | Market stall dressing sets; awnings, crates, hanging cloth and produce |

### 3.2 Recommended direction: stylized, dense, lit

**Keep the Quaternius/KayKit language: soft-shaded low-poly with gradient or simple textures.** Raise quality by:
- **variety:** the Pro editions of the kits we use;
- **density:** furnished interiors and trade sets;
- **specificity:** item models, leather and smithy sets;
- **atmosphere:** weather VFX, point lights, audio.

Polytope Studio's "Lowpoly Medieval Fantasy Series" is the paid line closest to this look. Synty's POLYGON is low-poly too but uses its own palette and chunkier proportions, so it needs normalisation (section 9).

### 3.3 The alternative, a realistic pivot: not recommended now
- **What it would take.** Replacing:
  - the buildings (the modular kit);
  - every character, with new rigs, outfits and heads;
  - all props.

  It would also need PBR texture streaming, a runtime GI substitute (APV, or SSAO plus reflection probes), and far higher memory.
- **Free CC0 realistic sources exist** (Poly Haven, ambientCG), but their models are mostly modern objects.
- **Medieval realistic kits** are mostly Unreal-first (Fab) or built-in-pipeline.

**Estimate:** a multi-phase art programme, not an acquisition. Recorded here so the owner can decide deliberately.

## 4. Candidates by category

**Keys:**
- **CC0 / QAL / EULA** = licence (see the registry).
- **U6** = a Unity 6000.x row present in the store's compatibility table.
- **URP ✓** = URP listed as compatible.

### 4.1 Architecture and buildings

| # | Product | Source | Price (USD) | Licence | Tech | Assessment |
|---|---|---|---|---|---|---|
| A1 | **Quaternius Medieval Village MegaKit, Standard (free)**: the **97 pieces not yet imported** | [itch.io](https://quaternius.itch.io/medieval-village-megakit), [page](https://quaternius.com/packs/medievalvillagemegakit.html) | Free | CC0 | FBX, OBJ, glTF; same as current | **Already effectively owned.** Same style and scale as every building today. Import the rest (owner approval to download). |
| A2 | **Quaternius Medieval Village MegaKit, Pro** | same | **$9.99+** (name your price, minimum) | CC0 | 300+ models, FBX, OBJ, glTF | Best value per visible change: roughly 4× the pieces the game uses, identical style, CC0 (commit-safe). |
| A3 | Polytope **Lowpoly Modular Village** | [Asset Store](https://assetstore.unity.com/packages/3d/environments/lowpoly-modular-village-medieval-fantasy-series-258073) | $59.99 | Asset Store EULA (Single Entity) | Built-in and URP ✓ (2021.3.40); 737 MB; v2.0.1, 2025-01-03 | Explorable buildings with interiors (339 parts, 32 buildings, per the publisher's ArtStation, snapshot). Strong, but a second building language beside Quaternius. Choose one kit per settlement. |
| A4 | Kenney **Fantasy Town Kit 2.0** | [kenney.nl](https://kenney.nl/assets/fantasy-town-kit) | Free | CC0 | 160 files | Simpler, toy-like; useful for walls and gates on the strategic map, not street level |
| A5 | Synty **POLYGON Fantasy Kingdom** | [Asset Store](https://assetstore.unity.com/packages/3d/environments/fantasy/polygon-fantasy-kingdom-164532) | $349.99 | Asset Store EULA, **"Restricted", Extension Asset** | 265.6 MB; v1.12.5, 2026-08-16; original 2022.3.56 | Large and polished, but expensive. The "Restricted / Extension Asset" label means per-seat terms, so check before buying. Its style differs from Quaternius. **Not first choice.** |

### 4.2 Interior furniture and furnishing

| # | Product | Source | Price | Licence | Tech | Assessment |
|---|---|---|---|---|---|---|
| F1 | **Already owned:** Fantasy Props MegaKit (free, 94) and KayKit Dungeon Remastered (208) | in project | — | CC0 | — | Beds, tables, chairs, cabinet, shelves, bookcase, barrels, crates, chests, candles and chandelier are **owned and unused in shops and houses**. First step: use them. |
| F2 | **Quaternius Fantasy Props MegaKit, Pro** | [itch.io](https://quaternius.itch.io/fantasy-props-megakit) | **$9.99+** | CC0 | 200+ props, worn texture variants, simple collisions; FBX, OBJ, glTF | Doubles prop variety in the house style; the worn variants help repetition. **Top value.** |
| F3 | Aletheia **Low Poly Medieval Interior Pack** | [Asset Store](https://assetstore.unity.com/packages/3d/props/low-poly-medieval-interior-pack-392096) | Free | Asset Store EULA | Built-in, URP and HDRP ✓; U6 (6000.5.0); 740 KB; 2026-07-28 | Tiny, contents unverified; free to evaluate |
| F4 | KayKit **Furniture Bits** | [itch.io](https://kaylousberg.itch.io/furniture-bits) | Free / Extra $3.95+ | CC0 | 50+ models; 1024² gradient atlas | Partly modern (desks, monitors). Beds and tables usable; the gradient-atlas style is KayKit's, close to the dungeon pack. Pick items, not the whole pack. |

### 4.3 Taverns, inns and kitchens

| # | Product | Source | Price | Licence | Tech | Assessment |
|---|---|---|---|---|---|---|
| T1 | Daniel Mistage **STYLIZED Fantasy Tavern & Kitchen** | [Asset Store](https://assetstore.unity.com/packages/3d/props/stylized-fantasy-tavern-kitchen-low-poly-3d-art-256440) | **$55** | Asset Store EULA (Single Entity) | Built-in, URP and HDRP ✓; **U6 (6000.0.44f1)**; 54.6 MB; v1.03, 2026-08-19 | The best-supported tavern set found; contents unverified, so check previews. The same publisher's free Armory (W3) lets the style be judged first. |
| T2 | Adrift Team **Stylized Tavern Interior Medieval Fantasy** | [Asset Store](https://assetstore.unity.com/packages/3d/props/interior/stylized-tavern-interior-medieval-fantasy-350942) | $15 | Asset Store EULA | **URP only**, U6 (6000.0.28); 17.6 MB; 2026-01-08 | Cheap and URP-native, but new with no ratings; contents unverified. Its "PBR" tag may mean a more textured look. |
| T3 | Quaternius **Ultimate RPG Pack** (2019) | [page](https://quaternius.com/packs/ultimaterpg.html) | Free | CC0 | 106 models; FBX, OBJ, Blend | Older, flat-coloured; tankards, food and props usable as filler |
| T4 | KayKit **Restaurant Bits** | [itch.io](https://kaylousberg.itch.io/restaurant-bits) | Free | CC0 | 140+ | **Rejected for taverns** (modern kitchen; see the rejected list) |

### 4.4 Blacksmith, forge and metalworking

| # | Product | Source | Price | Licence | Tech | Assessment |
|---|---|---|---|---|---|---|
| B1 | **Owned:** Fantasy Props anvil, Anvil_Log, Workbench, WeaponStand, Peg_Rack, Whetstone, Bucket, Chain, Pickaxe | in project | — | CC0 | — | Enough for a recognisable smithy **today**, if `ShopInteriorView` used them instead of boxes |
| B2 | TitanForge **LOW POLY – Blacksmithing Pack** | [Asset Store](https://assetstore.unity.com/packages/3d/props/low-poly-blacksmithing-pack-163654) | $39.99 | Asset Store EULA | Original 2018.3.14; v1.1, 2022-02-14; 8.5 MB; **pipeline not stated on the page** | Covers furnace, bellows and grindstone (per search; contents unverified). Old, and URP unconfirmed. Ask the publisher, or import and convert materials. |
| B3 | Fab **Stylized Artisan Forge Kit** | (Fab, search only) | (unverified) | Fab Standard | Built-in/URP per the search snippet | **Unverified: do not use** until the page is read |
| B4 | Fire, sparks and smoke | — | — | — | — | Build with Unity particles (no asset needed); see 4.10 |

### 4.5 Leatherworking and tanning (the scarcest category)

| # | Product | Source | Price | Licence | Tech | Assessment |
|---|---|---|---|---|---|---|
| L1 | PolySoft3D **Leatherworking** | [Asset Store](https://assetstore.unity.com/packages/3d/props/leatherworking-205982) | $20 | Asset Store EULA | Built-in, URP and HDRP ✓ (2019.4.15f1); **518 MB**; v1.0, 2021-12-02 | The only Unity leatherworking pack found. Tags say "lowpoly, PBR", but at 518 MB the textures are large: likely 4K PBR, a **style risk** beside Quaternius. Contents unverified. Look at previews; may need texture down-scaling and re-tinting. |
| L2 | Owned stand-ins | in project | — | CC0 | — | Workbench, Peg_Rack, Bag, Pouch_Large, Rope, Barrel (as a tanning tub), Table_Knife, plus a hide quad (a flat textured mesh made by the project) |
| L3 | Self-made hide and frame set | — | Time | Project-owned | — | A drying frame, a stretched hide and a scraping beam are simple shapes. Modelling 5–8 pieces in the Quaternius style in Blender is likely the most coherent result; recorded as an option, not done. |

Rejected here: the Fab "Medieval Leather Tanning Rack" (AI-generated; licence field empty).

### 4.6 Weapons, armour and equipment

| # | Product | Source | Price | Licence | Tech | Assessment |
|---|---|---|---|---|---|---|
| W1 | Polytope **Low Poly Medieval Weapons** | [Asset Store](https://assetstore.unity.com/packages/3d/props/weapons/lowpoly-medieval-weapons-pack-162437) | $39.99 | Asset Store EULA | v3.0.1, 2025-01-04; 136 MB; original 2021.3.40; "URP" in keywords | Snapshot says 259 items (weapons and equipment). Covers the item list's 14 weapons, 2 shields and helmets. **Strong match for `ItemModels`.** |
| W2 | KayKit **Fantasy Weapons Bits** | [itch.io](https://kaylousberg.itch.io/fantasy-weapons-bits) | Free / Extra $4.99+ | CC0 | 25+ (free), +15 (Extra); 1024² atlas | Swords, axes, hammers, bows, staves, shields, spears. Chunky KayKit proportions; free and commit-safe. Good stopgap. |
| W3 | Daniel Mistage **STYLIZED Fantasy Armory** | [Asset Store](https://assetstore.unity.com/packages/3d/environments/fantasy/stylized-fantasy-armory-low-poly-3d-art-249203) | **Free** | Asset Store EULA | 44.3 MB; v1.25, 2026-11-11; original 2022.3.62; pipeline not stated | Free armory environment (230+ models, snapshot). Racks, stands and weapons; also a style test for T1. |
| W4 | Quaternius **Modular Weapons Pack** (2018) | [page](https://quaternius.com/packs/medievalweapons.html) | Free | CC0 | 24, **untextured** | Flat colour; weaker than W1/W2 |
| W5 | Armour as worn models | Polytope modular characters (C2) | — | — | — | No standalone stylized armour-on-stand pack was found. Armour for display is best taken from a modular character set (C2), or the outfit meshes re-posed on a stand. |

**Mapping to the item system.** The 14 weapons, 2 shields, 3 headwear and 8 armour items (`ItemCatalog*.cs`) each need one model. `ItemDefinition.Visual` already supports `like:` redirection, so a pack can be mapped item by item without code changes beyond `ItemModels` loading a prefab where one exists.

### 4.7 General items, loot and inventory icons

| # | Product | Source | Price | Licence | Assessment |
|---|---|---|---|---|---|
| I1 | **Owned:** Fantasy Props potions, bottles, books, scrolls, keys, coins, pouches, food crates, mug, chalice, cutlery | in project | — | CC0 | Covers most non-weapon items (potions, keys, letters, coins, food) |
| I2 | **Render our own icons** from the item models | — | Time | Project-owned | **Recommended.** The capture code already renders off-screen (`Shoot` in the PlayMode tests). An editor step rendering each item model to a 128² transparent PNG gives item-specific icons that match the models. It can be tinted to the Phase 19 palette or kept in colour. |
| I3 | Medieval Fantasy Icons Pack | [Asset Store](https://marketplace.unity.com/packages/2d/gui/icons/medievalfantasyiconspack-314933) | $15.99 (snapshot) | EULA (snapshot) | Painted icons would clash with the game-icons silhouette system (Phase 19 design). Not recommended. |
| I4 | Fantasy Icons Megapack | [Asset Store](https://marketplace.unity.com/packages/2d/gui/icons/fantasy-icons-megapack-97000) | $99 (snapshot) | EULA | Same concern; large |

### 4.8 Markets and streets

| # | Product | Assessment |
|---|---|---|
| M1 | Owned: Quaternius Settlement Props (MarketStalls, MarketStand, Well, StreetLight), Fantasy Props Stall_Empty, Stall_Cart_Empty, FarmCrates, Barrel_Apples, Banners; KayKit banners (48 variants) | Enough for dressed markets once placement rules exist (Phase 21) |
| M2 | Medieval Village MegaKit Pro (A2) and Fantasy Props Pro (F2) | More stalls, carts, awnings (contents per Pro page: "market stalls" named) |

### 4.9 Nature and terrain

| # | Product | Source | Price | Licence | Assessment |
|---|---|---|---|---|---|
| N1 | **Stylized Nature MegaKit**: 13 free pieces not imported (81 vs 68) | [itch.io](https://quaternius.itch.io/stylized-nature-megakit) | Free | CC0 | Import the rest |
| N2 | **Stylized Nature MegaKit, Pro** (110 models) | same | $9.99+ | CC0 | Seasonal and regional variety in the same style |
| N3 | Polytope **Low Poly Environment – Nature** | [Asset Store](https://assetstore.unity.com/packages/3d/environments/lowpoly-environment-nature-medieval-fantasy-series-185195) | $69.99 (snapshot) | EULA | Only if the Polytope line is adopted |
| N4 | Terrain materials: ambientCG (CC0, already used) | [ambientcg.com](https://ambientcg.com) | Free | CC0 | More ground sets (mud, snow, gravel) for regions and seasons; not yet looked up individually |

### 4.10 Weather, atmosphere and lighting

| # | Product | Source | Price | Licence | Tech | Assessment |
|---|---|---|---|---|---|---|
| E1 | Distant Lands **COZY: Stylized Weather 3** | [Asset Store](https://assetstore.unity.com/packages/tools/utilities/cozy-stylized-weather-3-271742) | $50 | EULA, Extension Asset (per seat) | Built-in, URP and HDRP ✓ on **2022.3.62f1 only, no U6 row**; v3.6.23, 2026-09-04; 363 MB | Stylized look that matches. **Risk:** it is a whole weather simulation, while Realmkeeper's weather must be deterministic and simulation-owned (Phase 21). Use only as a renderer driven by our state, and confirm Unity 6 first. |
| E2 | Hendrik Haupt **Enviro 3 – Sky and Weather** | [Asset Store](https://assetstore.unity.com/packages/tools/particles-effects/enviro-3-sky-and-weather-236601) | $80 | EULA, Extension Asset | All three pipelines ✓ on **2020.3.48f1 only, no U6 row**; v3.3.2b, 2026-03-26 | Volumetric and realistic-leaning; heavier; same ownership concern. Deferred. |
| E3 | **Build it**: URP particles (rain, snow, sparks, smoke, dust), the procedural sky, fog by region, wind via shader | — | Time | Project-owned | **Recommended for Phase 21.** The simulation already owns weather; the visuals are a modest particle and shader job, and it keeps determinism and saves under our control. |

### 4.11 Characters, clothing and animation

| # | Product | Source | Price | Licence | Tech | Assessment |
|---|---|---|---|---|---|---|
| C1 | Quaternius **Characters – Fantasy Races** (new, Nov 2026) | [itch.io](https://quaternius.itch.io/characters-fantasy-races), [page](https://quaternius.com/packs/charactersfantasyraces.html) | Name your price; a $20+ tier is mentioned (unverified which files) | **QAL 1.0**: commercial use, no credit, **no redistribution of the asset files** | 12 models (6 male, 6 female), 3 colours each, size and muscle morph; humanoid rig; UAL compatible | **The most important find for Phase 20's open anatomy gap**: the same author and style as the bodies in use. **Races not named on the page**: check the files. QAL means **local-only** (git-ignored, like the Bestiary) unless the repository's privacy rules out "redistribution"; see section 7. |
| C2 | Polytope **Low Poly Fantasy Races** | [Asset Store](https://assetstore.unity.com/packages/3d/characters/lowpoly-fantasy-races-medieval-fantasy-series-278418) | $69.99 | EULA | **U6 URP ✓ (6000.3.12f1)**; 16.5 MB; 2026-08-23 | Verified Unity 6 URP. Races named only in tags (orc, goblin, troll, dwarf …); rig unstated. A different style from the Quaternius bodies, so it would need the Polytope characters throughout. |
| C3 | Quaternius **Universal Animation Library 2** | [itch.io](https://quaternius.itch.io/universal-animation-library-2) | Free (42) / Source $14.99+ (110+) | CC0 | Universal humanoid rig (the same family as UAL 1, in use) | Carrying, harvesting, planting, melee combos. **Free and same rig: take it.** |
| C4 | Kevin Iglesias **Human Crafting Animations** (Villager) | [Asset Store](https://assetstore.unity.com/packages/3d/animations/villager-animations-147875) | $25 | EULA | **U6 ✓ (6000.0.59f2)**; 49.9 MB; v2.2.1, 2025-11-25 | **Hammering (anvil, table, wall), carrying, chopping, mining, skinning, gathering** (itch listing of the same pack). Humanoid retarget per the author. **Fills the work-animation gap at stations.** Buy the Asset Store edition, not itch (the itch page states no licence). |
| C5 | Kevin Iglesias **Human Basic Motions** | [Asset Store](https://assetstore.unity.com/packages/3d/animations/human-basic-motions-157744) | $18 | EULA | U6 ✓; 51.3 MB; 2026-04-30 | Social clips (talking, waving, cheering; snapshot). **Sitting, eating and drinking are not confirmed.** |
| C6 | Mixamo | — | — | Adobe terms | — | Rejected in Phase 20 (not CC; redistribution unclear) |

### 4.12 Audio

| # | Product | Source | Price | Licence | Assessment |
|---|---|---|---|---|---|
| S1 | Sonniss **GDC Game Audio Bundles** (2015–2024, 9–48 GB each) | [sonniss.com/gameaudiogdc](https://sonniss.com/gameaudiogdc) | Free | Sonniss GDC licence: royalty-free, commercial, **no attribution**, unlimited projects; **no redistribution as sound effects**; no AI training | Enough rain, wind, fire, forge, crowd, footstep and door material for the whole game. Store only the chosen, edited files in the project (as part of the game, which is permitted). |
| S2 | Kenney **RPG Audio** (50 files) | [kenney.nl](https://kenney.nl/assets/rpg-audio) | Free | CC0 | Foley, footsteps, weapons; commit-safe |

### 4.13 Unity tools and systems

| Need | Recommendation | Why |
|---|---|---|
| GPU instancing and many props | **Unity 6 built-ins**: GPU Resident Drawer (URP Forward+), SRP Batcher, LOD Groups | No purchase or dependency; verify on the PC renderer before adding a tool |
| Weather | E3 (build) or E1 as a renderer | See 4.10 |
| Icons | Own render step (I2) | Matches the models; no licence |
| Interiors | Own generator (`InteriorGenerator`, Phase 21) fed by prop sets | Interiors are generated per building; a third-party interior tool would duplicate it |
| Modular building tools | None | `ModularEnvironmentBuilder` already assembles the Quaternius kit |
| Mesh LOD | Unity 6 Mesh LOD, or simple manual LODs | Low-poly assets rarely need it |

## 5. Combinations

| Combination | Assets (owned → free → paid) | Notes |
|---|---|---|
| **Blacksmith** | Owned anvil, workbench, weapon stand, peg rack, whetstone, barrel → Fantasy Props Pro (worn variants) → W1 weapons on racks → C4 hammering → URP fire and sparks particles → Sonniss forge audio | All one style except W1 (Polytope; close) |
| **Leatherworker** | Owned workbench, peg rack, bag, pouch, rope, barrel → self-made hide frame and stretched hide (L3) → L1 only if the previews match → C4 skinning clip | The scarcest; plan for a little custom modelling |
| **Tavern** | Owned tables, chairs, benches, mugs, barrels, chandelier, KayKit bottles and candles → T1 → Ultimate RPG food → C5 social and talking → Sonniss tavern ambience | Sitting and eating clips still unconfirmed |
| **Merchant** | Owned stalls, shelves, crates → Fantasy Props Pro → rendered icons (I2) | Shelves show real stock (existing rule) |
| **Residential** | Owned beds, nightstand, cabinet, bookcase, candles → Fantasy Props Pro → KayKit Furniture Bits (selected) → T1 kitchen pieces | |
| **Outdoor** | Nature MegaKit (complete, then Pro) → ambientCG ground sets → E3 weather → Sonniss and Kenney ambience | |

**Normalisation needed when mixing:**
- Polytope or Synty with Quaternius: match texel density, saturation and roughness, using one shared lit material per palette.
- KayKit gradient atlases sit well with Quaternius (already mixed in places).
- Avoid 4K PBR packs (L1, the realistic Fab items) unless re-textured.

## 6. Price bands (paid candidates)

| Band | Products |
|---|---|
| Under $20 | Quaternius Pro editions **$9.99+ each** (A2, F2, N2); UAL2 Source $14.99+; KayKit Extras $3.95–$7.49; T2 $15; Human Basic Motions $18; icon pack $15.99 (snapshot) |
| $20–$50 | L1 Leatherworking $20; C4 Crafting Animations $25; B2 Blacksmithing $39.99; W1 Weapons $39.99; Polytope Tools & Props $39.99 (snapshot); E1 COZY $50 |
| $50–$100 | T1 Tavern & Kitchen $55; A3 Modular Village $59.99; C2 Fantasy Races $69.99; N3 Nature $69.99 (snapshot); E2 Enviro 3 $80; Icons Megapack $99 (snapshot) |
| $100–$200 | Polytope Environment Bundle $109.99 (snapshot); Lite Bundle $149.99 (snapshot, on sale from $299.99); NPCs Bundle $189.99 (snapshot) |
| $200+ | Synty Fantasy Kingdom $349.99; Polytope Complete Bundle $599.99 (snapshot) |

**Best value:**
- The three Quaternius Pro editions (about $30 for roughly 4× the variety, CC0, commit-safe);
- Human Crafting Animations ($25);
- Polytope Weapons ($39.99).

**Largest visible change:** using owned props in shops and houses (no cost), then Fantasy Props Pro and Medieval Village Pro.

## 7. Licence concerns
1. **Repository visibility decides which licences can be committed.** The repository is on GitHub (`origin`); its visibility was not checked: `gh` is not installed here, and fetching it would send the owner's details to a service.
   - **CC0** may be committed anywhere.
   - **Asset Store EULA, Fab, Synty and QAL** assets must **not** be published in source form. In a **public** repository they must be git-ignored and kept local, as the Bestiary already is.
   - In a **private** repository the Asset Store EULA still requires each person using them to hold a licence.
2. **Seat terms.**
   - Many Asset Store listings show "Extension Asset" (per seat, 2 computers per seat, §2.3.2).
   - Synty's own store licence is 5 seats per purchase.
3. **No CC BY-SA or non-commercial asset is recommended.**
4. **Contents unverified.** Asset Store contents could not be read; judge from previews before purchase.
5. **Never take a Sketchfab or itch.io asset without the licence on its page.** Kevin Iglesias's itch pages state none; the Asset Store editions carry the EULA.

## 8. Technical concerns
- **Unity 6 not listed** for COZY, Enviro, TitanForge, PolySoft3D Leatherworking and Polytope Modular Village (2021.3). Most low-poly packs import fine, but materials may need converting to URP/Lit.
- **Large packs.** L1 (518 MB) and A3 (737 MB) bring big textures: review import settings and max sizes.
- **Rigs.** C1 and C3 share the Quaternius universal humanoid. C2 and C4 need Mecanim humanoid retargeting onto it (avatars exist: `FallbackAvatar`). Test one clip before buying more.
- **Runtime generation.** No baked lighting is possible: prefer assets whose look survives real-time lighting (low-poly gradient textures do).

## 9. Top lists

**Top 10 by overall value:**
1. Use the **owned** props in shops and houses (no cost). This is the largest visible gain.
2. Medieval Village MegaKit: import the 97 free pieces, then **Pro** ($9.99+).
3. Fantasy Props MegaKit **Pro** ($9.99+).
4. Quaternius **Characters – Fantasy Races** (QAL; races to confirm).
5. Quaternius **UAL 2** (free, CC0).
6. Kevin Iglesias **Human Crafting Animations** ($25).
7. Polytope **Low Poly Medieval Weapons** ($39.99).
8. **Sonniss GDC bundles** plus **Kenney RPG Audio** (free).
9. Daniel Mistage **Tavern & Kitchen** ($55), after the free Armory style test.
10. Stylized Nature MegaKit: the 13 free pieces, then **Pro** ($9.99+).

**Top 5 free:**
1. The unimported free pieces of the Village (97) and Nature (13) kits;
2. UAL 2;
3. Sonniss GDC bundles;
4. KayKit Fantasy Weapons Bits;
5. Kenney RPG Audio (with the Daniel Mistage Armory as a free style test).

**Top 5 paid:**
1. Medieval Village Pro;
2. Fantasy Props Pro;
3. Human Crafting Animations;
4. Polytope Weapons;
5. Tavern & Kitchen.

**Top 5 environment solutions (interiors, shops, workshops, buildings):**
1. Owned props, wired into `ShopInteriorView` and the Phase 21 interiors;
2. Fantasy Props Pro;
3. Medieval Village Pro;
4. Tavern & Kitchen;
5. Polytope Modular Village (if a second building language is wanted).

**Top 5 specialized:**
1. Polytope Weapons;
2. Crafting Animations;
3. rendered item icons (own);
4. weather built on URP particles (own), or COZY as a renderer;
5. Fantasy Races (anatomy).

## 10. Acquisition plan

| Group | Items |
|---|---|
| **Already owned** | Fantasy Props (94), Village (73), Nature (68), KayKit Dungeon (208), Settlement Props, Universal Base Characters, Outfits, UAL 1, Creatures; ambientCG; game-icons |
| **Free and licence-verified: needs owner approval to download into the project** | The Village MegaKit's free remainder; the Nature MegaKit's free remainder; UAL 2 (free tier); KayKit Fantasy Weapons Bits; Kenney RPG Audio; Kenney Fantasy Town Kit; Sonniss GDC (selected files only); Daniel Mistage Armory (EULA; local-only if the repository is public); Aletheia Interior Pack (EULA) |
| **Needs owner approval (licence decision)** | Quaternius Fantasy Races (QAL: local-only or private repository); any Asset Store item in a public repository |
| **Needs purchase** | Village Pro, Props Pro, Nature Pro (about $9.99 each, minimum); Crafting Animations $25; Polytope Weapons $39.99; Tavern & Kitchen $55; optional Basic Motions $18, COZY $50 |
| **Unsuitable** | See `Realmkeeper-Asset-Rejected-Candidates.md` |

## 11. Visual reference board
No images are embedded: store preview images are the publishers' property and are not CC-licensed. Each product link above opens its previews. By category:

| Category | Product links |
|---|---|
| Interiors and residences | F2, F3, F4, A3 |
| Taverns and inns | T1, T2 |
| Blacksmiths | B2, W3 |
| Leatherworkers | L1 |
| Markets | A2, F2 (market stalls) |
| Weapons and armour | W1, W2, W3 |
| Items | F2 |
| Nature | N2, N3 |
| Weather | E1, E2 |
| Characters | C1, C2 |
| Animation | C3, C4, C5 |
| Audio | S1, S2 |

What each is valuable for is in its row's assessment.

## 12. Facts and assumptions

**Verified on the product or licence page:** prices, licence names, pipeline tables, versions, dates and sizes, wherever not marked.

**Assumed:**
- **Asset Store contents:** from tags and search snippets, marked.
- **Style fit:** judged from descriptions, not imports.
- **The Pro editions' extra pieces** suiting the game.
- **Fantasy Races' races:** the page doesn't name them.

## 13. After the research: what was downloaded and integrated (2026-11-12, at the owner's instruction)

**Owner decisions:** download and integrate the free assets; **weather stays inside Unity** (option E3: URP particles, fog and sky driven by the simulation; no COZY or Enviro purchase).

**Corrections found on download:**
- The Stylized Nature MegaKit's free edition was already fully imported (68 of 68 models; the store's "81" counts other files). There was no remainder.
- The Village MegaKit's free edition has 176 models, so 103 were unimported, not 97.
- Characters – Fantasy Races (free edition) contains **Elf and Orc**, male and female, as unclothed base bodies with their own heads. The other races (dragon-kin, ape-kin, sea-kin) are in the paid Source edition.

| Asset | Downloaded | Integrated |
|---|---|---|
| Village MegaKit remainder (103) | Already on this machine | In the art catalog as building modules, with the kit's materials |
| KayKit Fantasy Weapons Bits (31) | Yes (3.2 MB) | Catalogued as `weapon_*`; **15 items now use them** in shops and in hands (`ItemModels.ArtModels`); 12 more items use owned Fantasy Props (keys, potions, scrolls, books, bag) |
| Kenney RPG Audio (50) | Yes (1 MB) | The game's first sounds: footsteps, door, coins, pages (`GameSounds`), with a volume setting |
| Quaternius UAL 2 (42 clips) | Yes (17 MB) | Imported on the humanoid rig; clips available. Driving them from NPC work is Phase 21 (NPC work activity) |
| Kenney Fantasy Town Kit (167) | Yes (3.8 MB) | Catalogued as `town_*`; not yet placed anywhere |
| Quaternius Fantasy Races (Elf, Orc) | Yes (93 MB) | Local-only folder (git-ignored). **Not wired to species:** the bodies are unclothed and the matching outfits are not released yet (the author says the outfit pack "will be updated to work with these") |

**Not downloaded:**
- **Sonniss GDC bundles:** 9–48 GB each, and choosing files needs listening. Deferred until an ambience system exists (Phase 21).
- **Daniel Mistage Armory and Aletheia Interior Pack** (free on the Unity Asset Store): they need a signed-in Unity account, which only the owner can use.
