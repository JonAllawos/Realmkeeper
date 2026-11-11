# ADR-0053: Loot is deterministic, filled once into a container, and bounded by rarity

**Status.** Accepted, 2026-11-11 (Phase 11).

**Context.**
- Phase 10 dropped coin and a few items from the bestiary's loot line straight into the player's pack.
- Gear the foes actually carried was ignored, and nothing limited rarity.

**Decision.**
1. **Tables are data.** `LootTables` holds tables for outlaws, captains, cultists, goblins, soldiers, game, wolves, bears, vermin, spiders, the undead, giants and old caches.
   - Entries can be guaranteed, have a chance, or be weighted picks.
   - Entries have quantity ranges, a condition, and required tags. A nested table is allowed.
   - `LootTables.Validate` checks every reference and range, and rejects lines that could never be given.
2. **What was carried.** `LootTables.Carried` lists each humanoid's weapons, worn or damaged as used. Creatures without a line fall back to the bestiary's own loot line, marked worn.
3. **Seeded and once.** `WorldEncounters.FillLoot` rolls from the encounter's seed and each combatant's ID. The same fight leaves the same things.
   - The loot goes into a `Loot` container keyed by the encounter.
   - A second call returns the existing container unchanged.
   - Coin goes into the container too.
4. **Rarity.** A `LootContext` caps rarity (Uncommon for fights and caches). Rare entries also need a `rare-allowed` tag.
5. **The player chooses.** The combat panel offers the loot, which opens in the hub's Storage section. Take one thing, or take all (all or nothing by weight).
6. **Bounded.** At most 30 loot piles beyond the settlements' own containers are kept. The oldest are cleared first, never chests or caches.

**Consequences.**
- Replays, reloads and a second resolution cannot duplicate loot.
- Out-of-town piles are reachable from Storage wherever the player is. There are no physical drop sites on the field yet.

**Alternatives considered.**
- *Rolling when the container is opened:* the result would depend on when the player looks.
- *Physical item props on the field:* there is no persistent field location to put them in yet.
