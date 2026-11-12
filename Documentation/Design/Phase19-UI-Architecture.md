# Phase 19: UI architecture

## Layers (unchanged rules)
- **Domain and Simulation:** no Unity, and no UI knowledge.
- **Application:** queries (snapshots) and commands. This is the only way a screen reads or changes the world.
- **Infrastructure:** persistence. Phase 19 adds `JsonWorldStore.ListSaves()` (reads file headers only) and `Delete(slot)`, both engine-free and covered by the .NET suite.
- **Presentation:**
  - `Realmkeeper.Presentation`: the strategic HUD, atlas and title screen.
  - `Realmkeeper.Exploration.Presentation`: the exploration HUD, hub, combat, dialogue, trade, creator and advancement.
- **New: `Realmkeeper.UI`.** A small assembly with no game references, used by both presentation assemblies and the PlayMode tests. It holds:
  - `RkIcons`: meaning → game-icons.net texture, tinted. `Lead()` puts the icon in a control's left padding as its own background image, so the control still sizes to its text (UI Toolkit does not measure text for an element that has children).
  - `RkTooltips`: runtime tooltips for every element with `tooltip` text. UI Toolkit shows `tooltip` only in the Editor.
  - `RkModal`: the confirmation dialog. Esc cancels, Enter confirms, and the action runs once.
  - `RkSettings`: interface scale, display mode and resolution, vSync and quality, stored in PlayerPrefs. It owns the shared runtime `PanelSettings`.
  - `RkSettingsWindow`: the settings screen.

## Panels and documents
- **One shared runtime panel.** `RkSettings.Panel` is a runtime copy of `RealmkeeperPanelSettings`. Both HUDs and the title screen use it, so the interface scale applies everywhere at once and the asset on disk is never modified (capture tests used to retarget the asset itself).
- **Documents and sorting order:**
  - strategic HUD: 0;
  - exploration HUD: 10;
  - title screen: 100.
- **Theme.** `RealmkeeperTheme.tss` imports Unity's default, then `RealmkeeperTokens.uss`, then `RealmkeeperComponents.uss`. Tokens are defined on `:root`, so every document inherits them.

## Screen management (no second system)
- Windows remain classes that own a root element and show or hide it with a class. The exploration controller still arbitrates which exploration window is open.
- **The title screen** (`TitleScreen`) is an overlay over the strategic view:
  - it opens when the game starts with the main scene (the first scene loaded, and one in the build list), and from the map's **Menu** button;
  - a test that loads the scene later, or builds the bootstrap in its own scene, never sees it;
  - opening it pauses playback and hides the strategic HUD; closing it restores the HUD.
- **Navigation:**
  - Esc goes back a page (title), closes a window (exploration) or cancels a dialog.
  - Every page has a visible way back ("Back", "Close (Esc)", "Resume").

## Data flow of the new screens

| Action | Path |
|---|---|
| Continue (at launch) | `JsonWorldStore.ListSaves()` → the newest readable, compatible save → `RealmkeeperRuntime.Load(slot)` (or `Host.StartFromSave` when no session is active) → `SelectSlot` |
| New world | Validate the name (1–40 characters) and the seed (a whole number) → confirm if a game is running → `Host.Shutdown()` → `Host.StartNew(CreateWorldCommand(name, seed, NewGameSettings + speed))` → a free slot from the name → `Save()` |
| Load world | Cards from the headers. Load is disabled, with a reason, for a damaged or newer save. |
| Delete world | `RkModal` ("cannot be undone") → `JsonWorldStore.Delete(slot)` → refresh the list |
| Campaigns | `GetCampaigns()` → Begin → `Dispatch(StartCampaignCommand)`. When a campaign cannot begin, the button is disabled and the `Why` is shown. |
| Reload (map top bar) | `RkModal` ("everything since will be lost") → the existing load path |
| Settings | `RkSettings` setters, which apply at once |

## Performance
- **Tooltips:** run on pointer events, with a single scheduled show. Nothing runs every frame.
- **Icons:** loaded once and cached, at 256 px or less. The panel's dynamic-atlas sub-texture limit was raised from 64 to 256 px, so they batch.
- **Atlas:** symbols are plain child elements, rebuilt only in `PlaceMarks()`, which already ran on refresh and geometry change.
- **Title:** builds a page when it is shown. The save list reads at most 4 KB per file.
- **No measurable cost** to the simulation. Nothing in Domain, Simulation or the day loop changed.

## What stays as it was (by design)
- **The character creator and advancement windows** are only restyled through the theme. Phase 20 owns character creation.
- **The combat interface** keeps the combat-overhaul layout (grouped bar, click-to-move). Phase 19 adds icons and states only.
