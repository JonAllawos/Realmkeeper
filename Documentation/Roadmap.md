# Roadmap and milestone status

| Milestone | Status | Where |
|---|---|---|
| Phase 0–1: foundation, world generation, strategic map | Done | `Documentation/Architecture`, ADR-0001…0007 |
| Phase 2: settlement economies, trade, chronicle | Done (`fa521f2`), completed by the Phase 2 completion pass below | `Documentation/Systems/Economy.md` |
| ADR-0009 prerequisites and exploration vertical slice | Accepted as a vertical slice (`0c7f60d`) | `Documentation/Architecture/DualPerspective.md` |
| Pre-Phase-2 stabilization: session rebinding, save slots | Done in the Phase 2 completion commit | `DualPerspective.md`, `Systems/Persistence.md` |
| Phase 2 completion | Done; see the test results for what was run | `Systems/Economy.md`, `Testing/TestResults.md` |
| Phase 3: factions and political engine | Done (`5c89f8a`, playtest fixes `d82a01b`) | `Documentation/Systems/Politics.md` |
| Graphics pass | Done (`b0545b5`): CC0 modular kit, humanoid residents, street-level lighting | `Reports/GraphicsPass-Report.md` |
| Phase 4: NPC Intelligence Engine | Done (this milestone); see the test results for what was run | `Systems/NPCs.md`, `Systems/Relationships.md`, `Reports/Phase4-NPC-Intelligence.md` |
| Phase 5: Living World, Player Hub & Commerce | Done (this milestone); see the test results for what was run | `Systems/WorldTime.md`, `Systems/Player.md`, `Systems/Inventory.md`, `Systems/Equipment.md`, `Systems/Commerce.md`, `Reports/Phase5-Living-World.md` |
| Phase 6 | **Not started.** It needs its own brief. | — |

## Phase 2 completion: what it added

- **Trade:** routes can be suspended and resumed through a typed command, which is chronicled
  and saved. Schema v4 adds this, with a migration from v3.
- **Inspector:**
  - a "Why" line under prosperity and stability, listing the terms behind each target;
  - the food reserves trend, and whether a shortage is easing or worsening;
  - "Generalist" for settlements without a specialization;
  - a Suspend/Resume toggle on each route, and a count of active routes.
- **Map:** markers above settlements for food shortage, declining stability, strong production
  and trade on the road.

## Known limitations carried forward

- One region per world; three settlements in the default world.
- There are no births, deaths, migration of individuals, construction, crafting or caravans.
  These were out of scope.
- Performance was measured only at the default world size: a 30-day run takes 7–9 ms. No
  claim is made for larger worlds.
- One save slot is active at a time. There is no save browser.
- The map markers use `Universal Render Pipeline/Unlit` found by name. A player build must
  include that shader (editor runs are unaffected). Not yet verified in a build.
## Phase 3: what it added

- **Factions:** first-class factions of seven types. They have status, leaders (existing
  residents), influence explained by its terms, a treasury funded by real dues, aggregate
  membership, presence, and goals.
- **Governance and claims:** governance and administration of settlements, and territorial
  claims that never move borders by themselves.
- **Relationships:** multi-dimensional, with readable states, causes and agreements.
- **Simulation:** a weekly political tick inside the daily transaction, tied to stability,
  prosperity and trade.
- **Player actions:** nine typed political commands, each chronicled as the player's.
- **UI:** the Factions list and inspector, a Politics section in the settlement inspector, and
  an optional political map overlay. The exploration HUD names the governing faction.
- **Saves:** schema v5, with a deterministic upgrade of older saves.

Still out of scope: warfare, rebellion, diplomacy trees, NPC political AI, religion or crime
detail, multiple regions.

## Phase 5: what it added

- **A clock:** a minute of the day on top of the date. Each midnight crossed runs one simulated day.
  - The realm map keeps whole-day pacing.
  - Settlements run in minutes, with pause, speed and wait.
- **Light that follows the clock:** sun, golden hour, moon, sky, haze, lit lamps and windows.
- **Residents who walk** to where the world says they are each hour. Their poses match what they
  are doing, and they go indoors at home or asleep.
- **A full player character:** identity, renderable appearance, purse, hunger, an inventory of
  stacks with one owner each, and eight equipment slots shown on the body.
- **The Player Hub:** eight sections and a live 3D preview.
- **Shops kept by existing residents:**
  - stocked from the settlement's stores through the existing ledger, paid for by the keeper;
  - atomic buying and selling, with explained prices and no arbitrage;
  - reputation that grows once a day;
  - trades in the ledger and the chronicle;
  - keepers remember notable trades.
- **Walk-in Outfitters and Smithy** whose shelves show every unit of stock.
- **Knowledge-bound talk** about work, shops, goods, prices and factions.
- **Save schema v7**, with a once-only migration that matches a fresh world.

Known limitations are listed in `Reports/Phase5-Living-World.md` §15. The main ones:

| Area | Limitation |
|---|---|
| Combat | None |
| Props | Procedural |
| Clothing | Two outfit families |
| Walking | Street routes rather than a navmesh |
| Shop stock | Counts, not item instances |
| Loose items | None in the world |
| Map advances | Leave the time of day and hunger unchanged |
