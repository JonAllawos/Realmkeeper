# Campaign architecture assessment (Phase 30)

**Date:** 2026-11-16. **Status:** assessment for owner review. **No story canon is decided or invented here.** `CampaignBible.md` is unchanged and remains the only canon reference.

## Answer first

**The campaign engine is ready to carry an authored campaign; the campaign itself does not exist.**
- Phase 17's engine handles a large campaign's structure: chapters, arcs (sequential, parallel, optional), decisions with delayed consequences, endings, companions reacting, and narrative resilience when people die.
- The quest, dialogue, discovery, place and faction systems underneath are implemented and tested.
- **Missing, technically:**
  - fixed story anchors (the engine casts its roles from whatever the world holds);
  - a scripted opening (stealth, a staged catastrophe, survival);
  - stealth mechanics;
  - antagonists and staged set pieces;
  - the ancient realm's content;
  - romance;
  - content-authoring tools that scale to tens of hours.
- **Missing, narratively:** most of the canon the Bible marks undecided.

## 1. What exists

| Capability | Status | Evidence |
|---|---|---|
| Campaign as validated data over the one world | Implemented | ADR-0082, `Application/Campaigns`, `CampaignValidator` |
| Chapters, arcs, stages, alternative groups | Implemented | ADR-0082 §4–5 |
| Decisions with typed, delayed consequences | Implemented | ADR-0083 |
| Narrative resilience (dead, captured or hostile people) | Implemented | ADR-0084 |
| Endings, with a fallback; the world continues afterwards | Implemented | ADR-0082 §7 |
| Quests: stages, optional, hidden and timed objectives, alternatives, rewards | Implemented, 37 quest definitions in code | Phase 12, ADR-0056–0061 |
| Dialogue: conditions, skill checks, consequences | Implemented, 33 dialogue definitions | ADR-0058 |
| Companions: recruitment, party, reactions, bonds, personal stories | Implemented, 4 personal stories | Phase 15, ADR-0087, ADR-0095 |
| Factions: standing, conflict stages, backing a side | Implemented | ADR-0090 |
| Discoveries, puzzles, persistent places | Implemented | ADR-0088, Phase 13 and 18 |
| Realms and a gateway the campaign can open | Foundation: one dormant gateway, one empty region | ADR-0111 |
| Authored content | **One provisional demonstration campaign** (*The Waystone Count*) and 4 two-quest chains | Not canon (Bible §11) |

## 2. Gaps against the brief's list

| Need | State | What is missing |
|---|---|---|
| Main quest progression, prerequisites | Engine ready | Content |
| Branching decisions, consequences, multiple outcomes | Engine ready | Content |
| Faction involvement | Engine ready | Canon factions (Bible §8–9 undecided) and their quests |
| Companions and recruitable characters | Engine ready | The roster (Bible §10: undecided); romance (no system) |
| Dialogue and conversations | Engine ready | Content at scale; an authoring format faster than C# data |
| Major antagonists | No support beyond named people and creatures | Boss encounters, staged confrontations, antagonist presence in the world |
| Regional story progression | Partial: world-state conditions exist | Region-gated chapters, travel unlocks, changes to regions as the story moves |
| Dungeons and ruins | Engine ready (places, puzzles, inhabitants) | Authored story places at campaign sites |
| Important discoveries | Engine ready | Content |
| Campaign milestones | Chapters and arcs | Autosave and journal hooks at milestones (`SaveLoad-Design.md` §5) |
| Failure and recovery | Narrative resilience | Design per chapter |
| Ending and post-campaign | Engine supports a final arc and a continuing world | The ending decisions (Bible §6, §16: open) |
| **The opening** (Bible §3) | **Not supported** | Creation before play (D4 currently allows pending play), a scripted infiltration with stealth (concealment is recorded but unread, ADR-0110), a staged catastrophe and battle, the crew scattering |

## 3. Technical requirements (no canon needed)

1. **Story anchors with fixed IDs** and an "anchor" role binding (`Architecture/SingleWorld-Transition.md` §3).
2. **Scripted sequences**: a small, typed set of staged events (move people, start a fight, change a place, play a line, fade) driven by campaign stages. No scripting language (ADR-0082 rejected one).
3. **Stealth**: detection from the recorded concealment and sight, sneaking, and being seen as a stage condition.
4. **Set-piece fights**: a boss or antagonist, with phases expressed through the existing combat engine.
5. **New Game into the opening**: creation first, then the first chapter (owner decision D4 revisited).
6. **Authoring at scale**: quests, dialogue and campaigns as data files (JSON or ScriptableObjects) validated by the existing validators, instead of C# object literals; a validator report in the editor.
7. **Romance**, if the owner keeps it (Bible §10 says "where appropriate"): relationship stages on the existing regard dimensions.
8. **The ancient realm**: regions, creatures and places, once the canon allows.

## 4. A campaign implementation framework

The Bible's nine placeholder stages (§11) are **structural placeholders, not chapters**. The framework below organises work around them without fixing their content:

| Layer | Contains | Built with |
|---|---|---|
| Campaign | One definition, versioned, with its final arc and endings | ADR-0082 |
| Acts (3–4) | Groups of chapters with a regional focus | Chapters |
| Chapters (about 9) | One per Bible stage, each with an entry condition, a goal and an exit decision | Chapters and arcs |
| Arcs | Parallel threads inside a chapter (main, crew, faction, discovery) | Arcs |
| Quests | The playable units, 30–90 minutes each | Quest definitions |
| Anchors | The people, places and factions the arcs need | World definition |

**Vertical slice first:** the opening and the first chapter, played start to finish, before any later chapter is written. It proves:
- the opening sequence;
- the anchor binding;
- stealth;
- a set-piece fight;
- autosave at milestones;
- the authoring pipeline.

## 5. Sizing the content (estimate, not a plan)

**Assumption** (not a decision): a main quest takes 30–90 minutes, including travel and fights. Then 40 hours of main story needs **roughly 30–70 main quests**. Side content of a similar size needs a comparable number of side quests, chains and places.

The world's size should follow from this:
- how many regions the acts move through;
- how many settlements and story places each needs;
- what travel time between them the owner wants.

Each region holds about 14 real minutes of crossing on foot (ADR-0110). Main-story travel alone is unlikely to need more than a few dozen regions. The number is an owner and design decision, to be made with the act plan.

## 6. Undecided story questions (owner)

Kept from the Bible, not resolved here:
- The ancient world: its name, condition, species, cultures, politics (Bible §5).
- The containment system's final decisions (§6, §16).
- The military conspiracy: its name, leadership, sponsors, survivors (§8).
- The crew: identities, roles, fates (§3, §10); the companion roster (§10).
- The factions and kingdoms of the surface world (§9).
- The antagonist(s); the ending choices.
- The new-game flow: creation before play (Bible §11 note; D4).
- Whether romance is in the first release.
