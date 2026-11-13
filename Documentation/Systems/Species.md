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

## Phase 20: lineages and appearance (rules 9, ADR-0100)

| Species | Choice | Options (SRD 5.2.1) | Saves from before get |
|---|---|---|---|
| Tiefling | `species:fiendish_legacy` | Abyssal (poison resistance; poison spray), Chthonic (necrotic resistance; chill touch), Infernal (fire resistance; fire bolt) | Infernal |
| Goliath | `species:giant_ancestry` | Cloud's Jaunt, Fire's Burn, Frost's Chill, Hill's Tumble, Stone's Endurance, Storm's Thunder, each usable a number of times equal to the proficiency bonus per long rest | Stone |
| Gnome | `species:gnome_lineage` | Forest (minor illusion, speak with small beasts), Rock (tinker's tools; mending) | Rock |

Notes:
- **What is played:** see the Phase 20A section below (the goliath boons and the tiefling cantrips now are; the gnome's utility magic is not).
- **Other variants.** Dwarf is the SRD 5.1 Hill Dwarf and Halfling the Lightfoot; their other variants are not in the SRD. High and Wood Elf remain separate species.
- **Appearance.**
  - Every species except human shows the human model at the species' height (labelled "placeholder body").
  - Each person now has their own height (92–108 % of the species' typical height) and build.
  - Tieflings, orcs, half-orcs, goliaths, dragonborn and aasimar have their own skin tones (`PlayerAppearanceOptions.SkinTonesFor`).
  - Ears, tusks, horns, tails and scales are not modelled. See `Research/Phase20-Character-Assets-and-References.md`.

## Phase 20A: what each lineage benefit does in play (ADR-0103, ADR-0104)

Every trait and lineage option is shown as **active** (always in effect), **conditional** (played in its situation, which is named) or **not yet played** (with the missing system named). The creator and the sheet's Origins tab both read this from `BenefitStatus`.

| Benefit | State | How it is played |
|---|---|---|
| Tiefling legacy resistances (poison, necrotic, fire) | Active | Damage resistance |
| Tiefling legacy cantrip: Poison Spray, Chill Touch, Fire Bolt | Conditional | Cast in a fight as a cantrip, with Charisma (proficiency + Charisma to hit; DC 8 + both). A class that already readies the same spell casts it with its own ability. Chill Touch stops the target regaining hit points until the caster's next turn ends. |
| Tiefling level 3 and 5 legacy spells (SRD 5.2.1) | Not in the catalog | Need the spells and a once-per-long-rest innate casting path |
| Goliath ancestry uses | — | Proficiency bonus per long rest (`stones_endurance`); each boon below spends one |
| Cloud's Jaunt | Conditional | Bonus action: teleport to a free square within 30 ft in clear line (chosen on the combat grid) |
| Fire's Burn / Frost's Chill | Conditional | After a hit with an attack roll: +1d10 fire / +1d6 cold and −10 ft speed until the goliath's next turn. Once per hit; dice double on a critical. |
| Hill's Tumble | Conditional | After a hit: the target (Large or smaller, not already prone) falls prone |
| Stone's Endurance | Conditional (partial) | Reaction when an attack damages the goliath: −(1d12 + Con). Not yet offered against spell, breath or hazard damage. |
| Storm's Thunder | Conditional (partial) | Reaction when a creature within 60 ft damages the goliath with an attack: 1d8 thunder to it. Not yet offered against spell damage. |
| Gnome forest: Minor Illusion, Speak with Animals | Not yet played | No illusion or animal-speech system |
| Gnome rock: tinker's tools | Active | Tool proficiency (crafting) |
| Gnome rock: Mending | Not yet played | No object-repair system; the option is flagged as a whole |
| Dragonborn breath weapon | Not yet played | Unchanged since Phase 20 |

Computer-run goliaths (companions and people) take the same reactions through the same path. Companions also use the after-hit boons.

**Eye colour (Phase 20A).** Ten colours for the player (`PlayerAppearanceOptions.EyePalette`; 0 is the eye as drawn), shown in the creator's preview and on the body in the world. Only the iris is recoloured, through a mask derived from the CC0 eye texture. Generated residents keep brown.
