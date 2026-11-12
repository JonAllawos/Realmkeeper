# Data ownership

| Data | Owner | Mutated by | Persisted | Source of truth |
|---|---|---|---|---|
| `WorldData` (identity, seed, versions, date, settings) | `WorldSession` | Commands only | Yes (JSON) | Save file |
| `SettlementData`, `ResourceInventory` | `WorldData` | Simulation systems (on a working copy), generator | Yes | Save file |
| `FactionData`, `NpcData` | `WorldData` | Generator (Phase 1) | Yes | Save file |
| `HistoricalEvent` | `WorldData` | Appended by the scheduler; immutable | Yes | Save file |
| `RegionMap` (elevation, terrain, rivers, roads) | `WorldSession` (one per region: `MapFor`; `Map` is the player's region, Phase 16) | Generator; future `IRegionMapEditor` | **No**, rebuilt from each region's own seed + generator version | Region seed |
| `RegionData` atlas fields (atlas cell, seed, mountain side, kind, discovery, neighbours, landmarks) (Phase 16) | `WorldData` | Atlas genesis (once); travel (discovery) | Yes | Save file |
| Route graph (`RouteSegmentData`: kind, length, terrain, condition, bridge, blockade) (Phase 16) | `WorldData` | Atlas genesis (once); world events (bridges, blockades) on a working copy | Yes | Save file (geometry still regenerated from the map) |
| Campaign instances (`CampaignInstanceData`: bindings, chapter/arc progress, decisions, pending consequences, applied keys, flags, quest links, history) (Phase 17) | `WorldData.Campaigns` | Campaign commands and the daily campaign pass, on a working copy | Yes | Save file (schema v19) |
| Campaign definitions (`CampaignDefinition`) (Phase 17) | `CampaignCatalog` (code, validated) | Authoring only | **No**, only the version an instance began with | Build |
| Companion remarks and second story (`CompanionRecordData.Remarks`, `StoryQuestId`) (Phase 17) | `WorldData.Companions` | Campaign decisions, arrivals, the daily pass; heard in conversation | Yes | Save file |
| Journey (`JourneyData`) (Phase 16) | `WorldData` | Travel commands only | Yes | Save file |
| Travelling groups (`TravelingGroupData`) (Phase 16) | `WorldData` | `realmkeeper.groups`, `realmkeeper.faction-activity`, world events (on a working copy) | Yes | Save file (cargo is the shipment's; strength of bandits is the population's) |
| World events (`WorldEventData`), including what the player knows of them (Phase 16) | `WorldData` | `realmkeeper.world-events`, `realmkeeper.groups`; travel and arrival (the player's knowledge) | Yes | Save file |
| Atlas view (`AtlasView`) and its selection and layers (Phase 16) | Presentation (queries) | Input | No | Transient, derived from the world |
| Simulation rule constants (`FoodEconomyRules`) | Composition root | Configuration | No | Code / future ScriptableObject |
| GameObjects, meshes, materials | Presentation | Presentation | Never | Derived from the two rows above |
| UI selection state (selected `SettlementId`) | Presentation | Input | No | Transient |
| `SettlementEconomy`, trade routes, shipments (Phase 2) | `WorldData` | Economy and trade systems (on a working copy), generator | Yes | Save file |
| *Future:* character state (position, perspective, inventory) | `WorldData` | Exploration commands only | Yes | Save file |
| *Future:* local environment state (changes inside a settlement) | `WorldData`, keyed by settlement and region ID | Exploration commands only | Yes (as deltas over generated or authored content) | Save file + seed |
| *Future:* exploration GameObjects (buildings, props, NPC avatars) | Exploration presentation | Streaming | Never | Derived from world data |

## Rules

- **Entity identity is a GUID**, never a name. Selection, lookups, events and save files all
  use `SettlementId` / `FactionId` / `EventId`. Names are display data and may repeat or change.
- **IDs are deterministic.** The generator and the scheduler draw GUIDs from seeded streams,
  so the same seed gives the same IDs. Only `WorldId` is random by default, because two
  worlds made from the same seed are still different worlds.
- **Presentation never holds mutable domain objects.** It receives snapshots and sends commands.
- **ScriptableObjects may hold configuration**, such as visual themes or tuning values, but
  never world state.
- **Unity scenes contain no world state.** A scene holds only the bootstrap and empty
  containers. Everything in the world is spawned from data at runtime.
- **One world for both perspectives (binding, ADR-0009).** Exploration mode reads the same
  `WorldData` through the same session and changes it only with commands. It never keeps a
  parallel copy of settlements, NPCs or resources, not even a cache that outlives a snapshot.
- **Companions are not copies (Phase 15, ADR-0073).** A party member is the world's own `NpcData`.
  `PartyData` records only who travels together, and `CompanionRecordData` records only loyalty and
  the history of the arrangement. A companion's items are item instances held once, like the
  player's. The follower bodies in a settlement hold no state: they read the party from the session
  and change it only through party commands.
- **Campaigns are not copies (Phase 17, ADR-0082).** A campaign instance holds only role bindings (IDs of the
  world's own people, settlements, factions, roads and regions), its own progress, and links to quest
  instances in `WorldData.Quests`. What has become of a story's people is read from the systems that
  decide it (`CampaignCharacters.StatusOf`), never stored. Campaign effects change the world only
  through each system's own path (reputation ledger, companion loyalty, world events, NPC mind,
  quest lifecycle), each applied once by key.
- **A road fight's ground is presentation (Phase 17, ADR-0086).** While a fight on the road is staged,
  the world still has the party on the road (`LocationKind` WorldMap, the journey naming the
  encounter). The ground scene is built from the near settlement's layout without buildings or
  people, and is not saved.
