# ADR-0099: Interface assets and fonts (CC BY icons, CC0 kits, OFL fonts)

**Status.** Accepted, 2026-11-12 (Phase 19). It extends ADR-0010 and ADR-0097.

**Context.**
- **Licence rules.** Phase 19 needs an icon family, window ornament, map symbols and typefaces. The owner approved CC0, and CC BY with credit (ADR-0097).
- **Research.** The Phase 19A research (`Documentation/Research/`) examined 46 candidates.
- **Fonts.** No suitable CC0 or CC BY text family was found except MFB Oldstyle. The owner was asked about the SIL Open Font License and **approved it on 2026-11-12**.

**Decision.**
1. **Icons: game-icons.net, CC BY 3.0.**
   - 142 icons from 11 authors, kept with their SVG sources.
   - Credited in `CREDITS.md`, in the game (Credits list) and in `GameIcons/License.txt`.
   - The key → file list is `Tools/UI/icon-manifest.tsv`.
2. **Kenney Fantasy UI Borders and Cartography Pack, CC0.** Only the used subset is kept (75 files), each pack with its `License.txt`.
3. **Fonts: EB Garamond and Alegreya Sans, SIL OFL 1.1.**
   - Unmodified, with their `OFL.txt`.
   - Recorded as OFL, never as CC0 or CC BY.
4. **Import rules.** Interface textures load from `Resources/RkIcons`, `RkFrames` and `RkMap`. `ArtImportPostprocessor` (version 8) gives them UI import settings.
5. **Not used:**
   - CC BY-SA, non-commercial, unverified and conflicting licences;
   - store-EULA assets;
   - anything from the reference games.

   The paid Wuggy set ($1.50, CC BY 4.0) was declined by the owner.

**Consequences.**
- **Attribution has a test.** Every icon author is named where the player can reach it (Player Hub ▸ Info, and the title screen's Credits). A test checks it.
- **Small footprint.** About 4 MB.

**Alternatives considered.**
- **CC0 fonts only** (MFB Oldstyle plus the engine font): weaker, and offered to the owner, who chose OFL.
- **Lucide (ISC):** its outline style clashes with fantasy silhouettes.
- **Pixel-art UI packs:** unreadable at 1080p, and out of style.
