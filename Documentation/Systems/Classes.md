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

## Progression to level 20 (Phase 7)

Numbers come from the SRD 5.2.1 class tables (CC BY 4.0), verified for Phase 7; the wording is
Realmkeeper's own. "ASI" means a feat choice (Ability Score Improvement or another feat).

| Class | Subclass (level; feature levels) | Feat levels | Key progression | Resources and scaling |
|---|---|---|---|---|
| Barbarian | Path of the Berserker (3; 6, 10, 14) | 4, 8, 12, 16, 19 boon | Danger Sense (advantage on Dex saves, conditional), Reckless Attack 2; Primal Knowledge (skill) 3; Extra Attack, **Fast Movement (+10 ft)** 5; **Feral Instinct (advantage on initiative)** 7; Brutal Strike 9; Relentless Rage 11; Persistent Rage 15; Indomitable Might 18; **Primal Champion (+4 Str, Con, max 25)** 20 | Rage 2→6; rage damage +2/+3/+4; weapon masteries 2→4 |
| Bard | College of Lore (3; 6, 14) | 4, 8, 12, 16, 19 | Expertise ×2 and **Jack of All Trades** 2; Font of Inspiration (short-rest recovery) 5; Countercharm 7; Expertise ×2 9; Magical Secrets 10; Superior Inspiration 18; Words of Creation 20 | Bardic Inspiration (Cha); die d6→d12; full caster slots 1–9 |
| Cleric | Life Domain (3; 6, 17) | 4, 8, 12, 16, 19 | Channel Divinity 2; Sear Undead 5; Blessed Strikes (choice) 7; Divine Intervention 10; Improved Blessed Strikes 14; Greater Divine Intervention 20 | Channel Divinity 2/3/4; full caster |
| Druid | Circle of the Land (3; 6, 10, 14) | 4, 8, 12, 16, 19 | **Primal Order (Magician or Warden) at 1**; Wild Shape, Wild Companion 2; Wild Resurgence 5; Elemental Fury (choice) 7; Improved Elemental Fury 15; Beast Spells 18; Archdruid 20 | Wild Shape 2/3/4; full caster |
| Fighter | Champion (3; 7, 10, 15, 18) | **4, 6, 8, 12, 14, 16**, 19 | Action Surge, Tactical Mind 2; Extra Attack, Tactical Shift 5; Indomitable, Tactical Master 9; Two Extra Attacks 11; Studied Attacks 13; Three Extra Attacks 20 | Second Wind 2→4; Action Surge 1→2; Indomitable 1→3; masteries 3→6 |
| Monk | Warrior of the Open Hand (3; 6, 11, 17) | 4, 8, 12, 16, 19 | Monk's Focus, **Unarmoured Movement**, Uncanny Metabolism 2; Deflect Attacks 3; Slow Fall 4; Extra Attack, Stunning Strike 5; Evasion 7; **Disciplined Survivor (all saves)** 14; **Body and Mind (+4 Dex, Wis)** 20 | Focus = level; martial arts d6→d12; movement +10→+30 ft |
| Paladin | Oath of Devotion (3; 7, 15, 20) | 4, 8, 12, 16, 19 | Fighting Style feat, Paladin's Smite 2; Channel Divinity 3; Extra Attack, Faithful Steed 5; **Aura of Protection (Cha to saves)** 6; Aura of Courage 10; Radiant Strikes 11; Restoring Touch 14; Aura Expansion 18 | Lay on Hands 5×level; Channel Divinity 2/3; half caster (slots 1–5) |
| Ranger | Hunter (3; 7, 11, 15) | 4, 8, 12, 16, 19 | Deft Explorer (expertise, 2 languages), Fighting Style 2; Extra Attack 5; **Roving (+10 ft, climb, swim)** 6; Expertise ×2 9; Tireless 10; Nature's Veil 14; **Feral Senses (blindsight 30)** 18; Foe Slayer 20 | Favoured Enemy 2→6; half caster |
| Rogue | Thief (3; 9, 13, 17) | 4, 8, **10**, 12, 16, 19 | Cunning Action 2; Steady Aim 3; Cunning Strike, Uncanny Dodge 5; Expertise ×2 6; Evasion, **Reliable Talent** 7; Devious Strikes 14; **Slippery Mind (Wis, Cha saves)** 15; Elusive 18; Stroke of Luck 20 | Sneak Attack 1d6→10d6 |
| Sorcerer | Draconic Sorcery (3; 6, 14, 18) | 4, 8, 12, 16, 19 | Font of Magic, Metamagic ×2 2; Sorcerous Restoration 5; Sorcery Incarnate 7; Metamagic ×2 10 and 17; Arcane Apotheosis 20 | Sorcery points = level; full caster |
| Warlock | Fiend Patron (3; 6, 10, 14) | 4, 8, 12, 16, 19 | **Invocation choice at 1**; invocations to 10 by 18 (2, 5, 7, 9, 12, 15, 18); Magical Cunning 2; Contact Patron 9; Mystic Arcanum 11, 13, 15, 17; Eldritch Master 20 | Pact slots 1→4, slot level 1→5 (short rest) |
| Wizard | Evoker (3; 6, 10, 14) | 4, 8, 12, 16, 19 | **Ritual Adept at 1**; Scholar (expertise) 2; Memorize Spell 5; Spell Mastery 18; Signature Spells 20 | Arcane Recovery; full caster |

**Bold** marks features with non-combat mechanical effects that apply now, in the sheet or in
exploration. Everything else is acquired, validated and shown, and says what it waits for.

**Level-1 additions in rules v7:**
- Druid **Primal Order** and the warlock's level-1 **invocation**, as choices. Phase 6 characters
  make them at their next level-up.
- Rogue **Weapon Mastery** and wizard **Ritual Adept**, as features.
