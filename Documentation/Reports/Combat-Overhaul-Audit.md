# Combat overhaul: audit of the combat experience

Date: 2026-11-11. Base: `242fde2` (Phase 14, committed locally, not pushed). This audit was written from the code and from captures of the running game before any combat interface code was changed. Phase 14 was complete: its line of sight, cover, doors and low cover are in place and are used here, not changed.

## 1. Architecture as found

```
  Player input (UI Toolkit buttons, grid cells)            Computer turns (timer, 0.6 s)
            │                                                        │
            ▼                                                        ▼
  CombatWindow (Exploration/Presentation) ──raises──► ExplorationController.Combat
            ▲                                        │  CombatAct / CombatMove / RunAiTurn
            │ Show(EncounterSnapshot)                ▼
            │                              WorldSession.Dispatch(CombatActionCommand(seq))
            │                                        │  (working copy, validated, committed)
            │                                        ▼
            └──────── GetEncounter() ◄──── CombatEngine (Domain): the only authority
                                                     │  turn order, actions, movement, rolls, log
                                                     ▼
                                   EncounterData in WorldData (saved, v16)
```

- **One engine.** `CombatEngine` (Domain) decides everything: action economy, movement cost (`StepCost`: 5 ft, +5 difficult terrain, +5 crawling), walls and shut doors (`IsBlocked`), opportunity attacks (`DoMove`), cover and line of effect (`GridSight`, Phase 14), reactions, and the log.
- **One way in.** Every player action is a `CombatActionCommand` with the snapshot's `Sequence`, so a stale or repeated click is refused (`StaleOrDuplicateActions_AreRefused`).
- **The presentation only reads `EncounterSnapshot`.** The grid is 1.5 m squares in the world, laid around where the player stood when the fight began (`ExplorationController.SquareToWorld`). Places use the same 1.5 m cells, so in-place fights line up with their rooms.

## 2. The interface as found

Measured from the laid-out elements in the running game (`CombatOverhaulCaptureTests`, before set). The UI panel scales with the screen (reference 1920×1080), so the share is the same at every resolution:

| Resolution | Combat interface | Share of the screen |
|---|---|---|
| 1920×1080 | one panel, 1862×508 px (97 % of the width, 47 % of the height) | **45.6 %** |
| 2560×1440 | same (scaled) | 45.2 % |
| 1366×768 | same (scaled); text at about 70 % size | 45.3 % |

Screenshots: `Documentation/Screenshots/CombatOverhaul/before_*.jpg`.

**Why it covers half the screen.** `.xk-combat` in `ExplorationHud.uss` is `left/right: 1.5 %; bottom: 1.5 %; height: 47 %` with an opaque background (0.94 alpha). It holds five fixed columns, all always visible:

| Column | Width | Contents |
|---|---|---|
| Initiative | 19 % | One two-line row per combatant |
| Field | 24 % | A 15×11 grid of buttons, the only way to move |
| Your turn | 20 % | Every option as a full-width button, folded by kind, unavailable options with their reasons inline |
| Result + Inspect | 19 % | The last result in full, and a full stat block of the inspected combatant |
| Combat log | rest | Every entry |

Nothing can be collapsed. Shrinking the fonts would not help, because the problem is that everything is shown at once.

**Other findings:**

| # | Area | Finding |
|---|---|---|
| 1 | Movement | Moving means clicking a square on the small grid. Clicking the world does nothing. |
| 2 | Movement | `PlayerMoveProblem` plans a **straight line of steps**. Around a wall it simply refuses; the grid marks unreachable squares, but there is no route finding for the player (the AI has `PathAround`). |
| 3 | Movement | The character **teleports** square to square when the snapshot changes (`UpdateMarkers` → `Player.Teleport`). Enemies are placeholder capsules and cubes that jump. |
| 4 | Dash | Dash is an ordinary option button. A move beyond the remaining movement is refused with "needs N ft", with no hint that Dash would cover it. |
| 5 | Actions | Every option is a full-width row. A level-3 fighter against three enemies sees over 20 rows (attack × target, shove × target, …). Main, bonus and reaction are a text line ("Action ready · bonus action ready · reaction ready"), not visible slots. |
| 6 | Targeting | Choosing a target means choosing a row ("Arming sword → Bandit (5 ft)"). The world shows no target highlight, range or cover. Phase 14 put cover in the order rows and on the grid's tooltips. |
| 7 | Feedback | Results appear only as text, in the Result box and the log. The world shows no swing, projectile, hit, miss or damage number. The down state turns a marker on its side. |
| 8 | Log | The log is always open and takes about a fifth of the panel. Roll breakdowns are in the text, which is good; keep that. |
| 9 | Camera | The camera stays in third person behind the player; the cursor is freed for the panel. There is no focus-on-active control. The panel covers the lower half, where the fight is. |
| 10 | Input | Mouse only, on panel buttons. No keyboard shortcuts for actions. Escape closes windows, but cannot close the fight. |
| 11 | Spells | Spells open a composer inside the actions column (Phase 9): target buttons, and aiming a point on the grid. This works; it needs to be reachable from the new bar. |
| 12 | Resolutions | At 1366×768 the panel's text is small (scaled to about 70 %) but not clipped. The 15×11 grid squares are 22 px, below a comfortable click size. |

**What works and stays:**
- the engine and its commands;
- the snapshot's options with their reasons (`ActionOptionView.Problem`);
- the sequence guard;
- the spell composer;
- the reaction prompt (Phase 10);
- the log's full roll text;
- Phase 14's cover on the snapshot (`CoverFromPlayer`, `InPlayersLine`).

## 3. Animation and assets

- **The player's body** (and the people of the towns) is the Quaternius Universal Base Character, animated by `Art/Characters/Locomotion.controller` through `HumanoidCharacter` (parameters `Speed`, `Talking`, `Activity`; states for locomotion, talk, chat, work and pray).
- **The CC0 Universal Animation Library** (`UAL1_Standard.fbx`) also holds `Sword_Attack`, `Punch_Jab`, `Hit_Chest`, `Hit_Head`, `Death01`, `Spell_Simple_Shoot` and `Pistol_Shoot`, but the controller has no attack, hit or death states. Wiring them means editing the controller asset; this pass does not do that. It uses procedural feedback instead, and records the clips as the next step.
- **Enemies** in practice fights and outings are placeholder capsules (humanoids) and boxes (beasts) in their side's colour. Town guards who join use the humanoid model.
- **No projectiles, hit effects or floating numbers exist.**
- **No new assets are needed:** procedural feedback (movement, lunge, flinch, projectiles, numbers, area rings) can be built from primitives, with no third-party art. If clips are used later, they are CC0 (already credited).

## 4. Research: patterns from other games

These notes describe publicly visible interface patterns, from experience and from published discussion of these games. Nothing is copied: no artwork, icons, layouts or wording.

| Game | Pattern | Verdict for Realmkeeper |
|---|---|---|
| Baldur's Gate 3 | A bottom hotbar with main and bonus action shown as distinct resource markers; a portrait with HP at the lower left; initiative portraits across the top; hover a target for hit chance; a path line with movement cost in metres. | **Adopted, adapted:** a top initiative strip; visible Action / Bonus / Reaction / Movement slots; a compact bar grouped by kind (not one icon per spell); hover cost. **Rejected:** a large icon grid of every ability. Realmkeeper has no icon set and labels read better. |
| Solasta: Crown of the Magister | Movement preview in squares and feet with the Dash extension in another colour; explicit confirmations; dice and modifiers inspectable in the log. | **Adopted:** reachable now versus "needs Dash" shown differently, with Dash asked for before it is spent; the log keeps the full roll breakdown. |
| Divinity: Original Sin 2 | Click the ground; a path preview with action-point cost; clear "not enough" feedback at the cursor. | **Adopted:** click the world, not a cell; a path line and a cursor label with feet, remaining movement and the reason when refused. **Rejected:** its surface layering (not in the rules). |
| Pathfinder: Wrath of the Righteous | Turn-based mode on a real-time engine; many toggles and a crowded action bar. | **Lesson:** keep the bar short with groups; the crowded bar is the thing to avoid. |
| Dragon Age: Inquisition | A tactical camera you can raise away from the hero. | **Adapted, small:** a focus-on-active key; the player keeps the camera, with no forced cuts. |
| XCOM 2 | Blue and yellow movement ranges; cover shown on hover; a confirmation step for the costly move. | **Adopted:** cover from the player on hover (from Phase 14's rule); a Dash confirmation. **Rejected:** forced cinematic cameras. |
| Pillars of Eternity II: Deadfire | A collapsible combat log with a hover breakdown; pause-and-play. | **Adopted:** a log collapsed by default to the last lines, expandable. |

**Radial wheel versus bar.** A wheel suits gamepads and a few categories. On mouse and keyboard, with labelled options of very different lengths ("Arming sword → Bandit archer (10 ft)"), a radial layout has to truncate or splay its labels, and it covers the target it opens beside. The decision is **a compact bar with group pop-ups**; no wheel is built (see the completion report). Context options on right-clicking an enemy were considered as the wheel's useful part; they were not built in this pass (right-drag turns the camera instead), and are listed as a next step.

## 5. Prioritised problems

1. **P1.** The panel covers about 46 % of the screen; the battlefield is hidden. → A compact HUD in pieces, details on demand.
2. **P1.** Moving needs the small grid; there is no route finding round walls. → Click the world, with an authoritative planner (Domain), a path preview and a cursor label.
3. **P1.** Dash is not offered when needed. → A "needs Dash" preview and a confirmation.
4. **P2.** Actions are a long list; the economy is a line of text. → Visible slots and a grouped bar with pop-ups.
5. **P2.** There is no visible action in the world. → Smooth movement, a lunge, a flinch, projectiles, area rings and floating numbers, keyed to log entries (never applied twice).
6. **P2.** No targeting in the world. → Hover highlight and a label with range, cover and the line from Phase 14; right-click options.
7. **P3.** The log dominates. → Collapsed by default, expandable.
8. **P3.** Input. → Keyboard shortcuts (1–9, Space for end turn, Tab for the grid, Z to focus, Esc to cancel).

## 6. Dependencies on Phase 14

- Walls, shut doors and low cover in fights (`EncounterData.Blocked`, `Doors`, `LowCover`).
- `CoverOf`, `InPlayersLine` and `CoverFromPlayer`.
- Difficult terrain.

The planner and the previews use these engine functions and add no rule of their own.
