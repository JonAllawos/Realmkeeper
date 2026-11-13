# Phase 19: UI design system ("Cartographer's Ledger")

This is direction A from the Phase 19A research, chosen by the owner on 2026-11-12. It covers:
- dark umber ink for the working interface;
- parchment for maps and documents;
- one bronze accent;
- an engraved corner on windows only.

## Where it lives

| What | File |
|---|---|
| Tokens (colours, type scale, spacing, motion, base font) | `Assets/Realmkeeper/Runtime/Presentation/UI/Resources/RealmkeeperTokens.uss` |
| Shared components and the restyled built-in controls | `…/RealmkeeperComponents.uss` |
| Theme (imports both, for every panel) | `…/RealmkeeperTheme.tss` |
| Screen styles, now on tokens | `RealmkeeperHud.uss`, `ExplorationHud.uss` (each ends with a "Phase 19" block) |
| Icons | `Realmkeeper.UI.RkIcons` (meaning → file), `Tools/UI/icon-manifest.tsv` |
| Tooltips, confirmation dialog, settings | `Realmkeeper.UI.RkTooltips`, `RkModal`, `RkSettings`, `RkSettingsWindow` |

**Rule.** A screen uses `var(--rk-*)` and the shared classes. New literal colours are not added. The 188 one-off literals that remain in the two screen stylesheets are mostly combat-grid and politics accents; they are listed by `Tools/UI/tokenize_uss.py` and are to be folded in as those screens are touched.

## Colour

| Token | Value | Use |
|---|---|---|
| `--rk-ink-950` | `rgb(16,13,10)` | Meter troughs, the darkest ground |
| `--rk-ink-900` | `rgb(22,18,14)` | Field inputs |
| `--rk-ink-800` | `rgb(30,25,19)` | Panel base |
| `--rk-ink-700` / `-600` / `-500` | `rgb(40,33,26)` / `rgb(52,43,33)` / `rgb(70,58,44)` | Cards, hover, scroll thumbs |
| `--rk-window` | `rgba(22,18,14,.97)` | Windows and modals |
| `--rk-panel` | `rgba(28,23,18,.93)` | HUD panels over the 3D world |
| `--rk-card` | `rgba(40,33,26,.92)` | Cards and list rows |
| `--rk-control` / `-hover` / `-active` | `rgba(46,38,30,.95)` / `rgba(62,51,39,.98)` / `rgb(84,68,50)` | Buttons |
| `--rk-line` / `-soft` / `-strong` | `rgb(75,62,47)` / `rgba(107,88,67,.45)` / `rgb(107,88,67)` | Borders, dividers, focus |
| `--rk-text` | `rgb(236,226,204)` | Primary text (≈14:1 on ink-800) |
| `--rk-text-2` | `rgb(194,181,156)` | Secondary text (≈8:1) |
| `--rk-text-3` | `rgb(150,138,118)` | Muted and disabled text (≈4.7:1) |
| `--rk-heading` | `rgb(232,206,156)` | Headings |
| `--rk-bronze` / `-hi` | `rgb(184,138,74)` / `rgb(221,176,108)` | The single accent: selection, the primary action, focus |
| `--rk-bronze-deep` / `-hi` | `rgb(110,82,44)` / `rgb(136,102,54)` | Primary button and selected-tab fills |
| `--rk-paper` / `-ink` / `-ink-2` | `rgb(228,213,180)` / `rgb(42,32,23)` / `rgb(96,78,58)` | Atlas and document surfaces |
| `--rk-good` / `-bg` | `rgb(140,182,112)` | Success, gains, healthy |
| `--rk-warn` / `-bg` | `rgb(222,163,70)` | Warnings, risk |
| `--rk-bad` / `-bg` | `rgb(226,122,100)` | Errors, damage, hostile, destructive |
| `--rk-info` / `-bg` | `rgb(140,170,222)` | Information, magic |
| `--rk-rare` | `rgb(170,140,214)` | Rare items, reactions |

## Meaning is never colour alone

| State | Treatment |
|---|---|
| Normal | `--rk-text` on ink |
| Important | `--rk-heading` and the serif face, or a `.rk-badge--info` |
| Warning | `.rk-badge--warn` with words ("Short of food"), the `warning` icon, ochre |
| Error | `--rk-bad` text with the reason in words; destructive buttons say the action ("Delete world") |
| Unavailable action | Dimmed to 45 %, with a tooltip giving the reason (combat: the "why" line; title: "No saved world yet") |
| Selected | Bronze border, with a 2 px bronze underline on tabs and a bronze fill on chips |
| Active effect | A condition name with its own icon (`poisoned`, `bleeding`, `exhausted`, `asleep`, `blinded`) |
| Persistent world change | The event list and the journal, with dates and the `events` icon |
| Used combat resource | Its own icon (action ⚔, bonus ✦, reaction 🛡), dimmed and italic, with the tooltip "used this turn" |
| Closed road (atlas) | Red **and crossed through**; a crossing is ochre **and thicker** |
| Unknown land (atlas) | Hatched parchment with the italic label "Uncharted" |

## Typography (SIL OFL 1.1, owner-approved)

| Role | Face | Size at 1080p | Class |
|---|---|---|---|
| Screen title | EB Garamond SemiBold | 28 | `.rk-title` |
| Section heading | EB Garamond SemiBold | 22 / 18 | `.rk-h1`, `.rk-h2`, and the window titles `.xk-w-title` (26) |
| Eyebrow | Alegreya Sans Medium, tracked 2 px | 12 | `.rk-eyebrow` |
| Body, controls | Alegreya Sans Regular | 15 | inherited from `:root` |
| Small, hints | Alegreya Sans | 13 / 12 | `.rk-small`, `.rk-hint` |
| Spoken line, empty states | EB Garamond Italic | 21 / 16 | `.xk-dialogue-line`, `.rk-empty` |
| Numbers | Alegreya Sans (lining figures) | as body | — |

The smallest text is 12 px at 1080p. With the Auto interface scale, it is never below about 11 physical pixels at 1366×768.

## Icons
- **Source.** One family, game-icons.net (CC BY 3.0): 142 white silhouettes, tinted by state with `.rk-icon--accent`, `--good`, `--warn`, `--bad`, `--info` and `--paper`.
- **Sizes.** `s` 14, default 18, `m` 22, `l` 30, `xl` 44.
- **Use.** An icon never replaces a label. `RkIcons.Lead` puts it in front of the text.
- **Map symbols.** The Kenney Cartography Pack (CC0), tinted in ink. These are the only icons drawn in a different style, and only on the parchment map.

## Components

| Component | Class or type | States |
|---|---|---|
| Button | `.rk-btn`, `--primary`, `--quiet`, `--danger`, `--icon`, `--menu` | hover, active, focus (bronze border), disabled (45 %) |
| Tab | `.rk-tabs` / `.rk-tab`, `--active` | hover, active underline |
| Chip (filters, layers) | `.rk-chip`, `--on` | hover, on |
| Card | `.rk-card`, `--selected` | — |
| Badge | `.rk-badge`, `--good`, `--warn`, `--bad`, `--info` | — |
| Meter | `.rk-meter` > `.rk-meter-fill`, `--good`, `--warn`, `--bad` | — |
| Statistic row | `.rk-stat` (icon, key, value) | — |
| Window | `.rk-window` with `.rk-frame` (9-sliced Kenney corner frame, tinted `--rk-line-strong`) | — |
| Divider | `.rk-rule` (Kenney fading divider) | — |
| Modal / confirmation | `RkModal` (`.rk-scrim` > `.rk-modal`) | Esc cancels, Enter confirms, the action runs at most once |
| Tooltip | `RkTooltips` (`.rk-tooltip`) | 450 ms delay, follows the pointer, kept on screen |
| Empty state | `.rk-empty` (serif italic, centred) | — |
| Built-in fields | `TextField`, `DropdownField`, `Toggle`, `Slider`, `ScrollView` restyled in the components sheet | hover, focus |

## Spacing, shape, motion
- **Spacing:** 4 px grid (`--rk-space-1…5` = 4, 8, 12, 16, 24).
- **Radius:** 3 px for controls, 4 px for windows; chips are pill-shaped.
- **Control height:** 32, with 36 for primary actions and 46 for title-menu entries.
- **Motion:** 120 ms on hover and colour (`--rk-fast`); 180 ms on panels (`--rk-slow`). Nothing animates continuously.

## Map tokens
- **Surface:** parchment (Kenney `parchmentBasic`) on `--rk-paper`.
- **Ink:** `rgb(42,32,23)` for text, with 45 % and 14 % ink for borders and hatching.
- **Region washes:** 24 % of the kind colour when visited, 14 % when only rumoured or discovered.
- **Faction influence:** a 30 % disc with a 70 % rim.
- **Roads:** open `rgb(92,64,38)` at 2 px; crossing `rgb(184,122,41)` at 3.5 px; closed `rgb(158,31,20)` crossed through.
- **Your journey:** `rgb(168,102,26)` at 4 px.

## Charts
`Painter2D` lines and fills on the tokens, as the existing prosperity and stability bars already do (`.rk-bar`). No chart library and no chart images.

## Phase 20 additions (character screens)

These use the same tokens, with no new design language.

| Element | Classes | Notes |
|---|---|---|
| Creator layout | `.xk-cc-body` > `.xk-cc-side` (preview, about 31 %, at least 340 px), `.xk-cc-content`, `.xk-cc-build` (300 px) | `.xk-cc--compact` below 1380 px of window width hides the build panel and shows the short summary under the preview |
| Step bar | `.xk-cc-step` (pill, 34 px, icon) with `--active` and `--done` | Done is green and active is bronze; the text says the step number and name |
| Choice cards | `.xk-cc-card` (name, then the gist on a second line) | Selected: a bronze border and fill |
| Build panel | `.xk-cc-build-name` (serif), `-what`, `-concept` (serif italic), `-head` (tracked small capitals style), `-row`, `-key`, `-value`, `-problem` (warn), `-ok` (good) | |
| Ability tiles | `.xk-cc-ability` (creator, small), `.xk-sheet-ability` (sheet and companions, 118 px) with `-score` (serif) and `-save` | Each tooltip gives the full breakdown |
| Species adjustment note | `.xk-cc-adjust` | Warn colours; says what changing species changed |
| Sheet tabs | `.rk-tabs` / `.rk-tab` (Phase 19 shared tabs) with icons | |
| Trait not yet played | `.xk-sheet-trait--pending` | Muted value and the words "not yet played by the game" |
| About them | `.xk-dialogue-about` | A card inside the conversation window; each value's tooltip says how it is known |
