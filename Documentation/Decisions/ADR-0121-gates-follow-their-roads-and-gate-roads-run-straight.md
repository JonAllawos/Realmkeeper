# ADR-0121: Gates follow their roads, and the road through a gate runs straight

**Status:** Accepted (Phase 29, 2026-11-16). Not hand-played: the owner checklist is open.

## Context

- The owner reported a town gate that looked open but could not be walked through: a tower or the wall stood across the road beside the arch.
- Cause, found in the code (`ModularEnvironmentBuilder.Plan.cs` before Phase 29):
  - Each gatehouse was turned to face the **settlement's centre**, whatever direction its road ran through the wall.
  - The wall was cut by a fixed length along the wall line, which is too short where a road crosses the wall at a slant.
  - Generated roads can jog at the wall. In the owner's case, the road turned about 70° twice within 6 m of the gate, so no gatehouse could stand on it with both towers clear.
- At Alderholm a 22 m bridge lands at the gate while the road line zigzags beside the deck.
- Across 60 gates in 10 seeded worlds, 3 were blocked.
- The wall and gate geometry lived inside the renderer, so no engine-free test could check every gate.

## Decision

1. **The boundary is computed from the plan by one pure function set: `SettlementWalls`** (Domain, `Runtime/Domain/Settlements/SettlementWalls.cs`). The renderer draws what it returns, and the .NET tests check what it returns.
   - `Gates(plan, type)`: each gatehouse is centred on its gate and **turned to its own road's direction through the wall**. Its opening is the larger of the type's opening and the road's width + 1.2 m.
   - `Runs(plan, type)`: the wall runs between towers, **cut back to the towers along the wall line** (allowing for slanted crossings), plus posterns where lanes cross.
   - Gate sizes: towns and smaller 7.5 m opening with 4 m towers; cities 8 m with 5 m towers; fortresses 7 m with 5.5 m towers.
2. **The road through a wall gate runs straight (layout generator v3).** `StraightenGateRoads` replaces the road within 9 m of the gate (`StraightReach`) with a straight run through the gate point (±5 m, `StraightCore`).
   - The gate itself stays where it was on the wall.
   - A bridge that lands at the gate (Alderholm): the road follows the bridge deck, straight, and runs on through the gate (`AlongBridge`).
   - A gate is left as it was if a bridge is near but does not land at it, or if the new run would cross water or a building. Each skip is reported to the tests with its reason.
   - Straightening an already straight gate changes nothing (idempotent). Unwalled plans are untouched.
3. **Existing saves are updated once, on load.** Plans are persisted with `GeneratorVersion`. A plan saved before v3 is straightened in `WorldSaveMapper.Layouts` and marked v3. The result is the same as generating a new plan. The save schema does not change.
4. **The gatehouse, landmark and gate point are snapped onto the core circle**, so Phase 28's arrival (ADR-0120) still finds the gate exactly.

## Consequences

- 0 of 60 gates are blocked across 10 seeds, including re-planned cities and fortresses (`Phase29GateClearanceTests`, 5 tests, .NET).
- In a walled town, the player walks out through every gate and back in (`Phase29GatePlayModeTests`).
- An old save's settlements change shape near their gates on first load. No buildings move; only the road within 9 m of each gate does.
- A gate skipped for water or a building keeps its old road. None were skipped in the 10 test seeds; the tests report any that are.
- Not seen in a hand-played game.

## Alternatives considered

- **Widen every opening until the jog fits:** gatehouses grow far out of scale, and the tower still stands on the road.
- **Move the gate along the wall to a straight part of the road:** breaks Phase 28's arrival and the watch posts, which are derived from the gate point (ADR-0120).
- **Fix only new worlds:** existing saves keep blocked gates, and old and new settlements behave differently.
