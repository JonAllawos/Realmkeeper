# Abilities, skills and saving throws

These are the rules configuration, compatible with fifth edition (SRD 5.2.1, CC BY 4.0), in
`AbilityRules` and `SkillRules`.

## Abilities

There are six abilities: Strength, Dexterity, Constitution, Intelligence, Wisdom and Charisma.

- **Modifier:** `floor((score − 10) / 2)`. It is always computed and never stored.
- **Final score:** base + species increase, at most 20. Level-1 characters are at most 17 (15 + 2).
- **Species increases:** this configuration gives ability increases to the species, as SRD 5.1 does.
  For example, a human gets +1 to all, and a dwarf +2 Constitution and +1 Wisdom. SRD 5.2.1 gives
  them to backgrounds instead; that is recorded as an alternative configuration, not implemented.

### Assignment at creation

| Method | Rule |
|---|---|
| Standard array | 15, 14, 13, 12, 10 and 8, each exactly once. The creator arranges them for the class's primary abilities, and ▲ ▼ swaps neighbours. |
| Point buy | All scores start at 8; the range is 8–15 and the budget is 27 points. |

Point-buy cost per score:

| Score | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|
| Cost | 0 | 1 | 2 | 3 | 4 | 5 | 7 | 9 |

There is no free entry and no rolling. Both are rejected by the command and the validator, not only
by the UI.

## Proficiency bonus

+2 at levels 1–4, +3 at 5–8, +4 at 9–12, +5 at 13–16, +6 at 17–20: `2 + (level − 1) / 4`.
Phase 6 characters are level 1.

## Skills

There are 18 skills. Each uses one ability:

| Ability | Skills |
|---|---|
| Strength | Athletics |
| Dexterity | Acrobatics, Sleight of Hand, Stealth |
| Intelligence | Arcana, History, Investigation, Nature, Religion |
| Wisdom | Animal Handling, Insight, Medicine, Perception, Survival |
| Charisma | Deception, Intimidation, Performance, Persuasion |

**Proficiency ranks:** none, proficient (+ the bonus) and expertise (+ twice the bonus). Expertise is
granted today by the rogue's level-1 feature and can be used by later features.

**Sources of proficiency:**
- the background's two skills;
- species traits (Perception for elves, Intimidation for half-orcs, Athletics for goliaths);
- the human's Skillful choice;
- the class's choices: its number, from its list, not repeating anything already granted.

**The skill bonus** is the ability modifier plus the proficiency (or twice it). The sheet shows the
breakdown and every source, e.g. "Wisdom +3, proficiency +2 · from Cleric (chosen)".

**Passive Perception** is 10 + the Perception bonus.

## Saving throws

Each class is proficient in two saves. The save bonus is the ability modifier plus proficiency if
proficient. Species may grant advantage against conditions: charmed (elves), poisoned (dwarves),
frightened (halflings), and magic aimed at the mind (gnomes). These are shown on the sheet and kept
for the combat phase.

## Hit points

- **Level 1:** the class hit die's maximum + the Constitution modifier.
- **Later levels:** the average (die ÷ 2 + 1) + Constitution, for the progression phase.
- **Bonuses:** plus per-level bonuses (dwarven toughness +1).
- **Floor:** at least 1 per level.

For example, a dwarf cleric with Constitution 16 has 8 + 3 + 1 = 12.

Current and temporary hit points are stored. The maximum is derived.

## Armour class

This rule is provisional until combat defines armour categories:

- **Base:** 10 + Dexterity.
- **No body armour:** the best unarmoured defence applies instead. A barbarian adds Constitution; a
  monk adds Wisdom, and only without a shield.
- **Worn items:** plus the protection of everything worn.

The sheet states that the rule is provisional.
