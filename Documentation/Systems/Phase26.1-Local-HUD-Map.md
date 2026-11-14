# Phase 26.1: the local HUD map

**Decision record:** ADR-0118.

**Code:**
- `Application/Interface/LocalMapQueries.cs`: geometry, decluttering, the naming rule.
- `Exploration/Presentation/LocalHudMap.cs`: the element.
- `Exploration/Presentation/ExplorationController.LocalMap.cs`: its data.
- Styles at the end of `ExplorationHud.uss`.

**Tests:**
- `Tests/EditMode/Atlas/Phase26_1LocalMapTests.cs` (.NET and Unity EditMode).
- `Tests/PlayMode/Phase26_1PlayModeTests.cs`.
- `Tests/PlayMode/Phase26_1CaptureTests.cs` (screenshots).

## What the player sees

**A small rounded-square map at the bottom right of the exploration HUD:**
- dark ink frame with a bronze line (Cartographer's Ledger);
- the ground drawn as a plan: roads, water, fields, building footprints, the square; or, in the country, the land's 40 m cells and the roads;
- the player's mark at the centre: a parchment arrowhead pointing up, with a faint cone ahead;
- coloured, shaped marks for nearby things;
- up to five labels;
- a small "N" on the rim where north lies;
- the range in the corner ("45 m").

**Clicking the map** switches between its two ranges (tooltip: "Nearby: the map turns with you. Click to change its range.").

**When it is hidden:**
- behind every window, menu and fight, with the rest of the HUD;
- inside places (interiors, mines).

## Placement and size

| Width class | Map | The bottom stack (action bar, prompt, response) |
|---|---|---|
| Regular (1400 logical px and wider) | 184 px | Centred; the map is clear of it |
| Compact (narrower than 1400; 1080p with Auto scale lands here) | 158 px | Padded 170 px each side, so the action bar wraps left of the map |
| Narrow (narrower than 1150) or short (shorter than 760) | 136 px, 8 px from the corner | Raised 148 px, so it sits above the map |

**Why bottom right** (preflight audit §4):
- the place and vitals column is at the top left;
- the standing actions and toasts are at the top right;
- both bottom corners were free during exploration;
- the right one balances the left column, and toasts only move there while a window (which hides the map) is open.

## Coordinates and orientation

**Positions.** World positions in metres:
- **in a settlement:** layout coordinates through the environment root;
- **in the country:** region metres, as the region map uses.

**Projection: heading-up.** `LocalMapGeometry.Project(player, heading, point, pixelsPerMetre)` gives:
- right = dx·cos h − dz·sin h;
- forward = dx·sin h + dz·cos h;
- screen x = right, screen y = −forward.

Heading follows Unity yaw: 0 is north (+z), 90 is east.

**Heading source.** `PlayerCharacter.Heading`: the heading the player moves by. It turns with the mouse while standing still; in first person the body faces it, and in third person the body turns toward it as you walk.

**Turning.** The ground canvas turns by −heading about the player's point (style `rotate`, `transformOrigin`); marks are projected by the same function.
- **Smoothing:** about 90 % of a turn in 0.1 s; exact when Reduce motion is on, or for frames longer than 0.25 s.
- **Measured:** a 120° turn in place settles within 1° in about 0.19 s.

**Scale.** Pixels per metre = half the map's width ÷ the range.

| Where | Ranges | Notes |
|---|---|---|
| Settlement | 45 m, 90 m | A street and the next |
| Country | 160 m, 320 m | 4 or 8 cells of the approved 40 m scale |

## Marks (shape first; colour helps)

| Kind | Shape | Colour | Source | Shown when |
|---|---|---|---|---|
| Player | Arrowhead, pointing up | Parchment | Player | Always |
| Companion | Large dot with a light ring | Green | The party's followers | In the party and active |
| Person | Small dot | Teal | `ExplorationController.Residents` | Out in the street (not hidden), within reach |
| Merchant (a person) | Dot | Amber | Shopkeeper of a world shop | As a person |
| Shop | Square | Amber | The shop views, at the building's door | Always in that town |
| Service | Square | Blue | The building's function (tavern, market hall, hall, shrine, smithy, mill, guard post, stable) | Always in that town |
| Town landmark | Diamond | Parchment | The plan's landmarks (keep, chapel, watchtower) | Always in that town |
| Gate | Bar | Parchment | The plan's gates | Always; kept at the edge as a direction |
| Settlement | Large diamond | Blue | Known overland sites | Known; kept at the edge as a direction |
| Place, way out | Ring | Parchment | Known overland sites | Known |
| Landmark | Diamond | Violet | `GetLandmarks()` | Found (re-read every 2 s) |
| Waypoint | Downward triangle | Gold | The waypoint | In this region; kept at the edge |
| Traveller | Ring | Teal | The world's travelling groups | Within reach |
| Outlaws | Upward triangle | Red | Travelling groups that are bandits | **Only within 70 m** (in sight) |

## What is never shown

Nothing is placed for the map's sake:
- hidden people;
- strangers' names (they show their trade: "Smith", "Guard", "Merchant");
- unfound landmarks;
- unknown sites;
- outlaws out of sight.

**Names.** `WorldSession.MapLabelFor` applies the codex's rule (`KnownPersonQueries.AcquaintanceOf` past `Seen`).

## Labels

**Selection.** `LocalMapGeometry.Declutter` chooses up to five, greedily by kind (outlaws, waypoint, companions, shops, settlements, landmarks, services, gates, places, travellers, buildings, people), then by nearness.

**Placement.**
- Each label goes right of its mark, or left if the right is taken.
- A label is dropped if it would leave the map, cover the player's mark, overlap another label, or overlap the map's own furniture (the range chip, the N).
- Labels are shortened once, at 18 characters, at a word.
- The mark always remains.

**When labels are chosen again:**
- four times a second;
- at once when the view has turned 6° or moved 1.5 m since the last choice.

## Update strategy and cost

- **Arrival in an area** (a new environment root: another town, the country, a reload): the ground and the still marks (shops, services, landmarks, gates, sites, found landmarks) are made once.
- **Every 0.4 s:** moving marks (people, companions, travellers, the waypoint) are renewed by key. Marks are reused, not recreated; those not renewed leave.
- **Every 2 s:** acquaintance and found landmarks are re-read.
- **Every frame:**
  - the canvas's position and rotation (three style values);
  - each mark's projection from its transform;
  - label positions;
  - one repaint of the marks layer (Painter2D shapes).
- **Ground redraw:** only when the player has walked 60 % of the range from where it was drawn, or the range or size changed.
- **No work while hidden** behind a window.

Measurements are in `Reports/Phase26.1-Completion-Report.md` §16. They come from the Unity editor in batch mode; no player build was measured.

## Accessibility

- **Text size:** labels, the N and the range use the `--rk-fs-12` token, so the setting reaches them. The label width estimate reads the resolved size, so at 130 % fewer labels are placed rather than overlapping.
- **High contrast:** opaque frame and label chips, a brighter bronze line.
- **Reduce motion:** the turn is exact, with no easing.
- **Colour is never the only cue:** every kind has its own shape.

## Known limitations

- No map inside places (interiors, mines).
- No marker selection; clicking the map changes its range. Hovering over the map shows its tooltip only.
- The range choice is not saved.
- **People in the street at a given hour are those the schedule puts outside.** At 08:00 the captured town's people in the street were all shopkeepers, so the captures show amber merchant dots rather than teal ones.
- **Country ground** is drawn per 40 m cell (softened), with roads rounded through their cells' centres. Fine detail (single trees, small streams) is not drawn.
