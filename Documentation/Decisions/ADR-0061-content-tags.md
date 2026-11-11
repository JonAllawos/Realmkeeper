# ADR-0061: Quest and dialogue text carries content tags with configurable presentation; no campaign canon is fixed

**Status.** Accepted, 2026-11-11 (Phase 12).

**Context.**
- Realmkeeper is meant for a mature fantasy setting. Content settings will come in a later milestone.
- The brief asks that dialogue not be architecturally limited to sanitised text, that its presentation be configurable, that there be no explicit sexual content, and that no main-campaign canon be fixed.

**Decision.**
1. **Tags.** Quest definitions, dialogue lines and responses carry `ContentTags`: profanity, violence, threats, blood and injury, mature themes, adult relationships, sensitive subjects.
2. **Rules.** `ContentSettingsData` (saved with the world's settings) maps each tag to Show, Soften or Hide. `SetContentPresentationCommand` changes a rule.
   - A line that is hidden is skipped, and the next line whose conditions hold is said.
   - A softened line uses its gentler variant, or is skipped if it has none.
   - A hidden response is not offered.
   - The default shows everything as written.
3. **Content in Phase 12.**
   - The slice's threat (intimidating the suspect) is tagged Threats.
   - Starting a hunt from dialogue is tagged Violence.
   - There is no explicit content, and no gratuitous material.
4. **No canon.** The main-campaign category exists with no quests. No kingdom names, protagonist identity or antagonist are authored. Quests bind to whatever the generated world has.

**Consequences.**
- A future content-settings screen only needs to call the command.
- Writers can author stronger lines with softened variants.

**Alternatives considered.**
- *Filtering words at display time:* unreliable, and it changes meaning.
- *Separate sanitised dialogue trees:* duplicated content.
