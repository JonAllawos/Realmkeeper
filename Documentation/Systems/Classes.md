# Classes

There are twelve classes with real level-one differences (`RpgCatalog.Classes`; mechanics from SRD
5.2.1, text Realmkeeper's own). A test asserts that no two classes share the same combination of hit
die, saves, armour, features and skill count.

| Class | Hit die | Primary | Saves | Armour | Skills | Level-1 features and resources | Casting |
|---|---|---|---|---|---|---|---|
| Barbarian | d12 | Str | Str, Con | Light, medium, shields | 2 | Rage (2/long rest); Unarmoured Defence (Con); Weapon Mastery 2 | — |
| Bard | d8 | Cha | Dex, Cha | Light | 3 (any) | Bardic Inspiration (Cha mod/long rest); lute | Cha · 2 cantrips · 2 slots |
| Cleric | d8 | Wis | Wis, Cha | Light, medium, shields | 2 | Divine Order: Protector (heavy armour, martial weapons) or Thaumaturge | Wis · 3 cantrips · 2 slots |
| Druid | d8 | Wis | Int, Wis | Light, shields | 2 | Druidic (language); herbalism kit | Wis · 2 cantrips · 2 slots |
| Fighter | d10 | Str/Dex | Str, Con | All, shields | 2 | Fighting Style (choose 1 of 5); Second Wind (2, short rest); Weapon Mastery 3 | — |
| Monk | d8 | Dex/Wis | Str, Dex | None | 2 | Unarmoured Defence (Wis); Martial Arts (d6) | — |
| Paladin | d10 | Str/Cha | Wis, Cha | All, shields | 2 | Lay on Hands (5 × level pool); Weapon Mastery 2 | Cha · 2 slots |
| Ranger | d10 | Dex/Wis | Str, Dex | Light, medium, shields | 3 | Favoured Enemy (2/long rest); Weapon Mastery 2 | Wis · 2 slots |
| Rogue | d8 | Dex | Dex, Int | Light | 4 | Expertise (choose 2); Sneak Attack 1d6; Thieves' Cant; thieves' tools | — |
| Sorcerer | d6 | Cha | Con, Cha | None | 2 | Innate Sorcery (2/long rest) | Cha · 4 cantrips · 2 slots |
| Warlock | d8 | Cha | Wis, Cha | Light | 2 | Eldritch Invocation (magic phase) | Cha · 2 cantrips · 1 pact slot (short rest) |
| Wizard | d6 | Int | Int, Wis | None | 2 | Arcane Recovery (1/day); spellbook | Int · 3 cantrips · 2 slots |

## Starting equipment

Each class has equipment groups. Exactly one option per group is chosen. All items are real
`ItemCatalog` items (Phase 6 added greataxe, rapier, shortsword, scimitar, light crossbow, javelins,
chain shirt, holy symbol, arcane focus, druidic focus, spellbook and the tools).

- Pieces marked to equip are put on through the normal equip rules; the clothes they replace go to
  the pack.
- The rest go in the pack, together with the background's kit and coin.
- It all happens once, inside the creation transaction.

## What the data holds for later phases

| Field | Holds |
|---|---|
| `Spellcasting` | Ability, style, cantrips, slots, focus and slot recovery. Spells themselves belong to the magic phase; spell save DC and attack are already derived. |
| `Resources` | Typed formulas (fixed, ability modifier, per level, proficiency bonus) and recovery. Current uses are saved. |
| `SubclassLevel`, `SubclassTitle` | When and what the subclass is (level 3, SRD 5.2.1). `SubclassId` stays unset. |
| `LaterFeatures` | A level → features map, empty in Phase 6, for the progression phase to fill without changing the character model. |
| `CombatRule` effects | Sneak Attack dice, rage damage, martial arts die, fighting styles, weapon masteries: data for the combat phase. |

**Not implemented in Phase 6:** levels 2–20, experience awards, subclasses, spells, combat use of any
feature.
