# ADR-0120: Settlement entrances are derived from the persisted plan

**Status:** Accepted (Phase 28, 2026-11-15).

## Context

- Travellers arrived at `plan.Arrival`, a point stored when a settlement's plan was generated: 30 % in from the **outer end** of its first main road, at the edge of the plan.
  - For a walled town that is some 85 m outside its walls, in open fields.
  - Every settlement's arrival looked the same.
- The plan already knows its gates: where each main road crosses the wall.
  - Its first main road's gate (`Gates[0]`) is its own "town gate" landmark.
  - Nothing used them for arrival, the gate guards or the watch.
- Plans are persisted. Changing what the generator stores would only reach new worlds, and old and new settlements would then behave differently.

## Decision

**Entrances are derived from the persisted plan, by one pure function, at load: `SettlementEntrances.Of(plan)`. Nothing new is saved.**

| Concept | Rule |
|---|---|
| **An entrance** | Where a main road crosses the wall (walled), or the built core's edge (unwalled), found from the centre outward. Each has: the gate point, the inward direction along the road, a validated standing point just inside on the road, and two watch posts beside the road facing out |
| **Standing point** | 11 m in through a gate, 4 m in at a way in. Others are tried in a fixed order. Each is checked clear of buildings, fenced fields and water, and on the plan |
| **Unwalled way in** | Not fixed by a wall: moved along its road to the nearest straight stretch (inward first) when the crossing falls on a bend |
| **The main entrance** | The plan's own front gate: its first main road (`Gates[0]`, the `lm-gate` landmark). Nothing is invented beside the plan |

**Arrival:**
- **From the strategic map:** just inside the main entrance, facing in.
- **From the country:** by the entrance facing the way the traveller came.
- **Fallback:**
  - an entrance with no clear standing point gives way to the next in rank;
  - only if none has one is the plan's old edge point used, and the layout reports it (`ArrivalIsFallback`).

**Everything that refers to the gate uses the entrances:**

| What | Uses |
|---|---|
| The named gate guards | `layout.Gate` and `GatePost(n)`: a post each |
| The unnamed watch | The posts the named guards leave free; the main gate's pair first |
| The gatehouses' inward side | The entrance it stands on |
| The way in from the country | `OverlandQueries.GateOf`: the road cell, six cells out, nearest the main gate's bearing |

## Consequences

- Entering from the map, from the country, again, or after a load lands at the same front gate, for old saves and new. **No save change.**
- A settlement's identity, position and stored plan are untouched. Only how arrival is read from the plan changed.
- **Tested:**
  - 45 plans across seven worlds (15 walled): every arrival is at an entrance, never the fallback;
  - one plan arrives by a secondary gate (its main gate had no clear standing point);
  - the fallback order itself is tested by blocking gates.
- The overland way in moved to the road leading to the main gate. Where a settlement's roads leave in several directions, the country entrance may be on a different side than before.
- **The watch:**
  - named gate guards now count toward it, so posts are never manned twice;
  - a walled town keeps at least its main gate's pair (by night only that pair);
  - cities half as many again, fortresses twice as many.

## Alternatives considered

- **Re-generate arrival in the plan generator.** Rejected: only new worlds would change, and the stored point would need a migration.
- **Spawn at the plan's centre or the square.** Rejected: it skips the arrival the owner asked for, the gate.
- **A separate gate-guard system.** Rejected: the existing watch planner and named residents already cover it; a second system would duplicate people.
