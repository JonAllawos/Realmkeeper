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

## Phase 14: the five unused archetypes, waking, bridges and fire (ADR-0071)

**The new places.** Phase 14 gives the five archetypes Phase 13 defined but never placed their own places:

| Place | Archetype | Its story |
|---|---|---|
| The Ashgrave Catacombs | `catacomb` | A sealed vault whose sleepers wake when it is opened, and a founder's register |
| The Lantern Fane | `temple` | A shrine with a burning room, and a lantern to return |
| Crowfoot Den | `bandit_hideout` | Outlaws who tax the trade road, and a strongbox |
| The Old Culverts | `underground` | A damaged bridge, deep channels and a sluice wheel that drains them |
| Thornwick | `abandoned_settlement` | A hamlet empty since the floods: shut houses, yards under the open sky, wolves at dusk |

**Placement.**
- Each world gets three to five of them, chosen by its own context. The catacombs need a village or town, the fane a faction-held settlement, the den a trade route (or two settlements), and the culverts a town or city; the hamlet fits anywhere. A place that cannot be sited gives way to the next eligible one. The same seed always makes the same choice.
- `PlaceGenesis.EnsurePhase14` adds them once to new worlds and to old saves alike. A Phase 13 save keeps every place it had, unchanged.
- Each place brings its quest: The Founders' Register, The Lantern of the Fane, Tolls on the Road or The Old Culverts.

**Waking.** A group may sleep beside a story feature (`GroupTemplate.WakesOn`).
- Opening, taking or using that feature wakes them.
- The fight starts only if the player is in that room or can see it through an open way. Otherwise the group is marked awake and fights when the player comes in.
- It happens once. The creatures are keyed, so going back in never wakes or spawns them again.

**Bridges** have a state:
- **Intact:** free to cross.
- **Damaged:** an Acrobatics check to cross. A failure drops the player into the water (harm through the shared rule) and brings the bridge down for good.
- **Destroyed:** no way for anyone, including followers and residents.

Bridges sit on loops, or on the passage to a side room that holds nothing a quest needs. The place map draws each bridge in its state.

**Fire.** A burning room is a hazard: a Dexterity save against fire damage.
- It burns for four hours from when it was first found, then stays burnt out. There is no spreading and no ticking: its state is the minute recorded in the place's variables.
- A torch or lantern burns away marked webbing, once.
- The scene shows firelight while the room burns. The place map marks the burning room.

**Map states.** The map's list of places and the journal's Places tab say how each visited place stands now:
- a damaged or fallen bridge;
- drained;
- on fire, or burnt out in places;
- its sleepers awake;
- some have left.

"Cleared" was already shown by the Phase 13 discovery state, so it is not repeated.

## Phase 18: places in every region, puzzles and discoveries (ADR-0088, ADR-0089)

- **Four more places, on the existing archetypes:**
  - Greywater Drift (a mine);
  - the Ringers' Vault (a crypt);
  - Hollin Tower (a ruin);
  - Cairn Watch (a keep, only where two factions exist).

  Every region gets its own set, sited deterministically from the world seed and the region. Their IDs carry the region (`place:{def}@{region}`). Old saves gain them on load, once per region.
- **A finishing step.** `PlaceDefinition.Finish` wires each mechanism to what it works, after generation. A layout whose puzzle cannot be solved (its workings behind the door they open) is rejected, and the generator tries again. Tested on 60 seeds per place.
- **Three mechanism patterns** (`PlaceActions.Mechanisms.cs`). State lives in the place's saved `Variables` and is never reset.
  - **Sequence:** bells in a verse's order. A wrong bell costs a save and resets the sequence; a second wrong peal wakes the dead. The door can still be picked or forced at DC 22.
  - **Staged water:** clear the intake (Athletics), then pump FULL → HALF → DRY. A choked intake cracks the barrel until it is mended. The gallery can still be swum.
  - **Raking light:** a flame held close reads a scored lintel and reveals the hidden way. A thorough search can find it too.

  New place actions: Clear (20), Illuminate (21), Mend (22).
- **Discoveries.** Every clue with an insight records a discovery with a kind and a certainty. A guess is confirmed by its proof, including `group:{name}@{place}` for creatures seen in a fight. Major findings give 25 XP once.
- **Followers** refuse undrained deep water and say why.
- **Older places.** Bramble Cave and the Broken Hall gain story features in worlds made from now on. Saved layouts are never changed.
