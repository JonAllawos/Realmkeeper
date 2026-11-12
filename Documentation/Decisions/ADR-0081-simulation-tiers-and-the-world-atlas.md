# ADR-0081: Distant regions are simulated in tiers; the world atlas is a view of queries, not of the world

**Context.** Six regions hold about six times the people. Every system must keep running everywhere, but the player sees one region at a time. The interface must show the connected world, its roads, groups, events and travel without exposing the world model, and without a wholesale UI overhaul.

**Decision.**
1. **Tiers.** `SimulationTiers.TierOf(world, region)` gives each region a tier:
   - **Local:** the player's region;
   - **Regional:** its neighbours;
   - **Distant:** the rest.

   Aggregate systems (economy, trade, groups, events, politics) run everywhere, every day. `NpcSystem` gives people in a distant region their full daily update only on their weekly day. Their needs, schedules and knowledge catch up then. Measured: on a given day all home people get a full update and no distant person does.
2. **The player's region.** `SimulationTiers.PlayersRegion` is the region of the player's settlement, of the last settlement passed, or of the journey. The 3D world view shows that region's map and settlements only (`GetSettlementsInView`). The view rebuilds when the player crosses into another region.
3. **The atlas** (`AtlasQueries.View`, `AtlasView`) is a query snapshot that holds:
   - regions, with discovery, tier, landmarks, economy and faction shares;
   - settlements, roads, and groups seen in the player's region;
   - the events the player knows;
   - the journey and the party.

   Regions not yet heard of are drawn as "Uncharted lands", with nothing in them. The home region's neighbours are known by name from the start. Entering a region makes its neighbours known.
4. **The atlas window** (`RealmkeeperHud.Atlas`, key A) is a panel of the existing HUD. It has:
   - eight layers: Terrain, Settlements, Roads, Factions, Economy, Threats, Events, Travel;
   - a painted map with clickable marks, and a side inspector for a region, settlement, road, group, event or the party;
   - a settlement inspector that lists what the player knows is happening there or on its roads;
   - travel planning with the alternatives from the route data, and a travel strip: an hour, travel on, halt, resume, press on through the night, give up, the outlaws' three choices, and fighting it out.

   Every button dispatches a command or calls a session method. The view never edits the world.

**Consequences.**
- A simulated day of the six-region world costs about 17 ms. Building the atlas view costs about 0.13 ms (measured).
- People far away still live and change, only more coarsely. The difference is visible only in the timing of their small daily changes.
- The atlas shows no information the player has not obtained.

**Alternatives considered.**
- **Freezing distant regions.** Rejected: the brief requires that the world lives everywhere.
- **Drawing all six regions in the 3D view.** Rejected: it would cost too much and add little, and the atlas serves the overview.
