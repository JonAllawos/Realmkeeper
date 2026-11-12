# ADR-0067: One grid function for line of sight, line of effect and cover

**Status.** Accepted, 2026-11-11 (Phase 14).

**Context.**
- Phase 13 put walls into place fights as blocked squares, but nothing else used them.
- The preflight audit confirmed:
  - a bow shot, a spell or a Fireball's sphere passed through solid wall;
  - the AI chose ranged targets by distance alone;
  - cover did not exist.
- Witnesses (Phase 14 crime) need to know what a person can see.
- One rule must serve all of these, or they will disagree.

**Decision.**
1. **`GridSight`** (`Domain/Combat/GridSight.cs`) is the only sight and cover function. It is pure, deterministic and uses integer arithmetic. It reads an `ISightGrid`: walls, low cover, and who stands where.
   - Fights use `EncounterSightGrid`: blocked squares, closed doors, low obstacles and the other combatants.
   - Places use the same function over their own cells, for witnesses.
2. **Geometry.** Squares are unit cells and corners are lattice points. A straight line is blocked when its open segment:
   - passes through the inside of a wall square;
   - runs along the seam between two wall squares; or
   - squeezes through a point where two wall squares touch diagonally.

   A line that only grazes a wall's face or corner is clear. The ends' own squares never block. The result is the same in both directions (tested over every pair of a drawn grid).
3. **Cover** uses corner lines: from each corner of the attacker's square to the four corners of the target's square, and the attacker's best corner counts.

   | Wall-blocked lines | Cover | Effect (SRD 5.2.1) |
   |---|---|---|
   | 0 | none | — |
   | 1–2 | half | +2 to AC and Dexterity saves |
   | 3 | three-quarters | +5 to AC and Dexterity saves |
   | 4 | total | cannot be targeted directly |

   A line crossing another creature or a low obstacle (and no wall) gives at least half cover. Different floors are total cover.
4. **Line of sight** is anything short of total cover. **Line of effect for areas** is the centre-to-centre line from the area's point of origin to each square. Only walls stop it; creatures do not. SRD 5.2.1: an effect spreads only along unblocked straight lines.
5. **Who uses it.**
   - Melee and ranged attacks are refused through total cover. Cover is added to the target's AC breakdown and named in the log.
   - Spells that target creatures need a clear line to each target. Point spells need a clear line to the point.
   - Area spells affect only squares their spread reaches; targets take the cover bonus on Dexterity saves.
   - Spells that ignore cover (Sacred Flame) skip the bonus but still need a clear line. Self spells need nothing.
   - The AI's ranged and spell choices check the same refusal.
   - Witnesses use it to see across rooms through open doors.
6. **Doors in fights.** A place fight carries its doors (`EncounterData.Doors`). Opening or closing one is the turn's free object interaction, or the action if that is already used. It changes the grid at once, and its state goes back to the place when the fight ends.

**Consequences.**
- Walls and doors mean the same thing to movement, attacks, spells, the AI and witnesses.
- The combat log says why a shot was refused ("total cover from a closed door") and why a hit missed ("+5 three-quarters cover from a wall").
- Cost: a cover check is up to 64 short segment walks (measured in the Phase 14 report, §U).

**Alternatives considered.**
- *Distance only (the old behaviour):* breaks the walls Phase 13 introduced.
- *Bresenham lines between centres:* asymmetric and inconsistent along walls and at corners. Rejected after the corner cases were written down.
- *Unity physics raycasts:* would put rules in the presentation layer and make the .NET harness unable to test them.
