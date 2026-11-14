# Phase 25: interface research (reference games and third-party assets)

**Date:** 2026-11-14.

**Builds on** the Phase 19A research, which stays valid and is not repeated here:
- `Phase19-Reference-Game-Study.md`: ten games, patterns and anti-patterns;
- `Phase19-UI-Asset-Research.md` and `Phase19-Asset-License-Register.md`: 46 asset candidates with verified licences;
- `Phase19-Visual-Direction.md`: the owner chose direction A, "Cartographer's Ledger".

This note adds what Phase 25 needed: the patterns behind the screens Phase 19 did not cover, and the candidates for interface sound, cursors, more icons and paid kits.

**Method.**
- No proprietary art, layout or code was copied.
- Patterns are described in words and rebuilt with Realmkeeper's own tokens and components.

## 1. Patterns adopted, by screen

The games named are the references in which each pattern is common; the rows say what Realmkeeper took.

| Screen | Pattern (where it is common) | Realmkeeper's version |
|---|---|---|
| Pause menu | One centred column, Resume first and focused, destructive entries last and confirmed (Skyrim, The Witcher 3, Baldur's Gate 3) | `RkMenuOverlay`: Resume, Save, Settings, Return to the map, Main menu, Exit. Esc resumes; the exits ask first and say what would be lost; unavailable entries are disabled with the reason |
| Notifications | Short, stacked, auto-dismissing toasts at one screen edge; repeats merged; a log to read later (Kingdom Come: Deliverance 2, Red Dead Redemption 2, Crusader Kings III's alerts) | `RkToasts`: at most 4 at once; repeats within 45 s suppressed; overflow folded into "N more"; history in the pause menu |
| Exploration HUD | Minimal, contextual: vitals in one corner, the place and time in another, prompts centred low (The Witcher 3, Kingdom Come: Deliverance 2) | Location top left with vitals under it; standing actions top right; the action bar centred above the controls line; nothing permanent that is not needed |
| Inventory | A list or grid with icons on one side, a persistent inspector on the other; actions in the inspector; comparison with what is worn shown as deltas (Baldur's Gate 3, Pillars of Eternity II, Pathfinder: Wrath of the Righteous) | Master-detail list: icon, name, category, quantity, weight, value. The main use stays on the row (one click). Everything else is in `RkInspector`, with the rules engine's comparison marked "better" or "worse" in words |
| Bestiary | Entries unlocked by encounter; more detail after defeat (The Witcher 3, Pathfinder's knowledge checks) | Met: kind, habitat, behaviour. Defeated: armour, hit points, attacks, defences. Unknown creatures are not listed at all (ADR-0116) |
| Journal | Grouped navigation: quests, then knowledge, then the world, with the selected quest's detail beside the list (Dragon Age: Inquisition, The Witcher 3) | Four headings (Quests, Knowledge, Deeds, Standing) over the thirteen existing pages |
| Settings | Accessibility as its own section; text size separate from interface scale; options apply at once (Baldur's Gate 3, recent Ubisoft titles) | Accessibility: text size (100 / 115 / 130 %), high contrast, reduced motion. Interface-sound volume. Every control works; what is not available is listed in words |
| Combat turn order | Portraits or cards with a health bar (Baldur's Gate 3, Divinity: Original Sin 2) | The existing cards gain a painted health line; the numbers stay in the text |

**Anti-patterns avoided** (as Phase 19 listed them):
- toasts that block the view or repeat;
- icons without labels;
- colour as the only signal;
- decorative frames that take space from data;
- tiny text (the 10–11 px sizes found in the audit are now 12 px).

## 2. Third-party assets examined in Phase 25

Licences were read from the files themselves, or from the publisher's own page where noted. A search snippet was not treated as a licence.

| Candidate | Source | Licence (evidence) | Commercial / modify / redistribute in a game | Attribution | Cost | Fit | Decision |
|---|---|---|---|---|---|---|---|
| **Kenney Interface Sounds 1.0** (100 OGG files, 0.8 MB) | kenney.nl/assets/interface-sounds; files from the official OpenGameArt mirror (opengameart.org/content/interface-sounds, `kenney_interfaceSounds.zip`) | **CC0 1.0**. The bundled `License.txt` reads "License: (Creative Commons Zero, CC0)"; the publisher's page states "Creative Commons CC0" | Yes / yes / yes | Not required (credited anyway) | Free | Short, neutral clicks and chimes that sit under the existing Kenney RPG Audio | **Adopted: 10 files** (`Art/ThirdParty/Kenney/InterfaceSounds`) |
| **game-icons.net**: 11 more icons by Lorc and Delapouite | game-icons.net, with the SVGs from github.com/game-icons/icons | **CC BY 3.0** (verified in Phase 19A: the site's about page and the repository's `license.txt`) | Yes / yes / yes | Required; done in game (Credits) and in `CREDITS.md` | Free | The same family as the 142 already used | **Adopted: 11 icons** (creature kinds, bestiary, notifications, sound, menu) |
| Kenney Cursor Pack 1.1 (180 files) | kenney.nl/assets/cursor-pack | CC0 according to the publisher's page. **The bundled licence was not inspected**: its download is behind the site's donation dialog, and the OpenGameArt page has no file | — | — | Free | The system cursor is adequate; a custom cursor was not needed for the brief | **Not adopted.** Unverified files are not used |
| Kenney UI Pack RPG Expansion (85 files) | kenney.nl/assets/ui-pack-rpg-expansion | CC0 according to the publisher's page | — | — | Free | Bright, cartoon panels (2014): a different style from the Ledger's restrained ink and bronze; the Fantasy UI Borders already in use cover frames | **Rejected** (style) |
| GUI Pro – Fantasy RPG (Layer Lab) | assetstore.unity.com/packages/2d/gui/gui-pro-kit-fantasy-rpg-170168; also layerlab.itch.io | Unity Asset Store EULA (standard licence) per the store listing; itch.io terms differ by version | Asset Store EULA: use in a game yes; redistribution of source assets no | No | **US$39.99** (Asset Store, one-time); bundle with two others US$89.99 | Built for uGUI prefabs and sprites. Realmkeeper is UI Toolkit only, so it would need re-slicing into USS. Its glossy, saturated mobile style conflicts with the owner-approved direction | **Not purchased; recommended against.** Owner decision if wanted |
| Classic Fantasy RPG – UI Kit | assetstore.unity.com/packages/2d/gui/classic-fantasy-rpg-ui-kit-193841 | Unity Asset Store EULA per the listing | As above | No | **US$19.99** (per a search summary; the listing was not opened) | uGUI sprites; would need conversion; style not assessed in the files | **Not purchased.** Listed for the owner; needs a closer look before any decision |

**Paid assets.** Nothing was bought, and no payment was started. The two kits above are presented for owner review only. Neither is needed for the Phase 25 interface, and neither is recommended in its current form.

## 3. Fonts

The two OFL 1.1 families from Phase 19 stay: EB Garamond and Alegreya Sans.
- No new font was added.
- The audit found the problem was size (10–11 px), not the faces.
- All sizes now go through tokens, so the Text size setting reaches every screen.

## 4. What was not researched again

- Map and parchment resources: the Kenney Cartography Pack is in use since Phase 19.
- Leather and paper grain textures (Poly Haven CC0, Phase 19A): still optional. The interface reads well without them, and they would add texture memory for little gain.

**Sources:**
- [Kenney: Interface Sounds](https://kenney.nl/assets/interface-sounds)
- [OpenGameArt: Interface Sounds (Kenney mirror)](https://opengameart.org/content/interface-sounds)
- [Kenney: Cursor Pack](https://kenney.nl/assets/cursor-pack)
- [Kenney: UI Pack RPG Expansion](https://kenney.nl/assets/ui-pack-rpg-expansion)
- [Unity Asset Store: GUI Pro – Fantasy RPG](https://assetstore.unity.com/packages/2d/gui/gui-pro-kit-fantasy-rpg-170168)
- [Layer Lab on itch.io: GUI Pro – Fantasy RPG](https://layerlab.itch.io/gui-pro-fantasy-rpg)
- [Unity Asset Store: Classic Fantasy RPG – UI Kit](https://assetstore.unity.com/packages/2d/gui/classic-fantasy-rpg-ui-kit-193841)
- [game-icons.net](https://game-icons.net)
