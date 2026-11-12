# ADR-0088: Discoveries have a kind, a certainty, and people who care about them

**Context.** The Phase 18 preflight audit found that discoveries were a flat list of key and text:
- Many clues recorded nothing when studied.
- Nothing in the world cared what the player had found.

The brief asks for discovery categories with real gameplay use, a journal that tells rumoured, unverified and confirmed apart, and rewards for meaningful discovery rather than for clicking.

**Decision.**
1. **Kinds.** `DiscoveryCategory` has ten kinds: Historical, Geographic, Cultural, Faction, Religious, Natural, Economic, Personal, Dangerous and Arcane, plus None for quest-only facts. Every feature with an insight declares its kind in `FeatureCatalog`. Each kind is used three ways:
   - the journal groups by it;
   - it decides who in the world wants to hear it (`DiscoveryRules.Interest`, by role and faction);
   - it decides which companions remark on it (`CompanionStories.Interested`, by trait).
   A test asserts that all ten kinds are used by something in the world.
2. **Certainty.**
   - Rumours stay their own list: heard, never seen.
   - A discovery is *Confirmed*, or *Unverified* when the clue's insight is a guess. Its proof is another discovery key, or `group:{name}@{place}` for creatures seen in a fight.
   - Recording the proof confirms the guess; finding the proof first confirms it as it is recorded.
   - A discovery also verifies a rumour with the same key.
   - Explored and cleared places keep their Phase 13 states.
3. **Every clue records.** A generic clue with an insight now records it, keyed by the place and the feature (`{placeDefinition}:{feature}`), so the claw marks of one cave are not those of another. Authored keys are unchanged.
4. **Rewards are earned, not clicked.**
   - Only *major* discoveries (authored story facts) give experience: 25 XP, once per key (`ExperienceLog` key `discovery:{key}`).
   - Telling a discovery to someone who cares gives what such a person has: settlement standing, faction standing, a little coin for trade news, or regard. Each is given once per settlement, recorded on the discovery itself (`SharedIn`), so it is safe from the bounded ledgers.
   - Unverified minor guesses are not worth telling.
5. **Bounds.** The list keeps 400 (was 200). The oldest *minor* discovery is dropped first, so major facts stay.

**Consequences.**
- The journal's Discoveries tab is grouped and marked: ◆ for major, [unverified], and where it was told.
- General conversation offers at most two findings the person would care about.
- Save schema v20 adds seven optional fields; older saves read as uncategorised and confirmed (ADR-0096).
