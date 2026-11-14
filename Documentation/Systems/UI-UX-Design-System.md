# UI/UX design system (version 2, Phase 25)

**Identity.** "Cartographer's Ledger", chosen by the owner in Phase 19 (`Documentation/Design/Phase19-UI-Design-System.md`):
- dark umber ink for the working interface;
- parchment for maps and documents;
- one bronze accent;
- an engraved corner on windows only.

**Phase 25 did not change the direction.** That would be an owner decision. It completed the system so every screen can use it, and made it accessible and responsive. Phase 19's colours, typography, components and map tokens all still apply; this document lists what version 2 adds and the rules that now hold everywhere.

## Principles

1. **One language.** A screen uses the tokens (`var(--rk-*)`) and the shared components; it adds no literal colours or sizes.
2. **Words first.** Every state is said in words, with an icon. Colour only helps: "▲ better" in green, "Studied" as a green badge, "Hungry" as an ochre badge.
3. **Only real data.** Nothing on screen is invented. Every number comes from a query, and every button runs a real command. What does not exist yet is said ("Unknown until you have defeated one", "Music volume: the game has no music yet").
4. **Contextual, not permanent.** The HUD shows what matters now: vitals, the place, the time, notices. Detail is one click away (hub, inspector, tooltips).
5. **The player's clock.** Menus that stop the game say so ("the world's clock is stopped while this menu is open").

## Tokens added in version 2 (`RealmkeeperTokens.uss`)

| Token | Values | Use |
|---|---|---|
| `--rk-fs-10` … `--rk-fs-30` | One per size the screens used; 10 and 11 now map to **12 px** | Every `font-size` in the screen sheets (147 of them) now goes through these, so the Text size setting reaches every screen. No text is below 12 px at 1080p |
| `--rk-quality-poor/common/good/fine/rare` | Muted, text, green, blue, violet | Item quality, always beside its word |
| `--rk-focus` | Bronze-high | The keyboard focus ring on every control |
| `--rk-toast` | Near-opaque ink | Notification surfaces |

## Accessibility variants (classes on each document root, applied by `RkRoot`)

| Class | Setting | Effect |
|---|---|---|
| `rk-text-115`, `rk-text-130` | Settings ▸ Accessibility ▸ Text size | Every type token and the base size grow (115 %, 130 %); panels keep their size and wrap. Separate from Interface scale, which still scales everything |
| `rk-contrast` | High contrast | Brighter secondary and muted text; stronger lines; opaque panels and toasts; darker scrims |
| `rk-reduced-motion` | Reduce motion | No transitions anywhere (`.rk-reduced-motion * { transition-duration: 0s; }`); toasts appear without sliding |

## Width and height classes (responsive layout)

`RkRoot` measures each document root in panel pixels, after the interface scale:

| Class | When | What screens do |
|---|---|---|
| `rk-w-compact` | narrower than 1400 | Inspectors 340 px; the HUD's standing actions wrap; notices move under them |
| `rk-w-narrow` | narrower than 1150 | Master-detail stacks (the inspector goes under the list); wide cells are hidden; the controls line and the pause menu's notice column are hidden |
| `rk-w-wide` | 2300 or wider (ultrawide) | Reserved; layouts are max-width bound, so nothing stretches |
| `rk-h-short` | shorter than 760 | Tighter padding; the controls line is hidden |

**Measured classes** (Auto interface scale):

| Screen | Class |
|---|---|
| 1920×1080, 1366×768, 1280×720 | regular |
| 2560×1440, 3440×1440 | regular (layouts are max-width bound) |
| 1024×768 | compact |

## Components (version 2 additions)

| Component | Type / class | Rules |
|---|---|---|
| Notifications | `RkToasts` (`.rk-toasts`, `.rk-toast`, `--<kind>`, `--important`) | At most 4 visible. The same notice is not repeated within 45 s. More than 8 waiting fold into "N more". Leaves after 6 s (10 s if important) or on a click. History of 40, shown in the pause menu |
| Pause menu | `RkMenuOverlay` (`.rk-pause`, `.rk-pause-menu`, `.rk-pause-recent`) | Resume first and focused. Destructive entries last and confirmed. Disabled entries show their reason. Esc resumes; arrows and Enter navigate |
| Inspector | `RkInspector` (`.rk-inspector`, `-head`, `-stat`, `-compare`, `-section`, `-note`, `-actions`, `-why`) | Header (icon, name, subtitle, word badges), then statistics, comparisons, text, actions. Comparisons say "better", "worse" or "no change". Disabled actions give the reason. One inspector serves items, equipment and creatures |
| Master-detail | `.rk-md`, `.rk-md-list`, `.rk-md-detail` | List left, inspector right; stacked when narrow |
| List row | `.rk-row`, `.rk-row-icon`, `-main`, `-name`, `-sub`, `-cell`, `--selected` | Icon in a square frame, name with its category under it, right-aligned cells; the row itself selects |
| Vitals | `.rk-vitals` (`xk-vitals`) | Name, hit-point meter (green above half, ochre above a quarter, red below), "HP · AC · hunger · level" in words, condition badges |
| Action bar | `.rk-actionbar` (`xk-actionbar`) | The exploration actions, centred above the controls line; wraps instead of overflowing |
| Interface sound | `RkUiSound` | Click (buttons), select (tabs and chips), open/close (menus), notify (toasts), toggle (settings). Never the only feedback; volume = sound × interface sound |
| Health line | `CombatWindow.HealthLine` | A painted 3 px bar on turn-order cards; the numbers stay in the text |

## Interaction states (all screens)

| State | Treatment |
|---|---|
| Default | `--rk-control` fill, `--rk-line` border |
| Hover | `--rk-control-hover`, stronger line |
| Focused (keyboard) | 2 px `--rk-focus` border on every button, chip, tab and hub tab |
| Selected | Bronze border (rows: `rk-row--selected`; tabs: underline) |
| Active (in use) | Bronze fill (`rk-btn--primary`, `xk-w-choice--on`) |
| Disabled / unavailable | 45 % opacity, with the reason in the tooltip and, in menus and the inspector, written under it |
| Warning | `rk-badge--warn` / `rk-inspector-note--warn`, with words |
| Error | `--rk-bad` text with the reason; destructive buttons name the action |
| Success | `rk-badge--good`, the "Saved" toast |

## Information hierarchy

| Level | Where |
|---|---|
| Primary | The screen's title (serif); the list; the inspector's name and first statistics |
| Secondary | Category lines under names (`rk-row-sub`), the inspector subtitle |
| Contextual | Tooltips (450 ms), the inspector's notes |
| Advanced statistics | Inspector sections (Strength, Attacks), the sheet's breakdown tooltips |
| Description and lore | The inspector's text; lore in the serif italic (`rk-lore`) |
| Warnings and requirements | Notes in the warning tone, and disabled reasons |
| Hidden or unknown | Not shown, or said to be unknown ("Unknown until you have defeated one"; "Nothing worn") |
