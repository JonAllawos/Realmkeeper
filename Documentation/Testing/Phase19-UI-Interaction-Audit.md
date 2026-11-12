# Phase 19: UI interaction audit

## How each control was checked

| Code | Method |
|---|---|
| **T** | An automated PlayMode test drives the real control (`Phase19PlayModeTests`, `Phase19ScenePlayModeTests`, `SessionRestartPlayModeTests`, or an earlier phase's UI test, named) and checks the outcome. |
| **C** | Seen working in a genuine Unity capture (`Documentation/Screenshots/Phase19/`), driven by `Phase19CaptureTests`. |
| **R** | Traced in code from the control to its command or query. This is not executed by a test in Phase 19. |
| **—** | Not verified in Phase 19. |

**No control was verified by a human.** No hand-played session took place (see the completion report).

The columns follow the brief's checklist:
- **Visible and readable:** "Readable".
- **Label says its purpose:** "Label".
- **Enabled state correct, and disabled state explained:** "Enabled / why".
- **Action works and data is accurate:** "Works".
- **Cancel and invalid input:** "Cancel / invalid".
- **No duplicate or unintended mutation:** "Once".
- **Returns correctly:** "Returns".

## Title screen (new)

| Control | Readable | Label | Enabled / why | Works | Cancel / invalid | Once | Returns | Check |
|---|---|---|---|---|---|---|---|---|
| Continue (launch) | ✓ | ✓ | Disabled with "No saved world yet" when there are no saves | Loads the newest readable, compatible save | — | ✓ | Closes to the world | C, R |
| Resume (in game) | ✓ | ✓ | Always | Closes | — | ✓ | Map | **T** |
| New world → name, seed, New seed, Starting speed, Found this world, Back | ✓ | ✓ | Found is disabled while the name is blank | Starts the world with that name and seed, its own slot, saved at once | A blank name, or a seed that is not a number: refused with a message, nothing changes | ✓ | Map | **T** (name, seed, slot, save, bad input) |
| Found over a running game | ✓ | ✓ | — | Confirmation first | Cancel keeps the game | ✓ | — | R |
| Load world: cards, Load, Delete | ✓ | ✓ | Load is disabled with a reason for a damaged or newer save | Load loads the slot; Delete confirms, deletes and refreshes the list | Cancel keeps the world | ✓ | Map / list | **T** (list, cancel, delete, refresh); C |
| Campaigns: Begin | ✓ | ✓ | Disabled with the campaign's own `Why` | `StartCampaignCommand` | — | The command refuses a second start | Page | C, R |
| Settings | ✓ | ✓ | — | Opens settings | Esc / Close | — | Title | C, R |
| Credits | ✓ | ✓ | — | SRD notices, then every asset with its licence | — | — | Back with Esc | C |
| Exit to desktop | ✓ | ✓ | In the editor it says to stop Play mode instead | `Application.Quit` in a build | — | — | — | R |
| Esc | — | — | — | Back a page; on the in-game menu, Resume | — | — | ✓ | R |

## Settings (new)

| Control | Works | Check |
|---|---|---|
| Display mode, Resolution | `Screen.SetResolution`. In the editor the Game view keeps its own size, and the status line says so. | R (not testable in batch: the display is 640×480) |
| Quality | `QualitySettings.SetQualityLevel`, keeping the player's vSync | R |
| Vertical sync | `QualitySettings.vSyncCount` | R |
| Interface scale | All documents scale (one shared panel); Auto follows the screen height | **T** (130 %, then Auto) |
| Close / Esc | Closes and returns focus | **T** |
| "Not available yet" | Words, not controls: audio, rebinding, colour-blind palettes, separate text size | **T** (listed) |

## Strategic map

| Control | Change in Phase 19 | Check |
|---|---|---|
| Play/Pause, +1 day/week/month, Speed | Icons, tooltips; unchanged commands | Earlier HUD tests (`SelectionAndHudTests`, `Phase2HudPlayModeTests`, `EconomyHudTests`); C |
| Save | Icon | `SessionRestartPlayModeTests` (T) |
| Load | **Now asks first** ("everything since will be lost") | **T**: the dialog opens, nothing reloads until confirmed, then the reload happens |
| Politics | Icon | Earlier politics tests; C |
| Settings (new) | Opens settings | **T** |
| Menu (new) | Opens the title as a pause menu | R, plus **T** via `Title.Open(false)` |
| Navigation: World, World atlas, Settlements, Places, Factions, People | Icons | Earlier HUD tests; C |
| "Characters / Adventures / Chronicle / World Editor (Coming later)" | **Removed** (they were disabled placeholders) | **T** (absent) |
| Inspector: close, Enter settlement, Manage | Icons on sections, serif names | Earlier tests (`CombatDriver.EnterTownAsFighter` presses Enter); C |
| Every `tooltip` | **Now shown at run time** | **T** (`RkTooltips.ShowFor`) |

## World atlas

| Control | Change | Check |
|---|---|---|
| Layer chips (Terrain, Settlements, Roads, Factions, Economy, Threats, Events, Travel) | Icon chips; on and off states | **T** (a toggle round trip, the icon); C |
| Settlement, region, group and event marks | Drawn on parchment; settlement marks carry tooltips | Earlier atlas tests (Phase 16 and 17); C |
| Travel actions in the side panel | Unchanged | Earlier tests |
| Close / A key | Unchanged. **A works only while a HUD element has keyboard focus, and also pans the camera.** | Known, documented |

## Exploration HUD, hub, dialogue, combat

| Control | Change | Check |
|---|---|---|
| Play/Pause, ×1, Wait, Hub, Party, Create, Level up | Icons | Earlier exploration tests; C |
| Practice fight, Practice vs caster, Go out | Quiet style (developer tools that stay working) | Earlier combat tests; C |
| Save (F5), Return to World Map (M) | Icons, tooltips | Earlier tests; C |
| Hub tabs (13) | Icon per section, serif title | **T** (icons, Inventory tab through its button); C |
| Hub content (sheet, inventory, equipment, crafting, spells, journal, party) | Restyled through tokens; behaviour unchanged | Earlier phase tests; C |
| Dialogue responses | Serif spoken line; unchanged behaviour | Earlier dialogue tests; C |
| Combat action, bonus and reaction pips | An icon each; used = dimmed and italic, with a tooltip | **T** (icons, width); C |
| Combat action bar, log, turn order, confirm | Unchanged layout (combat overhaul) | `CombatOverhaulPlayModeTests`; C |

## Shared dialog (`RkModal`)

| Behaviour | Check |
|---|---|
| Esc cancels without acting | **T** |
| Confirm runs the action once, even if pressed twice | **T** |

## Known gaps
- **Keyboard-only navigation** through the title and settings (Tab focus order) was not tested.
- **The display-mode and resolution changes** were not exercised: batch mode has no real display.
- **Tooltip placement** at screen edges was checked in code only.
- **The character creator and advancement windows** were restyled through the theme only. Phase 20 owns them.
