# Realmkeeper ruleset baseline

**Status:** baseline written in Phase 22 (2026-11-13), as amendment A3 of the Game Design Bible asks. The full ruleset document, and its name (owner decision D3), are Phase 30.

Realmkeeper draws on D&D through the System Reference Documents (SRD 5.2.1, and SRD 5.1 where marked), both CC BY 4.0. It is not required to reproduce 5e exactly. Every catalog entry records its source in code (`ContentSource`: `Srd521`, `Srd51` or `RealmkeeperOriginal`), and the creator shows it. A name in a catalog is not a claim that the thing is played: each trait and feature also carries how the game plays it (active, conditional, or not yet played, ADR-0103).

**The Tavern Table** (the owner's separate application) is a design reference only. None of its code, assets or data is in Realmkeeper.

## Classes of rule

- **Implemented: SRD 5.2.1 as-is.** The rule is SRD 5.2.1's, and the game plays it.
- **Implemented: adapted.** From the SRD, changed in a stated way.
- **Realmkeeper-specific.** Written for Realmkeeper and labelled as original.
- **Incomplete.** In the data and shown, but not (or only partly) played; the missing system is named.
- **Planned.** A later phase.
- **Unsupported.** Deliberately not offered.

## Rule areas at the end of Phase 22

| Area | Class | Detail |
|---|---|---|
| **Classes (12)** | Implemented: SRD 5.2.1 as-is | Levels 1–20, features by level, class tables, spell slots and Pact Magic, resources |
| **Subclasses** | Implemented, limited | One SRD subclass per class; further subclasses are deferred (Phase 30). The level-up says so |
| **Species** | Mixed | SRD 5.2.1: human, high and wood elf, orc, goliath. SRD 5.1: dwarf, halfling, gnome, half-orc, tiefling, dragonborn. Realmkeeper-specific: aasimar. Tiefling, goliath and gnome use the SRD 5.2.1 lineages (Phase 20) |
| **Ability increases at creation** | Adapted | Species give ability increases (SRD 5.1 style), where SRD 5.2.1 ties them to the background. This is Realmkeeper's rule today |
| **Backgrounds (12)** | Mixed | SRD 5.2.1: soldier, criminal, acolyte, scholar. Realmkeeper-specific: noble, artisan, merchant, outlander, sailor, entertainer, hermit, folk hero. Each gives skills, tools, a feature, a kit and coin |
| **Level 1 start** | Realmkeeper-specific baseline | Every new character is exactly level 1, with 0 experience (no experience before creation, D4), checked by `LevelOneBaseline` (ADR-0109) |
| **Experience and levels** | Implemented: SRD 5.2.1 thresholds | A keyed ledger; experience makes a character eligible and never levels them. The player confirms each level with its choices (ADR-0028, ADR-0109) |
| **Hit points after level 1** | Implemented: SRD 5.2.1 option | The fixed value (hit die ÷ 2 + 1) per level, one of the SRD's two options; Realmkeeper does not roll |
| **Feats** | Implemented: SRD 5.2.1 | Origin, general, fighting style and epic boon categories, with prerequisites |
| **Spells** | Implemented, with notes | SRD 5.2.1 spells, each with an implementation status; partly simulated spells say so (`Systems/SpellCatalogAudit.md`) |
| **Tiefling legacy** | Mixed | Level 1: resistance and cantrip played. Spellcasting ability chosen (rules 10). Thaumaturgy is incomplete. Level 3 and 5 legacy spells are locked until their levels, then incomplete (no once-per-long-rest innate casting path) |
| **Goliath ancestry** | Implemented: SRD 5.2.1 | All six. Stone's Endurance and Storm's Thunder answer spell damage too (Phase 22). Hazards outside a fight offer no reaction |
| **Gnome lineage** | Incomplete | Minor Illusion, Speak with Animals, Mending and Prestidigitation have no systems; marked. Tinker's tools work |
| **Dragonborn breath** | Incomplete | SRD 5.1 data (2d6, once per short rest), not played in combat; needs a non-spell area action (deferred, Phase 22 report) |
| **Combat** | Implemented: SRD 5.2.1, adapted | On the grid; conditions, reactions, death saves. Adaptations are listed in `Systems/Combat.md` |
| **Equipment** | Implemented, adapted | SRD items plus Realmkeeper items, with condition and repair (ADR-0050) |
| **Crafting, recipes, work stations** | Realmkeeper-specific | ADR-0107 |
| **Weather and seasons** | Realmkeeper-specific | ADR-0105 |
| **Multiclassing** | Unsupported | Not offered |
| **NPC construction** | Implemented: same rules | NPCs are built through the same creation draft and advancement (ADR-0043) |
