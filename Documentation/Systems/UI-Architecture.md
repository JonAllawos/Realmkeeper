# UI architecture (Phase 25)

**Earlier documents.** Phase 19's `Documentation/Design/Phase19-UI-Architecture.md` still describes the base; this document is the current whole.

## Technology

**UI Toolkit only.**
- No screen uses uGUI or IMGUI, and Phase 25 kept it so: a hybrid had no justification.
- There are no runtime web or online dependencies; fonts, icons and sounds are in the project.

## Documents and layers

| Document | Sorting order | Holds |
|---|---|---|
| Strategic HUD (`RealmkeeperHud`, `RealmkeeperHud.uxml`) | default | Top bar, navigation, settlement, faction and people inspectors, atlas, politics, events. Toasts (map notices) |
| Exploration HUD (`ExplorationHud`, `ExplorationHud.uxml`) | 10 | Location and vitals (left column), standing actions (top right), prompt, responses, action bar, controls line, toasts. Every exploration window lives in this document: hub, creator, advancement, trade, dialogue, combat, region map, pause menu |
| Title and loading screens | 100 | Main menu pages, settings; loading |

**Shared panel and theme.**
- All documents share one runtime `PanelSettings` (`RkSettings.Panel`): reference 1920×1080, match 0.5, times the player's interface scale (Auto: 1.25 up to 800 px tall, 1.1 up to 1000 px).
- The theme imports the tokens and the components sheet.

## Shared library (`Realmkeeper.UI` assembly, no game references)

| Type | Job |
|---|---|
| `RkSettings` | Every player setting, kept in PlayerPrefs (never in a world save): display, quality, vSync, sound volume, interface-sound volume, interface scale, text size, high contrast, reduced motion. `Changed` is raised on any change |
| `RkRoot` | Per document root: accessibility classes and width/height classes, kept current on settings changes and resizes |
| `RkIcons` | Meaning → icon (166 keys, 153 files), tinted; `Lead` puts an icon before a text without breaking its measurement |
| `RkTooltips`, `RkModal` | Tooltips; the one confirmation dialog |
| `RkToasts` | Notification stack (bounded, deduplicated, with history) |
| `RkMenuOverlay` | The pause menu |
| `RkInspector` | The shared inspector builder |
| `RkUiSound` | Interface sounds; attaches once per document root |
| `RkHooks` | Actions one layer offers another without a reference (the main menu, set by the strategic bootstrap, called by the exploration pause menu) |
| `RkSettingsWindow` | The settings screen, opened from the title, the map and the pause menu |

## Screen state and data binding

The rule is unchanged since Phase 2, and was audited again in Phase 25:
- **Windows render snapshots.** A window renders a snapshot from a `WorldSession` query (`GetPlayerHub`, `GetInventory`, `GetBestiary`, `GetJournal` …) and raises an event, or passes a `Func<WorldSession, CommandResult>`, for every action.
- **Controllers run real commands.** The controller dispatches the real command and re-renders the window from a fresh query.
- **No UI-side game state.** No window keeps game state of its own. Selection, filters and the open tab are view state only, and are never saved.
- **Transactions stay in the domain.** Item transfers (drop, store, take, split, equip) are single commands, validated by the domain and refused whole if invalid. The inspector cannot duplicate or destroy items, because it never edits data itself.

## Navigation and input

**Global.**
- **Esc** closes the open window or conversation. With nothing open, it opens the pause menu (exploration).
- On the map, **Menu** opens the title menu as the pause menu (Resume, Load, Settings, Credits, Exit).
- The pause menu holds the game: the clock pauses, movement and the camera stop, and the previous state is restored on Resume.

**Keyboard.**
- UI Toolkit's own focus navigation (Tab, arrows, Enter) works on every button.
- The pause menu adds Up/Down between entries.
- Every focusable control shows a 2 px bronze focus ring.
- Modals take focus, with Esc cancelling and Enter confirming.

**Not supported.**
- Controllers: there is no gamepad code, and none is claimed. The settings say so.
- Rebinding: the controls are fixed and listed in Settings ▸ Controls.

## Notifications pipeline

```
World (after any command, a day on the map, a fight)
  → WorldSession.GetNoticeSnapshot()   what the player can know, reduced
  → PlayerNotices.Compare(before, now) pure rule: words, kinds, keys, ≤ 8 per change
  → RkToasts.Push(key, kind, title, body, important)   dedupe 45 s, ≤ 4 visible
```

- **Exploration** (`ExplorationController.Phase25`) checks twice a second; **the map** (`RealmkeeperHud.Phase25`) checks once a second while visible.
- **Fresh starts:** a load or a new world starts afresh (`ReplaceCount`), and so does arriving in a settlement.

## Lifecycle and performance

- **Windows are built once per visit and re-rendered on change,** not every frame.
- **Lists are not virtualised.** They hold tens of rows: inventory, bestiary, journal.
- **Measured per-frame work:** the notice timer and vitals timer (unscaled time), and the HUD's window check (a class toggle, only when the value changes).
- **Measured cost:** a notice check takes about 0.03 ms in the played scene, and the .NET test holds it under 25 ms.
- **Toast lifetime:** toasts remove themselves (scheduled) and never accumulate. The stack is cleared on arrival in a new place.
- **Icons** are loaded once each and cached.

## Localisation readiness

- There is no localisation framework yet; every string is in code.
- New Phase 25 strings sit in one place per component (the pause entries, the notice rule, the inspector labels), so a later extraction to string tables is mechanical.
- This is recorded as a known limit.

## Phase 26 additions

| Addition | What it is |
|---|---|
| `RkText` (`Realmkeeper.UI`) | The interface string table: key → English, `Get` / `Format` / `Overlay`. 124 strings so far; the remaining hard-coded areas are listed in `Phase26-UI-Completion.md` |
| Trade | Rebuilt on the list-and-inspector pattern. `TradeWindow.CompareCatalogSource` and `CompareOwnedSource` call the read-only `WorldSession.CompareCatalogItem` and `CompareItem`. `BuyQuantityRequested` and `SellQuantityRequested` carry a quantity into the existing `BuyItemCommand` and `SellItemCommand` |
| Crafting | `PlayerHubWindow.Crafting.cs`: the recipe browser and inspector over the existing `WorkshopView` |
| Codex | `PlayerHubWindow.Codex.cs` over `WorldSession.GetCodex` (ADR-0117) |
| Measured (editor, batch mode; `Phase26PlayModeTests`) | Hub section renders 0.6–25 ms (inventory with 66 rows: 25 ms, 759 elements). First hub open 282 ms (it builds the 3D preview); later open–close cycles about 35 ms including two frames. Trade open 7 ms. Codex search 1.8 ms. Repeated open and close do not accumulate elements |
