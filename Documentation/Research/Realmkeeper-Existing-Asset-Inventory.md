# Realmkeeper: existing asset inventory (audit before research)

**Date:** 2026-11-12. **Source:** the repository at `8531143` on `feature/phase-0-1-foundation` (Phase 20 pushed; Phase 20A drafted, not yet committed). This was read from the files only; no scene or setting was changed.

## 1. What is in the project

| Pack (folder under `Assets/Realmkeeper/Art/ThirdParty/`) | Edition | Licence | Size | Contents (models) | Used by the game |
|---|---|---|---|---|---|
| Quaternius **Medieval Village MegaKit** | **Free** edition | CC0 | 24 MB | 73 modular pieces: plaster and uneven-brick walls (straight, door, window), wood and brick corners, round-tile and brick roofs (4×4 to 8×14), dormers, tower roof, doors, windows and shutters, balconies, stairs, floors (brick, wood), chimneys, vines, fence, wagon, crate, support beam | All registered in `RealmkeeperArtCatalog.asset`; used by `ModularEnvironmentBuilder` for every settlement building's shell |
| Quaternius **Fantasy Props MegaKit** | **Free (Standard)** edition | CC0 | 53 MB | 94 props: anvil, workbench (2), beds (2), bench, stool, chair, tables, cabinet, nightstand, bookcase, book groups, shelves (3), barrels (3), crates (3), chest, chandelier, candlesticks (3), candles, lantern, torch, cauldron, pots, bottles, potions (3), mug, chalice, cutlery, coins, keys, scrolls, rope, chain, banners, stalls (2), weapon stand, peg rack, dummy, bronze sword, axe and pickaxe, wooden shield, whetstone, farm crates, vases | All registered. Placed by `PlaceEnvironmentBuilder` (places: forge → Anvil, workbench, bed …) and in places' dressing |
| Quaternius **Stylized Nature MegaKit** | **Free (Standard)** edition | CC0 | 47 MB | 68: common, pine, twisted and dead trees (5 each), bushes, grasses (4), ferns, clover, flowers, mushrooms, plants, pebbles, rock paths, medium rocks | Registered; scattered outside settlements and on the wilderness |
| Quaternius **Universal Base Characters** + **Modular Character Outfits – Fantasy** | Free editions | CC0 | 7 + 24 MB | 2 bodies per frame, 2 heads, 8 hairstyles, eyebrows, beard; outfits: peasant and ranger (male, female) | Every person and the player; outfits recoloured by tint |
| Quaternius **Universal Animation Library** | Free edition | CC0 | 23 MB | Locomotion, idle, talk and work clips on the universal humanoid rig | `Locomotion.controller`, `PersonCombatant.overrideController` |
| Quaternius **Creatures** (14), **Farm Animals** (sheep), **Settlement Props** (well, stalls, stand, street light) | Poly Pizza / OGA copies | CC0 | 33 MB | Spider, rat, deer, stag, fox, goblin, giant, orc, bull, chicken, wolf, husky, horse, cow | Bestiary and livestock |
| Quaternius **Bestiary – Dungeon Monsters** | Free Standard | QAL 1.0 (no redistribution) | 22 MB | Imp, Puglin | **Local only** (git-ignored) |
| KayKit **Dungeon Remastered 1.0** | Free | CC0 | 9.6 MB | 208 OBJ pieces: dungeon walls and floors, banners (6 colours × 8 patterns), barrels, boxes, beds, bottles, candles, chairs, tables, shelves, torches, keys, coins, chests | Places (crypts, caves, mines) |
| KayKit **Skeletons** + **Character Animations 1.1** | Free | CC0 | 2.4 + 13 MB | 4 skeleton figures, weapons; Rig_Medium general, movement, melee, ranged, special sets | Undead combatants |
| KayKit **Post Lantern** | Free | CC0 | 70 KB | 1 | Street lighting |
| Poly by Google: black bear, boar, goat | — | **CC BY 3.0** | 2.6 MB | 3 | Bestiary (credited) |
| ambientCG materials | — | CC0 | 5.1 MB | Grass004, Ground037, Ground054, PavingStones070, Planks021, Wood049 (1K) | Terrain, roads, floors |
| game-icons.net | — | **CC BY 3.0** | 2.7 MB | 142 white silhouette icons | UI, inventory, map (Phase 19) |
| Kenney Fantasy UI Borders, Cartography | — | CC0 | 1.9 MB | 44 + 31 files | UI frames, atlas |
| EB Garamond, Alegreya Sans | — | OFL 1.1 | 3 MB | 2 families | UI text |
| Project's own | — | — | — | `Art/Materials` (bark, brick, body, canvas …), iris mask (Phase 20A, derived CC0) | Shared materials |

## 2. What is built from code primitives (no art asset)

| What | Where | Today | Gap |
|---|---|---|---|
| **Shop interiors** (counter, shelves, rack, forge, anvil) | `ShopInteriorView.Build` | 11 untextured boxes (`MetreMeshes.Box`) with flat materials; the Fantasy Props anvil and workbench are **not** used here | Largest visible gap: every shop interior is a few grey boxes |
| **Items and wares** (weapons, shields, hats, food, trade goods) | `ItemModels.Build` | 27 hand-built primitive models (boxes and lathes); 72 items, the rest drawn "like" another | Weapons and armour read as diagrams; 14 weapons share about 9 shapes; no armour model on a stand |
| Inventory icons | game-icons.net silhouettes | One icon per item kind, tinted | Consistent, but not item-specific (a dagger and a rapier look alike) |
| Weather | `ExplorationAtmosphere` | Exponential fog, a soft-shadowed sun, tonemapping; no rain, snow or clouds | Phase 21 scope |
| Sky | Unity's default procedural sky | — | No cloud layer |
| Audio | — | **No ambience or effect audio pack in the project** | Entire category missing |

## 3. Rendering
- **Pipeline.** URP (`Assets/Settings/PC_RPAsset`, `PC_Renderer`; mobile variants unused).
- **Post-processing.** Tonemapping, colour adjustments and bloom in `DefaultVolumeProfile`; the street volume is built in code.
- **Shadows and lighting.** Soft sun shadows to 90 m in streets; no baked lighting (worlds are generated at runtime, so lightmaps cannot be baked ahead).

**Consequence for any purchase:** assets must render well under runtime lighting, with no baked GI. Packs that depend on lightmaps for their look will disappoint.

## 4. Findings

1. **Free-only editions.** Every Quaternius pack in the project is the **free** edition. Quaternius sells larger editions of the same kits in the identical style:
   - Medieval Village MegaKit Pro;
   - Fantasy Props MegaKit Pro;
   - Stylized Nature MegaKit Pro;
   - the Ultimate packs.

   These are the cheapest route to more variety without a style break.
2. **Owned but unused where it matters.** The Fantasy Props anvil, workbenches, weapon stand, peg rack, shelves, barrels, crates and bottles are not used in shop interiors, which draw boxes instead. KayKit Dungeon Remastered has 208 pieces (beds, tables, shelves, bottles, candles, chests) usable in tavern and house interiors, but only places use them.
3. **Missing categories.**
   - Leatherworking and tanning (none);
   - tavern kitchen and food dressing (only a few props);
   - armour as models (none);
   - most weapons (code primitives);
   - textiles (rugs, curtains, tapestries, bedding variants);
   - audio (none);
   - weather VFX (none);
   - item-specific inventory icons.
4. **Art direction is set by what exists.** The world is **stylized low-poly**, flat-shaded with soft colour (Quaternius, KayKit). The reference games in the brief (Baldur's Gate 3, Kingdom Come 2, The Witcher 3) are photoreal or high-detail PBR.
   - Mixing realistic PBR furniture into Quaternius buildings would look wrong: different scale of detail, different texel density, different lighting response.
   - A realistic direction means replacing buildings, characters and props together. That is a large art pivot, not an asset purchase.
   - The research below evaluates both routes and says which assets belong to which.
5. **No duplicates to buy.** Anything recommended must add what the project lacks: interiors, shops, workshops, leather, armour, item models, icons, atmosphere and audio.
