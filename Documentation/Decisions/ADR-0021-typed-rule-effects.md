# ADR-0021: Species, background and class features are typed effects

**Status.** Accepted, 2026-11-10 (Phase 6).

**Context.**
- Twelve species, twelve backgrounds and twelve classes each grant proficiencies, senses,
  resistances, resources and special rules.
- Hard-coded `if (species == "dwarf")` branches would scatter the rules and turn content changes into
  code changes.
- Plain-text features cannot be validated or used by later systems.

**Decision.**

1. Every grant is a `RuleEffect(kind, key, value, text)`. Its `EffectKind` covers:
   - skill, save, tool, weapon and armour proficiency;
   - language, darkvision, resistance, speed and HP per level;
   - unarmoured defence, resource, save advantage and carry capacity;
   - innate magic, combat rule, social feature and shop price.
2. `CharacterRules` gathers effects from the species, background, class and chosen options, tagging
   each with its source. Each kind has exactly one interpretation.
3. Kinds with no consumer yet (combat, magic, social) are still typed, validated and shown on the
   sheet, so later phases read data instead of parsing text.
4. `RpgCatalog.Validate()` checks every effect key against the catalogs at first use.

**Consequences.**
- No per-species or per-class conditional exists in the rules or the UI.
- Three effects already change play: species speed, goliath carry capacity and merchant prices.
- A new species or class is data only, unless it needs a new effect kind.

**Alternatives considered.**
- Scripted features (one delegate per feature): rejected for now, because delegates can be neither
  saved nor validated.
- Free-text features: rejected, because nothing can use or check them.
