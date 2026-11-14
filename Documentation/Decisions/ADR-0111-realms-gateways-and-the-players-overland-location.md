# ADR-0111: Realms, gateways, and the player's overland location

**Status.** Accepted, 2026-11-13 (Phase 23). It adds save schema v25.

**Context.**
- **The ancient world has no model.** The Campaign Bible (§5) places an ancient world beside the surface, reached through gateways, but the world had no realm.
- **What is still undecided.** The ancient world's names, cultures and inhabitants. The brief forbids inventing them.
- **The player had no overland location.** The player was only ever on the map, in a settlement or in a place. They needed a fourth location, the country, with a position that saves and loads.

**Decision.**
1. **Realms (`Domain/World/Realms.cs`).** A `RealmData` holds:
   - an id, a name and a profile (`RealmProfiles.Surface` or `AncientPrototype`);
   - its regions, each a `RealmRegionData` with its own seed and kind.

   The surface realm (`realm:surface`) names the world's existing regions. The ancient realm (`realm:ancient`) has **one prototype region that is geography only**: no settlements, people, factions or names beyond "Prototype region (unnamed)" and "The ancient world (working name)".
2. **Gateways.** A `GatewayData` holds an id, a name, a state (`Dormant`, `Open` or `Sealed`), a discovered flag, and two ends (realm, region, cell).
   - **The prototype gateway.** `RealmGenesis` places one (`gateway:prototype-1`), dormant, on the surface far from settlements and places, with its far end at the centre of the ancient region.
   - **Opening it.** Nothing in play opens it. Only `SetGatewayStateCommand`, the campaign's hook, does.
3. **Creation is idempotent.** `RealmGenesis.Ensure` runs from `WorldAtlasGenesis.Ensure`, in new games and on load, and adds realms and gateways once. Ids come from the world seed, so a migrated save and a new game agree.
4. **The player's overland location.**
   - `CharacterLocationKind.Overland` (3), with the player's realm (`RealmId`, empty meaning the surface), region and pose in region metres.
   - `ReturnToOverland` remembers that a place was entered from the country, so leaving it returns there, at the place's cell.
5. **Commands, each validated and leaving the world unchanged when refused:**
   - `EnterOverlandCommand`, `LeaveOverlandCommand`, `WalkOverlandCommand` (pose; refuses impassable ground);
   - `EnterSettlementFromOverlandCommand`, `CrossRegionCommand` (only by a passable crossing route to a neighbour);
   - `UseGatewayCommand` (only an open gateway within reach), `SetGatewayStateCommand`.
6. **Discovery by sight.** Walking reveals:
   - places and gateways within 140 m, which become discovered with today's date;
   - the region itself, which becomes visited.

   A site is used within 30 m.
7. **Save schema v25.**
   - Realms, gateways, and the player's `RealmId` and `ReturnToOverland` are written and read.
   - `MigrationV24ToV25` is structural: the fields are optional, and the atlas step adds realms and gateways on load.
   - **A save from v24 loads unchanged** (tested). No migration moves a player into the country.
8. **Validation.** `WorldValidator.ValidateRealms` checks:
   - unique ids, and gateway ends that name a realm and a region of it;
   - an overland player stands in a known region of a known realm.

**Consequences.**
- **Simulation tiers stay per surface region.** `RoutePlanner.PlayersRegion` ignores non-surface realms. While the player is in the ancient realm, the surface simulates from the region they left.
- **The ancient region is a geography prototype only.** Its content waits for the campaign decisions the Bible leaves open.

**Alternatives considered.**
- **The ancient world as extra surface regions.** Rejected: it would break the atlas's grid, its crossings and the simulation tiers.
- **Gateways as places.** Rejected: places are interiors with layouts. A gateway joins two walkable regions.
