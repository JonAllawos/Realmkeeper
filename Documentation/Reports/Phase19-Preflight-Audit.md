# Phase 19: Preflight audit

**Date.** 2026-11-12.
**Branch.** `feature/phase-0-1-foundation`.

**Commits checked before the audit:**
- Phase 18 implementation: `713b9a0`.
- Phase 18 documentation: `c3dcd7e`.
- Phase 19A research: `6c49d3a`.

All three are local only, three commits ahead of `origin/feature/phase-0-1-foundation`; nothing was pushed. The working tree was clean apart from the owner's preserved, unstaged files:
- `ProjectSettings/GraphicsSettings.asset`;
- the ignored dev screenshots;
- the Unity `InitTestScene*` files;
- the local-only Quaternius Bestiary.

## 1. Framework and structure

| Area | Finding |
|---|---|
| UI framework | UI Toolkit (ADR-0001), Unity 6000.0.47f1. There is no uGUI in screens; the 3D map labels are mesh-based. |
| Input | Unity Input System. Exploration uses named actions (`ExplorationInput`); the other shortcuts are polled with `Keyboard.current` in the controllers. |
| Documents | There are two `UIDocument`s, the strategic HUD (`RealmkeeperHud`, UXML plus C#) and the exploration HUD (`ExplorationHud`, a UXML shell). All exploration windows (hub, combat, dialogue, trade, creator, advancement) are built in C# as `VisualElement` trees inside the exploration document. |
| Screen management | There is no screen manager. Each window is a class owning a root element, shown and hidden with the `xk-hidden` / `rk-hidden` classes. The exploration controller arbitrates which window is open (`WindowOpen`, `CloseWindows`). The runtime switches strategic and exploration views by `ViewMode`. Phase 19 keeps this; it does not add a second system. |
| Styles | `RealmkeeperHud.uss` (909 lines) and `ExplorationHud.uss` (1,433 lines). Every colour is a literal (about 420 occurrences, 180 distinct). One theme (`RealmkeeperTheme.tss`) imports Unity's default only. |
| Panel | `RealmkeeperPanelSettings`: scale with screen size, 1920×1080, match 0.5 (width or height). At 1366×768 the scale is 0.71, so 14 px text renders at about 10 px. |
| Data flow | Screens read query snapshots (`WorldSession.Get*`, `PlayerHub`, `CombatSnapshot`, `DialogueView`) and send commands (`Dispatch`). Refresh is event-driven (`WorldChanged`, playback, selection) and does not poll. No presentation code mutates `WorldData` directly. |
| Fonts, icons, textures | Unity's default runtime font only. **No icons, no UI textures, no shaders for UI.** |

## 2. Screens and their implementation

| Screen | Class | State before Phase 19 |
|---|---|---|
| Title, world selection, world creation, settings, loading | none | **Missing.** `RealmkeeperBootstrap` starts a world named "Realmkeeper" with seed 123456789. Save and Load in the top bar use the active slot ("autosave"). |
| Strategic map: top bar, navigation, settlement, faction and NPC inspectors, events, simulation | `RealmkeeperHud` (+ `.Politics`, `.People`, `.Places`, `.Structures`, `.Atlas`) | Functional. Text-only: no icons, flat dark panels. |
| World atlas | `RealmkeeperHud.Atlas` (Painter2D) | Functional, with layers, routes, travel and groups. Regions are drawn as flat squares, and unknown regions as empty grey boxes. Two lines of legend text sit under the map. |
| Exploration HUD | `ExplorationHud` + controller | Functional. Developer-style action strip (Pause, ×1, Wait, Hub, Party, Practice fight, Practice vs caster) and a key-hint line. |
| Player hub (13 sections) | `PlayerHubWindow` (+ `.Items`, `.Spells`, `.Party`, `.Journal`, `.Discoveries`, `.Campaign`) | Functional. A text-only navigation column. The journal has 11 sub-tabs that wrap onto two rows. Large empty area in the content pane. |
| Combat | `CombatWindow` (+ `.Spells`, `.Reactions`) | Functional (combat overhaul: grouped action bar, click-to-move, compact HUD). Three separate boxes; no icons for action, bonus and reaction (colour plus word). |
| Dialogue | `DialogueWindow` | Functional. Numbered responses with check tags. |
| Trade, creator, advancement | `TradeWindow`, `CharacterCreatorWindow`, `CharacterAdvancementWindow` | Functional. The creator uses Unity's white text fields. |

## 3. Reusable components found
- **Buttons:** `.rk-button` / `.xk-button`, two near-identical copies in the two stylesheets, each with a `--primary` variant.
- **Bars:** `.rk-bar`, `.xk-party-hp`, `.xk-inv-bar` and `.xk-xp-track`, four separate implementations.
- **Rows and other shared classes:** rows (`.xk-w-row`), titles (`.xk-w-title`), badges (`.rk-badge`), dividers (`.rk-divider` / `.xk-divider`).
- **No shared tooltip, modal, chip, card or icon component** existed.

## 4. Controls traced, and the problems found

Each finding below was traced to its handler. None was assumed.

| # | Control | Finding | Phase 19 action |
|---|---|---|---|
| 1 | Every `tooltip="…"` (UXML) and `.tooltip =` (C#), dozens across both HUDs | **Never visible in the game.** UI Toolkit shows `tooltip` only in the Editor. The explanations written since Phase 2 (structures, politics, people, routes) were dead text. | Fixed: `RkTooltips` shows them at run time. |
| 2 | Navigation: "Characters", "Adventures", "Chronicle", "World Editor" | Disabled buttons labelled "Coming later": placeholders suggesting features | Removed (they are listed on the roadmap) |
| 3 | Top bar **Load** | Reloads the slot at once. **Everything since the last save is lost, without warning.** | Fixed: a confirmation that says so |
| 4 | Speed dropdown; creator name field | Unity's light default field and popup on a dark interface | Fixed: theme overrides for all built-in fields, dropdowns, toggles, sliders and scrollers |
| 5 | Atlas shortcut "A" (`RealmkeeperHud.Atlas` KeyDownEvent) | Works only while a HUD element has keyboard focus, and the same key pans the camera left | Documented. The button stays the reliable way in. |
| 6 | Text at 1366×768 | 10 px body text | Fixed: Auto interface scale (1.25 up to 800 px tall, 1.1 up to 1000) and a manual setting |
| 7 | World start | No way to name, seed or choose a world, and no way to see saves | Built: title, world selection and world creation, on existing commands |
| 8 | Settings | None in the game | Built: only working settings; the rest stated as not available |
| 9 | Character creator "◇ placeholder body (final model pending)" | A placeholder label in a shipped screen | Left as is: the creator belongs to Phase 20 (brief §8), and the label is truthful |
| 10 | Exploration action strip "Practice fight (F)" / "Practice vs caster (G)" | Developer tools in the player HUD | Kept (they are working features the playtests use), restyled as quiet buttons |

## 5. Data dependencies (unchanged by Phase 19)
- **Strategic HUD:** `GetOverview`, `GetSettlementsInView`, `FindSettlement`, `FindRegion`, the politics, people and places queries, and `GetAtlas` (with the knowledge rules applied in the query).
- **Hub:** the `PlayerHub` snapshot, `CharacterSnapshot`, item, spell and party queries, `GetCampaigns`, and the journal queries.
- **Combat:** `CombatSnapshot` / `CombatQueries`.
- **Title (new):**
  - `JsonWorldStore.ListSaves` (file headers only) and `Delete`;
  - `WorldSessionHost.StartNew` / `StartFromSave`;
  - `RealmkeeperRuntime.Save` / `Load` / `SelectSlot`;
  - `GetCampaigns`, `StartCampaignCommand`.

## 6. Assets and licences
- **Before:** 3D only (Quaternius, KayKit and ambientCG, all CC0; Poly by Google, CC BY 3.0).
- **Phase 19 adds** (Phase 19A register):
  - game-icons.net (CC BY 3.0, 142 icons);
  - Kenney Fantasy UI Borders and Cartography Pack (CC0, used subsets);
  - EB Garamond and Alegreya Sans (SIL OFL 1.1, approved by the owner 2026-11-12).

## 7. Constraints
- Domain and Simulation stay free of `UnityEngine`. The new save listing lives in Infrastructure, which is engine-free and covered by the .NET harness.
- UI changes go through existing commands. The only new data operations are listing and deleting save files: file operations in Infrastructure, behind a confirmation.
- No per-frame UI rebuilding. Tooltips run on pointer events, and the title builds a page once when opened.
- Unity 6.0 UI Toolkit has no box-shadow, gradients, filters or SVG (without a preview package), so icons are tinted PNGs.

## 8. Implementation sequence (as carried out)
1. Assets with licences and import rules.
2. Tokens and shared components; theme overrides; shared runtime panel and scale.
3. Shared UI assembly (`Realmkeeper.UI`): icons, tooltips, modal, settings.
4. Entry screens: title, worlds, new world, campaigns, settings, credits.
5. Strategic HUD and inspector.
6. Atlas.
7. Exploration HUD, hub, dialogue, combat.
8. Tests, captures at two resolutions, full suites, documents.
