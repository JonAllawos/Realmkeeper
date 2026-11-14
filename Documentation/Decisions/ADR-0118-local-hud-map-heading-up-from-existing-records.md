# ADR-0118: The local HUD map is heading-up and drawn from existing records

**Status.** Accepted, 2026-11-14 (Phase 26.1). No save change.

**Context.**
- **The ask.** The owner asked for a compact local map in the exploration HUD:
  - small, attractive and in Cartographer's Ledger;
  - centred on the player and turning as the player turns;
  - with coloured, labelled marks for nearby people, shops, settlements and landmarks.
- **Constraints.** It must not reveal hidden or unknown things, must not become a second location or NPC system, and must not cost noticeable frame time.
- **The data already exists:**
  - the settlement plan;
  - the shop views;
  - the residents in the street;
  - companions;
  - the region's terrain and roads;
  - known sites, found landmarks, the waypoint;
  - the travelling groups.
- **One rule already decides whether a person is known:** `KnownPersonQueries.AcquaintanceOf`, the same rule the codex uses.

**Decision.**
1. **Heading-up.**
   - The player is at the centre, pointing up. The ground and every mark turn beneath by the player's heading (`PlayerCharacter.Heading`, which movement uses).
   - A small "N" on the rim shows where north lies.
   - The projection is `LocalMapGeometry.Project` (pure, in Application).
   - Smoothing is exponential, about 90 % of a turn in 0.1 s, and exact when Reduce motion is on.
2. **Drawn, not glyphs.**
   - The ground (roads, water, fields, footprints; or the country's 40 m cells and roads) is drawn once per area with Painter2D into a canvas.
   - The canvas is moved and turned by its style each frame, and redrawn only when the player has walked 60 % of the range from where it was drawn, or the range changes.
   - Marks are Painter2D shapes, so no font glyph can be missing. Each kind has its own shape as well as its colour.
3. **Marks come only from existing records** (table in `Systems/Phase26.1-Local-HUD-Map.md`):
   - people only when out (not hidden);
   - named only once met, otherwise by their trade;
   - sites only when known, landmarks only when found;
   - outlaws only within 70 m (sight).
4. **Update strategy.**
   - Still marks are made once per environment. A new `EnvironmentRoot` (another town, the country, a reload) rebuilds the area.
   - Moving marks are refreshed every 0.4 s and are reused by key, not recreated.
   - Their positions follow their transforms each frame.
   - Names, acquaintance and landmarks are re-read every 2 s.
   - Up to five labels are chosen four times a second, greedily by importance then nearness. They never overlap each other or the player.
5. **Placement.** Bottom right, 184 px; smaller at compact and narrow widths. It is hidden behind any window, menu or fight, and in places (interiors and mines).
6. **No save change.** The range choice is not persisted.

**Consequences.**
- Nothing new is stored, and nothing is revealed that the player could not see or know.
- Changing a record (meeting someone, finding a landmark) changes the map within 2 s.
- The map shows nothing inside places. That is a limitation, recorded in the known limitations.

**Alternatives considered.**
- **North-up with a turning arrow.** Rejected: the owner asked for the map to turn with the player.
- **A second camera rendering the scene from above into a texture.** Rejected:
  - It costs a camera render each frame.
  - It shows everything, hidden or not.
  - It would not read as a ledger map.
- **Glyph marks (as on the region map).** Rejected: shape coverage depends on the font. Painter2D shapes are certain.
- **A per-frame full rebuild.** Rejected for cost.
