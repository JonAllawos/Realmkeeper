# ADR-0119: Street life is drawn from the authoritative population, in two tiers

**Status.** Accepted, 2026-11-14 (Phase 27). No save change.

**Context.**
- **The problem.** A town reporting 1,644 inhabitants showed five or six people. Exploration drew only named NPCs, who are a tracked subset of `SettlementData.Population` (ADR-0009), and most of them are indoors at any hour.
- **The ask.** The settlement should look as populated, as varied and as economically active as the simulation says. That means more than decorative crowds, and no second simulation.

**Decision.**
1. **The unnamed majority is planned, not simulated.**
   - `TownsfolkPlanner` (Exploration.Domain, pure) decides who of the unnamed population is out:
     - a share of the authoritative population by hour, from 0.2 % at night to 6.8 % at midday;
     - minus the named already out;
     - capped at 160 for walkability.
   - What they do comes from the settlement's own labour (production workers by trade) and its places (the market, doors, stores, workshops, gates): errands and shopping, carrying, working by workshops, farmers through the gates, travellers, people gathering.
   - Each has a way along the roads (`LocalPaths.Route`), validated to stay on the streets.
   - **Same settlement, day and hour, same people.** Nothing is stored.
2. **The watch.**
   - About one guard per 250 inhabitants (1–10); half at night.
   - By day, a post at each gate. The rest patrol between the gates and the civic buildings.
3. **Two tiers.**
   - **Abstract:** every planned person's position is a pure function of time, so all of them exist without frame-by-frame simulation.
   - **Detailed:** the nearest 32 within 70 m get a full figure: the same humanoid bodies and appearance rules as named residents, looks seeded from the person's key. Figures are cached and reused, and built at most two each 0.3 s.
   - Townsfolk have no records, no collider and no conversation. Named residents are unchanged.
4. **The settlement's identity is shown from existing data.**
   - **Banners:** the owner's banner takes the strategic map's faction colour (the same faction list order).
   - **Shops:** trade signs from the kit by shop fit.
   - **Dressing:** everyday objects placed by rule from each building's purpose (`StreetDressingPlanner`), clear of roads, doors, buildings, water and fences.
5. **Shopkeepers call out** (data in `ShopCallouts`, by shop kind).
   - Only the keeper, only 3–12 m from the player, never during a window, fight or conversation.
   - One call per 14 s anywhere; once a minute per shop.

**Consequences.**
- A town of 1,644 has about 100 unnamed people and a watch of 6 out at noon, and 13 at 22:00. The market is about twice as busy as an equal area beyond it.
- Cost in the editor: +0.6 ms mean frame, and the townsfolk update about 0.12 ms. The owner accepted the measured worst-frame spike.
- Nothing contradicts the simulation:
  - the counts are the population's;
  - the trades are the economy's;
  - the banner is the owner's.
- Townsfolk cannot be talked to. That is deliberate: they are the unrecorded majority.
- **Addendum (2026-11-14, owner feedback):**
  - Children are part of the same derived population: a share of the day's unnamed people, never at night. No records and no save change.
  - Stall shops and forge yards are derived from the layout, like the other dressing.
  - Faction colours come from one shared table (`RkFactionColours`) for the map, banners and the watch.

**Alternatives considered.**
- **Instantiate named NPCs for the whole population.** Rejected: thousands of records and a second population model.
- **Decorative crowds at random.** Rejected: they would not follow the hour, the trades or the town's size.
- **Simulate every townsperson every frame.** Rejected: the pure function of time gives the same result at no cost.
