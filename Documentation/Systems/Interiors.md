# Building interiors

Phase 13. Decisions: ADR-0062, ADR-0063, ADR-0066. Code: `InteriorGenerator`, `EnterBuildingHandler`, `ExplorationController.Places`.

## Which buildings

Every settlement building with a function has an interior, except:

- the tavern hall and shops, which stay open-plan in the street as before;
- market halls, sheds and stables.

In the street, a door interactable (for example "Enter House 4", named after the building's Phase 4 location) stands at the building's door.

## Plans

The plan comes from the building's function, footprint (metres → cells, 5 to 14 per side), storeys and the settlement's wealth.

| Plan | Functions | Rooms |
|---|---|---|
| home | Home, LargeHome, Cottage, Farmhouse | Main room (hearth, desk if wealthy); bedroom (restricted, bed, household chest); larder if wide enough; upper room (bed) with a second storey; cellar (dark) at wealth ≥ 60 |
| workshop | Smithy, Workshop, Mill | Forge or workshop (forge or workbench); store room (crate); quarters (restricted, bed) |
| store | Granary, Warehouse, Barn | Store hall (crates); keeper's office (the granary keeper's working ledger, or a desk); cellar at wealth ≥ 60 |
| guard | GuardPost, Gatehouse | Guard room (duty roster); cells (restricted); armoury (restricted, locked DC 15, garrison chest) |
| chapel | Religious | Nave (altar); vestry (restricted, desk) |
| civic | Civic | Hall; clerk's office (restricted, desk, chest) |
| tower | Keep, Watchtower | Tower hall (banner); guard floors and a watch room up the stairs (up to 3 storeys) |

- Rooms share walls. Doorways are cut in the shared wall, 2 cells wide, as openings or doors.
- Stairs join the main room to the upper floor and, where there is one, the cellar.
- The plan is validated like any other layout (720 combinations tested).

## The first entry

`EnterBuildingCommand`, in the command's transaction:

1. Creates `PlaceData` with:
   - id `interior:{settlement}:{layout key}`;
   - the building's Phase 4 `LocationId` and name;
   - `ExitPose` outside the door, facing out;
   - a seed from the world seed and the id;
   - its plan.
2. Fills its containers.
3. Enters it.

Later entries reuse the same place. Leaving puts the character back outside the same door.

## People inside

- People whose current whereabouts (`NpcActivityPlan`) is the building are shown inside, in the room their activity suggests: a bedroom when asleep or resting, a workroom or office at work, otherwise the main room. Their cell is deterministic.
- They can be talked to when awake, through the Phase 12 conversation window.
- They are never duplicated: the settlement scene and the interior are never loaded together, and the settlement scene already hid people at home.
- Household chests are not the player's. Opening one is refused ("It belongs to the household"), because theft is not part of Phase 13.
- Entering a restricted room in someone's home is not yet trespass; only garrisons enforce restricted rooms.

## Phase 14: people move through their house, and followers keep a room (ADR-0070)

**Residents move.** Where a resident is is not a fixed room. `IndoorSchedule` (`Simulation/Places/IndoorSchedule.cs`) works it out from the person's daily plan and the minute:
- the room that suits what they are doing (bedroom to sleep, kitchen or hall to eat, the workroom to work);
- a free cell in it;
- the room they came from.

People move only along real passages (`PlaceRoutes`). Shut doors are opened as they pass. Residents hold the keys to their own house's locks and lift bars from the side they are on; nobody else passes a locked door. Secret ways are for the player once found, never for residents. A fallen bridge is no way for anyone, and a follower will not risk a damaged one.

**In the scene:**
- someone who has just changed rooms walks the route through the doorways (`PlaceWalker`, at 1.4 m/s);
- they never cross a wall.

**Followers** (a freed captive, an escort) have a room of their own:
- They follow the player through open ways.
- They wait at a locked or barred door or at a bridge they will not cross, and the HUD says why.
- They catch up when the way opens.
- Only those in the player's room join a fight there.
- Leaving a place takes only those with the player. The others stay where they wait.

Performance: an indoor schedule query takes 0.1 ms for a building with 24 people; a follower route takes 0.3–0.4 µs.
