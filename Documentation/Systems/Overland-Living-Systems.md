# The living country (Phase 24)

**Decisions:**
- ADR-0113: one travel state, the map position, waypoints.
- ADR-0114: landmarks, travellers, encounters, save v26.
- ADR-0115: ground weather.

**Builds on:** `Overland.md` (Phase 23).

## Landmarks

**Generation.** Landmarks are generated per region from its seed and geography (`LandmarkGenesis`), cached for the session and never saved. Their ids are stable: `landmark:{region}:{kind}:{n}`.

| Kind | Placed | Use |
|---|---|---|
| Waystone (at most 6) | Road junctions away from towns; every 40 cells along a long road | Read: each road's settlement, its direction, hours on foot (a road cell is a quarter of an hour) |
| Hilltop cairn (at most 4) | Hill, rocky or heath tops, 4+ cells from roads, 24 apart | Climb: finds places and landmarks within 650 m (less in poor weather and at night) |
| Travellers' camp (at most 3) | Beside the middle of roads of 24+ cells | A long rest here is never broken |
| Abandoned camp (2) | Woods and hills, 5–14 cells from a road | Search once: a small cache (`old_cache`) |
| Ruined steading (2) | Open ground, 3–12 cells from a road | Search once: a small cache |
| Watch post | Where a faction's settlement sends a road into another region | Shows the player's standing; lowers encounter odds within 700 m |
| Wreck | Only where a real `CaravanLost` event lies on the region's road | Search: confirms the event, records a discovery, and a cache |

**Discovery.** A landmark is found by sight while walking (the same distance as places, shortened by weather and night), or from a cairn.

**Saved.** Only `LandmarkStateData`: found, searched, rested.

**Views.** Built from the owned kits: stones and rocks, the props kit's fire pit, tripod, crates, barrels and wagon, the village kit's walls, fences, banners and a lantern post.

## Travellers

**Who they are.** The world's own `TravelingGroupData` (Phase 16): caravans, merchant parties, patrols, faction patrols, pilgrims, refugees, bandits.

**Where they are.** Placed where the simulation has them now (`Travellers.InRegion`) on the region's road. Nothing is created to fill the view.

**Bodies.**
- Made only within 600 m, at most four figures a group: the real leader's looks plus companions.
- A caravan has its wagon.
- Pooled, and dropped beyond 800 m.

**Talking.** Talking shares news: the unresolved events of their origin, destination or roads, learnt as rumours. A patrol answers by your standing and notoriety.

**Refugees.** Giving refugees a ration raises their home's reputation and their leader's gratitude, once per group.

**Bandits.** They are met as encounters, not conversations.

## Navigation

**The atlas marker.** It is the persistent position: the pose in the country, a place's cell, the road position, or the settlement.

**Turning to the map from the country.** First you walk (its time charged) to:
- the waiting journey's place, when within 80 m of it;
- a settlement, when it is nearer than any road or within 400 m;
- otherwise the nearest road, as a road position the journey planner understands.

**Stepping off a journey.**
- The atlas's "Walk from here (on foot)" (or entering the country while on the road) halts the journey where it is and puts you on the road there.
- The compass marks the place.
- "Back on the road (J)" resumes it within 80 m.
- Arriving at the destination on foot ends it; another settlement or region gives it up.

**Waypoints.** "Mark on compass" on the map's settlement panel, or the journey's place.
- Only known destinations can be marked.
- The compass shows a gold mark; the HUD gives the name and distance.
- In another region, it points to the way on toward it.
- "Clear the mark" removes it.

## Encounters

**When.** Checked as you walk, once per half-hour of the clock (a bucket id), with three quiet hours after any encounter.

**Sources.** Only real ones:
- a creature population whose range covers the place (challenge above 0);
- a bandit band holding a road within 160 m.

**Odds.**
- Raised by: night, deep woods, marsh and rocks.
- Lowered by: roads, towns, watch posts, patrols and poor visibility.

**Choices.**
- **Fight:** the one combat engine, on the ground where you stand.
- **Slip away:** a Stealth check, with cover helping.
- **Back off.**
- **Pay** a band's toll.
- Walking off more than 200 m counts as backing off.

**Saved.** The outcome is kept (the 40 most recent), and loading never rerolls one.

**Night rest.** A long rest in the open at night can be broken by a population whose range covers the place; a travellers' camp is safe.

## Ground weather

The ground's state comes from the region's saved weather, by the `GroundConditions` rule:

| State | Effect |
|---|---|
| Wet | Slows walking off the road |
| Mud | Slows walking off the road more |
| Snow, deep snow | Slows all ground, roads least |
| Frozen | Makes fords icy |
| Floods | Bar fords (cross by a bridge) |
| Fog, heavy rain, snow | Shorten sight |

The same factor slows the clock and the player's pace. The HUD names the condition; the ground darkens when wet and whitens under snow.

## Saves (v26)

`overland` holds landmark states, encounter records and the encounter clock. The player also gains `waypoint`. Both are optional: a v25 save loads with nothing found, nothing met and no mark.

## Performance

Measured in the editor in batch mode, not a player build; see the Phase 24 completion report.
- **Landmarks:** generation runs once per region per session.
- **Travellers:** the refresh runs once a second.
- **Ground:** the material updates only when the condition changes.
- **Never:**
  - another region's country is not built to place a traveller or a waypoint;
  - nothing scans the world per frame.

## Known limits and extension points

- **Not here yet:**
  - escort requests;
  - stranded travellers;
  - roadside trade;
  - quests begun by encounters;
  - patrols turning hostile in a fight.
- **Map positions** are road, settlement or place positions, never arbitrary off-road points (no off-road route planner).
- **Flooded fords** are barred and announced, but the water is not drawn higher.
- **The ancient realm** has no landmarks or encounters, because its content is undecided. Realm-specific sources can be added through `RealmIds` checks in `Landmarks.InRegion` and `OverlandEncounters.Odds`.
