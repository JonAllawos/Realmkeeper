# Phase 26: owner playtest checklist

| | |
|---|---|
| **Automated tests (done by Claude)** | The .NET suite (951/951, including `Phase26CodexAndTradeTests`) and targeted Unity PlayMode runs (`Phase26PlayModeTests`; the Phase 5, 8, 11 and 15 suites). **The full Unity EditMode and PlayMode suites were not run this round** (owner's instruction) |
| **Automated visual checks (done by Claude)** | Captures from the running game in `Documentation/Screenshots/Phase26/`, at 1920×1080, 1366×768, 1280×720, 1024×768, 1280×800 (16:10), 2560×1440, 3440×1440 and a 700×1000 narrow window, reviewed for clipping, overflow and readability |
| **Manual owner playtesting (yours, not done)** | Everything below. Phases 24 and 25 have not been hand-played either; their checklists remain open |

Each check says what to do and what you should see.

## Merchant

1. Open a shop. Four pages: **Buy, Sell, Particular goods, Mending**, each with a count.
2. **Buy.** Click a line.
   - The inspector shows the price made factor by factor, the item's numbers, and for armour or weapons "If worn … instead of …" with better or worse.
   - Press **+** twice: the total and "Purse after" change.
   - **Buy 3 for N coin.** You have three more, your purse fell by N, and the shop's stock fell by three.
3. **Too expensive.** Choose something you cannot afford: the row says "You are N coin short", and the button is greyed with the same reason.
4. **Sell.** Things the shop will not buy say why on the row.
   - Sell two of a stack: you get the coin for two, and two leave your pack.
5. **Particular goods and mending** work as before.
6. **Close and reopen:** nothing has changed that you did not do.

## Crafting

7. **Hub ▸ Crafting & work.** Category chips with counts, **Can make now**, search.
8. **A recipe:** each material shows have/need with a tick or a cross; tool, station, check and chance, and time are listed. **Start** works only when everything is there; otherwise it says why.
9. **Start a recipe you can make:** the materials are held; waiting finishes it; the result arrives.

## Party

10. **With companions:** Hub ▸ Party lists **you** first, then each companion with a health bar and status. Selecting one shows their sheet, loyalty in words and what they carry. The actions (Wait, Follow, Lead, Talk, Part ways, Give, Take) work.

## Character

11. **Creating a character** and **levelling up** behave as in Phase 22. Nothing is chosen for you, and the level is applied once.

## Dialogue

12. **Talking to someone:** responses carry an icon for their kind (a skill check, a quest, a decision, a trade, leaving) beside the same words as before. Number keys still choose.

## Maps

13. **The region map** (N, in the country) has a key under it and its scale: "One square = 40 m · the region is … km".
14. **The world map and atlas** behave as before. Your position, waypoints and found places are correct.

## Combat

15. **During a fight:** the selected combatant shows conditions as badges. When it ends, a word says how: Victory, Defeat, Withdrawn, Nobody left standing, or Called off.

## Codex

16. **Hub ▸ Codex.**
    - Only people you have met, settlements you have visited or heard of, places you have found or heard of, creatures you have fought, and your discoveries and known events appear.
    - Strangers and unknown places do not.
17. **Meet someone new and reopen:** they are there. Search for part of a name: it is found.
18. **Save, load:** the codex is the same.

## Settings, accessibility, resolution

19. **Text size Largest and High contrast:** trade, crafting, the codex and the party stay readable; nothing overlaps.
20. **Reduce motion**, and **Interface sounds 0:** no sliding, no clicks.
21. **Windowed sizes:** try 1280×800 and a tall narrow window (around 700×1000). The trade window and the hub stack their inspector under the list when narrow.

## Owner feedback still outstanding

- Hand-play notes from Phases 24, 25 and 26.
- The footsteps' timing (Phase 24) and the interface sounds (Phase 25), by ear.
