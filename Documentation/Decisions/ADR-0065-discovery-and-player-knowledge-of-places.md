# ADR-0065: What the player knows of a place is a state; the map, journal and scene show only that

**Status.** Accepted, 2026-11-11 (Phase 13).

**Context.**
- Map markers were unfinished in Phase 12.
- A rumour should not reveal exact whereabouts. Secret rooms and unexplored interiors must not show early.

**Decision.**
1. **States.** Each place has a `PlaceDiscovery`: Unknown → Rumored → Discovered → Visited → Explored → Cleared. It only ever rises.
   - Rumored: residents of the nearest settlement mention it when asked about rumours. That gives a name and a rough direction.
   - Discovered: asked again, a resident gives the way. A quest giver can also reveal it (`RevealPlace`), or reading a document can.
   - Visited: on entering.
   - Explored: every room that is not secret has been seen.
   - Cleared: no hostile inhabitants remain. A garrison holds its fort, so a fort is never cleared while its garrison stands.
2. **The map.** It lists Rumored places by name and direction only. Discovered places get a marker at their cell and their known ways in: a hidden side door is listed only once learned.
3. **The journal.** A Places tab shows the same.
4. **The scene.** It is built from `GetPlaceScene`:
   - secret rooms and passages only once found;
   - hidden features not drawn, although an armed hidden trap still goes off if stepped on;
   - inhabitants still there, and people held, following or at home inside.
5. **The place map.** The in-place map (N) draws only explored rooms, floor by floor.
6. **The Chronicle.** It records a place's discovery, first entry, clearing, secret ways found, traps sprung, captives freed, escorts brought home and trespass.

**Consequences.**
- Quests can ask the player to find, enter, explore a room, reach a floor, disarm a trap or use a feature, all through the Phase 12 objective engine (new objective kinds and events, no second engine).
- No route or travel time is shown, because the world model has none.

**Alternatives considered.**
- *Markers for everything generated:* removes discovery.
- *Fog of war on the region map:* larger than this phase, and not needed for places.
