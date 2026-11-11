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

## Level-up (Phase 7)

- **When it is offered.** When a created character's experience allows another level:
  - the HUD shows **Level up (L)**;
  - the Hub's sheet shows a banner under the experience bar.

  Nothing happens until the player opens it, and Esc or "Later" closes it with nothing applied.
- **The window (`CharacterAdvancementWindow`).** It has the same layout as the creator.
  1. **Overview:**
     - the level, hit points (before → after, and the gain);
     - proficiency and hit dice;
     - new features, each with what it waits for;
     - resources and class-table columns that change;
     - any open choices from earlier rules versions;
     - the choices ahead.
  2. **One page per choice:**
     - the subclass, with its features by level;
     - a feat (category, prerequisites, repeatable), followed by the feat's own pages (ability
       points with caps, skills, the spell list);
     - expertise, skills, languages, invocations, Metamagic and class options.

     Options that cannot be taken are disabled and say why: "needs level 5+", "needs the pact of
     the blade invocation", "would pass 20", "already taken".
  3. **Review:** everything that will change, and what is still missing.
- **Navigation.** Back and Next keep every pick. Changing a feat drops its old feat's pages.
  **Confirm level N** is enabled only when the whole level-up is valid.
- **After confirming.** The command is dispatched once.
  - If the experience allows another level, the window reopens for it, and each level's choices
    are separate.
  - Otherwise the Hub opens on **Character › Progression**.
- **The logic.** It lives in `AdvancementDraft` (Application, engine-free, tested in .NET). The
  window only renders it.

The Hub's Character section keeps its structure, with one new panel, **Progression**:
- the experience bar;
- each level gained, with its summary;
- the experience ledger (newest first);
- the next four levels' features;
- any open choices.

The other panels now show initiative, speed, passive scores, roll modes, senses, movement,
fighting styles, class-table columns, resource recovery, spell slots and prepared spells, and each
score's breakdown.
