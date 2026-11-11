# Class features in play

Phase 9 (ADR-0041). Class features are already typed rules in the character record (Phase 7,
ADR-0027). Phase 9 gives the combat-relevant ones a way to act.

## How features act

`CombatFeatureCatalog` (`Domain/Combat/CombatFeatures.cs`) describes how each feature is used in a
fight:

- **Activation:** action, bonus action, free, after a hit, or automatic.
- **Targeting:** self, ally, enemy, the creature last hit, or either.
- **Cost:** the resource and cost.
- **Range and options:** for example, Font of Magic's slot levels.
- **Status:** Functional or Partial, with a note.

A feature is matched to a combatant **by the feature ID in its rules**. Anyone who has the feature
can use it: the player today, and companions later.

- **Active features** are `UseFeatureAction(featureId, target, option, amount)`, resolved by
  `CombatEngine.DoFeature`.
  - **Legality** (`FeatureProblem`): the turn, the action economy, the resource left, range, the
    target's side, and the feature's own rules. Each refusal says why.
  - **Offered:** next to the attacks, and in the panel's **Class features** section.
- **Automatic features** are applied by the shared attack, damage and save code wherever their rule
  applies (Sneak Attack, Evasion, auras …).
- **Resources** are spent from the character sheet (the player) or the combatant (creatures).
  - **Recovery:** each resource names its recovery: short rest, long rest, or a partial short-rest
    regain (see `RestAndRecovery.md`).
  - **Display:** the Hub's Spells & rest section lists each feature's combat status; the inspector
    shows what is left.

## Per-class audit

Status words:
- **Functional:** every mechanical effect is resolved.
- **Partial:** the core effect is resolved, and the gap is named.
- **Sheet only:** shown with its integration need, not yet acting in a fight.

Features of levels above those reachable through the progression are in the same state as their
row says.

### Barbarian (no spellcasting)

| Feature | Use | Status |
|---|---|---|
| Rage | Bonus action; `rage` uses | Partial: bonus damage on Strength attacks, B/P/S resistance and advantage on Strength saves all work, as does the "no spells or concentration" rule. Rage lasts the fight while kept up; the 10-minute limit and the Strength-check advantage are not modelled. One use returns on a short rest. |
| Keep raging | Bonus action | Functional |
| Reckless Attack | Free, before the first attack | Functional (advantage given and taken) |
| Danger Sense | Automatic | Functional (Dexterity saves, through roll modes) |
| Frenzy (Berserker) | Automatic while raging | Functional (extra damage once per turn) |
| Other features (Weapon Mastery handled below, Primal Knowledge, Brutal Strike …) | — | Sheet only |

### Bard (full caster, known spells, Charisma)

| Feature | Use | Status |
|---|---|---|
| Spellcasting | Cantrip and known-spell picks; slots | Functional |
| Bardic Inspiration | Bonus action on an ally; uses | Partial: the die is used automatically when it turns a failed attack or save into a success; ability checks do not use it. |
| Lore subclass (Cutting Words …) | Reaction | Sheet only (reactions are automatic, ADR-0033) |

### Cleric (full caster, prepared, Wisdom)

| Feature | Use | Status |
|---|---|---|
| Spellcasting | Cantrip picks; preparation from the class list | Functional |
| Channel Divinity: Divine Spark | Action; heals an ally or harms a foe (Con save) | Functional. One use returns on a short rest. |
| Channel Divinity: Preserve Life (Life) | Action; five times the cleric level in healing, shared among bloodied allies within 30 ft, no one above half their maximum | Functional |
| Disciple of Life (Life) | Automatic (+2 + spell level on healing spells) | Functional |
| Life Domain spells | Always prepared | Functional |

### Druid (full caster, prepared, Wisdom)

| Feature | Use | Status |
|---|---|---|
| Spellcasting | Cantrip picks; preparation | Functional |
| Wild Shape | Bonus action; `wild_shape` uses | Partial: one known form (the SRD wolf), with its own hit points, statistics and attacks. Spellcasting is barred while transformed, and you return to your own form at 0 or when you leave. Other forms and the form limits by level come later. One use returns on a short rest. |
| Leave Wild Shape | Bonus action | Functional |
| Land subclass | — | Sheet only |

### Fighter (no spellcasting)

| Feature | Use | Status |
|---|---|---|
| Fighting Style, Extra Attack, Second Wind | Phase 8 | Functional |
| Action Surge | Free; one extra action (not a Magic action) | Functional |
| Indomitable | Automatic reroll of a failed save | Partial: used on the first failed save while uses remain (the player does not choose). |
| Improved / Superior Critical (Champion) | Automatic | Functional (critical on 19–20, then 18–20) |
| Weapon Mastery (all martial classes that have it) | Automatic | Partial: Vex, Sap, Slow, Topple, Push and Graze work for every proficient weapon; the chosen-weapons limit, Nick and Cleave are not modelled. |

### Monk (no spellcasting)

| Feature | Use | Status |
|---|---|---|
| Martial Arts | Unarmed strike with the Martial Arts die and Dexterity; bonus-action strike | Functional |
| Flurry of Blows, Patient Defence, Step of the Wind | Bonus action; focus points | Functional |
| Stunning Strike | After a hit; Con save or stunned | Functional |
| Wholeness of Body (Open Hand) | Bonus action; a Martial Arts die + Wisdom in hit points | Functional |
| Evasion | Automatic (Dex save areas) | Functional |
| Deflect Attacks and other reactions | — | Sheet only (reactions are automatic) |

### Paladin (half caster, prepared with one swap, Charisma)

| Feature | Use | Status |
|---|---|---|
| Spellcasting | Preparation; one swap after a long rest | Functional |
| Paladin's Smite (Divine Smite) | Always prepared; cast with a slot as a bonus action right after a melee hit (2d8 radiant, +1d8 a slot level, +1d8 against fiends and undead) | Functional (the free use once per long rest is not offered yet) |
| Lay on Hands | Bonus action; choose the amount from the pool | Functional |
| Channel Divinity: Sacred Weapon (Devotion) | Free; adds Charisma to attacks | Partial: lasts the fight; the light is not modelled. |
| Aura of Protection | Automatic; adds Charisma to allies' saves within 10 ft | Functional |
| Devotion Oath spells | Always prepared | Functional |

### Ranger (half caster, prepared with one swap, Wisdom)

| Feature | Use | Status |
|---|---|---|
| Spellcasting | Preparation | Functional |
| Favoured Enemy | Hunter's Mark always prepared and cast free a number of times per long rest | Functional |
| Hunter's Mark | Concentration; +1d6 on hits against the mark | Partial: moving the mark when the target falls, and the tracking advantage, are not modelled. |
| Hunter subclass | — | Sheet only |

### Rogue (no spellcasting)

| Feature | Use | Status |
|---|---|---|
| Sneak Attack | Automatic, once per turn | Functional |
| Cunning Action: Dash, Disengage | Bonus action | Functional (Hide needs a stealth model) |
| Steady Aim | Bonus action | Functional |
| Uncanny Dodge | Automatic reaction | Functional |
| Evasion | Automatic | Functional |
| Thief subclass | — | Sheet only |

### Sorcerer (full caster, known spells, Charisma)

| Feature | Use | Status |
|---|---|---|
| Spellcasting | Cantrip and known-spell picks | Functional |
| Innate Sorcery | Bonus action; +1 DC, advantage on spell attacks | Partial: lasts the fight. |
| Font of Magic: create a slot | Bonus action; sorcery points to a slot | Partial: it never raises a slot above the slot maximum. |
| Font of Magic: convert a slot | Free; slot to points | Partial: as above. |
| Metamagic | In the cast composer | Partial: Careful, Distant, Empowered, Heightened, Quickened and Twinned are simulated; Extended, Seeking, Subtle and Transmuted are not. |
| Draconic subclass | — | Sheet only |

### Warlock (Pact Magic, known spells, Charisma)

| Feature | Use | Status |
|---|---|---|
| Pact Magic | Pact slots at one level; they recover on a short rest | Functional |
| Eldritch Invocations | Automatic | Partial: Agonizing Blast (now offered from level 2, once Eldritch Blast exists) and Armour of Shadows (Mage Armour at will) act. The others are listed on the sheet with what they wait for. |
| Dark One's Blessing (Fiend) | Automatic temporary hit points on a kill | Functional |
| Fiend Patron spells | Always prepared | Functional |

### Wizard (full caster, spellbook, Intelligence)

| Feature | Use | Status |
|---|---|---|
| Spellcasting | Cantrip picks; 6 spellbook entries, then 2 a level; preparation from the book | Functional |
| Arcane Recovery | After a short rest, once per long rest | Functional (`RestAndRecovery.md`) |
| Potent Cantrip (Evoker) | Automatic (half damage on a miss or a save) | Functional |
| Sculpt Spells (Evoker) | Automatic (allies in your evocation succeed and take nothing) | Functional |
| Empowered Evocation (Evoker) | Automatic (+Intelligence to one damage roll) | Functional |

## Not done here (on purpose)

- **Reaction features and spells.** Combat reactions are automatic (ADR-0033). A reaction prompt is
  future work.
- **Features outside combat.** These include exploration, social and crafting features. They stay
  on the sheet with their integration need.
- **No invented subclasses.** No subclass outside the SRD 5.2.1 set listed in the pre-flight was
  added.
