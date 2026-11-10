# The player

The player character has been a persistent domain entity since Phase 3: `PlayerCharacterData`, one
per world, with its own ID, name, location and pose (ADR-0009). Phase 5 completes it (ADR-0015).

## What the player is (schema v7)

| Field | Meaning | Default for new worlds and older saves |
|---|---|---|
| `CharacterId` | Stable identity (Phase 3) | unchanged |
| `Name` | 1–64 characters | unchanged |
| `Species` | "Human" (the art has only human bodies) | Human |
| `Age` | 16–90 | 28 |
| `Background` | Short text, 1–40 characters | Wanderer |
| `Description` | Free text, up to 400 characters | empty |
| `Appearance` | Frame, hair, beard, skin tone, hair colour, cloth tint | male frame, parted hair, beard, the Phase 4 player's colours |
| `Purse` | Coin carried; never negative | 150 |
| `Fed` | 0–100; falls 2 per hour; food raises it | 80 |
| `Inventory` | Item stacks carried (at most 40 stacks) | knife, 3 loaves |
| `Equipment` | One item per slot | linen tunic, sleeves, wool trousers, simple shoes |
| `IsOutfitted` | The starting kit has been given (once only) | true after genesis or migration |

### The starting kit

The kit is the clothes the Phase 4 body already showed, a knife and some bread. It is enough to
walk into a shop and buy something.

- A save older than v7 receives it once, on load.
- A save that has it never receives it again: the `IsOutfitted` flag is checked.

## Appearance limited to the art

`PlayerAppearanceOptions` lists what the character art can actually draw:

- two frames;
- per frame, three hair parts or none;
- for the male frame, one beard or none;
- 5 skin tones, 6 hair colours and 4 cloth tints (the same palettes residents use).

The command, the validator and the Hub offer only these. Height and other options the kit cannot
show are deliberately absent.

## Commands

All of these are player-sourced, validated and atomic.

| Command | Effect |
|---|---|
| `SetPlayerProfileCommand(name?, age?, background?, description?)` | Edits identity; null fields are kept |
| `SetPlayerAppearanceCommand(appearance)` | Only renderable choices are accepted |
| Inventory and equipment commands | See `Inventory.md` and `Equipment.md` |
| `BuyItemCommand`, `SellItemCommand` | See `Commerce.md` |

## Condition and movement

`PlayerMovement.SpeedFactor` derives walking speed from world state:

- **Carried weight** (worn and packed): above 25 kg, speed falls linearly to 55% at 50 kg.
- **Hunger:** below fed 25, speed is ×0.85.

The Hub shows the factor and its reason ("Slowed: carrying 31 kg, hungry"). The exploration body
applies it.

## The Player Hub

Press **H** in a settlement, or click "Hub". The Hub has eight sections:

| Section | Shows | Actions |
|---|---|---|
| Character | Identity, condition, pace, arms | Edit and save the profile |
| Appearance | Frame, hair, beard, swatches | Changes apply at once |
| Inventory | Every stack, weight, description | Equip, Eat, Split, Combine, Drop |
| Equipment | All eight slots | Unequip |
| Weapons | Weapons and shields, worn and carried, with class, damage, two-handedness | Equip, Unequip |
| Clothing & armour | Clothing and armour, with protection and weight | Equip, Unequip |
| Currency & possessions | Purse, worth of possessions, carried weight, recent trades | — |
| Info | Time; who knows the player and why (talks, trades); how regard grows; controls | — |

- Beside the sections is a **live 3D preview** of the character (`CharacterPreview`): the same body
  builder as the street, on its own lit stage and layer, filmed into a render texture. Drag to turn
  it; use the wheel to zoom.
- Every button dispatches a command. The Hub then refreshes from the result, and the message
  (success or the reason for refusal) is shown.
- Selling happens only at a shop's counter.
