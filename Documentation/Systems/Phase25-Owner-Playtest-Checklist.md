# Phase 25: owner playtest checklist

**Automated verification (done by Claude).**
- The .NET tests.
- The Unity EditMode and PlayMode suites, including `Phase25PlayModeTests` and `Phase25InterfaceTests`.
- The capture run in `Documentation/Screenshots/Phase25/`, at 1920×1080, 1366×768, 1280×720, 1024×768, 2560×1440 and 3440×1440.

**Manual verification (yours).** Everything below. Nobody has hand-played Phase 25, and the Phase 24 checklist is also still open. Each check says what to do and what you should see; tick what holds and note what does not.

**Setup.** Start from the main menu with a new world, and create a character. Try at least two window sizes: your usual one, and 1280×720 or smaller (Settings ▸ Display ▸ Windowed).

## Main menu and global navigation

1. **The main menu.**
   - Every entry works; Continue is greyed with a reason when there is no save.
   - At 1024×768 the wordmark and menu fit.
2. **Exit to desktop** asks first. Cancel keeps you in the menu.
3. **New world**, then **Load world**: your saves are listed with their date; loading works. **Delete** asks first.
4. **The pause menu.**
   - In a town, press **Esc** with nothing open. The pause menu appears, the clock stops (watch the HUD clock), and you cannot walk.
   - **Esc** again resumes; the clock goes on only if it was running.
5. **Pause ▸ Save**: a "Saved" notice appears. **Pause ▸ Settings** opens over the menu; Esc closes only the settings.
6. **Pause ▸ Main menu** asks first. It returns you to the map, with the main menu over it. **Resume** there returns to the map.
7. **During a fight**, Esc cancels combat selections and does not open the pause menu; Return to map is not offered mid-fight.

## HUD and notifications

8. **The HUD.**
   - Top left: the town, region, date and clock, with your **vitals** under them (name, health bar, "HP · AC · hunger · level", any conditions).
   - The actions sit in one bar at the bottom.
   - Nothing runs off the screen at 1280×720 or 1024×768.
9. **Behind a window.** Open the hub (H): the HUD panels disappear behind it, and notices move to the bottom right.
10. **Notices.**
    - Equip or take off something: one notice each. Taking something off does not say "Received".
    - Wait an hour (T) or play on: quests, discoveries, rumours, reputation and level-up readiness appear as short notices, never repeated, and never more than four at once.
11. **Recent notices.** The pause menu lists them.

## Character and progression

12. **Creating a character** (C) works at 1024×768 (compact layout).
13. **The character sheet** matches what you chose: abilities, saves, hit points, armour and speed.
14. **Levelling up.**
    - Earn enough experience: one notice says you can advance, and that nothing is chosen for you.
    - The level-up (L) works as in Phase 22.

## Inventory, equipment, trade, crafting

15. **Inventory (hub ▸ Inventory).**
    - Icons, names and categories, quantity, weight, value.
    - **Inspect** shows the item on the right; every other action is there.
    - **Drop 1** removes exactly one.
16. **Comparison.** Inspect a weapon or armour you are not wearing: "If worn … instead of …" lists the changes with "better" or "worse" written out.
17. **Equipment.** Slots have icons; empty slots say "Nothing worn"; Unequip and Attune work.
18. **Storage and loot.** After a fight, its loot opens in Storage; Take and Take all work; nothing is doubled.
19. **A shop and a work station** behave as before (buy, sell, craft).

## Spells, journal, bestiary

20. **Spells.** With a caster: spells have type icons; the level, type and search filters work.
21. **The journal.**
    - Four headings (Quests, Knowledge, Deeds, Standing) with their pages under them.
    - Quests are listed with their detail.
    - Nothing you have not heard of appears.
22. **The bestiary (hub ▸ Bestiary).**
    - **Before a fight:** empty, and it says why.
    - **After a fight:** the creatures you faced are listed.
    - **Before you defeat one:** it says its strength is unknown.
    - **After you defeat one:** armour, hit points and attacks.
    - A bestiary notice appears for each new entry.

## Dialogue, party, map

23. **Talking to a resident**, the party (K), the world map, the atlas (A), the region map (N) and the compass work as in Phases 18–24.

## Settings and accessibility

24. **Text size.** Settings ▸ Accessibility ▸ Text size Largest: text is larger on every screen (menu, HUD, hub, journal). Panels keep their size and wrap.
25. **High contrast:** secondary text is brighter, borders are stronger, panels are solid.
26. **Reduce motion:** notices appear without sliding.
27. **Interface sounds:** clicks and notices are audible and quiet. The Interface sounds slider at 0 silences them, and the Sound volume at 0 silences everything. *(By ear; batch tests cannot hear.)*
28. **Keyboard.** Tab moves through buttons with a visible bronze ring; Enter activates; in the pause menu, Up and Down move and Enter chooses.

## Saves and continuation

29. **Save, quit and load:** everything is as you left it, including the bestiary.
30. **A save from before Phase 25** (v26) loads. Its bestiary lists the creatures from the fights the save still remembers (up to its last 20 fights).

## Owner calls

- **The direction.** Is the evolved "Cartographer's Ledger" the identity you want, or do you want a stronger change in a later phase?
- **Paid UI kits:** two were examined and **not bought**: GUI Pro – Fantasy RPG (US$39.99) and Classic Fantasy RPG – UI Kit (US$19.99). Neither is recommended (uGUI-only, different style). Approve only if you want them evaluated further.
- **Controllers:** not supported. Should controller support be planned?
- **D1** (the 40 m walking scale) is still yours to confirm.
