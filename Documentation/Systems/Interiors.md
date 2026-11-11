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
