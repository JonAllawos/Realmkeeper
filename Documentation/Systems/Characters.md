# Characters

Phase 6 turns the persistent player (`PlayerCharacterData`, since Phase 3) into an RPG character. It
is still one entity: the RPG record is a sub-object, `PlayerCharacterData.Rpg` (`CharacterSheetData`).
There is no second player model (ADR-0020).

## What is stored and what is derived

**Stored** (schema v8):
- creation state;
- title and personal history;
- species, background and class IDs;
- subclass (unset);
- level and experience;
- the ability method and six base scores;
- creation choices (class skills, species and class options, expertise, starting-kit options);
- current and temporary hit points;
- resource uses;
- `StartingEquipmentGranted`;
- creation date.

**Derived, never stored** (`CharacterRules`):
- final scores and modifiers;
- proficiency bonus;
- every skill and saving-throw bonus, with a breakdown;
- maximum hit points;
- armour class;
- speed, carrying capacity, darkvision, resistances and save advantages;
- languages;
- tool, weapon and armour proficiencies;
- features;
- resource maximums;
- spell save DC and spell attack.

Identity, appearance, inventory, equipment, purse, hunger, location and pose stay where they were:
on `PlayerCharacterData`, and in the inventory and equipment systems.

## Creation states

| State | Meaning | Who has it |
|---|---|---|
| `Pending` | Not yet created; nothing assigned | New worlds' players |
| `Legacy` | Travelled before characters existed; nothing assigned or invented | Players from v7 and older saves (migration) |
| `Complete` | Created: species, background, class, scores and choices set; kit given once | After `CreateCharacterCommand` |

Pending and legacy characters play normally with their Phase 5 belongings. The HUD offers
"Create character (C)" and the Hub's Character section offers it too. The game never forces it.

## Typed effects (ADR-0021)

Species traits, background features, class features and choice options are lists of `RuleEffect`.
Each kind has one meaning and one handler in `CharacterRules`:

| Kind | Used for | Consumed now by |
|---|---|---|
| Skill, save, tool, weapon, armour proficiency | Proficiencies | Sheet; skill and save bonuses |
| Language | Languages | Sheet |
| Darkvision | Senses | Sheet |
| Resistance, SaveAdvantage | Damage and conditions | Sheet (combat phase) |
| SpeedBonus | Walking speed | Sheet; **walking speed in exploration** (species pace) |
| MaxHpPerLevel | Dwarven toughness | Maximum hit points |
| UnarmoredDefense | Barbarian, monk | Armour class |
| Resource | Rage, slots, Lay on Hands … | Sheet; current uses saved |
| CarryCapacityPercent | Goliath powerful build | **Encumbrance in exploration** |
| ShopPricePercent | Merchant's trade connections | **Shop prices** (a labelled factor) |
| InnateMagic, CombatRule | Spells, attack and damage rules | Kept as data for the magic and combat phases |
| SocialFeature | Background privileges | Kept as data for social and quest systems |

There is no per-species or per-class conditional anywhere. The validator checks every effect's key
against the catalogs.

## Validation (`CharacterValidation`, part of `WorldValidator`)

It runs on every command and every load. It rejects:
- unknown species, class or background;
- an impossible level, or negative experience;
- base scores invalid for the method;
- a final score above 20;
- class skills that are the wrong count, not on the class list, repeated, or already granted;
- missing or invalid species and class choices;
- expertise in an unproficient skill;
- a missing starting-equipment choice;
- unknown choice keys;
- hit points outside 0–maximum, or negative temporary hit points;
- unknown resources, or uses above their maximum;
- a created character without their kit;
- a character not yet created who has anything assigned or granted;
- species or background names that disagree with the IDs;
- a look the species cannot show (no beard on a dragonborn).

## Licensing and provenance

Rules mechanics follow the **System Reference Document 5.2.1** (and 5.1 where noted), both by Wizards
of the Coast LLC under **CC BY 4.0**. Every species, background and class records its source
(`ContentSource`). The breakdown:

| Content | Source |
|---|---|
| Species | Human, High Elf, Wood Elf, Orc, Goliath (SRD 5.2.1); Dwarf, Halfling, Gnome, Half-Orc, Tiefling, Dragonborn (SRD 5.1 values); **Aasimar: Realmkeeper original** (not in any SRD; the name is flagged for the owner's IP review) |
| Backgrounds | Acolyte, Criminal, Scholar (Sage), Soldier (SRD 5.2.1 proficiencies); the other eight are Realmkeeper originals |
| Classes | All twelve: level-one mechanics from SRD 5.2.1 |
| Text | All descriptions, lore, feature wording and kits are Realmkeeper's own; no published setting lore, spells, subclasses or art |

**Required attribution** (to appear in the game's credits and documentation):

> This work includes material from the System Reference Document 5.2.1 ("SRD 5.2.1") by Wizards of
> the Coast LLC, available at https://www.dndbeyond.com/srd. The SRD 5.2.1 is licensed under the
> Creative Commons Attribution 4.0 International License, available at
> https://creativecommons.org/licenses/by/4.0/legalcode.

The same notice, naming SRD 5.1, covers the SRD 5.1 values. The product does not use the
"Dungeons & Dragons" trademark.

## Effects on play today

- **Species speed:** walking speed in exploration is speed ÷ 30, so a dwarf walks at 83%.
- **Powerful build:** a goliath carries 50 kg before slowing.
- **Merchant background:** −5% when buying in shops and +5% when selling, shown as "Trade
  connections".
- **Height:** each species is shown at its height (placeholder bodies).
- **The sheet:** HP, AC, saves, skills, features, resources and proficiencies all appear in the Hub.

Combat, magic, levelling and quests will consume the rest.
