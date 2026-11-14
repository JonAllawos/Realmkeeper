# ADR-0117: The codex is derived from existing knowledge records

**Status.** Accepted, 2026-11-14 (Phase 26). No save change.

**Context.**
- **The ask.** The Phase 26 brief asks for a codex of what the player has discovered: people, factions, settlements, places, creatures, discoveries and events. Discoveries must persist, must not duplicate, must update as more is learned, and must never disclose what the player does not know.
- **No new lore.** The ancient world is under campaign development, so nothing may be invented.
- **The knowledge already exists in saved records:**
  - `Social.VisitedSettlements`, `KnownPlaces`, `Discoveries`, `Rumors`, `FactionStandings`;
  - each NPC's `MetPlayerOn` and `PlayerRegard`;
  - `PlaceData.Discovery`;
  - `Companions`;
  - the journal's known events;
  - `KnownCreatures` (v27, ADR-0116).
- **One rule already decides what the player knows of a person:** the Phase 20 known-person rules (`KnownPersonQueries`).

**Decision.**
1. **`WorldSession.GetCodex(search, category, recentFirst)`** builds the codex on demand from those records. There is no codex state and no save change. Persistence, freedom from duplicates and updating come with the records themselves.
2. **Categories and their sources:**

   | Category | Source |
   |---|---|
   | People | NPCs the player has met or spoken with; facts from `KnownPersonQueries`, each with how it is known |
   | Companions | `Companions` records |
   | Factions | A standing with the player, or governing a visited settlement |
   | Settlements | Visited, or heard of |
   | Places | Rumoured (name and region only), found or visited (with the place's own description) |
   | Creatures | The bestiary record |
   | Discoveries and events | The journal's own entries |

3. **Never listed:** strangers, unheard-of settlements and unknown places. What knowing more would add is written as a note, never shown blank.
4. **Nothing is invented.** Names and descriptions are the world's own generated data; no ancient-world entry exists unless the campaign creates the records.

**Consequences.**
- **No migration.** The codex works on every existing save.
- **Cost:** built per call; it is called when the Codex section renders, not every frame (measured in Phase 26's timings).
- **Exact by construction:** the codex can never contradict the journal or the people inspector, because it reads the same records with the same rules.
