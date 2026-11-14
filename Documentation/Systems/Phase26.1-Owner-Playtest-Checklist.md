# Phase 26.1: owner playtest checklist

| | |
|---|---|
| **Automated tests (done by Claude)** | Full .NET suite; full Unity EditMode suite; full Unity PlayMode suite (one checkpoint, after the map was built); the focused Phase 26.1 tests. Results are in the completion report §4–7 |
| **Automated checks in the running game (done by Claude)** | Road vertices against the terrain collider; the player's feet on a sloping road; a player-sized capsule through every gate; real key events for typing and the number keys; the map's marks, turning, hiding and cost |
| **Screenshots (done by Claude)** | From the running game, in `Documentation/Screenshots/Phase26.1/` |
| **Manual playtesting (yours, not done)** | Everything below. **Claude has not played the game by hand** |

**Start a new world for checks 1–4.** Worlds made before the fence fix keep their old fences, because settlement plans are saved once.

## The four post-play fixes

1. **Roads, flat and sloping.** Walk along roads on flat ground and across slopes. The paving lies on the ground at your feet and never passes through your legs.
2. **Settlement entrances.** Walk in and out through the gate or on the main road. Nothing blocks the way.
3. **Fenced plots.** Find gardens, yards and pastures. Each has a visible gate (an opening between two taller posts) that you can walk in and out of. No fence crosses a road.
4. **Several settlements.** Make two or three new worlds and look round the first town of each. No plot is closed off, and nothing fences you in at the start.
5. **Typing.** On the title screen's new-world page, type a name with W, A, S, D, Q and E in it. The letters go into the name, and the map behind does not move, turn or zoom. Do the same in the codex search, trade search and crafting search.
6. **Numbers in conversation.** Talk to someone: 1 is "Talk…", each number does what its button says, the keypad works too, and 0 picks the tenth. In a full conversation, each number chooses that response.

## The local map

7. **Standing still.** The map is at the bottom right. You are the arrow in the middle, pointing up.
8. **Turning.** Turn with the mouse without moving: the map turns beneath the arrow, smoothly and without lag, and the N moves round the rim.
9. **Walking.** Walk in several directions: the marks slide past and stay where the things are.
10. **A merchant.** Find a shop: its amber square sits at its door, with its name. A shopkeeper in the street is an amber dot.
11. **People.** Walk past people: their dots move as they do. Strangers are labelled by their trade; people you have spoken with, by name.
12. **Leaving.** Walk away from a place or a person: their marks leave the map.
13. **A landmark.** In the country, find a landmark (read a waystone): within about two seconds its violet diamond appears.
14. **Nothing secret.** Unfound landmarks, unknown places, people indoors, and outlaws beyond about 70 m are not shown.
15. **Settlements.** Enter and leave a settlement: the map changes between the town plan (45 m) and the country (160 m). Click the map to switch to the wider range.
16. **Regions.** Cross into another region: the map shows the new region.
17. **Menus.** Open the hub, the pause menu, trade, a conversation window and a fight. The map is hidden while they are open and returns after.
18. **Sizes and text.** Try 1366×768, 1280×720 and a narrow window, with Text size Largest and High contrast. The map stays readable and covers nothing. With Reduce motion on, it turns without easing.
19. **Save and reload.** The map comes back the same.
20. **A busy street.** Stand where many people are. The game stays smooth; at most five labels show; the rest are marks.

## Feedback still outstanding

- Hand-play notes from Phases 24, 25 and 26 (their checklists remain open).
- Your view of the map's look, size and corner. Bottom right was chosen after inspecting the HUD; bottom left is free too if you prefer it.
