# Phase 26: interface completion

**Context.** Phase 25 gave every screen the shared system ("Cartographer's Ledger" v2, accessibility, width classes, components). Phase 26 finished the screens whose layout and interaction were still the old ones.

**Status words.**
- **Complete:** the layout and the interactions were redesigned, tested and captured.
- **Improved:** targeted changes to a sound existing design.
- **Unchanged:** shared styling only.

| Screen | Phase 26 status | What changed | Element names kept for tests |
|---|---|---|---|
| Trade | **Complete** | Four pages with counts: Buy, Sell, Particular goods, Mending. Icon rows with the refusal or shortfall reason written on the row. The shared inspector shows the price made factor by factor, the item's numbers, the rules engine's comparison with what is worn (new read-only `CompareCatalogItem`), a quantity stepper (bounded by stock, purse or what is carried), the total and the purse after, and one confirm button with its disabled reason. The shop's trade, shortfalls, supply and recent trades sit under the inspector; the side column scrolls | `xk-trade-sale-*`, `-offer-*`, `-goods-*`, `-repair-*`, `xk-trade-status`, `xk-trade-forsale`; the first button in a sale row is still Buy 1 |
| Crafting | **Complete** | Category chips with counts, Can make now, search. Rows with the output, or the one problem. The recipe inspector shows each material have/need with a tick or a cross, tool, station, check and chance, time, and Start or why not. Gathering, mending and attunement unchanged below | `xk-craft-<id>` with Start; `xk-gather-*`; `xk-work-active` |
| Party | **Complete** | List and inspector: you first, then companions with health meters and status; the selected member in the inspector (sheet, ability tiles, loyalty in words, items, give and take). Actions unchanged | `xk-party-head`, `xk-party-card-<id>`, `xk-party-detail`, `xk-party-abilities` |
| Codex | **New** | Hub ▸ Codex (ADR-0117); see `Phase26-Codex.md` | — |
| Dialogue | **Improved** | The speaker's emblem; every response has an icon for its kind (check, persuasion, intimidation, deception, trade, quest, decision, leaving) beside the unchanged words and brackets. Outcomes untouched | `xk-dialogue-response-*`, `xk-dialogue-why-*` |
| Combat | **Improved** | Condition and concentration badges on the selected combatant; an outcome banner (Victory, Defeat, Withdrawn, Nobody left standing, Called off) when the fight ends; health lines (Phase 25). Rules untouched | `xk-combat-turn` still reads "The fight is over" |
| Region map (N) | **Improved** | A key (you, settlement, place, landmark, waypoint, way on) and the scale line ("One square = 40 m · the region is 5.1 × 5.1 km"). Mark labels raised to the 12 px minimum | `xk-region-map`, `-image` |
| Character creation | **Unchanged in Phase 26** | The Phase 20/22 creator (steps, cards, build panel) is kept; shared styling only. Captured at 1920, 1280×800 and 1024×768 | — |
| Advancement | **Unchanged in Phase 26** | Phase 22 behaviour kept exactly; shared styling only | — |
| Strategic map panels | **Unchanged in Phase 26** | Inspector, factions and events as before; shared styling only | — |

## Shared components reused

`RkInspector`, the master-detail classes (`rk-md`), list rows (`rk-row`), `RkIcons`, the toasts, `RkRoot` width and accessibility classes, and the new `RkText` string table.

**No new one-off controls.** Two small classes were added: `xk-trade-stepper` for quantity, and `xk-craft-mat` for material lines.

## Translation readiness (`RkText`)

**The table.** `Realmkeeper.UI.RkText` holds key → English, with `Get(key)`, `Format(key, args…)` (numbered placeholders, invariant culture) and `Overlay(dictionary)` for a future translation. An unknown key shows itself.

**Migrated:**
- trade;
- crafting;
- codex;
- party (empty and headings);
- the pause menu and its confirmations;
- toasts;
- the region map key;
- combat outcomes;
- dialogue tags.

That is 124 strings.

**Not migrated (remaining):**
- the settings window;
- the title screen;
- the hub's other sections;
- the creator and advancement;
- the strategic HUD;
- the notice texts (`PlayerNotices`, Application layer);
- every message written by commands (Application).

**Not claimed:** the game is not translation-ready. Game data (names, descriptions, quest and dialogue text) belongs to its catalogues and is out of scope.
