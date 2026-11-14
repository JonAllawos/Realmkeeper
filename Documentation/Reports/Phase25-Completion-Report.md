# Phase 25 completion report

**Phase:** Complete UI/UX Overhaul, Interface Architecture, Visual Identity & Third-Party Asset Integration.

**Date:** 2026-11-14.

**Baseline:** `9360330` (Phase 24, pushed; branch even with origin). Phase 25 is committed locally only. **Nothing was pushed.**

**Status words:**
- **Completed:** built, tested and captured.
- **Partial:** improved through the shared system, but the screen's own layout is unchanged.
- **Deferred:** not done in this phase.
- **Unverified:** built, but not checked by a person (hand-play, ear).

## 1. Executive summary

Phase 25 completed the owner-approved Phase 19 identity ("Cartographer's Ledger") as one interface system across the game, and fixed the defects the audit found in the running build. The direction itself was not changed: that is an owner call.

**Shared system.**
- Design tokens version 2: every one of the 147 font sizes is on a token, with a 12 px minimum.
- Accessibility: text size, high contrast, reduced motion, interface-sound volume.
- Width classes, so layouts adapt from 1024×768 to 3440×1440.
- A component library: toasts, pause menu, inspector, master-detail lists, vitals, action bar, interface sounds.

**Screens.**
- A pause menu.
- Notifications derived from what the player knows.
- A new exploration HUD layout with vitals; the HUD no longer shows through windows.
- A master-detail inventory and equipment with the shared inspector and the rules engine's comparison.
- A bestiary of creatures met and defeated (save v27).
- A grouped journal, spell type icons, health lines in combat.
- An exit confirmation on the main menu.

**Assets.**
- Kenney Interface Sounds (CC0; 10 files) and 11 more game-icons.net icons (CC BY 3.0), with licences verified from the files.
- Two paid kits examined and **not bought** (owner decision; not recommended).

**Tests.** .NET 947/947; Unity EditMode 947/947; Unity PlayMode 133 passed, 1 failed (a stale test, fixed and re-run green), 34 capture-only skips (§20–23).

**Not hand-played.** Screens whose own layout is unchanged are listed as partial.

## 2. Preflight findings

`Systems/Phase25-Preflight-Audit.md` has the full audit.

**Git.**
- Clean at `9360330`.
- Two untracked `InitTestScene*` files (empty test-runner scenes) were inspected and left alone, neither committed nor deleted.
- A third copy has been tracked since an earlier phase and was left as it is.

**Defects measured in the running build.**
- At 1366×768 the top-right actions were cut off the screen and the action row overflowed its panel.
- The HUD showed through every hub window.
- The exploration HUD had no health display.
- 45 text styles were below the 12 px minimum (10–11 px).
- The settings still said "the game has no sound yet".
- There was no pause menu, no notification system and no bestiary screen.
- The journal had 13 tabs in one row.
- Inventory rows carried 4–5 text buttons each, with no icons.

**Architecture.** UI Toolkit only; windows render query snapshots and dispatch real commands; no UI-side game state. This was kept.

## 3. UI systems audited

All of these were audited from the code and from fresh captures:
- main menu, new and load, campaigns, credits, settings;
- strategic map, atlas, politics, people, places;
- exploration HUD;
- character creator, character sheet, advancement;
- the hub's 13 sections: character, party, spells, appearance, inventory, equipment, weapons, clothing, crafting, storage, journal, currency, info;
- trade, dialogue, combat, region map, loading screen.

**Reused rather than replaced:** creator, sheet, advancement, trade, dialogue, combat and the atlas. Their existing structure was sound; they gained the shared tokens, focus, sounds and accessibility.

## 4. Final visual design direction

**Cartographer's Ledger, version 2** (`Systems/UI-UX-Design-System.md`):
- dark umber ink for the working interface, parchment for maps, one bronze accent;
- EB Garamond for headings, Alegreya Sans for text;
- meaning always in words with an icon, never colour alone.

**Version 2 adds:**
- size tokens (12 px minimum);
- quality, focus and toast tokens;
- accessibility variants;
- width classes;
- component styles for every new component.

## 5. UI architecture and component library

`Systems/UI-Architecture.md` has the details.

**New in `Realmkeeper.UI`:**

| Component | Job |
|---|---|
| `RkRoot` | Accessibility and width classes on each document |
| `RkToasts` | Notifications |
| `RkMenuOverlay` | Pause menu |
| `RkInspector` | The shared inspector |
| `RkUiSound` | Interface sounds |
| `RkHooks` | The main-menu hook between layers |

**Extended:**
- `RkSettings`: text size, contrast, motion, interface-sound volume;
- `RkSettingsWindow`: an Accessibility section;
- `RkIcons`: 15 meanings added.

**Application layer (engine-free, tested in .NET):**
- `PlayerNotices`, `NoticeSnapshot` and `WorldSession.GetNoticeSnapshot` / `FillNoticeEvents`;
- `WorldSession.GetBestiary` and `WorldSession.ReplaceCount`.

**Domain:** `KnownCreatureData` and `CreatureKnowledge` (ADR-0116).

## 6. Main menu and HUD changes

| Item | Status | What changed |
|---|---|---|
| Main menu | Completed (evolved) | Exit asks first; in game it says unsaved progress would be lost. Narrow screens get a narrower menu and a smaller wordmark. Accessibility and sounds |
| New, load, campaigns, credits | Partial | Shared tokens and accessibility; layout unchanged. Credits list the new sounds and icons |
| Pause menu | Completed (new) | Esc with nothing open. Resume, Save, Settings, Return to the world map, Main menu, Exit. Stops the clock and movement and restores both; the exits are confirmed; the recent notices are shown beside it |
| Exploration HUD | Completed | Location and vitals in a left column. Actions in a bottom bar that wraps. Standing actions wrap on compact screens. The HUD steps aside for windows. The controls line says "Esc menu" |
| Notifications | Completed (new) | Quests (new lead, begun, updated, completed, failed, abandoned); discoveries; places; rumours; level-up readiness and levels; items received; equipment; reputation; overland encounters met and settled; bestiary entries; notable world events where the player has been; journey arrival or stop. At most 4 shown; repeats suppressed; ≤ 8 per change. Not raised for a load |
| Contextual prompts | Partial | The existing prompt and response panels, on tokens; layout unchanged |
| Combat HUD | Partial | Turn-order cards gain painted health lines; the rest of the combat window is unchanged (it was overhauled in the Combat Overhaul) |

## 7. Character creation and character sheet changes

| Item | Status |
|---|---|
| **Character creation** | **Partial.** The Phase 20 and 22 creator is kept, with shared tokens, larger minimum text and accessibility. Its compact mode was captured at 1024×768 |
| **Character sheet** | **Partial.** The Phase 20 sheet is kept. The 3D figure now shows only on Character, Appearance, Equipment and Clothing, so lists get the room elsewhere |
| **Advancement** | **Partial, behaviour unchanged** (Phase 22: eligibility, notice, the player's choices, saved, applied once, nothing chosen for the player). The new "You can advance" notice says so |

## 8. Inventory, equipment, shops and crafting changes

| Item | Status | What changed |
|---|---|---|
| Inventory | Completed | Master-detail. Rows: category icon, name, category and badges, quantity, weight, value, Inspect and the main use (Equip or Eat/Drink/…). The search, sort and group filters are kept. Stacks under the list at narrow widths |
| Item inspection | Completed | `RkInspector`: icon, name, rarity and badges, rules lines, description, lore, source, and every applicable action (Equip, Right hand, Off hand, Use, Attune, End attunement, Split, Store, Drop 1, Unequip), each a real command |
| Equipment comparison | Completed | The rules engine's existing comparison (`CompareSource`), shown in the inspector as "before → after ▲ better / ▼ worse / (no change)" |
| Equipment | Completed | Armour class and attunement tiles; slots with icons ("Nothing worn" when empty); the inspector |
| Loot and containers | Partial | Rows gain icons; layout and actions unchanged |
| Shops and trade | Partial | Shared tokens and accessibility; the Phase 21/22 trade window is otherwise unchanged |
| Crafting | Partial | Recipe rows gain icons; layout unchanged |

**Transactions.** Every item action is still one domain command; the UI cannot duplicate or destroy items. Tested: Drop 1 removes exactly one (`Phase25PlayModeTests`).

## 9. Spellbook and combat interface changes

| Item | Status |
|---|---|
| **Spellbook** | **Partial.** Spell rows gain type icons (damage, healing, control, support, utility). The Phase 9 level, type, scope and search filters are kept |
| **Abilities** | **Partial.** Features stay on the sheet's Features panel (captured). There is no hotbar system, so none is shown |
| **Combat information** | **Partial.** Health lines on the turn order. The log, chips and options are unchanged |

## 10. World map, journal and discovery changes

| Item | Status |
|---|---|
| **Strategic map, atlas** | **Partial.** Shared tokens, accessibility, interface sounds, and toasts on the map |
| **Region and overland map** | **Partial.** Unchanged in layout. The country gains vitals and the action bar. Phase 24 behaviour and the 40 m scale are unchanged |
| **Quest journal** | **Completed.** Four headings (Quests, Knowledge, Deeds, Standing) over the existing pages; the page names and the quest detail are unchanged |
| **Codex and discoveries** | **Partial.** Discoveries, places and rumours are under Knowledge; creatures are in the bestiary. There is no separate codex of people or factions, and no ancient-world entries were invented |

## 11. Dialogue, companion and faction changes

**Partial for all three:** shared tokens, focus, sounds and accessibility; their layouts are unchanged.
- **Dialogue:** the "About them" card from Phase 20.
- **Party:** Phase 15 companions.
- **Factions and settlements:** the strategic inspector and the politics overlay.

**Nothing hidden was exposed.** No relationship meters were added.

## 12. Settings and accessibility changes

**Completed.**
- **New Accessibility section:**
  - Text size (Normal, Large 115 %, Largest 130 %), reaching every screen through the tokens;
  - High contrast;
  - Reduce motion.
- **Interface sounds** volume, under the sound volume.
- **Corrected "Not available yet" list:**
  - removed: "no sound yet" and "use interface scale for text";
  - added: controllers, music volume and languages, stated as not supported.
- **Controls list:** Esc now opens the menu.
- **Persistence:** every setting is kept in PlayerPrefs and applied at once.

**Tested** in PlayMode: the classes reach the documents and are removed again.

## 13. Resolution and aspect-ratio test results

**Captured in the running game** (`Screenshots/Phase25/`). The width classes come from the capture log:

| Size | Ratio | Interface scale (Auto) | Width class | Captured screens |
|---|---|---|---|---|
| 1920×1080 | 16:9 | 1.00 | regular | all 30 screens |
| 1366×768 | 16:9 | 1.25 | regular | menu, map, HUD, sheet, inventory, spells, journal, advancement, merchant, dialogue, combat, bestiary, country |
| 1280×720 | 16:9 | 1.25 | regular | menu, settings, atlas, HUD, inspection |
| 1024×768 | 4:3 | 1.25 | compact | menu, new game, accessibility, creation, HUD, pause, sheet, inventory, comparison, bestiary, region map |
| 2560×1440 | 16:9 | 1.00 | regular | menu, HUD, combat |
| 3440×1440 | 21:9 | 1.00 | regular | menu, map, HUD |

**Reviewed visually.**
- **Fixed:**
  - the clipped actions at 1366×768 and 1024×768;
  - the HUD showing through windows;
  - toasts overlapping wrapped actions or hidden behind windows;
  - the wordmark overflow at 1024×768.
- **Seen holding, without clipping:** inventory and inspector at 1024×768 (side by side, compact widths); the pause menu at 1024×768.
- **Not separately captured:** the narrow class (below 1150 logical px, which Auto reaches only below about 900 px wide).
  - It is exercised by a PlayMode check on a 1000×700 element.
  - Its stacked master-detail layout has no screenshot.
- **Never tested:** 16:10 at 1280×800 was not captured, and no physical monitor was used. Captures render to a texture at each size.

## 14. Third-party asset research findings

In `Research/Phase25-UI-Research.md`, building on Phase 19A's 46-candidate register:
- 6 candidates examined;
- 2 adopted (free; licences verified in the files);
- 1 not adopted for an unverifiable licence file (Kenney Cursor Pack);
- 1 rejected for style;
- 2 paid kits presented for the owner, not bought.

## 15. Adopted CC0 assets

**Kenney Interface Sounds 1.0:** 10 OGG files, 130 KB.
- **Licence:** CC0 1.0, from the bundled `License.txt`, which is kept in the project.
- **Location:** `Art/ThirdParty/Kenney/InterfaceSounds/`.
- **Credited** in game and in `CREDITS.md`.

`Assets/Phase25-Asset-Register.md` has the full record.

## 16. Adopted CC BY assets and attribution

**game-icons.net: 11 more icons** (Lorc 5, Delapouite 6).
- **Licence:** CC BY 3.0. The credit is updated in the game's Credits (now 153 icons), in `CREDITS.md` and in `Art/ThirdParty/GameIcons/License.txt`.
- **Sources:** the SVGs are kept beside the PNGs.

## 17. Adopted paid assets, costs and approvals

**None.** Nothing was bought, and no payment was started.

**For the owner:** GUI Pro – Fantasy RPG (US$39.99) and Classic Fantasy RPG – UI Kit (US$19.99). Both are uGUI kits, and neither is recommended.

## 18. Rejected candidates and reasons

| Candidate | Reason |
|---|---|
| Kenney UI Pack RPG Expansion | Style: bright cartoon, against the approved direction |
| Kenney Cursor Pack | Its licence file could not be inspected; the system cursor is adequate |
| GUI Pro – Fantasy RPG | uGUI-only; style; Asset Store EULA |
| Classic Fantasy RPG UI Kit | uGUI-only; style unassessed |

## 19. Performance measurements

Measured in the editor in batch mode, not a player build.

| Measure | Value |
|---|---|
| Notice check (snapshot and comparison) in the played scene | 0.03 ms (capture log); every 0.5 s |
| Notice snapshot and comparison in a full .NET world | asserted under 25 ms per check (the test passes; the exact figure goes to the test log and was not read) |
| Vitals refresh | once a second (one character query) |
| HUD window check | a class toggle only when the value changes |
| Toasts | at most 4 elements; each removes itself; no accumulation |
| Lists | not virtualised (tens of rows) |
| New assets | 130 KB of sound; 11 icons at 512 px (≈10 KB each) |

**Not measured:** screen-opening times and layout rebuild costs, per screen. No regression was observed in the PlayMode suites' timings, but no dedicated UI timing test was added.

## 20. .NET test results

`Tools/DotNet`, Release: **947 passed, 0 failed, 0 skipped (947 total)**, in 5 min 11 s. That is Phase 24's 938, plus 9 new:
- 8 in `Phase25InterfaceTests` (bestiary, v27 save and seeding, validator, notices, load, bounds, cost);
- 1 in `Phase25JourneyThreatTests`.

## 21. Unity EditMode results

Full suite (Unity 6000.0.47f1, batch mode): **947 passed, 0 failed, 0 skipped (947 total)**.

## 22. Unity PlayMode results

**The full suite** (168 test cases):
- **133 passed, 1 failed, 34 skipped**;
- the skips are the capture-only tests, ignored outside capture runs, and include the new `Phase25CaptureTests`.

**The one failure** was a stale test (§23). It was fixed, and `Phase19PlayModeTests` was re-run (with another test first, as in the suite): **7/7 passed**.

**Earlier targeted runs, all passed:**
- `Phase25PlayModeTests` 1/1;
- `Phase11PlayModeTests` 6/6;
- `Phase5PlayModeTests` 4/4;
- `Phase25CaptureTests` with RK_CAPTURE_DIR, 67 captures.

## 23. Regression analysis

| Test | Classification | Cause | Action |
|---|---|---|---|
| `Phase19PlayModeTests.TheTopBar_OpensSettings_AndTheScaleChangesTheWholeInterface` (full run) | **Stale test**: the behaviour changed on purpose | It required the settings to list "Audio: the game has no sound yet" as not available. That line has been false since Phase 20A added sound, and Phase 25 removed it | The test now requires the true missing items (game controllers) and that sound is *not* listed as missing. Re-run: passed |
| `Phase19PlayModeTests.TheTitle_FoundsANamedWorld_…` (failed only in the isolated re-run of `Phase19PlayModeTests`) | **Environment: test order.** Not a regression; passed in the full suite | When it is the very first test of a run, its scene counts as the launch scene, so the title opens at launch, and the test's "not the launch scene" premise does not hold. This is a pre-existing condition of the test, not something Phase 25 changed | Re-run with another test first: passed. The test is not changed |

**Tests changed on purpose, because behaviour changed:**
- **`Phase11PlayModeTests`:** three steps (Right hand, Off hand, Store) now act through the inventory inspector (`ItemDriver.InspectorAction`), because those actions moved from the row to the inspector.
- **`Phase17StoryTests`, `Phase20AAppearanceTests`:** the schema pin is now 27.
- **`Phase24OverlandLifeTests`:** a v25 save now upgrades to the current schema (was the literal 26).
- **`Phase19PlayModeTests`:** as above.

No expectation was weakened to make a test pass.

**A bug found by the new tests and fixed:** taking off an item announced "Received" as well. Notices now count worn and carried items together.

**Phase 24 caveat.** It stays visible: the isolated Phase 24 PlayMode re-run met no road threat. Phase 25 adds `Phase25JourneyThreatTests`, a deterministic .NET test that passes:
- a strong band bars the road;
- stepping off is refused with the reason;
- after paying the toll it is allowed, the journey halts on the road, and rejoining resumes it.

The Phase 24 PlayMode test itself is unchanged.
## 24. Screenshot locations

**`Documentation/Screenshots/Phase25/`:** 67 captures and `p25_capture_log.txt`, which logs each image's size, scale and width classes.
- **The brief's 24 screens:** `p25_01` to `p25_24`.
- **Extras:**
  - `p25_10b` (equipment);
  - `p25_16b` (atlas);
  - `p25_17b` (region map);
  - `p25_24b` (text at 130 % with high contrast);
  - `p25_25` (pause menu).
- **`before/`:** five pre-Phase-25 captures of the same build, for comparison.

**Generated by** `Phase25CaptureTests`, with real game data:
- a cleric made through the creator's rules;
- a shop's real stock;
- a practice fight played to its end, from which the bestiary shows "Bandit (met 1, defeated 2)".

## 25. Documentation updates

**New:**
- `Systems/Phase25-Preflight-Audit.md`
- `Systems/UI-UX-Design-System.md`
- `Systems/UI-Architecture.md`
- `Systems/Phase25-Owner-Playtest-Checklist.md`
- `Assets/Phase25-Asset-Register.md`
- `Research/Phase25-UI-Research.md`
- `Decisions/ADR-0116-bestiary-knowledge-and-derived-notices.md`
- this report

**Updated:**
- `Decisions/README.md`
- `Systems/Persistence.md` (v27)
- `Roadmap.md`
- `Product/GameDesignBible.md`
- `Product/FutureRoadmap.md`
- `CREDITS.md`
- the icon licence file
- the icon manifest

**Not touched:** campaign documents. No campaign content was added.

## 26. Git commits and working-tree status

- **Branch:** `feature/phase-0-1-foundation`, based on `9360330`.
- **Commits:** the Phase 25 work is committed locally in one commit, with its hash in the closing message.
- **Left out on purpose:**
  - `ProjectSettings/GraphicsSettings.asset` and the untracked `Assets/InitTestScene*` files, which stay untracked and were not deleted;
  - `M_LampGlow.mat`, which batch runs re-save; it was restored and not committed.
- **Push:** **nothing was pushed to GitHub.**

## 27. Known limitations

- **Not hand-played.** The interface sounds have not been heard; the Phase 24 checklist is also still open.
- **Partial screens:** dialogue, party, trade, crafting, the creator and the strategic panels keep their own layouts under the shared system (§6–11).
- **Not supported:**
  - controllers (no gamepad code; stated in the settings);
  - key rebinding;
  - localisation (all strings are in code).
- **Narrow layouts:** the narrow class is unit-checked, but no screenshot shows its stacked layout. 16:10 was not captured. Captures render to textures, not physical displays.
- **Notices:**
  - they can be up to half a second late;
  - an item taken off and put back on within half a second is not announced;
  - world-event notices cover only the 20 newest events between checks.
- **The bestiary:**
  - it records fights from Phase 25 on;
  - older saves are seeded only from the fights they still hold (their last 20);
  - creatures seen without a fight are not recorded.
- **Timing:** no dedicated UI timing test exists.
- **D1** (the 40 m scale) is still unconfirmed.

## 28. Manual owner playtest checklist

`Systems/Phase25-Owner-Playtest-Checklist.md` has 30 checks plus the owner calls (direction, paid kits, controllers, D1). It separates the automated verification above from the manual checks. Nobody has hand-played Phase 25.

## 29. Recommended Phase 26 priorities

Suggestions only; the next brief decides.

1. Hand-play Phases 24 and 25 together, and fix what the owner finds first.
2. Bring the partial screens onto the master-detail and inspector pattern:
   - trade (stock list and inspector);
   - crafting (recipe inspector with output preview);
   - party (companion inspector);
   - the strategic settlement and faction inspectors.
3. A codex of people and factions the player knows, from the existing knowledge records (no new lore).
4. Controller support and rebinding, if wanted (an owner decision).
5. String extraction for localisation.
6. A UI timing test (screen open and re-render costs) and a player-build measurement.
7. Overworld geography generation, which the old roadmap had planned for Phase 25.
