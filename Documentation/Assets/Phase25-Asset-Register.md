# Phase 25: asset register

Every third-party asset adopted in Phase 25. **Nothing was purchased.**

The evaluations, and the candidates not adopted, are in `Documentation/Research/Phase25-UI-Research.md`. Assets adopted before Phase 25 remain in:
- `Documentation/Research/Realmkeeper-Asset-License-Registry.md`;
- `Documentation/Research/Phase19-Asset-License-Register.md`;
- `CREDITS.md`.

## 1. Kenney Interface Sounds 1.0 (10 of 100 files)

| Field | Value |
|---|---|
| Creator | Kenney (www.kenney.nl), created 11-02-2020 |
| Source | kenney.nl/assets/interface-sounds. Files from the official OpenGameArt mirror, `https://opengameart.org/sites/default/files/kenney_interfaceSounds.zip` (834,536 bytes) |
| Licence | Creative Commons Zero (CC0 1.0), https://creativecommons.org/publicdomain/zero/1.0/ |
| Licence evidence | The bundled `License.txt` (kept unmodified in the project): "License: (Creative Commons Zero, CC0) … This content is free to use in personal, educational and commercial projects. Support us by crediting Kenney or www.kenney.nl (this is not mandatory)". The publisher's page also states "Creative Commons CC0" |
| Attribution | Not required. Credited anyway: in game (Credits) and in `CREDITS.md` |
| Cost | Free |
| Downloaded | 2026-11-14 |
| Project location | `Assets/Realmkeeper/Art/ThirdParty/Kenney/InterfaceSounds/` (`License.txt`, `Resources/RkUiAudio/*.ogg`) |
| Files used | `click_002`, `select_002`, `confirmation_002`, `back_002`, `open_002`, `close_002`, `error_004`, `bong_001`, `drop_002`, `switch_002` (130 KB in all) |
| Modifications | None (the OGG files are as published) |
| Played by | `Realmkeeper.UI.RkUiSound` (click, select, confirm, cancel, open, close, error, notify, item, toggle), at the sound volume times the interface-sound volume |
| Dependencies | None (Unity's built-in OGG import) |
| Restrictions | None |

## 2. game-icons.net: 11 more icons

| Field | Value |
|---|---|
| Creators | Lorc: `paw-print`, `fanged-skull`, `fangs-circle`, `boar-tusks`, `ringing-bell`. Delapouite: `cow`, `deer-track`, `sheep`, `speaker`, `hamburger-menu`, `goblin-head` |
| Source | PNG: `https://game-icons.net/icons/ffffff/transparent/1x1/<author>/<icon>.png`. SVG: `https://raw.githubusercontent.com/game-icons/icons/master/<author>/<icon>.svg` |
| Licence | Creative Commons Attribution 3.0 Unported (CC BY 3.0) |
| Licence evidence | As verified in Phase 19A: the game-icons.net about page and the repository's `license.txt` |
| Attribution | **Required.** "Icons made by Lorc, Delapouite, … Available on https://game-icons.net. Licensed under CC BY 3.0." It is shown in game (Credits) and in `CREDITS.md`; the per-author counts are updated in `Art/ThirdParty/GameIcons/License.txt` (now 153 icons: Lorc 75, Delapouite 60) |
| Cost | Free |
| Downloaded | 2026-11-14 |
| Project location | PNG: `Assets/Realmkeeper/Art/ThirdParty/GameIcons/Resources/RkIcons/`. SVG source: `…/GameIcons/Source/<author>/` |
| Modifications | None to the shapes: white on transparent, tinted at run time (as for all the family) |
| Manifest | `Tools/UI/icon-manifest.tsv` and `RkIcons.Files`. Four more keys reuse icons already in the project: `undead`, `humanoid`, `compare` and `codex` |
| Restrictions | Keep the credit wherever the icons ship |

## 3. Not adopted, and why

| Candidate | Reason |
|---|---|
| Kenney Cursor Pack | Its bundled licence could not be inspected (the download is behind the donation dialog; the mirror has no file). The system cursor is adequate |
| Kenney UI Pack RPG Expansion | Bright cartoon style; conflicts with the owner-approved direction. The Fantasy UI Borders already cover frames |
| GUI Pro – Fantasy RPG (Layer Lab), US$39.99 | **Owner decision; not recommended.** uGUI sprite kit (the game is UI Toolkit only); glossy mobile style; Asset Store EULA. Not bought |
| Classic Fantasy RPG – UI Kit, US$19.99 | **Owner decision.** uGUI kit; style not assessed from files. Not bought |
