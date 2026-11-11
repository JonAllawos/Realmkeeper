# Character creation

## Where and when

- **New worlds:** the player starts **Pending**.
- **Saves from before Phase 6:** the player starts **Legacy**.
- **How it is offered:** in a settlement, the HUD shows **Create character (C)**, and the Player Hub's
  Character section offers it too. Creation is offered, never forced: pending and legacy players can
  walk, trade and talk as before.
- **Once only:** after creation the prompt disappears and the creator cannot be opened again.

## The creator (`CharacterCreatorWindow`)

The creator has nine steps, shown as tabs that turn green when complete. Back and Next keep every
choice. Esc or "Later" closes the creator without applying anything.

1. **Identity.**
   - Name (required).
   - Optional title or epithet, age (16–90), description and personal history.
2. **Appearance.**
   - Frame, hair, beard, skin, hair colour and cloth tint: only what the art can render.
   - Beards are hidden for species that cannot have them.
3. **Species.**
   - Twelve species, with the source, the placeholder notice (◇), description, lore, size and speed,
     increases, languages and every trait.
   - Option choices, such as Draconic Ancestry, are made here.
4. **Background.** Description, lore, skills, tools, languages, kit and coin, the feature, and
   suggestions.
5. **Class.**
   - Description, identity, primary ability, hit die (and level-1 hit points), saves, armour,
     weapons, tools and skill options.
   - Spellcasting (ability, cantrips, slots, focus).
   - Level-1 features, class option choices (Fighting Style, Divine Order), and the subclass note.
6. **Abilities.**
   - Standard array (arranged for the class, ▲ ▼ to swap) or point buy (− +, with points left).
   - The base, species, final and modifier columns are shown; ★ marks the primary abilities.
7. **Skills.**
   - All 18 skills with their current bonus. Granted skills are marked with their source; class
     options can be chosen up to the class's number.
   - The human's Skillful choice and the rogue's Expertise are made here.
8. **Equipment.** One option per class group ("(worn)" marks pieces that will be put on), plus the
   background's kit and coin.
9. **Review.** Identity, look, abilities, hit points and its breakdown, saves, armour and weapons,
   skills with bonuses, tools, languages and the full starting equipment, followed by any remaining
   problems. **Create character** is enabled only when nothing is missing.

Beside every step is the **live 3D preview** (`CharacterPreview`, turn and zoom). It shows the
draft's look at the species' height, **wearing the chosen kit**, and a summary of name, species,
background, class, HP, speed, proficiency and scores. The preview rebuilds only when the look
changes.

## Rules in one place

The creator edits a `CharacterDraft` (Application layer, engine-free):
- **Keeping choices consistent:** a new class clears its skills, options and kit (each kit group
  starts on its first option); a background or species that grants a skill removes it from the picks.
- **Enforcing limits:** counts, lists, granted skills, expertise only in proficient skills, and the
  point-buy budget.
- **Explaining what is missing:** what each step still needs, in words.

The window only renders the draft. The finished choices become one `CreateCharacterCommand`.

## The transaction (`CreateCharacterCommand`)

- **Refused:** if the character already exists, or if anything is invalid. The command runs the same
  checks as the validator, plus identity and appearance. A refused command leaves the saved world
  byte-identical (tested for every kind of error).
- **Applied, all on one working copy that is validated whole before commit:**
  - name, age, description and appearance;
  - the sheet, with hit points and resources full;
  - the kit: added through the inventory rules, marked pieces equipped through the equip rules;
  - the background's coin;
  - a `PlayerCharacterCreated` chronicle entry, e.g. "Ilsa Marrow, a dwarf acolyte, set out as a
    cleric."
- **Kept:** identity, location, pose and everything already owned.
- **Never repeated:** `StartingEquipmentGranted` together with the Complete state stop creation and
  the kit from happening twice. Loading a save never grants anything.

## Legacy players

A Phase 5 player loads as Legacy:
- the same ID, name, look, belongings, equipped items, purse, place and pose;
- no invented species, class or history.

When they complete setup, they keep everything and receive the class and background kit once, like
a new character. This is reversible in the sense that nothing they had is lost; nothing is assigned
until they choose.
