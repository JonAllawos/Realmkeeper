# Roadmap and milestone status

| Milestone | Status | Where |
|---|---|---|
| Phase 0–1: foundation, world generation, strategic map | Done | `Documentation/Architecture`, ADR-0001…0007 |
| Phase 2: settlement economies, trade, chronicle | Done (`fa521f2`), completed by the Phase 2 completion pass below | `Documentation/Systems/Economy.md` |
| ADR-0009 prerequisites and exploration vertical slice | Accepted as a vertical slice (`0c7f60d`) | `Documentation/Architecture/DualPerspective.md` |
| Pre-Phase-2 stabilization: session rebinding, save slots | Done in the Phase 2 completion commit | `DualPerspective.md`, `Systems/Persistence.md` |
| Phase 2 completion | Done; see the test results for what was run | `Systems/Economy.md`, `Testing/TestResults.md` |
| Phase 3: factions and political engine | Done (`5c89f8a`, playtest fixes `d82a01b`) | `Documentation/Systems/Politics.md` |
| Graphics pass | Done (`b0545b5`): CC0 modular kit, humanoid residents, street-level lighting | `Reports/GraphicsPass-Report.md` |
| Phase 4: NPC Intelligence Engine | Done (this milestone); see the test results for what was run | `Systems/NPCs.md`, `Systems/Relationships.md`, `Reports/Phase4-NPC-Intelligence.md` |
| Phase 5: Living World, Player Hub & Commerce | Done (`1f37bb6`) | `Systems/WorldTime.md`, `Systems/Player.md`, `Systems/Inventory.md`, `Systems/Equipment.md`, `Systems/Commerce.md`, `Reports/Phase5-Living-World.md` |
| Phase 6: Character Creation & Core RPG Foundation | Done (`f2682a2`) | `Systems/Characters.md`, `Systems/Species.md`, `Systems/Backgrounds.md`, `Systems/Classes.md`, `Systems/SkillsAndAbilities.md`, `Systems/CharacterCreation.md`, `Reports/Phase6-Character-Creation.md` |
| Phase 7: Character Progression & Core Rules Engine | Done (`a3cd9f5`) | `Systems/CharacterProgression.md`, `Systems/RulesEngine.md`, `Systems/Classes.md`, `Phase7/Phase7-Report.md` |
| Phase 8: Combat Foundation, Encounter Engine & Settlement Structures | Done (`2fe83eb`) | `Systems/Combat.md`, `Systems/SettlementStructures.md`, `Phase8/Phase8-Report.md`, ADR-0030…0036 |
| Phase 9: Spellcasting, Class Features & Combat Integration | Done (`27a5261`, pushed) | `Systems/Spellcasting.md`, `Systems/ClassFeatures.md`, `Systems/RestAndRecovery.md`, `Systems/SpellCatalogAudit.md`, `Phase9/Phase9-Report.md`, ADR-0037…0042 |
| Phase 10: Bestiary, NPC Character Systems, Encounter Intelligence & Living Settlement Generation | Done (`1707805`, pushed) | `Phase10/SettlementGeneration.md`, `Phase10/CreatureAndNpcSystems.md`, `Phase10/Testing.md`, `Phase10/Performance.md`, `Phase10/Limitations.md`, `Reports/Phase10-Bestiary-NPCs-Settlements.md`, ADR-0043…0049 |
| Phase 11: Inventory, Loot, Equipment, Crafting & Item Progression | Done (`7b2831b`, pushed with Phases 9–10 on the owner's instruction) | `Phase11/Items.md`, `Phase11/Testing.md`, `Phase11/Performance.md`, `Phase11/Limitations.md`, `Phase11/PreFlight.md`, `Systems/Inventory.md`, `Systems/Equipment.md`, `Reports/Phase11-Inventory-Items-Progression.md`, ADR-0050…0055 |
| Phase 12: Quest, Dialogue, Reputation & World Interaction Engine | Done (`1e593ae`, pushed on the owner's instruction after review) | `Systems/Quests.md`, `Systems/Dialogue.md`, `Systems/Reputation.md`, `Phase12/PreFlight.md`, `Phase12/Testing.md`, `Phase12/Limitations.md`, `Reports/Phase12-Quest-Dialogue-Reputation.md`, ADR-0056…0061 |
| Phase 13: Persistent Dungeons, Ruins, Interior Locations & Exploration Systems | Done (`cda9da0`, pushed on the owner's instruction after review) | `Systems/Locations.md`, `Systems/DungeonGeneration.md`, `Systems/Interiors.md`, `Systems/Interactions.md`, `Phase13/Testing.md`, `Phase13/Limitations.md`, `Reports/Phase13-Preflight-Integration-Audit.md`, `Reports/Phase13-Persistent-Locations-Exploration.md`, ADR-0062…0066 |
| Phase 14: Exploration Integrity, Living Locations & World Reactivity | Done (`242fde2`, pushed on the owner's instruction) | `Systems/Conditions.md`, `Systems/Crime.md`, `Systems/Combat.md` (sight and cover), `Systems/Locations.md`, `Systems/RestAndRecovery.md`, `Phase14/Testing.md`, `Phase14/Limitations.md`, `Reports/Phase14-Preflight-Integration-Audit.md`, `Reports/Phase14-Completion-Report.md`, ADR-0067…0071 |
| Combat overhaul: tactical movement, combat UI, action selection and visual feedback | Done (`08c222e`, pushed on the owner's instruction) | `Reports/Combat-Overhaul-Audit.md`, `Reports/Combat-Overhaul-Completion-Report.md`, `Systems/Combat.md` (the combat interface), ADR-0072 |
| Art assets: KayKit skeletons and character animations (CC0); Quaternius Bestiary local only | Done (`d975302`, report `4324084`, pushed) | `Reports/Art-Assets-KayKit-Bestiary-Report.md`, `Art/` credits |
| Phase 15: Companions, Parties, Relationships & Party Progression (with the Phase 15 art and animation addendum) | Done (`9a48d27`, pushed on the owner's instruction) | `Systems/Party.md`, `Reports/Phase15-Preflight-Audit.md`, `Reports/Phase15-Art-Animation-Report.md`, `Reports/Phase15-Completion-Report.md`, ADR-0073…0075 |
| Phase 16: World Events, Regional Travel, Dynamic Simulation & Persistent World Expansion | Done (`7ab5163`, pushed on the owner's instruction); closeout verified in Phase 17 (full EditMode 750/750) | `Systems/Travel.md`, `Systems/WorldEvents.md`, `Systems/WorldGeneration.md` (the connected world), `Reports/Phase16-Preflight-Audit.md`, `Reports/Phase16-Completion-Report.md`, ADR-0076…0081 |
| Phase 17: Phase 16 closeout, Campaign Engine, Narrative Systems & Persistent Storytelling | Done (`8f36025`; pushed later) | `Systems/Campaigns.md`, `Systems/Travel.md` (road fights, groups beyond sight), `Systems/Party.md`, `Systems/Dialogue.md`, `Reports/Phase17-Preflight-Audit.md`, `Reports/Phase16-Final-Verification.md`, `Reports/Phase17-Completion-Report.md`, ADR-0082…0087 |
| Phase 18: World Depth, Exploration, Living Content & Gameplay Expansion | Done (`713b9a0`; pushed later). Not hand-played: the owner playtest checklist is open | `Systems/Locations.md`, `Systems/Quests.md`, `Systems/WorldEvents.md`, `Systems/NPCs.md`, `Systems/Party.md`, `Systems/Dialogue.md`, `Systems/Campaigns.md`, `Systems/Persistence.md` (v20), `Reports/Phase18-Preflight-Audit.md`, `Reports/Phase18-Asset-Research.md`, `Reports/Phase18-Completion-Report.md`, `Testing/Phase18-Owner-Playtest-Checklist.md`, ADR-0088…0097 |
| Phase 19A: UI/UX asset research, licensing audit and visual direction | Done (`6c49d3a`) | `Research/Phase19-*.md` |
| Phase 19B: UI/UX, visual design and gameplay interface overhaul (workstream A, first pass) | Done (`6f225d8`, pushed at the owner's instruction). Full Unity EditMode 833/834 (timing bound raised at the owner's instruction, re-run green), PlayMode 121 passed, 0 failed. Partial screens listed in the report. Not hand-played | `Design/Phase19-UI-Design-System.md`, `Design/Phase19-UI-Architecture.md`, `Reports/Phase19-Preflight-Audit.md`, `Reports/Phase19-Completion-Report.md`, `Testing/Phase19-UI-Interaction-Audit.md`, `Testing/Phase19-Owner-Playtest-Checklist.md`, ADR-0098…0099 |
| Phase 20: Advanced Character Creation, Character Systems, Appearance, Progression & Character UI | Done (`6dce1a2`, pushed on the owner's instruction). Not hand-played: the owner checklist is open | `Reports/Phase20-Preflight-Audit.md`, `Reports/Phase20-Completion-Report.md`, `Reports/Phase20-Owner-Playtest-Checklist.md`, `Research/Phase20-Character-Assets-and-References.md`, `Systems/Species.md`, `Systems/CharacterCreation.md`, `Systems/Characters.md`, `Systems/NPCs.md`, `Systems/Persistence.md` (v21), ADR-0100…0102 |
| Phase 20A: Character Systems Completion, Mechanical Integration & Regression Closeout (with the free asset integration the owner asked for) | Done (`77240e7`, pushed on the owner's instruction). Not hand-played: the owner checklist is open | `Reports/Phase20A-Preflight-Audit.md`, `Reports/Phase20A-Completion-Report.md`, `Reports/Phase20A-Owner-Playtest-Checklist.md`, ADR-0103, ADR-0104, `Systems/Species.md`, `Systems/Combat.md`, `Systems/Spellcasting.md`, `Systems/NPCs.md`, `Systems/Persistence.md` (v22), `Research/Realmkeeper-Asset-*.md` |
| Phase 21: Environmental World, Living Settlements, Functional Shops, Crafting Workshops & Realistic Interiors | Done (`68b3063`; pushed 2026-11-13 at the owner's instruction). Not hand-played: the owner checklist is open. Weather is built inside Unity (owner decision). This row's earlier name, Mature World Content & Immersion, is now planned as Phase 33 | `Reports/Phase21-Preflight-Audit.md`, `Reports/Phase21-Completion-Report.md`, `Reports/Phase21-Owner-Playtest-Checklist.md`, `Systems/Commerce.md`, `Systems/Persistence.md` (v23), ADR-0105…0108 |
| Phase 22: Character Creation Integrity, Level 1 Starting Rules, Player-Driven Advancement & Targeted Phase 20A/21 Backfill | Done (`746ff3c`; pushed 2026-11-13 at the owner's instruction). Full Unity EditMode and PlayMode suites not run, at the owner's instruction; .NET and the targeted Unity tests run. Not hand-played: the owner checklist is open | `Reports/Phase22-Preflight-Audit.md`, `Reports/Phase22-Completion-Report.md`, `Reports/Phase22-Owner-Playtest-Checklist.md`, `Systems/CharacterProgression.md`, `Systems/CharacterCreation.md`, `Systems/Species.md`, `Systems/Persistence.md` (v24), ADR-0109 |
| Phase 23: Overland World Foundation, Expanded World Scale, Realm Architecture & Traversal | Done (`ea1422b`, then a follow-up round at the owner's request that fixed every weak spot of the first report with owned assets; committed locally per the brief, then pushed 2026-11-13 at the owner's instruction, up to `3ab344f`). Walkable country at 40 m a cell, 9 regions in a new game (236 km² of surface, 262 km² with the ancient prototype region), realms and gateways, loading transitions, textured ground, ground cover, solid trees, kit town silhouettes, entry from the map, 11 destinations a further region. Full Unity checkpoint run (EditMode 927/928, PlayMode 129/132 with 33 capture-only skips; the four stale tests fixed and re-run green); .NET 928/928. Not hand-played: the owner checklist is open | `Reports/Phase23-Preflight-Audit.md`, `Reports/Phase23-Completion-Report.md`, `Reports/Phase23-Owner-Playtest-Checklist.md`, `Systems/Overland.md`, `Systems/Persistence.md` (v25), ADR-0110, ADR-0111, ADR-0112 |
| Phase 24: Overland World Living Systems, Navigation Integration, Encounters, Hazards & Ground Weather | Done (`e0df4dd`; committed locally per the brief, then pushed 2026-11-14 at the owner's instruction). Landmarks (waystones, cairns, camps, steadings, watch posts, wrecks of real lost caravans), the simulation's travellers drawn on the roads (talk, help refugees), waypoints, the player's real position on the atlas, one travel state (step off a journey and rejoin it), encounters from real populations and bandit bands fought on the ground, ground weather (wet, mud, snow, frozen, flooded fords, sight), save v26, footsteps synced to the walk cycle and quieter. Full checkpoint: .NET 938/938, EditMode 938/938, PlayMode 132/133 with 33 capture-only skips; the one failure (the new Phase 24 test meeting a Phase 16 road threat on the journey's hour) fixed in the test and re-run green. Not hand-played: the owner checklist is open | `Systems/Phase24-Preflight-Audit.md`, `Reports/Phase24-Completion-Report.md`, `Systems/Phase24-Owner-Playtest-Checklist.md`, `Systems/Overland-Living-Systems.md`, `Systems/Persistence.md` (v26), ADR-0113, ADR-0114, ADR-0115 |
| Phases 25–34 (proposed) | **Planned, not started.** the open world (geography, traversal, navigation depth); optional fast travel; advancement depth; visual identity; interface overhaul; mature content; release readiness. Each needs its own brief | `Product/FutureRoadmap.md`, `Product/GameDesignBible.md` (owner design amendments, 2026-11-12) |

## Phase 17 carry-forward (status after Phase 18)

| Phase 17 limitation | Status |
|---|---|
| Two kinds of world story | **Done in Phase 18:** seven kinds, and every kind of world event has follow-up gameplay. The lost-caravan story could never appear in play (a lifecycle bug); this is fixed. |
| The second companion story needs a bond to someone elsewhere, which worlds rarely make | **Partly done in Phase 18:** two more stories (Going Home, Words at the Grave) start from bonds most worlds have. |
| No hand-played session and no video | **Still open.** Phase 18 also had no hand play and no recording, because no screen recorder is installed. The owner playtest checklist (`Testing/Phase18-Owner-Playtest-Checklist.md`) is the way to close it. |
| Road fights staged on the near settlement's land | Open |
| One provisional campaign; setting canon undecided | Open. *The Waystone Count* was deepened and is still provisional. |

## Phase 18 known limitations (carried forward)

| Limitation | Why |
|---|---|
| Not hand-played; no gameplay video | Claude drives Unity only through test runs, and no screen recorder is installed. See the owner checklist. |
| Dungeon walls and floors are still the built shapes | The KayKit kit dresses features only. Full kit walls would mean rebuilding the place builder (workstream D). |
| No sitting poses | The streets and interiors have no seats (workstreams A and D). |
| Companion formation, waiting and transitions not checked in play | Covered by code tests only. |
| Cairn Watch and its chain exist only where two factions exist | By design: the dispute needs two claimants. |

## Phase 16 carry-forward (status after Phase 17)

| Phase 16 limitation | Status |
|---|---|
| "Fight it out" on the atlas let the AI choose the player's actions | **Done in Phase 17** (ADR-0086). Road fights open in the combat scene on the ground at the road's nearer end, with the existing tactical panel. The party stays on the road and leaves the ground when the player says. The AI option stays only as an explicit, labelled choice. Remaining limit: the ground shown is the near settlement's land, not a generated stretch of road. |
| Groups on the roads drawn only in the player's region | **Done in Phase 17.** Beyond sight, a group shows where a report the player has puts it, marked as reported, with when and from whom, and no live numbers. |
| The full EditMode suite was not rerun after the Phase 14 timeout change | **Done in Phase 17:** 750/750 on `7ab5163`. The Phase 14 test still takes 189 s, now within its 10-minute limit. |
| Travel is on foot only | Open, deferred (mounts, ships and other transport are a later milestone). |
| No gameplay recordings | Still none: the batch-mode test runner cannot record video. Phase 17's evidence is captured screenshots with captions. |

## Workstreams kept for later milestones (not started; not reordered)

These were reaffirmed in the Phase 17 and Phase 18 briefs as required future milestones. None of them is part of Phase 18: Phase 18 made only small usability fixes and integrated assets. Their order is the owner's to set.

> **2026-11-12:** these workstreams are now placed in the proposed phase sequence in `Product/FutureRoadmap.md`:
> - A becomes Phase 32;
> - B becomes Phases 22, 30 and 31;
> - C becomes Phase 33;
> - D is spread across Phases 23–29 and 34.
>
> That document also adds the open-world travel and level 1 requirements. The letters below are kept as they were.

- **A. Complete UI/UX and visual design overhaul.** *(First pass delivered in Phase 19: design system, entry screens, settings, atlas, icons and restyle. Still open: inspector charts, event-feed filters, inventory grid and per-item icons, contextual exploration HUD, map framing, transitions; see the Phase 19 report, section 3.)* Main menus; world and campaign selection; navigation, tabs and panels; HUD and combat HUD; character sheets; inventory and equipment; spellbook; skill trees and progression; quest journal; dialogue; bestiary; faction interfaces; settlement and kingdom management; world atlas; settings and accessibility; transitions and animation; visual hierarchy, typography, colour system; icons and licensed downloadable assets. Study patterns (not assets or copied designs) from Baldur's Gate 3, Assassin's Creed, Far Cry, Kingdom Come: Deliverance, The Witcher, Dragon Age, Pathfinder, Crusader Kings, Red Dead Redemption and Skyrim.
- **B. Advanced character creation and character systems.** *(Delivered in Phase 20, within the licensed assets: lineages, identity, height and build, species skin tones, the creator and sheet, people by acquaintance. Still open: non-human anatomy (ears, horns, tails, scales), more hairstyles, subclasses beyond the SRD, eye colour; see the Phase 20 report.)* Expanded species and subspecies; classes and subclasses; backgrounds and origins; appearance (body types, faces, hair and facial hair, skin, distinguishing features, scars and tattoos); detailed previews; starting equipment; progression presentation; detailed character sheets; NPC creation and inspection; character identity and personality. Study *The Tavern Table* as a separate reference project; do not merge projects or copy its code without authorisation.
- **C. Mature world content and immersion.** Contextual, not gratuitous: profanity and dialogue realism; blood and injuries; violence and its consequences; mature themes; adult relationships and romance where appropriate; contextual non-sexual nudity; believable taverns, bathhouses, homes and medical settings; NPC behaviour that reflects circumstances; content settings controlling presentation, consistent across dialogue, appearance, environments and generated content. (The content-tag rules of Phase 12, ADR-0061, are the existing hook.)
- **D. Other existing work.** Expanded visual assets and animation; advanced exploration; settlement and civilisation development; audio and atmosphere; combat and encounter improvements; performance and production readiness; final integration, balancing and release preparation. Also: transport (mounts, ships), and further authored campaign content once the setting's canon is approved.

## Phase 15 carry-forward (status after Phase 16)

| Phase 15 limitation | Status |
|---|---|
| Party members are not drawn on the strategic map | **Done in Phase 16.** The atlas marks the player "You + n" and lists the party; the party travels and arrives together. |
| A waiting member in town reappears behind the player's arrival point | **Done in Phase 16.** Their spot is saved (`waitPose`) and they are drawn there. |
| No direct control of companions' turns | Open |
| One personal quest template | **Done in Phase 17.** A second story, "Unfinished Business", bound to a real person elsewhere, with three endings. |
| No companion banter, and no companion-to-companion relationships | **Done in Phase 17** (without random banter): companions react to decisions by their nature, say what they think when asked, and grow apart or closer through the existing person-to-person relations; quarrels can be talked round. |
| Followers in places stand with the player rather than walking room routes | Open |
| Charged items do not recharge for companions on a long rest | Open |
| The leader is presentation only | Open |

## Phase 2 completion: what it added

- **Trade:** routes can be suspended and resumed through a typed command, which is chronicled
  and saved. Schema v4 adds this, with a migration from v3.
- **Inspector:**
  - a "Why" line under prosperity and stability, listing the terms behind each target;
  - the food reserves trend, and whether a shortage is easing or worsening;
  - "Generalist" for settlements without a specialization;
  - a Suspend/Resume toggle on each route, and a count of active routes.
- **Map:** markers above settlements for food shortage, declining stability, strong production
  and trade on the road.

## Known limitations carried forward

- One region per world; three settlements in the default world. (Phase 16: a new game has six connected regions of three settlements each; an old world keeps one until the player discovers the lands beyond.)
- There are no births, deaths, migration of individuals, construction, crafting or caravans. (Crafting arrived in Phase 11, caravans and refugees in Phase 16.)
  These were out of scope.
- Performance was measured only at the default world size: a 30-day run takes 7–9 ms. No
  claim is made for larger worlds.
- One save slot is active at a time. There is no save browser.
- The map markers use `Universal Render Pipeline/Unlit` found by name. A player build must
  include that shader (editor runs are unaffected). Not yet verified in a build.
## Phase 3: what it added

- **Factions:** first-class factions of seven types. They have status, leaders (existing
  residents), influence explained by its terms, a treasury funded by real dues, aggregate
  membership, presence, and goals.
- **Governance and claims:** governance and administration of settlements, and territorial
  claims that never move borders by themselves.
- **Relationships:** multi-dimensional, with readable states, causes and agreements.
- **Simulation:** a weekly political tick inside the daily transaction, tied to stability,
  prosperity and trade.
- **Player actions:** nine typed political commands, each chronicled as the player's.
- **UI:** the Factions list and inspector, a Politics section in the settlement inspector, and
  an optional political map overlay. The exploration HUD names the governing faction.
- **Saves:** schema v5, with a deterministic upgrade of older saves.

Still out of scope: warfare, rebellion, diplomacy trees, NPC political AI, religion or crime
detail, multiple regions.

## Phase 5: what it added

- **A clock:** a minute of the day on top of the date. Each midnight crossed runs one simulated day.
  - The realm map keeps whole-day pacing.
  - Settlements run in minutes, with pause, speed and wait.
- **Light that follows the clock:** sun, golden hour, moon, sky, haze, lit lamps and windows.
- **Residents who walk** to where the world says they are each hour. Their poses match what they
  are doing, and they go indoors at home or asleep.
- **A full player character:** identity, renderable appearance, purse, hunger, an inventory of
  stacks with one owner each, and eight equipment slots shown on the body.
- **The Player Hub:** eight sections and a live 3D preview.
- **Shops kept by existing residents:**
  - stocked from the settlement's stores through the existing ledger, paid for by the keeper;
  - atomic buying and selling, with explained prices and no arbitrage;
  - reputation that grows once a day;
  - trades in the ledger and the chronicle;
  - keepers remember notable trades.
- **Walk-in Outfitters and Smithy** whose shelves show every unit of stock.
- **Knowledge-bound talk** about work, shops, goods, prices and factions.
- **Save schema v7**, with a once-only migration that matches a fresh world.

Known limitations are listed in `Reports/Phase5-Living-World.md` §15. The main ones:

| Area | Limitation |
|---|---|
| Combat | None |
| Props | Procedural |
| Clothing | Two outfit families |
| Walking | Street routes rather than a navmesh |
| Shop stock | Counts, not item instances |
| Loose items | None in the world |
| Map advances | Leave the time of day and hunger unchanged |

## Phase 6: what it added

- **One RPG character on the existing player** (ADR-0020): species, background, class, six
  ability scores, eighteen skills, six saves, hit points, resources and level 1. Every derived
  value is computed, never stored.
- **Twelve species, twelve backgrounds, twelve classes** as validated data with typed effects
  (ADR-0021), SRD 5.2.1 / 5.1 mechanics under CC BY 4.0 and Realmkeeper's own text (ADR-0022).
- **A nine-step character creator** in the exploration scene with a live 3D preview wearing the
  chosen kit, standard array or point buy, skill, option and equipment choices, and a review.
- **One creation transaction** that grants the class and background kit exactly once and keeps
  identity, belongings and location (ADR-0023).
- **A full character sheet** in the Player Hub (sheet, skills, features, proficiencies, identity).
- **Effects already in play:** species walking speed, goliath carrying capacity, merchant prices,
  species height.
- **Save schema v8:** older players load as Legacy, with nothing lost and nothing invented.

Main limitations (full list in `Reports/Phase6-Character-Creation.md`):

| Area | Limitation |
|---|---|
| Species art | Only humans have finished models; the other eleven are labelled placeholders (ADR-0024) |
| Levels | Level 1 only; no experience awards, levelling or subclasses |
| Spells | Spellcasting stats only; no spells |
| Combat | Features stored as typed data, not used |
| Armour class | Provisional (no armour categories yet) |
| Social features | Shown on the sheet, not yet read by dialogue, quests or reputation |

## Phase 7: what it added

- **Experience** through a keyed ledger (the same quest or discovery never pays twice), using the SRD 5.2.1
  thresholds as configuration. Experience makes a character eligible; it never levels them up.
- **Advancement from level 1 to 20** in one validated transaction per command (one or several levels), with a
  chronicle entry and an advancement record per level.
- **All twelve classes progress to 20** with SRD 5.2.1 features, scaling columns, resources, spell slots and
  subclass levels. One SRD subclass per class; SRD feats (Ability Score Improvement, Grappler, origin feats,
  Fighting Style feats, Epic Boons); invocations; Metamagic.
- **A shared rules engine:** conditions, stacking, advantage and disadvantage, initiative, passive scores,
  senses and movement, prerequisites, feature replacement, a breakdown for every value, and integration notes
  for what waits for combat, magic or rests.
- **Resource and hit-point commands:** spend, restore, recover (the rest hook), damage, healing, temporary hit
  points, hit dice.
- **A level-up window** in the exploration scene, the HUD's "Level up (L)", and the Hub's experience bar,
  level-up banner and Progression panel.
- **Save schema v9:** Phase 5 and Phase 6 saves load. Druids and warlocks from Phase 6 make their new level-1
  choice at their next level-up.

Main limitations (full list in `Phase7/Phase7-Report.md`):

| Area | Limitation |
|---|---|
| Experience sources | No gameplay system awards experience yet (quests and combat come later); the command is ready |
| Subclasses | One per class (the SRD's); others deferred |
| Combat, spells, rests | Features are acquired and shown, with what they wait for; nothing resolves attacks, spells or rest time |
| Invocations | Three that need damage cantrips, and Lessons of the First Ones, deferred |
| Multiclassing | Not supported |
| NPCs and companions | The services take any sheet, but no NPC holds one yet |
| Species art | Unchanged placeholders (Phase 6) |

## Phase 8: what it added

- **A combat engine in the domain** (ADR-0030), driven by commands:
  - encounters with any number of sides and reinforcements;
  - deterministic initiative and dice (ADR-0032);
  - the action economy;
  - a 5-ft grid with difficult terrain and opportunity attacks;
  - SRD attacks with advantage and disadvantage, natural 1 and 20, critical hits;
  - grapple and shove;
  - damage with resistance, vulnerability and temporary hit points;
  - all 15 conditions;
  - death saves, stabilizing and death (ADR-0034).
- **SRD armour and weapons replace the provisional armour class** (ADR-0033):
  - armour categories, Dex caps, shields;
  - training penalties and Strength requirements;
  - the AC breakdown;
  - weapon profiles on the existing items, plus chain mail.
- **Participants.** Residents fight as themselves, never copied (ADR-0031). Their deaths go through
  the shared NPC death routine.
- **A deterministic AI** that proposes; the engine decides.
- **A practice encounter** from the settlement HUD (F): two bandits, with the local guard on the
  player's side. It uses the encounter panel (initiative, a tactical grid, actions with reasons,
  results, inspector, log) and placeholder ground markers. Time stands still during the fight.
- **The chronicle** gets the start, the end, people downed and deaths only (ADR-0036).
- **Settlement structures** (ADR-0035):
  - every existing place is a functional structure, housing its shop;
  - informational and decorative structures are added by size;
  - shown in the settlement inspector and as signs in the street.
- **Save schema v10:** Phase 5, 6 and 7 saves load. Older worlds gain their structures, and no
  encounter.
- **Shipped credits:** `CREDITS.md`, the Hub's Info section and the README (the SRD CC BY 4.0
  attribution and the CC0 art).

Main limitations (full list in `Phase8/Phase8-Report.md`):

| Area | Limitation |
|---|---|
| Encounters | The labelled practice fight is the only way to start one in play; no encounter generator |
| Class features | Second Wind and Cunning Action only (the rest came in Phase 9) |
| Spells, concentration | Done in Phase 9 |
| Presentation | Placeholder ground markers and stand-in figures; no creature models or animations; the grid lives in the panel |
| NPC statistics | Residents use SRD Guard or Commoner profiles until NPCs have character sheets |
| Rewards, recovery | No experience from combat; no resurrection or medical system |
| Structures | Informational ones simulate nothing yet; no construction or upkeep |

## Phase 9: what it added

- **One spell engine for every caster** (ADR-0037).
  - **Catalog:** 47 SRD 5.2.1 spells, cantrips to level 5, each with an honest status: 27
    functional, 10 partial, 4 data-only and 6 deferred.
  - **Targeting:** creatures, points, self, and sphere, cube, cone, line and cylinder areas on the
    grid.
  - **Resolution:** spell attacks and saves through the shared rolls; upcasting and cantrip scaling;
    one levelled slot per turn.
  - **Effects:** a stored effect model with concentration, repeat saves and turn-based expiry
    (ADR-0039).
- **Spell state** (ADR-0038).
  - **Picks:** known spells, cantrips and wizard spellbooks are ordinary character choices, offered
    at creation and level-up and fillable later from the Hub.
  - **Preparation:** after a long rest; paladins and rangers swap one.
  - **Pact Magic:** kept distinct from spell slots.
  - **Rules version 8.**
- **Class features in play for all twelve classes** (ADR-0041): 38 combat features through one
  activation model, among them Rage, Sneak Attack, Action Surge, Channel Divinity, Wild Shape (one
  form), Martial Arts, Lay on Hands, Smite, Font of Magic, Metamagic and the Evoker features.
- **NPC casters** (ADR-0042): the SRD 5.1 Acolyte and Cult Fanatic cast through the same engine; a
  deterministic caster AI; a "Practice vs caster (G)" fight.
- **Rest and recovery** (ADR-0040):
  - short and long rests pass world time;
  - hit-die spending, and explicit recovery per resource;
  - Arcane Recovery;
  - one long rest per 24 hours;
  - idempotent.
- **UI:**
  - **Hub, Spells & rest:** the spellbook with search, filters, details, preparation, open picks,
    slots, rests and features.
  - **Encounter panel:** Spells and Class features sections; a cast composer with slot level,
    targets, metamagic and grid aiming with area preview; effects and concentration on the rows and
    in the inspector.
- **Save schema v11:** Phase 5–8 saves load.

Main limitations (full list in `Phase9/Phase9-Report.md`):

| Area | Limitation |
|---|---|
| Reactions | Automatic (ADR-0033); Shield, Hellish Rebuke, Counterspell and reaction features wait for a prompt |
| Durations | Effects end with the fight; out-of-combat durations (Mage Armour, Aid) are not tracked |
| Spells | Levels 6–9 have no catalog entries yet; summons, moving auras, light, stealth and terrain effects wait |
| Wild Shape | One form (wolf) |
| Metamagic | Four of ten options not simulated |
| NPC casters | Two SRD 5.1 stat blocks; slots refill per encounter |

## Phase 10: what it added

- **Named people with character records** (ADR-0043).
  - Fighters, healers and wardens hold full sheets, built by the player's rules.
  - Commands promote a person, award keyed experience and advance levels.
  - People with sheets fight as themselves.
  - The town and hamlet gain the posts their places call for (15 → 19 named people).
- **A bestiary and aggregate wildlife** (ADR-0044).
  - 20 SRD 5.1 creatures and 5 labelled originals, with traits and behaviours applied in combat.
  - Weekly populations, predation, hunting and herds.
  - Named individuals drawn into fights, whose fates persist.
- **Reactions asked, not taken** (ADR-0045). Suspend and replay. The encounter panel's prompt offers Accept or
  Decline, and the answer is atomic and saved while it waits.
- **Encounters from the world** (ADR-0046). Outings to the outskirts, the road or the farms, planned from the
  populations in range, checked before they start, and settled once.
- **Settlement identity and terrain-first plans** (ADR-0047, ADR-0048, ADR-0049).
  - Ground, water and bridges, roads, districts, buildings on buildable ground, farms and herds, landmarks and
    walls.
  - The plan is persisted and never regenerated.
  - The walkable settlement, the strategic map and the inspector all read it.
  - People walk its road graph and stand on its ground.
- **Schema v12** with migration; older saves gain the Phase 10 data once on load.

## Phase 11: what it added

- **An item model with instances** (ADR-0050). 114 items, 66 of them SRD 5.2.1. Each instance carries its own condition, make, charges, attunement, curse binding and provenance. One factory makes every instance.
- **One holder per instance, atomic transfers, a weight-limited pack** (ADR-0051).
  - The player's chest in every settlement, a cache filled once, loot piles.
  - Take, put, take all and coin.
  - SRD carrying capacity: over it Speed is 5 ft; past twice it nothing more can be taken.
- **Equipment and magic through the rules** (ADR-0052).
  - Condition that wears in play and changes attack and AC.
  - 11 magic items: +1 weapons, armour and shield, rings and cloaks of protection, gauntlets, goggles, boots, a wand with charges, and a cursed axe.
  - Attunement (three, an hour each).
  - Ammunition spent and half recovered.
  - Rings on either hand, and off-hand Light weapons.
- **Loot** (ADR-0053). 15 tables and carried gear, seeded and filled once into a container per fight, bounded by rarity. It opens from the combat panel.
- **Crafting, gathering, repair and attunement as work** (ADR-0054). 11 recipes; materials held; a tool check rolled when the work starts; world time passes. Gathering sites come from the settlement economy and recover weekly. An armourer mends for a price.
- **Merchants.** Offers count condition and make. Distinct items keep their identity as a shop's particular goods.
- **Screens** (ADR-0055).
  - Inventory search, filters, sorting, inspection and rules-engine comparison.
  - Eleven equipment slots.
  - Crafting & work, and Storage.
  - The chest and cache in the street.
  - The shop's particular goods and mending.
- **Schema v13** with migration; v12 saves gain the item world once on load.
