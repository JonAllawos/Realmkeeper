# Phase 25: preflight audit

**Phase:** Complete UI/UX Overhaul, Interface Architecture, Visual Identity & Third-Party Asset Integration.

**Audited:** 2026-11-14, against the code and a fresh capture of the current screens (the Phase 19 and Phase 22 capture tests re-run on HEAD, 41 images in the scratch folder). The earlier documentation was not taken on trust.

## 1. Git baseline

| Item | State |
|---|---|
| Project path | `C:\Dev\Realmkeeper`. The brief says `Documents/Projects/Realmkeeper`, but no project exists there; `C:\Dev\Realmkeeper` is the repository every phase has used |
| Branch | `feature/phase-0-1-foundation`, tracking `origin/feature/phase-0-1-foundation` |
| HEAD | `9360330` ("Docs: record the 2026-11-14 push of Phase 24"), after `e0df4dd` (Phase 24) |
| Synchronisation | Even with origin: no local commits and nothing to pull |
| Tracked changes | None |
| Untracked | `Assets/InitTestScene37e5c1f4-….unity` (+ `.meta`, 12 Nov) and `Assets/InitTestScene8aeb0c53-….unity` (+ `.meta`, 13 Nov) |
| Already tracked | `Assets/InitTestScenea160fc4b-….unity` (+ `.meta`, 10 Nov), committed in an earlier phase |

**The InitTestScene files.**
- **Inspected.** Each is an empty Unity scene with only the default occlusion, render and lightmap blocks and no game objects. Unity's test runner creates them for a PlayMode run, and they were left behind by runs that stopped early.
- **The two untracked files** are neither committed nor deleted (brief §2 and §25).
- **The tracked one** is left as it is: removing it from git would be a change to user-owned history that this phase does not need.
- **Recommendation for the owner:** add `Assets/InitTestScene*` to `.gitignore` and remove the tracked copy.

**Phase 24 carry-over:** its known limits are unchanged, and none of them is a reason to reopen Phase 24.
- It has not been hand-played.
- The footsteps have not been checked by ear.
- Helping refugees and a broken night's rest have no tests of their own.
- D1 (40 m a cell) is not approved.
- The isolated re-run of `Phase24PlayModeTests` met no road threat, so the journey step-off after a threat is still not exercised.

## 2. UI technology and architecture (as built)

**UI Toolkit only.**
- No screen uses uGUI. `com.unity.ugui` is installed, but no runtime file has `using UnityEngine.UI`.
- There is no IMGUI in the game.
- **Decision:** stay on UI Toolkit. A hybrid has no technical justification.

**Documents.** Three documents share one runtime `PanelSettings` (`RkSettings.Panel`: reference 1920×1080, match 0.5, the player's interface scale):

| Document | Sorting order |
|---|---|
| The strategic HUD (`RealmkeeperHud`, UXML plus code) | default |
| The exploration HUD (`ExplorationHud`, UXML plus code) | 10 |
| The title screen and the loading screen | 100 |

**Windows are built in code** inside the exploration HUD document:

| Window | Size |
|---|---|
| `PlayerHubWindow` (13 sections) | 2,000+ lines across five files |
| `CharacterCreatorWindow` | 956 lines |
| `CharacterAdvancementWindow` | |
| `TradeWindow` | |
| `DialogueWindow` | |
| `CombatWindow` | |
| the atlas, politics, people and places panels of the strategic HUD | |

**The design system (Phase 19, "Cartographer's Ledger", owner-chosen).**
- **Files:**
  - `RealmkeeperTokens.uss` (85 lines);
  - `RealmkeeperComponents.uss` (436 lines);
  - the theme;
  - `RkIcons` (142 game-icons.net silhouettes);
  - `RkTooltips`;
  - `RkModal`;
  - `RkSettings` and `RkSettingsWindow`.
- **Screen styles:** `RealmkeeperHud.uss` (1,094 lines) and `ExplorationHud.uss` (1,877 lines).
  - These still carry many literal colours and sizes.
  - There are two class vocabularies: the shared `rk-*` and the older per-screen `xk-*`.
- **Fonts:** EB Garamond and Alegreya Sans (SIL OFL 1.1).

**State and data.**
- Every window renders a snapshot from a `WorldSession` query and raises events.
- The controller dispatches the real command and re-renders, so no UI-only game state was found.
- This pattern is sound and is kept.

**Input.**
- The Input System package is used, keyboard and mouse only.
- **No gamepad code exists**, so no controller support can be claimed.
- UI Toolkit's own focus navigation (Tab, arrows, Enter) works on focusable controls.
- There is no rebinding.

**Localisation.**
- None. Every string is written in code; there is no Localization package and no string table.

**Accessibility (present):**
- interface scale (Auto, 90–130 %);
- states never shown by colour alone;
- minimum text 12 px at 1080p.

**Accessibility (absent):**
- a separate text size;
- a contrast option;
- reduced motion;
- control over UI sounds.

**Tests.**
- 57 PlayMode files, about 48 of which drive HUD or windows by element name (`xk-open-hub`, `xk-hub-tab-*`, `title-*`, `settings-*` and so on).
- **Any redesign must keep these names,** or change the tests only where the behaviour intentionally changes.

## 3. Interface inventory and findings

Assessed from the fresh captures (`p19_*`, `p22qa_*`) and the code.

| Screen | Exists | What works | Problems found |
|---|---|---|---|
| Title / main menu | Yes | Continue, New world, Load, Campaigns, Settings, Credits, Exit; disabled Continue explains why | Static left panel over the strategic map; no exit confirmation; good base |
| New world / load / campaigns | Yes | Real saves and campaigns | Plain lists; no slot details beyond name and date; delete confirmation exists |
| Pause menu | **No** | The strategic "Menu" re-opens the title screen | Esc in exploration only frees the cursor; there is no pause menu in a settlement, place or the country |
| Settings | Yes | Display, quality, vSync, volume, interface scale; controls list | **Stale text:** "Audio: the game has no sound yet" is wrong since Phase 20A. No text size, contrast, motion or UI-sound options |
| Credits | Yes | `Credits.cs` lists the third-party assets | Plain |
| Exploration HUD | Yes | Location, date, clock with weather tooltip, time controls, hub, party, practice fights, outings, save, return | **No vitals** (health, conditions). Ten text buttons in one row: at 1366×768 the row overflows its panel and "Return to World Map" is cut off the right edge. The location panel shows **through every hub window** (both are in the same document, and the HUD is not hidden) |
| Combat HUD | Yes | Initiative strip, action economy chips, actions, log, details | Initiative cards are small text boxes with no health bar; the log is clipped to three lines |
| Character creation | Yes (Phase 20/22) | Steps, choice cards, build panel, review | Already the most developed screen; compact mode below 1380 px |
| Character sheet | Yes | Seven sub-tabs, ability tiles, stat tiles, breakdowns | Good. The 3D preview takes 22 % of the width on every hub section, including those that don't need it |
| Advancement | Yes (Phase 22) | Player-driven, saved picks, apply once | Keep behaviour; visual refresh only |
| Inventory | Yes | Search, sort (category, name, weight, value, recent), category chips, capacity bar | **No item icons.** Each row carries 4–5 text buttons (Inspect, Equip, Store, Drop, …). Inspection is a separate expansion, and there is **no comparison with what is equipped** |
| Equipment | Yes | Slots, unequip | Text list; no comparison |
| Item inspection | Partial | An "Inspect" expansion in the row | No consistent inspector |
| Loot / containers | Yes (Phase 11) | Storage section, take/put | Text rows |
| Shops / trade | Yes | Buy/sell, prices, haggling text | Text rows, no icons |
| Crafting | Yes | Recipes, materials, stations | Text rows, no filter |
| Spellbook | Yes | Spells by level, prepare, rest | No filter or search; non-casters see only rest |
| Abilities | Partial | Features on the sheet; combat actions in the combat bar | No separate ability view (and there is no hotbar system to assign to) |
| Strategic map | Yes | Settlements, factions, places, people, inspector, events | Dense but working; good base |
| World atlas | Yes (Phase 16, 24) | Parchment map, journeys, real player position, waypoints | Good base |
| Region / overland map | Yes (Phase 23, 24) | N map, compass, waypoint line | — |
| Quest journal | Yes | Tabs: Campaign, Active, Available, Completed, Failed, Abandoned, Rumours, Discoveries, Decisions, Events, Places, Crimes, Reputation | **Thirteen tabs in one row**; the list and detail are not side by side |
| Codex / discoveries | Partial | Discoveries, Places, Events tabs in the journal | No codex of people, factions or creatures |
| Bestiary | **No** | `BestiaryModel` and the creature catalogue exist; combat shows a creature's bestiary text | **No bestiary screen, and no record of which creatures the player knows** |
| Dialogue | Yes | Speaker, line, topics, "About them" card | Good base |
| NPC inspection | Yes | People panel (strategic), "About them" (dialogue) | Different layouts for the same thing |
| Companions / party | Yes | Party section, companion sheets | Text rows |
| Factions / settlements | Yes | Strategic inspector, politics overlay | Good data; heavy text |
| Notifications | **No system** | One message line under the HUD, plus the interaction response panel | Nothing announces a quest update, discovery, level-up eligibility, new item or reputation change unless the player opens the journal |
| Tooltips | Yes | `RkTooltips` | Good |
| Confirmation dialogs | Yes | `RkModal` | Good; used by only a few destructive actions |
| Loading screen | Yes | | Plain |

## 4. Resolution behaviour (measured from the captures)

| Size | Finding |
|---|---|
| 1920×1080 | Works; HUD overlap under windows |
| 1366×768 | **Defects:** the top-right action panel is clipped, and the toolbar overflows the location panel |
| 1280×720 | Phase 22 captures of trade and crafting work; the HUD has the same defects |
| Below 1280×720 | Phase 22 documented these as unsupported |
| 2560×1440, ultrawide | Only atlas and combat were captured before; no ultrawide capture exists |

## 5. Performance (UI)

**Pattern.** Windows re-render their content (`Clear()` and rebuild) on every refresh.
- The hub re-renders when the player's body refreshes, about once a game hour or on an action.
- This is acceptable for the current list sizes (tens of rows) and is not virtualised.

**Rule kept.** No per-frame world queries were found in the UI. The exploration HUD's response timer is the only per-frame UI work.

## 6. Decisions for Phase 25

1. **Keep UI Toolkit and evolve the owner-approved Phase 19 direction,** not a new identity.
   - Phase 25 makes it complete and consistent: version 2 of the tokens, accessibility variants, and shared components for everything below.
   - Changing the visual direction itself would be an owner decision, so none is assumed.
2. **New shared components in `Realmkeeper.UI`:**
   - notification toasts (`RkToasts`);
   - UI sounds (`RkUiSound`);
   - a menu overlay for pause (`RkMenuOverlay`);
   - a reusable inspector card (`RkInspector`);
   - responsive width classes (`RkResponsive`);
   - a search/sort/filter bar (`RkFilterBar`).
3. **Notifications are derived, not hooked.** A pure Application rule compares two small notice snapshots of the world:
   - journal entries, quest states, discoveries, level-up eligibility, inventory, reputation and encounter outcomes;
   - it reports what changed, deduplicated and bounded;
   - it is testable in .NET, and no command handler is touched.
4. **Bestiary knowledge needs new state.**
   - Nothing records which creatures the player has met; the bounded combat history keeps only 20 fights.
   - Add `PlayerCharacterData.KnownCreatures` (save v27, additive). A creature is recorded when it takes part in a fight with the player, and again when it is defeated.
   - **Knowledge tiers:**
     - *met*: name, kind, habitat, behaviour;
     - *defeated*: armour, hit points, challenge and attacks, from the catalogue.
   - The v26→v27 migration seeds the record from the fights a save still holds.
   - This is an ADR (ADR-0116).
5. **A pause menu everywhere.**
   - **Where:** Esc in exploration (after closing open windows), and the strategic "Menu".
   - **Entries:** Resume, Save, Settings, Controls, Return to world map (exploration only), Main menu, Exit to desktop.
   - **It runs** the existing save, return and title commands.
   - **Confirmation:** the exits ask first, and say how long since the last save.
6. **Exploration HUD.**
   - **Layout:**
     - location top-left;
     - the actions moved to a bottom action bar that wraps;
     - vitals bottom-left (health, armour, conditions, hunger, level-up);
     - toasts top-right;
     - the standing actions top-right, shrinking.
   - **The HUD hides behind open windows.**
   - **Element names are kept** for the tests.
7. **The hub becomes a master–detail layout.**
   - Inventory, equipment, storage and crafting rows gain category icons.
   - The per-row buttons move into a selected-item inspector.
   - The inspector compares a wearable item with what is equipped, using the real rules. AC is recomputed on a cloned character with the item equipped; nothing is guessed.
   - The 3D preview shows only on the sections where it matters (Character, Appearance, Equipment, Clothing), and collapses on narrow screens.
8. **Journal.**
   - The thirteen tabs become grouped navigation: Quests, Knowledge, World, Standing, each with sub-filters.
   - The selected quest shows in a detail pane.
9. **New hub section: Bestiary.**
10. **Spellbook:** filter chips by level and a search, over the existing data.
11. **Responsiveness.**
    - Width classes on each document root: `rk-w-compact` below 1400 logical px, `rk-w-narrow` below 1150.
    - The test sizes are below; 1024×768 is the new floor.

    | Ratio | Sizes |
    |---|---|
    | 4:3 | 1024×768 |
    | 16:9 | 1280×720, 1366×768, 1600×900, 1920×1080, 2560×1440 |
    | 16:10 | 1280×800 |
    | 21:9 | 3440×1440 |
12. **Accessibility settings.**
    - **Text size** (100 / 115 / 130 %, through the type tokens and the base size).
    - **High contrast:** stronger lines and secondary text, opaque panels.
    - **Reduced motion:** transitions off.
    - **Interface sounds:** on or off, with their own volume.
    - **Fix the stale "Not available yet" text.**
13. **No controller claim.**
    - Menus and dialogs are checked for keyboard focus order.
    - Gamepad remains unsupported, and the settings say so.
14. **No localisation framework in this phase.**
    - A risk, recorded.
    - New strings are kept in one place per component, to ease a later extraction.

## 7. Risks

- **Scope.** The brief covers every screen. The bulk of the 12,000 lines of UI code cannot be rewritten in one phase without risking gameplay.
  - Priority goes to the defects above and to shared components that lift every screen.
  - Each screen's actual depth of change is reported honestly in the completion report.
- **Test coupling.** PlayMode tests address elements by name, so names are preserved.
- **Save v27.** It is additive and optional; v26 saves load with an empty bestiary, seeded from their recorded fights.
- **Third-party assets.** Only licences verified from the files themselves are adopted. Paid kits are listed for the owner, not bought.

## 8. Sequence

1. Research note and asset register, then integrate verified free assets: Kenney Interface Sounds (CC0) and further game-icons.net silhouettes for item categories (CC BY 3.0).
2. Tokens v2, accessibility variants, responsive classes; the shared components.
3. Pause menu, notifications, exploration HUD, combat HUD touch-ups, settings.
4. Hub master–detail: inventory, equipment, storage, trade, crafting, item inspector and comparison.
5. Spellbook filters; journal regrouping; bestiary (v27); codex links.
6. Dialogue, party, strategic inspector touch-ups to the shared components.
7. Captures at every size; tests; full checkpoint; documents; local commit (no push).
