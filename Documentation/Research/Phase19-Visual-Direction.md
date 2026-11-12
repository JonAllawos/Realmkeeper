# Phase 19A: Visual direction

## Starting point (from the Phase 16–18 screenshots and the stylesheets)
- **Framework.** UI Toolkit (ADR-0001), with two stylesheets:
  - `RealmkeeperHud.uss` (909 lines);
  - `ExplorationHud.uss` (1,433 lines).

  There is one theme (`RealmkeeperTheme.tss`) and one `PanelSettings`: Scale With Screen Size, 1920×1080 reference, match width-or-height 0.5.
- **Look.**
  - Dark translucent panels (`rgba(22,20,17,.84)`), thin gold borders (`rgba(201,164,101,.28)`), and parchment text (`rgb(232,223,204)`).
  - There are no icons, textures or custom fonts: everything uses Unity's default runtime font.
  - Colours are written literally in hundreds of rules. There are no USS variables.
- **Problems seen in the captures.**
  - The atlas is flat coloured squares, and "Uncharted lands" are empty grey boxes.
  - Long text strips sit under the map.
  - Hub tab rows wrap onto two lines (journal: 11 tabs).
  - Pill buttons all look alike.
  - The combat HUD is three disconnected boxes.
  - The dropdown and text fields use Unity's light defaults.
  - Information is text-only, with no icons.
  - Placeholder "Coming later" items show in the world sidebar.

## Direction A: "Cartographer's Ledger" (recommended)

A grounded, warm, low-fantasy look: **dark umber ink for the working interface, and parchment for documents and maps.** Bronze is the single accent, used for selection and the primary action only. The ornament is a small engraved corner, on window frames and modals only.

| Token | Value | Use |
|---|---|---|
| `--rk-ink-950` | `#100D0A` | Screen dim, modal scrim (at 70 %) |
| `--rk-ink-900` | `#16120E` | Window background |
| `--rk-ink-800` | `#1E1913` | Panel |
| `--rk-ink-700` | `#28211A` | Card, list row |
| `--rk-ink-600` | `#342B21` | Hover, raised control |
| `--rk-line` | `#4B3E2F` | Borders, dividers |
| `--rk-line-strong` | `#6B5843` | Focused border |
| `--rk-text` | `#ECE2CC` | Primary text (≈ 14:1 on ink-800) |
| `--rk-text-2` | `#C2B59C` | Secondary text |
| `--rk-text-3` | `#8E826F` | Muted, disabled (≥ 4.5:1 on ink-800) |
| `--rk-bronze` | `#B88A4A` | Accent: selected tab, primary button |
| `--rk-bronze-hi` | `#DDB06C` | Focus ring, tracked quest, active selection |
| `--rk-paper` | `#E4D5B4` | Map and journal page |
| `--rk-paper-ink` | `#2A2017` | Text on paper |
| `--rk-good` | `#7FA36A` (sage) | Success, gain, healthy |
| `--rk-warn` | `#D69B3B` (ochre) | Warning, risk |
| `--rk-bad` | `#B5503F` (oxblood) | Error, damage, hostile |
| `--rk-info` | `#6B8DBF` (lapis) | Information, magic |
| `--rk-rare` | `#9B7BC1` | Reaction pip, rare items |

- **Every state has a second cue:**
  - an icon (✓, ▲, ⚠, ✕);
  - a border style;
  - text ("Unavailable: needs a free hand").
- **Typography:**
  - If OFL is approved: EB Garamond SemiBold for headings, and Alegreya Sans for body and controls, with Alegreya Sans' lining figures for statistics.
  - CC0-only fallback: MFB Oldstyle Bold for headings, and Unity's default font for body text.
- **Sizes at 1080p:**
  - screen title 28;
  - section 20;
  - card title 16;
  - body 15;
  - small 13;
  - micro (legends) 12, never smaller.
- **Icons:** game-icons.net silhouettes, tinted to `--rk-text-2`, or to the state colour. Sizes are 16 inline, 20 in lists, 28 in tabs and the action bar, and 40 in item slots.
- **Buttons:**
  - Height 32 (36 for primary), radius 3, 1 px `--rk-line` border on an ink-700 fill.
  - Hover goes to ink-600. Pressed gets an inset darker fill.
  - Selected gets a bronze border with a 2 px bronze underline (for tabs).
  - Disabled is text-3 at 60 % opacity, with a tooltip giving the reason.
- **Panels:**
  - Ink-800, with a 1 px line border.
  - Windows get the Kenney "Double" corner frame tinted to `--rk-line-strong` (9-slice).
  - No gradients. There is optional 6 % leather grain on the hub only.
- **Map:**
  - A parchment base, with ink line-art symbols (Kenney Cartography).
  - Region fills are 18 % faction tints, with hatched borders.
  - Roads are ink lines: solid when open, dashed when crossing a dangerous road, and crossed when closed.
  - Unknown land is plain parchment with faint contour hatching and the label "Uncharted".
- **Motion:** 120 ms ease-out for hover, and 180 ms for panel fade and slide. Nothing loops except a slow 2 s pulse on the tracked marker.

Why this direction:
- It fits the setting: agrarian, grounded, simulation-heavy.
- It is the best match for the verified asset supply (Kenney Cartography, Fantasy UI Borders, game-icons.net, CC0 textures).
- It needs no shader work.
- It keeps the existing dark-panel structure, so screens change by restyling rather than rebuilding.

## Direction B: "Iron & Vellum"

Cool and austere, like a northern chronicle: slate and charcoal panels, a pewter and steel-blue accent, and an engraved, historic-map atlas (K.M. Alexander's CC0 brushes, after conversion).

| Token | Value |
|---|---|
| Background | `#121518` / panel `#1A1F24` / card `#232A31` |
| Line | `#3A4550` |
| Text | `#E3E7EA` / `#AEB7BF` / `#7C8792` |
| Accent | `#8FB3D1` (steel blue), `#C9CED3` (pewter) |
| States | good `#77A688`, warn `#D1A04A`, bad `#C25A55` |
| Paper | `#D9D3C3` cold vellum |

- **Type:** Cinzel for screen titles only (OFL), with Source Sans 3 for everything else (OFL).
- **Strengths:** very legible, and modern-strategy clarity like CK3.
- **Weaknesses:**
  - It needs OFL approval for both faces.
  - The engraved-map look depends on converting the Photoshop brushes.
  - It reads colder than the warm village art (Quaternius, orange roofs) and can feel generic.

## Direction C (considered, not proposed): "Illuminated"
Rich manuscript colours and gilded initials. Rejected:
- it drifts into heavy gold ornament;
- it needs bespoke art the free-asset supply lacks.

## Recommendation
**Direction A.** Implementation notes for Phase 19B:
1. Put the tokens in one `RealmkeeperTokens.uss` as USS custom properties (`var(--rk-*)`). Import it from both themes, and replace the literal colours.
2. Build shared classes (`rk-btn`, `rk-tab`, `rk-card`, `rk-chip`, `rk-badge`, `rk-meter`, `rk-tooltip`, `rk-modal`) before restyling any screen.
3. Restyle Unity's built-in controls (`TextField`, `DropdownField`, `ScrollView`, `Toggle`, `Slider`) to the ink palette.
4. Icons: a curated set of about 120 game-icons.net PNGs (128 px, white), each with its author recorded.
