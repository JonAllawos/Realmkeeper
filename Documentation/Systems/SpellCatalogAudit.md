# Spell catalog audit

Phase 9, rules version 8. The source is `Domain/Magic/SpellCatalog.cs`; `SpellCatalog.Validate()` is
run by the tests. Every spell's mechanics follow the SRD 5.2.1 (CC BY 4.0).

- **Names** are the SRD's.
- **Descriptions** are Realmkeeper's own short wording. No SRD spell text is copied.
- **Selection.** Spells were chosen to exercise the engine's mechanics, not to reach a count.
- **Left out.** Spells whose current rules could not be confirmed were left out rather than guessed.

## Status meanings

| Status | Meaning | Castable in a fight |
|---|---|---|
| Functional | Every mechanical effect is resolved | Yes |
| Partial | The core effect is resolved; the gap is in the note | Yes |
| DataOnly | Listed and selectable; it has no mechanical resolution (utility outside combat) | No (the reason is shown) |
| Deferred | Needs a system that does not exist yet (reaction prompt, summons, auras, resurrection) | No (the reason is shown) |

## Counts

| Level | Functional | Partial | DataOnly | Deferred | Total |
|---|---:|---:|---:|---:|---:|
| Cantrips | 6 | 1 | 3 | 0 | 10 |
| Level 1 | 12 | 4 | 1 | 2 | 19 |
| Level 2 | 5 | 2 | 0 | 1 | 8 |
| Level 3 | 2 | 2 | 0 | 3 | 7 |
| Level 4 | 1 | 0 | 0 | 0 | 1 |
| Level 5 | 1 | 1 | 0 | 0 | 2 |
| **All** | **27** | **10** | **4** | **6** | **47** |

By class list (a spell can be on several lists):

| Class | Cantrips | Levelled | Total |
|---|---:|---:|---:|
| Bard | 3 | 13 | 16 |
| Cleric | 3 | 16 | 19 |
| Druid | 2 | 10 | 12 |
| Paladin | 0 | 9 | 9 |
| Ranger | 0 | 5 | 5 |
| Sorcerer | 6 | 19 | 25 |
| Warlock | 3 | 8 | 11 |
| Wizard | 6 | 19 | 25 |

Barbarian, fighter, monk and rogue have no spellcasting. No subclass in the SRD set grants them a
spell list.

## The catalog

| Lvl | Spell | School | Classes | Resolution | Targeting | Conc. | Status | Note |
|---:|---|---|---|---|---|:-:|---|---|
| 0 | Fire Bolt | Evocation | sorcerer, wizard | Attack | Creatures |  | Functional |  |
| 0 | Ray of Frost | Evocation | sorcerer, wizard | Attack | Creatures |  | Functional |  |
| 0 | Shocking Grasp | Evocation | sorcerer, wizard | Attack | Creatures |  | Partial | The target cannot take any reaction (the rule names only opportunity attacks) until its next turn ends. |
| 0 | Sacred Flame | Evocation | cleric | Save | Creatures |  | Functional |  |
| 0 | Eldritch Blast | Evocation | warlock | Attack | Creatures |  | Functional |  |
| 0 | Vicious Mockery | Enchantment | bard | Save | Creatures |  | Functional |  |
| 0 | Poison Spray | Necromancy | druid, sorcerer, warlock, wizard | Attack | Creatures |  | Functional |  |
| 0 | Light | Evocation | bard, cleric, sorcerer, wizard | - | Creatures |  | DataOnly | Lighting is not simulated; the spell is listed for the spellbook only. |
| 0 | Mage Hand | Conjuration | bard, sorcerer, warlock, wizard | - | Point |  | DataOnly | Object interaction is not simulated. |
| 0 | Guidance | Divination | cleric, druid | - | Creatures | yes | DataOnly | Skill checks outside combat do not take spell bonuses yet. |
| 1 | Magic Missile | Evocation | sorcerer, wizard | Automatic | Creatures |  | Functional |  |
| 1 | Cure Wounds | Abjuration | bard, cleric, druid, paladin, ranger | Automatic | Creatures |  | Functional |  |
| 1 | Healing Word | Abjuration | bard, cleric, druid | Automatic | Creatures |  | Functional |  |
| 1 | Guiding Bolt | Evocation | cleric | Attack | Creatures |  | Functional |  |
| 1 | Burning Hands | Evocation | sorcerer, wizard | Save | FromSelf |  | Functional |  |
| 1 | Thunderwave | Evocation | bard, druid, sorcerer, wizard | Save | FromSelf |  | Functional |  |
| 1 | Shield of Faith | Abjuration | cleric, paladin | - | Creatures | yes | Functional |  |
| 1 | Bless | Enchantment | cleric, paladin | - | Creatures | yes | Functional |  |
| 1 | Bane | Enchantment | bard, cleric | Save | Creatures | yes | Functional |  |
| 1 | Mage Armour | Abjuration | sorcerer, wizard | - | Creatures |  | Partial | Lasts until the fight ends (out-of-combat durations are not tracked yet). |
| 1 | Faerie Fire | Evocation | bard, druid | Save | Point | yes | Partial | Outlined creatures are not revealed from invisibility (no stealth or sight model). |
| 1 | Inflict Wounds | Necromancy | cleric | Save | Creatures |  | Functional |  |
| 1 | Hunter's Mark | Divination | ranger | - | Creatures | yes | Partial | Moving the mark to a new target when the first falls, and the tracking advantage, are not modelled. |
| 1 | Divine Smite | Evocation | paladin | - | Creatures |  | Functional |  |
| 1 | Expeditious Retreat | Transmutation | sorcerer, warlock, wizard | - | Self | yes | Functional |  |
| 1 | Entangle | Conjuration | druid, ranger | Save | Point | yes | Partial | The square does not become difficult terrain. |
| 1 | Detect Magic | Divination | bard, cleric, druid, paladin, ranger, sorcerer, wizard | - | Self | yes | DataOnly | No magic items or auras exist in the world yet. |
| 1 | Hellish Rebuke | Evocation | warlock | Save | Creatures |  | Deferred | Reactions are taken automatically (ADR-0033); reaction spells wait for a reaction prompt. |
| 1 | Shield | Abjuration | sorcerer, wizard | - | Self |  | Deferred | Reactions are taken automatically (ADR-0033); reaction spells wait for a reaction prompt. |
| 2 | Scorching Ray | Evocation | sorcerer, wizard | Attack | Creatures |  | Functional |  |
| 2 | Shatter | Evocation | bard, sorcerer, warlock, wizard | Save | Point |  | Functional |  |
| 2 | Hold Person | Enchantment | bard, cleric, druid, sorcerer, warlock, wizard | Save | Creatures | yes | Functional |  |
| 2 | Misty Step | Conjuration | sorcerer, warlock, wizard | - | Point |  | Functional |  |
| 2 | Lesser Restoration | Abjuration | bard, cleric, druid, paladin, ranger | - | Creatures |  | Partial | Diseases are not simulated; the condition ended is the first in the listed order. |
| 2 | Invisibility | Illusion | bard, sorcerer, warlock, wizard | - | Creatures | yes | Functional |  |
| 2 | Aid | Abjuration | cleric, paladin | - | Creatures |  | Partial | Lasts until the fight ends (out-of-combat durations are not tracked yet); for the player, the current hit points gained stay. |
| 2 | Spiritual Weapon | Evocation | cleric | - | Point | yes | Deferred | Summoned entities are a hook for a later phase. |
| 3 | Fireball | Evocation | sorcerer, wizard | Save | Point |  | Functional |  |
| 3 | Lightning Bolt | Evocation | sorcerer, wizard | Save | FromSelf |  | Functional |  |
| 3 | Haste | Transmutation | sorcerer, wizard | - | Creatures | yes | Partial | The extra limited action each turn and the lethargy when the spell ends are not modelled. |
| 3 | Dispel Magic | Abjuration | bard, cleric, druid, paladin, sorcerer, warlock, wizard | - | Creatures |  | Partial | Spells above the slot level stay (the ability check to end them is not modelled). |
| 3 | Revivify | Necromancy | cleric, paladin | - | Creatures |  | Deferred | Resurrection is out of scope (ADR-0034). |
| 3 | Spirit Guardians | Conjuration | cleric | - | Self | yes | Deferred | Moving auras that trigger on entering an area are not modelled yet. |
| 3 | Counterspell | Abjuration | sorcerer, warlock, wizard | - | Creatures |  | Deferred | Reactions are taken automatically (ADR-0033); reaction spells wait for a reaction prompt. |
| 4 | Greater Invisibility | Illusion | bard, sorcerer, wizard | - | Creatures | yes | Functional |  |
| 5 | Cone of Cold | Evocation | sorcerer, wizard | Save | FromSelf |  | Functional |  |
| 5 | Mass Cure Wounds | Abjuration | bard, cleric, druid | - | Creatures |  | Partial | Targets are chosen creatures within range; the 30-foot grouping is not enforced. |

Areas in the catalog:
- **Cone:** Burning Hands (15 ft) and Cone of Cold (60 ft).
- **Cube:** Thunderwave (15 ft), Faerie Fire (20 ft) and Entangle (20 ft).
- **Sphere:** Shatter (10 ft), Spirit Guardians (15 ft, Deferred) and Fireball (20 ft).
- **Line:** Lightning Bolt (100 ft).

Cylinder areas are supported by the engine and tested, but no catalog spell uses one yet.

## Effects not supported (and what they wait for)

| Gap | Spells affected | Waits for |
|---|---|---|
| Reaction casting (reactions are automatic, ADR-0033) | Shield, Hellish Rebuke, Counterspell | A reaction prompt |
| Summoned entities | Spiritual Weapon | Summons as combatants |
| Moving auras triggered on entering | Spirit Guardians | Area triggers on movement |
| Resurrection (ADR-0034: nothing comes back) | Revivify | A design decision |
| Durations outside fights | Mage Armour, Aid (Partial) | Out-of-combat effect timing |
| Light, sight, stealth, objects | Light, Mage Hand, Detect Magic, Faerie Fire's reveal | Exploration models |
| Spell-made difficult terrain | Entangle (Partial) | Terrain effects |
| Skill checks taking spell bonuses outside combat | Guidance | Out-of-combat checks |
| The ability check to end higher-level spells | Dispel Magic (Partial) | — |
| Haste's extra limited action and lethargy | Haste (Partial) | A limited-action model |

## SRD 5.1 content

The Acolyte and the Cult Fanatic creature blocks, and their spellcasting, come from SRD 5.1
(CC BY 4.0): no SRD 5.2.1 equivalent was confirmed. Their spells are SRD 5.2.1 catalog entries.
Both SRD notices are in `CREDITS.md` and in the game's Info section.
