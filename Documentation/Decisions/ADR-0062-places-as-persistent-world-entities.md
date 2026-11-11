# ADR-0062: Explorable places are persistent world entities in a hierarchy, with one architecture for every kind

**Status.** Accepted, 2026-11-11 (Phase 13).

**Context.**
- The brief asks for dungeons, ruins, caves, mines, crypts, forts and building interiors that keep their state between visits and across saves. They must be part of the same authoritative world, not disposable scenes.
- "Location" already means a Phase 4 place inside a settlement that NPC schedules point at (`LocationData`: a hall, a house, the plaza).
- The player could be in one of two places: on the map, or in a settlement.

**Decision.**
1. **One model.** `PlaceData` (in `WorldData.Places`) is every explorable place:
   - identity: a stable id, its definition, name, category and archetype;
   - where it hangs: its parent (region, settlement, building or another place) and its map cell;
   - its saved layout;
   - what the player knows of it;
   - who holds it (a faction);
   - its threat;
   - every change made to it: explored rooms, door states, found secret ways, feature states, group states, small named facts, notes.
2. **Names.** In code they are *places* (`PlaceId`, `PlaceData`), so the two meanings of "location" never mix. An interior refers to its building's Phase 4 `LocationId`, so schedules and interiors agree.
3. **Hierarchy.** World → region → wilderness place (mine, crypt, fort), and world → region → settlement → building (Phase 4 location) → interior place. Inside, places have floors, then rooms, then features. Features may be Phase 11 containers.
4. **Definitions and instances.** `PlaceDefinition` and `ArchetypeDefinition` are templates; `PlaceData` is the instance with its own layout. A definition may be authored (the watch fort) or generated (the mine, crypt, cave and ruin). Interiors are generated from their building the first time anyone enters, then saved.
5. **The player.** `CharacterLocationKind` gains `Place`, with `PlaceId`, `PlaceFloor` and `PlaceRoom`. The pose is then in the place's own space (1 cell = 1.5 m = one combat square).
6. **Transitions.** They are commands:
   - entering a place from the map or from its settlement;
   - entering a building from the street;
   - leaving by a way in (an interior puts the character back outside its door, a wilderness place back on the map);
   - moving between rooms;
   - taking stairs and ladders.

   None copies a person: followers are the same `NpcData`, and the scene shows one body per identity.
7. **One environment at a time.** A place loads through the same streamer as a settlement, keyed by place. A settlement and a place are never loaded together.

**Consequences.**
- Everything a place knows is saved: schema v15, with `MigrationV14ToV15`. Older saves gain the wilderness places when they are loaded, as earlier phases' content does.
- New kinds of place (a camp, a tower) are new archetypes and definitions, not new systems.
- Fast travel, route distances and travel time are not modelled. Entering from the map is immediate, as entering a settlement already is.

**Alternatives considered.**
- *Separate scene types per kind of place:* duplicated rules and state.
- *Rebuilding the settlement's Phase 4 locations into places:* those are schedule targets, not explorable spaces. Interiors refer to them instead.
- *Keeping interiors open-plan in the settlement scene:* the tavern hall and shops stay that way; enclosed interiors need their own space.
