# Species

There are twelve species in a validated catalog (`RpgCatalog.Species`). Each has:

- a stable ID, name, description and lore (Realmkeeper's own text);
- a size, base speed, creature type and languages;
- ability increases;
- typed traits;
- choices;
- resources;
- an **appearance contract**;
- a content source.

Species never set class, alignment, personality, background or faction.

| Species | Size · speed | Increases | Key traits | Source |
|---|---|---|---|---|
| Human | Medium · 30 | +1 all | Skillful (choose a skill) | SRD 5.2.1 (5.1 increases) |
| High Elf | Medium · 30 | Dex +2, Int +1 | Darkvision 60; Perception; Fey Ancestry; Trance; an arcane cantrip (magic phase) | SRD 5.2.1 |
| Wood Elf | Medium · 30 (+5) | Dex +2, Wis +1 | Darkvision 60; Fleet of Foot; Perception; Fey Ancestry; Mask of the Wild | SRD 5.2.1 |
| Dwarf | Medium · 25 | Con +2, Wis +1 | Darkvision 60; poison resistance and save advantage; +1 HP per level; dwarven weapons, smith's tools | SRD 5.1 |
| Halfling | Small · 25 | Dex +2, Cha +1 | Lucky; Brave; Nimbleness | SRD 5.1 |
| Gnome | Small · 25 | Int +2, Con +1 | Darkvision 60; Gnomish Cunning; tinker's tools | SRD 5.1 |
| Orc | Medium · 30 | Str +2, Con +1 | Darkvision 120; Adrenaline Rush (proficiency-bonus uses); Relentless Endurance | SRD 5.2.1 (increases Realmkeeper's) |
| Half-Orc | Medium · 30 | Str +2, Con +1 | Darkvision 60; Intimidation; Relentless Endurance; Savage Attacks | SRD 5.1 |
| Tiefling | Medium · 30 | Cha +2, Int +1 | Darkvision 60; fire resistance; an infernal cantrip (magic phase) | SRD 5.1 |
| Dragonborn | Medium · 30 | Str +2, Cha +1 | Draconic Ancestry (choose fire, cold, lightning, acid or poison: matching resistance); Breath Weapon (1 per short rest) | SRD 5.1 |
| Aasimar | Medium · 30 | Cha +2, Wis +1 | Darkvision 60; radiant and necrotic resistance; Mending Touch (1 per long rest); Inner Light | **Realmkeeper original** (not in any SRD) |
| Goliath | Medium · 35 | Str +2, Con +1 | Powerful Build (carry ×2: real encumbrance effect); Stone's Endurance; Athletics (Realmkeeper addition) | SRD 5.2.1 (increases Realmkeeper's) |

## Appearance: honest placeholders

**Human** is the only species with finished art: the existing outfit bodies, heads and hair.

**Every other species** is offered with a **placeholder**: the human body at the species' height.
The scale comes from `SpeciesAppearance.HeightScale`:

| Species | Height |
|---|---|
| Dwarf | 0.80 |
| Halfling | 0.62 |
| Gnome | 0.60 |
| Goliath | 1.22 |

The creator, the review and the sheet label it plainly: "Placeholder body: the human model at this
species' height. The final model is pending." Nothing recolours a human and calls it a species.
Equipment attachment and the Hub preview work unchanged, because the skeleton is the same.

### The contract for the visual production phase

Each species declares:
- `BodySet` and `HeadSet` (the asset sets to build from; "Human" today);
- `HeightScale`;
- `AllowsBeard` (dragonborn: no);
- `Features`, the visual features its finished model needs.

| Species | Features needed |
|---|---|
| High Elf | Pointed ears, fine features |
| Wood Elf | Pointed ears, weathered skin tones |
| Dwarf | Broad stocky build, heavy brow |
| Halfling | Small frame, large bare feet |
| Gnome | Small frame, large nose and ears |
| Orc | Tusks, grey-green skin tones, heavy frame |
| Half-Orc | Small tusks, greyish skin tones |
| Tiefling | Horns, tail, red to violet skin tones, solid-colour eyes |
| Dragonborn | Scaled skin, draconic head, no hair |
| Aasimar | Luminous eyes, faint skin glow |
| Goliath | Very tall frame, stony skin patterning |

**Art requirement:** a rigged body and head per species on the existing humanoid skeleton (so
equipment and animation keep working), with matching outfit fits. No licensed asset was found or
added in Phase 6.
