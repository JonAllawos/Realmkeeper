# First release scope and deferred features (Phase 30)

**Date:** 2026-11-16. **Status:** proposal for owner review. Nothing is removed from the vision; this sorts **when**.

## The test for every feature

Does the first release need it to deliver **a complete 40-hour authored campaign in one believable, persistent world, with meaningful optional content**? Simulation exists to make that world believable, not for its own sake.

## Release critical

| Feature | Why | State |
|---|---|---|
| One canonical world; New Game, Continue, Load Game, Save Game, Settings, Exit | The product's shape | Partial (`SaveLoad-Design.md`, `SingleWorld-Transition.md`) |
| Three manual slots, autosave, backups, corrupted-save recovery | A long RPG must not lose progress | Partial |
| Baked, versioned world definition with story anchors | Authored content must not move under patches | Not started |
| The main campaign, about 40 hours, with its opening, endings and aftermath | The product | Engine only |
| Stealth, scripted sequences and set-piece fights the campaign needs | The opening requires them (Bible §3) | Not started |
| Companions with personal stories (roster per canon) | Bible §10 | Engine ready |
| Character creation, progression to the campaign's level range, combat, magic, items | Core RPG | Integrated; balance outstanding |
| Settlements, people, factions, economy and shops as they exist | The believable world | Integrated |
| Region travel and overland exploration across the campaign's regions | Physical travel is the design | Functional |
| Authored side content of a meaningful size (quests, chains, places) | Brief §5 | Little exists |
| Onboarding inside the opening | New players | Not started |
| Performance budgets met on a development build; long-session stability | Shippable | Unmeasured |
| Settings: audio, graphics, controls (remapping), accessibility | Shippable | Partial |

## High priority (strongly improves the release; not a blocker alone)

- Continuous region borders, crossable wherever passable.
- Fast travel or transport between known places (D2: the owner decides what it is).
- Cities, fortresses and ports placed in the world.
- Regional economic character (part of R2) where the campaign's regions use it.
- More creature kinds and models for the campaign's threats (Bible §7).
- Audio ambience and a mixed soundtrack; visual direction pass on characters (placeholder art today).
- Romance, if the owner keeps it for the first release.

## Optional (desirable; can wait)

- Frost and rising-water visuals for ground weather (ADR-0115 limits).
- Overland ambient content beyond what the campaign and side quests use (deferred Phase 29 §3–8 beyond the campaign's needs).
- More subclasses (Realmkeeper originals), more spells beyond the SRD set.
- Deeper survival.

## Post-release

| Feature | Why deferred |
|---|---|
| Player-owned property, homes and estates (R3) | Large, cross-cutting (ownership, employment, construction, tax); a natural first expansion |
| A world of "dozens of cities" (R2) at full scale | Size should follow the campaign; scale later with the dormant tier and partitioned saves |
| A connected underground network | R2; places cover the campaign's needs |
| Seamless streaming across region borders | Loading transitions are approved (ADR-0112) |
| Ancient-realm expansion beyond what the campaign uses | Canon still open |

## Scope to watch

| Pull | Serves | Note |
|---|---|---|
| More simulation depth (more economic goods, deeper politics, wider tiers) | Endless sandbox | Release only what the campaign and side content make visible |
| Bigger world before content | Sandbox | Empty regions cost load, save and tick time |
| New systems per phase | Breadth | Release work is now mostly content, integration and polish |

## Classification by purpose

| Purpose | Features |
|---|---|
| Needed for the 40-hour campaign | Canonical world, anchors, opening sequence, stealth, set pieces, campaign content, companions, save system |
| Needed for the world to feel alive | Settlement life, NPC schedules, factions, economy, weather, world events, emergent stories (all largely exist) |
| Mainly endless-sandbox simulation | Dormant tiers at scale, R2 at full scale, deep economics, estates |
| Expandable after release | R3, underground network, more regions, ancient-realm expansion |
