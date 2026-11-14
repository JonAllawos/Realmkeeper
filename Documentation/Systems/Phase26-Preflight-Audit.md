# Phase 26: preflight audit

**Phase:** Interface Completion, Gameplay Experience Refinement, Codex & UI Quality Assurance.

**Audited:** 2026-11-14, from the code and from the Phase 25 captures of the running game, not from the Phase 25 report alone.

## 1. Git and project state

| Item | State |
|---|---|
| Project path | `C:\Dev\Realmkeeper`. The brief says `Documents/Projects/Realmkeeper`, but no project exists there; every phase has used this repository |
| Branch | `feature/phase-0-1-foundation` |
| HEAD | `5e2629a` (Phase 25), based on `9360330` (Phase 24) |
| Remote | **1 commit ahead** of origin (Phase 25, local only); nothing to pull |
| Tracked changes | None |
| Untracked | `Assets/InitTestScene37e5c1f4-….unity` and `Assets/InitTestScene8aeb0c53-….unity` (+ `.meta`): empty test-runner scenes, inspected in Phase 25. **Preserved**: not deleted, not committed |
| User changes | None found |

## 2. Test state

- **.NET Release:** re-run at the start of Phase 26 (the result is in §20 of the completion report).
- **Unity suites:** not re-run at preflight. Since the Phase 25 full checkpoint, exactly one file changed: the corrected Phase 19 assertion, which was re-run green (7/7) before the commit. Phase 26 ends with a full checkpoint.
- **Known order dependency:** `Phase19PlayModeTests.TheTitle_FoundsANamedWorld_…` fails when it is the very first test of a run, because its scene then counts as the launch scene. This is pre-existing and tracked; it was not fixed silently.

## 3. Interface classification (before Phase 26)

| Interface | Class | Real usability problems found |
|---|---|---|
| Trade | **Shared styling only** | <ul><li>Two long text lists with a Buy or Sell button per row; refused offers show only "—".</li><li>The price explanation is one line at the bottom, far from the row.</li><li>No item details or comparison; no quantity (always 1).</li><li>Particular goods, mending, the ledger and supply notes stacked below and easy to miss.</li></ul> |
| Crafting | **Shared styling only** (Phase 25 added row icons) | <ul><li>One long scroll of every recipe, with requirements in small one-line notes.</li><li>No way to see what can be made now.</li><li>No search or category filter.</li><li>"Start" disabled without a visible reason beside it.</li></ul> |
| Party and companions | **Shared styling only** | <ul><li>Text rows per companion; details in an expander.</li><li>No side-by-side inspection.</li><li>The empty state is one line.</li></ul> |
| Character creation | **Partially redesigned** (Phase 20/22: steps, cards, build panel) | Works; dense. Step cards show the gist, but requirements and traits sit in long text |
| Advancement | **Partially redesigned** (Phase 22) | Works; the overview and the choices are text-heavy. Behaviour must not change |
| Character sheet | **Partially redesigned** (Phase 20) | Usable; out of scope apart from consistency |
| Dialogue | **Partially redesigned** | <ul><li>Readable.</li><li>Kinds of response (a skill check, a party request, leaving) are only marked inside the text.</li><li>No speaker emblem.</li><li>"About them" is a separate toggle.</li></ul> |
| Strategic map panels | **Shared styling only** | Dense inspector; usable; the header already gives a faction badge and a status |
| Region map (N) | **Shared styling only** | Marks exist (Phase 23/24); there is no legend or key, so the meaning of marks is unexplained |
| Combat window | **Partially redesigned** (Combat Overhaul; Phase 25 health lines) | <ul><li>The target card shows numbers but no condition badges.</li><li>The result of the fight is only in the log.</li></ul> |
| Inventory and equipment | **Fully redesigned** (Phase 25) | — |
| Journal | **Partially redesigned** (grouped in Phase 25) | — |
| Codex | **Not implemented** | Knowledge exists in records (people met, factions, places, discoveries, events, creatures) but is not gathered anywhere |
| Settings and accessibility | **Fully redesigned** (Phase 25) | Not yet checked on the new screens; 16:10 and narrow layouts never captured |

## 4. Underlying functionality of the targets

| Interface | Data source | Commands | Notes |
|---|---|---|---|
| Trade | `GetShopDetail(ShopId)`: `ForSale`, `Offers`, `Goods`, `Repairs`, `Ledger`, `Shortfalls`, `Supply` | `BuyItemCommand(shop, itemId, quantity, expectedUnitPrice)`, `SellItemCommand(shop, stack, quantity, price)`, `BuyGoodsCommand`, `RepairAtShopCommand` | **Quantity is supported by the commands** but the UI always sent 1. Comparison existed only for owned stacks (`CompareItem`). Tests address rows `xk-trade-sale-*`, `-offer-*`, `-goods-*`, `-repair-*` and the first button in a sale row |
| Crafting | `WorkshopView` (`Recipes` with inputs, tool, station, DC, chance, `CanStart`, `Problem`; sites; repairs; attunable) | `StartWorkCommand.Craft(id, shop)`, `Gather`, `Repair`, `Attune`, `CancelWorkCommand`, finish work | One job at a time, by design. Tests address `xk-craft-<id>` with a "Start" button |
| Party | `GetParty()` (members, sheets, stance, loyalty in words) | Party commands (stance, dismiss, regroup) | Phase 15; companions are recruited in dialogue |
| Dialogue | `DialogueView` (speaker, line, options with kinds and checks) | Dialogue commands (pick option) | Must not change outcomes |
| Codex | Social records (`KnownPlaces`, `VisitedSettlements`, `Discoveries`, `Rumors`, `FactionStandings`), `NpcData.PlayerRegard` and `HasMetPlayer`, the journal's known events, `KnownCreatures` (v27) | — | **No new knowledge state is needed:** every entry can be derived from existing records |

## 5. Priorities and decisions

1. **Trade (complete).** Tabs for Buy, Sell, Particular goods and Mending, with counts. A list with icons, prices and refusal reasons beside the shared inspector, which shows:
   - the price explanation factor by factor;
   - the item's rules;
   - a comparison with what is worn (a new read-only `CompareCatalogItem`, the same rules path on a copy);
   - a quantity stepper bounded by stock, purse and offer;
   - the total and the purse after;
   - one confirm button with the disabled reason.

   The commands are unchanged; the quantity now goes through them. **All four lists stay in the tree** (only the current one shown), so the tests that address rows by name keep working.
2. **Crafting (complete).** Category chips with counts, search, "Can make now", and a recipe inspector with:
   - an output preview;
   - each material as have/need with a tick or a cross;
   - tool and station, check and chance, time;
   - the exact problem;
   - Start, or the reason it is unavailable.

   Gathering, mending and attunement keep their sections.
3. **Party (complete).** Member list (the player first, then companions), each with health and condition, and a member inspector showing sheet numbers, equipment, abilities, loyalty in words and the existing actions.
4. **Codex (new hub section).** Derived only from existing records (ADR-0117): people, companions, factions, settlements, places, creatures, discoveries, events. No new save state.
5. **Dialogue, combat, maps, creator, advancement.** Targeted improvements without changing behaviour:
   - dialogue response tags as badges, and a speaker emblem;
   - combat target condition badges, and a result banner;
   - a region map legend;
   - strategic inspector readability;
   - creator and advancement: shared inspector styling and spacing.

   Each is reported as complete or partial honestly.
6. **String tables.** A `RkText` table in `Realmkeeper.UI` (key → English), with the shared components and the new screens using keys. The rest is documented as remaining.
7. **Performance.** A PlayMode timing test: open, close and tab-switch times for each major screen, repeated open and close (object counts and managed memory), and codex search. Editor-only measurements are labelled as such. A development build is attempted if practical; otherwise the report says it was not done.
8. **Captures.** 1920×1080, 1366×768, 1280×720, 1024×768, 2560×1440, 3440×1440, **1280×800 (16:10)**, and a **narrow** layout (a 900×700 capture).

## 6. Risks

- **Tests that drive screens by element name.** Names are kept; where an action moves (as in Phase 25), the affected test is updated and reported.
- **The breadth of "complete every interface".** The report classifies each screen honestly.
- **Owner decisions in force:** the paid kits are rejected; controller support and rebinding are deferred; D1 (40 m a cell) is confirmed and must not change.
