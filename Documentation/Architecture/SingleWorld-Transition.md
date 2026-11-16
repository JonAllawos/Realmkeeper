# Single-world transition architecture (Phase 30)

**Date:** 2026-11-16. **Status:** proposed; **not approved and not implemented**. Each step needs its own brief.
**Basis:** `CanonicalWorld-Audit.md`.

## Goal

The player starts one recognisable world, always the same, and every save is a state of that world. Generation stays the tool that builds the world; it stops being something the player chooses.

## 1. New Game produces the canonical world

| Today | Proposed |
|---|---|
| "New world": name, seed (rerolled at random), speed | "New Game": character creation first, then the opening (Campaign Bible §3, §11 implementation note) |
| Each world gets a slot named after it | The player picks one of three save slots (`Systems/SaveLoad-Design.md`) |
| "Campaigns" page: start a campaign in a world | The main campaign is part of the canonical world; optional authored arcs start from within play |

- `CreateWorldCommand` stays. The canonical New Game calls it with the world definition's fixed name and seed.
- The seed field becomes developer-only (a debug menu and the tests).
- Existing developer saves keep loading as they are; they are not the canonical world.

## 2. A baked, versioned world definition

**Problem:** geography is regenerated from code on every load (ADR-0005). Any change to `RegionGenerator`, `OverlandTerrain` or their tuning silently moves rivers, heights and passability under saved settlements, places and players. In a shipped game with patches, that is the main persistence risk.

**Proposal:**
- A `WorldDefinition` asset, versioned, shipped with the build. It holds:
  - the macro-map: regions, kinds and atlas positions;
  - each region's baked `RegionMap` (16,384 cells; ADR-0005 put this at 100–300 KB as JSON, less in a binary form);
  - authored terrain edits (`TerrainEdit` already exists);
  - the authored anchors (§3).
- **Build time:** a developer tool runs the existing generators from the canonical seed, then applies authored edits, then writes the asset.
- **Run time:** loading reads the baked maps instead of regenerating them.
- Saves record the definition version. A save from an older version is migrated by an explicit, tested step, like layout v2 to v3 in ADR-0121.
- **This is ADR-0005's documented fallback**, chosen for a shipped world. It needs a new ADR before work starts.

## 3. Authored anchors

The campaign engine casts its roles from whatever the world contains (ADR-0082: the home town, a person by trade, the far end of a road). That suits emergent stories, not a 40-hour authored campaign.

- **Anchors** are entities the story needs, with fixed IDs in the world definition: named settlements, sites (the garrison, ruins, gateways), people (crew members, antagonists, quest givers) and factions.
- Generation fills the world **around** anchors: an anchor reserves its site, and the generator places the rest.
- A new campaign role binding, "the anchor with this ID", sits beside the dynamic bindings. ADR-0084's narrative resilience still applies: an anchor can die, and the story must say what then.
- **Canon:** anchors carry no names or roles until the owner decides them (Campaign Bible §5, §8, §10 are open). The mechanism can be built with placeholder anchors marked provisional, as *The Waystone Count* is.

## 4. A larger atlas

- Replace `WorldAtlasGenesis.Columns = 3` and `MaxRegions = 9` with the macro-map's own layout.
- Place cities, fortresses and ports (types that exist but are never generated). Allow more than three settlements a region where the macro-map says so.
- **Size:** chosen from the content plan, then checked against measured budgets (`CanonicalWorld-Audit.md` §3). No size is proposed before the campaign's regional plan exists.

## 5. Continuous borders

- Generate each region's edge from a shared border strip: the same heights, rivers and roads on both sides.
- `CrossRegionCommand`: allowed at any passable border cell, not only the crossing point. The player arrives at the matching cell of the neighbour.
- Keep the loading transition (ADR-0112).

## 6. Scaling the command and save model

| Today | Proposed | Why |
|---|---|---|
| Each command deep-copies the whole `WorldData` (7.8 ms at 9 regions) | Copy only the aggregates a command touches (copy-on-write per collection or per region), keeping atomic commit and rollback | Frame cost must not grow with the world |
| Indented JSON written on the main thread (608 ms) | Compact JSON (about half the size), serialised from an immutable snapshot on a worker thread, then the atomic swap | Autosaves without a visible hitch |
| One file, atomic replace, no backup | Rotating backups and a recovery path (`Systems/SaveLoad-Design.md`) | A shipped game must survive a bad write |

Each change keeps the existing guarantees: validated input and output, atomic commit, the one source of truth.

## 7. Simulation at scale

- Measure the daily tick and the 4-days-per-tick catch-up against region count (9, 16, 25) before choosing a size.
- If needed, add a **dormant** tier: regions far from the player advance in aggregate (economy, population, weather) and catch up in detail when approached. It must be deterministic, so a region's state does not depend on when the player visits.

## 8. Order and dependencies

1. **New Game and three slots** (§1, `SaveLoad-Design.md`): small, visible, and unblocks everything player-facing.
2. **Save reliability and the command copy** (§6): before the world grows.
3. **World definition and baking** (§2): before any authored geography or campaign content is placed, because content placed on regenerated ground can move.
4. **Anchors** (§3): needs §2 and owner canon decisions.
5. **Larger atlas, cities and borders** (§4, §5): needs §2 and the campaign's regional plan.
6. **Dormant tier** (§7): only if measurement asks for it.

## 9. What does not change

- Domain, Application, Infrastructure and Presentation stay separated (`Architecture/Overview.md`).
- The one `WorldData`, one scheduler and one save document.
- Deterministic, idempotent generation and the `Ensure…` genesis pattern.
- The region as the unit of streaming and simulation tiers.
- Existing developer saves load; they are not converted into the canonical world.
