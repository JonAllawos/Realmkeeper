# Phase 20: Character assets and references

**Date:** 2026-11-12. **Scope:** character creation, character sheets, appearance customisation, progression, party and NPC inspection.

**Rules:**
- Licences were read at the source named in each row.
- Nothing below was imported into the project in Phase 20; see "Integrated", below.

## 1. Reference study: design principles, not assets

| Game | What works | What Realmkeeper takes |
|---|---|---|
| Baldur's Gate 3 | A large, lit character beside the choices. A choice changes the model at once. Every number on the sheet opens a tooltip naming its sources. Level-up is a guided sequence with a summary before confirming. | The creator now gives the 3D preview the left third of the screen, with a live build panel on the right. Sheet ability tiles carry their full breakdown in a tooltip. Level-up already confirms before applying, and closing it applies nothing. |
| Pathfinder: Wrath of the Righteous | The build is always visible while choosing. Prerequisites and the reason a choice is blocked are spelled out. | The creator's build panel lists everything still to choose, by step. A choice is never silently corrected; a species change that alters the skin tone says so. |
| Kingdom Come: Deliverance 2 | Character identity grows from deeds, not a chosen-one premise. Sheet sections are grounded in the period. | The identity step asks for a line in the player's own words and offers prompts, not a destiny. |
| The Witcher 3 | Compact, readable character screens with clear groupings. | The sheet's tabs (Sheet, Skills, Features, Origins, Proficiencies, Progression, Identity) each answer one question. |
| Dragon Age: Inquisition | Origin (race and background) explained alongside the mechanics. | The new **Origins** tab: species and lineage, every trait (flagged where the game cannot play it yet), background, history. |
| Skyrim | Appearance sliders for proportions. | Height (92–108 % of the species' typical height) and build (slight, average, heavy) change the real model. |
| Crusader Kings III | You know other people only as far as your knowledge reaches. | "About them" in conversation shows a person by acquaintance: seen, spoken with, known well, companion. A stranger's sheet stays hidden. |

No proprietary artwork, text or interface asset was used.

## 2. Asset candidates

| Asset | Source | Licence (evidence) | Commercial / attribution / redistribution | Format · rig | Fit | Decision |
|---|---|---|---|---|---|---|
| Universal Base Characters (Standard tier, already used) | quaternius.com/packs/universalbasecharacters.html | **CC0** ("Free to use in personal, educational and commercial projects") | Yes / none / yes | FBX, glTF · Humanoid; works with the Universal Animation Library | The project's body rig. The page lists 20 mixable hairstyles; the repository has 8. The page lists only human characters: no ears, horns or tails. | **Investigate:** more of its hairstyles (the original download is not in the repository; re-download needed). No non-human anatomy is available here. |
| Modular Character Outfits – Fantasy (already used) | quaternius.com | **CC0** | Yes / none / yes | FBX · same Humanoid rig | Clothing and armour parts on the project's rig | Already integrated (Phase 5). More outfits are in the paid Source tier (recommend-only). |
| KayKit Adventurers 2.0 | kaylousberg.itch.io/kaykit-adventurers | **CC0** ("Free for personal and commercial use, no attribution required") | Yes / none / yes (do not resell unmodified) | FBX, glTF · KayKit rig (the project already uses KayKit animations for skeletons) | Stylised, cartoon proportions that clash with the Quaternius bodies | **Reject** for the player character (style). Possible later for distant crowds. |
| MakeHuman exported models | makehumancommunity.org (FAQ) | **CC0** for exported models ("The exported models end up being licensed CC0"); assets under CC BY need separate care | Yes / none, unless CC BY add-on assets are used / yes | FBX · its own skeleton | Realistic proportions, different topology and rig. A full pipeline change. | **Investigate** for a later art phase, not Phase 20 |
| Littlelulu "Basic Horns + Tail" | littlelulu.gumroad.com/l/freehornstail | Custom: "Credit must be given at all times" (no named CC licence) | Unclear redistribution | Rigged | The only rigged horns and tail found | **Reject:** the licence is not CC0 or CC BY and is unverifiable |
| Dreamloft low-poly anime heads (ears, horns) | dreamloft3d.gumroad.com | Not stated in the material found | Unknown | .blend, .fbx | Anime style, not rigged to this skeleton | **Reject:** licence unverified, style mismatch |
| Mixamo characters and animations | mixamo.com | Adobe terms (not CC) | Use in projects allowed, but raw redistribution is restricted | FBX | Rig conversions needed | **Reject:** not CC0 or CC BY; raw files cannot sit in the repository |
| game-icons.net (already integrated in Phase 19) | game-icons.net | CC BY 3.0 | Yes / per author / yes | PNG and SVG | Step, tab and ability icons in the creator and sheet | **Used** (no new files) |

## 3. Downloaded
None in Phase 20.

## 4. Integrated
No new third-party asset. Phase 20 reuses:
- the CC0 Quaternius bodies, heads, hair and outfits;
- the CC BY game-icons.net set and the OFL fonts from Phase 19.

## 5. What this means for species appearance
| Species | Appearance status |
|---|---|
| Human | Finished model |
| Everyone else | **Placeholder body:** the human model, scaled to the species' typical height, now with the person's own height and build, and the species' own skin tones. The tones are a tiefling's ember, wine and violet; an orc's grey-green and moss; a goliath's stone; a dragonborn's scale colours; an aasimar's gilded and silvered. |
| Not modelled anywhere | Elf ears, tusks, horns, tails, scales and draconic heads. No CC0 or CC BY rigged asset was found that fits the Quaternius skeleton. |

The creator, the species detail and the sheet's Origins tab all say "placeholder body" and list what the final model needs. The work is recorded as deferred: a dedicated art pass, or a commission.
