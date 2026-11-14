# Phase 23: Owner playtest checklist

Nobody has hand-played Phase 23. These are the checks only a person at the keyboard can make. Each says what to do and what you should see. Tick what holds; note anything that does not.

**Start with a new world.** Older saves keep 6 regions and load unchanged, but only new games have 9.

## Going out and walking

1. **Out to the country.**
   - Create a character, then in the town press **O** (or click "Out to the country (O)").
   - A loading screen names where you are going and fills its bar.
   - You arrive on a road outside the town, on the ground, not inside a building or under the terrain.
2. **The scale.**
   - Look around. You should see:
     - the town's silhouette behind you;
     - roads leading away;
     - forests, hills and distant mountains through the haze.
   - Walk (WASD; Shift to run) for a minute. Nothing should pop in close to you, and the frame rate should stay smooth.
3. **The clock.**
   - Note the time, then run along a road for about a minute. The clock should advance about as fast as in town.
   - Then walk through forest or marsh for the same time. The clock should advance faster.
4. **Barriers.**
   - Try to walk into a mountain, a lake or a river away from a bridge. An invisible wall should stop you at the edge.
   - Find a **bridge** (planks where a road crosses water) and a **ford** (shallow water on a river's course, every few hundred metres) and cross both.
   - Walk into a **tree trunk** or a **big rock**: it stops you. Grass, ferns, flowers and bushes do not.
5. **Compass, location line and map.**
   - The compass strip shows directions and known sites.
   - The line under it names the region, the ground you stand on and the nearest known site with its distance.
   - Press **N**: the region map shows ground, water, roads, settlements, the ways on, and you.

## Destinations

6. **Into another settlement.**
   - Walk to another town (the compass or the map shows it).
   - At its banner, press **E**: a loading screen, then you are inside.
   - Press **O** to go back out: you are at its gate.
7. **Finding a place.**
   - Walk toward hills or forest away from the roads.
   - Within about 140 m of a cave, ruin or fort you have not found, it becomes discovered: it appears on the compass and map, with its name.
   - Go in (**E**). Leave it: you should be back in the country by its entrance, not on the world map.
8. **The road on.**
   - Follow a road to the region's edge, to a **signpost** ("The way on to …"). Press **E**: you arrive in the neighbouring region at its edge.
   - Go back the same way.
   - If a crossing is closed (for example, a bridge is out), the signpost should say why and refuse.
9. **Saving in the country.** Save (F5) in the country, quit to the menu and load. You should be where you saved, in the country.
10. **Back to the map.** Return to World Map (**M**) works from the country.
   - On the map, select a settlement of your region and click **Walk the country**. A loading screen, then you are at its gate (beside its banner, not inside it).
   - Select a settlement in another region and click it: it refuses ("travel there first").

## The gateway (only through the campaign hook for now)

11. **Dormant.**
    - Walk far from the settlements until you find a ring of standing stones; it is discovered by sight.
    - Its prompt says it is dormant and refuses.
12. **Opened.**
    - Nothing in play opens the gateway yet; only `SetGatewayStateCommand` does, as the Phase 23 PlayMode test shows.
    - If you open it in the editor, the ring lights.
    - Passing through takes you to the ancient prototype region: unnamed, with a different tint, nobody there.
    - The gateway at the far end brings you back.

## Things to judge (owner calls)

13. **The scale.** Does 40 m a cell feel right? A region takes about 14 minutes to cross at a run. (D1; ADR-0110.)
14. **More to find.** In a region other than your home, walk the hills and woods. You should come across a hollow, a fallen steading, a barrow or old workings, each named after a nearby settlement ("The hollow near …"). Is it still too empty?
15. **The look.** Judge:
    - the textured ground: grain, not flat colour; rock on steep and rocky ground; dirt on heath and shore;
    - grass and flowers around you;
    - the towns: as you come within about 1.4 km, a town appears in its own kit buildings, with a brief hitch (up to about 0.15 s) as it does;
    - the location panel (region · in the country · ground), and no practice-fight or outing buttons in the country.
16. **Weather.** The far-east regions should now be about as wet as the rest.
