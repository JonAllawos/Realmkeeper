# Travel, the atlas and groups on the roads (Phase 16)

Code:
- `Runtime/Domain/World/Atlas.cs`: route segments, journeys, travelling groups and world events, plus `TravelRules`.
- `Runtime/Simulation/Atlas/`: `RoutePlanner`, `GroupSystem`, `WorldEvents`, `WorldEventSystem` and `FactionActivitySystem`.
- `Runtime/Application/Atlas/`: `TravelCommands`, `WorldSession.Travel` and `AtlasQueries`.
- `Runtime/Infrastructure/Generation/WorldAtlasGenesis.cs`.
- `Runtime/Presentation/UI/RealmkeeperHud.Atlas.cs`.

Decisions: ADR-0076 to ADR-0081.

## The connected world

- A new game has **six regions** on a 3 × 2 atlas grid. The home region, where the player starts, is at (0, 0).
- Each region is made by the same `RegionGenerator` from a seed derived from the world's seed. Each therefore has its own terrain, river, three settlements, people, factions, economy, layouts and wildlife.
- Mountain ranges continue across borders. Crossings are a pass, a bridge, a forest trail or a road, chosen by the terrain on both sides.
- Positions are local to their region. The session keeps one map per region.
- **Discovery:**
  - the home region is visited, and its neighbours are known by name;
  - the far regions are uncharted until the player enters a region that borders them.
- An existing one-region world stays as it is. *Discover the lands beyond* (atlas button, or `WorldSession.DiscoverLandsBeyond`) adds the other five once, deterministically.

## The route graph and the scale

| Quantity | Value |
|---|---|
| A cell | 1.2 km |
| Walking pace | 4.8 km/h |
| Night (20:00–06:00) | half pace |
| A travel day for groups | 8 walking hours |
| A segment's hours | km ÷ 4.8 × terrain % × condition (worn: × 1.25) × damaged bridge (× 1.15) |

A segment joins two settlements. Its kind, length, terrain cost, condition, bridge and blockade are saved.

- A segment is **closed** when it is blocked, its bridge is destroyed, or a blockade holds it.
- `RoutePlanner.Plan` (Dijkstra) and `Alternatives` (Yen, up to three) plan only over open segments.
- Every plan lists its notes: crossings, passes, damaged bridges, worn roads, and outlaws the player has heard of.

## Journeys

1. **Choose.** Choose a settlement on the atlas. The inspector lists the ways there, from the route data: distance, duration, arrival time, and what lies along the way.
2. **Set out.** *Travel by the best way*, or by an alternative, dispatches `StartJourneyCommand`.
3. **Walk.**
   - *Travel 1 h* moves one hour along the segments (`TravelHourCommand`), then advances the world's clock by an hour.
   - *Travel on* continues until night, trouble or arrival.
   - *Press on through the night* continues at half pace.
4. **Stop.**
   - *Halt* and *Resume*.
   - *Give up the journey* leaves the party where it stands, on the road.
5. **Turn.** A new destination can be chosen from mid-road. The plan starts from where the party stands, either way along the road.
6. **Closed roads.**
   - A road that closes ahead of the party (a bridge falls, a blockade) stops the journey.
   - The party can always turn back the way they came. The closure lies ahead of where they stood. The plan says "back the way you came", and the journey records it (`back`).
7. **Arrival.** The town is entered as on any other visit, with the party.
8. **Other regions.** A settlement in another region can be entered only by travelling there. Crossing a border makes the new region visited and its neighbours known, and the 3D map follows the player.

### Outlaws on the road

Each hour on a road held by a band rolls once, keyed by the journey and the hour:

`chance = 4 + 4 × strength (+6 at night) − 2 × party size`, capped at 40%.

The strip then offers three choices:

| Choice | What happens |
|---|---|
| **Fight** | The one encounter engine runs, with combatants drawn from that band's population. The party joins. |
| **Slip past** | Stealth against DC 10 + strength. |
| **Pay** | 5 coin × strength. |

A band met once does not bar the same journey again.

**Fights on the road (Phase 17, ADR-0086).** Choosing to fight (or being seen while slipping past) starts the one engine's encounter, drawn near the road's nearer end, as before. The view now changes to it by itself: the exploration layer stages the fight on the open ground of that end of the road (the settlement's land without its buildings or people) and the existing combat panel runs it, with the tactical grid, the party and the outlaws. In the world the party stays on the road. When the fight is over, the ground stays until the player leaves it (closing the panel, or the HUD's return); the journey is then halted where it was ("after a fight on the road"), and travel goes on only when the player resumes it. Saving mid-fight keeps the fight and the road. "Let it play out (the AI plays your turns)" remains on the atlas as an explicit, labelled choice, never the default. Limitation: the ground shown is the near settlement's land, not a generated stretch of the road itself.

**Groups beyond sight (Phase 17).** On the atlas, groups in the player's own region are seen where they are (letters). Elsewhere a group shows only where a report the player has puts it — a world event they know of that names it — marked "?", as of the report and from whom, with no live numbers or history. Unknown groups are not shown.

### The party

- Active members with the player travel with them. They lower the threat, join fights, and arrive together.
- Reserves stay at home and live their ordinary lives.
- A member told to *wait* keeps the spot their body stood on (`waitPose`) and is found there on return.
- *Regroup* clears it.

## Groups on the roads (`realmkeeper.groups`, order 160)

| Group | Where it comes from |
|---|---|
| Caravan | A shipment in transit (the economy's own goods). Its leader is a person of the origin: merchants first, never a shopkeeper, someone posted, a party member or an escort, and one per settlement at a time. Its guards come from the cargo. |
| Bandits | A road band's creature population, stationed on its road, with strength equal to the population count. |
| Patrol / faction patrol | Sent by a faction (paid from its treasury) against outlaws, or along a border in a conflict. |
| Refugees | People leaving a settlement in long unrest or shortage. |

Each day, a caravan on a road held by outlaws may be attacked:

`chance = 10 + 10 × band − 6 × caravan strength`, capped at 60%.

- **Repelled:** the history says so.
- **Lost:** the goods are recorded as `lostOnRoad`. A `CaravanLost` event and a chronicle line follow.
- **Closed road:** the caravan halts and raises `TradeDisruption`.

A leader away is not at home, so towns do not draw them.

## The atlas (A)

**Layers:**

| Layer | What it shows |
|---|---|
| Terrain | Regions by kind; uncharted lands blank |
| Settlements | Settlements |
| Roads | Pale (open), ochre (crossing), dark red (closed) |
| Factions | Settlements tinted by faction |
| Economy | Settlements short of food marked |
| Threats | Outlaws |
| Events | `!` known, `?` rumoured, `~` out of date |
| Travel | The journey and the party |

**The inspector** shows whatever is selected:

| Selection | What it shows |
|---|---|
| Region | Kind, discovery, tier, landmarks, economy, faction shares, outlaws the player knows of |
| Settlement | Its standing, the news the player knows about it or its roads (open first; ended ones dimmed), its roads, and the ways there |
| Road | Kind, length, hours, condition, bridge, closed |
| Group | Kind, state, leader, cargo, route, history |
| Event | What the player knows, as of when, from whom; its history when seen |
| Party | Who travels with the player |

The view is built from queries (`AtlasQueries.View`). It shows only what the player knows. Groups are shown in the player's own region only.

## Measured (six regions, 18 settlements, 119 people, 25 road segments)

| Measure | Result |
|---|---|
| Route planning with three alternatives | 0.19 ms per pair |
| A simulated day | 16.9 ms |
| An hour of travel, including the clock | 6.3 ms |
| The atlas view | 0.13 ms |
| Save | 112 ms, 5.0 MB |
| Load | 371 ms |
| Generation | 0.6 s |

## The walkable country (Phase 23)

The travel scale above is unchanged. Each region can also be walked at 40 m a cell, where one walked metre stands for 30 m of road, so a walked road cell takes the same 15 game minutes as a journey. Walking advances the same clock. New games have 9 regions. See `Overland.md`, ADR-0110 and ADR-0111.
