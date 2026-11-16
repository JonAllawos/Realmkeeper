# Feature-completion matrix (Phase 30)

**Date:** 2026-11-16. **Evidence:** code, tests, documentation and owner reports, as of `7ce9749`.

## Levels

| Level | Meaning |
|---|---|
| 1 | Not started |
| 2 | Foundation: data and rules exist and are tested; little or nothing uses them in play |
| 3 | Functional: works in the running game |
| 4 | Integrated: works with the other systems it should touch |
| 5 | Content complete: all release content exists |
| 6 | Playtested: the owner has played it through and accepted it |
| 7 | Release ready |

## Verification

| Code | Meaning |
|---|---|
| **A** | Automated: .NET and Unity tests. Last full checkpoint Phase 26.1 (EditMode 954/954, PlayMode 139); Phases 27–29 focused only |
| **R** | Running-game verification by Claude: PlayMode tests in the shipped scene, or driving the editor (for example the townsfolk check on 2026-11-16) |
| **O** | Owner hand-play report exists for part of it |
| **U** | Unverified in play |

**No system is at level 5 or above:** no release content exists for any of them, and the owner checklists since Phase 18 are recorded as open.

## Matrix

| System | Level | Verified | Evidence | Depends on | Remaining for release |
|---|---|---|---|---|---|
| World generation and geography | 4 | A, R | ADR-0005, 0076, 0110; 9 regions, 14 biomes | — | Canonical world definition, baking, larger atlas, cities placed (`SingleWorld-Transition.md`) |
| Region streaming and transitions | 3 | A, R | ADR-0112, ADR-0122 cache | Geography | Continuous borders; transition time on a development build |
| Overland exploration | 3 | A, R, U | Phases 23–24, 29 | Streaming | Overland content (deferred Phase 29 §3–8), owner hand-play |
| Terrain and weather | 4 | A, R | ADR-0115, 0122; seasons, fronts, ground conditions | Geography | Development-build performance; frost and flood visuals optional |
| Settlements | 4 | A, R, O | Plans ADR-0047; gates ADR-0120–0121; owner gate and town reports | Geography | Cities, fortresses, ports placed; authored anchor towns |
| Settlement populations | 4 | A, R, O | ADR-0119 street life; owner townsfolk report fixed `b288ce5` | Settlements, NPCs | Owner hand-play after the fix |
| NPC schedules and behaviours | 4 | A, R | Phase 4, 10, 18 | — | Authored people for the campaign |
| Factions and politics | 4 | A | Phase 3, ADR-0090 | NPCs | Canon factions (undecided) |
| Economy and trade | 4 | A | Phase 2, R1 recorded | Settlements | Regional character (R2) scoped for release |
| Shops and crafting | 4 | A, U | Phases 11, 21; 26 recipes, 12 shop kinds | Economy, items | Balance; enchanting and armour recipes |
| Character creation | 4 | A, R | Phases 6, 20, 22 | — | Creation-first New Game (D4); placeholder art |
| Character progression | 4 | A, R | Levels 1–20, player-driven level-up | — | XP curve and pacing for 40 hours |
| Attributes and skills | 4 | A | SRD 5.2.1 | — | Skills used by stealth and social content |
| Inventory and equipment | 4 | A, R | 114 items, 11 magic | — | Release item set, rare gear |
| Combat | 4 | A, R, O | Phases 8–10, 14; turn-based | — | Set-piece and boss support; balance |
| Magic and abilities | 3 | A | 47 spells (28 functional, 12 partial) | Combat | Partial and deferred spells resolved |
| Companions and party | 4 | A, R | Phase 15, 17–18; 4 personal stories | NPCs | The roster (canon), more personal stories, romance decision |
| Dialogue | 4 | A | ADR-0058; 33 definitions | — | Authoring pipeline; content |
| Quests | 4 | A, R | ADR-0056–0061; 37 definitions | Dialogue | Content; authoring pipeline |
| Main campaign | 2 | A | Engine implemented; one provisional campaign | Quests, anchors, canon | Everything: `Campaign-Architecture-Assessment.md` |
| Side content | 3 | A | Emergent stories (7 kinds), 4 chains, world events (10 kinds) | Quests | Authored side quests and chains at volume |
| Dungeons and interiors | 4 | A, R | Phases 13, 18, 21, 27; 10 archetypes, puzzles | — | Story places; underground network (R2) post-release |
| Ancient world and gateways | 2 | A | ADR-0111: one empty region, one dormant gateway | Canon | Everything; canon undecided |
| Creatures and bestiary | 3 | A, R | 25+ kinds, 19 drawn with models | Combat | Campaign creatures (Bible §7) |
| Crime and reputation | 4 | A | ADR-0059, 0069 | NPCs | Guards' response in play; bounties at scale |
| Travel and transportation | 3 | A, R | Journeys, route graph, walking | Geography | Fast travel or transport (D2 undecided) |
| Survival and resting | 3 | A | Rests, hunger (fed level), exhaustion | — | Decide the release depth of survival |
| Save and load | 3 | A | v27, atomic, migrations | All | `SaveLoad-Design.md`: slots, autosave, backups, metadata |
| Main menu and UI | 3 | A, R | Phases 19, 25–26; design system v2 | — | Release menu (New Game, Continue, Load Game); onboarding |
| Audio | 3 | A, U | Audio pass `da4fb07`: music, footsteps, hits, jingles | — | Owner hand-play; ambience; mix |
| Graphics and visual effects | 3 | R, U | URP, kits, Unity Terrain, placeholder art | — | Character art (placeholder), visual direction pass, effects |
| Accessibility | 3 | A, R | Text size, contrast, reduced motion | UI | Key remapping, subtitles policy, colour-blind checks |
| Performance | 2 | R | Editor measurements only | All | Development-build budgets; long sessions |
| Settings | 3 | A | `RkSettings`: audio, text, motion | — | Graphics options, key remapping |
| Onboarding and tutorials | 1 | — | No tutorial code | Opening | First-hour teaching inside the opening |

## Optional-content systems (brief §5)

| Activity | Supported by | State |
|---|---|---|
| Side quests, settlement problems | Quests, emergent stories | Engine ready; little authored content |
| Faction quests | Factions, quests, conflict stages | Engine ready; canon factions undecided |
| NPC personal stories, companion quests | Companions, personal stories | 4 exist |
| Bounties and criminal activity | Crime, notoriety | Crime exists; bounty-hunting as content: not authored |
| Exploration discoveries, hidden locations | Discoveries, landmarks, place discovery | Implemented, 10 kinds |
| Ruins and dungeons | Places, puzzles | Implemented; deferred overland content (Phase 29 §3–8) |
| Rare equipment | Items, loot tables | 11 magic items |
| Environmental storytelling | Places, landmarks (wrecks of real caravans) | Partial |
| Randomised but persistent encounters | Overland encounters (v26), creature populations | Implemented |
| World events, travel discoveries | World events (10 kinds), landmarks | Implemented |
| Optional bosses | Combat | Not supported beyond ordinary fights |

## Known gaps in verification

- No full Unity suite run since Phase 26.1.
- None of Phases 23–29 measured a development or player build.
- The owner checklists from Phase 18 onward are recorded as open in `Roadmap.md`; owner reports exist for parts of Phases 26–29.
