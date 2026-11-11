# Places (explorable locations)

Phase 13. Decisions: ADR-0062 (places as persistent world entities), ADR-0065 (discovery), ADR-0066 (people in places).

## What a place is

A *place* is any explorable location beyond a settlement's streets: a mine, a crypt, a cave, a ruin, a fort, or the inside of a building. Places are called *places* in code (`PlaceData`, `PlaceId`) because *location* already names the Phase 4 places inside a settlement that NPC schedules point at.

| Part | Where | What it holds |
|---|---|---|
| Definition | `PlaceCatalog` | Name, description, archetype, an authored layout (or none), story features |
| Archetype | `ArchetypeCatalog` (10) | Floors, rooms, sizes, light, doors and locks, secrets, material, inhabitants, traps, hazards, containers, loot, clues |
| Instance | `WorldData.Places` | Identity, parent, map cell, seed, generator version, saved layout, what the player knows, owner faction, threat, and every change made to it |
| Layout | `PlaceLayout` | Floors; rooms (cell rectangles); connections (openings, doors, stairs, ladders, secret ways, entrances); features; inhabitant groups |

## The hierarchy

```
World
 └ Region
    ├ Wilderness place (mine, crypt, fort, cave, ruin)   ParentKind Region
    │   └ Floor → Room → Feature (a chest is a Phase 11 container)
    └ Settlement
       └ Building (Phase 4 location: "house-3", "granary")
          └ Interior place                              ParentKind Building, LocationId = the building's location
             └ Floor → Room → Feature
```

## Coordinates

- One cell is 1.5 m, which is one combat square. A room is a rectangle of cells on a floor.
- A floor has an elevation: negative below ground, positive above.
- Inside a place, the player's pose is in the place's own space: `x = (cell + 0.5) × 1.5`.

## The player in a place

- `CharacterLocationKind.Place`, with `PlaceId`, `PlaceFloor` and `PlaceRoom`.
- In an interior, `SettlementId` is the building's settlement. In a wilderness place it is empty, and the region is the place's.
- Commands:

| Command | What it does |
|---|---|
| `EnterPlaceCommand(place, entrance, approach)` | From the map (wilderness, Discovered or beyond) or from the settlement. A hidden entrance needs to be known; a locked one needs Pick or Force. |
| `EnterBuildingCommand(layoutKey)` | From the street, through the door. The interior is planned on first entry. |
| `MoveInPlaceCommand(room, pose)` | Into a room joined to the current one by a passable connection. Explores the room (see below). |
| `TraversePlaceCommand(connection)` | Up or down stairs or a ladder from the room at either end. |
| `ExitPlaceCommand()` | From a room with a way in. An interior puts the character outside its door (`ExitPose`); a wilderness place puts them on the map. |
| `PlaceActionCommand(target, action)` | See `Interactions.md`. |
| `SpringTrapCommand(feature)` | Sent by the scene when the character steps on an armed trap's cell. |

## Arriving in a room (`PlaceRules.Arrive`)

1. The room is marked explored, and the events `RoomExplored` and `FloorReached` fire.
2. Passive Perception (10 + Perception, −5 in an unlit dark room without a light or darkvision) finds hidden features, traps and secret ways whose DC it meets.
3. The room's hazard is met, once per visit.
4. Trespass is checked: a guard group in sight of a restricted room, or of anywhere inside without leave. It costs −10 faction standing (once per room and visit), adds a Chronicle entry and starts a fight with that group.
5. Lair and ambush groups in the room start a fight.
6. Discovery rises to Explored when every room that is not secret has been seen, and to Cleared when no hostile inhabitants remain (a garrison holds its fort).

## What the player knows (ADR-0065)

`Unknown → Rumored → Discovered → Visited → Explored → Cleared`. It only rises.

| State | How it is reached | What is shown |
|---|---|---|
| Rumored | A resident of the nearest settlement asked about rumours | Map list and journal: name and rough direction, no marker |
| Discovered | Asked again; a quest giver (`RevealPlace`); a document | Marker at its cell; its known ways in |
| Visited | Entering | Threat in words; visits |
| Explored / Cleared | Rooms seen / inhabitants defeated | The marker turns green when cleared |

- The scene view (`GetPlaceScene`) shows secret rooms and passages only once found.
- Hidden features are not drawn, although an armed hidden trap still goes off when stepped on.
- The place map (N) shows explored rooms only.

## People (ADR-0066)

- **Escorts** (`WorldData.Escorts`): Captive → Following → Arrived, or Waiting, or Lost.
  - While captive, following or waiting, a person is absent from their schedule and their home settlement.
  - Followers come through every transition. At most three follow at once.
  - Entering the follower's destination settlement brings them home and emits `Escorted`.
- **Garrisons**: guards are persistent people of the holding faction. While their group stands they are posted at the place. Guard groups hold doors (`GuardedBy`).
- **Interior occupants**: the people whose current whereabouts is the building, placed by what they are doing.

## The world's places

`PlaceGenesis` (once, when the living world is set up or an older save is loaded) puts five places on land near settlements, off the roads, at least 6 cells apart:

| Place | Archetype | Notes |
|---|---|---|
| The Hollow Seam | mine (2 levels) | The foreman's ledger (why it was abandoned), a collapsed gallery, the den; a resident held there; the quest "Lost Below" |
| The Fordkeepers' Crypt | crypt | An altar, a loose panel, tombs, traps, a secret room |
| Watch Fort | keep (authored) | Gate held by the watch; barracks; barred armoury with the captain's key; side door; the captain's quarters upstairs |
| Bramble Cave | cave | A den, rats, deep water or darkness |
| The Broken Hall | ruin | Goblin squatters, a trap, a secret room |

Interiors are made when first entered. Their number grows only with the buildings the player goes into.
