# ADR-0022: Fifth-edition-compatible rules from the SRD under CC BY 4.0

**Status.** Accepted, 2026-11-10 (Phase 6).

**Context.**
- The Game Design Bible asks for familiar tabletop rules: six abilities, eighteen skills,
  proficiency, classes and species.
- Only openly licensed content may be used. SRD 5.2.1 and SRD 5.1 are released by Wizards of the
  Coast under CC BY 4.0.
- Aasimar and eight of the requested backgrounds are in neither SRD.

**Decision.**

1. Mechanics follow SRD 5.2.1. SRD 5.1 values are used where 5.2.1 omits something (Half-Orc;
   species ability increases).
2. **Ability increases come from the species**, as in SRD 5.1. The 5.2.1 background-based increases
   are recorded as an alternative but not implemented.
3. Point buy (27 points, scores 8–15) and the standard array are the only ways to set scores.
4. All text is Realmkeeper's own: descriptions, lore, feature wording and kits. Every definition
   records its `ContentSource`: `Srd521`, `Srd51` or `RealmkeeperOriginal`.
5. Aasimar is a Realmkeeper original built from the trait system. Its name is flagged for the
   owner's IP review.
6. The CC BY 4.0 attribution statement is kept in `Systems/Characters.md` and must appear in the
   shipped credits. The "Dungeons & Dragons" trademark is not used.

**Consequences.**
- The content is legally usable with attribution, and each definition's origin can be checked.
- Switching to 5.2.1-style increases later is a rules change, not a data migration.

**Alternatives considered.**
- Fully original rules: rejected, because the Bible asks for the familiar ruleset.
- Non-SRD published content: rejected, because it is not licensed.
