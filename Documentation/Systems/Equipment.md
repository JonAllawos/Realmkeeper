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

## Phase 11: slots, condition and magic (ADR-0052)

**Slots.** There are now eleven: main hand, off hand, head, chest, hands, legs, feet, back, neck, and a ring on each hand.

- A ring goes on the free hand by default, or on the hand chosen ("Right hand").
- A Light weapon may be held in the off hand ("Off hand") while the main hand holds no two-handed weapon. That gives the SRD off-hand attack as a bonus action.
- The spear and quarterstaff are Versatile in SRD 5.2.1, so a shield now goes with them; only true two-handed weapons clear the off hand.

**The numbers are the rules'.** The Equipment section and the inventory header show the armour class breakdown and the main attack from `CombatStats`, the same calculation combat uses. Phase 5's "protection" and "damage" figures are no longer shown.

**Condition.** Each worn weapon, armour, shield and tool has its own condition:

| Condition | In play | Value |
|---|---|---|
| Pristine | — | 110 % |
| Good | — | 100 % |
| Worn | — | 75 % |
| Damaged | −1 to attack rolls (weapon) or −1 AC (armour, shield) | 50 % |
| Broken | The weapon cannot attack; armour or a shield gives no AC | 25 % |

A natural 1 on an attack wears the weapon one step. A critical hit taken wears the body armour, or the shield when none is worn. An armourer mends to Good for 15 % of base value per step. The player can mend with the craft's tools, as work (ADR-0054).

**Magic items** (11, SRD 5.2.1):

- +1 longsword, +1 dagger and +1 chain shirt;
- the +1 shield;
- the ring and the cloak of protection;
- the gauntlets of ogre power;
- goggles of night and boots of elvenkind;
- the wand of magic missiles;
- the cursed Berserker Axe.

Their bonuses and effects enter the rules as typed effects while worn, and while attuned when attunement is required.

- Attunement takes an hour of work (a short rest), up to three items.
- A curse binds the item when attuned. It cannot be unequipped, unattuned, sold or stored until a shrine's priest lifts the curse.
- The wand casts Magic Missile from its charges (one per spell level) in a fight, and regains 1d6 + 1 after a long rest.

**Ammunition.** Bows, crossbows and slings spend arrows, bolts or bullets from the pack. Without ammunition the attack is unavailable. Half of what was shot is gathered back when the fight ends.
