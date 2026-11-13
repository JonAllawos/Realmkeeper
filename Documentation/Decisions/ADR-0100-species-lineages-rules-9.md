# ADR-0100: Species lineages as choices (rules 9)

**Status.** Accepted, 2026-11-12 (Phase 20).

**Context.** SRD 5.2.1 gives tieflings a fiendish legacy (abyssal, chthonic, infernal), goliaths a giant ancestry (cloud, fire, frost, hill, stone, storm) and gnomes a lineage (forest, rock). Realmkeeper's catalog had one fixed variant of each: the SRD 5.1 tiefling (fire resistance), the stone goliath and the rock gnome.

**Decision.**
1. **The choices.** Each of the three becomes a species `ChoiceDefinition` of kind Option, introduced in rules version 9 (`CurrentRulesVersion` 8 → 9). Effects use the existing pipeline: resistance, tool proficiency, innate-magic notes, combat-rule notes. No new mechanics code was needed.
2. **What is not played yet** is flagged with `IntegrationNeed`, and the sheet says so:
   - **Magic:** innate spells.
   - **Combat:** the ancestry boons. Stone's Endurance was already only a note, so nothing is lost.
3. **The goliath resource** keeps its saved ID `stones_endurance` and is renamed "Giant Ancestry", so saved uses carry over.
4. **Saved characters keep what they had.** Migration v20 → v21 (ADR-0101) adds the matching choice: infernal, stone or rock.
5. **What is not added.**
   - **Subclasses:** the SRDs contain one per class, and all 12 are implemented. More would be Realmkeeper-original designs, which the brief rules out as inert additions.
   - **Mountain dwarf, stout halfling:** not in the SRD.
   - **Drow:** in SRD 5.2.1, but deferred until its appearance can be shown.

**Consequences.** New tieflings, goliaths and gnomes must choose a lineage, and the creator and its build panel say so. Generated people of those species get the first option deterministically, through the same draft. One existing test now chooses a goliath ancestry.

**Alternatives.** Separate species per lineage was rejected: it multiplies catalog entries for one difference.
