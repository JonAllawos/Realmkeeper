# ADR-0105: Seasons and regional weather live in the simulation; the street only shows them

**Context.** Phase 21 asks for weather and seasons that exist in the simulation, not only on screen. They must:
- differ by region, change believably, and be deterministic and saved;
- reach gameplay through existing systems (production, travel, people's days);
- be drawn in Unity without a third-party weather asset (owner decision, 2026-11-12).

Before Phase 21, the world had no climate, weather or season. It had a seasonal hook in the economy that returned 100 for every date, and a fixed sunrise and sunset.

**Decision.**

1. **Seasons come from the calendar that exists.** No dates change.
   - The seasons are spring (Seedtide–Greenmonth), summer (Highsun–Goldfall), autumn (Reaping–Frostcall) and winter (Longnight–Thawmoon).
   - Daylight runs from 16 h at midsummer (21 Highsun) to 8 h at midwinter, around a solar noon of 12:50. The noon sun is 42°–66° high.
   - All of this is in `SeasonRules`.
2. **A region's climate is derived from what the world already knows**, not invented: its kind, its latitude (atlas Z: north is colder) and its mountains.
   - Mountains on the west side (generator directions 3–5) catch the westerlies: wetter.
   - A highland region to the west casts a rain shadow: drier.
   - `RegionClimate.Describe()` says why in words.
   - No new geography or canon is introduced.
3. **Weather is worked out once a day, for every region,** by `WeatherSystem` (`realmkeeper.weather`, order 40: before labour and production, so the day's work sees the day's weather). The model (`WeatherModel`) works in five steps:
   1. **Fronts.** Bands of moist air are born at the atlas's western edge, more often in autumn and winter. They drift east about a region a day, weakening as they go. Neighbouring regions therefore share weather a day apart.
   2. **Moisture.** Each region's moisture follows its climate and the fronts over it. It carries over from day to day and is blended with its neighbours'.
   3. **Temperature.** It follows the season and climate, plus a daily rhythm (warmest at 15:00). Cold fronts lower it.
   4. **Eight three-hour periods.** A smooth value per region, continuous across midnight, sets rain or snow against the moisture. Below about 1 °C it snows. Heavy spring and summer rain can become a thunderstorm. Still, damp, cool mornings are foggy. Hot, dry summer afternoons in warm regions are "hot and dry".
   5. **The ground.** Wetness and lying snow are updated.

   Every random value comes from `SeedMixer` keyed by the world seed, the purpose, the day and the region, so the same seed and saved state give the same weather. At most 30 days are caught up after a long gap.
4. **Saved in schema v23** (`environment`: each region's day, moisture, ground and snow; the fronts).
   - Front positions are stored as doubles, so a save and load continue exactly as an unsaved run would; a test checks this.
   - A save without weather (v22 or older) gets it from the seed when the session loads it.
   - The v22 → v23 migration adds the weather system to the save's enabled systems, first, so old worlds simulate it.
   - A new, loaded or newly connected world has today's weather at once (`WorldSession.Replace`). A region added mid-day gets that day under the fronts as they stand.
5. **Effects through existing systems only.**
   - **Production:** the season, through the economy's existing hook (`EconomyRules.WithSeasons`, used by the game's composition).
     - Farming yields 85 % in spring, 110 % in summer, 140 % in autumn and 65 % in winter.
     - Fishing, hunting and logging vary less.
     - Each source averages 100 % over a year (tested), so a settlement's annual balance is unchanged.
     - Plain `new EconomyRules()` stays flat, so the economy's own arithmetic tests are unaffected.
   - **Travel:** `TravelRules.WeatherFactor` slows each hour on the road, and the hour's message says why.
     - Heavy rain × 0.8, thunderstorm × 0.7, falling snow × 0.85, heavy snow × 0.6, fog × 0.9.
     - Lying snow × 0.85; a pass in snow × 0.75.
   - **People:** `NpcActivityPlan`.
     - In a storm or heavy snow, people who work in the open (market, square) shelter at home until the period ends. Guards keep their posts.
     - The curious skip the market in rain or snow.
     - Fewer people go to the tavern on a stormy evening; those who would have gone say they are staying in.
   - No survival, temperature or clothing system is invented.
6. **Presentation only shows it.**
   - `WorldSession.GetWeather()` gives a worded summary: the season, the period's weather, today's outlook, sunrise and sunset, the climate's description, and the effects now.
   - In a settlement, `ExplorationAtmosphere` applies the season's daylight and the weather to the light: cloud dims the sun and greys the ambient light, haze thickens in rain, snow and fog, and the sky greys or turns night-blue.
   - Ground and roof materials, as per-visit copies, darken and gloss when wet and whiten under snow.
   - `WeatherView` draws:
     - rain and snow particles around the camera;
     - a world-pinned, wind-driven cloud layer;
     - stars and the moon on clear nights;
     - lightning in storms;
     - generated rain, wind and thunder sound, at the player's sound volume.
   - Everything is built in code and destroyed on leaving. Interiors and places show none of it.
   - The HUD clock shows the weather and temperature, with the outlook and effects on hover.

**Consequences.**
- Weather is part of the world state that everything reads. The atlas, the street, travel and people agree because they read the same record.
- The cost is about 50 random draws per region per day: negligible beside the rest of the daily tick (measured in the Phase 21 report).
- Weather of days other than today is not known, so the activity plan uses weather only for the current day.

**Alternatives considered.**
- **Weather only in presentation** (a random sky per visit): rejected. The brief requires simulated weather that affects the world and persists.
- **Per-region independent random weather:** rejected. Neighbours would not share weather, and rain would stop at borders.
- **A third-party weather package:** rejected by the owner's decision to keep weather inside Unity. It would also tie a simulation concept to a presentation asset.
- **Seasons in `new EconomyRules()` by default:** rejected. It changes the numbers of every economy unit test that checks arithmetic, not seasons. The game's composition opts in instead.
