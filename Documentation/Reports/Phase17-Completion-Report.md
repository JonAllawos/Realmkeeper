# Phase 17 completion report: Phase 16 Closeout, Campaign Engine, Narrative Systems & Persistent Storytelling

Date: 2026-11-12. Branch `feature/phase-0-1-foundation`. Committed locally, **not pushed** (the brief: "Do not push"). The commit hash is given in the hand-off message and in `git log`; a commit cannot contain its own hash.

## 1. Preflight findings

The full audit is in `Reports/Phase17-Preflight-Audit.md`. In short:
- **Quests can carry a campaign.** The Phase 12 quest system already had what campaign objectives need: role-bound templates, stages, alternative groups, hidden, optional and timed objectives, checked states, and keyed rewards and consequences. It needed no replacement, only a container above it.
- **What was missing:**
  - a place for decisions that persist across quests (quest decisions were quest-local);
  - condition kinds for campaign state, character status and world events;
  - disabled dialogue choices.
- **Status can be derived.** What becomes of a person is already decided by existing systems: death, captivity, hostility, absence, a companion's departure. A campaign can read it rather than store it.
- **The atlas fight was already real.** It was the one engine's encounter; only its presentation was missing.
- **Groups beyond sight.** The atlas hid all groups outside the player's region, even ones the player had heard of.

## 2. Baseline

| Item | Found |
|---|---|
| Branch / HEAD | `feature/phase-0-1-foundation` at `7ab5163`, level with `origin` (the Phase 16 push confirmed) |
| Working tree | Clean. `ProjectSettings/GraphicsSettings.asset` unmodified (committed in `48b0098`). |
| Save schema | v18 |

## 3. Phase 16 closeout

Full detail is in `Reports/Phase16-Final-Verification.md`.
- **The outstanding item is closed.** The full Unity EditMode suite on the unchanged `7ab5163` passed 750 of 750 (31 min). The Phase 14 save/reload test passed in 189 s, within its 10-minute limit; its run time is unchanged and its assertions were not touched.
- **The atlas fight transition is done** (ADR-0086). Fights met on the road open in the combat scene, staged on the ground at the road's nearer end, and are played with the existing tactical panel. The party stays on the road; leaving the ground halts the journey where it was, and travel resumes only when the player says. Saving mid-fight keeps both the fight and the road.
- **Groups beyond sight are done.** They show only where a report the player has puts them, marked as reported, with when and from whom, and no live numbers or history.
- **The atlas and travel checklist** was checked through the real UI by automated PlayMode tests, not by a person playing.

## 4. Full EditMode results

| Run | Passed | Failed | Skipped |
|---|---|---|---|
| Baseline on `7ab5163` (before any change) | 750 | 0 | 0 |
| Final (with Phase 17) | **791** | 0 | 0 |

The final run took 34 min. The slowest test is still the Phase 14 save/reload test; its fixture took 216 s, within its 10-minute limit, and nothing timed out. Of the 791, 41 are new in Phase 17: 37 campaign, decision, resilience, companion, story, persistence and determinism tests, 3 atlas tests and 1 performance test.

## 5. Other suites

| Suite | Result |
|---|---|
| .NET baseline on `7ab5163` | 750 passed, 0 failed, 0 skipped |
| .NET final | **791 passed**, 0 failed, 0 skipped (41 new) |
| Unity PlayMode final (full) | **115 passed**, 0 failed, 0 inconclusive, 29 skipped (the capture sets: they run only with `RK_CAPTURE_DIR`) |
| Phase 17 capture (editor, `RK_CAPTURE_DIR` set) | **1 passed**: 19 screenshots |

**PlayMode during development.** The Phase 16 atlas PlayMode test `EveryAtlasControl_WorksThroughTheWindow` failed in two ways, both because the fight now opens in the scene and the outlaws now act:
- Its withdrawal assumed nothing happened until "Fight it out".
- The existing rule refuses a withdrawal while an enemy stands next to the player.

The test now uses the atlas's Withdraw and, when that is refused, the labelled "Let it play out". Its other assertions are unchanged. In one run, an intermittent Unity engine log (a 160-byte temporary-allocator leak report while entering an ordinary town) failed the tests that followed it. It did not recur in the next runs.

## 6. Manual atlas and travel results

Every item in the brief's list was checked through the shipped scene's real UI by automated tests (PlayMode `TheAtlasChecklist…`, `EveryAtlasControl…`, `AFightOnTheRoad…`), and by EditMode tests where state is checked.

**No item was played by hand, and no gameplay video was recorded.** The batch test runner cannot record, and no recording tool was available.

Movement rules are unchanged: free WASD exploration, and the existing tactical grid in combat.

## 7. The combat transition

`TravelCommandHandlers.RoadFight` and `WorldSession.GetRoadFight` identify the fight the journey names. The runtime then switches to the exploration view, and the exploration controller stages `RoadsideGround`: the near settlement's terrain without its buildings, people, herds, streets or walls.

The existing combat panel runs the existing encounter, with its own participants: the outlaws, the player and the travelling companions. Loot, witnesses and crime follow the existing encounter end. `LeaveRoadFightCommand` is refused while the fight goes on.

The atlas keeps "Let it play out (the AI plays your turns)" as an explicit, labelled choice; it is never the default.

**Limitation:** the ground shown is the near settlement's land, not a generated stretch of road. Tactical terrain is the encounter's own, as for every fight.

## 8. Campaign architecture (ADR-0082)

- **Definitions are data.** `CampaignDefinition` is versioned and validated (`CampaignValidator`, with readable errors), and has a provisional flag. It holds:
  - roles cast from the world;
  - campaign quests (existing quest definitions);
  - chapters, arcs, decisions and endings.
- **Admission.** A malformed definition never enters the catalog, and a start attempt reports why.
- **Instances.** `CampaignInstanceData` in `WorldData.Campaigns` holds bindings to the world's own IDs, progress, decisions, pending consequences, applied keys, flags, quest links and a bounded history. There are no copies of anyone.
- **States are checked.**
  - Campaign: Active, Suspended, Completed, Failed or Abandoned. Closed is final.
  - Arc: Locked, Active, Completed, Failed or Abandoned.
  - Chapter: Locked, Active or Completed.
- **Sandbox by default.** A world without a campaign is a complete sandbox. An old world can take up a campaign when it has a road out of its home region; it is told why not otherwise.

## 9. Story arcs and chapters

- **Arc objectives** are linked quests, decisions or world-state conditions, in sequential stages and alternative groups. They can be optional, hidden or refused. Arcs can carry a time limit (the world settles it without the player), failure conditions, fallbacks, and completion and failure effects.
- **Chapters** open when the chapters they follow close, and close when their required arcs close. Optional arcs never hold a chapter.
- **Re-reading is event-driven:** a campaign quest's state change, a decision, or the day's pass. A re-read costs 0.015 ms.

## 10. Decisions and consequences (ADR-0083)

- **Made once.** A decision is recorded once with who put it, and never undone. It also goes into the player's decision log and as a quest decision event.
- **Effects.** Typed effects are applied once by key, through each system's own path: the reputation ledger, companion loyalty, world events, NPC memory, knowledge and relations, settlements, roads and quests.
- **Delayed consequences** are saved with a due time and optional conditions, and fire once. They happen even after the campaign closes, because they are the world's.
- **Disabled choices** are shown with their reason, and choosing them is refused.
- **Tested:**
  - consequences applied once across saves, reloads and simulated days;
  - a later decision changing what people say;
  - a decision changing what quests are offered;
  - decisions changing real state: standings, stability, regard, memories, a world event on the crossing, a mended road.

## 11. NPC narrative integration (ADR-0084)

- **Status is read from the world,** never stored: dead, captured, travelling with the player, left, hostile, missing, away, hurt, or available.
- **Roles follow the world.** `FactionLeader` reads the current leader; `FirstAvailable` passes to the next person who can still be met.
- **Other ways on:**
  - alternative objectives ("the letter goes unanswered");
  - fallbacks (the toll-keeper's household is asked);
  - an arc lost when nobody who knows is left.
- **Tested:** the warden killed; the toll-keeper made hostile, sent away, and killed with their household; the far faction's leader replaced. Nobody was raised.

## 12. Faction story integration

- **Standings and opinion.** Decisions move standings with the factions the two towns answer to, and the factions' opinion of each other, through the existing records.
- **No membership needed.** Refusing to take a side is a full path, and no part of the story requires joining a faction.
- **One faction-exclusive way.** A friend of the far town's faction (standing 20+) can make the case for a shared count.
- **No new political model.** The engine reads `FactionData.LeaderNpcId` and `FactionRelationData`.

## 13. Companion story integration (ADR-0087)

- **Reactions.** Companions travelling with the player (reserves never) react to decisions by their own traits. Loyalty moves once, with the reason, and they remember it.
- **Remarks.** Companions say what they think when asked, from decisions, falling out, arriving where their people live, and trouble at home. There is no random banter.
- **Bonds.** Companions on opposite sides of a decision grow apart through the existing person-to-person relations. Quarrels show at rivalry 20 and can be talked round (Persuasion).
- **A second personal story.** "Unfinished Business", bound to a real person elsewhere, has three endings and closes through the same loyalty rule as the first. "Old Ground" is unchanged.
- **In the Party section:** second story, remarks waiting, what they said, and how they get on with each other.

## 14. Dialogue and journal changes

**Dialogue:**
- condition kinds 22–37, and consequences `CampaignDecision`, `CampaignFlag`, `HearRemark`, `Reconcile` and `Relation`;
- disabled choices shown with reasons;
- decisions marked "◆ … [decision]";
- campaign role names in text;
- two companion options in general talk.

**Journal:**
- a **Campaign** tab in the existing journal: what could begin here, overview, story (chapters, arcs, objectives), decisions (what came of them, what was hinted), people (heard of or met; fate once met) and history;
- set aside, take up, give up, and refuse an optional arc;
- hidden arcs, objectives and endings stay hidden.

**Other UI:**
- the atlas marks reported groups "?", with their own inspector;
- the combat title wraps;
- on the road the HUD says "On the road".

## 15. Emergent storytelling (ADR-0085)

The daily story pass offers at most one new story a day, three open at most, one per cause ever. Every fact comes from the event and the people's own knowledge:
- **What Became of the Caravan:** a lost caravan; a witness who really heard of it; the event's own telling. The town is steadier when the story is resolved.
- **Grain Down the Road:** a hungry town beside a full one, preferably across a border. The grain really moves; the story lapses if the shortage ends first.

The giver carries the story as news, so it spreads by talk. Outlaw stories are left to Phase 16's existing event quests, so nothing is duplicated.

## 16. The demonstration campaign

***The Waystone Count* is provisional and non-canonical.**
- **Premise:** two real towns on either side of a border crossing, a toll-house, a short count.
- **Structure:** 3 chapters, 5 arcs (one optional), 6 campaign quests, 2 decisions with 7 choices, 6 delayed consequences, 5 endings.
- **What it demonstrates:**
  - a world-event interaction: a delayed consequence raises a trade disruption on the crossing, and the dialogue changes if a caravan was lost or the road is barred;
  - companion reactions;
  - a time limit;
  - fallbacks for dead or hostile people;
  - a faction-exclusive argument.
- **What it does not establish:** no kingdom, antagonist, history or destiny for the player.

All the text was written for Realmkeeper and reread for voice and accuracy. Two fixes came from that rereading: "coppers" became "coin" (no currency canon), and a companion's line wrongly implied they had paid a debt the player paid.

## 17. Save schema and migrations

- **Schema v19:** world `campaigns`; companion `remarks` and `storyQuestId`; system `realmkeeper.campaigns`.
- **`MigrationV18ToV19`:** an empty campaign list (an old world loads as a sandbox), empty remarks, and the campaign pass inserted after the quest pass.
- **Validator (`WorldValidator.Campaigns`):** one instance per campaign; refuses newer definition versions; real bindings and quest links; progress only for what the definition has; each decision once.
- **Tested:**
  - a v18 save loading as a sandbox with the pass enabled;
  - the chain from every older schema (the Phase 2 strip helpers gained a v18 step);
  - exact round trips mid-story, after decisions, with pending consequences, after a death and after the end.

## 18. Files changed

- **New code:**
  - `Runtime/Domain/Campaigns/` (model, validator, catalog, the demonstration), `Domain/Quests/QuestCatalog.Phase17.cs`, `Domain/World/WorldValidator.Campaigns.cs`;
  - `Runtime/Simulation/Campaigns/` (engine, binder, characters, conditions, story synthesis, daily system), `Simulation/Party/CompanionStories.cs`;
  - `Runtime/Application/Campaigns/` (commands, queries);
  - `Infrastructure/Persistence/WorldSaveMapper.Campaigns.cs`;
  - `Exploration/Presentation/PlayerHubWindow.Campaign.cs`.
- **New tests:**
  - EditMode: `Tests/EditMode/Campaigns/` (fixture, campaign, decision, story and performance tests) and `Tests/EditMode/Atlas/Phase17AtlasTests.cs`;
  - PlayMode: `Tests/PlayMode/Phase17PlayModeTests.cs` and `Phase17CaptureTests.cs`.
- **Edited:**
  - the quest and dialogue models and catalogs; party, world data, event types, validator; the quest world (prune, hook, story causes);
  - the system list; the party system; quest runtime and conditions; the dialogue engine and the choose command;
  - the session, travel commands and atlas queries; party queries; persistence (DTOs, mapper, migrations);
  - the exploration controller (road stage, HUD), the combat, dialogue, journal and party UI and their stylesheets; the runtime view mode;
  - the strategic atlas UI and stylesheet;
  - two existing tests: the Phase 2 daily order and save strip, and the Phase 16 atlas PlayMode test.
- **Docs:**
  - ADR-0082…0087 and the index;
  - `Systems/Campaigns.md` (new), plus Dialogue, Party, Quests, Travel, Economy (daily order) and Persistence (v19);
  - DataOwnership, the Game Design Bible, Product Foundation, Roadmap, Testing (results, untested areas);
  - Phase 17 preflight audit, Phase 16 final verification, this report;
  - `Screenshots/Phase17/`.

## 19. ADRs

| ADR | Decision |
|---|---|
| 0082 | A campaign is data laid over the one world; its instance points at the world's own entities and quests |
| 0083 | Decisions are records with typed, keyed consequences, some now and some later, applied once |
| 0084 | Narrative resilience reads what became of people from the world, and offers other ways; never resurrection |
| 0085 | The world offers its own stories, built only from what happened and who knows it |
| 0086 | A fight met on the road is fought on the ground at the road's nearer end, with the existing combat; the party stays on the road |
| 0087 | Companions react by their nature, say what they think when asked, and relate to each other through the existing records |

## 20. Performance

`Phase17PerformanceTests`, .NET, Release, on the six-region world:

| Measure | Result |
|---|---|
| Re-reading a campaign | 0.015 ms |
| The day's story pass | 0.46 ms |
| Dialogue view, conditions included | 0.37 ms |
| A decision with all its consequences (the whole command) | 5.2 ms |
| A simulated day | 18.9 ms without a campaign, 7.0 ms with one |
| Save / load | 167 ms / 314 ms, 5080 KB (the campaign section 6.8 KB) |

- **The two day figures are measurement order, not a real difference.** The campaign run came second, after warm-up. The bound asserted is "a campaign adds little to a day".
- **Bounded state:** campaign history 80 lines, companion remarks 12, stories 3 open and 1 new a day.
- **No per-frame evaluation.**
- **Unity frame time** was not measured in a development build.

## 21. Determinism

`TheSameWorld_AndTheSameChoices_GiveTheSameStory` plays the same world and the same choices twice: the same bindings, the same history, and the same world fingerprint.
- Role binding is seeded from the world and sorted by ID.
- The story pass is ordered by event number, then settlement ID.
- Effects are keyed.

No nondeterminism was added to Domain or Simulation, and neither references UnityEngine.

## 22. Screenshots and recordings

There are 19 screenshots, captured automatically in the editor by `Phase17CaptureTests` with `RK_CAPTURE_DIR` set (`Screenshots/Phase17/`, with the capture log). Each was looked at.

Where the capture changed the world to show something, the caption says so:
- moving between towns;
- the companions' temperaments;
- bolder road bands;
- a report of a farther group, when none had reached the player;
- one companion's trust raised;
- the rest of a fight let play out after its screenshot.

**No gameplay recordings were made.**

## 23. Known limitations and deferred work

- **Road fights** are staged on the near settlement's land, not a generated road scene.
- **Content:**
  - one provisional campaign;
  - two kinds of world story;
  - decisions made from the journal are supported but unused by the demonstration;
  - the second companion story needs a bond to someone living elsewhere, which generated worlds rarely make.
- **No hand-played session and no video.** The automated UI-driven tests stand in for them, as stated.
- **The main campaign and setting canon** await the owner's decisions.
- **Travel is on foot only.**
- **Kept for later milestones** (on the roadmap, not started, not reordered): the UI/UX overhaul; advanced character creation (with *The Tavern Table* as a reference only); mature content and immersion; transport; and the other existing workstreams.

## 24. Roadmap updates

- **Milestones:** Phase 16 is marked pushed and its closeout verified; Phase 17 is done (local); Phase 18 is not started.
- **Carry-forward tables:** a new Phase 16 table, and the Phase 15 table updated (second story, companion bonds).
- **Workstreams A–D** are recorded as kept for later.

## 25. Git and remote status

- **Committed locally on `feature/phase-0-1-foundation`.** One commit holds the Phase 17 implementation, tests, screenshots and documentation, on top of `7ab5163`; this section was filled in by a following docs-only commit. Its hash is in `git log` and in the hand-off message.

- **Not pushed.** The brief says "Do not push".
- **The working tree before Phase 17 was clean.** `ProjectSettings/GraphicsSettings.asset` was not touched, nothing was discarded, and Unity's `Assets/InitTestScene*` files and the local-only Bestiary were not staged.
- **Phase 18 was not started.**
