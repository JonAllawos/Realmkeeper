# Phase 12 report: Quest, Dialogue, Reputation & World Interaction Engine

Date: 2026-11-11. Branch `feature/phase-0-1-foundation`, on top of Phase 11 (`7b2831b`). Committed locally only; not pushed.

Status words: **Complete** = built, used, and tested through commands and the UI; **Partial** = works where named, with the gap stated; **Data-only** = typed and validated, with nothing in play using it yet; **Deferred** = not built; **Missing** = absent before this phase.

## A. Executive summary

Phase 12 connects the simulation to RPG play:

- **Quests** are data-driven templates bound to the generated world's own people, places, factions, items and creatures (ADR-0056). They have validated states and a stage-and-alternative objective engine fed by typed events from commands, inside the command's transaction (ADR-0057).
- **Dialogue** is data: conditional lines and responses, skill checks rolled with the world's dice, and typed consequences applied by one command (ADR-0058).
- **Reputation** is kept in separate dimensions: seven per person, plus faction standing, settlement reputation and notoriety, all with keyed changes. The player's deeds travel through the Phase 4 rumour channels, and only those who hear react (ADR-0059).
- **Emergent activities** arise from real conditions: food shortages, predators near herds, empty shelves. Players discover them through people, the notice board and the ledger. The journal shows only what the player knows (ADR-0060).
- **Content tags** carry configurable presentation, and no campaign canon is fixed (ADR-0061).
- **The vertical slice**, "Short Measures", is a real shortfall in a town's grain stores with an investigation and three endings. "Under Seal" (a particular letter, sealed or read) and "Teeth at the Folds" (a hunt through the encounter system) complete the requirements.
- **Schema v14**, with a migration from v13.

Verification:

- .NET: **603 passed, 0 failed** (0 warnings, 0 errors).
- Unity EditMode: **603 / 603 passed** (1 pre-existing compiler warning, CS0067).
- Unity PlayMode: **100: 83 passed, 17 skipped, 0 failed** (Phase 11: 80 passed, 16 skipped). The 17 skipped are the capture sets, which run only with `RK_CAPTURE_DIR`.
- Screenshots: **13**, genuine captures from the running game.

## B. Initial Git state and baseline

- HEAD `7b2831b` (Phase 11), level with `origin` (Phases 9–11 were pushed on the owner's instruction).
- Working tree: only the owner's `ProjectSettings/GraphicsSettings.asset`, which is preserved and never staged. An untracked Unity test scene (`Assets/InitTestScene….unity`) is ignored.
- Baseline:
  - .NET 574 passed, 0 build warnings;
  - EditMode 574 / 574;
  - PlayMode 96 tests (80 passed, 16 skipped capture sets, 0 failed);
  - schema v13.

## C. Phase 10 and Phase 11 integration audit

The full audit is in `Documentation/Phase12/PreFlight.md`. In short:

| System | Before | Phase 12 |
|---|---|---|
| NPC identity, personality, goals, schedules | Complete (Phase 4) | Dialogue conditions, quest roles |
| NPC knowledge (Known / Believed / Rumor, sources, hops) | Complete (Phase 4) | Gains the `PlayerDeed` topic, spread by the same sharing |
| NPC memories | Complete, bounded (Phase 4) | Quest and dialogue outcomes become memories |
| Player ↔ NPC regard | Partial (familiarity, affinity) | Adds trust, respect, fear, gratitude, hostility |
| Player ↔ faction standing, settlement reputation, notoriety | Missing | Built |
| Faction ↔ faction relations, influence | Complete (Phase 3) | Untouched; quests change influence through it |
| Settlement economy | Complete (Phase 2) | Real problems: grain removed and restored, food delivered into stores |
| Chronicle | Complete | 13 new event types; a player-known view |
| Conversation | Partial (topics, no choices) | Kept inside the new dialogue as "Ask about…" |
| Notice board, ledger | Partial | World objects through one inspect command |
| Inventory, merchants | Complete (Phase 11) | Deliveries, item rewards and shelves through the same holder rules |
| Encounters | Complete (Phase 10) | Defeat and survive events from the once-only resolution; a sought quarry |
| XP ledger | Complete (Phase 7) | Quest XP keyed `quest:{instance}:{reward}` |
| Quests, dialogue, discovery, content settings | Missing | Built |

## D. Quest architecture and data model

Definitions (`QuestDefinition`) are templates with:

- role specifications;
- `{role}` text;
- prerequisites, availability, objectives, failures, rewards (with optional outcome filters and journal text), consequences (timed OnAvailable, OnAccept, OnComplete, OnFail or OnAbandon, and by outcome);
- follow-ups, expiry, dialogue, content tags, source and version.

There are 12 categories with one engine. Instances (`QuestInstanceData`) keep bindings, state, progress, decisions, variables, outcome, rewards claimed, applied consequence keys, failure reason, a bounded history and tracking. IDs are stable: `quest:{definition}:{giver or cause}`.

The binder (`QuestBinder`) is generic and deterministic. The state machine is checked (`QuestStates.CanMove`): no double completion, and no reactivation unless the quest is repeatable.

## E. Objective engine and event processing

There are 22 objective kinds, with stages (sequential), groups (alternatives, each naming its outcome), and optional, hidden, timed, conditional and unavailable-when objectives, and objectives that fail the quest.

Commands emit typed `QuestEvent`s:

- entering a settlement;
- encounter resolution (each fallen foe, and whether the player survived);
- crafted work;
- inspect and read;
- dialogue (gives, decisions, explicit completions).

`QuestRuntime.Process` runs inside the command, before validation. It matches, re-reads state objectives, checks failures, and completes, all atomically. The daily `QuestSystem` (order 480) handles lapses, failures, timers, emergent quests and deed reactions. Nothing runs per frame.

| Status | Objective kinds |
|---|---|
| Fed by real events | Talk, persuade, investigate, discover, deliver, decide, defeat creature, survive encounter, reach settlement, craft |
| Partial | Escort, rescue, protect, defeat named: typed and matched, but no current system emits them |

## F. Dialogue architecture and implemented interactions

The graphs (`DialogueCatalog`) are:

- `npc_general`: a greeting by regard, mood and activity; the 11 Phase 4 topics; and engine hooks into quests and trade;
- one graph per quest, with a node per role.

Checks are d20 plus the rules engine's skill bonus against a DC, adjusted by the person's trait, and are shown before choosing. Consequences apply in `ChooseResponseCommand`, and the next line reflects them. The interactions are tabulated in `Systems/Dialogue.md`:

- accept, negotiate, decline;
- persuade, intimidate (tagged as a threat), bribe, deceive (with consequences if caught), insight;
- give items, receive the letter;
- start the hunt;
- trade.

## G. NPC knowledge, memory and rumour systems

- Deeds are knowledge (`PlayerDeed`), witnessed and then shared by Phase 4's rules: one item per meeting along ties, a 3-hop limit, confidence lost per retelling, the rumour lifetime, and deeds more interesting than gossip.
- Listeners react once, by their personality.
- In a 30-day test a deed reached 9 of 19 people, so propagation is bounded and not universal.
- Memories record outcomes ("Made to return the grain by …", "… lied to me about the grain").
- The player keeps discoveries, rumours (with source and confidence), known and visited places, and decisions, each bounded.

## H. Reputation and faction integration

- Per person, there are seven dimensions. Faction standing and settlement reputation run −100 to 100, notoriety 0 to 100.
- Every change is keyed in a bounded ledger: a repeat is refused.
- Tiers are presentation only, with configurable thresholds.
- Faction standing is a named price factor in shops. Standing of −50 or less, or a keeper's hostility of 70 or more, closes their shop and the trade response.
- Faction-to-faction relations (Phase 3) are separate and untouched.
- Theft and assault as reputation sources are Data-only, because no player action produces them yet.

## I. Emergent quest generation

| Cause | Key | Checked | Quest |
|---|---|---|---|
| Food shortage | `shortage:food:{settlement}` | Daily | Bread Before Winter |
| Wolves or dire wolves (3+) within reach of a settlement with herds | `predators:{settlement}:{creature}` | Weekly | Teeth at the Folds |
| Shop out of a material it deals in | `supplies:{shop}:{item}` | Weekly | Empty Shelves |

- At most one open instance per cause.
- Offers lapse on expiry, or when the cause ends.
- Deterministic: settlements and shops are taken in ID order, and the seeds come from the causes.

## J. Rewards and consequences

| Reward | Path |
|---|---|
| Experience | The keyed XP ledger |
| Coin | The purse |
| Items | The Phase 11 factory and holder rules (a reward too heavy to carry fails the completion as a whole) |
| Regard, standing, reputation | The keyed ledger |
| Other | Information, locations, follow-ups, merchant access |

Consequences are applied once each, keyed on the instance:

- settlement food, stability and prosperity;
- faction influence;
- memories, regard, deeds and morale;
- notoriety;
- Chronicle notes.

## K. Journal and UI implementation

- **The Player Hub's Journal:**
  - tabs for Active, Available, Completed, Failed, Abandoned, Rumours, Discoveries, Decisions, Events and Reputation;
  - a quest detail panel: objectives (hidden ones stay hidden, locked stages greyed, alternatives marked), people with known whereabouts, known and visited places, known rewards, failure conditions, decisions and history;
  - Track (up to 3) and Abandon.
- **The HUD** shows tracked quests with their next step, and a notice when a quest is new, updated, completed or failed.
- **The dialogue window** shows identity, tier and mood, the transcript, the line, numbered responses (keys 1–9, Esc to leave), checks named, and quest responses marked.
- **"Talk…"** is the first of a resident's topics.
- **The ledger and the notice board** dispatch the inspect command.

## L. Functional vertical slice walkthrough

See `Systems/Quests.md` § Vertical slice. Every step is covered by .NET tests (all three endings, decline, abandon, save and reload) and by PlayMode through the UI (`Phase12PlayModeTests`: Short Measures from approach to reward and after a reload, Under Seal declined then taken, a hunt progressed by a real fight).

## M. Persistence and migrations

Schema **v14**:

- quests and the conversation in progress;
- the player's social record;
- regard dimensions;
- content rules;
- the `PlayerDeed` topic.

`MigrationV13ToV14` adds the structure and enables the quest system. The living-world step adds the authored and emergent quests once. Validation rules are in `Systems/Persistence.md`.

## N. Testing and fault-injection results

Full detail: `Documentation/Phase12/Testing.md`.

| Suite | Result |
|---|---|
| .NET harness (Release) | 603 passed, 0 failed; build 0 warnings, 0 errors (Phase 11: 574) |
| Unity EditMode | 603 / 603 passed |
| Unity PlayMode | **100: 83 passed, 17 skipped, 0 failed** (Phase 11: 80 passed, 16 skipped). The 17 skipped are the capture sets, which run only with `RK_CAPTURE_DIR`. |
| Unity compiler warnings | 1, pre-existing: CS0067, `PlayerHubWindow.CombineRequested` is never raised (since Phase 11; not changed here) |
| Captures | 1 test, passed; 13 screenshots |
| Mutation checks | 6 of 7 caught; 1 equivalent survivor (the rewards-claimed guard, unreachable through the public API) |

New tests: `QuestSystemTests` (28), `QuestPerformanceTests` (1), `Phase12PlayModeTests` (3), `Phase12CaptureTests` (1).

Fault injection covered:

- a failed choice changes nothing;
- a reward too heavy to carry fails the whole completion;
- the validator catches impossible quest states;
- a corrupt quest section in a save is refused;
- consequences and reputation changes repeated with the same key apply once;
- an event for a stage not yet open is ignored.

Two existing tests were changed honestly because the design changed them:

- **The ledger test.** Examining the ledger now records a discovery, so the old premise that it changes nothing no longer holds.
- **The NPC topic count.** "Talk…" is one more button.

During the final run, one PlayMode assertion failed. The tracked-quest line is now hidden while a window is open, and the test read it with the journal still open. The test now checks both states: hidden while the window is open, shown once it closes.

## O. Performance measurements

From `QuestPerformanceTests` (.NET, Release, 155 quest instances with 60 active):

| Measure | Result |
|---|---|
| Journal | 0.88 ms |
| 60 quest details | 4.5 ms in all |
| An object inspected with 60 active quests (validated command plus event processing) | 1.5 ms |
| Conversation start with all hooks | 2.0 ms |
| 133 knowledge lookups | 0.08 ms |
| 30 simulated days with a deed spreading | 90 ms |
| Save / load of 975 KB (quests 245 KB) | 13 ms / 69 ms |

## P. Screenshots and documentation

Thirteen screenshots in `Documentation/Screenshots/Phase12/`, captured from the running game by `Phase12CaptureTests` (real scene, real UI, real commands). `p12_capture_log.txt` records what each shows.

| File | Shows |
|---|---|
| p12_01_conversation | Talking to the giver: their regard, a greeting chosen by mood and activity, the quest hook first |
| p12_02_dialogue_choices | Asking about the place: the answer comes from the person's own knowledge |
| p12_03_quest_offer | The offer: accept, negotiate (a Persuasion check shown before choosing) or decline |
| p12_04_journal_active | The journal: the active quest, only the objectives the player knows |
| p12_05_world_interaction | Examining the granary ledger in the street: the shortfall, recorded as a discovery |
| p12_06_skill_check | An Insight check with the keeper, rolled with the world's dice |
| p12_07_confrontation | The suspect cornered: persuade, intimidate (tagged as a threat), or take the silver |
| p12_08_quest_progress | The quest detail after the investigation: alternative endings revealed |
| p12_09_quest_reward | Quest completed: experience and coin through the existing systems |
| p12_10_reputation | Reputation: the town's standing, each person's separate dimensions, recent changes with reasons |
| p12_11_chronicle | The Chronicle as the player knows it |
| p12_12_completed_quest | The completed quest with its ending and history |
| p12_13_npc_reaction | The giver afterwards: a grateful greeting |

Documents:

- ADR-0056 … ADR-0061;
- `Systems/Quests.md`, `Systems/Dialogue.md`, `Systems/Reputation.md`;
- updates to `Systems/Persistence.md` and `Systems/Economy.md`;
- `Phase12/PreFlight.md`, `Phase12/Testing.md`, `Phase12/Limitations.md`;
- the Bible, ProductFoundation, the Roadmap and the README.

## Q. Licensing and content provenance

- All quest and dialogue writing is Realmkeeper's own, original to this phase. No proprietary dialogue or setting content is used.
- The skill checks use the SRD 5.2.1 skills and the existing rules engine (already credited).
- There are no new art assets.
- No explicit content. Threats are tagged.
- No canon (kingdoms, the protagonist, an antagonist) is fixed.
- The project remains separate from The Tavern Table.

## R. Known limitations and deferred features

See `Documentation/Phase12/Limitations.md`.

- **Partial:**
  - escort, rescue, protect and defeat-named objectives;
  - theft and assault reputation;
  - notice-style objects beyond the board and ledger;
  - incorrect-belief quests;
  - hunts depend on predators being in reach.
- **Data-only:**
  - main campaign, faction, exploration and bounty categories without quests;
  - most content tags;
  - repeatable quests.
- **Deferred:**
  - the main campaign and its canon, companions, a content-settings screen, map markers, voice and portraits.

## S. Final Git status and local commit hash

- Phase 12 is one local commit on `feature/phase-0-1-foundation`, on top of `7b2831b`: "Phase 12: quests, dialogue, reputation and world interaction". A file cannot contain its own commit's hash, so the hash is given with the delivery and in `git log`. **Not pushed.** The branch is ahead of `origin` by 1.
- Staged with `git add -A -- . ':!ProjectSettings/GraphicsSettings.asset' ':!Assets/InitTestScene*'`. The owner's change to `ProjectSettings/GraphicsSettings.asset` is preserved, unstaged, in the working tree. No reset, clean or history rewrite.
- Phase 13 has not been started.

## T. Recommended Phase 13 priorities

1. **Theft, assault and crime with witnesses.** The reputation model is ready, but the player actions are missing.
2. **Quests from more causes:** faction goals and disputes (negotiation quests between two factions), NPC personal goals, trade-route disruption.
3. **Escort and rescue,** with companions or captives moving through the world.
4. **Map markers** drawn from the player's knowledge.
5. **A content-settings screen** for the existing rules.
6. **More world objects** (shrines, monuments, letters left in places) through the one inspect command.
