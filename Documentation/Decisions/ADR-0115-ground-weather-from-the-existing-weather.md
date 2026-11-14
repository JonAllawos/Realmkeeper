# ADR-0115: Ground weather derived from the existing weather

**Status.** Accepted, 2026-11-13 (Phase 24). It adds to ADR-0105 (seasons and weather) and ADR-0110 (overland terrain).

**Context.**
- **What already exists.** Phase 21's weather is simulated and saved per region: eight three-hour periods a day, moisture, `GroundWetness` and `SnowCover`.
- **What is missing.** The walkable country ignored it.
- **The brief.** Weather must change walking, crossings, sight and the ground's look, consistently and explainably, without simulating every cell every frame.

**Decision.**
1. **`GroundConditions` (Domain, engine-free) is a pure rule over the region's saved weather and the minute of the day.** Nothing new is simulated or saved, so the same weather always gives the same ground.

   | State | Condition | Road | Off the road | Ford |
   |---|---|---|---|---|
   | Wet | wetness 40+ | ×1.0 | ×1.15 | ×1.1 |
   | Mud | wetness 70+ | ×1.1 | ×1.4 | ×1.3 |
   | Snow | snow cover 20+ | ×1.1 | ×1.5 | ×1.2 |
   | Deep snow | snow cover 60+ | ×1.3 | ×2.0 | ×1.3 |
   | Frozen | ≤ −2 °C on wet ground | ×1.05 | ×1.1 | ×1.5 (icy fords) |
2. **Fords flood** when the ground is sodden (wetness 85 or more), or wet (70 or more) while it rains hard (intensity 60 or more).
   - **In the rules:** stepping onto a flooded ford is refused, with the reason and the way round ("cross by a bridge").
   - **In the view:** the ford's cells become barriers.
   - **Unchanged:** bridges are roads, and journeys' routes are roads, so they are not affected.
3. **Sight factor:** thick fog ×0.4, fog ×0.6, severe weather ×0.6, rain or snow ×0.8; night halves it.
   - It shortens the distance at which places, gateways and landmarks are found.
   - It shortens a cairn's view.
   - It lowers encounter odds slightly.
4. **One factor, two effects.** `OverlandTerrain.WalkMinutes` multiplies each sample by the cell's factor, so the clock charges it. The player's pace is divided by the same factor (`PlayerCharacter.SpeedMultiplier`). The HUD names the condition and its effect, and says nothing on dry ground in clear air.
5. **The ground's look** is two material values on the overland ground (`_Wet` darkens the ground, `_Snow` whitens level ground). They are set only when the condition changes; no terrain deformation, no per-frame work.
6. **Other realms:** a region without a weather record (the ancient prototype) is calm.

**Consequences.**
- **No save change for weather.**
- **Regions are consistent across transitions:** each region's ground follows its own weather.
- **Limits:**
  - water levels are not drawn higher on flooded fords; the barrier and the HUD carry it;
  - rivers in spate do not damage bridges (the existing `BridgeDamaged` events do that);
  - roads are not closed by snow.
