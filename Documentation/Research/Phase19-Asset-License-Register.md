# Phase 19A: Asset licence register

**What this is.** Every candidate examined in Phase 19A, with:
- the licence evidence, read from the creator's own page or download (2026-11-12);
- a recommendation: **Use**, **Investigate** or **Reject**.

**How to read it.**
- "Verified" means the licence text was read at the source named in the row, or in the downloaded licence file.
- Where a licence could not be read (pages blocked to automated fetching, or no licence stated), the row says **Unverified**. An unverified asset is excluded from the approved recommendations.

**Approval rules (owner).**
- **CC0** is approved.
- **CC BY** is approved, with attribution preserved.
- **CC BY-SA, OFL, MIT/ISC and custom terms** are not pre-approved. They need a separate owner decision and are listed as such, never as CC0 or CC BY.

## Summary

| | Count |
|---|---|
| Candidates examined (rows below) | 46 |
| Licence verified CC0 | 14 |
| Licence verified CC BY | 3 (game-icons.net CC BY 3.0, Wuggy CC BY 4.0, Lamoot CC BY 3.0) |
| Licence verified, other permissive (OFL fonts, ISC/MIT, custom) | 11 |
| Licence unverified, conflicting, or non-commercial (the remainder) | 18 |
| **Use** / Investigate / Owner decision / Reject | **6** / 9 / 11 / 20 |

Licence status and recommendation are separate. Several verified-CC0 packs are rejected on style or resolution.

## Recommended (Use)

### 1. game-icons.net: the primary icon family

| Field | Value |
|---|---|
| Creator | 36 contributors, chiefly Lorc (1,429 icons) and Delapouite (2,022 icons); about 4,180 icons in all |
| Source | https://game-icons.net (about page: https://game-icons.net/about.html) |
| Repository | https://github.com/game-icons/icons (SVG, one folder per author) |
| Licence | **CC BY 3.0** (some contributors CC0) |
| Evidence | About page: "provided under the terms of the Creative Commons 3.0 BY license", with a link to CC BY 3.0. The repository's `license.txt` says CC BY 3.0 or CC0 depending on contributor, with one subfolder per contributor. |
| Commercial use | Yes (CC BY 3.0) |
| Modification | Yes (CC BY 3.0); the site itself offers recolouring |
| Attribution | Credit each author. The site suggests "Icons made by {author}. Available on https://game-icons.net". In a video game, the credit must be reachable from a menu (FAQ). Realmkeeper already has an in-game Credits section (`Credits.cs`) and `CREDITS.md`. |
| Redistribution | Allowed under CC BY 3.0 with attribution and the licence link |
| Formats, size | SVG per icon. The site also renders PNG per icon at 512×512 with chosen colours, e.g. `https://game-icons.net/icons/ffffff/transparent/1x1/lorc/crossed-swords.png` (verified: 200, image/png, about 8 KB). |
| Unity | Unity 6.0 cannot import SVG for UI Toolkit without the preview Vector Graphics package (built-in SVG arrived in 6.3). So use white-on-transparent PNGs, downscaled to 128 px and tinted with `-unity-background-image-tint-color`, and keep the SVG sources beside them. |
| URP / Unity 6 | Yes (plain textures) |
| Shipped commercial game | Yes, with per-author credit |
| **Recommendation** | **Use.** It is one coherent silhouette style that covers every category Realmkeeper needs: weapons, armour, spells, conditions, factions, buildings, travel, and settings actions (cog, padlock, hourglass, magnifying glass). Commit only the icons used, each with its author recorded. |

### 2. Kenney: Fantasy UI Borders 1.0

| Field | Value |
|---|---|
| Creator | Kenney (www.kenney.nl), created 03-12-2023 |
| Source | https://kenney.nl/assets/fantasy-ui-borders |
| Licence | **CC0 1.0** |
| Evidence | The page states "Creative Commons CC0". The downloaded `License.txt` says "License: (Creative Commons Zero, CC0)". The preview image adds "free to use in personal, educational and commercial projects". |
| Commercial / modification | Yes / Yes |
| Attribution | Not required (credit optional; Realmkeeper credits anyway) |
| Formats, size | 140 PNG files plus one SVG master; 358 KB zip. Default set 48×48, "Double" set 96×96. White, so tintable. Borders, panels, transparent-centre frames, dividers and fading dividers, in 32 corner styles. |
| Unity | Ideal for 9-slice: `-unity-slice-*` on `background-image`, tinted. Only small textures. |
| **Recommendation** | **Use.** It gives restrained corner ornaments for panels and modals, and dividers for headings, without heavy gold art. Use the "Double" PNGs (96 px) for high DPI. |

### 3. Kenney: Cartography Pack

| Field | Value |
|---|---|
| Creator | Kenney, created 16-06-2020 |
| Source | https://kenney.nl/assets/cartography-pack (also mirrored on OpenGameArt: https://opengameart.org/content/cartography-pack) |
| Licence | **CC0 1.0** (page and the downloaded `License.txt`) |
| Formats, size | 83 hand-drawn line symbols at 64 px (Default) and 128 px (Retina): castles, towers, towns, houses, churches, mountains, trees and forests, rivers, roads, bridges, a compass rose, a camp, a lighthouse, a skull, a chest, banners, shield frames. Also 5 parchment textures at 1024×1024 (Ancient, Basic, Crinkled, Folded, FoldedCrinkled), plus SVG and spritesheets. 4.2 MB zip. |
| Unity | The symbols are dark line art on transparent, so they tint well. For the atlas, they can be drawn as textured quads in `generateVisualContent` or as small `VisualElement`s. The parchment can sit behind the atlas map and journal pages. |
| **Recommendation** | **Use.** It gives settlement and landmark markers and terrain stamps for the world atlas, and parchment for map and document surfaces. It does not replace the authoritative map data. |

### 4. Poly Haven: textile and leather textures (selected)

| Field | Value |
|---|---|
| Creator | Poly Haven staff and donating artists |
| Source | https://polyhaven.com, category "Textiles & Leather" (API: `https://api.polyhaven.com/assets?t=textures&c=fabric`, which returns 43 entries) |
| Candidates | `brown_leather`, `fabric_leather_01`, `fabric_leather_02`, `rough_linen`, `hessian_230`, `book_pattern` |
| Licence | **CC0** |
| Evidence | https://polyhaven.com/license: "Our assets are all licensed as CC0", "You do not need to give credit". The CC0 covers the assets, not the site's logos or renders. The Terms forbid scraping, so download by hand, not by script. |
| Formats | JPG/PNG/EXR texture sets at 1K to 8K |
| Unity | Take the diffuse map at 1K, desaturate it, darken it and re-encode it at 512 px. Use it as a faint (5–8 %) panel grain under `background-color`. UI Toolkit has no blend modes, so bake the tint into the PNG. |
| **Recommendation** | **Use, sparingly.** One leather and one linen grain, for the hub and the character sheet. |

### 5. ambientCG (already in the project)

| Field | Value |
|---|---|
| Licence | **CC0 1.0** (verified at https://docs.ambientcg.com/license/: "All ambientCG assets are provided under the Creative Commons CC0 1.0 Universal License") |
| Status | Already credited in `CREDITS.md` and `Art/ThirdParty/AmbientCG/License.txt`. The site has a "Paper" category, but no specific paper asset ID was confirmed in this pass. |
| **Recommendation** | **Use** for any stone, wood or metal UI backdrop already in the repository (Wood049, Planks021). Investigate the paper IDs before downloading. |

### 6. Typography: CC0 option

| Field | Value |
|---|---|
| Font | **MFB Oldstyle** (a revival of Century Oldstyle, Morris Fuller Benton, ATF 1909) |
| Source | CTAN, `fonts/mfb-oldstyle` (README read at https://ctan.math.illinois.edu/fonts/mfb-oldstyle/README) |
| Licence | **CC0 1.0** ("placed in the public domain using the CC Zero 1.0 waiver") |
| Formats | OTF and Type 1; Regular, Italic, Bold and Bold Italic |
| Concerns | Small caps and old-style figures exist in Regular only. It must be tested at 14–16 px in Unity's TextCore (OTF with CFF outlines). |
| **Recommendation** | **Use for headings and dialogue if the owner keeps to CC0/CC BY fonts.** Otherwise see the OFL options below. |

## Investigate further

| Asset | Creator | Licence (evidence) | Why not "Use" yet |
|---|---|---|---|
| Kenney Game Icons (105 UI pictograms: gear, save, pause, zoom, lock, audio…) | Kenney | CC0 (`License.txt` in the download) | The app-style pictograms don't match the game-icons.net silhouettes. Keep them only as a fallback for pure interface actions. |
| K.M. Alexander fantasy map brushes (30+ sets traced from historic maps: Mercator, Vischer, Ogilby, Hogenburg…) | K.M. Alexander | CC0 ("All my brushes are released under a CC0 License and are free for personal or commercial use", https://kmalexander.com/free-stuff/fantasy-map-brushes/) | The formats are Photoshop/Illustrator brushes and need converting to PNG. They are a strong option for a historic engraved map style (Direction B). |
| Wuggy's Classic Cartography Set (342 brushes and PNGs: hills, mountains, forests, settlements, castles) | Wuggy | **CC BY 4.0** ("All the assets are licensed under the Creative Commons Attribution 4.0 International License") | It costs **$1.50**, which needs the owner's purchase approval (ADR-0010: purchases are recommend-only). |
| Painterly Spell Icons | J. W. Bjerk (eleazzaar) | Multi-licensed: CC BY 3.0 / CC BY-SA 3.0 / CC BY-SA 4.0 / GPL 2.0 (seen on the OpenGameArt listing, via search; the item page itself was not read) | Painted, not silhouettes, so it would clash with the icon family. The licence needs re-reading on the item page. |
| Flare windows | Blarumyrran (OpenGameArt) | CC0 (page licence field, read via HTTP) | Plain, dark and low-resolution. Usable only as reference. |
| FANTASY GUI | MELLE (OpenGameArt) | CC0 (page licence field) | Resolution and style not inspected (preview images only) |
| OSerif font | GGBotNet (itch.io) | CC0 per search result | Licence page not read. Aimed at games, with only Regular and Italic. |
| Seshat font | — (Font Squirrel) | CC0 per search result | Not read at source |
| Fantasy Map Design "Simple Stamps" | Fantasy Map Design (cartographyassets.com) | CC BY 4.0 per search result | The page returned 403, so the licence is **unverified** |

## Not pre-approved licence (owner decision needed)

| Asset | Licence (verified) | Note |
|---|---|---|
| EB Garamond | SIL OFL 1.1 (`google/fonts/ofl/ebgaramond/OFL.txt`) | Recommended heading and body serif if OFL is approved |
| Cinzel | SIL OFL 1.1 | Capitals-only display face; for screen titles only |
| Alegreya, Alegreya Sans | SIL OFL 1.1 | A matched serif and sans pair designed for long reading |
| Source Sans 3 | SIL OFL 1.1, with Reserved Font Name "Source" (a modified version must be renamed) | Very legible UI sans |
| Inter | SIL OFL 1.1 | Tabular figures for statistics |
| Cormorant Garamond | SIL OFL 1.1 | Too thin below 18 px; display only |
| Marcellus | SIL OFL 1.1 (Reserved Font Name) | Display |
| Spectral | SIL OFL 1.1 | Screen-first serif |
| IM Fell DW Pica | SIL OFL 1.1 | Historic; too rough for body text |
| Lucide icons | ISC, plus MIT for the Feather-derived icons (https://lucide.dev/license) | The notice must ship with copies. Its outline style clashes with the fantasy silhouettes. **Reject on style.** |
| RPG Fantasy UI Kit (samzerox) | Custom terms: commercial use and modification allowed, credit optional, "Do not resell or redistribute the assets as-is" | Not CC0 or CC BY, so excluded. Flat vector style. |

OFL obligations: the font may be bundled and sold with the game. It may not be sold on its own. The licence text must accompany it, and a modified version must not use a Reserved Font Name.

## Rejected

| Asset | Reason |
|---|---|
| Kenney UI Pack 2.0, Kenney UI Pack: RPG Expansion | CC0 (verified), but the cartoon style (bright colours, chunky bevels, glove cursors) is wrong for a mature RPG |
| Kenney Fonts | CC0, but the faces are pixel and rounded |
| Golden UI (Buch), Parchment GUI (zwonky), Veyroa Fantasy Pixel UI, Kibyra Free Medieval Fantasy UI, heyheythere Pixel UI Fantasy (CC BY 4.0) | Pixel art (16–64 px grids): unreadable as panels at 1080p and out of style |
| RPG GUI construction kit v1.0 (Lamoot) | CC BY 3.0 (verified). But it incorporates a third-party texture whose licence was not established, so its provenance is incomplete. |
| Crusenho Complete UI / GUI Essential Pack | Conflicting statements from the creator (CC0 for the free pack in one comment, CC BY 4.0 in another, "any commercial project" in a third) |
| MonkeyBlood Narrasil / Myrklund / Havenland | Personal use only, unless a commercial licence is bought |
| "Fantasy Mountains" (cartographyassets) | CC BY-NC 4.0: no commercial use |
| ProFantasy monthly symbols | Tied to Campaign Cartographer; no CC licence |
| fantasymapassets.com, Atypikk01 (DeviantArt) | "Free for commercial" with no named licence: unverifiable |
| Ancient Realms (free demo), Gilded Fantasy UI LITE, Crisp Quest Free | Licence not found |
| Unity Vector Graphics package (as an SVG route) | Preview, hidden in Unity 6.0. Rasterising offline avoids it. |
| Wikimedia Commons compass roses | Not examined in enough depth to verify a file (the category URL tried returned 404). Kenney's compass covers the need. |
| Reference-game UI (any) | Proprietary. Studied for principles only; no asset taken. |

## Downloaded in Phase 19A

Into the session scratch folder only, **not into the repository**, for inspection:
- Kenney Fantasy UI Borders;
- Cartography Pack;
- UI Pack;
- UI Pack: RPG Expansion;
- Game Icons.

All five are CC0, and each includes its `License.txt`. Nothing was imported into `Assets/`. Importing is a Phase 19B step, under ADR-0010's rules: only the used subset, licence file beside it, and an inventory row.
