# Audio pass (2026-11-15): asset register and decisions

The owner listed sources and gave "free rein to install and integrate any of the above". This register records what was
adopted, what was downloaded but not adopted, and what was not installed, with the reason for each. **Nothing was
purchased; no account was created; no licence or store terms were accepted on the owner's behalf.**

## Adopted

### 1. Kevin MacLeod: 12 tracks (music)

| Field | Value |
|---|---|
| Creator | Kevin MacLeod (incompetech.com) |
| Source | `https://incompetech.com/music/royalty-free/mp3-royaltyfree/<Title>.mp3`, downloaded 2026-11-15 |
| Licence | Creative Commons Attribution 4.0 (CC BY 4.0). Evidence: incompetech.com/music/royalty-free/licenses/: no charge, "Requires that you credit the music" |
| Attribution | **Required.** In game (Info → credits, `Credits.cs`), in `CREDITS.md`, and in `Art/ThirdParty/KevinMacLeod/License.txt`, in incompetech's generated format. A test (`AudioPassPlayModeTests.EveryTrack_IsInTheProject_AndEveryMoodHasMusic`) fails if a playlist names a track the credits do not |
| Tracks | Achaidh Cheide, Celtic Impulse, Crossing the Chasm, Five Armies, Lord of the Land, Master of the Feast, Midnight Tale, Minstrel Guild, Pippin the Hunchback, Teller of the Tales, Thatched Villagers, Village Consort (about 82 MB of MP3) |
| Modifications | None (file names lower-cased) |
| Project location | `Assets/Realmkeeper/Art/ThirdParty/KevinMacLeod/Resources/RkMusic/` |
| Import | Streamed Vorbis, quality 0.6, no preload (`Editor/AudioImportRules.cs`) |
| Played by | `Realmkeeper.UI.RkMusic` |

**Playlists (`RkMusic.Playlists`).** Title: Lord of the Land. Strategic map: Teller of the Tales, Achaidh Cheide. Country:
Celtic Impulse, Achaidh Cheide, Teller of the Tales. Settlement: Thatched Villagers, Village Consort, Minstrel Guild,
Master of the Feast, Pippin the Hunchback. Night: Midnight Tale. Places: Midnight Tale. Fight: Five Armies, Crossing the
Chasm.

**Mood rules.** Title screen > exploration > strategic map. In exploration: a fight > a place > night (night ≥ 0.6) >
country > settlement. A change of mood crossfades over 2.5 s; a finished track is followed by the next of its playlist.
Volume = sound volume × music volume (new setting "Music", default 50 %).

### 2. Kenney Impact Sounds 1.0 (35 of 130 files)

| Field | Value |
|---|---|
| Source | kenney.nl/assets/impact-sounds (official download), 2026-11-15 |
| Licence | CC0 1.0 (bundled `License.txt`, kept unmodified) |
| Files | `footstep_grass_000–004`, `footstep_wood_000–004`, `footstep_concrete_000–004`, `footstep_snow_000–004`, `impactPunch_medium_000–004`, `impactPunch_heavy_000–004`, `impactPlate_light_000–004` |
| Project location | `Assets/Realmkeeper/Art/ThirdParty/Kenney/ImpactSounds/Resources/RkAudio/` |
| Played by | `GameSounds`: `footstep-grass/-stone/-snow` (the player's steps follow `GameSounds.SurfaceFor`: stone in places, snow when it falls thickly, grass in the country, the existing earth-and-gravel steps in settlements); `hit` and `hit-heavy` (a critical) when damage lands; `block` when a shield turns a blow. `footstep-wood` is loaded but not yet chosen (interiors are not a separate state) |

### 3. Kenney Music Jingles (3 of 85 files)

| Field | Value |
|---|---|
| Source | kenney.nl/assets/music-jingles (official download), 2026-11-15 |
| Licence | CC0 1.0 (bundled `License.txt`) |
| Files | `jingles_PIZZI01` (quest completed), `jingles_PIZZI10` (quest failed), `jingles_STEEL01` (new level) |
| Played by | `ExplorationController.AfterQuestCommand` and the level-up notice. Each accompanies a visible notice; nothing is said by sound alone |
| Note | **Chosen by name, not by ear.** The owner checklist asks for them to be auditioned |

## Downloaded, not adopted

| Asset | Reason |
|---|---|
| Kenney Survival Kit 2.0 (CC0) | No camps exist in the game yet, and the road fights reuse a settlement's ground. Its logs, tools, barrels and signs are already covered by the owned Quaternius Pro kits in the game's style; Kenney's flat style would clash. Keep for a camp feature |
| Kenney Furniture Kit (CC0) | Almost entirely modern (televisions, fridges, laptops, showers). Medieval furniture is covered by the owned Fantasy Props MegaKit Pro |

The downloads stay outside the repository.

## Not installed

| Asset | Reason |
|---|---|
| Scott Buckley (CC BY 4.0) | Licence verified (scottbuckley.com.au/library/using-this-music; credit "'<Title>' by Scott Buckley – released under CC-BY 4.0. www.scottbuckley.com.au"; no resale or re-upload in isolation). The site's firewall refuses scripted downloads, and it was not bypassed. Six tracks were chosen (Call To Adventure, Penumbra, Home Was You, Memories of Stone, Katabasis I, Song of the Forge) for the owner to download by hand if wanted |
| Sonniss GDC bundles | 9–48 GB per year's bundle, and choosing ambience needs listening. Waits for the owner's choice of bundle and an ambience system |
| Mixamo | Needs the owner's Adobe sign-in. Rejected in Phase 20 on licence grounds (Adobe terms, not CC); the owner's listing now reopens it for work and social animations, retargeted through Mecanim humanoid |
| MapMagic 2, Gaia Pro / GeNa, MicroSplat | All three work on Unity `Terrain`. Realmkeeper's terrain is its own deterministic mesh chunks with `Realmkeeper/VertexColorLit`; adopting them means replacing the terrain system. Gaia Pro is paid; the free cores need an Asset Store account and EULA acceptance (per-seat, not commit-safe) |
| COZY: Stylized Weather 3 | Paid ($50 in the 2026-11-12 research). The owner decided on 2026-11-12 that weather stays inside Unity |
| Gaea | A desktop authoring application, not a Unity asset; the overland is generated from the simulation, not from authored heightmaps |
| Poly Haven skies | The camera is isometric and the sky is a tinted procedural material, barely seen. Lighting is Trilight ambient by time and weather |
| ambientCG (more grounds) | Useful (mud, snow, gravel) but needs `VertexColorLit` to take more ground layers: a terrain change for its own pass |
| Kenney Fantasy Town Kit | Already in the project (2026-11-12) |

## Owner checklist

1. Listen to each mood: the title, the strategic map, a town by day, a town at night, the country, a place, a fight. Is any track wrong for its mood?
2. Settings → Sound → Music: 0 silences the music; the sound volume scales it.
3. Walk in the country (grass), in a place (stone), and in heavy snow (snow).
4. In a fight: a blow lands (hit), a critical (heavier), a shield block.
5. Finish a quest, fail one, and reach a new level: do the three jingles suit?
