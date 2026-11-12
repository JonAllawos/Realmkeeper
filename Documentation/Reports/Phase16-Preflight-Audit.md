# Phase 16 preflight: what exists for a connected world

Date: 2026-11-12. Every finding comes from the current source, not from earlier reports.

## Repository state

| Item | State |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| HEAD | `48b0098`, equal to `origin` after a fetch. The working tree was clean. |
| `ProjectSettings/GraphicsSettings.asset` | No longer an uncommitted user change: the owner had it committed (`48b0098`, a single-line reformat with no value changes) and pushed. Nothing to preserve. |
| Ignored owner files | `Documentation/Screenshots/dev-*.png`, `Assets/InitTestScene*`, `Art/ThirdParty/QuaterniusBestiary/` (local only, QAL) |
| Save schema | **v17** (`WorldData.CurrentSchemaVersion`); migrations v1→v17 |
| Generator | `RegionGenerator` v1 |

## Answers to the brief's critical questions

1. **Is the world multi-region?** No.
   - `WorldPopulationInitializer.EnsureRegions` adds one region (`Origin (0,0)`, 128×128 cells) and assigns every settlement to it.
   - `RegionGenerator` places 3 settlements (town, village, hamlet), carves one river and one lake, and builds roads between every pair.
   - `RegionData` has only an ID, a name, an origin and a size.
2. **Can settlements be referenced across regions?** In data, yes: `SettlementData.RegionId` exists, and IDs are world-unique GUIDs. But settlement positions are **cells of their region's map**, and every function taking a `RegionMap` treats all settlements as if they were on that map:
   - the 3D strategic view (`WorldView.Rebuild(map, GetSettlements())`);
   - `PlaceGenesis` (anchors);
   - `FaunaGenesis` (wildlife per region, but sampled on the one map);
   - `LivingWorldGenesis.EnsureLayouts`.

   A second region needs these scoped to their own map.
3. **Are roads persistent data?** No.
   - Roads are `RoadPath` cell lists inside `RegionMap`, which is **regenerated from the seed** on every load and never saved.
   - Their only persistent trace is the trade routes: `TradeRouteData` with `TravelDays = ceil(cells / 25)`, one per direction per road.
   - Bridges exist only inside places (Phase 14 `BridgeState` on place connections). The world has no infrastructure state.
4. **Are trade routes a functioning economic system?** Yes (Phase 2).
   - `TradeSystem` (order 150) sends `ShipmentData` of aggregate resources with departure and arrival days, pays on delivery, and turns back when the destination is full.
   - It checks conservation of every resource each day.
   - Phase 14 `RoadThreat` takes a cut where outlaws hold a road.
   - Shipments are **not** visible groups and do not move along geometry.
5. **Can NPCs or groups travel between settlements?** No.
   - NPCs move between locations inside their home settlement's schedule (`NpcActivity`).
   - Escorts follow the player.
   - Phase 10's road bands are `CreaturePopulationData` tied to a road; they do not travel.
6. **Are there simulation tiers?** Partly.
   - Every daily system runs over the whole world in aggregate.
   - `NpcSystem` already staggers full updates (`IsDue`, with up to 7 days of catch-up) and moves everyone along their routine daily.
   - There is no notion of the player's region versus a distant one.
7. **How does the clock behave over long travel?** There is one clock.
   - `WorldSession.Execute(AdvanceTimeCommand)` runs one transactional simulated day per midnight crossed, up to 1,440 minutes per call, and sets hunger by hour marks on absolute time.
   - Many small steps equal one large one (tested since Phase 5).
   - Combat freezes time.
   - There is no travel: "entering" any settlement from the strategic map is instant.
8. **How are events represented?** Two kinds:
   - `HistoricalEvent`: facts in the Chronicle, append-only and bounded.
   - Typed state elsewhere: crimes, quests, shortages (`IsInFoodShortage`), road threat, faction relation states.

   There is no persistent world-event entity with a lifecycle. Quests already arise from shortages (Phase 12 emergent templates).
9. **How does the party survive region transitions?**
   - Party members are the world's own people; active members are "away" from their schedule (Phase 15).
   - Entering or leaving a settlement keeps the party. There is nothing yet for another region.
   - A waiting member's settlement pose is not stored, so on re-entry their body appears behind the player (a Phase 15 limitation).
10. **Can a local scene be rebuilt from persistent data without duplicates?** Yes.
    - Settlement layouts are persisted (Phase 10, ADR-0047). `ExplorationModule.LayoutFor(world, settlement)` needs no terrain map.
    - Residents are drawn once per ID, and party members replace their resident body (Phase 15).
    - A settlement in another region can therefore be explored once its layout exists, and the layout is generated with that region's own map.

## Reuse, extension and new work

| Need | Existing | Plan |
|---|---|---|
| Region geography | `RegionGenerator` (terrain-first, deterministic, validated) | **Reuse the same generator** for every new region with a derived seed. Its whole pipeline runs per region (terrain, river, settlements, roads, economy, people, politics, layouts, wildlife). The result is merged. No second generator. |
| Coherent borders | The generator chooses a mountain side at random | **Extend:** a neighbour's mountain side is chosen from the region it borders, so a range continues across the border instead of stopping at it. Border crossings get their kind (pass, bridge, trail, road) from the terrain on both sides. |
| Persistent roads | Road geometry is regenerated; trade routes persist | **New:** a persistent **route graph** (segments between settlements with kind, length, condition and bridge state). The geometry stays regenerated. |
| Travel | Instant enter | **New:** journeys on the one clock, in hour steps through `AdvanceTimeCommand`. Entering a settlement in another region requires arriving there. |
| Caravans | Shipments (aggregate, conserved) | **Extend:** each shipment travels with a caravan group (leader, guards, position from its days). Attacks and losses go through the shipment's existing accounting. Blocked routes delay shipments. |
| Bandits | Phase 10 road bands, Phase 14 `RoadThreat` | **Reuse:** bandit groups are the road bands; defeating them lowers the threat. Events escalate or resolve from their counts. |
| World events | Chronicle facts; shortage flags | **New:** a typed event entity (lifecycle, scope, cause, consequences keyed once, player knowledge). Handlers act on the existing systems. |
| Factions | Treasury, influence, presence strength, relation states | **Extend:** patrols paid from the treasury, with strength from presence, and conflicts driven by relation states. |
| Knowledge | `NpcMind.Learn`, sharing between people who meet | **Reuse:** events become NPC knowledge in the settlements that witness them or that travellers reach. The player learns by talking, from boards and by observation, with "as of" times. |
| Quests | Bound templates, `QuestLifecycle.Create` | **Reuse:** two compact templates created from events. |
| Tiers | `NpcSystem` staggering | **Extend:** distant regions' people get their full update only weekly. Aggregates run everywhere. |
| Map | 3D region view, HUD panels | **Extend:** the 3D view shows the player's region and its settlements only. A new world-atlas panel shows every region, route, group and event with layers, a legend and travel planning. |
| Save | v17 | **v18**, with a structural migration. An existing world keeps its single region until the player chooses to discover the lands beyond (one deterministic command), so no existing settlement or NPC is regenerated. |

## Hazards

- **Tests and existing worlds assume three settlements.** New games (the session's default settings) get the connected world. Explicit settings and test worlds keep one region unless asked.
- **Positions are region-local.** Every map-taking routine must scope to its region.
- **Exactly-once:** shipments are conserved by `TradeSystem`. Caravan losses must be recorded as losses, never silently removed.
- **Travel versus combat time:** combat freezes the clock. A journey interrupted by a fight resumes from the same state.
- **The Phase 15 carry-forward** list goes to the roadmap. This phase addresses the party on the world map and waiting members' positions only.
