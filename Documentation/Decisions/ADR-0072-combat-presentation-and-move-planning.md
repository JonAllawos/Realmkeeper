# ADR-0072: The combat interface shows and asks; one planner proposes moves; the engine decides

**Context.** The combat panel covered about 46 % of the screen, and moving meant clicking squares on a small grid. A straight line of steps was the only route, so a wall meant a refusal. Dash was a button with no link to the move that needed it. Results appeared only as text. A better experience must not create a second combat engine, or presentation state that the rules depend on.

**Decision.**
1. **Route planning is a Domain function.** `CombatPaths.Plan(engine, mover, square)` is Dijkstra over the 8-neighbour grid. It uses only the engine's own rules: `StepCost` (difficult terrain, crawling), `IsBlocked` (walls, shut doors), hostile combatants (who block), and occupied end squares. It never cuts a wall's corner, and it is deterministic. It returns:
   - the route and its cost;
   - the steps the movement left covers;
   - whether Dash is needed and would suffice;
   - the enemies whose reach the route leaves.

   `WorldSession.PlanPlayerMove` runs it on a copy and asks `CombatEngine.Unavailable` about any route that can be walked now. The planner is never the authority: every move is still a `CombatActionCommand` with the snapshot's sequence, and the engine moves the combatant, deducts movement and resolves opportunity attacks.
2. **Moving is a click on the ground.** The cursor's ray meets the ground (colliders that are not bodies, else the fight's floor plane). That point becomes a square; an invalid square snaps to the nearest valid neighbour. Hovering shows:
   - the route, green where it is covered now and amber where Dash is needed;
   - a destination marker;
   - a label with the cost, what is left, difficult terrain and opportunity attacks.

   A click on a reachable square moves at once. One that needs Dash asks first, and nothing is spent unless the player agrees; cancelling spends nothing. One too far even with Dash offers to go as far as possible. Any other square says why it cannot be reached. The grid remains for reading, on demand (Tab).
3. **The interface is compact pieces over a clear battlefield:**
   - a top strip with the initiative chips;
   - a bottom bar with the Action, Bonus and Reaction slots, movement left, grouped choices with counts, Dash and End turn;
   - a pop-up for the open group;
   - a character card, with details on demand;
   - a log collapsed by default.

   Element names the rest of the game and the tests use are kept.
4. **What happens shows in the world, from the log, once.** Bodies walk their routes. An attacker lunges; a projectile flies for ranged attacks and spells (coloured by damage words, as data, not one script per spell); damage, healing, misses, criticals, saves, conditions, downs and deaths float above the body. All of it is keyed by the log's sequence, so a refresh never replays it, and it changes no state.
5. **No radial wheel.** Labelled options vary too much in length for a ring, and a wheel covers the target beside which it opens. The grouped bar and keys (1–4, Space, Tab, Z, Esc) serve mouse and keyboard better.

**Consequences.**
- The interface covers about 15 % of the screen at rest, against about 46 % before; at most about 35 % with the grid and the expanded log open.
- Movement round walls works for the player as it already did for the AI.
- Dash cannot be spent by accident.
- Animation is procedural (lunge, flinch, projectiles, floats). The CC0 library's attack, hit and death clips are not yet wired to the Animator controller.
- Planning copies the world once per new square hovered: a few milliseconds of planning plus the copy. It runs only when the square under the cursor changes.

**Alternatives considered.**
- **Planning in Presentation.** Rejected: it would be untestable in the .NET harness and could drift from the rules.
- **The AI's `PathAround`.** Rejected: it aims at a target within reach, not at a chosen square, and it ignores Dash.
- **A full redesign of the window as a new class.** Rejected: the existing spell composer, reaction prompt and element names work and are tested.
