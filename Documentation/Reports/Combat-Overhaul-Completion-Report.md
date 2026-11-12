# Combat overhaul: tactical movement, combat UI, action selection and visual feedback — completion report

Date: 2026-11-11. Branch `feature/phase-0-1-foundation`, on top of Phase 14 (`242fde2`, committed locally). This milestone is committed locally in one commit. **Not pushed.** Decision record: ADR-0072. Audit: `Reports/Combat-Overhaul-Audit.md`.

## 1. Initial combat architecture audit

`Reports/Combat-Overhaul-Audit.md` describes the architecture as found:
- one authoritative `CombatEngine` (Domain);
- every player action a `CombatActionCommand` carrying the snapshot's sequence;
- a presentation that reads `EncounterSnapshot`;
- a 1.5 m world grid laid around where the fight began.

The audit was written, and the "before" captures were taken, before any combat interface code changed. Phase 14 was complete; its walls, doors, low cover and cover rule are used unchanged.

## 2. Previous combat UI problems

Measured in the running game (`CombatOverhaulCaptureTests`, before set):

| | 1920×1080 | 2560×1440 | 1366×768 |
|---|---|---|---|
| Share of the screen covered | 45.6 % | 45.2 % | 45.3 % |

The panel was one opaque box, 1862×508 reference px (97 % × 47 %), with five always-open columns. Other problems:
- **Movement:** only by clicking the small grid; a straight line of steps, so no route round walls; the body teleported.
- **Dash:** not linked to the move that needed it.
- **Actions:** 20 or more full-width option rows.
- **Feedback:** text only.
- **Log:** always open.

The prioritised list is in the audit, section 5.

## 3. New HUD layout

| Piece | Where | What |
|---|---|---|
| Top strip | top centre | Name, round, whose turn, Withdraw or Call off; initiative chips (name and HP; initiative, AC, conditions, effects and cover in the tooltip) |
| Bar | bottom centre | The Action, Bonus and Reaction slots (dimmed when used), movement left, groups 1 Attack · 2 Actions · 3 Spells · 4 Features (each with how many can be used now), Dash, Grid (Tab), End turn (Space) |
| Pop-up | above the bar | The open group only; unavailable choices keep their reason. The Phase 9 spell composer opens here. |
| Card | lower left | The selected combatant: name, HP bar, HP/AC/speed, conditions, cover from you, the last result, Details (the full stat block) |
| Log | lower right | Collapsed to the last lines; More expands it, with every roll and modifier |
| Grid | optional, right | The 5-ft grid with walls, doors, low cover and reach (Tab) |
| Cursor label, Dash question | near the cursor, centre | Shown only when needed |

Measured share of the screen (after set; the UI scales with the screen):

| State | Share |
|---|---|
| At rest, with a route previewed (1920×1080 / 2560×1440 / 1366×768) | 15.3 % / 15.0 % / 15.2 % |
| Attack group open | 27.4 % |
| Dash question open | 18.3 % |
| Grid and expanded log open | 34.3 % |

The largest piece at rest is the bar, 922×110 px (48 % × 10 %). No piece leaves the screen at any of the three sizes; a PlayMode test checks this at 1920×1080.

## 4. Movement implementation

- **Hover.** `UpdateCombatWorld` casts the cursor's ray onto the ground: the nearest collider that is not a body, else the fight's floor plane. The point becomes a square (`SquareAt`). If that square is a wall, a shut door or taken, the nearest valid neighbour is planned instead.
- **What the hover shows:** the route (green while covered, amber where Dash is needed), a destination disc, and a label with the cost, what is left, difficult terrain and opportunity attacks. The route and disc sit on the real ground (raycasts that ignore bodies).
- **Click** (`CommitCombatMove`). The plan is made again (the battlefield may have changed); then the player moves at once, is asked about Dash, is offered "go as far as you can", or is told why not.
- **The move itself:** `CombatActionCommand(MoveAction(path), sequence)`. The engine moves the player, deducts movement and resolves opportunity attacks. The body then walks the route square by square at 3.2 m/s, facing the way; if an opportunity attack stopped the move, it stops where the engine left it.
- **Grid squares:** clicking them (Tab) uses the same planner.
- **Other bodies** walk to their new squares instead of jumping.
- **Mouse input in tests:** `ExplorationController.MouseDrivesCombat` turns it off, so tests and captures preview and click through the same API.

## 5. Pathfinding and movement-cost behaviour

`CombatPaths.Plan` (Domain; deterministic; radius 30 squares) is Dijkstra over 8 neighbours, using:
- `StepCost`: 5 ft; +5 difficult terrain; +5 crawling while prone;
- `IsBlocked`: walls and shut doors;
- hostile conscious combatants block; the end square must be free;
- no diagonal past a wall's corner.

It returns the route, the cost, the reachable steps, whether Dash is needed and would suffice, the difficult squares, and the enemies whose reach the route leaves (none once disengaged). Speed 0 names its cause (restrained, grappled …). Speed already includes exhaustion, Slow and Haste.

`WorldSession.PlanPlayerMove` plans on a copy and passes any route that can be walked now through `CombatEngine.Unavailable`, which also catches fear. Planning never changes the world; a test compares the world before and after.

## 6. Dash integration

- A destination beyond the movement left, but within movement plus speed, is shown amber and labelled "needs Dash (uses your action)".
- Clicking it opens a question with the numbers ("That is 35 ft away and you have 30 ft of movement left. Dash uses your action this turn and gives you 30 ft more."). The buttons are **Dash and move** and **Cancel (Esc)**.
- Nothing is spent until **Dash and move** is pressed. Then `DashAction` is one command and the move is a second, each checked by the engine.
- **Cancel** spends nothing.
- With the action already spent, no second Dash is offered: the plan says so and the engine refuses one.
- The Dash button on the bar does the same thing directly.

Tests:
- .NET: `TwoMovesInOneTurn_EachDeductedOnce_ThenDash_AddsWhatIsNeeded_Once`.
- PlayMode: `AMoveThatNeedsDash_AsksFirst_CancelSpendsNothing_YesDashesAndGoes`.

## 7. Action selection architecture

- **Groups.** The bar groups the snapshot's existing choices: attacks (`AttackAction`), other actions (shove, dodge, disengage, help, using an item …), spells (`PlayerSpells`), class features (`PlayerFeatures`), Dash, and End turn.
- **One group at a time.** Opening a group shows its choices in the pop-up. The other choices stay in the hierarchy, hidden, under their usual names, so the spell composer, the reaction prompt and every earlier test work unchanged.
- **Unavailable choices** show their reason.
- **Keys:** 1–4 open the groups, Space ends the turn, Esc closes. A new turn closes the pop-up.
- **Repeated input:** the command's sequence guard (`StaleOrDuplicateActions_AreRefused`, Phase 8) still refuses stale or repeated actions.

## 8. Radial wheel decision and results

**Not built.**
- A ring cannot hold labelled choices of very different lengths ("Arming sword → Bandit archer (10 ft)") without truncating them.
- It covers the target it opens beside.
- On mouse and keyboard, the grouped bar with number keys does the same in fewer steps.

The research notes are in the audit, section 4. Context options on right-clicking an enemy, the useful part of a wheel, are a next step.

## 9. Visual combat presentation

`ShowCombatFeedback` reads new combat log entries, keyed by sequence, so each is shown once and a refresh or load never replays history:

| Entry | Shown as |
|---|---|
| Attacks and spells | The attacker lunges (melee) or a projectile flies (beyond 1.5 squares). Spell projectiles are coloured from their damage words: fire, cold, lightning, radiant, necrotic, force — data, not one script per spell. |
| Damage | A red number from the applied-damage entry only, so dice lines are not counted twice, marked "resisted" or "immune"; the target flinches |
| Healing | A green number |
| Other results | Miss, Critical!, Saved / Failed save, the condition's name, Down, Dies, Stable |

- Several words for one body are stacked.
- Numbers appear after the projectile's flight or the lunge.
- Nothing here changes the fight: the engine has already decided before anything is shown.

## 10. Animation and asset licensing

- **No new assets.** All feedback is built from primitives: lines, discs, spheres, world text.
- **Existing assets:** the player and town people are the Quaternius CC0 bodies, animated by `Locomotion.controller` (already credited).
- **Clips not used yet:** the CC0 Universal Animation Library includes `Sword_Attack`, `Punch_Jab`, `Hit_Chest`, `Hit_Head`, `Death01` and `Spell_Simple_Shoot`. The controller has no states for them; wiring them means editing the controller asset, which this pass did not do. Attacks are shown by procedural lunges and flinches, and **no bespoke attack animation is claimed**.
- **Enemies** in practice fights and outings are still placeholder capsules and boxes.

## 11. Camera and targeting

- **Camera.**
  - The player keeps control; nothing cuts or jumps.
  - Right-drag turns the camera; Z turns it to whoever is acting.
  - Zoom and pan were not added; the rig has no runtime zoom control.
- **Targeting.**
  - Each foe's cover from the player (Phase 14's rule) is shown in its chip tooltip and on the card.
  - The grid's tooltips give cover, doors and low cover, and figures out of sight are faded.
  - Unavailable attacks and spells state range, line and total cover.
  - **Not added:** a hover highlight on bodies in the world with range and cover, and an area preview in the world. Areas are still previewed on the grid by the Phase 9 composer.

## 12. Accessibility and input

- **Covered:**
  - mouse for everything;
  - keys for the common actions (1–4, Space, Tab, Z, Enter, Esc);
  - every button with a tooltip;
  - used slots dimmed as well as labelled;
  - bar targets at least 92 px wide, instead of 22 px grid squares;
  - restrained effects: no screen shake, short floats.
- **Not added:** key rebinding (the project has no binding settings for these keys), a separate UI scale option (the panel already scales with the screen), and a "reduce effects" setting.

## 13. Testing results

| Suite | Result |
|---|---|
| .NET build | 0 warnings, 0 errors |
| .NET tests | **691 passed, 0 failed** (681 + 10 new `CombatMoveTests`) |
| Unity compile | 0 errors, 0 warnings |
| Unity EditMode | **691 / 691 passed**, 0 compile warnings. A first full run had one timeout (`ArmourClass_FollowsSrdArmourCategories_AndTheShield`, 180 s), which coincided with a temporary measurement file being created and removed in the project during that run (my mistake). Alone, the test passes in 0.18 s; a clean full rerun passed 691 / 691. |
| Unity PlayMode | **119 tests: 98 passed, 0 failed, 21 skipped** (capture tests, which run only with `RK_CAPTURE_DIR`) |
| Captures | `CombatOverhaulCaptureTests`, before and after sets: 3 + 7 screenshots |

**New .NET tests (`Tests/EditMode/Combat/CombatMoveTests.cs`, 10):**
- a route round a wall, accepted and walked by the engine;
- a diagonal never cuts a corner;
- difficult terrain is costed and avoided when cheaper;
- a shut door is no way until opened;
- an enemy's square is refused by name, and enemies block;
- restrained means speed 0, with the reason;
- two moves in a turn, each deducted once; planning spends nothing; Dash adds what is needed, once; no second Dash;
- leaving reach is flagged as an opportunity attack, and disengaging clears it;
- the same plan every time;
- the session plans without changing the world, and refuses when it is not your turn.

**New PlayMode tests (`CombatOverhaulPlayModeTests`, 5), in the shipped scene:**
- the HUD covers under 30 %, its pieces are on screen, and the grid, log and pop-up are closed by default and open on demand;
- clicking the ground moves along the previewed route, the body walks, and a second move is made in the same turn;
- Dash asks first, Cancel spends nothing, and Yes dashes and moves;
- an unreachable square says why and nothing moves;
- the bar opens the attack group, the attack shows in the world, a refresh shows nothing twice, and End turn works from the bar.

**Changed test.** `Phase9PlayModeTests.Combat_ActivatesAClassFeature_FromThePanel` now reads the effect from the initiative chip's tooltip, where it moved.

**All earlier combat tests pass unchanged:** Phases 8–14, including the AI, the spell composer, reactions, cover and the sequence guard. They use the same element names.

**Integration:**
- the engine is untouched apart from the new planner, which only reads;
- feedback is keyed by log sequence;
- repeated clicks meet the sequence guard;
- cancelled questions spend nothing.

**Manual test instructions** (in the editor or a build):
1. In a town, press F (practice fight).
2. On your turn, move the mouse over the road: a green line, a disc and a label with the feet. Click: the character walks there.
3. Click again nearby: the second move uses what is left.
4. Point far away: the line turns amber and the label says "needs Dash". Click: the question appears. Press Esc: nothing is spent. Click again and press Dash and move.
5. Point at a bandit's square: the label says who stands there.
6. Press 1: the attack choices open above the bar. Click an attack: watch the lunge and the number above the target; the card shows the result.
7. Press Tab for the grid, and click More on the log. Press Z to face whoever acts. Right-drag turns the camera.
8. Press Space to end the turn.

## 14. Performance results

Ad hoc, in the .NET harness (Release), with a temporary test that was not kept: on a 61×61 area with 300 random walls, a plan takes 3.0 ms on average to squares up to 12 away, and 6.9 ms to the far corner. `PlanPlayerMove` also copies the world for each plan, as `PlayerMoveProblem` and every other query already do. Hovering replans only when the square under the cursor changes, plus up to 8 neighbour plans when that square is invalid. No hitch was seen in the captures, but no frame-time profile was taken.

## 15. Screenshots

In `Documentation/Screenshots/CombatOverhaul/`, with captions and measurements in `before_capture_log.txt` and `after_capture_log.txt`:

| Shot | Shows |
|---|---|
| `before_1920x1080_fight`, `before_2560x1440_fight`, `before_1366x768_fight` | The old panel, about 45 % of the screen |
| `after_1920x1080_fight`, `after_2560x1440_fight`, `after_1366x768_fight` | The new HUD, about 15 %, with a hovered route and its label ("15 ft · 15 ft left after; leaves the reach of Bandit: an opportunity attack") |
| `after_1920x1080_attack_group` | The attack group open above the bar |
| `after_1920x1080_dash_question` | The Dash question with its numbers, before anything is spent |
| `after_1920x1080_attack_feedback` | After the player's blow: the damage number and "Dies" above the bandit; the chip reads "dead" |
| `after_1920x1080_grid_and_log` | The optional grid and the expanded log |

All are actual renders from the running game. Each was opened and checked against its caption.

## 16. Known limitations

- **Animation:** attacks are procedural (lunge, flinch, projectile). The library's clips are not wired, and enemies are still placeholder shapes.
- **Enemy walking:** enemies walk straight lines to their new squares, because the snapshot holds only where they end, not their route.
- **Targeting:**
  - no hover highlight with range and cover on bodies in the world;
  - no right-click context options;
  - areas are previewed on the grid, not on the ground.
- **Camera:** no zoom or pan; turn and focus only.
- **Input:** keys cannot be rebound, and there is no reduce-effects setting.
- **Items:** the bar has no Items group. Usable items (the engine's `UseItemAction`, Phase 11) appear in the 2 Actions group with the other choices; a group of their own is a next step.
- **Plan cost:** about 3–7 ms plus a world copy per new hovered square.
- **Testing:** resolutions were verified by rendering into a texture at each size. The UI scales with the screen, so text at 1366×768 is about 70 % of its reference size. It was readable in the capture; that was not checked on a physical display.

## 17. Files changed

| Area | Files |
|---|---|
| Domain | `Runtime/Domain/Combat/CombatPaths.cs` (new: the planner) |
| Application | `Runtime/Application/CombatQueries.cs` (`PlanPlayerMove`) |
| Presentation | `Runtime/Exploration/Presentation/CombatWindow.cs` (the compact HUD), `ExplorationController.CombatWorld.cs` (new: hover, click, walking, keys, feedback), `ExplorationController.Combat.cs` (hooks; grid clicks use the planner; an action clears the preview), `ExplorationController.cs` (a click in a fight does not lock the cursor), `Resources/ExplorationHud.uss` (`xk-cbt-*` styles) |
| Tests | `Tests/EditMode/Combat/CombatMoveTests.cs` (new), `Tests/PlayMode/CombatOverhaulPlayModeTests.cs` (new), `Tests/PlayMode/CombatOverhaulCaptureTests.cs` (new), `Tests/PlayMode/Phase9PlayModeTests.cs` (one assertion) |
| Docs | `Reports/Combat-Overhaul-Audit.md`, this report, `Decisions/ADR-0072-…`, `Decisions/README.md`, `Systems/Combat.md`, `Roadmap.md`, `Screenshots/CombatOverhaul/` |

## 18. Git status and commit

- **Commit:** one local commit on `feature/phase-0-1-foundation`, after `242fde2` (Phase 14), titled "Combat overhaul: compact HUD, click-to-move with path preview, Dash confirmation, visual feedback". `git log -1` shows its hash.
- **Staged with:** `git add -A -- . ':!ProjectSettings/GraphicsSettings.asset' ':!Assets/InitTestScene*'`.
- **Left out:** the owner's `GraphicsSettings.asset` change, which stays unstaged and unchanged.
- **Pushed:** no. The branch is 2 commits ahead of `origin`.

## 19. Recommended next steps

1. **Wire the CC0 attack, hit and death clips** into `Locomotion.controller` (an attack layer with triggers), timed to the same log entries; give enemies humanoid and beast models.
2. **Targeting in the world:** a hover highlight with range, line and cover from `CoverOf`; right-click context options; an area outline on the ground for spells; a group of its own for usable items.
3. **Camera zoom and pan** in fights, and key rebinding with the settings work in the planned UI/UX milestone.
4. **Then Phase 15**, Companions and Parties. Party members will use the same planner, bar and card, with the card switching between characters.
