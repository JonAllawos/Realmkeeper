# Phase 24: Owner playtest checklist

**Automated verification (done by Claude):** the .NET tests, the Unity EditMode and PlayMode tests listed in the Phase 24 completion report, and the screenshots in `Documentation/Screenshots/Phase24/`.

**Manual verification (still yours):** everything below. Nobody has hand-played Phase 24. Each check says what to do and what you should see; tick what holds and note what does not.

Start a **new world** (older saves load, but only new worlds have nine regions), create a character, and let a few days pass on the map so the roads fill.

## Landmarks

1. **Waystones.**
   - Walk out of a town (O) and follow a road.
   - Within a few hundred metres of a junction you should find a waystone (a standing stone).
   - Read it (E): it names where each road goes, which way, and how long on foot.
2. **A camp or a steading.**
   - Off the road, in woods or open ground, find an abandoned camp or a ruined steading.
   - Search it (E): a small cache opens once.
   - Search again: "You have searched here already".
3. **A cairn.**
   - On a hilltop (not every region has one), climb the cairn (E).
   - It names what you can now see; the compass and the region map (N) show the newly found places.
4. **A travellers' camp.**
   - Rest there overnight (the Rest button): the night passes undisturbed.
   - Then rest in open wild country at night, inside a creature's range: sometimes the night is broken.

## Travellers

5. **The roads have people.**
   - Groups walk the roads: caravans (with a wagon), merchants, patrols, pilgrims, refugees; bandits hold some roads.
   - They should not pop up in front of you: groups appear within about 600 m, on the road, moving.
6. **The same group over time.**
   - Note a caravan's name.
   - Wait an hour (T): it has moved along its road, toward the same destination.
   - Open the atlas (A): it is there too.
7. **Talking.**
   - Walk up to a group and talk (E): they share news.
   - A patrol's answer depends on your standing with its faction.
   - Refugees: talk, then E again to give a ration (you need a ration); their home town thinks better of you.

## Navigation

8. **Your position on the map.**
   - Walk somewhere in the country, then Return to World Map (M).
   - You walk to the nearest road or town first (the clock moves accordingly).
   - The atlas marks you there, not at your last town.
   - Save, load, and check again.
9. **Waypoints.**
   - On the map, select a settlement and press "Mark on compass". Walk out: the compass shows a gold mark, and the line under it names the place and the distance.
   - Mark a settlement in another region: the line says which way on to take, and the compass points to that region exit.
   - "Clear the mark" removes it.
   - Try to mark a place you have not found: you can't.

## Journeys

10. **Leaving a planned journey.**
    - Plan a journey on the map and travel an hour.
    - Press "Walk from here (on foot)": a loading screen, then you stand on the road where the journey was. The compass marks that point.
    - Walk away 200 m or more. "Back on the road (J)" disappears.
    - Walk back: it reappears; press J and the journey continues from the same point.
11. **Finishing on foot.**
    - Step off again and walk to the destination town. Enter it: the journey is done.
    - Do the same into a different town, or across a region border: the journey is given up.

## Encounters

12. **Something in the wild.**
    - Walk off the roads in wild country (deep woods, marsh, rocks), ideally at night.
    - Now and then something finds you. A panel describes it, with Fight, Slip away (Stealth), Back off, and for bandits Pay their toll.
13. **Fight it.** The fight happens where you stand, with the normal combat. Afterwards you are in the country again.
14. **Bandits on a road.** Walk a road that bandits hold (the atlas shows bands): they demand a toll. Pay, slip past, fight or back off.
15. **Not too often.** After any encounter there are three quiet hours. Roads, towns, watch posts and patrols make it rarer. Walking near towns should be mostly quiet.
16. **Saving does not reroll.** Save with an encounter waiting, then load: it is the same encounter.

## Weather on the ground

17. **Rain and mud.** After rainy days, the HUD under the compass says "Wet ground" or "Mud".
    - The ground looks darker.
    - Walking off the road is slower; the road barely is.
    - The clock moves faster off the road.
18. **Snow.** In winter or the highlands, with snow cover: the ground is white on flat land, and walking is slower everywhere, roads least.
19. **Fords in flood.**
    - After heavy rain on wet ground, the HUD says "fords in flood: cross by a bridge".
    - Try to wade a ford (a shallow crossing of the river off the road): you are stopped, with the reason.
    - A bridge still crosses. When it dries, the ford is open again.
20. **Fog.** In fog you find things only when closer, and a cairn shows less.

## Regions and stability

21. **Repeated transitions.**
    - Cross into a neighbouring region and back several times, and enter and leave towns.
    - Landmarks and groups are never doubled, and the found ones stay found.
    - The game stays smooth.

## Things to judge (owner calls)

- **Pace:** encounter frequency, landmark density (at most 6 waystones a region), how much mud and snow slow you.
- **Turning to the map** puts you on the nearest road or at a town. Is that acceptable, or do you want arbitrary map positions (a later phase)?
- **The 40 m walking scale** (D1) is still yours to confirm.
