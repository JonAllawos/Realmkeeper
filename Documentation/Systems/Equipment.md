# Equipment

## Slots and rules

There are eight slots:

| Slot | Takes |
|---|---|
| Main hand | Weapons (a two-handed weapon also takes the off hand) |
| Off hand | Shields |
| Head | Hood, felt hat, iron cap |
| Chest | Tunics, jerkins |
| Hands | Sleeves, bracers |
| Legs | Trousers, leggings |
| Feet | Shoes, boots |
| Back | Pauldrons |

`EquipItemCommand(stack)`:

- An item goes only in its own slot (`ItemDefinition.Slot`); anything else is refused.
- Whatever was in that slot goes back to the pack.
- A two-handed weapon also clears the off hand.
- A shield puts away a two-handed weapon.
- From a stack of several, one unit is equipped and the rest stay packed.

`UnequipItemCommand(slot)` returns the item to the pack. It needs a free stack slot.

The validator rejects:

- an item in the wrong slot;
- more than one unit in a slot;
- a two-handed weapon with anything in the off hand.

## What equipment does now

Equipment has these effects (Phase 8 added combat):

- **Armour and weapons follow the SRD** (Phase 8, ADR-0033). Worn armour sets the armour class by its category; a shield adds 2 if trained; weapons give their SRD damage, properties and ranges in combat. See `Systems/Combat.md` for the item-to-SRD table. The older Protection and Damage numbers still drive prices and the Hub's equipment summary.
- **Protection** of everything worn and the **damage and class** of the wielded weapon are computed
  and shown in the Hub. Bare hands count as damage 1.
- **Weight** counts toward encumbrance and slows the player.
- **Worth** counts toward possessions; it can be sold back to a shop.
- It **changes the body** in the street and in the Hub preview (below).

## How it is shown (ADR-0017)

`PlayerBodyBuilder` builds the body from `PlayerLook`: appearance plus the item in each slot.

**Clothing.** The CC0 outfit models (peasant, ranger) each hold their parts as separate meshes on
the shared skeleton: body, arms, legs, feet, hood and pauldrons.

- Each worn item names its outfit set, part and cloth variant, e.g.
  `outfit:Ranger:Body:Ranger3`.
- The base body is the outfit of the chest item.
- A part from the other outfit is moved onto the skeleton (bones matched by name).
- An empty clothing slot shows plain undyed underclothes. The kit has no bare body fitted to the
  outfits.
- A hood hides the hair.

**Props.** The packs have no weapons, shields or hats, so these are procedural models
(`ItemModels`) built from the kit's materials. The same model is used on the shelf and in the hand.

| Item | Where it goes |
|---|---|
| Hats and caps | The head bone |
| One-handed weapons | The right hand, blade forward |
| Shields | The back of the left forearm |
| Two-handed spear, staff and bow | Slung across the back (there is no combat stance yet) |

The body is rebuilt whenever the look's signature changes: equip, unequip, appearance, or a
purchase that is then worn. A PlayMode test checks each case:

- the knife appears in `hand_r`;
- taking off the tunic shows underclothes;
- the female frame rebuilds.
