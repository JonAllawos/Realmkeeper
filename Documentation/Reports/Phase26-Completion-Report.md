# Phase 26 completion report

**Phase:** Interface Completion, Gameplay Experience Refinement, Codex & UI Quality Assurance.

**Date:** 2026-11-14.

**Baseline:** `5e2629a` (Phase 25, local only).

**Commit:** Phase 26 is committed locally only. **Nothing was pushed.**

**Status words used throughout:**
- **Complete:** the layout and interaction were redesigned, tested and captured.
- **Improved:** targeted changes to a sound design.
- **Unchanged:** shared styling only.
- **Unverified:** not checked by a person.

## 1. Executive summary

Phase 26 finished the screens Phase 25 had only restyled, and added a codex.

| Area | Status | What changed |
|---|---|---|
| Trade | Complete | Pages with counts; reasons on rows; an inspector with the price made factor by factor; a comparison for shop items (the rules engine on a copy); quantities; total and purse after; one confirm button |
| Crafting | Complete | Categories, Can make now, search; a recipe inspector with each material have/need |
| Party | Complete | You first; companions with health meters; the member inspector |
| Codex | New | Built from existing knowledge records, with no save change |
| Dialogue | Improved | Response icons |
| Combat | Improved | Condition badges; the outcome banner |
| Region map | Improved | A key and the 40 m scale |
| Creator, advancement, strategic map panels | Unchanged | Shared styling only |

**Also done:**
- **String table.** `RkText` is started (124 strings); it does not cover the whole interface.
- **UI timings.** Measured in the editor.
- **A trade inspector defect, found in review and fixed:** the shop panel was drawn over the quantity controls.

**Tests.** .NET 951/951. The full Unity EditMode and PlayMode suites were **not run** this round (owner's instruction); only targeted PlayMode runs passed (§18–21).

**Not hand-played.**

## 2. Preflight findings

`Systems/Phase26-Preflight-Audit.md` has the full audit.

**Git state.**
- HEAD was `5e2629a`, one commit ahead of origin (Phase 25, local only).
- The two untracked `InitTestScene*` files were inspected and preserved.

**Test state.** .NET was re-run at preflight: 947/947.

**Usability problems found** (not just styling):

| Screen | Problems |
|---|---|
| Trade | <ul><li>Refusals shown as "—" with no reason.</li><li>The price explanation far from the selection.</li><li>No quantity, though the commands support it.</li><li>No comparison.</li></ul> |
| Crafting | <ul><li>One long scroll.</li><li>No way to see what can be made.</li></ul> |
| Party | Text rows |
| Region map | <ul><li>No key.</li><li>11 px labels.</li></ul> |
| Combat | Conditions only in text |
| Codex | Absent |

## 3. Interfaces audited

Trade, crafting, party, character creation, advancement, character sheet, dialogue, strategic map panels, region map, combat, inventory and equipment, journal, settings and accessibility (classified in the preflight audit).

## 4. Interfaces fully redesigned

- **Trade**
- **Crafting** (the recipe browser)
- **Party**
- **Codex** (new)

## 5. Interfaces partially redesigned

- **Improved:** dialogue, combat, region map.
- **Unchanged in Phase 26** (shared styling only, behaviour kept):
  - character creation (the Phase 20/22 steps and build panel);
  - character advancement (Phase 22);
  - the strategic map panels.

  These are honestly not redesigned. Each is usable and captured.

## 6. Merchant and trade improvements

- **Pages and rows.**
  - Four pages with counts: Buy, Sell, Particular goods, Mending.
  - Each row has an item icon, category, quantity and price.
  - The reason for a refusal, or "You are N coin short", is written on the row.
- **The inspector:**
  - the price made factor by factor (base, then each factor, then each to you);
  - the item's numbers and description;
  - "If worn (slot) instead of …" from the rules engine, now also for items the player does not own (`CompareCatalogItem`, read-only; tested to equal the owned comparison);
  - quantity (−, +, Max), bounded by stock, purse or what is carried;
  - the total and the purse after;
  - "Buy 3 for 21 coin", disabled with its reason.
- **The shop:** its notes (trade, shortfalls, supply) and the recent trades sit under the inspector, and scroll with it.
- **Transactions.** They use the existing `BuyItemCommand` and `SellItemCommand` with the chosen quantity.
  - **Tested:** two bought and two sold move exactly two and the coin; buying more than the stock or purse is refused whole.
- **Unchanged:** quick Buy 1 and Sell 1 on each row, so routine trade is one click.

## 7. Crafting improvements

- **Finding recipes.**
  - Category chips with counts.
  - **Can make now**, showing the number makeable.
  - Search over names and outputs.
- **Rows** show the output, or the one problem stopping the recipe (in the warning colour).
- **The inspector:**
  - what it makes;
  - each material as have/need, with a tick or a cross and "(n more)";
  - tool (missing or proficient), station (present or not), the check and chance, time;
  - Start, or why not.
- **No new mechanics.** The recipes are unchanged.
- **Tested:** the inspector shows every material; Can make now lists only startable recipes; Start's state matches the rules.

## 8. Party and companion improvements

- **The list:** you first (class, HP, AC, health meter), then each companion with their health meter, status and the existing actions.
- **The inspector:** the selected companion's sheet numbers, ability tiles, loyalty in words, approvals, quest, items, give and take.
- **No new relationship values; nothing hidden exposed.**
- **Tested:** the Phase 15 party tests pass unchanged.

## 9. Character creation and advancement improvements

**None in Phase 26** beyond the shared system: text size, contrast, width classes, string-table-ready components. The Phase 22 rules are untouched:
- the player chooses;
- advancement applies once;
- nothing is chosen for the player.

Captured at 1920, 1280×800 and 1024×768 (creation), and at 1920 and 1366 (advancement).

**Recommended for Phase 27** (§27).

## 10. Dialogue improvements

- **A speaker emblem** in the header.
- **Response icons.** Every response carries an icon for its kind: skill check, persuasion, intimidation, deception, bribe or gift, trade, quest, decision, leaving. The words and bracketed tags are unchanged, and the number keys still choose.
- **Outcomes, options and dialogue logic are untouched.**

## 11. Map improvements

- **The region map (N).**
  - A key: you, settlement, place, landmark, waypoint, way to another region.
  - The scale line: "One square = 40 m · the region is 5.1 × 5.1 km".
  - Mark labels raised to the 12 px minimum.
- **D1 (40 m a cell) is confirmed by the owner** and recorded in ADR-0110 and FutureRoadmap; no dimension or movement changed.
- **The strategic map panels:** unchanged.

## 12. Combat interface improvements

- **Badges:** the selected combatant's conditions and concentration as word badges.
- **An outcome banner** when the fight ends: Victory, Defeat, Withdrawn, Nobody left standing, or Called off, worked out from the player's side.
- **Phase 25's** health lines on the turn order remain.
- **No rule, action economy or AI change.**

## 13. Codex implementation

`Systems/Phase26-Codex.md` and ADR-0117 have the details.

**What it is.** Hub ▸ Codex, with eight categories (people, companions, factions, settlements, places, creatures, discoveries, events), each offered only when something in it is known.

**The screen:**
- search over names and known facts;
- sort by name (grouped) or most recent;
- the shared inspector with every fact and how it is known, connections, what knowing more would add, and the source.

## 14. Discovery and persistence behaviour

**Derived, not stored.** The codex is built from existing records, so it has no state, needs no save change and cannot hold duplicates. It updates as records change.

**Tested** (`Phase26CodexAndTradeTests`):
- strangers, unheard-of settlements and unknown places are absent;
- meeting someone adds them, with the known-person rules' facts;
- a rumoured place has no description, and a found one does;
- search, category and sort work;
- there are no duplicates;
- the codex is identical after a save and load.

**No lore invented.**

## 15. Localisation readiness

**The table.** `RkText` is the interface string table (key → English; `Get`, `Format` with numbered placeholders, `Overlay` for a translation; unknown keys show themselves).

**Migrated: 124 strings.**
- trade, crafting, codex, party headings;
- the pause menu and its confirmations;
- toasts;
- the region map key;
- combat outcomes;
- dialogue tags.

**Remaining hard-coded:**
- the settings window, title screen and strategic HUD;
- the other hub sections;
- the creator and advancement;
- the notice texts and every command message (Application).

**Not claimed:** the game is not translation-ready. Game data is out of scope by design.

## 16. Accessibility and resolution verification

**Captured from the running game** (`Screenshots/Phase26/`):

| Size | Ratio | Notes |
|---|---|---|
| 1920×1080, 1366×768, 1280×720, 2560×1440 | 16:9 | |
| 1024×768 | 4:3 | |
| **1280×800** | **16:10** | First time captured |
| 3440×1440 | 21:9 | |
| **700×1000** | narrow | First real screenshot of the narrow layout: list and inspector stacked |

**Why a 700×1000 window.** With Auto scaling, every 16:9 or 4:3 window lays out at about 1300 logical pixels or more, so the narrow class (below 1150) is reached only by tall, narrow windows. This was found by measurement and is documented.

**Reviewed.**
- Found and fixed: the trade inspector and shop panel overlapping at 1920.
- No clipping or overlap remained in the reviewed captures.
- Text at 130 % with high contrast was captured on trade (`p26_20b`).

**Settings.** Captured (`p26_20`); the accessibility options from Phase 25 are kept.

## 17. Performance measurements

**Measured in the Unity editor, in batch mode** (`Phase26PlayModeTests`, `Screenshots/Phase26/p26_timings.txt`). **No development or player build was made in this phase,** so none of these figures are player-build numbers.

| Measure | Result |
|---|---|
| Trade window open | 7.0 ms (5 lines for sale, 6 offers) |
| Hub section render (large pack: 66 inventory rows) | Inventory 24.7 ms (759 elements); Weapons 5.5 ms; Crafting 11.2 ms (26 recipes); Codex 11.7 ms; every other section 0.6–6.4 ms |
| First hub open | 281 ms: it creates the 3D preview's render texture and figure. Later opens are fast |
| Open and close cycles (two frames each) | about 35 ms each |
| Interface elements over 20 cycles | 1210 → 577: nothing accumulates |
| Managed memory | First 20 cycles (warm-up) +42.5 MB. Then per 20 cycles: inventory +52.6 MB, codex −83.3 MB, inventory +72.2 MB, codex −73.0 MB. The net over 80 cycles fell by 31.6 MB: garbage reclaimed in batches, **no steady leak**. Inventory renders allocate a few MB of short-lived garbage each (pooling the rows would cut this) |
| Codex search and render | 1.8 ms over 14 entries |

**Not measured:** a codex with hundreds of entries (the test world reaches 14–17), and dialogue presentation timing.

## 18. .NET test results

`Tools/DotNet`, Release, final run: **951 passed, 0 failed, 0 skipped (951 total)**, in 5 min 40 s. That is Phase 25's 947 plus 4 new in `Phase26CodexAndTradeTests`. The preflight run gave 947/947.

## 19. Unity EditMode results

**Not run in full.** The owner chose to skip the Unity EditMode and PlayMode checkpoints for this round. A full EditMode run was started and stopped at the owner's instruction, with no result.

**Covered in part:** the .NET suite above compiles and runs the same EditMode test sources (all 951 pass). A Unity-side difference would not show there.

## 20. Unity PlayMode results

**The full suite was not run** (owner's instruction for this round). **No total is claimed.**

**Targeted runs during the phase, all in batch mode:**
- `Phase26PlayModeTests`: 1/1 passed (final version, including the per-screen memory batches).
- Together, in one run with the first version of the Phase 26 test: `Phase5PlayModeTests`, `Phase8PlayModeTests`, `Phase11PlayModeTests` and `Phase15PartyPlayModeTests` passed (17 of 18 tests in that run). The 18th was the first version of the Phase 26 test itself, which failed on a null in its own timing line after closing the window. That was fixed, and it passes since.
- `Phase26CaptureTests`: passed, 57 captures.
- **Not re-run after the last changes:** these older suites were not run again after the final trade layout fix (the side column made scrollable) and the codex capture change.

## 21. Regression and failure analysis

| Item | Classification | Detail |
|---|---|---|
| `Phase26PlayModeTests` first version, NullReferenceException at the timing line | Test defect, fixed | The trade window clears its detail on close; the counts are now read before closing |
| Trade inspector overlapping the shop panel at 1920×1080 | Real UI defect, found in screenshot review and fixed | The inspector's wrapper shrank while its card did not; the side column now scrolls as a whole |
| Codex "people" capture empty | Data, not a defect | Everyone met in that run joined the party, so the codex lists them as companions; the capture falls back to a companion entry |
| `Phase26CodexAndTradeTests.BuyingAndSellingSeveral` skipped at first | Test assumption | Weapons do not stack, so "sell two" found no stack of two; the test now uses stackable stock and passes |

**No existing test expectation was changed in Phase 26.** The redesigned screens kept every element name and action that older tests use.

**Unverified:** the full Unity EditMode and PlayMode suites, by the owner's choice. Run them before relying on Phase 26.

## 22. Screenshot locations

**`Documentation/Screenshots/Phase26/`:** 57 captures, `p26_capture_log.txt` (size, scale and width classes of each) and `p26_timings.txt`.

**The brief's 20 screens are covered:**

| # | Capture |
|---|---|
| 1, 2, 3 | Trade, merchant and player inventory, comparison |
| 4, 5 | Crafting browser, crafting detail |
| 6, 7 | Party, companion inspection |
| 8 | Creation |
| 9 | Advancement |
| 10 | Dialogue |
| 11 | Strategic map panels |
| 12 | Region map |
| 13 | Combat |
| 14–19 | The codex |
| 20 | Accessibility |

**Two substitutions in the codex captures:**
- **People:** the capture shows a **companion's** entry. Both people met in that run had joined the party, so the People category was empty in that world; the codex correctly files them under Companions.
- **Locations:** a **settlement**.

## 23. Documentation updates

**New:**
- `Systems/Phase26-Preflight-Audit.md`
- `Systems/Phase26-UI-Completion.md`
- `Systems/Phase26-Codex.md`
- `Systems/Phase26-Owner-Playtest-Checklist.md`
- `Decisions/ADR-0117-codex-derived-from-existing-knowledge.md`
- this report

**Updated:**
- `Decisions/README.md`
- `ADR-0110` (D1 confirmed)
- `Systems/UI-Architecture.md`
- `Systems/UI-UX-Design-System.md`
- `Roadmap.md`
- `Product/GameDesignBible.md`
- `Product/FutureRoadmap.md` (D1 confirmed; the Phase 26 note)

**Persistence:** unchanged. No save change (schema stays v27).

## 24. Git commits and working-tree status

- **Branch:** `feature/phase-0-1-foundation`, based on `5e2629a` (Phase 25, local only).
- **Commits:** the Phase 26 work is committed locally in one commit, with its hash in the closing message.
- **Ahead of origin:** two commits (Phase 25 and Phase 26).
- **Left out on purpose:**
  - the untracked `Assets/InitTestScene*` files, which stay untracked and were not deleted;
  - `M_LampGlow.mat`, which batch runs re-save; it was restored and not committed.
- **Push:** **nothing was pushed to GitHub.**

## 25. Known limitations

- **Not redesigned in Phase 26:** character creation, advancement, the strategic map panels.
- **String table:** partial (§15).
- **No built-player measurements;** editor only.
- **Inventory re-render allocations:** about 2–3 MB of garbage per render with 66 rows; not pooled.
- **The codex:**
  - lists events only from the journal's last 40;
  - shows faction knowledge as standing and governance only;
  - has no relationship meters (none were added).
- **Not supported:** controllers and rebinding (deferred by the owner).
- **The narrow layout** appears only in tall, narrow windows.
- **Not hand-played:** Phases 24, 25 and 26.

## 26. Manual owner playtest checklist

`Systems/Phase26-Owner-Playtest-Checklist.md` has 21 checks. It separates automated tests and automated visual checks from your manual testing, and lists the feedback still outstanding from Phases 24 and 25.

## 27. Recommended Phase 27 priorities

Suggestions only; the next brief decides.

1. Hand-play Phases 24–26 and fix what you find.
2. Redesign character creation and advancement on the inspector pattern, keeping the Phase 22 rules exactly. Redesign the strategic map panels.
3. Finish the string table: settings, title, strategic HUD, the remaining hub sections, notices and command messages.
4. Pool inventory rows (the measured allocation), and profile a development build.
5. Physical traversal and destination integration (the old Phase 26 plan).
