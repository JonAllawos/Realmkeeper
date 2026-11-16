# Phase 28: settlement arrival, gates and security

**Decision record:** ADR-0120.

**Code:**
- `Domain/Settlements/SettlementEntrances.cs`
- `Exploration/Domain/LocalEnvironmentGenerator.Plan.cs`, `LocalEnvironmentLayout.cs`, `Townsfolk.cs`, `LocalPaths.cs`
- `Exploration/Application/OverlandEntryHandlers.cs`
- `Application/Overland/OverlandQueries.cs`
- `Exploration/Presentation/ModularEnvironmentBuilder.Plan.cs`

## Arrival

| Way in | Where the player stands |
|---|---|
| Strategic map (Enter) | Just inside the **main entrance**, on the road, facing into the settlement |
| On foot from the country | Just inside the entrance whose bearing is nearest the way the player came (normally the main gate, since the country's way in lies on its road) |
| Load | Where the save was made (unchanged) |
| Again from the map | The same main entrance (deterministic) |

**The main entrance** is the plan's own front gate: its first main road (`Gates[0]` and the `lm-gate` landmark, "the town gate").

**Standing points:**

| | Inset from the gate | Tried in order |
|---|---|---|
| Wall gate | 11 m in | 11, 14, 9, 18 m |
| Unwalled way in | 4 m in | 4, 7, 10, 2 m |

Each point is checked to be:
- on the plan;
- out of water (1.5 m);
- clear of every building (1 m) and fenced field (0.5 m);
- on the road (by construction).

**Fallback, in this order:**
1. the next entrance in rank;
2. only if none is clear, the plan's stored edge point, reported as `ArrivalIsFallback`.

In 45 plans across seven test worlds, no arrival used the fallback; one used a secondary gate.

**Old saves:** entrances are derived from the persisted plan at load, so existing settlements arrive at their gates too. **No save change.**

## Gates and walls (towns, cities, fortresses)

**Gatehouses** stand over each gate (as before), now:

| | Opening | Towers | Height |
|---|---|---|---|
| Town | 7.5 m | 4 m | As before |
| City | 8 m | 5 m | +2.5 m |
| Fortress | 7 m | 5.5 m | +4 m |

**Each gatehouse has:**
- **Battlements** on both faces of each tower; stone **corbels** under the lintel so the opening reads as an arch.
- **A raised portcullis:** an iron grid at the outer face, about a metre showing below the arch, more than 3 m above the road. No collider.
- **Two plank leaves, iron-banded, open** and laid back against the sides of the passage inside, with thin colliders along them. They never narrow the way.
- **Wall lanterns** either side of the opening, inside and out.
- **Foundations** below the lowest ground under each tower. Before, the towers reached 2 m down, and the steepest gate site measured had 2.44 m of fall.

**Walls:**
- The wall runs **right up to each gatehouse's towers**. Before, whole 20 m segments near a gate were left out, leaving gaps of up to 20 m.
- Where a lane crosses the wall, the wall stops either side of the lane (wider where it crosses aslant), and a **stone lintel** spans the gap: a postern.

**Tested in the running game:**
- a player-sized capsule passes from 16 m outside the gate to the arrival point;
- every 0.8 m of wall within 14 m of the main gate meets a tower or wall;
- every tower's foundation is below the ground at its corners;
- the portcullis is more than 3 m up.

## Ways in (villages and hamlets)

Unwalled settlements have no gate, but their way in is marked, across the main road where it enters the built core:
- **Village:** a timber gateway (two posts with braces and a beam), a wall lantern on a post, and a lamp post.
- **Hamlet:** a stone waymarker and a lamp post.

Other ways in get a waymarker and a lamp. An unwalled way in that falls on a bend of its road moves along the road to the nearest straight stretch (up to 18 m in, or 9 m out), so the gateway stands square across the road.

## Security (the existing watch, extended)

- **Watch size:** one guard per 250 inhabitants, 1–10, none under 120 people (as before), with these additions:
  - **walled towns:** at least two (the main gate's pair);
  - **cities:** half as many again;
  - **fortresses:** twice as many (at most 16);
  - **by night:** half, never fewer than the main gate's pair in a walled town.
- **Posts:** a pair at each entrance, beside the road (road half-width + 1.5 m) just inside, facing out of the gate. They are manned in this order:
  1. the main gate's pair;
  2. one at each other gate;
  3. the second at each other gate.
  - By night only the main gate's pair.
- **Named gate guards** (the simulation's residents posted at the gate) stand at the first posts (`GatePost(n)`). The unnamed watch takes the posts left over and counts them toward its number, so **no post is held twice and no guard is duplicated**.
- **Patrols:** the rest patrol between the gates and civic buildings (Phase 27, unchanged).
- **Uniforms:** guards wear the owner's colour (Phase 27, shared `RkFactionColours`).
- **Hamlets** of under 120 people keep no watch.

**Measured in the test town** (Bramhaven, 1,644 people, two gates):
- 3 named gate guards;
- 1 unnamed guard at a post;
- 2 on patrol.

## Roads to the gates

**Measured over 90 entrances in 45 plans:**
- **Main roads** pass straight through their gates by construction. The sharpest turn within 15 m of a wall gate is 57°. A wall gate stands where the road meets the wall, so it is not moved; straightening those roads would change the generator, and only for new worlds.
- **Unwalled ways in:** every one now stands on a straight stretch, under 25° within 4 m.
- **Hill creases** on the approach roads, away from bridge banks: at most 8.9° change of grade per 2 m. The sharp readings (up to 53°) are all at river banks under bridge decks, which are not walked; bridges are unchanged (owner-approved).
- The road is draped on the walked ground (3 cm above it, Phase 26.1 test, still passing). The terrain's 2 m triangles were **not** smoothed: that would change the walked ground.

## The country

The way in from the country (`OverlandQueries.GateOf`) is the road cell, six cells (240 m) from the settlement, nearest the bearing of its main gate. Its main road was laid out toward that very road cell's direction, so:
- leaving for the country and walking back in lands at the main gate (tested);
- the overland site, navigation and the waypoint all use the same point.

## The local map

The HUD map is cleared and redrawn on each entry (Phase 26.1). Tested after entry from the map, from the country, after a load, and in a village and a hamlet after a town: the map draws the player where they stand, never at a previous place's position.

## Not done

- **Cities and fortresses:** not seen in the test world (it has none in view). Their larger gatehouses and watch are implemented and covered by logic tests, but not captured or seen.
- **Gates that close at night:** the gates stay open; no new access or crime rules (as the brief asked).
- **Faction architecture beyond the watch and banners** (notices, faction buildings): not part of this phase.
