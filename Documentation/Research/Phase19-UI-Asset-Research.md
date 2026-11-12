# Phase 19A: UI and UX asset research

**Date:** 2026-11-12.

**Branch:** `feature/phase-0-1-foundation`. HEAD was `c3dcd7e` (Phase 18 docs), above `713b9a0` (Phase 18 implementation), with both commits local and ahead of origin by 2. The working tree was clean apart from the owner's preserved files.

**Scope.** This is research only. No gameplay, assets, project settings or UI code were changed.

**Companion documents:**
- [Reference game study](Phase19-Reference-Game-Study.md)
- [Visual direction](Phase19-Visual-Direction.md)
- [Asset licence register](Phase19-Asset-License-Register.md)
- [Screen-asset matrix](Phase19-Screen-Asset-Matrix.md)

## 1. Preflight findings

| Area | Finding |
|---|---|
| UI framework | UI Toolkit (ADR-0001), Unity 6000.0.47f1, URP 17.0.4. There is no Vector Graphics package and no TextMeshPro asset; fonts are Unity's runtime default. |
| Stylesheets | `RealmkeeperHud.uss` (909 lines, strategic layer) and `ExplorationHud.uss` (1,433 lines, exploration and hub). Colours are literal values with no USS variables. One theme, `RealmkeeperTheme.tss`. |
| Panel settings | Scale With Screen Size, reference 1920×1080, match width (0). At 1366×768, everything scales to 71 %, so 14 px text renders at about 10 px. That is a legibility risk to fix in 19B, e.g. match 0.5 and a minimum text size. |
| Map | The 3D strategic map (`WorldView`, vertex-colour shader) plus a 2D atlas drawn in `Painter2D` (`RealmkeeperHud.Atlas.cs`) |
| Existing third-party assets | All 3D: Quaternius, KayKit, Poly by Google (CC BY 3.0), ambientCG. **No UI assets, icons or fonts.** |
| Credits | `CREDITS.md` and the in-game Hub ▸ Info (`Credits.cs`), with a CC BY section already in place, so CC BY icons fit the established pattern |
| Licence policy | ADR-0010 (CC0, used subset only, licence beside the files), extended by the owner to CC BY with credit (ADR-0097) |
| Missing screens | Main menu, world selection and creation, settings, loading. The game boots straight into a world. |

## 2. Sources searched
1. **Kenney:** 6 packs examined; 5 downloaded to scratch.
2. **OpenGameArt:** licence fields read from the pages over HTTP. The fetch tool was blocked (403).
3. **itch.io:** samzerox, Crusenho, Kibyra, Veyroa and Wuggy, plus tag listings.
4. **game-icons.net and its GitHub repository.**
5. **Poly Haven:** the licence page and the textiles API.
6. **ambientCG:** the licence.
7. **Fonts:** Google Fonts (the `google/fonts` OFL files), CTAN (MFB Oldstyle), Font Squirrel and itch.io (via search).
8. **Map assets:** K.M. Alexander, cartographyassets.com, fantasymapassets.com, MonkeyBlood Design, ProFantasy and DeviantArt.
9. **Icon sets:** Lucide.
10. **Unity documentation:** USS properties, and SVG support in 6.0.
11. **Wikimedia Commons:** attempted; the category URL tried returned 404.

**Unity Asset Store:** not pursued. Its EULA is a separate licence that is neither CC0 nor CC BY, and committing store assets to the repository raises redistribution questions. Any candidate there would need its own owner decision.

## 3. Results
- **Candidates examined:** 46.
- **Licence verified:** 14 CC0, 3 CC BY and 11 other permissive (OFL, ISC/MIT, custom).
- **Unverified, conflicting or non-commercial:** 18.
- **Recommendations:** 6 Use, 9 Investigate, 11 owner decision, 20 Reject.

### Best by category

| Category | Recommendation | Licence |
|---|---|---|
| Icons (all categories) | **game-icons.net** (about 4,180 silhouettes, one consistent style) | CC BY 3.0, credit per author |
| Panels, frames, dividers | **Kenney Fantasy UI Borders** (9-slice, white, tintable) | CC0 |
| Map symbols, compass, parchment | **Kenney Cartography Pack** | CC0 |
| Panel grain textures | **Poly Haven** `brown_leather`, `rough_linen` (at 512 px, darkened) | CC0 |
| Body and heading fonts | **EB Garamond + Alegreya Sans** if OFL is approved; otherwise **MFB Oldstyle** (CC0) for headings and the engine default for body text | OFL / CC0 |
| Dashboards and charts | No asset needed: draw meters and sparklines with `Painter2D`, styled with tokens | — |
| Shaders and transitions | No asset needed: USS `transition` (opacity, translate, colour), 120–180 ms. UI Toolkit in 6.0 has no custom UI shaders. | — |
| Map discovery and fog | No asset: a CPU-painted mask `Texture2D` or hatched Painter2D fill for unknown regions | — |
| Skill trees | Not applicable (SRD level-based progression) | — |

## 4. Technical compatibility (Unity 6.0 UI Toolkit)
- **SVG.** It is not importable for UI Toolkit in 6.0 without the preview Vector Graphics package; built-in support comes in 6.3. **Plan:**
  - use 128 px white PNGs;
  - keep the SVGs as sources;
  - tint with `-unity-background-image-tint-color`.

  This also lets one icon serve every state colour.
- **9-slice.** `-unity-slice-left/top/right/bottom` and `-unity-slice-scale` are supported, so Kenney's 96 px frames suit them.
- **Supported USS features** (Unity 6.0 manual):
  - `text-shadow`;
  - `letter-spacing`;
  - `-unity-font-definition`;
  - custom properties (`var()`);
  - transitions.
- **Unsupported:** box-shadow, gradients and CSS filters. Depth must come from flat fills and borders, which suits the restrained direction anyway.
- **Fonts.** TTF and OTF become dynamic `FontAsset`s. MFB Oldstyle's CFF-outline OTF must be test-imported first.
- **Atlas textures.** Painter2D draws vectors only. Textured symbols need `MeshGenerationContext.Allocate(vertexCount, indexCount, texture)` or positioned child elements. Prefer child elements for hit-testing and tooltips.
- **Dynamic atlas.** Icons ≤ 128 px batch into UI Toolkit's dynamic atlas. Parchment at 1024 px stays as its own texture.
- **High DPI.** 128 px icons drawn at 28–40 px stay crisp up to 2560×1440 at 150 % scale.
- **Interaction states.** All proposed assets are single-state and tinted. States come from USS pseudo-classes (`:hover`, `:active`, `:checked`, `:disabled`, `:focus`).

## 5. Major technical concerns
1. **Legibility at 1366×768**, caused by the current width-matched scaling. Fix this before any art.
2. **Hard-coded colours** throughout the two stylesheets and the C# (`style.color = new Color(...)` in windows). Tokens must come first, or the restyle will be inconsistent.
3. **Default Unity controls** (white text fields and dropdowns) need theme overrides in the `.tss`.
4. **Atlas rendering** needs texture support added to the Painter2D path.
5. **The font licence decision** blocks the typography work.
6. **CC BY attribution at icon granularity:** about 120 icons from several authors. Generate the credit list from a manifest, rather than by hand.

## 6. Suggested implementation order (Phase 19B)
1. Tokens file, theme overrides for the built-in controls, and the scaling fix.
2. Shared components (button, tab, card, chip, badge, meter, tooltip, modal) and the icon manifest with credits.
3. The hub, journal, character, inventory and spellbook windows.
4. Combat HUD.
5. Dialogue and trade.
6. Exploration HUD and prompts.
7. Atlas with cartography symbols and parchment, then the strategic sidebar and inspector.
8. New entry screens (title, world selection and creation, settings) using existing commands only.
9. Decoration (grain, corner frames), then screenshots at three resolutions.

## 7. Owner decisions requested
1. **OFL fonts:** approve EB Garamond and Alegreya Sans (Direction A), or stay CC0 (MFB Oldstyle plus the default font).
2. **Wuggy's Classic Cartography Set:** $1.50, CC BY 4.0. Buy or skip (optional).
3. Confirm **Direction A**.

## 8. Downloads
- Five Kenney CC0 packs were downloaded to the session scratch folder for inspection, each with its `License.txt`.
- **Nothing was added to the repository's assets.**
- Files changed in this pass: the five documents in `Documentation/Research/`.
